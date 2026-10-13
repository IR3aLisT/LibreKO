using Godot;

namespace LibreKO.Domain;

public static class SkySteer
{
    public static Vector3 Heading(float cameraYaw, Vector2 toCursor)
    {
        var forward = new Vector3(-Mathf.Sin(cameraYaw), 0f, -Mathf.Cos(cameraYaw));
        var right = forward.Cross(Vector3.Up);
        return (right * toCursor.X - forward * toCursor.Y).Normalized();
    }
}
