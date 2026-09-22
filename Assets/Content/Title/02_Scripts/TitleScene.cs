using UnityEngine;

public class TitleScene : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (PlayerPrefs.GetInt("NeverShowIntro", 0) == 1)
            {
                SceneLoader.Load(SceneName.LoginScene);
            }
            else
            {
                SceneLoader.Load(SceneName.IntroScene);
            }
        }
    }
}
