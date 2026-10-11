namespace LibreKO.Domain;

public static class Fall
{
    public const float Gravity = 9.8f;

    public static float Step(ref float speed, float y, float groundY, float delta)
    {
        if (groundY >= y)
        {
            speed = 0f;
            return groundY;
        }
        speed += Gravity * delta;
        float fallen = y - speed;
        if (fallen > groundY) return fallen;
        speed = 0f;
        return groundY;
    }
}
