using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Presentation.Career
{
    /// <summary>
    /// 경기 역할을 포지션 문맥에 맞는 플레이어 표시 문구로 변환한다.
    /// </summary>
    public static class CareerGameRoleFormatter
    {
        /// <summary>
        /// 새 투수 휴식 역할과 이전에 Bench로 계획된 투수 경기를 함께 판정한다.
        /// </summary>
        public static bool IsPitcherRest(PlayerGameRole role, PlayerPosition position)
        {
            bool isPitcher = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            return isPitcher && role is PlayerGameRole.PitcherRest or PlayerGameRole.Bench;
        }

        /// <summary>
        /// 선발 로테이션과 불펜의 비등판일을 구분해 표시한다.
        /// </summary>
        public static string GetPitcherRestLabel(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.StartingPitcher => "로테이션 휴식",
                PlayerPosition.ReliefPitcher => "불펜 휴식",
                _ => "투수 휴식"
            };
        }
    }
}
