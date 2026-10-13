using System;

namespace LibreKO.Network;

public partial class Net
{
    public event Action? ClanBattleNotifyEvent;

    public event Action<byte>? IdentityCheckResultEvent;

    private const byte IdentityCheckResult = 1;

    private void HandleClanBattle(Packet p)
    {
        ClanBattleNotifyEvent?.Invoke();
    }

    private void HandleIdentityCheck(Packet p)
    {
        if (p.RemainingBytes < 2 || p.ReadByte() != IdentityCheckResult) return;
        IdentityCheckResultEvent?.Invoke(p.ReadByte());
    }
}
