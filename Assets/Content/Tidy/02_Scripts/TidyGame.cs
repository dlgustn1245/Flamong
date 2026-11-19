using System;
using System.Collections;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Random = UnityEngine.Random;   // using System 의 System.Random 과 겹친다

// 마음 정리 한 판. Canvas - Main/Area - Tap(화면 전체 누름 영역)에 붙는다.
// 판 시계는 Time.deltaTime 을 더한 값이라 MiniGameScene 이 timeScale 을 0 으로 두면 판 전체가 멈춘다
public class TidyGame : MiniGame, IPointerDownHandler
{ 
    public override string GameId => GameList.Tidy;

    public Image[] icons;                  // 줄 아이콘 6개를 돌려 쓴다. 5칸이 보이고 1개는 서랍으로 날아가는 중
    public Image[] drawerIcons;            // [0] 왼쪽 · [1] 오른쪽
    public TextMeshProUGUI[] drawerNames;  // [0] 왼쪽 · [1] 오른쪽
    public Sprite[] emotionSprites;        // tired · stress · achievement · focus · calm 순서
    public string[] emotionNames = { "피로", "스트레스", "성취감", "몰입", "평온" };

    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI multText;
    public TextMeshProUGUI timeText;
    public Image timeFill;

    // 보이는 것만 여기서 정한다. 판 규칙(시간 · 잠금 · 배율)은 서버 rules
    public float[] slotY = { 330, 530, 680, 830, 980 };   // 줄 칸, 맨 아래가 0번
    public float[] slotScale = { 1, 0.7f, 0.7f, 0.7f, 0.7f };
    public float[] slotAlpha = { 1, 1, 0.8f, 0.6f, 0.4f };
    public float flyTime = 0.1f;        // 맨 아래 → 서랍. minTapMs(0.1초) 이하여야 날던 아이콘이 도착 전에 맨 위로 다시 쓰이지 않는다
    public float shiftTime = 0.08f;     // 한 칸 내려오기
    public float blinkRate = 4f;        // 곧 바뀔 서랍 깜빡임, 초당
    public float popTime = 0.2f;        // 서랍이 바뀔 때 · 배율이 오를 때 1.3 → 1
    public float pressTime = 0.1f;      // 누른 쪽 서랍 0.9 → 1
    public float shakeAmount = 12f;     // 틀렸을 때 맨 아래 흔들기
    public Color warnColor = new Color32(0xE8, 0x46, 0x5F, 0xFF);   // 남은 5초 글자색

    enum State { Idle, Countdown, Playing, Done }

    GameRules rules;
    Action<int> onEnd;

    State state;
    int score;
    int combo;
    int sorted;          // 지금까지 맞힌 수 = 맨 아래 아이콘의 순번
    float clock;         // 판 시계. timeScale 을 따라 멈춘 동안은 흐르지 않는다
    float lockUntil;
    float lastTapAt;

    int[] stageL;        // 단계(순번 / changeEvery)마다 왼쪽 서랍 감정
    int[] stageR;
    int[] changeSide;    // 그 단계에서 바뀐 쪽. 바뀌기 전에 깜빡일 서랍
    int[] sides;         // 순번마다 정답 쪽 (0 왼쪽 · 1 오른쪽)

    RectTransform[] iconRects;
    Color timeColor;

    // 연출. 모두 Update 에서 float 타이머로 진행한다(탭마다 코루틴을 만들지 않는다). 나이가 음수면 꺼짐
    Vector2[] fromPos;
    Vector2[] toPos;
    float[] fromScale;
    float[] toScale;
    float[] fromAlpha;
    float[] toAlpha;
    float[] moveAge;
    float[] moveTime;
    RectTransform[] drawerRects;
    Vector2[] drawerPos;     // 날아갈 곳. 줄 아이콘과 같은 부모(Group - Field)라 좌표를 그대로 쓴다
    float[] popAge = { -1f, -1f };
    float[] pressAge = { -1f, -1f };
    float shakeAge = -1f;
    int blinkSide = -1;      // 곧 바뀔 서랍. -1 이면 없음
    float blinkAge;
    RectTransform multRect;
    float multPopAge = -1f;
    int shownMult;
    int shownSec;
    float startTextAge = -1f;

    const float startTextTime = 0.5f;   // 처음 세기 뒤 「시작!」

    void Awake()
    {
        int n = icons.Length;
        iconRects = new RectTransform[n];
        for (int i = 0; i < n; i++)
        {
            iconRects[i] = icons[i].rectTransform;
        }
        timeColor = timeText.color;

        fromPos = new Vector2[n];
        toPos = new Vector2[n];
        fromScale = new float[n];
        toScale = new float[n];
        fromAlpha = new float[n];
        toAlpha = new float[n];
        moveAge = new float[n];
        moveTime = new float[n];
        drawerRects = new RectTransform[drawerIcons.Length];
        drawerPos = new Vector2[drawerIcons.Length];
        for (int i = 0; i < drawerIcons.Length; i++)
        {
            drawerRects[i] = drawerIcons[i].rectTransform;
            drawerPos[i] = drawerRects[i].anchoredPosition;
        }
        multRect = multText.rectTransform;
    }

    public override IEnumerator StartGame(GameRules rules, Action<int> onEnd)
    {
        this.rules = rules;
        this.onEnd = onEnd;
        delay = rules.countdownSec;
        BuildBoard();
        state = State.Countdown;
        yield return StartCoroutine(CountDown());   // 세는 동안 줄이 보여 첫 감정을 미리 읽을 수 있다
        state = State.Playing;
        countDownText.gameObject.SetActive(true);
        countDownText.SetText("시작!");
        SoundManager.Instance.PlaySfx(SoundManager.Instance.sparkle);
        startTextAge = 0f;
    }

    // 판 만들기. 다시 하기에서는 배열을 새로 만들지 않고 다시 쓴다
    void BuildBoard()
    {
        score = 0;
        combo = 0;
        sorted = 0;
        clock = 0f;
        lockUntil = -1f;
        lastTapAt = -1f;

        int maxTaps = rules.durationSec * 1000 / rules.minTapMs + 1;   // 한 판에 받아들일 수 있는 최대 입력
        int stages = maxTaps / rules.changeEvery + 2;
        if (stageL == null || stageL.Length < stages)
        {
            stageL = new int[stages];
            stageR = new int[stages];
            changeSide = new int[stages];
        }
        if (sides == null || sides.Length < maxTaps + icons.Length)
        {
            sides = new int[maxTaps + icons.Length];
        }

        // 단계 0 은 서로 다른 감정 둘. 그 뒤로는 한쪽씩 번갈아, 지금 두 서랍에 없는 감정으로 바꾼다
        int count = emotionSprites.Length;
        stageL[0] = Random.Range(0, count);
        stageR[0] = (stageL[0] + Random.Range(1, count)) % count;
        int change = Random.Range(0, 2);
        for (int s = 1; s < stages; s++)
        {
            int left = stageL[s - 1];
            int right = stageR[s - 1];
            int pick = Random.Range(0, count - 2);
            int next = 0;
            for (int e = 0; e < count; e++)
            {
                if (e == left || e == right)
                {
                    continue;
                }
                if (pick == 0)
                {
                    next = e;
                    break;
                }
                pick--;
            }
            stageL[s] = change == 0 ? next : left;
            stageR[s] = change == 1 ? next : right;
            changeSide[s] = change;
            change = 1 - change;
        }

        int prev = 0;
        int run = 0;
        for (int i = 0; i < sides.Length; i++)
        {
            sides[i] = NextSide(i, prev, run, rules.maxRun, Random.value);
            run = i > 0 && sides[i] == prev ? run + 1 : 1;
            prev = sides[i];
        }

        // 순번 0 ~ 4 를 줄 칸에 놓고, 남는 하나는 투명하게
        for (int i = 0; i < icons.Length; i++)
        {
            moveAge[i] = 0f;
            moveTime[i] = 0f;
            if (i < slotY.Length)
            {
                PlaceIcon(i, i);
            }
            else
            {
                icons[i].color = new Color(1f, 1f, 1f, 0f);
            }
        }
        SetDrawers(0);

        scoreText.SetText("{0}점", 0f);
        comboText.gameObject.SetActive(false);
        multText.gameObject.SetActive(false);
        timeText.color = timeColor;
        timeText.SetText("{0}", (float)rules.durationSec);
        timeFill.fillAmount = 1f;

        shownSec = rules.durationSec;
        shownMult = 1;
        shakeAge = -1f;
        blinkSide = -1;
        startTextAge = -1f;
        multPopAge = -1f;
        multRect.localScale = Vector3.one;
        for (int i = 0; i < popAge.Length; i++)
        {
            popAge[i] = -1f;
            pressAge[i] = -1f;
        }
        // changeEvery 가 보이는 칸 수(5) 이하면 시작부터 새 감정이 보인다
        if ((slotY.Length - 1) / rules.changeEvery > 0)
        {
            blinkSide = changeSide[1];
            blinkAge = 0f;
        }
    }

    // 화면 왼쪽 절반 = 0, 오른쪽 절반 = 1. 손을 뗄 때가 아니라 누르는 순간 판정한다
    public void OnPointerDown(PointerEventData eventData)
    {
        Tap(eventData.position.x < Screen.width * 0.5f ? 0 : 1);
    }

    void Tap(int side)
    {
        // 멈춤 · 다시 세는 중(timeScale 0), 끝난 프레임에 늦게 들어온 누름은 버린다
        if (state != State.Playing || Time.timeScale == 0f || clock >= rules.durationSec)
        {
            return;
        }
        // 틀린 뒤 잠금 · 연타 간격. 한 프레임에 두 번 눌리면 clock 이 같아 두 번째가 여기서 걸린다
        if (clock < lockUntil || clock - lastTapAt < rules.minTapMs / 1000f)
        {
            return;
        }
        lastTapAt = clock;
        pressAge[side] = 0f;

        if (side != sides[sorted])
        {
            SoundManager.Instance.PlaySfx(SoundManager.Instance.wrong);
            combo = 0;
            lockUntil = clock + rules.lockMs / 1000f;
            comboText.gameObject.SetActive(false);
            multText.gameObject.SetActive(false);
            shownMult = 1;
            shakeAge = 0f;
            return;
        }

        SoundManager.Instance.PlaySfx(SoundManager.Instance.pop);
        int n = icons.Length;
        Move(sorted % n, drawerPos[side], 0.5f, 0f, flyTime);   // 맨 아래 → 그 쪽 서랍으로 쏙
        score += PointsFor(combo, rules.comboAt);
        combo++;
        sorted++;
        shakeAge = -1f;

        // 남은 넷은 한 칸씩 내려오고, 새 순번이 맨 위 칸에 투명도 0 에서 나타난다
        int last = slotY.Length - 1;
        for (int slot = 0; slot < last; slot++)
        {
            Move((sorted + slot) % n, new Vector2(0f, slotY[slot]), slotScale[slot], slotAlpha[slot], shiftTime);
        }
        int top = sorted + last;
        int k = top % n;
        icons[k].sprite = emotionSprites[EmotionOf(top)];
        icons[k].color = new Color(1f, 1f, 1f, 0f);
        iconRects[k].anchoredPosition = new Vector2(0f, slotY[last]);
        iconRects[k].localScale = new Vector3(slotScale[last], slotScale[last], 1f);
        Move(k, new Vector2(0f, slotY[last]), slotScale[last], slotAlpha[last], shiftTime);

        int stage = sorted / rules.changeEvery;
        if (sorted % rules.changeEvery == 0)
        {
            SetDrawers(stage);
            popAge[changeSide[stage]] = 0f;
            blinkSide = -1;
        }
        // 다음 단계 아이콘(새 감정)이 맨 위 칸에 보이는 순간부터 바뀔 서랍을 깜빡인다
        if (blinkSide < 0 && top / rules.changeEvery > stage)
        {
            blinkSide = changeSide[stage + 1];
            blinkAge = 0f;
        }

        scoreText.SetText("{0}점", (float)score);
        comboText.gameObject.SetActive(true);
        comboText.SetText("콤보 {0}", (float)combo);
        int mult = PointsFor(combo, rules.comboAt);   // 다음 정답에 붙을 점수
        if (mult >= 2)
        {
            multText.gameObject.SetActive(true);
            multText.SetText("{0}배", (float)mult);
            if (mult > shownMult)
            {
                multPopAge = 0f;
            }
        }
        shownMult = mult;
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }

    void Update()
    {
        // timeScale 0 이어도 멈춘 그 프레임의 deltaTime 은 0 이 아닐 수 있어 따로 막는다
        if (state != State.Playing || Time.timeScale == 0f)
        {
            return;
        }
        float dt = Time.deltaTime;   // 멈추면(timeScale 0) 0 이라 판 시계 · 연출이 함께 멈춘다
        clock += dt;
        float remain = rules.durationSec - clock;
        timeFill.fillAmount = Mathf.Max(0f, remain / rules.durationSec);
        int sec = Mathf.Max(0, Mathf.CeilToInt(remain));
        if (sec != shownSec)
        {
            shownSec = sec;
            timeText.SetText("{0}", (float)sec);
            timeText.color = sec <= 5 ? warnColor : timeColor;
        }

        if (startTextAge >= 0f)
        {
            startTextAge += dt;
            if (startTextAge >= startTextTime)
            {
                startTextAge = -1f;
                countDownText.gameObject.SetActive(false);
            }
        }

        Animate(dt);
        if (remain <= 0f)
        {
            Finish();
        }
    }

    void Finish()
    {
        state = State.Done;
        Animate(10f);   // 날던 아이콘 · 흔들기 · 팝을 끝 모습으로
        blinkSide = -1;
        for (int i = 0; i < drawerIcons.Length; i++)
        {
            drawerIcons[i].color = Color.white;
        }
        startTextAge = -1f;
        countDownText.gameObject.SetActive(true);
        countDownText.SetText("그만!");
        onEnd?.Invoke(score);
    }

    // 아이콘 k 를 지금 모습에서 목표 모습으로 time 동안 옮긴다
    void Move(int k, Vector2 pos, float scale, float alpha, float time)
    {
        fromPos[k] = iconRects[k].anchoredPosition;
        fromScale[k] = iconRects[k].localScale.x;
        fromAlpha[k] = icons[k].color.a;
        toPos[k] = pos;
        toScale[k] = scale;
        toAlpha[k] = alpha;
        moveAge[k] = 0f;
        moveTime[k] = time;
    }

    void Animate(float dt)
    {
        int n = icons.Length;
        for (int k = 0; k < n; k++)
        {
            if (moveAge[k] >= moveTime[k])
            {
                continue;
            }
            moveAge[k] += dt;
            float t = Mathf.Clamp01(moveAge[k] / moveTime[k]);
            iconRects[k].anchoredPosition = Vector2.Lerp(fromPos[k], toPos[k], t);
            float s = Mathf.Lerp(fromScale[k], toScale[k], t);
            iconRects[k].localScale = new Vector3(s, s, 1f);
            var c = icons[k].color;
            c.a = Mathf.Lerp(fromAlpha[k], toAlpha[k], t);
            icons[k].color = c;
        }

        // 틀린 뒤 잠금 동안 맨 아래 아이콘 흔들기
        if (shakeAge >= 0f)
        {
            shakeAge += dt;
            float lockTime = rules.lockMs / 1000f;
            var rect = iconRects[sorted % n];
            float x = 0f;
            if (shakeAge >= lockTime)
            {
                shakeAge = -1f;
            }
            else
            {
                x = Mathf.Sin(shakeAge * 60f) * shakeAmount * (1f - shakeAge / lockTime);
            }
            rect.anchoredPosition = new Vector2(x, slotY[0]);
        }

        if (blinkSide >= 0)
        {
            blinkAge += dt;
            var c = drawerIcons[blinkSide].color;
            c.a = Mathf.Lerp(0.4f, 1f, Mathf.PingPong(blinkAge * blinkRate * 2f, 1f));
            drawerIcons[blinkSide].color = c;
        }

        // 서랍 크기 = 바뀔 때 팝 × 누를 때 눌림
        for (int d = 0; d < drawerRects.Length; d++)
        {
            if (popAge[d] < 0f && pressAge[d] < 0f)
            {
                continue;
            }
            float s = 1f;
            if (popAge[d] >= 0f)
            {
                popAge[d] += dt;
                if (popAge[d] >= popTime)
                {
                    popAge[d] = -1f;
                }
                else
                {
                    s *= Mathf.Lerp(1.3f, 1f, popAge[d] / popTime);
                }
            }
            if (pressAge[d] >= 0f)
            {
                pressAge[d] += dt;
                if (pressAge[d] >= pressTime)
                {
                    pressAge[d] = -1f;
                }
                else
                {
                    s *= Mathf.Lerp(0.9f, 1f, pressAge[d] / pressTime);
                }
            }
            drawerRects[d].localScale = new Vector3(s, s, 1f);
        }

        if (multPopAge >= 0f)
        {
            multPopAge += dt;
            float s = 1f;
            if (multPopAge >= popTime)
            {
                multPopAge = -1f;
            }
            else
            {
                s = Mathf.Lerp(1.3f, 1f, multPopAge / popTime);
            }
            multRect.localScale = new Vector3(s, s, 1f);
        }
    }

    // 맞히기 전 콤보로 이번 정답 점수. comboAt [10, 30] 이면 0~9 → 1, 10~29 → 2, 30 이상 → 3
    public static int PointsFor(int combo, int[] comboAt)
    {
        int points = 1;
        for (int i = 0; i < comboAt.Length; i++)
        {
            if (combo >= comboAt[i])
            {
                points++;
            }
        }
        return points;
    }

    // 순번 index 아이콘의 감정: 자기 단계의 서랍 짝 중 정답 쪽.
    // 맨 아래 순번이 늘 sorted 라서 맨 아래는 늘 지금 두 서랍 중 하나와 맞는다
    int EmotionOf(int index)
    {
        int stage = index / rules.changeEvery;
        return sides[index] == 0 ? stageL[stage] : stageR[stage];
    }

    // 순번 index 아이콘(icons[index % 6])을 줄 칸 slot 에 놓는다
    void PlaceIcon(int index, int slot)
    {
        var icon = icons[index % icons.Length];
        icon.sprite = emotionSprites[EmotionOf(index)];
        icon.color = new Color(1f, 1f, 1f, slotAlpha[slot]);
        var rect = iconRects[index % icons.Length];
        rect.anchoredPosition = new Vector2(0f, slotY[slot]);
        rect.localScale = new Vector3(slotScale[slot], slotScale[slot], 1f);
    }

    void SetDrawers(int stage)
    {
        drawerIcons[0].sprite = emotionSprites[stageL[stage]];
        drawerIcons[1].sprite = emotionSprites[stageR[stage]];
        drawerNames[0].text = emotionNames[stageL[stage]];
        drawerNames[1].text = emotionNames[stageR[stage]];
        for (int i = 0; i < drawerIcons.Length; i++)
        {
            drawerIcons[i].color = Color.white;
            drawerRects[i].localScale = Vector3.one;
        }
    }

    // 첫 개는 무작위, 둘째는 첫째의 반대(양쪽 서랍을 한 번씩 쓰게), 같은 쪽이 maxRun 번 이어졌으면 반대
    public static int NextSide(int index, int prevSide, int run, int maxRun, float random)
    {
        if (index == 1 || (index > 1 && run >= maxRun))
        {
            return 1 - prevSide;
        }
        return random < 0.5f ? 0 : 1;
    }
}
