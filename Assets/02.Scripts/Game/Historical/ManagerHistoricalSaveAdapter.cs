using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>감독모드 Runtime 상태와 버전이 명시된 저장 DTO를 손실 없이 변환한다.</summary>
    public sealed class ManagerHistoricalSaveAdapter
    {
        public const int CurrentSaveVersion = 1;

        private readonly IHistoricalContentProvider _contentProvider;
        private readonly CardEditionBalanceTable _cardEditionBalance;
        private readonly WorldHistorySaveMapper _worldHistoryMapper;

        public ManagerHistoricalSaveAdapter(
            IHistoricalContentProvider contentProvider,
            CardEditionBalanceTable cardEditionBalance,
            WorldHistorySaveMapper worldHistoryMapper = null)
        {
            _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
            _cardEditionBalance = cardEditionBalance ?? throw new ArgumentNullException(nameof(cardEditionBalance));
            _worldHistoryMapper = worldHistoryMapper ?? new WorldHistorySaveMapper();
        }

        public ManagerHistoricalSaveData CreateSaveData(ManagerHistoricalRuntimeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return new ManagerHistoricalSaveData
            {
                saveVersion = CurrentSaveVersion,
                contentReference = HistoricalContentReferenceMapper.CreateSaveData(state.ContentReference),
                playerTeamSeasonKey = state.PlayerTeamSeasonKey,
                worldHistory = _worldHistoryMapper.CreateSaveData(state.WorldHistory),
                league = CreateLeague(state.League),
                rosters = CreateRosters(state.Rosters),
                ownedCards = CreateOwnedCards(state.OwnedCards),
                economy = new ManagerEconomySaveData
                {
                    money = state.Economy.Money,
                    scoutingPoints = state.Economy.ScoutingPoints,
                    developmentPoints = state.Economy.DevelopmentPoints,
                    pityGauge = state.Economy.PityGauge
                }
            };
        }

        public ManagerHistoricalRuntimeState Restore(ManagerHistoricalSaveData saveData)
        {
            if (saveData == null)
                throw new ArgumentNullException(nameof(saveData));
            if (saveData.saveVersion != CurrentSaveVersion)
                throw new InvalidOperationException(
                    $"Manager Historical SaveVersion {saveData.saveVersion}은 현재 버전 {CurrentSaveVersion}과 호환되지 않습니다.");

            HistoricalContentReference contentReference = HistoricalContentReferenceMapper.Restore(
                Require(saveData.contentReference, nameof(saveData.contentReference)));
            HistoricalBakedContent bakedContent = _contentProvider.Load()
                ?? throw new InvalidOperationException("Runtime Historical Content Provider가 null을 반환했습니다.");
            contentReference.EnsureMatches(bakedContent.Manifest);

            WorldHistorySnapshot history = _worldHistoryMapper.Restore(
                Require(saveData.worldHistory, nameof(saveData.worldHistory)));
            ValidateWorldHistoryReferences(history, bakedContent);
            WorldCardCatalog catalog = WorldCardCatalogBuilder.Build(
                bakedContent.PlayerSeasons,
                history.Awards,
                _cardEditionBalance);
            LeagueInstance league = RestoreLeague(Require(saveData.league, nameof(saveData.league)));
            CurrentRosterState[] rosters = RestoreRosters(Require(saveData.rosters, nameof(saveData.rosters)));
            OwnedPlayerCardState[] ownedCards = RestoreOwnedCards(Require(saveData.ownedCards, nameof(saveData.ownedCards)));
            ManagerEconomySaveData economyData = Require(saveData.economy, nameof(saveData.economy));

            return new ManagerHistoricalRuntimeState(
                saveData.playerTeamSeasonKey,
                contentReference,
                history,
                catalog,
                league,
                rosters,
                ownedCards,
                new ManagerEconomyState(
                    economyData.money,
                    economyData.scoutingPoints,
                    economyData.developmentPoints,
                    economyData.pityGauge));
        }

        /// <summary>저장된 파생 기록이 현재 고정 Content의 동일 선수·구단·연도를 가리키는지 검증한다.</summary>
        private static void ValidateWorldHistoryReferences(
            WorldHistorySnapshot history,
            HistoricalBakedContent bakedContent)
        {
            for (int index = 0; index < history.Statistics.Count; index++)
            {
                SeasonStatistics statistics = history.Statistics[index];
                if (!bakedContent.TryGetPlayerSeason(
                        statistics.PlayerSeasonId,
                        out PlayerSeasonDefinition playerSeason))
                {
                    throw new InvalidOperationException(
                        $"저장된 World History가 현재 Content에 없는 PlayerSeasonId를 참조합니다: " +
                        $"{statistics.PlayerSeasonId}");
                }
                if (!bakedContent.TryGetTeamSeason(
                        statistics.TeamSeasonKey,
                        out TeamSeasonDefinition teamSeason))
                {
                    throw new InvalidOperationException(
                        $"저장된 World History가 현재 Content에 없는 TeamSeasonKey를 참조합니다: " +
                        $"{statistics.TeamSeasonKey}");
                }
                if (statistics.SeasonYear != playerSeason.OriginYear ||
                    statistics.SeasonYear != teamSeason.OriginYear)
                {
                    throw new InvalidOperationException(
                        $"저장된 World History의 SeasonYear가 Baked Content와 다릅니다: " +
                        $"playerSeasonId={statistics.PlayerSeasonId}, teamSeasonKey={statistics.TeamSeasonKey}, " +
                        $"saved={statistics.SeasonYear}, player={playerSeason.OriginYear}, team={teamSeason.OriginYear}");
                }
                if (!string.Equals(
                        statistics.TeamSeasonKey,
                        playerSeason.OriginTeamSeasonKey,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"저장된 World History의 TeamSeasonKey가 PlayerSeason 원소속과 다릅니다: " +
                        $"playerSeasonId={statistics.PlayerSeasonId}, saved={statistics.TeamSeasonKey}, " +
                        $"expected={playerSeason.OriginTeamSeasonKey}");
                }
            }

            for (int index = 0; index < history.Awards.Entries.Count; index++)
            {
                WorldAwardEntry award = history.Awards.Entries[index];
                if (!bakedContent.TryGetPlayerSeason(
                        award.PlayerSeasonId,
                        out PlayerSeasonDefinition playerSeason))
                {
                    throw new InvalidOperationException(
                        $"저장된 World Award가 현재 Content에 없는 PlayerSeasonId를 참조합니다: " +
                        $"{award.PlayerSeasonId}");
                }
                if (award.SeasonYear != playerSeason.OriginYear)
                {
                    throw new InvalidOperationException(
                        $"저장된 World Award의 SeasonYear가 Baked PlayerSeason과 다릅니다: " +
                        $"playerSeasonId={award.PlayerSeasonId}, saved={award.SeasonYear}, " +
                        $"expected={playerSeason.OriginYear}");
                }
            }
        }

        private static LeagueInstanceSaveData CreateLeague(LeagueInstance league)
        {
            var regular = new string[league.RegularTeamSeasonKeys.Count];
            for (int index = 0; index < regular.Length; index++)
                regular[index] = league.RegularTeamSeasonKeys[index];
            var special = new SpecialCompositeTeamRegistrationSaveData[league.SpecialCompositeTeams.Count];
            for (int index = 0; index < special.Length; index++)
            {
                SpecialCompositeTeamRegistration registration = league.SpecialCompositeTeams[index];
                special[index] = new SpecialCompositeTeamRegistrationSaveData
                {
                    teamSeasonKey = registration.TeamSeasonKey,
                    originYear = registration.OriginYear,
                    teamType = (int)registration.TeamType
                };
            }
            Array.Sort(special, (left, right) => left.teamType.CompareTo(right.teamType));
            return new LeagueInstanceSaveData
            {
                leagueInstanceId = league.LeagueInstanceId,
                grade = (int)league.Grade,
                regularTeamSeasonKeys = regular,
                specialCompositeTeams = special
            };
        }

        private static LeagueInstance RestoreLeague(LeagueInstanceSaveData source)
        {
            ValidateEnum<LeagueGrade>(source.grade, nameof(source.grade));
            string[] regular = Require(source.regularTeamSeasonKeys, nameof(source.regularTeamSeasonKeys));
            SpecialCompositeTeamRegistrationSaveData[] specialData =
                Require(source.specialCompositeTeams, nameof(source.specialCompositeTeams));
            var special = new SpecialCompositeTeamRegistration[specialData.Length];
            for (int index = 0; index < special.Length; index++)
            {
                SpecialCompositeTeamRegistrationSaveData registration =
                    Require(specialData[index], nameof(source.specialCompositeTeams));
                ValidateEnum<SpecialCompositeTeamType>(registration.teamType, nameof(registration.teamType));
                special[index] = new SpecialCompositeTeamRegistration(
                    registration.teamSeasonKey,
                    registration.originYear,
                    (SpecialCompositeTeamType)registration.teamType);
            }
            return new LeagueInstance(source.leagueInstanceId, (LeagueGrade)source.grade, regular, special);
        }

        private static CurrentRosterSaveData[] CreateRosters(IReadOnlyList<CurrentRosterState> source)
        {
            var rosters = new CurrentRosterSaveData[source.Count];
            for (int rosterIndex = 0; rosterIndex < source.Count; rosterIndex++)
            {
                CurrentRosterState roster = source[rosterIndex];
                var entries = new ActiveRosterEntrySaveData[roster.Entries.Count];
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    ActiveRosterEntry entry = roster.Entries[entryIndex];
                    entries[entryIndex] = new ActiveRosterEntrySaveData
                    {
                        cardId = entry.CardId,
                        playerSeasonId = entry.PlayerSeasonId,
                        playerPersonId = entry.PlayerPersonId,
                        registrationType = (int)entry.RegistrationType,
                        role = (int)entry.Role
                    };
                }
                Array.Sort(entries, CompareRosterEntries);
                rosters[rosterIndex] = new CurrentRosterSaveData
                {
                    teamSeasonKey = roster.TeamSeasonKey,
                    entries = entries
                };
            }
            Array.Sort(rosters, (left, right) => StringComparer.Ordinal.Compare(left.teamSeasonKey, right.teamSeasonKey));
            return rosters;
        }

        private static CurrentRosterState[] RestoreRosters(CurrentRosterSaveData[] source)
        {
            var rosters = new CurrentRosterState[source.Length];
            for (int rosterIndex = 0; rosterIndex < source.Length; rosterIndex++)
            {
                CurrentRosterSaveData roster = Require(source[rosterIndex], nameof(source));
                ActiveRosterEntrySaveData[] entriesData = Require(roster.entries, nameof(roster.entries));
                var entries = new ActiveRosterEntry[entriesData.Length];
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    ActiveRosterEntrySaveData entry = Require(entriesData[entryIndex], nameof(roster.entries));
                    ValidateEnum<RegistrationType>(entry.registrationType, nameof(entry.registrationType));
                    ValidateEnum<ActiveRosterRole>(entry.role, nameof(entry.role));
                    entries[entryIndex] = new ActiveRosterEntry(
                        entry.cardId,
                        entry.playerSeasonId,
                        entry.playerPersonId,
                        (RegistrationType)entry.registrationType,
                        (ActiveRosterRole)entry.role);
                }
                rosters[rosterIndex] = new CurrentRosterState(roster.teamSeasonKey, entries);
            }
            return rosters;
        }

        private static OwnedPlayerCardSaveData[] CreateOwnedCards(IReadOnlyList<OwnedPlayerCardState> source)
        {
            var result = new OwnedPlayerCardSaveData[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                OwnedPlayerCardState card = source[index];
                var training = new int[PlayerAbilityCatalog.AbilityCount];
                for (int abilityIndex = 0; abilityIndex < training.Length; abilityIndex++)
                    training[abilityIndex] = card.Training.GetBonus((PlayerAbility)abilityIndex);
                result[index] = new OwnedPlayerCardSaveData
                {
                    cardId = card.CardId,
                    enhancementLevel = card.EnhancementLevel,
                    duplicateCount = card.DuplicateCount,
                    isLocked = card.IsLocked,
                    isFavorite = card.IsFavorite,
                    trainingBonuses = training
                };
            }
            Array.Sort(result, (left, right) => StringComparer.Ordinal.Compare(left.cardId, right.cardId));
            return result;
        }

        private static OwnedPlayerCardState[] RestoreOwnedCards(OwnedPlayerCardSaveData[] source)
        {
            var result = new OwnedPlayerCardState[source.Length];
            for (int index = 0; index < result.Length; index++)
            {
                OwnedPlayerCardSaveData card = Require(source[index], nameof(source));
                result[index] = new OwnedPlayerCardState(
                    card.cardId,
                    card.enhancementLevel,
                    card.duplicateCount,
                    card.isLocked,
                    card.isFavorite,
                    new CardTrainingState(Require(card.trainingBonuses, nameof(card.trainingBonuses))));
            }
            return result;
        }

        private static int CompareRosterEntries(ActiveRosterEntrySaveData left, ActiveRosterEntrySaveData right)
        {
            int comparison = left.role.CompareTo(right.role);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.playerSeasonId, right.playerSeasonId);
        }

        private static T Require<T>(T value, string parameterName) where T : class
        {
            if (value == null)
                throw new ArgumentException("세이브 필수 값이 없습니다.", parameterName);
            return value;
        }

        private static void ValidateEnum<T>(int value, string parameterName) where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
                throw new ArgumentOutOfRangeException(parameterName, value, "저장된 enum 값이 유효하지 않습니다.");
        }
    }
}
