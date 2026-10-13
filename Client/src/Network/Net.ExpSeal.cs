using System;
using LibreKO.Domain;

namespace LibreKO.Network;

public readonly record struct ExpSealReply(byte Sub, short Code, long Total)
{
    public static ExpSealReply? Read(Packet p)
    {
        if (p.RemainingBytes < 1) return null;
        byte sub = p.ReadByte();
        switch (sub)
        {
            case ExpSeal.SubOn or ExpSeal.SubOff when p.RemainingBytes >= 1:
                return new ExpSealReply(sub, p.ReadByte(), 0);
            case ExpSeal.SubStore when p.RemainingBytes >= sizeof(short):
            {
                short code = p.ReadShort();
                long total = code == ExpSeal.Succeeded && p.RemainingBytes >= sizeof(long) ? p.ReadLong() : 0;
                return new ExpSealReply(sub, code, total);
            }
            case ExpSeal.SubTotal when p.RemainingBytes >= sizeof(long):
                return new ExpSealReply(sub, ExpSeal.Succeeded, p.ReadLong());
            case ExpSeal.SubUseJar when p.RemainingBytes >= sizeof(short):
                return new ExpSealReply(sub, p.ReadShort(), 0);
            default:
                return null;
        }
    }
}

public partial class Net
{
    public event Action<ExpSealReply>? ExpSealEvent;
    public byte ExpSealState { get; private set; }
    public long SealedExperience { get; private set; }
    public long SealedGain { get; private set; }

    public bool ExpSealOn => ExpSealState == ExpSeal.StateOn;

    private void HandleExpSeal(Packet p)
    {
        if (ExpSealReply.Read(p) is not { } reply) return;
        SealedGain = 0;
        switch (reply.Sub)
        {
            case ExpSeal.SubOn when reply.Code == ExpSeal.Succeeded:
                ExpSealState = ExpSeal.StateOn;
                break;
            case ExpSeal.SubOff when reply.Code == ExpSeal.Succeeded:
                ExpSealState = ExpSeal.StateOff;
                break;
            case ExpSeal.SubStore when reply.Code == ExpSeal.Succeeded:
                SealedExperience = reply.Total;
                break;
            case ExpSeal.SubTotal:
                SealedGain = reply.Total - SealedExperience;
                SealedExperience = reply.Total;
                break;
        }
        ExpSealEvent?.Invoke(reply);
    }

    public void SendExpSealToggle(bool on) => SendExpSeal(on ? ExpSeal.SubOn : ExpSeal.SubOff, null);

    public void SendExpSealStore(byte bagPos, long amount) => SendExpSeal(ExpSeal.SubStore, p =>
    {
        p.WriteInt(ExpSeal.EmptyJar);
        p.WriteByte(bagPos);
        p.WriteLong(amount);
    });

    public void SendExpSealUse(int jar, byte bagPos) => SendExpSeal(ExpSeal.SubUseJar, p =>
    {
        p.WriteInt(jar);
        p.WriteByte(bagPos);
    });

    private void SendExpSeal(byte sub, Action<Packet>? body)
    {
        var p = new Packet(GameOpcodes.GS_EXP_SEAL);
        p.WriteByte(sub);
        body?.Invoke(p);
        _conn.Send(p);
    }

    private void ResetExpSeal(long sealedTotal)
    {
        ExpSealState = ExpSeal.StateUnknown;
        SealedExperience = sealedTotal;
        SealedGain = 0;
    }
}
