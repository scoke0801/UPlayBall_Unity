using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Baseball.Game.Historical
{
    /// <summary>오프라인 상세 경기로 확정한 역사 팀 순위와 최초 보상이다.</summary>
    [Serializable]
    public sealed class LegendaryPracticeTeam
    {
        public string challengeTeamId, teamSeasonKey, rosterHash;
        public int rank, year, wins, losses, draws, runsScored, runsAllowed;
        public long rewardMoney;
        public int rewardDevelopment, rewardScouting;
        public int historicalWins, historicalLosses;
        public double HistoricalWinRate => historicalWins + historicalLosses == 0 ? 0d :
            (double)historicalWins / (historicalWins + historicalLosses);
        public int Games => wins + losses + draws;
        public double WinRate => wins + losses == 0 ? 0d : (double)wins / (wins + losses);
    }

    /// <summary>런타임 정렬 없이 순위·내용 해시를 검사하는 읽기 전용 콘텐츠 경계다.</summary>
    [Serializable]
    public sealed class LegendaryPracticeCatalog
    {
        public string simulationVersion, contentHash, dataHash;
        public ulong seed;
        public int candidateCount, gamesPerCandidate;
        public double historicalWinRateWeight;
        public LegendaryPracticeTeam[] teams;
        public string[] candidateTeamSeasonKeys;

        /// <summary>성장·경제 등 경기 외 설정의 해시와 독립적으로 공통 경기 자산을 식별한다.</summary>
        public static string CreateSimulationVersion(string miniGameJson, string ratingCurveJson) =>
            "practice-v6:" + Baseball.Core.Rules.SimulationVersionStamp.CurrentEngineVersion + ":" +
            Hash(miniGameJson) + ":" + Hash(ratingCurveJson);

        public LegendaryPracticeTeam Find(string id)
        {
            foreach (var team in teams) if (team.challengeTeamId == id) return team;
            throw new InvalidOperationException("선택한 역사 팀을 찾을 수 없습니다.");
        }

        public void Validate(int expectedCount = 100)
        {
            if (teams == null || teams.Length != expectedCount || string.IsNullOrWhiteSpace(simulationVersion) ||
                string.IsNullOrWhiteSpace(contentHash) || gamesPerCandidate < 1000 || candidateCount < teams.Length ||
                double.IsNaN(historicalWinRateWeight) || historicalWinRateWeight <= 0.5d || historicalWinRateWeight >= 1d)
                throw new InvalidOperationException("역대 강팀 콘텐츠 구성이 올바르지 않습니다.");
            if (candidateTeamSeasonKeys == null || candidateTeamSeasonKeys.Length != candidateCount)
                throw new InvalidOperationException("전체 역사 팀의 Bake 참가 명단이 필요합니다.");
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in candidateTeamSeasonKeys)
                if (string.IsNullOrWhiteSpace(key) || !candidates.Add(key))
                    throw new InvalidOperationException("Bake 참가 팀이 비어 있거나 중복되었습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < teams.Length; i++)
            {
                var team = teams[i];
                if (team == null || team.rank != i + 1 || string.IsNullOrWhiteSpace(team.challengeTeamId) ||
                    !ids.Add(team.challengeTeamId) || !candidates.Contains(team.teamSeasonKey) ||
                    string.IsNullOrWhiteSpace(team.rosterHash) || team.Games != gamesPerCandidate ||
                    team.wins < 0 || team.losses < 0 || team.draws < 0 || team.year < 1 ||
                    team.historicalWins < 0 || team.historicalLosses < 0 || team.historicalWins + team.historicalLosses <= 0 ||
                    team.runsScored < 0 || team.runsAllowed < 0 || team.rewardMoney < 0 ||
                    team.rewardDevelopment < 0 || team.rewardScouting < 0 ||
                    (i > 0 && Compare(teams[i - 1], team) > 0))
                    throw new InvalidOperationException("역대 강팀 순위 또는 경기 표본이 올바르지 않습니다.");
            }
            if (!string.Equals(dataHash, CalculateHash(), StringComparison.Ordinal))
                throw new InvalidOperationException("역대 강팀 콘텐츠 검증에 실패했습니다.");
        }

        /// <summary>순위에 들지 못한 팀도 정본의 모든 팀과 함께 Bake에 참가했는지 검사한다.</summary>
        public void ValidateCoverage(IReadOnlyList<Baseball.Core.Historical.TeamSeasonDefinition> expectedTeams)
        {
            Validate();
            var remaining = new HashSet<string>(candidateTeamSeasonKeys, StringComparer.Ordinal);
            foreach (var team in expectedTeams)
                if (!remaining.Remove(team.TeamSeasonKey))
                    throw new InvalidOperationException("Bake에서 역사 팀이 누락되었거나 정본이 중복되었습니다: " + team.TeamSeasonKey);
            if (remaining.Count != 0)
                throw new InvalidOperationException("Bake에 정본에 없는 역사 팀이 있습니다.");
        }

        /// <summary>역사 승률을 주축으로 상세 경기 평가를 보조 반영한다. 두 승률 모두 무승부를 제외한다.</summary>
        public double GetRankingScore(LegendaryPracticeTeam team) =>
            historicalWinRateWeight * team.HistoricalWinRate + (1d - historicalWinRateWeight) * team.WinRate;

        public int Compare(LegendaryPracticeTeam a, LegendaryPracticeTeam b)
        {
            int order = GetRankingScore(b).CompareTo(GetRankingScore(a));
            if (order != 0) return order;
            order = b.HistoricalWinRate.CompareTo(a.HistoricalWinRate);
            if (order != 0) return order;
            order = b.WinRate.CompareTo(a.WinRate);
            if (order != 0) return order;
            order = b.Games.CompareTo(a.Games); if (order != 0) return order;
            order = ((double)(b.runsScored - b.runsAllowed) / b.Games)
                .CompareTo((double)(a.runsScored - a.runsAllowed) / a.Games);
            return order != 0 ? order : string.CompareOrdinal(a.challengeTeamId, b.challengeTeamId);
        }

        public string CalculateHash()
        {
            var text = new StringBuilder();
            text.Append(simulationVersion).Append('|').Append(contentHash).Append('|').Append(seed.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(candidateCount).Append('|').Append(gamesPerCandidate)
                .Append('|').Append(historicalWinRateWeight.ToString("R", CultureInfo.InvariantCulture));
            if (candidateTeamSeasonKeys != null)
                foreach (string key in candidateTeamSeasonKeys) text.Append('\n').Append("candidate:").Append(key);
            foreach (var t in teams)
                text.Append('\n').Append(t.challengeTeamId).Append('|').Append(t.teamSeasonKey).Append('|').Append(t.rosterHash)
                    .Append('|').Append(t.rank).Append('|').Append(t.year).Append('|').Append(t.wins).Append('|').Append(t.losses)
                    .Append('|').Append(t.draws).Append('|').Append(t.runsScored).Append('|').Append(t.runsAllowed)
                    .Append('|').Append(t.rewardMoney).Append('|').Append(t.rewardDevelopment).Append('|').Append(t.rewardScouting)
                    .Append('|').Append(t.historicalWins).Append('|').Append(t.historicalLosses);
            return Hash(text.ToString());
        }

        public static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
    }
}
