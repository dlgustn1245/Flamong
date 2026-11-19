public static class GameList
{
    public const string Tidy = "tidy";
    public const string Pairs = "pairs";

    public static string NameOf(string game)
    {
        switch (game)
        {
            case Tidy:
                return "마음 정리";
            case Pairs:
                return "짝꿍 찾기";
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
            case Pairs:
                return SceneName.PairsGameScene;
            default:
                return null;
        }
    }
}
