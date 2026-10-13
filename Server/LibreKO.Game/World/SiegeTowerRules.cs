namespace LibreKO.Game.World;

public static class SiegeTowerRules
{
    public const int MovingTowerSkill = 450017;
    public const int GuardTowerSkill = 450018;
    public const short MovingTowerLook = 30070;
    public const short GuardTowerLook = 30210;
    public const byte GuardTowerNpcType = 191;
    public const float BoardReach = 7f;
    public const int WireScale = 10;

    private static readonly short[] SiegeLooks = [30010, 30040, 30050, 30060, 30061, MovingTowerLook, GuardTowerLook];

    public static bool InSiegeLook(short transformId) => Array.IndexOf(SiegeLooks, transformId) >= 0;

    public static bool Within(float ax, float ay, float az, float bx, float by, float bz, float reach)
    {
        float dx = ax - bx, dy = ay - by, dz = az - bz;
        return dx * dx + dy * dy + dz * dz <= reach * reach;
    }

    public static bool WithinGround(float ax, float az, float bx, float bz, float reach)
    {
        float dx = ax - bx, dz = az - bz;
        return dx * dx + dz * dz <= reach * reach;
    }

    public static bool SameSide(int clan, int alliance, int otherClan, int otherAlliance) =>
        (clan > 0 && clan == otherClan) || (alliance > 0 && alliance == otherAlliance);

    public static ushort Wire(float position) => (ushort)Math.Clamp((int)(position * WireScale), 0, ushort.MaxValue);
}
