using Baseball.Core.Players;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 포지션과 능력치 배분의 어긋남을 생성 차단 없이 경고로만 판정한다.
    /// </summary>
    public static class PlayerBuildAdvisor
    {
        /// <summary>
        /// 포지션 핵심 능력치가 전체 평균보다 크게 낮으면 안내 문구를 반환한다.
        /// </summary>
        public static string GetWarning(Player player)
        {
            if (player == null)
                return string.Empty;

            if (player.PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher)
                return GetPitcherWarning(player.PitcherAttributes, player.PrimaryPosition);

            return GetBatterWarning(player.BatterAttributes, player.PrimaryPosition);
        }

        private static string GetBatterWarning(BatterAttributes value, PlayerPosition position)
        {
            int average = (value.Contact + value.Power + value.Speed + value.Bunt + value.Defense + value.Mental) / 6;
            int keyValue = position switch
            {
                PlayerPosition.Catcher => (value.Defense + value.Mental) / 2,
                PlayerPosition.FirstBase => value.Power,
                PlayerPosition.SecondBase => value.Defense,
                PlayerPosition.ThirdBase => value.Power,
                PlayerPosition.Shortstop => (value.Defense + value.Mental) / 2,
                PlayerPosition.CenterField => (value.Speed + value.Defense) / 2,
                PlayerPosition.DesignatedHitter => (value.Contact + value.Power) / 2,
                _ => (value.Power + value.Defense) / 2
            };

            return keyValue + 5 < average
                ? "선택한 포지션의 핵심 능력치가 전체 평균보다 낮습니다. 이 빌드로도 생성을 계속할 수 있습니다."
                : string.Empty;
        }

        private static string GetPitcherWarning(PitcherAttributes value, PlayerPosition position)
        {
            int average = (value.Stamina + value.Velocity + value.Stuff + value.Breaking + value.Control + value.Mental) / 6;
            int keyValue = position == PlayerPosition.StartingPitcher
                ? (value.Stamina + value.Control) / 2
                : (value.Velocity + value.Stuff) / 2;

            return keyValue + 5 < average
                ? "선택한 투수 역할의 핵심 능력치가 전체 평균보다 낮습니다. 이 빌드로도 생성을 계속할 수 있습니다."
                : string.Empty;
        }
    }
}
