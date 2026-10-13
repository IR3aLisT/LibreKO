using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _shoutLayer = null!;
    private Label _shoutLabel = null!;
    private int _shoutToken;

    private static readonly Color ShoutGold = UiTheme.GoldBright;
    private static readonly Color ShoutKarus = new("00bfff");
    private static readonly Color ShoutElmorad = new("f08080");

    private void ShoutInit()
    {
        BuildShoutBanner();
        Net.I.ShoutEvent += OnShout;
        Net.I.ShoutUpgradeEvent += OnShoutUpgrade;
        Net.I.ShoutRareItemEvent += OnShoutRareItem;
        Net.I.ShoutResultEvent += OnShoutResult;
    }

    private void ShoutDispose()
    {
        Net.I.ShoutEvent -= OnShout;
        Net.I.ShoutUpgradeEvent -= OnShoutUpgrade;
        Net.I.ShoutRareItemEvent -= OnShoutRareItem;
        Net.I.ShoutResultEvent -= OnShoutResult;
    }

    private void BuildShoutBanner()
    {
        _shoutLayer = new CanvasLayer { Layer = 68, Visible = false };
        AddChild(_shoutLayer);

        var panel = new PanelContainer
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0, AnchorBottom = 0,
            GrowHorizontal = Control.GrowDirection.Both,
            OffsetTop = 150,
        };
        panel.AddThemeStyleboxOverride("panel", UiTheme.Panel(7, true));
        _shoutLayer.AddChild(panel);

        var m = new MarginContainer();
        UiTheme.Margins(m, 18, 9, 18, 9);
        panel.AddChild(m);

        _shoutLabel = UiTheme.Text("", 16, ShoutGold, HorizontalAlignment.Center);
        _shoutLabel.AddThemeConstantOverride("outline_size", 5);
        m.AddChild(_shoutLabel);
    }

    private void ShowShoutBanner(string text, Color colour)
    {
        _shoutLabel.Text = text;
        _shoutLabel.AddThemeColorOverride("font_color", colour);
        _shoutLayer.Visible = true;

        int token = ++_shoutToken;
        double secs = Mathf.Clamp(3.5 + text.Length * 0.045, 4.0, 12.0);
        GetTree().CreateTimer(secs).Timeout += () => { if (_shoutToken == token) _shoutLayer.Visible = false; };
    }

    private void OnShout(string message, byte r, byte g, byte b, int rank)
    {
        if (message.Length == 0) return;
        var colour = new Color(r / 255f, g / 255f, b / 255f);
        float lum = 0.2126f * colour.R + 0.7152f * colour.G + 0.0722f * colour.B;
        if (lum < 0.18f) colour = ShoutGold;

        ShowShoutBanner(message, colour);
        Chat.AppendShout(message);
    }

    private const int TextShoutRegistered = 7687;
    private const int TextShoutRetryLater = 7688;
    private const int TextShoutFailed = 7690;
    private const int TextShoutNoItem = 2302;
    private const int TextShoutLevel = 30302;
    private const int TextShoutRestricted = 30303;
    private const int TextRareItemShout = 30534;
    private const int TextUpgradeShout = 30535;
    private const int TextUpgradeFailShout = 30536;
    private static readonly Color ShoutResultColour = new(1f, 1f, 0f);
    private static readonly Color ShoutAnnouncementColour = Color.Color8(229, 204, 255);

    private void OnShoutUpgrade(bool ok, string name, int itemId, int rank)
    {
        string template = ok
            ? SystemText(TextUpgradeShout, "%s has successfully upgraded to the %s item. Please congratulate them.")
            : SystemText(TextUpgradeFailShout, "%s failed to upgrade the %s item. Please send them your support.");
        string line = TextTemplate.Fill(template, name, AnnouncedItem(itemId));
        ShowShoutBanner(line, ok ? ShoutGold : new Color("ff8a5c"));
        CombatLogAdd(line, CombatLogKind.Status, ShoutAnnouncementColour, AnnouncedMarkup(template, name, itemId));
    }

    private void OnShoutRareItem(string finder, int itemId, byte nation)
    {
        string line = TextTemplate.Fill(
            SystemText(TextRareItemShout, "%s has obtained the %s item. Please congratulate them."),
            finder, AnnouncedItem(itemId));
        var colour = nation == Nations.Karus ? ShoutKarus
            : nation == Nations.ElMorad ? ShoutElmorad : ShoutGold;
        ShowShoutBanner(line, colour);
        CombatLogAdd(line, CombatLogKind.Status, ShoutAnnouncementColour,
            AnnouncedMarkup(SystemText(TextRareItemShout, "%s has obtained the %s item. Please congratulate them."), finder, itemId));
    }

    private const string LogItemMeta = "i:";

    private static string AnnouncedMarkup(string template, string name, int itemId) =>
        TextTemplate.Fill(BbCode.Esc(template), BbCode.Esc(name),
            $"[url={LogItemMeta}{itemId}][color=#{ItemGrade.Tint(itemId).ToHtml(false)}][lb]{BbCode.Esc(AnnouncedItem(itemId))}[rb][/color][/url]");

    private void OnLogMetaHover(Variant meta)
    {
        string text = meta.AsString();
        if (text.StartsWith(LogItemMeta, System.StringComparison.Ordinal) && int.TryParse(text[LogItemMeta.Length..], out int itemId))
            ShowChatItemTip(itemId);
    }

    private static string AnnouncedItem(int itemId) =>
        ItemData.DisplayName(itemId) is { Length: > 0 } name ? name : $"item {itemId}";

    private void OnShoutResult(int result, int number)
    {
        string text = result switch
        {
            Net.ShoutRegisterAccepted => TextTemplate.Fill(
                SystemText(TextShoutRegistered, "Your message has been registered as number %d."), number),
            Net.ShoutRegisterLevelTooLow => SystemText(TextShoutLevel, "Available at level 30 or above"),
            Net.ShoutRegisterChatRestricted => SystemText(TextShoutRestricted, "Cannot be used while in chat restriction status."),
            Net.ShoutRegisterRetryLater => SystemText(TextShoutRetryLater, "Cannot register message. Please try again later."),
            Net.ShoutRegisterNoItem => SystemText(TextShoutNoItem, "Not enough items left in the  inventory."),
            _ => SystemText(TextShoutFailed, "Message registration failed."),
        };
        CombatLogAdd(text, CombatLogKind.Status, ShoutResultColour);
    }

    public void DoShout(string message)
    {
        message = message?.Trim() ?? "";
        if (message.Length == 0) return;
        Net.I.SendLogoShout(message);
    }
}
