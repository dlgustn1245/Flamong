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
		print($"GET [ url: {url} ]");
		var request = UnityWebRequest.Get(url);
		if (headers != null)
		{
			foreach (var header in headers)
			{
				request.SetRequestHeader(header.Key, header.Value);
			}
		}

		request.timeout = timeout;
		yield return request.SendWebRequest();
            
		if (request.result == UnityWebRequest.Result.Success)
		{
			try
			{
#if UNITY_EDITOR
				// 응답에는 playerId(사실상 로그인 열쇠)가 들어 있어 빌드의 브라우저 콘솔에는 남기지 않는다
				print($"Response: {request.downloadHandler.text}");
#endif
				var result = JsonUtility.FromJson<TResponse>(request.downloadHandler.text);
				onSuccess?.Invoke(result);
			}
			catch (Exception e)
			{
				onError?.Invoke(request.responseCode, $"파싱 실패: {e.Message}");
			}
		}
		else
		{
			onError?.Invoke(request.responseCode, request.error);
		}

		request.Dispose();
	}
	
	public IEnumerator PostRequest<TRequest, TResponse>(
		string url,
		TRequest jsonData,
		Action<TResponse> onSuccess,
		Action<long, string> onError,
		Dictionary<string, string> headers = null,
		int timeout = 60)
	{
		return SendJson("POST", url, jsonData, onSuccess, onError, headers, timeout);
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
		return SendJson("PATCH", url, jsonData, onSuccess, onError, headers, timeout);
	}

	// JSON 본문을 보내는 요청 (POST · PATCH 공통)
	IEnumerator SendJson<TRequest, TResponse>(
		string method,
		string url,
		TRequest jsonData,
		Action<TResponse> onSuccess,
		Action<long, string> onError,
		Dictionary<string, string> headers,
		int timeout)
	{
		string jsonString = JsonUtility.ToJson(jsonData);
		byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);

		// 본문은 찍지 않는다. 가입 · 로그인 요청에 비밀번호가 원문으로 들어 있고, 웹 빌드에서는 브라우저 콘솔에 그대로 남는다
		print($"{method} [ url : {url} ]");

		var request = new UnityWebRequest(url, method);
		request.uploadHandler = new UploadHandlerRaw(jsonBytes);
		request.downloadHandler = new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");

		if (headers != null)
		{
			foreach (var header in headers)
			{
				request.SetRequestHeader(header.Key, header.Value);
			}
		}

		request.timeout = timeout;
		yield return request.SendWebRequest();

		if (request.result == UnityWebRequest.Result.Success)
		{
			try
			{
#if UNITY_EDITOR
				// 응답에는 playerId(사실상 로그인 열쇠)가 들어 있어 빌드의 브라우저 콘솔에는 남기지 않는다
				print($"Response: {request.downloadHandler.text}");
#endif
				TResponse result = JsonUtility.FromJson<TResponse>(request.downloadHandler.text);
				onSuccess?.Invoke(result);
			}
			catch (Exception e)
			{
				onError?.Invoke(request.responseCode, $"파싱 실패: {e.Message}");
			}
		}
		else
		{
			onError?.Invoke(request.responseCode, request.error);
		}
		request.Dispose();
	}
}
