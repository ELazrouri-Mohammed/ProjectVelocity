using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Plays the slice's sounds from a <see cref="SoundBank"/> through a small pool of audio sources (no allocation), plus two
    /// loops: an ambient bed and wind that rises with the player's speed. Sounds without a position play flat (the player's own
    /// sounds); with one they play in the world. <see cref="PlayerFeedback"/> decides what plays when.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliceAudio : MonoBehaviour
    {
        [SerializeField] SoundBank bank;

        [Tooltip("One-shot voices. More = more overlapping sounds.")]
        [SerializeField, Range(4, 32)] int voices = 14;

        [SerializeField, Range(0f, 1f)] float masterVolume = 0.9f;

        [Tooltip("Speed (m/s) at which the wind is silent, and at which it's loudest.")]
        [SerializeField] Vector2 windSpeedRange = new Vector2(14f, 55f);

        [SerializeField, Range(0f, 1f)] float windVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] float ambientVolume = 0.35f;

        [SerializeField] VelocityMotor motor;

        AudioSource[] sources;
        int nextSource;
        AudioSource ambient;
        AudioSource wind;

        public static SliceAudio Instance { get; private set; }

        public SoundBank Bank
        {
            get => bank;
            set => bank = value;
        }

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        void Awake()
        {
            Instance = this;
            sources = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject("Voice " + i);
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 10f;
                source.maxDistance = 160f;
                source.dopplerLevel = 0f;
                sources[i] = source;
            }
            ambient = MakeLoop("Ambient", SoundId.AmbientLoop);
            wind = MakeLoop("Wind", SoundId.WindLoop);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        AudioSource MakeLoop(string name, SoundId id)
        {
            SoundBank.Entry entry = bank != null ? bank.Get(id) : null;
            if (entry == null || entry.clip == null)
                return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = entry.clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
            return source;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (ambient != null)
                ambient.volume = Mathf.MoveTowards(ambient.volume, ambientVolume * masterVolume * Volume(SoundId.AmbientLoop), dt * 0.5f);
            if (wind != null)
            {
                float speed = motor != null ? motor.Velocity.magnitude : 0f;
                float w = Mathf.InverseLerp(windSpeedRange.x, windSpeedRange.y, speed);
                wind.volume = Mathf.Lerp(wind.volume, w * w * windVolume * masterVolume * Volume(SoundId.WindLoop), 1f - Mathf.Exp(-6f * dt));
                wind.pitch = Mathf.Lerp(0.85f, 1.25f, w);
            }
        }

        float Volume(SoundId id)
        {
            SoundBank.Entry entry = bank != null ? bank.Get(id) : null;
            return entry != null ? entry.volume : 0f;
        }

        /// <summary>Plays a sound flat (no position): the player's own sounds.</summary>
        public void Play(SoundId id, float volume = 1f, float pitch = 1f)
        {
            PlayInternal(id, Vector3.zero, false, volume, pitch);
        }

        /// <summary>Plays a sound in the world at <paramref name="position"/>.</summary>
        public void PlayAt(SoundId id, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            PlayInternal(id, position, true, volume, pitch);
        }

        void PlayInternal(SoundId id, Vector3 position, bool spatial, float volume, float pitch)
        {
            if (bank == null || sources == null)
                return;
            SoundBank.Entry entry = bank.Get(id);
            if (entry == null || entry.clip == null)
                return;

            AudioSource source = null;
            for (int k = 0; k < sources.Length; k++)
            {
                AudioSource candidate = sources[(nextSource + k) % sources.Length];
                if (!candidate.isPlaying)
                {
                    source = candidate;
                    break;
                }
            }
            if (source == null)
                source = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;

            source.transform.position = position;
            source.spatialBlend = spatial ? 0.8f : 0f;
            source.clip = entry.clip;
            source.volume = Mathf.Clamp01(volume * entry.volume * masterVolume);
            source.pitch = pitch * (1f + Random.Range(-entry.pitchVariance, entry.pitchVariance));
            source.Play();
        }
    }
}
