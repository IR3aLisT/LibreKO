using System;
using System.Collections.Generic;
using Godot;
using LibreKO.Network;
using LibreKO.Plugins;

namespace LibreKO;

public partial class World
{
    private const string FriendAddTitle = "Friends";
    private const string FriendAddPrompt = "Character name to add:";
    private const string FriendRemovePrompt = "Remove {0} from your friends?";
    private const int FriendNameMax = 20;

    private void PluginFriendsInit() => PluginHost.Game.AttachFriends(new PluginFriendsBridge(this));

    private void AskFriendAdd() =>
        Notice.AskText(this, FriendAddPrompt, typed =>
        {
            string name = typed.Trim();
            if (name.Length == 0) return;
            if (name == (Net.I.LastEnter.Name ?? "")) { SetFriendStatus("You can't add yourself.", true); return; }
            RequestFriend(name);
        }, FriendAddTitle, placeholder: "Character name", maxLength: FriendNameMax);

    private sealed class PluginFriendsBridge(World w) : IGameFriends
    {
        event Action? IGameFriends.Changed { add { } remove { } }

        public IReadOnlyList<GameFriend> Entries
        {
            get
            {
                w.EnsureFriendsLoaded();
                var list = new List<GameFriend>(w._friends.Count);
                foreach (var f in w._friends)
                    list.Add(new GameFriend(f.Name, f.IsOnline, f.InParty, f.Level,
                        f.Class > 0 ? World.ClassName(f.Class) : "", f.IsOnline ? FriendZoneName(f.ZoneId) : ""));
                return list;
            }
        }

        public void Refresh() => Net.I.SendFriendListRequest();

        public void Add() => w.AskFriendAdd();

        public void Remove(string name) =>
            Notice.Confirm(w, string.Format(FriendRemovePrompt, name), "Remove", "Cancel", () => Net.I.SendFriendRemove(name));

        public void Whisper(string name) => w.OpenWhisperWith(name);

        public void InviteToParty(string name) => w.InvitePlayerToParty(name);

        public void ShowPlayerMenu(string name, Vector2 at) => w.ShowPlayerMenuByName(name, at);
    }
}
