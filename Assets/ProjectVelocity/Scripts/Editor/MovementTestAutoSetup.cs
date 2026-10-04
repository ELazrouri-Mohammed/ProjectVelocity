using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// The first time the project is opened (or whenever the movement test scene is missing),
    /// builds and opens the movement test automatically so there is nothing to set up by hand.
    /// </summary>
    [InitializeOnLoad]
    static class MovementTestAutoSetup
    {
        const string CheckedThisSessionKey = "ProjectVelocity.MovementTestAutoSetupChecked";

        static MovementTestAutoSetup()
        {
            if (Application.isBatchMode || SessionState.GetBool(CheckedThisSessionKey, false))
                return;
            EditorApplication.delayCall += TryBuild;
        }

        static void TryBuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryBuild;
                return;
            }

            SessionState.SetBool(CheckedThisSessionKey, true);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MovementTestBuilder.ScenePath) != null)
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Debug.Log("[Project Velocity] Movement test scene not found: building it now.");
            MovementTestBuilder.Build();
        }
    }
}
