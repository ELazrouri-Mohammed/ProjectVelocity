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
}
