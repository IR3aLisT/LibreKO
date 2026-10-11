using System;
using System.Collections.Generic;
using LibreKO.Domain;

namespace LibreKO.Network;

public partial class Net
{
    public event Action<int, int, int>? PremiumEvent;

    public int PremiumAccountStatus { get; private set; }
    public int PremiumType { get; private set; }
    public int PremiumHours { get; private set; }
    public IReadOnlyList<PremiumBonus> PremiumBonuses { get; private set; } = Array.Empty<PremiumBonus>();

    private const byte PremiumListSub = 1;
    private const int PremiumEntryBytes = 3;

    internal static (int Status, int Type, int Hours)? ReadPremiumList(Packet p)
    {
        if (p.RemainingBytes < 2 || p.ReadByte() != PremiumListSub) return null;
        int count = p.ReadByte();
        var hoursByType = new Dictionary<int, int>();
        for (int i = 0; i < count && p.RemainingBytes >= PremiumEntryBytes; i++)
        {
            int type = p.ReadByte();
            hoursByType[type] = p.ReadUShort();
        }
        int inUse = p.RemainingBytes >= 1 ? p.ReadByte() : 0;
        int hours = count > 0 && hoursByType.TryGetValue(inUse, out int left) ? left : 0;
        return hours > 0 ? (PremiumActive, inUse, hours) : (PremiumNone, 0, 0);
    }

    private const int PremiumNone = 0;
    private const int PremiumActive = 1;

    private void HandlePremium(Packet p)
    {
        if (ReadPremiumList(p) is not { } premium) return;
        var (accountStatus, premiumType, premiumHours) = premium;

        PremiumAccountStatus = accountStatus;
        PremiumType = premiumType;
        PremiumHours = premiumHours;
        PremiumBonuses = ReadPremiumBonuses(p);

        PremiumEvent?.Invoke(accountStatus, premiumType, premiumHours);
    }

    internal static IReadOnlyList<PremiumBonus> ReadPremiumBonuses(Packet p)
    {
        if (p.RemainingBytes < 1) return Array.Empty<PremiumBonus>();
        int count = p.ReadByte();
        var bonuses = new List<PremiumBonus>(count);
        for (int i = 0; i < count && p.RemainingBytes >= PremiumBonusBytes; i++)
            bonuses.Add(new PremiumBonus((PremiumBonusKind)p.ReadByte(), p.ReadShort()));
        return bonuses;
    }

    private const int PremiumBonusBytes = 3;

    internal void SeedPreviewPremium(int accountStatus, int premiumType, int premiumHours, IReadOnlyList<PremiumBonus>? bonuses = null)
    {
        PremiumAccountStatus = accountStatus;
        PremiumType = premiumType;
        PremiumHours = premiumHours;
        PremiumBonuses = bonuses ?? Array.Empty<PremiumBonus>();
    }

    public void SendPremiumRequest()
    {
        var p = new Packet(GameOpcodes.GS_PREMIUM);
        _conn.Send(p);
    }
}
