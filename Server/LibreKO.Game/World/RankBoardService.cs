using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Services;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using Microsoft.Extensions.DependencyInjection;

namespace LibreKO.Game.World;

public interface IRankBoardService
{
    Task SendAsync(UserSession session);
}

public sealed class RankBoardService(
    SessionManager sessionManager,
    IKingSystemRuntimeService kings,
    IServiceScopeFactory scopeFactory) : IRankBoardService
{
    public async Task SendAsync(UserSession session)
    {
        using var scope = scopeFactory.CreateScope();
        var characters = scope.ServiceProvider.GetRequiredService<ICharacterRepository>();
        var entries = new List<RankBoardEntry>();

        if (kings.GetKingData(session.Nation)?.KingName is { Length: > 0 } king)
            entries.Add(WithClan(RankBoardPacketWriter.KingSlot, king, await ClanOfAsync(king, characters)));

        var clans = sessionManager.Knights.GetAll()
            .Where(clan => clan.Nation == (byte)session.Nation && clan.Ranking is >= 1 and <= RankBoardPacketWriter.Rows)
            .OrderBy(clan => clan.Ranking)
            .ToList();
        for (var i = 0; i < clans.Count; i++)
            entries.Add(WithClan((ushort)(RankBoardPacketWriter.FirstClanSlot + i), clans[i].Chief, clans[i]));

        var players = await characters.GetPlayerIdsByLoyalty(session.Nation, RankBoardPacketWriter.Rows, monthly: true);
        for (var i = 0; i < players.Count; i++)
        {
            var (name, clanId) = await PlayerAsync(players[i], characters);
            if (name.Length == 0) continue;
            entries.Add(WithClan((ushort)(RankBoardPacketWriter.FirstPlayerSlot + i), name, sessionManager.Knights.GetClan(clanId)));
        }

        await session.Client.SendPacket(RankBoardPacketWriter.Board(entries));
    }

    private async Task<KnightsEntity?> ClanOfAsync(string name, ICharacterRepository characters)
    {
        var clanId = sessionManager.GetByName(name)?.KnightsId
                     ?? (await characters.GetByName(name))?.KnightsId ?? 0;
        return sessionManager.Knights.GetClan(clanId);
    }

    private async Task<(string Name, int ClanId)> PlayerAsync(int characterId, ICharacterRepository characters)
    {
        if (sessionManager.GetByCharacterId(characterId) is { } online)
            return (online.Name, online.KnightsId);
        return await characters.GetById(characterId) is { } stored ? (stored.Name, stored.KnightsId) : (string.Empty, 0);
    }

    private static RankBoardEntry WithClan(ushort slot, string name, KnightsEntity? clan) => clan == null
        ? new RankBoardEntry(slot, name, RankBoardEntry.NoClan, 0, string.Empty, 0, 0)
        : new RankBoardEntry(slot, name, clan.Id, clan.MarkVersion, clan.Name, clan.Flag, clan.Grade);
}
