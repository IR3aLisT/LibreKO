using System;

namespace LibreKO.Domain;

public enum JitterOutput
{
    Wait,
    Frame,
    Lost,
}

public sealed class VoiceJitterBuffer
{
    public const int TargetFrames = 3;
    public const int Capacity = 32;
    public const int MaxConcealed = 5;

    private readonly byte[]?[] _frames = new byte[Capacity][];
    private readonly ushort[] _seqs = new ushort[Capacity];
    private ushort _next;
    private bool _started;
    private bool _playing;
    private int _concealed;

    public bool Playing => _playing;

    public int Buffered
    {
        get
        {
            int count = 0;
            for (int i = 0; i < Capacity; i++)
                if (_frames[i] != null && Ahead(_seqs[i]) is >= 0 and < Capacity) count++;
            return count;
        }
    }

    public void Push(ushort seq, byte[] frame)
    {
        if (!_started)
        {
            _next = seq;
            _started = true;
        }
        int ahead = Ahead(seq);
        if (ahead < 0)
        {
            if (_playing || ahead <= -Capacity) return;
            _next = seq;
        }
        else if (ahead >= Capacity)
        {
            Reset();
            _next = seq;
            _started = true;
        }
        int slot = seq % Capacity;
        _frames[slot] = frame;
        _seqs[slot] = seq;
    }

    public JitterOutput Pop(out byte[]? frame)
    {
        frame = null;
        if (!_started) return JitterOutput.Wait;
        if (!_playing)
        {
            if (Buffered < TargetFrames) return JitterOutput.Wait;
            _playing = true;
        }
        if (Take(_next, out frame))
        {
            _next++;
            _concealed = 0;
            return JitterOutput.Frame;
        }
        if (Buffered > 0)
        {
            if (_concealed < MaxConcealed)
            {
                _next++;
                _concealed++;
                Peek(_next, out frame);
                return JitterOutput.Lost;
            }
            _next = Earliest();
            _concealed = 0;
            if (Take(_next, out frame))
            {
                _next++;
                return JitterOutput.Frame;
            }
        }
        _playing = false;
        if (Buffered == 0) _started = false;
        return JitterOutput.Wait;
    }

    public void Reset()
    {
        Array.Clear(_frames);
        _started = false;
        _playing = false;
        _concealed = 0;
    }

    private int Ahead(ushort seq) => (short)(ushort)(seq - _next);

    private ushort Earliest()
    {
        int best = Capacity;
        for (int i = 0; i < Capacity; i++)
            if (_frames[i] != null && Ahead(_seqs[i]) is >= 0 and var ahead && ahead < best) best = ahead;
        return (ushort)(_next + (best == Capacity ? 0 : best));
    }

    private bool Take(ushort seq, out byte[]? frame)
    {
        int slot = seq % Capacity;
        frame = _frames[slot];
        if (frame == null || _seqs[slot] != seq) { frame = null; return false; }
        _frames[slot] = null;
        return true;
    }

    private void Peek(ushort seq, out byte[]? frame)
    {
        int slot = seq % Capacity;
        frame = _frames[slot] != null && _seqs[slot] == seq ? _frames[slot] : null;
    }
}

public sealed class VoiceActivityGate
{
    public const float MinDb = -60f;
    public const float MaxDb = -15f;
    public const int HangoverFrames = 15;
    private const double Floor = 1e-6;

    private int _hang;

    public float LevelDb { get; private set; } = MinDb;

    public float Level => Position(LevelDb);

    public static float ThresholdDb(float threshold) => MinDb + (MaxDb - MinDb) * Math.Clamp(threshold, 0f, 1f);

    public static float Position(float db) => Math.Clamp((db - MinDb) / (MaxDb - MinDb), 0f, 1f);

    public bool Above(float threshold) => LevelDb >= ThresholdDb(threshold);

    public bool Process(ReadOnlySpan<float> frame, float threshold)
    {
        double sum = 0;
        foreach (float sample in frame) sum += sample * sample;
        double rms = frame.Length == 0 ? 0 : Math.Sqrt(sum / frame.Length);
        LevelDb = Math.Max(MinDb, (float)(20 * Math.Log10(Math.Max(rms, Floor))));
        if (LevelDb >= ThresholdDb(threshold))
        {
            _hang = HangoverFrames;
            return true;
        }
        if (_hang > 0)
        {
            _hang--;
            return true;
        }
        return false;
    }

    public void Reset()
    {
        _hang = 0;
        LevelDb = MinDb;
    }
}

public sealed class PcmFrameCutter
{
    public const int SampleRate = 48000;
    public const int FrameSamples = 960;
    public const int FrameMs = 20;
    private const int MaxPendingFrames = 10;

    private readonly float[] _pending = new float[FrameSamples * MaxPendingFrames];
    private int _count;

    public int Pending => _count;

    public void Push(ReadOnlySpan<float> mono)
    {
        if (mono.Length >= _pending.Length)
        {
            mono[^_pending.Length..].CopyTo(_pending);
            _count = _pending.Length;
            return;
        }
        int overflow = _count + mono.Length - _pending.Length;
        if (overflow > 0)
        {
            Array.Copy(_pending, overflow, _pending, 0, _count - overflow);
            _count -= overflow;
        }
        mono.CopyTo(_pending.AsSpan(_count));
        _count += mono.Length;
    }

    public bool TryTake(Span<float> frame)
    {
        if (_count < FrameSamples || frame.Length < FrameSamples) return false;
        _pending.AsSpan(0, FrameSamples).CopyTo(frame);
        _count -= FrameSamples;
        Array.Copy(_pending, FrameSamples, _pending, 0, _count);
        return true;
    }

    public void Clear() => _count = 0;
}

public sealed class LinearResampler(int fromRate, int toRate)
{
    private readonly double _step = fromRate / (double)toRate;
    private double _position;
    private float _last;

    public bool PassThrough => fromRate == toRate;

    public int MaxOutput(int inputLength) => (int)Math.Ceiling((inputLength + 1) / _step) + 1;

    public int Process(ReadOnlySpan<float> input, Span<float> output)
    {
        if (input.Length == 0) return 0;
        if (PassThrough)
        {
            int copy = Math.Min(input.Length, output.Length);
            input[..copy].CopyTo(output);
            return copy;
        }
        int written = 0;
        while (written < output.Length)
        {
            int index = (int)Math.Floor(_position);
            if (index + 1 >= input.Length) break;
            float a = index < 0 ? _last : input[index];
            float b = input[index + 1];
            output[written++] = a + (b - a) * (float)(_position - index);
            _position += _step;
        }
        _position -= input.Length;
        _last = input[^1];
        return written;
    }
}
