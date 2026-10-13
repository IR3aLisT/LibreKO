using System.Threading;
using Godot;
using Godot.Collections;

namespace LibreKO;

public sealed partial class RenderFaultWatch : Logger
{
    private const string MissingUniformSet = "Uniforms were never supplied for set";

    private static int _faults;
    private static bool _installed;

    public static int Faults => Volatile.Read(ref _faults);

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        OS.AddLogger(new RenderFaultWatch());
    }

    public override void _LogError(string function, string file, int line, string code, string rationale,
        bool editorNotify, int errorType, Array<ScriptBacktrace> scriptBacktraces)
    {
        if (code.Contains(MissingUniformSet) || rationale.Contains(MissingUniformSet))
            Interlocked.Increment(ref _faults);
    }
}
