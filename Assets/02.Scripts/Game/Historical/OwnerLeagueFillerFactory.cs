using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Game.Historical
{
    /// <summary>
    /// 추첨한 조의 빈 자리를 등급별 덱의 CPU 임시 구단으로 채워 항상 목표 구단 수 체제를 만든다.
    /// 생성한 로스터와 TeamId는 호출자가 넘긴 월드 목록에 바로 등록한다.
    /// </summary>
    internal sealed class OwnerLeagueFillerFactory
    {
        private const ulong FillerStream = 0x46494C4C45524445UL;
        private readonly LeagueDefinition _rules;
        private readonly WorldCardCatalog _catalog;
        private readonly ulong _worldSeed;
        private readonly List<CurrentRosterState> _rosters;
        private readonly Dictionary<string, ManagerTeamReference> _references;
        private LeagueFillerDeckBuilder _builder;
        private int _nextTeamId;

        public OwnerLeagueFillerFactory(LeagueDefinition rules, WorldCardCatalog catalog, ulong worldSeed,
            List<CurrentRosterState> rosters, Dictionary<string, ManagerTeamReference> references, int nextTeamId)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _worldSeed = worldSeed;
            _rosters = rosters ?? throw new ArgumentNullException(nameof(rosters));
            _references = references ?? throw new ArgumentNullException(nameof(references));
            if (nextTeamId <= 0) throw new ArgumentOutOfRangeException(nameof(nextTeamId));
            _nextTeamId = nextTeamId;
        }

        /// <summary>실제 구단 뒤에 모자란 수만큼 CPU 임시 구단 Key를 붙여 반환한다.</summary>
        public string[] Fill(LeagueGrade grade, int groupIndex, int seasonNumber, string[] regularKeys)
        {
            if (regularKeys == null) throw new ArgumentNullException(nameof(regularKeys));
            int missing = _rules.GroupTeamCount - regularKeys.Length;
            if (missing <= 0) return regularKeys;

            _builder ??= new LeagueFillerDeckBuilder(_catalog);
            OwnerLeagueRankRule rule = _rules.GetRankRule(grade);
            LeagueFillerDeckType deck = rule.FillerDeck;
            ulong seed = DeterministicSeed.Derive(DeterministicSeed.Derive(_worldSeed, FillerStream),
                ((ulong)(uint)seasonNumber << 32) | ((ulong)(uint)grade << 24) | (uint)groupIndex);
            var random = new Pcg32Random(seed);
            // 같은 조에 실제 구단과 같은 연도 구단 덱, 또는 같은 원본 덱이 둘 나오면 이름만 보고 구분할 수 없다.
            var usedSources = new HashSet<string>(regularKeys, StringComparer.Ordinal);

            var result = new string[_rules.GroupTeamCount];
            Array.Copy(regularKeys, result, regularKeys.Length);
            for (int slot = 0; slot < missing; slot++)
            {
                string source = deck == LeagueFillerDeckType.YearTeam ? PickYearTeamSource(random, usedSources) : null;
                string key = LeagueFillerTeamKey.Create(seasonNumber, grade, groupIndex, slot, deck, source);
                _rosters.Add(_builder.Build(key, rule.FillerTargetCost, _rules.FillerStarCostMargin, random));
                _references.Add(key, new ManagerTeamReference(_nextTeamId, key));
                _nextTeamId = checked(_nextTeamId + 1);
                result[regularKeys.Length + slot] = key;
            }
            return result;
        }

        private string PickYearTeamSource(IRandomSource random, HashSet<string> usedSources)
        {
            IReadOnlyList<string> sources = _builder.YearTeamSeasonKeys;
            if (sources.Count == 0) throw new InvalidOperationException("연도 구단 덱을 만들 역사 구단이 없습니다.");
            int start = (int)(random.NextDouble() * sources.Count);
            // 난수로 시작점을 고르고 이미 쓴 원본은 순서대로 건너뛴다. 원본이 조 크기보다 적으면 중복을 허용한다.
            for (int offset = 0; offset < sources.Count; offset++)
            {
                string candidate = sources[(start + offset) % sources.Count];
                if (usedSources.Add(candidate)) return candidate;
            }
            return sources[start];
        }
    }
}
