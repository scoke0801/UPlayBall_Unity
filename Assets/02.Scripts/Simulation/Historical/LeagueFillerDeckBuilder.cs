using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>
    /// CPU 임시 구단의 25인 로스터를 덱 종류에 맞는 카드로 결정론적으로 구성한다.
    /// 특수 덱은 약한 역사 구단을 바탕으로 목표 평균 Cost를 넘지 않을 때까지 해당 Edition 선수로 약한 자리부터 교체한다.
    /// 특수 카드만으로 채우면 가장 약한 덱도 역사상 최강 구단보다 강해져(평균 Cost 8.3~9.9 대 최고 7.2)
    /// 상위 등급 순위표를 CPU가 독점하므로, 덱의 정체성은 스타 선수로 살리고 전체 강도는 등급 목표에 맞춘다.
    /// </summary>
    public sealed class LeagueFillerDeckBuilder
    {
        private static readonly ActiveRosterRole[] RoleOrder =
        {
            // 후보가 적은 수비 포지션을 먼저 채워야 같은 인물이 흔한 포지션에 먼저 쓰여 버리지 않는다.
            ActiveRosterRole.StartingCatcher,
            ActiveRosterRole.StartingShortstop,
            ActiveRosterRole.StartingSecondBase,
            ActiveRosterRole.StartingCenterField,
            ActiveRosterRole.StartingThirdBase,
            ActiveRosterRole.StartingRightField,
            ActiveRosterRole.StartingLeftField,
            ActiveRosterRole.StartingFirstBase,
            ActiveRosterRole.StartingDesignatedHitter,
            ActiveRosterRole.Closer,
            ActiveRosterRole.Setup,
            ActiveRosterRole.StartingPitcher1,
            ActiveRosterRole.StartingPitcher2,
            ActiveRosterRole.StartingPitcher3,
            ActiveRosterRole.StartingPitcher4,
            ActiveRosterRole.StartingPitcher5,
            ActiveRosterRole.Bullpen1,
            ActiveRosterRole.Bullpen2,
            ActiveRosterRole.Bullpen3,
            ActiveRosterRole.Bullpen4
        };

        /// <summary>
        /// 역할 재배치 순서다. 선발 라인업과 로테이션을 먼저 확정해야 강한 선발 투수가 마무리·셋업 자리로 밀리지 않는다.
        /// 선수를 고르는 순서(RoleOrder)와 달리 여기서는 이미 고른 25명의 배치만 정한다.
        /// </summary>
        private static readonly ActiveRosterRole[] NormalizeOrder =
        {
            ActiveRosterRole.StartingCatcher,
            ActiveRosterRole.StartingShortstop,
            ActiveRosterRole.StartingSecondBase,
            ActiveRosterRole.StartingCenterField,
            ActiveRosterRole.StartingThirdBase,
            ActiveRosterRole.StartingRightField,
            ActiveRosterRole.StartingLeftField,
            ActiveRosterRole.StartingFirstBase,
            ActiveRosterRole.StartingPitcher1,
            ActiveRosterRole.StartingPitcher2,
            ActiveRosterRole.StartingPitcher3,
            ActiveRosterRole.StartingPitcher4,
            ActiveRosterRole.StartingPitcher5,
            ActiveRosterRole.StartingDesignatedHitter,
            ActiveRosterRole.Closer,
            ActiveRosterRole.Setup,
            ActiveRosterRole.Bullpen1,
            ActiveRosterRole.Bullpen2,
            ActiveRosterRole.Bullpen3,
            ActiveRosterRole.Bullpen4
        };

        private readonly Dictionary<PlayerCardEdition, Candidate[]> _editionPools =
            new Dictionary<PlayerCardEdition, Candidate[]>();
        private readonly Dictionary<string, Candidate[]> _yearTeamPools =
            new Dictionary<string, Candidate[]>(StringComparer.Ordinal);
        private readonly Candidate[] _normalPool;
        private readonly string[] _yearTeamSeasonKeys;
        private readonly Dictionary<string, double> _yearTeamAverageCosts = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly ActiveRosterValidator _validator = new ActiveRosterValidator();

        public LeagueFillerDeckBuilder(WorldCardCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var byEdition = new Dictionary<PlayerCardEdition, List<Candidate>>();
            var byYearTeam = new Dictionary<string, List<Candidate>>(StringComparer.Ordinal);
            var normal = new List<Candidate>();
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                var candidate = new Candidate(card, catalog.GetPlayerSeason(card));
                if (card.Edition == PlayerCardEdition.Normal)
                {
                    normal.Add(candidate);
                    if (!byYearTeam.TryGetValue(candidate.Season.OriginTeamSeasonKey, out List<Candidate> team))
                        byYearTeam.Add(candidate.Season.OriginTeamSeasonKey, team = new List<Candidate>());
                    team.Add(candidate);
                    continue;
                }
                if (!byEdition.TryGetValue(card.Edition, out List<Candidate> pool))
                    byEdition.Add(card.Edition, pool = new List<Candidate>());
                pool.Add(candidate);
            }

            foreach (var pair in byEdition) _editionPools.Add(pair.Key, SortById(pair.Value));
            foreach (var pair in byYearTeam) _yearTeamPools.Add(pair.Key, SortById(pair.Value));
            _normalPool = SortById(normal);
            _yearTeamSeasonKeys = new string[_yearTeamPools.Count];
            _yearTeamPools.Keys.CopyTo(_yearTeamSeasonKeys, 0);
            Array.Sort(_yearTeamSeasonKeys, StringComparer.Ordinal);
            // 생성 뒤에는 읽기만 하도록 바탕 구단 강도를 미리 계산한다.
            foreach (string key in _yearTeamSeasonKeys)
            {
                int total = 0;
                List<Pick> picks = SelectRoster(CreateYearTeamTiers(key));
                foreach (Pick pick in picks) total += pick.Candidate.Season.Cost;
                _yearTeamAverageCosts.Add(key, total / (double)picks.Count);
            }
        }

        /// <summary>연도 구단 덱의 원본이 될 수 있는 모든 역사 구단 TeamSeasonKey다. 순서는 Ordinal로 고정된다.</summary>
        public IReadOnlyList<string> YearTeamSeasonKeys => _yearTeamSeasonKeys;

        /// <summary>
        /// Key에 기록된 덱 종류로 25인 로스터를 만든다. 같은 입력과 같은 난수 상태면 항상 같은 로스터다.
        /// <paramref name="targetAverageCost"/>가 0 이하이면 특수 덱을 해당 Edition만으로 채운다.
        /// <paramref name="starCostMargin"/>은 바탕 구단이 목표보다 최소 이만큼 약해야 한다는 조건으로, 스타가 들어갈 자리를 확보한다.
        /// </summary>
        public CurrentRosterState Build(string teamSeasonKey, double targetAverageCost, double starCostMargin, IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (double.IsNaN(targetAverageCost) || double.IsNaN(starCostMargin) || starCostMargin < 0d)
                throw new ArgumentOutOfRangeException(nameof(targetAverageCost));
            if (!LeagueFillerTeamKey.TryParse(teamSeasonKey, out LeagueFillerDeckType deck, out string source))
                throw new ArgumentException("CPU 임시 구단 Key가 아닙니다.", nameof(teamSeasonKey));

            List<Pick> picks;
            if (deck == LeagueFillerDeckType.YearTeam)
                picks = SelectRoster(CreateYearTeamTiers(source));
            else if (targetAverageCost <= 0d)
                picks = SelectRoster(CreateEditionTiers(deck, random));
            else
                picks = BuildStarDeck(deck, targetAverageCost, starCostMargin, random);
            return CreateRoster(teamSeasonKey, NormalizeRoles(picks));
        }

        /// <summary>바탕 구단을 고르고 목표 평균 Cost 안에서 해당 Edition 선수로 가장 약한 자리부터 교체한다.</summary>
        private List<Pick> BuildStarDeck(LeagueFillerDeckType deck, double targetAverageCost, double starCostMargin,
            IRandomSource random)
        {
            string baseTeam = PickBaseTeam(targetAverageCost - starCostMargin, random);
            List<Pick> picks = SelectRoster(CreateYearTeamTiers(baseTeam));
            if (!_editionPools.TryGetValue(ToEdition(deck), out Candidate[] pool)) return picks;

            // 특수 덱은 매 시즌 같은 명단이 반복되지 않도록 난수 순서로 스타 후보를 본다.
            var order = new int[pool.Length];
            var keys = new double[pool.Length];
            for (int index = 0; index < pool.Length; index++)
            {
                order[index] = index;
                keys[index] = random.NextDouble();
            }
            Array.Sort(keys, order);

            // 부동소수 누적 오차 없이 비교하려고 Cost 합계를 정수로 관리한다.
            int budget = (int)Math.Floor(targetAverageCost * ActiveRosterCompositionRule.ActiveRosterSize + 1e-9);
            int total = 0;
            foreach (Pick pick in picks) total += pick.Candidate.Season.Cost;
            foreach (int poolIndex in order)
            {
                Candidate star = pool[poolIndex];
                int slot = FindUpgradeSlot(picks, star, budget - total);
                if (slot < 0) continue;
                total += star.Season.Cost - picks[slot].Candidate.Season.Cost;
                picks[slot] = new Pick(picks[slot].Role, star);
            }
            return picks;
        }

        /// <summary>교체 후에도 예산 안이고 원래 포지션 규칙을 지키는 자리 중 현재 가장 약한 자리를 고른다.</summary>
        private static int FindUpgradeSlot(List<Pick> picks, Candidate star, int remainingBudget)
        {
            int foreignCount = 0;
            for (int index = 0; index < picks.Count; index++)
            {
                PlayerSeasonDefinition season = picks[index].Candidate.Season;
                if (season.PlayerPersonId == star.Season.PlayerPersonId) return -1;
                if (season.RegistrationType == RegistrationType.Foreign) foreignCount++;
            }

            int best = -1;
            for (int index = 0; index < picks.Count; index++)
            {
                PlayerSeasonDefinition occupant = picks[index].Candidate.Season;
                int gain = star.Season.Cost - occupant.Cost;
                if (gain <= 0 || gain > remainingBudget) continue;
                if (GetEligibleFit(star.Season, picks[index].Role) < 0) continue;
                bool addsForeign = star.Season.RegistrationType == RegistrationType.Foreign &&
                                   occupant.RegistrationType != RegistrationType.Foreign;
                if (addsForeign && foreignCount >= ActiveRosterCompositionRule.MaxForeignPlayers) continue;
                if (best < 0 || IsBetterUpgradeSlot(picks, index, best)) best = index;
            }
            return best;
        }

        /// <summary>예산이 한정되어 있으므로 경기 영향이 큰 자리부터 스타로 채우고, 같은 중요도면 가장 약한 자리를 고른다.</summary>
        private static bool IsBetterUpgradeSlot(List<Pick> picks, int index, int best)
        {
            int priority = GetSlotPriority(picks[index].Role);
            int bestPriority = GetSlotPriority(picks[best].Role);
            if (priority != bestPriority) return priority < bestPriority;
            return picks[index].Candidate.Season.Cost < picks[best].Candidate.Season.Cost;
        }

        /// <summary>선발 라인업과 선발 로테이션이 0, 마무리·셋업이 1, 나머지 불펜이 2, 벤치가 3이다.</summary>
        private static int GetSlotPriority(ActiveRosterRole role)
        {
            if (role == ActiveRosterRole.BenchHitter) return 3;
            if (role == ActiveRosterRole.Bullpen1 || role == ActiveRosterRole.Bullpen2 ||
                role == ActiveRosterRole.Bullpen3 || role == ActiveRosterRole.Bullpen4) return 2;
            if (role == ActiveRosterRole.Setup || role == ActiveRosterRole.Closer) return 1;
            return 0;
        }

        /// <summary>
        /// 고른 25명 안에서 역할만 다시 배치한다. 스타를 교체로 밀어 넣은 뒤 그대로 두면 EX 선발 투수가 불펜에,
        /// 강한 타자가 벤치에 남는다. 중요한 자리부터 적합도·Cost 순으로 채워 실제 감독이 짤 법한 배치로 만든다.
        /// </summary>
        private static List<Pick> NormalizeRoles(List<Pick> picks)
        {
            var pool = new List<Candidate>(picks.Count);
            foreach (Pick pick in picks) pool.Add(pick.Candidate);
            var result = new List<Pick>(picks.Count);
            foreach (ActiveRosterRole role in NormalizeOrder) result.Add(new Pick(role, TakeBestForRole(pool, role)));
            for (int index = 0; index < ActiveRosterCompositionRule.BenchHitterCount; index++)
                result.Add(new Pick(ActiveRosterRole.BenchHitter, TakeBestForRole(pool, ActiveRosterRole.BenchHitter)));
            return result;
        }

        private static Candidate TakeBestForRole(List<Candidate> pool, ActiveRosterRole role)
        {
            int best = -1;
            int bestFit = -1;
            for (int index = 0; index < pool.Count; index++)
            {
                int fit = GetEligibleFit(pool[index].Season, role);
                if (fit < 0) continue;
                if (best < 0 || fit > bestFit ||
                    (fit == bestFit && IsStrongerForRole(pool[index], pool[best])))
                {
                    best = index;
                    bestFit = fit;
                }
            }
            if (best < 0) throw new InvalidOperationException($"CPU 임시 구단의 {role} 역할에 배치할 선수가 없습니다.");
            Candidate chosen = pool[best];
            pool.RemoveAt(best);
            return chosen;
        }

        private static bool IsStrongerForRole(Candidate candidate, Candidate best)
        {
            int costOrder = candidate.Season.Cost.CompareTo(best.Season.Cost);
            if (costOrder != 0) return costOrder > 0;
            // Cost가 같으면 능력치 보정이 붙은 카드가 실제로 더 강하다. 레전드가 같은 Cost의 Normal에 밀리지 않게 한다.
            int modifierOrder = GetModifierTotal(candidate).CompareTo(GetModifierTotal(best));
            return modifierOrder != 0
                ? modifierOrder > 0
                : string.CompareOrdinal(candidate.Card.CardId, best.Card.CardId) < 0;
        }

        private static int GetModifierTotal(Candidate candidate)
        {
            int total = 0;
            for (int ability = 0; ability < PlayerAbilityCatalog.AbilityCount; ability++)
                total += candidate.Card.GetModifier((PlayerAbility)ability);
            return total;
        }

        /// <summary>목표 상한 이하의 연도 구단 중 하나를 고른다. 없으면 가장 약한 연도 구단을 쓴다.</summary>
        private string PickBaseTeam(double maximumAverageCost, IRandomSource random)
        {
            var eligible = new List<string>();
            string weakest = null;
            double weakestCost = double.MaxValue;
            foreach (string key in _yearTeamSeasonKeys)
            {
                double cost = _yearTeamAverageCosts[key];
                if (cost <= maximumAverageCost) eligible.Add(key);
                if (cost < weakestCost) { weakestCost = cost; weakest = key; }
            }
            if (eligible.Count == 0) return weakest;
            return eligible[(int)(random.NextDouble() * eligible.Count)];
        }

        private Tier[] CreateYearTeamTiers(string sourceTeamSeasonKey)
        {
            if (!_yearTeamPools.TryGetValue(sourceTeamSeasonKey, out Candidate[] team))
                throw new ArgumentException($"원본 연도 구단 {sourceTeamSeasonKey}의 카드가 없습니다.");
            // 연도 구단은 실제 그 해 로스터를 재현하는 것이 목적이므로 난수보다 Cost 높은 선수를 먼저 쓴다.
            return new[] { new Tier(team, null), new Tier(_normalPool, null) };
        }

        /// <summary>
        /// 덱 등급 → 더 낮은 특수 등급(내림차순) → 더 높은 특수 등급(오름차순) → 전체 Normal 순으로 대체한다.
        /// 낮은 등급을 먼저 보는 이유는 빈 포지션 하나 때문에 덱 등급보다 강한 선수가 섞이는 것을 줄이기 위해서다.
        /// </summary>
        private Tier[] CreateEditionTiers(LeagueFillerDeckType deck, IRandomSource random)
        {
            var tiers = new List<Tier>();
            int target = (int)deck;
            for (int value = target; value > (int)LeagueFillerDeckType.YearTeam; value--)
                AddEditionTier(tiers, (LeagueFillerDeckType)value, random);
            for (int value = target + 1; value <= (int)LeagueFillerDeckType.Legend; value++)
                AddEditionTier(tiers, (LeagueFillerDeckType)value, random);
            tiers.Add(new Tier(_normalPool, null));
            return tiers.ToArray();
        }

        private void AddEditionTier(List<Tier> tiers, LeagueFillerDeckType deck, IRandomSource random)
        {
            if (!_editionPools.TryGetValue(ToEdition(deck), out Candidate[] pool)) return;
            var order = new double[pool.Length];
            for (int index = 0; index < order.Length; index++) order[index] = random.NextDouble();
            tiers.Add(new Tier(pool, order));
        }

        private static List<Pick> SelectRoster(Tier[] tiers)
        {
            var picks = new List<Pick>(ActiveRosterCompositionRule.ActiveRosterSize);
            var persons = new HashSet<string>(StringComparer.Ordinal);
            int foreignCount = 0;
            foreach (ActiveRosterRole role in RoleOrder)
                picks.Add(Select(role, tiers, persons, ref foreignCount));
            for (int index = 0; index < ActiveRosterCompositionRule.BenchHitterCount; index++)
                picks.Add(Select(ActiveRosterRole.BenchHitter, tiers, persons, ref foreignCount));
            return picks;
        }

        private static Pick Select(ActiveRosterRole role, Tier[] tiers, HashSet<string> persons, ref int foreignCount)
        {
            for (int tierIndex = 0; tierIndex < tiers.Length; tierIndex++)
            {
                Tier tier = tiers[tierIndex];
                int best = -1;
                int bestFit = -1;
                for (int index = 0; index < tier.Pool.Length; index++)
                {
                    PlayerSeasonDefinition season = tier.Pool[index].Season;
                    if (persons.Contains(season.PlayerPersonId) ||
                        (season.RegistrationType == RegistrationType.Foreign &&
                         foreignCount >= ActiveRosterCompositionRule.MaxForeignPlayers))
                        continue;
                    int fit = GetEligibleFit(season, role);
                    if (fit < 0) continue;
                    if (best < 0 || IsBetter(tier, index, fit, best, bestFit))
                    {
                        best = index;
                        bestFit = fit;
                    }
                }
                if (best < 0) continue;

                PlayerSeasonDefinition chosen = tier.Pool[best].Season;
                persons.Add(chosen.PlayerPersonId);
                if (chosen.RegistrationType == RegistrationType.Foreign) foreignCount++;
                return new Pick(role, tier.Pool[best]);
            }
            throw new InvalidOperationException($"CPU 임시 구단의 {role} 역할을 채울 선수가 없습니다.");
        }

        private CurrentRosterState CreateRoster(string teamSeasonKey, List<Pick> picks)
        {
            var entries = new List<ActiveRosterEntry>(picks.Count);
            foreach (Pick pick in picks)
            {
                PlayerSeasonDefinition season = pick.Candidate.Season;
                entries.Add(new ActiveRosterEntry(pick.Candidate.Card.CardId, season.PlayerSeasonId,
                    season.PlayerPersonId, season.RegistrationType, pick.Role));
            }
            entries.Sort((left, right) =>
            {
                int order = left.Role.CompareTo(right.Role);
                return order != 0 ? order : string.CompareOrdinal(left.CardId, right.CardId);
            });
            var roster = new CurrentRosterState(teamSeasonKey, entries);
            RosterValidationResult validation = _validator.Validate(roster);
            if (!validation.IsValid)
                throw new InvalidOperationException($"CPU 임시 구단 로스터가 1군 규칙을 위반했습니다: {validation.Issues[0].Code}");
            return roster;
        }

        /// <summary>
        /// 선발 야수는 원래 포지션 선수만 받는다. 투수는 보직이 달라도 받는다 — 특수 카드 투수 대부분이 선발이라
        /// 불펜을 막으면 특수 덱의 불펜에 해당 Edition 선수가 들어갈 수 없다.
        /// </summary>
        private static int GetEligibleFit(PlayerSeasonDefinition season, ActiveRosterRole role)
        {
            if (!SpecialCompositeTeamBuilder.CanFillRole(season, role)) return -1;
            // 지명타자는 수비를 보지 않으므로 전업 DH 우대 없이 남은 타자 중 가장 좋은 타자를 세운다.
            if (role == ActiveRosterRole.StartingDesignatedHitter) return 1;
            int fit = SpecialCompositeTeamBuilder.GetRoleFit(season, role);
            // 공용 적합도는 불펜 슬롯에만 구원투수 가산점을 준다. 마무리·셋업도 구원 보직이므로 같게 본다.
            if (fit == 0 && (role == ActiveRosterRole.Setup || role == ActiveRosterRole.Closer) &&
                season.Position == PlayerPosition.ReliefPitcher)
                fit = 1;
            bool isFieldingStarter = ActiveRosterCompositionRule.Standard.IsStartingHitterRole(role);
            return isFieldingStarter && fit == 0 ? -1 : fit;
        }

        private static bool IsBetter(Tier tier, int index, int fit, int best, int bestFit)
        {
            if (fit != bestFit) return fit > bestFit;
            if (tier.RandomOrder != null)
            {
                int randomOrder = tier.RandomOrder[index].CompareTo(tier.RandomOrder[best]);
                if (randomOrder != 0) return randomOrder < 0;
            }
            else
            {
                int costOrder = tier.Pool[index].Season.Cost.CompareTo(tier.Pool[best].Season.Cost);
                if (costOrder != 0) return costOrder > 0;
            }
            return string.CompareOrdinal(tier.Pool[index].Card.CardId, tier.Pool[best].Card.CardId) < 0;
        }

        private static PlayerCardEdition ToEdition(LeagueFillerDeckType deck)
        {
            return deck switch
            {
                LeagueFillerDeckType.Ex => PlayerCardEdition.Ex,
                LeagueFillerDeckType.AllStar => PlayerCardEdition.AllStar,
                LeagueFillerDeckType.Mvp => PlayerCardEdition.Mvp,
                LeagueFillerDeckType.CareerHigh => PlayerCardEdition.CareerHigh,
                LeagueFillerDeckType.Legend => PlayerCardEdition.Legend,
                _ => throw new ArgumentOutOfRangeException(nameof(deck))
            };
        }

        private static Candidate[] SortById(List<Candidate> source)
        {
            source.Sort((left, right) => string.CompareOrdinal(left.Card.CardId, right.Card.CardId));
            return source.ToArray();
        }

        private readonly struct Candidate
        {
            public Candidate(PlayerCardDefinition card, PlayerSeasonDefinition season)
            {
                Card = card;
                Season = season;
            }

            public PlayerCardDefinition Card { get; }
            public PlayerSeasonDefinition Season { get; }
        }

        private readonly struct Pick
        {
            public Pick(ActiveRosterRole role, Candidate candidate)
            {
                Role = role;
                Candidate = candidate;
            }

            public ActiveRosterRole Role { get; }
            public Candidate Candidate { get; }
        }

        private readonly struct Tier
        {
            public Tier(Candidate[] pool, double[] randomOrder)
            {
                Pool = pool;
                RandomOrder = randomOrder;
            }

            public Candidate[] Pool { get; }
            public double[] RandomOrder { get; }
        }
    }
}
