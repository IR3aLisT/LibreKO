using System;
using Godot;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int TextResurrectOffer = 44304;
    private const int TextResurrectInPlace = 44305;
    private const int TextRebirthStandby = 44300;
    private const int TextRebirthMissingItem = 44301;
    private const int TextRebirthError = 44303;
    private const double ResurrectOfferSeconds = 30.0;
    private const double ResurrectChoiceSeconds = 10.0;
    private const int RespawnFxKarus = 10020;
    private const int RespawnFxElmorad = 10019;
    private const short ResurrectOffered = 2;
    private const int ResurrectResultSlot = 1;

    private Notice? _resurrectPrompt;
    private int _resurrectToken;

    private void ResurrectOfferInit()
    {
        Net.I.ResurrectOfferEvent += OnResurrectOffer;
        Net.I.RegeneRefusedEvent += OnRegeneRefused;
    }

    private void ResurrectOfferDispose()
    {
        Net.I.ResurrectOfferEvent -= OnResurrectOffer;
        Net.I.RegeneRefusedEvent -= OnRegeneRefused;
    }

    private static bool ResurrectionOnlyOffered(SkillData.Skill s, short[] data) =>
        s.IsResurrect && data.Length > ResurrectResultSlot && data[ResurrectResultSlot] == ResurrectOffered;

    private void OnResurrectOffer(int casterId, int skillId)
    {
        if (!_selfDead) return;
        string text = FillInOrder(
            SystemText(TextResurrectOffer, "%s used %s. Would you like to resurrect?"),
            CombatEntityName(casterId), SkillData.Get(skillId)?.Name ?? "");
        ShowTimedPrompt(text, ResurrectOfferSeconds, accepted =>
        {
            if (accepted) Net.I.SendResurrectAccept(casterId);
            else AskResurrectInPlace(casterId);
        });
    }

    private void AskResurrectInPlace(int casterId)
    {
        if (!_selfDead) return;
        ShowTimedPrompt(
            SystemText(TextResurrectInPlace,
                "Would you like to resurrect in place? If you cancel, you will resurrect in town."),
            ResurrectChoiceSeconds,
            inPlace => Net.I.SendResurrectChoice(inPlace ? Net.ResurrectInPlace : Net.ResurrectInTown, casterId));
    }

    private void ShowTimedPrompt(string text, double seconds, Action<bool> answer)
    {
        CloseResurrectPrompt();
        int token = _resurrectToken;
        bool answered = false;
        void Answer(bool yes)
        {
            if (answered || token != _resurrectToken) return;
            answered = true;
            _resurrectPrompt = null;
            answer(yes);
        }
        var prompt = Notice.Confirm(this, text, "OK", "Cancel", () => Answer(true), () => Answer(false));
        _resurrectPrompt = prompt;
        GetTree().CreateTimer(seconds).Timeout += () =>
        {
            if (answered || token != _resurrectToken) return;
            Answer(false);
            if (IsInstanceValid(prompt)) prompt.Close();
        };
    }

    private void CloseResurrectPrompt()
    {
        _resurrectToken++;
        if (_resurrectPrompt != null && IsInstanceValid(_resurrectPrompt)) _resurrectPrompt.Close();
        _resurrectPrompt = null;
    }

    private void OnRegeneRefused(ushort code, int itemId, ushort amount)
    {
        string text = code switch
        {
            RegeneReply.RefusedStandby => SystemText(TextRebirthStandby, "You are in a standby for the Rebirth."),
            RegeneReply.RefusedMissingItem => FillInOrder(
                SystemText(TextRebirthMissingItem, "Player does not have the required item [%s]. Amount Needed : %d"),
                ItemData.DisplayName(itemId), amount.ToString()),
            _ => FillInOrder(SystemText(TextRebirthError, "Rebirth Error (%d)"), code.ToString()),
        };
        CombatNotice(text);
        if (_selfDead) ArmRespawnButton(DeathOffersScrolls ? ScrollConfirmHint : RespawnHint);
    }

    private void SpawnRespawnEffect()
    {
        int fx = Net.I.LastEnter.Nation == Nations.Karus ? RespawnFxKarus : RespawnFxElmorad;
        if (Fx.NameForId(fx) is { } name) SpawnFxOn(_myId, name, 0f);
    }

    private static string FillInOrder(string text, params string[] values)
    {
        foreach (string value in values)
        {
            int at = FirstPlaceholder(text);
            if (at < 0) break;
            text = text[..at] + value + text[(at + 2)..];
        }
        return text;
    }

    private static int FirstPlaceholder(string text)
    {
        int s = text.IndexOf("%s", StringComparison.Ordinal);
        int d = text.IndexOf("%d", StringComparison.Ordinal);
        return s < 0 ? d : d < 0 ? s : Math.Min(s, d);
    }
}
