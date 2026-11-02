using System.Collections.Generic;
using ServerData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PostScene : MonoBehaviour
{
    public string kind;
    
    public NoticePanel noticePanel;
    public ErrorPanel errorPanel;
    public Button backButton;

    public TMP_InputField inputField;
    public TextMeshProUGUI countText;
    public Button writeButton;

    public GameObject writePanel;
    public GameObject donePanel;

    public ScrollRect scrollRect;
    public RectTransform[] items;
    public TextMeshProUGUI[] nicknameTexts;
    public TextMeshProUGUI[] bodyTexts;
    public TextMeshProUGUI[] dateTexts;
    public GameObject emptyText;
    
    Dictionary<string, string> header;

    void Start()
    {
        header = new Dictionary<string, string> { { "X-Player-Id", SessionState.Instance.playerId } };
        GetPost();
        
        inputField.onValueChanged.AddListener(body =>
        {
            countText.SetText("{0} / 100", body.Length);
            writeButton.interactable = !string.IsNullOrWhiteSpace(body);
        });

        backButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.MainScene);
        });
        writeButton.onClick.AddListener(PostMessage);
    }

    void OnDestroy()
    {
        inputField.onValueChanged.RemoveAllListeners();
        backButton.onClick.RemoveAllListeners();
        writeButton.onClick.RemoveAllListeners();
        StopAllCoroutines();
    }

    void PostMessage()
    {
        writeButton.interactable = false;
        
        var request = new PostRequestBody
        {
            kind = kind,
            body = inputField.text
        };
        
        StartCoroutine(NetworkManager.Instance.PostRequest<PostRequestBody, OkBody>(
            $"{NetworkManager.BASE_URL}/posts", request,
            OnSuccessPostMessage, OnFailPostMessage, header));
    }

    void OnSuccessPostMessage(OkBody response)
    {
        print($"OnSuccessPostMessage: {JsonUtility.ToJson(response)}");
        if (kind == "garden")
        {
            noticePanel.Show("꽃을 심었어요");
        }
        else
        {
            noticePanel.Show("소식을 전했어요");
        }
        GetPost();
    }

    void OnFailPostMessage(long code, string error)
    {
        print($"OnFailPostMessage: {code} {error}");
        
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        if (code == 409)
        {
            GetPost();
            return;
        }
        if (code == 400)
        {
            noticePanel.Show("쓸 수 없는 글자가 있어요\n이모지는 빼 주세요");
            writeButton.interactable = true;
            return;
        }
        errorPanel.Show(PostMessage);
    }

    void GetPost()
    {
        StartCoroutine(NetworkManager.Instance.GetRequest<PostsBody>(
            $"{NetworkManager.BASE_URL}/posts?kind={kind}",
            OnSuccessGetPost,
            OnFailGetPost, 
            header));
    }

    void OnSuccessGetPost(PostsBody response)
    {
        print($"OnSuccessGetPost: {JsonUtility.ToJson(response)}");
        writePanel.SetActive(response.canWrite);
        donePanel.SetActive(!response.canWrite);
        
        SetMessageCards(response.posts);
    }

    void OnFailGetPost(long code, string error)
    {
        print($"OnFailGetPost: {code} {error}");
        if (code == 401)
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(GetPost);
    }

    void SetMessageCards(PostItem[] posts)
    {
        for (int i = 0; i < items.Length; i++)
        {
            bool hasMessage = i < posts.Length;
            items[i].gameObject.SetActive(hasMessage);
            if (!hasMessage)
            {
                continue;
            }
            var post = posts[i];
            nicknameTexts[i].text = post.nickname;
            bodyTexts[i].text = post.body;
            dateTexts[i].text = post.date.Substring(2).Replace('-', '.');
            
            var body = bodyTexts[i].rectTransform;
            float h = bodyTexts[i].preferredHeight;
            
            body.sizeDelta = new Vector2(body.sizeDelta.x, h);
            items[i].sizeDelta = new Vector2(items[i].sizeDelta.x, 
                Mathf.Max(160f, -body.anchoredPosition.y + h + 64f));
        }
        
        emptyText.SetActive(posts.Length == 0);
        scrollRect.verticalNormalizedPosition = 1f;
    }
}
