using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    private const string RankAuraNode = "rank_fx";
    private const string RankAuraRetiredNode = "rank_fx_retired";
    private static readonly StringName RankAuraFxMeta = "rank_fx_name";

    private int _selfPersonalRank = NationRankAura.Unranked;
    private bool _selfKing;

    private void RefreshRankAura(Node3D? body, int personalRank, bool stealthed, bool king = false, bool shown = true)
    {
        if (body == null || !GodotObject.IsInstanceValid(body)) return;
        string? want = null;
        if (shown && !stealthed && !NationRankAura.HiddenIn(_zone))
        {
            int fxId = NationRankAura.FxIdFor(personalRank, king);
            if (fxId != NationRankAura.NoFx) want = Fx.NameForId(fxId);
        }
        var current = body.GetNodeOrNull<Node3D>(RankAuraNode);
        string? have = current != null && current.HasMeta(RankAuraFxMeta) ? current.GetMeta(RankAuraFxMeta).AsString() : null;
        if (want == have) return;
        if (current != null)
        {
            current.Name = RankAuraRetiredNode;
            Fx.Free(current);
        }
        if (want == null) return;
        var aura = Fx.Spawn(want, body, Vector3.Zero, oneShot: false);
        if (aura == null) return;
        aura.Name = RankAuraNode;
        aura.SetMeta(RankAuraFxMeta, want);
    }

    private bool SelfHasRankAura => NationRankAura.FxIdFor(_selfPersonalRank, _selfKing) != NationRankAura.NoFx;

    private void RankAuraInit() => Net.I.RankAuraShownEvent += OnRankAuraShown;

    private void RankAuraDispose() => Net.I.RankAuraShownEvent -= OnRankAuraShown;

    private void OnRankAuraShown(int charId, bool shown)
    {
        RefreshRankAuraFor(charId, _stealthIds.Contains(charId));
        if (charId == _myId) RefreshLookToggles();
    }

    private void RefreshRankAuraFor(int charId, bool stealthed)
    {
        if (charId == _myId) RefreshRankAura(_self, _selfPersonalRank, stealthed, _selfKing, Net.I.OwnRankAuraShown);
        else if (_ents.TryGetValue(charId, out var e) && !e.IsNpc)
        {
            RefreshRankAura(e.Body, e.PersonalRank, stealthed, shown: Net.I.RankAuraShown(charId));
            string? symbol = RankSymbol.PlatePath(e.KnightsRank, e.PersonalRank);
            e.Plate?.SetRank(symbol != null ? GD.Load<Texture2D>(symbol) : null);
            e.Plate?.SetNameMark(NameMark.For(e.ClanFame, e.ClanFlag, _zone, e.Nation == Net.I.Nation));
        }
    }
}
