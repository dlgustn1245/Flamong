using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RankingPanel : MonoBehaviour
{
    public Button closeButton;
    
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI gameText;
    public TextMeshProUGUI myRankText;
    public GameObject emptyText;
    
    public RectTransform[] ranksRows;
    public TextMeshProUGUI[] ranksTexts;
    public TextMeshProUGUI[] nickNameTexts;
    public TextMeshProUGUI[] bestTexts;

    public RectTransform myRow;

    void Start()
    {
        closeButton.onClick.AddListener(() =>
        {
            gameObject.SetActive(false);
        });
    }

    void OnDestroy()
    {
        closeButton.onClick.RemoveAllListeners();
    }

    public void ShowRankingBoard(Board board)
    {
        titleText.SetText($"{int.Parse(board.month.Substring(5))}월 랭킹");
        gameText.SetText(GameList.NameOf(board.game));

        var ranks = board.top;
        emptyText.SetActive(ranks.Length == 0);
        myRow.gameObject.SetActive(false);
        
        for (int i = 0; i < ranksRows.Length; i++)
        {
            if (i >= ranks.Length)
            {
                ranksRows[i].gameObject.SetActive(false);
                continue;
            }
            ranksRows[i].gameObject.SetActive(true);
            ranksTexts[i].text = ranks[i].rank.ToString();
            nickNameTexts[i].text = ranks[i].nickname;
            bestTexts[i].text = ranks[i].best.ToString();

            if (ranks[i].nickname.Equals(SessionState.Instance.nickName))
            {
                myRow.gameObject.SetActive(true);
                myRow.anchoredPosition = 
                    new Vector2(myRow.anchoredPosition.x, ranksRows[i].anchoredPosition.y);
            }
        }

        if (board.rank != 0)
        {
            myRankText.SetText($"내 기록: {board.rank}등 · {board.best}점");
        }
        else
        {
            myRankText.SetText("기록이 없어요");
        }
        gameObject.SetActive(true);
    }
}
