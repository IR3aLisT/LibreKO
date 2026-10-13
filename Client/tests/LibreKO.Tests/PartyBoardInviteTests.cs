using LibreKO.Network;
using Xunit;

namespace LibreKO.Tests;

public class PartyBoardInviteTests
{
    [Fact]
    public void TheServersInvitePromptIsRead()
    {
        var p = new Packet(GameOpcodes.GS_PARTY_BBS);
        p.WriteByte(1);
        p.WriteString("Velkan");
        p.WriteByte(62);
        p.WriteShort(105);
        p.ResetOffset();

        var invite = PartyBoardInvite.Read(p);

        Assert.Equal(new PartyBoardInvite("Velkan", 62, 105), invite);
        Assert.Equal(0, p.RemainingBytes);
    }

    [Fact]
    public void AFailedResultCarriesNoPrompt()
    {
        var p = new Packet(GameOpcodes.GS_PARTY_BBS);
        p.WriteByte(0);
        p.ResetOffset();

        Assert.Null(PartyBoardInvite.Read(p));
    }

    [Fact]
    public void ThePromptNamesTheInviterThenTheJob()
    {
        var invite = new PartyBoardInvite("Velkan", 62, 105);

        Assert.Equal("ID: Velkan,  Job: Berserker has invited you into a party.\nWill you accept?",
            invite.Prompt(PartyBoardInvite.PromptFallback, "Berserker"));
    }
}
