using LibreKO.Network;

namespace LibreKO.Domain;

public enum PartyBoardKind { All, Players, Parties }

public readonly record struct PartyBoardFilter(PartyBoardKind Kind, bool SameZone, bool MyLevelBand)
{
    public bool Passes(PartyBbsEntry entry, int myZone, int myLevel)
    {
        if (Kind == PartyBoardKind.Players && entry.IsLeaderRecruiting) return false;
        if (Kind == PartyBoardKind.Parties && !entry.IsLeaderRecruiting) return false;
        if (SameZone && entry.ZoneId != myZone) return false;
        if (MyLevelBand && !entry.IsLeaderRecruiting)
        {
            var (min, max) = SeekingParty.Band(myLevel);
            if (entry.Level < min || entry.Level > max) return false;
        }
        return true;
    }
}
