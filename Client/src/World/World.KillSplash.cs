using System;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const string KillSplashDir = "res://assets/ui/kill_splash/";
    private const string KillSplashSound = "new_killing.ogg";
    private const string NarrationKillDir = "narration/kill/";
    private const string NarrationAssistDir = "narration/assist/";
    private const int AssistNarrationBase = 12;
    private const float KillSplashFade = 0.5f;
    private static readonly Vector2 KillSplashSize = new(284, 254);
    private static readonly Rect2 KillBackRect = new(14, 0, 252, 254);
    private static readonly Rect2 KillWordRect = new(1, 103, 274, 47);
    private static readonly Rect2 KillGemRect = new(125, 80, 30, 30);
    private static readonly Rect2 KillNameRect = new(0, 180, 280, 17);
    private static readonly Rect2 AssistBackRect = new(0, 0, 254, 254);
    private static readonly Rect2 AssistWordRect = new(128, 117, 97, 25);
    private static readonly Rect2 AssistGradeRect = new(25, 117, 97, 25);
    private const float KillDigitY = 148f;
    private const float AssistDigitY = 142f;
    private const float SplashDigitSize = 20f;
    private static readonly Color StreakLineColour = new("adff2f");

    private CanvasLayer _killSplashLayer = null!;
    private Control? _killSplash;
    private Tween? _killSplashTween;

    private void KillSplashInit()
    {
        _killSplashLayer = new CanvasLayer { Layer = 86 };
        AddChild(_killSplashLayer);
        Net.I.KillStreakEvent += OnKillStreak;
        Net.I.AssistEvent += OnAssist;
    }

    private void KillSplashDispose()
    {
        Net.I.KillStreakEvent -= OnKillStreak;
        Net.I.AssistEvent -= OnAssist;
    }

    private void OnKillStreak(KillStreakReply kill)
    {
        if (kill.Kind == KillSplash.Announcement)
        {
            if (kill.Count > 0 && kill.Count % KillSplash.AnnouncementStep == 0)
                CombatLogAdd(StreakAnnouncement(kill), CombatLogKind.Status, StreakLineColour);
            return;
        }
        if (kill.Count == 0)
        {
            CombatLogAdd(ItemData.Text(KillSplash.ResetText, "Kill count has been reset."), CombatLogKind.Status, StreakLineColour);
            return;
        }
        ShowKillSplash(BuildKillSplash(kill.Count, kill.Nation, kill.Name));
        Audio.PlayUiFile(KillSplashSound);
        int narration = KillSplash.NarrationFor(kill.Count, kill.Nation);
        if (narration > 0) Audio.PlayUiFile($"{NarrationKillDir}{KillSplash.NarrationVoice}/{narration:00}.ogg");
    }

    private void OnAssist(AssistReply assist)
    {
        if (assist.Count == 0)
        {
            CombatLogAdd(ItemData.Text(KillSplash.AssistResetText, "Assist count has been reset."), CombatLogKind.Status, StreakLineColour);
            return;
        }
        ShowKillSplash(BuildAssistSplash(assist.Count, assist.Grade));
        Audio.PlayUiFile($"{NarrationAssistDir}{KillSplash.NarrationVoice}/{AssistNarrationBase + Math.Clamp((int)assist.Grade, 1, 4):00}.ogg");
    }

    private static string StreakAnnouncement(KillStreakReply kill)
    {
        string nameFormat = ItemData.Text(kill.Nation == Nations.Karus ? KillSplash.KarusNameText : KillSplash.ElmoradNameText, " %s ");
        string name = QuestMarkup.Plain(nameFormat, "").Replace("%s", kill.Name);
        string word = ItemData.Text(KillSplash.AnnouncementWordText(kill.Count, kill.Nation), "");
        string achieved = QuestMarkup.Plain(ItemData.Text(KillSplash.AchievedText, "User %s achieved."), "").Replace("%s", word);
        string streak = QuestMarkup.Plain(ItemData.Text(KillSplash.StreakCountText, "(Continuous %d Kill)"), "").Replace("%d", kill.Count.ToString());
        return $"-{name}{achieved}{streak} -";
    }

    internal Control BuildKillSplash(int count, int nation, string name)
    {
        var root = SplashRoot();
        SplashPiece(root, KillSplash.BackFor(count), KillBackRect);
        SplashPiece(root, KillSplash.WordFor(count, nation), KillWordRect);
        SplashPiece(root, KillSplash.GemFor(count), KillGemRect);
        foreach (var digit in KillSplash.Digits(count))
            SplashPiece(root, $"digit_{digit.Digit}", new Rect2(digit.X, KillDigitY, SplashDigitSize, SplashDigitSize));
        var label = UiTheme.Text(name, 12, Colors.White, HorizontalAlignment.Center);
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        label.Position = KillNameRect.Position;
        label.Size = KillNameRect.Size;
        root.AddChild(label);
        return root;
    }

    internal Control BuildAssistSplash(int count, byte grade)
    {
        var root = SplashRoot();
        SplashPiece(root, "assist_back", AssistBackRect);
        SplashPiece(root, KillSplash.GradeFor(grade), AssistGradeRect);
        SplashPiece(root, "assist_word", AssistWordRect);
        foreach (var digit in KillSplash.AssistDigits(count))
            SplashPiece(root, $"assist_digit_{digit.Digit}", new Rect2(digit.X, AssistDigitY, SplashDigitSize, SplashDigitSize));
        return root;
    }

    private static Control SplashRoot() =>
        new() { CustomMinimumSize = KillSplashSize, Size = KillSplashSize, MouseFilter = Control.MouseFilterEnum.Ignore };

    private static void SplashPiece(Control root, string piece, Rect2 rect)
    {
        var texture = GD.Load<Texture2D>($"{KillSplashDir}{piece}.png");
        if (texture == null) return;
        root.AddChild(new TextureRect
        {
            Texture = texture,
            Position = rect.Position,
            Size = rect.Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    private void ShowKillSplash(Control splash)
    {
        _killSplashTween?.Kill();
        if (_killSplash != null && IsInstanceValid(_killSplash)) _killSplash.QueueFree();
        _killSplash = splash;
        _killSplashLayer.AddChild(splash);
        var viewport = GetViewport().GetVisibleRect().Size;
        splash.Position = new Vector2((viewport.X - KillSplashSize.X) * 0.5f, KillSplash.TopY);
        _killSplashTween = splash.CreateTween();
        _killSplashTween.TweenInterval(KillSplash.HoldSeconds);
        _killSplashTween.TweenProperty(splash, "modulate:a", 0f, KillSplashFade);
        _killSplashTween.TweenCallback(Callable.From(splash.QueueFree));
    }
}
