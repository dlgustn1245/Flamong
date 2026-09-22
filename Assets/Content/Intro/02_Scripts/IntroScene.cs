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
            SceneLoader.Load(SceneName.LoginScene);
        });
        
        nextButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.LoginScene);
        });
    }
}
