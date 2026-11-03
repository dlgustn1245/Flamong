using UnityEngine;

public class SessionState : Singleton<SessionState>
{
    public static readonly string HEART_TAPS_KEY = "HeartsTaps";
    public string playerId;
    public int speciesId = -1;
    public int level = 1;
    public int eggIdx;

    public string team;
    public string playerName;
    public string nickName;

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
