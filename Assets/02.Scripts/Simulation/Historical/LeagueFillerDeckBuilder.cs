using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>
    /// CPU 임시 구단의 25인 로스터를 덱 종류에 맞는 카드로 결정론적으로 구성한다.
    /// 특수 카드 풀은 포지션이 편중되어 있으므로(EX 포수 0명, 레전드 유격수 2명) 선발 야수는 원래 포지션만 받고,
    /// 덱 등급에 맞는 선수가 없으면 가까운 등급 카드부터 대체 후보를 찾는다.
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

        private readonly Dictionary<PlayerCardEdition, Candidate[]> _editionPools =
            new Dictionary<PlayerCardEdition, Candidate[]>();
        private readonly Dictionary<string, Candidate[]> _yearTeamPools =
            new Dictionary<string, Candidate[]>(StringComparer.Ordinal);
        private readonly Candidate[] _normalPool;
        private readonly string[] _yearTeamSeasonKeys;
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
        }

        /// <summary>연도 구단 덱의 원본이 될 수 있는 모든 역사 구단 TeamSeasonKey다. 순서는 Ordinal로 고정된다.</summary>
        public IReadOnlyList<string> YearTeamSeasonKeys => _yearTeamSeasonKeys;

        /// <summary>Key에 기록된 덱 종류로 25인 로스터를 만든다. 같은 Key와 같은 난수 상태면 항상 같은 로스터다.</summary>
        public CurrentRosterState Build(string teamSeasonKey, IRandomSource random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (!LeagueFillerTeamKey.TryParse(teamSeasonKey, out LeagueFillerDeckType deck, out string source))
                throw new ArgumentException("CPU 임시 구단 Key가 아닙니다.", nameof(teamSeasonKey));

            Tier[] tiers = CreateTiers(deck, source, random);
            var selected = new List<ActiveRosterEntry>(ActiveRosterCompositionRule.ActiveRosterSize);
            var persons = new HashSet<string>(StringComparer.Ordinal);
            int foreignCount = 0;
            foreach (ActiveRosterRole role in RoleOrder)
                Select(role, tiers, selected, persons, ref foreignCount);
            for (int index = 0; index < ActiveRosterCompositionRule.BenchHitterCount; index++)
                Select(ActiveRosterRole.BenchHitter, tiers, selected, persons, ref foreignCount);

            selected.Sort((left, right) =>
            {
                int order = left.Role.CompareTo(right.Role);
                return order != 0 ? order : string.CompareOrdinal(left.CardId, right.CardId);
            });
            var roster = new CurrentRosterState(teamSeasonKey, selected);
            RosterValidationResult validation = _validator.Validate(roster);
            if (!validation.IsValid)
                throw new InvalidOperationException($"CPU 임시 구단 로스터가 1군 규칙을 위반했습니다: {validation.Issues[0].Code}");
            return roster;
        }

        /// <summary>
        /// 덱 등급 → 더 낮은 특수 등급(내림차순) → 더 높은 특수 등급(오름차순) → 전체 Normal 순으로 대체한다.
        /// 낮은 등급을 먼저 보는 이유는 빈 포지션 하나 때문에 덱 등급보다 강한 선수가 섞이는 것을 줄이기 위해서다.
        /// </summary>
        private Tier[] CreateTiers(LeagueFillerDeckType deck, string sourceTeamSeasonKey, IRandomSource random)
        {
            var tiers = new List<Tier>();
            if (deck == LeagueFillerDeckType.YearTeam)
            {
                if (!_yearTeamPools.TryGetValue(sourceTeamSeasonKey, out Candidate[] team))
                    throw new ArgumentException($"원본 연도 구단 {sourceTeamSeasonKey}의 카드가 없습니다.");
                // 연도 구단은 실제 그 해 로스터를 재현하는 것이 목적이므로 난수보다 Cost 높은 선수를 먼저 쓴다.
                tiers.Add(new Tier(team, null));
                tiers.Add(new Tier(_normalPool, null));
                return tiers.ToArray();
            }

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
            // 특수 덱은 매 시즌 같은 명단이 반복되지 않도록 적합도가 같으면 난수 순서로 고른다.
            var order = new double[pool.Length];
            for (int index = 0; index < order.Length; index++) order[index] = random.NextDouble();
            tiers.Add(new Tier(pool, order));
        }

        private static void Select(ActiveRosterRole role, Tier[] tiers, List<ActiveRosterEntry> selected,
            HashSet<string> persons, ref int foreignCount)
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
                selected.Add(new ActiveRosterEntry(tier.Pool[best].Card.CardId, chosen.PlayerSeasonId,
                    chosen.PlayerPersonId, chosen.RegistrationType, role));
                return;
            }
            throw new InvalidOperationException($"CPU 임시 구단의 {role} 역할을 채울 선수가 없습니다.");
        }

        /// <summary>
        /// 선발 야수는 원래 포지션 선수만 받는다. 투수는 보직이 달라도 받는다 — 특수 카드 투수 대부분이 선발이라
        /// 불펜을 막으면 레전드 덱의 불펜이 전부 Normal 카드로 채워져 덱 강도가 무너진다.
        /// </summary>
        private static int GetEligibleFit(PlayerSeasonDefinition season, ActiveRosterRole role)
        {
            if (!SpecialCompositeTeamBuilder.CanFillRole(season, role)) return -1;
            int fit = SpecialCompositeTeamBuilder.GetRoleFit(season, role);
            bool isFieldingStarter = ActiveRosterCompositionRule.Standard.IsStartingHitterRole(role) &&
                                     role != ActiveRosterRole.StartingDesignatedHitter;
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
