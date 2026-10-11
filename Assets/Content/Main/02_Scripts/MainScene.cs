using System.Collections;
using System.Collections.Generic;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainScene : MonoBehaviour
{
    readonly string baseUrl = "http://localhost:3000";
    
    public Button heartButton;
    public Image heartFill;
    public TextMeshProUGUI touchCountText;

    public Image gaugeFill;
    public TextMeshProUGUI loveLevelText;

    public HeartParticles heartParticles;

    public Image characterImage;
    public Sprite[] characterSprites;
    
    public ErrorPanel errorPanel;

    int heartTaps;

    Dictionary<string, string> header;

    bool isSending;

    void Start()
    {
        header = new Dictionary<string, string> { { "X-Player-Id", SessionState.Instance.playerId } };
        heartButton.interactable = false;
        characterImage.enabled = false;   // /me 응답 전에 씬의 기본 그림이 잠깐 보이지 않게
        Init();
        heartButton.onClick.AddListener(OnClickHeartButton);
    }
    
    void Init()
    {
        StartCoroutine(NetworkManager.Instance.GetRequest<MeResponse>(
            $"{baseUrl}/me", OnSuccessGetMe, OnFailGetMe, header));
    }

    void OnSuccessGetMe(MeResponse response)
    {
        if (response.statusCode == 200)
        {
            ShowCharacter(response.body.speciesId);
            Apply(response.body.growth);
        }
    }

    // 서버 종 수가 늘었는데 그림이 아직 없으면 씬에 놓인 기본 그림을 그대로 쓴다
    void ShowCharacter(int speciesId)
    {
        if (speciesId >= 0 && speciesId < characterSprites.Length)
        {
            characterImage.sprite = characterSprites[speciesId];
        }
        characterImage.enabled = true;
    }

    void OnFailGetMe(string error)
    {
        print($"Get Me Failed: {error}");
        errorPanel.Show(Init);
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

    void OnClickHeartButton()
    {
        if (isSending)
        {
            return;
        }
        
        heartFill.fillAmount = ++heartTaps / 10f;
        heartParticles.Emit(1);
        if (heartTaps >= 10)
        {
            StartCoroutine(PostTap());
        }
    }

    IEnumerator PostTap()
    {
        isSending = true;
        yield return NetworkManager.Instance.PostRequest<EmptyBody, TapResponse>(
            $"{baseUrl}/tap", new EmptyBody(), OnSuccessTap, OnFailTap, header);
        isSending = false;
    }

    void OnSuccessTap(TapResponse response)
    {
        heartTaps = 0;
        Apply(response.body.growth);
        heartParticles.Emit(5);
        if (response.body.levelUp || response.body.growth.completed)
        {
            heartParticles.Emit(10);
        }
    }

    void OnFailTap(string error)
    {
        print($"Tap Failed: {error}");
        Init();
    }

    void OnDestroy()
    {
        heartButton.onClick.RemoveAllListeners();
    }
}
