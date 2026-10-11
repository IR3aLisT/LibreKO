using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;

namespace LibreKO.Game.Protocol;

public interface IRankerMessagePacketCoordinator
{
    Task HandleAsync(IClient client, Packet packet);
}

public sealed class RankerMessagePacketCoordinator(
    SessionManager sessionManager,
    IRankerStatueService statues,
    IServiceScopeFactory scopeFactory) : IRankerMessagePacketCoordinator
{
    private const int SecondsPerMinute = 60;

    public async Task HandleAsync(IClient client, Packet packet)
    {
        var session = sessionManager.GetByClientId(client.Id);
        if (session == null || packet.RemainingBytes < 2)
            return;

        var sub = (RankerMessageSub)packet.ReadByte();
        var npcType = packet.ReadByte();
        var reply = sub switch
        {
            RankerMessageSub.View => await ViewAsync(npcType),
            RankerMessageSub.Write => await WriteAsync(session, npcType,
                packet.RemainingBytes >= sizeof(short) ? packet.ReadString() : string.Empty),
            RankerMessageSub.Redress => await RedressAsync(session, npcType),
            _ => null,
        };
        if (reply != null)
            await session.Client.SendPacket(reply);
    }

    private async Task<Packet> ViewAsync(byte npcType)
    {
        if (!NpcSpawnPacketWriter.IsRankerStatue(npcType))
            return RankerMessagePacketWriter.Result(RankerMessageSub.View, RankerMessageResult.ViewInvalidType);
        var ranker = statues.RankerOf(npcType);
        var name = statues.NameOn(npcType);
        if (ranker == 0 || name == null)
            return RankerMessagePacketWriter.Result(RankerMessageSub.View, RankerMessageResult.ViewUnderRevision);

        using var scope = scopeFactory.CreateScope();
        var note = await scope.ServiceProvider.GetRequiredService<IRankerStatueNoteRepository>().GetByCharacterId(ranker);
        return RankerMessagePacketWriter.Paper(name, note?.Message ?? string.Empty);
    }

    private async Task<Packet> WriteAsync(UserSession session, byte npcType, string message)
    {
        var ranker = NpcSpawnPacketWriter.IsRankerStatue(npcType) ? statues.RankerOf(npcType) : 0;
        if (ranker == 0)
            return RankerMessagePacketWriter.Result(RankerMessageSub.Write, RankerMessageResult.WriteNoData);
        if (ranker != session.CharacterId)
            return RankerMessagePacketWriter.Result(RankerMessageSub.Write, RankerMessageResult.WriteNotRanker);
        if (message.Length == 0 || message.EnumerateRunes().Count() > RankerStatueNote.MessageMaxLength)
            return RankerMessagePacketWriter.Result(RankerMessageSub.Write, RankerMessageResult.WriteLengthError);

        using var scope = scopeFactory.CreateScope();
        var notes = scope.ServiceProvider.GetRequiredService<IRankerStatueNoteRepository>();
        var note = await notes.GetByCharacterId(ranker) ?? new RankerStatueNote { CharacterId = ranker };
        note.Message = message;
        await notes.SaveAsync(note);
        return RankerMessagePacketWriter.Result(RankerMessageSub.Write, RankerMessageResult.Done);
    }

    private async Task<Packet> RedressAsync(UserSession session, byte npcType)
    {
        if (!NpcSpawnPacketWriter.IsRankerStatue(npcType))
            return RankerMessagePacketWriter.Result(RankerMessageSub.Redress, RankerMessageResult.RedressInvalidType);
        if (statues.RankerOf(npcType) != session.CharacterId)
            return RankerMessagePacketWriter.Result(RankerMessageSub.Redress, RankerMessageResult.RedressNotRanker);

        using var scope = scopeFactory.CreateScope();
        var notes = scope.ServiceProvider.GetRequiredService<IRankerStatueNoteRepository>();
        var note = await notes.GetByCharacterId(session.CharacterId) ?? new RankerStatueNote { CharacterId = session.CharacterId };
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (note.LookChangedAt > 0 && (now - note.LookChangedAt) / SecondsPerMinute < GameConstants.DailyOperationWindowMinutes)
            return RankerMessagePacketWriter.Result(RankerMessageSub.Redress, RankerMessageResult.RedressOncePerDay);

        await statues.RedressAsync(npcType, session);
        note.LookChangedAt = now;
        await notes.SaveAsync(note);
        return RankerMessagePacketWriter.Result(RankerMessageSub.Redress, RankerMessageResult.Done);
    }
}
