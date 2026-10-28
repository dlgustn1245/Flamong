using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SceneFader : MonoBehaviour
{
	public Graphic graphic;
	public float fadeInTime = 1f;

	IEnumerator Start()
	{
		Color color = graphic.color;
		color.a = 1f;
		graphic.color = color;
		graphic.enabled = true;
		graphic.raycastTarget = true;
		graphic.canvasRenderer.SetAlpha(1f);
		graphic.CrossFadeAlpha(0f, fadeInTime, false);

		yield return new WaitForSeconds(0.5f);
		graphic.raycastTarget = false;
	}
}
