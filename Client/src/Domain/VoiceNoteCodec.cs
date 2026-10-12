using System;
using System.Collections.Generic;
using System.IO;
using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;

namespace LibreKO.Domain;

public sealed class VoiceNoteRecorder : IDisposable
{
    public const int Bitrate = 20000;
    public const int LeadInMs = 300;
    public const double LeadInSeconds = LeadInMs / 1000.0;
    private const int ResamplerQuality = 5;
    private const int LeadInFrames = LeadInMs / PcmFrameCutter.FrameMs;
    private const int MaxWaitFrames = VoiceNoteRules.MaxSeconds * 1000 / PcmFrameCutter.FrameMs;

    private readonly MemoryStream _file = new();
    private readonly OpusOggWriteStream _ogg;
    private readonly float[] _frame = new float[PcmFrameCutter.FrameSamples];
    private readonly List<float> _levels = new();
    private readonly List<float[]> _beforeVoice = new();
    private bool _voiceHeard;

    public VoiceNoteRecorder()
    {
        OpusCodecFactory.AttemptToUseNativeLibrary = false;
        var encoder = OpusCodecFactory.CreateEncoder(PcmFrameCutter.SampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        encoder.Bitrate = Bitrate;
        encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
        _ogg = new OpusOggWriteStream(encoder, _file, new OpusTags(), PcmFrameCutter.SampleRate, ResamplerQuality, true);
    }

    public int Samples { get; private set; }

    public double Seconds => Samples / (double)PcmFrameCutter.SampleRate;

    public bool Full => Seconds >= VoiceNoteRules.MaxSeconds || _beforeVoice.Count >= MaxWaitFrames;

    public void Write(ReadOnlySpan<float> frame, bool voice = true)
    {
        if (!_voiceHeard && !voice)
        {
            _beforeVoice.Add(frame.ToArray());
            return;
        }
        if (!_voiceHeard)
        {
            _voiceHeard = true;
            for (int i = Math.Max(0, _beforeVoice.Count - LeadInFrames); i < _beforeVoice.Count; i++) Encode(_beforeVoice[i]);
            _beforeVoice.Clear();
        }
        Encode(frame);
    }

    private void Encode(ReadOnlySpan<float> frame)
    {
        int count = Math.Min(frame.Length, _frame.Length);
        frame[..count].CopyTo(_frame);
        _ogg.WriteSamples(_frame, 0, count);
        _levels.Add(VoiceWaveform.Rms(frame[..count]));
        Samples += count;
    }

    public byte[] Waveform() => VoiceWaveform.FromLevels(_levels);

    public byte[] Finish()
    {
        foreach (var frame in _beforeVoice) Encode(frame);
        _beforeVoice.Clear();
        _ogg.Finish();
        return _file.ToArray();
    }

    public void Dispose() => _file.Dispose();
}

public static class VoiceNoteCodec
{
    public const float TargetPeak = 0.89f;
    public const float MaxGain = 8f;

    public static float Normalize(short[] pcm)
    {
        int peak = 0;
        foreach (short sample in pcm) peak = Math.Max(peak, Math.Abs((int)sample));
        if (peak == 0) return 1f;
        float gain = Math.Min(MaxGain, TargetPeak * short.MaxValue / peak);
        if (gain <= 1f) return 1f;
        for (int i = 0; i < pcm.Length; i++)
            pcm[i] = (short)Math.Clamp((int)Math.Round(pcm[i] * gain), short.MinValue, short.MaxValue);
        return gain;
    }

    public static byte[] Encode(ReadOnlySpan<float> pcm48k)
    {
        using var recorder = new VoiceNoteRecorder();
        for (int offset = 0; offset + PcmFrameCutter.FrameSamples <= pcm48k.Length; offset += PcmFrameCutter.FrameSamples)
            recorder.Write(pcm48k.Slice(offset, PcmFrameCutter.FrameSamples));
        return recorder.Finish();
    }

    public static short[] Decode(byte[] file)
    {
        OpusCodecFactory.AttemptToUseNativeLibrary = false;
        var decoder = OpusCodecFactory.CreateDecoder(PcmFrameCutter.SampleRate, 1);
        using var stream = new MemoryStream(file);
        var ogg = new OpusOggReadStream(decoder, stream);
        var pcm = new List<short>(PcmFrameCutter.SampleRate * VoiceNoteRules.MaxSeconds);
        while (ogg.HasNextPacket)
        {
            short[] packet = ogg.DecodeNextPacket();
            if (packet != null) pcm.AddRange(packet);
        }
        return pcm.ToArray();
    }
}
