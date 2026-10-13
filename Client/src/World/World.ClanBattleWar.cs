using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int ClanBattleAskText = 6900;
    private const int ClanBattleRefusalFirstText = 6901;
    private const int ClanBattleNotAtWarText = 6909;
    private const int ClanBattleTheySurrenderedText = 6910;
    private const int ClanBattleYouSurrenderedText = 6911;
    private const int ClanBattleBegunText = 6912;
    private const byte ClanBattleLastRefusal = 9;
    private const byte ClanBattleNotAtWar = 16;

    private void ClanBattleWarInit()
    {
        Net.I.ClanBattleResultEvent += OnClanBattleResult;
        Net.I.ClanBattleBegunEvent += OnClanBattleBegun;
        Net.I.ClanBattleEndEvent += OnClanBattleEnd;
    }

    private void ClanBattleWarDispose()
    {
        Net.I.ClanBattleResultEvent -= OnClanBattleResult;
        Net.I.ClanBattleBegunEvent -= OnClanBattleBegun;
        Net.I.ClanBattleEndEvent -= OnClanBattleEnd;
    }

    private void OnClanBattleResult(byte result, int challengerId, string challenger)
    {
        if (result == Net.ClanBattleChallenged)
        {
            string ask = TextTemplate.Fill(ItemData.Text(ClanBattleAskText,
                "Clan Leader %s has challenged your clan for a clan battle. Will you accept?"), challenger);
            Notice.Confirm(this, ask, "Accept", "Decline",
                () => Net.I.SendClanBattleAnswer(true, challengerId),
                () => Net.I.SendClanBattleAnswer(false, challengerId));
            return;
        }
        int text = result switch
        {
            >= 2 and <= ClanBattleLastRefusal => ClanBattleRefusalFirstText + result - 2,
            ClanBattleNotAtWar => ClanBattleNotAtWarText,
            _ => 0,
        };
        if (text > 0) Notice.Show(this, ItemData.Text(text, ""));
    }

    private void OnClanBattleBegun(short enemyClan)
    {
        RefreshPlayerHostility(Net.I.CurrentZoneAbility);
        Notice.Show(this, ItemData.Text(ClanBattleBegunText, "The clan battle has begun."));
    }

    private void OnClanBattleEnd(byte kind, string clan)
    {
        if (kind == Net.ClanBattleCleared)
        {
            RefreshPlayerHostility(Net.I.CurrentZoneAbility);
            return;
        }
        int text = kind == Net.ClanBattleTheySurrendered ? ClanBattleTheySurrenderedText : ClanBattleYouSurrenderedText;
        Notice.Show(this, TextTemplate.Fill(ItemData.Text(text, ""), clan));
    }

    private bool CanChallengeClan(Ent target) =>
        MyClan.InClan && MyClan.IsChief && Net.I.EnemyClanId == 0
        && target.KnightsId > 0 && target.KnightsId != MyClan.ClanId;
}
