using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TitleScene : MonoBehaviour
{
    public Button screenButton;   // 화면 전체인 배경(Image - BG)에 붙인 버튼. 오류 창이 뜨면 그 배경이 터치를 막는다
    public ErrorPanel errorPanel;
    public TMP_Text versionText;

    void Awake()
    {
        #if UNITY_EDITOR
        Application.runInBackground = true;
        #endif
    }

    void Start()
    {
        screenButton.onClick.AddListener(OnClickScreen);
        // buildGUID 는 빌드마다 바뀌어, 버전 숫자를 안 올려도 새 빌드인지 구분된다. 에디터에서는 비어 있다
        string build = Application.buildGUID;
        versionText.SetText($"v{Application.version} · {(build != null && build.Length >= 6 ? build.Substring(0, 6) : "editor")}");
    }

    void OnClickScreen()
    {
        if (PlayerPrefs.GetInt("NeverShowIntro", 0) == 1)
        {
            screenButton.interactable = false;   // /me 응답을 기다리는 동안 다시 눌리지 않게
            EntryRouter.Enter(this, errorPanel);
        }
        else
        {
            SceneLoader.Load(SceneName.IntroScene);
        }
    }

    void OnDestroy()
    {
        screenButton.onClick.RemoveAllListeners();
        StopAllCoroutines();
    }
}
