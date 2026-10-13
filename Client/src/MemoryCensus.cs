using System;
using Godot;

namespace LibreKO;

public static class MemoryCensus
{
    private const double BytesPerMegabyte = 1024.0 * 1024.0;
    private const int FullCollectionGeneration = 2;

    public static void Log(string where)
    {
        double vram = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.VideoMemUsed) / BytesPerMegabyte;
        double textures = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TextureMemUsed) / BytesPerMegabyte;
        double buffers = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.BufferMemUsed) / BytesPerMegabyte;
        double resources = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
        double objects = Performance.GetMonitor(Performance.Monitor.ObjectCount);
        double nodes = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        double orphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        double managed = GC.GetTotalMemory(false) / BytesPerMegabyte;
        int fullCollections = GC.CollectionCount(FullCollectionGeneration);
        GD.Print($"[mem] {where}: vram {vram:0} MB (textures {textures:0}, buffers {buffers:0}), resources {resources:0}, " +
            $"objects {objects:0}, nodes {nodes:0}, orphans {orphans:0}, managed {managed:0} MB, full GCs {fullCollections}, " +
            $"render faults {RenderFaultWatch.Faults}");
    }
}
