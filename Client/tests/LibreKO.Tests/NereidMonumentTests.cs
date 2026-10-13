using LibreKO.Domain;
using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class NereidMonumentTests
{
    private static Packet Bytes(params object[] parts)
    {
        var p = new Packet(GameOpcodes.GS_MAP_EVENT);
        foreach (var part in parts)
        {
            switch (part)
            {
                case byte b: p.WriteByte(b); break;
                case ushort u: p.WriteUShort(u); break;
                case uint u: p.WriteUInt(u); break;
                case int i: p.WriteInt(i); break;
            }
        }
        p.ResetOffset();
        return p;
    }

    [Fact]
    public void TheOwnerListIsReadInFlagOrder()
    {
        var reply = MapEventReply.Read(Bytes((byte)0, (byte)7, (byte)1, (byte)2, (byte)0, (byte)0, (byte)2, (byte)1, (byte)0))!;

        Assert.Equal(MapEventKind.Owners, reply.Kind);
        Assert.Equal(new byte[] { 1, 2, 0, 0, 2, 1, 0 }, reply.Owners);
    }

    [Fact]
    public void ACaptureCarriesTheFlagAndTheNewOwner()
    {
        var reply = MapEventReply.Read(Bytes((byte)1, (byte)1, (byte)3, (byte)2))!;

        Assert.Equal(MapEventKind.Captured, reply.Kind);
        Assert.Equal(3, reply.Index);
        Assert.Equal(2, reply.Nation);
        Assert.Null(MapEventReply.Read(Bytes((byte)1, (byte)2, (byte)3, (byte)2)));
    }

    [Fact]
    public void PointsAreKarusFirst()
    {
        var reply = MapEventReply.Read(Bytes((byte)2, (ushort)14, (ushort)6))!;

        Assert.Equal(MapEventKind.Points, reply.Kind);
        Assert.Equal(14, reply.A);
        Assert.Equal(6, reply.B);
    }

    [Fact]
    public void TheOtherSubsAreRead()
    {
        var provisions = MapEventReply.Read(Bytes((byte)3, (byte)1, (ushort)10))!;
        Assert.Equal((MapEventKind.Provisions, 1, 10L), (provisions.Kind, provisions.Nation, provisions.A));
        Assert.Equal(500L, MapEventReply.Read(Bytes((byte)4, (uint)500))!.A);
        Assert.Equal(2, MapEventReply.Read(Bytes((byte)5, (byte)2))!.Nation);
        Assert.Equal(400L, MapEventReply.Read(Bytes((byte)6, 400))!.A);
    }

    [Fact]
    public void HeldWeightsAndCaptureTextsFollowTheOwners()
    {
        byte[] owners = { 1, 2, 0, 1, 1, 2, 0 };

        Assert.Equal(6, NereidMonuments.Held(owners, Nations.Karus));
        Assert.Equal(4, NereidMonuments.Held(owners, Nations.ElMorad));
        Assert.Equal(NereidMonuments.OccupiedText, NereidMonuments.CaptureText(Nations.Karus, Nations.Karus));
        Assert.Equal(NereidMonuments.LostText, NereidMonuments.CaptureText(Nations.ElMorad, Nations.Karus));
        Assert.Equal(13010, NereidMonuments.Name(3).Id);
    }
}
