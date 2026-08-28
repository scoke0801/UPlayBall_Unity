using System;
using System.Collections.Generic;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 세이브 가능한 경기 상태를 만들기 전의 불변 정규 시즌 일정 한 경기를 표현한다.
    /// </summary>
    public readonly struct ScheduledGameDefinition
    {
        public ScheduledGameDefinition(int gameId, int round, int awayTeamId, int homeTeamId)
        {
            GameId = gameId;
            Round = round;
            AwayTeamId = awayTeamId;
            HomeTeamId = homeTeamId;
        }

        public int GameId { get; }
        public int Round { get; }
        public int AwayTeamId { get; }
        public int HomeTeamId { get; }
    }

    /// <summary>
    /// 모든 구단이 매 라운드 한 번씩 경기하는 결정론적 Round-robin 일정을 생성한다.
    /// </summary>
    public sealed class SeasonScheduleGenerator
    {
        private readonly IRandomSource _random;

        public SeasonScheduleGenerator(IRandomSource random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// 구단마다 정확히 gamesPerTeam 경기를 갖는 일정을 생성한다.
        /// </summary>
        public ScheduledGameDefinition[] Generate(IReadOnlyList<int> teamIds, int gamesPerTeam)
        {
            if (teamIds == null)
                throw new ArgumentNullException(nameof(teamIds));
            if (teamIds.Count < 2 || teamIds.Count % 2 != 0)
                throw new ArgumentException("Round-robin 일정에는 2개 이상의 짝수 구단이 필요합니다.", nameof(teamIds));
            if (gamesPerTeam <= 0)
                throw new ArgumentOutOfRangeException(nameof(gamesPerTeam));

            int[] rotation = CopyAndShuffle(teamIds);
            int roundsPerCycle = rotation.Length - 1;
            int gamesPerRound = rotation.Length / 2;
            var result = new ScheduledGameDefinition[gamesPerTeam * gamesPerRound];
            int resultIndex = 0;

            for (int round = 0; round < gamesPerTeam; round++)
            {
                int cycle = round / roundsPerCycle;
                int cycleRound = round % roundsPerCycle;
                for (int pair = 0; pair < gamesPerRound; pair++)
                {
                    int left = rotation[pair];
                    int right = rotation[rotation.Length - 1 - pair];
                    bool swapHome = ((cycle + cycleRound + pair) & 1) != 0;
                    int away = swapHome ? right : left;
                    int home = swapHome ? left : right;
                    result[resultIndex] = new ScheduledGameDefinition(
                        resultIndex + 1,
                        round + 1,
                        away,
                        home);
                    resultIndex++;
                }

                RotateKeepingFirst(rotation);
            }

            return result;
        }

        private int[] CopyAndShuffle(IReadOnlyList<int> teamIds)
        {
            var result = new int[teamIds.Count];
            for (int index = 0; index < result.Length; index++)
            {
                int teamId = teamIds[index];
                if (teamId <= 0)
                    throw new ArgumentException("TeamId는 양수여야 합니다.", nameof(teamIds));
                for (int previous = 0; previous < index; previous++)
                {
                    if (result[previous] == teamId)
                        throw new ArgumentException("TeamId는 중복될 수 없습니다.", nameof(teamIds));
                }
                result[index] = teamId;
            }

            for (int index = result.Length - 1; index > 0; index--)
            {
                int swapIndex = (int)(_random.NextDouble() * (index + 1));
                if (swapIndex > index)
                    swapIndex = index;
                (result[index], result[swapIndex]) = (result[swapIndex], result[index]);
            }

            return result;
        }

        private static void RotateKeepingFirst(int[] rotation)
        {
            int last = rotation[rotation.Length - 1];
            for (int index = rotation.Length - 1; index > 1; index--)
                rotation[index] = rotation[index - 1];
            rotation[1] = last;
        }
    }
}
