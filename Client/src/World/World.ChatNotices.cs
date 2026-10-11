using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int TextQuestFailed = 7456;
    private const int TextTopSecretCaptured = 7457;
    private const int TextTopSecretLost = 7458;
    private const int TextTopSecretExpired = 7459;
    private const int TextMonumentCaptured = 7460;
    private const int TextMonumentLost = 7461;
    private const int TextSpyRescueStarted = 7462;
    private const int TextSpyRescueDone = 7463;
    private const int TextEnemySpyRescueStarted = 7464;
    private const int TextEnemySpyRescueDone = 7465;
    private const int TextEnemySuspicious = 7466;
    private const int TextEnemiesInvaded = 7467;
    private const int TextChaosStoneBroken = 7470;
    private const int TextChaosStoneBrokenByEnemy = 7471;
    private const int TextBoxItem = 17000;
    private const int AlertInvaded = 1;
    private const int AlertSuspicious = 2;
    private static readonly Color OrderMessageRed = new(1f, 0f, 0f);
    private static readonly Color OrderMessageYellow = new(1f, 1f, 0f);
    private static readonly Color BoxItemNoticeColour = new("e70df2");

    private CanvasLayer? _orderMessageLayer;
    private Label? _orderMessage;
    private OrderMessage _orderLook;
    private double _orderShownAt = double.NegativeInfinity;

    private void ChatNoticesInit()
    {
        Net.I.MonumentNoticeEvent += OnMonumentNotice;
        Net.I.ChaosStoneNoticeEvent += OnChaosStoneNotice;
        Net.I.BoxItemNoticeEvent += OnBoxItemNotice;
    }

    private void ChatNoticesDispose()
    {
        Net.I.MonumentNoticeEvent -= OnMonumentNotice;
        Net.I.ChaosStoneNoticeEvent -= OnChaosStoneNotice;
        Net.I.BoxItemNoticeEvent -= OnBoxItemNotice;
    }

    private void OnMonumentNotice(MonumentNotice notice)
    {
        bool own = notice.Nation == Net.I.Nation;
        switch (notice.Kind)
        {
            case MonumentNotice.TopSecretCaptured:
                ShowOrderMessage(own ? NoticeText(TextTopSecretCaptured, "<%s>captured enemy nation's Top-Secret successfully", notice.Name)
                    : NoticeText(TextTopSecretLost, "Top-Secret has been captured by enemy Nation"), OrderMessageRed, OrderMessage.Flash);
                break;
            case MonumentNotice.QuestFailed:
                ShowOrderMessage(NoticeText(TextQuestFailed, "%s Quest failed", QuestData.Name(notice.Value, Net.I.LastEnter.Name)),
                    OrderMessageRed, OrderMessage.FlashOnce);
                break;
            case MonumentNotice.TopSecretExpired:
                ShowOrderMessage(NoticeText(TextTopSecretExpired, "Enemy Nation's Top-secret capturing time has been expired"),
                    OrderMessageRed, OrderMessage.Flash);
                break;
            case MonumentNotice.MonumentCaptured:
                ShowOrderMessage(own ? NoticeText(TextMonumentCaptured, "<%s> has captured center Monument", notice.Name)
                    : NoticeText(TextMonumentLost, "Enemy nation has captured center Monument"), OrderMessageYellow, OrderMessage.Hold);
                Audio.PlayUi(Sfx.TowerDestroyed);
                break;
            case MonumentNotice.SpyRescue:
                ShowOrderMessage(SpyRescueText(own, notice.Done, notice.Name), OrderMessageRed, OrderMessage.Hold);
                break;
            case MonumentNotice.Alert when notice.Value is AlertInvaded or AlertSuspicious:
                ShowOrderMessage(notice.Value == AlertInvaded
                    ? NoticeText(TextEnemiesInvaded, "Enemies invaded")
                    : NoticeText(TextEnemySuspicious, "Enemy's activity is suspicious"), OrderMessageRed, OrderMessage.Flash);
                break;
        }
    }

    private static string SpyRescueText(bool own, bool done, string name) => (own, done) switch
    {
        (true, false) => NoticeText(TextSpyRescueStarted, "<%s>is carrying out the rescue spy operation", name),
        (true, true) => NoticeText(TextSpyRescueDone, "<%s>rescued %d number of spy successfully", name),
        (false, false) => NoticeText(TextEnemySpyRescueStarted, "<%s>is attempting to rescue the spy", name),
        _ => NoticeText(TextEnemySpyRescueDone, "Enemy nation has rescued the spy succesfully"),
    };

    private void OnChaosStoneNotice(ChaosStoneNotice notice) =>
        ShowOrderMessage(notice.Nation == Net.I.Nation
            ? notice.Breaker + NoticeText(TextChaosStoneBroken, "You have destroyed the [Summoning Stone of Chaos] and opened the Gates of Chaos.")
            : NoticeText(TextChaosStoneBrokenByEnemy, "The enemy has destroyed the [Summoning Stone of Chaos] and opened the Gates of Chaos."),
            OrderMessageRed, OrderMessage.Flash);

    private void OnBoxItemNotice(BoxItemNotice notice) =>
        CombatNotice(NoticeText(TextBoxItem, "From %s, %s has acquired the %s item.",
            ItemData.DisplayName(ItemData.BaseId(notice.BoxItem)), notice.Name, ItemData.DisplayName(ItemData.BaseId(notice.Item))),
            BoxItemNoticeColour);

    private static string NoticeText(int id, string fallback, params object[] args) =>
        TextTemplate.Fill(ItemData.Text(id, fallback), args);

    private void ShowOrderMessage(string text, Color colour, OrderMessage look) =>
        PlaceOrderMessage(text, colour, look, GetViewport().GetVisibleRect().Size.X);

    private void PlaceOrderMessage(string text, Color colour, OrderMessage look, float viewWidth)
    {
        if (_orderMessage == null || !IsInstanceValid(_orderMessage))
        {
            _orderMessageLayer = new CanvasLayer { Layer = 70 };
            AddChild(_orderMessageLayer);
            _orderMessage = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _orderMessage.SetAnchorsPreset(Control.LayoutPreset.TopWide);
            _orderMessage.OffsetTop = OrderMessage.Top;
            _orderMessage.AddThemeFontSizeOverride("font_size", OrderMessage.FontSize);
            _orderMessage.AddThemeColorOverride("font_shadow_color", Colors.Black);
            _orderMessage.AddThemeConstantOverride("shadow_offset_x", 1);
            _orderMessage.AddThemeConstantOverride("shadow_offset_y", 1);
            _orderMessageLayer.AddChild(_orderMessage);
        }

        var label = _orderMessage;
        float textWidth = label.GetThemeFont("font").GetStringSize(text, HorizontalAlignment.Left, -1, OrderMessage.FontSize).X;
        label.Text = OrderMessage.Split(text, textWidth > viewWidth);
        label.AddThemeColorOverride("font_color", colour);
        _orderLook = look;
        _orderShownAt = Now();
        label.Visible = true;
    }

    private void OrderMessageTick(double now)
    {
        if (_orderMessage == null || !_orderMessage.Visible && now - _orderShownAt >= _orderLook.TotalSeconds) return;
        _orderMessage.Visible = _orderLook.VisibleAt((float)(now - _orderShownAt));
    }
}
