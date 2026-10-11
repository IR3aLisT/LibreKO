namespace LibreKO.Game.World;

public enum SoccerBallSpot
{
    InPlay,
    BlueScores,
    RedScores,
    Out,
}

public static class SoccerPitch
{
    public const float CentreX = 672f;
    public const float CentreZ = 160f;
    public const float BlueKickOffZ = 166f;
    public const float RedKickOffZ = 154f;
    public const float BlueExitX = 639f;
    public const float BlueExitZ = 194f;
    public const float RedExitX = 703f;
    public const float RedExitZ = 127f;

    public const float GoalMinX = 661f;
    public const float GoalMaxX = 681f;
    public const float RedGoalMinZ = 108f;
    public const float RedGoalMaxZ = 120f;
    public const float BlueGoalMinZ = 199f;
    public const float BlueGoalMaxZ = 208f;
    public const float FieldMinX = 644f;
    public const float FieldMaxX = 699f;
    public const float FieldMinZ = 120f;
    public const float FieldMaxZ = 200f;

    public const float SpectatorRange = 80f;
    public const float MaxKickReach = 15f;
    public const float MaxKickerDistance = 6f;

    public static SoccerBallSpot Classify(float x, float z)
    {
        bool inGoalMouth = x >= GoalMinX && x <= GoalMaxX;
        if (inGoalMouth && z >= RedGoalMinZ && z <= RedGoalMaxZ)
            return SoccerBallSpot.BlueScores;
        if (inGoalMouth && z >= BlueGoalMinZ && z <= BlueGoalMaxZ)
            return SoccerBallSpot.RedScores;
        bool onField = x >= FieldMinX && x <= FieldMaxX && z >= FieldMinZ && z <= FieldMaxZ;
        return onField ? SoccerBallSpot.InPlay : SoccerBallSpot.Out;
    }

    public static (float X, float Z) KickLanding(float ballX, float ballZ, float wantedX, float wantedZ)
    {
        float dx = wantedX - ballX, dz = wantedZ - ballZ;
        float length = MathF.Sqrt(dx * dx + dz * dz);
        if (length <= MaxKickReach)
            return (wantedX, wantedZ);
        float scale = MaxKickReach / length;
        return (ballX + dx * scale, ballZ + dz * scale);
    }
}
