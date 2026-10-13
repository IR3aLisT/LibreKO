using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private PanelContainer _clanBattlePanel = null!;
    private Label _clanBattleLabel = null!;
    private int _clanBattleToken;

    private static readonly Color ClanBattleGold = UiTheme.GoldBright;

    private void ClanBattleInit()
    {
        BuildClanBattleBanner();
        Net.I.ClanBattleNotifyEvent += OnClanBattleNotify;
        Net.I.IdentityCheckResultEvent += OnIdentityCheckResult;
    }

    private void ClanBattleDispose()
    {
        Net.I.ClanBattleNotifyEvent -= OnClanBattleNotify;
        Net.I.IdentityCheckResultEvent -= OnIdentityCheckResult;
    }

    private void BuildClanBattleBanner()
    {
        var layer = new CanvasLayer { Layer = 67 };
        AddChild(layer);

        var panel = new PanelContainer { Visible = false };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Panel(7, true));
        layer.AddChild(panel);
        _clanBattlePanel = panel;

        var m = new MarginContainer();
        UiTheme.Margins(m, 18, 9, 18, 9);
        panel.AddChild(m);

        _clanBattleLabel = UiTheme.Text("", 16, ClanBattleGold, HorizontalAlignment.Center);
        _clanBattleLabel.AddThemeConstantOverride("outline_size", 5);
        m.AddChild(_clanBattleLabel);
        AddEventPlate(panel);
    }

    private void ShowClanBattleBanner(string text, Color colour)
    {
        _clanBattleLabel.Text = text;
        _clanBattleLabel.AddThemeColorOverride("font_color", colour);
        _clanBattlePanel.Visible = true;

        int token = ++_clanBattleToken;
        double secs = Mathf.Clamp(3.5 + text.Length * 0.045, 4.0, 12.0);
        GetTree().CreateTimer(secs).Timeout += () => { if (_clanBattleToken == token) _clanBattlePanel.Visible = false; };
    }

    private void OnClanBattleNotify()
    {
        const string line = "Clan battle status has changed.";
        ShowClanBattleBanner(line, ClanBattleGold);
        CombatLogAdd(line, CombatLogKind.Status);
    }

    private const byte IdentityFailed = 0;
    private const byte IdentityAuthorized = 1;
    private const byte IdentityWrongNumber = 2;
    private const byte IdentityAlreadyAuthorized = 3;
    private const byte IdentityTooManyTries = 4;
    private const byte IdentityWrongNumberAgain = 5;

    private void OnIdentityCheckResult(byte result)
    {
        var (id, fallback) = result switch
        {
            IdentityFailed => (25051, "You have failed to authorize your identification."),
            IdentityAuthorized => (25052, "You have authorized your identification."),
            IdentityWrongNumber or IdentityWrongNumberAgain => (1511, "Wrong ID number"),
            IdentityAlreadyAuthorized => (25053, "You have already authorized your identification."),
            IdentityTooManyTries => (25054, "You have tried more than limited time. You need to relog-in the game."),
            _ => (0, ""),
        };
        if (id != 0) CombatNotice(ItemData.Text(id, fallback));
    }
}
