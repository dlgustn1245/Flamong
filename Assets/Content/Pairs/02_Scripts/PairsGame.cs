using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 짝꿍 찾기 한 판. 루트의 빈 오브젝트 PairsGame 에 붙는다(멈출 때 꺼지는 Group - Field 아래에 두면 다시 세기가 돌지 않는다).
// 세기 · 판 시계 · 남은 시간 · 점수 · 「그만!」은 부모 MiniGame 이 맡는다
public class PairsGame : MiniGame
{
    public override string GameId => GameList.Pairs;

    public Button[] cardButtons;           // 12장. 왼쪽 위부터 가로로
    public CanvasGroup[] cardGroups;       // 맞춘 카드를 흐리게
    public Image[] backs;                  // 뒷면(알)
    public Image[] faces;                  // 앞면(아기 플라몽)
    public CharacterSprites characterSprites;   // 앞면은 Lv1 그림. 종 번호는 알 하나에 3종씩 이어진다

    public TextMeshProUGUI streakText;

    // 보이는 것만 여기서 정한다. 판 규칙(시간 · 점수 · 난이도)은 서버 rules
    public float matchedAlpha = 0.35f;  // 맞춘 카드
    public float popTime = 0.12f;       // 뒤집을 때 1.1 → 1
    public float clearTime = 0.3f;      // 다 맞춘 판을 잠깐 보여 준 뒤 새 판

    const int speciesPerEgg = 3;

    enum Phase { Preview, Pick, Miss, Clear }

    Phase phase;
    int streak;          // 안 틀리고 이어 맞춘 수. 판을 넘어 이어진다
    int board;           // 판 번호(0부터)
    int left;            // 이 판에 남은 짝
    int first;           // 먼저 뒤집은 카드. 없으면 -1
    int missA;
    int missB;
    float phaseUntil;

    int[] deck;          // [카드] = speciesId
    bool[] matched;
    int[] eggOrder;
    RectTransform[] cardRects;
    float[] popAge;      // 음수면 꺼짐

    protected override void Awake()
    {
        base.Awake();
        int n = cardButtons.Length;
        deck = new int[n];
        matched = new bool[n];
        popAge = new float[n];
        cardRects = new RectTransform[n];
        eggOrder = new int[characterSprites.SpeciesCount / speciesPerEgg];
        for (int i = 0; i < n; i++)
        {
            int card = i;
            cardRects[i] = (RectTransform)cardButtons[i].transform;
            cardButtons[i].onClick.AddListener(() => Tap(card));
        }
    }

    protected override void OnDestroy()
    {
        for (int i = 0; i < cardButtons.Length; i++)
        {
            cardButtons[i].onClick.RemoveAllListeners();
        }
        base.OnDestroy();
    }

    // 세는 동안은 모두 뒷면
    protected override void OnReset()
    {
        streak = 0;
        board = 0;
        first = -1;
        phase = Phase.Preview;
        for (int i = 0; i < cardButtons.Length; i++)
        {
            matched[i] = false;
            cardGroups[i].alpha = 1f;
            popAge[i] = -1f;
            cardRects[i].localScale = Vector3.one;
            Show(i, false);
        }
        streakText.gameObject.SetActive(false);
    }

    // 다 세면 첫 판을 깔고 미리 보기부터 판 시계가 흐른다. 「시작!」 글자는 미리 보기 카드를 가려 띄우지 않는다
    protected override void OnPlay()
    {
        Deal();
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
            faces[i].sprite = characterSprites.Get(deck[i], 1);
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
        // 미리 보기 · 오답을 보여 주는 중 · 이미 뒤집힌 카드
        if (phase != Phase.Pick || matched[card] || card == first)
        {
            return;
        }
        if (!AcceptTap())
        {
            return;
        }
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
        int points = PointsFor(streak, rules);
        streak++;
        left--;
        if (left == 0)
        {
            points += rules.clearBonus;
            board++;
            phase = Phase.Clear;
            phaseUntil = clock + clearTime;
        }
        AddScore(points);
        streakText.gameObject.SetActive(true);
        streakText.SetText("연속 {0}", (float)streak);
    }

    // 맞추기 전 연속 수로 이번 짝 점수. 10 · 15 · 20 · 25 · 30(최대). 서버 pairsMaxScore 와 같은 식
    public static int PointsFor(int streak, ServerData.GameRules rules)
    {
        return Mathf.Min(rules.streakMax, rules.pairScore + streak * rules.streakStep);
    }

    protected override void OnTick(float dt)
    {
        Animate(dt);
        // 시간이 다 된 프레임에는 판을 더 진행하지 않는다(새 판을 깔거나 카드를 뒤집지 않는다)
        if (clock >= rules.durationSec || phase == Phase.Pick || clock < phaseUntil)
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

    // 커지던 카드를 끝 모습으로
    protected override void OnFinish()
    {
        Animate(10f);
    }
}
