using Godot;
using LibreKO.Domain;

namespace LibreKO;

public static class WeaponMount
{
    private const int ShieldKind = 60;
    private const string RightWrist = "rightwrist";
    private const string LeftWrist = "leftwrist";
    private const string LeftForearm = "leftelbow";
    private const string RightArm = "rightarm";
    private const string LeftArm = "leftarm";

    public static int Bone(Skeleton3D skel, bool right, int itemId)
    {
        if (!right && ItemData.Get(itemId) is { Kind: ShieldKind })
        {
            int forearm = Find(skel, LeftForearm);
            if (forearm >= 0) return forearm;
        }
        int wrist = Find(skel, right ? RightWrist : LeftWrist);
        if (wrist < 0) return ArmChainEnd(skel, right ? RightArm : LeftArm);
        var kids = skel.GetBoneChildren(wrist);
        return kids.Length > 0 ? kids[0] : wrist;
    }

    private static int ArmChainEnd(Skeleton3D skel, string armKey)
    {
        int bone = Find(skel, armKey);
        if (bone < 0) return -1;
        while (DeepestChild(skel, bone) is var next and >= 0)
            bone = next;
        return bone;
    }

    private static int DeepestChild(Skeleton3D skel, int bone)
    {
        int best = -1, bestDepth = -1;
        foreach (int child in skel.GetBoneChildren(bone))
        {
            int depth = ChainDepth(skel, child);
            if (depth > bestDepth) { best = child; bestDepth = depth; }
        }
        return best;
    }

    private static int ChainDepth(Skeleton3D skel, int bone)
    {
        int deepest = 0;
        foreach (int child in skel.GetBoneChildren(bone))
            deepest = Mathf.Max(deepest, ChainDepth(skel, child));
        return deepest + 1;
    }

    private static int Find(Skeleton3D skel, string key)
    {
        for (int i = 0; i < skel.GetBoneCount(); i++)
            if (skel.GetBoneName(i).Replace(" ", "").ToLowerInvariant() == key) return i;
        return -1;
    }
}
