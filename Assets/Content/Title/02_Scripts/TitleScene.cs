using UnityEngine;
using UnityEngine.UI;

public class TitleScene : MonoBehaviour
{
    public Button screenButton;   // 화면 전체인 배경(Image - BG)에 붙인 버튼. 오류 창이 뜨면 그 배경이 터치를 막는다
    public ErrorPanel errorPanel;

    void Start()
    {
        screenButton.onClick.AddListener(OnClickScreen);
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
