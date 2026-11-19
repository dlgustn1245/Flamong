using System;
using System.Collections;
using ServerData;
using TMPro;
using UnityEngine;

public abstract class MiniGame : MonoBehaviour
{
    readonly WaitForSecondsRealtime oneSecond = new WaitForSecondsRealtime(1f);

    public GameObject countDownGroup;      // 세는 동안 켜는 것. 글자만 있으면 글자 오브젝트, 빛을 깔았으면 그 묶음
    public TextMeshProUGUI countDownText;

    protected int delay; 
    
    public abstract string GameId { get; }
    public abstract IEnumerator StartGame(GameRules rules, Action<int> onEnd);
    public IEnumerator CountDown(Action timerAction = null)
    {
        countDownGroup.SetActive(true);
        for (int i = delay; i > 0; i--)
        {
            countDownText.SetText("{0}", i);
            SoundManager.Instance.PlaySfx(SoundManager.Instance.count);
            yield return oneSecond;
        }
        countDownGroup.SetActive(false);
        timerAction?.Invoke();
    }
}
