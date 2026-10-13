using Godot;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class NpcBuiltInPlugsTests
{
    [Fact]
    public void ASidecarListsEachPlugWithItsSlotJointAndPose()
    {
        var plugs = NpcBuiltInPlugs.Parse("""
            {"plugs":[
              {"slot":0,"model":"mob_lost_weapon_2016.n3pmesh","joint":25,"pos":[-0.5,1,2],"quat":[0,0,0,1],"scale":[1,1,1],"anim":""},
              {"slot":2,"model":"11_1042_40_0.n3chr","joint":12,"pos":[0,0,0],"quat":[0,1,0,0],"scale":[2,2,2],"anim":"breath"}
            ]}
            """);

        Assert.Equal(2, plugs.Count);
        Assert.Equal(new BuiltInPlug(0, 25, "mob_lost_weapon_2016.n3pmesh", new Vector3(-0.5f, 1f, 2f),
            Quaternion.Identity, Vector3.One, ""), plugs[0]);
        Assert.Equal(2, plugs[1].Slot);
        Assert.Equal(new Quaternion(0f, 1f, 0f, 0f), plugs[1].Rotation);
        Assert.Equal(new Vector3(2f, 2f, 2f), plugs[1].Scale);
        Assert.Equal("breath", plugs[1].Anim);
    }

    [Fact]
    public void AMalformedSidecarYieldsNoPlugs()
    {
        Assert.Empty(NpcBuiltInPlugs.Parse("{\"plugs\":[{\"slot\":0}]}"));
        Assert.Empty(NpcBuiltInPlugs.Parse("{}"));
    }

    [Fact]
    public void AHandWeaponTakesTheBuiltInPlugOfItsSlot()
    {
        Assert.Equal(0, NpcPlugSlots.PlugIndex(rightHand: true));
        Assert.Equal(1, NpcPlugSlots.PlugIndex(rightHand: false));
    }
}
