using UnityEngine;
using UnityEngine.UI;

// 버튼을 누르면 pop. 씬 스크립트마다 소리 줄을 넣지 않고 버튼에 붙여 쓴다.
// 자기 소리가 따로 있는 버튼(Main 하트)에는 붙이지 않는다. 두 번 난다
[RequireComponent(typeof(Button))]
public class ButtonSound : MonoBehaviour
{
    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(Play);
    }

    void Play()
    {
        SoundManager.Instance.PlaySfx(SoundManager.Instance.pop);
    }

    void OnDestroy()
    {
        button.onClick.RemoveListener(Play);
    }
}
