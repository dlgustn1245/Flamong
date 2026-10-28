using System;
using System.Collections;
using System.Collections.Generic;
using ServerData;
using UnityEngine;

// 저장된 playerId 로 GET /me 를 불러 들어갈 씬을 정한다.
// Title · Intro(다시 접속)와 Login(로그인 성공 후)이 같이 쓴다. 갈 곳 규칙은 여기 한 곳에만 둔다.
public static class EntryRouter
{
    // 다시 접속할 때. 저장된 playerId 가 없으면 Login. 서버에 닿지 못하면 오류 창 → 다시 시도
    public static void Enter(MonoBehaviour host, ErrorPanel errorPanel)
    {
        if (string.IsNullOrEmpty(SessionState.Instance.playerId))
        {
            SceneLoader.Load(SceneName.LoginScene);
            return;
        }

        host.StartCoroutine(Go((code, error) =>
        {
            Debug.Log($"Enter Failed: {code} {error}");
            errorPanel.Show(() => Enter(host, errorPanel));
        }));
    }

    // 401(서버가 모르는 계정: DB 초기화 · 탈퇴)만 저장값을 지우고 Login 으로 보낸다. 그 밖의 실패는 onFail 로 넘긴다.
    // 0(서버에 닿지 못함)이나 404(없는 주소, BASE_URL 실수 등)에도 지우면 계정과 상관없는 문제로 저장값을 잃는다
    public static IEnumerator Go(Action<long, string> onFail)
    {
        var session = SessionState.Instance;
        var header = new Dictionary<string, string> { { "X-Player-Id", session.playerId } };
        yield return NetworkManager.Instance.GetRequest<MeBody>(
            $"{NetworkManager.BASE_URL}/me",
            me =>
            {
                session.team = me.team;
                session.playerName = me.name;
                session.nickName = me.nickname;
                // 가입만 하고 알을 고르기 전에 나간 사람은 알 고르기부터
                SceneLoader.Load(me.speciesId < 0 ? SceneName.EggSelectScene : SceneName.MainScene);
            },
            (code, error) =>
            {
                if (code == 401)
                {
                    GoToLogin();
                    return;
                }
                onFail?.Invoke(code, error);
            },
            header);
    }

    public static void GoToLogin()
    {
        SessionState.Instance.playerId = string.Empty;
        PlayerPrefs.DeleteKey(SessionState.HEART_TAPS_KEY); 
        SessionState.Instance.SavePlayerId();
        SceneLoader.Load(SceneName.LoginScene);
    }
}
