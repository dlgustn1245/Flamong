using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 「정말 할까요?」 확인 창 (Common/04_Prefabs/Panel - Confirm).
// 문구와 [네]를 눌렀을 때 할 일을 Show 로 넘긴다. [아니요]는 넘긴 일이 없으면 닫기만 한다.
public class ConfirmPanel : MonoBehaviour
{
    public TMP_Text messageText;
    public Button yesButton;
    public Button noButton;

    Action onYes;
    Action onNo;

    // 창이 꺼진 채 시작하므로 처음 Show 할 때 Awake 가 불린다
    void Awake()
    {
        yesButton.onClick.AddListener(OnYes);
        noButton.onClick.AddListener(OnNo);
    }

    public void Show(string message, Action yes, Action no = null)
    {
        messageText.SetText(message);
        onYes = yes;
        onNo = no;
        gameObject.SetActive(true);
    }

    void OnYes()
    {
        Close();
        onYes?.Invoke();
    }

    void OnNo()
    {
        Close();
        onNo?.Invoke();
    }

    void Close()
    {
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        yesButton.onClick.RemoveAllListeners();
        noButton.onClick.RemoveAllListeners();
    }
}
