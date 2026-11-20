using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// onError(responseCode, error): responseCode 는 서버가 돌려준 HTTP 상태 코드(401 · 404 · 409 · 429 …).
// 서버에 닿지도 못했으면(꺼짐 · 네트워크 끊김 · 시간 초과) 0 이다.
// 로그인한 요청의 401(서버가 모르는 계정)은 onError 로 가지 않고 여기서 Login 으로 보낸다.
public class NetworkManager : Singleton<NetworkManager>
{
	// 서버 주소는 여기 한 곳에만. 배포할 때 이 줄만 바꾼다
#if UNITY_EDITOR
	public const string BASE_URL = "http://localhost:3000";
#else
	public const string BASE_URL = "https://flamong.lhsdev.info/api";
#endif

	public IEnumerator GetRequest<TResponse>(
		string url,
		Action<TResponse> onSuccess,
		Action<long, string> onError,
		int timeout = 10)
	{
		return Send("GET", url, null, onSuccess, onError, timeout);
	}

	public IEnumerator PostRequest<TRequest, TResponse>(
		string url,
		TRequest jsonData,
		Action<TResponse> onSuccess,
		Action<long, string> onError,
		int timeout = 60)
	{
		return Send("POST", url, ToJsonBytes(jsonData), onSuccess, onError, timeout);
	}

	// 일부만 고치는 요청 (PATCH /players/me). 형식은 POST 와 같다
	public IEnumerator PatchRequest<TRequest, TResponse>(
		string url,
		TRequest jsonData,
		Action<TResponse> onSuccess,
		Action<long, string> onError,
		int timeout = 60)
	{
		return Send("PATCH", url, ToJsonBytes(jsonData), onSuccess, onError, timeout);
	}

	public IEnumerator DeleteRequest<TResponse>(string url, Action<TResponse> onSuccess, Action<long, string> onError, int timeout = 10)
	{
		return Send("DELETE", url, null, onSuccess, onError, timeout);
	}

	static byte[] ToJsonBytes<T>(T data)
	{
		return Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
	}

	// body 가 null 이면 본문 없는 요청(GET)
	IEnumerator Send<TResponse>(
		string method,
		string url,
		byte[] body,
		Action<TResponse> onSuccess,
		Action<long, string> onError,
		int timeout)
	{
		// 본문은 찍지 않는다. 가입 · 로그인 요청에 비밀번호가 원문으로 들어 있고, 웹 빌드에서는 브라우저 콘솔에 그대로 남는다
		print($"{method} [ url: {url} ]");

		using (var request = new UnityWebRequest(url, method))
		{
			request.downloadHandler = new DownloadHandlerBuffer();
			if (body != null)
			{
				request.uploadHandler = new UploadHandlerRaw(body);
				request.SetRequestHeader("Content-Type", "application/json");
			}
			// 로그인한 뒤의 모든 요청에 붙인다. 씬마다 헤더를 만들지 않는다. 가입 · 로그인 전에는 비어 있어 붙지 않는다
			string playerId = SessionState.Instance.playerId;
			if (!string.IsNullOrEmpty(playerId))
			{
				request.SetRequestHeader("X-Player-Id", playerId);
			}

			request.timeout = timeout;
			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				// 로그인한 요청에 401(서버가 모르는 계정: DB 초기화 · 탈퇴)이 오면 저장값을 지우고 Login 으로 보낸다. 씬마다 다루지 않는다.
				// 로그인 요청의 401(닉네임 · 비밀번호 틀림)은 playerId 없이 보낸 것이라 여기 걸리지 않고 onError 로 간다.
				// 0(서버에 닿지 못함)이나 404(없는 주소, BASE_URL 실수 등)에는 지우지 않는다. 계정과 상관없는 문제로 저장값을 잃는다
				if (request.responseCode == 401 && !string.IsNullOrEmpty(playerId))
				{
					print($"401 → Login [ url: {url} ]");
					EntryRouter.GoToLogin();
					yield break;
				}
				// 서버가 본문({"error":"posted"} 등)을 보냈으면 그것을, 연결 실패 · 시간 초과면 Unity 오류 문구를 넘긴다
				string errorBody = request.downloadHandler.text;
				onError?.Invoke(request.responseCode, string.IsNullOrEmpty(errorBody) ? request.error : errorBody);
				yield break;
			}

#if UNITY_EDITOR
			// 응답에는 playerId(사실상 로그인 열쇠)가 들어 있어 빌드의 브라우저 콘솔에는 남기지 않는다
			print($"Response: {request.downloadHandler.text}");
#endif
			TResponse response;
			try
			{
				response = JsonUtility.FromJson<TResponse>(request.downloadHandler.text);
			}
			catch (Exception e)
			{
				onError?.Invoke(request.responseCode, $"파싱 실패: {e.Message}");
				yield break;
			}

			// 콜백 안의 예외는 잡지 않는다. onError 로 보내면 오류 창 [다시 시도]가 이미 성공한 요청(가입 등)을 또 보낸다
			onSuccess?.Invoke(response);
		}
	}
}
