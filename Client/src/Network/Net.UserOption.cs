using System;
using System.Collections.Generic;

namespace LibreKO.Network;

public partial class Net
{
    public const byte UserOptionRankAura = 2;
    private const int RankAuraRecordBytes = 4 + 1;

    private readonly HashSet<int> _rankAuraHidden = new();
    public event Action<int, bool>? RankAuraShownEvent;

    public bool OwnRankAuraShown { get; private set; } = true;

    public bool RankAuraShown(int id) => id == MyCharId ? OwnRankAuraShown : !_rankAuraHidden.Contains(id);

    private void HandleUserOption(Packet p)
    {
        if (p.RemainingBytes < 1 + RankAuraRecordBytes || p.ReadByte() != UserOptionRankAura) return;
        int id = p.ReadInt();
        bool shown = p.ReadByte() != 0;
        if (shown) _rankAuraHidden.Remove(id);
        else _rankAuraHidden.Add(id);
        RankAuraShownEvent?.Invoke(id, shown);
    }

    public void SetOwnRankAuraShown(bool shown)
    {
        if (OwnRankAuraShown == shown) return;
        OwnRankAuraShown = shown;
        var p = new Packet(GameOpcodes.GS_USER_OPTION);
        p.WriteByte(UserOptionRankAura);
        p.WriteByte((byte)(shown ? 1 : 0));
        _conn.Send(p);
        RankAuraShownEvent?.Invoke(MyCharId, shown);
    }
}
