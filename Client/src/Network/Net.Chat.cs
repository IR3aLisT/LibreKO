using System;

namespace LibreKO.Network;

public partial class Net
{
    public const byte SubChatTargetWhisper = 1, SubChatTargetBlock = 2, SubChatTargetClanAdmission = 4;
    private const byte ConsultClan = 0, ConsultPlayer = 1;

    public event Action<bool, string, bool>? ClanConsultationEvent;

    public void SendClanConsultation(int clanId)
    {
        var p = new Packet(GameOpcodes.GS_CHAT_TARGET);
        p.WriteByte(SubChatTargetClanAdmission);
        p.WriteByte(ConsultClan);
        p.WriteShort((short)clanId);
        _conn.Send(p);
    }

    public void SendPlayerConsultation(string name)
    {
        var p = new Packet(GameOpcodes.GS_CHAT_TARGET);
        p.WriteByte(SubChatTargetClanAdmission);
        p.WriteByte(ConsultPlayer);
        p.WriteString(name);
        _conn.Send(p);
    }

    public const byte ChatAuthorityPlayer = 1, ChatAuthorityGameMaster = 2;

    public const int ChatTargetNotFound = 0, ChatTargetConnected = 1, ChatTargetBlocked = -1,
                     ChatTargetCrossNation = -2, ChatTargetSenderBlocked = -3;

    public System.Collections.Generic.IReadOnlyList<LibreKO.Domain.RoomPost> WhisperHistory(string name) =>
        _directs.TryGetValue(name, out var timeline)
            ? timeline.Posts
            : System.Array.Empty<LibreKO.Domain.RoomPost>();

    public readonly record struct OpenWhisper(string Name, Godot.Vector2 Position, bool Minimized, bool Unread);

    private readonly System.Collections.Generic.List<OpenWhisper> _openWhispers = new();

    public System.Collections.Generic.IReadOnlyList<OpenWhisper> OpenWhispers => _openWhispers;

    public void KeepOpenWhispers(System.Collections.Generic.IEnumerable<OpenWhisper> windows)
    {
        _openWhispers.Clear();
        _openWhispers.AddRange(windows);
    }

    private string _whispersOf = "";

    private void ResetWhispersOnCharacterChange()
    {
        if (string.Equals(_whispersOf, SelectedChar, StringComparison.OrdinalIgnoreCase)) return;
        _whispersOf = SelectedChar;
        _directs.Clear();
        _openWhispers.Clear();
    }

    public void RecordWhisper(string name, LibreKO.Domain.RoomPost post) => DirectTimeline(name).Add(post);

    public event Action<ChatLine>? ChatEvent;

    public readonly record struct DeathNotice(
        int VictimNation, int KillerNation, int Kind, int KillerId, string KillerName,
        int VictimId, string VictimName, int X, int Z, bool VictimIsKing = false);

    public event Action<DeathNotice>? DeathNoticeEvent;
    public event Action<int>? KrowazNoticeEvent;
    public event Action<MonumentNotice>? MonumentNoticeEvent;
    public event Action<ChaosStoneNotice>? ChaosStoneNoticeEvent;
    public event Action<BoxItemNotice>? BoxItemNoticeEvent;

    public const byte ChatTypeDeathNotice = 26;
    private const int DeathNoticeMinBytes = 4 + 4 + 1 + 4 + 1 + 4;

    public event Action<int, string>? ChatTargetEvent;

    private void HandleChat(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte type = p.ReadByte();
        if (type == ChatTypeDeathNotice)
        {
            HandleDeathNotice(p);
            return;
        }
        if (type == ChatType.ChatRoom)
        {
            HandleRoomChat(p);
            return;
        }
        if (type == ChatType.KrowazNotice)
        {
            if (ReadKrowazSeconds(p) is { } seconds) KrowazNoticeEvent?.Invoke(seconds);
            return;
        }
        switch (type)
        {
            case ChatType.MonumentNotice:
                if (MonumentNotice.Read(p) is { } monument) MonumentNoticeEvent?.Invoke(monument);
                return;
            case ChatType.ChaosStoneNotice:
                if (ChaosStoneNotice.Read(p) is { } stone) ChaosStoneNoticeEvent?.Invoke(stone);
                return;
            case ChatType.BoxItemNotice:
                if (BoxItemNotice.Read(p) is { } box) BoxItemNoticeEvent?.Invoke(box);
                return;
        }
        int nation = p.RemainingBytes >= 1 ? p.ReadByte() : 0;
        int charId = p.RemainingBytes >= 4 ? p.ReadInt() : 0;
        string name = p.RemainingBytes >= 1 ? p.ReadSByteString() : "";
        string message = p.RemainingBytes >= 2 ? p.ReadString() : "";
        bool isGm = p.RemainingBytes >= 1 && p.ReadByte() == ChatAuthorityGameMaster;

        if (message.Length == 0) return;
        ChatEvent?.Invoke(new ChatLine(type, nation, charId, name, message, isGm));
    }

    internal static int? ReadKrowazSeconds(Packet p) => p.RemainingBytes >= 2 ? p.ReadShort() : null;

    private void HandleDeathNotice(Packet p)
    {
        if (ReadDeathNotice(p) is { } notice) DeathNoticeEvent?.Invoke(notice);
    }

    internal static DeathNotice? ReadDeathNotice(Packet p)
    {
        if (p.RemainingBytes < DeathNoticeMinBytes) return null;
        int killerNation = p.ReadByte();
        int victimNation = p.ReadByte();
        bool king = p.ReadByte() != 0;
        int kind = p.ReadByte();
        if (kind == DeathNotices.DefeatedKind) (killerNation, victimNation) = (victimNation, killerNation);
        int killerId = p.ReadInt();
        string killerName = p.ReadSByteString();
        if (p.RemainingBytes < 5) return null;
        int victimId = p.ReadInt();
        string victimName = p.ReadSByteString();
        int x = p.RemainingBytes >= 2 ? p.ReadShort() : 0;
        int z = p.RemainingBytes >= 2 ? p.ReadShort() : 0;
        return new DeathNotice(victimNation, killerNation, kind, killerId, killerName, victimId, victimName, x, z, king);
    }

    private void HandleChatTarget(Packet p)
    {
        if (p.RemainingBytes < 3) return;
        byte type = p.ReadByte();
        if (type == SubChatTargetClanAdmission)
        {
            short admission = p.ReadShort();
            string who = admission == ChatTargetConnected && p.RemainingBytes >= 2 ? p.ReadString() : "";
            bool received = p.RemainingBytes >= 1 && p.ReadByte() != 0;
            ClanConsultationEvent?.Invoke(admission == ChatTargetConnected, who, received);
            return;
        }
        if (type != SubChatTargetWhisper) return;
        short result = p.ReadShort();
        string name = p.RemainingBytes >= 2 ? p.ReadString() : "";
        ChatTargetEvent?.Invoke(result, name);
    }
}
