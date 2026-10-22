using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 문구와 [확인]만 있는 안내 창 (Common/04_Prefabs/Panel - Notice).
// 「준비 중이에요」, 레벨업 · 완성처럼 알리기만 할 때 Show 로 문구를 넘긴다
public class NoticePanel : MonoBehaviour
{
    public TMP_Text messageText;
    public Button okButton;

    // 창이 꺼진 채 시작하므로 처음 Show 할 때 Awake 가 불린다
    void Awake()
    {
        okButton.onClick.AddListener(Close);
    }

    public void Show(string message)
    {
        messageText.SetText(message);
        gameObject.SetActive(true);
    }

    void Close()
    {
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        okButton.onClick.RemoveAllListeners();
    }
}
