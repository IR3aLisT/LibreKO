using Godot;

namespace LibreKO.Plugins;

public enum HudPart
{
    StatusBars,
    TargetFrame,
    MiniMap,
    Hotbar,
    Chat,
    Launcher,
    ExpBar,
    CombatLog,
    QuestTracker,
    Party,
    WarBoard,
    GenieSub,
}

public readonly record struct PlayerMenuRow(string Key, string Caption, Action Run);

public sealed class PlayerMenuRequest
{
    public string Name { get; }
    public Vector2 Position { get; }
    public IReadOnlyList<PlayerMenuRow> Rows { get; }

    private readonly Action _close;

    internal PlayerMenuRequest(string name, Vector2 position, IReadOnlyList<PlayerMenuRow> rows, Action close)
    {
        Name = name;
        Position = position;
        Rows = rows;
        _close = close;
    }

    public void Close() => _close();
}

public readonly record struct ExitMenuRow(string Key, string Caption, Action Run);

public readonly record struct ExitMenuItem(int ItemId, Texture2D? Icon, string Name, string Place, string TimeLeft, bool Urgent);

public sealed class ExitMenuRequest
{
    public string Notice { get; }
    public IReadOnlyList<ExitMenuItem> TimedItems { get; }
    public IReadOnlyList<ExitMenuRow> Rows { get; }

    private readonly Action _close;

    internal ExitMenuRequest(string notice, IReadOnlyList<ExitMenuItem> timedItems, IReadOnlyList<ExitMenuRow> rows, Action close)
    {
        Notice = notice;
        TimedItems = timedItems;
        Rows = rows;
        _close = close;
    }

    public void Close() => _close();
}

public sealed class WindowHost
{
    public string Id { get; }
    public string Title { get; }
    public Control Window { get; }
    public Control? DragHandle { get; private set; }

    public event Action? Shown;
    public event Action? Hidden;

    private readonly Action _close;

    internal WindowHost(string id, string title, Control window, Action close)
    {
        Id = id;
        Title = title;
        Window = window;
        _close = close;
    }

    public void SetDragHandle(Control handle) => DragHandle = handle;

    public void Close() => _close();

    internal void RaiseShown() => Shown?.Invoke();

    internal void RaiseHidden() => Hidden?.Invoke();
}

public sealed class DialogRequest
{
    public string Title { get; }
    public string Message { get; private set; }
    public string ConfirmText { get; }
    public string? CancelText { get; }
    public bool Dismissable { get; }
    public bool HasCancel => CancelText != null;

    public event Action<string>? MessageChanged;

    private readonly Action _confirm;
    private readonly Action _cancel;
    private readonly Action _dismiss;

    internal DialogRequest(string title, string message, string confirmText, string? cancelText, bool dismissable,
        Action confirm, Action cancel, Action dismiss)
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        Dismissable = dismissable;
        _confirm = confirm;
        _cancel = cancel;
        _dismiss = dismiss;
    }

    public void Confirm() => _confirm();

    public void Cancel() => _cancel();

    public void Dismiss() => _dismiss();

    internal void SetMessage(string message)
    {
        Message = message;
        MessageChanged?.Invoke(message);
    }
}

public sealed class TextRequest
{
    public string Title { get; }
    public string Message { get; }
    public string Initial { get; }
    public string Placeholder { get; }
    public int MaxLength { get; }
    public bool Secret { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }

    private readonly Action<string> _submit;
    private readonly Action _cancel;

    internal TextRequest(string title, string message, string initial, string placeholder, int maxLength, bool secret,
        string confirmText, string cancelText, Action<string> submit, Action cancel)
    {
        Title = title;
        Message = message;
        Initial = initial;
        Placeholder = placeholder;
        MaxLength = maxLength;
        Secret = secret;
        ConfirmText = confirmText;
        CancelText = cancelText;
        _submit = submit;
        _cancel = cancel;
    }

    public void Submit(string text) => _submit(text);

    public void Cancel() => _cancel();
}

public sealed class WindowFrame
{
    public string Id { get; }
    public string Title { get; private set; }
    public Control Content { get; }
    public bool Closable { get; }
    public Control? DragHandle { get; private set; }

    public event Action<string>? TitleChanged;
    public event Action? Shown;
    public event Action? Hidden;

    private readonly Action _close;

    internal WindowFrame(string id, string title, Control content, bool closable, Action close)
    {
        Id = id;
        Title = title;
        Content = content;
        Closable = closable;
        _close = close;
    }

    public void SetDragHandle(Control handle) => DragHandle = handle;

    public void Close() => _close();

    internal void SetTitle(string title)
    {
        if (title == Title) return;
        Title = title;
        TitleChanged?.Invoke(title);
    }

    internal void RaiseShown() => Shown?.Invoke();

    internal void RaiseHidden() => Hidden?.Invoke();
}

internal sealed class Owned<T>
{
    private readonly List<(string Owner, T Value)> _entries = new();

    public bool Has => _entries.Count > 0;

    public T? Current => _entries.Count > 0 ? _entries[^1].Value : default;

    public IEnumerable<T> All => _entries.Select(e => e.Value);

    public void Set(string owner, T value)
    {
        _entries.RemoveAll(e => e.Owner == owner);
        _entries.Add((owner, value));
    }

    public void Add(string owner, T value) => _entries.Add((owner, value));

    public bool Remove(string owner) => _entries.RemoveAll(e => e.Owner == owner) > 0;

    public void Clear() => _entries.Clear();
}

internal sealed class WindowRule
{
    public readonly Owned<bool> HiddenBy = new();
    public readonly Owned<Func<WindowHost, Control>> Replacements = new();
    public readonly Owned<Func<WindowFrame, Control>> Frames = new();
    public readonly Owned<Action<Control>> ExtendedBy = new();

    public bool Hidden => HiddenBy.Has;
    public Func<WindowHost, Control>? Replacement => Replacements.Current;
    public Func<WindowFrame, Control>? Frame => Frames.Current;
    public IEnumerable<Action<Control>> Extenders => ExtendedBy.All;
    public bool Empty => !HiddenBy.Has && !Replacements.Has && !Frames.Has && !ExtendedBy.Has;

    public bool Remove(string owner)
    {
        bool removed = HiddenBy.Remove(owner);
        removed |= Replacements.Remove(owner);
        removed |= Frames.Remove(owner);
        removed |= ExtendedBy.Remove(owner);
        return removed;
    }
}

public sealed class PluginUi
{
    public static readonly string[] KnownWindowIds =
    {
        "achievements", "admin_panel", "anvil", "attendance", "bounty", "cape", "changehair", "character_info",
        "chat_room", "clan", "clanwarehouse", "class_change", "collectionrace", "combinerecipes", "disguise",
        "equipview", "exchange", "fishinghall", "forces", "fortune", "genie", "globalmap",
        "inventory", "itemcombine", "kingballot", "kingelection", "kingnominate", "kingplan", "kingvote", "lottery",
        "mail", "mailcompose", "mailread", "marketprice", "merchantmenu", "messenger", "namechange", "nation_chat",
        "nation_chat_create", "nation_chat_password", "nationintro",
        "nationtax", "nationtaxrate", "npc_dialog", "party", "pet", "piecechange", "presets", "quest_available",
        "quest_receipt", "quest_target", "quests", "rank", "rebirth", "repair", "report", "repurchase", "roulette", "seal",
        "seek_party", "sellstall", "shop", "shoppingmall", "siegechallengers", "siegedefenders", "siegeguard",
        "siegeoffice", "siegeschedule", "siegetaxlist", "siegetaxrate", "skills", "specialauction", "titles",
        "tournament", "user_vote", "user_vote_ask", "user_vote_notice", "userinfo", "vendor", "vipwarehouse", "wantedstall", "warehouse", "warp", "whisper_*",
        "wishfind", "wishlist",
    };

    public const string EveryWindow = "*";

    private sealed class Registry
    {
        public readonly Dictionary<string, WindowRule> Windows = new(StringComparer.OrdinalIgnoreCase);
        public readonly Owned<Func<WindowFrame, Control>> EveryFrame = new();
        public readonly Dictionary<HudPart, Owned<Func<Control>?>> Hud = new();
        public readonly Owned<Func<Control>> ExtraHud = new();
        public readonly Owned<Func<DialogRequest, Control>> Dialogs = new();
        public readonly Owned<Func<PlayerMenuRequest, Control>> PlayerMenus = new();
        public readonly Owned<Func<ExitMenuRequest, Control>> ExitMenus = new();
        public readonly Owned<Func<TextRequest, Control>> TextInputs = new();
    }

    private readonly Registry _store;
    private readonly string _owner;

    public PluginUi() : this(new Registry(), "")
    {
    }

    private PluginUi(Registry store, string owner)
    {
        _store = store;
        _owner = owner;
    }

    internal PluginUi ForOwner(string owner) => new(_store, owner);

    internal Func<DialogRequest, Control>? DialogBuilder => _store.Dialogs.Current;

    internal Func<PlayerMenuRequest, Control>? PlayerMenuBuilder => _store.PlayerMenus.Current;

    internal Func<ExitMenuRequest, Control>? ExitMenuBuilder => _store.ExitMenus.Current;

    internal Func<TextRequest, Control>? TextInputBuilder => _store.TextInputs.Current;

    internal IReadOnlyList<Func<Control>> ExtraHud => _store.ExtraHud.All.ToList();

    public void AddHud(Func<Control> build) => _store.ExtraHud.Add(_owner, build);

    public IReadOnlyList<string> WindowIds => KnownWindowIds;

    public void HideWindow(string id) => Rule(id).HiddenBy.Set(_owner, true);

    public void ReplaceWindow(string id, Func<WindowHost, Control> build) => Rule(id).Replacements.Set(_owner, build);

    public void ExtendWindow(string id, Action<Control> extend) => Rule(id).ExtendedBy.Add(_owner, extend);

    public void FrameWindow(string id, Func<WindowFrame, Control> build)
    {
        if (id == EveryWindow) _store.EveryFrame.Set(_owner, build);
        else Rule(id).Frames.Set(_owner, build);
    }

    public void HideHud(HudPart part) => HudEntry(part).Set(_owner, null);

    public void ReplaceHud(HudPart part, Func<Control> build) => HudEntry(part).Set(_owner, build);

    public void ReplaceDialogs(Func<DialogRequest, Control> build) => _store.Dialogs.Set(_owner, build);

    public void ReplacePlayerMenu(Func<PlayerMenuRequest, Control> build) => _store.PlayerMenus.Set(_owner, build);

    public void ReplaceExitMenu(Func<ExitMenuRequest, Control> build) => _store.ExitMenus.Set(_owner, build);

    public void ReplaceTextInput(Func<TextRequest, Control> build) => _store.TextInputs.Set(_owner, build);

    public bool IsWindowOverridden(string id) =>
        RuleFor(id) is { } r && (r.Hidden || r.Replacement != null);

    internal WindowRule? RuleFor(string id)
    {
        if (_store.Windows.TryGetValue(id, out var r)) return r;
        int cut = id.IndexOf(FamilySeparator);
        return cut > 0 && _store.Windows.TryGetValue(id[..(cut + 1)] + FamilyWildcard, out var family) ? family : null;
    }

    internal Func<WindowFrame, Control>? FrameFor(string id) => RuleFor(id)?.Frame ?? _store.EveryFrame.Current;

    private const char FamilySeparator = '_';
    private const string FamilyWildcard = "*";

    internal bool HudHidden(HudPart part) => _store.Hud.TryGetValue(part, out var h) && h.Has;

    internal Func<Control>? HudReplacement(HudPart part) => _store.Hud.TryGetValue(part, out var h) ? h.Current : null;

    internal bool RemoveOwner(string owner)
    {
        bool removed = false;
        foreach (var (id, rule) in _store.Windows.ToList())
        {
            removed |= rule.Remove(owner);
            if (rule.Empty) _store.Windows.Remove(id);
        }
        foreach (var entry in _store.Hud.Values) removed |= entry.Remove(owner);
        removed |= _store.EveryFrame.Remove(owner);
        removed |= _store.ExtraHud.Remove(owner);
        removed |= _store.Dialogs.Remove(owner);
        removed |= _store.PlayerMenus.Remove(owner);
        removed |= _store.ExitMenus.Remove(owner);
        removed |= _store.TextInputs.Remove(owner);
        return removed;
    }

    internal void Reset()
    {
        _store.Windows.Clear();
        _store.Hud.Clear();
        _store.EveryFrame.Clear();
        _store.ExtraHud.Clear();
        _store.Dialogs.Clear();
        _store.PlayerMenus.Clear();
        _store.ExitMenus.Clear();
        _store.TextInputs.Clear();
    }

    private WindowRule Rule(string id)
    {
        if (!_store.Windows.TryGetValue(id, out var r))
            _store.Windows[id] = r = new WindowRule();
        return r;
    }

    private Owned<Func<Control>?> HudEntry(HudPart part)
    {
        if (!_store.Hud.TryGetValue(part, out var entry))
            _store.Hud[part] = entry = new Owned<Func<Control>?>();
        return entry;
    }
}
