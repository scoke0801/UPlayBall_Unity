using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;

namespace Baseball.Game.Historical
{
    /// <summary>역사 정본 Core25의 약한 슬롯을 같은 프랜차이즈 레전드로 보강해 경기 입력에 동결한다.</summary>
    public sealed class LegendaryPracticeRosterBuilder
    {
        // 연습경기 특수 구원투수는 불펜 1·2번에서 기용한다.
        private const int SpecialRelieverSlotCount = 2;
        /// <summary>구멍 판정과 후보 비교에 쓰는 슬롯 역할별 능력치 가중치다.</summary>
        private static readonly PlayerAbility[] BatterAbilities =
        {
            PlayerAbility.Contact, PlayerAbility.Power, PlayerAbility.Speed,
            PlayerAbility.Bunt, PlayerAbility.Defense, PlayerAbility.BatterMental
        };
        private static readonly PlayerAbility[] PitcherAbilities =
        {
            PlayerAbility.Stamina, PlayerAbility.Velocity, PlayerAbility.Stuff,
            PlayerAbility.Breaking, PlayerAbility.Control, PlayerAbility.PitcherMental
        };
        private static readonly int[] BatterWeights = { 4, 4, 2, 1, 2, 2 };
        private static readonly int[] PitcherWeights = { 3, 3, 4, 3, 4, 2 };

        /// <summary>후보 점수는 로스터 구성 중 반복 비교되므로 카드별로 한 번만 계산해 둔다.</summary>
        private readonly struct LegendCandidate
        {
            public LegendCandidate(PlayerCardDefinition card, PlayerSeasonDefinition season,
                int batterScore, int starterScore, int relieverScore)
            {
                Card = card; Season = season;
                BatterScore = batterScore; StarterScore = starterScore; RelieverScore = relieverScore;
            }

            public PlayerCardDefinition Card { get; }
            public PlayerSeasonDefinition Season { get; }
            public int BatterScore { get; }
            public int StarterScore { get; }
            public int RelieverScore { get; }
        }

        private readonly HistoricalBakedContent _content;
        private readonly BalanceTable _balance;
        private readonly LegendaryPracticeDevelopment _development;
        private readonly WorldCardCatalog _cardCatalog;
        /// <summary>연습경기의 원 시즌 수상 카드와 공개 표시가 공유하는 카드 정본이다.</summary>
        public WorldCardCatalog CardCatalog => _cardCatalog;
        private readonly Dictionary<string, PlayerCardDefinition> _seasonUpgrades = new Dictionary<string, PlayerCardDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<LegendCandidate>> _franchiseLegends = new Dictionary<string, List<LegendCandidate>>(StringComparer.Ordinal);

        public LegendaryPracticeRosterBuilder(HistoricalBakedContent content, BalanceTable balance,
            LegendaryPracticeDevelopmentBalance development = null, CardEditionBalanceTable cardEditionBalance = null,
            WorldCardCatalog cardCatalog = null)
        {
            _content = content; _balance = balance;
            if (development != null) _development = new LegendaryPracticeDevelopment(balance, development);
            var awards = new List<WorldAwardEntry>(content.OriginalAwardRecords.Count);
            foreach (var record in content.OriginalAwardRecords) awards.Add(record.Award);
            _cardCatalog = cardCatalog ?? WorldCardCatalogBuilder.Build(content.PlayerSeasons, new WorldAwardRecord(awards),
                cardEditionBalance ?? CardEditionBalanceTable.CreateInitial(), content.PlayerPersons,
                content.TeamSeasons, content.SpecialCards);
            foreach (var card in _cardCatalog.Cards)
            {
                if (IsSeasonUpgrade(card.Edition))
                {
                    if (!_seasonUpgrades.TryGetValue(card.PlayerSeasonId, out var chosen) ||
                        IsPreferredSeasonUpgrade(card, chosen))
                        _seasonUpgrades[card.PlayerSeasonId] = card;
                    continue;
                }
                if (card.Edition != PlayerCardEdition.CareerHigh && card.Edition != PlayerCardEdition.Legend) continue;
                if (!content.TryGetPlayerSeason(card.PlayerSeasonId, out var season)) continue;
                if (!_franchiseLegends.TryGetValue(season.OriginFranchiseId, out var pool))
                    _franchiseLegends.Add(season.OriginFranchiseId, pool = new List<LegendCandidate>());
                var source = season.CreateBaseAttributes();
                pool.Add(new LegendCandidate(card, season,
                    Score(source, card, BatterWeights, isBatter: true, staminaWeight: 0),
                    Score(source, card, PitcherWeights, isBatter: false, staminaWeight: 3),
                    Score(source, card, PitcherWeights, isBatter: false, staminaWeight: 1)));
            }
            foreach (var pool in _franchiseLegends.Values)
                pool.Sort((a, b) => string.CompareOrdinal(a.Card.CardId, b.Card.CardId));
        }

        /// <summary>커리어하이·레전드를 제외한 시즌 특수 카드만 원본 시즌의 우선 배치 후보로 사용한다.</summary>
        private static bool IsSeasonUpgrade(PlayerCardEdition edition) =>
            edition == PlayerCardEdition.GoldenGlove || edition == PlayerCardEdition.AllStar ||
            edition == PlayerCardEdition.Rare || edition == PlayerCardEdition.Mvp ||
            edition == PlayerCardEdition.Ex;

        /// <summary>같은 시즌의 후보는 기존 역할별 전력 점수로 비교하고 동점은 ID 순서로 고정한다.</summary>
        private bool IsPreferredSeasonUpgrade(PlayerCardDefinition candidate, PlayerCardDefinition current)
        {
            if (!_content.TryGetPlayerSeason(candidate.PlayerSeasonId, out var season))
                throw new InvalidOperationException("역사 팀의 선수 원본이 없습니다.");
            bool isBatter = season.PlayerType == PlayerType.Batter;
            int staminaWeight = season.PitcherRole == PitcherRole.Starter ? 3 : 1;
            var source = season.CreateBaseAttributes();
            var weights = isBatter ? BatterWeights : PitcherWeights;
            int candidateScore = Score(source, candidate, weights, isBatter, staminaWeight);
            int currentScore = Score(source, current, weights, isBatter, staminaWeight);
            return candidateScore > currentScore || (candidateScore == currentScore &&
                string.CompareOrdinal(candidate.CardId, current.CardId) < 0);
        }

        /// <summary>Contact·Stuff처럼 결과 기여가 큰 능력치에 가중치를 실어 슬롯 전력을 한 정수로 압축한다.</summary>
        private static int Score(AbilityRatings source, PlayerCardDefinition card, int[] weights, bool isBatter, int staminaWeight)
        {
            var abilities = isBatter ? BatterAbilities : PitcherAbilities;
            int total = 0;
            for (int i = 0; i < abilities.Length; i++)
            {
                int weight = !isBatter && abilities[i] == PlayerAbility.Stamina ? staminaWeight : weights[i];
                total += weight * (source.Get(abilities[i]) + card.GetModifier(abilities[i]));
            }
            return total;
        }

        /// <summary>같은 선수·시즌의 특수 카드를 우선 적용한 뒤 남은 약점 슬롯을 프랜차이즈 레전드로 메운다.</summary>
        public PlayerCardDefinition[] SelectCards(TeamSeasonDefinition team, int specialCardLimit = OwnerSpecialCardRosterRule.MaxTotalCount)
        {
            if (specialCardLimit < 0 || specialCardLimit > OwnerSpecialCardRosterRule.MaxTotalCount)
                throw new ArgumentOutOfRangeException(nameof(specialCardLimit));
            var cards = new PlayerCardDefinition[25];
            var seasons = new PlayerSeasonDefinition[25];
            for (int i = 0; i < 25; i++)
            {
                if (!_content.TryGetNormalCard(team.Core25CardIds[i], out var card))
                    throw new InvalidOperationException("역사 팀의 기본 카드가 없습니다.");
                if (_seasonUpgrades.TryGetValue(card.PlayerSeasonId, out var upgrade)) card = upgrade;
                if (!_content.TryGetPlayerSeason(card.PlayerSeasonId, out seasons[i]))
                    throw new InvalidOperationException("역사 팀의 선수 원본이 없습니다.");
                cards[i] = card;
            }
            FillRosterHoles(team, cards, seasons, specialCardLimit);
            ArrangeSpecialRelievers(cards);
            return cards;
        }

        /// <summary>보강된 구원투수를 불펜 1·2번과 교환해 공개 편성과 경기 입력의 우선순위를 맞춘다.</summary>
        private static void ArrangeSpecialRelievers(PlayerCardDefinition[] cards)
        {
            int target = 19;
            for (int slot = 19; slot < cards.Length; slot++)
            {
                if (cards[slot].Edition != PlayerCardEdition.CareerHigh &&
                    cards[slot].Edition != PlayerCardEdition.Legend) continue;
                var displaced = cards[target];
                cards[target] = cards[slot];
                cards[slot] = displaced;
                target++;
            }
        }

        /// <summary>선발 야수와 투수진에서 이득이 가장 큰 슬롯부터 차례로 보강한다.</summary>
        private void FillRosterHoles(TeamSeasonDefinition team, PlayerCardDefinition[] cards, PlayerSeasonDefinition[] seasons, int specialCardLimit)
        {
            if (!_franchiseLegends.TryGetValue(team.FranchiseId, out var pool)) return;
            var persons = new HashSet<string>(StringComparer.Ordinal);
            var scores = new int[25];
            var replaced = new bool[25];
            for (int i = 0; i < 25; i++)
            {
                persons.Add(seasons[i].PlayerPersonId);
                var source = seasons[i].CreateBaseAttributes();
                scores[i] = i < 14 ? Score(source, cards[i], BatterWeights, isBatter: true, staminaWeight: 0)
                    : Score(source, cards[i], PitcherWeights, isBatter: false, staminaWeight: i < 19 ? 3 : 1);
            }
            int total = 0, hitters = 0, pitchers = 0, relievers = 0;
            while (total < specialCardLimit)
            {
                int bestSlot = -1, bestCandidate = -1, bestGain = 0;
                for (int slot = 0; slot < 25; slot++)
                {
                    // 벤치는 상승폭이 커도 선발로 승격되지 않으므로 한정된 특수 카드 보강 대상에서 제외한다.
                    if (slot >= 9 && slot < 14) continue;
                    if (slot >= 19 && relievers >= SpecialRelieverSlotCount) continue;
                    if (replaced[slot]) continue;
                    if (slot < 14 ? hitters >= OwnerSpecialCardRosterRule.MaxHitterCount
                        : pitchers >= OwnerSpecialCardRosterRule.MaxPitcherCount) continue;
                    for (int index = 0; index < pool.Count; index++)
                    {
                        var candidate = pool[index];
                        if (!IsSlotCompatible(slot, seasons[slot], candidate.Season)) continue;
                        if (!string.Equals(candidate.Season.PlayerPersonId, seasons[slot].PlayerPersonId, StringComparison.Ordinal) &&
                            persons.Contains(candidate.Season.PlayerPersonId))
                        {
                            int benchSlot = FindBenchSlot(seasons, candidate.Season.PlayerPersonId);
                            // 같은 인물의 벤치 카드는 선발과 교환하되 백업 포수·외국인 구성을 보존한다.
                            if (benchSlot < 0 || !IsSlotCompatible(benchSlot, seasons[benchSlot], seasons[slot])) continue;
                        }
                        int gain = CandidateScore(slot, candidate) - scores[slot];
                        if (gain > bestGain) { bestGain = gain; bestSlot = slot; bestCandidate = index; }
                    }
                }
                if (bestSlot < 0) break;
                var chosen = pool[bestCandidate];
                int promotedBenchSlot = FindBenchSlot(seasons, chosen.Season.PlayerPersonId);
                if (promotedBenchSlot >= 0)
                {
                    cards[promotedBenchSlot] = cards[bestSlot];
                    seasons[promotedBenchSlot] = seasons[bestSlot];
                }
                else persons.Remove(seasons[bestSlot].PlayerPersonId);
                persons.Add(chosen.Season.PlayerPersonId);
                cards[bestSlot] = chosen.Card; seasons[bestSlot] = chosen.Season;
                scores[bestSlot] = CandidateScore(bestSlot, chosen);
                replaced[bestSlot] = true; total++;
                if (bestSlot < 14) hitters++; else pitchers++;
                if (bestSlot >= 19) relievers++;
            }
        }

        private static int CandidateScore(int slot, LegendCandidate candidate) =>
            slot < 14 ? candidate.BatterScore : slot < 19 ? candidate.StarterScore : candidate.RelieverScore;

        private static int FindBenchSlot(PlayerSeasonDefinition[] seasons, string personId)
        {
            for (int slot = 9; slot < 14; slot++)
                if (string.Equals(seasons[slot].PlayerPersonId, personId, StringComparison.Ordinal)) return slot;
            return -1;
        }

        /// <summary>포지션·외국인 구성을 그대로 두어야 25인 엔트리와 백업 포수 검증이 유지된다.</summary>
        private static bool IsSlotCompatible(int slot, PlayerSeasonDefinition current, PlayerSeasonDefinition candidate)
        {
            if (candidate.RegistrationType != current.RegistrationType) return false;
            // DH 등으로 기록된 백업 포수도 부포지션을 잃는 보강으로 교체하면 안 된다.
            if (HasCatcherPosition(current) && !HasCatcherPosition(candidate)) return false;
            if (slot < 14) return candidate.PlayerType == PlayerType.Batter && candidate.Position == current.Position;
            if (candidate.PlayerType != PlayerType.Pitcher) return false;
            return slot < 19 ? candidate.PitcherRole == PitcherRole.Starter : candidate.PitcherRole != PitcherRole.Starter;
        }

        private static bool HasCatcherPosition(PlayerSeasonDefinition season)
        {
            if (season.Position == PlayerPosition.Catcher) return true;
            foreach (var position in season.SecondaryPositions)
                if (position.Position == PlayerPosition.Catcher && position.Proficiency > 0) return true;
            return false;
        }

        public string GetRosterHash(TeamSeasonDefinition team)
        {
            var text = new System.Text.StringBuilder(_content.Manifest.ContentHash).Append('|').Append(team.TeamSeasonKey);
            foreach (var card in SelectCards(team))
            {
                text.Append('|').Append(card.CardId);
                for (int i = 0; i < PlayerAbilityCatalog.AbilityCount; i++) text.Append(':').Append(card.GetModifier((PlayerAbility)i));
            }
            return LegendaryPracticeCatalog.Hash(text.ToString());
        }

        /// <summary>순위 산정은 기본 편성, 실제 도전은 확정 순위의 성장 상태를 같은 경기 변환으로 처리한다.</summary>
        public MatchRosterSnapshot[] Build(TeamSeasonDefinition team, WorldIdentityRegistry identities, int teamId,
            int playerIdBase, out TeamColorDefinition[] teamColors, int rank = 0)
        {
            var seasons = new PlayerSeasonDefinition[25];
            var cards = SelectCards(team);
            var persons = new PlayerPersonDefinition[25];
            var entries = new ActiveRosterEntry[25];
            var unique = new HashSet<string>(StringComparer.Ordinal);
            int catchers = 0;
            for (int i = 0; i < 25; i++)
            {
                if (!_content.TryGetPlayerSeason(cards[i].PlayerSeasonId, out seasons[i]) ||
                    !_content.TryGetPlayerPerson(seasons[i].PlayerPersonId, out persons[i]) ||
                    !unique.Add(seasons[i].PlayerPersonId))
                    throw new InvalidOperationException("역사 팀의 선수 원본 또는 중복 검증에 실패했습니다.");
                ActiveRosterRole role = i < 9 ? (ActiveRosterRole)i : i < 14 ? ActiveRosterRole.BenchHitter :
                    (ActiveRosterRole)((int)ActiveRosterRole.StartingPitcher1 + i - 14);
                entries[i] = new ActiveRosterEntry(cards[i].CardId, seasons[i].PlayerSeasonId,
                    seasons[i].PlayerPersonId, seasons[i].RegistrationType, role);
                if (HasCatcherPosition(seasons[i])) catchers++;
            }
            var roster = new CurrentRosterState(team.TeamSeasonKey, entries);
            if (!new ActiveRosterValidator().Validate(roster).IsValid || catchers < 2)
                throw new InvalidOperationException("역사 팀의 엔트리 또는 백업 포수가 유효하지 않습니다: " + team.TeamSeasonKey + " / 포수 " + catchers);
            var bonuses = ManagerModeMatchService.ResolveAiTeamColorBonuses(roster, _cardCatalog, _balance.TeamColor, out teamColors);
            var players = new Player[25];
            var abilityResolver = new OwnerCardAbilityResolver(_balance.Growth);
            for (int i = 0; i < 25; i++)
            {
                var season = seasons[i]; var card = cards[i]; var source = season.CreateBaseAttributes();
                var owned = rank == 0 ? null : CreateDevelopment(card, rank);
                int Base(PlayerAbility ability) => abilityResolver.ResolveRawPermanent(season, card, owned, ability);
                int Raw(PlayerAbility ability) => checked(Base(ability) + bonuses.Get(card.CardId, ability));
                int Permanent(PlayerAbility ability) => Math.Max(1, Math.Min(AttributeRating.Maximum,
                    Base(ability)));
                double Effective(PlayerAbility ability) => Math.Max(1d, Math.Min(_balance.MatchRatingCurve.Caps.HardCap, Raw(ability)));
                int Get(PlayerAbility ability) => MatchRatingCurve.ResolveMatchInput(Raw(ability), ability, _balance.MatchRatingCurve);
                players[i] = new Player(playerIdBase + i + 1, identities.GetPlayerDisplayName(season.PlayerPersonId),
                    season.Position, persons[i].Bats, persons[i].Throws,
                    new BatterAttributes(Get(PlayerAbility.Contact), Get(PlayerAbility.Power), Get(PlayerAbility.Speed),
                        Get(PlayerAbility.Bunt), Get(PlayerAbility.Defense), Get(PlayerAbility.BatterMental)),
                    new PitcherAttributes(Get(PlayerAbility.Stamina), Get(PlayerAbility.Velocity), Get(PlayerAbility.Stuff),
                        Get(PlayerAbility.Breaking), Get(PlayerAbility.Control), Get(PlayerAbility.PitcherMental)),
                    secondaryPositions: season.SecondaryPositions, pitchRepertoire: season.PitchRepertoire,
                    traitIds: abilityResolver.ResolveActiveTraitIds(owned),
                    cardTrait: owned == null ? default : new CardTraitEffect(owned.Trait.trait,
                        _balance.TraitTraining.Get(owned.Trait.trait).effect * _balance.TraitTraining.multipliers[(int)owned.Trait.rank - 1]),
                    isPositionEvidenceMissing: season.IsPositionEvidenceMissing,
                    bakedPitcherAttributes: source.ToPitcherAttributes(), permanentPitcherAttributes: new PitcherAttributes(
                        Permanent(PlayerAbility.Stamina), Permanent(PlayerAbility.Velocity), Permanent(PlayerAbility.Stuff),
                        Permanent(PlayerAbility.Breaking), Permanent(PlayerAbility.Control), Permanent(PlayerAbility.PitcherMental)),
                    hasResolvedMatchRatings: true,
                    uncurvedPitcherAttributes: new PitcherRatingValues(Effective(PlayerAbility.Stamina), Effective(PlayerAbility.Velocity),
                        Effective(PlayerAbility.Stuff), Effective(PlayerAbility.Breaking), Effective(PlayerAbility.Control), Effective(PlayerAbility.PitcherMental)));
            }
            var fielding = new LineupSlot[9];
            for (int i = 0; i < 9; i++) fielding[i] = new LineupSlot(players[i], (PlayerPosition)(i + 1));
            var lineup = new ManagerLineupAi(_balance.ManagerLineup).BuildLineup(fielding);
            var bench = new Player[5]; Array.Copy(players, 9, bench, 0, 5);
            PitcherRosterEntry Pitcher(int index, PitcherRole role)
            {
                var season = seasons[index];
                var usage = HistoricalPitcherUsageResolver.Resolve(season, role, _balance.HistoricalPitcherUsage);
                return new PitcherRosterEntry(players[index], role,
                    naturalRole: season.PitcherRole, playerSeasonId: season.PlayerSeasonId,
                    naturalRoleConfidence: season.PitcherRoleConfidence,
                    capacityMultiplier: usage.Capacity, recoveryMultiplier: usage.Recovery);
            }
            var bullpen = new PitcherRosterEntry[6];
            for (int i = 0; i < 6; i++) bullpen[i] = Pitcher(19 + i, i < 4 ? PitcherRole.MiddleRelief : i == 4 ? PitcherRole.Setup : PitcherRole.Closer);
            string name = team.OriginYear + " " + identities.GetFranchiseDisplayName(team.FranchiseId);
            var result = new MatchRosterSnapshot[5];
            for (int i = 0; i < 5; i++) result[i] = new MatchRosterSnapshot(teamId, name, lineup,
                Pitcher(14 + i, PitcherRole.Starter), bullpen, bench, default, RunningApproach.Balanced);
            return result;
        }

        /// <summary>공개 카드도 실제 대전과 같은 독립된 성장 상태를 받는다.</summary>
        public OwnedPlayerCardState CreateDevelopment(PlayerCardDefinition card, int rank)
        {
            if (_development == null) throw new InvalidOperationException("연습경기 성장 설정이 없습니다.");
            if (!_content.TryGetPlayerSeason(card.PlayerSeasonId, out var season))
                throw new InvalidOperationException("연습경기 선수 원본이 없습니다.");
            return _development.Create(card, season, rank);
        }
    }
}
