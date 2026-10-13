using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int TextItemAnnouncement = 7681;
    private const int SparkleFx = 20100;
    private const int GlowFx = 10106;
    private static readonly Color AnnouncementColour = new(1f, 1f, 0f);

    private void EffectInit() => Net.I.EffectEvent += OnEffect;

    private void EffectDispose() => Net.I.EffectEvent -= OnEffect;

    private void OnEffect(EffectReply e)
    {
        if (e.EntityId == EffectReply.Announcement)
        {
            if (e.Name.Length == 0 || e.Value == 0) return;
            ShowUpgradeNoticeBanner(FillInOrder(
                SystemText(TextItemAnnouncement, "Congratulation!! %s has received [%s] item."),
                e.Name, ItemData.DisplayName(e.Value)), AnnouncementColour);
            return;
        }

        int fx = e.Value switch
        {
            EffectReply.SparkleValue => SparkleFx,
            EffectReply.GlowValue => GlowFx,
            _ => 0,
        };
        if (fx != 0)
        {
            if (Fx.NameForId(fx) is { } name) SpawnFxOn(e.EntityId, name, 0f);
            return;
        }
        if (e.Value >= EffectReply.WindowValueFirst) return;
        if (SkillData.Get(e.Value)?.TargetFx is { } targetFx) SpawnFxOn(e.EntityId, targetFx, 0f);
    }
}
