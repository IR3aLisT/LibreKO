using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int TowerNoButton = -1;
    private const int TowerBoardMode = 0;
    private const int TowerUseMode = 1;
    private const int DropOffLeave = 1;
    private const int DropOffCancel = 2;
    private const float DropOffWidth = 85f;
    private const float DropOffHeight = 25f;
    private const float DropOffBelowCentre = 100f;
    private const float DropOffBeside = 2f;
    private const double TowerWarpQuiet = 3.0;
    private static readonly Color TowerLineColour = new(1f, 1f, 0f);
    private static readonly Color TowerTownColour = new(1f, 0f, 1f);

    private Button _towerButton = null!;
    private int _towerButtonMode = TowerNoButton;
    private CanvasLayer _dropOffLayer = null!;
    private Button _dropOff = null!;
    private int _dropOffMode;
    private bool _onTower;
    private int _towerHostId = -1;
    private double _towerCooldownUntil;

    private void BuildTowerButton(VBoxContainer box)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddChild(row);
        _towerButton = UiTheme.SmallButton("", "");
        _towerButton.Visible = false;
        _towerButton.Pressed += OnTowerButton;
        row.AddChild(_towerButton);
    }

    private void SiegeTowerInit()
    {
        _dropOffLayer = new CanvasLayer { Layer = 63 };
        AddChild(_dropOffLayer);
        _dropOff = UiTheme.ActionButton("", "");
        _dropOff.CustomMinimumSize = new Vector2(DropOffWidth, DropOffHeight);
        _dropOff.Visible = false;
        _dropOff.Pressed += OnDropOff;
        _dropOffLayer.AddChild(_dropOff);
        GetViewport().SizeChanged += PlaceDropOff;
        Net.I.SiegeTowerEvent += OnSiegeTower;
    }

    private void SiegeTowerDispose()
    {
        GetViewport().SizeChanged -= PlaceDropOff;
        Net.I.SiegeTowerEvent -= OnSiegeTower;
    }

    private static string TowerText(int id) => ItemData.Text(id, SiegeTower.Fallback(id));

    private void TowerLine((int Id, string Fallback) text) => CombatNotice(ItemData.Text(text.Id, text.Fallback), TowerLineColour);

    private void TowerLine(int id) => CombatNotice(TowerText(id), TowerLineColour);

    private int LookOf(int skillId) => SkillData.Get(skillId)?.TransformModelId ?? 0;

    private bool InSiegeLook => SiegeTower.InSiegeLook(LookOf(_selfTransformSkill));

    private int TowerModeFor(int id)
    {
        if (id < 0 || !_ents.TryGetValue(id, out var e) || e.Dead || _onTower) return TowerNoButton;
        if (!e.IsNpc && LookOf(e.TransformSkill) == SiegeTower.MovingTowerLook && !InSiegeLook
            && SiegeTower.MayBoard(_zone == SiegeWar.Delos, MyClan.InClan ? MyClan.ClanId : 0, MyClan.InClan ? MyClan.AllianceId : 0,
                e.KnightsId, e.AllianceId, e.Attackable))
            return TowerBoardMode;
        if (e.IsNpc && e.NpcType == SiegeTower.GuardTowerNpcType && _selfTransformSkill == 0 && !e.Attackable)
            return TowerUseMode;
        return TowerNoButton;
    }

    private void SyncTowerButton(int shownId)
    {
        int mode = TowerModeFor(shownId);
        if (mode == _towerButtonMode) return;
        _towerButtonMode = mode;
        _towerButton.Visible = mode != TowerNoButton;
        if (mode != TowerNoButton)
            _towerButton.Text = TowerText(mode == TowerBoardMode ? SiegeTower.BoardingText : SiegeTower.UseText);
    }

    private void OnTowerButton()
    {
        double left = _towerCooldownUntil - Now();
        if (left > 0)
        {
            CombatNotice(TextTemplate.Fill(TowerText(SiegeTower.CooldownText), SiegeTower.CooldownSeconds(left)), TowerLineColour);
            return;
        }
        RequestTower(_targetHudId, _towerButtonMode);
    }

    private void RequestTower(int id, int mode)
    {
        if (Net.I.SiegeTowerPending || mode == TowerNoButton || TowerModeFor(id) != mode) return;
        if (!_ents.TryGetValue(id, out var e)) return;
        if (e.Body.Position.DistanceTo(_self.Position) > SiegeTower.BoardReach)
        {
            TowerLine(SiegeTower.TooFarText);
            return;
        }
        _hasMoveTarget = false;
        _terrainMoveHeld = false;
        _autoMoveForward = false;
        SendMoveStop();
        if (mode == TowerBoardMode)
        {
            if (Net.I.SendSiegeTowerBoard(id)) _towerHostId = id;
        }
        else Net.I.SendSiegeTowerUse(id);
    }

    private void OnDropOff()
    {
        if (_dropOffMode == DropOffCancel)
        {
            if (LookOf(_selfTransformSkill) == SiegeTower.GuardTowerLook) Net.I.SendSiegeTowerExit();
            return;
        }
        if (!_onTower)
        {
            TowerLine(SiegeTower.NotOnboardText);
            return;
        }
        if (!_ents.TryGetValue(_towerHostId, out var tower))
        {
            Net.I.SendSiegeTowerLeave(SiegeTower.Wire(_myKoX), SiegeTower.Wire(_myKoZ));
            return;
        }
        var side = tower.Body.GlobalTransform.Basis.X.Normalized() * DropOffBeside;
        float x = tower.KoX + side.X, z = tower.KoZ + side.Z;
        var ground = EntityGroundPos(x, z, NoServerHeight, 0f);
        if (!float.IsFinite(ground.Y))
        {
            TowerLine(SiegeTower.UnloadableText);
            return;
        }
        Net.I.SendSiegeTowerLeave(SiegeTower.Wire(x), SiegeTower.Wire(z));
    }

    private void ShowDropOff(int mode)
    {
        _dropOffMode = mode;
        _dropOff.Text = TowerText(mode == DropOffCancel ? SiegeTower.CancelText : SiegeTower.ExitText);
        _dropOff.Visible = true;
        PlaceDropOff();
    }

    private void PlaceDropOff()
    {
        if (_dropOff == null || !IsInstanceValid(_dropOff)) return;
        var screen = GetViewport().GetVisibleRect().Size;
        _dropOff.Position = new Vector2((screen.X - DropOffWidth) * 0.5f, (screen.Y - DropOffHeight) * 0.5f + DropOffBelowCentre);
    }

    private void OnSiegeTower(SiegeTowerReply reply)
    {
        bool ok = reply.Result == SiegeTower.Succeeded;
        switch (reply.Sub)
        {
            case SiegeTower.SubBoard:
                if (!ok) { _towerHostId = -1; TowerLine(SiegeTower.BoardRefusal(reply.Result)); break; }
                _onTower = true;
                _blinkWarpUntil = Now() + TowerWarpQuiet;
                ShowDropOff(DropOffLeave);
                break;
            case SiegeTower.SubLeave:
                if (!ok) { TowerLine(SiegeTower.LeaveRefusal(reply.Result)); break; }
                _blinkWarpUntil = Now() + TowerWarpQuiet;
                LeaveTower();
                break;
            case SiegeTower.SubEnded:
                TowerLine(SiegeTower.EndedText(reply.Result));
                LeaveTower();
                break;
            case SiegeTower.SubUse:
                if (!ok) { TowerLine(SiegeTower.UseRefusal(reply.Result)); break; }
                if (reply.UserId == _myId)
                {
                    _blinkWarpUntil = Now() + TowerWarpQuiet;
                    ShowDropOff(DropOffCancel);
                }
                else PlaceOnTower(reply.UserId, reply.X, reply.Z);
                break;
            case SiegeTower.SubExit:
                if (!ok) { TowerLine(SiegeTower.ExitRefusal); break; }
                if (reply.UserId != _myId) break;
                _dropOff.Visible = false;
                _towerCooldownUntil = Now() + SiegeTower.Cooldown;
                break;
        }
    }

    private void LeaveTower()
    {
        _dropOff.Visible = false;
        _onTower = false;
        _towerHostId = -1;
        _towerCooldownUntil = Now() + SiegeTower.Cooldown;
        if (Fx.NameForId(SiegeTower.LeaveEffect) is { } fx) SpawnFxOn(_myId, fx, 0f);
    }

    private void PlaceOnTower(int charId, float x, float z)
    {
        if (!_ents.TryGetValue(charId, out var e)) return;
        var pos = EntityGroundPos(x, z, NoServerHeight, e.Lift);
        e.Body.Position = pos;
        e.Target = pos;
        e.HasTarget = true;
        e.Speed = 0f;
        e.KoX = x;
        e.KoZ = z;
    }

    private bool RefusedTownOnTower()
    {
        if (!_onTower && !InSiegeLook) return false;
        CombatNotice(TowerText(SiegeTower.TownRefusedText), TowerTownColour);
        return true;
    }
}
