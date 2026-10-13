using System;
using System.Collections.Generic;

namespace LibreKO.Domain;

public enum DeathPose
{
    Disjoint = 0,
    KnockDown = 1,
    KeepPosition = 2,
}

public static class DeathPoses
{
    public const int PlayerKeepClip = 8;
    public const int PlayerKnockDownClip = 9;
    public const int PlayerDisjointClip = 10;
    public const int MonsterKeepClip = 10;
    public const int MonsterFallClip = 11;
    public const int UnpushedModel = 30300;

    public const float PushSpeed = 5f;
    public const float PushFullSeconds = 0.2f;
    public const float PushEndSeconds = 0.4f;
    public const float PushBase = 0.3f;
    public const float PushSizeShare = 0.7f;
    private const float MinimumSize = 0.01f;

    private static readonly HashSet<int> TwoHandedKinds = new() { 22, 32, 42, 52, 61, 62, 63 };
    private static readonly HashSet<int> OneHandedKinds = new() { 21, 31, 41, 51 };

    public static DeathPose ForKillerKind(int rightHandKind) =>
        TwoHandedKinds.Contains(rightHandKind) ? DeathPose.Disjoint
        : OneHandedKinds.Contains(rightHandKind) ? DeathPose.KnockDown
        : DeathPose.KeepPosition;

    public static int ClipIndex(DeathPose pose, bool monster) => monster
        ? pose == DeathPose.KeepPosition ? MonsterKeepClip : MonsterFallClip
        : pose switch
        {
            DeathPose.Disjoint => PlayerDisjointClip,
            DeathPose.KnockDown => PlayerKnockDownClip,
            _ => PlayerKeepClip,
        };

    public static bool Pushes(DeathPose pose, int modelId) => pose != DeathPose.KeepPosition && modelId != UnpushedModel;

    public static float PushSpeedAt(float seconds) =>
        seconds <= PushFullSeconds ? PushSpeed
        : seconds < PushEndSeconds ? PushSpeed * (PushEndSeconds - seconds)
        : 0f;

    public static float PushStep(float seconds, float size, float delta) =>
        (PushBase + PushSizeShare / Math.Max(size, MinimumSize)) * PushSpeedAt(seconds) * delta;
}
