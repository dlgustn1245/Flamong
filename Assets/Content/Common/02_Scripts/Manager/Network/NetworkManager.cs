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
				print($"Response: {request.downloadHandler.text}");
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
		string jsonString = JsonUtility.ToJson(jsonData);
		byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);
        
		print($"POST [ url : {url} json : {jsonString} ]");

		var request = new UnityWebRequest(url, "POST");
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
				print($"Response: {request.downloadHandler.text}");
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
