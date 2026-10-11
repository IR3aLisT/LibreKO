using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private readonly Dictionary<int, QuestView> _questViews = new();
    private int _questRewardChoice = -1;
    private readonly QuestRewardSelection<QuestTransfer> _questRewardSelection = new();
    private bool _questUiRefreshQueued;

    private void QueueQuestUiRefresh()
    {
        if (_questUiRefreshQueued) return;
        _questUiRefreshQueued = true;
        Callable.From(() =>
        {
            _questUiRefreshQueued = false;
            if (!IsInsideTree()) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            if (QuestsTabOpen()) RefreshQuestLog();
            RefreshTracker();
            RefreshNpcQuestMarkers();
            Diag.Hitch("quest refresh", started);
        }).CallDeferred();
    }

    private void OnQuestView(QuestView view)
    {
        _questViews[view.QuestId] = view;
        _questRewardSelection.Viewed(view.QuestId,
            view.State is QuestViewState.InProgress or QuestViewState.Claimable, view.Options);
        _questStrings[view.QuestId] = new QuestStrings(view.QuestId, view.Title, view.Journal);
        _questObjectives[view.QuestId] = view.Objectives;
        _questKills[view.QuestId] = view.Counts;
        var state = view.State switch
        {
            QuestViewState.InProgress => 1,
            QuestViewState.Claimable => 3,
            QuestViewState.Completed => 2,
            _ => 0
        };
        var index = _quests.FindIndex(q => q.QuestId == view.QuestId);
        if (index < 0) _quests.Add(new QuestEntry(view.QuestId, state));
        else _quests[index] = new QuestEntry(view.QuestId, state);
        QueueQuestUiRefresh();
        if (_questNotifications.Any(q => q.QuestId == view.QuestId && q.State != view.State)
            || view.ZoneId != 0 && view.ZoneId != _zone)
        {
            _questNotifications.RemoveAll(q => q.QuestId == view.QuestId);
            RefreshQuestNotification();
        }
        if (view.Notification)
            ShowQuestNotification(view);
        else if (view.Open && view.Page == QuestPageKind.Conversation)
            ShowQuestConversation(view);
        else if (view.Open)
            ShowQuestView(view);
    }

    private void ShowQuestConversation(QuestView view)
    {
        BeginNpcDialog(GameData.I != null ? GameData.I.NpcName(view.NpcId, false) : $"NPC #{view.NpcId}",
            view.Dialogue);
        int shown = 0;
        for (var index = 0; index < view.Topics.Length; index++)
        {
            var offered = index;
            AddNpcMenuButton($"{++shown}.   {view.Topics[index]}", () => OnNpcMenuClick(offered));
        }
        AddNpcMenuButton($"{++shown}.   Close", CloseNpcDialog);
        EndNpcDialog(shown);
    }

    private void ShowQuestView(QuestView view)
    {
        _questRewardChoice = _questRewardSelection.ChoiceIndex(view.QuestId, view.Options);
        BeginNpcDialog(GameData.I != null ? GameData.I.NpcName(view.NpcId, false) : view.Title, "");
        _npcBodyPanel.Visible = false;
        _npcQuestScroll.Visible = true;
        _npcQuestScroll.ScrollVertical = 0;
        foreach (var child in _npcQuestContent.GetChildren()) { _npcQuestContent.RemoveChild(child); child.QueueFree(); }
        _npcQuestContent.AddChild(QuestTitleLine(view));
        _npcQuestContent.AddChild(QuestParagraph(
            view.Dialogue.Length > 0 ? view.Dialogue : view.Journal, UiTheme.TextHi));

        if (view.Objectives.Groups.Length > 0)
        {
            var hunt = QuestSection(QuestObjectiveHeading(view.Objectives.Groups.Length > 0, false));
            if (view.Objectives.AnyWillDo)
                hunt.AddChild(UiTheme.Text("Complete any one", 12, UiTheme.TextLo));
            for (var index = 0; index < view.Objectives.Groups.Length; index++)
            {
                var goal = view.Objectives.Groups[index];
                var name = goal.Name ?? (GameData.I != null ? GameData.I.NpcName(goal.Monsters[0], true) : "Creature");
                var huntRow = new VBoxContainer();
                huntRow.AddThemeConstantOverride("separation", 3);
                huntRow.AddChild(QuestValueRow(name, $"{view.Counts[index]} / {goal.Count}",
                    view.Counts[index] >= goal.Count ? UiTheme.Good : UiTheme.GoldBright));
                huntRow.AddChild(QuestHuntBar(view.Counts[index], goal.Count));
                hunt.AddChild(QuestObjectiveTarget(huntRow, view.QuestId, index, goal.HasTarget));
            }
        }
        var deliveries = view.Transfers.Where(t => t.Take).ToArray();
        if (deliveries.Length > 0)
        {
            var collect = QuestSection(QuestObjectiveHeading(false, true));
            foreach (var transfer in deliveries)
            {
                var held = transfer.Kind switch
                {
                    0 => Inv.CountOf(transfer.ItemId),
                    1 => Sheet.Gold,
                    _ => -1
                };
                collect.AddChild(QuestItemRow(transfer.DisplayItemId, QuestTransferName(transfer),
                    held < 0 ? $"× {transfer.Count:n0}" : $"{Mathf.Min(held, transfer.Count):n0} / {transfer.Count:n0}",
                    held >= transfer.Count ? UiTheme.Good : UiTheme.GoldBright));
            }
        }
        if (view.Objectives.Groups.Length == 0 && deliveries.Length == 0)
            QuestSection("Objectives").AddChild(QuestParagraph(view.StandingObjective, UiTheme.TextLo));

        var payouts = view.Transfers.Where(t => !t.Take).ToArray();
        if (payouts.Length > 0)
        {
            var rewards = QuestSection("Rewards");
            foreach (var transfer in payouts)
                rewards.AddChild(QuestItemRow(transfer.DisplayItemId, QuestTransferName(transfer),
                    transfer.Kind is 4 or 5 ? "" : transfer.Count.ToString("n0"), UiTheme.GoldBright));
        }
        System.Action? onRewardChosen = null;
        if (view.Options.Length > 0 && !view.CanClaim)
        {
            var options = QuestSection("Choose one");
            foreach (var option in view.Options)
                options.AddChild(QuestItemRow(option.DisplayItemId, QuestTransferName(option),
                    option.Count.ToString("n0"), UiTheme.GoldBright));
        }
        else if (view.Options.Length > 0)
        {
            var choice = QuestSection("Choose one");
            var marks = new List<Label>();
            var frames = new List<PanelContainer>();
            for (var index = 0; index < view.Options.Length; index++)
            {
                var option = view.Options[index];
                var row = QuestItemRow(option.DisplayItemId, QuestTransferName(option),
                    $"{option.Count:n0}  ○", UiTheme.GoldBright);
                marks.Add(row.GetChild<Label>(row.GetChildCount() - 1));
                frames.Add(row.GetChild<PanelContainer>(0));
                choice.AddChild(QuestRewardOption(row, index, () =>
                {
                    _questRewardSelection.Choose(view.QuestId, option);
                    onRewardChosen?.Invoke();
                }));
            }
            void PaintChoice()
            {
                for (var index = 0; index < marks.Count; index++)
                {
                    var chosen = index == _questRewardChoice;
                    marks[index].Text = $"{view.Options[index].Count:n0}  {(chosen ? "●" : "○")}";
                    marks[index].AddThemeColorOverride("font_color", chosen ? UiTheme.Good : UiTheme.GoldBright);
                    frames[index].AddThemeStyleboxOverride("panel", QuestIconFrame(view.Options[index].DisplayItemId, chosen));
                }
            }
            onRewardChosen = PaintChoice;
            PaintChoice();
        }
        if (view.Daily && view.State == QuestViewState.Completed && view.NextReset > 0)
            _npcQuestContent.AddChild(QuestParagraph($"Available again: {DateTimeOffset.FromUnixTimeSeconds(view.NextReset).ToLocalTime():g}", UiTheme.TextLo));

        for (var index = 0; index < view.Topics.Length; index++)
        {
            var offered = index;
            AddNpcMenuButton(view.Topics[index], () => OnNpcMenuClick(offered));
        }
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        _npcMenuBox.AddChild(actions);
        Button Action(string label, System.Action run, bool enabled = true)
        {
            var button = new Button { Text = label, CustomMinimumSize = new Vector2(0, 36), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Disabled = !enabled };
            if (!enabled) button.TooltipText = ChooseRewardFirst;
            button.Pressed += () => { CloseNpcDialog(); run(); };
            actions.AddChild(button);
            return button;
        }
        if (view.CanAccept) Action("Accept", () => Net.I.SendQuestAccept(view.QuestId));
        if (view.CanClaim)
        {
            var confirm = Action("Confirm", () => Net.I.SendQuestComplete(view.QuestId, _questRewardChoice),
                view.Options.Length == 0 || _questRewardChoice >= 0);
            var paint = onRewardChosen;
            onRewardChosen = () =>
            {
                paint?.Invoke();
                confirm.Disabled = _questRewardChoice < 0;
                confirm.TooltipText = confirm.Disabled ? ChooseRewardFirst : "";
            };
        }
        if (!view.CanClaim && view.State is QuestViewState.InProgress or QuestViewState.Claimable)
            Action("Abandon", () => Net.I.SendQuestAbandon(view.QuestId));
        Action(view.CanAccept ? "Reject" : "Close", () => { });
        EndNpcDialog(view.Topics.Length + 1);
        _npcQuestScroll.CustomMinimumSize = Vector2.Zero;
        ((SceneTree)Engine.GetMainLoop()).Connect(SceneTree.SignalName.ProcessFrame, Callable.From(FitQuestPanel), (uint)ConnectFlags.OneShot);
    }

    private void FitQuestPanel()
    {
        if (!IsInstanceValid(_npcPanel) || !_npcPanel.IsInsideTree() || !_npcQuestScroll.Visible) return;
        var viewport = _npcPanel.GetViewportRect().Size;
        var room = Mathf.Max(100, viewport.Y - 80 - _npcMenuScroll.CustomMinimumSize.Y);
        _npcQuestScroll.CustomMinimumSize = new Vector2(0, Mathf.Min(_npcQuestContent.GetCombinedMinimumSize().Y, room));
        _npcPanel.ResetSize();
        _npcPanel.Position = new Vector2(Mathf.Clamp(_npcPanel.Position.X, 0, Mathf.Max(0, viewport.X - _npcPanel.Size.X)),
            Mathf.Clamp(_npcPanel.Position.Y, 0, Mathf.Max(0, viewport.Y - _npcPanel.Size.Y)));
    }

    private VBoxContainer QuestSection(string title) => QuestSection(_npcQuestContent, title);

    private static VBoxContainer QuestSection(Control parent, string title)
    {
        parent.AddChild(UiTheme.Text(title, 13, UiTheme.Gold));
        var panel = UiTheme.Section();
        parent.AddChild(panel);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        panel.AddChild(body);
        return body;
    }

    private const float QuestParagraphWidth = 400f;

    internal static string QuestObjectiveHeading(bool kills, bool deliveries) =>
        kills ? "Hunt" : deliveries ? "Collect" : "Objectives";

    private static RichTextLabel QuestParagraph(string text, Color color, float width = QuestParagraphWidth)
    {
        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            Text = QuestMarkup.Rich(text, Net.I?.LastEnter.Name ?? ""),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(width, 0)
        };
        label.AddThemeFontSizeOverride("normal_font_size", 14);
        label.AddThemeColorOverride("default_color", color);
        return label;
    }

    private Control QuestRewardOption(Control row, int index, System.Action chosen)
    {
        row.MouseFilter = Control.MouseFilterEnum.Stop;
        row.TooltipText = "Pick this reward";
        row.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { ButtonIndex: MouseButton.Left } click) return;
            row.AcceptEvent();
            if (!click.Pressed) return;
            _questRewardChoice = index;
            chosen();
        };
        return row;
    }

    private Control QuestObjectiveTarget(Control row, int questId, int group, bool hasTarget)
    {
        if (!hasTarget) return row;
        row.MouseFilter = Control.MouseFilterEnum.Stop;
        row.TooltipText = "Show where to find this";
        row.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                Net.I.SendQuestTargetRequest(questId, group);
        };
        return row;
    }

    private const float QuestRowIconSide = 32f;
    private const int TextChooseRewardFirst = 7435;
    private static string ChooseRewardFirst =>
        ItemData.Text(TextChooseRewardFirst, "First select the item you would like to receive as your reward.");
    private const float QuestHuntBarHeight = 4f;
    private const float QuestGlyphInset = 0.18f;
    private const int TextQuestUnderWay = 7422;
    private const int TextQuestBeforeReward = 7423;
    private const int TextQuestComplete = 7424;
    private static readonly Color QuestHuntTrack = new(0.05f, 0.05f, 0.05f, 0.9f);

    private Control QuestItemRow(int itemId, string name, string value, Color color)
    {
        var row = QuestValueRow(name, value, color);
        var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", QuestIconFrame(itemId, false));
        frame.AddChild(QuestRewardGlyph(itemId) ?? QuestItemIcon(itemId));
        row.AddChild(frame);
        row.MoveChild(frame, 0);
        return QuestItemHover(row, itemId);
    }

    private static TextureRect QuestItemIcon(int itemId)
    {
        var icon = new TextureRect
        {
            Texture = ItemData.Icon(itemId),
            CustomMinimumSize = new Vector2(QuestRowIconSide, QuestRowIconSide),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        UpgradeBadge.Show(icon, itemId);
        return icon;
    }

    private static Control? QuestRewardGlyph(int itemId)
    {
        string? glyph = itemId switch
        {
            QuestData.CoinItemId => "system/coins",
            QuestData.ExpItemId => "system/level",
            QuestData.LadderPointItemId => "system/medal",
            _ => null,
        };
        if (glyph == null) return null;
        var box = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        int inset = Mathf.RoundToInt(QuestRowIconSide * QuestGlyphInset);
        foreach (var margin in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            box.AddThemeConstantOverride(margin, inset);
        float side = QuestRowIconSide - 2 * inset;
        var image = UiIcons.Image(glyph, new Vector2(side, side), UiTheme.GoldBright);
        image.MouseFilter = Control.MouseFilterEnum.Ignore;
        box.AddChild(image);
        return box;
    }

    private static StyleBoxFlat QuestIconFrame(int itemId, bool chosen) =>
        UiTheme.Slot(chosen ? UiTheme.GoldBright : QuestData.IsVirtualReward(itemId) ? null : ItemGrade.Tint(itemId), chosen);

    private static Control QuestHuntBar(int count, int goal)
    {
        var track = new ColorRect
        {
            Color = QuestHuntTrack,
            CustomMinimumSize = new Vector2(0, QuestHuntBarHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var fill = new ColorRect
        {
            Color = count >= goal ? UiTheme.Good : UiTheme.GoldBright,
            AnchorRight = goal > 0 ? Mathf.Clamp((float)count / goal, 0f, 1f) : 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        track.AddChild(fill);
        return track;
    }

    private Control QuestTitleLine(QuestView view)
    {
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 8);
        var title = UiTheme.Text(view.Title, 15, UiTheme.GoldBright);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        line.AddChild(title);
        (int TextId, string Fallback, Color Colour)? state = view.State switch
        {
            QuestViewState.InProgress => (TextQuestUnderWay, "[Under way]", UiTheme.TextLo),
            QuestViewState.Claimable => (TextQuestBeforeReward, "[Before compensation]", UiTheme.GoldBright),
            QuestViewState.Completed => (TextQuestComplete, "[Complete]", UiTheme.Good),
            _ => null,
        };
        if (state is { } word)
            line.AddChild(UiTheme.Text(ItemData.Text(word.TextId, word.Fallback), 12, word.Colour, HorizontalAlignment.Right));
        return line;
    }

    private static bool HasItemCard(int itemId) =>
        !QuestData.IsVirtualReward(itemId) && ItemData.Get(itemId) != null;

    private T QuestItemHover<T>(T control, int itemId) where T : Control
    {
        if (!HasItemCard(itemId)) return control;
        control.MouseFilter = Control.MouseFilterEnum.Pass;
        control.MouseEntered += () => ShowItemTooltip(-1, TooltipItem(itemId));
        control.MouseExited += HideItemTooltip;
        return control;
    }

    private static HBoxContainer QuestValueRow(string name, string value, Color color)
    {
        var row = new HBoxContainer();
        var label = UiTheme.Text(name, 14, UiTheme.TextHi);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(label);
        row.AddChild(UiTheme.Text(value, 14, color, HorizontalAlignment.Right));
        return row;
    }

    private static string QuestTransferName(QuestTransfer transfer) => transfer.Kind switch
    {
        4 => "First job change",
        5 => "Second job change",
        _ => QuestRewardName(transfer.DisplayItemId)
    };

    private static string QuestRewardName(int displayItemId) => displayItemId switch
    {
        QuestData.CoinItemId => "Gold",
        QuestData.ExpItemId => "Experience",
        QuestData.LadderPointItemId => "Ladder points",
        _ => ItemData.DisplayName(displayItemId)
    };

    private IEnumerable<(int ItemId, int Count)> QuestHandIns(int questId) =>
        _questViews.TryGetValue(questId, out var view)
            ? view.Transfers.Where(t => t.Take && t.Kind == 0).Select(t => (t.ItemId, t.Count))
            : QuestData.HandIns(questId).Select(t => (t.ItemId, t.Count));

    private IEnumerable<(int ItemId, int Count)> QuestRewards(int questId) =>
        _questViews.TryGetValue(questId, out var view)
            ? view.Transfers.Where(t => !t.Take).Select(t => (t.DisplayItemId, t.Count))
            : QuestData.Rewards(questId, _selfClass).Select(t => (t.ItemId, t.Count));
}
