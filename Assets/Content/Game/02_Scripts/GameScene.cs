using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameScene : MonoBehaviour
{
    public Button backButton;
    
    public ErrorPanel errorPanel;
    public RankingPanel rankingPanel;

    public Button[] monthButtons;
    public CanvasGroup[] monthGroups;
    public TextMeshProUGUI[] gameTexts;
    public TextMeshProUGUI[] recordTexts;
    public GameObject[] nowMarks;
    
    int nowMonth = -1;
    MonthItem[] months;

    void Start()
    {
        GetMonths();
        
        backButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.MainScene);
        });
        
        for (int i = 0; i < monthButtons.Length; i++)
        {
            int idx = i;
            monthButtons[i].onClick.AddListener(() =>
            {
                OnClickCalendar(idx);
            });
        }
    }

    void OnDestroy()
    {
        backButton.onClick.RemoveAllListeners();
        for (int i = 0; i < monthButtons.Length; i++)
        {
            monthButtons[i].onClick.RemoveAllListeners();
        }
        StopAllCoroutines();
    }

    void GetMonths()
    {
        StartCoroutine(NetworkManager.Instance.GetRequest<MonthsBody>(
            $"{NetworkManager.BASE_URL}/games/months", 
            OnSuccessGetMonths, OnFailGetMonths));
    }

    void OnSuccessGetMonths(MonthsBody response)
    {
        print($"OnSuccessGetMonths: {JsonUtility.ToJson(response)}");
        
        months = response.months;
        var currMonth = int.Parse(response.current.Substring(5)) - 1;

        for (int i = 0; i < monthButtons.Length; i++)
        {
            var month = months[i];
            if (i < currMonth)
            {
                if (!string.IsNullOrEmpty(month.game))
                {
                    monthButtons[i].interactable = true;
                    gameTexts[i].text = GameList.NameOf(month.game);
                    if (month.rank == 0)
                    {
                        recordTexts[i].SetText("기록 없음");
                    }
                    else
                    {
                        recordTexts[i].SetText($"{month.rank}등 · {month.best}점");
                    }
                }
                else
                {
                    monthGroups[i].alpha = 0.45f;   // 출시 전
                }
            }

            else if (i == currMonth)
            {
                nowMarks[i].SetActive(true);
                monthButtons[i].interactable = GameList.SceneOf(month.game) != null;
                gameTexts[i].SetText(GameList.NameOf(month.game));
                nowMonth = currMonth;
                if (month.rank > 0)
                {
                    recordTexts[i].SetText($"도전 중 · 최고 {month.best}점");
                }
                else
                {
                    recordTexts[i].SetText("도전 중 · 기록 없음");
                }
            }
            else
            {
                monthGroups[i].alpha = 0.45f;
                gameTexts[i].SetText("?");
            }
        }
    }

    void OnFailGetMonths(long code, string error)
    {
        print($"OnFailGetMonths: {code} {error}");
        errorPanel.Show(GetMonths);
    }

    void OnClickCalendar(int idx)
    {
        if (idx == nowMonth)
        {
            SceneLoader.Load(GameList.SceneOf(months[idx].game));
            return;
        }

        StartCoroutine(NetworkManager.Instance.GetRequest<Board>(
            $"{NetworkManager.BASE_URL}/rankings?month={months[idx].month}", 
            OnSuccessGetBoard, 
            (code, error) =>
            {
                OnFailGetBoard(code, error, idx);
            }, 5));
    }

    void OnSuccessGetBoard(Board response)
    {
        print($"OnSuccessGetRanking: {JsonUtility.ToJson(response)}");
        rankingPanel.ShowRankingBoard(response);
    }

    void OnFailGetBoard(long code, string error, int idx)
    {
        print($"OnFailGetRanking: {code} {error}");
        errorPanel.Show(() =>
        {
            OnClickCalendar(idx);
        });
    }
}
