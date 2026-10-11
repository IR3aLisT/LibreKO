using System.Collections.Generic;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const int ClanSymbolCost = 5_000_000;
    private const int ClanSymbolCostText = 2733;
    private const int ClanSymbolOkText = 7610;
    private const int ClanSymbolFailText = 7611;
    private const int ClanSymbolAskText = 7617;
    private const int ClanSymbolNoCoins = 14;
    private const float ClanCardMarkSize = 40f;

    private readonly Dictionary<int, (int Version, Texture2D? Texture)> _clanMarks = new();
    private readonly HashSet<int> _clanMarkAsked = new();
    private TextureRect? _clanCardMark;
    private Button? _clanSymbolBtn;
    private FileDialog? _clanSymbolPicker;

    private void ClanMarksInit()
    {
        Net.I.ClanMarkEvent += OnClanMark;
        Net.I.ClanMarkRegisterEvent += OnClanMarkRegister;
    }

    private void ClanMarksDispose()
    {
        Net.I.ClanMarkEvent -= OnClanMark;
        Net.I.ClanMarkRegisterEvent -= OnClanMarkRegister;
    }

    private Texture2D? ClanMark(int clanId, int version)
    {
        if (clanId <= 0 || version <= 0) return null;
        if (_clanMarks.TryGetValue(clanId, out var cached) && cached.Version == version) return cached.Texture;
        if (_clanMarkAsked.Add(clanId)) Net.I.SendClanMarkRequest(clanId);
        return cached.Texture;
    }

    private void OnClanMark(int clanId, int version, byte[] data)
    {
        _clanMarkAsked.Remove(clanId);
        var texture = ClanMarkCodec.Decode(data);
        _clanMarks[clanId] = (version, texture);
        foreach (var e in _ents.Values)
            if (e.KnightsId == clanId)
            {
                e.ClanMarkVersion = version;
                e.Plate?.SetMark(PlateClanMark(texture));
            }
        if (MyClan.InClan && MyClan.ClanId == clanId)
        {
            SelfPlate()?.SetMark(PlateClanMark(texture));
            RefreshClanCardMark();
        }
        RefreshRankBoardMarks();
    }

    private void RefreshSelfClanMark()
    {
        var mark = MyClan.InClan ? ClanMark(MyClan.ClanId, MyClan.MarkVersion) : null;
        SelfPlate()?.SetMark(PlateClanMark(mark));
        RefreshClanCardMark();
    }

    private Texture2D? PlateClanMark(Texture2D? mark) => NameMark.HidesClan(_zone) ? null : mark;

    private void RefreshClanCardMark()
    {
        if (_clanCardMark == null || !IsInstanceValid(_clanCardMark)) return;
        var info = MyClan;
        var mark = info.InClan ? ClanMark(info.ClanId, info.MarkVersion) : null;
        _clanCardMark.Texture = mark;
        _clanCardMark.Visible = mark != null;
        if (_clanSymbolBtn != null) _clanSymbolBtn.Visible = info.IsChief && info.Flag >= ClanTypes.Promoted;
    }

    private Control BuildClanCardMark()
    {
        _clanCardMark = new TextureRect
        {
            CustomMinimumSize = new Vector2(ClanCardMarkSize, ClanCardMarkSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
            Visible = false,
        };
        return _clanCardMark;
    }

    private Button BuildClanSymbolButton()
    {
        _clanSymbolBtn = UiTheme.SmallButton("Symbol...", "Register a 32x32 Knights symbol");
        _clanSymbolBtn.Visible = false;
        _clanSymbolBtn.Pressed += PickClanSymbol;
        return _clanSymbolBtn;
    }

    private void PickClanSymbol()
    {
        if (_clanSymbolPicker == null || !IsInstanceValid(_clanSymbolPicker))
        {
            _clanSymbolPicker = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.OpenFile,
                Access = FileDialog.AccessEnum.Filesystem,
                Filters = new[] { "*.png, *.jpg, *.jpeg, *.bmp ; Images" },
                Title = "Knights Symbol",
                UseNativeDialog = true,
            };
            _clanSymbolPicker.FileSelected += OnClanSymbolPicked;
            AddChild(_clanSymbolPicker);
        }
        _clanSymbolPicker.PopupCentered(new Vector2I(720, 480));
    }

    private void OnClanSymbolPicked(string path)
    {
        var image = Image.LoadFromFile(path);
        if (image == null || image.IsEmpty())
        {
            CombatNotice(ItemData.Text(ClanSymbolFailText, "Failed to register the symbol. Please try again."));
            return;
        }
        byte[] mark = ClanMarkCodec.Encode(image);
        if (Sheet.Gold < ClanSymbolCost)
        {
            CombatNotice(ClanSymbolCostNotice());
            return;
        }
        Notice.Confirm(this,
            $"{ItemData.Text(ClanSymbolAskText, "Will you register your Knights symbol?")}\n{ClanSymbolCost:n0} coins",
            "Register", "Cancel", () => Net.I.SendClanMarkRegister(mark));
    }

    private static string ClanSymbolCostNotice() => TextTemplate.Fill(
        ItemData.Text(ClanSymbolCostText, "Sorry, but you'll need %d Coins in order to register your Knights symbol."),
        ClanSymbolCost);

    private void OnClanMarkRegister(int result, int version)
    {
        if (result == Net.KnResultOk)
        {
            CombatNotice(ItemData.Text(ClanSymbolOkText, "Successfully registered the symbol."));
            _clanMarks.Remove(MyClan.ClanId);
            RefreshSelfClanMark();
            return;
        }
        CombatNotice(result == ClanSymbolNoCoins
            ? ClanSymbolCostNotice()
            : ItemData.Text(ClanSymbolFailText, "Failed to register the symbol. Please try again."));
    }
}
