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

    public EggRotater eggRotater;
    // eggId(EggSelect 순서) → 원 위 칸 번호(12시부터 시계 방향: 핑크 · 그레이 · 오렌지 · 라벤더 · 크림 · 그린 · 퍼플 · 블루)
    public int[] slotOfEgg = { 0, 6, 7, 5, 1, 2, 3, 4 };
    public Image flashImage;   // 화면 전체 흰색. 평소 투명

    [Header("알이 깨지는 연출")]
    public float moveTime = 0.4f;
    public float hatchScale = 1.6f;
    public float shakeTime = 0.6f;
    public float shakeAngle = 15f;
    public float flashInTime = 0.1f;
    public float flashOutTime = 0.3f;

    bool received;   // 서버 응답을 받음. 결과 창은 연출이 끝난 뒤 HatchEgg 가 연다

    void Start()
    {
        startButton.onClick.AddListener(OnClickStart);
        okButton.onClick.AddListener(() =>
        {
            SceneLoader.Load(SceneName.MainScene);
        });
    }

    void OnClickStart()
    {
        startButton.interactable = false;
        SendRequest();   // 바퀴가 서는 동안 응답을 받아 둔다
        eggRotater.StopAt(slotOfEgg[SessionState.Instance.eggIdx], OnWheelStopped);
    }

    // 오류 창의 [다시 시도]도 요청만 다시 보낸다. 바퀴와 연출은 그대로 이어진다
    void SendRequest()
    {
        var request = new PostCharacterRequestBody
        {
            eggId = SessionState.Instance.eggIdx
        };
        var header = new Dictionary<string, string>{{"X-Player-Id",  SessionState.Instance.playerId}};
        StartCoroutine(NetworkManager.Instance.PostRequest<PostCharacterRequestBody, PostCharacterResponse>(
            $"{NetworkManager.BASE_URL}/characters", request, OnSuccessPostCharacter, OnFailPostCharacter, header, 5));
    }

    // 실패(2xx 아님 · 응답을 못 읽음)는 NetworkManager 가 OnFail 로 보낸다
    void OnSuccessPostCharacter(PostCharacterResponse response)
    {
        int speciesId = response.body.speciesId;
        if (speciesId >= 0 && speciesId < characterSprites.Length)
        {
            characterImage.sprite = characterSprites[speciesId];
        }
        received = true;
    }

    void OnFailPostCharacter(long code, string error)
    {
        print("Server Error");
        if (code == 401)   // 서버가 모르는 계정
        {
            EntryRouter.GoToLogin();
            return;
        }
        errorPanel.Show(SendRequest);
    }

    void OnWheelStopped()
    {
        StartCoroutine(HatchEgg(eggRotater.items[slotOfEgg[SessionState.Instance.eggIdx]]));
    }

    IEnumerator HatchEgg(RectTransform egg)
    {
        // 1. 원 가운데로 가며 커진다
        Vector2 from = egg.anchoredPosition;
        for (float t = 0f; t < moveTime; t += Time.deltaTime)
        {
            float u = t / moveTime;
            egg.anchoredPosition = Vector2.Lerp(from, eggRotater.center, u);
            egg.localScale = Vector3.one * Mathf.Lerp(1f, hatchScale, u);
            yield return null;
        }
        egg.anchoredPosition = eggRotater.center;
        egg.localScale = Vector3.one * hatchScale;

        // 2. 흔들린다. 응답이 늦거나 오류 창에서 다시 시도 중이면 올 때까지 계속 흔든다
        for (float t = 0f; t < shakeTime || !received; t += Time.deltaTime)
        {
            float amplitude = shakeAngle * Mathf.Min(1f, t / shakeTime);
            egg.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * 30f) * amplitude);
            yield return null;
        }
        egg.localEulerAngles = Vector3.zero;

        // 3. 하얗게 번쩍. 가장 밝을 때 결과 창을 열고, 걷히면서 드러나게 한다
        yield return Flash(0f, 1f, flashInTime);
        resultPanel.SetActive(true);
        yield return Flash(1f, 0f, flashOutTime);
    }

    IEnumerator Flash(float from, float to, float time)
    {
        var color = flashImage.color;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            color.a = Mathf.Lerp(from, to, t / time);
            flashImage.color = color;
            yield return null;
        }
        color.a = to;
        flashImage.color = color;
    }

    void OnDestroy()
    {
        startButton.onClick.RemoveAllListeners();
        okButton.onClick.RemoveAllListeners();
        StopAllCoroutines();
    }
}
