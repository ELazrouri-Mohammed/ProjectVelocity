using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// Generated textures for the vertical slice: brutalist formwork concrete, the panoramic sky, soft particle sprites and the
    /// HUD's sprites. Small (the art leans on shape, light and fog, not texture density) and deterministic. Each is created once;
    /// delete the file to regenerate it.
    /// </summary>
    static class SliceTextures
    {
        /// <summary>The sun's direction (toward the sun): ahead and to the left of the route, low.</summary>
        public static readonly Vector3 SunDirection = new Vector3(-0.47f, 0.31f, 0.83f).normalized;

        /// <summary>Concrete covering 4 x 4 m (512 px): 2 x 1 m formwork panels, tie holes, soft weathering. Grey, tinted by material.</summary>
        public static Texture2D Concrete(string path)
        {
            return LoadOrCreate(path, 512, 512, (x, y) =>
            {
                float n = Fbm(x / 64f, y / 64f, 4, 11) * 0.06f + Fbm(x / 9f, y / 9f, 2, 23) * 0.03f;
                float v = 0.93f + n;
                // Panel seams: vertical every 2 m (256 px), horizontal every 1 m (128 px).
                int sx = x % 256;
                int sy = y % 128;
                float seam = Mathf.Min(Mathf.Min(sx, 256 - sx), Mathf.Min(sy, 128 - sy));
                if (seam < 2.5f)
                    v -= 0.2f * (1f - seam / 2.5f);
                // Tie holes, two rows per panel.
                float hx = Mathf.Abs((sx % 128) - 64f);
                float hy = Mathf.Abs(sy - 64f);
                float hole = Mathf.Sqrt(hx * hx + hy * hy);
                if (hole < 4f)
                    v -= 0.25f * (1f - hole / 4f);
                // Rain streaks running down from the seams.
                float streak = Fbm(x / 3f, 0.5f, 2, 41);
                v -= Mathf.Clamp01(streak - 0.55f) * 0.25f * (1f - sy / 128f);
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
                return new Color32(b, b, b, 255);
            }, importer =>
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.mipmapEnabled = true;
            });
        }

        /// <summary>
        /// Latitude-longitude sky (for Skybox/Panoramic): desaturated teal zenith, warm pale haze at the horizon, the low sun's glow,
        /// thin high cloud bands and one faint, enormous halo ring straight ahead down the route: the anomaly, hanging in the sky.
        /// </summary>
        public static Texture2D Sky(string path)
        {
            const int width = 1024;
            const int height = 512;
            Vector3 halo = Quaternion.Euler(-22f, 0f, 0f) * Vector3.forward;
            return LoadOrCreate(path, width, height, (x, y) =>
            {
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;
                // Skybox/Panoramic: u = 0.5 - atan2(z, x) / 2pi, v = 1 - acos(y) / pi.
                float longitude = (0.5f - u) * Mathf.PI * 2f;
                float latitude = (1f - v) * Mathf.PI;
                var dir = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                float elevation = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;

                Color zenith = new Color(0.36f, 0.48f, 0.57f);
                Color upper = new Color(0.62f, 0.68f, 0.72f);
                Color horizon = new Color(0.92f, 0.85f, 0.76f);
                Color below = new Color(0.56f, 0.54f, 0.53f);
                Color deep = new Color(0.30f, 0.31f, 0.34f);
                Color c;
                if (elevation >= 0f)
                {
                    float t = Mathf.Pow(elevation / 90f, 0.55f);
                    c = t < 0.45f ? Color.Lerp(horizon, upper, t / 0.45f) : Color.Lerp(upper, zenith, (t - 0.45f) / 0.55f);
                }
                else
                {
                    float t = Mathf.Pow(-elevation / 90f, 0.6f);
                    c = Color.Lerp(horizon, below, Mathf.Clamp01(t * 3f));
                    c = Color.Lerp(c, deep, Mathf.Clamp01((t - 0.33f) / 0.67f));
                }

                // Sun glow: a wide warm wash and a tight bright core.
                float toSun = Vector3.Angle(dir, SunDirection);
                c += new Color(1f, 0.78f, 0.55f) * (0.35f * Mathf.Exp(-toSun * toSun / (2f * 18f * 18f)));
                c += new Color(1f, 0.95f, 0.85f) * (0.9f * Mathf.Exp(-toSun * toSun / (2f * 2.2f * 2.2f)));

                // High cloud bands just above the horizon.
                if (elevation > 2f && elevation < 35f)
                {
                    float band = Fbm(u * 18f, elevation / 3.5f, 3, 7);
                    float fade = Mathf.Sin(Mathf.InverseLerp(2f, 35f, elevation) * Mathf.PI);
                    c = Color.Lerp(c, new Color(0.97f, 0.93f, 0.88f), Mathf.Clamp01(band - 0.52f) * 1.4f * fade);
                }

                // The halo: a thin, faint ring of light far ahead.
                float ringAngle = Vector3.Angle(dir, halo);
                float ring = Mathf.Exp(-Mathf.Pow((ringAngle - 26f) / 0.9f, 2f)) * 0.22f + Mathf.Exp(-Mathf.Pow((ringAngle - 26f) / 4f, 2f)) * 0.05f;
                c += new Color(0.75f, 0.95f, 1f) * ring;

                return (Color32)new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
            }, importer =>
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
            });
        }

        /// <summary>A soft round particle (white, alpha falloff).</summary>
        public static Texture2D SoftDot(string path)
        {
            return LoadOrCreate(path, 64, 64, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                return new Color32(255, 255, 255, (byte)(a * 255f));
            }, ParticleImport);
        }

        /// <summary>A thin soft ring (white, alpha), for expanding shock rings.</summary>
        public static Texture2D RingTexture(string path)
        {
            return LoadOrCreate(path, 128, 128, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64f, 64f)) / 64f;
                float a = Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.07f, 2f));
                return new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }, ParticleImport);
        }

        /// <summary>Screen-edge glow for the HUD: clear in the middle, opaque at the edges.</summary>
        public static Sprite Vignette(string path)
        {
            LoadOrCreate(path, 256, 256, (x, y) =>
            {
                float dx = Mathf.Abs((x + 0.5f) / 128f - 1f);
                float dy = Mathf.Abs((y + 0.5f) / 128f - 1f);
                float d = Mathf.Pow(Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f), 0.25f);
                float a = Mathf.Clamp01((d - 0.55f) / 0.45f);
                return new Color32(255, 255, 255, (byte)(a * a * 255f));
            }, SpriteImport);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>A diamond outline reticle (tether anchors).</summary>
        public static Sprite Diamond(string path)
        {
            LoadOrCreate(path, 128, 128, (x, y) =>
            {
                float d = (Mathf.Abs(x + 0.5f - 64f) + Mathf.Abs(y + 0.5f - 64f)) / 64f;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.06f);
                return new Color32(255, 255, 255, (byte)(a * 255f));
            }, SpriteImport);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Corner brackets (the pulse target).</summary>
        public static Sprite Brackets(string path)
        {
            LoadOrCreate(path, 128, 128, (x, y) =>
            {
                float px = Mathf.Abs(x + 0.5f - 64f);
                float py = Mathf.Abs(y + 0.5f - 64f);
                bool edgeX = px > 52f && px < 58f && py > 30f && py < 58f;
                bool edgeY = py > 52f && py < 58f && px > 30f && px < 58f;
                return new Color32(255, 255, 255, (byte)(edgeX || edgeY ? 255 : 0));
            }, SpriteImport);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>A plain white sprite (full-screen flashes, panels).</summary>
        public static Sprite White(string path)
        {
            LoadOrCreate(path, 8, 8, (x, y) => new Color32(255, 255, 255, 255), SpriteImport);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void ParticleImport(TextureImporter importer)
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        static void SpriteImport(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        static Texture2D LoadOrCreate(string path, int width, int height, Func<int, int, Color32> pixel, Action<TextureImporter> configure)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = pixel(x, y);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                configure(importer);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ Deterministic noise

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 2147483647;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        static float ValueNoise(float x, float y, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, seed);
            float b = Hash(x0 + 1, y0, seed);
            float c = Hash(x0, y0 + 1, seed);
            float d = Hash(x0 + 1, y0 + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float x, float y, int octaves, int seed)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            float total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x, y, seed + i * 17) * amplitude;
                total += amplitude;
                x *= 2.03f;
                y *= 2.03f;
                amplitude *= 0.5f;
            }
            return sum / total;
        }
    }
}
