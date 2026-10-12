using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace LibreKO.Domain;

public sealed class VoiceSeal : IDisposable
{
    public const int KeyLength = 16;
    public const int HeaderLength = 8;
    public const int TagLength = 16;
    public const int Overhead = HeaderLength + TagLength;
    public const byte ClientToServer = 0;
    public const byte ServerToClient = 1;
    private const int NonceLength = 12;
    private const int NonceSessionOffset = 4;
    private const int NonceCounterOffset = 8;

    private readonly AesGcm _aes;
    private readonly ReplayWindow _window = new();
    private readonly object _gate = new();
    private uint _sendCounter;

    public VoiceSeal(uint sessionId, byte[] key)
    {
        SessionId = sessionId;
        _aes = new AesGcm(key, TagLength);
    }

    public uint SessionId { get; }

    public static bool Supported => AesGcm.IsSupported;

    public static uint SessionOf(ReadOnlySpan<byte> datagram) =>
        datagram.Length < HeaderLength ? 0 : BinaryPrimitives.ReadUInt32LittleEndian(datagram);

    public int Seal(byte direction, ReadOnlySpan<byte> plain, Span<byte> datagram)
    {
        Span<byte> nonce = stackalloc byte[NonceLength];
        lock (_gate)
        {
            uint counter = ++_sendCounter;
            BinaryPrimitives.WriteUInt32LittleEndian(datagram, SessionId);
            BinaryPrimitives.WriteUInt32LittleEndian(datagram[4..], counter);
            Nonce(direction, SessionId, counter, nonce);
            _aes.Encrypt(nonce, plain, datagram.Slice(HeaderLength, plain.Length),
                datagram.Slice(HeaderLength + plain.Length, TagLength), datagram[..HeaderLength]);
        }
        return plain.Length + Overhead;
    }

    public bool TryOpen(byte direction, ReadOnlySpan<byte> datagram, Span<byte> plain, out int length)
    {
        length = 0;
        if (datagram.Length < Overhead || SessionOf(datagram) != SessionId)
            return false;
        uint counter = BinaryPrimitives.ReadUInt32LittleEndian(datagram[4..]);
        int size = datagram.Length - Overhead;
        if (plain.Length < size)
            return false;
        Span<byte> nonce = stackalloc byte[NonceLength];
        Nonce(direction, SessionId, counter, nonce);
        lock (_gate)
        {
            if (!_window.Allows(counter))
                return false;
            try
            {
                _aes.Decrypt(nonce, datagram.Slice(HeaderLength, size), datagram.Slice(HeaderLength + size, TagLength),
                    plain[..size], datagram[..HeaderLength]);
            }
            catch (AuthenticationTagMismatchException)
            {
                return false;
            }
            _window.Mark(counter);
        }
        length = size;
        return true;
    }

    private static void Nonce(byte direction, uint sessionId, uint counter, Span<byte> nonce)
    {
        nonce.Clear();
        nonce[0] = direction;
        BinaryPrimitives.WriteUInt32LittleEndian(nonce[NonceSessionOffset..], sessionId);
        BinaryPrimitives.WriteUInt32LittleEndian(nonce[NonceCounterOffset..], counter);
    }

    public void Dispose() => _aes.Dispose();
}

public sealed class ReplayWindow
{
    private const int Width = 64;
    private uint _highest;
    private ulong _seen;

    public bool Allows(uint counter)
    {
        if (counter == 0) return false;
        if (counter > _highest) return true;
        uint back = _highest - counter;
        return back < Width && (_seen & (1UL << (int)back)) == 0;
    }

    public void Mark(uint counter)
    {
        if (counter > _highest)
        {
            uint shift = counter - _highest;
            _seen = shift >= Width ? 0 : _seen << (int)shift;
            _seen |= 1;
            _highest = counter;
            return;
        }
        _seen |= 1UL << (int)(_highest - counter);
    }
}
