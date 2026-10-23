using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
	static T instance;

	public static T Instance
	{
		get
		{
			if (instance == null)
			{
				instance = FindAnyObjectByType<T>();
				if (instance != null)
				{
					DontDestroyOnLoad(instance.gameObject);
				}
			}
			return instance;
		}
	}

	protected virtual void Awake()
	{
		if (instance != null && instance != this)
		{
			Destroy(gameObject);
			return;
		}

		instance = this as T;
		DontDestroyOnLoad(gameObject);
	}
}
