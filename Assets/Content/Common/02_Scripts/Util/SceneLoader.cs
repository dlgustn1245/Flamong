using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneName
{
	public const string TitleScene = "Title";
	public const string IntroScene = "Intro";
	public const string LoginScene = "Login";
	public const string EggSelectScene = "EggSelect";
	public const string HatchScene = "Hatch";
	public const string MainScene = "Main";
}

public static class SceneLoader
{
	static bool isLoading;

	public static void Load(string sceneName)
	{
		if (isLoading)
		{
			return;
		}
		isLoading = true;
		var loadScene = SceneManager.LoadSceneAsync(sceneName);
		if (loadScene == null)
		{
			Debug.Log($"Scene: {sceneName} is null");
			isLoading = false;
			return;
		}
		
		loadScene.completed += _ =>
		{
			isLoading = false;
			Debug.Log($"Scene: {sceneName} loaded");
			Resources.UnloadUnusedAssets();
			GC.Collect();
		};
	}
}
