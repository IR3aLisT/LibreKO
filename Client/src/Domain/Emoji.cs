using System.Collections.Generic;

namespace LibreKO.Domain;

public static class Emoji
{
    private const string Presentation = "️";

    public static readonly IReadOnlyList<string> Picker = new[]
    {
        "😀", "😃", "😄", "😁", "😆", "😅", "😂", "🤣", "😊", "😇", "🙂", "😉",
        "😍", "😘", "😋", "😛", "😜", "🤪", "😎", "🤓", "🤔", "🤨", "😐", "🙄",
        "😏", "😬", "😴", "😌", "🤤", "😷", "🤢", "🤮", "🥵", "🥶", "😵", "🤯",
        "🥳", "😕", "🙁", "😮", "😲", "😳", "🥺", "😢", "😭", "😱", "😩", "😤",
        "😡", "🤬", "😈", "💀", "👻", "👽", "🤖", "💩", "🙈", "🙉", "🙊", "👀",
        "👍", "👎", "👌", "✌" + Presentation, "🤞", "🤘", "👊", "✊", "👏", "🙌", "🙏", "💪",
        "👋", "🤝", "❤" + Presentation, "💔", "💯", "💢", "💥", "💤", "✨", "🔥", "⭐", "🎉",
        "🎁", "🏆", "👑", "💰", "💎", "⚔" + Presentation, "🛡" + Presentation, "🏹", "🗡" + Presentation, "🪓", "🔮", "🧪",
        "📜", "🐉", "🐺", "💣", "⚡", "☠" + Presentation, "🍺", "🍖", "🌙", "☀" + Presentation, "❄" + Presentation, "🌹",
    };
}
