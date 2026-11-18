using UnityEngine;
using UnityEngine.UI;

public class IntroScene : MonoBehaviour
{
    public Button neverShowButton;
    public Button nextButton;
    public ErrorPanel errorPanel;

    bool isEntering;   // /me 요청 중 버튼 연타 방지

    void Start()
    {
        BindButtonEvent();   
    }

    void BindButtonEvent()
    {
        neverShowButton.onClick.AddListener(() =>
        {
            PlayerPrefs.SetInt("NeverShowIntro", 1);
            PlayerPrefs.Save();
            LoadNextScene();
        });
        
        nextButton.onClick.AddListener(LoadNextScene);
    }

    void LoadNextScene()
    {
        if (isEntering)
        {
            return;
        }
        isEntering = true;
        EntryRouter.Enter(this, errorPanel);
    }

    void OnDestroy()
    {
        neverShowButton.onClick.RemoveAllListeners();
        nextButton.onClick.RemoveAllListeners();
        StopAllCoroutines();
    }
}
