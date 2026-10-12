using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _npcLayer = null!;
    private HudWindow _npcPanel = null!;
    private RichTextLabel _npcBody = null!;
    private ScrollContainer _npcMenuScroll = null!;
    private VBoxContainer _npcMenuBox = null!;
    private VBoxContainer _npcQuestContent = null!;
    private ScrollContainer _npcQuestScroll = null!;
    private bool _npcDialogShown;
    private bool _npcBodyEmpty;
    private string _npcDialogScript = "";

    private CanvasLayer _balloonLayer = null!;
    private PanelContainer _balloonPanel = null!;
    private Label _balloonLabel = null!;
    private int _balloonToken;

    private const float TalkPickRadius = 70f;
    private const int MenuRowsVisible = 15;
    private const float NpcTalkMaxHeight = 200f;
    private const float MenuRowHeight = 39f;
    private const int FamiliarShopGroup = 232000;

    private System.Collections.Generic.List<QuestData.Offer> _npcOffers = new();
    private PanelContainer _npcBodyPanel = null!;
    private ScrollContainer _npcBodyScroll = null!;
    private string _npcOfferHeader = "", _npcOfferTitle = "";
    private const float NpcInteractRange = Net.NpcInteractRange;
    private const double NpcRangeInterval = 0.25;

    private int _npcTalkId = -1;
    private double _npcRangeAccum;

    private void NpcInit()
    {
        BuildNpcDialog();
        BuildNpcBalloon();
        BuildInteractionDialogs();

        Net.I.NpcDialogEvent += OnNpcDialog;
        Net.I.NpcSayEvent += OnNpcSay;
        Net.I.NpcSayTextEvent += ShowNpcBalloon;
        Net.I.NpcMsgEvent += OnNpcMsg;
        Net.I.NpcWindowEvent += OnNpcWindow;
    }

    private void NpcDispose()
    {
        Net.I.NpcDialogEvent -= OnNpcDialog;
        Net.I.NpcSayEvent -= OnNpcSay;
        Net.I.NpcSayTextEvent -= ShowNpcBalloon;
        Net.I.NpcMsgEvent -= OnNpcMsg;
        Net.I.NpcWindowEvent -= OnNpcWindow;
    }

    private bool TryInteractAt(Vector2 mouse)
    {
        if (TryOpenStallAt(mouse)) return true;
        if (TryOpenPlayerMenu(mouse)) return true;

        var best = PickEntityAt(mouse, TalkPickRadius, out int bestId, out _);
        if (best == null) return TryOpenWarpGate(mouse) || TryOpenAnvil(mouse);
        if (best.Attackable) return false;

        Select(bestId, best);
        if (!best.Attackable) TalkToNpc(bestId);
        return true;
    }

    private bool HasNearbyNpc()
    {
        if (!_worldReady || _self == null || _selfDead) return false;
        foreach (var (_, e) in _ents)
        {
            if (!e.IsNpc || e.Dead || e.Attackable) continue;
            if (FlatDistance(_self.Position, e.Body.Position) <= NpcInteractRange)
                return true;
        }
        return false;
    }

    public bool TalkToNearestNpc(string nameFragment)
    {
        if (!_worldReady || _self == null) return false;
        int bestId = -1;
        float bestDist = float.MaxValue;
        foreach (var (id, e) in _ents)
        {
            if (!e.IsNpc || e.Dead || e.Attackable) continue;
            if (nameFragment.Length > 0
                && !e.Name.Contains(nameFragment, System.StringComparison.OrdinalIgnoreCase)) continue;
            float d = FlatDistance(_self.Position, e.Body.Position);
            if (d > NpcInteractRange) continue;
            if (d < bestDist) { bestDist = d; bestId = id; }
        }
        if (bestId < 0) return false;
        TalkToNpc(bestId);
        return true;
    }

    private void TalkToNpc(int npcUniqueId)
    {
        if (_selfDead) return;
        if (!_ents.TryGetValue(npcUniqueId, out var e) || !e.IsNpc || e.Dead || e.Attackable) return;
        if (TryOpenRankerStatue(e)) return;
        if (TryOperateKeyGate(e, npcUniqueId)) return;

        if (FlatDistance(_self.Position, e.Body.Position) > NpcInteractRange) return;

        if (e.NpcType == NpcTypes.Lever)
        {
            TryOperateObject((short)e.NpcId, npcUniqueId);
            return;
        }

        StopForNpcTalk(e);
        if (TurnsToTalker(e) && YawToward(e.Body.Position, _self.Position) is { } towardTalker)
            FaceEntity(e, towardTalker, immediate: false);

        _npcTalkId = npcUniqueId;
        _npcRangeAccum = 0;
        _vendorNpcId = npcUniqueId;
        _vendorNpcName = e.Name.Length > 0
            ? e.Name
            : GameData.I != null ? GameData.I.NpcName(e.NpcId, false) : "Merchant";

        Net.I.SendNpcEvent(npcUniqueId);
        Net.I.SendWarpListRequest(e.NpcId);
    }

    private static bool TurnsToTalker(Ent e) =>
        e.Anim != null && !e.Statue && e.ObjectType == 0 && !EntityMoving(e);

    private void StopForNpcTalk(Ent e)
    {
        StopForInteraction();

        Vector3 toNpc = e.Body.Position - _self.Position;
        toNpc.Y = 0;
        if (toNpc.LengthSquared() <= 0.0004f) return;
        _faceDir = toNpc.Normalized();
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
        => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private void NpcTick(double delta)
    {
        if (_npcTalkId < 0) return;

        if (!AnyInteractionDialogShown()) { _npcTalkId = -1; return; }

        _npcRangeAccum += delta;
        if (_npcRangeAccum < NpcRangeInterval) return;
        _npcRangeAccum = 0;

        bool inRange = _ents.TryGetValue(_npcTalkId, out var e)
                       && !e.Dead
                       && FlatDistance(_self.Position, e.Body.Position) <= NpcInteractRange;
        if (inRange) return;

        CloseInteractionDialogs();
        _npcTalkId = -1;
    }

    private void BuildNpcDialog()
    {
        _npcLayer = new CanvasLayer { Layer = 73 };
        AddChild(_npcLayer);

        _npcPanel = new HudWindow("npc_dialog", "NPC", bodyMinWidth: 420) { Visible = false };
        _npcPanel.Closed += CloseNpcDialog;
        _npcLayer.AddChild(_npcPanel);

        var root = _npcPanel.Body;
        root.AddThemeConstantOverride("separation", 8);

        _npcBodyPanel = UiTheme.Section();
        root.AddChild(_npcBodyPanel);
        _npcBodyScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _npcBodyPanel.AddChild(_npcBodyScroll);

        _npcBody = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(420, 0),
        };
        _npcBody.AddThemeFontSizeOverride("normal_font_size", 14);
        _npcBody.AddThemeColorOverride("default_color", UiTheme.TextLo);
        _npcBodyScroll.AddChild(_npcBody);

        _npcQuestScroll = new ScrollContainer
        {
            Visible = false,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(_npcQuestScroll);
        _npcQuestContent = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _npcQuestContent.AddThemeConstantOverride("separation", 10);
        _npcQuestScroll.AddChild(_npcQuestContent);

        _npcMenuScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(_npcMenuScroll);
        _npcMenuBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _npcMenuBox.AddThemeConstantOverride("separation", 5);
        _npcMenuScroll.AddChild(_npcMenuBox);
    }

    private void OnNpcDialog(NpcDialog dlg)
    {
        if (dlg.Flag == NpcDialog.RebirthPanelFlag)
        {
            CloseNpcDialog();
            OpenRebirthPicker();
            return;
        }

        if (dlg.Flag == NpcDialog.RebirthResetPanelFlag)
        {
            CloseNpcDialog();
            OpenRebirthReset();
            return;
        }

        if (dlg.Flag == NpcDialog.RepurchaseWindowFlag)
        {
            CloseNpcDialog();
            OpenRepurchase(dlg.NpcId);
            return;
        }

        if (dlg.Flag == NpcDialog.FamiliarPanelFlag)
        {
            CloseNpcDialog();
            OpenPetHatch(dlg.NpcId);
            return;
        }

        if (dlg.Flag == NpcDialog.GenderChangePanelFlag)
        {
            CloseNpcDialog();
            OpenGenderChange();
            return;
        }

        if (dlg.Flag == NpcDialog.FortunePanelFlag)
        {
            CloseNpcDialog();
            OpenFortune();
            return;
        }

        if (dlg.Flag is ExpSeal.SmallJarFlag or ExpSeal.LargeJarFlag or ExpSeal.RebirthJarFlag)
        {
            CloseNpcDialog();
            UseExpJar(dlg.Flag);
            return;
        }

        if (dlg.Flag == NpcDialog.FullMoonBoardFlag)
        {
            CloseNpcDialog();
            OpenFullMoonBoard();
            return;
        }

        if (dlg.Flag == NpcDialog.SpecialAuctionPanelFlag)
        {
            CloseNpcDialog();
            OpenSpecialAuction();
            return;
        }

        if (dlg.Flag == NpcDialog.ItemCombinePanelFlag)
        {
            CloseNpcDialog();
            OpenItemCombine(dlg.NpcId);
            return;
        }

        if (dlg.Flag == NpcDialog.CombineRecipeBookFlag)
        {
            CloseNpcDialog();
            OpenCombineRecipeBook();
            return;
        }

        if (dlg.Flag == NpcDialog.DisassemblePanelFlag)
        {
            int talkedTo = _npcTalkId;
            CloseNpcDialog();
            OpenItemSmash(dlg.NpcId, talkedTo);
            return;
        }

        if (dlg.Flag == NpcDialog.FamiliarShopFlag)
        {
            OpenVendor(FamiliarShopGroup);
            return;
        }

        _npcDialogScript = dlg.ScriptFile;
        if (dlg.HeaderText is { Length: > 0 } body)
            BeginNpcDialog(NpcHeaderName(dlg.NpcId), body);
        else
            BeginNpcDialog(dlg.NpcId, dlg.HeaderTextId);

        int shown = 0;
        foreach (var (index, textId, text) in dlg.Buttons)
        {
            string label = text is { Length: > 0 } ? text : QuestText.Menu(textId);
            if (label.Length == 0) label = $"…({textId})";
            int menuIndex = index;
            AddNpcMenuButton($"{++shown}.   {label}", () => OnNpcMenuClick(menuIndex));
        }

        if (shown == 0 && !_npcBodyEmpty)
            AddNpcMenuButton($"{++shown}.   Close", CloseNpcDialog);
        EndNpcDialog(shown);
    }

    // Sub 7's text is the HEADER of the NPC's quest menu; retail builds the list client-side.
    private void OnNpcMsg(int textId, int npcId)
    {
        _npcOffers = QuestData.OffersAtNpc(
            npcId, QuestStateOf, Sheet.Level, _selfClass, Net.I.LastEnter.Nation, _zone);
        _npcOfferHeader = QuestText.Talk(textId, Net.I.LastEnter.Name ?? "");
        _npcOfferTitle = NpcHeaderName(npcId);
        ShowNpcOffers();
    }

    private void ShowNpcOffers()
    {
        BeginNpcDialog(_npcOfferTitle, _npcOfferHeader);
        if (_npcOffers.Count == 0)
        {
            if (_npcBodyEmpty) { CloseNpcDialog(); return; }
            AddNpcMenuButton("Close", CloseNpcDialog);
            EndNpcDialog(1);
            return;
        }

        int shown = 0;
        foreach (var offer in _npcOffers)
        {
            string name = QuestData.Name(offer.QuestId, Net.I.LastEnter.Name ?? "");
            var (icon, tag) = offer.State switch
            {
                QuestStateActive => ("system/clock", "In progress"),
                QuestStateReadyToTurnIn => ("system/gift", "Ready"),
                _ => ("system/scroll", ""),
            };
            ++shown;
            AddNpcMenuButton(name, () => AcceptNpcOffer(offer), icon, tag, offer.State == QuestStateReadyToTurnIn);
        }
        EndNpcDialog(shown);
    }

    private void AcceptNpcOffer(QuestData.Offer offer)
    {
        CloseNpcDialog();
        Net.I.SendQuestAccept(offer.QuestId);
    }

    private void BeginNpcDialog(int npcId, int textId) =>
        BeginNpcDialog(NpcHeaderName(npcId), QuestText.Talk(textId, Net.I.LastEnter.Name ?? ""));

    private string NpcHeaderName(int npcId) =>
        NpcTitle.Of(GameData.I?.KnownNpcName(npcId),
            _ents.TryGetValue(_npcTalkId, out var talker) && talker.NpcId == npcId ? talker.Name : null, npcId);

    private void BeginNpcDialog(string title, string body)
    {
        _npcQuestScroll.Visible = false;
        _npcBodyPanel.Visible = body.Length > 0;
        _npcBodyScroll.ScrollVertical = 0;
        _npcPanel.Title = QuestMarkup.Plain(title, Net.I?.LastEnter.Name ?? "");
        _npcBody.Visible = body.Length > 0;
        _npcBody.Text = QuestMarkup.Rich(body, Net.I?.LastEnter.Name ?? "");
        _npcBodyEmpty = body.Length == 0;

        foreach (var c in _npcMenuBox.GetChildren()) { _npcMenuBox.RemoveChild(c); c.QueueFree(); }
    }

    private void ShowNpcServiceChoice(string body, params (string Label, System.Action Act)[] options)
    {
        BeginNpcDialog(_vendorNpcName, body);
        int shown = 0;
        foreach (var (label, act) in options)
        {
            var run = act;
            AddNpcMenuButton($"{++shown}.   {label}", () => { CloseNpcDialog(); run(); });
        }
        AddNpcMenuButton($"{++shown}.   Close", CloseNpcDialog);
        EndNpcDialog(shown);
    }

    private const int NpcChoiceIconSize = 16;
    private const int NpcChoiceTagFont = 11;
    private const int NpcChoiceTagInset = 10;
    private static readonly string[] NpcReadyColourStates = { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "icon_normal_color", "icon_hover_color" };

    private void AddNpcMenuButton(string label, System.Action onPressed, string? icon = null, string tag = "", bool ready = false)
    {
        var (caption, readyTopic) = NpcChoice.SplitReady(NpcChoice.Caption(QuestMarkup.Plain(label, Net.I?.LastEnter.Name ?? "")));
        var b = new Button
        {
            Text = caption,
            TooltipText = caption,
            FocusMode = Control.FocusModeEnum.None,
            Alignment = HorizontalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            ClipText = true,
            Icon = UiIcons.Get(icon ?? NpcChoice.Icon(NpcChoice.Classify(caption))),
            IconAlignment = HorizontalAlignment.Left,
            ExpandIcon = false,
            CustomMinimumSize = new Vector2(0, 34),
        };
        b.AddThemeFontSizeOverride("font_size", 13);
        b.AddThemeConstantOverride("icon_max_width", NpcChoiceIconSize);
        b.AddThemeColorOverride("icon_normal_color", UiTheme.Gold);
        b.AddThemeColorOverride("icon_hover_color", UiTheme.GoldBright);
        if (ready || readyTopic)
            foreach (var state in NpcReadyColourStates) b.AddThemeColorOverride(state, UiTheme.Good);
        if (tag.Length > 0)
        {
            var tagLabel = UiTheme.Text(tag, NpcChoiceTagFont, UiTheme.GoldBright, HorizontalAlignment.Right);
            tagLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            tagLabel.SetAnchorsPreset(Control.LayoutPreset.RightWide);
            tagLabel.OffsetRight = -NpcChoiceTagInset;
            tagLabel.GrowHorizontal = Control.GrowDirection.Begin;
            tagLabel.VerticalAlignment = VerticalAlignment.Center;
            b.AddChild(tagLabel);
        }
        b.Pressed += () => onPressed();
        _npcMenuBox.AddChild(b);
    }

    private void EndNpcDialog(int shown)
    {
        _npcMenuScroll.Visible = shown > 0;
        _npcMenuScroll.CustomMinimumSize = new Vector2(0,
            Mathf.Min(shown, MenuRowsVisible) * MenuRowHeight
            + (shown > MenuRowsVisible ? MenuRowHeight * 0.45f : 0f));

        if (_npcBodyEmpty && shown == 0) { CloseNpcDialog(); return; }
        ShowNpcDialog();
        Callable.From(FitNpcTalk).CallDeferred();
    }

    private void FitNpcTalk()
    {
        if (!IsInstanceValid(_npcPanel) || !_npcBodyPanel.Visible) return;
        _npcBodyScroll.CustomMinimumSize = new Vector2(0, Mathf.Min(_npcBody.GetContentHeight(), NpcTalkMaxHeight));
        _npcPanel.ResetSize();
    }

    private void OnNpcMenuClick(int menuIndex)
    {
        Net.I.SendSelectMsg(menuIndex, _npcDialogScript, -1);
        CloseNpcDialog();
    }

    private void ShowNpcDialog()
    {
        _npcPanel.Visible = true;
        _npcDialogShown = true;
    }

    private void CloseNpcDialog()
    {
        if (!_npcDialogShown) return;
        _npcDialogShown = false;
        _npcPanel.Visible = false;
        HideItemTooltip();
    }

    private void BuildNpcBalloon()
    {
        _balloonLayer = new CanvasLayer { Layer = 68, Visible = false };
        AddChild(_balloonLayer);

        _balloonPanel = new PanelContainer();
        _balloonPanel.AddThemeStyleboxOverride("panel", UiTheme.Panel(6, true));
        _balloonLayer.AddChild(_balloonPanel);

        _balloonLabel = HudStyle.Label(15, HorizontalAlignment.Center);
        _balloonLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _balloonLabel.CustomMinimumSize = new Vector2(520, 0);
        _balloonPanel.AddChild(_balloonLabel);
    }

    private void OnNpcSay(int[] textIds)
    {
        string selfName = Net.I.LastEnter.Name ?? "";
        var lines = new System.Collections.Generic.List<string>(textIds.Length);
        foreach (int id in textIds)
        {
            string s = QuestText.Talk(id, selfName);
            if (s.Length > 0) lines.Add(s);
        }
        ShowNpcBalloon(lines.ToArray());
    }

    private void ShowNpcBalloon(string[] lines)
    {
        if (lines.Length == 0) return;

        string text = string.Join("\n", lines);
        _balloonLabel.Text = text;
        _balloonLayer.Visible = true;
        Callable.From(CentreBalloon).CallDeferred();

        int token = ++_balloonToken;
        double secs = Mathf.Clamp(2.5 + text.Length * 0.04, 3.0, 9.0);
        GetTree().CreateTimer(secs).Timeout += () => { if (_balloonToken == token) _balloonLayer.Visible = false; };
    }

    private void CentreBalloon()
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        Vector2 sz = _balloonPanel.Size;
        _balloonPanel.Position = new Vector2((vp.X - sz.X) * 0.5f, vp.Y * 0.14f);
    }

    private void OnNpcWindow(GameOpcodes op)
    {
        CombatNotice("This NPC's service isn't available yet.");
    }
}
