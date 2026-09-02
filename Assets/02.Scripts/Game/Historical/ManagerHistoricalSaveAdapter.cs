using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Game.Historical
{
    /// <summary>감독모드 Runtime 상태와 버전이 명시된 저장 DTO를 손실 없이 변환한다.</summary>
    public sealed class ManagerHistoricalSaveAdapter
    {
        public const int CurrentSaveVersion = 1;

        public ManagerHistoricalSaveData CreateSaveData(ManagerHistoricalRuntimeState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            return new ManagerHistoricalSaveData
            {
                saveVersion = CurrentSaveVersion,
                playerTeamSeasonKey = state.PlayerTeamSeasonKey,
                worldHistory = CreateWorldHistory(state.WorldHistory),
                worldCardCatalog = CreateCardCatalog(state.WorldCardCatalog),
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

            WorldHistorySnapshot history = RestoreWorldHistory(Require(saveData.worldHistory, nameof(saveData.worldHistory)));
            WorldCardCatalog catalog = RestoreCardCatalog(Require(saveData.worldCardCatalog, nameof(saveData.worldCardCatalog)));
            LeagueInstance league = RestoreLeague(Require(saveData.league, nameof(saveData.league)));
            CurrentRosterState[] rosters = RestoreRosters(Require(saveData.rosters, nameof(saveData.rosters)));
            OwnedPlayerCardState[] ownedCards = RestoreOwnedCards(Require(saveData.ownedCards, nameof(saveData.ownedCards)));
            ManagerEconomySaveData economyData = Require(saveData.economy, nameof(saveData.economy));

            return new ManagerHistoricalRuntimeState(
                saveData.playerTeamSeasonKey,
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

        private static WorldHistorySaveData CreateWorldHistory(WorldHistorySnapshot snapshot)
        {
            var statistics = new SeasonStatisticsSaveData[snapshot.Statistics.Count];
            for (int index = 0; index < snapshot.Statistics.Count; index++)
            {
                SeasonStatistics row = snapshot.Statistics[index];
                statistics[index] = new SeasonStatisticsSaveData
                {
                    playerSeasonId = row.PlayerSeasonId,
                    teamSeasonKey = row.TeamSeasonKey,
                    seasonYear = row.SeasonYear,
                    position = (int)row.Position,
                    plateAppearances = row.PlateAppearances,
                    hits = row.Hits,
                    homeRuns = row.HomeRuns,
                    walks = row.Walks,
                    strikeouts = row.Strikeouts,
                    stolenBases = row.StolenBases,
                    pitchingOuts = row.PitchingOuts,
                    earnedRuns = row.EarnedRuns,
                    pitchingStrikeouts = row.PitchingStrikeouts,
                    defensiveChances = row.DefensiveChances,
                    defensiveOutsAboveAverage = row.DefensiveOutsAboveAverage,
                    fieldingErrors = row.FieldingErrors,
                    isFirstHalf = row.IsFirstHalf,
                    isPostseason = row.IsPostseason,
                    isAllStarGame = row.IsAllStarGame
                };
            }
            Array.Sort(statistics, CompareStatistics);

            var awards = new WorldAwardEntrySaveData[snapshot.Awards.Entries.Count];
            for (int index = 0; index < snapshot.Awards.Entries.Count; index++)
            {
                WorldAwardEntry award = snapshot.Awards.Entries[index];
                awards[index] = new WorldAwardEntrySaveData
                {
                    seasonYear = award.SeasonYear,
                    awardType = (int)award.AwardType,
                    playerSeasonId = award.PlayerSeasonId,
                    position = (int)award.Position
                };
            }
            Array.Sort(awards, CompareAwards);

            return new WorldHistorySaveData
            {
                recordMode = (int)snapshot.RecordMode,
                worldHistorySeed = snapshot.WorldHistorySeed,
                statistics = statistics,
                awards = awards
            };
        }

        private static WorldHistorySnapshot RestoreWorldHistory(WorldHistorySaveData source)
        {
            ValidateEnum<WorldRecordMode>(source.recordMode, nameof(source.recordMode));
            SeasonStatisticsSaveData[] statisticsData = Require(source.statistics, nameof(source.statistics));
            WorldAwardEntrySaveData[] awardData = Require(source.awards, nameof(source.awards));

            var statistics = new SeasonStatistics[statisticsData.Length];
            for (int index = 0; index < statistics.Length; index++)
            {
                SeasonStatisticsSaveData row = Require(statisticsData[index], nameof(source.statistics));
                ValidateEnum<PlayerPosition>(row.position, nameof(row.position));
                statistics[index] = new SeasonStatistics(
                    row.playerSeasonId,
                    row.teamSeasonKey,
                    row.seasonYear,
                    (PlayerPosition)row.position,
                    row.plateAppearances,
                    row.hits,
                    row.homeRuns,
                    row.walks,
                    row.strikeouts,
                    row.stolenBases,
                    row.pitchingOuts,
                    row.earnedRuns,
                    row.pitchingStrikeouts,
                    row.defensiveChances,
                    row.defensiveOutsAboveAverage,
                    row.fieldingErrors,
                    row.isFirstHalf,
                    row.isPostseason,
                    row.isAllStarGame);
            }

            var awards = new WorldAwardEntry[awardData.Length];
            for (int index = 0; index < awards.Length; index++)
            {
                WorldAwardEntrySaveData award = Require(awardData[index], nameof(source.awards));
                ValidateEnum<WorldAwardType>(award.awardType, nameof(award.awardType));
                ValidateEnum<PlayerPosition>(award.position, nameof(award.position));
                awards[index] = new WorldAwardEntry(
                    award.seasonYear,
                    (WorldAwardType)award.awardType,
                    award.playerSeasonId,
                    (PlayerPosition)award.position);
            }

            return new WorldHistorySnapshot(
                (WorldRecordMode)source.recordMode,
                source.worldHistorySeed,
                statistics,
                new WorldAwardRecord(awards));
        }

        private static WorldCardCatalogSaveData CreateCardCatalog(WorldCardCatalog catalog)
        {
            var cards = new PlayerCardSaveData[catalog.Cards.Count];
            var playerSeasons = new Dictionary<string, PlayerSeasonSaveData>(StringComparer.Ordinal);
            for (int index = 0; index < catalog.Cards.Count; index++)
            {
                PlayerCardDefinition card = catalog.Cards[index];
                var modifiers = new int[PlayerAbilityCatalog.AbilityCount];
                for (int abilityIndex = 0; abilityIndex < modifiers.Length; abilityIndex++)
                    modifiers[abilityIndex] = card.GetModifier((PlayerAbility)abilityIndex);
                cards[index] = new PlayerCardSaveData
                {
                    cardId = card.CardId,
                    playerSeasonId = card.PlayerSeasonId,
                    edition = (int)card.Edition,
                    editionStatModifiers = modifiers
                };

                if (!playerSeasons.ContainsKey(card.PlayerSeasonId))
                {
                    PlayerSeasonDefinition season = catalog.GetPlayerSeason(card);
                    playerSeasons.Add(season.PlayerSeasonId, new PlayerSeasonSaveData
                    {
                        playerSeasonId = season.PlayerSeasonId,
                        playerPersonId = season.PlayerPersonId,
                        originYear = season.OriginYear,
                        originFranchiseId = season.OriginFranchiseId,
                        originTeamSeasonKey = season.OriginTeamSeasonKey,
                        position = (int)season.Position,
                        pitcherRole = (int)season.PitcherRole,
                        playerType = (int)season.PlayerType,
                        registrationType = (int)season.RegistrationType,
                        baseAttributes = season.CreateBaseAttributes().ToArray(),
                        cost = season.Cost,
                        trainingCeiling = season.CreateTrainingCeiling().ToArray()
                    });
                }
            }
            Array.Sort(cards, (left, right) => StringComparer.Ordinal.Compare(left.cardId, right.cardId));

            var seasons = new PlayerSeasonSaveData[playerSeasons.Count];
            int seasonIndex = 0;
            foreach (PlayerSeasonSaveData season in playerSeasons.Values)
                seasons[seasonIndex++] = season;
            Array.Sort(seasons, (left, right) => StringComparer.Ordinal.Compare(left.playerSeasonId, right.playerSeasonId));

            return new WorldCardCatalogSaveData { playerSeasons = seasons, cards = cards };
        }

        private static WorldCardCatalog RestoreCardCatalog(WorldCardCatalogSaveData source)
        {
            PlayerSeasonSaveData[] seasonData = Require(source.playerSeasons, nameof(source.playerSeasons));
            PlayerCardSaveData[] cardData = Require(source.cards, nameof(source.cards));
            var seasons = new PlayerSeasonDefinition[seasonData.Length];
            for (int index = 0; index < seasons.Length; index++)
            {
                PlayerSeasonSaveData season = Require(seasonData[index], nameof(source.playerSeasons));
                ValidateEnum<PlayerPosition>(season.position, nameof(season.position));
                ValidateEnum<PitcherRole>(season.pitcherRole, nameof(season.pitcherRole));
                ValidateEnum<PlayerType>(season.playerType, nameof(season.playerType));
                ValidateEnum<RegistrationType>(season.registrationType, nameof(season.registrationType));
                seasons[index] = new PlayerSeasonDefinition(
                    season.playerSeasonId,
                    season.playerPersonId,
                    season.originYear,
                    season.originFranchiseId,
                    season.originTeamSeasonKey,
                    (PlayerPosition)season.position,
                    (PitcherRole)season.pitcherRole,
                    (PlayerType)season.playerType,
                    (RegistrationType)season.registrationType,
                    new AbilityRatings(Require(season.baseAttributes, nameof(season.baseAttributes))),
                    season.cost,
                    new AbilityRatings(Require(season.trainingCeiling, nameof(season.trainingCeiling))));
            }

            var cards = new PlayerCardDefinition[cardData.Length];
            for (int index = 0; index < cards.Length; index++)
            {
                PlayerCardSaveData card = Require(cardData[index], nameof(source.cards));
                ValidateEnum<PlayerCardEdition>(card.edition, nameof(card.edition));
                cards[index] = new PlayerCardDefinition(
                    card.cardId,
                    card.playerSeasonId,
                    (PlayerCardEdition)card.edition,
                    Require(card.editionStatModifiers, nameof(card.editionStatModifiers)));
            }
            return new WorldCardCatalog(seasons, cards);
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

        private static int CompareStatistics(SeasonStatisticsSaveData left, SeasonStatisticsSaveData right)
        {
            int comparison = left.seasonYear.CompareTo(right.seasonYear);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(left.playerSeasonId, right.playerSeasonId);
            if (comparison != 0) return comparison;
            comparison = left.isPostseason.CompareTo(right.isPostseason);
            if (comparison != 0) return comparison;
            comparison = left.isAllStarGame.CompareTo(right.isAllStarGame);
            if (comparison != 0) return comparison;
            return left.isFirstHalf.CompareTo(right.isFirstHalf);
        }

        private static int CompareAwards(WorldAwardEntrySaveData left, WorldAwardEntrySaveData right)
        {
            int comparison = left.seasonYear.CompareTo(right.seasonYear);
            if (comparison != 0) return comparison;
            comparison = left.awardType.CompareTo(right.awardType);
            if (comparison != 0) return comparison;
            comparison = left.position.CompareTo(right.position);
            if (comparison != 0) return comparison;
            return StringComparer.Ordinal.Compare(left.playerSeasonId, right.playerSeasonId);
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
