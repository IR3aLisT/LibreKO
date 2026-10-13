using FluentAssertions;
using LibreKO.Common.Domain.Entities;
using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Domain.Services;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Network;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Protocol;
using LibreKO.Game.World;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace LibreKO.Game.Tests;

public class GmPanelEditTests : GameTestBase
{
    private const byte ReqStats = 3;
    private const byte ReqSetClass = 5;
    private const byte ReqSetLevel = 11;
    private const byte ReqSetSkill = 12;
    private const byte ReqSetLook = 13;
    private const byte KeepProgress = 0;
    private const byte ResetProgress = 1;

    private const short KarusWarrior = 101;
    private const short ElMoradPriest = 204;
    private const byte StartLevel = 10;

    [Fact]
    public async Task SetLevelKeepsProgressWhenAskedTo()
    {
        var (provider, client, session) = Arrange();
        using (provider)
        {
            await Send(provider, client, ReqSetLevel, 50, KeepProgress);

            session.Level.Should().Be(50);
        }
    }

    [Fact]
    public async Task SetLevelWithResetClearsMastery()
    {
        var (provider, client, session) = Arrange();
        using (provider)
        {
            session.SkillPoints[ProgressionTable.MasteryClassFirstSlot] = 20;

            await Send(provider, client, ReqSetLevel, 40, ResetProgress);

            session.Level.Should().Be(40);
            session.SkillPoints[ProgressionTable.MasteryClassFirstSlot].Should().Be(0);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ProgressionTable.MaxLevel + 1)]
    public async Task SetLevelRefusesALevelOutsideTheTable(int level)
    {
        var (provider, client, session) = Arrange();
        using (provider)
        {
            await Send(provider, client, ReqSetLevel, (byte)level, KeepProgress);

            session.Level.Should().Be(StartLevel);
        }
    }

    [Fact]
    public async Task SkillEditWritesThePoolAndEveryTree()
    {
        var (provider, client, session) = Arrange();
        using (provider)
        {
            await Send(provider, client, ReqSetSkill, 10, 1, 2, 3, 4, KeepProgress);

            session.SkillPoints[ProgressionTable.MasteryPoolSlot].Should().Be(10);
            session.SkillPoints
                .Skip(ProgressionTable.MasteryClassFirstSlot)
                .Take(ProgressionTable.MasteryClassSlotCount)
                .Should().Equal(1, 2, 3, 4);
        }
    }

    [Fact]
    public async Task SkillResetClearsTheTrees()
    {
        var (provider, client, session) = Arrange();
        using (provider)
        {
            session.SkillPoints[ProgressionTable.MasteryMasterSlot] = 5;

            await Send(provider, client, ReqSetSkill, 0, 0, 0, 0, 0, ResetProgress);

            session.SkillPoints[ProgressionTable.MasteryMasterSlot].Should().Be(0);
            session.SkillData.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task SetLookAcceptsABodyOfTheChosenNation()
    {
        var (provider, client, session) = Arrange();
        using (provider)
        {
            await Send(provider, client, ReqSetLook, (byte)AccountNation.ElMorad, (byte)CharacterRace.ElMoradMale);

            session.Nation.Should().Be(AccountNation.ElMorad);
            session.Race.Should().Be((byte)CharacterRace.ElMoradMale);
        }
    }

    [Fact]
    public async Task ANationChangeMovesEveryCharacterOfTheAccount()
    {
        var provider = CreateProvider(
            db =>
            {
                db.Accounts.Add(new Account { Login = "gm-look", Password = "pw", Nation = AccountNation.Karus });
                db.SaveChanges();
                int accountId = db.Accounts.Single(account => account.Login == "gm-look").Id;
                db.Characters.Add(LookCharacter(accountId, 0, "GameMaster", (byte)CharacterRace.KarusArchTuarek, KarusWarrior));
                db.Characters.Add(LookCharacter(accountId, 1, "Healer", (byte)CharacterRace.KarusPuriTuarek, KarusPriest));
            },
            gameData => gameData.GetCoefficient(Arg.Any<short>())
                .Returns(call => CreateBasicCoefficient(call.Arg<short>())));
        using (provider)
        {
            int accountId = await GetAccountIdAsync(provider, "gm-look");
            int gmId = await GetCharacterIdAsync(provider, "GameMaster");
            var client = Substitute.For<IClient>();
            client.Id.Returns(Guid.NewGuid());
            client.SendPacket(Arg.Any<Packet>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var session = provider.GetRequiredService<SessionManager>().CreateSession(client, gmId, accountId);
            session.Name = "GameMaster";
            session.IsGM = true;
            session.Class = KarusWarrior;
            session.Nation = AccountNation.Karus;
            session.Race = (byte)CharacterRace.KarusArchTuarek;

            await Send(provider, client, ReqSetLook, (byte)AccountNation.ElMorad, (byte)CharacterRace.ElMoradMale);

            session.Class.Should().Be(NationTransferRules.NewClass(KarusWarrior));
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var healer = db.Characters.Single(character => character.Name == "Healer");
            healer.Class.Should().Be(NationTransferRules.NewClass(KarusPriest));
            healer.Race.Should().Be((byte)CharacterRace.ElMoradFemale);
            db.Characters.Single(character => character.Name == "GameMaster").Race.Should().Be((byte)CharacterRace.ElMoradMale);
            db.Accounts.Single(account => account.Id == accountId).Nation.Should().Be(AccountNation.ElMorad);
        }
    }

    private const short KarusPriest = 104;

    private static Character LookCharacter(int accountId, byte slot, string name, byte race, short classId) => new()
    {
        AccountId = accountId,
        Slot = slot,
        Name = name,
        Race = race,
        Class = classId,
        Level = StartLevel,
        Hp = 100,
        Mp = 100,
        MapId = 21,
        Items = new byte[InventoryConstants.InventoryTotal * 8],
        SkillPointData = new byte[9],
    };

    [Theory]
    [InlineData((byte)CharacterRace.ElMoradMale)]
    [InlineData(99)]
    public async Task SetLookRefusesABodyTheNationCannotWear(byte race)
    {
        var (provider, client, session) = Arrange();
        using (provider)
        {
            await Send(provider, client, ReqSetLook, (byte)AccountNation.Karus, race);

            session.Nation.Should().Be(AccountNation.Karus);
            session.Race.Should().Be((byte)CharacterRace.KarusArchTuarek);
        }
    }

    [Theory]
    [InlineData(250_000, 250_000)]
    [InlineData(-5, 0)]
    public async Task StatsEditSetsTheNationalPoints(int requested, int expected)
    {
        var (provider, client, session) = Arrange();
        using (provider)
        {
            session.Loyalty = 1_200;

            var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
            packet.WriteByte(ReqStats);
            foreach (byte stat in new byte[] { 90, 80, 70, 60, 50 })
                packet.WriteByte(stat);
            packet.WriteShort(12);
            packet.WriteInt(requested);
            await provider.GetRequiredService<IAdminPanelPacketCoordinator>().HandleAsync(client, packet);

            session.Strength.Should().Be(90);
            session.StatPoints.Should().Be(12);
            session.Loyalty.Should().Be(expected);
        }
    }

    [Fact]
    public async Task StatsEditWithoutNationalPointsKeepsThem()
    {
        var (provider, client, session) = Arrange();
        using (provider)
        {
            session.Loyalty = 1_200;

            await Send(provider, client, ReqStats, 90, 80, 70, 60, 50, 12, 0);

            session.Strength.Should().Be(90);
            session.Loyalty.Should().Be(1_200);
        }
    }

    [Fact]
    public async Task ClassChangeReachesAClassOutsideTheOldFamily()
    {
        var (provider, client, session) = Arrange();
        using (provider)
        {
            var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
            packet.WriteByte(ReqSetClass);
            packet.WriteShort(ElMoradPriest);
            await provider.GetRequiredService<IAdminPanelPacketCoordinator>().HandleAsync(client, packet);

            session.Class.Should().Be(ElMoradPriest);
        }
    }

    private static (ServiceProvider Provider, IClient Client, UserSession Session) Arrange()
    {
        var provider = CreateProvider(
            _ => { },
            gameData => gameData.GetCoefficient(Arg.Any<short>())
                .Returns(call => CreateBasicCoefficient(call.Arg<short>())));

        var client = Substitute.For<IClient>();
        client.Id.Returns(Guid.NewGuid());
        client.SendPacket(Arg.Any<Packet>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var session = provider.GetRequiredService<SessionManager>()
            .CreateSession(client, characterId: 900, accountId: 910);
        session.Name = "GameMaster";
        session.IsGM = true;
        session.Class = KarusWarrior;
        session.Nation = AccountNation.Karus;
        session.Race = (byte)CharacterRace.KarusArchTuarek;
        session.Level = StartLevel;
        session.MaxHp = 100;
        session.Hp = 100;
        return (provider, client, session);
    }

    private static Task Send(ServiceProvider provider, IClient client, byte sub, params byte[] body)
    {
        var packet = new Packet(GameOpcodes.GS_ADMIN_PANEL);
        packet.WriteByte(sub);
        foreach (var value in body)
            packet.WriteByte(value);
        return provider.GetRequiredService<IAdminPanelPacketCoordinator>().HandleAsync(client, packet);
    }
}
