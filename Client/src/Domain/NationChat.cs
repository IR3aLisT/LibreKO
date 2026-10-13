using System;

namespace LibreKO.Domain;

public static class NationChatTexts
{
    public const int ButtonTip = 30900;
    public const int PublicRefreshTip = 30901;
    public const int PrivateRefreshTip = 30902;
    public const int Entered = 30930;
    public const int Left = 30931;
    public const int NewOwner = 30932;
    public const int Kicked = 30933;
    public const int AlreadyInRoom = 30934;
    public const int SelectRoom = 30935;
    public const int NationMismatch = 30936;
    public const int RefreshWait = 30937;
    public const int CreateWhileInRoom = 30938;
    public const int Created = 30939;
    public const int NoRoom = 30940;
    public const int NoSpace = 30941;
    public const int PasswordLength = 30942;
    public const int PasswordWrong = 30943;
    public const int NoPassword = 30944;
    public const int NoTitle = 30945;
    public const int TitleLength = 30946;
    public const int CannotCreate = 30947;
    public const int OptionsChanged = 30948;
    public const int NotOwner = 30949;
    public const int SearchDone = 30950;
    public const int SearchNone = 30951;
    public const int NoTarget = 30952;
    public const int KickSelf = 30953;
    public const int YouWereKicked = 30954;
    public const int YouLeft = 30955;
    public const int Public = 30956;
    public const int Private = 30957;
    public const int NotInRoom = 30958;
}

public static class NationChatRules
{
    public const int PublicCapacity = 500;
    public const int TitleMaxLength = 30;
    public const int PasswordMaxLength = 8;
    public const int PublicRows = 16;
    public const int PrivateRows = 20;
    public const double RefreshCooldownSeconds = 10.0;
    public const int NoRoom = -1;
    public const byte KickedCode = 5;
    public const byte MembersNoRoomCode = 4;
    public static readonly int[] RoomSizes = [10, 30, 50, 100, 150, 200];

    public static int PublicEnterText(byte result) => result switch
    {
        1 => NationChatTexts.AlreadyInRoom,
        2 => NationChatTexts.NoRoom,
        5 => NationChatTexts.NoSpace,
        _ => 0,
    };

    public static int CreateText(byte result) => result switch
    {
        1 => NationChatTexts.Created,
        2 => NationChatTexts.TitleLength,
        3 => NationChatTexts.PasswordLength,
        4 => NationChatTexts.CannotCreate,
        _ => 0,
    };

    public static int PrivateEnterText(byte result) => result switch
    {
        1 => NationChatTexts.AlreadyInRoom,
        2 => NationChatTexts.NoRoom,
        3 => NationChatTexts.PasswordLength,
        4 => NationChatTexts.PasswordWrong,
        5 => NationChatTexts.NationMismatch,
        6 => NationChatTexts.NoSpace,
        _ => 0,
    };

    public static int SearchText(byte result) => result switch
    {
        0 => NationChatTexts.SearchDone,
        1 => NationChatTexts.SearchNone,
        2 => NationChatTexts.TitleLength,
        _ => 0,
    };

    public static int OptionsText(byte result) => result switch
    {
        0 => NationChatTexts.OptionsChanged,
        1 => NationChatTexts.NotOwner,
        2 => NationChatTexts.TitleLength,
        3 => NationChatTexts.PasswordLength,
        4 => NationChatTexts.NoRoom,
        _ => 0,
    };

    public static int HandOverText(byte result) => result switch
    {
        1 => NationChatTexts.NotOwner,
        2 => NationChatTexts.NoTarget,
        4 => NationChatTexts.NoRoom,
        _ => 0,
    };

    public static int KickText(byte result) => result switch
    {
        1 => NationChatTexts.NotOwner,
        2 => NationChatTexts.KickSelf,
        3 => NationChatTexts.NoTarget,
        4 => NationChatTexts.NoRoom,
        5 => NationChatTexts.YouWereKicked,
        _ => 0,
    };

    public static string Validate(string title, bool isPrivate, string password, Func<int, string, string> text) =>
        title.Trim().Length == 0 ? text(NationChatTexts.NoTitle, "Room name was not entered.")
        : isPrivate && password.Length == 0 ? text(NationChatTexts.NoPassword, "Password was not entered.")
        : "";

    public static string Row(string name, int members, int capacity) => $"{name}({members}/{capacity})";

    public static string Title(string name, int members, int capacity) =>
        members > 0 ? $"{name} ({members}/{capacity})" : name;
}

public sealed class NationChatState
{
    public bool InPublic { get; private set; }
    public bool InPrivate { get; private set; }
    public bool Owner { get; private set; }
    public int RoomId { get; private set; } = NationChatRules.NoRoom;
    public string RoomName { get; private set; } = "";
    public bool RoomPrivate { get; private set; }
    public string RoomPassword { get; private set; } = "";
    public int RoomMax { get; private set; } = NationChatRules.RoomSizes[0];
    public int RoomMembers { get; private set; }
    public bool Kept { get; private set; }

    public bool InRoom => InPublic || InPrivate;

    public void KeptChanged(bool kept) => Kept = kept && InPrivate;

    public void Describe(string name, bool locked, int members, int max)
    {
        RoomName = name;
        RoomPrivate = locked;
        RoomMembers = members;
        RoomMax = max;
    }

    public void Counted(int members) => RoomMembers = members;

    public void EnteredPublic(int channelId)
    {
        InPublic = true;
        InPrivate = false;
        Owner = false;
        RoomId = channelId;
        RoomMax = NationChatRules.PublicCapacity;
    }

    public void EnteredPrivate(int roomId)
    {
        InPublic = false;
        InPrivate = true;
        RoomId = roomId;
    }

    public void OwnRoom(int roomId, string name, bool isPrivate, string password, int max)
    {
        EnteredPrivate(roomId);
        Owner = true;
        RoomName = name;
        RoomPrivate = isPrivate;
        RoomPassword = password;
        RoomMax = max;
        if (RoomMembers == 0) RoomMembers = 1;
    }

    public void LostOwnership() => Owner = false;

    public void Configured(string name, bool isPrivate, string password, int max)
    {
        RoomName = name;
        RoomPrivate = isPrivate;
        RoomPassword = isPrivate ? password : "";
        RoomMax = Math.Max(max, RoomMembers);
    }

    public void Left()
    {
        InPublic = false;
        InPrivate = false;
        Owner = false;
        RoomId = NationChatRules.NoRoom;
        RoomName = "";
        RoomPrivate = false;
        RoomPassword = "";
        RoomMax = NationChatRules.RoomSizes[0];
        RoomMembers = 0;
        Kept = false;
    }
}
