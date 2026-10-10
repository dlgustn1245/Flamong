using UnityEngine;

public class EggRotater : MonoBehaviour
{
    readonly Vector2 center = new Vector2(0, 300); 
    
    public float radius;
    public float speed;
    
    public RectTransform[] items;
    float angle;
    
    void Awake()
    {
        Rotate();
    }

    void Update()
    {
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
}
