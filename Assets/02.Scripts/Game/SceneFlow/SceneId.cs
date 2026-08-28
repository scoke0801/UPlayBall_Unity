namespace Baseball.Game.SceneFlow
{
    /// <summary>
    /// Build Settings에 등록되는 Unity Scene의 논리적 식별자다.
    /// </summary>
    public enum SceneId
    {
        Boot,
        Loading,
        Management,
        Match
    }

    /// <summary>
    /// 대상 Scene으로 이동할 때 사용할 전환 방식을 지정한다.
    /// </summary>
    public enum SceneTransitionMode
    {
        Direct,
        LoadingScreen
    }

    /// <summary>
    /// SceneId와 실제 Unity Scene 이름 사이의 단일 매핑을 제공한다.
    /// </summary>
    public static class SceneCatalog
    {
        public const string BootSceneName = "Boot";
        public const string LoadingSceneName = "Loading";
        public const string ManagementSceneName = "Management";
        public const string MatchSceneName = "Match";

        /// <summary>
        /// 논리 Scene 식별자를 Build Settings의 Scene 이름으로 변환한다.
        /// </summary>
        public static string GetSceneName(SceneId sceneId)
        {
            return sceneId switch
            {
                SceneId.Boot => BootSceneName,
                SceneId.Loading => LoadingSceneName,
                SceneId.Management => ManagementSceneName,
                SceneId.Match => MatchSceneName,
                _ => string.Empty
            };
        }

        /// <summary>
        /// 플레이어가 실제로 머무르는 콘텐츠 Scene인지 반환한다.
        /// </summary>
        public static bool IsContentScene(SceneId sceneId)
        {
            return sceneId is SceneId.Management or SceneId.Match;
        }
    }
}
