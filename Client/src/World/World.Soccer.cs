using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string SoccerArt = "res://assets/ui/soccer/";
    private const string SoccerKicksFile = SoccerArt + "kicks.json";
    private const string SoccerBoardLayoutId = "hud_soccer_score";
    private const int SoccerKickXSlot = 0;
    private const int SoccerKickYSlot = 1;
    private const int SoccerKickZSlot = 2;
    private static readonly Color SoccerBlueColor = WarScoreStrip.ElmoradColour;
    private static readonly Color SoccerRedColor = WarScoreStrip.KarusColour;
    private static readonly Color SoccerNoticeColor = new(1f, 1f, 0f);

    private CanvasLayer _soccerLayer = null!;
    private WarScoreStrip? _soccerBoard;
    private double _soccerSeconds;
    private bool _soccerZoneChecked;
    private readonly Dictionary<int, int> _soccerTeams = new();
    private readonly Dictionary<int, Node3D> _soccerFx = new();
    private readonly Dictionary<int, int> _soccerKicks = new();

    private void SoccerInit()
    {
        _soccerLayer = new CanvasLayer { Layer = 61 };
        AddChild(_soccerLayer);
        LoadSoccerKicks();
        Net.I.SoccerGoalEvent += OnSoccerGoal;
        Net.I.SoccerClockEvent += OnSoccerClock;
        Net.I.SoccerSyncEvent += OnSoccerSync;
        Net.I.SoccerEndEvent += OnSoccerEnd;
        Net.I.StateChangeEvent += OnSoccerStateChange;
    }

    private void SoccerDispose()
    {
        Net.I.SoccerGoalEvent -= OnSoccerGoal;
        Net.I.SoccerClockEvent -= OnSoccerClock;
        Net.I.SoccerSyncEvent -= OnSoccerSync;
        Net.I.SoccerEndEvent -= OnSoccerEnd;
        Net.I.StateChangeEvent -= OnSoccerStateChange;
    }

    private void LoadSoccerKicks()
    {
        using var file = Godot.FileAccess.Open(SoccerKicksFile, Godot.FileAccess.ModeFlags.Read);
        if (file == null) return;
        foreach (var (id, distance) in Json.ParseString(file.GetAsText()).AsGodotDictionary())
            _soccerKicks[id.AsString().ToInt()] = distance.AsInt32();
    }

    private bool OnSoccerPitch()
    {
        foreach (var area in _areas)
            if (area.Id == Soccer.PitchRegion && area.Contains(_myKoX, _myKoZ)) return true;
        return false;
    }

    private bool SelfOnSoccerTeam => Net.I.SoccerTeam != Soccer.TeamNone;

    private bool SoccerSkillAllowed(SkillData.Skill s) =>
        Soccer.SkillAllowed(SkillReach.IsSoccerSkill(s.Id), OnSoccerPitch(), SelfOnSoccerTeam, IsSpeedBuff(s));

    private short[]? KickRequestData(int skillId, int targetId)
    {
        if (!_soccerKicks.TryGetValue(skillId, out int distance) || !_ents.TryGetValue(targetId, out var ball)) return null;
        var dest = Soccer.Landing(ball.Body.GlobalPosition, _self.GlobalPosition, distance);
        var (koX, koZ) = WorldToKo(dest);
        var data = new short[MagicDataSlots];
        data[SoccerKickXSlot] = (short)(int)koX;
        data[SoccerKickYSlot] = (short)(int)dest.Y;
        data[SoccerKickZSlot] = (short)(int)koZ;
        return data;
    }

    private void SoccerTick(double delta)
    {
        if (!_soccerZoneChecked && _areasZone == _zone)
        {
            _soccerZoneChecked = true;
            if (SelfOnSoccerTeam && !OnSoccerPitch())
            {
                Net.I.SoccerTeam = Soccer.TeamNone;
                HideSoccerBoard();
            }
            else if (SelfOnSoccerTeam) ApplySoccerTeam(_myId, Net.I.SoccerTeam);
        }
        if (_soccerBoard is not { Visible: true } board || _soccerSeconds <= 0) return;
        _soccerSeconds = Mathf.Max(0, _soccerSeconds - delta);
        board.Centre = Soccer.Clock(_soccerSeconds);
    }

    private void OnSoccerStateChange(int charId, int type, int value)
    {
        if (type != Soccer.StateChangeTeam) return;
        ApplySoccerTeam(charId, value);
        if (charId != _myId) return;
        Net.I.SoccerTeam = value;
        if (value != Soccer.TeamNone)
        {
            if (_soccerBoard?.CloseButton is { } close) close.Visible = false;
            return;
        }
        HideSoccerBoard();
        if (OnSoccerPitch())
            ShowSoccerNotice(SystemText(Soccer.ChooseTeamText, "Please choose a team from arena's manager."));
    }

    private void ApplySoccerTeam(int charId, int team)
    {
        if (_soccerFx.Remove(charId, out var running) && IsInstanceValid(running)) running.QueueFree();
        if (team == Soccer.TeamNone) _soccerTeams.Remove(charId);
        else _soccerTeams[charId] = team;

        var body = charId == _myId ? _self : _ents.TryGetValue(charId, out var e) ? e.Body : null;
        if (body == null || !IsInstanceValid(body) || Fx.NameForId(Soccer.FxFor(team)) is not { } fxName) return;
        if (Fx.Spawn(fxName, body, Vector3.Zero) is { } fx) _soccerFx[charId] = fx;
    }

    private void SoccerForgetEntity(int id)
    {
        _soccerTeams.Remove(id);
        _soccerFx.Remove(id);
    }

    private int SoccerTeamOf(int id) => id == _myId ? Net.I.SoccerTeam : _soccerTeams.GetValueOrDefault(id);

    private void OnSoccerGoal(SoccerGoal goal)
    {
        var board = EnsureSoccerBoard();
        if (!board.Visible)
        {
            ResetSoccerBoard();
            board.Visible = true;
        }
        SetSoccerScores(goal.Blue, goal.Red);

        string kicker = goal.KickerId == _myId ? Net.I.LastEnter.Name
            : _ents.TryGetValue(goal.KickerId, out var e) && !e.IsNpc ? e.Name : "";
        if (kicker.Length > 0)
        {
            bool own = Soccer.IsOwnGoal(SoccerTeamOf(goal.KickerId), goal.GoalTeam);
            string text = own
                ? SystemText(Soccer.OwnGoalText, "%s, you made an own goal. LOL ~~", kicker)
                : SystemText(Soccer.GoalText, "%s Goal! Goal! Goal!", kicker);
            CombatNotice(text, SoccerNoticeColor);
        }

        int mine = Net.I.SoccerTeam;
        if (mine != Soccer.TeamNone && mine == goal.KickerTeam && goal.KickerTeam != goal.GoalTeam)
        {
            _selfMoving = false;
            _hasMoveTarget = false;
            _autoMoveForward = false;
            SendMoveStop();
            Deselect();
            Net.I.SendEmote(Soccer.FirstProvokeEmote + (int)(GD.Randi() % Soccer.ProvokeEmotes));
        }
    }

    private void OnSoccerClock(int seconds)
    {
        var board = EnsureSoccerBoard();
        ResetSoccerBoard();
        board.Visible = true;
        SetSoccerScores(0, 0);
        _soccerSeconds = seconds;
        board.Centre = Soccer.Clock(seconds);
    }

    private void OnSoccerSync(int seconds, int blue, int red)
    {
        var board = EnsureSoccerBoard();
        ResetSoccerBoard();
        board.Visible = true;
        SetSoccerScores(blue, red);
        _soccerSeconds = seconds;
        board.Centre = Soccer.Clock(seconds);
    }

    private void OnSoccerEnd(SoccerEnd end)
    {
        HideSoccerBoard();
        var (textId, blueFirst) = Soccer.Result(end.Winner);
        int first = blueFirst ? end.Blue : end.Red, second = blueFirst ? end.Red : end.Blue;
        string text = textId == Soccer.DrawText
            ? SystemText(Soccer.DrawText, "This game ended in a draw, %d - %d")
            : SystemText(Soccer.WinText, "%s team won this game, %d - %d", end.Winner == Soccer.TeamBlue
                ? SystemText(Soccer.BlueText, "Blue")
                : SystemText(Soccer.RedText, "Red"));
        int at = text.IndexOf("%d", System.StringComparison.Ordinal);
        if (at >= 0) text = text.Remove(at, 2).Insert(at, first.ToString());
        at = text.IndexOf("%d", System.StringComparison.Ordinal);
        if (at >= 0) text = text.Remove(at, 2).Insert(at, second.ToString());
        CombatNotice(text, SoccerNoticeColor);
    }

    private void ShowSoccerNotice(string text)
    {
        var box = new AcceptDialog { DialogText = text, Unresizable = true, Exclusive = false };
        box.Confirmed += box.QueueFree;
        box.Canceled += box.QueueFree;
        _soccerLayer.AddChild(box);
        box.PopupCentered();
    }

    private void HideSoccerBoard()
    {
        if (_soccerBoard == null) return;
        ResetSoccerBoard();
        _soccerBoard.Visible = false;
    }

    private void ResetSoccerBoard()
    {
        _soccerSeconds = 0;
        if (_soccerBoard == null) return;
        _soccerBoard.Centre = Soccer.Clock(0);
        SetSoccerScores(0, 0);
        if (_soccerBoard.CloseButton is { } close) close.Visible = !SelfOnSoccerTeam;
    }

    private void SetSoccerScores(int blue, int red) =>
        _soccerBoard?.SetScores(Mathf.Max(0, blue), Mathf.Max(0, red));

    private WarScoreStrip EnsureSoccerBoard()
    {
        if (_soccerBoard != null) return _soccerBoard;
        var board = new WarScoreStrip(withStatus: false,
            SystemText(Soccer.BlueText, "Blue"), SoccerBlueColor,
            SystemText(Soccer.RedText, "Red"), SoccerRedColor, closable: true)
        {
            Visible = false,
        };
        _soccerLayer.AddChild(board);
        _soccerBoard = board;
        board.CloseButton!.Pressed += () =>
        {
            if (SelfOnSoccerTeam) return;
            HideSoccerBoard();
        };
        var layout = HudLayout.Attach(board, SoccerBoardLayoutId, board, () => EventPlateSpot(board));
        AddEventPlate(board, layout, SoccerBoardLayoutId);
        return board;
    }
}
