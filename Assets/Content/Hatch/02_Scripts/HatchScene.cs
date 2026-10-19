using System.Collections;
using System.Collections.Generic;
using ServerData;
using UnityEngine;
using UnityEngine.UI;

public class HatchScene : MonoBehaviour
{
    public Button startButton;

    public GameObject resultPanel;
    public Image characterImage;
    public Sprite[] characterSprites; 
    public Button okButton;
    
    public ErrorPanel errorPanel;

    void Start()
    {
        startButton.onClick.AddListener(Request);
        okButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.MainScene);
        });
    }

    void Request()
    {
        startButton.interactable = false;
        StartCoroutine(PostCharacter());
    }

    IEnumerator PostCharacter()
    {
        var request = new PostCharacterRequestBody
        {
            eggId = SessionState.Instance.eggIdx
        };
        string url = $"{NetworkManager.BASE_URL}/characters";
        var header = new Dictionary<string, string>{{"X-Player-Id",  SessionState.Instance.playerId}};
        yield return StartCoroutine(
            NetworkManager.Instance.PostRequest<PostCharacterRequestBody, PostCharacterResponse>(
            url, request, OnSuccessPostCharacter, OnFailPostCharacter, header, 5));
    }

    void OnSuccessPostCharacter(PostCharacterResponse response)
    {
        if (response.statusCode == 200)
        {
            int speciesId = response.body.speciesId;
            print($"Species Id: {speciesId}");
            SessionState.Instance.speciesId = speciesId;
            if (speciesId >= 0 && speciesId < characterSprites.Length)
            {
                characterImage.sprite = characterSprites[speciesId];
            }
            resultPanel.SetActive(true);
        }
    }

    void OnFailPostCharacter(long code, string error)
    {
        print("Server Error");
        if (code == 401)   // 서버가 모르는 계정
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(Request);
    }

    void OnDestroy()
    {
        startButton.onClick.RemoveAllListeners();
        okButton.onClick.RemoveAllListeners();
        StopAllCoroutines();
    }
}