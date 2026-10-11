using Godot;

namespace LibreKO;

internal interface IWorldContext
{
    Node3D Root { get; }

    int SelfCharId { get; }

    Node3D? BodyOf(int charId);

    float HeadHeight(int charId);

    PlateStack? PlateOf(int charId);

    double Now { get; }
}
