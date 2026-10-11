using System;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _bdwHudLayer = null!;
    private WarScoreStrip _bdwScoreBanner = null!;
    private Godot.Timer _bdwTick = null!;

    private const int BdwVictorySound = 340119;
    private const int BdwDefeatSound = 340118;
    private const string BdwCentre = "VS";
    private const string BdwAltarStands = "Altar of Manes: Active in Center";
    private const string BdwCarrierFx = "ft_chr_point_fxqallll";
    private const string BdwCarrierFxNode = "bdw_carrier_fx";
    private const string JuraidStatus = "Clear the rooms and slay Devabird";
    private Node3D? _bdwCarrierFx;

    private static bool IsWarRoomZone(int zone) => zone is BdwZone or JuraidZone;

    private void BorderDefenseWarInit()
    {
        _bdwHudLayer = new CanvasLayer { Layer = 95 };
        AddChild(_bdwHudLayer);

        BuildBdwScoreBanner();

        _bdwTick = new Godot.Timer { WaitTime = 1.0, Autostart = false, OneShot = false };
        _bdwTick.Timeout += RefreshBdwHud;
        _bdwHudLayer.AddChild(_bdwTick);

        Net.I.TempleScreenScoreEvent += OnBdwScoresReceived;
        Net.I.AltarFlagEvent += OnBdwAltarFlagReceived;
        Net.I.AltarTimerEvent += OnBdwAltarTimerReceived;
        Net.I.TempleEventFinishEvent += OnBdwFinishReceived;
        Net.I.BorderWarRankEvent += OnBorderWarRanking;

        if (!IsWarRoomZone(_zone))
        {
            Net.I.BorderWar.Reset();
            return;
        }
        _bdwScoreBanner.RankingShown = _zone == BdwZone;
        _bdwScoreBanner.Visible = true;
        RefreshBdwHud();
        RefreshBdwCarrierFx();
        if (Net.I.BorderWar.Finished && Net.I.BorderWar.HomeSecondsLeft(DateTime.UtcNow) > 0)
            ShowWarResult();
    }

    private void BorderDefenseWarDispose()
    {
        Net.I.TempleScreenScoreEvent -= OnBdwScoresReceived;
        Net.I.AltarFlagEvent -= OnBdwAltarFlagReceived;
        Net.I.AltarTimerEvent -= OnBdwAltarTimerReceived;
        Net.I.TempleEventFinishEvent -= OnBdwFinishReceived;
        Net.I.BorderWarRankEvent -= OnBorderWarRanking;

        _bdwTick?.Stop();
        if (_bdwHudLayer != null && IsInstanceValid(_bdwHudLayer))
            _bdwHudLayer.QueueFree();
    }

    private void BuildBdwScoreBanner()
    {
        _bdwScoreBanner = new WarScoreStrip(withStatus: true)
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Centre = BdwCentre,
        };
        _bdwScoreBanner.RankingPressed += ToggleWarRanking;
        _bdwHudLayer.AddChild(_bdwScoreBanner);
        AddEventPlate(_bdwScoreBanner);
    }

    private void OnBdwScoresReceived(int karus, int elmo) => RefreshBdwHud();

    private void OnBdwAltarFlagReceived(string playerName, byte nation)
    {
        RefreshBdwHud();
        RefreshBdwCarrierFx();
    }

    private void OnBdwAltarTimerReceived(int seconds)
    {
        RefreshBdwHud();
        RefreshBdwCarrierFx();
    }

    private void RefreshBdwCarrierFx()
    {
        string carrier = _zone == BdwZone ? Net.I.BorderWar.Carrier : "";
        Node3D? body = carrier.Length == 0 ? null : BodyNamed(carrier);
        if (_bdwCarrierFx != null && IsInstanceValid(_bdwCarrierFx))
        {
            if (body != null && _bdwCarrierFx.GetParent() == body) return;
            Fx.Free(_bdwCarrierFx);
        }
        _bdwCarrierFx = null;
        if (body == null) return;
        _bdwCarrierFx = Fx.Spawn(BdwCarrierFx, body, Vector3.Zero, oneShot: false);
        if (_bdwCarrierFx != null) _bdwCarrierFx.Name = BdwCarrierFxNode;
    }

    private Node3D? BodyNamed(string name)
    {
        if (string.Equals(name, Net.I.LastEnter.Name, StringComparison.OrdinalIgnoreCase)) return _self;
        foreach (var ent in _ents.Values)
            if (!ent.IsNpc && string.Equals(ent.Name, name, StringComparison.OrdinalIgnoreCase)) return ent.Body;
        return null;
    }

    private void OnBdwFinishReceived(int eventId, int winnerNation, uint seconds)
    {
        bool won = winnerNation != 0 && winnerNation == Net.I.LastEnter.Nation;
        Audio.PlayUi(won ? BdwVictorySound : BdwDefeatSound);
        CombatNotice(BdwResultLine(winnerNation));
        RefreshBdwHud();
        RefreshBdwCarrierFx();
        if (IsWarRoomZone(_zone)) ShowWarResult();
    }

    private static string BdwResultLine(int winnerNation) =>
        winnerNation == 0 ? "The battle ended in a draw."
        : winnerNation == Net.I.LastEnter.Nation ? "Your nation has won the battle."
        : "Your nation has lost the battle.";

    private void RefreshBdwHud()
    {
        if (!IsWarRoomZone(_zone) || !IsInstanceValid(_bdwScoreBanner))
        {
            _bdwTick.Stop();
            return;
        }

        var war = Net.I.BorderWar;
        var now = DateTime.UtcNow;
        _bdwScoreBanner.SetScores(war.KarusScore, war.ElmoradScore);
        int end = war.EndSecondsLeft(now);
        _bdwScoreBanner.SetClock(end > 0 ? $"End  {ClockText(end)}" : "");
        bool counting = end > 0;
        RefreshWarResult(now);
        if (war.Winner is { } winner)
        {
            int home = war.HomeSecondsLeft(now);
            var colour = winner == 0 ? UiTheme.TextHi : winner == Net.I.LastEnter.Nation ? UiTheme.Good : UiTheme.Bad;
            _bdwScoreBanner.SetStatus($"{BdwResultLine(winner)}  {home / 60}:{home % 60:00}", colour);
            counting |= home > 0;
        }
        else if (_zone == JuraidZone)
        {
            _bdwScoreBanner.SetStatus(JuraidStatus, UiTheme.TextHi);
        }
        else if (war.Carrier.Length > 0)
        {
            ShowBdwCarrier(war.Carrier, war.CarrierNation);
        }
        else if (war.AltarSecondsLeft(now) is > 0 and var altar)
        {
            _bdwScoreBanner.SetStatus($"Altar respawning in {altar}s", UiTheme.GoldBright);
            counting = true;
        }
        else
        {
            _bdwScoreBanner.SetStatus(BdwAltarStands, UiTheme.Good);
        }

        if (!counting) _bdwTick.Stop();
        else if (_bdwTick.IsStopped()) _bdwTick.Start();
    }

    private void ShowBdwCarrier(string carrier, byte nation)
    {
        bool karus = nation == Nations.Karus;
        _bdwScoreBanner.SetStatus($"Carrier: {carrier} ({(karus ? "Karus" : "El Morad")})",
            karus ? WarScoreStrip.KarusColour : WarScoreStrip.ElmoradColour);
    }
}
