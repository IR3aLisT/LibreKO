using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int NameChangeAuctionOpenText = 43715;
    private const int TextClanRenameHint = 7708;
    private const int TextClanNameTaken = 7709;
    private const int TextClanRenamed = 7710;
    private const int TextClanRenameNotMember = 7712;

    private CanvasLayer _nameChangeLayer = null!;
    private HudWindow _nameChangePanel = null!;
    private LineEdit _nameChangeEdit = null!;
    private Label _nameChangeStatus = null!;
    private Button _nameChangeBtn = null!;
    private bool _nameChangeShown;
    private bool _nameChangeClan;
    private string _nameChangePending = "";
    private Label _nameChangeTitle = null!, _nameChangeHint = null!;

    private void NameChangeInit()
    {
        BuildNameChangePanel();
        Net.I.NameChangeSuccessEvent += OnNameChangeSuccess;
        Net.I.NameChangeResultEvent += OnNameChangeResult;
        Net.I.ClanNameChangeResultEvent += OnClanNameChangeResult;
        Net.I.ClanRenamedEvent += OnClanRenamed;
    }

    private void NameChangeDispose()
    {
        Net.I.NameChangeSuccessEvent -= OnNameChangeSuccess;
        Net.I.NameChangeResultEvent -= OnNameChangeResult;
        Net.I.ClanNameChangeResultEvent -= OnClanNameChangeResult;
        Net.I.ClanRenamedEvent -= OnClanRenamed;
    }

    private void BuildNameChangePanel()
    {
        _nameChangeLayer = new CanvasLayer { Layer = 74 };
        AddChild(_nameChangeLayer);

        _nameChangePanel = new HudWindow("namechange", "Change Name") { Visible = false };
        _nameChangePanel.Closed += CloseNameChange;
        _nameChangeLayer.AddChild(_nameChangePanel);

        var r = _nameChangePanel.Body;
        r.AddThemeConstantOverride("separation", 8);

        _nameChangeTitle = UiTheme.SectionTitle("Rename Character");
        r.AddChild(_nameChangeTitle);

        _nameChangeHint = HudStyle.Label(12);
        _nameChangeHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _nameChangeHint.CustomMinimumSize = new Vector2(260, 0);
        r.AddChild(_nameChangeHint);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        _nameChangeEdit = new LineEdit
        {
            PlaceholderText = "new name",
            MaxLength = 20,
            CustomMinimumSize = new Vector2(200, 0),
        };
        _nameChangeEdit.TextSubmitted += _ => SubmitNameChange();
        row.AddChild(_nameChangeEdit);
        _nameChangeBtn = new Button { Text = "Rename", FocusMode = Control.FocusModeEnum.None };
        _nameChangeBtn.Pressed += SubmitNameChange;
        row.AddChild(_nameChangeBtn);
        r.AddChild(row);

        _nameChangeStatus = HudStyle.Label(13);
        r.AddChild(_nameChangeStatus);
    }

    private void OpenNameChange(bool clan = false)
    {
        _nameChangeClan = clan;
        _nameChangeTitle.Text = clan ? "Rename Clan" : "Rename Character";
        _nameChangeHint.Text = clan
            ? ItemData.Text(TextClanRenameHint, "Click on the clan ID you would like to change.")
            : "Requires a Scroll of Identity. 3-20 characters.";
        _nameChangePanel.Visible = true;
        _nameChangeShown = true;
        _nameChangeEdit.Text = clan ? "" : Net.I.LastEnter.Name ?? "";
        _nameChangeEdit.CaretColumn = _nameChangeEdit.Text.Length;
        SetNameChangeStatus("", false);
        _nameChangeEdit.GrabFocus();
    }

    private void CloseNameChange()
    {
        if (!_nameChangeShown) return;
        _nameChangeShown = false;
        _nameChangePanel.Visible = false;
    }

    private void SubmitNameChange()
    {
        string name = _nameChangeEdit.Text.Trim();
        if (name.Length is < 3 or > 20)
        {
            SetNameChangeStatus("Name must be 3-20 characters.", true);
            return;
        }
        _nameChangePending = name;
        SetNameChangeStatus("Renaming…", false);
        if (_nameChangeClan)
        {
            Net.I.SendClanNameChangeConfirm(name);
            return;
        }
        if (name == (Net.I.LastEnter.Name ?? ""))
        {
            SetNameChangeStatus("That's already your name.", true);
            return;
        }
        Net.I.SendNameChangeConfirm(name);
    }

    private void OnNameChangeSuccess(string newName)
    {
        if (string.IsNullOrEmpty(newName)) newName = _nameChangePending;
        Net.I.ApplyOwnName(newName);
        if (SelfPlate() is { } plate) plate.Name.Text = newName;
        CombatNotice($"Your character is now named \"{newName}\".");
        SetNameChangeStatus("Name changed!", false);
        CloseNameChange();
    }

    private void OnNameChangeResult(int code)
    {
        switch (code)
        {
            case Net.NameChangeShowDialog:
                if (_nameChangeShown)
                    SetNameChangeStatus("You need a Scroll of Identity to rename.", true);
                else
                    OpenNameChange();
                break;
            case Net.NameChangeInvalid:
                SetNameChangeStatus("That name is taken or invalid.", true);
                break;
            case Net.NameChangeInClan:
                SetNameChangeStatus("Leave your clan before renaming.", true);
                break;
            case Net.NameChangeAuctionOpen:
                SetNameChangeStatus(ItemData.Text(NameChangeAuctionOpenText, "You are already bidding or have items to claim"), true);
                break;
            default:
                SetNameChangeStatus("Couldn't change the name.", true);
                break;
        }
    }

    private void OnClanNameChangeResult(int code)
    {
        switch (code)
        {
            case Net.ClanNameChangeShowDialog:
                OpenNameChange(clan: true);
                break;
            case Net.ClanNameChangeInvalid:
                if (!_nameChangeShown || !_nameChangeClan) OpenNameChange(clan: true);
                SetNameChangeStatus(ItemData.Text(TextClanNameTaken, "The ID you entered is being used by another clan. Please pick another ID."), true);
                break;
            case Net.ClanNameChangeNotInClan:
                CombatNotice(ItemData.Text(TextClanRenameNotMember, "You can change your clan ID only if you are a clan member!"));
                CloseNameChange();
                break;
        }
    }

    private void OnClanRenamed(string newName)
    {
        if (_nameChangeClan) CloseNameChange();
        CombatNotice(ItemData.Text(TextClanRenamed, "Your clan ID has been changed."));
        Net.I.SendClanMembersRequest();
    }

    private void SetNameChangeStatus(string text, bool warn)
    {
        _nameChangeStatus.Text = text;
        _nameChangeStatus.AddThemeColorOverride("font_color", warn ? new Color("ff6a6a") : Colors.White);
    }
}
