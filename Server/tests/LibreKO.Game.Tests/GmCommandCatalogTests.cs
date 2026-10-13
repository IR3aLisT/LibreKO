using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol;
using LibreKO.Game.Protocol.Writers;
using Xunit;

namespace LibreKO.Game.Tests;

public class GmCommandCatalogTests
{
    [Fact]
    public void EveryCommandIsAPlusCommandWithAGroupAndADescription()
    {
        GmCommandCatalog.All.Should().NotBeEmpty();
        GmCommandCatalog.All.Should().OnlyContain(command =>
            command.Syntax.StartsWith('+') && command.Group.Length > 0 && command.Description.Length > 0);
        GmCommandCatalog.All.Select(command => command.Syntax).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TheReplyCarriesTheOpenFlagAndEveryCommand()
    {
        var packet = AdminPanelPacketWriter.Commands(open: true, GmCommandCatalog.All);
        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_ADMIN_PANEL);
        packet.ResetOffset();

        packet.ReadByte().Should().Be(AdminPanelPacketWriter.CommandsSub);
        packet.ReadByte().Should().Be(AdminPanelPacketWriter.Granted);
        packet.ReadShort().Should().Be((short)GmCommandCatalog.All.Count);
        var first = GmCommandCatalog.All[0];
        packet.ReadString().Should().Be(first.Group);
        packet.ReadString().Should().Be(first.Syntax);
        packet.ReadString().Should().Be(first.Description);
    }
}
