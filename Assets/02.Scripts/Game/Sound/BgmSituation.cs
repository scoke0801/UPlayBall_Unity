namespace Baseball.Game.Sound
{
    /// <summary>
    /// BGM을 결정하는 게임 국면이다. 화면(UI) 단위가 아니라 플레이어가 처한 상황 단위로 나눈다.
    /// 화면 단위로 나누면 탭을 옮길 때마다 곡이 끊기고, 경기 준비/결과 화면에서 음악이 튄다.
    /// </summary>
    public enum BgmSituation
    {
        /// <summary>경기 밖의 모든 커리어 관리 국면(대시보드·성장·일정·계약 등)이다.</summary>
        Lobby = 0,

        /// <summary>경기 세션이 살아 있는 동안(준비 → 진행 → 결과)이다.</summary>
        MatchPlay = 1
    }
}
