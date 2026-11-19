using System.Collections;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MiniGameScene : MonoBehaviour
{
    public MiniGame miniGame;
    
    public ErrorPanel errorPanel;
    public NoticePanel noticePanel;
    public RankingPanel rankingPanel;
    public ConfirmPanel confirmPanel;
    
    public Button backButton;
    public Button rankingButton;
    
    public TextMeshProUGUI bestText;
    public Button startButton;
    
    public Image characterImage;
    public CharacterSprites characterSprites;

    public GameObject readyPanel;
    public GameObject playPanel;
    public GameObject fieldPanel;
    public GameObject boardPanel;    // Canvas - Main/Group - Board: 안내 글. 판 · 결과 창 중에 켠다

    public GameObject resultPanel;
    public TextMeshProUGUI resultScoreText;
    public TextMeshProUGUI resultBestText;
    public TextMeshProUGUI resultRankText;
    public TextMeshProUGUI resultNextText;
    public TextMeshProUGUI resultAffectionText;
    public Button resultCloseButton;
    public Button resultRankingButton;
    public Button retryButton;

    int lastScore;   // 연결 실패로 다시 보낼 때 같은 점수를 쓴다
    bool isPlaying;
    bool isPaused;   // 멈춘 뒤 다시 3 · 2 · 1 을 다 셀 때까지. 그 사이 [뒤로]를 또 누르면 세기가 겹친다
    
    void Start()
    {
        ShowCharacter();

        backButton.onClick.AddListener(() =>
        {
            if (isPlaying)
            {
                if (isPaused)
                {
                    return;
                }
                isPaused = true;
                fieldPanel.SetActive(false);
                Time.timeScale = 0;
                confirmPanel.Show("그만할까요?\n이번 판은 기록되지 않아요",
                    () =>
                    {
                        // 씬이 넘어가는 몇 프레임 사이에 판이 끝나 점수가 가지 않게 먼저 끈다
                        miniGame.enabled = false;
                        Time.timeScale = 1;
                        SceneLoader.Load(SceneName.GameScene);
                    },
                    () =>
                    {
                        StartCoroutine(miniGame.CountDown(() =>
                        {
                            fieldPanel.SetActive(true);
                            Time.timeScale = 1;
                            isPaused = false;
                        }));
                    });
            }
            else
            {
                SceneLoader.Load(SceneName.GameScene);
            }
        });
        
        rankingButton.onClick.AddListener(() =>
        {
            GetRanking(true);
        });
        
        startButton.onClick.AddListener(PostGame);

        resultCloseButton.onClick.AddListener(BackToReady);
        // 준비 화면을 거치지 않고 바로 시작한다(응답을 기다리는 동안 준비 화면이 깜빡이지 않게)
        retryButton.onClick.AddListener(() =>
        {
            resultPanel.SetActive(false);
            PostGame();
        });
        resultRankingButton.onClick.AddListener(() =>
        {
            BackToReady();
            GetRanking(true);
        });

        GetRanking(false);
    }

    void OnDestroy()
    {
        Time.timeScale = 1;
        backButton.onClick.RemoveAllListeners();
        rankingButton.onClick.RemoveAllListeners();
        startButton.onClick.RemoveAllListeners();
        resultCloseButton.onClick.RemoveAllListeners();
        retryButton.onClick.RemoveAllListeners();
        resultRankingButton.onClick.RemoveAllListeners();

        StopAllCoroutines();
    }

    // 캐릭터가 없으면 그림을 끈다
    void ShowCharacter()
    {
        var sprite = characterSprites.Get(SessionState.Instance.speciesId, SessionState.Instance.level);
        if (sprite != null)
        {
            characterImage.sprite = sprite;
        }
        characterImage.enabled = sprite != null;
    }

    void GetRanking(bool openBoard)
    {
        StartCoroutine(NetworkManager.Instance.GetRequest<Board>(
            $"{NetworkManager.BASE_URL}/rankings",
            response =>
            {
                OnSuccessGetBoard(response, openBoard);
            },
            (code, error) =>
            {
                OnFailGetBoard(code, error, openBoard);
            }, 5));
    }

    void OnSuccessGetBoard(Board response, bool openBoard)
    {
        print($"OnSuccessGetBoard: {JsonUtility.ToJson(response)}");
        if (response.rank > 0)
        {
            bestText.SetText($"{response.rank}등 · {response.best}점");
        }
        else
        {
            bestText.SetText("이번 달 기록이 없어요");
        }
        
        // [랭킹] 직후 [시작]을 눌렀으면 늦게 온 응답으로 창을 열지 않는다. 판이 창 아래에서 시작된다
        if (openBoard && startButton.interactable)
        {
            rankingPanel.ShowRankingBoard(response);
        }
    }

    void OnFailGetBoard(long code, string error, bool openBoard)
    {
        print($"OnFailGetBoard: {code} {error}");
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        // [시작]을 누른 뒤(응답 대기 · 판 · 결과 창)에는 띄우지 않는다. 오류 창이 판을 가리거나 [시작]의 다시 시도를 덮는다
        if (!startButton.interactable)
        {
            return;
        }
        errorPanel.Show(() =>
        {
            GetRanking(openBoard);
        });
    }

    void PostGame()
    {
        startButton.interactable = false;   // 응답 전에 또 눌러 시작이 두 번 가지 않게
        rankingButton.gameObject.SetActive(false);   // 응답 전에 랭킹 창을 열면 그 아래에서 판이 시작된다
        var request = new GameStartRequestBody
        {
            game = miniGame.GameId
        };
        
        StartCoroutine(NetworkManager.Instance.PostRequest<GameStartRequestBody, GameStartBody>(
            $"{NetworkManager.BASE_URL}/games/start", request,
            OnSuccessPostGame, OnFailPostGame, 5));
    }

    void OnSuccessPostGame(GameStartBody response)
    {
        print($"OnSuccessPostGame: {JsonUtility.ToJson(response)}");
        PlayGame(response.rules);
    }

    void OnFailPostGame(long code, string error)
    {
        print($"OnFailPostGame: {code} {error}");
        
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(PostGame);
    }

    void PlayGame(GameRules rules)
    {
        readyPanel.SetActive(false);
        playPanel.SetActive(true);
        boardPanel.SetActive(true);
        rankingButton.gameObject.SetActive(false);
        characterImage.gameObject.SetActive(false);
        StartCoroutine(StartGame(rules));
    }

    // 처음 3 · 2 · 1 동안은 아직 누른 것이 없어 [뒤로]가 바로 달력으로 간다. 멈춤은 다 센 뒤부터
    IEnumerator StartGame(GameRules rules)
    {
        yield return StartCoroutine(miniGame.StartGame(rules, OnGameEnd));
        isPlaying = true;
    }

    void OnGameEnd(int score)
    {
        isPlaying = false;   // 이제 [뒤로]는 멈춤이 아니라 나가기
        lastScore = score;
        PostScore();
    }

    void PostScore()
    {
        var request = new ScoreRequestBody
        {
            score = lastScore
        };

        StartCoroutine(NetworkManager.Instance.PostRequest<ScoreRequestBody, ScoreBody>(
            $"{NetworkManager.BASE_URL}/scores", request,
            OnSuccessPostScore, OnFailPostScore, 10));
    }

    void OnSuccessPostScore(ScoreBody response)
    {
        print($"OnSuccessPostScore: {JsonUtility.ToJson(response)}");
        resultScoreText.SetText($"{lastScore}점");
        resultBestText.SetText($"이번 달 최고 {response.best}점");
        resultRankText.SetText($"이번 달 {response.rank}등");
        resultNextText.SetText(response.gap > 0 ? $"다음 등수까지 {response.gap}점" : "1등이에요!");

        // 레벨은 levelUp 과 상관없이 응답대로 맞춘다. 다시 보낸 점수(levelUp false)에도 첫 요청이 올린 레벨이 들어 있다
        SessionState.Instance.level = response.growth.level;
        ShowCharacter();

        // 오른 애정도는 서버가 정한다(하루 첫 판 +2). 다시 보낸 점수면 0 이라 줄을 끈다
        resultAffectionText.gameObject.SetActive(response.gain > 0);
        if (response.levelUp)
        {
            resultAffectionText.SetText($"애정도 +{response.gain} · Lv {response.growth.level} 달성!");
        }
        else if (response.growth.completed)
        {
            resultAffectionText.SetText($"애정도 +{response.gain} · 다 자랐어요!");
        }
        else
        {
            resultAffectionText.SetText($"오늘 첫 판! 애정도 +{response.gain}");
        }

        resultPanel.SetActive(true);
        SoundManager.Instance.PlaySfx(SoundManager.Instance.levelUp);
        GetRanking(false);   // 준비 카드의 이번 달 기록을 새로 고친다
    }

    void OnFailPostScore(long code, string error)
    {
        print($"OnFailPostScore: {code} {error}");
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        // 시작 없음 · 너무 빠름 · 10분 넘음(409), 범위 밖(400): 다시 보내도 같다
        if (code == 400 || code == 409)
        {
            noticePanel.Show("이번 판은 기록되지 않았어요");
            BackToReady();
            GetRanking(false);
            return;
        }
        errorPanel.Show(PostScore);
    }

    // 결과 창을 닫거나 기록이 안 된 판 뒤에 준비 화면으로
    void BackToReady()
    {
        isPlaying = false;
        resultPanel.SetActive(false);
        playPanel.SetActive(false);
        boardPanel.SetActive(false);
        readyPanel.SetActive(true);
        rankingButton.gameObject.SetActive(true);
        characterImage.gameObject.SetActive(true);
        startButton.interactable = true;
    }
}
