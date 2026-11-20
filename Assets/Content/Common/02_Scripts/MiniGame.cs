using System;
using System.Collections;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 미니게임 한 판의 부모. 3 · 2 · 1 세기, 판 시계, 남은 시간 표시, 점수, 「그만!」을 맡는다.
// 자식은 판 규칙만 쓴다: OnReset(판을 처음 모습으로) → OnPlay(다 센 직후) → OnTick(매 프레임) → OnFinish(시간이 다 됨).
// 판 시계는 Time.deltaTime 을 더한 값이라 MiniGameScene 이 timeScale 을 0 으로 두면 판 전체가 멈춘다
public abstract class MiniGame : MonoBehaviour
{
    readonly WaitForSecondsRealtime oneSecond = new WaitForSecondsRealtime(1f);

    public GameObject countDownGroup;      // 세는 동안 켜는 것. 글자만 있으면 글자 오브젝트, 빛을 깔았으면 그 묶음
    public TextMeshProUGUI countDownText;

    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public Image timeFill;
    public Color warnColor = new Color32(0xE8, 0x46, 0x5F, 0xFF);   // 남은 5초 글자색

    protected GameRules rules;
    protected int score;
    protected float clock;   // 판 시계(초). timeScale 을 따라 멈춘 동안은 흐르지 않는다. 자식은 읽기만 한다

    Action<int> onEnd;
    bool isPlaying;          // 다 센 뒤부터 시간이 다 될 때까지
    int delay;               // 카운트다운 초
    float lastTapAt;
    Color timeColor;
    int shownSec;

    public abstract string GameId { get; }

    protected abstract void OnReset();          // 판을 처음 모습으로. 세는 동안 이 모습이 보인다. 다시 하기에서도 불린다
    protected abstract void OnPlay();           // 다 센 직후 한 번
    protected abstract void OnTick(float dt);   // 판 중 매 프레임. 마지막 프레임(clock 이 판 길이를 넘은 프레임)에도 불린다
    protected abstract void OnFinish();         // 시간이 다 된 순간. 진행 중이던 연출을 끝 모습으로

    protected virtual void Awake()
    {
        timeColor = timeText.color;
    }

    protected virtual void OnDestroy()
    {
        StopAllCoroutines();
    }

    // 다 셀 때 끝나는 코루틴. MiniGameScene 은 이것이 끝난 뒤부터 [뒤로]를 멈춤으로 다룬다. 판이 끝나면 onEnd(점수)를 한 번 부른다
    public IEnumerator StartGame(GameRules rules, Action<int> onEnd)
    {
        this.rules = rules;
        this.onEnd = onEnd;
        delay = rules.countdownSec;
        isPlaying = false;
        score = 0;
        clock = 0f;
        lastTapAt = -1f;
        scoreText.SetText("{0}점", 0f);
        timeText.color = timeColor;
        timeText.SetText("{0}", (float)rules.durationSec);
        timeFill.fillAmount = 1f;
        shownSec = rules.durationSec;
        OnReset();
        yield return StartCoroutine(CountDown());
        isPlaying = true;
        SoundManager.Instance.PlaySfx(SoundManager.Instance.sparkle);
        OnPlay();
    }

    // timeScale 이 0 인 동안(멈춤 뒤 다시 세기)에도 돌아야 해서 Realtime 으로 센다
    public IEnumerator CountDown(Action timerAction = null)
    {
        countDownGroup.SetActive(true);
        for (int i = delay; i > 0; i--)
        {
            countDownText.SetText("{0}", i);
            SoundManager.Instance.PlaySfx(SoundManager.Instance.count);
            yield return oneSecond;
        }
        countDownGroup.SetActive(false);
        timerAction?.Invoke();
    }

    void Update()
    {
        // timeScale 0 이어도 멈춘 그 프레임의 deltaTime 은 0 이 아닐 수 있어 따로 막는다
        if (!isPlaying || Time.timeScale == 0f)
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

        OnTick(dt);
        if (remain <= 0f)
        {
            isPlaying = false;
            OnFinish();
            countDownGroup.SetActive(true);
            countDownText.SetText("그만!");
            onEnd?.Invoke(score);
        }
    }

    // 지금 누름을 받아도 되는가. 받으면 연타 시계를 지금으로 맞춘다.
    // 멈춤 · 다시 세는 중(timeScale 0), 끝난 프레임에 늦게 들어온 누름, minTapMs 안의 연타는 버린다.
    // 한 프레임에 두 번 눌리면 clock 이 같아 두 번째가 걸린다. 서버의 점수 상한이 이 간격으로 계산된다
    protected bool AcceptTap()
    {
        if (!isPlaying || Time.timeScale == 0f || clock >= rules.durationSec)
        {
            return false;
        }
        if (clock - lastTapAt < rules.minTapMs / 1000f)
        {
            return false;
        }
        lastTapAt = clock;
        return true;
    }

    protected void AddScore(int points)
    {
        score += points;
        scoreText.SetText("{0}점", (float)score);
    }
}
