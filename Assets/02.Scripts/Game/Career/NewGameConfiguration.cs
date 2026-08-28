using System;
using Baseball.Core.Balance;
using Baseball.Core.Teams;

namespace Baseball.Game.Career
{
    /// <summary>
    /// Game 레이어의 SO를 새 게임 로직이 소비할 수 있는 순수 C# 값으로 변환한 결과다.
    /// </summary>
    public sealed class NewGameConfiguration
    {
        public NewGameConfiguration(
            BalanceTable balance,
            int teamCount,
            int firstSeasonYear,
            int startingAge,
            TeamArchetypeProfile[] archetypes,
            TeamIdentityDefinition[] teamIdentities,
            string[] playerNamePool,
            WorldGenerationConfiguration worldGeneration = null)
        {
            if (balance == null)
                throw new ArgumentNullException(nameof(balance));
            if (teamCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamCount));
            if (teamIdentities == null || teamIdentities.Length < teamCount)
                throw new ArgumentException("구단 정체성 후보가 구단 수보다 적습니다.", nameof(teamIdentities));

            Balance = balance;
            TeamCount = teamCount;
            FirstSeasonYear = firstSeasonYear;
            StartingAge = startingAge;
            Archetypes = (TeamArchetypeProfile[])archetypes.Clone();
            TeamIdentities = (TeamIdentityDefinition[])teamIdentities.Clone();
            PlayerNamePool = (string[])playerNamePool.Clone();
            WorldGeneration = worldGeneration ?? WorldGenerationConfiguration.CreateDefault();
        }

        public BalanceTable Balance { get; }
        public int TeamCount { get; }
        public int FirstSeasonYear { get; }
        public int StartingAge { get; }
        public TeamArchetypeProfile[] Archetypes { get; }
        public TeamIdentityDefinition[] TeamIdentities { get; }
        public string[] PlayerNamePool { get; }
        public WorldGenerationConfiguration WorldGeneration { get; }

        /// <summary>
        /// 데이터 Asset을 읽지 못한 개발·테스트 환경에서도 같은 계약으로 동작하는 기본값을 만든다.
        /// </summary>
        public static NewGameConfiguration CreateDefault()
        {
            return new NewGameConfiguration(
                BalanceTable.CreateDefault(),
                teamCount: 8,
                firstSeasonYear: 2028,
                startingAge: 18,
                TeamArchetypeLibrary.CreateDefaultPool(),
                CreateDefaultTeamIdentities(),
                CreateDefaultPlayerNamePool());
        }

        private static TeamIdentityDefinition[] CreateDefaultTeamIdentities()
        {
            return new[]
            {
                new TeamIdentityDefinition("서울 블루윙스", new TeamColor(45, 105, 210)),
                new TeamIdentityDefinition("부산 마리너스", new TeamColor(25, 92, 138)),
                new TeamIdentityDefinition("인천 웨이브", new TeamColor(32, 156, 168)),
                new TeamIdentityDefinition("광주 레드폭스", new TeamColor(202, 62, 71)),
                new TeamIdentityDefinition("수원 스타즈", new TeamColor(113, 83, 171)),
                new TeamIdentityDefinition("대전 호크스", new TeamColor(224, 139, 47)),
                new TeamIdentityDefinition("대구 크라운", new TeamColor(195, 166, 52)),
                new TeamIdentityDefinition("창원 블레이즈", new TeamColor(216, 76, 43)),
                new TeamIdentityDefinition("울산 가디언즈", new TeamColor(52, 133, 89)),
                new TeamIdentityDefinition("전주 팔콘스", new TeamColor(103, 119, 138)),
                new TeamIdentityDefinition("제주 돌핀스", new TeamColor(38, 171, 197)),
                new TeamIdentityDefinition("춘천 스톰", new TeamColor(96, 108, 145))
            };
        }

        private static string[] CreateDefaultPlayerNamePool()
        {
            return new[]
            {
                "김도윤", "이준서", "박시우", "최민재", "정우진", "강현우", "조성민", "윤태호",
                "장민준", "임재현", "한승우", "오지훈", "서동현", "신예준", "권민성", "황준혁",
                "안지환", "송재원", "전성훈", "홍민기", "유건우", "고은찬", "문태윤", "양시현",
                "배준영", "백승현", "허도현", "남시우", "심건호", "노재민", "하윤성", "곽준호"
            };
        }
    }
}
