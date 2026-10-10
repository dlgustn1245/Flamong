using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneName
{
	public static string TitleScene = "Title";
	public static string IntroScene = "Intro";
	public static string LoginScene = "Login";
	public static string EggSelectScene = "EggSelect";
	public static string HatchScene = "Hatch";
	public static string MainScene = "Main";
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
    
		Debug.Log($"Scene: {sceneName} loaded");
		loadScene.completed += _ => isLoading = false;
	}
}
