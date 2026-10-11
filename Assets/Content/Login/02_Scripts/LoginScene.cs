using System.Collections;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LoginScene : MonoBehaviour
{
    readonly string baseUrl = "http://localhost:3000";

    public TMP_InputField teamField;
    public TMP_InputField nameField;
    public TMP_InputField nickNameField;
    public Button checkButton;

    [Header("중복 확인 결과 패널")]
    public GameObject resultPanel;
    public TMP_Text resultText;
    public Button yesButton;
    public Button noButton;
    public Button closeButton;

    bool checkButtonClicked;   // 요청 중 연타 방지
    string checkedNickName;    // 확인을 요청한 닉네임. 응답 전에 입력이 바뀌어도 이 값으로 가입한다

    void Start()
    {
        resultPanel.SetActive(false);
        checkButton.onClick.AddListener(OnClickCheck);
        yesButton.onClick.AddListener(OnClickYes);
        noButton.onClick.AddListener(ClosePanel);
        closeButton.onClick.AddListener(ClosePanel);
    }

    void OnDestroy()
    {
        checkButton.onClick.RemoveAllListeners();
        yesButton.onClick.RemoveAllListeners();
        noButton.onClick.RemoveAllListeners();
        closeButton.onClick.RemoveAllListeners();
        StopAllCoroutines();
    }

    void OnClickCheck()
    {
        if (checkButtonClicked) return;
        if (string.IsNullOrEmpty(teamField.text) ||
            string.IsNullOrEmpty(nameField.text) ||
            string.IsNullOrEmpty(nickNameField.text)) return;

        checkButtonClicked = true;
        checkedNickName = nickNameField.text;
        StartCoroutine(NetworkManager.Instance.GetRequest<CheckNickNameResponse>(
            $"{baseUrl}/players/check?nickname={UnityWebRequest.EscapeURL(checkedNickName)}",
            OnSuccessCheckNickName,
            OnFailCheckNickName,
            null,
            3));
    }

    void OnSuccessCheckNickName(CheckNickNameResponse response)
    {
        checkButtonClicked = false;
        if (response == null || response.body == null)
        {
            ShowResult("서버 응답을 읽을 수 없어요.\n잠시 후 다시 시도해주세요.", false);
            return;
        }

        if (response.body.available)
        {
            ShowResult(
                $"[{checkedNickName}]\n사용할 수 있는 닉네임이에요.\n이 닉네임으로 할까요?",
                true);
        }
        else
        {
            ShowResult(
                $"[{checkedNickName}]\n이미 사용 중이거나\n쓸 수 없는 글자가 있어요.", 
                false);
        }
    }

    void OnFailCheckNickName(string response)
    {
        checkButtonClicked = false;
        print($"Check NickName Failed: {response}");
        ShowResult("확인하지 못했어요.\n잠시 후 다시 시도해주세요.", false);
    }

    // available 이면 네/아니요, 아니면 닫기만
    void ShowResult(string message, bool available)
    {
        resultText.text = message;
        yesButton.gameObject.SetActive(available);
        noButton.gameObject.SetActive(available);
        closeButton.gameObject.SetActive(!available);
        resultPanel.SetActive(true);
    }

    void ClosePanel()
    {
        resultPanel.SetActive(false);
    }

    void OnClickYes()
    {
        ClosePanel();
        nickNameField.text = checkedNickName;
        StartCoroutine(PostPlayer());
    }

    IEnumerator PostPlayer()
    {
        string url = $"{baseUrl}/players";

        var player = new PostPlayerRequestBody
        {
            team = teamField.text,
            name = nameField.text,
            nickname = checkedNickName
        };

        yield return StartCoroutine(NetworkManager.Instance.PostRequest
            <PostPlayerRequestBody, PostPlayerResponse>(
            url, player, OnSuccessPostPlayer, OnFailPostPlayer, null, 10));
    }

    void OnSuccessPostPlayer(PostPlayerResponse response)
    {
        if (response.statusCode == 200)
        {
            print($"Player ID: {response.body.playerId}");
            SessionState.Instance.playerId = response.body.playerId;
            SessionState.Instance.SavePlayerId();
            SceneLoader.Load(SceneName.EggSelectScene);
        }
        else
        {
            print("Server Error");
            ShowResult("서버 응답을 읽을 수 없어요.\n잠시 후 다시 시도해주세요.", false);
        }
    }

    void OnFailPostPlayer(string response)
    {
        print($"Post Player Failed: {response}");
        ShowResult("가입하지 못했어요.\n잠시 후 다시 시도해주세요.", false);
    }
}
