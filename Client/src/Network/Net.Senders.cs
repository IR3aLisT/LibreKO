using Godot;

namespace LibreKO.Network;

public partial class Net
{
    private void SendVersionCheck()
    {
        var p = new Packet(GameOpcodes.GS_VERSION_CHECK);
        p.WriteUShort((ushort)Config.ServerVersion);
        p.WriteString(Build.Version);
        _conn.Send(p);
    }

    public void Login(string account, string password)
    {
        _account = account;
        _password = password;
        var p = new Packet(GameOpcodes.GS_LOGIN);
        p.WriteString(account);
        p.WriteString(KoPassword.Encode(password));
        _conn.Send(p);
    }

    public void SelectNation(int nation)
    {
        var p = new Packet(GameOpcodes.GS_NATION_SELECT);
        p.WriteByte((byte)nation);
        _conn.Send(p);
    }

    public void CreateCharacter(int slot, string name, int race, int cls, int face, int hair,
        int str, int sta, int dex, int intel, int mag)
    {
        var p = new Packet(GameOpcodes.GS_CREATE_CHARACTER);
        p.WriteByte((byte)slot);
        p.WriteString(name);
        p.WriteByte((byte)race);
        p.WriteShort((short)cls);
        p.WriteByte((byte)face);
        p.WriteInt(hair);
        p.WriteByte((byte)str);
        p.WriteByte((byte)sta);
        p.WriteByte((byte)dex);
        p.WriteByte((byte)intel);
        p.WriteByte((byte)mag);
        _conn.Send(p);
    }

    public void RequestCharList()
    {
        var loading = new Packet(GameOpcodes.GS_LOADING_LOGIN);
        loading.WriteByte(1);
        _conn.Send(loading);

        var p = new Packet(GameOpcodes.GS_ALLCHAR_INFO_REQ);
        p.WriteByte(1);
        _conn.Send(p);
    }

    public void SelectChar(string charName)
    {
        SelectedChar = charName;
        _selectRetries = 0;
        SendSelect();
    }

    private void SendSelect()
    {
        var p = new Packet(GameOpcodes.GS_SELECT_CHARACTER);
        p.WriteString(_account);
        p.WriteString(SelectedChar);
        p.WriteByte(1);
        _conn.Send(p);
    }

    private const byte GameStartCharacterPicked = 1;
    private const byte GameStartReady = 2;

    public ulong OnlineSinceMsec { get; private set; } = Godot.Time.GetTicksMsec();

    private void SendGameStart(byte sub)
    {
        if (sub == GameStartCharacterPicked)
        {
            OnlineSinceMsec = Godot.Time.GetTicksMsec();
            GmCollisionsOff = false;
            ResetAdminGrant();
            ResetWhispersOnCharacterChange();
        }
        var p = new Packet(GameOpcodes.GS_GAMESTART);
        p.WriteByte(sub);
        p.WriteByte(0);
        p.WriteSByteString(SelectedChar);
        _conn.Send(p);
    }

    public void SendReady() => SendGameStart(GameStartReady);

    private const float MaxWireSpeed = 90f;

    public void SendMove(float x, float z, float y, float speed, byte echo = MoveEchoMove) =>
        SendMove(x, z, y, speed, echo, x, z, y);

    public void SendMove(float willX, float willZ, float willY, float speed, byte echo, float curX, float curZ, float curY)
    {
        var p = GenieActionPacket(GameOpcodes.GS_MOVE, GenieMove);
        p.WriteUShort(WirePosition(willX)); p.WriteUShort(WirePosition(willZ)); p.WriteUShort(WirePosition(willY));
        p.WriteShort((short)Mathf.Clamp(speed * 10f, -MaxWireSpeed, MaxWireSpeed));
        p.WriteByte(echo);
        p.WriteUShort(WirePosition(curX)); p.WriteUShort(WirePosition(curZ)); p.WriteUShort(WirePosition(curY));
        _conn.Send(p);
    }

    private static ushort WirePosition(float value) => (ushort)Mathf.Clamp(value * 10f, 0f, ushort.MaxValue);

    public void SendTargetHpRequest(int targetId)
    {
        var p = new Packet(GameOpcodes.GS_TARGET_HP);
        p.WriteInt(targetId);
        p.WriteByte(TargetHpPollEcho);
        _conn.Send(p);
    }

    public void SendRotate(float degrees)
    {
        var p = GenieActionPacket(GameOpcodes.GS_ROTATE, GenieRotate);
        p.WriteShort(Coord.HeadingToWire(degrees));
        _conn.Send(p);
    }

    public void SendSitting(bool sitting)
    {
        var p = new Packet(GameOpcodes.GS_STATE_CHANGE);
        p.WriteByte(StateChange.Pose);
        p.WriteInt(sitting ? UserPose.Sitting : UserPose.Standing);
        _conn.Send(p);
    }

    public void SendCombatStance(bool ready)
    {
        var p = new Packet(GameOpcodes.GS_STATE_CHANGE);
        p.WriteByte(StateChange.CombatStance);
        p.WriteInt(ready ? StateChange.StanceReady : StateChange.StanceRelaxed);
        _conn.Send(p);
    }

    public void SendAttack(int targetId, short swingDelay, int attackType = 1, bool chained = false)
    {
        var p = GenieActionPacket(GameOpcodes.GS_ATTACK, GenieMainAttack);
        p.WriteByte((byte)attackType);
        p.WriteByte(0);
        p.WriteInt(targetId);
        p.WriteShort(swingDelay);
        p.WriteShort(0);
        p.WriteByte(chained ? (byte)1 : (byte)0);
        p.WriteByte(0);
        _conn.Send(p);
    }

    public void SendMagic(int subOpcode, int skillId, int targetId, short[]? data = null)
    {
        var p = GenieActionPacket(GameOpcodes.GS_MAGIC_PROCESS, GenieMagic);
        p.WriteByte((byte)subOpcode);
        p.WriteInt(skillId);
        p.WriteInt(MyCharId);
        p.WriteInt(targetId);
        for (int i = 0; i < 7; i++)
            p.WriteInt(data != null && i < data.Length ? data[i] : 0);
        _conn.Send(p);
    }

    public void SendCastFailed(int skillId)
    {
        var data = new short[MagicFail.ResultSlot + 1];
        data[MagicFail.ResultSlot] = MagicFail.Casting;
        SendMagic(MagicSub.Fail, skillId, MyCharId, data);
    }

    public const byte RegeneTown = 1;
    public const byte RegeneResurrectChoice = 4;
    public const byte RegeneResurrectAccept = 5;
    public const byte ResurrectInPlace = 1;
    public const byte ResurrectInTown = 2;

    public void SendRegene()
    {
        var p = new Packet(GameOpcodes.GS_REGENE);
        p.WriteByte(RegeneTown);
        _conn.Send(p);
    }

    public void SendResurrectAccept(int casterId)
    {
        var p = new Packet(GameOpcodes.GS_REGENE);
        p.WriteByte(RegeneResurrectAccept);
        p.WriteInt(casterId);
        _conn.Send(p);
    }

    public void SendResurrectChoice(byte choice, int casterId)
    {
        var p = new Packet(GameOpcodes.GS_REGENE);
        p.WriteByte(RegeneResurrectChoice);
        p.WriteByte(choice);
        p.WriteInt(casterId);
        _conn.Send(p);
    }

    public void SendSkillDataSave(int[] slotIds)
    {
        int count = System.Math.Clamp(slotIds.Length, 1, SkillBarMaxSlots);
        var p = new Packet(GameOpcodes.GS_SKILLDATA);
        p.WriteByte(SkillBarSave);
        p.WriteShort((short)count);
        var bar = new int[count];
        for (int i = 0; i < count; i++)
        {
            bar[i] = i < slotIds.Length ? slotIds[i] : 0;
            p.WriteInt(bar[i]);
        }
        _conn.Send(p);
        SkillBar = bar;
    }

    public void SendSkillDataLoad()
    {
        var p = new Packet(GameOpcodes.GS_SKILLDATA);
        p.WriteByte(SkillBarLoad);
        _conn.Send(p);
    }

    public void SendPointChange(int type)
    {
        var p = new Packet(GameOpcodes.GS_POINT_CHANGE);
        p.WriteByte((byte)type);
        p.WriteShort(1);
        _conn.Send(p);
    }

    public void SendInventoryArrange()
    {
        var p = new Packet(GameOpcodes.GS_ITEM_MOVE);
        p.WriteByte(ItemMove.ArrangeRequest);
        _conn.Send(p);
    }

    public void SendItemMove(byte direction, int itemId, byte sourcePos, byte destPos, ushort amount)
    {
        if (!_conn.Connected) { ItemMoveResultEvent?.Invoke(false); return; }
        _pendingItemMove = new PendingItemMove(direction, sourcePos, destPos, amount);
        _conn.Send(ItemMoveWire.Move(direction, itemId, sourcePos, destPos, amount));
    }

    public void SendItemRemove(byte type, byte position, int itemId)
    {
        if (!_conn.Connected) { ItemRemoveResultEvent?.Invoke(false, ItemRemoveNoReason); return; }
        var p = new Packet(GameOpcodes.GS_ITEM_REMOVE);
        p.WriteByte(type);
        p.WriteByte(position);
        p.WriteInt(itemId);
        _pendingRemoveSlot = type switch
        {
            ItemRemove.FromEquipment => position,
            ItemRemove.FromMagicBag => InventoryConstants.MagicBagStart + position,
            _ => InventoryConstants.InventoryStart + position,
        };
        _conn.Send(p);
    }

    public void SendBundleOpen(int bundleId)
    {
        var p = new Packet(GameOpcodes.GS_BUNDLE_OPEN_REQ);
        p.WriteInt(bundleId);
        _conn.Send(p);
    }

    public void SendItemGet(int bundleId, int itemId, int bundleSlot)
    {
        var p = new Packet(GameOpcodes.GS_ITEM_GET);
        p.WriteInt(bundleId);
        p.WriteInt(itemId);
        p.WriteUShort((ushort)bundleSlot);
        _conn.Send(p);
    }

    public void SendItemDrop(byte gridPos, int itemId, ushort count)
    {
        var p = new Packet(GameOpcodes.GS_ITEM_DROP);
        p.WriteByte(gridPos);
        p.WriteInt(itemId);
        p.WriteUShort(count);
        _conn.Send(p);
    }

    public void SendGoTown()
    {
        _conn.Send(new Packet(GameOpcodes.GS_HOME));
    }

    public const int ChatMessageMaxLength = 128;

    public void SendChat(string message, byte type = 1)
    {
        if (string.IsNullOrEmpty(message)) return;
        message = Domain.TextRunes.Truncate(message, ChatMessageMaxLength);
        var p = new Packet(GameOpcodes.GS_CHAT);
        p.WriteByte(type);
        p.WriteString(message);
        _conn.Send(p);
    }

    public void SendChatTarget(string targetName)
    {
        if (string.IsNullOrEmpty(targetName)) return;
        if (targetName.Length > 20) targetName = targetName.Substring(0, 20);
        var p = new Packet(GameOpcodes.GS_CHAT_TARGET);
        p.WriteByte(SubChatTargetWhisper);
        p.WriteString(targetName);
        _conn.Send(p);
    }

    public bool WhispersBlocked { get; private set; }
    public bool TimedItemsOffered { get; set; }

    public bool GmCollisionsOff { get; set; }

    public void SendChatBlock(bool block)
    {
        WhispersBlocked = block;
        var p = new Packet(GameOpcodes.GS_CHAT_TARGET);
        p.WriteByte(SubChatTargetBlock);
        p.WriteByte(block ? (byte)1 : (byte)0);
        _conn.Send(p);
    }

    public void SendZoneEnterAck()
    {
        var p = new Packet(GameOpcodes.GS_ZONE_CHANGE);
        p.WriteByte(1);
        _conn.Send(p);
    }

    private void SendZoneFinish()
    {
        var p = new Packet(GameOpcodes.GS_ZONE_CHANGE);
        p.WriteByte(2);
        _conn.Send(p);
    }

    public void SendPartyCreate(string targetName, byte boardType = 0)
    {
        if (string.IsNullOrEmpty(targetName)) return;
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyRequest.Create);
        p.WriteString(targetName);
        p.WriteByte(boardType);
        p.WriteByte(PartyRowMeterMode);
        p.WriteByte(PartyBoardMeterMode);
        _conn.Send(p);
    }

    public void SendPartyInvite(string targetName, byte boardType = 0) => SendPartyInviteName(PartyRequest.Insert, targetName, boardType);

    private void SendPartyInviteName(byte sub, string targetName, byte boardType)
    {
        if (string.IsNullOrEmpty(targetName)) return;
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(sub);
        p.WriteString(targetName);
        p.WriteByte(boardType);
        _conn.Send(p);
    }

    public void SendPartyAnswer(bool accept)
    {
        _partyJoining = accept && !InParty;
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyRequest.Permit);
        p.WriteByte(accept ? (byte)1 : (byte)0);
        _conn.Send(p);
    }

    public void SendPartyLeave(int memberId)
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyRequest.Remove);
        p.WriteInt(memberId);
        _conn.Send(p);
    }

    public void SendPartyDisband()
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyRequest.Delete);
        _conn.Send(p);
    }

    public void SendPartyCommander(int memberId)
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyRequest.CommanderAppoint);
        p.WriteInt(memberId);
        _conn.Send(p);
    }

    public void SendCommanderMark(int targetId, bool clear)
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyRequest.CommanderMark);
        p.WriteInt(targetId);
        p.WriteInt(CommanderOrders.MarkingSkill);
        p.WriteByte((byte)(clear ? 1 : 0));
        _conn.Send(p);
    }

    public void SendCommanderOrder(byte order)
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyRequest.CommanderOrder);
        p.WriteByte(order);
        p.WriteInt(CommanderOrders.OrderSkill);
        _conn.Send(p);
    }

    public void SendPartyPromote(int newLeaderId)
    {
        var p = new Packet(GameOpcodes.GS_PARTY);
        p.WriteByte(PartyRequest.Promote);
        p.WriteInt(newLeaderId);
        _conn.Send(p);
    }

    private const byte PartyBbsModeNormal = 0;
    private const byte PartyBbsListSub = 0x0B;

    public void SendPartyBbsRegister(byte board = PartyBbsModeNormal)
    {
        var p = new Packet(GameOpcodes.GS_PARTY_BBS);
        p.WriteByte(board);
        p.WriteByte(0x01);
        _conn.Send(p);
    }

    public void SendPartyBbsInvite(string name, byte board = PartyBbsModeNormal) =>
        SendPartyBbsName(PartyBoardInvite.Sub, name, board);

    public void SendPartyBbsAccept(string inviter, byte board = PartyBbsModeNormal) =>
        SendPartyBbsName(PartyBoardInvite.AcceptSub, inviter, board);

    private void SendPartyBbsName(byte sub, string name, byte board)
    {
        var p = new Packet(GameOpcodes.GS_PARTY_BBS);
        p.WriteByte(board);
        p.WriteByte(sub);
        p.WriteString(name);
        _conn.Send(p);
    }

    public void SendPartyBbsDelete(byte board = PartyBbsModeNormal)
    {
        var p = new Packet(GameOpcodes.GS_PARTY_BBS);
        p.WriteByte(board);
        p.WriteByte(0x02);
        _conn.Send(p);
    }

    public void SendPartyBbsList(int pageIndex, byte board = PartyBbsModeNormal)
    {
        var p = new Packet(GameOpcodes.GS_PARTY_BBS);
        p.WriteByte(board);
        p.WriteByte(PartyBbsListSub);
        p.WriteShort((short)System.Math.Max(0, pageIndex));
        p.WriteByte(0);
        p.WriteShort(0);
        p.WriteByte(0);
        _conn.Send(p);
    }

    public void SendPartyBbsWanted(int wantedClass, int pageIndex, string message, byte board = PartyBbsModeNormal)
    {
        var p = new Packet(GameOpcodes.GS_PARTY_BBS);
        p.WriteByte(board);
        p.WriteByte(0x04);
        p.WriteShort((short)wantedClass);
        p.WriteShort((short)System.Math.Max(0, pageIndex));
        p.WriteSByteString(message ?? "");
        _conn.Send(p);
    }

    public const byte NpcEventTouch = 2;

    public void SendNpcTouch(int npcUniqueId)
    {
        var p = new Packet(GameOpcodes.GS_NPC_EVENT);
        p.WriteByte(NpcEventTouch);
        p.WriteInt(npcUniqueId);
        p.WriteInt(-1);
        _conn.Send(p);
    }

    public void SendNpcEvent(int npcUniqueId)
    {
        var p = new Packet(GameOpcodes.GS_NPC_EVENT);
        p.WriteByte(0);
        p.WriteInt(npcUniqueId);
        p.WriteInt(-1);
        _conn.Send(p);
    }

    public void SendClientEvent(int npcUniqueId)
    {
        var p = new Packet(GameOpcodes.GS_CLIENT_EVENT);
        p.WriteInt(npcUniqueId);
        _conn.Send(p);
    }

    public void SendSelectMsg(int menuIndex, string luaFile, int selectedReward = -1)
    {
        var p = new Packet(GameOpcodes.GS_SELECT_MSG);
        p.WriteByte((byte)menuIndex);
        p.WriteSByteString(luaFile ?? "");
        p.WriteByte((byte)(sbyte)selectedReward);
        _conn.Send(p);
    }

    public void SendQuestLogRequest()
    {
        var p = new Packet(GameOpcodes.GS_QUEST);
        p.WriteByte(1);
        _conn.Send(p);
    }

    public void SendQuestAccept(int questId) => SendQuestAction(3, questId);

    public void SendQuestComplete(int questId, int chosenReward = -1)
    {
        var p = new Packet(GameOpcodes.GS_QUEST);
        p.WriteByte(4);
        p.WriteInt(questId);
        p.WriteByte((byte)(chosenReward is >= 0 and < 255 ? chosenReward : 255));
        _conn.Send(p);
    }

    public void SendQuestAbandon(int questId) => SendQuestAction(5, questId);

    public void SendQuestNotificationReply(int questId, int choice)
    {
        var packet = new Packet(GameOpcodes.GS_QUEST);
        packet.WriteByte(17);
        packet.WriteInt(questId);
        packet.WriteShort((short)choice);
        _conn.Send(packet);
    }

    public void SendQuestTargetRequest(int questId, int group)
    {
        var packet = new Packet(GameOpcodes.GS_QUEST);
        packet.WriteByte((byte)QuestSub.TargetDetail);
        packet.WriteInt(questId);
        packet.WriteByte((byte)group);
        _conn.Send(packet);
    }

    private void SendQuestAction(byte sub, int questId)
    {
        var p = new Packet(GameOpcodes.GS_QUEST);
        p.WriteByte(sub);
        p.WriteInt(questId);
        _conn.Send(p);
    }

    public void SendQuestKillCountRequest(int questId)
    {
        var p = new Packet(GameOpcodes.GS_QUEST);
        p.WriteByte(9);
        p.WriteByte(1);
        p.WriteShort((short)questId);
        _conn.Send(p);
    }

    private void SendPing()
    {
        var p = new Packet(GameOpcodes.GS_PING);
        p.WriteLong((long)Time.GetTicksMsec());
        _conn.Send(p);
    }
}
