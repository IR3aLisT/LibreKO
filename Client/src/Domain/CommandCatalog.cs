using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public enum CommandGroup { Private, Trade, Party, Clan, Knights, King }

public enum CommandId
{
    Pm, Town, Exit, Greeting, Greeting2, Greeting3, Provoke, Provoke2, Provoke3, Recommend, IndividualBattle,
    SitStand, WalkRun, Location, AddFriend, ChatBlock, NationChat, Vault, Achieve,
    Trade, BlockTrade, AllowTrade, Merchant, Price, EditMerchant,
    Party, QuitParty, SeekingParty, BlockParty, AllowParty,
    JoinClan, QuitClan, BanFromClan, ClanCommand, ClanWar, Surrender, AppointVice, ClanChat, ClanBattle, ClanNotice, ClanKick,
    Confederacy, BanKnights, QuitKnights, Report,
    RoyalOrder, Prize, ExperiencePoint, DropRate, Rain, Clear,
}

public readonly record struct CommandEntry(CommandId Id, CommandGroup Group, int NameText, int HintText, string Fallback, bool Prefill);

public static class CommandCatalog
{
    public const int FirstGroupText = 7800;
    public const int KingGroupText = 7806;
    public const int NoTargetText = 7680;
    public const int TradeBlockedText = 7600;
    public const int PartyBlockedText = 7601;
    public const int TradeAllowedText = 7602;
    public const int PartyAllowedText = 7603;
    public const int TradeRequestsRefusedText = 7604;
    public const int PartyRequestsRefusedText = 7605;
    public const int NoPremiumText = 42205;
    public const int ClanBattleInPartyText = 10959;
    public const int ClanBattleNoRightText = 10958;
    public const int BanFromClanAskText = 8710;
    private const string HintSlot = "%s";
    private const int PrivateNames = 8000, PrivateHints = 8100;
    private const int TradeNames = 8200, TradeHints = 8300;
    private const int PartyNames = 8400, PartyHints = 8500;
    private const int ClanNames = 8600, ClanHints = 8700;
    private const int KnightsNames = 8800, KnightsHints = 8900;
    private const int KingNames = 9400, KingHints = 9500;

    public static readonly CommandGroup[] Groups =
        [CommandGroup.Private, CommandGroup.Trade, CommandGroup.Party, CommandGroup.Clan, CommandGroup.Knights, CommandGroup.King];

    private static readonly CommandEntry[] All = Build();

    public static int GroupText(CommandGroup group) =>
        group == CommandGroup.King ? KingGroupText : FirstGroupText + (int)group;

    public static string GroupFallback(CommandGroup group) => group switch
    {
        CommandGroup.Private => "Private",
        CommandGroup.Trade => "Trade",
        CommandGroup.Party => "Party",
        CommandGroup.Clan => "Clan",
        CommandGroup.Knights => "Knights",
        _ => "King",
    };

    public static IEnumerable<CommandEntry> Entries(CommandGroup group)
    {
        foreach (var entry in All)
            if (entry.Group == group) yield return entry;
    }

    public static CommandEntry? Find(string word, Func<int, string, string> text)
    {
        foreach (var entry in All)
            if (string.Equals(word, text(entry.NameText, entry.Fallback), StringComparison.OrdinalIgnoreCase))
                return entry;
        return null;
    }

    public static string Hint(CommandEntry entry, Func<int, string, string> text)
    {
        string hint = text(entry.HintText, "");
        return hint.Replace(HintSlot, text(entry.NameText, entry.Fallback)).Replace("%%", "%");
    }

    private static CommandEntry[] Build()
    {
        var list = new List<CommandEntry>();
        void Group(CommandGroup group, int firstName, int firstHint, (CommandId Id, string Name, bool Prefill)[] rows, Func<int, int>? nameOffset = null, Func<int, int>? hintOffset = null)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                var (id, name, prefill) = rows[i];
                if (id == default && name.Length == 0) continue;
                list.Add(new CommandEntry(id, group, firstName + (nameOffset?.Invoke(i) ?? i), firstHint + (hintOffset?.Invoke(i) ?? i), name, prefill));
            }
        }

        const int PrivateTextShift = 17;
        Group(CommandGroup.Private, PrivateNames, PrivateHints,
        [
            (CommandId.Pm, "PM", true), (CommandId.Town, "Town", false), (CommandId.Exit, "Exit", false),
            (CommandId.Greeting, "Greeting", false), (CommandId.Greeting2, "Greeting2", false), (CommandId.Greeting3, "Greeting3", false),
            (CommandId.Provoke, "Provoke", false), (CommandId.Provoke2, "Provoke2", false), (CommandId.Provoke3, "Provoke3", false),
            (default, "", false),
            (CommandId.Recommend, "Recommend", false), (CommandId.IndividualBattle, "Individual_Battle", false),
            (CommandId.SitStand, "Sit/Stand", false), (CommandId.WalkRun, "Walk/Run", false), (CommandId.Location, "Location", false),
            (CommandId.AddFriend, "Add_Friend", true),
            (default, "", false),
            (CommandId.ChatBlock, "chatblock", false), (CommandId.NationChat, "nation_chat", false), (CommandId.Vault, "Vault", false),
            (CommandId.Achieve, "Achieve", false),
        ], i => i >= PrivateTextShift ? i + 1 : i, i => i >= PrivateTextShift ? i + 1 : i);

        Group(CommandGroup.Trade, TradeNames, TradeHints,
        [
            (CommandId.Trade, "Trade", false), (CommandId.BlockTrade, "Block_Trade_Request", false),
            (CommandId.AllowTrade, "Allow_Trade_Request", false), (CommandId.Merchant, "Merchant", false),
            (CommandId.Price, "Price", false), (CommandId.EditMerchant, "Edit_Merchant", false),
        ]);

        Group(CommandGroup.Party, PartyNames, PartyHints,
        [
            (CommandId.Party, "Party", false), (CommandId.QuitParty, "Quit_party", false), (CommandId.SeekingParty, "Seeking_Party", false),
            (CommandId.BlockParty, "Block_Party", false), (CommandId.AllowParty, "Allow_Party", false),
        ]);

        Group(CommandGroup.Clan, ClanNames, ClanHints,
        [
            (CommandId.JoinClan, "Join_Clan", false), (CommandId.QuitClan, "Quit_Clan", false), (CommandId.BanFromClan, "Ban_from_Clan", true),
            (CommandId.ClanCommand, "Command", true), (CommandId.ClanWar, "Clan_War", false), (CommandId.Surrender, "Surrender", false),
            (CommandId.AppointVice, "Appoint_as_Assistant_Clan_Leader", true), (CommandId.ClanChat, "Clan_Chat", true),
            (CommandId.ClanBattle, "Clan_Battle", false), (CommandId.ClanNotice, "ClanNotice", true), (CommandId.ClanKick, "Kick", false),
        ]);

        Group(CommandGroup.Knights, KnightsNames, KnightsHints,
        [
            (CommandId.Confederacy, "Confederacy", false), (CommandId.BanKnights, "Ban_Knights", false),
            (CommandId.QuitKnights, "Quit_Knights", false), (CommandId.Report, "Report", true),
        ]);

        Group(CommandGroup.King, KingNames, KingHints,
        [
            (CommandId.RoyalOrder, "RoyalOrder", true), (CommandId.Prize, "Prize", true), (CommandId.ExperiencePoint, "ExperiencePoint", true),
            (CommandId.DropRate, "DropRate", true), (CommandId.Rain, "Rain", true), (default, "", false), (CommandId.Clear, "Clear", true),
        ]);

        return list.ToArray();
    }
}
