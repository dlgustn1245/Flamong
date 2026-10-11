using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainScene : MonoBehaviour
{
    public Button heartButton;
    public Image heartFill;
    public TextMeshProUGUI touchCountText;

    public Image gaugeFill;
    public TextMeshProUGUI loveLevelText;

    public HeartParticles heartParticles;

    int heartTaps;
    
    // ── 임시: 서버 GET /me · POST /tap 이 생기면 여기부터 아래 Fake 코드 전부 삭제 ──
    // 각 레벨이 시작되는 누적 애정도. 마지막 값은 완성 (Lv5 게이지 끝)
    static readonly int[] LEVEL_AT = { 0, 5, 30, 65, 115, 175 };
    const int FAKE_HEARTS_MAX = 15;

    int fakeHearts = 5;     // 가입 직후 하트 5개
    int fakeAffection = 0;      // 누적 애정도

    void Start()
    {
        Init();
        heartButton.onClick.AddListener(OnClickHeartButton);
    }

    void OnClickHeartButton()
    {
        heartFill.fillAmount = ++heartTaps / 10f;
        heartParticles.Emit(1);
        if (heartTaps >= 10)
        {
            heartTaps = 0;
            var tap = FakeTap();
            Apply(tap.growth);
            heartParticles.Emit(5);
            if (tap.levelUp || tap.growth.completed)
            {
                heartParticles.Emit(10);
            }
        }
    }

    void Init()
    {
        Apply(FakeMe());
    }

    void Apply(Growth growth)
    {
        touchCountText.SetText("남은 하트: {0}", growth.hearts);
        loveLevelText.SetText("Lv {0}", growth.level);
        gaugeFill.fillAmount = (float)growth.levelProgress / growth.levelNeed;
        heartButton.interactable = growth.hearts > 0 && !growth.completed;
        if (!heartButton.interactable)
        {
            heartFill.fillAmount = 0;
        }
    }

    Growth FakeMe()
    {
        int level = 1;
        while (level < LEVEL_AT.Length - 1 && fakeAffection >= LEVEL_AT[level])
        {
            level++;
        }

        int progress = fakeAffection - LEVEL_AT[level - 1];
        int need = LEVEL_AT[level] - LEVEL_AT[level - 1];

        return new Growth
        {
            hearts = fakeHearts,
            heartsMax = FAKE_HEARTS_MAX,
            affection = fakeAffection,
            level = level,
            levelProgress = progress,
            levelNeed = need,
            completed = fakeAffection >= LEVEL_AT[LEVEL_AT.Length - 1],
        };
    }

    TapBody FakeTap()
    {
        var now = FakeMe();

        if (now.hearts <= 0 || now.completed)
        {
            return new TapBody { levelUp = false, growth = now };
        }
        
        --fakeHearts;
        fakeAffection = Mathf.Min(fakeAffection + 1, LEVEL_AT[LEVEL_AT.Length - 1]);
        
        var after = FakeMe();
        return new TapBody { levelUp = after.level > now.level, growth = after };
    }

    void OnDestroy()
    {
        heartButton.onClick.RemoveAllListeners();
    }
}
