using System;

namespace LibreKO.Domain;

public enum NpcChoiceKind { Talk, Shop, Storage, Repair, Travel, Quest, Upgrade, Exchange, Group, Reward, Close }

public static class NpcChoice
{
    private const char PluralEnding = 's';
    private const char NumberDot = '.';
    public const string ReadyPrefix = "[Ready] ";

    private static readonly (NpcChoiceKind Kind, string[] Words)[] Keywords =
    {
        (NpcChoiceKind.Close, new[] { "close", "exit", "goodbye", "good bye", "cancel", "leave", "bye", "nothing", "no thanks" }),
        (NpcChoiceKind.Shop, new[] { "shop", "store", "buy", "sell", "merchant", "purchase" }),
        (NpcChoiceKind.Storage, new[] { "warehouse", "storage", "inn", "vault", "deposit" }),
        (NpcChoiceKind.Repair, new[] { "repair", "fix" }),
        (NpcChoiceKind.Travel, new[] { "teleport", "warp", "move to", "travel", "gate", "enter", "go to", "transport" }),
        (NpcChoiceKind.Upgrade, new[] { "upgrade", "enchant", "refine", "craft", "combine", "smash", "disassemble" }),
        (NpcChoiceKind.Exchange, new[] { "exchange", "trade", "swap", "change" }),
        (NpcChoiceKind.Reward, new[] { "reward", "receive", "claim", "gift", "prize" }),
        (NpcChoiceKind.Group, new[] { "clan", "party", "alliance", "knights" }),
        (NpcChoiceKind.Quest, new[] { "quest", "mission", "task", "hunt" }),
    };

    public static string Caption(string label)
    {
        int i = 0;
        while (i < label.Length && char.IsDigit(label[i])) i++;
        if (i == 0 || i >= label.Length || label[i] != NumberDot) return label;
        return label[(i + 1)..].TrimStart();
    }

    public static (string Caption, bool Ready) SplitReady(string caption) =>
        caption.StartsWith(ReadyPrefix, StringComparison.Ordinal)
            ? (caption[ReadyPrefix.Length..], true)
            : (caption, false);

    public static NpcChoiceKind Classify(string caption)
    {
        string text = caption.Trim().ToLowerInvariant();
        foreach (var (kind, words) in Keywords)
            foreach (var word in words)
                if (ContainsWord(text, word)) return kind;
        return NpcChoiceKind.Talk;
    }

    private static bool ContainsWord(string text, string word)
    {
        for (int at = text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.Ordinal))
        {
            bool startsWord = at == 0 || !char.IsLetter(text[at - 1]);
            int end = at + word.Length;
            bool endsWord = end >= text.Length || !char.IsLetter(text[end])
                || text[end] == PluralEnding && (end + 1 >= text.Length || !char.IsLetter(text[end + 1]));
            if (startsWord && endsWord) return true;
        }
        return false;
    }

    public static string Icon(NpcChoiceKind kind) => kind switch
    {
        NpcChoiceKind.Shop => "system/storefront",
        NpcChoiceKind.Storage => "system/package",
        NpcChoiceKind.Repair => "system/hammer",
        NpcChoiceKind.Travel => "system/door",
        NpcChoiceKind.Quest => "system/scroll",
        NpcChoiceKind.Upgrade => "system/sparkle",
        NpcChoiceKind.Exchange => "system/coins",
        NpcChoiceKind.Group => "system/users-three",
        NpcChoiceKind.Reward => "system/gift",
        NpcChoiceKind.Close => "system/close",
        _ => "system/chat-circle",
    };
}
