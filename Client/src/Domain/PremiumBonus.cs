using System.Collections.Generic;
using System.Text;

namespace LibreKO.Domain;

public enum PremiumBonusKind : byte
{
    Experience = 1,
    Coins = 2,
    Drops = 3,
    RepairDiscount = 4,
    SellPrice = 5,
    DeathExpLoss = 6,
    FreeReset = 7,
    FreeRebirthReset = 8,
    PriceHistory = 9,
}

public readonly record struct PremiumBonus(PremiumBonusKind Kind, int Value)
{
    public string? Text => Kind switch
    {
        PremiumBonusKind.Experience => $"EXP +{Value}%",
        PremiumBonusKind.Coins => $"Coins +{Value}%",
        PremiumBonusKind.Drops => $"Drop chance +{Value}%",
        PremiumBonusKind.RepairDiscount => $"Repairs {Value}% cheaper",
        PremiumBonusKind.SellPrice => $"Items sell for {Value}% more",
        PremiumBonusKind.DeathExpLoss => $"EXP lost on death cut to {Value}%",
        PremiumBonusKind.FreeReset => "Free stat and skill resets",
        PremiumBonusKind.FreeRebirthReset => "Free rebirth stat resets",
        PremiumBonusKind.PriceHistory => "Item price history",
        _ => null,
    };

    public static string Tooltip(string name, string timeLeft, IReadOnlyList<PremiumBonus> bonuses)
    {
        var text = new StringBuilder($"{name} — {timeLeft} left");
        foreach (var bonus in bonuses)
            if (bonus.Text is { } line) text.Append('\n').Append(line);
        return text.ToString();
    }
}
