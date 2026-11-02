using System.Collections;
using System.Collections.Generic;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainScene : MonoBehaviour
{
    readonly string[] emotions = { "tired", "stress", "achievement", "focus", "calm" };
    
    public Button heartButton;
    public Image heartFill;
    public TextMeshProUGUI touchCountText;
    public TextMeshProUGUI heartHintText;

    public Image gaugeFill;
    public float gaugeFillTime = 0.5f;   // 애정도가 오를 때 게이지가 차오르는 시간
    public TextMeshProUGUI loveLevelText;

    public HeartParticles heartParticles;

    public Image characterImage;
    public Sprite[] characterSprites;
    
    public ErrorPanel errorPanel;
    public ConfirmPanel confirmPanel;
    public NoticePanel noticePanel;

    public Button settingButton;
    public Button settingCloseButton;
    public GameObject settingPanel;

    public TMP_InputField currTeam;
    public TMP_InputField currName;
    public TextMeshProUGUI currNickName;

    public Button patchPlayerInfo;
    public Button resetPlayer;
    public Button logOut;
    public Button contact;
    public Button withdraw;
    
    public GameObject contactPanel;
    public Button contactCloseButton;
    
    public Slider bgmSlider;
    public Slider sfxSlider;
    
    public Button newsButton;
    public GameObject newsButtonDot;
    public Button gardenButton;
    public Button collectionButton;
    public Button potionButton;
    public GameObject potionDot; 
    public Button gameButton;
    
    public GameObject emotionPanel;
    public Button[] emotionButtons; 
    
    int heartTaps;
    int speciesId;
    Coroutine gaugeRoutine;
    Dictionary<string, string> header;
    bool isSending;
    bool isCompleted;

    void Start()
    {
        header = new Dictionary<string, string> { { "X-Player-Id", SessionState.Instance.playerId } };
        heartButton.interactable = false;
        characterImage.enabled = false;   // /me 응답 전에 씬의 기본 그림이 잠깐 보이지 않게
        heartTaps = PlayerPrefs.GetInt(SessionState.HEART_TAPS_KEY);
        Init();
        BindButtonEvents();
    }

    void BindButtonEvents()
    {
        heartButton.onClick.AddListener(OnClickHeartButton);
        settingButton.onClick.AddListener(OpenSetting);
        settingCloseButton.onClick.AddListener(() =>
        {
            settingPanel.SetActive(false);
        });
        patchPlayerInfo.onClick.AddListener(() =>
        {
            if (string.IsNullOrEmpty(currTeam.text) && string.IsNullOrEmpty(currName.text))
            {
                return;
            }
            confirmPanel.Show("정보를 수정할까요?", PatchPlayer);
        });
        logOut.onClick.AddListener(() =>
        {
            confirmPanel.Show("로그아웃할까요?", LogOut);
        });
        resetPlayer.onClick.AddListener(() =>
        {
            confirmPanel.Show(isCompleted
                    ? "캐릭터를 새로 키우시겠어요?"
                    : "아직 다 자라지 않았어요.\n지금 새로 키우면 도감에 남지 않아요.\n새로 키우시겠어요?",
                ResetCharacter);
        });
        withdraw.onClick.AddListener(() =>
        {
            confirmPanel.Show(
                "탈퇴하면 캐릭터와 기록이 모두 사라지고\n되돌릴 수 없어요.\n탈퇴할까요?",
                WithdrawCharacter);
        });
        
        newsButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.NewsScene);
        });
        gardenButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.GardenScene);
        });
        collectionButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.CollectionScene);
        });
        potionButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.PotionScene);
        });
        gameButton.onClick.AddListener(ShowComingSoon);
        
        for (int i = 0; i < emotionButtons.Length; i++)
        {
            int idx = i;
            emotionButtons[idx].onClick.AddListener(() =>
            {
                PostEmotion(emotions[idx]);
            });
        }
        
        contact.onClick.AddListener(() =>
        {
            contactPanel.SetActive(true);
        });
        contactCloseButton.onClick.AddListener(() =>
        {
            contactPanel.SetActive(false);
        });
        
        bgmSlider.onValueChanged.AddListener(SoundManager.Instance.SetBgmVolume);
        sfxSlider.onValueChanged.AddListener(SoundManager.Instance.SetSfxVolume);
    }

    void ShowComingSoon()
    {
        noticePanel.Show("준비 중이에요");
    }

    void PostEmotion(string emotion)
    {
        foreach (var emotionButton in emotionButtons)
        {
            emotionButton.interactable = false;
        }

        var request = new EmotionRequestBody
        {
            emotion = emotion
        };

        StartCoroutine(NetworkManager.Instance.PostRequest<EmotionRequestBody, TapBody>(
            $"{NetworkManager.BASE_URL}/emotions", 
            request, OnSuccessPostEmotion, (code, error) =>
            {
                OnFailPostEmotion(code, error, emotion);
            }, header));
    }

    void OnSuccessPostEmotion(TapBody response)
    {
        print($"Success Post Emotion: {JsonUtility.ToJson(response)}");
        foreach (var emotionButton in emotionButtons)
        {
            emotionButton.interactable = true;
        }
        emotionPanel.SetActive(false);
        ApplyGrowth(response);
    }

    void OnFailPostEmotion(long code, string response, string emotion)
    {
        print($"Fail Post Emotion: {code}, {response}");
        if (code == 409)
        {
            foreach (var emotionButton in emotionButtons)
            {
                emotionButton.interactable = true;
            }
            emotionPanel.SetActive(false);
            noticePanel.Show("오늘은 이미 골랐어요.");
            return;
        }

        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }

        errorPanel.Show(() =>
        {
            PostEmotion(emotion);
        });
    }
    
    void OnDestroy()
    {
        PlayerPrefs.Save();
        heartButton.onClick.RemoveAllListeners();
        settingButton.onClick.RemoveAllListeners();
        settingCloseButton.onClick.RemoveAllListeners();
        patchPlayerInfo.onClick.RemoveAllListeners();
        logOut.onClick.RemoveAllListeners();
        resetPlayer.onClick.RemoveAllListeners();
        contact.onClick.RemoveAllListeners();
        bgmSlider.onValueChanged.RemoveAllListeners();
        sfxSlider.onValueChanged.RemoveAllListeners();
        newsButton.onClick.RemoveAllListeners();
        gardenButton.onClick.RemoveAllListeners();
        potionButton.onClick.RemoveAllListeners();
        collectionButton.onClick.RemoveAllListeners();
        gameButton.onClick.RemoveAllListeners();
        withdraw.onClick.RemoveAllListeners();
        foreach (var emotionButton in emotionButtons)
        {
            emotionButton.onClick.RemoveAllListeners();
        }
        contactCloseButton.onClick.RemoveAllListeners();
        StopAllCoroutines();
    }

    void LogOut()
    {
        SessionState.Instance.playerId =
            SessionState.Instance.team =
                SessionState.Instance.playerName =
                    SessionState.Instance.nickName = string.Empty;
        PlayerPrefs.DeleteKey(SessionState.HEART_TAPS_KEY);
        SessionState.Instance.SavePlayerId();
        SceneLoader.Load(SceneName.TitleScene);
    }

    void ResetCharacter()
    {
        StartCoroutine(NetworkManager.Instance.PostRequest<EmptyBody, OkBody>(
            $"{NetworkManager.BASE_URL}/characters/reset",
            new EmptyBody(),
            OnSuccessResetCharacter,
            OnFailResetCharacter,
            header));
    }

    void WithdrawCharacter()
    {
        StartCoroutine(NetworkManager.Instance.DeleteRequest<OkBody>(
            $"{NetworkManager.BASE_URL}/players/me", 
            OnSuccessDeleteMe, 
            OnFailDeleteMe, 
            header));
    }

    void OnSuccessDeleteMe(OkBody response)
    {
        print($"Delete Me Success: {JsonUtility.ToJson(response)}");
        LogOut();
    }

    void OnFailDeleteMe(long code, string error)
    {
        print($"Delete Me Failed: {code} {error}");
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(WithdrawCharacter);
    }

    // 실패(2xx 아님 · 응답을 못 읽음)는 NetworkManager 가 OnFail 로 보낸다
    void OnSuccessResetCharacter(OkBody response)
    {
        print($"Reset Character Success: {JsonUtility.ToJson(response)}");
        SceneLoader.Load(SceneName.EggSelectScene);
    }

    void OnFailResetCharacter(long code, string error)
    {
        print($"Reset Character Failed: {code} {error}");
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(ResetCharacter);
    }

    void Init()
    {
        StartCoroutine(NetworkManager.Instance.GetRequest<MeBody>(
            $"{NetworkManager.BASE_URL}/me", OnSuccessGetMe, OnFailGetMe, header));
    }

    void OnSuccessGetMe(MeBody response)
    {
        print($"Get Me Success: {JsonUtility.ToJson(response)}");
        speciesId = response.speciesId;
        ShowCharacter(speciesId, response.growth.level);
        Apply(response.growth);
        potionDot.SetActive(response.potionReady);
        newsButtonDot.SetActive(response.newsReady);
        if (!response.emotionToday)
        {
            emotionPanel.SetActive(true);
        }
    }

    void OpenSetting()
    {
        var session = SessionState.Instance;
        currNickName.text = session.nickName;
        ((TMP_Text)currName.placeholder).SetText(session.playerName);
        ((TMP_Text)currTeam.placeholder).SetText(session.team);
        currTeam.text = string.Empty;
        currName.text = string.Empty;
        bgmSlider.SetValueWithoutNotify(SoundManager.Instance.BgmVolume);
        sfxSlider.SetValueWithoutNotify(SoundManager.Instance.SfxVolume);
        settingPanel.SetActive(true);
    }
    
    void ShowCharacter(int speciesId, int level)
    {
        int idx = speciesId * 5 + level - 1;
        if (idx >= 0 && idx < characterSprites.Length)
        {
            characterImage.sprite = characterSprites[idx];
        }
        characterImage.enabled = true;
    }

    void OnFailGetMe(long code, string error)
    {
        print($"Get Me Failed: {code} {error}");
        if (code == 401)   // 서버가 모르는 계정
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(Init);
    }
    
    void Apply(Growth growth)
    {
        touchCountText.SetText("남은 하트: {0}", growth.hearts);
        loveLevelText.SetText("Lv {0}", growth.level);
        SetHeartHintText(growth);
        isCompleted = growth.completed;
        gaugeFill.fillAmount = (float)growth.levelProgress / growth.levelNeed;
        heartButton.interactable = growth.hearts > 0 && !growth.completed;
        heartFill.fillAmount = heartButton.interactable ? heartTaps / 10f : 0f;
    }

    void SetHeartHintText(Growth growth)
    {
        if (growth.completed)
        {
            heartHintText.SetText("다 자랐어요");
        }
        else if (growth.hearts == 0)
        {
            heartHintText.SetText("내일 0시에 하트가 채워져요");
        }
        else
        {
            heartHintText.SetText("하트를 눌러주세요");
        }
    }

    void OnClickHeartButton()
    {
        if (isSending)
        {
            return;
        }
        
        heartFill.fillAmount = ++heartTaps / 10f;
        PlayerPrefs.SetInt(SessionState.HEART_TAPS_KEY, heartTaps);   // 메모리에만 적는다. 파일 쓰기는 화면을 떠날 때

        heartParticles.Emit(1);
        if (heartTaps >= 10)
        {
            StartCoroutine(PostTap());
        }
    }

    void PatchPlayer()
    {
        var body = new PatchPlayerRequestBody
        {
            team = string.IsNullOrEmpty(currTeam.text) ? 
                SessionState.Instance.team : currTeam.text,
            name = string.IsNullOrEmpty(currName.text) ? 
                SessionState.Instance.playerName : currName.text
        };
        StartCoroutine(NetworkManager.Instance.PatchRequest<PatchPlayerRequestBody, OkBody>
        (
            $"{NetworkManager.BASE_URL}/players/me", 
            body, 
            OnSuccessPatchPlayer, 
            OnFailPatchPlayer,
            header
            ));
    }

    void OnSuccessPatchPlayer(OkBody response)
    {
        print($"Patch Player Success: {JsonUtility.ToJson(response)}");
        if (!string.IsNullOrEmpty(currTeam.text))
        {
            SessionState.Instance.team = currTeam.text;
        }
        if (!string.IsNullOrEmpty(currName.text))
        {
            SessionState.Instance.playerName = currName.text;
        }
        OpenSetting();
    }

    void OnFailPatchPlayer(long code, string error)
    {
        print($"Patch Player Failed: {code} {error}");
        if (code == 401)   // 서버가 모르는 계정
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(PatchPlayer);
    }

    IEnumerator PostTap()
    {
        isSending = true;
        yield return NetworkManager.Instance.PostRequest<EmptyBody, TapBody>(
            $"{NetworkManager.BASE_URL}/tap", new EmptyBody(), OnSuccessTap, OnFailTap, header);
        isSending = false;
    }

    void OnSuccessTap(TapBody response)
    {
        print($"Tap Success: {JsonUtility.ToJson(response)}");
        heartTaps = 0;
        PlayerPrefs.SetInt(SessionState.HEART_TAPS_KEY, 0);
        heartParticles.Emit(5);
        ApplyGrowth(response);
    }

    // 응답의 성장 상태를 화면에 반영하고 레벨업 · 완성이면 연출한다. 하트 탭과 오늘의 감정이 같이 쓴다
    void ApplyGrowth(TapBody body)
    {
        // 완성 뒤에도 오늘의 감정 응답은 completed 가 계속 true 라, 완성 안내는 처음 완성됐을 때 한 번만
        bool justCompleted = body.growth.completed && !isCompleted;
        float gaugeFrom = gaugeFill.fillAmount;
        Apply(body.growth);
        // Apply 가 바로 맞춘 값을 이전 값에서부터 차오르게 다시 그린다
        if (gaugeRoutine != null)
        {
            StopCoroutine(gaugeRoutine);
        }
        gaugeRoutine = StartCoroutine(FillGauge(gaugeFrom, gaugeFill.fillAmount, body.levelUp));

        if (body.levelUp)
        {
            ShowCharacter(speciesId, body.growth.level);
            heartParticles.Emit(10);
            noticePanel.Show($"Lv {body.growth.level} 달성!");
        }

        // 도감은 서버가 완성된 캐릭터 기록(애정도 175)으로 만든다. 클라이언트가 따로 등록하지 않는다
        if (justCompleted)
        {
            heartParticles.Emit(10);
            noticePanel.Show("다 자랐어요!\n도감에 등록됐어요.");
        }
    }

    // 레벨업이면 끝까지 채운 뒤 0에서 새 레벨의 진행값까지 다시 차오른다
    IEnumerator FillGauge(float from, float to, bool levelUp)
    {
        if (levelUp)
        {
            yield return FillGaugeTo(from, 1f);
            from = 0f;
        }
        yield return FillGaugeTo(from, to);
        gaugeRoutine = null;
    }

    IEnumerator FillGaugeTo(float from, float to)
    {
        for (float t = 0f; t < gaugeFillTime; t += Time.deltaTime)
        {
            gaugeFill.fillAmount = Mathf.Lerp(from, to, t / gaugeFillTime);
            yield return null;
        }
        gaugeFill.fillAmount = to;
    }

    void OnFailTap(long code, string error)
    {
        print($"Tap Failed: {code} {error}");
        Init();
    }

    // 브라우저 탭을 닫으면 OnDestroy 가 불리지 않으므로 포커스를 잃을 때도 저장한다
    void OnApplicationFocus(bool focus)
    {
        if (!focus)
        {
            PlayerPrefs.Save();
        }
    }
}
