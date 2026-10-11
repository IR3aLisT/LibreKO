using Godot;

namespace LibreKO;

public partial class World : Node3D
{
    private static readonly string[] StatLabels = { "STR", "HP", "DEX", "INT", "MP" };
    private static readonly int[] StatDisplayOrder = { 0, 1, 2, 4, 3 };
    private static readonly string[] ResistLabels = { "Fire", "Ice", "Lightning", "Magic", "Curse", "Poison" };

    private const string LevelUpFxKarus = "karu_levelup";
    private const string LevelUpFxElMorad = "elmo_levelup";

    internal CharacterSheet Sheet => Net.I.Sheet;

    private VBoxContainer _statsContent = null!;
    private Label _stHeaderName = null!, _stHeaderSub = null!, _stExpLbl = null!, _stApLbl = null!,
        _stNpLbl = null!, _stMannerLbl = null!, _stBonusLbl = null!, _stLevelLbl = null!, _stNationLbl = null!;
    private readonly Label[] _statValLbls = new Label[CharacterSheet.StatCount];
    private readonly Button[] _statBtns = new Button[CharacterSheet.StatCount];
    private readonly Label[] _resistLbls = new Label[CharacterSheet.ResistCount];

    private void StatsInit()
    {
        BuildStatsPanel();
        BuildTitlePicker();
        RefreshStatsUI();

        Net.I.PointChangeEvent += OnPointChange;
        Net.I.GoldChangeEvent += OnGoldChange;
        Net.I.LootGoldEvent += OnLootGold;
        Net.I.GoldChangeLogEvent += OnGoldChangeLog;
        Net.I.ExpChangeEvent += OnExpChange;
        Net.I.ItemStatsEvent += OnSheetStats;
        Net.I.LevelChangeEvent += OnLevelChange;
        Net.I.PeerLevelChangeEvent += OnPeerLevelChange;
        Net.I.LoyaltyChangeEvent += OnLoyaltyChange;
        Net.I.MannerChangeEvent += OnMannerChange;
        Net.I.StatResetEvent += OnStatReset;
        Net.I.ClassEligibilityEvent += OnClassEligibility;
        Net.I.JobChangeResultEvent += OnJobChangeResult;
        Net.I.ClassPromotedEvent += OnClassPromoted;
    }

    private void StatsDispose()
    {
        Net.I.PointChangeEvent -= OnPointChange;
        Net.I.GoldChangeEvent -= OnGoldChange;
        Net.I.LootGoldEvent -= OnLootGold;
        Net.I.GoldChangeLogEvent -= OnGoldChangeLog;
        Net.I.ExpChangeEvent -= OnExpChange;
        Net.I.ItemStatsEvent -= OnSheetStats;
        Net.I.LevelChangeEvent -= OnLevelChange;
        Net.I.PeerLevelChangeEvent -= OnPeerLevelChange;
        Net.I.LoyaltyChangeEvent -= OnLoyaltyChange;
        Net.I.MannerChangeEvent -= OnMannerChange;
        Net.I.StatResetEvent -= OnStatReset;
        Net.I.ClassEligibilityEvent -= OnClassEligibility;
        Net.I.JobChangeResultEvent -= OnJobChangeResult;
        Net.I.ClassPromotedEvent -= OnClassPromoted;
    }

    private void OnStatReset(bool ok, int money, int[] stats, int maxHp, int maxMp, int ap, int statPoints)
    {
        if (!ok) return;
        Sheet.ApplyReset(stats, statPoints, ap);
        Vitals.ApplyMaxima(maxHp, maxMp);
        _hpBar?.Set(Vitals.Hp, Vitals.MaxHp);
        _mpBar?.Set(Vitals.Mp, Vitals.MaxMp);
        RefreshStatsUI();
    }

    private void OnClassEligibility(int code) =>
        CombatNotice(code == 1 ? "You are eligible to change class." : "You cannot change class yet.");

    private void OnClassPromoted(int charId, int newClass)
    {
        if (charId != _myId && charId != Net.I.LastEnter.CharId) return;
        ApplyClassChange(newClass);
        CombatNotice($"You are now a {CharacterClassCatalog.SpecializationName(newClass)}!");
    }

    private void ApplyClassChange(int newClass)
    {
        _selfClass = newClass;
        RefreshStatsUI();
        RebuildSkillWindow();
        ClearHotbar();
    }

    private void OnJobChangeResult(int code)
    {
        CombatNotice(code switch
        {
            1 => "Class changed!",
            4 => "Take off your equipment before changing class.",
            6 => "You need a job-change scroll (and a different class).",
            _ => "Class change failed.",
        });
    }

    private const int CharacterPageWidth = 560;
    private const int StatsCombatColumn = 132;
    private const int StatsResistColumn = 172;
    private const int StatsIconSize = 22;
    private const int StatsResistIconSize = 20;

    private static readonly string[] StatIcons =
        { "system/stat-str", "system/stat-hp", "system/stat-dex", "system/stat-int", "system/stat-mp" };

    private static readonly Color[] StatIconTints =
    {
        new("e0855a"), new("d95f5f"), new("5fc7d9"), new("b98ce0"), new("5f9fd9"),
    };

    private static readonly string[] ResistIcons =
    {
        "system/res-fire", "system/res-ice", "system/res-lightning",
        "system/res-magic", "system/res-curse", "system/res-poison",
    };

    private static readonly Color[] ResistTints =
    {
        new("e2703a"), new("6fd0e8"), new("e8c53a"), new("b06fe8"), new("8f6fe8"), new("6fd07a"),
    };

    private Label _stRebirthCaption = null!, _stRebirthLbl = null!, _stClanLbl = null!;
    private VSeparator _stRebirthRule = null!;
    private TextureRect _stMedal = null!;
    private readonly PanelContainer[] _statRowFrames = new PanelContainer[CharacterSheet.StatCount];
    private Godot.Timer _statRepeat = null!;
    private int _statRepeatRow = -1;
    private const double StatRepeatDelay = 0.4;
    private const double StatRepeatEvery = 0.08;
    private const float StatsMedalSize = 34f;
    private const float StatRowPadX = 4f;
    private const float StatRowPadY = 1f;
    private const string RecommendedStatHint = "Recommended for your class";
    private static readonly Color RecommendedFrame = new(UiTheme.Gold, 0.55f);
    private Label _stAcLbl = null!;
    private ProgressBar _stExpBar = null!;
    private Label _stExpPercentLbl = null!;
    private readonly Label[] _statBonusLbls = new Label[CharacterSheet.StatCount];

    private static Color BonusColour(int bonus) =>
        bonus > 0 ? UiTheme.Good : bonus < 0 ? UiTheme.Bad : UiTheme.TextDim;

    private static string BonusText(int bonus) =>
        bonus == 0 ? "" : bonus > 0 ? $"(+{bonus})" : $"({bonus})";


    private void BuildStatsPanel()
    {
        var root = new VBoxContainer { CustomMinimumSize = new Vector2(CharacterPageWidth, 0) };
        root.AddThemeConstantOverride("separation", 8);
        _statsContent = root;

        BuildStatsHeader(root);
        BuildStatsProgress(root);

        var middle = new HBoxContainer();
        middle.AddThemeConstantOverride("separation", 8);
        root.AddChild(middle);
        BuildStatsCombat(middle);
        BuildStatsAttributes(middle);

        var right = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(StatsResistColumn, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        right.AddThemeConstantOverride("separation", 8);
        middle.AddChild(right);
        BuildStatsResistance(right);
    }

    private void BuildStatsHeader(VBoxContainer root)
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);

        _stMedal = new TextureRect
        {
            CustomMinimumSize = new Vector2(StatsMedalSize, StatsMedalSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            Visible = false,
        };
        header.AddChild(_stMedal);

        var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 0);
        header.AddChild(names);
        _stHeaderName = HudStyle.Label(17);
        names.AddChild(_stHeaderName);
        _stHeaderSub = UiTheme.Text("", 12, UiTheme.TextLo);
        names.AddChild(_stHeaderSub);
        _stClanLbl = UiTheme.Text("", 12, UiTheme.Gold);
        names.AddChild(_stClanLbl);

        var presetBtn = new Button
        {
            Text = "Stat Preset",
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            TooltipText = "Plan a stat or mastery spread",
        };
        presetBtn.Pressed += TogglePreset;
        header.AddChild(presetBtn);
    }

    private void BuildStatsProgress(VBoxContainer root)
    {
        var section = StatsSection(root, "Progress", out _);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        section.AddChild(top);
        _stLevelLbl = StatsPairField(top, "Level");
        top.AddChild(new VSeparator());
        _stNationLbl = StatsPairField(top, "Nation");
        _stRebirthRule = new VSeparator();
        top.AddChild(_stRebirthRule);
        _stRebirthLbl = StatsPairField(top, "Rebirth", out _stRebirthCaption);

        section.AddChild(StatsRule());

        var expRow = new HBoxContainer();
        expRow.AddThemeConstantOverride("separation", 10);
        section.AddChild(expRow);
        var expCaption = UiTheme.Text("EXP", 13, UiTheme.TextLo);
        expCaption.CustomMinimumSize = new Vector2(70, 0);
        expRow.AddChild(expCaption);

        _stExpBar = new ProgressBar
        {
            MaxValue = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(96, 14),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _stExpBar.AddThemeStyleboxOverride("background", UiTheme.MeterTrack());
        _stExpBar.AddThemeStyleboxOverride("fill", UiTheme.MeterFill(UiTheme.Gold));
        expRow.AddChild(_stExpBar);

        _stExpPercentLbl = UiTheme.Text("", 13, UiTheme.TextHi, HorizontalAlignment.Right);
        _stExpPercentLbl.CustomMinimumSize = new Vector2(58, 0);
        expRow.AddChild(_stExpPercentLbl);

        _stExpLbl = UiTheme.Text("", 12, UiTheme.TextDim, HorizontalAlignment.Right);
        section.AddChild(_stExpLbl);

        section.AddChild(StatsRule());
        _stNpLbl = StatsCaptionRow(section, "Contribution");
        _stMannerLbl = StatsCaptionRow(section, "Manner");
    }

    private void BuildStatsCombat(HBoxContainer parent)
    {
        var column = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(StatsCombatColumn, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        parent.AddChild(column);
        var section = StatsSection(column, "Combat", out _, fill: true);

        _stApLbl = StatsCombatEntry(section, "system/combat-attack", "Attack", UiTheme.GoldBright);
        section.AddChild(StatsRule());
        _stAcLbl = StatsCombatEntry(section, "system/combat-defence", "Defence", UiTheme.TextLo);
    }

    private static Label StatsCombatEntry(VBoxContainer section, string icon, string caption, Color tint)
    {
        var box = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 2);
        box.Alignment = BoxContainer.AlignmentMode.Center;
        section.AddChild(box);

        var iconRow = new CenterContainer();
        iconRow.AddChild(UiIcons.Image(icon, new Vector2(38, 38), tint));
        box.AddChild(iconRow);

        box.AddChild(UiTheme.Text(caption, 12, UiTheme.TextLo, HorizontalAlignment.Center));
        var value = UiTheme.Text("0", 21, UiTheme.TextHi, HorizontalAlignment.Center);
        box.AddChild(value);
        return value;
    }

    private void BuildStatsAttributes(HBoxContainer parent)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(column);
        var section = StatsSection(column, "Attributes", out var titleRow);

        titleRow.AddChild(UiTheme.Text("Stat Point", 12, UiTheme.TextLo));
        _stBonusLbl = UiTheme.Text("0", 13, UiTheme.GoldBright, HorizontalAlignment.Center);
        _stBonusLbl.CustomMinimumSize = new Vector2(26, 0);
        var pointFrame = new PanelContainer();
        pointFrame.AddThemeStyleboxOverride("panel", UiTheme.Inset());
        var pointMargin = new MarginContainer();
        UiTheme.Margins(pointMargin, 6, 1, 6, 1);
        pointMargin.AddChild(_stBonusLbl);
        pointFrame.AddChild(pointMargin);
        titleRow.AddChild(pointFrame);

        foreach (int row in StatDisplayOrder)
        {
            int index = row;
            if (row != StatDisplayOrder[0]) section.AddChild(StatsRule());

            var frame = new PanelContainer();
            _statRowFrames[row] = frame;
            section.AddChild(frame);
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 8);
            frame.AddChild(line);

            line.AddChild(UiIcons.Image(
                StatIcons[row], new Vector2(StatsIconSize, StatsIconSize), StatIconTints[row]));

            var name = UiTheme.Text(StatLabels[row], 13, UiTheme.GoldBright);
            name.CustomMinimumSize = new Vector2(42, 0);
            line.AddChild(name);

            _statValLbls[row] = UiTheme.Text("0", 14, UiTheme.TextHi, HorizontalAlignment.Right);
            _statValLbls[row].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            line.AddChild(_statValLbls[row]);

            _statBonusLbls[row] = UiTheme.Text("", 12, UiTheme.TextDim);
            _statBonusLbls[row].CustomMinimumSize = new Vector2(48, 0);
            _statBonusLbls[row].MouseFilter = Control.MouseFilterEnum.Pass;
            line.AddChild(_statBonusLbls[row]);

            var btn = new Button
            {
                Text = "+",
                CustomMinimumSize = new Vector2(30, 24),
                FocusMode = Control.FocusModeEnum.None,
                TooltipText = $"Spend a point on {StatLabels[row]}",
            };
            btn.ButtonDown += () => StartStatRepeat(index);
            btn.ButtonUp += StopStatRepeat;
            _statBtns[row] = btn;
            line.AddChild(btn);
        }

        _statRepeat = new Godot.Timer { OneShot = false };
        _statRepeat.Timeout += RepeatStat;
        section.AddChild(_statRepeat);
    }

    private void StartStatRepeat(int row)
    {
        OnAllocate(row);
        _statRepeatRow = row;
        _statRepeat.Start(StatRepeatDelay);
    }

    private void RepeatStat()
    {
        if (_statRepeatRow < 0 || !Sheet.CanAllocate || !_statBtns[_statRepeatRow].IsPressed()) { StopStatRepeat(); return; }
        OnAllocate(_statRepeatRow);
        _statRepeat.WaitTime = StatRepeatEvery;
    }

    private void StopStatRepeat()
    {
        _statRepeatRow = -1;
        _statRepeat.Stop();
    }

    private static string StatBonusTooltip(int rebirth, int other)
    {
        if (rebirth == 0 && other == 0) return "";
        var lines = new System.Collections.Generic.List<string>();
        if (rebirth != 0) lines.Add($"Rebirth {rebirth:+#;-#}");
        if (other != 0) lines.Add($"Items and buffs {other:+#;-#}");
        return string.Join("\n", lines);
    }

    private void BuildStatsResistance(VBoxContainer parent)
    {
        var section = StatsSection(parent, "Resistance", out _, fill: true);
        for (int index = 0; index < CharacterSheet.ResistCount; index++)
        {
            if (index > 0) section.AddChild(StatsRule());
            AddResistEntry(section, index);
        }
    }

    private void AddResistEntry(VBoxContainer column, int index)
    {
        var line = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        line.AddThemeConstantOverride("separation", 8);
        line.AddChild(UiIcons.Image(
            ResistIcons[index], new Vector2(StatsResistIconSize, StatsResistIconSize), ResistTints[index]));
        line.AddChild(UiTheme.Text(ResistLabels[index], 12, UiTheme.TextLo));

        _resistLbls[index] = UiTheme.Text("0", 13, UiTheme.TextHi, HorizontalAlignment.Right);
        _resistLbls[index].SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        line.AddChild(_resistLbls[index]);
        column.AddChild(line);
    }

    private static VBoxContainer StatsSection(
        Control parent, string title, out HBoxContainer titleRow, bool fill = false)
    {
        var box = UiTheme.Section();
        box.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (fill) box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        parent.AddChild(box);

        var margin = new MarginContainer();
        UiTheme.Margins(margin, 10, 8, 10, 8);
        box.AddChild(margin);

        var column = new VBoxContainer();
        if (fill) column.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        column.AddThemeConstantOverride("separation", 6);
        margin.AddChild(column);

        titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 6);
        column.AddChild(titleRow);
        titleRow.AddChild(UiTheme.Text("◆", 10, UiTheme.Bronze));
        var caption = UiTheme.Text(title.ToUpperInvariant(), 12, UiTheme.Gold);
        caption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(caption);

        column.AddChild(StatsRule());
        return column;
    }

    private static Control StatsRule()
    {
        var rule = new Panel { CustomMinimumSize = new Vector2(0, 1) };
        var style = new StyleBoxFlat { BgColor = UiTheme.EdgeSoft };
        rule.AddThemeStyleboxOverride("panel", style);
        return rule;
    }

    private static Label StatsPairField(HBoxContainer row, string caption) =>
        StatsPairField(row, caption, out _);

    private static Label StatsPairField(HBoxContainer row, string caption, out Label captionLabel)
    {
        var name = UiTheme.Text(caption, 13, UiTheme.TextLo);
        name.CustomMinimumSize = new Vector2(60, 0);
        row.AddChild(name);
        captionLabel = name;

        var value = UiTheme.Text("", 13, UiTheme.TextHi, HorizontalAlignment.Right);
        value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(value);
        return value;
    }

    private static Label StatsCaptionRow(VBoxContainer parent, string caption)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        parent.AddChild(row);

        var name = UiTheme.Text(caption, 13, UiTheme.TextLo);
        name.CustomMinimumSize = new Vector2(70, 0);
        row.AddChild(name);

        var value = UiTheme.Text("", 13, UiTheme.TextHi, HorizontalAlignment.Right);
        value.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(value);
        return value;
    }

    private void RefreshStatsUI()
    {
        UpdateLevelOrb();

        if (_stHeaderName == null) return;
        var info = Net.I.LastEnter;
        _stHeaderName.Text = $"{info.Name}    Lv {Sheet.LevelLabel}";
        _stHeaderSub.Text = $"{ClassName(info.Class)}   •   {Nations.Name(info.Nation)}";
        var clan = Net.I.MyClan;
        _stClanLbl.Text = clan.InClan ? $"{clan.Name}   •   {ClanRanks.Name(clan.Fame)}" : "";
        _stClanLbl.Visible = clan.InClan;
        string? medal = RankSymbol.PlatePath(info.KnightsRank, info.PersonalRank);
        _stMedal.Visible = medal != null;
        _stMedal.Texture = medal != null ? GD.Load<Texture2D>(medal) : null;
        _stMedal.TooltipText = info.PersonalRank > 0 ? $"National rank {info.PersonalRank}" : "";
        _stLevelLbl.Text = Sheet.Level.ToString();
        _stNationLbl.Text = Nations.Name(info.Nation);
        bool reborn = Sheet.RebirthLevel > 0;
        _stRebirthRule.Visible = _stRebirthCaption.Visible = _stRebirthLbl.Visible = reborn;
        _stRebirthLbl.Text = Sheet.RebirthLevel.ToString();
        _stExpBar.Value = Sheet.ExpPercent;
        _stExpPercentLbl.Text = $"{Sheet.ExpPercent:0.00}%";
        _stExpLbl.Text = Sheet.MaxExp > 0
            ? $"{Sheet.Exp:n0} / {Sheet.MaxExp:n0}"
            : Sheet.Exp.ToString("n0");
        _stNpLbl.Text = Sheet.MonthlyNp is { } monthlyNp ? $"{Sheet.Np:n0} / {monthlyNp:n0}" : Sheet.Np.ToString("n0");
        _stMannerLbl.Text = Sheet.Manner.ToString("n0");

        _stApLbl.Text = Sheet.Ap.ToString("n0");
        _stAcLbl.Text = Sheet.Ac.ToString("n0");
        _stBonusLbl.Text = Sheet.Points.ToString();

        for (int i = 0; i < CharacterSheet.StatCount; i++)
        {
            int bonus = Sheet.StatBonusAtRow(i);
            _statValLbls[i].Text = Sheet.StatAtRow(i).ToString();
            _statBonusLbls[i].Text = BonusText(bonus);
            _statBonusLbls[i].AddThemeColorOverride("font_color", BonusColour(bonus));
            int rebirth = Sheet.RebirthBonusAtRow(i);
            _statBonusLbls[i].TooltipText = StatBonusTooltip(rebirth, bonus - rebirth);
            _statBtns[i].Disabled = !Sheet.CanAllocate;
            bool recommended = StarterStats.Recommended(_selfClass > 0 ? _selfClass : info.Class, i);
            _statRowFrames[i].AddThemeStyleboxOverride("panel", recommended ? RecommendedRowStyle() : PlainRowStyle());
            _statRowFrames[i].TooltipText = recommended ? RecommendedStatHint : "";
        }

        for (int i = 0; i < CharacterSheet.ResistCount; i++)
            _resistLbls[i].Text = Sheet.ResistAt(i).ToString();
    }

    private static StyleBoxFlat RecommendedRowStyle()
    {
        var style = new StyleBoxFlat { BgColor = new Color(UiTheme.Gold, 0.06f), BorderColor = RecommendedFrame };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        style.ContentMarginLeft = style.ContentMarginRight = StatRowPadX;
        style.ContentMarginTop = style.ContentMarginBottom = StatRowPadY;
        return style;
    }

    private static StyleBoxEmpty PlainRowStyle() => new()
    {
        ContentMarginLeft = StatRowPadX, ContentMarginRight = StatRowPadX,
        ContentMarginTop = StatRowPadY, ContentMarginBottom = StatRowPadY,
    };

    private void OnAllocate(int statIndex)
    {
        if (!Sheet.CanAllocate) return;
        Net.I.SendPointChange(CharacterSheet.WireTypeForRow(statIndex));
    }

    private void OnPointChange(int type, int newVal, int maxHp, int maxMp, int totalHit, int maxWeight)
    {
        Sheet.SetMaxWeight(maxWeight);
        if (!Sheet.ApplyPointChange(type, newVal, totalHit)) return;
        Vitals.ApplyMaxima(maxHp, maxMp);
        _hpBar?.Set(Vitals.Hp, Vitals.MaxHp);
        _mpBar?.Set(Vitals.Mp, Vitals.MaxMp);
        RefreshStatsUI();
    }

    private void OnSheetStats(DerivedStats s)
    {
        Sheet.ApplyDerived(s);
        RefreshStatsUI();
    }

    private void OnGoldChange(int total)
    {
        long gained = total - Sheet.Gold;
        Sheet.SetGold(total);
        if (gained > 0) Floaters?.Gold(gained);
        RefreshStatsUI();
    }

    private const int TextPickedUpCoins = 6067;
    private const int TextEarnedExp = 3007;
    private const int TextLostExp = 3008;
    private const int TextEarnedNp = 6100;
    private const int TextEarnedManner = 6101;
    private const int TextLostNp = 6104;
    private const int TextEarnedLadder = 6103;
    private const int TextLostLadder = 6105;

    private static string WithAmount(int textId, string fallback, long amount) =>
        ItemData.Text(textId, fallback).Replace("%I64d", amount.ToString("n0")).Replace("%d", amount.ToString("n0"));

    private const int TextEarnedCoins = 6088;
    private const int TextLostCoins = 6089;
    private const int TextUsedCoins = 6099;
    private const int TextReceivedCoins = 6091;

    private void OnGoldChangeLog(GoldChangeReply reply)
    {
        switch (reply.Sub)
        {
            case GoldChangeReply.Earned:
                CombatLogAdd(WithAmount(TextEarnedCoins, "Earned %d Coins.", reply.Amount), CombatLogKind.Resource);
                break;
            case GoldChangeReply.Lost:
                CombatLogAdd(WithAmount(TextLostCoins, "Lost %d Coins.", reply.Amount), CombatLogKind.Incoming);
                break;
            case GoldChangeReply.Used:
                CombatLogAdd(WithAmount(TextUsedCoins, "Used %d Coins.", reply.Amount), CombatLogKind.Incoming);
                break;
            case GoldChangeReply.Received:
                CombatLogAdd(WithAmount(TextReceivedCoins, "Received %d coins.", reply.Amount), CombatLogKind.Resource);
                break;
        }
    }

    private void OnLootGold(int total)
    {
        long gained = total - Sheet.Gold;
        if (gained > 0)
            CombatLogAdd(WithAmount(TextPickedUpCoins, "Picked up %d Coins.", gained), CombatLogKind.Resource);
    }

    private void LogExpDelta(long delta)
    {
        if (delta > 0) CombatLogAdd(WithAmount(TextEarnedExp, "Earned %I64d Experience Points", delta), CombatLogKind.Experience);
        else if (delta < 0) CombatLogAdd(WithAmount(TextLostExp, "Lost %I64d Experience Points", -delta), CombatLogKind.Experience);
    }

    private void UpdateLevelOrb()
    {
        if (_orb == null || !GodotObject.IsInstanceValid(_orb)) return;
        _orb.Set(Sheet.Level, Sheet.ExpPercent);
        UpdateRebirthEmblem();
        UpdateExpBar();
    }

    private const float RebirthEmblemSize = 30f;
    private static readonly Vector2 RebirthEmblemPos = new(72f, 4f);
    private RebirthEmblem? _rebirthEmblem;

    private void UpdateRebirthEmblem()
    {
        if (_rebirthEmblem == null || !GodotObject.IsInstanceValid(_rebirthEmblem)) return;
        bool shown = !Platform.TouchUi && RebirthProgress.Shown(Sheet.Level, Sheet.RebirthLevel);
        _rebirthEmblem.Visible = shown;
        if (!shown) return;
        _rebirthEmblem.Set(Sheet.RebirthLevel,
            RebirthProgress.Fraction(Sheet.Level, Sheet.ExpPercent, Sheet.RebirthLevel),
            RebirthProgress.Ready(Sheet.Level, Sheet.ExpPercent, Sheet.RebirthLevel),
            RebirthProgress.Tooltip(Sheet.Level, Sheet.ExpPercent, Sheet.RebirthLevel, Sheet.RebirthBonusAtRow));
    }

    private void OnExpChange(long exp)
    {
        long gained = exp - Sheet.Exp;
        Sheet.ApplyExp(exp);
        if (gained > 0) Floaters?.Exp(gained);
        LogExpDelta(gained);
        RefreshStatsUI();
    }

    private void OnLevelChange(int level, int statPoints, int skillPool, long maxExp, long exp, int maxHp, int hp, int maxMp, int mp)
    {
        int oldLevel = Sheet.Level;
        long oldExp = Sheet.Exp, oldMax = Sheet.MaxExp;
        bool dingedUp = Sheet.ApplyLevel(level, statPoints, exp, maxExp);
        if (oldLevel > 0 && level != oldLevel)
            LogExpDelta(level > oldLevel ? exp + (oldMax - oldExp) : exp - oldExp - maxExp);
        Vitals.Seed(hp, maxHp, mp, maxMp);
        _hpBar?.Set(Vitals.Hp, Vitals.MaxHp);
        _mpBar?.Set(Vitals.Mp, Vitals.MaxMp);
        SyncMasteryPool(skillPool);
        RefreshStatsUI();
        RefreshInventoryFooter();
        if (dingedUp)
        {
            PlayLevelUp(_myId, Net.I.LastEnter.Nation);
        }
        RefreshSelfClanLine();
        NoticeNoahGraduation(oldLevel, level);
    }

    private void OnPeerLevelChange(int charId, int level)
    {
        if (!_ents.TryGetValue(charId, out var e)) return;
        bool dingedUp = level > e.Level;
        e.Level = level;
        RefreshClanLine(charId);
        if (dingedUp) PlayLevelUp(charId, e.Nation);
    }

    private void PlayLevelUp(int charId, int nation)
    {
        SpawnFxOn(charId, nation == Nations.Karus ? LevelUpFxKarus : LevelUpFxElMorad, 0f);
        Audio.Play(Sfx.LevelUp(nation), WorldPosOf(charId) ?? _self.GlobalPosition);
    }

    private void OnLoyaltyChange(int np, int monthly)
    {
        int gained = np - Sheet.Np;
        int? previousMonthly = Sheet.MonthlyNp;
        Sheet.ApplyLoyalty(np, monthly);
        if (Sheet.Level <= 0) { RefreshStatsUI(); return; }
        if (gained > 0) CombatLogAdd(WithAmount(TextEarnedNp, "Earned %d national points.", gained), CombatLogKind.National);
        else if (gained < 0) CombatLogAdd(WithAmount(TextLostNp, "You have lost %d country CONT.", -gained), CombatLogKind.National);
        int ladder = previousMonthly is { } before ? monthly - before : 0;
        if (ladder > 0) CombatLogAdd(WithAmount(TextEarnedLadder, "Received %d ladder points.", ladder), CombatLogKind.National);
        else if (ladder < 0) CombatLogAdd(WithAmount(TextLostLadder, "You have lost %d ladder points.", -ladder), CombatLogKind.National);
        RefreshStatsUI();
    }

    private void OnMannerChange(int manner)
    {
        int gained = manner - Sheet.Manner;
        Sheet.SetManner(manner);
        if (gained > 0) CombatLogAdd(WithAmount(TextEarnedManner, "Earned %d manner points.", gained), CombatLogKind.National);
        RefreshStatsUI();
    }

    private static string ClassName(int cls) => CharacterClassCatalog.DisplayName(cls);
}
