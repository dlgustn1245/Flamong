using System;
using UnityEngine;

public class EggRotater : MonoBehaviour
{
    public readonly Vector2 center = new Vector2(0, 300);   // HatchScene 이 고른 알을 여기로 옮긴다
    
    public float radius;
    public float speed;

    public float stopTime = 2.0f;
    public float minStopAngle = 360.0f;
    
    bool stopping;
    float stopFrom, stopDistance, stopElapsed;
    Action onStopped;
    
    public RectTransform[] items;
    float angle;
    
    void Awake()
    {
        Rotate();
    }

    void Update()
    {
        if (stopping)
        {
            stopElapsed += Time.deltaTime;
            float u = Mathf.Clamp01(stopElapsed / stopTime);
            float k = 1f - u;
            angle = Mathf.Repeat(stopFrom + stopDistance * (1f - k * k * k), 360f);   // 빠르게 시작해 천천히 끝남
            Rotate();
            if (u >= 1f)
            {
                stopping = false;
                speed = 0f;            // 이후 Update 가 위치를 덮어쓰지 않게
                var done = onStopped;
                onStopped = null;
                done?.Invoke();
            }
            return;
        }
        
        if (speed == 0f)
        {
            return;
        }
        angle = Mathf.Repeat(angle + speed * Time.deltaTime, 360f);
        Rotate();
    }

    void Rotate()
    {
        float step = 360f / items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            float rad = (90f - angle - step * i) * Mathf.Deg2Rad;
            items[i].anchoredPosition = center + 
                                        new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        }
    }

    public void StopAt(int index, Action action)
    {
        float step = 360f / items.Length;
        float target = Mathf.Repeat(180f - step * index, 360f);   // 포인터(▲)가 원 아래 6시에 있어 그 자리에 세운다
        float distance = Mathf.Repeat(target - angle, 360f);
        while (distance < minStopAngle)
        {
            distance += 360f;
        }
        stopFrom = angle;
        stopDistance = distance;
        stopElapsed = 0f;
        onStopped = action;
        stopping = true;
    }
}
