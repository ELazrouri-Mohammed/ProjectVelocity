using UnityEngine;
using UnityEngine.UI;

namespace ProjectVelocity
{
    /// <summary>The on-screen buttons, in the order <see cref="TouchControlsView"/> stores them.</summary>
    public enum TouchButton
    {
        Jump,
        Boost,
        Action,
        Reset,
        /// <summary>Added last so the earlier buttons keep their places.</summary>
        Attack,
    }

    /// <summary>
    /// Prototype visuals for the touch controls. Purely cosmetic and never raycast: <see cref="MobileInputSource"/> does all
    /// the hit testing and tells this where things are, in screen pixels. Elements are only touched when something changes,
    /// so an idle frame costs nothing.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class TouchControlsView : MonoBehaviour
    {
        public const int ButtonCount = 5;

        // Knob diameter relative to the stick's travel diameter.
        const float KnobSize = 0.9f;

        [SerializeField] RectTransform stickBase;
        [SerializeField] RectTransform stickKnob;

        [Tooltip("Jump, Boost, Action, Reset, Attack (in that order).")]
        [SerializeField] RectTransform[] buttons = new RectTransform[ButtonCount];

        [Tooltip("Opacity of controls that aren't being touched.")]
        [SerializeField, Range(0f, 1f)] float idleOpacity = 0.45f;

        [Tooltip("Opacity of the stick while held and of buttons while pressed.")]
        [SerializeField, Range(0f, 1f)] float activeOpacity = 0.9f;

        Canvas canvas;
        bool initialized;
        float pixelsPerUnit = 1f;
        Graphic stickBaseGraphic;
        Graphic stickKnobGraphic;
        Color stickBaseColor;
        Color stickKnobColor;
        readonly Graphic[] buttonGraphics = new Graphic[ButtonCount];
        readonly Color[] buttonColors = new Color[ButtonCount];
        readonly bool[] buttonPressed = new bool[ButtonCount];
        bool stickActive;

        /// <summary>Hooks up the parts (used by the scene builder).</summary>
        public void SetParts(RectTransform stickBaseRect, RectTransform stickKnobRect, RectTransform[] buttonRects)
        {
            stickBase = stickBaseRect;
            stickKnob = stickKnobRect;
            buttons = buttonRects;
            initialized = false;
        }

        public void SetVisible(bool visible)
        {
            Init();
            canvas.enabled = visible;
        }

        /// <summary>Sets the size of one layout unit in screen pixels.</summary>
        public void SetPixelsPerUnit(float value)
        {
            Init();
            pixelsPerUnit = Mathf.Max(1e-3f, value);
            canvas.scaleFactor = pixelsPerUnit;
        }

        /// <summary>Sets the stick's size from its travel radius (pixels).</summary>
        public void SetStickRadius(float radius)
        {
            Init();
            float diameter = radius * 2f / pixelsPerUnit;
            if (stickBase != null)
                stickBase.sizeDelta = new Vector2(diameter, diameter);
            if (stickKnob != null)
                stickKnob.sizeDelta = new Vector2(diameter, diameter) * (KnobSize * 0.5f);
        }

        /// <summary>Moves the stick (centre and knob offset in pixels) and shows whether it is held.</summary>
        public void SetStick(Vector2 centre, Vector2 knobOffset, bool active)
        {
            Init();
            if (stickBase != null)
                stickBase.anchoredPosition = centre / pixelsPerUnit;
            if (stickKnob != null)
                stickKnob.anchoredPosition = knobOffset / pixelsPerUnit;
            if (active != stickActive)
            {
                stickActive = active;
                ApplyOpacity(stickBaseGraphic, stickBaseColor, active);
                ApplyOpacity(stickKnobGraphic, stickKnobColor, active);
            }
        }

        /// <summary>Places a button (centre and radius in pixels), or hides it.</summary>
        public void PlaceButton(TouchButton button, Vector2 centre, float radius, bool visible)
        {
            Init();
            RectTransform rect = GetButton(button);
            if (rect == null)
                return;
            if (rect.gameObject.activeSelf != visible)
                rect.gameObject.SetActive(visible);
            float diameter = radius * 2f / pixelsPerUnit;
            rect.anchoredPosition = centre / pixelsPerUnit;
            rect.sizeDelta = new Vector2(diameter, diameter);
        }

        public void SetButtonPressed(TouchButton button, bool pressed)
        {
            Init();
            int index = (int)button;
            if (buttonPressed[index] == pressed)
                return;
            buttonPressed[index] = pressed;
            ApplyOpacity(buttonGraphics[index], buttonColors[index], pressed);
        }

        void Awake()
        {
            Init();
        }

        void Init()
        {
            if (initialized)
                return;
            initialized = true;

            canvas = GetComponent<Canvas>();
            stickBaseGraphic = stickBase != null ? stickBase.GetComponent<Graphic>() : null;
            stickKnobGraphic = stickKnob != null ? stickKnob.GetComponent<Graphic>() : null;
            stickBaseColor = stickBaseGraphic != null ? stickBaseGraphic.color : Color.white;
            stickKnobColor = stickKnobGraphic != null ? stickKnobGraphic.color : Color.white;
            stickActive = false;
            ApplyOpacity(stickBaseGraphic, stickBaseColor, false);
            ApplyOpacity(stickKnobGraphic, stickKnobColor, false);

            for (int i = 0; i < ButtonCount; i++)
            {
                RectTransform rect = GetButton((TouchButton)i);
                buttonGraphics[i] = rect != null ? rect.GetComponent<Graphic>() : null;
                buttonColors[i] = buttonGraphics[i] != null ? buttonGraphics[i].color : Color.white;
                buttonPressed[i] = false;
                ApplyOpacity(buttonGraphics[i], buttonColors[i], false);
            }
        }

        RectTransform GetButton(TouchButton button)
        {
            int index = (int)button;
            return buttons != null && index < buttons.Length ? buttons[index] : null;
        }

        void ApplyOpacity(Graphic graphic, Color color, bool active)
        {
            if (graphic == null)
                return;
            color.a = active ? activeOpacity : idleOpacity;
            graphic.color = color;
        }
    }
}
