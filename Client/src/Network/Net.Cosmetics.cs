using System;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<int, bool>? HelmetEvent;

    public bool HelmetHidden { get; private set; }

    public bool CostumeHidden { get; private set; }

    public event Action<int, bool>? CostumeEvent;

    private void HandleHelmet(Packet p)
    {
        if (p.RemainingBytes < 6) return;
        bool hidden = p.ReadByte() != 0;
        bool costumeHidden = p.ReadByte() != 0;
        int charId = p.ReadInt();
        if (charId == LastEnter.CharId)
        {
            HelmetHidden = hidden;
            CostumeHidden = costumeHidden;
        }
        HelmetEvent?.Invoke(charId, hidden);
        CostumeEvent?.Invoke(charId, costumeHidden);
    }

    public void SendLookToggles(bool hideHelmet, bool hideCostume)
    {
        var p = new Packet(GameOpcodes.GS_HELMET);
        p.WriteByte((byte)(hideHelmet ? 1 : 0));
        p.WriteByte((byte)(hideCostume ? 1 : 0));
        _conn.Send(p);
    }
}
