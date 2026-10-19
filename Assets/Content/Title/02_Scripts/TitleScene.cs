using UnityEngine;

public class TitleScene : MonoBehaviour
{
    public ErrorPanel errorPanel;

    // /me 요청 중이거나 오류 창이 떠 있으면 화면 탭을 무시한다.
    // Input 은 오류 창의 반투명 배경(터치 막기)과 상관없이 들어오기 때문이다
    bool isEntering;

    void Update()
    {
        if (isEntering || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (PlayerPrefs.GetInt("NeverShowIntro", 0) == 1)
        {
            isEntering = true;
            EntryRouter.Enter(this, errorPanel);
        }
        else
        {
            SceneLoader.Load(SceneName.IntroScene);
        }
    }
}
