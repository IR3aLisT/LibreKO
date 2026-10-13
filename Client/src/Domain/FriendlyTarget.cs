namespace LibreKO.Domain;

public enum FriendlyPick { Self, Selected, PartyPick, Nothing }

public static class FriendlyTarget
{
    public static FriendlyPick Choose(int moral, bool hasSelection, bool selectionValid, bool selectionInParty, bool hasPartyPick) =>
        moral switch
        {
            SkillTarget.Party when !hasSelection => hasPartyPick ? FriendlyPick.PartyPick : FriendlyPick.Self,
            SkillTarget.Party when selectionValid && selectionInParty => FriendlyPick.Selected,
            SkillTarget.Party => hasPartyPick ? FriendlyPick.PartyPick : FriendlyPick.Nothing,
            SkillTarget.FriendOnly => hasSelection && selectionValid ? FriendlyPick.Selected : FriendlyPick.Nothing,
            _ => hasSelection && selectionValid ? FriendlyPick.Selected : FriendlyPick.Self,
        };
}
