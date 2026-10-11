namespace LibreKO.Network;

public readonly record struct EventTitleNotice(bool Removed, string Title, string Message)
{
    public const byte Shown = 1;
    public const byte Gone = 2;
    public const byte ShownWithEffect = 3;

    public static EventTitleNotice? Read(Packet p)
    {
        if (p.RemainingBytes < 1 + sizeof(ushort)) return null;
        byte sub = p.ReadByte();
        if (sub is not (Shown or Gone or ShownWithEffect)) return null;
        string title = p.ReadString();
        if (sub == Gone) return new EventTitleNotice(true, title, "");
        string message = p.RemainingBytes >= sizeof(ushort) ? p.ReadString() : "";
        if (sub == ShownWithEffect && p.RemainingBytes >= 1) p.ReadByte();
        return new EventTitleNotice(false, title, message);
    }
}
