using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int PreviewPotion = 389070000;
    private const int PreviewHpPotion = 389015000;
    private const int PreviewMpPotion = 389020000;
    private const int PreviewPotionCount = 63;
    private const int PreviewMonsterStone = 900144023;
    private const int PreviewNestTimeLeft = 1786;
    private const int PreviewNestGraceLeft = 11;
    private const int PreviewBattleSecondsLeft = 1325;
    private const int PreviewUpgradedWeapon = 156210008;
    private const int PreviewUpgradedResult = 156210009;
    private const int PreviewReverseWeapon = 156211038;
    private const int PreviewUniqueRing = 330620433;
    private const int PreviewKrowazBoots = 208005000;
    private const int PreviewKrowazTop = 208001000;
    private const int PreviewNoRepairHelmet = 202013000;
    private const int PreviewNonStorableTattoo = 810433000;
    private const int PreviewCompoundRing = 340947000;
    private const float PreviewScanShare = 0.6f;
    private const int PreviewSealStone = 810890000;

    internal Node3D? SelfVisual => _self;

    internal Texture2D? TouchSlotIconUiPreview(int slotInPage)
    {
        SkillData.EnsureLoaded();
        if (_previewTouchSlots == null)
        {
            _previewTouchSlots = new int[TouchControls.ActionSlots];
            int filled = 0;
            foreach (var s in SkillData.ForClass(_selfClass != 0 ? _selfClass : 205))
            {
                if (s.Tree % 10 != 0 || s.Level < 1 || SkillData.Icon(s.Id) == null) continue;
                _previewTouchSlots[filled] = s.Id;
                if (++filled >= _previewTouchSlots.Length) break;
            }
        }
        int id = _previewTouchSlots[slotInPage % _previewTouchSlots.Length];
        return id == 0 ? null : SkillData.Icon(id);
    }

    private int[]? _previewTouchSlots;

    internal Control BuildExpBarUiPreview()
    {
        Sheet.ApplyLevel(61, 0, 168, 1000);
        ExpBarInit();
        UpdateExpBar();
        SetExpBarClock("14:33   Clear");
        if (_expBarStats != null) _expBarStats.Text = "60 fps  38 ms";
        return DetachPreviewControl(_expBarRoot!);
    }

    internal (Control Hp, Control Mp) BuildPotionButtonsUiPreview()
    {
        ItemData.EnsureLoaded();
        SkillData.EnsureLoaded();
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart + 1] = PreviewItem(PreviewHpPotion, PreviewPotionCount, 1);
        Inv[GridStart + 2] = PreviewItem(PreviewMpPotion, PreviewPotionCount, 1);
        return CreatePotionButtons();
    }

    internal string? HairStemFor(int race, int style) => HairPartStem(race, style);

    private const int UiPreviewVp = 182;
    private const int UiPreviewMaxVp = 250;

    internal Control BuildStatusUiPreview()
    {
        _zone = 21;
        _myKoX = 267f;
        _myKoZ = 303f;
        BuildStatusHud();
        if (Platform.TouchUi) ScaleVitals(_statusRoot);
        _orb.Set(61, 23.269);
        Sheet.SeedRebirth(2, 2, 0, 2, 0, 0);
        _rebirthEmblem?.Set(2, 0.64f, false, RebirthProgress.Tooltip(83, 64, 2, Sheet.RebirthBonusAtRow));
        if (_rebirthEmblem != null) _rebirthEmblem.Visible = true;
        _hpBar.Set(257, 257);
        _mpBar.Set(157, 157);
        ShowVpBar(UiPreviewVp, UiPreviewMaxVp);
        UpdateStatusHud();
        return DetachPreviewControl((Control)_orb.GetParent());
    }

    internal Control BuildTargetUiPreview()
    {
        BuildTargetHud();
        _targetName.Text = "Worm   Lv 2";
        _targetHp.Set(32, 48);
        _targetBox.Visible = true;
        return DetachPreviewControl(_targetBox);
    }

    internal Control BuildTargetDropsUiPreview()
    {
        var target = BuildTargetUiPreview();
        _targetDrops = new[] { new TargetQuestDrop(379273000, 2, 3), new TargetQuestDrop(379274000, 0, 3) };
        RebuildTargetDropRows();
        return target;
    }

    internal Control BuildZoneAbilityUiPreview(byte zoneType)
    {
        BuildZoneAbilityChip();
        ApplyZoneAbility(new ZoneAbilityInfo { ZoneType = zoneType, Tariff = 10 }, announce: false);
        _zoneabilityChip.Visible = true;
        return DetachPreviewControl(_zoneabilityChip);
    }

    internal Control BuildChatUiPreview()
    {
        Chat = new ChatSystem(this);
        Chat.Build();
        Chat.DetachNetwork();
        Chat.PersonalRankOf = charId => PreviewChatRanks.TryGetValue(charId, out int rank) ? rank : NationRankAura.Unranked;
        foreach (var line in PreviewChatLines()) Chat.PreviewLine(line);
        Chat.PreviewSent("Tester10", "yep, west side by the bridge");
        Chat.ShowNoticePreview("The Moradon market opens in 5 minutes.");
        return DetachPreviewControl(Chat.Panel);
    }

    private static readonly Dictionary<int, int> PreviewChatRanks = new() { [9001] = 1, [9003] = 7, [9004] = 35, [9008] = 150 };

    private static IEnumerable<ChatLine> PreviewChatLines()
    {
        yield return new ChatLine(1, 0, 9001, "Arwen", "Anyone hunting worms near the bridge?", false);
        yield return new ChatLine(3, 1, 9002, "Rin", "Ready when you are (Smile)(Thumbs up) 🔥", false);
        yield return new ChatLine(5, 0, 9003, "Kaito", $"Selling {ChatItemLink.Token(PreviewUpgradedWeapon)} cheap, whisper me", false);
        yield return new ChatLine(6, 1, 9004, "Mira", "Clan war practice tonight ⚔️🛡️", false);
        yield return new ChatLine(15, 2, 9005, "Borin", "Alliance meeting at the castle.", false);
        yield return new ChatLine(2, 2, 9006, "Tester10", "hey, are you farming Ronark?", false);
        yield return new ChatLine(14, 0, 9007, "Nightwalker", $"WTS {ChatItemLink.Token(PreviewUniqueRing)}", false);
        yield return new ChatLine(1, 0, 9008, "GameMaster", "Welcome to Moradon.", true);
    }

    internal void AttachChatUiPreviewLayout(Control chat) => Chat.AttachPreviewLayout(chat);

    internal void SetChatLookUiPreview(string tab, bool timestamps, int font, int background, bool locked) =>
        Chat.PreviewLook(tab, timestamps, font, background, locked);

    internal void TypeChatUiPreview(string text, bool link, string tail) =>
        Chat.PreviewTyped(text, link ? PreviewUpgradedWeapon : 0, tail);

    internal void ScrollChatUpUiPreview(int fresh)
    {
        var lines = new List<ChatLine>();
        for (int i = 0; i < fresh; i++)
            lines.Add(new ChatLine(1, 0, 9100 + i, "Arwen", $"Still looking for a party, try {i + 1}.", false));
        Chat.PreviewScrolledUp(lines);
    }

    internal void OpenChatMenuUiPreview(string which) => Chat.PreviewMenu(which);

    internal void ShowNearbyUiPreview(bool shown) => Chat.PreviewNearbyShown(shown);

    internal void FillNearbyUiPreview()
    {
        _nearbyCard = new NearbyCard();
        Chat.DockNearby(_nearbyCard);
        var me = new NearbyViewer("Zeus", 1, 7, 267, 303, false);
        var seen = new List<NearbySeen>
        {
            new(101, "Rin", 1, 62, 0, 0, 270, 309, false),
            new(102, "Mira", 1, 70, 0, 7, 255, 290, false),
            new(103, "Arwen", 1, 55, 0, 0, 290, 330, false),
            new(104, "Kaito", 2, 71, 0, 0, 240, 280, false),
            new(105, "Borin", 1, 48, 0, 0, 300, 302, false),
        };
        var listed = new List<NearbyListed>
        {
            new("Nightwalker", 2, 412, 515, 0),
            new("Tester10", 2, 120, 90, 0),
            new("Ophelia", 1, 600, 150, 0),
            new("Seraphine", 1, 350, 700, 9),
        };
        var party = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Rin" };
        _nearbyRows = NearbyRoster.Build(me, listed, seen, party);
        _nearbyCard.SetRows(_nearbyRows);
    }

    internal Control BuildChatColorsUiPreview()
    {
        OpenChatColors();
        return DetachPreviewControl(_chatColorsWindow!);
    }

    internal void OpenChatPaletteUiPreview(int slot)
    {
        if (_chatColorSwatches[slot]?.GetParent() is Control anchor) OpenChatPalette(slot, anchor);
    }

    internal void SetChatUiPreviewState(bool inputActive, byte channel) =>
        Chat.SetPreviewState(inputActive, channel);

    internal bool FloaterUiPreview()
    {
        if (!_worldReady || Floaters == null) return false;
        PreviewFloaters();
        return true;
    }

    internal Control BuildCombatLogUiPreview()
    {
        BuildCombatLog();
        _combatLogFrame.Visible = true;
        for (int i = 0; i < 6; i++)
            CombatLogAdd($"{i + 2} MP Recovered", CombatLogKind.Resource, tab: LogTab.State);
        CombatLogAdd("42 HP Recovered", CombatLogKind.Recovery, tab: LogTab.State);
        CombatLogAdd("Worm received 14 damage", CombatLogKind.Damage);
        CombatLogAdd("3 HP Damage", CombatLogKind.Incoming, tab: LogTab.State);
        CombatLogAdd("Picked up 1,250 Coins.", CombatLogKind.Resource, tab: LogTab.Item);
        CombatLogAdd("Worm received 21 damage", CombatLogKind.Damage);
        return DetachPreviewControl(_combatLogFrame);
    }

    internal void AttachCombatLogUiPreviewLayout(Control combatLog, Vector2 position) =>
        AttachCombatLogLayout(combatLog, () => position, persist: false);

    internal Control BuildHotbarUiPreview(bool vertical = false, int extraBars = 0)
    {
        SkillData.EnsureLoaded();
        ItemData.EnsureLoaded();
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(PreviewPotion, 12, 1);
        BuildHotbar(vertical, extraBars);
        int filled = 0;
        foreach (var s in SkillData.ForClass(_selfClass != 0 ? _selfClass : 205))
        {
            if (s.Tree % 10 != 0 || s.Level < 1) continue;
            _hotbar[filled] = s.Id;
            if (++filled >= HotSlotsPerPage - 1) break;
        }
        _hotbar[HotSlotsPerPage - 1] = PreviewPotion;
        _hotSelected = 2;
        RefreshHotbar();

        for (int i = 0; i < _hotCells.Count; i++)
        {
            _hotCells[i].SetCooldown(i switch { 1 => 0.85f, 2 => 0.62f, 3 => 0.40f, 4 => 0.15f, _ => 0f });
            _hotCells[i].SetDim(i == 6);
            if (i is 5 or 6) _hotCells[i].SetCount(i == 5 ? 248 : 0, i == 5);
        }
        _hotCells[HotSlotsPerPage - 1].SetCount(12, true);
        var hotbar = DetachPreviewControl(_hotbarBox);
        HudAnchor.Pin(hotbar, HudAnchor.Spot.BottomCenter);
        return hotbar;
    }

    internal void BuildInventoryTooltipHostUiPreview() => BuildInventoryPanel();

    internal Control? HoverHotbarSlotUiPreview(int slotInPage)
    {
        if (slotInPage < 0 || slotInPage >= _hotCells.Count) return null;
        _hotCells[slotInPage].EmitSignal(Control.SignalName.MouseEntered);
        return _itemTipPanel.Visible ? DetachPreviewControl(_itemTipPanel) : null;
    }

    internal Control BuildHudLauncherUiPreview() =>
        DetachPreviewControl(BuildHudLauncher());

    internal Control? BuildTownButtonUiPreview() =>
        _townButton == null ? null : DetachPreviewControl(_townButton);

    internal Control BuildBuffsUiPreview()
    {
        SkillData.EnsureLoaded();
        _myId = 1001;
        BuildBuffBar();

        int normalCap = Platform.TouchUi ? 3 : 5;
        int powerCap = Platform.TouchUi ? 0 : 4;
        int normal = 0, power = 0;
        foreach (var s in SkillData.All)
        {
            if (s.Duration <= 0 || SkillData.Icon(s.Id) == null) continue;
            if (s.Type1 is not (MagicType.Buff or MagicType.DotHeal or MagicType.Stealth)) continue;
            bool isPower = IsPowerBuff(s);
            if (isPower ? power >= powerCap : normal >= normalCap) continue;
            if (isPower) power++; else normal++;
            RegisterBuff(s, _myId, s.Duration);
            if (normal >= normalCap && power >= powerCap) break;
        }
        BuffBarTick(Now());
        return DetachPreviewControl(_buffPanel!);
    }

    internal static Vector2 BuffsUiPreviewPosition(Vector2 viewport) =>
        viewport * 0.5f + BuffPanelFromCenter;

    internal Control BuildPremiumUiPreview()
    {
        BuildPremiumChip();
        OnPremiumStatus(1, 1, 52);
        return DetachPreviewControl(_premiumChip);
    }

    private const int PreviewClanOnline = 24;

    internal Control BuildClanBonusUiPreview()
    {
        Net.I.SeedPreviewClan(PreviewClan(asChief: false));
        BuildClanBonusChip();
        OnClanBonus(PreviewClanOnline);
        return DetachPreviewControl(_clanBonusChip);
    }

    internal Control BuildEventTitlesUiPreview(int count, bool expanded)
    {
        var samples = new[]
        {
            new EventTitle("Exp Event", "EXP Increase Event is under way! Earn 50 percent extra EXP from monster hunting."),
            new EventTitle("Clan Notice", "Siege practice tonight at nine, meet at the Moradon gate."),
            new EventTitle("Drop Event", "Drop Increase Event is under way! Earn 30 percent extra Drop from monster hunting."),
            new EventTitle("NP Event", "Bonus National point event in effect. You'll receive additional 20 percent of NPs for kill."),
        };
        Net.I.EventTitles.Clear();
        for (int i = 0; i < count && i < samples.Length; i++) Net.I.EventTitles.Show(samples[i].Title, samples[i].Message);
        Net.I.EventTitles.Expanded = expanded;
        BuildEventTitleColumn();
        RefreshEventTitles();
        return DetachPreviewControl(_eventTitleColumn);
    }

    internal Control BuildAttendanceGiftUiPreview()
    {
        BuildAttendanceGift();
        SetAttendanceGift(2);
        return DetachPreviewControl(_attendanceGift);
    }

    internal Control BuildTrophyUiPreview()
    {
        BuildTrophy();
        RefreshTrophy(1);
        return DetachPreviewControl(_trophy);
    }

    internal Control BuildMailIconUiPreview(int unread = 3)
    {
        BuildMailIcon();
        RefreshMailIcon(unread);
        return DetachPreviewControl(_mailIconButton);
    }

    internal (Control Store, Control Lottery) BuildTopIconsUiPreview()
    {
        TopIconsInit();
        _lotteryState = new LotteryState { Active = true };
        RefreshLotteryIcon();
        return (DetachPreviewControl(_powerUpStoreIcon), DetachPreviewControl(_lotteryIcon));
    }

    internal Control BuildClockUiPreview()
    {
        BuildClockHud();
        UpdateClock(0.42f, Weather.Sunny);
        return DetachPreviewControl(_clockLabel);
    }

    private const int PreviewPersonalRank = 3;

    internal Control BuildPrimaryWindowUiPreview(string windowName, int classCode = 201)
    {
        ItemData.EnsureLoaded();
        SkillData.EnsureLoaded();
        _myId = 1001;
        _selfPersonalRank = PreviewPersonalRank;
        Vitals.Seed(hp: 214, maxHp: 257, mp: 151, maxMp: 157);
        _selfClass = classCode;
        Sheet.SeedStats(str: 60, sta: 72, dex: 184, intel: 50, mag: 50, points: 3);
        Sheet.SeedCombat(ap: 412, ac: 286);
        Sheet.ApplyDerived(new DerivedStats
        {
            TotalHit = 412, TotalAc = 286, MaxWeight = Sheet.MaxWeight,
            StrBonus = 57, StaBonus = 13, DexBonus = 10, IntBonus = 0, ChaBonus = -4,
            FireR = 20, ColdR = 15, LightningR = 15, MagicR = 10, DiseaseR = 8, PoisonR = 12,
        });
        Sheet.SeedWealth(gold: 1_180_000, np: 2_450);
        Sheet.ApplyLoyalty(np: 2_450, monthly: 880);
        Sheet.SetManner(36);
        Sheet.SeedProgress(level: 82, exp: 18_704_073_876, maxExp: 24_523_906_932);
        Sheet.SeedResists(fire: 20, cold: 15, lightning: 15, magic: 10, disease: 8, poison: 12);
        Mastery.Seed(new byte[] { 4, 0, 0, 0, 0, 61, 12, 0, 0 });

        Sheet.SetMaxWeight(10_000);

        Inv.EnsureLength(InventoryConstants.InventoryTotal);
        Inv[6] = PreviewItem(PreviewUpgradedWeapon, 1, 7000);
        int[] previewBag =
        {
            389310000, 379001000, 379022000, 810684000, 811182000,
            900402000, 379040000, 320410011, 330310000, 810418000,
        };
        for (int i = 0; i < previewBag.Length; i++)
            Inv[GridStart + i] = PreviewItem(previewBag[i], (short)(3 + i * 7), 1);
        Inv[GridStart + 12] = PreviewItem(PreviewReverseWeapon, 1, 7000);
        Inv[GridStart + 13] = PreviewItem(201001000, 1, 3000);
        Inv[GridStart + 14] = PreviewItem(PreviewUniqueRing, 1, 1);

        Inv[InventoryConstants.CosWing] = PreviewItem(810178000, 1, 1);
        Inv[InventoryConstants.BagSlotFor(0)] = PreviewItem(700011000, 1, 1);
        Inv[InventoryConstants.MagicBagPageStart(0)] = PreviewItem(379001000, 40, 1);
        Inv[InventoryConstants.MagicBagPageStart(0) + 3] = PreviewItem(810418000, 1, 12);

        BuildStatsPanel();
        BuildFriendsPanel();
        BuildClanPage();
        RefreshStatsUI();
        _stHeaderName.Text = "Zeus";
        _stHeaderSub.Text = "Rogue";
        BuildInventoryPanel();
        RefreshInventoryUI();
        BuildSkillWindow();
        SeedPreviewQuests();
        BuildQuestLog();
        RefreshQuestLog();
        BuildPartyPanel();
        Net.I.SeedPartyPreview(
            new PartyMember(1001, 1, "Zeus", 257, 214, 61, 202, 157, 151),
            new PartyMember(1002, 1, "Rin", 742, 612, 80, 106, 122, 94),
            new PartyMember(1003, 1, "Aria", 304, 267, 60, 209, 688, 541));
        RefreshPartyUI();
        MainPanelInit();

        if (windowName == "Skills")
            RebuildSkillWindow();

        if (windowName == "Friends")
        {
            windowName = "Character";
            OnFriendList(new System.Collections.Generic.List<Network.FriendEntry>
            {
                new() { Name = "Hera", CharId = 1002, Status = 1, Level = 72, Class = 202, Nation = Nations.ElMorad, ZoneId = 21 },
                new() { Name = "Ares", CharId = 1003, Status = 3, Level = 80, Class = 201, Nation = Nations.ElMorad, ZoneId = 48 },
                new() { Name = "Athena", CharId = 1004, Status = 0, Level = 64, Class = 204, Nation = Nations.ElMorad },
            });
            ShowCharacterPage(CharacterPage.Friends);
        }

        if (!_mainWindows.TryGetValue(windowName, out HudWindow? window))
            window = _mainWindows["Inventory"];
        window.Visible = true;
        RefreshMainWindow(windowName);
        _stHeaderName.Text = "Zeus    Lv 61";
        _stHeaderSub.Text = "Rogue   ·   El Morad";
        return DetachPreviewControl(window);
    }

    internal void SelectAdminItemTabUiPreview(string tab) => _admItemSearch.SelectTab(tab);

    internal void SetAdminZoneFilterUiPreview(string filter)
    {
        _admZoneFilter.Text = filter;
        RefreshAdminZoneList();
    }

    internal void SelectSkillTabUiPreview(int tab) => SelectSkillTab(tab);

    internal void ToggleBagUiPreview(int bagIndex) => ToggleBag(InventoryConstants.BagSlotFor(bagIndex));

    private const int PreviewPlayerAuthority = 1;

    internal Control BuildRoyaleBoardUiPreview()
    {
        ItemData.EnsureLoaded();
        OpenRoyaleBoard(1, 12, 245);
        _royaleBoardTime!.Text = KnightRoyale.Clock(245);
        _royaleBoard!.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        return DetachPreviewControl(_royaleBoard);
    }

    internal Control BuildRoyaleScoreUiPreview()
    {
        ItemData.EnsureLoaded();
        EnsureRoyaleScore();
        _royaleSurvivors!.Text = TextTemplate.Fill(ItemData.Text(KnightRoyale.SurvivorsText, "Survivor(s): %d"), 17);
        _royaleNextGas!.Text = TextTemplate.Fill(ItemData.Text(KnightRoyale.NextGasText, "Remaining time until # %d Poison Gas:"), 3);
        _royaleGasTime!.Text = KnightRoyale.Clock(94);
        _royaleScore!.Visible = true;
        return DetachPreviewControl(_royaleScore);
    }

    internal Control BuildChaosSkillPageUiPreview()
    {
        ItemData.EnsureLoaded();
        SkillData.EnsureLoaded();
        _zone = ChaosBattle.Zone;
        EnsureChaosSkillPage();
        RenderChaosSkillPage();
        _chaosSkillPage!.Visible = true;
        return DetachPreviewControl(_chaosSkillPage);
    }

    internal Control BuildChaosLadderUiPreview(bool final)
    {
        ItemData.EnsureLoaded();
        var me = Net.I.LastEnter;
        me.Name = "Zeus";
        me.Nation = Nations.ElMorad;
        me.Authority = PreviewPlayerAuthority;
        Net.I.SeedPreviewEnter(me);
        _chaosRanking = new ChaosRanking(new System.Collections.Generic.List<ChaosRankRow>
        {
            new("Velkan", 9, 2), new("Zeus", 7, 3), new("Aurelia", 7, 3), new("Morgrim", 5, 4), new("Rikka", 4, 6),
            new("Talon", 3, 1), new("Seren", 2, 5), new("Bram", 1, 7), new("Ilsa", 0, 4), new("Quill", 0, 9),
        }, 1_520_000, 3_040_000);
        ShowChaosLadder(final);
        return DetachPreviewControl(_chaosLadder!);
    }

    internal Control BuildClanHandoverUiPreview()
    {
        ItemData.EnsureLoaded();
        var me = Net.I.LastEnter;
        me.Name = "Zeus";
        Net.I.SeedPreviewEnter(me);
        Net.I.SeedPreviewClan(PreviewClan(asChief: true));
        _handoverPreselect = "Velkan";
        ShowHandoverWindow(HandoverListOk, new System.Collections.Generic.List<string> { "Aurelia", "Velkan", "Morgrim" });
        return DetachPreviewControl(_handoverWindow!);
    }

    private const float PreviewBadgeX = 800f;
    private const float PreviewBadgeY = 450f;
    private const int PreviewPulseFrames = 90;
    private const double PreviewFrameSeconds = 1.0 / 60.0;

    internal Control BuildCommanderUiPreview(byte order)
    {
        SkillData.EnsureLoaded();
        BuildCommanderBar();
        BuildCommanderAlarm();
        _commanderBar!.Visible = true;
        if (order != CommanderOrders.None) OnCommanderOrder(order);
        _commanderBadge.Position = new Vector2(PreviewBadgeX, PreviewBadgeY);
        for (int i = 0; i < PreviewPulseFrames; i++) _commanderPulse.Frame(PreviewFrameSeconds);
        ShowCommanderPulse();
        RemoveChild(_commanderAlarmLayer);
        return DetachPreviewControl(_commanderBar);
    }

    internal CanvasLayer CommanderAlarmUiPreview() => _commanderAlarmLayer;

    internal CanvasLayer OrderMessageUiPreview(bool own, float viewWidth)
    {
        ItemData.EnsureLoaded();
        PlaceOrderMessage(own
                ? "Zeus" + NoticeText(TextChaosStoneBroken, "You have destroyed the [Summoning Stone of Chaos] and opened the Gates of Chaos.")
                : NoticeText(TextMonumentLost, "Enemy nation has captured center Monument"),
            own ? OrderMessageRed : OrderMessageYellow, own ? OrderMessage.Flash : OrderMessage.Hold, viewWidth);
        RemoveChild(_orderMessageLayer);
        return _orderMessageLayer!;
    }

    internal void SearchBagUiPreview(string query)
    {
        if (_invSearch != null) _invSearch.Text = query;
        RefreshBagSearch();
    }

    internal Control BuildInventoryDeleteUiPreview(int items = 1)
    {
        for (int i = 0; i < items; i++) AskDeleteItem(GridStart + 1 + i);
        return DetachPreviewControl(_invDelPanel);
    }

    internal Control BuildAdminSpawnUiPreview()
    {
        BuildAdminPanelUiPreview("Find");
        OnAdminSpawnRow(new AdminSpawnRow
        {
            CanPersist = true, Index = 7800, NpcId = 31741, Name = "[Trader] Julia", Zone = 21, X = 769, Z = 369, Y = 35.2f,
            Direction = 90, Count = 1, RespawnSeconds = 3600, SpawnRange = 0, Alive = 1,
        });
        return DetachPreviewControl(_admSpawnPanel);
    }

    internal Control BuildAdminPanelUiPreview(string tab, string itemQuery = "raptor")
    {
        ItemData.EnsureLoaded();
        SkillData.EnsureLoaded();
        _isGm = true;
        _admEnabled = true;
        _selfClass = 105;
        Sheet.SeedProgress(level: 72, exp: 0, maxExp: 1_000_000);
        _admState = new AdminState
        {
            Granted = true,
            Class = 105, Level = 72,
            Str = 190, Sta = 148, Dex = 90, Intel = 60, MagicStat = 60,
            StatPoints = 12,
            MaxHp = 3_284, MaxMp = 812, Ap = 517, Ac = 936,
            Gold = 1_180_000,
            Loyalty = 18_450,
            SkillPoints = new byte[] { 6, 0, 0, 0, 0, 63, 42, 21, 0 },
            ClassOptions = new[] { 101, 106 },
        };
        _zone = 21;
        BuildAdminPanel();
        SelectAdminTab(tab);
        if (tab == "Races")
        {
            OnAdminCollectionRaces(
            [
                new AdminCollectionRace { Id = 1, Name = "Moradon Rookie Roundup", ZoneId = 21, MinLevel = 1, MaxLevel = 35, DurationMinutes = 60, AutoStart = true, Active = true, RemainingSeconds = 41 * 60 + 12, Completions = 3, Schedule = "Sun 10:00, Wed 15:00", Objectives = "15 x Kecoon, 10 x Bulcan, 10 x Werewolf" },
                new AdminCollectionRace { Id = 2, Name = "Moradon Apple Harvest", ZoneId = 21, MinLevel = 1, MaxLevel = 35, DurationMinutes = 60, AutoStart = true, Schedule = "Mon 11:00, Thu 16:00", Objectives = "15 x Apples of Moradon, 5 x Teeth of Bandicoot, 5 x Silk bundle" },
                new AdminCollectionRace { Id = 5, Name = "Wolves of Moradon", ZoneId = 21, MinLevel = 1, MaxLevel = 83, DurationMinutes = 60, AutoStart = true, Schedule = "Daily 12:00", Objectives = "15 x Werewolf, 10 x Dark Eyes, 10 x Dire Wolf" },
                new AdminCollectionRace { Id = 21, Name = "Ronark Apostles of Flame", ZoneId = 71, MinLevel = 61, MaxLevel = 70, DurationMinutes = 60, AutoStart = false, Schedule = "manual", Objectives = "15 x Apostle of Flame, 10 x Doom Soldier, 10 x Troll, 2 enemy players" },
            ]);
        }
        if (tab == AdminCommandsTab)
        {
            OnAdminCommands(
            [
                new GmCommandInfo("Items and currency", "+item <itemId> [count] | <name>", "Give yourself an item, or search items by name"),
                new GmCommandInfo("Items and currency", "+gold <amount>", "Give or take gold"),
                new GmCommandInfo("Items and currency", "+kc <name> <amount>", "Give or take Knight Cash"),
                new GmCommandInfo("Character", "+setlevel <1-83>", "Set the level; resets stats and mastery, clears the skill bar"),
                new GmCommandInfo("Character", "+gm", "Toggle GM mode: the GM aura, one-hit kills, 1 damage taken"),
                new GmCommandInfo("Players", "+summon <name|pattern>", "Bring a player, or everyone whose name matches (* = any text), to you"),
                new GmCommandInfo("Players", "+goto <name> | <zone> <x> <z>", "Go to a player or to a spot"),
                new GmCommandInfo("Events", "+jr / +bdw / +chaos [sec|min] / +templecancel", "Temple events: Juraid Mountain, Border Defense War, Chaos Dungeon"),
            ], open: false);
        }
        if (tab == "Find")
        {
            _admFindQuery.Text = "julia";
            _admFindLastQuery = "julia";
            OnAdminFind(Net.AdminFindNpcs, 3,
            [
                new AdminFindHit { Id = 31741, SpawnRow = 7800, Name = "[Trader] Julia", Level = 80, Zone = 21, X = 769, Z = 369 },
                new AdminFindHit { Id = 29235, SpawnRow = 3125, Name = "[VIP Manager] Juliane", Level = 80, Zone = 21, X = 797, Z = 546 },
                new AdminFindHit { Id = 32558, SpawnRow = 7554, Name = "[Analyst] Julius", Level = 80, Zone = 71, X = 605, Z = 890 },
            ]);
        }
        if (tab == "Items")
        {
            _admItemSearch.SetQuery(itemQuery);
            _admItemSearch.Run();
            if (itemQuery == "raptor") _admItemSearch.SelectPlus(8);
        }
        SetAdminStatus("Stats set — STR 190 STA 148 DEX 90 INT 60 MP 60, 12 free.", false);
        _admShown = true;
        _admPanel.Visible = true;
        return DetachPreviewControl(_admPanel);
    }

    internal void WalkToForSmoke(float koX, float koZ)
    {
        if (_self == null) return;
        _moveTarget = GroundPos(koX, koZ, _myKoY, _selfLift);
        _hasMoveTarget = true;
        ShowMoveIndicator(_moveTarget);
    }

    internal (float X, float Z) KoPositionForSmoke() => (_myKoX, _myKoZ);

    internal (int Players, int Npcs, int Monsters, int Shown) CensusForSmoke()
    {
        int players = 0, npcs = 0, monsters = 0, shown = 0;
        foreach (var e in _ents.Values)
        {
            if (!e.IsNpc) players++;
            else if (e.Attackable) monsters++;
            else npcs++;
            if (e.OnScreen) shown++;
        }
        return (players, npcs, monsters, shown);
    }

    internal void CastSkillForSmoke(int skillId) => CastSkill(skillId);

    internal void ShowAttendanceFailureUiPreview() =>
        OnAttendanceFailed(Net.EventBoardAttendanceClaim, Net.AttendanceClaimInventoryFull);

    internal Control BuildAttendanceUiPreview(bool noReply = false)
    {
        ItemData.EnsureLoaded();
        BuildItemTooltip();
        BuildAttendancePanel();
        if (noReply)
        {
            _attendanceShown = true;
            _attendancePanel.Visible = true;
            return DetachPreviewControl(_attendancePanel);
        }

        int total = Net.AttendanceDailySlots + Net.AttendanceBonusSlots;
        var slots = new int[total];
        var states = new byte[total];
        const int reached = 16;
        for (int i = 0; i < Net.AttendanceDailySlots; i++)
        {
            slots[i] = i + 1;
            states[i] = slots[i] < reached
                ? (i % 5 == 3 ? Net.AttendanceStateExpired : Net.AttendanceStateClaimed)
                : slots[i] == reached ? Net.AttendanceStateClaimable : AttendanceStateLocked;
        }
        for (int i = 0; i < Net.AttendanceBonusSlots; i++)
        {
            slots[Net.AttendanceDailySlots + i] = Net.AttendanceBonusFirstSlot + i;
            states[Net.AttendanceDailySlots + i] =
                i == 0 ? Net.AttendanceStateClaimable : AttendanceStateLocked;
        }

        OnAttendanceBoard(slots, states);
        _attendanceShown = true;
        _attendancePanel.Visible = true;
        return DetachPreviewControl(_attendancePanel);
    }

    internal Control OpenAttendanceUiPreview()
    {
        ItemData.EnsureLoaded();
        BuildItemTooltip();
        BuildAttendanceGift();
        BuildAttendancePanel();
        ShowAttendancePanel();
        return DetachPreviewControl(_attendancePanel);
    }

    internal Control? HoverAttendanceSlotUiPreview(int index)
    {
        if (index < 0 || index >= _attendanceGrid.GetChildCount()) return null;
        var socket = _attendanceGrid.GetChild(index)
            .GetChild(0).GetChild(0) as Control;
        socket?.EmitSignal(Control.SignalName.MouseEntered);
        return _itemTipPanel.Visible ? DetachPreviewControl(_itemTipPanel) : null;
    }

    internal Control BuildUpgradeUiPreview()
    {
        ItemData.EnsureLoaded();
        Sheet.SeedWealth(gold: 1_180_000, np: 2_450);
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(PreviewUpgradedWeapon, 1, 7000);
        Inv[GridStart + 1] = PreviewItem(UpgradeScrollHighBlessed, 3, 1);
        Inv[GridStart + 2] = PreviewItem(TrinaPiece, 5, 1);
        Inv[GridStart + 3] = PreviewItem(PreviewReverseWeapon, 1, 6200);
        Inv[GridStart + 4] = PreviewItem(PreviewUniqueRing, 1, 1);
        Inv[GridStart + 5] = PreviewItem(PreviewPotion, 24, 1);
        BuildUpgradePanel();
        _upgradeAnvilId = 1;
        _upgradeShown = true;
        _upgradePanel.Visible = true;
        StageAnvilUpgrade(PreviewUpgradedWeapon, UpgradeScrollHighBlessed, TrinaPiece);
        OnUpgradeResult(new UpgradeResult(2, UpgradeTypePreview, UpgradeResultSucceeded,
            new[] { new UpgradeSlotResult(PreviewUpgradedResult, 0) }));
        return DetachPreviewControl(_upgradePanel);
    }

    internal Control BuildRepairUiPreview()
    {
        ItemData.EnsureLoaded();
        Sheet.SeedWealth(gold: 1_180_000, np: 2_450);
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(PreviewUpgradedWeapon, 1, 1_100);
        Inv[GridStart + 1] = PreviewItem(PreviewKrowazTop, 1, 8_200);
        Inv[GridStart + 2] = PreviewItem(PreviewKrowazBoots, 1, 15_900);
        Inv[GridStart + 3] = PreviewItem(PreviewNoRepairHelmet, 1, 3_000);
        BuildInventoryPanel();
        BuildRepairPanel();
        _vendorNpcName = "[Blacksmith] Hepa";
        OpenRepair();
        return DetachPreviewControl(_repairPanel);
    }

    internal Control BuildWarehouseUiPreview()
    {
        ItemData.EnsureLoaded();
        Sheet.SeedWealth(gold: 1_180_000, np: 2_450);
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(PreviewUpgradedWeapon, 1, 7000);
        Inv[GridStart + 1] = PreviewItem(PreviewPotion, 24, 1);
        Inv[GridStart + 2] = PreviewItem(UpgradeScrollHighBlessed, 3, 1);
        Inv[GridStart + 3] = PreviewItem(PreviewNonStorableTattoo, 1, 1);
        BuildInventoryPanel();
        BuildWarehousePanel();
        _whShown = true;
        _whPanel.Visible = true;
        _whMoney = 4_820_000;
        _warehouse[0] = PreviewItem(PreviewReverseWeapon, 1, 6200);
        _warehouse[1] = PreviewItem(PreviewUniqueRing, 1, 1);
        _warehouse[2] = PreviewItem(PreviewUpgradedResult, 1, 7000);
        _warehouse[3] = PreviewItem(PreviewPotion, 40, 1);
        _warehouse[30] = PreviewItem(PreviewUpgradedWeapon, 1, 7000);
        _warehouse[60] = PreviewItem(TrinaPiece, 12, 1);
        RefreshWarehouse();
        _whStatus.Status($"Stored {WhItemLine(PreviewPotion, 10)}.", bad: false);
        return DetachPreviewControl(_whPanel);
    }

    private const int PreviewVaultDays = 23;
    private const int PreviewVaultHours = 5;

    internal Control BuildVipVaultUiPreview(bool locked)
    {
        ItemData.EnsureLoaded();
        Sheet.SeedWealth(gold: 1_180_000, np: 2_450);
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(PreviewUpgradedWeapon, 1, 7000);
        Inv[GridStart + 1] = PreviewItem(PreviewPotion, 24, 1);
        BuildInventoryPanel();
        BuildVipWarehousePanel();
        var ends = DateTimeOffset.UtcNow + (locked ? -TimeSpan.FromDays(2)
            : TimeSpan.FromDays(PreviewVaultDays) + TimeSpan.FromHours(PreviewVaultHours));
        var slots = new ItemSlot[VipWhSlots];
        slots[0] = PreviewItem(PreviewReverseWeapon, 1, 6200);
        slots[1] = PreviewItem(PreviewUniqueRing, 1, 1);
        slots[7] = PreviewItem(PreviewPotion, 40, 1);
        slots[20] = PreviewItem(TrinaPiece, 12, 1);
        OnVipWarehouseContents(new VipVaultContents(PinSet: !locked, Open: !locked, (uint)ends.ToUnixTimeSeconds(), slots));
        if (!locked) _vipWhStatus.Status($"Stored {WhItemLine(PreviewPotion, 10)}.", bad: false);
        return DetachPreviewControl(_vipWhPanel);
    }

    internal Control BuildClanBankUiPreview(bool member)
    {
        ItemData.EnsureLoaded();
        Sheet.SeedWealth(gold: 1_180_000, np: 2_450);
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(PreviewUpgradedWeapon, 1, 7000);
        Net.I.SeedPreviewClan(PreviewClan(asChief: !member));
        BuildInventoryPanel();
        BuildClanWarehousePanel();
        _clanWhShown = true;
        _clanWhPanel.Visible = true;
        var slots = new ItemSlot[ClanWhSlots];
        slots[0] = PreviewItem(PreviewReverseWeapon, 1, 6200);
        slots[2] = PreviewItem(PreviewPotion, 40, 1);
        slots[5] = PreviewItem(UpgradeScrollHighBlessed, 3, 1);
        slots[30] = PreviewItem(TrinaPiece, 12, 1);
        OnClanWhContents(2_400_000, slots);
        if (member) AskClanWithdraw(0, -1);
        return DetachPreviewControl(_clanWhPanel);
    }

    internal string SetWarehouseSearchUiPreview(string query)
    {
        _whSearch.Text = query;
        RefreshWarehouse();
        return $"search={query} hits={_whHits.Count}";
    }

    internal QuantityPrompt OpenWarehouseAmountUiPreview()
    {
        AskDepositAmount(GridStart + 1, -1);
        RemoveChild(_whAmount);
        foreach (var node in _whAmount.GetChildren())
            if (node is Control control) control.Theme = HudTheme.Shared;
        return _whAmount;
    }

    internal Control? HoverWarehouseCellUiPreview(bool bag, int index)
    {
        if (index < 0 || index >= _whCells.Length) return null;
        _whCells[index].EmitSignal(Control.SignalName.MouseEntered);
        return _itemTipPanel.Visible ? DetachPreviewControl(_itemTipPanel) : null;
    }

    internal Control BuildItemTooltipUiPreview(int itemId)
    {
        ItemData.EnsureLoaded();
        _selfClass = 105;
        Sheet.SeedProgress(level: 72, exp: 0, maxExp: 1_000_000);
        Sheet.SeedStats(str: 60, sta: 72, dex: 184, intel: 50, mag: 50, points: 3);
        Inv.EnsureLength(GridStart + GridCount);
        BuildInventoryPanel();

        var def = ItemData.Get(itemId);
        var ext = ItemData.ExtFor(itemId);
        int durability = (def?.Duration ?? 0) + (ext?.DurationBonus ?? 0);
        ShowItemTooltip(-1, new ItemSlot
        {
            ItemId = itemId,
            Count = 1,
            Durability = (short)Mathf.Min(durability, short.MaxValue),
        });
        return DetachPreviewControl(_itemTipPanel);
    }

    private static readonly (int Slot, int ItemId)[] PreviewDragonPieces =
    {
        (InventoryConstants.Head, 507003000),
        (InventoryConstants.Breast, 507001000),
        (InventoryConstants.Leg, 507002000),
    };
    private const int PreviewWornSword = 156211038;
    private const int PreviewBagSword = 156211031;

    private static readonly (int ItemId, int Count)[] PreviewLoot =
    {
        (Net.GoldItemId, 48210), (330620433, 1), (389070000, 3), (156211038, 1), (379016000, 12),
    };

    internal Control BuildLootFilterUiPreview()
    {
        ItemData.EnsureLoaded();
        var filter = CurrentLootFilter();
        filter.Enabled = true;
        filter.MinSellPrice = 500;
        filter.SetRule(389010000, true);
        filter.SetRule(379021000, false);
        BuildLootFilter();
        RefreshLootRules();
        _lootFilterWindow!.Visible = true;
        return DetachPreviewControl(_lootFilterWindow);
    }

    internal Control BuildRankBoardUiPreview()
    {
        ItemData.EnsureLoaded();
        BuildRankBoard();
        _rankBoardRows = new List<RankBoardRow>
        {
            new(RankBoardKind.King, 0, "Valerian", 12, 0, "Wolves", 0),
            new(RankBoardKind.Clan, 0, "Valerian", 12, 0, "Wolves", 0),
            new(RankBoardKind.Clan, 1, "Morgana", 15, 0, "Ironclad", 0),
            new(RankBoardKind.Clan, 2, "Theo", 18, 0, "Ashen Vow", 0),
            new(RankBoardKind.Player, 0, "Veteran", 12, 0, "Wolves", 0),
            new(RankBoardKind.Player, 1, "Rising", 0, 0, "", 0),
            new(RankBoardKind.Player, 2, "Morgana", 15, 0, "Ironclad", 0),
            new(RankBoardKind.TopKiller, 0, "Veteran", 12, 0, "Wolves", 412),
        };
        FillRankBoard();
        _rankBoard.Visible = true;
        return DetachPreviewControl(_rankBoard);
    }

    internal Control BuildRankerPaperUiPreview()
    {
        ItemData.EnsureLoaded();
        _rankerType = RankerStatue.KarusFirstType;
        _rankerName = "Veteran";
        _rankerText = "Glory to Karus. Meet me at the Ronark Land bridge every evening; the strong are always welcome in our ranks.";
        ShowRankerPaper();
        return DetachPreviewControl(_rankerPaper);
    }

    internal Control BuildDropListUiPreview()
    {
        ItemData.EnsureLoaded();
        BuildTargetHud();
        _targetName.Text = "Orc Watcher   Lv 41";
        _targetHp.Set(800, 1000);
        _targetBox.Visible = true;
        _dropListBtn.Visible = true;
        _dropPanel.Visible = true;
        ShowDropList(new DropListReply(1, true, 700, 1000, 420, new List<DropLine>
        {
            new(389010000, DropBand.Common),
            new(379021000, DropBand.Uncommon),
            new(110110001, DropBand.Rare),
            new(379016000, DropBand.VeryRare),
        }));
        return DetachPreviewControl(_targetBox);
    }

    internal Control BuildEventsUiPreview(string variant = "")
    {
        Sheet.SeedProgress(level: 62, exp: 0, maxExp: 1_000_000);
        BuildEventsWindow();
        var utc = DateTime.UtcNow;
        var soon = utc.AddMinutes(PreviewEventLeadMinutes);
        const int daily = EventSchedule.EveryDay;
        _eventSchedule = new EventSchedule(TimeSpan.Zero,
            new List<EventNow>
            {
                new(EventKind.Temple, (int)TempleEventType.BorderDefenseWar, "Border Defense War", EventPhase.SignUp, PreviewSignUpSeconds, 20, 83),
                new(EventKind.CollectionRace, 0, "Moradon Rookie Roundup", EventPhase.Running, PreviewRaceSeconds, 1, 35),
            },
            new List<ScheduledEvent>
            {
                new(EventKind.Temple, "Border Defense War", "8 vs 8 capture the flag", 20, 83,
                    new List<EventSlot> { new(daily, 23, 0), new(daily, 8, 0), new(daily, 10, 0), new(daily, 13, 0), new(daily, 18, 0) }),
                new(EventKind.Temple, "Forgotten Temple", "Hold out against the monster waves", 46, 59,
                    new List<EventSlot> { new(daily, soon.Hour, soon.Minute), new(daily, 16, 0) }),
                new(EventKind.Temple, "Under The Castle", "Boss dungeon, one hour", 70, 83,
                    new List<EventSlot> { new((int)DayOfWeek.Friday, 18, 0) }),
                new(EventKind.CastleSiege, "Castle Siege War", "Clan war for Delos Castle", 0, 0,
                    new List<EventSlot> { new((int)DayOfWeek.Sunday, 17, 30) }),
                new(EventKind.Krowaz, "Krowaz's Dominion", "Opens for 60 minutes", 70, 0,
                    new List<EventSlot> { new(daily, 7, 0), new(daily, 20, 0) }),
                new(EventKind.Lottery, "Moradon Grand Lottery", "Lottery draw, 15 minutes", 0, 0,
                    new List<EventSlot> { new(daily, 12, 0), new(daily, 16, 0), new(daily, 20, 0) }),
                new(EventKind.CollectionRace, "Moradon Rookie Roundup", "Collection Race, 60 minutes, first 3 win", 1, 35,
                    new List<EventSlot> { new((int)DayOfWeek.Sunday, 10, 0), new((int)DayOfWeek.Wednesday, 11, 0) }),
            },
            utc);
        if (variant == PreviewEventsRaces) _eventsTabRaces.ButtonPressed = true;
        RefreshEventsWindow();
        _eventsPanel.Visible = true;
        if (variant == PreviewEventsEnd)
        {
            var bar = _eventsScroll.GetVScrollBar();
            bar.Changed += () => bar.Value = bar.MaxValue;
        }
        return DetachPreviewControl(_eventsPanel);
    }

    private const int PreviewEventLeadMinutes = 15;
    internal const string PreviewEventsRaces = "races";
    internal const string PreviewEventsEnd = "end";
    private const int PreviewSignUpSeconds = 412;
    private const int PreviewRaceSeconds = 1830;

    internal Control BuildLootUiPreview()
    {
        ItemData.EnsureLoaded();
        BuildLootWindow();
        _lootEntries.Clear();
        foreach (var (id, count) in PreviewLoot) _lootEntries.Add(new LootEntry(id, count));
        RefreshLootWindow();
        return DetachPreviewControl(_lootPanel);
    }

    internal Control BuildItemTooltipCaseUiPreview(string kind)
    {
        ItemData.EnsureLoaded();
        _selfClass = 105;
        Sheet.SeedProgress(level: 72, exp: 0, maxExp: 1_000_000);
        Sheet.SeedStats(str: 60, sta: 72, dex: 184, intel: 50, mag: 50, points: 3);
        Inv.EnsureLength(GridStart + GridCount);
        BuildInventoryPanel();

        switch (kind)
        {
            case "set":
                foreach (var (slot, id) in PreviewDragonPieces) Inv[slot] = TooltipItem(id);
                ShowItemTooltip(InventoryConstants.Head, Inv[InventoryConstants.Head]);
                return DetachPreviewControl(_itemTipPanel);
            case "bound":
            {
                var item = TooltipItem(PreviewWornSword);
                item.Flag = (byte)ItemFlag.Bound;
                ShowItemTooltip(-1, item);
                return DetachPreviewControl(_itemTipPanel);
            }
            default:
            {
                Inv[InventoryConstants.RightHand] = TooltipItem(PreviewWornSword);
                Inv[GridStart] = TooltipItem(PreviewBagSword);
                _tooltipCompareForced = true;
                ShowItemTooltip(GridStart, Inv[GridStart]);
                _tooltipCompareForced = false;
                var pair = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                pair.AddThemeConstantOverride("separation", (int)TooltipPanelGap);
                pair.AddChild(DetachPreviewControl(_itemTipPanel));
                pair.AddChild(DetachPreviewControl(_itemTipCompare));
                return pair;
            }
        }
    }

    internal Control BuildAnvilChoiceUiPreview()
    {
        QuestText.EnsureLoaded();
        BuildNpcDialog();
        ShowAnvilChoice();
        return DetachPreviewControl(_npcPanel);
    }

    internal Control BuildAnvilAccessoryUiPreview()
    {
        ItemData.EnsureLoaded();
        Sheet.SeedWealth(gold: 1_180_000, np: 2_450);
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(PreviewCompoundRing, 1, 1);
        Inv[GridStart + 1] = PreviewItem(PreviewCompoundRing, 1, 1);
        Inv[GridStart + 2] = PreviewItem(PreviewCompoundRing, 1, 1);
        Inv[GridStart + 3] = PreviewItem(AccessoryCompoundScrollFirst, 4, 1);
        Inv[GridStart + 4] = PreviewItem(PreviewUpgradedWeapon, 1, 7000);
        BuildUpgradePanel();
        _upgradeAnvilId = 1;
        _anvilTabs.Select((int)AnvilBench.Accessory, notify: false);
        SetAnvilBench(AnvilBench.Accessory);
        _upgradeShown = true;
        _upgradePanel.Visible = true;
        PlaceUpgradeItem(GridStart);
        PlaceUpgradeItem(GridStart + 1);
        PlaceUpgradeItem(GridStart + 3);
        PlaceUpgradeItem(GridStart + 4);
        Callable.From(HoldBenchHeight).CallDeferred();
        return DetachPreviewControl(_upgradePanel);
    }

    internal Control BuildAnvilScanUiPreview()
    {
        var panel = BuildUpgradeUiPreview();
        _upgradeSession.Sent();
        SetAnvilStrip(_anvilStrip.Title.Text, "Upgrade in progress...", UiTheme.TextLo);
        _anvilLids.Visible = true;
        _anvilLidTop.AnchorBottom = 0.5f;
        _anvilLidBottom.AnchorTop = 0.5f;
        _anvilSeam.AnchorRight = PreviewScanShare;
        RefreshUpgradeActions();
        return panel;
    }

    internal Control BuildAnvilOutcomeUiPreview(bool succeeded)
    {
        var panel = BuildUpgradeUiPreview();
        _upgradeSession.Sent();
        _upgradeSession.Answered();
        ApplyUpgradeResult(new UpgradeResult(2, UpgradeTypeNormal,
            succeeded ? UpgradeResultSucceeded : UpgradeResultFailed,
            new[] { new UpgradeSlotResult(succeeded ? PreviewUpgradedResult : 0, 0) }), onBench: true);
        return panel;
    }

    internal Control BuildNpcServiceChoiceUiPreview()
    {
        QuestText.EnsureLoaded();
        BuildNpcDialog();
        _vendorNpcName = "[Sundries] Zarta";
        OnRepairOpen(255000);
        return DetachPreviewControl(_npcPanel);
    }

    internal Control BuildAchievementUiPreview(AchievementTab category, bool hideClaimed = false, bool completedOnly = false,
        int group = AchievementData.AllGroups)
    {
        AchievementInit();
        _achTab = category;
        _achGroup = group;
        _achOnSummary = false;
        ApplyAchievementPageVisibility();
        _achHideClaimed.ButtonPressed = hideClaimed;
        if (completedOnly)
        {
            _achStatusFilter = AchievementStatusFilter.Completed;
            _achStatus.Selected = Array.FindIndex(AchievementFilter.Options, o => o.Filter == AchievementStatusFilter.Completed);
        }
        var entries = PreviewAchievementList();
        if (completedOnly)
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].State == Net.AchievementStateAchieved)
                    entries[i] = entries[i] with { State = Net.AchievementStateClaimed };
        OnAchievementList(entries);
        _achPanel.Visible = true;
        _achShown = true;
        RebuildAchievementList(keepScroll: false);
        return DetachPreviewControl(_achPanel);
    }

    internal Control BuildAchievementSummaryUiPreview(bool withReport = true)
    {
        AchievementInit();
        _achOnSummary = true;
        ApplyAchievementPageVisibility();
        OnAchievementList(PreviewAchievementList());

        if (withReport)
        {
            OnAchievementSummary(new AchievementSummary
            {
                PlayMinutes = 107401,
                MonstersDefeated = 139847,
                PlayersDefeated = 687,
                Deaths = 765,
                Points = 1700,
                RecentlyAchieved = PreviewRecentAchievements(),
                AchievedPerTab = new int[Net.AchievementTabCount],
            });
        }

        _achPanel.Visible = true;
        _achShown = true;
        RefreshAchievementCounts();
        RebuildAchievementSummary();
        return DetachPreviewControl(_achPanel);
    }

    private static int[] PreviewRecentAchievements()
    {
        var recent = new int[Net.AchievementRecentSlots];
        int slot = 0;
        foreach (int id in AchievementData.Ids)
        {
            if (slot >= recent.Length) break;
            if (AchievementData.Get(id) is not { } info || info.Name.Length == 0) continue;
            recent[slot++] = id;
        }
        return recent;
    }

    private static System.Collections.Generic.List<AchievementEntry> PreviewAchievementList()
    {
        var list = new System.Collections.Generic.List<AchievementEntry>();
        int index = 0;
        foreach (int id in AchievementData.Ids)
        {
            int state = (index % 9) switch
            {
                0 => Net.AchievementStateAchieved,
                1 or 2 => Net.AchievementStateClaimed,
                _ => Net.AchievementStateInProgress,
            };
            int target = 10 + index % 40 * 5;
            int progress = state == Net.AchievementStateInProgress
                ? target * (index % 7) / 8
                : target;
            list.Add(new AchievementEntry
            {
                Id = id, State = state, Progress = progress, Target = target,
            });
            index++;
        }
        return list;
    }

    private static MyClanInfo PreviewClan(bool asChief, byte flag = ClanTypes.Accredited5) => new()
    {
        InClan = true, ClanId = 1, Name = "Olympus", Flag = flag, Fame = asChief ? ClanRanks.Chief : ClanRanks.Trainee,
        Grade = 2, Ranking = 3, PointFund = 5_000_000, Notice = "Siege practice tonight. Bring repair scrolls.",
        Online = 3, MaxMembers = ClanTypes.MaxMembers,
    };

    private static System.Collections.Generic.List<ClanMember> PreviewClanMembers() => new()
    {
        new() { Name = "Zeus", Fame = ClanRanks.Chief, Level = 61, Class = 207, IsOnline = true, Memo = "Siege at 21:00" },
        new() { Name = "Hera", Fame = ClanRanks.ViceChief, Level = 72, Class = 202, IsOnline = true, Memo = "" },
        new() { Name = "Ares", Fame = ClanRanks.Trainee, Level = 72, Class = 201, IsOnline = false, Memo = "Back next week", HoursSinceLogin = 187 * 24 },
        new() { Name = "Athena", Fame = ClanRanks.Trainee, Level = 72, Class = 204, IsOnline = true, Memo = "" },
        new() { Name = "Tester10", Fame = ClanRanks.Trainee, Level = 31, Class = 205, IsOnline = false, Memo = "", HoursSinceLogin = 5 },
    };

    internal Control BuildCapeUiPreview(bool asChief = true, int select = -1)
    {
        var me = Net.I.LastEnter;
        me.Name = asChief ? "Zeus" : "Tester10";
        Net.I.SeedPreviewEnter(me);
        Net.I.SeedPreviewClan(PreviewClan(asChief));

        CapeInit();
        _capePanel.Visible = true;
        _capeShown = true;

        BuildCapeCatalogue();
        if (select >= 0)
        {
            ShowCapePattern(Cape.TryGet(select, out var pick) ? pick.M : 0);
            SelectCape(select);
        }
        UpdateCapeGate();
        return DetachPreviewControl(_capePanel);
    }

    internal Control BuildClanUiPreview(bool inClan, bool asChief = true, ClanTab tab = ClanTab.Members)
    {
        var me = Net.I.LastEnter;
        me.Name = asChief ? "Zeus" : "Tester10";
        Net.I.SeedPreviewEnter(me);
        Net.I.SeedPreviewClan(inClan ? PreviewClan(asChief) : new MyClanInfo { Name = "", Notice = "", MaxMembers = ClanTypes.MaxMembers });

        var window = BuildPrimaryWindowUiPreview("Character");
        ShowCharacterPage(CharacterPage.Clan);
        _clanLoaded = true;

        if (inClan)
        {
            OnClanMembers(PreviewClanMembers());
            if (tab == ClanTab.Points)
            {
                ShowClanTab(ClanTab.Points);
                OnClanDonationList(new System.Collections.Generic.List<(string, int)>
                {
                    ("Hera", 41_200), ("Zeus", 33_900), ("Ares", 12_050), ("Athena", 800),
                });
            }
            if (tab == ClanTab.Top)
            {
                ShowClanTab(ClanTab.Top);
                OnClanTop10(new System.Collections.Generic.List<(int, int, string)>
                {
                    (Nations.Karus, 0, "NightWatch"), (Nations.Karus, 1, "Wolves"), (Nations.Karus, 2, "Ravens"),
                    (Nations.ElMorad, 0, "Doves"), (Nations.ElMorad, 1, "Lions"),
                });
            }
        }

        return window;
    }

    internal Control BuildClanPointsUiPreview(bool asChief = true)
    {
        var me = Net.I.LastEnter;
        me.Name = asChief ? "Zeus" : "Tester10";
        Net.I.SeedPreviewEnter(me);
        Sheet.SeedWealth(gold: 1_180_000, np: 12_450);
        Net.I.SeedPreviewClan(PreviewClan(asChief));

        ClanInit();
        _clanPointsShown = true;
        _clanPointsPanel.Visible = true;
        ApplyClanPointsState();
        OnClanPointStatus(true, 12_450, 5_000_000);
        return DetachPreviewControl(_clanPointsPanel);
    }

    internal Control BuildWarpUiPreview(bool blocked = false)
    {
        Sheet.SeedProgress(level: 42, exp: 0, maxExp: 1_000_000);
        Sheet.SeedWealth(9_500, 0);
        BuildWarpPanel();
        _vendorNpcName = "El Morad Warp Gate";
        OnWarpList(new System.Collections.Generic.List<Net.WarpListEntry>
        {
            new(2121, "Folk Village", "", 22, 0, 3_000),
            new(2122, "Tale Village", "", 22, 0, 3_000),
            new(2123, "El Morad Castle", "", 2, 0, 5_000),
            new(2124, "Lunar Valley", "", 2, 0, 10_000),
            new(2125, "Delos", "", 30, 0, 17_000),
            new(2126, "Ardream", "", 72, 0, 3_000),
            new(2127, "Ronark Land Base", "", 73, 0, 17_000),
            new(2128, "Ronark Land", "", 71, 0, 17_000),
            new(7115, "Moradon", "", 21, 0, 0),
        });
        int row = blocked ? 7 : 2;
        SelectWarpRow(row);
        Callable.From(() => SelectWarpRow(row)).CallDeferred();
        return DetachPreviewControl(_warpPanel);
    }

    internal Control BuildNpcMsgUiPreview(bool quests)
    {
        QuestText.EnsureLoaded();
        BuildNpcDialog();
        if (quests)
        {
            Sheet.SeedProgress(level: 60, exp: 0, maxExp: 1_000_000);
            var me = Net.I.LastEnter;
            me.Nation = Nations.ElMorad;
            Net.I.SeedPreviewEnter(me);
            _selfClass = 202;
            _zone = 21;
            _quests.Add(new QuestEntry(60, 1));
            _quests.Add(new QuestEntry(62, 2));
            _quests.Add(new QuestEntry(65, 3));
            _quests.Add(new QuestEntry(67, 4));
            OnNpcMsg(167, 13013);
            if (_npcOffers.Count == 0)
            {
                _npcOffers = new List<QuestData.Offer>
                {
                    new(60, 0, 0), new(62, 0, QuestStateActive), new(65, 0, QuestStateReadyToTurnIn),
                };
                ShowNpcOffers();
            }
        }
        else
        {
            OnNpcMsg(9170, 31511);
        }
        return DetachPreviewControl(_npcPanel);
    }

    internal void SetQuestViewUiPreview(QuestView view)
    {
        if (_npcPanel == null && !view.Notification)
        {
            _questSelected = view.QuestId;
            OnQuestView(view with { Open = false });
            return;
        }
        OnQuestView(view);
    }

    internal Control BuildQuestOfferUiPreview(string kind = "")
    {
        ItemData.EnsureLoaded();
        QuestText.EnsureLoaded();
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(810090000, 1, 1);
        Sheet.SeedWealth(gold: 1_180_000, np: 2_450);
        _selfClass = 1;
        BuildItemTooltip();
        BuildNpcDialog();
        ShowQuestView(kind == "hunt"
            ? new QuestView(314, 14201, 2, true, false, true, false,
                QuestViewState.Claimable, 0, "Hunting the Werewolves",
                "Hunt Werewolves and Lycans for [Warrior Master] Skaki.",
                "You did it! Pick the reward you like.",
                new QuestObjectives(314, false, [new QuestKillGroup(10, [1301], "Werewolf"), new QuestKillGroup(12, [1302], "Lycan")]),
                [10, 7],
                [
                    new QuestTransfer(false, 2, 0, 250_000, 0),
                    new QuestTransfer(false, 1, 0, 50_000, 0),
                    new QuestTransfer(false, 3, 0, 20, 0),
                ], [])
            {
                Options = [new QuestTransfer(false, 0, 156211031, 1, 0), new QuestTransfer(false, 0, 389015000, 20, 0)],
            }
            : kind == "talk"
            ? new QuestView(663, 31561, 2, true, true, false, false,
                QuestViewState.Available, 0, "How to hunt",
                "You must talk to [Hunter] Halon.",
                "So you think you've got enough skills? Have you even had a proper hunting before?",
                new QuestObjectives(663, false, []), [], [], [])
            : kind == "fee"
            ? new QuestView(71, 18004, 21, true, true, false, false,
                QuestViewState.Available, 0, "1st job change",
                "I have to pay [Grand Merchant] Kaishan for the job change.",
                "So you want to change jobs? It costs 3,000 gold to register the change with the guild.",
                new QuestObjectives(71, false, []), [],
                [
                    new QuestTransfer(true, 1, 0, 3000, 0),
                    new QuestTransfer(false, 4, 0, 1, 0),
                    new QuestTransfer(false, 2, 0, 1500, 0),
                ], [])
            : new QuestView(273, 14201, 2, true, true, false, false,
                QuestViewState.Available, 0, "2nd job change",
                "I promised to bring [Warrior Master] Skaki collectible items.",
                "Oh, it's time for you to undergo a 2nd job change? You have persevered well. Don't be lax as "
                + "the true chivalric mission begins now. You need the ingredients below to perform the 2nd job "
                + "change ritual.",
                new QuestObjectives(273, false, []), [],
                [
                    new QuestTransfer(true, 0, 810095000, 1, 0),
                    new QuestTransfer(true, 0, 810090000, 1, 0),
                    new QuestTransfer(true, 0, 810094000, 1, 0),
                    new QuestTransfer(false, 5, 0, 1, 0),
                ], []));
        return DetachPreviewControl(_npcPanel);
    }

    internal Control? HoverQuestOfferRowUiPreview(int index)
    {
        var rows = _npcQuestContent.GetChildren()
            .OfType<PanelContainer>()
            .SelectMany(section => section.GetChildren().OfType<VBoxContainer>())
            .SelectMany(body => body.GetChildren().OfType<Control>())
            .ToList();
        if (index < 0 || index >= rows.Count) return null;
        rows[index].EmitSignal(Control.SignalName.MouseEntered);
        return _itemTipPanel.Visible ? DetachPreviewControl(_itemTipPanel) : null;
    }

    private const int PreviewNpcFinderZone = 21;
    private const int PreviewNpcFinderKind = 2;
    private static readonly Vector2 PreviewNpcFinderSpot = new(700f, 520f);

    internal Control BuildNpcFinderUiPreview()
    {
        _zone = PreviewNpcFinderZone;
        _miniMap = new MiniMap();
        _miniMap.SetZone("moradon", "Moradon");
        _npcFinderKind = PreviewNpcFinderKind;
        _blipScratch.Clear();
        AddNpcFinderBlips();
        _miniMap.UpdateView(PreviewNpcFinderSpot.X, PreviewNpcFinderSpot.Y, 0f, _blipScratch);
        _miniMap.SetNpcKinds(NpcFinder.KindsPresent(NpcSpots, _zone, Net.I.LastEnter.Nation), _npcFinderKind);
        _miniMap.ShowNpcList();
        return _miniMap;
    }

    internal Control BuildQuestMapUiPreview()
    {
        _zone = 21;
        _miniMap = new MiniMap();
        AddChild(_miniMap);
        _miniMap.SetZone("moradon", "Moradon");
        OnQuestTarget(new QuestTargetDetail(62, 21, 632, 465, "Bandicoot hunt", "Bandicoot",
            "Bandicoot\nA rodent which grew abnormally by feeding on corpses of evil races in the War with Pathos.",
            "Inhabits Seashore of Hope"));
        return DetachPreviewControl(_questTargetWindow!);
    }

    internal Control BuildQuestReceiptUiPreview()
    {
        OnQuestReceipt(new QuestReceipt(62, [
            new QuestReceiptEntry(900001000, 375), new QuestReceiptEntry(900000000, 2700)]));
        return DetachPreviewControl(_questReceiptWindow!);
    }

    internal Control BuildQuestProgressToastUiPreview(string quest, string objective, int done, int needed)
    {
        ShowQuestProgressToast(quest, objective, done, needed);
        return DetachPreviewControl(_questToastPanel!);
    }

    internal Control BuildBattleBoardUiPreview(int zone, int karus, int elmo)
    {
        BuildBattleEventUi();
        _battleeventBoard.TooltipText = BattleZoneName(zone);
        _battleeventBoard.SetScores(karus, elmo);
        _battleeventRemaining = PreviewBattleSecondsLeft;
        UpdateBattleTimerLabel();
        _battleeventBoard.Visible = true;
        return DetachPreviewControl(_battleeventBoard);
    }

    internal Control BuildBorderWarUiPreview(int karus, int elmorad, string carrier, byte nation)
    {
        _bdwHudLayer = new CanvasLayer();
        AddChild(_bdwHudLayer);
        BuildBdwScoreBanner();
        _bdwScoreBanner.SetScores(karus, elmorad);
        ShowBdwCarrier(carrier, nation);
        _bdwScoreBanner.RankingShown = true;
        _bdwScoreBanner.SetClock($"End  {ClockText(PreviewWarEndSeconds)}");
        _bdwScoreBanner.Visible = true;
        return DetachPreviewControl(_bdwScoreBanner);
    }

    private const int PreviewGeniePoints = 87;

    internal Control BuildGenieMiscUiPreview() => BuildGenieTabUiPreview(PreviewGenieMiscTab);

    internal Control BuildGenieMainUiPreview() => BuildGenieTabUiPreview(PreviewGenieMainTab);

    internal Control BuildUserVoteSelectUiPreview() => BuildUserVoteSelect(2, "Rikka");

    internal Control BuildUserVoteAskUiPreview() =>
        BuildUserVoteAsk(new UserVoteAsk("Rikka", UserVote.ReasonOther, "left the bridge to the enemy"));

    private Control BuildGenieTabUiPreview(int tab)
    {
        GenieInit();
        if (_geniePanel.Body.GetChildOrNull<TabContainer>(0) is { } tabs) tabs.CurrentTab = tab;
        _geniePanel.Visible = true;
        return DetachPreviewControl(_geniePanel);
    }

    private const int PreviewGenieMainTab = 0;
    private const int PreviewGenieMiscTab = 1;

    internal Control BuildGenieSubUiPreview()
    {
        _genieLayer = new CanvasLayer();
        AddChild(_genieLayer);
        GenieSubInit();
        _genieMonsters.Clear();
        _genieMonsters.Add("Worm");
        _genieMonsters.Add("Bulcan");
        _genieMonstersOff.Add("Worm");
        _geniePoints = PreviewGeniePoints;
        RefreshGenieSub();
        return DetachPreviewControl(_genieSub!);
    }

    private const int PreviewNationRoom = 1002;

    internal Control BuildNationChatUiPreview(string page)
    {
        NationChatInit();
        _nationChatShown = true;
        _ncList.Visible = true;
        Net.I.NationChat.Left();
        switch (page)
        {
            case "public":
                Net.I.NationChat.EnteredPublic(2);
                ShowNationTabQuietly(publicTab: true);
                RefreshNationButtons();
                _ncSelectedChannel = 3;
                OnNationChannels(
                [
                    new NationChannel(1, "General", 212), new NationChannel(2, "Trade", 87),
                    new NationChannel(3, "Party", 41), new NationChannel(4, "Help", 9),
                ]);
                return DetachPreviewControl(_ncList);
            case "create":
                OpenNationCreate();
                _ncCreateTitle.Text = "Siege at nine";
                _ncCreateOption.Selected = 1;
                ApplyNationCreateOption();
                _ncCreatePassword.Text = "1234";
                _ncCreateSize.Selected = 1;
                return DetachPreviewControl(_ncCreate);
            default:
                Net.I.NationChat.OwnRoom(PreviewNationRoom, "Siege at nine", true, "1234", 30);
                ShowNationTabQuietly(publicTab: false);
                RefreshNationButtons();
                _ncSelectedRoom = 1004;
                OnNationRooms(
                [
                    new NationRoom(1001, "Moradon traders", false, Nations.Karus, 12, 50),
                    new NationRoom(PreviewNationRoom, "Siege at nine", true, Nations.Karus, 4, 30),
                    new NationRoom(1003, "Colony hunters", false, Nations.ElMorad, 7, 10),
                    new NationRoom(1004, "Clan recruits", true, Nations.Karus, 2, 10),
                ]);
                return DetachPreviewControl(_ncList);
        }
    }

    private const int PreviewRoomSelf = 9006;
    private const int PreviewRoomRikka = 9001;
    private const int PreviewRoomBorin = 9003;
    private const int PreviewRoomArwen = 9004;
    private const int PreviewRoomKaito = 9005;
    private const int PreviewRoomMembers = 6;
    private const int PreviewRoomCrowdBase = 9100;
    private const int PreviewRoomCrowdInCall = 3;
    private static readonly string[] PreviewRoomCrowd =
    [
        "Nightwalker", "Mira", "Rin", "Aldric", "Selene", "Gorr", "Thorvald", "Isolde", "Kasimir",
        "Liora", "Brannoch", "Maelis", "Oskar",
    ];
    private const int PreviewRoomSize = 30;
    private const int PreviewPublicMembers = 212;
    private const float PreviewRecordingLevel = 0.55f;
    private const double PreviewRecordingSeconds = 7;
    private const double PreviewRoomRepostSeconds = 0.3;
    private const float PreviewRoomPlayed = 0.38f;
    private const int PreviewRoomNotesScroll = 150;
    private const float PreviewRoomLoaded = 0.6f;

    private static byte[] PreviewWaveform(int seed)
    {
        var levels = new List<float>();
        for (int i = 0; i < 300; i++)
            levels.Add((float)(0.02 + 0.3 * Math.Abs(Math.Sin(i * 0.21 * seed) * Math.Sin(i * 0.047 + seed))));
        return VoiceWaveform.FromLevels(levels);
    }
    private const float PreviewRoomSettingsLevel = 0.62f;
    private const long PreviewReactedPost = 103;
    private const long PreviewReactPost = 106;

    private void SeedPreviewRoomReactions(RoomTimeline timeline)
    {
        string heart = ReactionRules.QuickPicks[1];
        if (timeline.Find(PreviewReactedPost) is { } asked)
        {
            asked.Reactions.Set(PreviewRoomBorin, "Borin", heart);
            asked.Reactions.Set(PreviewRoomArwen, "Arwen", heart);
            asked.Reactions.Set(_myId, "Tester10", "🙏");
        }
        timeline.Find(105)?.Reactions.Set(PreviewRoomKaito, "Kaito", "👍");
        if (timeline.Find(108) is { } mine)
        {
            mine.Reactions.Set(PreviewRoomRikka, "Rikka", "😂");
            mine.Reactions.Set(PreviewRoomBorin, "Borin", "😂");
            mine.Reactions.Set(PreviewRoomArwen, "Arwen", "(Laughing)");
            mine.Reactions.Set(PreviewRoomKaito, "Kaito", "😮");
        }
    }

    private void PreviewRoomReactionPopup(bool details)
    {
        if (!IsInstanceValid(_reactionLayer)) ChatReactionsInit();
        EnsureReactionPopups();
        var timer = new Godot.Timer { WaitTime = PreviewRoomRepostSeconds, OneShot = true, Autostart = true };
        timer.Timeout += () =>
        {
            Window popup = details ? _reactionDetails! : _reactionBar!;
            popup.GetParent().RemoveChild(popup);
            _roomWindow.AddChild(popup);
            if (details && Net.I.RoomTimeline.Find(PreviewReactedPost) is { } reacted && _roomThread.PillOf(reacted) is { } pill)
            {
                _roomThread.StickBottom = false;
                OpenReactionDetails(_roomThread, reacted, pill);
            }
            else if (!details && Net.I.RoomTimeline.Find(PreviewReactPost) is { } target)
            {
                _roomThread.Pinned = target;
                _roomThread.ShowReactButton(target);
                if (_roomThread.ReactButtonOf(target) is { } button) OpenReactionBar(_roomThread, target, button);
            }
        };
        _roomWindow.AddChild(timer);
    }
    private static readonly Vector2I PreviewRoomSettingsAt = new(1010, 420);

    internal Control BuildChatRoomUiPreview(string page)
    {
        _myId = PreviewRoomSelf;
        var me = Net.I.LastEnter;
        me.Name = "Tester10";
        me.Nation = Nations.Karus;
        Net.I.SeedPreviewEnter(me);
        ChatRoomInit();
        var room = Net.I.NationChat;
        room.Left();
        var now = DateTime.Now;
        var evening = now.Date.AddDays(-1).AddHours(21);
        if (page == "public")
        {
            room.EnteredPublic(1);
            room.Describe("General", false, PreviewPublicMembers, NationChatRules.PublicCapacity);
        }
        else
        {
            if (page is "voice" or "settings" or "config") room.OwnRoom(PreviewNationRoom, "Siege at nine", true, "1234", PreviewRoomSize);
            else room.EnteredPrivate(PreviewNationRoom);
            room.Describe("Siege at nine", true, PreviewRoomMembers, PreviewRoomSize);
            var members = new List<NationRoomMember>
            {
                new(_myId, "Tester10"), new(PreviewRoomRikka, "Rikka"), new(PreviewRoomBorin, "Borin"),
                new(PreviewRoomArwen, "Arwen"), new(PreviewRoomKaito, "Kaito"),
            };
            foreach (var (name, i) in PreviewRoomCrowd.Select((name, i) => (name, i)))
                members.Add(new NationRoomMember(PreviewRoomCrowdBase + i, name));
            _ncMemberList = new NationRoomMembers(PreviewNationRoom, "Siege at nine", _myId, PreviewRoomSize, members);
        }
        Net.I.RoomTimeline.Clear();
        Net.I.RoomTimeline.MergeHistory(
        [
            new RoomPost(101, RoomPostKind.Text, PreviewRoomBorin, "Borin", evening.AddMinutes(4), "Gate at nine tomorrow, bring Ascetic potions", 0, 0),
            new RoomPost(102, RoomPostKind.Text, _myId, "Tester10", evening.AddMinutes(6), "I'll be there (Thumbs up)", 0, 0),
            new RoomPost(103, RoomPostKind.Text, PreviewRoomRikka, "Rikka", now.AddMinutes(-14), "Morning! Who has a spare scroll?", 0, 0),
            new RoomPost(104, RoomPostKind.Text, PreviewRoomRikka, "Rikka", now.AddMinutes(-13), "the 30-minute one 🙏", 0, 0),
            new RoomPost(105, RoomPostKind.VoiceNote, PreviewRoomArwen, "Arwen", now.AddMinutes(-11), "", 124, 31000) { Waveform = PreviewWaveform(1) },
            new RoomPost(110, RoomPostKind.VoiceNote, PreviewRoomRikka, "Rikka", now.AddMinutes(-10), "", 386, 96000) { Waveform = PreviewWaveform(2) },
            new RoomPost(106, RoomPostKind.Text, PreviewRoomKaito, "Kaito", now.AddMinutes(-9),
                $"Selling {ChatItemLink.Token(PreviewUpgradedWeapon)} for the siege, whisper me before we leave tonight so I can hold it for you", 0, 0),
            new RoomPost(107, RoomPostKind.VoiceNote, _myId, "Tester10", now.AddMinutes(-4), "", 47, 12000) { Waveform = PreviewWaveform(3) },
            new RoomPost(108, RoomPostKind.Text, _myId, "Tester10", now.AddMinutes(-4), "listen to that (Laughing)", 0, 0),
        ], more: true);
        SeedPreviewRoomReactions(Net.I.RoomTimeline);
        Net.I.RoomTimeline.Add(RoomPost.Notice("Borin has entered. (Current players: 6)", now.AddMinutes(-2)));
        Net.I.RoomTimeline.Add(RoomPost.Line(PreviewRoomBorin, "Borin", "here", now.AddMinutes(-1)));
        if (page == "voice")
            Net.I.RoomTimeline.Add(RoomPost.Notice(new RoomMediaReplies.SilenceNotice(PreviewRoomBorin, true, "Tester10", "Borin").Text, now));
        var inCall = new List<VoicePeer>
        {
            new(PreviewRoomRikka, 0), new(PreviewRoomBorin, (byte)(page == "voice" ? VoiceFlags.Silenced : VoiceFlags.MicMuted)),
            new(PreviewRoomArwen, 0), new(PreviewRoomKaito, VoiceFlags.Deafened),
        };
        if (page is "voice" or "settings" or "config")
        {
            inCall.AddRange(PreviewRoomCrowd.Take(PreviewRoomCrowdInCall).Select((_, i) => new VoicePeer(PreviewRoomCrowdBase + i, 0)));
            Net.I.PreviewVoice(new VoiceSession(0, 1, new byte[VoiceSeal.KeyLength], 0, inCall));
            VoiceChat.I?.PreviewSpeaking(PreviewRoomRikka, self: false);
            VoiceChat.I?.PreviewSpeaking(_myId, self: true);
        }
        else if (page is "public" or "recording") Net.I.PreviewVoice(null);
        else Net.I.PreviewVoice(null, inCall.Take(2).ToList());
        if (page is "voice" or "text" or "notes") VoiceChat.I?.PreviewNote(105, PreviewRoomPlayed, 110, PreviewRoomLoaded);
        else VoiceChat.I?.PreviewNote(0, 0f, 0, 0f);
        ShowChatRoom();
        if (page == "voice") SelectChatRoomMember(PreviewRoomBorin);
        if (page is "reactions" or "react") PreviewRoomReactionPopup(page == "reactions");
        if (page == "older")
        {
            Net.I.RoomTimeline.MergeHistory(
            [
                new RoomPost(91, RoomPostKind.Text, PreviewRoomArwen, "Arwen", evening.AddMinutes(-40), "Who is coming to the siege tomorrow?", 0, 0),
                new RoomPost(92, RoomPostKind.Text, PreviewRoomKaito, "Kaito", evening.AddMinutes(-38), "me, with two clan mates", 0, 0),
            ], more: true);
            RenderChatRoom();
            var repost = new Godot.Timer { WaitTime = PreviewRoomRepostSeconds, OneShot = true, Autostart = true };
            repost.Timeout += () =>
            {
                Net.I.RoomTimeline.Add(new RoomPost(109, RoomPostKind.VoiceNote, PreviewRoomRikka, "Rikka", DateTime.Now, "", 63, 16000));
                RenderChatRoom();
                _roomThread.StickBottom = false;
                _roomThread.ScrollVertical = 0;
            };
            _roomWindow.AddChild(repost);
        }
        if (page == "minimized") _roomWindow.SetMinimized(true);
        if (page == "notes")
        {
            var scroll = new Godot.Timer { WaitTime = PreviewRoomRepostSeconds, OneShot = true, Autostart = true };
            scroll.Timeout += () =>
            {
                _roomThread.StickBottom = false;
                _roomThread.ScrollVertical = PreviewRoomNotesScroll;
                _roomThread.RefreshNotes();
            };
            _roomWindow.AddChild(scroll);
        }
        if (page == "settings")
        {
            FillChatRoomVoiceSettings();
            var card = _roomVoiceSettings!;
            card.GetParent().RemoveChild(card);
            _roomWindow.AddChild(card);
            _roomSettingsLevel.Value = PreviewRoomSettingsLevel;
            card.TreeEntered += () => Callable.From(() =>
            {
                card.Position = PreviewRoomSettingsAt;
                card.Popup();
            }).CallDeferred();
            _roomTick.Stop();
        }
        if (page == "config")
        {
            room.KeptChanged(true);
            FillChatRoomConfig();
            var card = _roomConfig!;
            card.GetParent().RemoveChild(card);
            _roomWindow.AddChild(card);
            card.TreeEntered += () => Callable.From(() =>
            {
                card.Position = PreviewRoomSettingsAt;
                card.Popup();
            }).CallDeferred();
            _roomTick.Stop();
        }
        if (page == "recording")
        {
            _roomTick.Stop();
            var record = new Godot.Timer { WaitTime = PreviewRoomRepostSeconds, OneShot = true, Autostart = true };
            record.Timeout += () => _roomComposer.ShowRecording(PreviewRecordingSeconds, PreviewRecordingLevel);
            _roomWindow.AddChild(record);
        }
        return DetachPreviewControl(_roomWindow);
    }

    private const int PreviewWarEndSeconds = 1342;
    private const uint PreviewWarHomeSeconds = 17;
    private const uint PreviewWarVictoryExp = 1_250_400;

    internal Control BuildWarResultUiPreview()
    {
        _bdwHudLayer = new CanvasLayer();
        AddChild(_bdwHudLayer);
        _bdwTick = new Godot.Timer();
        _bdwHudLayer.AddChild(_bdwTick);
        Net.I.BorderWar.Reset();
        Net.I.BorderWar.SetScores(212, 148);
        Net.I.BorderWar.Finish(Nations.Karus, PreviewWarHomeSeconds, DateTime.UtcNow);
        BuildWarResult();
        _warResult!.Visible = true;
        RefreshWarResult(DateTime.UtcNow);
        return DetachPreviewControl(_warResult);
    }

    internal Control BuildWarRankingUiPreview()
    {
        _bdwHudLayer = new CanvasLayer();
        AddChild(_bdwHudLayer);
        BuildWarRanking();
        _warRanking!.Visible = true;
        OnBorderWarRanking(new BorderWarRanking(
        [
            new BorderWarRankRow("Rikka", 1, 12, "Olympus", 41),
            new BorderWarRankRow("Nightwalker", 1, 0, "", 23),
            new BorderWarRankRow("Borin", 1, 7, "IronVanguard", 9),
        ],
        [
            new BorderWarRankRow("Zeus", 2, 3, "Pantheon", 37),
            new BorderWarRankRow("Ophelia", 2, 0, "", 12),
        ], PreviewWarVictoryExp, 0));
        return DetachPreviewControl(_warRanking);
    }

    internal Control BuildNestTimerUiPreview(bool completed)
    {
        ShowNestTimer(PreviewMonsterStone, completed ? PreviewNestGraceLeft : PreviewNestTimeLeft, completed);
        return DetachPreviewControl(_nestTimerPanel!);
    }

    internal Control ShowNoticeUiPreview(string text)
    {
        Chat.ShowNoticePreview(text);
        return DetachPreviewControl(Chat.NoticePanel);
    }

    private static List<MailEntry> MailUiPreviewEntries() =>
    [
        new MailEntry
        {
            Id = 3, Sender = "LibreKO", Subject = "Collection Race: Moradon Rookie Roundup", Read = false,
            Attachments = MailAttachmentState.Pending, SentAt = DateTime.UtcNow.AddMinutes(-12),
            Items =
            [
                new MailAttachment { Kind = MailAttachmentKind.Gold, ItemId = QuestData.CoinItemId, Count = 300_000 },
                new MailAttachment { Kind = MailAttachmentKind.Experience, ItemId = QuestData.ExpItemId, Count = 150_000 },
                new MailAttachment { Kind = MailAttachmentKind.Item, ItemId = 379154000, Count = 1 },
            ],
        },
        new MailEntry
        {
            Id = 2, Sender = "Rikka", Subject = "Apples for the raid", Read = false,
            Attachments = MailAttachmentState.Pending, SentAt = DateTime.UtcNow.AddHours(-5),
            Items = [new MailAttachment { Kind = MailAttachmentKind.Item, ItemId = 810418000, Count = 20 }],
        },
        new MailEntry
        {
            Id = 1, Sender = "Zeus", Subject = "Welcome to the clan", Read = true,
            Attachments = MailAttachmentState.None, SentAt = DateTime.UtcNow.AddDays(-3),
        },
    ];

    internal (Control Inventory, CanvasLayer MailLayer) BuildMailDragUiPreview()
    {
        var inventory = BuildPrimaryWindowUiPreview("Inventory");
        _mailDragSeedSlot = Inv.FirstFreeGridSlot();
        if (_mailDragSeedSlot >= 0)
        {
            Inv[_mailDragSeedSlot] = new ItemSlot { ItemId = 389018000, Count = 5, Durability = 1 };
            Inv[_mailDragSeedSlot + 1] = new ItemSlot { ItemId = 379154000, Count = 1, Durability = 1 };
            RefreshInventoryUI();
        }
        BuildMailWindow();
        BuildMailComposeWindow();
        _mailContacts.UnionWith(["Rikka"]);
        _mailComposeShown = true;
        _mailComposeWindow.Visible = true;
        _mailTo.Text = "Rikka";
        _mailSubject.Text = "Drag test";
        OnMailBodyChanged();
        RenderMailAttachments();
        RemoveChild(_mailLayer);
        return (inventory, _mailLayer);
    }

    private int _mailDragSeedSlot = -1;

    internal (Vector2 From, Vector2 To)? MailDragPointsUiPreview(int offset)
    {
        var abs = _mailDragSeedSlot + offset;
        var cell = _mailDragSeedSlot < 0 ? null : _invBagCells.FirstOrDefault(c => c.Slot == abs);
        if (cell == null || !cell.IsInsideTree())
            return null;
        return (cell.GetGlobalRect().GetCenter(), _mailDropZone.GetGlobalRect().GetCenter());
    }

    internal (Vector2 From, Vector2 To)? StackDragPointsUiPreview(int count)
    {
        int seed = Inv.FirstFreeGridSlot();
        if (seed < 0) return null;
        Inv[seed] = PreviewItem(PreviewPotion, count, 1);
        RefreshInventoryUI();
        var from = _invBagCells.FirstOrDefault(c => c.Slot == seed);
        var to = _invBagCells.FirstOrDefault(c => c.Slot > seed && Inv[c.Slot].IsEmpty);
        if (from == null || to == null || !from.IsInsideTree()) return null;
        return (from.GetGlobalRect().GetCenter(), to.GetGlobalRect().GetCenter());
    }

    internal string MailDragDebugUiPreview()
    {
        var has = _invCells.TryGetValue(_mailDragSeedSlot, out var cell);
        return $"seed={_mailDragSeedSlot} len={Inv.Length} cells={_invCells.Count} has={has} inTree={(has && cell!.IsInsideTree())} keys={string.Join(',', _invCells.Keys.OrderBy(k => k))}";
    }

    internal string MailComposeSizeUiPreview() =>
        $"window size={_mailComposeWindow.Size} min={_mailComposeWindow.GetCombinedMinimumSize()} attachments={_mailAttachments.Count} body={_mailComposeWindow.Body.Size}";

    internal Control BuildPartyHudUiPreview()
    {
        SkillData.EnsureLoaded();
        Net.I.SeedPartyPreview(
            new PartyMember(1001, 1, "Tansie", 4210, 3890, 62, 208, 1820, 1500),
            new PartyMember(1002, 1, "HiddenTuna", 5120, 4630, 61, 206, 1400, 1210),
            new PartyMember(1003, 1, "LikeAKitty", 3890, 2210, 60, 210, 2300, 2300),
            new PartyMember(1004, 1, "NevilleLongbotto", 4470, 1040, 59, 212, 1990, 640));
        var buffs = PreviewPartyBuffs();
        foreach (var (id, ping, permille, first, count) in PreviewPartyMembers)
            Net.I.SeedPartyMemberPreview(id, ping, permille, buffs.Skip(first).Take(count).ToArray());
        Net.I.SeedPartyCommanderPreview(PreviewPartyCommander);
        _partyPick = PreviewPartyPick;
        foreach (var (id, share) in PreviewWarBoardShares) _warBoardRowShares[id] = share;
        BuildPartyHud();
        return DetachPreviewControl(_partyHud!);
    }

    internal Control BuildPartyHudDragUiPreview()
    {
        var hud = BuildPartyHudUiPreview();
        if (PartyRowFor(PreviewPartyDragged) is not { } row) return hud;
        float grab = PartyDiamondSize * 0.5f;
        float pressY = row.Frame.Position.Y + grab;
        _partyDrag = new PartyRowDrag(row, pressY, grab);
        DragPartyRow(pressY - PreviewPartyDragLift);
        return hud;
    }

    internal Control BuildPartyHudConfigUiPreview()
    {
        var hud = BuildPartyHudUiPreview();
        BuildPartyConfig();
        RefreshPartyConfig();
        var popup = _partyConfig!;
        popup.GetParent().RemoveChild(popup);
        hud.AddChild(popup);
        popup.TreeEntered += () => Callable.From(() => Callable.From(() =>
        {
            popup.ResetSize();
            popup.Position = (Vector2I)PartyConfigSpot(popup.Size, hud.GetViewportRect().Size);
            popup.Popup();
        }).CallDeferred()).CallDeferred();
        return hud;
    }

    private static List<int> PreviewPartyBuffs() => SkillData.All
        .Where(s => s.Duration > 0 && s.Type1 == MagicType.Buff && SkillData.Icon(s.Id) != null)
        .Select(s => s.Id).Take(PreviewPartyBuffCount).ToList();

    private const int PreviewPartyBuffCount = 6;
    private const int PreviewPartyCommander = 1003;
    private const int PreviewPartyPick = 1002;
    private const int PreviewPartyDragged = 1004;
    private const float PreviewPartyDragLift = 74f;

    private static readonly (int Id, int PingMs, int LevelPermille, int FirstBuff, int Buffs)[] PreviewPartyMembers =
    {
        (1001, 84, 625, 0, 2),
        (1002, 172, 310, 2, 2),
        (1003, 290, 880, 1, 5),
        (1004, PartySignal.NoSignal, 50, 0, 0),
    };

    private static readonly (int Id, int Share)[] PreviewWarBoardShares =
    {
        (1001, 312), (1002, 241), (1003, 168), (1004, 97),
    };

    internal Control BuildClanBoardsUiPreview(string view)
    {
        ItemData.EnsureLoaded();
        ClanBoardInit();
        _selfClass = 112;
        var adverts = new List<ClanAdvert>
        {
            new(10, "Wolves", 2, 7, "Alpha", 0, "We hunt every night in Ronark Land. Level 70+ welcome, voice chat preferred."),
            new(11, "Doves", 3, 4, "Beta", 0, "Relaxed clan for questing and Lunar War."),
            new(12, "NightWatch", 1, 12, "Cassandra", 0, "Royal clan recruiting priests and mages for castle siege."),
        };
        var posts = new List<WishPost>
        {
            new("Rin", "Priest, level 72, online most evenings. Looking for an active clan.", 72, 112, 25, true, 101, "2026-10-09"),
            new("Kaito", "Archer looking for a Lunar War clan.", 68, 107, 31, false, -1, "2026-10-07"),
        };
        if (view == "wish-board" || view == "wish-message")
        {
            OnWishStatus(new WishStatus(ClanBoards.RoleRecruiter, "Kaito", 0));
            OnWishPosts(posts);
            if (view == "wish-message")
            {
                OpenWishView(posts[0]);
                return DetachPreviewControl(_wmWindow!);
            }
            return DetachPreviewControl(_wbWindow!);
        }
        OnClanBoardStatus(new ClanBoardStatus(ClanBoards.StateClanless, new List<ClanApplicant>(), 11, 0, "Doves"));
        OnClanAdverts(adverts);
        if (view == "clan-message")
        {
            OpenClanMessage(ClanMessageMode.ViewAdvert, adverts[0]);
            return DetachPreviewControl(_cmWindow!);
        }
        return DetachPreviewControl(_cbWindow!);
    }

    internal Control BuildPetPlateUiPreview()
    {
        _petPlate = BuildPetPlate();
        RefreshPetPlate(new PetSheet
        {
            Name = "Kitsune", Level = 42, Hp = 3120, MaxHp = 4400, Mp = 860, MaxMp = 1500,
            Satisfaction = 7300, ExpPercent = 4100,
        });
        return DetachPreviewControl(_petPlate);
    }

    private const int PreviewBindOnEquipSword = 191329000;

    internal Control BuildItemVeilsUiPreview()
    {
        ItemData.EnsureLoaded();
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        (int Id, ItemFlag Flag, short Durability)[] cells =
        [
            (PreviewBagSword, ItemFlag.Unsealed, 1000),
            (PreviewBagSword, ItemFlag.Rented, 1000),
            (PreviewBagSword, ItemFlag.Duplicate, 1000),
            (PreviewBagSword, ItemFlag.Sealed, 1000),
            (PreviewBagSword, ItemFlag.Unsealed, 0),
            (PreviewBindOnEquipSword, ItemFlag.Unsealed, 1000),
            (PreviewBagSword, ItemFlag.Bound, 1000),
        ];
        foreach (var (id, flag, durability) in cells)
        {
            var cell = new WarehouseCell(GridStart, bag: true);
            cell.Set(new ItemSlot { ItemId = id, Count = 1, Durability = durability, Flag = (byte)flag });
            row.AddChild(cell);
        }
        return row;
    }

    internal void AttachWeaponsForCapture(Node3D body, int[] gear, int race) =>
        AttachWeapons(body, gear, new HandWearer(race));

    internal (bool Ready, int Paths, string InFlight) GearStreamProbe(EntitySnapshot info) =>
        (SceneReadyFor(info), GearPaths(PlayerRig.LookRace(info.Race, info.Class), info.Gear).Count, _sceneLoadInFlight ?? "");

    internal Control BuildSoccerUiPreview()
    {
        ItemData.EnsureLoaded();
        _soccerLayer = new CanvasLayer();
        AddChild(_soccerLayer);
        var board = EnsureSoccerBoard();
        OnSoccerSync(454, 2, 1);
        return DetachPreviewControl(board);
    }

    internal Control BuildDailyRankUiPreview(string view)
    {
        ItemData.EnsureLoaded();
        _drLayer = new CanvasLayer();
        AddChild(_drLayer);
        LoadDailyRankBoards();
        _drWindow = BuildDailyRank();
        bool clans = view == "daily-rank-clan";
        int board = clans ? DailyRank.ClanBoard : DailyRank.AdonisBoard;
        var names = clans ? PreviewDailyRankClans : PreviewDailyRankPlayers;
        var entries = new List<DailyRankEntry>();
        for (int i = 0; i < names.Length; i++) entries.Add(new DailyRankEntry(i + 1, names[i]));
        _drCache[board] = new DailyRankBoardData(board, clans ? 4 : 12, clans ? -2 : 3, entries);
        RenderDailyRank(board);
        _drWindow.Visible = true;
        return DetachPreviewControl(_drWindow);
    }

    private const int PreviewSecondsPerHour = 3600;
    private const int PreviewSmashDagger = 119101000;
    private const int PreviewSmashScrap = 810369000;
    private const int PreviewSmashGem = 900823000;

    internal Control BuildTradeUiPreview()
    {
        ItemData.EnsureLoaded();
        Inv.EnsureLength(InventoryConstants.InventoryTotal);
        Sheet.SeedWealth(gold: 1_180_000, np: 2_450);
        BuildExchangePanel();
        _exPartnerName = "Rikka";
        _exMyOffer.Add(new ExOfferItem { ItemId = PreviewSmashDagger, Count = 1, Dura = 1000 });
        _exMyOffer.Add(new ExOfferItem { ItemId = PreviewHpPotion, Count = 40, Dura = 1 });
        _exMyGoldOffer = 250_000;
        _exTheirOffer.Add(new ExOfferItem { ItemId = PreviewWornSword, Count = 1, Dura = 1000 });
        _exConfirmedByPartner = true;
        _exMineHeader.Text = "You offer";
        _exTheirsHeader.Text = $"{_exPartnerName} offers";
        _exPanel.Title = $"Trade — {_exPartnerName}";
        RefreshExchangeOffers();
        _exPanel.Visible = true;
        return DetachPreviewControl(_exPanel);
    }

    internal Control BuildItemSmashUiPreview(bool result)
    {
        ItemData.EnsureLoaded();
        Inv.EnsureLength(InventoryConstants.InventoryTotal);
        Inv[GridStart] = PreviewItem(PreviewSmashDagger, 1, 1000);
        BuildSmashPanel();
        _smashShown = true;
        PlaceSmashItem(GridStart);
        if (result)
            OnItemSmashReply(new ItemSmashReply(ItemSmash.Succeeded, PreviewSmashDagger, 0,
                [new SmashPiece(PreviewSmashScrap, 1, 3), new SmashPiece(PreviewSmashGem, 2, 1), new SmashPiece(PreviewSmashScrap, 3, 2)]));
        _smashPanel.Visible = true;
        return DetachPreviewControl(_smashPanel);
    }

    internal CanvasLayer ExitMenuUiPreview(bool timed)
    {
        ItemData.EnsureLoaded();
        Inv.EnsureLength(InventoryConstants.InventoryTotal);
        if (timed) SeedPreviewTimedItems();
        BuildEscMenu();
        RemoveChild(_escLayer);
        _escShown = true;
        _escLayer.Visible = true;
        RefreshExitMenu();
        return _escLayer;
    }

    internal Control BuildRepurchaseUiPreview()
    {
        ItemData.EnsureLoaded();
        _rbLayer = new CanvasLayer();
        AddChild(_rbLayer);
        long deletes = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 2 * 24 * 3600;
        _rbRows.Clear();
        _rbRows.Add(new RepurchaseRow(0, PreviewBagSword, 180_000, (uint)deletes));
        _rbRows.Add(new RepurchaseRow(1, PreviewWornSword, 2_400_000, (uint)(deletes - 3600)));
        _rbRows.Add(new RepurchaseRow(2, PreviewHpPotion, 1_350, (uint)(deletes - 3 * 24 * 3600)));
        _rbWindow = BuildRepurchase();
        _rbScroll.MaxValue = 0;
        _rbScroll.Visible = false;
        RenderRepurchase();
        _rbWindow.Visible = true;
        return DetachPreviewControl(_rbWindow);
    }

    internal Control BuildTimedItemsUiPreview()
    {
        ItemData.EnsureLoaded();
        Inv.EnsureLength(InventoryConstants.InventoryTotal);
        SeedPreviewTimedItems();
        _tiLayer = new CanvasLayer();
        AddChild(_tiLayer);
        OpenTimedItems();
        return DetachPreviewControl(_tiWindow!);
    }

    private void SeedPreviewTimedItems()
    {
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        (int Abs, int ItemId, long HoursLeft)[] timed =
        [
            (GridStart + 2, PreviewBagSword, 12 * 24),
            (InventoryConstants.RightHand, PreviewWornSword, 2 * 24 + 5),
            (InventoryConstants.MagicBagStart, PreviewHpPotion, 5),
            (GridStart + 7, PreviewMpPotion, 0),
        ];
        foreach (var (abs, itemId, hoursLeft) in timed)
            Inv[abs] = new ItemSlot { ItemId = itemId, Count = 1, Durability = 1, ExpiresAt = (int)(now + hoursLeft * PreviewSecondsPerHour + PreviewSecondsPerHour / 2) };
    }

    private static readonly string[] PreviewDailyRankPlayers =
    {
        "NevilleLongbotto", "IGaLaXia", "TuMachoUrsoPol", "NOAAAAAAAAA", "Rin", "Kaito", "Veteran", "Alpha",
        "Beta", "Cassandra", "Doves", "Rikka", "Zeus", "Wolfgang",
    };

    private static readonly string[] PreviewDailyRankClans = { "NightWatch", "Wolves", "Doves", "Ravens", "Lions" };

    internal Control BuildWarBoardUiPreview()
    {
        BuildPartyHudUiPreview();
        _warBoard = BuildWarBoard();
        _warBoard.Visible = true;
        _warBoardMode = PartyWarBoard.ModeDamage;
        _warBoardDeal.ButtonPressed = true;
        var shares = new List<WarBoardShare>();
        foreach (var (id, share) in PreviewWarBoardShares) shares.Add(new WarBoardShare(id, share));
        FillWarBoard(shares);
        return DetachPreviewControl(_warBoard);
    }

    internal Control BuildMailUiPreview()
    {
        ItemData.EnsureLoaded();
        BuildMailWindow();
        BuildMailComposeWindow();
        _mailShown = true;
        _mailWindow.Visible = true;
        OnMailUnread(2);
        OnMailList(MailUiPreviewEntries());
        return DetachPreviewControl(_mailWindow);
    }

    internal Control BuildMailReadUiPreview()
    {
        ItemData.EnsureLoaded();
        BuildMailWindow();
        BuildMailComposeWindow();
        OnMailList(MailUiPreviewEntries());
        SelectMail(3);
        OnMailRead(3, true, "You completed the Collection Race 'Moradon Rookie Roundup' in Moradon. Your rewards are attached to this mail.");
        return DetachPreviewControl(_mailReadWindow);
    }

    internal Control BuildMailComposeUiPreview(bool lateAttach = false)
    {
        var window = BuildMailComposeUiPreviewNow();
        if (!lateAttach) return window;
        _mailAttachments.RemoveAt(1);
        RenderMailAttachments();
        var later = new Godot.Timer { WaitTime = 1.0, OneShot = true, Autostart = true };
        later.Timeout += () =>
        {
            var data = new Godot.Collections.Dictionary { { "id", Inv[Inventory.GridStart + 1].ItemId }, { "invFrom", Inventory.GridStart + 1 } };
            _mailDropZone._CanDropData(Vector2.Zero, data);
            _mailDropZone._DropData(Vector2.Zero, data);
            BuildInventoryTooltipHostUiPreview();
            ShowItemTooltip(Inventory.GridStart + 1, Inv[Inventory.GridStart + 1]);
        };
        window.AddChild(later);
        return window;
    }

    private Control BuildMailComposeUiPreviewNow()
    {
        ItemData.EnsureLoaded();
        BuildMailWindow();
        BuildMailComposeWindow();
        Inv.EnsureLength(InventoryConstants.InventoryTotal);
        Inv[Inventory.GridStart] = new ItemSlot { ItemId = 810418000, Count = 20, Durability = 1 };
        Inv[Inventory.GridStart + 1] = new ItemSlot { ItemId = 379154000, Count = 1, Durability = 1 };
        Inv[Inventory.GridStart + 2] = new ItemSlot { ItemId = 389018000, Count = 5, Durability = 1 };
        _mailContacts.UnionWith(["Rikka", "Zeus", "Ariel", "Marduk"]);
        _mailComposeShown = true;
        _mailComposeWindow.Visible = true;
        _mailTo.Text = "Rikka";
        _mailSubject.Text = "Apples for the raid";
        _mailBody.Text = "Here are the apples you asked for. Good hunting!";
        _mailGold.Value = 25_000;
        _mailAttachments.Add((Inventory.GridStart, 20));
        _mailAttachments.Add((Inventory.GridStart + 1, 1));
        OnMailBodyChanged();
        RenderMailAttachments();
        return DetachPreviewControl(_mailComposeWindow);
    }

    private const int CollectionRacePreviewWinners = 16;

    internal Control BuildCollectionRaceUiPreview(bool completed)
    {
        ItemData.EnsureLoaded();
        BuildCollectionRaceWindow();
        ShowCollectionRace(new CollectionRaceState
        {
            Name = "Moradon Rookie Roundup",
            RemainingSeconds = 56 * 60,
            Winners = completed ? 4 : 3,
            MaxWinners = CollectionRacePreviewWinners,
            Objectives =
            [
                new CollectionRaceObjective { Kind = CollectionRaceObjectiveKind.Monster, TargetId = 150, Name = "Kecoon", Count = 15, Current = completed ? 15 : 4 },
                new CollectionRaceObjective { Kind = CollectionRaceObjectiveKind.Monster, TargetId = 250, Name = "Bulcan", Count = 10, Current = 10 },
                new CollectionRaceObjective { Kind = CollectionRaceObjectiveKind.Item, TargetId = 810418000, Name = "Apples of Moradon", Count = 15, Current = completed ? 15 : 7 },
            ],
            IsCompleted = completed,
            Rewards =
            [
                new CollectionRaceReward { ItemId = QuestData.CoinItemId, ItemCount = 500_000, Rate = 100 },
                new CollectionRaceReward { ItemId = QuestData.ExpItemId, ItemCount = 250_000, Rate = 100 },
                new CollectionRaceReward { ItemId = QuestData.LadderPointItemId, ItemCount = 100, Rate = 100 },
                new CollectionRaceReward { ItemId = 379154000, ItemCount = 1, Rate = 35 },
            ],
        });
        return DetachPreviewControl(_crWindow);
    }

    internal Control BuildQuestNotificationUiPreview(int count = 3)
    {
        QuestText.EnsureLoaded();
        var samples = new[]
        {
            new QuestView(663, 31561, 2, true, true, false, false, QuestViewState.Available, 0, "How to hunt",
                "You must talk to [Hunter] Halon.",
                "So you think you've got enough skills? Have you even had a proper hunting before? Come and see me at the camp.",
                new QuestObjectives(663, false, []), [], [], ["I'll come by", "Not now"], Notification: true),
            new QuestView(60, 13013, 21, true, true, false, false, QuestViewState.Available, 0, "Doom Soldier hunt",
                "Hunt 40 Doom Soldiers for the guard captain.",
                "The Doom Soldiers are pressing on the north gate again. Thin their ranks and the captain will reward you.",
                new QuestObjectives(60, false, []), [], [], ["Accept"], Notification: true),
            new QuestView(1233, 25002, 21, true, false, true, false, QuestViewState.Claimable, 0, "Draki's Heart",
                "Bring the Spiritual Stone to the sage.",
                "You found it. Bring the stone to me and I will tell you what the Draki left behind.",
                new QuestObjectives(1233, false, []), [], [], ["Turn in"], Notification: true),
        };
        for (var i = 0; i < Mathf.Min(count, samples.Length); i++)
            ShowQuestNotification(samples[i]);
        return DetachPreviewControl(_questNotificationWindow!);
    }

    internal void SetNpcDialogUiPreview(NpcDialog dialog, int scroll)
    {
        OnNpcDialog(dialog);
        _npcMenuScroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, scroll);
    }

    internal Control BuildNpcDialogUiPreview(bool full, bool rewards = false)
    {
        QuestText.EnsureLoaded();
        BuildNpcDialog();
        var dlg = new NpcDialog
        {
            NpcId = 506,
            Flag = 1,
            HeaderTextId = full ? 9309 : 200,
            ScriptFile = "moradon_guide.quest",
        };
        int[] menuIds = full
            ? new[] { 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 10 }
            : new[] { 11, 12, 13, 10 };
        if (!full)
        {
            dlg.Buttons.Add((0, 0, NpcChoice.ReadyPrefix + "Kecoon Captain hunt"));
            dlg.Buttons.Add((1, 0, "[In progress] Bandicoot hunt"));
        }
        for (int i = 0; i < menuIds.Length; i++) dlg.Buttons.Add((dlg.Buttons.Count, menuIds[i], null));
        if (rewards)
        {
            dlg.HeaderText = "Choose your reward.";
            dlg.Buttons.Clear();
            dlg.Buttons.Add((0, 0, "1,500,000 experience"));
            dlg.Buttons.Add((1, 0, "100,000 gold"));
            dlg.Buttons.Add((2, 0, "Cancel"));
        }
        OnNpcDialog(dlg);
        return DetachPreviewControl(_npcPanel);
    }

    internal Control BuildVendorUiPreview()
    {
        ItemData.EnsureLoaded();
        Sheet.SeedWealth(gold: 1_180_000, np: 2_450);
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(811182000, 35, 1);
        Inv[GridStart + 1] = PreviewItem(810684000, 12, 1);
        Inv[GridStart + 2] = PreviewItem(900402000, 5, 1);
        BuildInventoryPanel();
        BuildVendorPanel();
        _vendorNpcName = "[Potion Merchant] Euronia";
        OnVendorOpen(255000);
        return DetachPreviewControl(_vendorPanel);
    }

    internal string SetVendorSearchUiPreview(string query)
    {
        _vendorSearch.Text = query;
        OnVendorSearch();
        return $"search={query} shown={_vendorResults.Count}";
    }

    internal string SetVendorGroupUiPreview(int group)
    {
        OpenVendor(group);
        return $"group={group} pages={_vendorCatalogue.Pages.Count} items={_vendorCatalogue.Count}";
    }

    internal Control? HoverVendorCellUiPreview(int index)
    {
        if (index < 0 || index >= _vendorCells.Length) return null;
        _vendorCells[index].EmitSignal(Control.SignalName.MouseEntered);
        return _itemTipPanel.Visible ? DetachPreviewControl(_itemTipPanel) : null;
    }

    internal QuantityPrompt OpenVendorSellPromptUiPreview()
    {
        AskSell(GridStart);
        return DetachTradePrompt();
    }

    internal QuantityPrompt OpenVendorBuyPromptUiPreview()
    {
        int stackable = System.Linq.Enumerable.FirstOrDefault(_vendorCellIds, id => ItemData.Get(id) is { Countable: not 0 });
        AskBuy(stackable, -1);
        return DetachTradePrompt();
    }

    private QuantityPrompt DetachTradePrompt()
    {
        RemoveChild(_tradePrompt);
        foreach (var node in _tradePrompt.GetChildren())
            if (node is Control control) control.Theme = HudTheme.Shared;
        return _tradePrompt;
    }

    internal Control BuildFamiliarUiPreview(bool summoned)
    {
        ItemData.EnsureLoaded();
        BuildPetPanel();
        var sheet = new PetSheet
        {
            Index = 1, Name = "Kauly", Class = 101, Level = 12, ExpPercent = 4375,
            MaxHp = 168, Hp = 131, MaxMp = 190, Mp = 190, Satisfaction = 7240, Attack = 51, Defence = 110,
            Mode = PetSheet.ModeAttack,
        };
        sheet.Items[0] = new ItemSlot { ItemId = PreviewAutomaticLooting, Count = 1, Durability = 1 };
        ShowPetSheet(summoned ? sheet : null);
        if (summoned) SetPetStatus("50.00% satisfaction rate increase", false);
        _petPanel.Visible = true;
        return DetachPreviewControl(_petPanel);
    }

    internal Control BuildFamiliarHatchUiPreview(bool transform)
    {
        ItemData.EnsureLoaded();
        BuildPetHatchPanel();
        if (transform)
        {
            Net.I.PetItems[PreviewPetIndex] = new PetItemInfo(PreviewPetIndex, "Kauly", 101, 12, 4200, 7300);
            Inv.ApplySlotUpdate(GridStart + 1, new ItemSlot
            {
                ItemId = PreviewKaulItem, Count = 1, Durability = 1, UniqueId = PreviewPetIndex,
            });
            Inv.ApplySlotUpdate(GridStart + 4, new ItemSlot { ItemId = PreviewImageChange, Count = 1, Durability = 1 });
            Inv.ApplySlotUpdate(GridStart + 9, new ItemSlot { ItemId = PreviewEtarothScroll, Count = 1, Durability = 1 });
        }
        else
        {
            Inv.ApplySlotUpdate(GridStart + 2, new ItemSlot { ItemId = PreviewEggItem, Count = 1, Durability = 1 });
            Inv.ApplySlotUpdate(GridStart + 6, new ItemSlot { ItemId = PreviewEggItem, Count = 1, Durability = 1 });
        }
        OpenPetHatch(PreviewTrainerNpc);
        _petHatchName.Text = "Kauly";
        RefreshPetHatchUI();
        return DetachPreviewControl(_petHatchPanel);
    }

    internal Control BuildFamiliarBarUiPreview(int page)
    {
        ItemData.EnsureLoaded();
        SkillData.EnsureLoaded();
        _petBarLayer = new CanvasLayer();
        AddChild(_petBarLayer);
        BuildPetBar();
        var sheet = new PetSheet
        {
            Index = 1, Name = "Kauly", Class = 101, Level = 32, MaxHp = 420, Hp = 400, MaxMp = 190, Mp = 150,
        };
        Net.I.SeedPreviewPet(sheet);
        OnPetBarSummoned(sheet);
        _petBarPage = page;
        RefreshPetBar();
        double now = Now();
        _petSkillReadyAt[PetSkills.DesignatedAttack] = now + 0.6;
        if (_petBarSkills.Count > 1) _petSkillReadyAt[_petBarSkills[1]] = now + 2.5;
        PetBarTick(now);
        return DetachPreviewControl(_petBar);
    }

    private const int PreviewEggItem = 600001000;
    private const int PreviewKaulItem = 610001000;
    private const int PreviewPetIndex = 1;
    private const int PreviewImageChange = 700017000;
    private const int PreviewEtarothScroll = 700019001;
    private const int PreviewAutomaticLooting = 700012000;
    private const int PreviewTrainerNpc = 13016;

    internal Control BuildClassChangeUiPreview()
    {
        BuildClassChangePanel();
        _vendorNpcName = "Captain Kaishan";
        OnClassChangeNpc();
        return DetachPreviewControl(_classChangePanel);
    }

    internal Control BuildQuestTrackerUiPreview()
    {
        SeedPreviewQuests();
        BuildQuestTracker();
        RefreshTracker();
        var tracker = DetachPreviewControl(_trackerPanel);
        Callable.From(() => tracker.Position = TrackerDefaultPosition()).CallDeferred();
        return tracker;
    }

    internal Control BuildPkLadderUiPreview()
    {
        static RankEntry Row(int place, string name, string clan, int points, int bonus, int symbol) =>
            new() { Place = place, Name = name, ClanName = clan, Loyalty = points, Bonus = bonus, Symbol = symbol };
        BuildRankPanel();
        _ladder = new PkLadderReply(
            new[] { Row(1, "Velkan", "Wolves", 12_480, 120, 1), Row(2, "Rikka", "Wolves", 9_310, 0, 7), Row(3, "Nightwalker", "", 4_005, 45, 0) },
            new[] { Row(1, "Arwen", "Lionheart", 15_200, 999, 3), Row(2, "Ophelia", "Lionheart", 2_150, 0, 150) },
            4, 1_870, 60);
        RenderLadder();
        _rankPanel.Visible = true;
        return DetachPreviewControl(_rankPanel);
    }

    internal CanvasLayer ClanMatchUiPreview(bool banner)
    {
        _matchLayer = new CanvasLayer();
        for (int team = 0; team < ClanMatch.TeamSlots; team++) _matchTeams[team] = BuildMatchTeam(team);
        Net.I.SeedPreviewMatch(new ClanMatchPair(96, 40, 50));
        var roster = new System.Collections.Generic.List<ClanMatchFighter>();
        string[] names = { "Velkan", "Rikka", "Nightwalker", "Ophelia", "Arwen", "Zeus", "Morgrim", "Aurelia" };
        int[] classes = { 105, 211, 109, 112, 207, 106, 113, 203 };
        for (int i = 0; i < names.Length; i++)
            roster.Add(new ClanMatchFighter(i % 2 == 0 ? 40 : 50, i + 1, names[i], 900, 900 - i * 90, classes[i], 400, 400 - i * 40, i % 2 + 1));
        OnMatchRoster(roster);
        _matchTeams[0].Position = new Vector2(20f, 200f);
        _matchTeams[1].Position = new Vector2(1453f, 200f);
        if (banner) _matchLayer.AddChild(BuildMatchBanner(MatchBanner.RedWins));
        return _matchLayer;
    }

    internal Control BuildExpSealUiPreview()
    {
        ItemData.EnsureLoaded();
        _expSealLayer = new CanvasLayer();
        BuildExpSealWindow();
        _expSealJarPlaced = true;
        _expSealJar.Texture = ItemData.Icon(ExpSeal.EmptyJar);
        _expSealPick.Selected = 2;
        _expSealWindow.Visible = true;
        return DetachPreviewControl(_expSealWindow);
    }

    internal Control BuildCommandListUiPreview(int group)
    {
        ItemData.EnsureLoaded();
        _cmdLayer = new CanvasLayer();
        BuildCommandList();
        _cmdGroups.Select(group);
        FillCommandEntries(CommandCatalog.Groups[group]);
        _cmdWindow.Visible = true;
        return DetachPreviewControl(_cmdWindow);
    }

    internal Control BuildCharacterSealUiPreview(string page)
    {
        ItemData.EnsureLoaded();
        _csLayer = new CanvasLayer();
        if (page == "trans")
        {
            BuildCharacterSealTrans();
            _csSocket.Set(new ItemSlot { ItemId = 800111000, Count = 1 });
            _csTransHeadline.Text = ItemData.Get(800111000)?.Name ?? "";
            _csTransGold.Text = $"{12_345_678:n0}";
            _csTrans.Visible = true;
            return DetachPreviewControl(_csTrans);
        }
        if (page == "sheet")
        {
            BuildSealedSheet();
            _csSheetId = 11;
            Net.I.PetItems[11] = new PetItemInfo(11, "Velkan", 105, 70, 2500, 1, 2);
            var sheet = new SealedCharacterSheet
            {
                Name = "Velkan", Nation = Nations.Karus, Race = 1, Class = 106, Level = 70, Loyalty = 18_250, Gold = 5_400_000,
                FreePoints = 3, Stats = [160, 90, 80, 50, 50], StatBonus = [12, 0, 4, 0, 0], SkillTrees = [40, 35, 0, 5],
            };
            int[] gear = [0, 203003000, 0, 0, 203001000, 0, 136570000, 0, 171510000, 0, 203002000, 0, 203004000, 203005000];
            for (int i = 0; i < gear.Length; i++) sheet.Items[i] = new ItemSlot { ItemId = gear[i], Count = 1 };
            sheet.Items[14] = new ItemSlot { ItemId = 389010000, Count = 120 };
            sheet.Items[15] = new ItemSlot { ItemId = 800111000, Count = 1 };
            OnSealedSheet(sheet);
            return DetachPreviewControl(_csSheet);
        }
        BuildCharacterSealWindow();
        if (page == "select")
        {
            _csMode = CharacterSeal.Mode.Seal;
            _csSlots =
            [
                new CharacterSummary { Name = "Zeus", Class = 101, Level = 60 },
                new CharacterSummary { Name = "Velkan", Class = 106, Level = 70 },
                new CharacterSummary(),
                new CharacterSummary { Name = "Rikka", Class = 111, Level = 58 },
            ];
            _csPicked = 1;
            _csSelectTitle.Text = "Select Character Slot";
            _csSelectPrompt.Text = ">> Select Character to Seal <<";
            RefreshCharacterSealSlots();
            ShowCharacterSealPage(_csSelectPage, "Character Seal");
        }
        else if (page == "code")
        {
            ShuffleCharacterSealKeys();
            _csCode = "1234";
            _csCodeStars.Text = "****";
            ShowCharacterSealPage(_csCodePage, "Character Seal");
        }
        else ShowCharacterSealPage(_csSealingPage, CharacterSealTitle);
        return DetachPreviewControl(_csWindow);
    }

    internal Control BuildPartyBoardUiPreview()
    {
        ItemData.EnsureLoaded();
        BuildPartyPanel();
        BuildSeekPartyPanel();
        _seekBoard = FullMoon.NormalBoard;
        ApplySeekBoard();
        UpdateSeekStatus();
        _seekShown = true;
        OnBbsList(0, 2, new System.Collections.Generic.List<PartyBbsEntry>
        {
            new("Velkan", 105, 62, 2, "Looking for party", 21, 0, 1),
            new("Rikka", 211, 60, 3, "Need a priest", 21, 3, 1),
        });
        _seekPanel.Visible = true;
        return DetachPreviewControl(_seekPanel);
    }

    internal Control BuildFullMoonBoardUiPreview()
    {
        BuildSeekPartyPanel();
        _seekBoard = FullMoon.FullMoonBoard;
        ApplySeekBoard();
        UpdateSeekStatus();
        _seekShown = true;
        OnBbsList(0, 1, new System.Collections.Generic.List<PartyBbsEntry>
        {
            new("Velkan", 105, 62, 2, "Full Moon tonight", 21, 0, 1),
            new("Rikka", 211, 60, 3, "Need a priest", 21, 3, 1),
        });
        _seekPanel.Visible = true;
        return DetachPreviewControl(_seekPanel);
    }

    internal CanvasLayer FullMoonCaptionUiPreview()
    {
        _fullMoonCaption = BuildFullMoonCaption();
        RemoveChild(_fullMoonCaptionLayer);
        return _fullMoonCaptionLayer!;
    }

    internal Control BuildNereidUiPreview()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        _fullMapLayer = new CanvasLayer();
        _nereidStrip = UiTheme.Text("", 12, UiTheme.TextHi, HorizontalAlignment.Center);
        _nereidStrip.AddThemeStyleboxOverride("normal", UiTheme.MeterTrack());
        _nereidStrip.CustomMinimumSize = new Vector2(MiniMap.MapSize, NereidStripHeight);
        _nereidFlagBoard = NereidBoard(Control.LayoutPreset.TopLeft, 0f);
        _nereidScoreBoard = NereidBoard(Control.LayoutPreset.TopLeft, 0f);
        foreach (var label in new[] { _nereidFlagBoard, _nereidScoreBoard })
        {
            _fullMapLayer.RemoveChild(label);
            label.CustomMinimumSize = new Vector2(NereidBoardWidth, NereidStripHeight);
        }
        _nereidStrip.Text = "El Morad  6 : 8  Karus";
        _nereidFlagBoard.Text = "Total Point   Elmo: 6   Karu: 8";
        _nereidScoreBoard.Text = "TotalPoint   Elmo: 12   Karu: 26";
        _nereidFlagBoard.Visible = _nereidScoreBoard.Visible = true;
        box.AddChild(_nereidStrip);
        box.AddChild(_nereidFlagBoard);
        box.AddChild(_nereidScoreBoard);
        return box;
    }

    internal Control BuildVanguardUiPreview(int mode)
    {
        _vanguardLayer = new CanvasLayer();
        BuildVanguardInvite();
        BuildVanguardInviteMini();
        BuildVanguardList();
        BuildVanguardListMini();
        _vanguardCountdown = 7.4;
        VanguardTick(0);
        HudWindow window = mode switch
        {
            VanguardInviteFull => _vanguardInvite,
            VanguardListFull => _vanguardList,
            VanguardListMini => _vanguardListMini,
            _ => _vanguardInviteMini,
        };
        _vanguardNationBand.Color = new Color("#" + DeathNotices.KarusNameColour);
        string[] names = { "Velkan", "Rikka", "Nightwalker", "Ophelia", "Arwen", "Zeus" };
        for (int i = 0; i < Vanguard.ListRows; i++) _vanguardRows[i].Text = names[i];
        _vanguardOthers.Text = Vanguard.OthersCount(names.Length).ToString();
        window.Visible = true;
        return DetachPreviewControl(window);
    }

    internal Control BuildSiegeTowerUiPreview()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 24);
        BuildTowerButton(box);
        _towerButton.Text = TowerText(SiegeTower.BoardingText);
        _towerButton.Visible = true;
        _dropOff = UiTheme.ActionButton(TowerText(SiegeTower.ExitText), "");
        _dropOff.CustomMinimumSize = new Vector2(DropOffWidth, DropOffHeight);
        _dropOff.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        box.AddChild(_dropOff);
        return box;
    }

    internal Control BuildSiegeWarUiPreview()
    {
        BuildSiegeWarWindow();
        ShowSiegeStatus(new SiegeWarStatus(2, 120, 4, 0, 912, 2405, "Wolves"));
        _siegeWarPanel.Visible = true;
        return DetachPreviewControl(_siegeWarPanel);
    }

    internal Control BuildChaosGuideUiPreview()
    {
        var window = BuildChaosGuide();
        window.Visible = true;
        return DetachPreviewControl(window);
    }

    internal Control BuildNestIndunUiPreview()
    {
        const int monsterStone = 900144023;
        BuildNestWindow();
        _nestStone = monsterStone;
        _nestWindow.Title = ItemData.DisplayName(monsterStone);
        _nestExplain.Text = ItemData.Get(monsterStone)?.Desc ?? "";
        _nestWindow.Visible = true;
        return DetachPreviewControl(_nestWindow);
    }

    internal Control BuildDeathDialogUiPreview(bool respawnPending)
    {
        BuildDeathDialog();
        _selfDead = true;
        _deathExpLost = 18_240;
        ShowDeathDialog();
        if (respawnPending) MarkRespawnPending();
        return DetachPreviewControl(_deathPanel);
    }

    internal Control BuildUserInfoUiPreview()
    {
        LoadDailyRankBoards();
        UserInfoInit();
        OnUserInformation(new UserInfoReply(
            "Tester10", 61, 202, 1_245_000, 38_200,
            17, 3, 1, 4, "Noah Knights", "Ardream", 2, 1, 3,
            new[] { 4, 255, 37, 0, 120, 0, 1 },
            new[] { new RankMark(0, 2, 1, 0), new RankMark(1, 3, 0, 0), new RankMark(2, 0, 0, 1), new RankMark(51, 0, 0, 2) }));
        return DetachPreviewControl(_userInfoPanel);
    }

    internal Control BuildEquipViewUiPreview()
    {
        ItemData.EnsureLoaded();
        BuildInventoryPanel();
        EquipViewInit();
        RequestEquipmentView("Tester10");
        OnEquipmentView(Net.EquipmentViewResult.Accepted, new Net.EquipmentView(
            "Tester10", 202, 12, 1, 3, 61, 1, Nations.ElMorad,
            257, 157,
            60, 5, 72, 0, 184, 12, 50, 0, 50, 0,
            412, 286, 20, 15, 15, 10, 8, 12,
            new System.Collections.Generic.List<(int, int, short, byte)>
            {
                (InventoryConstants.Head, 208003000, 7000, 0),
                (InventoryConstants.Breast, 208001000, 6400, 0),
                (InventoryConstants.Glove, 208004000, 5100, 0),
                (InventoryConstants.Leg, 208002000, 6100, 0),
                (InventoryConstants.Foot, 208005000, 4800, 0),
                (InventoryConstants.RightHand, 127410000, 5600, 0),
                (InventoryConstants.LeftHand, 136710000, 5200, 0),
                (InventoryConstants.Neck, 330310000, 4200, 0),
                (InventoryConstants.RightRing, 320410011, 3900, 0),
            }));
        return DetachPreviewControl(_equipViewPanel);
    }

    private const string PreviewWhisperPartner = "Rikka";

    internal Control BuildWhisperUiPreview(bool minimized = false)
    {
        ItemData.EnsureLoaded();
        Chat = new ChatSystem(this);
        _myId = PreviewRoomSelf;
        var me = Net.I.LastEnter;
        me.Name = "Tester10";
        me.Nation = Nations.Karus;
        Net.I.SeedPreviewEnter(me);
        if (!IsInstanceValid(_reactionLayer)) ChatReactionsInit();
        WhisperInit();
        var timeline = Net.I.DirectTimeline(PreviewWhisperPartner);
        if (timeline.Posts.Count == 0)
        {
            var now = DateTime.Now;
            int them = PreviewRoomRikka;
            string partner = PreviewWhisperPartner;
            timeline.MergeHistory(
            [
                new RoomPost(201, RoomPostKind.Text, them, partner, now.AddDays(-1).AddHours(-2), "hey, are you farming Ronark?", 0, 0),
                new RoomPost(202, RoomPostKind.Text, _myId, "Tester10", now.AddDays(-1).AddHours(-2), "yep, west side by the bridge. bring pots", 0, 0),
                new RoomPost(203, RoomPostKind.Text, them, partner, now.AddMinutes(-20), "omw (Smile)", 0, 0),
                new RoomPost(204, RoomPostKind.VoiceNote, them, partner, now.AddMinutes(-18), "", 86, 21000) { Waveform = PreviewWaveform(4) },
                new RoomPost(205, RoomPostKind.Text, _myId, "Tester10", now.AddMinutes(-17), "channel 2, near the north gate", 0, 0),
                new RoomPost(206, RoomPostKind.VoiceNote, _myId, "Tester10", now.AddMinutes(-12), "", 41, 10000) { Waveform = PreviewWaveform(5) },
                new RoomPost(207, RoomPostKind.Text, _myId, "Tester10", now.AddMinutes(-3), $"also got this {ChatItemLink.Token(MarketPricePreviewItem)} for you", 0, 0),
                new RoomPost(208, RoomPostKind.Text, them, partner, now.AddMinutes(-2), $"{ChatItemLink.Token(MarketPricePreviewItem)} nice, thanks! how much do you want for it?", 0, 0),
            ], more: true);
            timeline.Find(202)?.Reactions.Set(them, partner, ReactionRules.QuickPicks[0]);
            timeline.Find(204)?.Reactions.Set(_myId, "Tester10", "😂");
            if (timeline.Find(207) is { } gift)
            {
                gift.Reactions.Set(them, partner, ReactionRules.QuickPicks[1]);
                gift.Reactions.Set(_myId, "Tester10", ReactionRules.QuickPicks[1]);
            }
            timeline.Add(RoomPost.Notice(ItemData.Text(ChatSystem.TextWhisperConnected, "Successfully connected for a private chat"), now.AddMinutes(-1)));
        }
        OpenWhisperWith(PreviewWhisperPartner);
        var chat = _whispers[PreviewWhisperPartner];
        if (!minimized) return DetachPreviewControl(chat.Window);

        chat.Window.SetMinimized(true);
        var longName = GetOrCreateWhisper("Bartholomew the Third", minimized: true);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        stack.AddChild(DetachPreviewControl(chat.Window));
        stack.AddChild(DetachPreviewControl(longName.Window));
        return stack;
    }

    internal Control BuildTradeWaitUiPreview()
    {
        BuildExchangeWaitPanel();
        ShowExchangeWait("Waiting for Tester10 to accept the trade…");
        return DetachPreviewControl(_exWaitLabel.GetParent().GetParent().GetParent<Control>());
    }

    internal Control BuildTradeAmountUiPreview()
    {
        ItemData.EnsureLoaded();
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(389310000, 137, 1);
        BuildExchangeAmountPrompt();
        OpenExchangeAmount(GridStart, 137);
        return DetachPreviewControl(_exAmountIcon.GetParent().GetParent().GetParent().GetParent<Control>());
    }

    internal Control BuildSealUiPreview(
        SealMode mode, bool socketed, ItemFlag state = ItemFlag.Unsealed, bool pad = false)
    {
        ItemData.EnsureLoaded();
        Inv.EnsureLength(GridStart + GridCount);
        var boots = PreviewItem(PreviewKrowazBoots, 1, 100);
        boots.Flag = (byte)state;
        Inv[GridStart] = boots;
        Inv[GridStart + 1] = PreviewItem(PreviewSealStone, 4, 0);
        Inv[GridStart + 2] = PreviewItem(PreviewHpPotion, PreviewPotionCount, 1);
        Inv[GridStart + 3] = PreviewItem(PreviewUniqueRing, 1, 100);
        Sheet.SetGold(151_235_840);
        BuildSealWindow();
        OpenSealWindow(mode);
        if (socketed) TakeSealSocket(0);
        if (pad) ShowSealKeypadUiPreview();
        return DetachPreviewControl(_sealPanel);
    }

    internal void ShowSealKeypadUiPreview()
    {
        _sealPad.Visible = true;
        for (int i = 0; i < 5; i++) PushSealDigit(i + 1);
    }

    internal void SetQuestObjectivesUiPreview(QuestObjectives objectives)
    {
        OnQuestObjectives(objectives);
        SelectQuest(objectives.QuestId);
    }

    private void SeedPreviewQuests()
    {
        if (_quests.Count != 0) return;
        _quests.Add(new QuestEntry(60, 1));
        _quests.Add(new QuestEntry(62, 1));
        _quests.Add(new QuestEntry(274, 1));
        _quests.Add(new QuestEntry(275, 3));
        _quests.Add(new QuestEntry(278, 1));
        _quests.Add(new QuestEntry(808, 2));
        _quests.Add(new QuestEntry(810, 2));
        _quests.Add(new QuestEntry(1091, 4));
        _questKills[60] = new ushort[] { 3, 0, 0, 0 };
        _questKills[62] = new ushort[] { 1, 0, 0, 0 };
        _questKills[274] = new ushort[] { 13, 0, 0, 0 };
        _questKills[275] = new ushort[] { 20, 0, 0, 0 };
        _questTracked.Add(274);
        _questSelected = 60;
        if (!Config.LegacyQuestFallback) SeedPreviewQuestViews();
    }

    private void SeedPreviewQuestViews()
    {
        foreach (var view in PreviewQuestViews())
        {
            _questViews[view.QuestId] = view;
            _questStrings[view.QuestId] = new QuestStrings(view.QuestId, view.Title, view.Journal);
            _questObjectives[view.QuestId] = view.Objectives;
            _questKills[view.QuestId] = view.Counts;
            int state = view.State == QuestViewState.Claimable ? QuestStateReadyToTurnIn : QuestStateActive;
            int index = _quests.FindIndex(q => q.QuestId == view.QuestId);
            if (index < 0) _quests.Add(new QuestEntry(view.QuestId, state));
            else _quests[index] = new QuestEntry(view.QuestId, state);
        }
        _questSelected = 274;
    }

    private static IEnumerable<QuestView> PreviewQuestViews()
    {
        yield return new QuestView(274, 14201, 21, false, false, false, false, QuestViewState.InProgress, 0,
            "Worm hunt", "[Guard] Patrick asked me to thin out the worms near the Moradon bridge.", "",
            new QuestObjectives(274, false,
            [
                new QuestKillGroup(20, [150], "Worm", HasTarget: true),
                new QuestKillGroup(10, [151], "Bulcan"),
            ]),
            [13, 10],
            [
                new QuestTransfer(false, 2, QuestData.ExpItemId, 125_400, 0),
                new QuestTransfer(false, 1, QuestData.CoinItemId, 2_500_000, 0),
                new QuestTransfer(false, 0, 389010000, 5, 0),
            ], []);
        yield return new QuestView(275, 14201, 21, false, false, true, false, QuestViewState.Claimable, 0,
            "Patrick's letter", "Bring the letter back to [Guard] Patrick.", "",
            new QuestObjectives(275, false, []), [], [], []);
        yield return new QuestView(60, 18004, 0, false, false, false, false, QuestViewState.InProgress, 0,
            "Daily practice", "Defeat any monster anywhere.", "",
            new QuestObjectives(60, false, [new QuestKillGroup(5, [150], "Any monster")]), [3], [], []);
    }

    internal Control BuildMerchantMenuUiPreview()
    {
        ItemData.EnsureLoaded();
        MerchantInit();
        _merchantMenu.Visible = true;
        _merchantMenuShown = true;
        return DetachPreviewControl(_merchantMenu);
    }

    internal Control BuildSellStallUiPreview(bool staged)
    {
        SeedMerchantPreview();
        MerchantInit();
        OnMerchantOpenResult(Net.MerchantOpenAccepted);
        if (staged)
        {
            OnMerchantItemAdd(true, 156211000, 1, 30, 350_000_000, 0, 0);
            OnMerchantItemAdd(true, 379080000, 8, 0, 5_000_000, 2, 1);
            OnMerchantItemAdd(true, 811182000, 35, 1, 12_500, 3, 2);
            _sellAdvert.Text = "cheap gear, come look";
        }
        return DetachPreviewControl(_sellStallPanel);
    }

    internal Control? HoverStallCellUiPreview(int index)
    {
        if (index < 0 || index >= _sellStallCells.Length) return null;
        _sellStallCells[index].EmitSignal(Control.SignalName.MouseEntered);
        return _itemTipPanel.Visible ? DetachPreviewControl(_itemTipPanel) : null;
    }

    internal Control BuildStallSignUiPreview()
    {
        SeedMerchantPreview();
        MerchantInit();

        var stall = new Stall { IsBuying = false, ItemIds = new[] { 156211051, 156210008, 0, 330620433 } };
        _stalls[4242] = stall;
        RefreshStallSign(4242, stall);
        stall.Sign!.Visible = true;
        return DetachPreviewControl(stall.Sign);
    }

    internal Control? HoverStallSignUiPreview(int index)
    {
        if (!_stalls.TryGetValue(4242, out var stall) || index >= stall.SignCells.Length) return null;
        stall.SignCells[index].EmitSignal(Control.SignalName.MouseEntered);
        return _itemTipPanel.Visible ? DetachPreviewControl(_itemTipPanel) : null;
    }

    internal Control BuildShopUiPreview()
    {
        SeedMerchantPreview();
        MerchantInit();
        OnMerchantList(4242, new[]
        {
            PreviewStallItem(156211000, 1, 30, 350_000_000),
            PreviewStallItem(379080000, 8, 0, 5_000_000),
            PreviewStallItem(811182000, 35, 1, 12_500),
            default, default, default, default, default, default, default, default, default,
        });
        _merchantAdverts[4242] = "cheap gear, come look";
        _shopPanel.Title = ShopTitle(4242);
        return DetachPreviewControl(_shopPanel);
    }

    internal Control BuildTradeConfirmUiPreview()
    {
        SeedMerchantPreview();
        MerchantInit();
        OnMerchantList(4242, new[]
        {
            PreviewStallItem(379080000, 40, 0, 5_000_000),
            default, default, default, default, default,
            default, default, default, default, default, default,
        });
        BuyFromStall(0);
        return DetachPreviewControl((Control)_amountLayer.GetChild(1));
    }

    internal Control BuildPricePromptUiPreview(bool stackable)
    {
        SeedMerchantPreview();
        MerchantInit();
        OnMerchantOpenResult(Net.MerchantOpenAccepted);
        StageStallItem(stackable ? 2 : 0);
        TypePriceUiPreview("50000000");
        return DetachPreviewControl((Control)_amountLayer.GetChild(1));
    }

    internal string TypePriceUiPreview(string typed)
    {
        _amountPrice.GrabFocus();
        _amountPrice.Text = "";
        foreach (char c in typed)
        {
            _amountPrice.Text += c;
            _amountPrice.CaretColumn = _amountPrice.Text.Length;
            _amountPrice.EmitSignal(LineEdit.SignalName.TextChanged, _amountPrice.Text);
        }
        return $"shown=\"{_amountPrice.Text}\" value={_amountPrice.Value} total={_amountTotal.Text}";
    }

    internal Control BuildWishListUiPreview(bool filled)
    {
        SeedMerchantPreview();
        MerchantInit();
        OpenWishList();
        if (filled)
        {
            _wishes[0] = new Network.MerchantWishItem { ItemId = 156211000, Count = 1, Price = 350_000_000 };
            _wishes[1] = new Network.MerchantWishItem { ItemId = 379080000, Count = 20, Price = 4_800_000 };
            RefreshWishList();
        }
        return DetachPreviewControl(_wishPanel);
    }

    internal Control BuildWishQuantityUiPreview(string query)
    {
        SeedMerchantPreview();
        MerchantInit();
        OpenWishList();
        OnWishSlotClicked(0);
        _wishFind.SetQuery(query);
        _wishFind.Run();
        _wishFind.RegisterFirstUiPreview();
        return DetachPreviewControl((Control)_amountLayer.GetChild(1));
    }

    internal Control BuildItemSearchUiPreview(string query)
    {
        SeedMerchantPreview();
        MerchantInit();
        OpenWishList();
        OnWishSlotClicked(0);
        _wishFind.SetQuery(query);
        _wishFind.Run();
        return DetachPreviewControl(_wishFindPanel);
    }

    internal Control BuildWantedStallUiPreview()
    {
        SeedMerchantPreview();
        MerchantInit();
        OnBuyMerchantList(4242, new[]
        {
            PreviewStallItem(379080000, 20, 0, 4_800_000),
            PreviewStallItem(811182000, 50, 1, 9_000),
            default, default, default, default, default, default, default, default, default, default,
        });
        return DetachPreviewControl(_wantedPanel);
    }

    private void SeedMerchantPreview()
    {
        ItemData.EnsureLoaded();
        Sheet.SeedWealth(gold: 9_250_671, np: 2_450);
        Inv.EnsureLength(GridStart + GridCount);
        Inv[GridStart] = PreviewItem(156211000, 1, 30);
        Inv[GridStart + 1] = PreviewItem(810684000, 12, 1);
        Inv[GridStart + 2] = PreviewItem(379080000, 128, 0);
        Inv[GridStart + 3] = PreviewItem(811182000, 35, 1);
        Inv[GridStart + 5] = PreviewItem(900402000, 5, 1);
        BuildItemTooltip();
    }

    private static Network.MerchantStallItem PreviewStallItem(int id, int count, short durability, int price) => new()
    {
        ItemId = id,
        Count = count,
        Durability = durability,
        Price = price,
    };

    private static ItemSlot PreviewItem(int id, int count, int durability) => new()
    {
        ItemId = id,
        Count = (short)count,
        Durability = (short)durability,
    };

    private static Control DetachPreviewControl(Control control)
    {
        foreach (Node child in control.GetChildren())
        {
            if (child is not HudLayout layout) continue;
            control.RemoveChild(layout);
            layout.Free();
        }
        control.GetParent()?.RemoveChild(control);
        return control;
    }
}
