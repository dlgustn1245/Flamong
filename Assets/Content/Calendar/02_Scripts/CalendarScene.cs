using System;
using System.Collections.Generic;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CalendarScene : MonoBehaviour
{
    readonly string[] emotions = { "tired", "stress", "achievement", "focus", "calm" };
    readonly string[] emotionNames = { "피로", "스트레스", "성취감", "몰입", "평온" };
    readonly string[] weekNames = { "일요일", "월요일", "화요일", "수요일", "목요일", "금요일", "토요일" };
    
    public ErrorPanel errorPanel;
    public Button backButton;
    
    public TextMeshProUGUI monthText;
    public Button prevButton;
    public Button nextButton;

    public Button[] cells;
    public TextMeshProUGUI[] dayTexts;
    public Image[] cellIcons;
    
    public Image infoIcon;
    public TextMeshProUGUI infoDateText;
    public TextMeshProUGUI infoEmotionText;
    public GameObject infoHint;
    
    public Sprite[] emotionSprites;
    public Color todayColor = new Color32(0xFF, 0xFE, 0xB1, 0xFF);
    
    Dictionary<string, string> header;
    CalendarBody calendar;
    string month = string.Empty;

    void Start()
    {
        header = new Dictionary<string, string> { { "X-Player-Id", SessionState.Instance.playerId } };
        BindButtonEvents();
        GetCalendar();
    }

    void BindButtonEvents()
    {
        backButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.MainScene);
        });
        
        prevButton.onClick.AddListener(() =>
        {
            month = calendar.prev;
            GetCalendar();
        });

        nextButton.onClick.AddListener(() =>
        {
            month = calendar.next;
            GetCalendar();
        });

        for (int i = 0; i < cells.Length; i++)
        {
            int idx = i;
            cells[i].onClick.AddListener(() =>
            {
                OnClickCell(idx);
            });
        }
    }

    void OnDestroy()
    {
        backButton.onClick.RemoveAllListeners();
        prevButton.onClick.RemoveAllListeners();
        nextButton.onClick.RemoveAllListeners();
        foreach (var cell in cells)
        {
            cell.onClick.RemoveAllListeners();
        }
        StopAllCoroutines();
    }

    void GetCalendar()
    {
        string query = string.IsNullOrEmpty(month) ? string.Empty : $"?month={month}";
        StartCoroutine(NetworkManager.Instance.GetRequest<CalendarBody>(
            $"{NetworkManager.BASE_URL}/emotions{query}", 
            OnSuccessGetCalendar, OnFailGetCalendar, header));
    }

    void OnSuccessGetCalendar(CalendarBody response)
    {
        print($"Get Calendar Success: {JsonUtility.ToJson(response)}");

        calendar = response;
        monthText.text = $"{int.Parse(response.month.Substring(0, 4))}년 {int.Parse(response.month.Substring(5))}월";
        prevButton.interactable = !string.IsNullOrEmpty(response.prev);
        nextButton.interactable = !string.IsNullOrEmpty(response.next);

        for (int i = 0; i < cells.Length; i++)
        {
            int day = i - response.firstWeekday + 1;
            bool inMonth = day >= 1 && day <= response.emotions.Length;
            
            cells[i].gameObject.SetActive(inMonth);
            if (!inMonth)
            {
                continue;
            }
            
            dayTexts[i].text = day.ToString();
            int emotionIdx = Array.IndexOf(emotions, response.emotions[day - 1]);
            if (emotionIdx == -1)
            {
                cellIcons[i].gameObject.SetActive(false);
                cells[i].interactable = false;
            }
            else
            {
                cellIcons[i].gameObject.SetActive(true);
                cells[i].interactable = true;
                cellIcons[i].sprite = emotionSprites[emotionIdx];
            }
            
            cells[i].image.color = day == response.today ? todayColor : Color.white;
        }
        
        infoIcon.gameObject.SetActive(false);
        infoDateText.text = infoEmotionText.text = string.Empty;
        infoHint.gameObject.SetActive(true);
    }

    void OnFailGetCalendar(long code, string error)
    {
        print($"Get Calendar Failed: {code} {error}");
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(GetCalendar);
    }

    void OnClickCell(int idx)
    {
        int day = idx - calendar.firstWeekday + 1;
        int emotionIdx = Array.IndexOf(emotions, calendar.emotions[day - 1]);
        
        infoHint.SetActive(false);
        infoIcon.gameObject.SetActive(true);
        infoIcon.sprite = emotionSprites[emotionIdx];
        infoEmotionText.text = emotionNames[emotionIdx];
        infoDateText.text = $"{int.Parse(calendar.month.Substring(5))}월 {day}일 {weekNames[idx % 7]}";
    }
}
