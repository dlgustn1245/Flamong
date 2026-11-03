using System.Runtime.InteropServices;
using UnityEngine;

// 폰 브라우저에서 비밀번호 칸을 누르면 숫자 키패드가 뜨게 한다(WebKeyboard.jslib). 씬과 상관없이 시작할 때 한 번
public static class WebKeyboard
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void WebKeyboard_NumericPassword();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        WebKeyboard_NumericPassword();
    }
#endif
}
