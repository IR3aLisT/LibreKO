using System;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<CharacterSealList>? CharacterSealListEvent;
    public event Action<CharacterSealOutcome>? CharacterSealEvent;
    public event Action<CharacterUnsealOutcome>? CharacterUnsealEvent;
    public event Action<SealedCharacterSheet?>? SealedCharacterSheetEvent;
    public event Action<int[]>? SealedAchievementsEvent;

    private const ushort SealServerIndex = 1;
    private const int SealRequestFromBag = -1;

    private void HandleCharacterSeal(Packet p)
    {
        if (p.RemainingBytes < 2) return;
        switch (p.ReadByte())
        {
            case CharacterSealWire.ListSub:
                CharacterSealListEvent?.Invoke(CharacterSealWire.ReadList(p));
                break;
            case CharacterSealWire.SealSub:
            {
                var outcome = CharacterSealWire.ReadSeal(p);
                if (outcome.Summary is { } summary) PetItems[summary.Index] = summary;
                CharacterSealEvent?.Invoke(outcome);
                break;
            }
            case CharacterSealWire.UnsealSub:
                CharacterUnsealEvent?.Invoke(CharacterSealWire.ReadUnseal(p));
                break;
            case CharacterSealWire.SheetSub:
                SealedCharacterSheetEvent?.Invoke(CharacterSealWire.ReadSheet(p));
                break;
            case CharacterSealWire.AchievementsSub:
                SealedAchievementsEvent?.Invoke(CharacterSealWire.ReadAchievements(p));
                break;
        }
    }

    private static Packet CharacterSealPacket(byte kind)
    {
        var p = new Packet(GameOpcodes.GS_ITEM_UPGRADE);
        p.WriteByte(CharacterSealWire.Sub);
        p.WriteByte(kind);
        return p;
    }

    public void SendCharacterSealList()
    {
        var p = CharacterSealPacket(CharacterSealWire.ListSub);
        p.WriteUShort(SealServerIndex);
        _conn.Send(p);
    }

    public void SendCharacterSeal(byte bagPos, int scrollId, string name, string code)
    {
        var p = CharacterSealPacket(CharacterSealWire.SealSub);
        p.WriteInt(SealRequestFromBag);
        p.WriteByte(bagPos);
        p.WriteInt(scrollId);
        p.WriteString(name);
        p.WriteString(code);
        _conn.Send(p);
    }

    public void SendCharacterUnseal(byte bagPos, int ringId, byte slot)
    {
        var p = CharacterSealPacket(CharacterSealWire.UnsealSub);
        p.WriteInt(SealRequestFromBag);
        p.WriteByte(bagPos);
        p.WriteInt(ringId);
        p.WriteByte(slot);
        _conn.Send(p);
    }

    public void SendSealedCharacterSheet(int uniqueId)
    {
        var p = CharacterSealPacket(CharacterSealWire.SheetSub);
        p.WriteInt(uniqueId);
        _conn.Send(p);
    }

    public void SendSealedAchievements(string name)
    {
        var p = CharacterSealPacket(CharacterSealWire.AchievementsSub);
        p.WriteString(name);
        _conn.Send(p);
    }
}
