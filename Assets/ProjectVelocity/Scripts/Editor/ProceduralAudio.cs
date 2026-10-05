using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// Temporary sounds for the slice, synthesised here (no downloaded or licensed audio): short noise-and-oscillator designs for
    /// movement, combat, the tether, enemies and the world, plus two seamless loops. Written once as WAV files under
    /// Generated/Slice/Audio and assigned to empty Sound Bank slots, so replacing any of them later is a drag and drop. The same
    /// code always writes the same files.
    /// </summary>
    static class ProceduralAudio
    {
        const int Rate = 22050;

        /// <summary>Loads or creates the bank, generates any missing sound files, and fills empty slots. Assigned clips are kept.</summary>
        public static SoundBank BuildBank(string bankPath, string folder)
        {
            SoundBank bank = MovementTestBuilder.LoadOrCreateAsset<SoundBank>(bankPath);
            var entries = new List<SoundBank.Entry>(bank.Entries ?? new SoundBank.Entry[0]);
            bool changed = false;
            foreach (SoundId id in (SoundId[])Enum.GetValues(typeof(SoundId)))
            {
                SoundBank.Entry entry = entries.Find(e => e != null && e.id == id);
                if (entry == null)
                {
                    entry = new SoundBank.Entry { id = id, volume = DefaultVolume(id), pitchVariance = DefaultVariance(id) };
                    entries.Add(entry);
                    changed = true;
                }
                if (entry.clip != null)
                    continue;
                entry.clip = LoadOrCreateClip($"{folder}/{id}.wav", id);
                changed |= entry.clip != null;
            }
            if (changed)
            {
                bank.Entries = entries.ToArray();
                EditorUtility.SetDirty(bank);
            }
            return bank;
        }

        static float DefaultVolume(SoundId id)
        {
            switch (id)
            {
                case SoundId.Footstep: return 0.35f;
                case SoundId.AmbientLoop: return 0.8f;
                case SoundId.WindLoop: return 0.9f;
                case SoundId.WorldRumble: return 0.85f;
                case SoundId.Death: return 0.9f;
                default: return 0.8f;
            }
        }

        static float DefaultVariance(SoundId id)
        {
            switch (id)
            {
                case SoundId.Footstep: return 0.12f;
                case SoundId.Slash: return 0.08f;
                case SoundId.AmbientLoop:
                case SoundId.WindLoop:
                case SoundId.Checkpoint:
                case SoundId.Finish:
                    return 0f;
                default: return 0.05f;
            }
        }

        static AudioClip LoadOrCreateClip(string path, SoundId id)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null)
                return existing;
            float[] samples = Synthesize(id);
            if (samples == null)
                return null;
            WriteWav(path, samples);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                bool loop = id == SoundId.AmbientLoop || id == SoundId.WindLoop;
                settings.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ------------------------------------------------------------------ Designs

        static float[] Synthesize(SoundId id)
        {
            var r = new System.Random(1000 + (int)id);
            switch (id)
            {
                case SoundId.Footstep:
                    return Mix(0.1f, Noise(0.1f, r, 0.6f).LowPass(1800f).Env(0.002f, 0.03f), Sine(0.1f, 95f, 70f, 0.8f).Env(0.002f, 0.04f));
                case SoundId.Jump:
                    return Mix(0.25f, Noise(0.25f, r, 0.7f).Band(600f, 3200f).Swell(0.08f, 0.17f), Sine(0.25f, 170f, 260f, 0.3f).Env(0.01f, 0.1f));
                case SoundId.Land:
                    return Mix(0.32f, Sine(0.32f, 110f, 50f, 1f).Env(0.002f, 0.14f), Noise(0.32f, r, 0.6f).LowPass(1300f).Env(0.002f, 0.1f));
                case SoundId.LandHard:
                    return Mix(0.7f, Sine(0.7f, 85f, 32f, 1f).Env(0.002f, 0.3f), Noise(0.7f, r, 0.8f).LowPass(1600f).Env(0.002f, 0.18f),
                        Noise(0.7f, r, 0.4f).LowPass(250f).Env(0.05f, 0.5f));
                case SoundId.Boost:
                    return Mix(0.6f, Noise(0.6f, r, 0.8f).Band(500f, 5000f).Swell(0.05f, 0.5f), Sine(0.6f, 220f, 900f, 0.35f).Tremolo(24f, 0.4f).Env(0.01f, 0.4f),
                        Sine(0.6f, 70f, 40f, 0.9f).Env(0.002f, 0.12f));
                case SoundId.WallRun:
                    return Mix(0.5f, Noise(0.5f, r, 0.6f).Band(1800f, 6000f).Tremolo(32f, 0.6f).Env(0.01f, 0.4f));
                case SoundId.WallJump:
                    return Mix(0.3f, Sine(0.3f, 120f, 60f, 0.9f).Env(0.002f, 0.1f), Noise(0.3f, r, 0.6f).Band(700f, 3500f).Swell(0.04f, 0.2f));
                case SoundId.Slash:
                    return Mix(0.22f, Noise(0.22f, r, 1f).Band(2200f, 8000f).Swell(0.05f, 0.14f));
                case SoundId.SlashHit:
                    return Mix(0.35f, Noise(0.35f, r, 0.8f).HighPass(2000f).Env(0.001f, 0.05f), Ring(0.35f, 0.5f, 0.2f, 1300f, 2100f, 3400f),
                        Sine(0.35f, 140f, 70f, 0.7f).Env(0.002f, 0.1f));
                case SoundId.Kill:
                    return Mix(0.65f, Shatter(0.65f, r, 14, 2000f, 6000f, 0.25f), Sine(0.65f, 120f, 50f, 0.8f).Env(0.002f, 0.15f),
                        Noise(0.65f, r, 0.4f).HighPass(3000f).Env(0.001f, 0.12f));
                case SoundId.HeavyKill:
                    return Mix(1.3f, Sine(1.3f, 62f, 26f, 1f).Env(0.003f, 0.6f), Noise(1.3f, r, 0.7f).LowPass(800f).Env(0.01f, 0.8f),
                        Shatter(1.3f, r, 18, 1200f, 4500f, 0.45f));
                case SoundId.Deflect:
                    return Mix(0.55f, Ring(0.55f, 0.8f, 0.45f, 520f, 1240f, 1890f, 2650f), Noise(0.55f, r, 0.7f).HighPass(2500f).Env(0.001f, 0.03f));
                case SoundId.PulseFire:
                    return Mix(0.28f, Sine(0.28f, 1900f, 450f, 0.5f).Env(0.002f, 0.18f), Sine(0.28f, 3800f, 900f, 0.2f).Env(0.002f, 0.12f));
                case SoundId.PulseHit:
                    return Mix(0.32f, Noise(0.32f, r, 0.6f).Band(800f, 6000f).Env(0.001f, 0.08f), Sine(0.32f, 900f, 260f, 0.5f).Env(0.002f, 0.2f));
                case SoundId.TetherFire:
                    return Mix(0.2f, Sine(0.2f, 300f, 2600f, 0.45f).Env(0.002f, 0.15f), Noise(0.2f, r, 0.4f).HighPass(3000f).Env(0.002f, 0.1f));
                case SoundId.TetherConnect:
                    return Mix(0.4f, Ring(0.4f, 0.7f, 0.3f, 700f, 1650f, 2800f), Sine(0.4f, 150f, 90f, 0.8f).Env(0.002f, 0.08f));
                case SoundId.TetherRelease:
                    return Mix(0.3f, Noise(0.3f, r, 0.8f).HighPass(3500f).Env(0.001f, 0.02f), Noise(0.3f, r, 0.5f).Band(600f, 3000f).Swell(0.03f, 0.25f));
                case SoundId.TargetLaunch:
                    return Mix(0.55f, Sine(0.55f, 200f, 1300f, 0.4f).Env(0.01f, 0.4f), Noise(0.55f, r, 0.6f).Band(800f, 6000f).Swell(0.1f, 0.4f));
                case SoundId.EnemyCharge:
                    return Mix(0.8f, Sine(0.8f, 300f, 1450f, 0.45f).TremoloRamp(8f, 40f, 0.5f).Swell(0.7f, 0.08f));
                case SoundId.EnemyFire:
                    return Mix(0.38f, Square(0.38f, 1200f, 180f, 0.4f).Env(0.002f, 0.25f), Noise(0.38f, r, 0.5f).Band(1000f, 5000f).Env(0.002f, 0.1f));
                case SoundId.BoltImpact:
                    return Mix(0.3f, Noise(0.3f, r, 0.7f).LowPass(3000f).Env(0.001f, 0.08f), Sine(0.3f, 420f, 140f, 0.6f).Env(0.002f, 0.15f));
                case SoundId.HoundScreech:
                    return Mix(0.5f, Fm(0.5f, 1300f, 1900f, 70f, 600f, 0.4f).Swell(0.1f, 0.35f));
                case SoundId.HoundDash:
                    return Mix(0.38f, Noise(0.38f, r, 0.9f).Band(1500f, 7000f).Swell(0.05f, 0.3f), Ring(0.38f, 0.3f, 0.2f, 1800f, 2700f));
                case SoundId.WardenCharge:
                    return Mix(0.95f, Square(0.95f, 55f, 120f, 0.5f).LowPass(700f).Swell(0.8f, 0.12f), Noise(0.95f, r, 0.5f).LowPass(300f).Swell(0.8f, 0.12f),
                        Sine(0.95f, 400f, 1100f, 0.2f).Swell(0.85f, 0.08f));
                case SoundId.WardenSlam:
                    return Mix(1.1f, Sine(1.1f, 58f, 24f, 1f).Env(0.003f, 0.55f), Noise(1.1f, r, 0.9f).LowPass(2000f).Env(0.001f, 0.15f),
                        Noise(1.1f, r, 0.5f).LowPass(220f).Env(0.05f, 0.8f));
                case SoundId.EnemyDeath:
                    return Mix(0.55f, Sine(0.55f, 620f, 70f, 0.5f).Env(0.002f, 0.4f), Noise(0.55f, r, 0.5f).Band(500f, 4000f).Crackle(r, 0.4f).Env(0.002f, 0.35f));
                case SoundId.ShieldBreak:
                    return Mix(0.55f, Shatter(0.55f, r, 10, 1500f, 5000f, 0.2f), Sine(0.55f, 1200f, 180f, 0.5f).Env(0.002f, 0.35f),
                        Sine(0.55f, 90f, 50f, 0.8f).Env(0.002f, 0.12f));
                case SoundId.ShieldRestore:
                    return Mix(0.45f, Sine(0.45f, 600f, 1250f, 0.35f).Env(0.05f, 0.3f), Sine(0.45f, 900f, 1880f, 0.2f).Env(0.05f, 0.25f));
                case SoundId.Death:
                    return Mix(0.95f, Sine(0.95f, 820f, 55f, 0.6f).BitCrush(10f).Env(0.002f, 0.7f), Noise(0.95f, r, 0.7f).Band(200f, 3000f).Env(0.002f, 0.3f),
                        Sine(0.95f, 60f, 30f, 0.9f).Env(0.002f, 0.4f));
                case SoundId.Checkpoint:
                    return Mix(1.3f, Bell(1.3f, 660f, 0.9f), Bell(1.3f, 990f, 0.7f), Bell(1.3f, 1320f, 0.5f), Noise(1.3f, r, 0.2f).HighPass(5000f).Swell(0.05f, 1f));
                case SoundId.Finish:
                    return Mix(2.6f, Bell(2.6f, 440f, 1f), Bell(2.6f, 660f, 0.8f), Bell(2.6f, 880f, 0.7f), Bell(2.6f, 1320f, 0.5f),
                        Noise(2.6f, r, 0.25f).Band(2000f, 8000f).Swell(0.6f, 1.8f));
                case SoundId.WorldWarning:
                    return Mix(0.6f, Beeps(0.6f, 110f, 2, 0.12f, 0.18f), Noise(0.6f, r, 0.2f).LowPass(600f).Env(0.01f, 0.4f));
                case SoundId.WorldRumble:
                    return Mix(1.8f, Noise(1.8f, r, 1f).LowPass(320f).Tremolo(7f, 0.5f).Swell(0.3f, 1.4f), Sine(1.8f, 42f, 36f, 0.8f).Swell(0.3f, 1.4f),
                        Noise(1.8f, r, 0.3f).Band(800f, 2500f).Crackle(r, 0.15f).Swell(0.4f, 1.2f));
                case SoundId.WorldImpact:
                    return Mix(1.2f, Sine(1.2f, 72f, 28f, 1f).Env(0.003f, 0.6f), Noise(1.2f, r, 0.8f).LowPass(1500f).Env(0.002f, 0.25f),
                        Noise(1.2f, r, 0.4f).LowPass(200f).Env(0.05f, 0.8f));
                case SoundId.Crusher:
                    return Mix(0.75f, Sine(0.75f, 75f, 35f, 1f).Env(0.002f, 0.35f), Ring(0.75f, 0.5f, 0.35f, 300f, 720f, 1130f),
                        Noise(0.75f, r, 0.7f).LowPass(2500f).Env(0.001f, 0.1f));
                case SoundId.Parry:
                    return Mix(0.38f, Ring(0.38f, 0.7f, 0.25f, 2400f, 3600f, 5100f), Noise(0.38f, r, 0.6f).HighPass(4000f).Env(0.001f, 0.02f));
                case SoundId.AmbientLoop:
                    return Loop(8f, 0.6f, length =>
                    {
                        var r2 = new System.Random(77);
                        return Mix(length, Sine(length, 55f, 55f, 0.35f).Tremolo(0.125f, 0.3f), Sine(length, 82.5f, 82.5f, 0.22f).Tremolo(0.25f, 0.4f),
                            Sine(length, 110f, 110f, 0.12f).Tremolo(0.375f, 0.5f), Noise(length, r2, 0.25f).LowPass(220f).Tremolo(0.125f, 0.5f));
                    });
                case SoundId.WindLoop:
                    return Loop(3f, 0.5f, length =>
                    {
                        var r2 = new System.Random(91);
                        return Mix(length, Noise(length, r2, 1f).Band(350f, 1800f).Tremolo(0.7f, 0.35f), Noise(length, r2, 0.4f).Band(2500f, 6000f).Tremolo(1.3f, 0.5f));
                    });
                default:
                    return null;
            }
        }

        // ------------------------------------------------------------------ Building blocks

        static float[] Noise(float seconds, System.Random r, float amplitude)
        {
            var s = new float[Mathf.CeilToInt(seconds * Rate)];
            for (int i = 0; i < s.Length; i++)
                s[i] = ((float)r.NextDouble() * 2f - 1f) * amplitude;
            return s;
        }

        /// <summary>A sine sweeping from <paramref name="from"/> to <paramref name="to"/> Hz (exponentially).</summary>
        static float[] Sine(float seconds, float from, float to, float amplitude)
        {
            var s = new float[Mathf.CeilToInt(seconds * Rate)];
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float u = i / (float)s.Length;
                float f = from * Mathf.Pow(to / from, u);
                phase += 2.0 * Math.PI * f / Rate;
                s[i] = (float)Math.Sin(phase) * amplitude;
            }
            return s;
        }

        static float[] Square(float seconds, float from, float to, float amplitude)
        {
            float[] s = Sine(seconds, from, to, 1f);
            for (int i = 0; i < s.Length; i++)
                s[i] = (float)Math.Tanh(s[i] * 4f) * amplitude;
            return s;
        }

        static float[] Fm(float seconds, float from, float to, float modRate, float modDepth, float amplitude)
        {
            var s = new float[Mathf.CeilToInt(seconds * Rate)];
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float u = i / (float)s.Length;
                float f = Mathf.Lerp(from, to, u) + Mathf.Sin(2f * Mathf.PI * modRate * t) * modDepth;
                phase += 2.0 * Math.PI * f / Rate;
                s[i] = (float)Math.Sin(phase) * amplitude;
            }
            return s;
        }

        /// <summary>Inharmonic metallic partials ringing out.</summary>
        static float[] Ring(float seconds, float amplitude, float decay, params float[] partials)
        {
            var s = new float[Mathf.CeilToInt(seconds * Rate)];
            for (int p = 0; p < partials.Length; p++)
            {
                float a = amplitude / (1f + p * 0.6f);
                float d = decay / (1f + p * 0.3f);
                for (int i = 0; i < s.Length; i++)
                {
                    float t = i / (float)Rate;
                    s[i] += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * a * Mathf.Exp(-t / d);
                }
            }
            return s;
        }

        static float[] Bell(float seconds, float frequency, float amplitude)
        {
            return Ring(seconds, amplitude, seconds * 0.45f, frequency, frequency * 2.01f, frequency * 3.03f);
        }

        /// <summary>Glass: many short random pings in the first moment.</summary>
        static float[] Shatter(float seconds, System.Random r, int pings, float low, float high, float spread)
        {
            var s = new float[Mathf.CeilToInt(seconds * Rate)];
            for (int p = 0; p < pings; p++)
            {
                float start = (float)r.NextDouble() * spread;
                float f = Mathf.Lerp(low, high, (float)r.NextDouble());
                float d = 0.02f + (float)r.NextDouble() * 0.05f;
                float a = 0.25f + (float)r.NextDouble() * 0.3f;
                int i0 = Mathf.FloorToInt(start * Rate);
                for (int i = i0; i < s.Length; i++)
                {
                    float t = (i - i0) / (float)Rate;
                    if (t > d * 6f)
                        break;
                    s[i] += Mathf.Sin(2f * Mathf.PI * f * t) * a * Mathf.Exp(-t / d);
                }
            }
            return s;
        }

        static float[] Beeps(float seconds, float frequency, int count, float length, float gap)
        {
            var s = new float[Mathf.CeilToInt(seconds * Rate)];
            for (int b = 0; b < count; b++)
            {
                int i0 = Mathf.FloorToInt(b * (length + gap) * Rate);
                int n = Mathf.FloorToInt(length * Rate);
                for (int i = 0; i < n && i0 + i < s.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.01f) * Mathf.Min(1f, (length - t) / 0.03f);
                    s[i0 + i] += (float)Math.Tanh(Mathf.Sin(2f * Mathf.PI * frequency * t) * 3f) * 0.6f * env;
                }
            }
            return s;
        }

        /// <summary>Mixes layers (each its own length) into one clip of <paramref name="seconds"/>, normalised.</summary>
        static float[] Mix(float seconds, params float[][] layers)
        {
            var s = new float[Mathf.CeilToInt(seconds * Rate)];
            foreach (float[] layer in layers)
            {
                for (int i = 0; i < s.Length && i < layer.Length; i++)
                    s[i] += layer[i];
            }
            float peak = 1e-4f;
            foreach (float v in s)
                peak = Mathf.Max(peak, Mathf.Abs(v));
            float gain = 0.9f / peak;
            for (int i = 0; i < s.Length; i++)
                s[i] *= gain;
            return s;
        }

        /// <summary>A seamless loop: makes the sound a little longer and crossfades the overlap into the start.</summary>
        static float[] Loop(float seconds, float crossfade, Func<float, float[]> make)
        {
            float[] long_ = make(seconds + crossfade);
            int n = Mathf.CeilToInt(seconds * Rate);
            int f = Mathf.CeilToInt(crossfade * Rate);
            var s = new float[n];
            for (int i = 0; i < n; i++)
                s[i] = long_[i];
            for (int i = 0; i < f && n + i < long_.Length; i++)
            {
                float w = i / (float)f;
                s[i] = long_[i] * w + long_[n + i] * (1f - w);
            }
            return s;
        }

        static void WriteWav(string path, float[] samples)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(Rate);
                writer.Write(Rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                foreach (float v in samples)
                    writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
            }
        }

        // ------------------------------------------------------------------ Processing (extension methods on sample arrays)

        static float[] Env(this float[] s, float attack, float decay)
        {
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float a = attack > 0f ? Mathf.Min(1f, t / attack) : 1f;
                s[i] *= a * Mathf.Exp(-Mathf.Max(0f, t - attack) / Mathf.Max(1e-4f, decay));
            }
            return s;
        }

        /// <summary>Rises over <paramref name="rise"/> s, then falls over <paramref name="fall"/> s.</summary>
        static float[] Swell(this float[] s, float rise, float fall)
        {
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float e = t < rise ? Mathf.Pow(t / Mathf.Max(rise, 1e-4f), 2f) : Mathf.Exp(-(t - rise) / Mathf.Max(fall * 0.4f, 1e-4f));
                s[i] *= e;
            }
            return s;
        }

        static float[] LowPass(this float[] s, float cutoff)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / Rate;
            float a = dt / (rc + dt);
            float y = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                y += a * (s[i] - y);
                s[i] = y;
            }
            return s;
        }

        static float[] HighPass(this float[] s, float cutoff)
        {
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float dt = 1f / Rate;
            float a = rc / (rc + dt);
            float y = 0f;
            float previous = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float x = s[i];
                y = a * (y + x - previous);
                previous = x;
                s[i] = y;
            }
            return s;
        }

        static float[] Band(this float[] s, float low, float high)
        {
            return s.HighPass(low).LowPass(high).LowPass(high);
        }

        static float[] Tremolo(this float[] s, float rate, float depth)
        {
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                s[i] *= 1f - depth * 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * rate * t));
            }
            return s;
        }

        static float[] TremoloRamp(this float[] s, float fromRate, float toRate, float depth)
        {
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float u = i / (float)s.Length;
                phase += 2.0 * Math.PI * Mathf.Lerp(fromRate, toRate, u) / Rate;
                s[i] *= 1f - depth * 0.5f * (1f + (float)Math.Sin(phase));
            }
            return s;
        }

        static float[] Crackle(this float[] s, System.Random r, float density)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (r.NextDouble() > density * 0.02)
                    s[i] *= 0.35f;
            }
            return s;
        }

        static float[] BitCrush(this float[] s, float steps)
        {
            for (int i = 0; i < s.Length; i++)
                s[i] = Mathf.Round(s[i] * steps) / steps;
            return s;
        }
    }
}
