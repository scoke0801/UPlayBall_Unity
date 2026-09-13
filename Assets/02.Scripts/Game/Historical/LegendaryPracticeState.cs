using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>정규시즌과 독립된 역대 강팀 진행의 저장 단위다.</summary>
    [Serializable]
    public sealed class LegendaryPracticeProgress
    {
        public string teamId;
        public int wins, attempts, losses, draws, bestRunDifference;
        public bool rewardClaimed;
        public bool hasCleared;
        public int runStartAttempt;
        public long lastPlayedUtcTicks;
        public int lastPlayerScore, lastOpponentScore;
        public bool HasCleared => hasCleared || wins == 3;
        public int NextStarterIndex => (attempts - runStartAttempt) % 5;
        public LegendaryPracticeProgress Copy() => (LegendaryPracticeProgress)MemberwiseClone();
    }

    /// <summary>동일 시도를 다시 확정하거나 최초 보상을 중복 지급하지 않는 도전 원장이다.</summary>
    public sealed class LegendaryPracticeState
    {
        private readonly Dictionary<string, LegendaryPracticeProgress> _teams =
            new Dictionary<string, LegendaryPracticeProgress>(StringComparer.Ordinal);

        public LegendaryPracticeProgress Get(string teamId) => _teams.TryGetValue(teamId, out var value)
            ? value.Copy() : new LegendaryPracticeProgress { teamId = teamId };

        public bool CanPlay(LegendaryPracticeCatalog catalog, LegendaryPracticeTeam team) =>
            team.rank == catalog.teams.Length || Get(catalog.teams[team.rank].challengeTeamId).HasCleared;

        /// <summary>통산 기록·격파·보상 권리를 보존하고 이번 도전을 0승·1선발로 되돌린다.</summary>
        public bool Restart(LegendaryPracticeCatalog catalog, string teamId)
        {
            var team = catalog.Find(teamId);
            var progress = Get(teamId);
            if (progress.attempts == 0 || !CanPlay(catalog, team)) return false;
            progress.hasCleared = progress.HasCleared;
            progress.wins = 0;
            // 전체 시도 번호는 결과 중복 확정 방지와 경기 Seed에 쓰이므로 초기화하지 않는다.
            progress.runStartAttempt = progress.attempts;
            _teams[teamId] = progress;
            return true;
        }

        /// <summary>순차 시도 번호만 수용하여 결과 확정 재시도를 멱등 처리한다.</summary>
        public bool Commit(LegendaryPracticeCatalog catalog, string teamId, int attempt,
            int playerScore, int opponentScore, long utcTicks)
        {
            var team = catalog.Find(teamId);
            var progress = Get(teamId);
            if (attempt <= progress.attempts) return false;
            if (attempt != checked(progress.attempts + 1) || !CanPlay(catalog, team))
                throw new InvalidOperationException("도전 순서가 올바르지 않습니다.");
            if (playerScore < 0 || opponentScore < 0 || utcTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(playerScore));
            progress.attempts = attempt;
            if (playerScore > opponentScore) progress.wins = Math.Min(3, progress.wins + 1);
            else if (playerScore < opponentScore) progress.losses++;
            else progress.draws++;
            progress.hasCleared = progress.HasCleared;
            int difference = playerScore - opponentScore;
            progress.bestRunDifference = attempt == 1 ? difference : Math.Max(progress.bestRunDifference, difference);
            progress.lastPlayerScore = playerScore; progress.lastOpponentScore = opponentScore;
            progress.lastPlayedUtcTicks = utcTicks;
            _teams[teamId] = progress;
            return true;
        }

        /// <summary>상태와 지갑은 동일한 세이브 트랜잭션에서 확정한다.</summary>
        public bool Claim(LegendaryPracticeCatalog catalog, string teamId, ManagerEconomyState economy)
        {
            var team = catalog.Find(teamId);
            var progress = Get(teamId);
            if (!progress.HasCleared || progress.rewardClaimed) return false;
            // 어느 재화도 먼저 변경되지 않도록 모든 합계를 사전 검사한다.
            checked
            {
                _ = economy.Money + Math.Max(0L, team.rewardMoney - economy.ContractArrears);
                _ = economy.DevelopmentPoints + team.rewardDevelopment;
                _ = economy.ScoutingPoints + team.rewardScouting;
            }
            economy.AddMoney(team.rewardMoney);
            economy.AddDevelopmentPoints(team.rewardDevelopment);
            economy.AddScoutingPoints(team.rewardScouting);
            progress.rewardClaimed = true; _teams[teamId] = progress;
            return true;
        }

        public LegendaryPracticeProgress[] Capture()
        {
            var ids = new List<string>(_teams.Keys); ids.Sort(StringComparer.Ordinal);
            var result = new LegendaryPracticeProgress[ids.Count];
            for (int i = 0; i < ids.Count; i++) result[i] = Get(ids[i]);
            return result;
        }

        public static LegendaryPracticeState Restore(LegendaryPracticeProgress[] data)
        {
            var state = new LegendaryPracticeState();
            foreach (var entry in data ?? Array.Empty<LegendaryPracticeProgress>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.teamId) || entry.wins < 0 || entry.wins > 3 ||
                    entry.attempts < entry.wins + (long)entry.losses + entry.draws || entry.losses < 0 || entry.draws < 0 ||
                    entry.runStartAttempt < 0 || entry.runStartAttempt > entry.attempts ||
                    entry.wins > entry.attempts - entry.runStartAttempt ||
                    (entry.rewardClaimed && !entry.HasCleared) || state._teams.ContainsKey(entry.teamId))
                    throw new ArgumentException("역대 강팀 진행 데이터가 올바르지 않습니다.");
                state._teams.Add(entry.teamId, entry.Copy());
            }
            return state;
        }
    }

    public sealed partial class ManagerHistoricalRuntimeState
    {
        public LegendaryPracticeState LegendaryPractice { get; private set; } = new LegendaryPracticeState();
        public void RestoreLegendaryPractice(LegendaryPracticeProgress[] data) =>
            LegendaryPractice = LegendaryPracticeState.Restore(data);
    }
}
