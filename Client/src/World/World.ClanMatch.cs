using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string MatchArt = "res://assets/ui/tournament/";
    private static readonly Vector2 MatchTeamSize = new(127f, 469f);
    private static readonly Rect2 MatchTitleRect = new(5f, 6f, 115f, 38f);
    private static readonly Rect2 MatchRedTitle = new(1f, 0f, 131f, 41f);
    private static readonly Rect2 MatchBlueTitle = new(1f, 40f, 131f, 40f);
    private const float MatchRowTop = 43f;
    private const float MatchRowStep = 53f;
    private static readonly Vector2 MatchRowSize = new(122f, 55f);
    private static readonly Rect2 MatchHpRect = new(9f, 10f, 107f, 10f);
    private static readonly Rect2 MatchMpRect = new(9f, 25f, 107f, 5f);
    private static readonly Rect2 MatchIconRect = new(8f, 34f, 16f, 16f);
    private static readonly Vector2 MatchNamePos = new(25f, 35f);
    private static readonly Vector2 MatchBannerDesign = new(1024f, 768f);
    private static readonly Color MatchRedName = new(1f, 0f, 0f);
    private static readonly Color MatchHpColour = new("c0392b");
    private static readonly Color MatchMpColour = new("2e86de");
    private static readonly Color MatchBlueName = new(0f, 0f, 1f);
    private const double MatchFrameFade = 1.0 / 1.2;
    private const double MatchStarFade = 1.0 / 5.4;
    private const double MatchPulseSeconds = 0.8;
    private const float MatchPulseLow = 0.55f;

    private sealed class MatchRow
    {
        public Control Root = null!;
        public TextureRect Icon = null!;
        public Label Name = null!;
        public PartyMeter Hp = null!, Mp = null!;
        public int Id = -1;
    }

    private CanvasLayer _matchLayer = null!;
    private readonly Control[] _matchTeams = new Control[ClanMatch.TeamSlots];
    private readonly TextureRect[] _matchTitles = new TextureRect[ClanMatch.TeamSlots];
    private readonly MatchRow[,] _matchRows = new MatchRow[ClanMatch.TeamSlots, ClanMatch.MembersPerTeam];
    private Control? _matchBanner;

    private void ClanMatchInit()
    {
        _matchLayer = new CanvasLayer { Layer = 61 };
        AddChild(_matchLayer);
        for (int team = 0; team < ClanMatch.TeamSlots; team++)
            _matchTeams[team] = BuildMatchTeam(team);
        Net.I.ClanMatchPairEvent += OnMatchPair;
        Net.I.ClanMatchRosterEvent += OnMatchRoster;
        Net.I.ClanMatchVitalsEvent += OnMatchVitals;
        Net.I.ClanMatchResultEvent += OnMatchResult;
    }

    private void ClanMatchDispose()
    {
        Net.I.ClanMatchPairEvent -= OnMatchPair;
        Net.I.ClanMatchRosterEvent -= OnMatchRoster;
        Net.I.ClanMatchVitalsEvent -= OnMatchVitals;
        Net.I.ClanMatchResultEvent -= OnMatchResult;
    }

    private static Texture2D? MatchTexture(string file) =>
        ResourceLoader.Exists(MatchArt + file) ? GD.Load<Texture2D>(MatchArt + file) : null;

    private Control BuildMatchTeam(int team)
    {
        var root = new Control { CustomMinimumSize = MatchTeamSize, Size = MatchTeamSize, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _matchLayer.AddChild(root);
        _matchTitles[team] = new TextureRect
        {
            Position = MatchTitleRect.Position, Size = MatchTitleRect.Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        root.AddChild(_matchTitles[team]);
        for (int i = 0; i < ClanMatch.MembersPerTeam; i++)
        {
            var row = new MatchRow
            {
                Root = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore },
            };
            row.Root.AddThemeStyleboxOverride("panel", UiTheme.MeterTrack());
            row.Root.Position = new Vector2(1f, MatchRowTop + i * MatchRowStep);
            row.Root.Size = MatchRowSize;
            row.Root.Visible = false;
            var canvas = row.Root;
            row.Hp = new PartyMeter(PartyBarTrack) { Position = MatchHpRect.Position, Size = MatchHpRect.Size, Fill = MatchHpColour };
            row.Mp = new PartyMeter(PartyBarTrack) { Position = MatchMpRect.Position, Size = MatchMpRect.Size, Fill = MatchMpColour };
            row.Icon = new TextureRect
            {
                Position = MatchIconRect.Position, Size = MatchIconRect.Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            row.Name = UiTheme.Text("", 11, UiTheme.TextHi);
            row.Name.Position = MatchNamePos;
            canvas.AddChild(row.Hp);
            canvas.AddChild(row.Mp);
            canvas.AddChild(row.Icon);
            canvas.AddChild(row.Name);
            root.AddChild(row.Root);
            _matchRows[team, i] = row;
        }
        return root;
    }

    private void OnMatchPair(ClanMatchPair pair) => RefreshMatchNames();

    private void OnMatchRoster(List<ClanMatchFighter> roster)
    {
        if (Net.I.MatchPair is not { } pair) return;
        var teams = ClanMatch.Split(roster);
        var titles = MatchTexture("titles.png");
        for (int slot = 0; slot < ClanMatch.TeamSlots; slot++)
        {
            var members = teams[slot];
            _matchTeams[slot].Visible = members.Count > 0;
            if (members.Count > 0 && titles != null)
                _matchTitles[slot].Texture = new AtlasTexture
                {
                    Atlas = titles,
                    Region = ClanMatch.TeamOf(members[0].Clan, pair) == ClanMatch.RedTeam ? MatchRedTitle : MatchBlueTitle,
                };
            for (int i = 0; i < ClanMatch.MembersPerTeam; i++)
            {
                var row = _matchRows[slot, i];
                bool used = i < members.Count && members[i].MaxHp > 0 && members[i].MaxMp > 0;
                row.Root.Visible = used;
                row.Id = i < members.Count ? members[i].Id : -1;
                if (!used) continue;
                var fighter = members[i];
                row.Name.Text = fighter.Name;
                row.Icon.Texture = UiIcons.Get(PartyHud.ClassIcon(fighter.Class));
                row.Hp.Target = ClanMatch.Percent(fighter.Hp, fighter.MaxHp) / 100f;
                row.Mp.Target = ClanMatch.Percent(fighter.Mp, fighter.MaxMp) / 100f;
            }
        }
        PlaceMatchTeams();
        RefreshMatchNames();
    }

    private void OnMatchVitals(ClanMatchVitals vitals)
    {
        foreach (var row in _matchRows)
        {
            if (row.Id != vitals.Id) continue;
            row.Hp.Target = ClanMatch.Percent(vitals.Hp, vitals.MaxHp) / 100f;
            row.Mp.Target = ClanMatch.Percent(vitals.Mp, vitals.MaxMp) / 100f;
        }
    }

    private void PlaceMatchTeams()
    {
        if (!IsInsideTree()) return;
        var screen = GetViewport().GetVisibleRect().Size;
        float top = (screen.Y - MatchTeamSize.Y) * 0.5f;
        _matchTeams[0].Position = new Vector2(0f, top);
        _matchTeams[1].Position = new Vector2(screen.X - MatchTeamSize.X, top);
    }

    private void RefreshMatchNames()
    {
        if (Net.I.MatchPair is not { } pair || !ClanMatch.ColoursNames(_zone, pair.Zone)) return;
        foreach (var e in _ents.Values)
        {
            if (e.IsNpc || e.NameTag == null || !GodotObject.IsInstanceValid(e.NameTag)) continue;
            if (e.KnightsId == pair.ClanA) NamePlate.Tint(e.NameTag, MatchRedName);
            else if (e.KnightsId == pair.ClanB) NamePlate.Tint(e.NameTag, MatchBlueName);
        }
    }

    private void OnMatchResult(int winnerClan)
    {
        if (Net.I.MatchPair is not { } pair) return;
        _matchBanner?.QueueFree();
        _matchBanner = BuildMatchBanner(ClanMatch.BannerFor(winnerClan, pair));
        _matchLayer.AddChild(_matchBanner);
    }

    private Control BuildMatchBanner(MatchBanner style)
    {
        var centre = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var stage = new Control { CustomMinimumSize = MatchBannerDesign, MouseFilter = Control.MouseFilterEnum.Ignore };
        centre.AddChild(stage);
        if (IsInsideTree())
        {
            float scale = Mathf.Min(1f, GetViewport().GetVisibleRect().Size.Y / MatchBannerDesign.Y);
            stage.Scale = new Vector2(scale, scale);
            stage.PivotOffset = MatchBannerDesign * 0.5f;
        }

        var frames = new List<(TextureRect Image, double Fade)>();
        TextureRect Frame(string file, Rect2 at, Rect2 uv, double fade)
        {
            var image = new TextureRect
            {
                Texture = MatchTexture(file) is { } tex ? new AtlasTexture { Atlas = tex, Region = new Rect2(uv.Position * tex.GetSize(), uv.Size * tex.GetSize()) } : null,
                Position = at.Position, Size = at.Size,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                Modulate = new Color(1f, 1f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            stage.AddChild(image);
            frames.Add((image, fade));
            return image;
        }

        var whole = new Rect2(0f, 0f, 1f, 1f);
        Frame("victory_bg.png", new Rect2(0f, 140f, 1024f, 479f), whole, MatchFrameFade);
        Frame("victory_light1.png", new Rect2(239f, 79f, 527f, 571f), new Rect2(0f, 0f, 1f, 0.9609f), MatchFrameFade);
        Frame("victory_light2.png", new Rect2(239f, 79f, 527f, 571f), new Rect2(0f, 0f, 1f, 0.9609f), MatchFrameFade);
        Frame("victory_wing.png", new Rect2(244f, 96f, 512f, 276f), new Rect2(0f, 0f, 1f, 0.4883f), MatchFrameFade);
        Frame("victory_sword.png", new Rect2(420f, 119f, 161f, 550f), new Rect2(0f, 0f, 0.3516f, 1f), MatchFrameFade);
        foreach (float x in new[] { 296f, 339f, 381f, 423f, 465f, 509f, 551f, 594f, 636f })
            Frame("victory_star.png", new Rect2(x, 558f, 64f, 64f), whole, MatchStarFade);
        var result = style switch
        {
            MatchBanner.BlueWins => Frame("victory_team.png", new Rect2(242f, 287f, 512f, 283f), new Rect2(0f, 0f, 1f, 0.5f), MatchFrameFade),
            MatchBanner.RedWins => Frame("victory_team.png", new Rect2(242f, 295f, 512f, 283f), new Rect2(0f, 0.5f, 1f, 0.5f), MatchFrameFade),
            _ => Frame("victory_draw.png", new Rect2(242f, 295f, 512f, 283f), new Rect2(0f, 0f, 1f, 0.5f), MatchFrameFade),
        };

        if (IsInsideTree())
        {
            var tween = CreateTween();
            foreach (var (image, fade) in frames)
                tween.TweenProperty(image, "modulate:a", 1f, fade);
            var pulse = CreateTween().SetLoops();
            pulse.TweenInterval(frames.Count * MatchFrameFade);
            pulse.TweenProperty(result, "modulate:a", MatchPulseLow, MatchPulseSeconds);
            pulse.TweenProperty(result, "modulate:a", 1f, MatchPulseSeconds);
        }
        else
            foreach (var (image, _) in frames) image.Modulate = Colors.White;
        return centre;
    }
}
