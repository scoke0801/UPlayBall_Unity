using System;
using System.Collections.Generic;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>선수 커리어의 명시적 Historical 경로가 공통 Baked 원본만 소비하는지 검증한다.</summary>
    public sealed class CareerBakedContentIntegrationTests
    {
        [Test]
        public void HistoricalNewGame_BakedCore25로모든리그를만들고Runtime생성경로를우회한다()
        {
            CareerBakedContent content = CreateContent(WorldRecordMode.OriginalHistory, 91001UL);
            var provider = new RecordingProvider(content);
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault()
                .WithBakedHistoricalContent(provider, WorldRecordMode.OriginalHistory);
            NewGameFlow flow = CreatePlayerCard(configuration, 91001UL);

            flow.GenerateOffers();

            Assert.That(provider.LoadCount, Is.EqualTo(1));
            Assert.That(provider.LastRequest.RecordMode, Is.EqualTo(WorldRecordMode.OriginalHistory));
            Assert.That(provider.LastRequest.WorldHistorySeed, Is.EqualTo(91001UL));
            Assert.That(flow.State.SetupResult.Teams.Length, Is.EqualTo(10));
            Assert.That(flow.State.SetupResult.Teams[0].Name, Does.StartWith("Baked "));

            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();

            Assert.That(flow.Career.World.Leagues.Count, Is.EqualTo(10));
            for (int index = 0; index < flow.Career.World.Leagues.Count; index++)
                Assert.That(flow.Career.World.Leagues[index].Teams.Count, Is.EqualTo(10));
            Assert.That(flow.Career.World.Players.Count, Is.EqualTo(2_500));
            PlayerState bakedPlayer = FindBakedPlayer(flow.Career.World.Players);
            Assert.That(bakedPlayer.GrowthState, Is.Not.Null);
            Assert.That(
                bakedPlayer.GrowthState.BaseAbilities.Get(PlayerAbility.Contact),
                Is.EqualTo(60));
        }

        [Test]
        public void HistoricalNewGame_같은Baked원본과Seed면일반선수ID가같다()
        {
            CareerBakedContent content = CreateContent(WorldRecordMode.SimulatedHistory, 77UL);
            NewGameConfiguration firstConfiguration = NewGameConfiguration.CreateDefault()
                .WithBakedHistoricalContent(new RecordingProvider(content), WorldRecordMode.SimulatedHistory);
            NewGameConfiguration secondConfiguration = NewGameConfiguration.CreateDefault()
                .WithBakedHistoricalContent(new RecordingProvider(content), WorldRecordMode.SimulatedHistory);
            NewGameFlow first = CreatePlayerCard(firstConfiguration, 77UL);
            NewGameFlow second = CreatePlayerCard(secondConfiguration, 77UL);

            CompleteContract(first);
            CompleteContract(second);

            Assert.That(second.Career.World.Players.Count, Is.EqualTo(first.Career.World.Players.Count));
            for (int index = 0; index < first.Career.World.Players.Count; index++)
            {
                Assert.That(
                    second.Career.World.Players[index].PlayerId,
                    Is.EqualTo(first.Career.World.Players[index].PlayerId));
            }
        }

        [Test]
        public void CareerSaveRoot_감독모드OwnedEconomy타입을소유하지않는다()
        {
            Type[] careerTypes =
            {
                typeof(CareerState),
                typeof(WorldState),
                typeof(LeagueState),
                typeof(TeamState),
                typeof(PlayerState)
            };
            Type[] forbiddenTypes =
            {
                typeof(OwnedPlayerCardState),
                typeof(ManagerEconomyState),
                typeof(CardTrainingState)
            };

            for (int typeIndex = 0; typeIndex < careerTypes.Length; typeIndex++)
            {
                PropertyInfo[] properties = careerTypes[typeIndex].GetProperties(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
                {
                    for (int forbiddenIndex = 0; forbiddenIndex < forbiddenTypes.Length; forbiddenIndex++)
                    {
                        Assert.That(
                            ContainsType(properties[propertyIndex].PropertyType, forbiddenTypes[forbiddenIndex]),
                            Is.False,
                            $"{careerTypes[typeIndex].Name}.{properties[propertyIndex].Name}에 감독모드 소유 경제가 유출됐습니다.");
                    }
                }

                FieldInfo[] fields = careerTypes[typeIndex].GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    for (int forbiddenIndex = 0; forbiddenIndex < forbiddenTypes.Length; forbiddenIndex++)
                    {
                        Assert.That(
                            ContainsType(fields[fieldIndex].FieldType, forbiddenTypes[forbiddenIndex]),
                            Is.False,
                            $"{careerTypes[typeIndex].Name}.{fields[fieldIndex].Name}에 감독모드 소유 경제가 유출됐습니다.");
                    }
                }
            }
        }

        [Test]
        public void BakedConfiguration_Provider없이는선택할수없다()
        {
            NewGameConfiguration source = NewGameConfiguration.CreateDefault();
            Assert.Throws<ArgumentException>(() => new NewGameConfiguration(
                source.Balance,
                source.TeamCount,
                source.FirstSeasonYear,
                source.StartingAge,
                source.Archetypes,
                source.TeamIdentities,
                source.PlayerNamePool,
                source.WorldGeneration,
                source.CareerCreationRules,
                source.TeamEmblemCount,
                NewGameContentSource.BakedHistorical,
                bakedContentProvider: null));
        }

        private static void CompleteContract(NewGameFlow flow)
        {
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
        }

        private static PlayerState FindBakedPlayer(IReadOnlyList<PlayerState> players)
        {
            for (int index = 0; index < players.Count; index++)
                if (players[index].Name.StartsWith("Baked ", StringComparison.Ordinal)) return players[index];
            throw new AssertionException("Baked 일반 선수를 찾을 수 없습니다.");
        }

        private static NewGameFlow CreatePlayerCard(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("커리어 선수", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(60, 55, 58, 57, 62, 60));
            return flow;
        }

        private static CareerBakedContent CreateContent(WorldRecordMode recordMode, ulong worldHistorySeed)
        {
            var persons = new List<PlayerPersonDefinition>(2_500);
            var seasons = new List<PlayerSeasonDefinition>(2_500);
            var cards = new List<PlayerCardDefinition>(2_500);
            var teams = new List<CareerBakedTeamRuntimeDefinition>(100);
            TeamArchetypeProfile archetype = TeamArchetypeLibrary.CreateDefaultPool()[0];
            int teamId = 1;
            for (int gradeIndex = 0; gradeIndex < 10; gradeIndex++)
            {
                for (int teamIndex = 0; teamIndex < 10; teamIndex++)
                {
                    string teamSeasonKey = $"F{teamIndex:D2}_{2000 + gradeIndex}";
                    var entries = new ActiveRosterEntry[25];
                    var cardIds = new string[25];
                    for (int playerIndex = 0; playerIndex < 25; playerIndex++)
                    {
                        string personId = $"P_{gradeIndex:D2}_{teamIndex:D2}_{playerIndex:D2}";
                        string seasonId = $"S_{gradeIndex:D2}_{teamIndex:D2}_{playerIndex:D2}";
                        string cardId = PlayerCardDefinition.CreateStableCardId(
                            seasonId,
                            PlayerCardEdition.Normal);
                        ActiveRosterRole role = GetRole(playerIndex);
                        PlayerPosition position = GetPosition(role, playerIndex);
                        PlayerType playerType = playerIndex >= 14 ? PlayerType.Pitcher : PlayerType.Batter;
                        PitcherRole pitcherRole = GetPitcherRole(role);
                        var baseRatings = new AbilityRatings(60);
                        var ceiling = new AbilityRatings(70);
                        persons.Add(new PlayerPersonDefinition(
                            personId,
                            $"Baked {personId}",
                            birthYear: 1995,
                            Handedness.Right,
                            Handedness.Right,
                            position,
                            RegistrationType.Domestic,
                            careerStartYear: 2018,
                            careerEndYear: 2035,
                            new PersonPotentialTrait(new int[12])));
                        seasons.Add(new PlayerSeasonDefinition(
                            seasonId,
                            personId,
                            2000 + gradeIndex,
                            $"F{teamIndex:D2}",
                            teamSeasonKey,
                            position,
                            pitcherRole,
                            playerType,
                            RegistrationType.Domestic,
                            baseRatings,
                            cost: 5,
                            ceiling));
                        cards.Add(new PlayerCardDefinition(cardId, seasonId, PlayerCardEdition.Normal, new int[12]));
                        entries[playerIndex] = new ActiveRosterEntry(
                            cardId,
                            seasonId,
                            personId,
                            RegistrationType.Domestic,
                            role);
                        cardIds[playerIndex] = cardId;
                    }

                    var teamSeason = new TeamSeasonDefinition(
                        teamSeasonKey,
                        $"F{teamIndex:D2}",
                        2000 + gradeIndex,
                        cardIds,
                        cardIds,
                        referenceStrength: 60d);
                    teams.Add(new CareerBakedTeamRuntimeDefinition(
                        teamId++,
                        (LeagueGrade)gradeIndex,
                        teamSeason,
                        new CurrentRosterState(teamSeasonKey, entries),
                        new TeamIdentityDefinition(
                            $"Baked {(LeagueGrade)gradeIndex} {teamIndex + 1}",
                            new TeamColor((byte)(20 + teamIndex), 100, 150)),
                        archetype,
                        emblemId: teamId));
                }
            }

            var catalog = new WorldCardCatalog(seasons, cards);
            var history = new WorldHistorySnapshot(
                recordMode,
                worldHistorySeed,
                Array.Empty<SeasonStatistics>(),
                new WorldAwardRecord(Array.Empty<WorldAwardEntry>()));
            return new CareerBakedContent(
                new SyntheticContentManifest("test-reference", "test-generator", "test-balance", 1UL, "test-hash"),
                persons,
                catalog,
                teams,
                history);
        }

        private static ActiveRosterRole GetRole(int playerIndex)
        {
            if (playerIndex < 9) return (ActiveRosterRole)playerIndex;
            if (playerIndex < 14) return ActiveRosterRole.BenchHitter;
            return (ActiveRosterRole)((int)ActiveRosterRole.StartingPitcher1 + playerIndex - 14);
        }

        private static PlayerPosition GetPosition(ActiveRosterRole role, int playerIndex)
        {
            if (role >= ActiveRosterRole.StartingCatcher && role <= ActiveRosterRole.StartingDesignatedHitter)
                return ActiveRosterCompositionRule.Standard.GetAssignedPosition(role);
            if (role == ActiveRosterRole.BenchHitter)
                return (PlayerPosition)((int)PlayerPosition.Catcher + playerIndex - 9);
            return role <= ActiveRosterRole.StartingPitcher5
                ? PlayerPosition.StartingPitcher
                : PlayerPosition.ReliefPitcher;
        }

        private static PitcherRole GetPitcherRole(ActiveRosterRole role)
        {
            if (!ActiveRosterCompositionRule.Standard.IsPitcherRole(role))
                return PitcherRole.MiddleRelief;
            return ActiveRosterCompositionRule.Standard.GetAssignedPitcherRole(role);
        }

        private static bool ContainsType(Type candidate, Type forbidden)
        {
            if (candidate == forbidden)
                return true;
            if (candidate.IsArray)
                return ContainsType(candidate.GetElementType(), forbidden);
            if (!candidate.IsGenericType)
                return false;
            Type[] arguments = candidate.GetGenericArguments();
            for (int index = 0; index < arguments.Length; index++)
                if (ContainsType(arguments[index], forbidden)) return true;
            return false;
        }

        private sealed class RecordingProvider : ICareerBakedContentProvider
        {
            private readonly CareerBakedContent _content;

            public RecordingProvider(CareerBakedContent content)
            {
                _content = content;
            }

            public int LoadCount { get; private set; }
            public CareerBakedContentRequest LastRequest { get; private set; }

            public CareerBakedContent Load(CareerBakedContentRequest request)
            {
                LoadCount++;
                LastRequest = request;
                return _content;
            }
        }
    }
}
