using UnityEditor;
using UnityEngine;

// 빌드 버튼을 누르면 버전을 묻고, 입력한 버전(Player Settings Version)으로 빌드한다. Title 이 이 값을 보여 준다
public class BuildVersionPrompt : EditorWindow
{
    string version;
    bool confirmed;

    [InitializeOnLoadMethod]
    static void Register()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuild);
    }

    static void OnBuild(BuildPlayerOptions options)
    {
        var window = CreateInstance<BuildVersionPrompt>();
        window.titleContent = new GUIContent("빌드 버전");
        window.version = PlayerSettings.bundleVersion;
        window.minSize = window.maxSize = new Vector2(320, 110);
        window.ShowModal();   // 창을 닫을 때까지 여기서 기다린다
        if (!window.confirmed)
        {
            return;
        }
        PlayerSettings.bundleVersion = window.version;
        AssetDatabase.SaveAssets();
        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
    }

    void OnGUI()
    {
        version = EditorGUILayout.TextField("버전", version);
        bool valid = System.Version.TryParse(version, out var v) && v.Build >= 0 && v.Revision < 0;
        if (GUILayout.Button("다음 버전 추천") && valid)
        {
            version = $"{v.Major}.{v.Minor}.{v.Build + 1}";
            GUI.FocusControl(null);
        }
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = valid;
        if (GUILayout.Button("빌드"))
        {
            confirmed = true;
            Close();
        }
        GUI.enabled = true;
        if (GUILayout.Button("취소"))
        {
            Close();
        }
        EditorGUILayout.EndHorizontal();
    }
}