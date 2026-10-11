using UnityEngine;

public class SessionState : Singleton<SessionState>
{
    public string playerId;

    protected override void Awake()
    {
        base.Awake();
        playerId = PlayerPrefs.GetString("playerId");
    }

    public void SavePlayerId()
    {
        PlayerPrefs.SetString("playerId", playerId);
        PlayerPrefs.Save();
    }
}
