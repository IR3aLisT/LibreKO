using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public static class Gestures
{
    public const int Greeting1 = 1;
    public const int Greeting2 = 2;
    public const int Greeting3 = 3;
    public const int Provoke1 = 11;
    public const int Provoke2 = 12;
    public const int Provoke3 = 13;
    public const int Stomp = 14;
    public const int Kick = 15;

    public const int StompSkill = 490049;
    public const int KickSkill = 490065;
    public const int KarusFlagSkill = 490066;
    public const int ElMoradFlagSkill = 490067;

    public const int CorpseJiggleClip = 142;
    public const int TauntChoices = 2;
    public const int CorpseSkillChoices = 3;

    private const int StompRoll = 0;
    private const int KickRoll = 1;

    private static readonly Dictionary<int, int> Clips = new()
    {
        [Greeting1] = 104,
        [Greeting2] = 105,
        [Greeting3] = 106,
        [Provoke1] = 108,
        [Provoke2] = 107,
        [Provoke3] = 109,
        [Stomp] = 140,
        [Kick] = 143,
    };

    private static readonly (int Text, string Fallback, int Action)[] Commands =
    {
        (8003, "Greeting", Greeting1),
        (8004, "Greeting2", Greeting2),
        (8005, "Greeting3", Greeting3),
        (8006, "Provoke", Provoke1),
        (8007, "Provoke2", Provoke2),
        (8008, "Provoke3", Provoke3),
    };

    public static int ClipFor(int action) => Clips.TryGetValue(action, out int clip) ? clip : -1;

    public static int TauntFor(int roll) => roll % TauntChoices == KickRoll ? Kick : Stomp;

    public static int CorpseSkillFor(int roll, int nation) => (roll % CorpseSkillChoices) switch
    {
        StompRoll => StompSkill,
        KickRoll => KickSkill,
        _ => nation == Nations.Karus ? KarusFlagSkill : ElMoradFlagSkill,
    };

    public static bool IsCorpseSkill(int skillId) =>
        skillId is StompSkill or KickSkill or KarusFlagSkill or ElMoradFlagSkill;

    public static int? CommandAction(string word, Func<int, string, string> text)
    {
        foreach (var (id, fallback, action) in Commands)
            if (string.Equals(word, text(id, fallback), StringComparison.OrdinalIgnoreCase))
                return action;
        return null;
    }
}
