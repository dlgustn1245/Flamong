using System;
using UnityEngine;
using UnityEngine.UI;

public class IntroScene : MonoBehaviour
{
    public Button neverShowButton;
    public Button nextButton;

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
        if (string.IsNullOrEmpty(SessionState.Instance.playerId))
        {
            SceneLoader.Load(SceneName.LoginScene);
        }
        else
        {
            SceneLoader.Load(SceneName.MainScene);
        }
    }

    void OnDestroy()
    {
        neverShowButton.onClick.RemoveAllListeners();
        nextButton.onClick.RemoveAllListeners();
    }
}
