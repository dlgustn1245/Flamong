using System;
using System.Collections;
using ServerData;
using UnityEngine;

public abstract class MiniGame : MonoBehaviour
{
    public abstract string GameId { get; }
    public abstract IEnumerator StartGame(GameRules rules, Action<int> onEnd);
    public abstract IEnumerator CountDown(Action timerAction = null);
}
