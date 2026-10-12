using System;
using System.Collections.Generic;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<PartyMember>? PartyMemberEvent;

    public event Action<int>? PartyErrorEvent;

    public event Action<int, string>? PartyInviteEvent;

    public event Action<int, string>? PartyRemovedEvent;

    public event Action<int, int, int, int, int>? PartyMemberStatsEvent;

    public event Action<int, int>? PartyMemberLevelEvent;

    public event Action<int, int>? PartyMemberClassEvent;

    public event Action<int, byte, bool>? PartyMemberStatusEvent;

    public event Action<int, IReadOnlyList<int>>? PartyMemberBuffsEvent;

    public event Action? PartySignalEvent;

    public event Action<int, int>? PartyLevelProgressEvent;

    public event Action? PartyDisbandEvent;
    public event Action<int>? PartyRejoinForgetEvent;
    public event Action<UserVoteAsk>? UserVoteAskEvent;
    public event Action<short>? UserVoteRefusedEvent;
    public event Action<UserVoteOutcome>? UserVoteResultEvent;
    public (short Warnings, short Limit)? PendingVoteNotice { get; set; }

    public void SendUserVote(int targetId, byte reason, string text)
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyRequest.UserVote);
        p.WriteInt(targetId);
        p.WriteByte(reason);
        if (reason == Domain.UserVote.ReasonOther) p.WriteString(text);
        _conn.Send(p);
    }

    public void SendPartySignalEcho(uint token) => _conn.Send(new PartySignalProbe(token).Echo());

    public void SendUserVoteAnswer(byte answer)
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyRequest.UserVoteAnswer);
        p.WriteByte(answer);
        _conn.Send(p);
    }

    public event Action<CommanderNamedReply>? PartyCommanderEvent;

    public event Action<CommanderMarkReply>? CommanderMarkEvent;

    public event Action<byte>? CommanderOrderEvent;

    public int PartyCommanderId { get; private set; } = -1;

    public event Action<bool>? PartyBbsRegisterEvent;

    public event Action? PartyBbsDeleteEvent;

    public event Action<int, int, List<PartyBbsEntry>>? PartyBbsListEvent;

    public event Action? PartyBbsWantedFailEvent;
    public event Action<PartyBoardInvite>? PartyBbsInviteEvent;

    private readonly List<PartyMember> _party = new();
    private readonly Dictionary<int, HashSet<byte>> _partyStatus = new();
    private readonly Dictionary<int, IReadOnlyList<int>> _partyBuffs = new();
    private readonly Dictionary<int, int> _partyPings = new();
    private readonly Dictionary<int, int> _partyLevelPermille = new();
    private bool _partyJoining;
    public byte PartyType { get; private set; }
    public byte PartyBoardMode { get; private set; }
    private const int MemberTailBytes = 2 + sizeof(int) + 1;

    public IReadOnlyList<PartyMember> Party => _party;

    public bool InParty => _party.Count > 0;

    public IReadOnlyCollection<byte>? PartyStatusOf(int charId) =>
        _partyStatus.TryGetValue(charId, out var status) ? status : null;

    public IReadOnlyList<int> PartyBuffsOf(int charId) =>
        _partyBuffs.TryGetValue(charId, out var skillIds) ? skillIds : Array.Empty<int>();

    public int PartyPingOf(int charId) =>
        _partyPings.TryGetValue(charId, out int ms) ? ms : Domain.PartySignal.Unknown;

    public int? PartyLevelPermilleOf(int charId) =>
        _partyLevelPermille.TryGetValue(charId, out int permille) ? permille : null;

    public void SeedPartyPreview(params PartyMember[] members)
    {
        _party.Clear();
        _party.AddRange(members);
    }

    public void SeedPartyMemberPreview(int charId, int pingMs, int levelPermille, IReadOnlyList<int> skillIds)
    {
        _partyPings[charId] = pingMs;
        _partyLevelPermille[charId] = levelPermille;
        _partyBuffs[charId] = skillIds;
    }

    public void SeedPartyCommanderPreview(int charId) => PartyCommanderId = charId;

    private void ClearParty(string reason)
    {
        if (_party.Count > 0) Godot.GD.Print($"[party] roster cleared: {reason}");
        _party.Clear();
        _partyStatus.Clear();
        _partyBuffs.Clear();
        _partyPings.Clear();
        _partyLevelPermille.Clear();
        _partyJoining = false;
        PartyCommanderId = -1;
    }

    private PartyMember SelfPartyMember() =>
        new(MyCharId, 1, LastEnter.Name is { Length: > 0 } name ? name : "You",
            Vitals.MaxHp, Vitals.Hp, Sheet.Level, LastEnter.Class, Vitals.MaxMp, Vitals.Mp);

    private void ApplyPartyMember(PartyMember m)
    {
        if (m.CharId == MyCharId) _partyJoining = false;
        if (m.BecameLeader)
        {
            _party.RemoveAll(x => x.CharId == m.CharId);
            _party.Insert(0, m);
        }
        else
        {
            int idx = _party.FindIndex(x => x.CharId == m.CharId);
            if (idx >= 0) _party[idx] = m;
            else _party.Add(m);
        }

        if (!_partyJoining && _party.FindIndex(x => x.CharId == MyCharId) < 0)
            _party.Insert(0, SelfPartyMember());
    }

    private void UpdatePartyMember(int charId, Func<PartyMember, PartyMember> update)
    {
        int idx = _party.FindIndex(x => x.CharId == charId);
        if (idx >= 0) _party[idx] = update(_party[idx]);
    }

    public static class PartyRequest
    {
        public const byte Create = 1, Permit = 2, Insert = 3, Remove = 4, Delete = 5, Promote = 28,
            CommanderAppoint = 30, CommanderMark = 31, CommanderOrder = 32, UserVote = 33, UserVoteAnswer = 34;
    }

    public const byte SubPartyInvite = 2, SubPartyMemberInfo = 3, SubPartyMemberLeft = 4,
                      SubPartyDisband = 5, SubPartyVitals = 6, SubPartyLevel = 7,
                      SubPartyClass = 8, SubPartyStatusEffect = 9, SubPartyCommander = 30,
                      SubPartyCommanderMark = 31, SubPartyCommanderOrder = 32, SubPartyRejoinForget = 39,
                      SubPartyUserVote = 33, SubPartyUserVoteResult = 35, SubPartyMemberBuffs = 64,
                      SubPartySignal = 65, SubPartySignalTable = 66, SubPartyLevelProgress = 67;

    public const short PartyStatusMemberRecord = 1;

    private void HandleParty(Packet p)
    {
        if (p.RemainingBytes < 1) return;
        byte sub = p.ReadByte();
        switch (sub)
        {
            case SubPartyMemberInfo:
            {
                if (p.RemainingBytes < 2) return;
                short status = p.ReadShort();
                if (status != PartyStatusMemberRecord)
                {
                    _partyJoining = false;
                    PartyErrorEvent?.Invoke(status);
                    return;
                }

                if (p.RemainingBytes < 5) return;
                int id = p.ReadInt();
                byte successCode = p.ReadByte();
                string name = p.RemainingBytes >= 2 ? p.ReadString() : "";
                int maxHp = p.RemainingBytes >= 2 ? p.ReadShort() : 0;
                int hp = p.RemainingBytes >= 2 ? p.ReadShort() : 0;
                int level = p.RemainingBytes >= 1 ? p.ReadByte() : 0;
                int cls = p.RemainingBytes >= 2 ? p.ReadShort() : 0;
                int maxMp = p.RemainingBytes >= 2 ? p.ReadShort() : 0;
                int mp = p.RemainingBytes >= 2 ? p.ReadShort() : 0;
                if (p.RemainingBytes >= MemberTailBytes)
                {
                    p.ReadByte();
                    p.ReadByte();
                    int marked = p.ReadInt();
                    PartyType = p.ReadByte();
                    if (id == MyCharId && marked >= 0) CommanderMarkEvent?.Invoke(new CommanderMarkReply(marked, false));
                }
                var member = new PartyMember(id, successCode, name, maxHp, hp, level, cls, maxMp, mp);
                ApplyPartyMember(member);
                PartyMemberEvent?.Invoke(member);
                break;
            }
            case SubPartyInvite:
            {
                if (p.RemainingBytes < 4) return;
                int inviterId = p.ReadInt();
                string inviterName = p.RemainingBytes >= 2 ? p.ReadString() : "";
                PartyInviteEvent?.Invoke(inviterId, inviterName);
                break;
            }
            case SubPartyMemberLeft:
            {
                if (p.RemainingBytes < 4) return;
                int removedId = p.ReadInt();
                int idx = _party.FindIndex(x => x.CharId == removedId);
                string removedName = idx >= 0 ? _party[idx].Name : "";
                if (idx >= 0) _party.RemoveAt(idx);
                _partyStatus.Remove(removedId);
                _partyBuffs.Remove(removedId);
                _partyPings.Remove(removedId);
                _partyLevelPermille.Remove(removedId);
                if (removedId == MyCharId) ClearParty("the server removed us from the party (GS_PARTY sub 4)");
                if (removedId == PartyCommanderId) PartyCommanderId = -1;
                PartyRemovedEvent?.Invoke(removedId, removedName);
                break;
            }
            case SubPartyDisband:
                ClearParty("the server disbanded the party (GS_PARTY sub 5)");
                PartyDisbandEvent?.Invoke();
                break;

            case SubPartyVitals:
            {
                if (p.RemainingBytes < 12) return;
                int cid = p.ReadInt();
                int maxHp = p.ReadShort(); int hp = p.ReadShort();
                int maxMp = p.ReadShort(); int mp = p.ReadShort();
                UpdatePartyMember(cid, m => new PartyMember(
                    m.CharId, m.SuccessCode, m.Name, maxHp, hp, m.Level, m.Class, maxMp, mp));
                PartyMemberStatsEvent?.Invoke(cid, maxHp, hp, maxMp, mp);
                break;
            }
            case SubPartyLevel:
            {
                if (p.RemainingBytes < 5) return;
                int cid = p.ReadInt();
                int level = p.ReadByte();
                UpdatePartyMember(cid, m => new PartyMember(
                    m.CharId, m.SuccessCode, m.Name, m.MaxHp, m.Hp, level, m.Class, m.MaxMp, m.Mp));
                PartyMemberLevelEvent?.Invoke(cid, level);
                break;
            }
            case SubPartyClass:
            {
                if (p.RemainingBytes < 6) return;
                int cid = p.ReadInt();
                int cls = p.ReadShort();
                UpdatePartyMember(cid, m => new PartyMember(
                    m.CharId, m.SuccessCode, m.Name, m.MaxHp, m.Hp, m.Level, cls, m.MaxMp, m.Mp));
                PartyMemberClassEvent?.Invoke(cid, cls);
                break;
            }
            case SubPartyStatusEffect:
            {
                if (p.RemainingBytes < 6) return;
                int cid = p.ReadInt();
                byte statusType = p.ReadByte();
                bool applied = p.ReadByte() != 0;
                if (statusType != 0)
                {
                    if (!_partyStatus.TryGetValue(cid, out var set))
                    {
                        set = new HashSet<byte>();
                        _partyStatus[cid] = set;
                    }
                    if (applied) set.Add(statusType); else set.Remove(statusType);
                }
                PartyMemberStatusEvent?.Invoke(cid, statusType, applied);
                break;
            }
            case SubPartyMemberBuffs:
                if (PartyMemberBuffs.Read(p) is { } buffs)
                {
                    _partyBuffs[buffs.CharId] = buffs.SkillIds;
                    PartyMemberBuffsEvent?.Invoke(buffs.CharId, buffs.SkillIds);
                }
                break;
            case SubPartySignal:
                if (PartySignalProbe.Read(p) is { } probe) SendPartySignalEcho(probe.Token);
                break;
            case SubPartySignalTable:
                if (PartySignalTable.Read(p) is { } table)
                {
                    foreach (var entry in table) _partyPings[entry.CharId] = entry.Ms;
                    PartySignalEvent?.Invoke();
                }
                break;
            case SubPartyLevelProgress:
                if (PartyLevelProgress.Read(p) is { } progress)
                {
                    _partyLevelPermille[progress.CharId] = progress.Permille;
                    PartyLevelProgressEvent?.Invoke(progress.CharId, progress.Permille);
                }
                break;
            case SubPartyWarBoard:
                HandlePartyWarBoard(p);
                break;
            case SubPartyRejoinForget:
                if (p.RemainingBytes >= 4) PartyRejoinForgetEvent?.Invoke(p.ReadInt());
                break;
            case SubPartyUserVote:
            {
                var (result, ask) = UserVoteReply.ReadStart(p);
                if (ask is { } started) UserVoteAskEvent?.Invoke(started);
                else UserVoteRefusedEvent?.Invoke(result);
                break;
            }
            case SubPartyUserVoteResult:
            {
                var outcome = UserVoteReply.ReadResult(p);
                if (outcome.Counted) PendingVoteNotice = (outcome.Warnings, outcome.Limit);
                UserVoteResultEvent?.Invoke(outcome);
                break;
            }
            case SubPartyCommander:
                if (CommanderNamedReply.Read(p) is { } named)
                {
                    PartyCommanderId = named.CharId;
                    PartyCommanderEvent?.Invoke(named);
                }
                break;
            case SubPartyCommanderMark:
                if (CommanderMarkReply.Read(p) is { } mark) CommanderMarkEvent?.Invoke(mark);
                break;
            case SubPartyCommanderOrder:
                if (p.RemainingBytes >= 1) CommanderOrderEvent?.Invoke(p.ReadByte());
                break;

        }
    }

    private void HandlePartyBbs(Packet p)
    {
        if (p.RemainingBytes < 2) return;
        PartyBoardMode = p.ReadByte();
        byte sub = p.ReadByte();
        switch (sub)
        {
            case 0x01:
                PartyBbsRegisterEvent?.Invoke(p.RemainingBytes >= 1 && p.ReadByte() != 0);
                break;
            case 0x02:
                PartyBbsDeleteEvent?.Invoke();
                break;
            case 0x04:
                if (p.RemainingBytes >= 1 && p.ReadByte() == 0) PartyBbsWantedFailEvent?.Invoke();
                break;
            case 0x0B:
                ParseBbsList(p);
                break;
            case PartyBoardInvite.Sub:
                if (PartyBoardInvite.Read(p) is { } invite) PartyBbsInviteEvent?.Invoke(invite);
                break;
        }
    }

    private void ParseBbsList(Packet p)
    {
        if (p.RemainingBytes < 7) return;
        p.ReadByte();
        int pageIndex = p.ReadShort();
        int count = p.ReadShort();
        int totalPages = p.ReadShort();

        var entries = new List<PartyBbsEntry>(count);
        for (int i = 0; i < count; i++)
        {
            if (p.RemainingBytes < 15) break;
            string name = p.ReadString();
            int classOrWanted = p.ReadInt();
            int level = p.ReadByte();
            p.ReadByte();
            byte type = p.ReadByte();
            string message = p.ReadSByteString();
            int zoneId = p.ReadShort();
            int memberCount = p.ReadByte();
            int nation = p.ReadByte();
            p.ReadByte();
            if (type == 2 || type == 3)
                entries.Add(new PartyBbsEntry(name, classOrWanted, level, type, message, zoneId, memberCount, nation));
        }

        PartyBbsListEvent?.Invoke(pageIndex, totalPages, entries);
    }
}
