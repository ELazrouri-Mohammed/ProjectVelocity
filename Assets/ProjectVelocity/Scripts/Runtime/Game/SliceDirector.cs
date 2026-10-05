using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Runs the vertical slice: which checkpoint you'd restart at, what the world looks like when you do, the run timer and the
    /// finish. Restarts are instant: the player controller asks it where to respawn, and on respawn it puts every
    /// <see cref="SliceSegment"/> before the checkpoint in its finished state and resets the rest, so the world you come back to
    /// is the one you left, minus the part you failed. One per scene.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public sealed class SliceDirector : MonoBehaviour
    {
        const string BestTimeKey = "ProjectVelocity.Slice.BestTime";

        [SerializeField] VelocityPlayerController player;
        [SerializeField] PlayerHealth health;

        [Tooltip("Checkpoints in route order (index 0 = the start). The one marked Is Finish ends the run.")]
        [SerializeField] SliceCheckpoint[] checkpoints;

        [Tooltip("Segments in route order (index matches the checkpoint each starts at).")]
        [SerializeField] SliceSegment[] segments;

        [Tooltip("Shown at the start and on the results.")]
        [SerializeField] string sliceName = "FRACTURE";

        [Tooltip("Falling below this height kills when no checkpoint says otherwise.")]
        [SerializeField] float defaultKillHeight = -80f;

        [Header("Finish")]
        [Tooltip("World speed for the moment you cross the finish.")]
        [SerializeField, Range(0.05f, 1f)] float finishSlowMotion = 0.25f;

        [Tooltip("Real seconds of slow motion before the results freeze the run.")]
        [SerializeField, Min(0f)] float finishDelay = 1.4f;

        int current;
        float runTime;
        bool finished;
        bool frozenAfterFinish;
        float finishTimer;
        float bestTime;
        int finishDeaths;
        bool newBest;

        public static SliceDirector Current { get; private set; }

        /// <summary>Raised when a new checkpoint is reached.</summary>
        public event Action<SliceCheckpoint> CheckpointReached;
        /// <summary>Raised when the finish line is crossed.</summary>
        public event Action Finished;
        /// <summary>Raised when the whole run starts over.</summary>
        public event Action RunRestarted;

        public VelocityPlayerController Player
        {
            get => player;
            set => player = value;
        }

        public PlayerHealth Health
        {
            get => health;
            set => health = value;
        }

        public string SliceName => sliceName;
        public float RunTime => runTime;
        public bool IsFinished => finished;
        /// <summary>Whether the results are up (the run is over and frozen).</summary>
        public bool ShowingResults => frozenAfterFinish;
        public float BestTime => bestTime;
        public bool NewBest => newBest;
        public int Deaths => finished ? finishDeaths : health != null ? health.Deaths : 0;
        public int CheckpointIndex => current;
        public int CheckpointCount => checkpoints != null ? checkpoints.Length : 0;

        public float KillHeight
        {
            get
            {
                SliceCheckpoint checkpoint = CurrentCheckpoint;
                return checkpoint != null ? checkpoint.KillHeight : defaultKillHeight;
            }
        }

        SliceCheckpoint CurrentCheckpoint =>
            checkpoints != null && current >= 0 && current < checkpoints.Length ? checkpoints[current] : null;

        /// <summary>Wires it up (used by the scene builder).</summary>
        public void Configure(VelocityPlayerController playerController, PlayerHealth playerHealth, SliceCheckpoint[] gates,
            SliceSegment[] stretches, string displayName)
        {
            player = playerController;
            health = playerHealth;
            checkpoints = gates;
            segments = stretches;
            sliceName = displayName;
        }

        void Awake()
        {
            Current = this;
            if (player == null)
                player = FindAnyObjectByType<VelocityPlayerController>();
            if (health == null && player != null)
                health = player.GetComponent<PlayerHealth>();
            bestTime = PlayerPrefs.GetFloat(BestTimeKey, 0f);
        }

        void OnEnable()
        {
            Current = this;
            if (player != null)
                player.Respawned += OnRespawned;
            if (checkpoints == null)
                return;
            foreach (SliceCheckpoint checkpoint in checkpoints)
            {
                if (checkpoint == null)
                    continue;
                checkpoint.Crossed += OnCrossed;
                checkpoint.SetPlayer(player != null ? player.transform : null);
            }
        }

        void OnDisable()
        {
            if (Current == this)
                Current = null;
            if (player != null)
                player.Respawned -= OnRespawned;
            if (checkpoints == null)
                return;
            foreach (SliceCheckpoint checkpoint in checkpoints)
            {
                if (checkpoint != null)
                    checkpoint.Crossed -= OnCrossed;
            }
        }

        void Start()
        {
            if (checkpoints != null && checkpoints.Length > 0 && checkpoints[0] != null)
                checkpoints[0].SetLit(true);
        }

        void Update()
        {
            if (!finished)
            {
                runTime += Time.deltaTime;
                return;
            }
            if (frozenAfterFinish)
                return;
            finishTimer -= Time.unscaledDeltaTime;
            if (finishTimer <= 0f)
            {
                frozenAfterFinish = true;
                GameTime.ClearSlowMotion();
                if (player != null)
                    player.Frozen = true;
            }
        }

        /// <summary>Where to restart: the last checkpoint reached.</summary>
        public bool TryGetRestart(out Vector3 position, out float yaw)
        {
            SliceCheckpoint checkpoint = CurrentCheckpoint;
            if (checkpoint != null && checkpoint.RestartPoint != null)
            {
                position = checkpoint.RestartPoint.position;
                yaw = checkpoint.RestartPoint.eulerAngles.y;
                return true;
            }
            position = Vector3.zero;
            yaw = 0f;
            return false;
        }

        /// <summary>The whole run again from the start (the results' Retry).</summary>
        public void RestartRun()
        {
            finished = false;
            frozenAfterFinish = false;
            newBest = false;
            runTime = 0f;
            current = 0;
            GameTime.Reset();
            if (checkpoints != null)
            {
                for (int i = 0; i < checkpoints.Length; i++)
                {
                    if (checkpoints[i] != null)
                        checkpoints[i].SetLit(i == 0);
                }
            }
            if (health != null)
                health.ResetHealth();
            RunRestarted?.Invoke();
            if (player != null)
                player.Respawn();
        }

        void OnCrossed(SliceCheckpoint checkpoint)
        {
            if (finished || checkpoint == null)
                return;
            if (checkpoint.IsFinish)
            {
                Finish(checkpoint);
                return;
            }
            if (checkpoint.Index <= current)
                return;
            current = checkpoint.Index;
            checkpoint.SetLit(true);
            CheckpointReached?.Invoke(checkpoint);
        }

        void Finish(SliceCheckpoint line)
        {
            finished = true;
            finishTimer = finishDelay;
            finishDeaths = health != null ? health.Deaths : 0;
            line.SetLit(true);
            newBest = bestTime <= 0f || runTime < bestTime;
            if (newBest)
            {
                bestTime = runTime;
                PlayerPrefs.SetFloat(BestTimeKey, bestTime);
                PlayerPrefs.Save();
            }
            GameTime.SlowMotion(finishSlowMotion, -1f);
            Finished?.Invoke();
        }

        void OnRespawned()
        {
            ResetWorld(current);
        }

        /// <summary>Segments before <paramref name="checkpoint"/> finished, the rest reset.</summary>
        void ResetWorld(int checkpoint)
        {
            if (segments == null)
                return;
            foreach (SliceSegment segment in segments)
            {
                if (segment == null)
                    continue;
                if (segment.Index < checkpoint)
                    segment.CompleteSegment();
                else
                    segment.ResetSegment();
            }
        }
    }
}
