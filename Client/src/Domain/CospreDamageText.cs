namespace LibreKO.Domain;

public static class CospreDamageText
{
    public static int Dealt(int classCode) => classCode switch
    {
        1 => 9605, 2 => 9606, 3 => 9607, 4 => 9608, 5 => 9615,
        _ => 0,
    };

    public static int Received(int classCode) => classCode switch
    {
        0 => 9614, 1 => 9609, 2 => 9610, 3 => 9611, 4 => 9612,
        _ => 0,
    };
}
