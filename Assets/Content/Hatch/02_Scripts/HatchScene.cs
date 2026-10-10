using UnityEngine;
using UnityEngine.UI;

public class HatchScene : MonoBehaviour
{
    public Button startButton;

    void Start()
    {
        startButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.MainScene);
        });
    }

    void OnDestroy()
    {
        startButton.onClick.RemoveAllListeners();
    }
}