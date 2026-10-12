using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Concentus;
using Concentus.Enums;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class VoiceChat : Node
{
    public const string BusVoiceChat = "VoiceChat";
    public const string BusMic = "Mic";
    private const int SampleRate = PcmFrameCutter.SampleRate;
    private const int FrameSamples = PcmFrameCutter.FrameSamples;
    private const int LiveBitrate = 24000;
    private const int ExpectedLossPercent = 10;
    private const int MaxOpusPacket = 400;
    private const double HelloSeconds = 15.0;
    private const double ProbeSeconds = 3.0;
    private const double ProbeHelloSeconds = 0.5;
    private const double SpeakingHoldSeconds = 0.3;
    private const float GeneratorSeconds = 0.25f;
    private const float SilentDb = -80f;
    private const string AndroidRecordAudio = "android.permission.RECORD_AUDIO";

    public enum Transport
    {
        Off,
        Probing,
        Udp,
        Tcp,
    }

    private sealed class Speaker(int characterId, IOpusDecoder decoder, AudioStreamPlayer player)
    {
        public int CharacterId { get; } = characterId;
        public VoiceJitterBuffer Jitter { get; } = new();
        public IOpusDecoder Decoder { get; } = decoder;
        public AudioStreamPlayer Player { get; } = player;
        public AudioStreamGeneratorPlayback? Playback { get; set; }
        public float[] Pcm { get; } = new float[FrameSamples];
        public Vector2[] Stereo { get; } = new Vector2[FrameSamples];
    }

    public readonly record struct PeerMix(float Volume, bool Muted);

    public static VoiceChat? I { get; private set; }

    private AudioStreamPlayer _micPlayer = null!;
    private AudioEffectCapture _capture = null!;
    private bool _micOn;
    private LinearResampler? _micResampler;
    private float[] _micMono = new float[4096];
    private float[] _micResampled = new float[8192];
    private readonly float[] _frame = new float[FrameSamples];
    private readonly byte[] _opus = new byte[MaxOpusPacket];
    private readonly byte[] _datagram = new byte[MaxOpusPacket + VoiceSeal.Overhead + 8];
    private readonly byte[] _plain = new byte[MaxOpusPacket + 16];
    private readonly PcmFrameCutter _cutter = new();
    private readonly VoiceActivityGate _gate = new();
    private IOpusEncoder? _encoder;
    private VoiceSeal? _seal;
    private PacketPeerUdp? _udp;
    private double _probeLeft;
    private double _helloLeft;
    private ushort _seq;
    private byte _flags;
    private double _clock;
    private double _selfSpeakingUntil = double.NegativeInfinity;
    private readonly Dictionary<int, Speaker> _speakers = new();
    private readonly Dictionary<int, PeerMix> _mix = new();
    private readonly Dictionary<int, double> _lastHeard = new();

    public event Action? StateChanged;

    public Transport CurrentTransport { get; private set; }
    public bool InVoice => Net.I.Voice != null;
    public bool MicMuted => (_flags & VoiceFlags.MicMuted) != 0;
    public bool Deafened => (_flags & VoiceFlags.Deafened) != 0;
    public bool CanSpeak => Net.I.Voice is { } voice && (voice.SelfFlags & (VoiceFlags.CannotSpeak | VoiceFlags.Silenced)) == 0;
    public bool SilencedByLeader => Net.I.Voice is { } voice && (voice.SelfFlags & VoiceFlags.Silenced) != 0;
    public float MicLevel => _gate.Level;
    public bool SelfSpeaking => _clock < _selfSpeakingUntil;

    public static VoiceChat Ensure(Node host)
    {
        if (I != null && IsInstanceValid(I)) return I;
        var chat = new VoiceChat { Name = "VoiceChat" };
        host.AddChild(chat);
        return chat;
    }

    public override void _Ready()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
        OpusCodecFactory.AttemptToUseNativeLibrary = false;
        EnsureBuses();
        _micPlayer = new AudioStreamPlayer { Name = "Microphone", Bus = BusMic };
        AddChild(_micPlayer);
        ApplyVolume();
        Config.VoiceChatChanged += ApplyVolume;
        Config.VoiceChatChanged += ApplyInputDevice;
        Net.I.VoiceJoinedEvent += OnJoined;
        Net.I.VoiceLeftEvent += OnLeft;
        Net.I.VoicePeerEvent += OnPeer;
        Net.I.VoiceTcpFrameEvent += OnFrame;
        PruneNoteCache();
    }

    public override void _ExitTree()
    {
        Config.VoiceChatChanged -= ApplyVolume;
        Config.VoiceChatChanged -= ApplyInputDevice;
        Net.I.VoiceJoinedEvent -= OnJoined;
        Net.I.VoiceLeftEvent -= OnLeft;
        Net.I.VoicePeerEvent -= OnPeer;
        Net.I.VoiceTcpFrameEvent -= OnFrame;
        TearDownLive();
        if (I == this) I = null;
    }

    private void EnsureBuses()
    {
        if (AudioServer.GetBusIndex(BusVoiceChat) < 0)
        {
            int idx = AudioServer.BusCount;
            AudioServer.AddBus(idx);
            AudioServer.SetBusName(idx, BusVoiceChat);
            AudioServer.SetBusSend(idx, Audio.BusMaster);
        }
        int mic = AudioServer.GetBusIndex(BusMic);
        if (mic < 0)
        {
            mic = AudioServer.BusCount;
            AudioServer.AddBus(mic);
            AudioServer.SetBusName(mic, BusMic);
            AudioServer.SetBusSend(mic, Audio.BusMaster);
            AudioServer.SetBusMute(mic, true);
            AudioServer.AddBusEffect(mic, new AudioEffectCapture { BufferLength = 0.5f });
        }
        _capture = (AudioEffectCapture)AudioServer.GetBusEffect(mic, 0);
    }

    private static void ApplyVolume()
    {
        int idx = AudioServer.GetBusIndex(BusVoiceChat);
        if (idx < 0) return;
        float linear = Config.VoiceChatVolume;
        AudioServer.SetBusVolumeDb(idx, linear <= 0f ? SilentDb : Mathf.LinearToDb(linear));
        AudioServer.SetBusMute(idx, linear <= 0f);
    }

    private static void ApplyInputDevice()
    {
        string wanted = Config.VoiceInputDevice;
        foreach (string device in AudioServer.GetInputDeviceList())
            if (device == wanted)
            {
                AudioServer.InputDevice = device;
                return;
            }
        AudioServer.InputDevice = Config.DefaultAudioDevice;
    }

    public static string[] InputDevices() => AudioServer.GetInputDeviceList();

    private Action? _afterPermission;
    private bool _permissionHooked;

    private bool AskForMicrophone(Action then)
    {
        if (OS.GetName() != "Android") return true;
        foreach (string granted in OS.GetGrantedPermissions())
            if (granted == AndroidRecordAudio) return true;
        _afterPermission = then;
        if (!_permissionHooked)
        {
            _permissionHooked = true;
            GetTree().OnRequestPermissionsResult += OnPermissionResult;
        }
        OS.RequestPermission(AndroidRecordAudio);
        return false;
    }

    private void OnPermissionResult(string permission, bool granted)
    {
        if (permission != AndroidRecordAudio) return;
        var then = _afterPermission;
        _afterPermission = null;
        if (granted) then?.Invoke();
        else NoteNotice?.Invoke("Voice chat needs access to the microphone.");
    }

    public void Join()
    {
        if (InVoice || !Net.I.NationChat.InRoom) return;
        if (!AskForMicrophone(Join)) return;
        Net.I.SendVoiceJoin();
    }

    public void Leave() => Net.I.SendVoiceLeave();

    public void ToggleMute() => SetFlags((byte)(_flags ^ VoiceFlags.MicMuted));

    public void ToggleDeafen() => SetFlags((byte)(_flags ^ VoiceFlags.Deafened));

    private void SetFlags(byte flags)
    {
        _flags = flags;
        Net.I.SendVoiceState(_flags);
        foreach (var speaker in _speakers.Values) ApplyMix(speaker);
        StateChanged?.Invoke();
    }

    public bool IsSpeaking(int characterId) =>
        _lastHeard.TryGetValue(characterId, out double heard) && _clock - heard < SpeakingHoldSeconds;

    internal void PreviewSpeaking(int characterId, bool self)
    {
        if (self) _selfSpeakingUntil = double.PositiveInfinity;
        else _lastHeard[characterId] = double.PositiveInfinity;
    }

    public PeerMix MixOf(int characterId) => _mix.TryGetValue(characterId, out var mix) ? mix : new PeerMix(1f, false);

    public void SetMix(int characterId, PeerMix mix)
    {
        _mix[characterId] = new PeerMix(Math.Clamp(mix.Volume, 0f, 1f), mix.Muted);
        if (_speakers.TryGetValue(characterId, out var speaker)) ApplyMix(speaker);
        StateChanged?.Invoke();
    }

    private void ApplyMix(Speaker speaker)
    {
        var mix = MixOf(speaker.CharacterId);
        bool silent = mix.Muted || mix.Volume <= 0f || Deafened;
        speaker.Player.VolumeDb = silent ? SilentDb : Mathf.LinearToDb(mix.Volume);
    }

    private void OnJoined(VoiceSession session)
    {
        TearDownLive();
        _flags = 0;
        _encoder = OpusCodecFactory.CreateEncoder(SampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        _encoder.Bitrate = LiveBitrate;
        _encoder.UseInbandFEC = true;
        _encoder.PacketLossPercent = ExpectedLossPercent;
        _encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
        _seq = 0;
        _gate.Reset();
        _cutter.Clear();
        bool udp = session.UdpPort != 0 && VoiceSeal.Supported && Net.I.ServerHost.Length > 0;
        if (udp)
        {
            _seal = new VoiceSeal(session.SessionId, session.Key);
            _udp = new PacketPeerUdp();
            string address = Net.I.ServerHost.IsValidIPAddress() ? Net.I.ServerHost : IP.ResolveHostname(Net.I.ServerHost, IP.Type.Ipv4);
            udp = address.Length > 0 && _udp.ConnectToHost(address, session.UdpPort) == Error.Ok;
        }
        if (udp)
        {
            CurrentTransport = Transport.Probing;
            _probeLeft = ProbeSeconds;
            SendHello();
        }
        else UseTcp();
        RetainMic(true);
        StateChanged?.Invoke();
    }

    private void OnLeft()
    {
        TearDownLive();
        StateChanged?.Invoke();
    }

    private void OnPeer(int characterId, byte change, byte flags)
    {
        if (change == RoomMediaSubs.MemberLeft && _speakers.Remove(characterId, out var speaker))
            speaker.Player.QueueFree();
        StateChanged?.Invoke();
    }

    private void TearDownLive()
    {
        foreach (var speaker in _speakers.Values)
            if (IsInstanceValid(speaker.Player)) speaker.Player.QueueFree();
        _speakers.Clear();
        _lastHeard.Clear();
        _udp?.Close();
        _udp = null;
        _seal?.Dispose();
        _seal = null;
        _encoder = null;
        CurrentTransport = Transport.Off;
        _selfSpeakingUntil = double.NegativeInfinity;
        RetainMic(false);
    }

    private void UseTcp()
    {
        CurrentTransport = Transport.Tcp;
        _udp?.Close();
        _udp = null;
        Net.I.SendVoiceTransport(true);
        StateChanged?.Invoke();
    }

    private void SendHello()
    {
        _helloLeft = CurrentTransport == Transport.Probing ? ProbeHelloSeconds : HelloSeconds;
        SendDatagram([VoiceDatagram.Hello]);
    }

    private void SendDatagram(ReadOnlySpan<byte> plain)
    {
        if (_udp == null || _seal == null) return;
        int length = _seal.Seal(VoiceSeal.ClientToServer, plain, _datagram);
        _udp.PutPacket(_datagram.AsSpan(0, length).ToArray());
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if (_micOn) PumpMicrophone();
        if (CurrentTransport is Transport.Probing or Transport.Udp) PumpUdp(delta);
        foreach (var speaker in _speakers.Values) FillSpeaker(speaker);
        ProcessNotes();
    }

    private void RetainMic(bool live)
    {
        bool want = (live && InVoice) || Recording;
        if (want == _micOn) return;
        _micOn = want;
        if (want)
        {
            ApplyInputDevice();
            _capture.ClearBuffer();
            _micResampler = new LinearResampler((int)AudioServer.GetMixRate(), SampleRate);
            _micPlayer.Stream = new AudioStreamMicrophone();
            _micPlayer.Play();
        }
        else
        {
            _micPlayer.Stop();
            _micPlayer.Stream = null;
            _cutter.Clear();
            _gate.Reset();
        }
    }

    private void PumpMicrophone()
    {
        int available = _capture.GetFramesAvailable();
        if (available <= 0 || _micResampler == null) return;
        Vector2[] stereo = _capture.GetBuffer(available);
        if (_micMono.Length < stereo.Length) _micMono = new float[stereo.Length * 2];
        for (int i = 0; i < stereo.Length; i++) _micMono[i] = (stereo[i].X + stereo[i].Y) * 0.5f;
        int room = _micResampler.MaxOutput(stereo.Length);
        if (_micResampled.Length < room) _micResampled = new float[room * 2];
        int written = _micResampler.Process(_micMono.AsSpan(0, stereo.Length), _micResampled);
        _cutter.Push(_micResampled.AsSpan(0, written));
        while (_cutter.TryTake(_frame))
        {
            bool open = _gate.Process(_frame, Config.VoiceThreshold);
            if (Recording) RecordFrame(_frame, _gate.Above(Config.VoiceThreshold));
            if (InVoice) Transmit(open);
        }
    }

    private bool PushToTalkHeld() =>
        KeyBinds.Held(KeyAction.VoicePushToTalk) && GetViewport()?.GuiGetFocusOwner() is not (LineEdit or TextEdit);

    private void Transmit(bool gateOpen)
    {
        if (_encoder == null || MicMuted || !CanSpeak || CurrentTransport == Transport.Off) return;
        bool send = Config.VoicePushToTalk ? PushToTalkHeld() : gateOpen;
        if (!send) return;
        int length;
        try
        {
            length = _encoder.Encode(_frame, FrameSamples, _opus, _opus.Length);
        }
        catch (OpusException)
        {
            return;
        }
        if (length <= 0) return;
        ushort seq = _seq++;
        _selfSpeakingUntil = _clock + SpeakingHoldSeconds;
        if (CurrentTransport == Transport.Tcp)
        {
            Net.I.SendVoiceFrame(seq, _opus.AsSpan(0, length));
            return;
        }
        _plain[0] = VoiceDatagram.Frame;
        BinaryPrimitives.WriteUInt16LittleEndian(_plain.AsSpan(1), seq);
        _opus.AsSpan(0, length).CopyTo(_plain.AsSpan(VoiceDatagram.ClientFrameHeader));
        SendDatagram(_plain.AsSpan(0, VoiceDatagram.ClientFrameHeader + length));
    }

    private void PumpUdp(double delta)
    {
        if (_udp == null || _seal == null) return;
        while (_udp.GetAvailablePacketCount() > 0)
        {
            byte[] packet = _udp.GetPacket();
            if (!_seal.TryOpen(VoiceSeal.ServerToClient, packet, _plain, out int length) || length < 1) continue;
            if (_plain[0] == VoiceDatagram.Hello)
            {
                if (CurrentTransport == Transport.Probing)
                {
                    CurrentTransport = Transport.Udp;
                    StateChanged?.Invoke();
                }
                continue;
            }
            if (_plain[0] != VoiceDatagram.Frame || length <= VoiceDatagram.ServerFrameHeader) continue;
            int speaker = BinaryPrimitives.ReadInt32LittleEndian(_plain.AsSpan(1));
            ushort seq = BinaryPrimitives.ReadUInt16LittleEndian(_plain.AsSpan(5));
            OnFrame(speaker, seq, _plain.AsSpan(VoiceDatagram.ServerFrameHeader, length - VoiceDatagram.ServerFrameHeader).ToArray());
        }
        if (CurrentTransport == Transport.Probing && (_probeLeft -= delta) <= 0)
        {
            UseTcp();
            return;
        }
        if ((_helloLeft -= delta) <= 0) SendHello();
    }

    private void OnFrame(int characterId, ushort seq, byte[] opus)
    {
        if (!InVoice || characterId == Net.I.MyCharId) return;
        if (!_speakers.TryGetValue(characterId, out var speaker))
        {
            var player = new AudioStreamPlayer
            {
                Name = $"Voice{characterId}",
                Bus = BusVoiceChat,
                Stream = new AudioStreamGenerator
                {
                    MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Custom,
                    MixRate = SampleRate,
                    BufferLength = GeneratorSeconds,
                },
            };
            AddChild(player);
            player.Play();
            speaker = new Speaker(characterId, OpusCodecFactory.CreateDecoder(SampleRate, 1), player)
            {
                Playback = player.GetStreamPlayback() as AudioStreamGeneratorPlayback,
            };
            _speakers[characterId] = speaker;
            ApplyMix(speaker);
        }
        _lastHeard[characterId] = _clock;
        speaker.Jitter.Push(seq, opus);
    }

    private static void FillSpeaker(Speaker speaker)
    {
        var playback = speaker.Playback;
        if (playback == null) return;
        while (playback.GetFramesAvailable() >= FrameSamples)
        {
            var output = speaker.Jitter.Pop(out byte[]? frame);
            if (output == JitterOutput.Wait) return;
            int decoded;
            try
            {
                decoded = output == JitterOutput.Frame
                    ? speaker.Decoder.Decode(frame, speaker.Pcm, FrameSamples, false)
                    : speaker.Decoder.Decode(frame ?? ReadOnlySpan<byte>.Empty, speaker.Pcm, FrameSamples, frame != null);
            }
            catch (OpusException)
            {
                continue;
            }
            for (int i = 0; i < decoded; i++) speaker.Stereo[i] = new Vector2(speaker.Pcm[i], speaker.Pcm[i]);
            if (decoded > 0) playback.PushBuffer(decoded == FrameSamples ? speaker.Stereo : speaker.Stereo[..decoded]);
        }
    }
}

public static class VoiceDatagram
{
    public const byte Hello = 1;
    public const byte Frame = 2;
    public const int ClientFrameHeader = 3;
    public const int ServerFrameHeader = 7;
}
