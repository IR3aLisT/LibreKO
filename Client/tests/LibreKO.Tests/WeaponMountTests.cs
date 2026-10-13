using Godot;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class WeaponMountTests
{
    private const float WrinkleTuarekGauntletScale = 0.643f;
    private static readonly Vector3 WrinkleTuarekGauntletPosition = new(0.127f, -0.365f, 0.029f);
    private const int Precision = 4;

    private const float WrinkleTuarekHandScale = 0.791f;
    private const float WrinkleTuarekBindHeight = WrinkleTuarekHandScale * WeaponMount.HandReferenceHeight;
    private const float ArkTuarekHandScale = 1.194f;
    private const float ArkTuarekBindHeight = ArkTuarekHandScale * WeaponMount.HandReferenceHeight;
    private static readonly PlugPose ChitinShield =
        new(new Vector3(-0.087f, -0.373f, 0.0196f), new Quaternion(-0.0234f, -0.4501f, 0.0012f, 0.8927f).Normalized(), Vector3.One);
    private static readonly PlugPose StretchedPlug =
        new(new Vector3(0.01f, 0.4f, 0f), Quaternion.Identity, new Vector3(2.3f, 1.957f, 2.767f));
    private const int DrakiElRasaga = 9839;
    private const float NpcSizeScale = 1.5f;
    private const int RaceBlockCount = 8;
    private const int V1 = 1;

    private static PlugPose[] Blocks()
    {
        var blocks = new PlugPose[RaceBlockCount];
        for (int i = 0; i < blocks.Length; i++)
            blocks[i] = new PlugPose(new Vector3(0.01f * i, -0.02f * i, 0f), Quaternion.Identity,
                                     new Vector3(1f - 0.01f * i, 1f - 0.01f * i, 1f - 0.01f * i));
        return blocks;
    }

    [Fact]
    public void ThePlugOffsetIsScaledWithThePlug()
    {
        var scale = new Vector3(WrinkleTuarekGauntletScale, WrinkleTuarekGauntletScale, WrinkleTuarekGauntletScale);
        var local = WeaponMount.PlugLocal(WrinkleTuarekGauntletPosition, Quaternion.Identity, scale);
        Assert.Equal(WrinkleTuarekGauntletPosition.Y * WrinkleTuarekGauntletScale, local.Origin.Y, Precision);
        Assert.Equal(WrinkleTuarekGauntletPosition.X * WrinkleTuarekGauntletScale, local.Origin.X, Precision);
    }

    [Fact]
    public void ANonUniformScaleStretchesThePlugAlongItsOwnAxes()
    {
        const float stretch = 2f;
        var quarterTurn = new Quaternion(Vector3.Up, Mathf.Pi / 2f);
        var local = WeaponMount.PlugLocal(Vector3.Zero, quarterTurn, new Vector3(stretch, 1f, 1f));
        var plugX = local.Basis * Vector3.Right;
        Assert.Equal(stretch, plugX.Length(), Precision);
        Assert.Equal(-stretch, plugX.Z, Precision);
    }

    [Fact]
    public void APlayersHandScaleIsItsBindHeightOverTheReferenceHeight()
    {
        Assert.Equal(WrinkleTuarekHandScale, WeaponMount.HandScale(new HandWearer(PlayerRig.WrinkleTuarek), WrinkleTuarekBindHeight), Precision);
        Assert.Equal(ArkTuarekHandScale, WeaponMount.HandScale(new HandWearer(PlayerRig.ArkTuarek), ArkTuarekBindHeight), Precision);
    }

    [Fact]
    public void AnUnscaledNpcKeepsThePlugScale()
    {
        var npc = new HandWearer(WeaponMount.NoRace, Npc: true);
        Assert.Equal(1f, WeaponMount.HandScale(npc, ArkTuarekBindHeight), Precision);
    }

    [Fact]
    public void AScaledNpcUsesTheHeightRule()
    {
        var npc = new HandWearer(WeaponMount.NoRace, Npc: true, NpcScale: NpcSizeScale);
        Assert.Equal(ArkTuarekHandScale, WeaponMount.HandScale(npc, ArkTuarekBindHeight), Precision);
    }

    [Fact]
    public void TheGiantReferenceNpcsDivideByTheGiantHeight()
    {
        var npc = new HandWearer(WeaponMount.NoRace, Npc: true, NpcId: DrakiElRasaga);
        Assert.Equal(ArkTuarekBindHeight / WeaponMount.GiantHandReferenceHeight,
                     WeaponMount.HandScale(npc, ArkTuarekBindHeight), Precision);
    }

    [Theory]
    [InlineData(PlayerRig.ArkTuarek, 1)]
    [InlineData(PlayerRig.Tuarek, 2)]
    [InlineData(PlayerRig.WrinkleTuarek, 3)]
    [InlineData(PlayerRig.PuriTuarek, 4)]
    [InlineData(PlayerRig.PuriTuarekMagician, 4)]
    [InlineData(PlayerRig.ElMoradWoman, 4)]
    [InlineData(PlayerRig.Barbarian, 5)]
    [InlineData(PlayerRig.ElMoradMan, 6)]
    [InlineData(PlayerRig.KarusKurian, 7)]
    [InlineData(PlayerRig.ElMoradKurian, 7)]
    [InlineData(WeaponMount.NoRace, 0)]
    public void EachRaceReadsItsBlock(int race, int block) => Assert.Equal(block, WeaponMount.RaceBlock(race));

    [Fact]
    public void AnOldPlugOnAShortRaceShrinksUniformlyByTheHandScaleWithItsOffset()
    {
        var wearer = new HandWearer(PlayerRig.WrinkleTuarek);
        var pose = WeaponMount.HandPose(ChitinShield, 0, System.Array.Empty<PlugPose>(), right: false, wearer,
                                        WrinkleTuarekBindHeight);
        Assert.Equal(new Vector3(WrinkleTuarekHandScale, WrinkleTuarekHandScale, WrinkleTuarekHandScale), pose.Scale);
        var local = WeaponMount.PlugLocal(pose);
        Assert.Equal(ChitinShield.Position.Length() * WrinkleTuarekHandScale, local.Origin.Length(), Precision);
    }

    [Fact]
    public void AnOldPlugTakesItsYScaleOnEveryAxis()
    {
        var pose = WeaponMount.HandPose(StretchedPlug, 0, System.Array.Empty<PlugPose>(), right: true,
                                        new HandWearer(PlayerRig.ArkTuarek), ArkTuarekBindHeight);
        float uniform = StretchedPlug.Scale.Y * ArkTuarekHandScale;
        Assert.Equal(uniform, pose.Scale.X, Precision);
        Assert.Equal(uniform, pose.Scale.Z, Precision);
    }

    [Fact]
    public void ARaceBlockPlugInTheLeftHandUsesTheWearersBlockUnscaledByHeight()
    {
        var blocks = Blocks();
        var pose = WeaponMount.HandPose(ChitinShield, WeaponMount.RaceBlockVersion, blocks, right: false,
                                        new HandWearer(PlayerRig.WrinkleTuarek), WrinkleTuarekBindHeight);
        Assert.Equal(blocks[WeaponMount.RaceBlock(PlayerRig.WrinkleTuarek)], pose);
    }

    [Fact]
    public void ARaceBlockPlugInTheRightHandUsesTheDefaultBlock()
    {
        var blocks = Blocks();
        var pose = WeaponMount.HandPose(ChitinShield, WeaponMount.RaceBlockVersion, blocks, right: true,
                                        new HandWearer(PlayerRig.WrinkleTuarek), WrinkleTuarekBindHeight);
        Assert.Equal(blocks[0], pose);
    }

    [Fact]
    public void ATransformedLookUsesTheDefaultBlockInEitherHand()
    {
        var blocks = Blocks();
        var pose = WeaponMount.HandPose(ChitinShield, WeaponMount.RaceBlockVersion, blocks, right: false,
                                        HandWearer.TransformedLook, ArkTuarekBindHeight);
        Assert.Equal(blocks[0], pose);
    }

    [Fact]
    public void AVersion1PlugIgnoresBlocksAndUsesTheHeightRule()
    {
        var pose = WeaponMount.HandPose(ChitinShield, V1, Blocks(), right: false,
                                        new HandWearer(PlayerRig.ArkTuarek), ArkTuarekBindHeight);
        Assert.Equal(ArkTuarekHandScale, pose.Scale.Y, Precision);
        Assert.Equal(ChitinShield.Position, pose.Position);
    }

    [Fact]
    public void ARaceBlockVersionWithoutBlocksFallsBackToTheHeightRule()
    {
        var pose = WeaponMount.HandPose(ChitinShield, WeaponMount.RaceBlockVersion, System.Array.Empty<PlugPose>(),
                                        right: false, new HandWearer(PlayerRig.ArkTuarek), ArkTuarekBindHeight);
        Assert.Equal(ArkTuarekHandScale, pose.Scale.Y, Precision);
    }
}
