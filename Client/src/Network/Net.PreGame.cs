using System;
using System.Collections.Generic;

namespace LibreKO.Network;

public partial class Net
{
    private const byte CharListSub = 1;
    private const byte KingFlag = 1;
    private const byte CharListReady = 1;

    private int _selectRetries;
    private int _pendingZone;

    private const byte ServerStateClientOutOfDate = 3;
    public const string ClientOutOfDateText = "Your client is out of date. Run the launcher to update it.";

    private void HandleVersionCheck(Packet p)
    {
        p.ReadByte();
        ServerVersion = p.ReadUShort();
        if (p.RemainingBytes >= 2)
        {
            p.ReadByte();
            if (p.ReadByte() == ServerStateClientOutOfDate)
            {
                _autoLoginPending = false;
                ErrorEvent?.Invoke(ClientOutOfDateText);
                return;
            }
        }

        VersionEvent?.Invoke(ServerVersion);
        if (_autoLoginPending)
        {
            _autoLoginPending = false;
            Login(_account, _password);
        }
    }

    private void HandleLogin(Packet p)
    {
        var result = p.ReadByte();
        if (result == 0xFF && HandleLoginDenied(p)) return;
        Nation = result == 0xFF ? Nations.NotSelected : result;
        if (ReconnectLogin(result != 0xFF)) return;
        LoginResultEvent?.Invoke(result != 0xFF, Nation);
    }

    private void HandleNationSelect(Packet p)
    {
        int nation = p.ReadByte();
        if (nation != Nations.NotSelected) Nation = nation;
        NationResultEvent?.Invoke(nation);
    }

    private void HandleCreateCharacter(Packet p)
    {
        if (p.RemainingBytes <= 2)
            CreateCharResultEvent?.Invoke(p.ReadByte());
    }

    private void ParseCharList(Packet p)
    {
        if (p.ReadByte() != CharListSub || p.ReadByte() != CharListReady) return;

        var list = new List<CharacterSummary>();
        for (int slot = 0; slot < CharacterRecordWire.SlotCount; slot++)
        {
            if (p.RemainingBytes < 2) break;
            list.Add(CharacterRecordWire.Read(p));
        }
        CharListEvent?.Invoke(list);
    }

    private void ParseSelect(Packet p)
    {
        byte result = p.ReadByte();
        if (result != 1)
        {
            if (_selectRetries < 2 && SelectedChar.Length > 0)
            {
                _selectRetries++;
                int generation = _connGeneration;
                GetTree().CreateTimer(0.8).Timeout += () =>
                {
                    if (generation == _connGeneration && _conn.Connected) SendSelect();
                };
                return;
            }
            if (ReconnectSelectFailed()) return;
            ErrorEvent?.Invoke($"character select failed (result {result})");
            return;
        }
        _pendingZone = p.ReadShort();
        p.ReadShort(); p.ReadShort(); p.ReadShort();
        SendGameStart(GameStartCharacterPicked);
    }

    private void ParseMyInfo(Packet p)
    {
        MyCharId = p.ReadInt();
        string name = p.ReadSByteString();
        float x = p.ReadShort() / 10f;
        float z = p.ReadShort() / 10f;
        float y = p.ReadShort() / 10f;

        int authority = 1;
        int knightsRank = LibreKO.Domain.NationRankAura.Unranked, personalRank = LibreKO.Domain.NationRankAura.Unranked;
        int level = 0, maxHp = 0, hp = 0, maxMp = 0, mp = 0;
        long exp = 0, maxExp = 0;
        int nation = 0, race = 0, cls = 0, face = 0, hair = 0;
        int statPoints = 0, np = 0, monthlyNp = 0, gold = 0, maxWeight = 0;
        int str = 0, sta = 0, dex = 0, intel = 0, magicStat = 0;
        int strB = 0, staB = 0, dexB = 0, intelB = 0, magicB = 0;
        int ap = 0, ac = 0, fr = 0, cr = 0, lr = 0, mr = 0, dr = 0, pr = 0;
        int[] gear = System.Array.Empty<int>();
        ItemSlot[] inventory = System.Array.Empty<ItemSlot>();
        byte[] skillPoints = new byte[9];
        int capeId = 0, capeR = 0, capeG = 0, capeB = 0;
        short knightsId = 0; byte clanFame = 0, clanFlag = 0, clanGrade = 0, clanRanking = 0;
        int allianceId = 0, markVersion = 0; string clanName = "";
        bool isKing = false;
        int rebirthLevel = 0, rebStr = 0, rebSta = 0, rebDex = 0, rebInt = 0, rebMag = 0, manner = 0;
        long sealedExp = 0;
        try
        {
            nation = p.ReadByte();
            race = p.ReadByte();
            cls = p.ReadShort();
            face = p.ReadByte();
            hair = p.ReadInt();
            p.ReadByte(); p.ReadByte();
            isKing = p.ReadByte() == KingFlag;
            p.ReadByte();
            level = p.ReadByte();
            statPoints = p.ReadShort();
            maxExp = p.ReadLong();
            exp = p.ReadLong();
            np = p.ReadInt();
            monthlyNp = p.ReadInt();
            knightsId = p.ReadShort();
            clanFame = p.ReadByte();
            if (knightsId > 0)
            {
                allianceId = p.ReadShort();
                clanFlag = p.ReadByte();
                clanName = p.ReadSByteString();
                clanGrade = p.ReadByte(); clanRanking = p.ReadByte();
                markVersion = p.ReadShort();
                capeId = p.ReadShort();
                capeR = p.ReadByte(); capeG = p.ReadByte(); capeB = p.ReadByte();
                p.ReadByte();
            }
            else
            {
                p.ReadLong();
                capeId = p.ReadUShort();
                p.ReadInt();
            }
            p.ReadLong();
            maxHp = p.ReadShort(); hp = p.ReadShort();
            maxMp = p.ReadShort(); mp = p.ReadShort();
            maxWeight = p.ReadInt(); p.ReadInt();
            str = p.ReadByte(); strB = p.ReadByte();
            sta = p.ReadByte(); staB = p.ReadByte();
            dex = p.ReadByte(); dexB = p.ReadByte();
            intel = p.ReadByte(); intelB = p.ReadByte();
            magicStat = p.ReadByte(); magicB = p.ReadByte();
            ap = p.ReadShort(); ac = p.ReadShort();
            fr = p.ReadByte(); cr = p.ReadByte(); lr = p.ReadByte();
            mr = p.ReadByte(); dr = p.ReadByte(); pr = p.ReadByte();
            gold = p.ReadInt();
            authority = p.ReadByte();
            knightsRank = p.ReadByte();
            personalRank = p.ReadByte();
            skillPoints = new byte[9];
            for (int i = 0; i < 9; i++) skillPoints[i] = p.ReadByte();
            gear = new int[InventoryConstants.VisualSlotCount];
            inventory = new ItemSlot[InventoryConstants.InventoryTotal];
            for (int wireIndex = 0; wireIndex < InventoryConstants.MyInfoWireTotal; wireIndex++)
            {
                if (p.RemainingBytes < MyInfoSlotBytes)
                {
                    Godot.GD.PushWarning($"[myinfo] inventory block ends at wire slot {wireIndex} of "
                        + $"{InventoryConstants.MyInfoWireTotal} - server is on an older slot layout");
                    break;
                }
                int slot = InventoryConstants.MyInfoWireSlot(wireIndex);
                inventory[slot] = ReadItemRecord(p);
                int itemId = inventory[slot].ItemId;
                int vis = System.Array.IndexOf(InventoryConstants.VisualSlots, slot);
                if (vis >= 0) gear[vis] = itemId;
            }
            ReadMyInfoTail(p, out manner, out rebirthLevel, out rebStr, out rebSta, out rebDex, out rebInt, out rebMag, out sealedExp);
        }
        catch (System.Exception e)
        {
            Godot.GD.PushWarning($"[myinfo] parse failed, GM features disabled: {e.Message}");
            authority = 1;
        }

        bool refresh = SelfRefreshInWorld;
        LastEnter = new MyInfo
        {
            CharId = MyCharId, Name = name, X = x, Z = z, Y = y,
            Zone = refresh ? LastEnter.Zone : _pendingZone, Authority = authority,
            Nation = nation, Race = race, Class = cls, Face = face, Hair = hair,
            Gear = gear, Inventory = inventory,
            CapeId = capeId, CapeR = capeR, CapeG = capeG, CapeB = capeB,
            KnightsId = knightsId, ClanFame = clanFame, AllianceId = allianceId, ClanFlag = clanFlag,
            ClanName = clanName, ClanGrade = clanGrade, ClanRanking = clanRanking, MarkVersion = markVersion,
            KnightsRank = knightsRank, PersonalRank = personalRank, IsKing = isKing,
        };
        SeedMyClan(LastEnter);
        if (!refresh) ResetSessionState();
        SeedPlayerState(
            level, exp, maxExp, hp, maxHp, mp, maxMp,
            str, sta, dex, intel, magicStat, statPoints,
            strB, staB, dexB, intelB, magicB,
            ap, ac, gold, np, maxWeight,
            fr, cr, lr, mr, dr, pr, skillPoints);
        Sheet.ApplyLoyalty(np, monthlyNp);
        Sheet.SetManner(manner);
        Sheet.SeedRebirth(rebirthLevel, rebStr, rebSta, rebDex, rebInt, rebMag);
        if (refresh)
        {
            ResetExpSeal(sealedExp);
            RebuildWorldInPlace(x, z, y);
            return;
        }
        _known.Clear();
        _gmFxStates.Clear();
        _rankAuraHidden.Clear();
        OwnRankAuraShown = true;
        EventTitles.Clear();
        _flashTitle = "";
        SiegeWar = SiegeWarStatus.None;
        SiegePointTotal = 0;
        SiegeTowerPending = false;
        ResetVanguard();
        ResetExpSeal(sealedExp);
        if (ReconnectEntered()) return;
        EnterWorldEvent?.Invoke(LastEnter);
    }

    private const int MyInfoSlotBytes = 19;
    private const int MyInfoTailBeforeRebirth = 4 + 4 + 6 + 2;

    internal static void ReadMyInfoTail(Packet p, out int manner, out int level, out int str, out int sta, out int dex, out int intel, out int mag,
        out long sealedExp)
    {
        manner = level = str = sta = dex = intel = mag = 0;
        sealedExp = 0;
        int reserved = InventoryConstants.MyInfoReservedItemRecords * MyInfoSlotBytes;
        if (p.RemainingBytes < reserved + MyInfoTailBeforeRebirth + 6) return;
        for (int i = 0; i < reserved; i++) p.ReadByte();
        p.ReadByte();
        if (p.ReadByte() != 0) return;
        p.ReadByte(); p.ReadByte();
        manner = p.ReadInt();
        for (int i = 0; i < 6; i++) p.ReadByte();
        p.ReadShort();
        level = p.ReadByte();
        str = p.ReadByte(); sta = p.ReadByte(); dex = p.ReadByte(); intel = p.ReadByte(); mag = p.ReadByte();
        if (p.RemainingBytes >= sizeof(long)) sealedExp = p.ReadLong();
    }
}
