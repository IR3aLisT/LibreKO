using FluentAssertions;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class DevilFormTests : GameTestBase
{
    private const int DevilSkill = 114710;

    [Fact]
    public void TheDevilBuffAnnouncesStateTwelveOnceEachWay()
    {
        using var provider = CreateProvider(_ => { });
        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        var session = provider.GetRequiredService<SessionManager>().CreateSession(client, characterId: 500, accountId: 501);
        session.SetBuff(DevilSkill, new ActiveBuff
        {
            MagicId = DevilSkill,
            BuffType = BuffType.DevilTransform,
            ExpireTicks = DateTime.UtcNow.AddMinutes(10).Ticks,
        });

        var start = DevilForms.Sync(session);
        start.Should().NotBeNull();
        start!.ReadInt().Should().Be(500);
        start.ReadByte().Should().Be((byte)StateChangeType.DevilForm);
        start.ReadInt().Should().Be(1);
        DevilForms.Sync(session).Should().BeNull();

        session.RemoveBuff(DevilSkill);
        var end = DevilForms.Sync(session);
        end.Should().NotBeNull();
        end!.ReadInt();
        end.ReadByte();
        end.ReadInt().Should().Be(0);
        session.DevilForm.Should().BeFalse();
    }
}
