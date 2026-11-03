public static class GameList
{
    public const string Tidy = "tidy";

    public static string NameOf(string game)
    {
        switch (game)
        {
            case Tidy:
                return "마음 정리";
            default:
                return "새 게임";
        }
    }

    // 이 빌드가 모르는 게임이면 null (서버만 먼저 새 게임으로 바뀐 경우)
    public static string SceneOf(string game)
    {
        switch (game)
        {
            case Tidy:
                return SceneName.TidyGameScene;
            default:
                return null;
        }
    }
}
