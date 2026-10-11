using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const float NestExplainWidth = 230f;
    private const float NestExplainHeight = 160f;
    private const string NestQuestion = "Do you like to Enter?";
    private const string NestTeleportLine = "Teleport";
    private static readonly Color NestLogColour = new(1f, 1f, 0f);

    private CanvasLayer _nestLayer = null!;
    private HudWindow _nestWindow = null!;
    private Label _nestExplain = null!;
    private Notice? _nestExpConfirm;
    private int _nestStone;
    private double _battleUntil;

    private void MonsterStoneInit()
    {
        BuildNestWindow();
        Net.I.MonsterStoneEvent += OnMonsterStoneResult;
    }

    private void MonsterStoneDispose()
    {
        Net.I.MonsterStoneEvent -= OnMonsterStoneResult;
        DismissNestExpConfirm();
    }

    private void BuildNestWindow()
    {
        _nestLayer = new CanvasLayer { Layer = 74 };
        AddChild(_nestLayer);
        _nestWindow = new HudWindow("nestindun", "Monster Stone") { Visible = false };
        _nestWindow.Closed += CloseNestWindow;
        _nestLayer.AddChild(_nestWindow);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        _nestWindow.Body.AddChild(column);

        _nestExplain = HudStyle.Label(13);
        _nestExplain.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _nestExplain.VerticalAlignment = VerticalAlignment.Top;
        _nestExplain.CustomMinimumSize = new Vector2(NestExplainWidth, NestExplainHeight);
        column.AddChild(_nestExplain);

        column.AddChild(NestLine(NestQuestion, UiTheme.TextHi));
        column.AddChild(NestLine(NestTeleportLine, UiTheme.Gold));

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 6);
        buttons.AddChild(LookActionButton("Enter", EnterNest));
        buttons.AddChild(LookActionButton("Receive EXP", AskNestExp));
        buttons.AddChild(LookActionButton("Cancel", CloseNestWindow));
        column.AddChild(buttons);
    }

    private static Label NestLine(string text, Color colour)
    {
        var label = UiTheme.Text(text, 13, colour);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private static bool OpensNestDungeon(SkillData.Skill? skill) =>
        skill?.CooldownGroup == NestDungeon.EventSummonGroup;

    private bool NestBlockedByBattle() => Now() < _battleUntil || IsStunned();

    private void NestLog(int textId, string fallback) => CombatNotice(ItemData.Text(textId, fallback), NestLogColour);

    private void OpenNestDungeon(int itemId)
    {
        if (InParty)
        {
            NestLog(NestDungeon.InPartyText, "You cannot use is on Party Status.");
            return;
        }
        if (_selfDead)
        {
            NestLog(NestDungeon.DeadText, "Monster's Stone cannot be used in death.");
            return;
        }
        if (_nestWindow.Visible || CountInBackpack(itemId) < 1) return;

        _nestStone = itemId;
        _nestWindow.Title = ItemData.DisplayName(itemId);
        _nestExplain.Text = ItemData.Get(itemId)?.Desc ?? "";
        _nestWindow.Visible = true;
    }

    private void EnterNest()
    {
        if (NestBlockedByBattle())
        {
            NestLog(NestDungeon.BattleText, "Cannot be used during battle");
            return;
        }
        if (_selfDead)
        {
            NestLog(NestDungeon.DeadText, "Monster's Stone cannot be used in death.");
            return;
        }
        int stone = _nestStone;
        CloseNestWindow();
        if (CountInBackpack(stone) < 1) return;
        Net.I.SendMonsterStone(stone);
    }

    private void AskNestExp()
    {
        DismissNestExpConfirm();
        string text = ItemData.Text(NestDungeon.ExpQuestionText, "Do you wish to receive EXP using Monster Stone? \n EXP : %d")
            .Replace("%d", NestDungeon.ExpFor(Net.I.Sheet.Level).ToString("n0"));
        int stone = _nestStone;
        _nestExpConfirm = Notice.Confirm(this, text, "OK", "Cancel", () => ReceiveNestExp(stone), () => _nestExpConfirm = null);
    }

    private void ReceiveNestExp(int stone)
    {
        _nestExpConfirm = null;
        CloseNestWindow();
        if (CountInBackpack(stone) < 1) return;
        Net.I.SendMonsterStoneExp(stone);
    }

    private void CloseNestWindow()
    {
        _nestWindow.Visible = false;
        _nestStone = 0;
    }

    private void OnMonsterStoneResult(MonsterStoneResult result, int itemId)
    {
        if (NestDungeon.Reply(result, _zone) is not { } reply) return;
        string text = ItemData.Text(reply.Id, reply.Fallback);
        if (reply.Boxed) Notice.Show(this, text);
        else CombatNotice(text, NestLogColour);
    }

    private void DismissNestExpConfirm()
    {
        if (_nestExpConfirm != null && GodotObject.IsInstanceValid(_nestExpConfirm))
            _nestExpConfirm.Close();
        _nestExpConfirm = null;
    }
}
