using UnityEditor;
using UnityEngine;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// Shows a component's tuning asset inline, so all the numbers can be tweaked
    /// from the Player / Main Camera without hunting for the asset.
    /// </summary>
    abstract class TuningHostEditor : Editor
    {
        const string TuningProperty = "tuning";

        Editor tuningEditor;
        bool expanded = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SerializedProperty property = serializedObject.FindProperty(TuningProperty);
            Object asset = property != null ? property.objectReferenceValue : null;
            if (asset == null)
            {
                EditorGUILayout.HelpBox("No tuning asset assigned: built-in defaults are used. " +
                                        "Run Tools > Project Velocity > Build Movement Test to create one.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            expanded = EditorGUILayout.InspectorTitlebar(expanded, asset);
            if (!expanded)
                return;

            EditorGUILayout.HelpBox("Edits here are saved in the tuning asset and are kept after you stop Play Mode.", MessageType.None);
            CreateCachedEditor(asset, null, ref tuningEditor);
            tuningEditor.OnInspectorGUI();
        }

        void OnDisable()
        {
            if (tuningEditor != null)
                DestroyImmediate(tuningEditor);
        }
    }

    [CustomEditor(typeof(VelocityMotor))]
    sealed class VelocityMotorEditor : TuningHostEditor
    {
    }

    [CustomEditor(typeof(VelocityCamera))]
    sealed class VelocityCameraEditor : TuningHostEditor
    {
    }

    /// <summary>Points to where the target feel values live.</summary>
    [CustomEditor(typeof(TraversalTargeting))]
    sealed class TraversalTargetingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("Selection, propulsion and chaining values are in Movement Tuning under Traversal Targets " +
                                    "(shown on the Velocity Motor above).", MessageType.None);
        }
    }

    [CustomEditor(typeof(TraversalTarget)), CanEditMultipleObjects]
    sealed class TraversalTargetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Shared values (detection range, propulsion speed, upward bias, momentum, air boost refresh) are in " +
                                    "Movement Tuning under Traversal Targets, on the Player. The values here adjust this target only.",
                MessageType.None);
            DrawDefaultInspector();
        }
    }
}
