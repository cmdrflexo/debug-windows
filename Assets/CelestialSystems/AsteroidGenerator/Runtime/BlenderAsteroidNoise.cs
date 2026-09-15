using UnityEngine;
namespace jcan.CelestialSystems
{
    public static partial class BlenderAsteroidGenerator
    {
        private enum TextureKind { Clouds, Musgrave, Distorted, Stucci, Voronoi }
        private sealed class Layer
        {
            public TextureKind Kind;
            public float Scale = 0.25f, Strength, Midlevel = 0.5f, Brightness = 1, Contrast = 1;
            public float Dimension = 1, Lacunarity = 2, Distortion = 1, Intensity = 1;
            public int Basis, DistortBasis, Depth = 2, Metric, StucciType;
            public bool Hard;
            public float Sample(Vector3 position)
            {
                Vector3 p = position / Mathf.Max(0.025f, Scale);
                float value;
                switch (Kind)
                {
                    case TextureKind.Voronoi: value = Cellular(p, Metric, 0); break;
                    case TextureKind.Musgrave:
                        value = 1; float weight = 1;
                        for (int i = 0; i < Depth; i++)
                        { value *= 1 + (BasisNoise(p, 0) * 2 - 1) * weight; weight *= Mathf.Pow(Lacunarity, -Dimension); p *= Lacunarity; }
                        value *= Intensity;
                        break;
                    case TextureKind.Distorted:
                        var warp = new Vector3(BasisNoise(p + new Vector3(13.5f, 0, 0), DistortBasis), BasisNoise(p, DistortBasis), BasisNoise(p - new Vector3(13.5f, 0, 0), DistortBasis));
                        value = BasisNoise(p + (warp * 2 - Vector3.one) * Distortion, Basis); break;
                    case TextureKind.Stucci:
                        // Approximation of the legacy Stucci turbulence kernel.
                        float baseNoise = BasisNoise(p, Basis);
                        float step = 0.05f * (StucciType == 0 ? 1 : baseNoise * baseNoise);
                        value = BasisNoise(p + Vector3.one * step, Basis);
                        if (Hard) value = Mathf.Abs(2 * value - 1);
                        if (StucciType == 1) value = 1 - value;
                        break;
                    default:
                        value = 0; float amplitude = 1, total = 0;
                        for (int i = 0; i <= Depth; i++)
                        { float t = BasisNoise(p, Basis); if (Hard) t = Mathf.Abs(2 * t - 1); value += amplitude * t; total += amplitude; amplitude *= 0.5f; p *= 2; }
                        value /= total; break;
                }
                return Mathf.Clamp01((value - 0.5f) * Contrast + Brightness - 0.5f);
            }
        }
        private static Layer[] CreateLayers(BlenderAsteroidSettings s, RandomStream rng)
        {
            var broad = RandomLayer(TextureKind.Musgrave, 0, rng);
            var cells = RandomLayer(TextureKind.Voronoi, 0, rng);
            var medium = RandomLayer((TextureKind)Mathf.Clamp(Mathf.RoundToInt(-Mathf.Log(rng.Unit()) / 2.125f), 0, 4), 1, rng);
            var fine = RandomLayer((TextureKind)Mathf.Clamp(Mathf.RoundToInt(-Mathf.Log(rng.Unit()) / 2.125f), 0, 4), 2, rng);
            float deform = Mathf.Clamp(s.Deformation, 0, 1024) / 10;
            float rough = Mathf.Clamp(s.Roughness, 0, 1024) / 100;
            broad.Strength = rng.Gaussian(deform / 100, deform / 300); broad.Midlevel = 0;
            cells.Strength = rng.Gaussian(deform, deform / 3); cells.Midlevel = 0;
            medium.Strength = rng.Gaussian(rough * 2, rough / 3);
            fine.Strength = rng.Gaussian(rough, rough / 3);
            return new[] { broad, cells, medium, fine };
        }
        private static Layer RandomLayer(TextureKind kind, int level, RandomStream rng)
        {
            var t = new Layer { Kind = kind };
            switch (kind)
            {
                case TextureKind.Clouds: t.Hard = rng.Integer(2) != 0; t.Basis = rng.Integer(7); t.Depth = 8; break;
                case TextureKind.Musgrave:
                    t.Dimension = Mathf.Abs(rng.Gaussian(0, 0.6f)) + 0.2f; t.Lacunarity = rng.Beta38() * 8.2f + 1.8f;
                    t.Depth = level == 0 ? 1 : 8;
                    if (level == 0) t.Intensity = 0.2f;
                    else { t.Brightness = rng.Gaussian(1, 1f / 6); t.Contrast = 0.2f; }
                    break;
                case TextureKind.Distorted:
                    t.DistortBasis = rng.Integer(9); t.Basis = rng.Integer(9);
                    t.Distortion = SkewedGaussian(2, 2.6666f, new Vector2(0, 10), false, rng); break;
                case TextureKind.Stucci: t.Hard = rng.Integer(2) != 0; t.StucciType = rng.Integer(3); t.Basis = rng.Integer(7); break;
                case TextureKind.Voronoi:
                    t.Metric = rng.Integer(level == 0 ? 2 : 7);
                    if (level == 0) { t.Contrast = 0.5f; t.Brightness = 0.7f; }
                    break;
            }
            if (level == 0) t.Scale = rng.Gaussian(0.625f, 1f / 24);
            else if (level == 2) t.Scale = 0.15f;
            return t;
        }
        // Fixed, deterministic 3D fields. Unlike the old implementation, noise is sampled in
        // Cartesian object space; no spherical cells, latitude mapping or sine-band displacement.
        private static uint Hash(int x, int y, int z, uint salt)
        {
            unchecked { uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)z * 0xCB1AB31Fu ^ salt; h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; return h ^ (h >> 16); }
        }
        private static float UnitHash(int x, int y, int z, uint salt) => (Hash(x, y, z, salt) & 0xFFFFFFu) / 16777216f;
        private static float BasisNoise(Vector3 p, int basis)
        {
            // Approximation: Blender Original/Original Perlin use this improved-gradient kernel too.
            return basis < 3 ? GradientNoise(p) : Cellular(p, 0, basis - 3);
        }
        private static float Fade(float x) => x * x * x * (x * (x * 6 - 15) + 10);
        private static float Gradient(uint hash, float x, float y, float z)
        {
            int h = (int)(hash & 15); float u = h < 8 ? x : y, v = h < 4 ? y : h == 12 || h == 14 ? x : z;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
        private static float GradientNoise(Vector3 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float a = p.x - x, b = p.y - y, c = p.z - z, u = Fade(a), v = Fade(b), w = Fade(c);
            float n000 = Gradient(Hash(x,y,z,0),a,b,c), n100 = Gradient(Hash(x+1,y,z,0),a-1,b,c);
            float n010 = Gradient(Hash(x,y+1,z,0),a,b-1,c), n110 = Gradient(Hash(x+1,y+1,z,0),a-1,b-1,c);
            float n001 = Gradient(Hash(x,y,z+1,0),a,b,c-1), n101 = Gradient(Hash(x+1,y,z+1,0),a-1,b,c-1);
            float n011 = Gradient(Hash(x,y+1,z+1,0),a,b-1,c-1), n111 = Gradient(Hash(x+1,y+1,z+1,0),a-1,b-1,c-1);
            return Mathf.Clamp01(0.5f + 0.5f * Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(n000,n100,u), Mathf.Lerp(n010,n110,u),v), Mathf.Lerp(Mathf.Lerp(n001,n101,u),Mathf.Lerp(n011,n111,u),v),w));
        }
        private static float Cellular(Vector3 p, int metric, int basis)
        {
            int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y), cz = Mathf.FloorToInt(p.z);
            float f1 = float.MaxValue, f2 = f1, f3 = f1, f4 = f1;
            // 5^3 neighborhood avoids F1 discontinuities from a narrow 3^3 search.
            for (int z = cz - 2; z <= cz + 2; z++) for (int y = cy - 2; y <= cy + 2; y++) for (int x = cx - 2; x <= cx + 2; x++)
            {
                float a = Mathf.Abs(x + UnitHash(x,y,z,0x123u) - p.x), b = Mathf.Abs(y + UnitHash(x,y,z,0x456u) - p.y), c = Mathf.Abs(z + UnitHash(x,y,z,0x789u) - p.z);
                float d;
                switch (metric)
                {
                    case 1: d = a*a+b*b+c*c; break;
                    case 2: d = a+b+c; break;
                    case 3: d = Mathf.Max(a,Mathf.Max(b,c)); break;
                    case 4: d = Mathf.Pow(Mathf.Sqrt(a)+Mathf.Sqrt(b)+Mathf.Sqrt(c),2); break;
                    case 5: d = Mathf.Pow(a*a*a*a+b*b*b*b+c*c*c*c,0.25f); break;
                    case 6: d = Mathf.Pow(Mathf.Pow(a,2.5f)+Mathf.Pow(b,2.5f)+Mathf.Pow(c,2.5f),0.4f); break;
                    default: d = Mathf.Sqrt(a*a+b*b+c*c); break;
                }
                if (d < f1) { f4=f3; f3=f2; f2=f1; f1=d; } else if (d < f2) { f4=f3; f3=f2; f2=d; } else if (d < f3) { f4=f3; f3=d; } else if (d < f4) f4=d;
            }
            switch (basis) { case 1: return f2; case 2: return f3; case 3: return f4; case 4: return f2-f1; case 5: return Mathf.Min(1,10*(f2-f1)); default: return f1; }
        }
    }
}
