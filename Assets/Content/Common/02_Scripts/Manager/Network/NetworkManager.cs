using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NetworkManager : Singleton<NetworkManager>
{
	public IEnumerator GetRequest<TResponse>(
		string url,
		Action<TResponse> onSuccess,
		Action<string> onError,
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
			print($"Response: {request.downloadHandler.text}");
			var result = JsonUtility.FromJson<TResponse>(request.downloadHandler.text);
			onSuccess?.Invoke(result);
		}
		else
		{
			onError?.Invoke(request.error);
		}
		
		request.Dispose();
	}
	
	public IEnumerator PostRequest<TRequest, TResponse>(
		string url,
		TRequest jsonData,
		Action<TResponse> onSuccess,
		Action<string> onError,
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
				onError?.Invoke($"파싱 실패: {e.Message}");
			}
		}
		else
		{
			onError?.Invoke(request.error);
		}
		request.Dispose();
	}
}
