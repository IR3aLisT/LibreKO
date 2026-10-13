using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private const float MonumentBlipRadius = 7f;
    private const float NereidStripHeight = 22f;
    private const float NereidBoardWidth = 260f;
    private const float NereidBoardTop = 60f;
    private const float NereidBoardBottom = 70f;
    private static readonly Color MonumentFree = new(0.62f, 0.6f, 0.55f);
    private static readonly Color MonumentKarus = new(0.82f, 0.2f, 0.18f);
    private static readonly Color MonumentElMorad = new(0.2f, 0.45f, 0.9f);
    private static readonly Color NereidLineColour = new(1f, 1f, 0f);
    private static readonly Color NereidContColour = new("9b9bff");

    private Label _nereidStrip = null!;
    private Label _nereidFlagBoard = null!, _nereidScoreBoard = null!;
    private int _nereidKarusPoints, _nereidElmoPoints;
    private bool _nereidPointsKnown;

    private bool InNereid => _zone == NereidMonuments.Zone;

    private void NereidInit()
    {
        _nereidStrip = UiTheme.Text("", 12, UiTheme.TextHi, HorizontalAlignment.Center);
        _nereidStrip.AddThemeStyleboxOverride("normal", UiTheme.MeterTrack());
        _nereidStrip.Visible = false;
        if (_miniMap != null)
        {
            _miniMap.AddChild(_nereidStrip);
            _nereidStrip.Position = new Vector2(0f, MiniMap.MapSize - NereidStripHeight);
            _nereidStrip.Size = new Vector2(MiniMap.MapSize, NereidStripHeight);
        }

        _nereidFlagBoard = NereidBoard(Control.LayoutPreset.CenterTop, NereidBoardTop);
        _nereidScoreBoard = NereidBoard(Control.LayoutPreset.CenterBottom, -NereidBoardBottom);
        Net.I.MapEventEvent += OnMapEvent;
        RefreshNereid();
    }

    private void NereidDispose() => Net.I.MapEventEvent -= OnMapEvent;

    private Label NereidBoard(Control.LayoutPreset preset, float offset)
    {
        var board = UiTheme.Text("", 15, UiTheme.GoldBright, HorizontalAlignment.Center);
        board.AddThemeStyleboxOverride("normal", UiTheme.MeterTrack());
        board.SetAnchorsPreset(preset);
        board.OffsetLeft = -NereidBoardWidth * 0.5f;
        board.OffsetRight = NereidBoardWidth * 0.5f;
        board.OffsetTop = offset;
        board.OffsetBottom = offset + NereidStripHeight;
        board.MouseFilter = Control.MouseFilterEnum.Ignore;
        board.Visible = false;
        _fullMapLayer.AddChild(board);
        return board;
    }

    private void OnMapEvent(MapEventReply reply)
    {
        switch (reply.Kind)
        {
            case MapEventKind.Captured:
                if (!NereidMonuments.Has(reply.Index)) break;
                var name = NereidMonuments.Name(reply.Index);
                int text = NereidMonuments.CaptureText(reply.Nation, Net.I.Nation);
                CombatNotice(ItemData.Text(text, text == NereidMonuments.OccupiedText
                        ? "You have occupied %s." : "%s has been occupied by the opponent country.")
                    .Replace("%s", ItemData.Text(name.Id, name.Fallback)), NereidLineColour);
                break;
            case MapEventKind.Points:
                _nereidKarusPoints = (int)reply.A;
                _nereidElmoPoints = (int)reply.B;
                _nereidPointsKnown = true;
                break;
            case MapEventKind.Provisions:
                var nation = NereidMonuments.NationName(reply.Nation);
                CombatNotice(ItemData.Text(NereidMonuments.ProvisionsText, "%s has secured provisions.")
                    .Replace("%s", ItemData.Text(nation.Id, nation.Fallback)), NereidLineColour);
                if (reply.A != 0)
                    CombatNotice(TextTemplate.Fill(ItemData.Text(NereidMonuments.ProvisionPointsText, "(+ %d occupation point acquired)"), reply.A),
                        NereidLineColour);
                break;
            case MapEventKind.Cont:
                CombatNotice(ItemData.Text(NereidMonuments.ContText, "Acquired %u Nereidth occupation CONT")
                    .Replace("%u", Net.I.NereidContGain.ToString()), NereidContColour);
                break;
            case MapEventKind.Result:
                var owners = Net.I.MonumentOwners;
                var winner = NereidMonuments.NationName(reply.Nation);
                string result = TextTemplate.Fill(
                    ItemData.Text(NereidMonuments.ResultText, "*Called* Karus %d: El Morad %d       %s has succeeded in occupying Nereidth."),
                    NereidMonuments.Held(owners, Nations.Karus), NereidMonuments.Held(owners, Nations.ElMorad),
                    ItemData.Text(winner.Id, winner.Fallback));
                ShowBattleBanner(result);
                break;
        }
        RefreshNereid();
    }

    private void RefreshNereid()
    {
        var owners = Net.I.MonumentOwners;
        bool shown = InNereid && owners.Length > 0;
        int karus = NereidMonuments.Held(owners, Nations.Karus);
        int elmo = NereidMonuments.Held(owners, Nations.ElMorad);
        _nereidStrip.Visible = shown;
        _nereidStrip.Text = $"El Morad  {elmo} : {karus}  Karus";
        _nereidFlagBoard.Visible = shown;
        _nereidFlagBoard.Text = $"Total Point   Elmo: {elmo}   Karu: {karus}";
        _nereidScoreBoard.Visible = shown && _nereidPointsKnown;
        _nereidScoreBoard.Text = $"TotalPoint   Elmo: {_nereidElmoPoints}   Karu: {_nereidKarusPoints}";
    }

    private void AddMonumentBlips()
    {
        if (!InNereid) return;
        var owners = Net.I.MonumentOwners;
        for (int i = 0; i < owners.Length && NereidMonuments.Has(i); i++)
        {
            var (x, z) = NereidMonuments.Spot(i);
            var colour = owners[i] switch
            {
                Nations.Karus => MonumentKarus,
                Nations.ElMorad => MonumentElMorad,
                _ => MonumentFree,
            };
            _blipScratch.Add(new MiniMap.Blip(x, z, colour, MonumentBlipRadius, false));
        }
    }
}
