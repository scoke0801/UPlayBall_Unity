using System;
using System.Collections.Generic;
using Baseball.Core.Rules;
using Baseball.Simulation.Match;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 공개된 이벤트만으로 복원한 이닝별 득점표다. 총 타석 수는 경기마다 다르므로
    /// 진행률 막대 대신 라인 스코어로 경기가 어디까지 왔는지 보여 준다.
    /// </summary>
    public sealed class MatchLineScore
    {
        /// <summary>
        /// 아직 시작하지 않은 이닝을 나타내는 값이다.
        /// </summary>
        public const int NotPlayed = -1;

        private readonly int[] _awayRuns;
        private readonly int[] _homeRuns;

        private MatchLineScore(int[] awayRuns, int[] homeRuns, int inningCount, int currentInning)
        {
            _awayRuns = awayRuns;
            _homeRuns = homeRuns;
            InningCount = inningCount;
            CurrentInning = currentInning;
        }

        /// <summary>
        /// 표에 표시할 이닝 수다. 연장전에서는 정규 이닝보다 늘어난다.
        /// </summary>
        public int InningCount { get; }

        public int CurrentInning { get; }

        public int AwayTotal => Sum(_awayRuns);
        public int HomeTotal => Sum(_homeRuns);

        /// <summary>
        /// 1부터 시작하는 이닝 번호의 원정 팀 득점을 반환한다. 미진행 이닝은 <see cref="NotPlayed"/>다.
        /// </summary>
        public int GetAwayRuns(int inning) => _awayRuns[inning - 1];

        /// <summary>
        /// 1부터 시작하는 이닝 번호의 홈 팀 득점을 반환한다. 미진행 이닝은 <see cref="NotPlayed"/>다.
        /// </summary>
        public int GetHomeRuns(int inning) => _homeRuns[inning - 1];

        /// <summary>
        /// 공개된 이벤트 구간만으로 라인 스코어를 만든다.
        /// </summary>
        public static MatchLineScore Create(IReadOnlyList<MatchEvent> events, int visibleEventCount)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));
            if (visibleEventCount < 0 || visibleEventCount > events.Count)
                throw new ArgumentOutOfRangeException(nameof(visibleEventCount));

            int maximumInning = BaseballRules.RegulationInnings;
            for (int index = 0; index < visibleEventCount; index++)
            {
                if (events[index].Inning > maximumInning)
                    maximumInning = events[index].Inning;
            }

            var awayRuns = new int[maximumInning];
            var homeRuns = new int[maximumInning];
            for (int inningIndex = 0; inningIndex < maximumInning; inningIndex++)
            {
                awayRuns[inningIndex] = NotPlayed;
                homeRuns[inningIndex] = NotPlayed;
            }

            int currentInning = 1;
            for (int index = 0; index < visibleEventCount; index++)
            {
                MatchEvent matchEvent = events[index];
                if (matchEvent.EventType == MatchEventType.MatchEnded)
                    continue;

                int inningIndex = matchEvent.Inning - 1;
                currentInning = matchEvent.Inning;
                int[] runs = matchEvent.Half == InningHalf.Top ? awayRuns : homeRuns;
                if (runs[inningIndex] == NotPlayed)
                    runs[inningIndex] = 0;
                if (matchEvent.EventType == MatchEventType.Score)
                    runs[inningIndex]++;
            }

            return new MatchLineScore(awayRuns, homeRuns, maximumInning, currentInning);
        }

        private static int Sum(int[] runs)
        {
            int total = 0;
            for (int index = 0; index < runs.Length; index++)
            {
                if (runs[index] > 0)
                    total += runs[index];
            }
            return total;
        }
    }
}
