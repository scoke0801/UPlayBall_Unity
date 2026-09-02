using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>감독모드 한 세이브가 소유하는 역사·리그·로스터·플레이어 구단 경제 상태다.</summary>
    public sealed class ManagerHistoricalRuntimeState
    {
        private readonly CurrentRosterState[] _rosters;
        private readonly OwnedPlayerCardState[] _ownedCards;
        private readonly Dictionary<string, CurrentRosterState> _rostersByTeamSeasonKey;
        private readonly Dictionary<string, OwnedPlayerCardState> _ownedCardsById;

        public ManagerHistoricalRuntimeState(
            string playerTeamSeasonKey,
            WorldHistorySnapshot worldHistory,
            WorldCardCatalog worldCardCatalog,
            LeagueInstance league,
            IReadOnlyList<CurrentRosterState> rosters,
            IReadOnlyList<OwnedPlayerCardState> ownedCards,
            ManagerEconomyState economy)
        {
            PlayerTeamSeasonKey = RequireId(playerTeamSeasonKey, nameof(playerTeamSeasonKey));
            WorldHistory = worldHistory ?? throw new ArgumentNullException(nameof(worldHistory));
            WorldCardCatalog = worldCardCatalog ?? throw new ArgumentNullException(nameof(worldCardCatalog));
            League = league ?? throw new ArgumentNullException(nameof(league));
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));

            if (!Contains(league.RegularTeamSeasonKeys, PlayerTeamSeasonKey))
                throw new ArgumentException("플레이어 구단은 정규 Franchise 10구단 중 하나여야 합니다.", nameof(playerTeamSeasonKey));

            _rosters = CopyAndValidateRosters(rosters, worldCardCatalog, league);
            _rostersByTeamSeasonKey = IndexRosters(_rosters);
            _ownedCards = CopyAndValidateOwnedCards(ownedCards, worldCardCatalog);
            _ownedCardsById = IndexOwnedCards(_ownedCards);
            ValidateSpecialEditionActivation();
            ValidatePlayerRosterOwnership();
            ValidateSpecialCompositeOverlap();
        }

        public string PlayerTeamSeasonKey { get; }
        public WorldHistorySnapshot WorldHistory { get; }
        public WorldAwardRecord WorldAwardRecord => WorldHistory.Awards;
        public WorldCardCatalog WorldCardCatalog { get; }
        public LeagueInstance League { get; }
        public IReadOnlyList<CurrentRosterState> Rosters => _rosters;
        public IReadOnlyList<OwnedPlayerCardState> OwnedCards => _ownedCards;
        public ManagerEconomyState Economy { get; }

        public CurrentRosterState GetRoster(string teamSeasonKey)
        {
            string key = RequireId(teamSeasonKey, nameof(teamSeasonKey));
            if (!_rostersByTeamSeasonKey.TryGetValue(key, out CurrentRosterState roster))
                throw new KeyNotFoundException($"TeamSeasonKey {key}의 CurrentRosterState가 없습니다.");
            return roster;
        }

        public bool TryGetOwnedCard(string cardId, out OwnedPlayerCardState ownedCard)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                ownedCard = null;
                return false;
            }
            return _ownedCardsById.TryGetValue(cardId.Trim(), out ownedCard);
        }

        /// <summary>AI 구단은 카드 소유 경제를 갖지 않으므로 플레이어 구단 여부만 명시적으로 반환한다.</summary>
        public bool HasOwnedEconomy(string teamSeasonKey)
        {
            return string.Equals(
                RequireId(teamSeasonKey, nameof(teamSeasonKey)),
                PlayerTeamSeasonKey,
                StringComparison.Ordinal);
        }

        private void ValidatePlayerRosterOwnership()
        {
            CurrentRosterState playerRoster = GetRoster(PlayerTeamSeasonKey);
            for (int index = 0; index < playerRoster.Entries.Count; index++)
            {
                string cardId = playerRoster.Entries[index].CardId;
                if (!_ownedCardsById.ContainsKey(cardId))
                    throw new ArgumentException("플레이어 구단의 1군 카드는 OwnedPlayerCardState에 존재해야 합니다.", nameof(_ownedCards));
            }
        }

        private void ValidateSpecialEditionActivation()
        {
            for (int index = 0; index < WorldCardCatalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = WorldCardCatalog.Cards[index];
                bool isActivated;
                switch (card.Edition)
                {
                    case PlayerCardEdition.Normal:
                        continue;
                    case PlayerCardEdition.AllStar:
                        isActivated = WorldAwardRecord.HasAward(card.PlayerSeasonId, WorldAwardType.AllStar);
                        break;
                    case PlayerCardEdition.GoldenGlove:
                        isActivated = WorldAwardRecord.HasAward(card.PlayerSeasonId, WorldAwardType.GoldenGlove);
                        break;
                    case PlayerCardEdition.Mvp:
                        isActivated = WorldAwardRecord.HasAward(card.PlayerSeasonId, WorldAwardType.RegularSeasonMvp) ||
                                      WorldAwardRecord.HasAward(card.PlayerSeasonId, WorldAwardType.AllStarGameMvp) ||
                                      WorldAwardRecord.HasAward(card.PlayerSeasonId, WorldAwardType.PostseasonMvp);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(card.Edition));
                }
                if (!isActivated)
                    throw new ArgumentException("특수 Edition은 해당 WorldAwardRecord가 존재할 때만 활성화할 수 있습니다.", nameof(WorldCardCatalog));
            }
        }

        private void ValidateSpecialCompositeOverlap()
        {
            if (League.SpecialCompositeTeams.Count == 0)
                return;

            var assignedPlayerSeasons = new HashSet<string>(StringComparer.Ordinal);
            for (int teamIndex = 0; teamIndex < League.SpecialCompositeTeams.Count; teamIndex++)
            {
                CurrentRosterState roster = GetRoster(League.SpecialCompositeTeams[teamIndex].TeamSeasonKey);
                for (int rosterIndex = 0; rosterIndex < roster.Entries.Count; rosterIndex++)
                {
                    if (!assignedPlayerSeasons.Add(roster.Entries[rosterIndex].PlayerSeasonId))
                        throw new ArgumentException("세 특수 합성팀의 최종 로스터는 PlayerSeasonId가 겹칠 수 없습니다.", nameof(_rosters));
                }
            }
        }

        private static CurrentRosterState[] CopyAndValidateRosters(
            IReadOnlyList<CurrentRosterState> source,
            WorldCardCatalog catalog,
            LeagueInstance league)
        {
            int expectedCount = league.ParticipantTeamCount;
            if (source == null || source.Count != expectedCount)
                throw new ArgumentException("모든 리그 참가팀의 CurrentRosterState가 필요합니다.", nameof(source));

            var result = new CurrentRosterState[source.Count];
            var teamKeys = new HashSet<string>(StringComparer.Ordinal);
            var validator = new ActiveRosterValidator();
            for (int index = 0; index < source.Count; index++)
            {
                CurrentRosterState roster = source[index]
                    ?? throw new ArgumentException("null CurrentRosterState가 있습니다.", nameof(source));
                if (!IsParticipant(league, roster.TeamSeasonKey))
                    throw new ArgumentException("리그 참가팀이 아닌 로스터가 포함되었습니다.", nameof(source));
                if (!teamKeys.Add(roster.TeamSeasonKey))
                    throw new ArgumentException("TeamSeasonKey별 CurrentRosterState는 하나만 존재해야 합니다.", nameof(source));

                RosterValidationResult validation = validator.Validate(roster);
                if (!validation.IsValid)
                    throw new ArgumentException(
                        $"{roster.TeamSeasonKey} 로스터가 ActiveRoster 계약을 위반했습니다: {validation.Issues[0].Code}",
                        nameof(source));
                ValidateRosterCards(roster, catalog);
                result[index] = roster;
            }
            return result;
        }

        private static void ValidateRosterCards(CurrentRosterState roster, WorldCardCatalog catalog)
        {
            for (int index = 0; index < roster.Entries.Count; index++)
            {
                ActiveRosterEntry entry = roster.Entries[index];
                if (!catalog.TryGetCard(entry.CardId, out PlayerCardDefinition card))
                    throw new ArgumentException("CurrentRosterState가 WorldCardCatalog에 없는 카드를 참조합니다.", nameof(roster));
                PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                if (!string.Equals(card.PlayerSeasonId, entry.PlayerSeasonId, StringComparison.Ordinal) ||
                    !string.Equals(season.PlayerPersonId, entry.PlayerPersonId, StringComparison.Ordinal) ||
                    season.RegistrationType != entry.RegistrationType)
                {
                    throw new ArgumentException("로스터 항목과 WorldCardCatalog 원본의 선수 식별 정보가 일치하지 않습니다.", nameof(roster));
                }
            }
        }

        private static OwnedPlayerCardState[] CopyAndValidateOwnedCards(
            IReadOnlyList<OwnedPlayerCardState> source,
            WorldCardCatalog catalog)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var result = new OwnedPlayerCardState[source.Count];
            var cardIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                OwnedPlayerCardState owned = source[index]
                    ?? throw new ArgumentException("null OwnedPlayerCardState가 있습니다.", nameof(source));
                if (!catalog.TryGetCard(owned.CardId, out _))
                    throw new ArgumentException("OwnedPlayerCardState가 WorldCardCatalog에 없는 카드를 참조합니다.", nameof(source));
                if (!cardIds.Add(owned.CardId))
                    throw new ArgumentException("카드 소유 상태는 CardId별 하나만 존재해야 합니다.", nameof(source));
                result[index] = owned;
            }
            return result;
        }

        private static Dictionary<string, CurrentRosterState> IndexRosters(IReadOnlyList<CurrentRosterState> rosters)
        {
            var result = new Dictionary<string, CurrentRosterState>(rosters.Count, StringComparer.Ordinal);
            for (int index = 0; index < rosters.Count; index++)
                result.Add(rosters[index].TeamSeasonKey, rosters[index]);
            return result;
        }

        private static Dictionary<string, OwnedPlayerCardState> IndexOwnedCards(IReadOnlyList<OwnedPlayerCardState> cards)
        {
            var result = new Dictionary<string, OwnedPlayerCardState>(cards.Count, StringComparer.Ordinal);
            for (int index = 0; index < cards.Count; index++)
                result.Add(cards[index].CardId, cards[index]);
            return result;
        }

        private static bool IsParticipant(LeagueInstance league, string teamSeasonKey)
        {
            if (Contains(league.RegularTeamSeasonKeys, teamSeasonKey))
                return true;
            for (int index = 0; index < league.SpecialCompositeTeams.Count; index++)
                if (string.Equals(league.SpecialCompositeTeams[index].TeamSeasonKey, teamSeasonKey, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int index = 0; index < values.Count; index++)
                if (string.Equals(values[index], value, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }
}
