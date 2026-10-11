using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class PlayerLookRaceTests
{
    [Theory]
    [InlineData(PlayerRig.PuriTuarek, 103, PlayerRig.PuriTuarekMagician)]
    [InlineData(PlayerRig.PuriTuarek, 109, PlayerRig.PuriTuarekMagician)]
    [InlineData(PlayerRig.PuriTuarek, 110, PlayerRig.PuriTuarekMagician)]
    [InlineData(PlayerRig.PuriTuarek, 104, PlayerRig.PuriTuarek)]
    [InlineData(PlayerRig.PuriTuarek, 111, PlayerRig.PuriTuarek)]
    [InlineData(3, 103, 3)]
    [InlineData(13, 203, 13)]
    public void PuriTuarekMagesWearTheMagicianLook(int race, int classCode, int expected)
    {
        Assert.Equal(expected, PlayerRig.LookRace(race, classCode));
    }
}
