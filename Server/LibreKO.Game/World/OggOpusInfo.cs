using System.Buffers.Binary;

namespace LibreKO.Game.World;

public readonly record struct OggOpusInfo(int Channels, int DurationMs)
{
    public const int OpusRate = 48000;
    private const int PageHeaderLength = 27;
    private const int OpusHeadLength = 19;
    private const byte FirstPageFlag = 0x02;
    private static ReadOnlySpan<byte> Capture => "OggS"u8;
    private static ReadOnlySpan<byte> OpusHead => "OpusHead"u8;

    public static bool TryRead(ReadOnlySpan<byte> file, out OggOpusInfo info)
    {
        info = default;
        int offset = 0;
        int pages = 0;
        uint serial = 0;
        long lastGranule = -1;
        int channels = 0;
        int preSkip = 0;
        while (offset < file.Length)
        {
            if (file.Length - offset < PageHeaderLength || !file.Slice(offset, Capture.Length).SequenceEqual(Capture))
                return false;
            byte flags = file[offset + 5];
            long granule = BinaryPrimitives.ReadInt64LittleEndian(file[(offset + 6)..]);
            uint pageSerial = BinaryPrimitives.ReadUInt32LittleEndian(file[(offset + 14)..]);
            int segments = file[offset + 26];
            if (file.Length - offset < PageHeaderLength + segments)
                return false;
            int body = 0;
            for (int i = 0; i < segments; i++)
                body += file[offset + PageHeaderLength + i];
            int bodyStart = offset + PageHeaderLength + segments;
            if (file.Length - bodyStart < body)
                return false;
            if (pages == 0)
            {
                if ((flags & FirstPageFlag) == 0 || body < OpusHeadLength || !file.Slice(bodyStart, OpusHead.Length).SequenceEqual(OpusHead))
                    return false;
                serial = pageSerial;
                channels = file[bodyStart + 9];
                preSkip = BinaryPrimitives.ReadUInt16LittleEndian(file[(bodyStart + 10)..]);
            }
            else if (pageSerial != serial)
                return false;
            if (granule >= 0) lastGranule = granule;
            pages++;
            offset = bodyStart + body;
        }
        if (pages < 2 || lastGranule < preSkip)
            return false;
        long samples = lastGranule - preSkip;
        info = new OggOpusInfo(channels, (int)(samples * 1000 / OpusRate));
        return true;
    }
}
