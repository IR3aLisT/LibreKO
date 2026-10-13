using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;
using LibreKO.Game.Protocol.Writers;

namespace LibreKO.Game.Protocol;

public interface IPartyBbsPacketCoordinator
{
    Task HandleAsync(IClient client, Packet packet);
}

public class PartyBbsPacketCoordinator(SessionManager sessionManager, IPartyPacketCoordinator parties) : IPartyBbsPacketCoordinator
{
    private const byte PartyBbsRegister = 0x01;
    private const byte PartyBbsDelete = 0x02;
    private const byte PartyBbsNeeded = 0x03;
    private const byte PartyBbsWanted = 0x04;
    private const byte PartyBbsList = 0x0B;
    private const int MaxBbsPage = 10;
    private const int ListRequestBytes = sizeof(short) + 1 + sizeof(short) + 1;

    private readonly record struct BoardFilter(byte State, short Zone, byte Level)
    {
        public static readonly BoardFilter None = new(FullMoonRules.AnyState, FullMoonRules.AnyZone, FullMoonRules.AnyLevel);
    }

    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 2)
            return;

        var mode = packet.ReadByte();
        if (mode != PartyBbsPacketWriter.ModeNormal && mode != PartyBbsPacketWriter.ModeRestricted)
            return;

        var subOpcode = packet.ReadByte();
        switch (subOpcode)
        {
            case PartyBbsRegister:
                await RegisterAsync(session, mode);
                break;

            case PartyBbsDelete:
                await DeleteAsync(session, mode);
                break;

            case PartyBbsNeeded:
                if (packet.RemainingBytes >= 2)
                    await SendNeededPageAsync(session, mode, packet.ReadShort(), BoardFilter.None);
                break;

            case PartyBbsList:
                if (packet.RemainingBytes >= ListRequestBytes)
                {
                    var page = packet.ReadShort();
                    var filter = new BoardFilter(packet.ReadByte(), packet.ReadShort(), packet.ReadByte());
                    await SendNeededPageAsync(session, mode, page, mode == FullMoonRules.FullMoonBoard ? BoardFilter.None : filter);
                }
                break;

            case PartyBbsWanted:
                await UpdateWantedAsync(session, mode, packet);
                break;

            case PartyBbsPacketWriter.SubInvite:
                if (packet.RemainingBytes >= sizeof(short))
                    await parties.BoardInviteAsync(session, packet.ReadString(), mode);
                break;

            case PartyBbsPacketWriter.SubInviteAccept:
                if (packet.RemainingBytes >= sizeof(short))
                    await parties.BoardAcceptAsync(session, packet.ReadString(), mode);
                break;
        }
    }

    private async Task RegisterAsync(UserSession session, byte mode)
    {
        if (!session.IsInParty)
        {
            session.PartyBoardType = mode;
            await SetSeekingAsync(session, true);
        }

        var packet = PartyBbsPacketWriter.Result(
            mode,
            PartyBbsRegister,
            session.IsInParty ? PartyBbsPacketWriter.Failed : PartyBbsPacketWriter.Succeeded);

        await session.Client.SendPacket(packet);
    }

    private async Task SetSeekingAsync(UserSession session, bool seeking)
    {
        if (session.SeekingParty == seeking)
            return;
        session.SeekingParty = seeking;
        await sessionManager.Regions.SendToRegion(
            session,
            MovementPacketWriter.StateChange(
                session.CharacterId,
                (byte)StateChangeType.NeedParty,
                seeking ? UserInfoPacketWriter.SeekingParty : UserInfoPacketWriter.NotSeekingParty),
            excludeSender: false);
    }

    private async Task DeleteAsync(UserSession session, byte mode)
    {
        await SetSeekingAsync(session, false);
        session.PartyBbsMessage = string.Empty;
        session.WantedClass = 0;
        session.PartyBoardType = FullMoonRules.NormalBoard;

        var packet = PartyBbsPacketWriter.Result(
            mode, PartyBbsDelete, PartyBbsPacketWriter.Succeeded);
        await session.Client.SendPacket(packet);
    }

    private byte BoardOf(UserSession candidate) =>
        candidate.IsInParty
            ? sessionManager.Parties.GetParty(candidate.PartyIndex)?.BoardType ?? FullMoonRules.NormalBoard
            : candidate.PartyBoardType;

    private async Task SendNeededPageAsync(UserSession session, byte mode, short pageIndex, BoardFilter filter)
    {
        var sanitizedPageIndex = (short)Math.Max(pageIndex, (short)0);
        var entries = sessionManager
            .GetAll()
            .Where(candidate =>
                candidate.Nation == session.Nation
                && BoardOf(candidate) == mode
                && ((candidate.SeekingParty && !candidate.IsInParty)
                    || (candidate.IsPartyLeader && candidate.WantedClass > 0)))
            .Select(candidate => (
                Player: candidate,
                Type: (byte)(candidate.IsPartyLeader && candidate.WantedClass > 0 ? 3 : 2)))
            .Where(item => FullMoonRules.PassesFilter(filter.State, filter.Zone, filter.Level,
                item.Type == PartyBbsPacketWriter.EntryWantedParty, item.Player.ZoneId, item.Player.Level))
            .ToList();

        var totalCount = entries.Count;
        var totalPages = (totalCount + MaxBbsPage - 1) / MaxBbsPage;
        var startIndex = sanitizedPageIndex * MaxBbsPage;
        var page = entries.Skip(startIndex).Take(MaxBbsPage).ToList();

        var boardEntries = page.Select(item => new PartyBbsPacketWriter.BoardEntry(
            item.Player.Name,
            item.Type == PartyBbsPacketWriter.EntryWantedParty
                ? item.Player.WantedClass
                : item.Player.Class,
            item.Player.Level,
            item.Type,
            item.Player.PartyBbsMessage,
            item.Player.ZoneId,
            (byte)(item.Type == PartyBbsPacketWriter.EntryWantedParty && item.Player.IsInParty
                ? sessionManager.Parties.GetParty(item.Player.PartyIndex)?.MemberCount ?? 0
                : 0),
            (byte)item.Player.Nation)).ToList();

        var packet = PartyBbsPacketWriter.Page(
            mode, PartyBbsList,
            sanitizedPageIndex, (short)totalPages, boardEntries);
        await session.Client.SendPacket(packet);
    }

    private async Task UpdateWantedAsync(UserSession session, byte mode, Packet packet)
    {
        if (!session.IsPartyLeader)
        {
            var fail = PartyBbsPacketWriter.Result(
                mode, PartyBbsWanted, PartyBbsPacketWriter.Failed);
            await session.Client.SendPacket(fail);
            return;
        }

        if (packet.RemainingBytes < 4)
            return;

        session.WantedClass = packet.ReadShort();
        var pageIndex = packet.ReadShort();

        if (packet.RemainingBytes >= 1)
        {
            var messageLength = packet.ReadByte();
            if (messageLength > 0 && packet.RemainingBytes >= messageLength)
            {
                var bytes = new byte[messageLength];
                for (var i = 0; i < messageLength; i++)
                    bytes[i] = packet.ReadByte();
                session.PartyBbsMessage = System.Text.Encoding.UTF8.GetString(bytes);
            }
        }

        await SendNeededPageAsync(session, mode, pageIndex, BoardFilter.None);
    }
}
