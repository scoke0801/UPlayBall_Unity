using System;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 경기 입력과 화면이 공유하는 선발 선수 선택과 감독 타순 편성을 제공한다.
    /// </summary>
    internal static class CareerLineupPlan
    {
        /// <summary>
        /// 포지션별 선발을 선택한 뒤 감독 AI가 능력치에 맞는 타순으로 재배치한다.
        /// </summary>
        public static Lineup BuildStartingLineup(
            TeamState team,
            Player myPlayer,
            PlayerGameRole playerRole,
            ManagerLineupAi lineupAi)
        {
            if (team == null)
                throw new ArgumentNullException(nameof(team));
            if (lineupAi == null)
                throw new ArgumentNullException(nameof(lineupAi));

            var fieldingAssignments = new LineupSlot[BaseballRules.BattingOrderSize];
            for (int index = 0; index < fieldingAssignments.Length; index++)
            {
                var position = (PlayerPosition)(index + 1);
                Player batter = IsPlayerStartingAt(myPlayer, playerRole, position)
                    ? myPlayer
                    : CreateRosterPlayer(team.GetStrongestCompetitor(position));
                fieldingAssignments[index] = new LineupSlot(batter, position);
            }

            return lineupAi.BuildLineup(fieldingAssignments);
        }

        /// <summary>
        /// 완성된 선발 라인업에서 지정 선수의 1부터 시작하는 타순을 반환하며, 없으면 0을 반환한다.
        /// </summary>
        public static int GetPlayerBattingOrder(Lineup lineup, int playerId)
        {
            if (lineup == null)
                throw new ArgumentNullException(nameof(lineup));

            for (int index = 0; index < lineup.Count; index++)
            {
                if (lineup[index].Player.PlayerId == playerId)
                    return index + 1;
            }
            return 0;
        }

        /// <summary>
        /// 완성된 선발 라인업에서 지정 수비 위치가 차지하는 0부터 시작하는 타순을 반환한다.
        /// </summary>
        public static int GetBattingOrderIndex(Lineup lineup, PlayerPosition position)
        {
            if (lineup == null)
                throw new ArgumentNullException(nameof(lineup));

            for (int index = 0; index < lineup.Count; index++)
            {
                if (lineup[index].FieldingPosition == position)
                    return index;
            }
            throw new InvalidOperationException($"{position} 수비 위치를 선발 라인업에서 찾을 수 없습니다.");
        }

        /// <summary>
        /// 저장된 경쟁자 요약을 경기 시뮬레이션용 선수로 변환한다.
        /// </summary>
        public static Player CreateRosterPlayer(RosterCompetitorState competitor)
        {
            bool isPitcher = competitor.Position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;
            int batterRating = isPitcher ? 20 : competitor.Overall;
            int pitcherRating = isPitcher ? competitor.Overall : 20;
            Handedness battingHand = competitor.PlayerId % 3 == 0
                ? Handedness.Switch
                : competitor.PlayerId % 2 == 0 ? Handedness.Left : Handedness.Right;
            Handedness throwingHand = competitor.PlayerId % 4 == 0 ? Handedness.Left : Handedness.Right;
            return new Player(
                competitor.PlayerId,
                competitor.Name,
                competitor.Position,
                battingHand,
                throwingHand,
                new BatterAttributes(
                    batterRating,
                    batterRating,
                    batterRating,
                    batterRating,
                    batterRating,
                    batterRating),
                new PitcherAttributes(
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating,
                    pitcherRating));
        }

        private static bool IsPlayerStartingAt(
            Player player,
            PlayerGameRole role,
            PlayerPosition position)
        {
            return player != null &&
                   role == PlayerGameRole.StartingBatter &&
                   player.PrimaryPosition == position;
        }
    }
}
