using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int TextNoahPlateLine = 40100;
    private const int TextNoahGraduationNear = 40103;
    private const int TextNoahGraduated = 40104;
    private static readonly Color NoahGraduationColour = new("ffff00");

    internal bool SelfIsNoahKnight => NoahKnights.IsMember(Sheet.Level, MyClan.InClan ? MyClan.Fame : ClanRanks.None);

    private void RefreshClanLine(int charId)
    {
        if (charId == _myId) RefreshSelfClanLine();
        else if (_ents.TryGetValue(charId, out var ent)) RefreshClanLine(ent, charId);
    }

    private void RefreshClanLine(Ent ent, int charId)
    {
        if (ent.IsNpc) return;
        if (SiegeClanLine(ent) is { } siege) { ent.Plate?.SetClan(siege.Text, siege.Colour); return; }
        ApplyClanLine(ent.Plate, ent.ClanName, ent.Level, ent.ClanFame, _stalls.ContainsKey(charId));
    }

    private void RefreshSelfClanLine()
    {
        var plate = SelfPlate();
        ApplyClanLine(plate, MyClan.InClan ? MyClan.Name : "", Sheet.Level,
            MyClan.InClan ? MyClan.Fame : ClanRanks.None, _stalls.ContainsKey(_myId));
        plate?.SetNameMark(NameMark.For(MyClan.InClan ? MyClan.Fame : ClanRanks.None, MyClan.InClan ? MyClan.Flag : 0, _zone, true));
    }

    private static void ApplyClanLine(PlateStack? plate, string clanName, int level, int clanDuty, bool hasStall)
    {
        if (plate == null) return;
        if (NameMark.HidesClan(Net.I.LastEnter.Zone)) plate.SetClan("");
        else if (NoahKnights.ShowsPlateLine(clanName, level, clanDuty, hasStall))
            plate.SetClan(ItemData.Text(TextNoahPlateLine, "[Noah Knights]"), NoahKnights.PlateColour);
        else
            plate.SetClan(clanName);
    }

    private void NoticeNoahGraduation(int oldLevel, int level)
    {
        if (oldLevel <= 0 || level == oldLevel) return;
        if (level == NoahKnights.MaxLevel)
            CombatNotice(ItemData.Text(TextNoahGraduationNear,
                "Starting from level 51 you graduate from the Noah Knights and become a freelance"), NoahGraduationColour);
        else if (level == NoahKnights.GraduationLevel)
            Notice.Show(this, ItemData.Text(TextNoahGraduated,
                "Congratulations for graduating from the Noah Knights. \n May your path be straight and peaceful."));
    }
}
