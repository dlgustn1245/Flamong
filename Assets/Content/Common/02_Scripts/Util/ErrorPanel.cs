using System;
using UnityEngine;
using UnityEngine.UI;

// 서버 오류 안내 창 (Common/04_Prefabs/Panel - Error). 「다시 시도」로 할 일을 씬마다 Show 로 넘긴다.
public class ErrorPanel : MonoBehaviour
{
    public Button retryButton;

    Action retry;

    // 창이 꺼진 채 시작하므로 처음 Show 할 때 Awake 가 불린다
    void Awake()
    {
        retryButton.onClick.AddListener(OnRetry);
    }

    public void Show(Action onRetry)
    {
        retry = onRetry;
        gameObject.SetActive(true);
    }

    void OnRetry()
    {
        gameObject.SetActive(false);
        retry?.Invoke();
    }

    void OnDestroy()
    {
        retryButton.onClick.RemoveAllListeners();
    }
}
