using System.Collections;
using System.Collections.Generic;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainScene : MonoBehaviour
{
    public Button heartButton;
    public Image heartFill;
    public TextMeshProUGUI touchCountText;
    public TextMeshProUGUI heartHintText;

    public Image gaugeFill;
    public TextMeshProUGUI loveLevelText;

    public HeartParticles heartParticles;

    public Image characterImage;
    public Sprite[] characterSprites;
    
    public ErrorPanel errorPanel;
    public ConfirmPanel confirmPanel;

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
    
    public Slider bgmSlider;
    public Slider sfxSlider;
    
    int heartTaps;
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
        
        bgmSlider.onValueChanged.AddListener(SoundManager.Instance.SetBgmVolume);
        sfxSlider.onValueChanged.AddListener(SoundManager.Instance.SetSfxVolume);
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
        StartCoroutine(NetworkManager.Instance.PostRequest<EmptyBody, OkResponse>(
            $"{NetworkManager.BASE_URL}/characters/reset",
            new EmptyBody(),
            OnSuccessResetCharacter,
            OnFailResetCharacter,
            header));
    }

    void OnSuccessResetCharacter(OkResponse response)
    {
        if (response.statusCode == 200 && response.body.ok)
        {
            print("Reset Character Success");
            SceneLoader.Load(SceneName.EggSelectScene);
        }
        else
        {
            print("Reset Character Failed.");
            print($"status code: {response.statusCode}");
            print($"status ok: {response.body.ok}");
            errorPanel.Show(ResetCharacter);
        }
    }

    void OnFailResetCharacter(long code, string error)
    {
        print("Reset Character Fail");
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(ResetCharacter);
    }

    void Init()
    {
        StartCoroutine(NetworkManager.Instance.GetRequest<MeResponse>(
            $"{NetworkManager.BASE_URL}/me", OnSuccessGetMe, OnFailGetMe, header));
    }

    void OnSuccessGetMe(MeResponse response)
    {
        if (response.statusCode == 200)
        {
            ShowCharacter(response.body.speciesId);
            SetPlayerInfo(response.body);
            Apply(response.body.growth);
        }
    }

    void SetPlayerInfo(MeBody body)
    {
        SessionState.Instance.team = body.team;
        SessionState.Instance.playerName = body.name;
        SessionState.Instance.nickName = body.nickname;
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
    
    void ShowCharacter(int speciesId)
    {
        if (speciesId >= 0 && speciesId < characterSprites.Length)
        {
            characterImage.sprite = characterSprites[speciesId];
        }
        characterImage.enabled = true;
    }

    void OnFailGetMe(long code, string error)
    {
        print($"Get Me Failed: {error}");
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
        StartCoroutine(NetworkManager.Instance.PatchRequest<PatchPlayerRequestBody, OkResponse>
        (
            $"{NetworkManager.BASE_URL}/players/me", 
            body, 
            OnSuccessPatchPlayer, 
            OnFailPatchPlayer,
            header
            ));
    }

    void OnSuccessPatchPlayer(OkResponse response)
    {
        if (response.body.ok)
        {
            print("Patch Player Success");
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
        else
        {
            print("Patch Player Failed: ok->false");
            errorPanel.Show(PatchPlayer);
        }
    }

    void OnFailPatchPlayer(long code, string error)
    {
        print($"Patch Player Failed: {error}");
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
        yield return NetworkManager.Instance.PostRequest<EmptyBody, TapResponse>(
            $"{NetworkManager.BASE_URL}/tap", new EmptyBody(), OnSuccessTap, OnFailTap, header);
        isSending = false;
    }

    void OnSuccessTap(TapResponse response)
    {
        heartTaps = 0;
        PlayerPrefs.SetInt(SessionState.HEART_TAPS_KEY, 0);
        Apply(response.body.growth);
        heartParticles.Emit(5);
        if (response.body.levelUp || response.body.growth.completed)
        {
            heartParticles.Emit(10);
        }
    }

    void OnFailTap(long code, string error)
    {
        print($"Tap Failed: {error}");
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
