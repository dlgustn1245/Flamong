using System.Collections.Generic;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PotionScene : MonoBehaviour
{
    public ErrorPanel errorPanel;
    public NoticePanel noticePanel;

    public Button backButton;
    
    public Image potionBottle;
    public TextMeshProUGUI visitCountText;
    public Button claimButton;
    
    public Image[] hiddenIcons;
    public TextMeshProUGUI[] hiddenTitles;
    public TextMeshProUGUI[] hiddenHints;
    public Sprite potionIcon;
    public Button[] hiddenClaimButtons;
    public TextMeshProUGUI[] hiddenClaimButtonTexts;

    public string[] unlockTitle;
    public string[] unlockDescription;
    public string visitMissionId;
    public string[] hiddenMissionIds;
    
    Dictionary<string, string> header;

    void Start()
    {
        BindButtonEvents();
        
        header = new Dictionary<string, string> { { "X-Player-Id", SessionState.Instance.playerId } };
        GetMissions();
    }

    void BindButtonEvents()
    {
        backButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.MainScene);
        });
        claimButton.onClick.AddListener(() =>
        {
            claimButton.interactable = false;
            ClaimPotion(visitMissionId);
        });
        for (int i = 0; i < hiddenClaimButtons.Length; i++)
        {
            int idx = i;
            
            hiddenClaimButtons[i].onClick.AddListener(() =>
            {
                hiddenClaimButtons[idx].interactable = false;
                ClaimPotion(hiddenMissionIds[idx]);
            });
        }
    }

    void OnDestroy()
    {
        backButton.onClick.RemoveAllListeners();
        claimButton.onClick.RemoveAllListeners();
        for (int i = 0; i < hiddenClaimButtons.Length; i++)
        {
            hiddenClaimButtons[i].onClick.RemoveAllListeners();
        }
        StopAllCoroutines();
    }

    void ClaimPotion(string missionId)
    {
        var request = new ClaimRequestBody
        {
            missionId = missionId
        };
        
        StartCoroutine(NetworkManager.Instance.PostRequest<ClaimRequestBody, TapBody>(
            $"{NetworkManager.BASE_URL}/potions/claim", 
            request,
            OnSuccessClaimPotion,
            (code, error) =>
            {
                OnFailClaimPotion(code, error, missionId);
            },
            header));
    }

    void OnSuccessClaimPotion(TapBody response)
    {
        noticePanel.Show(response.levelUp
            ? $"묘약을 마셨어요!\n레벨이 올랐어요 (Lv {response.growth.level})"
            : "묘약을 마셨어요!\n애정도가 10 올랐어요");
        GetMissions();
    }

    void OnFailClaimPotion(long code, string error, string missionId)
    {
        print($"Post Potions Failed: {code} {error}");
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        if (code == 409)
        {
            GetMissions();
            return;
        }
        
        errorPanel.Show(() =>
        {
            ClaimPotion(missionId);
        });
    }

    void GetMissions()
    {
        StartCoroutine(NetworkManager.Instance.GetRequest<MissionsBody>(
            $"{NetworkManager.BASE_URL}/missions", 
            OnSuccessGetMissions, 
            OnFailedGetMissions, 
            header));   
    }

    void OnSuccessGetMissions(MissionsBody response)
    {
        potionBottle.fillAmount = response.visit.progress / (float)response.visit.need;
        claimButton.interactable = response.canDrink && 
                                   response.visit.progress >=  response.visit.need;
        visitCountText.text = $"{response.visit.progress} / {response.visit.need}";
        SetHiddenMissions(response.hidden, response.canDrink);
    }

    void SetHiddenMissions(HiddenMission[] hiddenMissions, bool canDrink)
    {
        for (int i = 0; i < hiddenMissions.Length; i++)
        {
            if (hiddenMissions[i].done && !hiddenMissions[i].claimed)
            {
                if (canDrink)
                {
                    hiddenIcons[i].sprite = potionIcon;
                    hiddenTitles[i].text = unlockTitle[i];
                    hiddenHints[i].text = unlockDescription[i];
                    hiddenClaimButtons[i].gameObject.SetActive(true);
                    hiddenClaimButtons[i].interactable = true;
                }
                else
                {
                    hiddenIcons[i].sprite = potionIcon;
                    hiddenTitles[i].text = unlockTitle[i];
                    hiddenHints[i].text = "새로 키울 때\n마실 수 있어요";
                    hiddenClaimButtons[i].gameObject.SetActive(true);
                    hiddenClaimButtons[i].interactable = false;
                }
            }

            if (hiddenMissions[i].claimed)
            {
                hiddenIcons[i].sprite = potionIcon;
                hiddenTitles[i].text = unlockTitle[i];
                hiddenHints[i].text = unlockDescription[i];
                hiddenClaimButtons[i].gameObject.SetActive(true);
                hiddenClaimButtons[i].interactable = false;
                hiddenClaimButtonTexts[i].text = "받음";
            }
        }
    }

    void OnFailedGetMissions(long code, string error)
    {
        print($"Get Missions Failed: {code} {error}");
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(GetMissions);
    }
}
