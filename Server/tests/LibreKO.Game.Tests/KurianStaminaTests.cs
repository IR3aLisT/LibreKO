using FluentAssertions;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.Protocol.Writers;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class KurianStaminaTests : GameTestBase
{
    private const short KarusKurianNovice = 114;
    private const short KarusWarrior = 105;
    private const short AliveHp = 100;

    [Theory]
    [InlineData(59, 150)]
    [InlineData(60, 250)]
    public void TheBarFillsToTheLevelsMaximum(byte level, short expected)
    {
        var session = Session(KarusKurianNovice, level);

        KurianStamina.Refill(session);

        session.KurianMaxSp.Should().Be(expected);
        session.KurianSp.Should().Be(expected);
    }

    [Fact]
    public void ASkillCostingMoreThanTheBarIsRefusedAndOnlyKuriansPay()
    {
        var kurian = Session(KarusKurianNovice, 70);
        KurianStamina.Refill(kurian);
        kurian.KurianSp = 30;
        var warrior = Session(KarusWarrior, 70);

        KurianStamina.CanPay(kurian, 40).Should().BeFalse();
        KurianStamina.CanPay(kurian, 30).Should().BeTrue();
        KurianStamina.CanPay(warrior, 40).Should().BeTrue();
        KurianStamina.Spend(warrior, 40).Should().BeFalse();
        KurianStamina.Spend(kurian, 30).Should().BeTrue();
        kurian.KurianSp.Should().Be(0);
    }

    [Fact]
    public void TheBarRefillsOverTimeButNotWhenDead()
    {
        var session = Session(KarusKurianNovice, 70);
        KurianStamina.Refill(session);
        session.KurianSp = 200;

        KurianStamina.Regenerate(session, 3).Should().BeTrue();
        session.KurianSp.Should().Be(230);
        KurianStamina.Regenerate(session, 10).Should().BeTrue();
        session.KurianSp.Should().Be(250);
        KurianStamina.Regenerate(session, 10).Should().BeFalse();

        session.KurianSp = 0;
        session.Hp = 0;
        KurianStamina.Regenerate(session, 10).Should().BeFalse();
    }

    [Fact]
    public void TheBarPacketCarriesMaximumThenCurrent()
    {
        var packet = VitalsPacketWriter.StaminaChange(250, 182);

        packet.GetOpcode().Should().Be((byte)GameOpcodes.GS_SP_CHANGE);
        packet.ResetOffset();
        packet.ReadByte().Should().Be(VitalsPacketWriter.StaminaBar);
        packet.ReadByte().Should().Be(VitalsPacketWriter.StaminaBarShown);
        packet.ReadByte().Should().Be(250);
        packet.ReadByte().Should().Be(182);
    }

    private static UserSession Session(short classId, byte level)
    {
        var provider = CreateProvider(_ => { });
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var session = provider.GetRequiredService<SessionManager>().CreateSession(client, characterId: 74, accountId: 84);
        session.Class = classId;
        session.Level = level;
        session.Hp = AliveHp;
        return session;
    }
}
