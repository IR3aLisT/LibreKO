using System;
using System.Collections.Generic;
using Godot;

namespace LibreKO;

// Order is load-bearing: later facets read what earlier ones built. Teardown runs in the same order.
public partial class World : Node3D
{
    private readonly List<Action> _facetTeardown = new();

    private void Facet(Action init, Action? teardown = null)
    {
        init();
        if (teardown != null) _facetTeardown.Add(teardown);
    }

    private void BuildFacets()
    {
        Facet(BuildStatusHud);
        Facet(BuildBuffBar);
        Facet(BuildMiniMap);
        Facet(BuildNpcFinder);
        Facet(BuildFullMap);
        Facet(BuildClockHud);
        Facet(BuildSelectionRing);
        Facet(BuildObjectHighlight);
        Facet(BuildTargetHud, TargetQuestDropsDispose);
        if (Config.Development)
            Facet(BuildInfoPanel);
        Facet(BuildEscMenu);
        Facet(EscCloseInit, EscCloseDispose);
        Facet(
            () => { Chat = new ChatSystem(this); Chat.Build(); Chat.LocalCommand = RunLocalCommand; Chat.NationChatRequested = ToggleNationChat; Chat.GestureRequested = SendChatGesture; },
            () => Chat?.Dispose());
        Facet(BuildCombatLog, CombatLogDispose);
        Facet(() => { Floaters = new FloaterSystem(this); Floaters.Build(); }, () => Floaters?.Dispose());
        Facet(CombatInit);
        Facet(InventoryInit);
        Facet(StatsInit, StatsDispose);
        Facet(FriendsInit, FriendsDispose);
        Facet(ClanInit, ClanDispose);
        Facet(ResetConfirmInit, ResetConfirmDispose);
        Facet(MonsterStoneInit, MonsterStoneDispose);
        Facet(HotbarInit, HotbarDispose);
        Facet(SkillWindowInit, SkillWindowDispose);
        Facet(PartyInit, PartyDispose);
        Facet(LootInit, LootDispose);
        Facet(NpcInit, NpcDispose);
        Facet(QuestInit, QuestDispose);
        Facet(MainPanelInit);
        Facet(BagCompanionInit);
        Facet(DockInit, DockDispose);
        Facet(ChatHostInit, ChatHostDispose);
        Facet(EventPlatesInit, EventPlatesDispose);
        Facet(() => BuildHudLauncher());
        if (Platform.TouchUi) Facet(TouchHudInit, TouchHudDispose);
        Facet(VendorInit, VendorDispose);
        Facet(UpgradeInit, UpgradeDispose);
        Facet(PieceChangeInit, PieceChangeDispose);
        Facet(ItemSmashInit, ItemSmashDispose);
        Facet(RepairInit, RepairDispose);
        Facet(WarehouseInit, WarehouseDispose);
        Facet(ClassChangeInit, ClassChangeDispose);
        Facet(ExchangeInit, ExchangeDispose);
        Facet(GatherInit, GatherDispose);
        Facet(MerchantInit, MerchantDispose);
        Facet(CosmeticsInit, CosmeticsDispose);
        Facet(RankAuraInit, RankAuraDispose);
        Facet(PremiumInit, PremiumDispose);
        Facet(EventTitlesInit, EventTitlesDispose);
        Facet(SeasonalInit, SeasonalDispose);
        Facet(ZoneAbilityInit, ZoneAbilityDispose);
        Facet(ShoutInit, ShoutDispose);
        Facet(RankInit, RankDispose);
        Facet(ChaosInit, ChaosDispose);
        Facet(KnightRoyaleInit, KnightRoyaleDispose);
        Facet(ChallengeInit, ChallengeDispose);
        Facet(WarpInit, WarpDispose);
        Facet(ShoppingMallInit, ShoppingMallDispose);
        Facet(BattleEventInit, BattleEventDispose);
        Facet(PetInit, PetDispose);
        Facet(PetBarInit, PetBarDispose);
        Facet(RebirthInit, RebirthDispose);
        Facet(GenderChangeInit, GenderChangeDispose);
        Facet(NationTransferInit, NationTransferDispose);
        Facet(MerchantSearchInit, MerchantSearchDispose);
        Facet(MarketPriceInit, MarketPriceDispose);
        Facet(BifrostInit, BifrostDispose);
        Facet(BorderDefenseWarInit, BorderDefenseWarDispose);
        Facet(CapeInit, CapeDispose);
        Facet(NameChangeInit, NameChangeDispose);
        Facet(KingInit, KingDispose);
        Facet(SiegeInit, SiegeDispose);
        Facet(PvpInit, PvpDispose);
        Facet(StateVisualInit, StateVisualDispose);
        Facet(RespawnBlinkInit, RespawnBlinkDispose);
        Facet(SizeStateInit, SizeStateDispose);
        Facet(GroundEffectInit, GroundEffectDispose);
        Facet(EffectInit, EffectDispose);
        Facet(AngerGaugeInit, AngerGaugeDispose);
        Facet(DeathDialogInit, DeathDialogDispose);
        Facet(ResurrectOfferInit, ResurrectOfferDispose);
        Facet(TargetEffectsInit, TargetEffectsDispose);
        Facet(ControlStateInit, ControlStateDispose);
        Facet(SealInit, SealDispose);
        Facet(ClanWarehouseInit, ClanWarehouseDispose);
        Facet(VipWarehouseInit, VipWarehouseDispose);
        Facet(PartyCommanderInit, PartyCommanderDispose);
        Facet(UserVoteInit, UserVoteDispose);
        Facet(UiOpacityInit, UiOpacityDispose);
        Facet(() => NamePlate.RescaledEvent += OnNamePlatesRescaled, () => NamePlate.RescaledEvent -= OnNamePlatesRescaled);
        Facet(ClanBattleInit, ClanBattleDispose);
        Facet(ClanPremiumInit, ClanPremiumDispose);
        Facet(ObjectEventInit, ObjectEventDispose);
        Facet(ReportInit, ReportDispose);
        Facet(StealthInit, StealthDispose);
        Facet(AwakenInit, AwakenDispose);
        Facet(ChangeHairInit, ChangeHairDispose);
        Facet(TownRecallInit);
        Facet(NestTimerInit, NestTimerDispose);
        Facet(UpgradeNoticeInit, UpgradeNoticeDispose);
        Facet(MailInit, MailDispose);
        Facet(SpecialAuctionInit, SpecialAuctionDispose);
        Facet(AttendanceInit, AttendanceDispose);
        Facet(AchievementInit, AchievementDispose);
        Facet(MailIconInit, MailIconDispose);
        Facet(TopIconsInit, TopIconsDispose);
        Facet(EventsInit, EventsDispose);
        Facet(SiegeWarInit, SiegeWarDispose);
        Facet(SiegeTowerInit, SiegeTowerDispose);
        Facet(VanguardInit, VanguardDispose);
        Facet(NereidInit, NereidDispose);
        Facet(FullMoonInit, FullMoonDispose);
        Facet(ExpSealInit, ExpSealDispose);
        Facet(CharacterSealInit, CharacterSealDispose);
        Facet(CommandListInit, CommandListDispose);
        Facet(ExitMenuInit, ExitMenuDispose);
        Facet(ClanMatchInit, ClanMatchDispose);
        Facet(DropListInit, DropListDispose);
        Facet(ClanMarksInit, ClanMarksDispose);
        Facet(BountyInit, BountyDispose);
        Facet(TournamentInit, TournamentDispose);
        Facet(DisguiseInit, DisguiseDispose);
        Facet(PresetInit, PresetDispose);
        Facet(MessengerInit, MessengerDispose);
        Facet(ForcesInit, ForcesDispose);
        Facet(NationChatInit, NationChatDispose);
        Facet(ChatRoomInit, ChatRoomDispose);
        Facet(KillSplashInit, KillSplashDispose);
        Facet(KrowazInit, KrowazDispose);
        Facet(ChatNoticesInit, ChatNoticesDispose);
        Facet(FortuneInit, FortuneDispose);
        Facet(ItemCombineInit, ItemCombineDispose);
        Facet(RouletteInit, RouletteDispose);
        Facet(FishingHallInit, FishingHallDispose);
        Facet(PluginBridgeInit, PluginBridgeDispose);
        Facet(GuardPetInit, GuardPetDispose);
        Facet(GlobalMapInit, GlobalMapDispose);
        Facet(GenieInit, GenieDispose);
        Facet(CollectionRaceInit, CollectionRaceDispose);
        Facet(LotteryInit, LotteryDispose);
        Facet(RentalInit, RentalDispose);
        Facet(PlayerMenuInit, PlayerMenuDispose);
        Facet(UserInfoInit, UserInfoDispose);
        Facet(EquipViewInit, EquipViewDispose);
        Facet(WhisperInit, WhisperDispose);
        Facet(ChatReactionsInit, ChatReactionsDispose);
        Facet(CursorInit, CursorDispose);
        Facet(AdminPanelInit, AdminPanelDispose);
        Facet(GmFxInit, GmFxDispose);
        Facet(RankerMessageInit, RankerMessageDispose);
        Facet(RankBoardInit, RankBoardDispose);
        Facet(HpStatusInit, HpStatusDispose);
        Facet(ClanBattleWarInit, ClanBattleWarDispose);
        Facet(PartyWarBoardInit, PartyWarBoardDispose);
        Facet(ClanBoardInit, ClanBoardDispose);
        Facet(DailyRankInit, DailyRankDispose);
        Facet(TimedItemsInit);
        Facet(RepurchaseInit, RepurchaseDispose);
        Facet(SoccerInit, SoccerDispose);
        Facet(ContinuanceInit, ContinuanceDispose);
        Facet(PetPlateInit);
        Facet(DisplayPrefsInit, DisplayPrefsDispose);
        Facet(FxLayerInit, FxLayerDispose);
        Facet(CentredWindowsInit);
        Facet(BuildAudio, TeardownAudio);
    }

    private FxLayer? _fxLayer;

    private void FxLayerInit() => _fxLayer = FxLayer.Attach(GetViewport());

    private void FxLayerDispose()
    {
        if (_fxLayer != null && GodotObject.IsInstanceValid(_fxLayer)) _fxLayer.QueueFree();
        _fxLayer = null;
    }

    private void TeardownFacets()
    {
        foreach (var teardown in _facetTeardown) teardown();
        _facetTeardown.Clear();
    }
}
