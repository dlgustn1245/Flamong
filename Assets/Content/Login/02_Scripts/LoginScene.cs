using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LoginScene : MonoBehaviour
{
    public Button signUp, login;
    public GameObject signUpPanel, loginPanel;
    
    [Header("가입 패널")]
    public TMP_InputField teamField;
    public TMP_InputField nameField;
    public TMP_InputField passwordField;
    public TMP_InputField nickNameField;
    public Button checkButton;
    
    [Header("로그인 패널")]
    public TMP_InputField loginNickNameField;
    public TMP_InputField loginPasswordField;
    public Button loginButton;

    [Header("중복 확인 결과 패널")]
    public GameObject resultPanel;
    public TMP_Text resultText;
    public Button yesButton;
    public Button noButton;
    public Button closeButton;

    bool checkButtonClicked;   // 요청 중 연타 방지
    string checkedNickName;    // 확인을 요청한 닉네임. 응답 전에 입력이 바뀌어도 이 값으로 가입한다
    bool isLoggingIn;          // 로그인 요청 중 연타 방지

    void Start()
    {
        resultPanel.SetActive(false);
        checkButton.onClick.AddListener(OnClickCheck);
        yesButton.onClick.AddListener(OnClickYes);
        noButton.onClick.AddListener(ClosePanel);
        closeButton.onClick.AddListener(ClosePanel);

        signUp.onClick.AddListener(() => ShowTab(true));
        login.onClick.AddListener(() => ShowTab(false));
        loginButton.onClick.AddListener(OnClickLogin);
        ShowTab(true);
    }
    
    void ShowTab(bool isSignUp)
    {
        signUpPanel.SetActive(isSignUp);
        loginPanel.SetActive(!isSignUp);
        SetAlpha(signUp.image, isSignUp ? 1f : 0.3f);
        SetAlpha(login.image, isSignUp ? 0.3f : 1f);
    }

    static void SetAlpha(Image image, float alpha)
    {
        var c = image.color;
        c.a = alpha;
        image.color = c;
    }

    void OnDestroy()
    {
        checkButton.onClick.RemoveAllListeners();
        yesButton.onClick.RemoveAllListeners();
        noButton.onClick.RemoveAllListeners();
        closeButton.onClick.RemoveAllListeners();
        signUp.onClick.RemoveAllListeners();
        login.onClick.RemoveAllListeners();
        loginButton.onClick.RemoveAllListeners();
        StopAllCoroutines();
    }

    void OnClickCheck()
    {
        if (checkButtonClicked)
        {
            return;
        }
        if (string.IsNullOrEmpty(teamField.text) ||
            string.IsNullOrEmpty(nameField.text) ||
            string.IsNullOrEmpty(nickNameField.text))
        {
            ShowResult("팀 · 이름 · 닉네임을\n모두 적어주세요.", false);
            return;
        }
        if (passwordField.text.Length < 4)
        {
            ShowResult("비밀번호는 숫자\n4~6자리로 적어주세요.", false);
            return;
        }

        checkButtonClicked = true;
        checkedNickName = nickNameField.text;
        StartCoroutine(NetworkManager.Instance.GetRequest<CheckNickNameResponse>(
            $"{NetworkManager.BASE_URL}/players/check?nickname={UnityWebRequest.EscapeURL(checkedNickName)}",
            OnSuccessCheckNickName,
            OnFailCheckNickName,
            null,
            3));
    }

    void OnSuccessCheckNickName(CheckNickNameResponse response)
    {
        checkButtonClicked = false;
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

    void OnFailCheckNickName(long code, string response)
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
        PostPlayer();
    }

    void PostPlayer()
    {
        var player = new PostPlayerRequestBody
        {
            team = teamField.text,
            name = nameField.text,
            nickname = checkedNickName,
            password =  passwordField.text
        };

        StartCoroutine(NetworkManager.Instance.PostRequest<PostPlayerRequestBody, PostPlayerResponse>(
            $"{NetworkManager.BASE_URL}/players", player, OnSuccessPostPlayer, OnFailPostPlayer, null, 10));
    }

    // 실패(2xx 아님 · 응답을 못 읽음)는 NetworkManager 가 OnFail 로 보낸다
    void OnSuccessPostPlayer(PostPlayerResponse response)
    {
        SessionState.Instance.playerId = response.body.playerId;
        SessionState.Instance.team = teamField.text;
        SessionState.Instance.playerName = nameField.text;
        SessionState.Instance.nickName = nickNameField.text;
        SessionState.Instance.SavePlayerId();
        SceneLoader.Load(SceneName.EggSelectScene);
    }

    void OnFailPostPlayer(long code, string response)
    {
        print($"Post Player Failed: {response}");
        ShowResult("가입하지 못했어요.\n잠시 후 다시 시도해주세요.", false);
    }

    // 로그인: POST /players/login 으로 playerId 를 받고, /me 로 갈 곳을 정한다
    void OnClickLogin()
    {
        if (isLoggingIn)
        {
            return;
        }
        if (string.IsNullOrEmpty(loginNickNameField.text) ||
            loginPasswordField.text.Length < 4)
        {
            ShowResult("닉네임과 비밀번호\n(숫자 4~6자리)를 적어주세요.", false);
            return;
        }

        isLoggingIn = true;
        var body = new LoginRequestBody
        {
            nickname = loginNickNameField.text,
            password = loginPasswordField.text
        };
        StartCoroutine(
            NetworkManager.Instance.PostRequest<LoginRequestBody, PostPlayerResponse>(
            $"{NetworkManager.BASE_URL}/players/login", body, OnSuccessLogin, OnFailLogin, null, 10));
    }

    void OnSuccessLogin(PostPlayerResponse response)
    {
        SessionState.Instance.playerId = response.body.playerId;
        SessionState.Instance.SavePlayerId();
        // 갈 곳은 다시 접속할 때와 같은 규칙 (EntryRouter)
        StartCoroutine(EntryRouter.Go(OnFailLogin));
    }

    void OnFailLogin(long code, string error)
    {
        isLoggingIn = false;
        print($"Login Failed: {code} {error}");
        if (code == 401)
        {
            ShowResult("닉네임이나 비밀번호가\n맞지 않아요.", false);
        }
        else if (code == 429)
        {
            ShowResult("여러 번 틀렸어요.\n10분 뒤에 다시 시도해주세요.", false);
        }
        else
        {
            ShowResult("로그인하지 못했어요.\n잠시 후 다시 시도해주세요.", false);
        }
    }
}
