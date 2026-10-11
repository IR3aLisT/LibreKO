namespace LibreKO.Domain;

public static class NpcTitle
{
    public static string Of(string? tableName, string? spawnedName, int npcId) =>
        tableName is { Length: > 0 } ? tableName
        : spawnedName is { Length: > 0 } ? spawnedName
        : $"NPC #{npcId}";
}
