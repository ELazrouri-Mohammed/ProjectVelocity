using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// The slice's art direction, "surreal monumental sci-fi", as a controlled palette: pale warm stone for structure, near-black
    /// graphite for mechanisms, deep cyan for energy and interaction, orange-red only for danger, a little verdigris growth and
    /// bone cloth for life. Emission always means something. Plus the sky, the light, the fog, the colour script and a
    /// restrained post stack.
    /// </summary>
    public static partial class VerticalSliceBuilder
    {
        sealed class Art
        {
            // Structure
            public Material Stone;
            public Material StoneLight;
            public Material StoneShadow;
            public Material Graphite;
            public Material GraphiteTrim;
            public Material Verdigris;
            public Material Cloth;
            public Material Void;
            // Signals
            public Material Energy;
            public Material EnergySoft;
            public Material Danger;
            public Material DangerSoft;
            public Material Beam;
            // Reality pieces
            public Material RealityBody;
            public Material RealityDormant;
            public Material RealityWarning;
            public Material RealityShifting;
            public Material RealitySettled;
            public Material HazardBody;
            public Material HazardDormant;
            public Material HazardWarning;
            public Material HazardMoving;
            // Hero
            public Material HeroSuit;
            public Material HeroArmor;
            public Material HeroEnergy;
            public Material HeroScarf;
            public Material BladeSteel;
            // Enemies
            public Material EnemyShell;
            public Material EnemyCore;
            public Material EnemyEye;
            public Material EnemyEyeCharge;
            public Material EnemyHalo;
            public Material EnemySelected;
            public Material EnemyHit;
            public Material EnemyWeakSpot;
            // Interaction
            public Material TargetIdle;
            public Material TargetSelected;
            public Material TargetCooldown;
            public Material AnchorIdle;
            public Material AnchorSelected;
            public Material BeaconDormant;
            public Material BeaconLit;
            // Effects
            public Material FxDust;
            public Material FxSpark;
            public Material FxFlash;
            public Material FxArc;
            public Material FxRing;
            public Material FxShard;
            public Material FxStreak;
            public Material FxRope;
            public Material FxTrail;
            public Material FxAimLine;
            public Material FxShockwave;
            public Material Bolt;
            public Material Pulse;
            // Textures, sprites
            public Texture2D Concrete;
            public Texture2D SoftDot;
            public Texture2D RingTexture;
            public Sprite Vignette;
            public Sprite Diamond;
            public Sprite Brackets;
            public Sprite White;
            public Material Skybox;
            // Meshes (made after the scene opens)
            public Mesh TargetRing;
            public Mesh AnchorRing;
            public Mesh Octahedron;
            public Mesh FlatRing;
            public Mesh Torus;
            public Mesh ThinTorus;
        }

        static Art CreateArtAssets()
        {
            var art = new Art
            {
                Concrete = SliceTextures.Concrete(TextureFolder + "/Concrete.png"),
                SoftDot = SliceTextures.SoftDot(TextureFolder + "/SoftDot.png"),
                RingTexture = SliceTextures.RingTexture(TextureFolder + "/Ring.png"),
                Vignette = SliceTextures.Vignette(TextureFolder + "/Vignette.png"),
                Diamond = SliceTextures.Diamond(TextureFolder + "/ReticleDiamond.png"),
                Brackets = SliceTextures.Brackets(TextureFolder + "/ReticleBrackets.png"),
                White = SliceTextures.White(TextureFolder + "/White.png"),
            };
            Texture2D sky = SliceTextures.Sky(TextureFolder + "/Sky.png");

            Color energyHdr = new Color(0.25f, 1.6f, 2.2f);
            Color dangerHdr = new Color(2.6f, 0.8f, 0.15f);

            art.Stone = Lit("Stone", new Color(0.86f, 0.81f, 0.73f), art.Concrete, 0.16f);
            art.StoneLight = Lit("StoneLight", new Color(0.95f, 0.92f, 0.86f), art.Concrete, 0.2f);
            art.StoneShadow = Lit("StoneShadow", new Color(0.62f, 0.58f, 0.53f), art.Concrete, 0.1f);
            art.Graphite = Lit("Graphite", new Color(0.085f, 0.09f, 0.105f), null, 0.55f, 0.4f);
            art.GraphiteTrim = Lit("GraphiteTrim", new Color(0.17f, 0.18f, 0.2f), null, 0.45f, 0.3f);
            art.Verdigris = Lit("Verdigris", new Color(0.30f, 0.46f, 0.40f), art.Concrete, 0.08f);
            art.Cloth = Lit("Cloth", new Color(0.9f, 0.86f, 0.78f), null, 0.05f, 0f, null, true);
            art.Void = Lit("Void", new Color(0.33f, 0.35f, 0.39f), null, 0f);

            art.Energy = Lit("Energy", new Color(0.2f, 0.75f, 0.85f), null, 0.6f, 0f, energyHdr);
            art.EnergySoft = Lit("EnergySoft", new Color(0.15f, 0.5f, 0.6f), null, 0.5f, 0f, energyHdr * 0.35f);
            art.Danger = Lit("Danger", new Color(0.9f, 0.35f, 0.1f), null, 0.5f, 0f, dangerHdr);
            art.DangerSoft = Lit("DangerSoft", new Color(0.6f, 0.2f, 0.08f), null, 0.4f, 0f, dangerHdr * 0.3f);
            art.Beam = Lit("AnomalyBeam", new Color(0.8f, 0.95f, 1f), null, 0.2f, 0f, new Color(2.2f, 3.4f, 4f));

            art.RealityBody = Lit("RealityBody", new Color(0.78f, 0.74f, 0.68f), art.Concrete, 0.18f);
            art.RealityDormant = Lit("RealityDormant", new Color(0.15f, 0.45f, 0.55f), null, 0.5f, 0f, energyHdr * 0.25f);
            art.RealityWarning = Lit("RealityWarning", new Color(0.85f, 0.95f, 1f), null, 0.5f, 0f, new Color(2.5f, 3.2f, 3.6f));
            art.RealityShifting = Lit("RealityShifting", new Color(0.3f, 0.85f, 0.95f), null, 0.5f, 0f, energyHdr * 1.1f);
            art.RealitySettled = Lit("RealitySettled", new Color(0.15f, 0.5f, 0.6f), null, 0.5f, 0f, energyHdr * 0.45f);
            art.HazardBody = Lit("HazardBody", new Color(0.13f, 0.1f, 0.1f), null, 0.5f, 0.35f);
            art.HazardDormant = Lit("HazardDormant", new Color(0.55f, 0.18f, 0.06f), null, 0.4f, 0f, dangerHdr * 0.3f);
            art.HazardWarning = Lit("HazardWarning", new Color(1f, 0.7f, 0.3f), null, 0.4f, 0f, new Color(4f, 2.2f, 0.5f));
            art.HazardMoving = Lit("HazardMoving", new Color(1f, 0.35f, 0.12f), null, 0.4f, 0f, dangerHdr * 1.2f);

            art.HeroSuit = Lit("HeroSuit", new Color(0.06f, 0.065f, 0.075f), null, 0.5f, 0.35f);
            art.HeroArmor = Lit("HeroArmor", new Color(0.91f, 0.89f, 0.84f), null, 0.7f, 0.05f);
            art.HeroEnergy = Lit("HeroEnergy", new Color(0.3f, 0.9f, 1f), null, 0.6f, 0f, new Color(0.5f, 2.6f, 3.4f));
            art.HeroScarf = Lit("HeroScarf", new Color(0.04f, 0.42f, 0.48f), null, 0.15f, 0f, new Color(0f, 0.12f, 0.15f), true);
            art.BladeSteel = Lit("HeroBlade", new Color(0.78f, 0.85f, 0.9f), null, 0.9f, 0.8f, new Color(0.1f, 0.5f, 0.65f));

            art.EnemyShell = Lit("EnemyShell", new Color(0.76f, 0.73f, 0.68f), null, 0.6f, 0.05f);
            art.EnemyCore = Lit("EnemyCore", new Color(0.07f, 0.07f, 0.08f), null, 0.6f, 0.5f);
            art.EnemyEye = Lit("EnemyEye", new Color(1f, 0.4f, 0.1f), null, 0.5f, 0f, dangerHdr * 0.8f);
            art.EnemyEyeCharge = Lit("EnemyEyeCharge", new Color(1f, 0.85f, 0.6f), null, 0.5f, 0f, new Color(5f, 2.6f, 0.8f));
            art.EnemyHalo = Lit("EnemyHalo", new Color(0.15f, 0.15f, 0.17f), null, 0.5f, 0.4f, new Color(0.25f, 0.06f, 0.02f));
            art.EnemySelected = Lit("EnemySelected", new Color(1f, 0.92f, 0.7f), null, 0.5f, 0f, new Color(3.2f, 2.6f, 1.4f));
            art.EnemyHit = Lit("EnemyHit", Color.white, null, 0.5f, 0f, new Color(4f, 4f, 4f));
            art.EnemyWeakSpot = Lit("EnemyWeakSpot", new Color(0.3f, 0.9f, 1f), null, 0.5f, 0f, energyHdr * 1.2f);

            art.TargetIdle = Lit("TargetIdle", new Color(0.2f, 0.7f, 0.8f), null, 0.6f, 0f, energyHdr * 0.6f);
            art.TargetSelected = Lit("TargetSelected", new Color(0.9f, 1f, 1f), null, 0.6f, 0f, new Color(2.6f, 3.6f, 4f));
            art.TargetCooldown = Lit("TargetCooldown", new Color(0.2f, 0.22f, 0.25f), null, 0.4f, 0f);
            art.AnchorIdle = Lit("AnchorIdle", new Color(0.25f, 0.8f, 0.9f), null, 0.6f, 0f, energyHdr * 0.8f);
            art.AnchorSelected = Lit("AnchorSelected", new Color(1f, 1f, 1f), null, 0.6f, 0f, new Color(3.2f, 4f, 4.4f));
            art.BeaconDormant = Lit("BeaconDormant", new Color(0.12f, 0.3f, 0.35f), null, 0.5f, 0f, energyHdr * 0.15f);
            art.BeaconLit = Lit("BeaconLit", new Color(0.4f, 0.95f, 1f), null, 0.5f, 0f, energyHdr * 1.6f);

            art.FxDust = Particles("FxDust", art.SoftDot, false, new Color(1f, 1f, 1f, 0.7f));
            art.FxSpark = Particles("FxSpark", art.SoftDot, true, Color.white);
            art.FxFlash = Particles("FxFlash", art.SoftDot, true, Color.white);
            art.FxArc = Particles("FxArc", null, true, Color.white);
            art.FxRing = Particles("FxRing", art.RingTexture, true, Color.white);
            art.FxShard = Particles("FxShard", null, false, Color.white, opaque: true);
            art.FxStreak = Particles("FxStreak", art.SoftDot, true, Color.white);
            art.FxRope = Particles("FxRope", null, true, new Color(0.5f, 1.8f, 2.4f, 1f));
            art.FxTrail = Particles("FxTrail", art.SoftDot, true, new Color(0.6f, 1.6f, 2.2f, 1f));
            art.FxAimLine = Particles("FxAimLine", null, true, new Color(2f, 0.6f, 0.15f, 0.8f));
            art.FxShockwave = Particles("FxShockwave", null, true, new Color(2.4f, 0.8f, 0.2f, 0.9f));
            art.Bolt = Lit("EnemyBolt", new Color(1f, 0.6f, 0.2f), null, 0.5f, 0f, new Color(5f, 1.8f, 0.4f));
            art.Pulse = Lit("PlayerPulse", new Color(0.6f, 1f, 1f), null, 0.5f, 0f, new Color(1.5f, 4f, 5f));

            art.Skybox = SkyboxMaterial(sky);
            return art;
        }

        static void CreateArtMeshes(Art art)
        {
            art.TargetRing = GrayboxMeshes.Torus(1.35f, 0.09f, 40, 8);
            art.AnchorRing = GrayboxMeshes.Torus(1.1f, 0.16f, 32, 6);
            art.Octahedron = GrayboxMeshes.Octahedron(0.5f, 0.75f);
            art.FlatRing = SliceMeshes.FlatRing("Shockwave Ring", 0.92f, 1f, 64);
            art.Torus = GrayboxMeshes.Torus(1f, 0.055f, 96, 8);
            art.ThinTorus = GrayboxMeshes.Torus(1f, 0.02f, 96, 6);
        }

        // ------------------------------------------------------------------ Lighting, sky, atmosphere

        static Light SetupLighting(Art art)
        {
            // No baked or realtime GI: the slice is lit by one sun, a three-colour ambient and emission. Nothing to bake.
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "SliceLighting" };
                AssetDatabase.CreateAsset(settings, LightingPath);
            }
            settings.bakedGI = false;
            settings.realtimeGI = false;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;

            RenderSettings.skybox = art.Skybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.70f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.58f, 0.54f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.25f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.80f, 0.76f, 0.70f);
            RenderSettings.fogDensity = 0.0008f;
            RenderSettings.reflectionIntensity = 0.6f;

            var sunObject = new GameObject("Sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.8f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 0.8f;
            sunObject.transform.rotation = Quaternion.LookRotation(-SliceTextures.SunDirection);
            RenderSettings.sun = sun;
            return sun;
        }

        /// <summary>The colour script, keyed to where each beat starts.</summary>
        static AtmosphereZones.Zone[] AtmosphereScript(LevelContext level)
        {
            return new[]
            {
                Zone(level.BeatStart[0], new Color(0.80f, 0.76f, 0.70f), 0.0008f, 0.5f, 1.0f, new Color(0.66f, 0.70f, 0.78f), new Color(0.62f, 0.58f, 0.54f), new Color(0.26f, 0.25f, 0.27f), new Color(1f, 0.92f, 0.8f), 1.45f),
                Zone(level.BeatStart[2], new Color(0.72f, 0.72f, 0.72f), 0.001f, 0.47f, 0.95f, new Color(0.6f, 0.66f, 0.74f), new Color(0.56f, 0.55f, 0.54f), new Color(0.24f, 0.24f, 0.27f), new Color(1f, 0.9f, 0.78f), 1.35f),
                Zone(level.BeatStart[3], new Color(0.66f, 0.70f, 0.75f), 0.0007f, 0.5f, 1.05f, new Color(0.58f, 0.66f, 0.76f), new Color(0.6f, 0.56f, 0.52f), new Color(0.22f, 0.23f, 0.27f), new Color(1f, 0.86f, 0.68f), 1.55f),
                Zone(level.BeatStart[5], new Color(0.45f, 0.60f, 0.64f), 0.0012f, 0.42f, 0.85f, new Color(0.42f, 0.6f, 0.68f), new Color(0.44f, 0.5f, 0.52f), new Color(0.18f, 0.22f, 0.25f), new Color(0.85f, 0.95f, 1f), 1.15f),
                Zone(level.BeatStart[7], new Color(0.80f, 0.62f, 0.52f), 0.0009f, 0.55f, 1.0f, new Color(0.7f, 0.6f, 0.58f), new Color(0.66f, 0.5f, 0.42f), new Color(0.26f, 0.2f, 0.2f), new Color(1f, 0.72f, 0.5f), 1.55f),
                Zone(level.BeatStart[8], new Color(0.92f, 0.90f, 0.85f), 0.0005f, 0.55f, 1.25f, new Color(0.78f, 0.82f, 0.88f), new Color(0.78f, 0.74f, 0.68f), new Color(0.32f, 0.31f, 0.33f), new Color(1f, 0.95f, 0.86f), 1.8f),
            };
        }

        static AtmosphereZones.Zone Zone(float z, Color fog, float density, float tint, float exposure, Color sky, Color equator, Color ground,
            Color sun, float intensity)
        {
            return new AtmosphereZones.Zone
            {
                startZ = z,
                fog = fog,
                fogDensity = density,
                skyTint = new Color(tint, tint, tint, 1f),
                skyExposure = exposure,
                ambientSky = sky,
                ambientEquator = equator,
                ambientGround = ground,
                sun = sun,
                sunIntensity = intensity,
            };
        }

        static void BuildPostProcessing(Camera camera)
        {
            // A restrained stack that survives a phone: bloom for the emissive signals, neutral tonemapping to keep the palette,
            // a touch of contrast and warmth, a soft vignette. No depth of field, motion blur or chromatic aberration.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProfilePath);

                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1.0f);
                bloom.intensity.Override(0.7f);
                bloom.scatter.Override(0.62f);
                bloom.tint.Override(new Color(1f, 0.97f, 0.92f));

                var tonemapping = profile.Add<Tonemapping>(true);
                tonemapping.mode.Override(TonemappingMode.Neutral);

                var color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(0.15f);
                color.contrast.Override(14f);
                color.saturation.Override(-6f);
                color.colorFilter.Override(new Color(1f, 0.98f, 0.95f));

                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.22f);
                vignette.smoothness.Override(0.45f);
                vignette.color.Override(new Color(0.07f, 0.06f, 0.05f));

                foreach (VolumeComponent component in profile.components)
                {
                    component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
                EditorUtility.SetDirty(profile);
            }

            var volumeObject = new GameObject("Post Processing");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                data.renderShadows = true;
                data.stopNaN = false;
                data.dithering = true;
            }
        }

        static VelocityCamera BuildCamera(Player player, CameraTuning tuning)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 4000f;
            cam.fieldOfView = tuning.fovMin;
            cam.allowHDR = true;
            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<VelocityCamera>();
            rig.Target = player.Root.transform;
            rig.TargetMotor = player.Motor;
            rig.Tuning = tuning;

            Quaternion rotation = Quaternion.Euler(tuning.startPitch, player.Root.transform.eulerAngles.y, 0f);
            Vector3 pivot = player.Root.transform.position + Vector3.up * tuning.pivotHeight;
            go.transform.SetPositionAndRotation(pivot + rotation * Vector3.back * tuning.distance, rotation);
            return rig;
        }

        // ------------------------------------------------------------------ Material helpers

        static Material Lit(string name, Color color, Texture texture, float smoothness, float metallic = 0f, Color? emission = null,
            bool doubleSided = false)
        {
            string path = $"{SliceFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.color = color;
            material.mainTexture = texture;
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            Color glow = emission ?? Color.black;
            material.SetColor("_EmissionColor", glow);
            if (glow.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            material.doubleSidedGI = doubleSided;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>URP Particles/Unlit: vertex-coloured (particles, lines, trails), alpha-blended or additive, or opaque.</summary>
        static Material Particles(string name, Texture texture, bool additive, Color color, bool opaque = false)
        {
            string path = $"{SliceFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (opaque)
            {
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);
                material.SetOverrideTag("RenderType", "Opaque");
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Geometry;
            }
            else
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", additive ? 2f : 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                if (material.HasProperty("_SrcBlendAlpha"))
                    material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (material.HasProperty("_DstBlendAlpha"))
                    material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", (float)CullMode.Off);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material SkyboxMaterial(Texture2D sky)
        {
            string path = $"{SliceFolder}/SliceSky.mat";
            Shader shader = Shader.Find("Skybox/Panoramic");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }
            material.SetTexture("_MainTex", sky);
            if (material.HasProperty("_Tint"))
                material.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));
            if (material.HasProperty("_Exposure"))
                material.SetFloat("_Exposure", 1f);
            if (material.HasProperty("_Mapping"))
                material.SetFloat("_Mapping", 1f); // latitude-longitude
            if (material.HasProperty("_ImageType"))
                material.SetFloat("_ImageType", 0f); // 360 degrees
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
