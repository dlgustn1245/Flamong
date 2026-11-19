using System;
using System.Collections;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;   // using System 의 System.Random 과 겹친다

// 짝꿍 찾기 한 판. 루트의 빈 오브젝트 PairsGame 에 붙는다(멈출 때 꺼지는 Group - Field 아래에 두면 다시 세기가 돌지 않는다).
// 판 시계는 Time.deltaTime 을 더한 값이라 MiniGameScene 이 timeScale 을 0 으로 두면 판 전체가 멈춘다
public class PairsGame : MiniGame
{
    public override string GameId => GameList.Pairs;
    
    public Button[] cardButtons;           // 12장. 왼쪽 위부터 가로로
    public CanvasGroup[] cardGroups;       // 맞춘 카드를 흐리게
    public Image[] backs;                  // 뒷면(알)
    public Image[] faces;                  // 앞면(아기 플라몽)
    public Sprite[] faceSprites;           // [speciesId] = Lv1 그림 24장. 알 하나에 3종씩 이어진다

    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI streakText;
    public TextMeshProUGUI timeText;
    public Image timeFill;

    // 보이는 것만 여기서 정한다. 판 규칙(시간 · 점수 · 난이도)은 서버 rules
    public float matchedAlpha = 0.35f;  // 맞춘 카드
    public float popTime = 0.12f;       // 뒤집을 때 1.1 → 1
    public float clearTime = 0.3f;      // 다 맞춘 판을 잠깐 보여 준 뒤 새 판
    public Color warnColor = new Color32(0xE8, 0x46, 0x5F, 0xFF);   // 남은 5초 글자색

    const int speciesPerEgg = 3;

    enum State { Idle, Countdown, Playing, Done }
    enum Phase { Preview, Pick, Miss, Clear }

    GameRules rules;
    Action<int> onEnd;

    State state;
    Phase phase;
    int score;
    int streak;          // 안 틀리고 이어 맞춘 수. 판을 넘어 이어진다
    int board;           // 판 번호(0부터)
    int left;            // 이 판에 남은 짝
    int first;           // 먼저 뒤집은 카드. 없으면 -1
    int missA;
    int missB;
    float clock;         // 판 시계. timeScale 을 따라 멈춘 동안은 흐르지 않는다
    float phaseUntil;
    float lastTapAt;

    int[] deck;          // [카드] = speciesId
    bool[] matched;
    int[] eggOrder;
    RectTransform[] cardRects;
    float[] popAge;      // 음수면 꺼짐
    Color timeColor;
    int shownSec;

    void Awake()
    {
        int n = cardButtons.Length;
        deck = new int[n];
        matched = new bool[n];
        popAge = new float[n];
        cardRects = new RectTransform[n];
        eggOrder = new int[faceSprites.Length / speciesPerEgg];
        for (int i = 0; i < n; i++)
        {
            int card = i;
            cardRects[i] = (RectTransform)cardButtons[i].transform;
            cardButtons[i].onClick.AddListener(() => Tap(card));
        }
        timeColor = timeText.color;
    }

    void OnDestroy()
    {
        for (int i = 0; i < cardButtons.Length; i++)
        {
            cardButtons[i].onClick.RemoveAllListeners();
        }
        StopAllCoroutines();
    }

    // 세는 동안은 모두 뒷면. 다 세면 첫 판을 깔고 미리 보기부터 판 시계가 흐른다
    public override IEnumerator StartGame(GameRules rules, Action<int> onEnd)
    {
        this.rules = rules;
        this.onEnd = onEnd;
        delay = rules.countdownSec;
        ResetGame();
        state = State.Countdown;
        yield return StartCoroutine(CountDown());
        state = State.Playing;
        SoundManager.Instance.PlaySfx(SoundManager.Instance.sparkle);   // 「시작!」 글자는 미리 보기 카드를 가려 띄우지 않는다
        Deal();
    }

    void ResetGame()
    {
        score = 0;
        streak = 0;
        board = 0;
        clock = 0f;
        lastTapAt = -1f;
        first = -1;
        for (int i = 0; i < cardButtons.Length; i++)
        {
            matched[i] = false;
            cardGroups[i].alpha = 1f;
            popAge[i] = -1f;
            cardRects[i].localScale = Vector3.one;
            Show(i, false);
        }

        scoreText.SetText("{0}점", 0f);
        streakText.gameObject.SetActive(false);
        timeText.color = timeColor;
        timeText.SetText("{0}", (float)rules.durationSec);
        timeFill.fillAmount = 1f;
        shownSec = rules.durationSec;
    }

    // 새 판. 서로 다른 알에서 한 종씩 고르되, 판 번호가 similarAt 값 이상이 될 때마다 같은 알의 2종 묶음을 하나씩 넣는다
    void Deal()
    {
        int n = cardButtons.Length;
        int pairs = n / 2;
        int similar = 0;
        for (int i = 0; i < rules.similarAt.Length; i++)
        {
            if (board >= rules.similarAt[i])
            {
                similar++;
            }
        }
        similar = Mathf.Min(similar, pairs / 2);

        for (int i = 0; i < eggOrder.Length; i++)
        {
            eggOrder[i] = i;
        }
        Shuffle(eggOrder);

        int c = 0;
        int e = 0;
        for (int g = 0; g < similar; g++)
        {
            int egg = eggOrder[e++];
            int a = Random.Range(0, speciesPerEgg);
            int b = (a + Random.Range(1, speciesPerEgg)) % speciesPerEgg;
            deck[c] = deck[c + 1] = egg * speciesPerEgg + a;
            deck[c + 2] = deck[c + 3] = egg * speciesPerEgg + b;
            c += 4;
        }
        while (c < n)
        {
            deck[c] = deck[c + 1] = eggOrder[e++] * speciesPerEgg + Random.Range(0, speciesPerEgg);
            c += 2;
        }
        Shuffle(deck);

        for (int i = 0; i < n; i++)
        {
            matched[i] = false;
            cardGroups[i].alpha = 1f;
            faces[i].sprite = faceSprites[deck[i]];
            Show(i, true);
        }
        first = -1;
        left = pairs;
        phase = Phase.Preview;
        phaseUntil = clock + rules.previewMs / 1000f;
    }

    static void Shuffle(int[] a)
    {
        for (int i = a.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
    }

    void Show(int card, bool faceUp)
    {
        faces[card].gameObject.SetActive(faceUp);
        backs[card].gameObject.SetActive(!faceUp);
    }

    void Tap(int card)
    {
        // 멈춤 · 다시 세는 중(timeScale 0), 끝난 프레임에 늦게 들어온 누름은 버린다
        if (state != State.Playing || Time.timeScale == 0f || clock >= rules.durationSec)
        {
            return;
        }
        // 미리 보기 · 오답을 보여 주는 중 · 이미 뒤집힌 카드
        if (phase != Phase.Pick || matched[card] || card == first)
        {
            return;
        }
        // 연타 간격. 서버의 점수 상한이 이 간격으로 계산된다
        if (clock - lastTapAt < rules.minTapMs / 1000f)
        {
            return;
        }
        lastTapAt = clock;
        Show(card, true);
        popAge[card] = 0f;

        if (first < 0)
        {
            first = card;
            SoundManager.Instance.PlaySfx(SoundManager.Instance.pop);
            return;
        }

        int other = first;
        first = -1;
        if (deck[other] != deck[card])
        {
            SoundManager.Instance.PlaySfx(SoundManager.Instance.wrong);
            streak = 0;
            streakText.gameObject.SetActive(false);
            missA = other;
            missB = card;
            phase = Phase.Miss;
            phaseUntil = clock + rules.missMs / 1000f;
            return;
        }

        SoundManager.Instance.PlaySfx(SoundManager.Instance.sparkle);
        matched[other] = true;
        matched[card] = true;
        cardGroups[other].alpha = matchedAlpha;
        cardGroups[card].alpha = matchedAlpha;
        score += PointsFor(streak, rules);
        streak++;
        left--;
        if (left == 0)
        {
            score += rules.clearBonus;
            board++;
            phase = Phase.Clear;
            phaseUntil = clock + clearTime;
        }
        scoreText.SetText("{0}점", (float)score);
        streakText.gameObject.SetActive(true);
        streakText.SetText("연속 {0}", (float)streak);
    }

    // 맞추기 전 연속 수로 이번 짝 점수. 10 · 15 · 20 · 25 · 30(최대). 서버 pairsMaxScore 와 같은 식
    public static int PointsFor(int streak, GameRules rules)
    {
        return Mathf.Min(rules.streakMax, rules.pairScore + streak * rules.streakStep);
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

        Animate(dt);
        if (remain <= 0f)
        {
            Finish();
            return;
        }

        if (phase == Phase.Pick || clock < phaseUntil)
        {
            return;
        }
        if (phase == Phase.Preview)
        {
            for (int i = 0; i < cardButtons.Length; i++)
            {
                Show(i, false);
            }
            phase = Phase.Pick;
        }
        else if (phase == Phase.Miss)
        {
            Show(missA, false);
            Show(missB, false);
            phase = Phase.Pick;
        }
        else
        {
            Deal();
        }
    }

    void Animate(float dt)
    {
        for (int i = 0; i < popAge.Length; i++)
        {
            if (popAge[i] < 0f)
            {
                continue;
            }
            popAge[i] += dt;
            float s = 1f;
            if (popAge[i] >= popTime)
            {
                popAge[i] = -1f;
            }
            else
            {
                s = Mathf.Lerp(1.1f, 1f, popAge[i] / popTime);
            }
            cardRects[i].localScale = new Vector3(s, s, 1f);
        }
    }

    void Finish()
    {
        state = State.Done;
        Animate(10f);   // 커지던 카드를 끝 모습으로
        countDownGroup.SetActive(true);
        countDownText.SetText("그만!");
        onEnd?.Invoke(score);
    }
}
