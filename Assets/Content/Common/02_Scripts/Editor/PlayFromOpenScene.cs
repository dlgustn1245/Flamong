using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// 에디터에서 어느 씬을 열어 두고 플레이해도 Title 을 먼저 띄워 싱글톤(NetworkManager · SessionState · SoundManager)을 만든 뒤
// 열려 있던 씬으로 넘어간다. 중간 씬을 바로 플레이하면 Instance 가 null 이라 생기는 오류를 없앤다
[InitializeOnLoad]
static class PlayFromOpenScene
{
    const string KEY = "Flamong.PlayFromOpenScene";
    const string TITLE = "Assets/Content/Title/01_Scenes/Title.unity";

    static PlayFromOpenScene()
    {
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TITLE);
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
        {
            EditorPrefs.SetString(KEY, SceneManager.GetActiveScene().name);
        }
        else if (change == PlayModeStateChange.EnteredPlayMode)
        {
            string scene = EditorPrefs.GetString(KEY);
            if (!string.IsNullOrEmpty(scene) && scene != SceneName.TitleScene)
            {
                SceneLoader.Load(scene);
            }
        }
    }
}
