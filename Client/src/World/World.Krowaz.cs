using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int TextKrowazTeleport = 7667;
    private static readonly Color KrowazNoticeColour = new(1f, 1f, 0x32 / 255f);

    private void KrowazInit() => Net.I.KrowazNoticeEvent += OnKrowazNotice;

    private void KrowazDispose() => Net.I.KrowazNoticeEvent -= OnKrowazNotice;

    private void OnKrowazNotice(int seconds) =>
        CombatNotice(TextTemplate.Fill(SystemText(TextKrowazTeleport, "You will be teleported to Crowaz in %d seconds"), seconds),
            KrowazNoticeColour);
}
