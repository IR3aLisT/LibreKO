namespace LibreKO.Common.Enums;

public enum CreateCharacterResult : byte
{
    Success = 0,
    SlotFull = 1,
    NationRaceMismatch = 2,
    NameAlreadyExists = 3,
    ServerError = 4,
    InvalidName = 5,
    NameMissing = 6,
    InvalidRace = 7,
    UnsupportedRace = 8,
    InvalidClass = 9,
    PointsRemaining = 10,
    InvalidStats = 11,
    InvalidCharacters = 12,
}
