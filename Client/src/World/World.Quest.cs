using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private readonly List<QuestEntry> _quests = new();

    private int QuestStateOf(int questId)
    {
        int idx = _quests.FindIndex(q => q.QuestId == questId);
        return idx >= 0 ? _quests[idx].State : 0;
    }
    private readonly Dictionary<int, ushort[]> _questKills = new();
    private Dictionary<(int Quest, string Objective), int> _questProgressSeen = new();
    private readonly Dictionary<int, QuestObjectives> _questObjectives = new();
    private readonly Dictionary<int, QuestStrings> _questStrings = new();

    private const int QuestFilterAvailable = 0;
    private const int QuestFilterActive = 1;
    private const int QuestFilterCompleted = 2;
    private const int QuestStateActive = 1;
    private const int QuestStateCompleted = 2;
    private const int QuestStateReadyToTurnIn = 3;
    private const int QuestStateAbandoned = 4;

    private const float QuestDetailWidth = 244f;
    private const float QuestListWidth = 408f;

    private VBoxContainer _questsContent = null!;
    private VBoxContainer _questListBox = null!;
    private ScrollContainer _questListScroll = null!;
    private string _questSearch = "";
    private bool _questScrollTop;
    private int _questFilter = QuestFilterActive;
    private QuestData.Kind? _questKind;
    private int _questSelected = -1;
    private readonly HashSet<int> _questTracked = new();
    private readonly Dictionary<string, Button> _questSubBtns = new();
    private readonly Dictionary<string, Button> _questKindBtns = new();

    private Label _questDetailTitle = null!, _questDetailSub = null!, _questDetailFacts = null!;
    private RichTextLabel _questDetailDesc = null!;
    private VBoxContainer _questObjectiveBox = null!;
    private readonly HashSet<int> _questTargetNpcs = new();
    private Label _questObjectiveTitle = null!;
    private Label _questRewardTitle = null!;
    private HFlowContainer _questRewardBox = null!;
    private Label _questRewardOptionTitle = null!;
    private HBoxContainer _questRewardOptionBox = null!;
    private Control _questDetailBody = null!;
    private Label _questDetailEmpty = null!;
    private Button _questAbandonBtn = null!, _questTrackBtn = null!, _questCompleteBtn = null!;

    private CanvasLayer _trackerLayer = null!;
    private PanelContainer _trackerPanel = null!;
    private VBoxContainer _trackerBox = null!;
    private static int TrackerPageSize => Platform.Pick(4, 1);
    private const int TrackerShortMax = 34;
    private static float TrackerPlatePadX => Platform.Pick(16f, 26f);
    private static float TrackerPlatePadY => Platform.Pick(8f, 14f);
    private int _trackerPage;
    private int _trackerPageCount = 1;
    private const string TrackerLayoutId = "quest_tracker";
    private const float TrackerHeaderHeight = 24f;
    private const float TrackerBarHeight = 3f;
    private PanelContainer? _trackerHeader;
    private Label _trackerCount = null!;
    private Button _trackerFold = null!;

    private readonly record struct TrackerGoal(string Text, int Have, int Need);

    private void QuestInit()
    {
        BuildQuestLog();
        BuildQuestTracker();

        Net.I.QuestLogEvent += OnQuestLog;
        Net.I.QuestStateEvent += OnQuestState;
        Net.I.QuestKillCountsEvent += OnQuestKillCounts;
        Net.I.QuestObjectivesEvent += OnQuestObjectives;
        Net.I.QuestStringsEvent += OnQuestStrings;
        Net.I.QuestViewEvent += OnQuestView;
        Net.I.QuestTargetEvent += OnQuestTarget;
        Net.I.QuestReceiptEvent += OnQuestReceipt;
        Net.I.QuestKillUpdateEvent += OnQuestKillUpdate;
        Net.I.QuestRewardRefusedEvent += OnQuestRewardRefused;

        Net.I.SendQuestLogRequest();
    }

    private void QuestDispose()
    {
        Net.I.QuestLogEvent -= OnQuestLog;
        Net.I.QuestStateEvent -= OnQuestState;
        Net.I.QuestKillCountsEvent -= OnQuestKillCounts;
        Net.I.QuestObjectivesEvent -= OnQuestObjectives;
        Net.I.QuestStringsEvent -= OnQuestStrings;
        Net.I.QuestViewEvent -= OnQuestView;
        Net.I.QuestTargetEvent -= OnQuestTarget;
        Net.I.QuestReceiptEvent -= OnQuestReceipt;
        Net.I.QuestKillUpdateEvent -= OnQuestKillUpdate;
        Net.I.QuestRewardRefusedEvent -= OnQuestRewardRefused;
    }

    private const int TextRewardWeight = 7432;
    private const int TextRewardCoins = 7433;
    private const int TextRewardBagFull = 7434;

    private void OnQuestRewardRefused(QuestRewardRefusal reason) => Notice.Show(this, reason switch
    {
        QuestRewardRefusal.WeightExceeded => ItemData.Text(TextRewardWeight,
            "You are not eligible for a reward as your weight limitation has been exceeded."),
        QuestRewardRefusal.CoinsExceeded => ItemData.Text(TextRewardCoins,
            "You are not eligible for a reward as your Noah limitation has been exceeded."),
        _ => ItemData.Text(TextRewardBagFull, "You are not eligible for a reward as your bag is full."),
    });

    private void BuildQuestLog()
    {
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 7);
        _questsContent = root;

        var statusBar = new HBoxContainer();
        statusBar.AddThemeConstantOverride("separation", 1);
        root.AddChild(statusBar);
        var statusGroup = new ButtonGroup();
        foreach (var (label, filter) in new[]
                 {
                     ("In Progress", QuestFilterActive),
                     ("Available", QuestFilterAvailable),
                     ("Completed", QuestFilterCompleted),
                 })
        {
            int f = filter;
            var b = UiTheme.TopTabButton(label, 13);
            b.ButtonGroup = statusGroup;
            b.Pressed += () => SetQuestFilter(f);
            _questSubBtns[label] = b;
            statusBar.AddChild(b);
        }

        var kindBar = new HBoxContainer();
        kindBar.AddThemeConstantOverride("separation", 1);
        root.AddChild(kindBar);
        var kindGroup = new ButtonGroup();
        foreach (var (label, kind) in new (string, QuestData.Kind?)[]
                 {
                     ("All", null),
                     ("Story", QuestData.Kind.Story),
                     ("Hunt", QuestData.Kind.Hunt),
                     ("Delivery", QuestData.Kind.Delivery),
                 })
        {
            var k = kind;
            var b = UiTheme.TopTabButton(label, 11);
            b.ButtonGroup = kindGroup;
            b.Pressed += () => SetQuestKind(k);
            _questKindBtns[label] = b;
            kindBar.AddChild(b);
        }
        var search = new LineEdit
        {
            PlaceholderText = "Search quests",
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        search.AddThemeFontSizeOverride("font_size", 12);
        search.TextChanged += text =>
        {
            _questSearch = text;
            _questScrollTop = true;
            RefreshQuestLog();
        };
        kindBar.AddChild(search);

        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 10);
        root.AddChild(body);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(QuestListWidth, 372),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body.AddChild(scroll);
        _questListScroll = scroll;
        _questListBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _questListBox.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(_questListBox);

        body.AddChild(BuildQuestDetail());

        var footer = UiTheme.Text(
            "Click a quest for its details  ·  tick its box to track it on screen  ·  click a pinned objective to see where it is",
            11, UiTheme.TextDim);
        root.AddChild(footer);

        SetQuestFilter(QuestFilterActive);
        SetQuestKind(null);
    }

    private Control BuildQuestDetail()
    {
        var panel = UiTheme.Section();
        panel.CustomMinimumSize = new Vector2(QuestDetailWidth + 24f, 0);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 7);
        panel.AddChild(box);

        _questDetailEmpty = UiTheme.Text("Select a quest to see its details.", 12, UiTheme.TextDim,
            HorizontalAlignment.Center);
        _questDetailEmpty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _questDetailEmpty.CustomMinimumSize = new Vector2(QuestDetailWidth, 0);
        box.AddChild(_questDetailEmpty);

        var detail = new VBoxContainer { Visible = false };
        detail.AddThemeConstantOverride("separation", 7);
        box.AddChild(detail);
        _questDetailBody = detail;

        _questDetailTitle = UiTheme.Text("", 16, UiTheme.GoldBright);
        _questDetailTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _questDetailTitle.CustomMinimumSize = new Vector2(QuestDetailWidth, 0);
        detail.AddChild(_questDetailTitle);

        _questDetailSub = UiTheme.Text("", 11, UiTheme.TextDim);
        detail.AddChild(_questDetailSub);

        _questDetailFacts = UiTheme.Text("", 11, UiTheme.TextLo);
        _questDetailFacts.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _questDetailFacts.CustomMinimumSize = new Vector2(QuestDetailWidth, 0);
        detail.AddChild(_questDetailFacts);

        _questDetailDesc = QuestParagraph("", UiTheme.TextLo, QuestDetailWidth);
        _questDetailDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _questDetailDesc.CustomMinimumSize = new Vector2(QuestDetailWidth, 0);
        detail.AddChild(_questDetailDesc);

        detail.AddChild(new HSeparator());
        _questObjectiveTitle = UiTheme.SectionTitle("Objectives");
        detail.AddChild(_questObjectiveTitle);
        _questObjectiveBox = new VBoxContainer();
        _questObjectiveBox.AddThemeConstantOverride("separation", 3);
        detail.AddChild(_questObjectiveBox);

        _questRewardTitle = UiTheme.SectionTitle("Rewards");
        detail.AddChild(_questRewardTitle);
        _questRewardBox = new HFlowContainer { CustomMinimumSize = new Vector2(QuestDetailWidth, 0) };
        _questRewardBox.AddThemeConstantOverride("h_separation", 5);
        _questRewardBox.AddThemeConstantOverride("v_separation", 5);
        detail.AddChild(_questRewardBox);
        _questRewardOptionTitle = UiTheme.SectionTitle("Choose one");
        detail.AddChild(_questRewardOptionTitle);
        _questRewardOptionBox = new HBoxContainer();
        _questRewardOptionBox.AddThemeConstantOverride("separation", 5);
        detail.AddChild(_questRewardOptionBox);

        detail.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 5);
        detail.AddChild(buttons);
        _questCompleteBtn = QuestActionButton("Turn in", "Talk to the quest NPC, then turn in your completed objectives");
        _questCompleteBtn.Pressed += CompleteSelectedQuest;
        buttons.AddChild(_questCompleteBtn);
        _questAbandonBtn = QuestActionButton("Abandon", "Give up this quest");
        _questAbandonBtn.Pressed += AbandonSelectedQuest;
        buttons.AddChild(_questAbandonBtn);
        _questTrackBtn = QuestActionButton("Track", "Show this quest in the on-screen tracker");
        _questTrackBtn.Pressed += ToggleTrackSelectedQuest;
        buttons.AddChild(_questTrackBtn);

        return panel;
    }

    private static Button QuestActionButton(string text, string tooltip)
    {
        var b = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = tooltip,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        b.AddThemeFontSizeOverride("font_size", 12);
        return b;
    }

    private void SetQuestFilter(int filter)
    {
        _questFilter = filter;
        string label = filter switch
        {
            QuestFilterCompleted => "Completed",
            QuestFilterAvailable => "Available",
            _ => "In Progress",
        };
        if (_questSubBtns.TryGetValue(label, out var b)) b.ButtonPressed = true;
        RefreshQuestLog();
    }

    private void SetQuestKind(QuestData.Kind? kind)
    {
        _questKind = kind;
        string label = kind?.ToString() ?? "All";
        if (_questKindBtns.TryGetValue(label, out var b)) b.ButtonPressed = true;
        RefreshQuestLog();
    }

    private bool QuestInFilter(int state) => _questFilter switch
    {
        QuestFilterActive => state is QuestStateActive or QuestStateReadyToTurnIn,
        QuestFilterCompleted => state == QuestStateCompleted,
        QuestFilterAvailable => state == QuestStateAbandoned,
        _ => false,
    };

    private List<(int QuestId, int State)> QuestsInView()
    {
        var rows = new List<(int, int)>();
        foreach (var q in _quests)
            if (QuestInFilter(q.State) && (Config.LegacyQuestFallback || _questViews.ContainsKey(q.QuestId)))
                rows.Add((q.QuestId, q.State));

        if (_questFilter == QuestFilterAvailable)
        {
            foreach (int id in QuestData.Startable(QuestStateOf, Sheet.Level, _selfClass, Net.I.LastEnter.Nation))
                if (!_questViews.ContainsKey(id)) rows.Add((id, 0));
            foreach (var view in _questViews.Values)
                if (view.State == QuestViewState.Available) rows.Add((view.QuestId, 0));
        }

        if (_questKind is { } kind)
            rows.RemoveAll(r => QuestFacts(r.Item1).Kind != kind);

        if (_questSearch.Length > 0)
        {
            string selfName = Net.I.LastEnter.Name ?? "";
            rows.RemoveAll(r => !QuestJournalRules.Matches(QuestName(r.Item1, selfName), _questSearch));
        }

        rows.Sort((a, b) => QuestJournalRules.Compare(JournalRow(a.Item1, a.Item2), JournalRow(b.Item1, b.Item2)));
        return rows;
    }

    private QuestJournalRules.Row JournalRow(int questId, int state)
    {
        var facts = QuestFacts(questId);
        return new QuestJournalRules.Row(questId, state, facts.Zone, facts.Level);
    }

    private void RefreshQuestLog()
    {
        if (_questListBox == null) return;
        int keepScroll = _questScrollTop ? 0 : _questListScroll.ScrollVertical;
        _questScrollTop = false;
        foreach (var c in _questListBox.GetChildren()) { _questListBox.RemoveChild(c); c.QueueFree(); }
        string selfName = Net.I.LastEnter.Name ?? "";

        var rows = QuestsInView();
        var zoneCounts = new Dictionary<int, int>();
        foreach (var (questId, _) in rows)
        {
            int zone = QuestFacts(questId).Zone;
            zoneCounts[zone] = zoneCounts.GetValueOrDefault(zone) + 1;
        }
        int lastZone = int.MinValue;
        foreach (var (questId, state) in rows)
        {
            int zone = QuestFacts(questId).Zone;
            if (zone != lastZone)
            {
                lastZone = zone;
                _questListBox.AddChild(QuestZoneHeader(zone, zoneCounts[zone]));
            }
            _questListBox.AddChild(BuildQuestRow(questId, state, selfName));
        }

        if (rows.Count == 0)
        {
            var hint = UiTheme.Text(
                _questSearch.Length > 0 ? "No quest matches your search." : _questFilter switch
                {
                    QuestFilterCompleted => "Nothing finished yet.",
                    QuestFilterAvailable => "No quests are open to you right now.",
                    _ => "No quests in progress.\nTalk to NPCs to find some.",
                },
                13, UiTheme.TextLo, HorizontalAlignment.Center);
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hint.CustomMinimumSize = new Vector2(QuestListWidth - 16f, 0);
            _questListBox.AddChild(hint);
        }

        if (_questSelected >= 0 && rows.FindIndex(r => r.QuestId == _questSelected) < 0)
            _questSelected = -1;

        RefreshQuestDetail();
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(_questListScroll)) _questListScroll.ScrollVertical = keepScroll;
        }).CallDeferred();
    }

    private Control QuestZoneHeader(int zone, int count)
    {
        var panel = new PanelContainer();
        var band = new StyleBoxFlat { BgColor = new Color(0.105f, 0.108f, 0.125f, 0.95f) };
        band.SetCornerRadiusAll(2);
        band.ContentMarginLeft = 8;
        band.ContentMarginRight = 8;
        band.ContentMarginTop = band.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", band);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        panel.AddChild(row);
        var caret = UiTheme.Text("▼", 8, new Color(UiTheme.TextLo, 0.7f));
        caret.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(caret);
        row.AddChild(UiTheme.Text(QuestJournalRules.GroupTitle(QuestZoneName(zone), count), 12, UiTheme.TextLo));
        return panel;
    }

    private static string QuestZoneName(int zone)
    {
        if (zone == 0) return "Anywhere";
        foreach (var z in ZoneCatalog.All) if (z.Id == zone) return z.Name;
        return $"Zone {zone}";
    }

    private Control BuildQuestRow(int questId, int state, string selfName)
    {
        bool selected = questId == _questSelected;
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", UiTheme.ListRow(selected));
        panel.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                SelectQuest(questId);
        };

        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        panel.AddChild(row);

        var glyph = UiTheme.Text(
            state == QuestStateReadyToTurnIn ? "✓" : state == QuestStateCompleted ? "✓" : "◆",
            12, StateColor(state), HorizontalAlignment.Center);
        glyph.CustomMinimumSize = new Vector2(16, 0);
        glyph.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(glyph);

        var text = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        text.AddThemeConstantOverride("separation", -1);
        row.AddChild(text);

        var name = UiTheme.Text(QuestName(questId, selfName), 13,
            selected ? UiTheme.GoldBright : StateColor(state));
        name.ClipText = true;
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        text.AddChild(name);

        var facts = QuestFacts(questId);
        var status = UiTheme.Text(QuestJournalRules.StatusLine(facts.Level, QuestStatusText(questId, state)), 10,
            UiTheme.TextDim);
        status.ClipText = true;
        status.MouseFilter = Control.MouseFilterEnum.Ignore;
        text.AddChild(status);

        var (current, target) = QuestProgress(questId);
        var progress = UiTheme.Text(target > 0 ? $"{current} / {target}" : "", 11,
            target > 0 && current >= target ? UiTheme.Good : UiTheme.Gold, HorizontalAlignment.Right);
        progress.CustomMinimumSize = new Vector2(48, 0);
        progress.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(progress);

        var track = UiTheme.FlatCheck("", _questTracked.Contains(questId));
        track.TooltipText = "Track this quest on screen";
        track.Disabled = state is not (QuestStateActive or QuestStateReadyToTurnIn);
        track.Toggled += on => SetQuestTracked(questId, on);
        row.AddChild(track);

        return panel;
    }

    private string QuestStatusText(int questId, int state) =>
        _questViews.TryGetValue(questId, out var view) ? view.StateLabel : StateName(state);

    private (int Current, int Target) QuestProgress(int questId)
    {
        if (_questObjectives.TryGetValue(questId, out var declared))
            return declared.Progress(_questKills.GetValueOrDefault(questId) ?? []);
        var groups = QuestGroups(questId);
        if (groups.Length == 0) return (0, 0);
        _questKills.TryGetValue(questId, out var counts);
        int current = 0, target = 0;
        for (int g = 0; g < groups.Length; g++)
        {
            target += groups[g].Count;
            current += Mathf.Min(counts != null && g < counts.Length ? counts[g] : 0, groups[g].Count);
        }
        return (current, target);
    }

    private QuestData.KillGroup[] QuestGroups(int questId) =>
        _questObjectives.TryGetValue(questId, out var declared)
            ? [.. declared.Groups.Select(group => new QuestData.KillGroup(group.Monsters, group.Count))]
            : QuestData.Groups(questId);

    private QuestData.Facts QuestFacts(int questId)
    {
        if (_questViews.TryGetValue(questId, out var view))
            return new QuestData.Facts(0, 0, 0, view.ZoneId, 0, 0,
                view.Objectives.Groups.Length > 0 ? QuestData.Kind.Hunt
                : view.Transfers.Any(t => t.Take) ? QuestData.Kind.Delivery : QuestData.Kind.Story);
        var facts = QuestData.Get(questId);
        return _questObjectives.ContainsKey(questId) ? facts with { Kind = QuestData.Kind.Hunt } : facts;
    }

    private void SelectQuest(int questId)
    {
        _questSelected = questId;
        Audio.PlayUi(Sfx.UiButton);
        RefreshQuestLog();
    }

    private void SetQuestTracked(int questId, bool tracked)
    {
        bool changed = tracked ? _questTracked.Add(questId) : _questTracked.Remove(questId);
        if (!changed) return;
        RefreshTracker();
        RefreshQuestDetail();
    }

    private void ToggleTrackSelectedQuest()
    {
        if (_questSelected < 0) return;
        SetQuestTracked(_questSelected, !_questTracked.Contains(_questSelected));
        RefreshQuestLog();
    }

    private void AbandonSelectedQuest()
    {
        if (_questSelected < 0) return;
        Net.I.SendQuestAbandon(_questSelected);
    }

    private void CompleteSelectedQuest()
    {
        if (_questSelected < 0 || QuestStateOf(_questSelected) != QuestStateReadyToTurnIn) return;
        Net.I.SendQuestComplete(_questSelected);
    }

    private void RefreshQuestDetail()
    {
        if (_questDetailBody == null) return;
        bool has = _questSelected >= 0;
        _questDetailBody.Visible = has;
        _questDetailEmpty.Visible = !has;
        if (!has) return;

        string selfName = Net.I.LastEnter.Name ?? "";
        int questId = _questSelected;
        int state = QuestStateOf(questId);
        var facts = QuestFacts(questId);

        _questDetailTitle.Text = QuestName(questId, selfName);
        var sub = new List<string> { facts.Kind.ToString() };
        if (facts.Level > 0) sub.Add($"Lv. {facts.Level}");
        sub.Add(_questViews.TryGetValue(questId, out var view) ? view.StateLabel : StateName(state));
        _questDetailSub.Text = string.Join("  ·  ", sub);
        _questDetailFacts.Text = QuestPlaceFacts(questId, state, facts.Zone);
        _questDetailFacts.Visible = _questDetailFacts.Text.Length > 0;
        _questDetailDesc.Text = QuestMarkup.Rich(QuestJournalSource(questId, selfName), selfName);

        foreach (var c in _questObjectiveBox.GetChildren()) { _questObjectiveBox.RemoveChild(c); c.QueueFree(); }
        _questObjectiveTitle.Text = QuestObjectiveHeading(
            QuestGroups(questId).Length > 0, QuestHandIns(questId).Any());
        if (_questObjectives.TryGetValue(questId, out var objectives) && objectives.AnyWillDo)
            _questObjectiveBox.AddChild(QuestObjectiveRow("Complete any one:"));
        var kills = KillProgress(questId);
        for (var group = 0; group < kills.Count; group++)
        {
            bool pinned = _questViews.TryGetValue(questId, out var goals) && group < goals.Objectives.Groups.Length
                && goals.Objectives.Groups[group].HasTarget;
            var (mob, current, target) = kills[group];
            _questObjectiveBox.AddChild(QuestObjectiveTarget(
                QuestObjectiveProgress($"{mob} {current}/{target}", current, target, pinned), questId, group, pinned));
        }
        foreach (var want in QuestHandIns(questId))
        {
            int have = Mathf.Min(Inv.CountOf(want.ItemId), want.Count);
            _questObjectiveBox.AddChild(QuestObjectiveProgress(
                $"{ItemData.DisplayName(want.ItemId)} {have}/{want.Count}", have, want.Count, false));
        }
        if (_questObjectiveBox.GetChildCount() == 0)
            _questObjectiveBox.AddChild(QuestObjectiveRow(state switch
            {
                QuestStateReadyToTurnIn => "Ready to turn in",
                QuestStateCompleted => "You have finished this quest.",
                _ => QuestTalkToLine(questId, state),
            }));

        foreach (var c in _questRewardBox.GetChildren()) { _questRewardBox.RemoveChild(c); c.QueueFree(); }
        foreach (var c in _questRewardOptionBox.GetChildren()) { _questRewardOptionBox.RemoveChild(c); c.QueueFree(); }
        foreach (var reward in QuestRewards(questId))
            _questRewardBox.AddChild(QuestRewardTile(reward.ItemId, reward.Count));
        _questRewardTitle.Visible = _questRewardBox.GetChildCount() > 0;
        var options = _questViews.TryGetValue(questId, out var rewardView) ? rewardView.Options : [];
        var chosen = _questRewardSelection.Chosen(questId, out var pending) && options.Contains(pending);
        foreach (var option in chosen ? new[] { pending } : options)
            _questRewardOptionBox.AddChild(QuestRewardTile(option.DisplayItemId, option.Count));
        _questRewardOptionTitle.Visible = _questRewardOptionBox.GetChildCount() > 0;

        _questTrackBtn.Text = _questTracked.Contains(questId) ? "Untrack" : "Track";
        _questTrackBtn.Disabled = state is not (QuestStateActive or QuestStateReadyToTurnIn);
        _questAbandonBtn.Disabled = state is not (QuestStateActive or QuestStateReadyToTurnIn)
            || _questViews.TryGetValue(questId, out var autoView) && autoView.AutoAccepted;
        _questCompleteBtn.Disabled = _questViews.TryGetValue(questId, out var actionView)
            ? !actionView.CanClaim || actionView.Options.Length > 0
            : state != QuestStateReadyToTurnIn;
    }

    private string QuestPlaceFacts(int questId, int state, int zone)
    {
        var parts = new List<string>(2);
        if (zone != QuestJournalRules.AnywhereZone) parts.Add($"Area: {QuestZoneName(zone)}");
        var npcs = _questViews.TryGetValue(questId, out var view)
            ? new[] { view.NpcId } : QuestData.NpcsForState(questId, state);
        if (npcs.Length > 0 && npcs[0] > 0) parts.Add($"NPC: {NpcDisplayName(npcs[0])}");
        return string.Join("   ·   ", parts);
    }

    private string QuestTalkToLine(int questId, int state)
    {
        var npcs = _questViews.TryGetValue(questId, out var view)
            ? new[] { view.NpcId } : QuestData.NpcsForState(questId, state);
        if (npcs.Length == 0) return "See the quest text";
        string who = NpcDisplayName(npcs[0]);
        return state == 0 ? $"Ask {who} for this quest" : $"Report to {who}";
    }

    private Control QuestRewardTile(int itemId, int count)
    {
        var box = QuestItemHover(
            new VBoxContainer { TooltipText = HasItemCard(itemId) ? "" : ItemData.DisplayName(itemId) }, itemId);
        box.AddThemeConstantOverride("separation", 3);

        var tile = new PanelContainer { CustomMinimumSize = new Vector2(52, 52) };
        tile.AddThemeStyleboxOverride("panel", UiTheme.Slot());
        var icon = ItemData.Icon(itemId);
        if (icon != null)
        {
            var rect = new TextureRect
            {
                Texture = icon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            tile.AddChild(rect);
            UpgradeBadge.Show(tile, itemId);
        }
        else if (QuestVirtualIcon(itemId) is { } virtualIcon)
        {
            tile.AddChild(new TextureRect
            {
                Texture = UiIcons.Get(virtualIcon),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SelfModulate = UiTheme.Gold,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            box.TooltipText = $"{QuestRewardName(itemId)}  {count:n0}";
        }
        else
        {
            var glyph = UiTheme.Text("?", 15, UiTheme.TextLo, HorizontalAlignment.Center);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            tile.AddChild(glyph);
        }
        box.AddChild(tile);

        var caption = UiTheme.Text(QuestJournalRules.CompactCount(count), 10, UiTheme.TextHi, HorizontalAlignment.Center);
        caption.CustomMinimumSize = new Vector2(52, 0);
        caption.ClipText = true;
        box.AddChild(caption);
        return box;
    }

    private static string? QuestVirtualIcon(int itemId) => itemId switch
    {
        QuestData.CoinItemId => "system/coins",
        QuestData.ExpItemId => "system/level",
        QuestData.LadderPointItemId => "system/medal",
        QuestData.JobChangeItemId => "system/star",
        _ => null,
    };

    private static Control QuestObjectiveProgress(string text, int current, int target, bool pinned)
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        box.AddThemeConstantOverride("separation", 2);
        var row = QuestObjectiveRow(text);
        row.MouseFilter = Control.MouseFilterEnum.Pass;
        if (pinned)
            row.AddChild(new TextureRect
            {
                Texture = UiIcons.Get("system/pin"),
                CustomMinimumSize = new Vector2(QuestPinSize, QuestPinSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SelfModulate = UiTheme.Gold,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        box.AddChild(row);
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = QuestJournalRules.Fraction(current, target),
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, QuestObjectiveBarHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bar.AddThemeStyleboxOverride("background", UiTheme.MeterTrack());
        bar.AddThemeStyleboxOverride("fill", UiTheme.MeterFill(current >= target ? UiTheme.Good : UiTheme.Gold));
        box.AddChild(bar);
        return box;
    }

    private const float QuestPinSize = 12f;
    private const float QuestObjectiveBarHeight = 3f;

    private static Control QuestObjectiveRow(string text)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var bullet = UiTheme.Text("◆", 10, UiTheme.Gold);
        bullet.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(bullet);
        var label = UiTheme.Text(text, 12, UiTheme.TextLo);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(QuestDetailWidth - 20f, 0);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(label);
        return row;
    }

    private void BuildQuestTracker()
    {
        _trackerLayer = new CanvasLayer { Layer = 65 };
        AddChild(_trackerLayer);
        PluginHudSeam(_trackerLayer, LibreKO.Plugins.HudPart.QuestTracker);
        _trackerPanel = new PanelContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(MiniMap.SquareSize, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _trackerPanel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _trackerLayer.AddChild(_trackerPanel);
        _trackerBox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(MiniMap.SquareSize, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _trackerBox.AddThemeConstantOverride("separation", 5);
        _trackerPanel.AddChild(_trackerBox);
        if (Platform.TouchUi)
            HudPlacement.QuestTracker.ApplyTo(_trackerPanel);
        else
        {
            _trackerHeader = BuildTrackerHeader();
            HudLayout.Attach(_trackerPanel, TrackerLayoutId, _trackerHeader, TrackerDefaultPosition);
        }
        RefreshTracker();
    }

    private Vector2 TrackerDefaultPosition()
    {
        var viewport = _trackerPanel.GetViewportRect().Size;
        return new Vector2(viewport.X - HudAnchor.Edge - _trackerPanel.Size.X,
            Mathf.Max(HudAnchor.Edge, (viewport.Y - _trackerPanel.Size.Y) * 0.5f));
    }

    private PanelContainer BuildTrackerHeader()
    {
        var header = new PanelContainer
        {
            CustomMinimumSize = new Vector2(MiniMap.SquareSize, TrackerHeaderHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = "Drag to move",
        };
        header.AddThemeStyleboxOverride("panel", UiTheme.HeaderBand(2));
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        header.AddChild(row);
        var title = TrackerText("Quests", 12, UiTheme.GoldBright);
        title.VerticalAlignment = VerticalAlignment.Center;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(title);
        _trackerCount = TrackerText("", 11, UiTheme.TextLo);
        _trackerCount.VerticalAlignment = VerticalAlignment.Center;
        _trackerCount.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _trackerCount.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(_trackerCount);
        _trackerFold = UiTheme.IconButton(Config.QuestTrackerCollapsed ? "▸" : "▾", "Collapse or expand");
        _trackerFold.CustomMinimumSize = new Vector2(22, 20);
        _trackerFold.Pressed += () =>
        {
            Config.SetQuestTrackerCollapsed(!Config.QuestTrackerCollapsed);
            _trackerFold.Text = Config.QuestTrackerCollapsed ? "▸" : "▾";
            RefreshTracker();
        };
        row.AddChild(_trackerFold);
        return header;
    }

    private List<QuestEntry> TrackedQuests()
    {
        var active = new List<QuestEntry>();
        foreach (var q in _quests)
        {
            if (!Config.LegacyQuestFallback && !_questViews.ContainsKey(q.QuestId)) continue;
            if (q.State != QuestStateActive && q.State != QuestStateReadyToTurnIn) continue;
            if (_questTracked.Count > 0 && !_questTracked.Contains(q.QuestId)) continue;
            active.Add(q);
        }
        return active;
    }

    private List<LibreKO.Plugins.GameQuestTrack> TrackedQuestsForPlugins()
    {
        string selfName = Net.I.LastEnter.Name ?? "";
        var list = new List<LibreKO.Plugins.GameQuestTrack>();
        foreach (var q in TrackedQuests())
        {
            bool done = q.State == QuestStateReadyToTurnIn;
            var lines = done ? new List<string> { "Ready to turn in" } : KillProgressLines(q.QuestId);
            if (lines.Count == 0) lines.Add(ShortObjective(q.QuestId, selfName));
            list.Add(new LibreKO.Plugins.GameQuestTrack(q.QuestId, QuestName(q.QuestId, selfName), done, lines));
        }
        return list;
    }

    private void RefreshTracker()
    {
        PluginNotifyQuests();
        if (_trackerBox == null) return;
        foreach (var c in _trackerBox.GetChildren())
        {
            _trackerBox.RemoveChild(c);
            if (c != _trackerHeader) c.QueueFree();
        }
        string selfName = Net.I.LastEnter.Name ?? "";

        var active = TrackedQuests();

        ReportQuestProgress();

        _trackerPageCount = Math.Max(1, (active.Count + TrackerPageSize - 1) / TrackerPageSize);
        _trackerPage = Math.Clamp(_trackerPage, 0, _trackerPageCount - 1);

        if (_trackerHeader != null)
        {
            _trackerCount.Text = $"({active.Count})";
            _trackerBox.AddChild(_trackerHeader);
            if (Config.QuestTrackerCollapsed)
            {
                _trackerPanel.Visible = active.Count > 0;
                return;
            }
        }

        int start = _trackerPage * TrackerPageSize;
        int end = Math.Min(active.Count, start + TrackerPageSize);
        for (int i = start; i < end; i++)
            _trackerBox.AddChild(Platform.TouchUi
                ? BuildTouchTrackerQuest(active[i], selfName)
                : BuildTrackerQuest(active[i], selfName));

        if (!Platform.TouchUi && _trackerPageCount > 1)
        {
            _trackerBox.AddChild(BuildTrackerPager());
        }
        _trackerPanel.Visible = active.Count > 0;
        Callable.From(FitTracker).CallDeferred();
    }

    private void FitTracker()
    {
        if (!GodotObject.IsInstanceValid(_trackerPanel) || _trackerPanel.GetParent() != _trackerLayer) return;
        if (Platform.TouchUi) HudPlacement.QuestTracker.ApplyTo(_trackerPanel);
        else _trackerPanel.ResetSize();
    }

    private Control BuildTouchTrackerQuest(QuestEntry q, string selfName)
    {
        bool done = q.State == QuestStateReadyToTurnIn;
        int questId = q.QuestId;

        var button = new Button
        {
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
        };
        var plate = new StyleBoxFlat
        {
            BgColor = new Color(0.020f, 0.023f, 0.030f, 0.72f),
            BorderColor = new Color(done ? UiTheme.Good : UiTheme.Edge, 0.34f),
        };
        plate.SetCornerRadiusAll(5);
        plate.SetBorderWidthAll(1);
        plate.ContentMarginLeft = plate.ContentMarginRight = TrackerPlatePadX;
        plate.ContentMarginTop = plate.ContentMarginBottom = TrackerPlatePadY;
        var hover = (StyleBoxFlat)plate.Duplicate();
        hover.BgColor = new Color(0.055f, 0.060f, 0.070f, 0.86f);
        button.AddThemeStyleboxOverride("normal", plate);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("focus", plate);

        var label = TrackerText(done ? "Ready to turn in" : ShortObjective(questId, selfName),
                                13, done ? UiTheme.Good : Colors.White);
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        button.AddChild(label);
        button.CustomMinimumSize = label.GetMinimumSize()
            + new Vector2(TrackerPlatePadX * 2f, TrackerPlatePadY * 2f);

        button.Pressed += () =>
        {
            ShowMainWindow("Quests");
            SelectQuest(questId);
        };
        return button;
    }

    private string ShortObjective(int questId, string selfName)
    {
        var kills = KillProgressLines(questId);
        if (kills.Count > 0) return Clip($"Kill {kills[0]}");
        return Clip(QuestGoalLines(questId, selfName)[0]);
    }

    private List<string> QuestGoalLines(int questId, string selfName)
    {
        var lines = new List<string>();
        foreach (var want in QuestHandIns(questId))
            lines.Add($"Bring {ItemData.DisplayName(want.ItemId)} "
                      + $"{Mathf.Min(Inv.CountOf(want.ItemId), want.Count)}/{want.Count}");
        if (lines.Count > 0) return lines;

        var npcs = _questViews.TryGetValue(questId, out var view)
            ? new[] { view.NpcId } : QuestData.NpcsForState(questId, QuestStateActive);
        if (npcs.Length > 0)
        {
            lines.Add($"Talk to {NpcDisplayName(npcs[0])}");
            return lines;
        }

        string text = QuestJournal(questId, selfName);
        if (string.IsNullOrWhiteSpace(text)) text = QuestName(questId, selfName);
        int newline = text.IndexOf('\n');
        lines.Add((newline >= 0 ? text[..newline] : text).Trim());
        return lines;
    }

    private static string NpcDisplayName(int npcId) =>
        GameData.I != null ? GameData.I.TalkNpcName(npcId) : $"#{npcId}";

    private static string Clip(string text)
    {
        int newline = text.IndexOf('\n');
        if (newline >= 0) text = text[..newline].TrimEnd();
        return text.Length > TrackerShortMax
            ? text[..TrackerShortMax].TrimEnd() + "\u2026"
            : text;
    }

    private Control BuildTrackerQuest(QuestEntry q, string selfName)
    {
        var box = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(MiniMap.SquareSize, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        box.AddThemeConstantOverride("separation", 2);

        var titlePanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(MiniMap.SquareSize, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        titlePanel.AddThemeStyleboxOverride("panel", UiTheme.Row(q.State == 3, true));
        int questId = q.QuestId;
        titlePanel.MouseFilter = Control.MouseFilterEnum.Stop;
        titlePanel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        titlePanel.TooltipText = "Open in the quest journal";
        titlePanel.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            ShowMainWindow("Quests");
            SelectQuest(questId);
        };
        box.AddChild(titlePanel);

        var titleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titleRow.AddThemeConstantOverride("separation", 6);
        titlePanel.AddChild(titleRow);

        var icon = TrackerText(q.State == 3 ? "✓" : "◇", 11,
            q.State == 3 ? UiTheme.Good : UiTheme.GoldBright, HorizontalAlignment.Center);
        icon.CustomMinimumSize = new Vector2(14, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        titleRow.AddChild(icon);

        var title = TrackerText(QuestName(q.QuestId, selfName),
            12, q.State == 3 ? UiTheme.Good : Colors.White);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.CustomMinimumSize = new Vector2(MiniMap.SquareSize - 38f, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        titleRow.AddChild(title);

        var objectiveBox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(MiniMap.SquareSize, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        objectiveBox.AddThemeConstantOverride("separation", 1);
        box.AddChild(objectiveBox);

        if (q.State == 3)
        {
            objectiveBox.AddChild(TrackerObjective("Ready to turn in", UiTheme.Good));
        }
        else
        {
            foreach (var goal in TrackerGoals(q.QuestId, selfName))
            {
                if (string.IsNullOrWhiteSpace(goal.Text)) continue;
                objectiveBox.AddChild(TrackerObjective(goal.Text, goal.Need > 0 && goal.Have >= goal.Need ? UiTheme.Good : Colors.White));
                if (goal.Need > 0) objectiveBox.AddChild(TrackerBar(goal.Have, goal.Need));
            }
        }

        return box;
    }

    private List<TrackerGoal> TrackerGoals(int questId, string selfName)
    {
        var goals = new List<TrackerGoal>();
        foreach (var (mob, current, target) in KillProgress(questId))
            goals.Add(new TrackerGoal($"Kill {mob} {current}/{target}", current, target));
        if (goals.Count > 0) return goals;
        foreach (var want in QuestHandIns(questId))
        {
            int have = Mathf.Min(Inv.CountOf(want.ItemId), want.Count);
            goals.Add(new TrackerGoal($"Bring {ItemData.DisplayName(want.ItemId)} {have}/{want.Count}", have, want.Count));
        }
        if (goals.Count > 0) return goals;
        foreach (var line in QuestGoalLines(questId, selfName))
            goals.Add(new TrackerGoal(line.StartsWith("Talk to") ? line : $"Note: {line}", 0, 0));
        return goals;
    }

    private static Control TrackerBar(int have, int need)
    {
        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", (int)TrackerPlatePadX / 2);
        margin.AddThemeConstantOverride("margin_right", (int)TrackerPlatePadX / 2);
        margin.AddChild(QuestHuntBar(have, need));
        margin.GetChild<Control>(0).CustomMinimumSize = new Vector2(0, TrackerBarHeight);
        return margin;
    }

    private Control BuildTrackerPager()
    {
        var pager = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(MiniMap.SquareSize, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        pager.AddThemeConstantOverride("separation", 5);

        var prev = UiTheme.IconButton("<", "Previous quest page");
        prev.CustomMinimumSize = new Vector2(28, 22);
        prev.AddThemeFontSizeOverride("font_size", 12);
        prev.Disabled = _trackerPage <= 0;
        prev.Pressed += () =>
        {
            _trackerPage = Math.Max(0, _trackerPage - 1);
            RefreshTracker();
        };
        pager.AddChild(prev);

        var page = TrackerText($"{_trackerPage + 1}/{_trackerPageCount}", 10, UiTheme.TextHi,
            HorizontalAlignment.Center);
        page.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        page.VerticalAlignment = VerticalAlignment.Center;
        page.MouseFilter = Control.MouseFilterEnum.Ignore;
        pager.AddChild(page);

        var next = UiTheme.IconButton(">", "Next quest page");
        next.CustomMinimumSize = new Vector2(28, 22);
        next.AddThemeFontSizeOverride("font_size", 12);
        next.Disabled = _trackerPage >= _trackerPageCount - 1;
        next.Pressed += () =>
        {
            _trackerPage = Math.Min(_trackerPageCount - 1, _trackerPage + 1);
            RefreshTracker();
        };
        pager.AddChild(next);

        return pager;
    }

    private static Label TrackerText(string text, int rawSize, Color color,
        HorizontalAlignment align = HorizontalAlignment.Left)
    {
        int size = (int)(rawSize * HudPlacement.TrackerFontScale);
        var label = UiTheme.Text(text, size, color, align);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.95f));
        label.AddThemeConstantOverride("outline_size", 5);
        return label;
    }

    private static Label TrackerObjective(string text, Color color)
    {
        var label = TrackerText("  " + text, 11, color);
        label.CustomMinimumSize = new Vector2(MiniMap.SquareSize, 0);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private List<string> KillProgressLines(int questId)
    {
        var lines = new List<string>();
        foreach (var (mob, current, target) in KillProgress(questId)) lines.Add($"{mob} {current}/{target}");
        return lines;
    }

    private List<(string Mob, int Current, int Target)> KillProgress(int questId)
    {
        var groups = QuestGroups(questId);
        var lines = new List<(string, int, int)>(groups.Length);
        _questKills.TryGetValue(questId, out var counts);
        for (int g = 0; g < groups.Length; g++)
        {
            int cur = counts != null && g < counts.Length ? counts[g] : 0;
            int target = groups[g].Count;
            int firstNpc = groups[g].Npcs[0];
            string mob = _questObjectives.TryGetValue(questId, out var goals) && goals.Groups[g].Name is { } named
                ? named
                : firstNpc is Nations.Karus or Nations.ElMorad ? $"{Nations.Name(firstNpc)} players"
                : GameData.I != null ? GameData.I.NpcName(firstNpc, true) : $"#{firstNpc}";
            lines.Add((mob, Mathf.Min(cur, target), target));
        }
        return lines;
    }

    private void OnQuestLog(List<QuestEntry> entries)
    {
        _questViews.Clear();
        _questObjectives.Clear();
        _questStrings.Clear();
        _quests.Clear();
        _quests.AddRange(entries);
        RefreshNpcQuestMarkers();
        if (QuestsTabOpen()) RefreshQuestLog();
        RefreshTracker();
    }

    private void OnQuestState(int questId, int state)
    {
        int idx = _quests.FindIndex(q => q.QuestId == questId);
        if (idx >= 0) _quests[idx] = new QuestEntry(questId, state);
        else _quests.Add(new QuestEntry(questId, state));
        RefreshNpcQuestMarkers();

        if (state is 2 or 4) _questKills.Remove(questId);

        if (QuestsTabOpen()) RefreshQuestLog();
        RefreshTracker();
    }

    private void RefreshNpcQuestMarkers()
    {
        RebuildQuestTargets();
        RebuildViewMarkers();
        foreach (var ent in _ents.Values) RefreshNpcQuestMarker(ent);
    }

    private readonly Dictionary<int, string> _viewMarkerByNpc = new();
    private readonly HashSet<int> _questsWithViews = new();

    private void RebuildViewMarkers()
    {
        _viewMarkerByNpc.Clear();
        _questsWithViews.Clear();
        foreach (var quest in _quests)
        {
            if (!_questViews.TryGetValue(quest.QuestId, out var view)) continue;
            _questsWithViews.Add(quest.QuestId);
            if (view.ZoneId != 0 && view.ZoneId != _zone) continue;
            _viewMarkerByNpc.TryGetValue(view.NpcId, out string? current);
            if (current == QuestMarkerFx("readyToTurnIn")) continue;
            if (view.State == QuestViewState.Claimable)
                _viewMarkerByNpc[view.NpcId] = QuestMarkerFx("readyToTurnIn");
            else if (string.IsNullOrEmpty(current) && view.State is QuestViewState.Available or QuestViewState.InProgress)
                _viewMarkerByNpc[view.NpcId] = QuestMarkerFx(view.State == QuestViewState.Available ? "available" : "inProgress");
        }
    }

    private void RebuildQuestTargets()
    {
        _questTargetNpcs.Clear();
        foreach (var view in _questViews.Values)
        {
            if (view.ZoneId != 0 && view.ZoneId != _zone) continue;
            if (view.State == QuestViewState.Claimable)
            {
                _questTargetNpcs.Add(view.NpcId);
                continue;
            }
            if (view.State != QuestViewState.InProgress) continue;
            for (var group = 0; group < view.Objectives.Groups.Length; group++)
            {
                var goal = view.Objectives.Groups[group];
                var done = group < view.Counts.Length ? view.Counts[group] : 0;
                if (done >= goal.Count) continue;
                foreach (var monster in goal.Monsters) _questTargetNpcs.Add(monster);
            }
        }
    }

    private void RefreshNpcQuestMarker(Ent ent)
    {
        if (!ent.IsNpc || ent.IsMonster || ent.NpcId <= 0) return;
        _npcRoleFx ??= LoadNpcRoleFx();
        if (_npcRoleFx.ContainsKey(ent.NpcId)) return;
        string desired = _viewMarkerByNpc.TryGetValue(ent.NpcId, out string? fromView) ? fromView : "";

        foreach (var quest in Config.LegacyQuestFallback && desired != QuestMarkerFx("readyToTurnIn") ? _quests : [])
        {
            if (_questsWithViews.Contains(quest.QuestId)) continue;
            if (quest.State == 3
                && (QuestData.NpcForState(quest.QuestId, 2, ent.NpcId)
                    || QuestData.NpcForState(quest.QuestId, 3, ent.NpcId)))
            {
                desired = QuestMarkerFx("readyToTurnIn");
                break;
            }
            if (desired.Length == 0 && quest.State == 1
                && (QuestData.NpcForState(quest.QuestId, 1, ent.NpcId)
                    || QuestData.NpcForState(quest.QuestId, 2, ent.NpcId)))
                desired = QuestMarkerFx("inProgress");
        }

        if (desired.Length == 0)
        {
            foreach (int questId in QuestData.StartingQuestsForNpc(ent.NpcId))
                if (!_questViews.ContainsKey(questId)
                    && _quests.FindIndex(q => q.QuestId == questId && q.State is 1 or 2 or 3) < 0)
                {
                    desired = QuestMarkerFx("available");
                    break;
                }
        }

        if (desired == ent.IndicatorName) return;
        if (ent.IndicatorFx != null && GodotObject.IsInstanceValid(ent.IndicatorFx))
            ent.IndicatorFx.QueueFree();
        ent.IndicatorFx = desired.Length > 0 ? SpawnNpcIndicator(ent, desired) : null;
        ent.IndicatorName = desired;
    }

    private static Dictionary<int, string>? _npcRoleFx;

    private static Node3D? SpawnNpcRoleFx(Ent ent)
    {
        if (!ent.IsNpc || ent.IsMonster || ent.NpcId <= 0) return null;
        _npcRoleFx ??= LoadNpcRoleFx();
        return _npcRoleFx.TryGetValue(ent.NpcId, out string? fx)
            ? SpawnNpcIndicator(ent, fx)
            : null;
    }

    private static Dictionary<string, string>? _questMarkerFx;

    private static string QuestMarkerFx(string state)
    {
        _npcRoleFx ??= LoadNpcRoleFx();
        return _questMarkerFx != null && _questMarkerFx.TryGetValue(state, out string? fx) ? fx : "";
    }

    private static Dictionary<int, string> LoadNpcRoleFx()
    {
        var result = new Dictionary<int, string>();
        _questMarkerFx = new Dictionary<string, string>();
        using var file = Godot.FileAccess.Open(
            "res://assets/npcs/role_fx.json", Godot.FileAccess.ModeFlags.Read);
        if (file == null) return result;
        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary) return result;
        var root = parsed.AsGodotDictionary();
        if (root.TryGetValue("questMarkers", out var markers)
            && markers.VariantType == Variant.Type.Dictionary)
            foreach (var pair in markers.AsGodotDictionary())
                _questMarkerFx[pair.Key.AsString()] = pair.Value.AsString();
        if (!root.TryGetValue("npcs", out var value)
            || value.VariantType != Variant.Type.Dictionary)
            return result;
        foreach (var pair in value.AsGodotDictionary())
            if (int.TryParse(pair.Key.AsString(), out int npcId))
                result[npcId] = pair.Value.AsString();
        return result;
    }

    private void WarmRoleFx()
    {
        _npcRoleFx ??= LoadNpcRoleFx();
        var names = new HashSet<string>(_npcRoleFx.Values);
        if (_questMarkerFx != null)
            foreach (var fx in _questMarkerFx.Values) names.Add(fx);
        var warm = new Node3D { Name = "FxWarm", Visible = false };
        AddChild(warm);
        foreach (var name in names) Fx.Spawn(name, warm, Vector3.Zero);
        warm.QueueFree();
    }

    private static Node3D? SpawnNpcIndicator(Ent ent, string fxName)
    {
        // Same rise for every bundle: keying off each one's own extent instead made the wide-winged
        // icons ride visibly higher than the compact ones.
        const float indicatorRise = 1.40f;

        float top = ent.NameTag?.Position.Y
            ?? Mathf.Max(1.8f, ModelTopY(ent.Body) + 0.30f);
        return Fx.Spawn(
            fxName,
            ent.Body,
            new Vector3(0f, top + indicatorRise - Fx.AuthoredCentreY(fxName), 0f));
    }

    private void OnQuestKillCounts(int questId, ushort[] counts)
    {
        _questKills[questId] = counts;
        if (QuestsTabOpen()) RefreshQuestLog();
        RefreshTracker();
    }

    private IEnumerable<(string Label, int Done, int Needed)> QuestObjectiveProgress(int questId)
    {
        var groups = QuestGroups(questId);
        _questKills.TryGetValue(questId, out var counts);
        for (int g = 0; g < groups.Length; g++)
        {
            int firstNpc = groups[g].Npcs[0];
            string mob = _questObjectives.TryGetValue(questId, out var goals) && goals.Groups[g].Name is { } named
                ? named
                : firstNpc is Nations.Karus or Nations.ElMorad ? $"{Nations.Name(firstNpc)} players"
                : GameData.I != null ? GameData.I.NpcName(firstNpc, true) : $"#{firstNpc}";
            int cur = counts != null && g < counts.Length ? counts[g] : 0;
            yield return (mob, Mathf.Min(cur, groups[g].Count), groups[g].Count);
        }
        foreach (var want in QuestHandIns(questId))
            yield return (ItemData.DisplayName(want.ItemId),
                Mathf.Min(Inv.CountOf(want.ItemId), want.Count), want.Count);
    }

    private void ReportQuestProgress()
    {
        string selfName = Net.I.LastEnter.Name ?? "";
        (string Quest, string Label, int Done, int Needed)? advanced = null;
        var seen = new Dictionary<(int, string), int>();
        foreach (var quest in _quests)
        {
            if (quest.State != QuestStateActive && quest.State != QuestStateReadyToTurnIn) continue;
            foreach (var (label, done, needed) in QuestObjectiveProgress(quest.QuestId))
            {
                var key = (quest.QuestId, label);
                seen[key] = done;
                if (advanced is null && _questProgressSeen.TryGetValue(key, out var was) && done > was)
                    advanced = (QuestName(quest.QuestId, selfName), label, done, needed);
            }
        }
        _questProgressSeen = seen;
        if (advanced is { } step)
            ShowQuestProgressToast(step.Quest, step.Label, step.Done, step.Needed);
    }

    private void OnQuestObjectives(QuestObjectives objectives)
    {
        _questObjectives[objectives.QuestId] = objectives;
        if (QuestsTabOpen()) RefreshQuestLog();
        RefreshTracker();
    }

    private void OnQuestStrings(List<QuestStrings> texts)
    {
        foreach (var text in texts) _questStrings[text.QuestId] = text;
        if (QuestsTabOpen()) RefreshQuestLog();
        RefreshTracker();
    }

    private string QuestName(int questId, string selfName) =>
        QuestMarkup.Plain(QuestNameSource(questId, selfName), selfName);

    private string QuestNameSource(int questId, string selfName) =>
        _questViews.TryGetValue(questId, out var view)
            ? string.IsNullOrEmpty(view.Title) ? $"Quest {questId}" : view.Title
        : _questStrings.TryGetValue(questId, out var text) && text.Title.Length > 0
            ? text.Title
            : QuestData.Name(questId, selfName);

    private string QuestJournal(int questId, string selfName) =>
        QuestMarkup.Plain(QuestJournalSource(questId, selfName), selfName).Trim();

    private string QuestJournalSource(int questId, string selfName) =>
        (_questViews.TryGetValue(questId, out var view) ? view.Journal
            : _questStrings.TryGetValue(questId, out var text) && text.Journal.Length > 0
                ? text.Journal : QuestData.Objective(questId, selfName))
        .Trim();

    private void OnQuestKillUpdate(int questId, int group, int count)
    {
        if (!_questKills.TryGetValue(questId, out var counts))
        {
            counts = new ushort[4];
            _questKills[questId] = counts;
        }
        int g = group - 1;
        if (g >= 0 && g < 4) counts[g] = (ushort)count;
        if (QuestsTabOpen()) RefreshQuestLog();
        RefreshTracker();
    }

    private static string StateName(int state) => state switch
    {
        1 => "In progress", 2 => "Completed", 3 => "Ready to turn in", 4 => "Abandoned", _ => "Available",
    };

    private static Color StateColor(int state) => state switch
    {
        2 => UiTheme.Good, 3 => UiTheme.GoldBright, 4 => UiTheme.TextDim, _ => UiTheme.TextHi,
    };
}
