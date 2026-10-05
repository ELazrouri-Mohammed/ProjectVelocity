using UnityEngine;
using UnityEngine.UI;

namespace ProjectVelocity
{
    /// <summary>
    /// The slice's whole HUD, deliberately small so the player mostly sees the world: a run timer, the shield as a screen-edge
    /// glow, flashes for hits and deaths, a one-word reason when you die (so you know why), checkpoint toasts, reticles on the
    /// selected tether anchor and pulse target, a few one-time hints, an FPS readout in development builds, and the results
    /// card at the end. Built by the scene builder from plain uGUI; never raycast.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliceHUD : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] VelocityPlayerController player;
        [SerializeField] VelocityMotor motor;
        [SerializeField] PlayerHealth health;
        [SerializeField] TetherTargeting tetherTargeting;
        [SerializeField] CombatController combat;
        [SerializeField] Camera view;

        [Header("Elements")]
        [SerializeField] RectTransform canvasRect;
        [SerializeField] Image vignette;
        [SerializeField] Image flash;
        [SerializeField] Text timer;
        [SerializeField] Text toast;
        [SerializeField] Text title;
        [SerializeField] Text subtitle;
        [SerializeField] Text hint;
        [SerializeField] Text deathWord;
        [SerializeField] Text fps;
        [SerializeField] RectTransform anchorReticle;
        [SerializeField] RectTransform pulseReticle;
        [SerializeField] GameObject results;
        [SerializeField] Text resultsTitle;
        [SerializeField] Text resultsBody;
        [SerializeField] Text resultsPrompt;

        [Header("Colours")]
        [SerializeField] Color shieldDown = new Color(1f, 0.18f, 0.12f, 1f);
        [SerializeField] Color energy = new Color(0.3f, 0.92f, 1f, 1f);

        float titleTime;
        float toastTime = -1f;
        float hintTime = -1f;
        float deathWordTime = -1f;
        float flashAlpha;
        Color flashColor = Color.white;
        float restoreGlow;
        int shownTenths = -1;
        float fpsTimer;
        float smoothedDelta;
        float resultsShownAt = -1f;
        bool hintedTether;
        bool hintedPulse;
        bool hintedHeavy;
        SliceDirector director;
        TouchControlsView touchView;
        TraversalTargeting traversalTargeting;

        public void SetSources(VelocityPlayerController playerController, VelocityMotor playerMotor, PlayerHealth playerHealth,
            TetherTargeting anchors, CombatController playerCombat, Camera camera)
        {
            player = playerController;
            motor = playerMotor;
            health = playerHealth;
            tetherTargeting = anchors;
            combat = playerCombat;
            view = camera;
        }

        public void SetElements(RectTransform root, Image edgeGlow, Image screenFlash, Text runTimer, Text toastText, Text titleText,
            Text subtitleText, Text hintText, Text deathText, Text fpsText, RectTransform anchorMark, RectTransform pulseMark,
            GameObject resultsPanel, Text resultsHeading, Text resultsText, Text resultsHint)
        {
            canvasRect = root;
            vignette = edgeGlow;
            flash = screenFlash;
            timer = runTimer;
            toast = toastText;
            title = titleText;
            subtitle = subtitleText;
            hint = hintText;
            deathWord = deathText;
            fps = fpsText;
            anchorReticle = anchorMark;
            pulseReticle = pulseMark;
            results = resultsPanel;
            resultsTitle = resultsHeading;
            resultsBody = resultsText;
            resultsPrompt = resultsHint;
        }

        void Start()
        {
            director = SliceDirector.Current;
            if (player != null)
            {
                touchView = player.GetComponent<MobileInputSource>() != null ? player.GetComponent<MobileInputSource>().View : null;
                traversalTargeting = player.GetComponent<TraversalTargeting>();
                player.Respawned += OnRespawned;
            }
            if (health != null)
            {
                health.ShieldBroken += OnShieldBroken;
                health.ShieldRestored += OnShieldRestored;
                health.Died += OnDied;
            }
            if (director != null)
            {
                director.CheckpointReached += OnCheckpoint;
                director.Finished += OnFinished;
                director.RunRestarted += OnRunRestarted;
            }
            SliceEnemy.AnyCue += OnEnemyCue;
            if (results != null)
                results.SetActive(false);
            if (fps != null)
                fps.gameObject.SetActive(Debug.isDebugBuild);
            SetAlpha(hint, 0f);
            SetAlpha(toast, 0f);
            SetAlpha(deathWord, 0f);
            titleTime = 0f;
        }

        void OnDestroy()
        {
            SliceEnemy.AnyCue -= OnEnemyCue;
            if (player != null)
                player.Respawned -= OnRespawned;
            if (health != null)
            {
                health.ShieldBroken -= OnShieldBroken;
                health.ShieldRestored -= OnShieldRestored;
                health.Died -= OnDied;
            }
            if (director != null)
            {
                director.CheckpointReached -= OnCheckpoint;
                director.Finished -= OnFinished;
                director.RunRestarted -= OnRunRestarted;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateTitle(dt);
            UpdateTimer();
            UpdateFades(dt);
            UpdateReticles();
            UpdateButtons();
            UpdateResults();
            UpdateFps(dt);
        }

        void UpdateTitle(float dt)
        {
            titleTime += dt;
            float a = titleTime < 0.4f ? titleTime / 0.4f : titleTime < 2.2f ? 1f : Mathf.Clamp01(1f - (titleTime - 2.2f) / 0.8f);
            SetAlpha(title, a);
            SetAlpha(subtitle, a * 0.8f);
        }

        void UpdateTimer()
        {
            if (timer == null || director == null)
                return;
            int tenths = Mathf.FloorToInt(director.RunTime * 10f);
            if (tenths == shownTenths)
                return;
            shownTenths = tenths;
            timer.text = FormatTime(director.RunTime);
        }

        void UpdateFades(float dt)
        {
            // Shield: a pulsing red edge while it's down, fading as it recharges; a short cyan glow when it comes back.
            if (vignette != null && health != null)
            {
                restoreGlow = Mathf.MoveTowards(restoreGlow, 0f, dt * 2.5f);
                Color c;
                if (!health.ShieldUp && !health.IsDead)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
                    c = shieldDown;
                    c.a = Mathf.Lerp(0.55f, 0.25f, health.ShieldRecharge01) * (0.75f + 0.25f * pulse);
                }
                else
                {
                    c = energy;
                    c.a = restoreGlow * 0.5f;
                }
                SetColor(vignette, c);
            }

            if (flash != null)
            {
                flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, dt * 3f);
                Color c = flashColor;
                c.a = flashAlpha;
                SetColor(flash, c);
            }

            Fade(toast, ref toastTime, 1.8f, dt);
            Fade(hint, ref hintTime, 3.4f, dt);
            Fade(deathWord, ref deathWordTime, 0.9f, dt);
        }

        void UpdateReticles()
        {
            TetherAnchor anchor = tetherTargeting != null && motor != null && !motor.IsTethering ? tetherTargeting.Selected : null;
            Place(anchorReticle, anchor != null ? anchor.Position : (Vector3?)null, 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f));
            if (anchor != null && !hintedTether)
            {
                hintedTether = true;
                ShowHint("HOLD LINK TO SWING · LET GO TO FLY");
            }

            CombatTargeting targeting = combat != null ? combat.Targeting : null;
            CombatEnemy far = targeting != null && combat.PulseReady ? targeting.PulseSelected : null;
            Place(pulseReticle, far != null ? far.HurtboxCenter : (Vector3?)null, 1f);
            if (far != null && !hintedPulse && hintTime < 0f)
            {
                hintedPulse = true;
                ShowHint("ATTACK AT RANGE FIRES A PULSE");
            }
        }

        void UpdateButtons()
        {
            if (touchView == null || motor == null)
                return;
            bool linkReady = (tetherTargeting != null && tetherTargeting.Selected != null && motor.CanTether) ||
                             (traversalTargeting != null && traversalTargeting.Selected != null && motor.CanActivateTarget) ||
                             motor.IsTethering;
            touchView.SetButtonState(TouchButton.Action, true, linkReady);
            touchView.SetButtonState(TouchButton.Boost, motor.CanBoost, false);
            CombatTargeting targeting = combat != null ? combat.Targeting : null;
            bool attackReady = targeting != null && (targeting.Selected != null || (targeting.PulseSelected != null && combat.PulseReady));
            touchView.SetButtonState(TouchButton.Attack, true, attackReady);
        }

        void UpdateResults()
        {
            if (director == null || results == null)
                return;
            bool showing = director.ShowingResults;
            if (showing != results.activeSelf)
            {
                results.SetActive(showing);
                if (showing)
                {
                    resultsShownAt = Time.unscaledTime;
                    if (resultsTitle != null)
                        resultsTitle.text = director.SliceName + " — CLEARED";
                    if (resultsBody != null)
                    {
                        resultsBody.text =
                            "TIME   " + FormatTime(director.RunTime) + "\n" +
                            "BEST   " + FormatTime(director.BestTime) + (director.NewBest ? "   NEW BEST" : "") + "\n" +
                            "DEATHS   " + director.Deaths;
                    }
                    if (resultsPrompt != null)
                        resultsPrompt.text = "PRESS JUMP TO RUN IT AGAIN";
                }
            }
            if (!showing || player == null || player.InputSource == null || Time.unscaledTime - resultsShownAt < 0.8f)
                return;
            PlayerIntent intent = player.InputSource.ReadIntent();
            if (intent.JumpPressed || intent.AttackPressed || intent.TargetPressed || intent.RespawnPressed)
                director.RestartRun();
        }

        void UpdateFps(float dt)
        {
            if (fps == null || !fps.gameObject.activeSelf)
                return;
            smoothedDelta = smoothedDelta <= 0f ? dt : Mathf.Lerp(smoothedDelta, dt, 0.08f);
            fpsTimer -= dt;
            if (fpsTimer > 0f)
                return;
            fpsTimer = 0.25f;
            fps.text = smoothedDelta > 0f ? Mathf.RoundToInt(1f / smoothedDelta) + " FPS" : "";
        }

        // ------------------------------------------------------------------ Events

        void OnShieldBroken(Vector3 source)
        {
            Flash(shieldDown, 0.35f);
        }

        void OnShieldRestored()
        {
            restoreGlow = 1f;
        }

        void OnDied(string reason)
        {
            Flash(Color.white, 0.55f);
            if (deathWord != null)
            {
                deathWord.text = DeathWord(reason);
                deathWordTime = 0f;
                SetAlpha(deathWord, 1f);
            }
        }

        void OnRespawned()
        {
            Flash(Color.black, 0.5f);
        }

        void OnCheckpoint(SliceCheckpoint checkpoint)
        {
            if (toast == null || checkpoint == null)
                return;
            toast.text = "CHECKPOINT  ·  " + checkpoint.Label;
            toastTime = 0f;
            SetAlpha(toast, 1f);
            Flash(energy, 0.12f);
        }

        void OnFinished()
        {
            Flash(Color.white, 0.6f);
        }

        void OnRunRestarted()
        {
            titleTime = 0f;
            shownTenths = -1;
        }

        void OnEnemyCue(SliceEnemy enemy, EnemyCue cue, Vector3 position)
        {
            if (cue == EnemyCue.Woke && enemy is HeavyWarden && !hintedHeavy)
            {
                hintedHeavy = true;
                ShowHint("SHIELDED  ·  STRIKE FROM BEHIND OR ABOVE");
            }
        }

        // ------------------------------------------------------------------ Helpers

        void ShowHint(string text)
        {
            if (hint == null)
                return;
            hint.text = text;
            hintTime = 0f;
            SetAlpha(hint, 1f);
        }

        void Flash(Color color, float alpha)
        {
            flashColor = color;
            flashAlpha = Mathf.Max(flashAlpha, alpha);
        }

        void Place(RectTransform reticle, Vector3? world, float scale)
        {
            if (reticle == null)
                return;
            bool show = false;
            if (world.HasValue && view != null && canvasRect != null)
            {
                Vector3 screen = view.WorldToScreenPoint(world.Value);
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                {
                    reticle.anchoredPosition = local;
                    reticle.localScale = Vector3.one * scale;
                    show = true;
                }
            }
            if (reticle.gameObject.activeSelf != show)
                reticle.gameObject.SetActive(show);
        }

        static void Fade(Text text, ref float time, float duration, float dt)
        {
            if (text == null || time < 0f)
                return;
            time += dt;
            float a = time < duration * 0.7f ? 1f : Mathf.Clamp01(1f - (time - duration * 0.7f) / (duration * 0.3f));
            SetAlpha(text, a);
            if (time >= duration)
                time = -1f;
        }

        /// <summary>
        /// Sets a graphic's alpha, and turns it off entirely while it's invisible: a transparent full-screen image still costs a
        /// full-screen blend on a phone.
        /// </summary>
        static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;
            Color c = graphic.color;
            c.a = alpha;
            SetColor(graphic, c);
        }

        static void SetColor(Graphic graphic, Color color)
        {
            bool visible = color.a > 0.004f;
            if (graphic.enabled != visible)
                graphic.enabled = visible;
            if (visible && graphic.color != color)
                graphic.color = color;
        }

        static string DeathWord(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return "DOWN";
            if (reason.StartsWith("crushed"))
                return "CRUSHED";
            if (reason.StartsWith("hit"))
                return "STRUCK";
            if (reason == "fell")
                return "FELL";
            if (reason == "shot")
                return "SHOT DOWN";
            if (reason == "shockwave")
                return "SHOCKWAVE";
            if (reason.StartsWith("cut"))
                return "CUT DOWN";
            return reason.ToUpperInvariant();
        }

        static string FormatTime(float seconds)
        {
            if (seconds <= 0f)
                return "-:--.-";
            int tenths = Mathf.FloorToInt(seconds * 10f);
            int minutes = tenths / 600;
            int secs = tenths / 10 % 60;
            return minutes + ":" + secs.ToString("00") + "." + tenths % 10;
        }
    }
}
