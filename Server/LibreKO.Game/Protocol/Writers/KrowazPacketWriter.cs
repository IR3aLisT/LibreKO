using LibreKO.Common.Infrastructure.Network;

namespace LibreKO.Game.Protocol.Writers;

public static class KrowazPacketWriter
{
    public const string OpenedText = "Krowaz's Dominion is open for one hour.";
    public const string ClosedText = "Krowaz's Dominion has closed.";
    public const string CountdownText = "Krowaz's Dominion closes in {0} minutes.";

    public static Packet Opened() => NoticePacketWriter.Broadcast(OpenedText);

    public static Packet Closed() => NoticePacketWriter.Broadcast(ClosedText);

    public static Packet Countdown(int minutes) => NoticePacketWriter.Broadcast(string.Format(CountdownText, minutes));
}
