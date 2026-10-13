using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    private CanvasLayer _reactionLayer = null!;
    private ReactionBar? _reactionBar;
    private ReactionDetails? _reactionDetails;
    private EmoticonPicker? _reactionPicker;
    private RoomPost? _reactionPost;
    private ChatThreadView? _reactionThread;
    private Rect2 _reactionAnchor;

    private void ChatReactionsInit()
    {
        _reactionLayer = new CanvasLayer { Layer = 80 };
        _reactionLayer.SetMeta(Audio.QuietMeta, true);
        AddChild(_reactionLayer);
        Net.I.ChatPostChangedEvent += OnChatPostChanged;
    }

    private void ChatReactionsDispose()
    {
        Net.I.ChatPostChangedEvent -= OnChatPostChanged;
    }

    private void WireChatThread(ChatThreadView thread)
    {
        thread.NameClicked += ShowPlayerMenuByName;
        thread.ItemClicked += ShowChatItemTip;
        thread.ItemHoverEnded += HideItemTooltip;
        thread.ReactRequested += (post, anchor) => OpenReactionBar(thread, post, anchor);
        thread.ReactionsClicked += (post, anchor) => OpenReactionDetails(thread, post, anchor);
    }

    private void EnsureReactionPopups()
    {
        if (_reactionBar != null) return;
        _reactionBar = new ReactionBar();
        _reactionBar.Picked += React;
        _reactionBar.More += OpenReactionPicker;
        _reactionBar.PopupHide += ReleaseReactionPin;
        _reactionLayer.AddChild(_reactionBar);

        _reactionDetails = new ReactionDetails();
        _reactionDetails.AddRequested += () =>
        {
            if (_reactionThread != null && _reactionPost != null)
            {
                _reactionThread.Pinned = _reactionPost;
                _reactionThread.ShowReactButton(_reactionPost);
            }
            _reactionBar.ShowAbove(_reactionAnchor, _reactionPost?.Reactions.Of(_myId) ?? "");
        };
        _reactionDetails.RemoveMine += () =>
        {
            React("");
            _reactionDetails.Hide();
        };
        _reactionLayer.AddChild(_reactionDetails);

        _reactionPicker = new EmoticonPicker();
        _reactionPicker.Picked += React;
        _reactionPicker.PopupHide += ReleaseReactionPin;
        _reactionLayer.AddChild(_reactionPicker);
    }

    private void OpenReactionBar(ChatThreadView thread, RoomPost post, Control anchor)
    {
        EnsureReactionPopups();
        if (_reactionThread != null && !ReferenceEquals(_reactionThread, thread)) _reactionThread.Unpin();
        _reactionThread = thread;
        _reactionPost = post;
        _reactionAnchor = anchor.GetGlobalRect();
        _reactionBar!.ShowAbove(_reactionAnchor, post.Reactions.Of(_myId));
    }

    private void OpenReactionDetails(ChatThreadView thread, RoomPost post, Control anchor)
    {
        EnsureReactionPopups();
        _reactionThread = thread;
        _reactionPost = post;
        _reactionAnchor = anchor.GetGlobalRect();
        _reactionDetails!.ShowFor(post, _myId, _reactionAnchor);
    }

    private void OpenReactionPicker()
    {
        if (_reactionPicker == null) return;
        _reactionThread!.Pinned = _reactionPost;
        _reactionPicker.ShowAt(_reactionAnchor.Position);
    }

    private void ReleaseReactionPin()
    {
        if (_reactionPicker is { Visible: true } || _reactionBar is { Visible: true }) return;
        _reactionThread?.Unpin();
    }

    private void React(string emoji)
    {
        if (_reactionPost is not { Id: > 0 } post) return;
        Net.I.SendReaction(post.Id, ReactionRules.Toggle(post.Reactions.Of(_myId), emoji));
    }

    private void OnChatPostChanged(RoomPost post)
    {
        if (IsInstanceValid(_roomThread)) _roomThread.Refresh(post);
        foreach (var chat in _whispers.Values)
            if (IsInstanceValid(chat.Thread)) chat.Thread.Refresh(post);
        if (_reactionDetails is { Visible: true } details && ReferenceEquals(details.Post, post)) details.Refresh();
    }
}
