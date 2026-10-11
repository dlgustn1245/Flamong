using UnityEngine;

public class SessionState : Singleton<SessionState>
{
    public string playerId;
    public int eggIdx;
    public int speciesId;

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
