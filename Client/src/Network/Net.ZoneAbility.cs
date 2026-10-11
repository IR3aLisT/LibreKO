using System;
using System.Collections.Generic;

namespace LibreKO.Network;

public partial class Net
{
    public ZoneAbilityInfo CurrentZoneAbility { get; private set; } = new() { ZoneType = 0, Tariff = 10 };

    public event Action<ZoneAbilityInfo>? ZoneAbilityEvent;

    public const byte SubZoneAbilityInfo = 1, SubZoneAbilityUserStatus = 2;
    public const byte UserStatusCured = 0, UserStatusInflicted = 1;

    private readonly HashSet<byte> _ownStatus = new();

    public IReadOnlyCollection<byte> OwnStatus => _ownStatus;

    public event Action<byte, bool>? OwnStatusEvent;

    private void HandleZoneAbility(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();
        if (sub == SubZoneAbilityUserStatus)
        {
            HandleOwnStatus(p);
            return;
        }
        if (sub != SubZoneAbilityInfo || p.RemainingBytes < 5) return;
        bool canTrade = p.ReadByte() != 0;
        byte zoneType = p.ReadByte();
        bool canTalk = p.ReadByte() != 0;
        int tariff = p.ReadUShort();

        CurrentZoneAbility = new ZoneAbilityInfo
        {
            ZoneType = zoneType,
            Tariff = tariff,
            CanTrade = canTrade,
            CanTalk = canTalk,
        };
        foreach (var known in _known.Values)
            known.Attackable = known.IsNpc ? IsHostileNpc(known) : IsHostilePlayer(known);
        ZoneAbilityEvent?.Invoke(CurrentZoneAbility);
    }

    private void HandleOwnStatus(Packet p)
    {
        if (p.RemainingBytes < 2) return;
        byte type = p.ReadByte();
        bool inflicted = p.ReadByte() == UserStatusInflicted;
        if (inflicted) _ownStatus.Add(type); else _ownStatus.Remove(type);
        OwnStatusEvent?.Invoke(type, inflicted);
    }

    public bool IsHostilePlayer(EntitySnapshot e) =>
        !e.IsNpc && e.Id != MyCharId
        && (IsHostilePlayer(e.Nation, e.KnightsId, e.AllianceId) || AtClanWarWith(e));

    public bool IsHostilePlayer(int nation, int clan, int alliance) =>
        Domain.SiegeWar.At(LastEnter.Zone, SiegeWar)
            ? Domain.SiegeWar.Hostile(MyClan.InClan ? MyClan.ClanId : 0, MyClan.InClan ? MyClan.AllianceId : 0, clan, alliance, SiegeWar)
            : CurrentZoneAbility.IsHostilePlayer(Nation, nation);

    public int SiegeChatMask(int senderId, bool senderIsGm)
    {
        if (senderIsGm || senderId == MyCharId || !Domain.SiegeWar.At(LastEnter.Zone, SiegeWar)) return 0;
        _known.TryGetValue(senderId, out var sender);
        return Domain.SiegeWar.TargetMaskText(IsGm, MyClan.InClan ? MyClan.ClanId : 0, MyClan.InClan ? MyClan.AllianceId : 0,
            sender?.KnightsId ?? 0, sender?.AllianceId ?? 0, SiegeWar);
    }

    internal void RefreshKnownHostility()
    {
        foreach (var known in _known.Values)
            known.Attackable = known.IsNpc ? IsHostileNpc(known) : IsHostilePlayer(known);
    }

    public bool IsHostileNpc(EntitySnapshot e) =>
        NpcHostility.IsHostile(e, Nation, CurrentZoneAbility.NpcsAreTargets);
}
