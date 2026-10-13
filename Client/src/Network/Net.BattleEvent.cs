using System;
using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    private const byte BattleEventOpenSub   = 1;
    private const byte BattleEventResultSub = 3;
    private const byte RankBoardSub = 13;

    public const byte BattleZoneOpen      = 0x00;
    public const byte BattleZoneClose     = 0x01;
    public const byte BattleZoneSnowOpen  = 0x09;

    public const byte BattleDeclareWinner = 0x02;
    public const byte BattleDeclareLoser  = 0x03;

    public const byte BattleStateNone   = 0;
    public const byte BattleStateNation = 1;
    public const byte BattleStateSnow   = 2;

    public event Action<int, int>? BattleZoneToggleEvent;

    public event Action<int, int, int>? BattleStatusEvent;

    public event Action<int, int>? BattleResultEvent;

    public event Action<int, int, int>? BattleScoreEvent;
    public event Action<MapEventReply>? MapEventEvent;
    public event Action<List<ClanMatchFighter>>? ClanMatchRosterEvent;
    public event Action<ClanMatchVitals>? ClanMatchVitalsEvent;
    public event Action<int>? ClanMatchResultEvent;
    public byte[] MonumentOwners { get; private set; } = Array.Empty<byte>();
    public long NereidCont { get; private set; }
    public long NereidContGain { get; private set; }

    public event Action<List<RankBoardRow>>? RankBoardEvent;

    private void HandleBattleEvent(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();
        switch (sub)
        {
            case ClanMatchReply.BattleSub:
                HandleClanMatch(p);
                break;
            case BattleEventOpenSub:
            {
                if (p.RemainingBytes < 2) return;
                int a = p.ReadByte();
                int zone = p.ReadByte();
                if (p.RemainingBytes >= 4)
                {
                    int remaining = p.ReadInt();
                    BattleStatusEvent?.Invoke(a, zone, remaining);
                }
                else
                {
                    BattleZoneToggleEvent?.Invoke(a, zone);
                }
                break;
            }
            case RankBoardSub:
                RankBoardEvent?.Invoke(ReadRankBoard(p));
                break;
            case BattleEventResultSub:
            {
                if (p.RemainingBytes < 2) return;
                int declareType = p.ReadByte();
                int nation = p.ReadByte();
                BattleResultEvent?.Invoke(declareType, nation);
                break;
            }
        }
    }

    private static List<RankBoardRow> ReadRankBoard(Packet p)
    {
        var rows = new List<RankBoardRow>();
        int count = p.RemainingBytes >= 1 ? p.ReadByte() : 0;
        for (int i = 0; i < count && p.RemainingBytes >= 2; i++)
        {
            int slot = p.ReadUShort();
            string name = p.ReadString();
            p.ReadUShort();
            int clanId = p.ReadShort();
            int markVersion = p.ReadShort();
            string clanName = p.ReadString();
            int tail = p.ReadShort();
            if (RankBoard.Row(slot, name, clanId, markVersion, clanName, tail) is { } row) rows.Add(row);
        }
        return rows;
    }

    private void HandleClanMatch(Packet p)
    {
        if (p.RemainingBytes < 1 || !Domain.ClanMatch.ShowsPlate(LastEnter.Zone)) return;
        switch (p.ReadByte())
        {
            case ClanMatchReply.ResultSub when p.RemainingBytes >= 2:
                ClanMatchResultEvent?.Invoke(p.ReadShort());
                break;
            case ClanMatchReply.RosterSub:
                ClanMatchRosterEvent?.Invoke(ClanMatchReply.ReadRoster(p));
                break;
            case ClanMatchReply.VitalsSub:
                if (ClanMatchReply.ReadVitals(p) is { } vitals && vitals.Id >= 0) ClanMatchVitalsEvent?.Invoke(vitals);
                break;
        }
    }

    private void HandleMapEvent(Packet p)
    {
        if (MapEventReply.Read(p) is not { } reply) return;
        switch (reply.Kind)
        {
            case MapEventKind.Owners:
                MonumentOwners = reply.Owners;
                break;
            case MapEventKind.Captured:
                if (reply.Index >= MonumentOwners.Length) return;
                MonumentOwners[reply.Index] = (byte)reply.Nation;
                break;
            case MapEventKind.Points:
                BattleScoreEvent?.Invoke(MapEventReply.SubPoints, (int)reply.A, (int)reply.B);
                break;
            case MapEventKind.Cont:
                NereidContGain = reply.A - NereidCont;
                NereidCont = reply.A;
                break;
        }
        MapEventEvent?.Invoke(reply);
    }

    public void SendBattleStatusRequest()
    {
        var p = new Packet(GameOpcodes.GS_BATTLE_EVENT);
        p.WriteByte(BattleEventOpenSub);
        _conn.Send(p);
    }

    public void SendMapEvent(byte eventType = MapEventReply.SubPoints)
    {
        var p = new Packet(GameOpcodes.GS_MAP_EVENT);
        p.WriteByte(eventType);
        _conn.Send(p);
    }
}
