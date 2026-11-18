using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LoginScene : MonoBehaviour
{
    public Button signUp, login;
    public GameObject signUpPanel, loginPanel;
    public GameObject pcNotice;   // 「PC에서는 한글을 입력할 수 없어요」
    
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
        // Unity 웹 빌드는 PC 브라우저의 한글 조합(IME)을 받지 못한다. 폰은 폰 키보드로 입력돼 괜찮다
        pcNotice.SetActive(!Application.isMobilePlatform);
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
            string.IsNullOrWhiteSpace(nickNameField.text))
        {
            ShowResult("팀 · 이름 · 닉네임을\n모두 적어주세요.", false);
            return;
        }
        if (passwordField.text.Length < 4)
        {
            ShowResult("비밀번호는 숫자\n4~6자리로 적어주세요.", false);
            return;
        }

        // 앞뒤 공백은 실수로 보고 지운다. 서버는 앞뒤 공백 · 이어진 공백이 있는 닉네임을 받지 않는다
        checkedNickName = nickNameField.text.Trim();
        // 서버는 이어진 공백도 그냥 「쓸 수 없음」으로만 돌려줘서, 이유를 알 수 있게 여기서 먼저 알린다
        if (checkedNickName.Contains("  "))
        {
            ShowResult("닉네임에 공백을\n이어서 쓸 수 없어요.", false);
            return;
        }
        checkButtonClicked = true;
        StartCoroutine(NetworkManager.Instance.GetRequest<CheckNickNameResponseBody>(
            $"{NetworkManager.BASE_URL}/players/check?nickname={UnityWebRequest.EscapeURL(checkedNickName)}",
            OnSuccessCheckNickName,
            OnFailCheckNickName,
            3));
    }

    void OnSuccessCheckNickName(CheckNickNameResponseBody response)
    {
        print($"Check NickName Success: {JsonUtility.ToJson(response)}");
        checkButtonClicked = false;
        if (response.available)
        {
            ShowResult(
                $"[{checkedNickName}]\n사용할 수 있는 닉네임이에요.\n다른 기기에서도 이 닉네임으로\n로그인해요.\n이 닉네임으로 할까요?",
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
        print($"Check NickName Failed: {code} {response}");
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

        StartCoroutine(NetworkManager.Instance.PostRequest<PostPlayerRequestBody, PostPlayerResponseBody>(
            $"{NetworkManager.BASE_URL}/players", player, OnSuccessPostPlayer, OnFailPostPlayer, 10));
    }

    // 실패(2xx 아님 · 응답을 못 읽음)는 NetworkManager 가 OnFail 로 보낸다
    void OnSuccessPostPlayer(PostPlayerResponseBody response)
    {
        // playerId 는 사실상 로그인 열쇠라 웹 빌드 브라우저 콘솔에 남지 않게 앞 6자리만 찍는다
        print($"Post Player Success: playerId={response.playerId.Substring(0, 6)}…");
        SessionState.Instance.playerId = response.playerId;
        SessionState.Instance.team = teamField.text;
        SessionState.Instance.playerName = nameField.text;
        SessionState.Instance.nickName = checkedNickName;   // 보낸 값. 응답 전에 입력칸을 고쳐도 서버 값과 어긋나지 않게
        SessionState.Instance.SavePlayerId();
        SceneLoader.Load(SceneName.EggSelectScene);
    }

    void OnFailPostPlayer(long code, string response)
    {
        print($"Post Player Failed: {code} {response}");
        if (code == 400)
        {
            ShowResult("팀 · 이름 · 닉네임에\n쓸 수 없는 글자가 있어요.", false);
            return;
        }
        ShowResult("가입하지 못했어요.\n잠시 후 다시 시도해주세요.", false);
    }

    // 로그인: POST /players/login 으로 playerId 를 받고, /me 로 갈 곳을 정한다
    void OnClickLogin()
    {
        if (isLoggingIn)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(loginNickNameField.text) ||
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
            NetworkManager.Instance.PostRequest<LoginRequestBody, PostPlayerResponseBody>(
            $"{NetworkManager.BASE_URL}/players/login", body, OnSuccessLogin, OnFailLogin, 10));
    }

    void OnSuccessLogin(PostPlayerResponseBody response)
    {
        print($"Login Success: playerId={response.playerId.Substring(0, 6)}…");
        SessionState.Instance.playerId = response.playerId;
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
            ShowResult("닉네임이나 비밀번호가\n맞지 않아요.\n기억나지 않으면 운영자에게\n알려 주세요.", false);
        }
        // 429 는 계정 잠금(locked)과 IP 횟수 제한(rate_limited) 두 가지다. 회사 와이파이는 모두 같은 IP 라 안 틀린 사람도 rate_limited 를 받는다
        else if (code == 429 && error != null && error.Contains("rate_limited"))
        {
            ShowResult("요청이 많아요.\n잠시 후 다시 시도해주세요.", false);
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
