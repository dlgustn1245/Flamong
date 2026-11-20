using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class HoldRepeat : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float holdDelay = 0.4f;
    public float repeatInterval = 0.05f;

    public Action onPress;   // MainScene 이 넣고 OnDestroy 에서 비운다

    Button button;
    WaitForSeconds delayWait;
    WaitForSeconds repeatWait;
    Coroutine repeat;
    int pointerId;

    void Awake()
    {
        button = GetComponent<Button>();
        delayWait = new WaitForSeconds(holdDelay);
        repeatWait = new WaitForSeconds(repeatInterval);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (repeat != null || !button.IsInteractable())
        {
            return;
        }
        pointerId = eventData.pointerId;
        repeat = StartCoroutine(Repeat());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == pointerId)
        {
            Stop();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData.pointerId == pointerId)
        {
            Stop();
        }
    }

    IEnumerator Repeat()
    {
        onPress?.Invoke();
        yield return delayWait;
        while (button.IsInteractable())
        {
            onPress?.Invoke();
            yield return repeatWait;
        }

        repeat = null;
    }
    
    void Stop()
    {
        if (repeat != null)
        {
            StopCoroutine(repeat);
            repeat = null;
        }
    }
    
    void OnDisable()
    {
        Stop();
    }
}
