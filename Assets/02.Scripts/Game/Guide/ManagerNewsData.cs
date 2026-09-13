using System;
using Baseball.Core.Growth;

namespace Baseball.Game.Guide
{
    public enum ManagerNewsKind { None, Training, Study, HomeRunMilestone, StrikeoutMilestone, WeeklyReview, DecisionFollowup }

    /// <summary>확정 결과만 저장한다. 문구·승패 원인 추측·경기 RNG는 소유하지 않는다.</summary>
    [Serializable]
    public sealed class ManagerNewsEvidence
    {
        public ManagerNewsKind kind;
        public string label;
        public int[] growth = new int[PlayerAbilityCatalog.AbilityCount];
        public int milestone, games, wins, losses, draws, runsFor, runsAgainst;
        public int bullpenOuts, bullpenWalks, previousOuts, previousWalks, previousGames;
        public int appearances, plateAppearances, hits, outs, earnedRuns, walks, strikeouts;
        public bool hasComparison, roleChanged;
        /// <summary>복원된 근거가 음수·손상 배열·불가능한 집계를 포함하지 않는지 확인한다.</summary>
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(ManagerNewsKind), kind) || growth == null || growth.Length != PlayerAbilityCatalog.AbilityCount ||
                milestone < 0 || games < 0 || wins < 0 || losses < 0 || draws < 0 || runsFor < 0 || runsAgainst < 0 ||
                bullpenOuts < 0 || bullpenWalks < 0 || previousOuts < 0 || previousWalks < 0 || previousGames < 0 ||
                appearances < 0 || plateAppearances < 0 || hits < 0 || outs < 0 || earnedRuns < 0 || walks < 0 || strikeouts < 0 ||
                (kind == ManagerNewsKind.WeeklyReview && (long)wins + losses + draws != games) ||
                (hasComparison && (bullpenOuts == 0 || previousOuts == 0)))
                throw new ArgumentException("구단 소식의 저장 근거가 잘못되었습니다.");
            foreach (int value in growth) if (value < 0) throw new ArgumentException("성장 근거가 잘못되었습니다.");
        }
        public ManagerNewsEvidence Copy()
        {
            var copy = (ManagerNewsEvidence)MemberwiseClone();
            copy.growth = growth == null ? new int[PlayerAbilityCatalog.AbilityCount] : (int[])growth.Clone();
            return copy;
        }
    }

    /// <summary>확정된 한 경기에서 보고에 필요한 선수의 공식 기록만 전달한다.</summary>
    public sealed class ManagerNewsPlayerLine
    {
        public string CardId;
        public bool HasPitchingLine, StartedPitching;
        public int PlateAppearances, Hits, HomeRuns, SeasonHomeRuns;
        public int Outs, EarnedRuns, Walks, Strikeouts, SeasonStrikeouts;
    }

    [Serializable]
    public sealed class ManagerNewsCursor
    {
        public string stream;
        public int sequence;
    }

    [Serializable]
    public sealed class ManagerDecisionObservation
    {
        public string key, cardId;
        public int sequence, season, startGame, games, appearances, plateAppearances, hits, outs, earnedRuns, walks, strikeouts;
        public bool expectsStarter, hasPitchingLine, roleChanged;
        public ManagerDecisionObservation Copy() => (ManagerDecisionObservation)MemberwiseClone();
    }

    /// <summary>발행 한도·멱등 커서·진행 중 집계를 같은 세이브에 보존한다.</summary>
    [Serializable]
    public sealed class ManagerNewsProgressData
    {
        public int budgetSeason, budgetWeek, matchSeason, lastMatchOrdinal, weeklyIndex, decisionSequence;
        public string[] announcedCases = Array.Empty<string>();
        public ManagerNewsCursor[] cursors = Array.Empty<ManagerNewsCursor>();
        public ManagerDecisionObservation[] decisions = Array.Empty<ManagerDecisionObservation>();
        public ManagerNewsEvidence weekly = new ManagerNewsEvidence();
        public ManagerNewsEvidence previousWeek = new ManagerNewsEvidence();
    }
}
