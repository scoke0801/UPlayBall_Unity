namespace Baseball.Game.Manager
{
    /// <summary>
    /// Game 레이어 서비스가 GameManager 생명주기에 참여하기 위한 계약이다.
    /// </summary>
    public interface IManager
    {
        int InitializationOrder { get; }
        bool IsInitialized { get; }
        void Initialize();
        void AfterInitialize();
        void Shutdown();
    }

    /// <summary>
    /// 프레임 단위 처리가 필요한 매니저만 구현하는 선택 계약이다.
    /// </summary>
    public interface IUpdatableManager
    {
        void Tick(float deltaTime);
    }

    /// <summary>
    /// 씬 전환 통지가 필요한 매니저만 구현하는 선택 계약이다.
    /// </summary>
    public interface ISceneChangedManager
    {
        void OnSceneChanged(string previousSceneName, string currentSceneName);
    }
}
