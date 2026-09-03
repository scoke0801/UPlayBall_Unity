using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Career
{
    /// <summary>새 선수 커리어가 일반 선수 월드를 어디에서 읽는지 명시한다.</summary>
    public enum NewGameContentSource
    {
        Unconfigured,
        ExplicitSyntheticTestFixture,
        BakedHistorical
    }

    /// <summary>Baked Content Provider가 같은 월드 기록 옵션과 Seed를 소비하도록 고정한 요청이다.</summary>
    public readonly struct CareerBakedContentRequest
    {
        public CareerBakedContentRequest(WorldRecordMode recordMode, ulong worldHistorySeed)
        {
            RecordMode = recordMode;
            WorldHistorySeed = worldHistorySeed;
        }

        public WorldRecordMode RecordMode { get; }
        public ulong WorldHistorySeed { get; }
    }

    /// <summary>Editor Bake 결과를 읽어 공통 Definition 기반의 커리어 월드 입력을 반환하는 경계다.</summary>
    public interface ICareerBakedContentProvider
    {
        CareerBakedContent Load(CareerBakedContentRequest request);
    }

    /// <summary>공통 TeamSeason과 CurrentRoster에 커리어 표시·영속 ID만 결합한 Runtime 입력이다.</summary>
    public sealed class CareerBakedTeamRuntimeDefinition
    {
        public CareerBakedTeamRuntimeDefinition(
            int teamId,
            LeagueGrade grade,
            TeamSeasonDefinition teamSeason,
            CurrentRosterState activeRoster,
            TeamIdentityDefinition identity,
            TeamArchetypeProfile archetype,
            int emblemId)
        {
            if (teamId <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamId));
            if (teamSeason == null)
                throw new ArgumentNullException(nameof(teamSeason));
            if (activeRoster == null)
                throw new ArgumentNullException(nameof(activeRoster));
            if (!string.Equals(teamSeason.TeamSeasonKey, activeRoster.TeamSeasonKey, StringComparison.Ordinal))
                throw new ArgumentException("TeamSeason과 CurrentRoster의 TeamSeasonKey가 다릅니다.", nameof(activeRoster));
            if (string.IsNullOrWhiteSpace(identity.Name))
                throw new ArgumentException("가상 구단 표시 이름이 필요합니다.", nameof(identity));
            if (emblemId <= 0)
                throw new ArgumentOutOfRangeException(nameof(emblemId));

            TeamId = teamId;
            Grade = grade;
            TeamSeason = teamSeason;
            ActiveRoster = activeRoster;
            Identity = identity;
            Archetype = archetype;
            EmblemId = emblemId;
        }

        public int TeamId { get; }
        public LeagueGrade Grade { get; }
        public TeamSeasonDefinition TeamSeason { get; }
        public CurrentRosterState ActiveRoster { get; }
        public TeamIdentityDefinition Identity { get; }
        public TeamArchetypeProfile Archetype { get; }
        public int EmblemId { get; }
    }

    /// <summary>두 모드가 공유하는 Baked 선수·카드·로스터와 월드 기록을 한 번 검증한 결과다.</summary>
    public sealed class CareerBakedContent
    {
        private readonly PlayerPersonDefinition[] _persons;
        private readonly CareerBakedTeamRuntimeDefinition[] _teams;
        private readonly Dictionary<string, PlayerPersonDefinition> _personsById;
        private readonly Dictionary<string, CareerBakedTeamRuntimeDefinition> _teamsByKey;
        private readonly CareerBakedTeamRuntimeDefinition[][] _teamsByGrade;

        public CareerBakedContent(
            SyntheticContentManifest manifest,
            IReadOnlyList<PlayerPersonDefinition> persons,
            WorldCardCatalog cardCatalog,
            IReadOnlyList<CareerBakedTeamRuntimeDefinition> teams,
            WorldHistorySnapshot worldHistory)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            CardCatalog = cardCatalog ?? throw new ArgumentNullException(nameof(cardCatalog));
            WorldHistory = worldHistory ?? throw new ArgumentNullException(nameof(worldHistory));
            if (persons == null || persons.Count == 0)
                throw new ArgumentException("Baked PlayerPerson이 필요합니다.", nameof(persons));
            if (teams == null)
                throw new ArgumentNullException(nameof(teams));

            _persons = new PlayerPersonDefinition[persons.Count];
            _personsById = new Dictionary<string, PlayerPersonDefinition>(StringComparer.Ordinal);
            for (int index = 0; index < persons.Count; index++)
            {
                PlayerPersonDefinition person = persons[index]
                    ?? throw new ArgumentException("null PlayerPerson이 있습니다.", nameof(persons));
                if (!_personsById.TryAdd(person.PlayerPersonId, person))
                    throw new ArgumentException("PlayerPersonId는 중복될 수 없습니다.", nameof(persons));
                _persons[index] = person;
            }

            int gradeCount = Enum.GetValues(typeof(LeagueGrade)).Length;
            var gradeTeams = new List<CareerBakedTeamRuntimeDefinition>[gradeCount];
            for (int gradeIndex = 0; gradeIndex < gradeCount; gradeIndex++)
                gradeTeams[gradeIndex] = new List<CareerBakedTeamRuntimeDefinition>();
            _teams = new CareerBakedTeamRuntimeDefinition[teams.Count];
            _teamsByKey = new Dictionary<string, CareerBakedTeamRuntimeDefinition>(StringComparer.Ordinal);
            var teamIds = new HashSet<int>();
            var emblemIds = new HashSet<int>();
            var playerInstanceIds = new HashSet<int>();
            var rosterValidator = new ActiveRosterValidator();
            for (int index = 0; index < teams.Count; index++)
            {
                CareerBakedTeamRuntimeDefinition team = teams[index]
                    ?? throw new ArgumentException("null TeamSeason Runtime 입력이 있습니다.", nameof(teams));
                if (!teamIds.Add(team.TeamId))
                    throw new ArgumentException("커리어 TeamId는 중복될 수 없습니다.", nameof(teams));
                if (!emblemIds.Add(team.EmblemId))
                    throw new ArgumentException("커리어 EmblemId는 중복될 수 없습니다.", nameof(teams));
                if (!_teamsByKey.TryAdd(team.TeamSeason.TeamSeasonKey, team))
                    throw new ArgumentException("TeamSeasonKey는 월드에서 중복될 수 없습니다.", nameof(teams));
                if (!rosterValidator.Validate(team.ActiveRoster).IsValid)
                    throw new ArgumentException("Baked CurrentRoster가 공통 ActiveRoster 규칙을 만족하지 않습니다.", nameof(teams));
                ValidateRosterReferences(team, playerInstanceIds);
                gradeTeams[(int)team.Grade].Add(team);
                _teams[index] = team;
            }

            _teamsByGrade = new CareerBakedTeamRuntimeDefinition[gradeCount][];
            for (int gradeIndex = 0; gradeIndex < gradeCount; gradeIndex++)
            {
                List<CareerBakedTeamRuntimeDefinition> source = gradeTeams[gradeIndex];
                source.Sort(CompareTeams);
                if (source.Count != LeagueInstance.RequiredRegularFranchiseTeamCount)
                {
                    throw new ArgumentException(
                        $"{(LeagueGrade)gradeIndex} LeagueInstance에는 정규 Franchise 구단 10개가 필요합니다.",
                        nameof(teams));
                }
                _teamsByGrade[gradeIndex] = source.ToArray();
            }
        }

        public SyntheticContentManifest Manifest { get; }
        public WorldCardCatalog CardCatalog { get; }
        public WorldHistorySnapshot WorldHistory { get; }
        public IReadOnlyList<PlayerPersonDefinition> Persons => _persons;
        public IReadOnlyList<CareerBakedTeamRuntimeDefinition> Teams => _teams;

        public IReadOnlyList<CareerBakedTeamRuntimeDefinition> GetTeams(LeagueGrade grade)
        {
            if (!Enum.IsDefined(typeof(LeagueGrade), grade))
                throw new ArgumentOutOfRangeException(nameof(grade));
            return _teamsByGrade[(int)grade];
        }

        public PlayerPersonDefinition GetPerson(string playerPersonId)
        {
            if (!_personsById.TryGetValue(playerPersonId, out PlayerPersonDefinition person))
                throw new InvalidOperationException($"PlayerPersonId {playerPersonId}를 찾을 수 없습니다.");
            return person;
        }

        public CareerBakedTeamRuntimeDefinition GetTeam(string teamSeasonKey)
        {
            if (!_teamsByKey.TryGetValue(teamSeasonKey, out CareerBakedTeamRuntimeDefinition team))
                throw new InvalidOperationException($"TeamSeasonKey {teamSeasonKey}를 찾을 수 없습니다.");
            return team;
        }

        private void ValidateRosterReferences(
            CareerBakedTeamRuntimeDefinition team,
            HashSet<int> playerInstanceIds)
        {
            IReadOnlyList<ActiveRosterEntry> entries = team.ActiveRoster.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                ActiveRosterEntry entry = entries[index];
                if (!CardCatalog.TryGetCard(entry.CardId, out PlayerCardDefinition card))
                    throw new ArgumentException("CurrentRoster 카드가 WorldCardCatalog에 없습니다.");
                PlayerSeasonDefinition season = CardCatalog.GetPlayerSeason(card);
                if (card.Edition != PlayerCardEdition.Normal ||
                    !string.Equals(card.PlayerSeasonId, entry.PlayerSeasonId, StringComparison.Ordinal) ||
                    !string.Equals(season.PlayerPersonId, entry.PlayerPersonId, StringComparison.Ordinal) ||
                    season.RegistrationType != entry.RegistrationType ||
                    !string.Equals(season.OriginTeamSeasonKey, team.TeamSeason.TeamSeasonKey, StringComparison.Ordinal) ||
                    !_personsById.ContainsKey(entry.PlayerPersonId))
                {
                    throw new ArgumentException("CurrentRoster의 공통 Person/Season/Normal Card 참조가 일치하지 않습니다.");
                }

                int playerId = CareerBakedContentAdapter.CreateStablePlayerInstanceId(
                    team.TeamSeason.TeamSeasonKey,
                    season.PlayerSeasonId);
                if (!playerInstanceIds.Add(playerId))
                    throw new ArgumentException("Baked ID에서 커리어 PlayerId 충돌이 발생했습니다.");
            }
        }

        private static int CompareTeams(
            CareerBakedTeamRuntimeDefinition left,
            CareerBakedTeamRuntimeDefinition right)
        {
            int idComparison = left.TeamId.CompareTo(right.TeamId);
            return idComparison != 0
                ? idComparison
                : string.CompareOrdinal(left.TeamSeason.TeamSeasonKey, right.TeamSeason.TeamSeasonKey);
        }
    }

    /// <summary>공통 Baked Definition을 기존 Career 런타임 입력으로 손실 없이 변환한다.</summary>
    internal static class CareerBakedContentAdapter
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const int PlayerIdOffset = 100_000_000;
        private const int PlayerIdRange = 1_900_000_000;

        public static GeneratedTeam[] CreateGeneratedTeams(
            CareerBakedContent content,
            LeagueGrade grade,
            Baseball.Core.Balance.PlayerEvaluationBalance evaluationBalance)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            IReadOnlyList<CareerBakedTeamRuntimeDefinition> source = content.GetTeams(grade);
            var result = new GeneratedTeam[source.Count];
            var evaluator = new PlayerValueEvaluator(evaluationBalance);
            for (int teamIndex = 0; teamIndex < source.Count; teamIndex++)
            {
                CareerBakedTeamRuntimeDefinition team = source[teamIndex];
                var competitors = new RosterCompetitor[team.ActiveRoster.Entries.Count];
                var strongestByPosition = new int[(int)PlayerPosition.ReliefPitcher + 1];
                for (int playerIndex = 0; playerIndex < team.ActiveRoster.Entries.Count; playerIndex++)
                {
                    ActiveRosterEntry entry = team.ActiveRoster.Entries[playerIndex];
                    PlayerCardDefinition card = GetCard(content.CardCatalog, entry.CardId);
                    PlayerSeasonDefinition season = content.CardCatalog.GetPlayerSeason(card);
                    PlayerPersonDefinition person = content.GetPerson(season.PlayerPersonId);
                    int playerId = CreateStablePlayerInstanceId(
                        team.TeamSeason.TeamSeasonKey,
                        season.PlayerSeasonId);
                    AbilityRatings ratings = season.CreateBaseAttributes();
                    var player = new Player(
                        playerId,
                        person.FictionalName,
                        season.Position,
                        person.Bats,
                        person.Throws,
                        ratings.ToBatterAttributes(),
                        ratings.ToPitcherAttributes(),
                        nationality: season.RegistrationType == RegistrationType.Foreign ? "외국인" : string.Empty);
                    int overall = evaluator.CalculatePositionValue(player);
                    competitors[playerIndex] = new RosterCompetitor(
                        playerId,
                        person.FictionalName,
                        season.Position,
                        overall);
                    int positionIndex = (int)season.Position;
                    if (overall > strongestByPosition[positionIndex])
                        strongestByPosition[positionIndex] = overall;
                }

                var positionNeeds = new int[strongestByPosition.Length];
                for (int positionIndex = 0; positionIndex < strongestByPosition.Length; positionIndex++)
                    positionNeeds[positionIndex] = 100 - strongestByPosition[positionIndex];
                result[teamIndex] = new GeneratedTeam(
                    team.TeamId,
                    team.Identity.Name,
                    team.Archetype,
                    team.Identity.PrimaryColor,
                    positionNeeds,
                    competitors,
                    team.EmblemId);
            }
            return result;
        }

        public static PlayerState CreatePlayerState(
            CareerBakedContent content,
            CareerBakedTeamRuntimeDefinition team,
            ActiveRosterEntry entry,
            LeagueId leagueId,
            int firstSeasonYear)
        {
            PlayerCardDefinition card = GetCard(content.CardCatalog, entry.CardId);
            PlayerSeasonDefinition season = content.CardCatalog.GetPlayerSeason(card);
            PlayerPersonDefinition person = content.GetPerson(season.PlayerPersonId);
            AbilityRatings baseRatings = season.CreateBaseAttributes();
            int age = firstSeasonYear - person.BirthYear;
            if (age < 16) age = 16;
            if (age > 60) age = 60;
            int playerId = CreateStablePlayerInstanceId(team.TeamSeason.TeamSeasonKey, season.PlayerSeasonId);
            var player = new PlayerState(
                NewGameFlow.CurrentSaveVersion,
                playerId,
                person.FictionalName,
                season.RegistrationType == RegistrationType.Foreign ? "외국인" : string.Empty,
                age,
                season.Position,
                person.Bats,
                person.Throws,
                baseRatings.ToBatterAttributes(),
                baseRatings.ToPitcherAttributes(),
                team.TeamId,
                leagueId);
            player.AttachGrowthState(new PlayerGrowthState(
                playerId,
                age,
                season.PlayerType,
                baseRatings,
                season.CreateTrainingCeiling(),
                WorkEthicGrade.Normal,
                condition: 90,
                fatigue: 0,
                durability: 70));
            player.InitializeSeasonStatus(condition: 90, managerEvaluation: 50);
            return player;
        }

        public static int CreateStablePlayerInstanceId(string teamSeasonKey, string playerSeasonId)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey가 필요합니다.", nameof(teamSeasonKey));
            if (string.IsNullOrWhiteSpace(playerSeasonId))
                throw new ArgumentException("PlayerSeasonId가 필요합니다.", nameof(playerSeasonId));
            uint hash = FnvOffsetBasis;
            AddHash(ref hash, teamSeasonKey);
            AddHash(ref hash, "|");
            AddHash(ref hash, playerSeasonId);
            return PlayerIdOffset + (int)(hash % PlayerIdRange);
        }

        private static PlayerCardDefinition GetCard(WorldCardCatalog catalog, string cardId)
        {
            if (!catalog.TryGetCard(cardId, out PlayerCardDefinition card))
                throw new InvalidOperationException($"CardId {cardId}를 찾을 수 없습니다.");
            return card;
        }

        private static void AddHash(ref uint hash, string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                hash ^= (byte)character;
                hash *= FnvPrime;
                hash ^= (byte)(character >> 8);
                hash *= FnvPrime;
            }
        }
    }
}
