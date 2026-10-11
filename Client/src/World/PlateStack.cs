using Godot;

namespace LibreKO;

public sealed class PlateStack
{
    private readonly Label3D _name;
    private readonly Node _parent;

    private Label3D? _title;
    private string _titleText = "";
    private Label3D? _clan;
    private Label3D? _status;
    private Label3D? _seeking;
    private Sprite3D? _mark;
    private Sprite3D? _leader;
    private Sprite3D? _rank;
    private Sprite3D? _nameMark;
    private LibreKO.Domain.NameMarkKind _nameMarkKind;
    private const string LeaderIcon = "system/star";

    public PlateStack(Label3D name)
    {
        _name = name;
        _parent = name.GetParent();
    }

    public Label3D Name => _name;

    public void MoveTo(Vector3 position)
    {
        _name.Position = position;
        foreach (var part in new Node3D?[] { _title, _clan, _status, _seeking, _mark, _leader, _rank, _nameMark })
            if (part != null && GodotObject.IsInstanceValid(part)) part.Position = position;
    }

    public int Lines
    {
        get
        {
            int lines = 1;
            foreach (var label in new[] { _title, _clan, _status, _seeking })
                if (label != null && GodotObject.IsInstanceValid(label)) lines++;
            return lines;
        }
    }

    public void SetNameText(string text)
    {
        if (_name.Text == text) return;
        _name.Text = text;
        if (_mark != null && GodotObject.IsInstanceValid(_mark)) NamePlate.PlaceMark(_mark, _name);
        PlaceLeader();
        var kind = _nameMarkKind;
        _nameMarkKind = LibreKO.Domain.NameMarkKind.None;
        if (kind != LibreKO.Domain.NameMarkKind.None) SetNameMark(kind);
    }

    public void SetTitle(string text)
    {
        _titleText = text;
        _title = Set(_title, Config.ShowTitles ? text : "", NamePlate.MakeTitle);
        Layout();
    }

    public void RefreshTitle() => SetTitle(_titleText);

    public void SetClan(string text) => SetClan(text, NamePlate.ClanColor);

    public void SetClan(string text, Color colour)
    {
        _clan = Set(_clan, text, NamePlate.MakeClan);
        if (_clan != null) NamePlate.Tint(_clan, colour);
        Layout();
    }

    public void SetMark(Texture2D? texture)
    {
        if (texture == null)
        {
            if (_mark != null && GodotObject.IsInstanceValid(_mark)) _mark.QueueFree();
            _mark = null;
            PlaceLeader();
            return;
        }
        if (_mark == null || !GodotObject.IsInstanceValid(_mark))
        {
            _mark = NamePlate.MakeMark(texture, _name);
            _mark.Visible = _name.Visible;
            _parent.AddChild(_mark);
        }
        else _mark.Texture = texture;
        NamePlate.PlaceMark(_mark, _name);
        PlaceLeader();
    }

    public void SetLeader(bool leader)
    {
        if (!leader)
        {
            if (_leader != null && GodotObject.IsInstanceValid(_leader)) _leader.QueueFree();
            _leader = null;
            return;
        }
        if (_leader == null || !GodotObject.IsInstanceValid(_leader))
        {
            if (UiIcons.Get(LeaderIcon) is not { } icon) return;
            _leader = NamePlate.MakeMark(icon, _name);
            _leader.Modulate = NamePlate.LeaderColour;
            _leader.Visible = _name.Visible;
            _parent.AddChild(_leader);
        }
        PlaceLeader();
    }

    public void SetRank(Texture2D? texture)
    {
        if (texture == null)
        {
            if (_rank != null && GodotObject.IsInstanceValid(_rank)) _rank.QueueFree();
            _rank = null;
            PlaceLeader();
            return;
        }
        if (_rank == null || !GodotObject.IsInstanceValid(_rank))
        {
            _rank = NamePlate.MakeMark(texture, _name);
            _rank.Visible = _name.Visible;
            _parent.AddChild(_rank);
        }
        else _rank.Texture = texture;
        PlaceLeader();
    }

    public void SetNameMark(LibreKO.Domain.NameMarkKind kind)
    {
        if (kind == _nameMarkKind && (kind == LibreKO.Domain.NameMarkKind.None || _nameMark != null)) return;
        _nameMarkKind = kind;
        if (_nameMark != null && GodotObject.IsInstanceValid(_nameMark)) _nameMark.QueueFree();
        _nameMark = NamePlate.MakeNameMark(_name, kind);
        if (_nameMark == null) return;
        _nameMark.Visible = _name.Visible;
        _parent.AddChild(_nameMark);
    }

    private void PlaceLeader()
    {
        int slot = _mark != null && GodotObject.IsInstanceValid(_mark) ? 1 : 0;
        if (_rank != null && GodotObject.IsInstanceValid(_rank)) NamePlate.PlaceMark(_rank, _name, slot++);
        if (_leader == null || !GodotObject.IsInstanceValid(_leader)) return;
        NamePlate.PlaceMark(_leader, _name, slot);
    }

    public void SetSeeking(string text, Color colour)
    {
        _seeking = Set(_seeking, text, NamePlate.MakeStatus);
        if (_seeking != null) NamePlate.Tint(_seeking, colour);
        Layout();
    }

    public void SetStatus(string text, Color colour)
    {
        _status = Set(_status, text, NamePlate.MakeStatus);
        if (_status != null) _status.Modulate = colour;
        Layout();
    }

    private bool? _visible;

    public void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        if (_name.Visible != visible) _name.Visible = visible;
        if (_title != null && _title.Visible != visible) _title.Visible = visible;
        if (_clan != null && _clan.Visible != visible) _clan.Visible = visible;
        if (_status != null && _status.Visible != visible) _status.Visible = visible;
        if (_seeking != null && _seeking.Visible != visible) _seeking.Visible = visible;
        if (_mark != null && _mark.Visible != visible) _mark.Visible = visible;
        if (_leader != null && _leader.Visible != visible) _leader.Visible = visible;
        if (_rank != null && _rank.Visible != visible) _rank.Visible = visible;
        if (_nameMark != null && _nameMark.Visible != visible) _nameMark.Visible = visible;
    }

    private Label3D? Set(Label3D? label, string text, System.Func<string, float, Label3D> make)
    {
        if (text.Length == 0)
        {
            if (label != null && GodotObject.IsInstanceValid(label)) label.QueueFree();
            label = null;
        }
        else if (label == null || !GodotObject.IsInstanceValid(label))
        {
            label = make(text, _name.Position.Y);
            label.Position = _name.Position;
            label.Visible = _name.Visible;
            _parent.AddChild(label);
        }
        else
        {
            label.Text = text;
        }

        return label;
    }

    private void Layout()
    {
        int line = 1;
        foreach (var label in new[] { _title, _clan, _status, _seeking })
        {
            if (label == null || !GodotObject.IsInstanceValid(label)) continue;
            int priority = NamePlate.StackPriority + line * 2;
            NamePlate.Stack(label, line);
            label.RenderPriority = priority;
            label.OutlineRenderPriority = priority - 1;
            line++;
        }
    }
}
