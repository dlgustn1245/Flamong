using System.Collections.Generic;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CollectionScene : MonoBehaviour
{
    public Sprite[] characterSprites;  // speciesId * 5 + level - 1 (Main 과 같은 순서)
    public Button backButton;
    public TextMeshProUGUI countText;
    public TextMeshProUGUI eggNameText;
    
    public Button[] eggTabs;           // eggId 순서 (핑크 · 퍼플 · 블루 · 그린 · 그레이 · 오렌지 · 라벤더 · 크림)
    public RectTransform tabGlow;      // 고른 탭 뒤의 빛. x 만 탭 위치로 옮긴다
    public Image[] cells;              // 줄 r(계열 안 몇 번째 종) · 칸 c(Lv) → cells[r * 5 + c]

    public ErrorPanel errorPanel;
    
    Dictionary<string, string> header;
    string[] eggNames = {"핑크 알", "퍼플 알", "블루 알", "그린 알", "그레이 알", "오렌지 알", "라벤더 알", "크림 알"};
    int[] completed;
    int[] levels;
    int speciesId;

    void Start()
    {
        header = new Dictionary<string, string> { { "X-Player-Id", SessionState.Instance.playerId } };
        GetCollection();
        
        backButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.MainScene);
        });

        for (int i = 0; i < eggTabs.Length; i++)
        {
            int idx = i;
            eggTabs[i].onClick.AddListener(() =>
            {
                OnClickEggTab(idx);
            });
            
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < eggTabs.Length; i++)
        {
            eggTabs[i].onClick.RemoveAllListeners();
        }
        backButton.onClick.RemoveAllListeners();
        StopAllCoroutines();
    }

    void SetCharacterSprites(int idx)
    {
        if (levels == null)
        {
            return;
        }
        
        for (int r = 0; r < 3; r++)
        {
            int species = idx * 3 + r;
            for (int c = 0; c < 5; c++)
            {
                cells[r * 5 + c].sprite = characterSprites[species * 5 + c];
                cells[r * 5 + c].color = levels[species] >= c + 1 ? Color.white : Color.black;
            }
        }
    }

    void OnClickEggTab(int idx)
    {
        tabGlow.anchoredPosition = eggTabs[idx].GetComponent<RectTransform>().anchoredPosition;
        eggNameText.SetText(eggNames[idx]);
        SetCharacterSprites(idx);
    }

    void GetCollection()
    {
        StartCoroutine(NetworkManager.Instance.GetRequest<CollectionResponse>(
            $"{NetworkManager.BASE_URL}/collection", OnSuccessGetCollection, OnFailGetCollection, header));
    }

    void OnSuccessGetCollection(CollectionResponse response)
    {
        print("Get Collection Success");
        completed = response.body.completed;
        speciesId = response.body.speciesId;
        levels = response.body.levels;
        OnClickEggTab(speciesId >= 0 ? speciesId / 3 : 0);
        countText.SetText($"{completed.Length} / 24");
    }

    void OnFailGetCollection(long code, string error)
    {
        print($"Get Collection Failed: {error}");
        if (code == 401)   // 서버가 모르는 계정
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(GetCollection);
    }
}
