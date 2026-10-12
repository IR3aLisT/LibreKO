using System.Buffers.Binary;
using LibreKO.Domain;
using Xunit;

namespace LibreKO.Tests;

public class VoiceAudioTests
{
    private static readonly byte[] VectorKey = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
    private const uint VectorSession = 0x01020304;

    [Fact]
    public void SealingMatchesTheServerVectors()
    {
        using var client = new VoiceSeal(VectorSession, VectorKey);
        var datagram = new byte[64];
        int length = client.Seal(VoiceSeal.ClientToServer, [1], datagram);
        Assert.Equal("040302010100000038a12a628534651a41e7e527b35b823c93", Convert.ToHexString(datagram, 0, length).ToLowerInvariant());

        using var listener = new VoiceSeal(VectorSession, VectorKey);
        var fromServer = Convert.FromHexString("0403020101000000cafd4e7530bc019e1661f10d3da937b78fd7f7c87374abaa5d37");
        var plain = new byte[64];
        Assert.True(listener.TryOpen(VoiceSeal.ServerToClient, fromServer, plain, out int opened));
        Assert.Equal(new byte[] { 2, 7, 0, 0, 0, 9, 0, (byte)'a', (byte)'b', (byte)'c' }, plain[..opened]);
    }

    [Fact]
    public void OpeningRejectsTamperingReplaysAndTheWrongDirection()
    {
        using var sender = new VoiceSeal(VectorSession, VectorKey);
        using var receiver = new VoiceSeal(VectorSession, VectorKey);
        var datagram = new byte[64];
        var plain = new byte[64];
        var sealedBytes = datagram[..sender.Seal(VoiceSeal.ServerToClient, [2, 1, 0, 0, 0, 5, 0, 42], datagram)];

        Assert.False(receiver.TryOpen(VoiceSeal.ClientToServer, sealedBytes, plain, out _));
        var tampered = (byte[])sealedBytes.Clone();
        tampered[^1] ^= 1;
        Assert.False(receiver.TryOpen(VoiceSeal.ServerToClient, tampered, plain, out _));
        Assert.True(receiver.TryOpen(VoiceSeal.ServerToClient, sealedBytes, plain, out _));
        Assert.False(receiver.TryOpen(VoiceSeal.ServerToClient, sealedBytes, plain, out _));
        using var stranger = new VoiceSeal(VectorSession + 1, VectorKey);
        Assert.False(stranger.TryOpen(VoiceSeal.ServerToClient, sealedBytes, plain, out _));
    }

    private static byte[] Frame(int n) => [(byte)n];

    private static List<(JitterOutput Output, int Frame)> Drain(VoiceJitterBuffer buffer, int pops)
    {
        var result = new List<(JitterOutput, int)>();
        for (int i = 0; i < pops; i++)
        {
            var output = buffer.Pop(out byte[]? frame);
            result.Add((output, frame?[0] ?? -1));
        }
        return result;
    }

    [Fact]
    public void TheJitterBufferWaitsToPrimeThenPlaysInOrder()
    {
        var buffer = new VoiceJitterBuffer();
        buffer.Push(11, Frame(11));
        buffer.Push(10, Frame(10));
        Assert.Equal(JitterOutput.Wait, buffer.Pop(out _));
        buffer.Push(12, Frame(12));

        var played = Drain(buffer, 4);

        Assert.Equal([(JitterOutput.Frame, 10), (JitterOutput.Frame, 11), (JitterOutput.Frame, 12), (JitterOutput.Wait, -1)], played);
        Assert.False(buffer.Playing);
    }

    [Fact]
    public void ALostFrameIsConcealedWithTheNextFrameForRecovery()
    {
        var buffer = new VoiceJitterBuffer();
        foreach (ushort seq in new ushort[] { 1, 2, 4, 5 }) buffer.Push(seq, Frame(seq));

        var played = Drain(buffer, 5);

        Assert.Equal([(JitterOutput.Frame, 1), (JitterOutput.Frame, 2), (JitterOutput.Lost, 4), (JitterOutput.Frame, 4), (JitterOutput.Frame, 5)], played);
    }

    [Fact]
    public void SequenceNumbersWrapAround()
    {
        var buffer = new VoiceJitterBuffer();
        foreach (ushort seq in new ushort[] { 65534, 65535, 0, 1 }) buffer.Push(seq, Frame(seq % 256));

        var played = Drain(buffer, 4);

        Assert.All(played, p => Assert.Equal(JitterOutput.Frame, p.Output));
        Assert.Equal([254, 255, 0, 1], played.Select(p => p.Frame));
    }

    [Fact]
    public void LateFramesAreDroppedAndAFarJumpStartsAgain()
    {
        var buffer = new VoiceJitterBuffer();
        foreach (ushort seq in new ushort[] { 100, 101, 102 }) buffer.Push(seq, Frame(seq));
        Drain(buffer, 2);
        buffer.Push(100, Frame(1));
        Assert.Equal((JitterOutput.Frame, 102), Drain(buffer, 1)[0]);

        foreach (ushort seq in new ushort[] { 900, 901, 902 }) buffer.Push(seq, Frame(seq % 256));
        Assert.Equal(900 % 256, Drain(buffer, 1)[0].Frame);
    }

    [Fact]
    public void TheGateOpensOnSpeechAndHoldsThroughShortPauses()
    {
        var gate = new VoiceActivityGate();
        var silence = new float[PcmFrameCutter.FrameSamples];
        var speech = Enumerable.Range(0, PcmFrameCutter.FrameSamples).Select(i => 0.3f * MathF.Sin(i * 0.1f)).ToArray();

        Assert.False(gate.Process(silence, 0.35f));
        Assert.Equal(VoiceActivityGate.MinDb, gate.LevelDb);
        Assert.True(gate.Process(speech, 0.35f));
        Assert.True(gate.Level > 0.5f);
        for (int i = 0; i < VoiceActivityGate.HangoverFrames; i++) Assert.True(gate.Process(silence, 0.35f));
        Assert.False(gate.Process(silence, 0.35f));
        var quiet = speech.Select(sample => sample / 6).ToArray();
        Assert.False(gate.Process(quiet, 0.8f));
    }

    [Fact]
    public void TheCutterHandsOutTwentyMillisecondFramesAndDropsTheOldestOnOverflow()
    {
        var cutter = new PcmFrameCutter();
        var frame = new float[PcmFrameCutter.FrameSamples];
        cutter.Push(Enumerable.Range(0, 1500).Select(i => (float)i).ToArray());
        Assert.True(cutter.TryTake(frame));
        Assert.Equal(0f, frame[0]);
        Assert.Equal(959f, frame[^1]);
        Assert.False(cutter.TryTake(frame));
        Assert.Equal(540, cutter.Pending);

        cutter.Push(new float[PcmFrameCutter.FrameSamples * 20]);
        Assert.Equal(PcmFrameCutter.FrameSamples * 10, cutter.Pending);
    }

    [Fact]
    public void TheResamplerConvertsTheRateAndKeepsAConstantSignalConstant()
    {
        var resampler = new LinearResampler(44100, PcmFrameCutter.SampleRate);
        var input = Enumerable.Repeat(0.25f, 441).ToArray();
        var output = new float[resampler.MaxOutput(input.Length)];
        int total = 0;
        for (int i = 0; i < 100; i++) total += resampler.Process(input, output);

        Assert.InRange(total, 48000 - 2, 48000 + 2);
        Assert.All(output.Take(400), sample => Assert.Equal(0.25f, sample, 4));

        var same = new LinearResampler(PcmFrameCutter.SampleRate, PcmFrameCutter.SampleRate);
        Assert.True(same.PassThrough);
        Assert.Equal(3, same.Process([1f, 2f, 3f], new float[3]));
    }

    private static float[] Sine(float amplitude) =>
        Enumerable.Range(0, PcmFrameCutter.FrameSamples)
            .Select(i => amplitude * MathF.Sin(2 * MathF.PI * 440 * i / PcmFrameCutter.SampleRate)).ToArray();

    [Fact]
    public void AutoGainLiftsAQuietVoiceToSpeechLevel()
    {
        var gain = new VoiceAutoGain();
        float[] frame = [];
        for (int i = 0; i < 200; i++)
        {
            frame = Sine(0.02f);
            gain.Process(frame, speech: true);
        }

        Assert.InRange(VoiceWaveform.Rms(frame), VoiceAutoGain.TargetRms * 0.9f, VoiceAutoGain.TargetRms * 1.1f);
        Assert.InRange(gain.Gain, 1f, VoiceAutoGain.MaxGain);
    }

    [Fact]
    public void AutoGainHoldsOutsideSpeechAndKeepsALoudWordUnderTheCeiling()
    {
        var gain = new VoiceAutoGain();
        for (int i = 0; i < 200; i++) gain.Process(Sine(0.02f), speech: true);
        float lifted = gain.Gain;
        for (int i = 0; i < 50; i++) gain.Process(Sine(0.001f), speech: false);
        Assert.Equal(lifted, gain.Gain);

        for (int i = 0; i < 3; i++)
        {
            var shout = Sine(0.9f);
            gain.Process(shout, speech: true);
            Assert.All(shout, sample => Assert.True(Math.Abs(sample) <= VoiceAutoGain.PeakCeiling + 1e-4f));
        }
    }

    private static float[] Tone(double seconds)
    {
        int samples = (int)(seconds * PcmFrameCutter.SampleRate);
        return Enumerable.Range(0, samples).Select(i => 0.4f * MathF.Sin(2 * MathF.PI * 440 * i / PcmFrameCutter.SampleRate)).ToArray();
    }

    private static (int Channels, double Seconds) OggOpusLength(byte[] file)
    {
        int offset = 0, channels = 0, preSkip = 0;
        long granule = 0;
        bool first = true;
        while (offset < file.Length)
        {
            Assert.Equal("OggS", System.Text.Encoding.ASCII.GetString(file, offset, 4));
            long pageGranule = BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(offset + 6));
            int segments = file[offset + 26];
            int body = 0;
            for (int i = 0; i < segments; i++) body += file[offset + 27 + i];
            int start = offset + 27 + segments;
            if (first)
            {
                Assert.Equal("OpusHead", System.Text.Encoding.ASCII.GetString(file, start, 8));
                channels = file[start + 9];
                preSkip = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(start + 10));
                first = false;
            }
            if (pageGranule >= 0) granule = pageGranule;
            offset = start + body;
        }
        return (channels, (granule - preSkip) / (double)PcmFrameCutter.SampleRate);
    }

    [Fact]
    public void TheWaveformHas64SamplesScaledToTheLoudestPart()
    {
        var levels = Enumerable.Range(0, 500).Select(i => i < 250 ? 0.01f : 0.3f).ToList();

        var wave = VoiceWaveform.FromLevels(levels);

        Assert.Equal(VoiceWaveform.Samples, wave.Length);
        Assert.Equal(VoiceWaveform.Peak, wave.Max());
        Assert.True(wave[0] < wave[^1]);
        Assert.All(wave, sample => Assert.True(sample > 0));
        Assert.Empty(VoiceWaveform.FromLevels([]));
        Assert.Equal(VoiceWaveform.Samples, VoiceWaveform.FromLevels([0.2f, 0.1f, 0.3f]).Length);
    }

    [Fact]
    public void TheRecorderReturnsAWaveformForWhatItRecorded()
    {
        using var recorder = new VoiceNoteRecorder();
        var tone = Tone(2.0);
        for (int offset = 0; offset + PcmFrameCutter.FrameSamples <= tone.Length; offset += PcmFrameCutter.FrameSamples)
            recorder.Write(tone.AsSpan(offset, PcmFrameCutter.FrameSamples));

        var wave = recorder.Waveform();

        Assert.Equal(VoiceWaveform.Samples, wave.Length);
        Assert.Equal(VoiceWaveform.Peak, wave.Max());
    }

    [Fact]
    public void ANoteEncodesToMonoOggOpusTheServerCanMeasureAndDecodesBack()
    {
        var file = VoiceNoteCodec.Encode(Tone(3.0));

        var (channels, seconds) = OggOpusLength(file);
        Assert.Equal(1, channels);
        Assert.InRange(seconds, 2.9, 3.05);

        var pcm = VoiceNoteCodec.Decode(file);
        Assert.InRange(pcm.Length / (double)PcmFrameCutter.SampleRate, 2.9, 3.1);
        Assert.True(pcm.Max(s => Math.Abs((int)s)) > 4000);
    }

    [Fact]
    public void SilenceBeforeTheFirstWordIsCutDownToTheLeadIn()
    {
        using var recorder = new VoiceNoteRecorder();
        var quiet = new float[PcmFrameCutter.FrameSamples];
        for (int i = 0; i < 100; i++) recorder.Write(quiet, voice: false);
        Assert.Equal(0, recorder.Seconds);
        var tone = Tone(1.0);
        for (int offset = 0; offset + PcmFrameCutter.FrameSamples <= tone.Length; offset += PcmFrameCutter.FrameSamples)
            recorder.Write(tone.AsSpan(offset, PcmFrameCutter.FrameSamples), voice: true);

        var file = recorder.Finish();

        Assert.InRange(recorder.Seconds, 1.0 + VoiceNoteRecorder.LeadInSeconds - 0.01, 1.0 + VoiceNoteRecorder.LeadInSeconds + 0.01);
        Assert.InRange(OggOpusLength(file).Seconds, 1.2, 1.35);
    }

    [Fact]
    public void ANoteWithoutAnyVoiceKeepsEverythingRecorded()
    {
        using var recorder = new VoiceNoteRecorder();
        var quiet = new float[PcmFrameCutter.FrameSamples];
        for (int i = 0; i < 50; i++) recorder.Write(quiet, voice: false);

        recorder.Finish();

        Assert.InRange(recorder.Seconds, 0.99, 1.01);
    }

    [Fact]
    public void AFullMinuteFitsUnderTheServerCap()
    {
        using var recorder = new VoiceNoteRecorder();
        var tone = Tone(1.0);
        var random = new Random(7);
        var frame = new float[PcmFrameCutter.FrameSamples];
        while (!recorder.Full)
        {
            for (int i = 0; i < frame.Length; i++) frame[i] = tone[(recorder.Samples + i) % tone.Length] + (float)(random.NextDouble() - 0.5) * 0.2f;
            recorder.Write(frame);
        }
        var file = recorder.Finish();

        Assert.True(file.Length < VoiceNoteRules.MaxBytes, $"{file.Length} bytes");
        Assert.InRange(OggOpusLength(file).Seconds, VoiceNoteRules.MaxSeconds - 0.1, VoiceNoteRules.MaxSeconds + 0.05);
    }
}
