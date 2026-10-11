using System;
using System.Runtime.CompilerServices;
using Godot;

namespace LibreKO;

internal static class MeshArraysCache
{
    internal sealed class Surface
    {
        public Vector3[] Vertices = Array.Empty<Vector3>();
        public int[] Indices = Array.Empty<int>();
        public int[] Bones = Array.Empty<int>();
        public float[] Weights = Array.Empty<float>();
    }

    private static readonly ConditionalWeakTable<Mesh, Surface?[]> Cache = new();

    public static Surface Get(Mesh mesh, int surface)
    {
        var surfaces = Cache.GetValue(mesh, m => new Surface?[m.GetSurfaceCount()]);
        if (surface < 0 || surface >= surfaces.Length) return Read(mesh, surface);
        return surfaces[surface] ??= Read(mesh, surface);
    }

    private static Surface Read(Mesh mesh, int surface)
    {
        using var arrays = mesh.SurfaceGetArrays(surface);
        bool Has(Mesh.ArrayType type) => arrays.Count > (int)type && arrays[(int)type].VariantType != Variant.Type.Nil;
        return new Surface
        {
            Vertices = Has(Mesh.ArrayType.Vertex) ? arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array() : Array.Empty<Vector3>(),
            Indices = Has(Mesh.ArrayType.Index) ? arrays[(int)Mesh.ArrayType.Index].AsInt32Array() : Array.Empty<int>(),
            Bones = Has(Mesh.ArrayType.Bones) ? arrays[(int)Mesh.ArrayType.Bones].AsInt32Array() : Array.Empty<int>(),
            Weights = Has(Mesh.ArrayType.Weights) ? arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array() : Array.Empty<float>(),
        };
    }
}
