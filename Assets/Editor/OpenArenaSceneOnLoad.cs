using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Headless batch builds clear Library/LastSceneManagerSetup.txt, so the editor
/// then opens with a blank "Untitled" scene and the arena looks lost. This opens
/// the real scene again automatically whenever the editor starts on an empty
/// untitled scene. Ignored while playing.
/// </summary>
[InitializeOnLoad]
public static class OpenArenaSceneOnLoad
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    static OpenArenaSceneOnLoad()
    {
        EditorApplication.delayCall += TryOpen;
    }

    private static void TryOpen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var active = SceneManager.GetActiveScene();
        bool blankUntitled = string.IsNullOrEmpty(active.path)
                             && active.GetRootGameObjects().Length <= 2; // fresh scene = camera + light

        if (blankUntitled)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
}
