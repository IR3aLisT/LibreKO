using System;
using Godot;

namespace LibreKO;

public static class HoldRepeat
{
    public const double FirstDelay = 0.4;
    public const double Every = 0.08;

    public static void Attach(BaseButton button, Action step)
    {
        var timer = new Godot.Timer { OneShot = false };
        button.AddChild(timer);
        timer.Timeout += () =>
        {
            if (!button.IsPressed() || button.Disabled)
            {
                timer.Stop();
                return;
            }
            step();
            timer.WaitTime = Every;
        };
        button.ButtonDown += () =>
        {
            step();
            timer.Start(FirstDelay);
        };
        button.ButtonUp += timer.Stop;
    }
}
