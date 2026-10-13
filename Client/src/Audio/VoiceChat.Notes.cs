using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;
using LibreKO.Domain;

namespace LibreKO;

public partial class VoiceChat
{
    private const string NoteCacheDir = "user://voice_notes";
    private const double MinNoteSeconds = 0.5;

    public enum NoteState
    {
        Idle,
        Loading,
        Playing,
        Missing,
    }

    private VoiceNoteRecorder? _recorder;
    private AudioStreamPlayer? _notePlayer;
    private long _playingNote;
    private readonly HashSet<long> _loadingNotes = new();
    private readonly HashSet<long> _missingNotes = new();
    private readonly HashSet<long> _decodingNotes = new();
    private (long Note, float Fraction) _seekOnPlay;
    private (long Note, float Fraction) _previewPlaying;
    private bool _netHooked;

    public bool Recording => _recorder != null;
    public double RecordingSeconds => _recorder?.Seconds ?? 0;

    public event Action<byte[], double, byte[]>? NoteRecorded;
    public event Action<string>? NoteNotice;

    public bool StartRecording()
    {
        if (Recording || !Net.I.NationChat.InRoom) return false;
        if (!AskForMicrophone(() => StartRecording())) return false;
        _recorder = new VoiceNoteRecorder();
        RetainMic(InVoice);
        StateChanged?.Invoke();
        return true;
    }

    public void StopRecording(bool send)
    {
        if (_recorder is not { } recorder) return;
        _recorder = null;
        RetainMic(InVoice);
        byte[] file = recorder.Finish();
        double seconds = recorder.Seconds;
        byte[] waveform = recorder.Waveform();
        recorder.Dispose();
        StateChanged?.Invoke();
        if (send && seconds >= MinNoteSeconds) NoteRecorded?.Invoke(file, seconds, waveform);
    }

    private void RecordFrame(float[] frame, bool voice)
    {
        if (_recorder == null) return;
        _recorder.Write(frame, voice);
        if (_recorder.Full) StopRecording(send: true);
    }

    public NoteState StateOf(long noteId) =>
        _previewPlaying.Note == noteId && noteId != 0 ? NoteState.Playing
        : _missingNotes.Contains(noteId) ? NoteState.Missing
        : _loadingNotes.Contains(noteId) ? NoteState.Loading
        : _playingNote == noteId && _notePlayer is { Playing: true } ? NoteState.Playing
        : NoteState.Idle;

    public float NoteProgress(long noteId)
    {
        if (_previewPlaying.Note == noteId && noteId != 0) return _previewPlaying.Fraction;
        if (_playingNote != noteId || _notePlayer?.Stream is not AudioStreamWav wav || !_notePlayer.Playing) return 0f;
        double length = wav.GetLength();
        return length <= 0 ? 0f : (float)Math.Clamp(_notePlayer.GetPlaybackPosition() / length, 0, 1);
    }

    public float LoadProgress(long noteId) =>
        !_loadingNotes.Contains(noteId) ? 0f
        : _decodingNotes.Contains(noteId) ? 1f
        : _previewLoading.Note == noteId ? _previewLoading.Fraction
        : Net.I.NoteDownloadProgress(noteId);

    private (long Note, float Fraction) _previewLoading;

    internal void PreviewNote(long playing, float played, long loading, float loaded)
    {
        _previewPlaying = (playing, played);
        _previewLoading = (loading, loaded);
        _loadingNotes.Clear();
        if (loading != 0) _loadingNotes.Add(loading);
    }

    public void SeekNote(long noteId, float fraction)
    {
        if (StateOf(noteId) == NoteState.Playing && _notePlayer?.Stream is AudioStreamWav wav)
        {
            _notePlayer.Seek((float)(wav.GetLength() * fraction));
            return;
        }
        _seekOnPlay = (noteId, fraction);
        if (StateOf(noteId) == NoteState.Idle) ToggleNote(noteId);
    }

    public void ToggleNote(long noteId)
    {
        if (StateOf(noteId) == NoteState.Playing)
        {
            StopNote();
            return;
        }
        if (_loadingNotes.Contains(noteId)) return;
        HookNet();
        if (NoteById(noteId) is { } note && CachePath(note) is var cached && File.Exists(cached)
            && new FileInfo(cached).Length == note.Size)
        {
            DecodeAndPlay(noteId, File.ReadAllBytes(cached));
            return;
        }
        _missingNotes.Remove(noteId);
        _loadingNotes.Add(noteId);
        Net.I.SendVoiceNoteFetch(noteId);
        StateChanged?.Invoke();
    }

    public void StopNote()
    {
        _notePlayer?.Stop();
        _playingNote = 0;
        StateChanged?.Invoke();
    }

    private void HookNet()
    {
        if (_netHooked) return;
        _netHooked = true;
        Net.I.VoiceNoteFetchedEvent += OnNoteFetched;
        Net.I.VoiceNoteMissingEvent += OnNoteMissing;
        TreeExiting += () =>
        {
            Net.I.VoiceNoteFetchedEvent -= OnNoteFetched;
            Net.I.VoiceNoteMissingEvent -= OnNoteMissing;
        };
    }

    private void OnNoteFetched(long noteId, byte[] file)
    {
        try
        {
            if (NoteById(noteId) is { } note && file.Length == note.Size)
            {
                string path = CachePath(note);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, file);
            }
        }
        catch (IOException ex)
        {
            GD.PushWarning($"[voice] note {noteId} not cached: {ex.Message}");
        }
        DecodeAndPlay(noteId, file);
    }

    private void OnNoteMissing(long noteId)
    {
        _loadingNotes.Remove(noteId);
        _missingNotes.Add(noteId);
        NoteNotice?.Invoke(RoomMediaResults.Text(RoomMediaResults.NotFound));
        StateChanged?.Invoke();
    }

    private void DecodeAndPlay(long noteId, byte[] file)
    {
        _loadingNotes.Add(noteId);
        _decodingNotes.Add(noteId);
        StateChanged?.Invoke();
        Task.Run(() =>
        {
            short[]? pcm = null;
            try
            {
                pcm = VoiceNoteCodec.Decode(file);
                VoiceNoteCodec.Normalize(pcm);
            }
            catch (Exception ex) { GD.PushWarning($"[voice] note {noteId} could not be decoded: {ex.Message}"); }
            Callable.From(() => Play(noteId, pcm)).CallDeferred();
        });
    }

    private void Play(long noteId, short[]? pcm)
    {
        _loadingNotes.Remove(noteId);
        _decodingNotes.Remove(noteId);
        if (pcm == null || pcm.Length == 0)
        {
            _missingNotes.Add(noteId);
            StateChanged?.Invoke();
            return;
        }
        var bytes = new byte[pcm.Length * sizeof(short)];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        var wav = new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = SampleRate,
            Stereo = false,
            Data = bytes,
        };
        if (_notePlayer == null)
        {
            _notePlayer = new AudioStreamPlayer { Name = "VoiceNote", Bus = BusVoiceChat };
            _notePlayer.Finished += () =>
            {
                _playingNote = 0;
                StateChanged?.Invoke();
            };
            AddChild(_notePlayer);
        }
        _notePlayer.Stream = wav;
        _playingNote = noteId;
        float from = _seekOnPlay.Note == noteId ? (float)(wav.GetLength() * _seekOnPlay.Fraction) : 0f;
        _seekOnPlay = default;
        _notePlayer.Play(from);
        StateChanged?.Invoke();
    }

    private void ProcessNotes()
    {
        if (_recorder != null && !Net.I.NationChat.InRoom) StopRecording(send: false);
    }

    private static string CacheRoot() => ProjectSettings.GlobalizePath(NoteCacheDir);

    private static string CachePath(RoomPost note) => Path.Combine(CacheRoot(), VoiceNoteRules.CacheName(Net.I.ServerHost, note));

    private static RoomPost? NoteById(long noteId)
    {
        var posts = Net.I.RoomTimeline.Posts;
        for (int i = posts.Count - 1; i >= 0; i--)
            if (posts[i].Id == noteId && posts[i].Kind == RoomPostKind.VoiceNote) return posts[i];
        return null;
    }

    private static void PruneNoteCache()
    {
        try
        {
            string root = CacheRoot();
            if (!Directory.Exists(root)) return;
            var cutoff = DateTime.UtcNow.AddDays(-VoiceNoteRules.CacheDays);
            foreach (string file in Directory.EnumerateFiles(root, "*" + VoiceNoteRules.CacheExtension, SearchOption.TopDirectoryOnly))
                File.Delete(file);
            foreach (string file in Directory.EnumerateFiles(root, "*" + VoiceNoteRules.CacheExtension, SearchOption.AllDirectories))
                if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
        }
        catch (IOException ex)
        {
            GD.PushWarning($"[voice] note cache prune failed: {ex.Message}");
        }
    }
}
