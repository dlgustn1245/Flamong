using UnityEngine;
using UnityEngine.UI;

// Group - HeartDeco 아래 하트 이미지들을 풀로 돌려 쓰는 간이 파티클.
// 꺼진 하트를 켜서 Area - Spawn 안의 임의 위치에서 위로 띄우고, 수명이 다하면 다시 끈다.
// Area - Spawn 과 하트들은 같은 부모 아래, 같은 앵커(가운데)여야 위치 계산이 맞는다.
public class HeartParticles : MonoBehaviour
{
    public RectTransform spawnArea;
    public float lifetime = 1.1f;                          // 하트 하나가 떠 있는 시간 (초)
    public Vector2 riseRange = new Vector2(220f, 340f);    // 위로 올라가는 거리 (최소, 최대)
    public float swayAmount = 30f;                         // 좌우로 흔들리는 폭
    public float maxTilt = 15f;                            // 올라가며 기우는 각도 (도)

    Image[] hearts;
    RectTransform[] rects;
    float[] age;        // 음수면 꺼진 상태
    float[] rise;
    float[] sway;
    float[] tilt;
    Vector2[] origin;
    int activeCount;

    void Awake()
    {
        hearts = GetComponentsInChildren<Image>(true);
        int n = hearts.Length;
        rects = new RectTransform[n];
        age = new float[n];
        rise = new float[n];
        sway = new float[n];
        tilt = new float[n];
        origin = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            rects[i] = hearts[i].rectTransform;
            age[i] = -1f;
            hearts[i].gameObject.SetActive(false);
        }
    }

    public void Emit(int count)
    {
        for (int k = 0; k < count; k++)
        {
            Spawn(NextSlot());
        }
    }

    void Update()
    {
        if (activeCount == 0)   // 떠 있는 하트가 없으면 아무것도 하지 않는다
        {
            return;
        }

        for (int i = 0; i < age.Length; i++)
        {
            if (age[i] < 0f)
            {
                continue;
            }

            age[i] += Time.deltaTime;
            float t = age[i] / lifetime;
            if (t >= 1f)
            {
                age[i] = -1f;
                activeCount--;
                hearts[i].gameObject.SetActive(false);
                continue;
            }
            Apply(i, t);
        }
    }

    // 꺼진 하트가 없으면 가장 오래된 것을 다시 쓴다
    int NextSlot()
    {
        int oldest = 0;
        for (int i = 0; i < age.Length; i++)
        {
            if (age[i] < 0f)
            {
                return i;
            }
            if (age[i] > age[oldest])
            {
                oldest = i;
            }
        }
        return oldest;
    }

    void Spawn(int i)
    {
        Rect area = spawnArea.rect;
        origin[i] = spawnArea.anchoredPosition
                    + new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax));
        if (age[i] < 0f)
        {
            activeCount++;
        }
        age[i] = 0f;
        rise[i] = Random.Range(riseRange.x, riseRange.y);
        sway[i] = Random.Range(-swayAmount, swayAmount);
        tilt[i] = Random.Range(-maxTilt, maxTilt);
        hearts[i].gameObject.SetActive(true);
        Apply(i, 0f);
    }

    void Apply(int i, float t)
    {
        float up = 1f - (1f - t) * (1f - t);                      // 처음엔 빠르게, 끝에선 느리게
        float x = Mathf.Sin(t * Mathf.PI * 2f) * sway[i];          // 좌우로 한 번 흔들림
        rects[i].anchoredPosition = origin[i] + new Vector2(x, rise[i] * up);

        float pop = t < 0.15f ? Mathf.Lerp(0.5f, 1f, t / 0.15f) : 1f;   // 튀어나오듯 커짐
        rects[i].localScale = new Vector3(pop, pop, 1f);
        rects[i].localRotation = Quaternion.Euler(0f, 0f, tilt[i] * t);

        Color c = hearts[i].color;
        c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;             // 마지막 40% 동안 사라짐
        hearts[i].color = c;
    }
}
