using FluentAssertions;
using LibreKO.Game.Protocol.Writers;
using Xunit;

namespace LibreKO.Game.Tests;

public class DeathNoticeWireTests
{
    [Fact]
    public void TheNoticeCarriesTheKillersNationFirstThenTheKingFlagAndKind()
    {
        var data = ChatPacketWriter.DeathNotice(1, 2, victimIsKing: true, 0, 7, "Rin", 9, "Kai", 100, 200).GetData();

        data.Take(5).Should().Equal(ChatPacketWriter.TypeDeathNotice, 1, 2, 1, 0);
    }
}
