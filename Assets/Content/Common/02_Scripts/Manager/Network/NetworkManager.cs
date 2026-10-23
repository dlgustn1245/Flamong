using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// onError(responseCode, error): responseCode 는 서버가 돌려준 HTTP 상태 코드(401 · 404 · 409 · 429 …).
// 서버에 닿지도 못했으면(꺼짐 · 네트워크 끊김 · 시간 초과) 0 이다.
public class NetworkManager : Singleton<NetworkManager>
{
	// 서버 주소는 여기 한 곳에만. 배포할 때 이 줄만 바꾼다
	public const string BASE_URL = "http://localhost:3000";

	public IEnumerator GetRequest<TResponse>(
		string url,
		Action<TResponse> onSuccess,
		Action<long, string> onError,
		Dictionary<string, string> headers = null,
		int timeout = 10)
	{
		return Send("GET", url, null, onSuccess, onError, headers, timeout);
	}

	public IEnumerator PostRequest<TRequest, TResponse>(
		string url,
		TRequest jsonData,
		Action<TResponse> onSuccess,
		Action<long, string> onError,
		Dictionary<string, string> headers = null,
		int timeout = 60)
	{
		return Send("POST", url, ToJsonBytes(jsonData), onSuccess, onError, headers, timeout);
	}

	// 일부만 고치는 요청 (PATCH /players/me). 형식은 POST 와 같다
	public IEnumerator PatchRequest<TRequest, TResponse>(
		string url,
		TRequest jsonData,
		Action<TResponse> onSuccess,
		Action<long, string> onError,
		Dictionary<string, string> headers = null,
		int timeout = 60)
	{
		return Send("PATCH", url, ToJsonBytes(jsonData), onSuccess, onError, headers, timeout);
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
		Dictionary<string, string> headers,
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
			if (headers != null)
			{
				foreach (var header in headers)
				{
					request.SetRequestHeader(header.Key, header.Value);
				}
			}

			request.timeout = timeout;
			yield return request.SendWebRequest();

			if (request.result != UnityWebRequest.Result.Success)
			{
				onError?.Invoke(request.responseCode, request.error);
				yield break;
			}

			try
			{
#if UNITY_EDITOR
				// 응답에는 playerId(사실상 로그인 열쇠)가 들어 있어 빌드의 브라우저 콘솔에는 남기지 않는다
				print($"Response: {request.downloadHandler.text}");
#endif
				onSuccess?.Invoke(JsonUtility.FromJson<TResponse>(request.downloadHandler.text));
			}
			catch (Exception e)
			{
				onError?.Invoke(request.responseCode, $"파싱 실패: {e.Message}");
			}
		}
	}
}
