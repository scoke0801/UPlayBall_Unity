using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Core.Historical
{
    /// <summary>선수의 1군 외국인 등록 제한 판정에 사용하는 Baked 등록 유형이다.</summary>
    public enum RegistrationType
    {
        Domestic,
        Foreign
    }

    /// <summary>한 가상 인물이 Baked 다년도 커리어에서 유지하는 성장 성향이다.</summary>
    public sealed class PersonPotentialTrait
    {
        private readonly int[] _abilityBiases;

        public PersonPotentialTrait(IReadOnlyList<int> abilityBiases)
        {
            if (abilityBiases == null)
                throw new ArgumentNullException(nameof(abilityBiases));
            if (abilityBiases.Count != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("모든 능력치의 성장 성향이 필요합니다.", nameof(abilityBiases));

            _abilityBiases = new int[abilityBiases.Count];
            for (int index = 0; index < abilityBiases.Count; index++)
            {
                int value = abilityBiases[index];
                if (value < 0 || value > 100)
                    throw new ArgumentOutOfRangeException(nameof(abilityBiases), "성장 성향은 0~100이어야 합니다.");
                _abilityBiases[index] = value;
            }
        }

        public int Get(PlayerAbility ability)
        {
            if (ability < 0 || ability >= PlayerAbility.Count)
                throw new ArgumentOutOfRangeException(nameof(ability));
            return _abilityBiases[(int)ability];
        }
    }

    /// <summary>두 게임 모드가 공유하는 가상 선수 인물 단위의 읽기 전용 Baked 정의다.</summary>
    public sealed class PlayerPersonDefinition
    {
        public PlayerPersonDefinition(
            string playerPersonId,
            string fictionalName,
            int birthYear,
            Handedness bats,
            Handedness throws,
            PlayerPosition primaryPosition,
            RegistrationType registrationType,
            int careerStartYear,
            int careerEndYear,
            PersonPotentialTrait potentialTrait)
        {
            if (string.IsNullOrWhiteSpace(playerPersonId))
                throw new ArgumentException("PlayerPersonId는 비어 있을 수 없습니다.", nameof(playerPersonId));
            if (string.IsNullOrWhiteSpace(fictionalName))
                throw new ArgumentException("가상 선수 이름은 비어 있을 수 없습니다.", nameof(fictionalName));
            if (birthYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(birthYear));
            if (throws == Handedness.Switch)
                throw new ArgumentException("투구 손은 Switch일 수 없습니다.", nameof(throws));
            if (primaryPosition == PlayerPosition.Unknown)
                throw new ArgumentException("주 포지션이 필요합니다.", nameof(primaryPosition));
            if (careerStartYear <= 0 || careerEndYear < careerStartYear)
                throw new ArgumentOutOfRangeException(nameof(careerEndYear));

            PlayerPersonId = playerPersonId.Trim();
            FictionalName = fictionalName.Trim();
            BirthYear = birthYear;
            Bats = bats;
            Throws = throws;
            PrimaryPosition = primaryPosition;
            RegistrationType = registrationType;
            CareerStartYear = careerStartYear;
            CareerEndYear = careerEndYear;
            PotentialTrait = potentialTrait ?? throw new ArgumentNullException(nameof(potentialTrait));
        }

        public string PlayerPersonId { get; }
        public string FictionalName { get; }
        public int BirthYear { get; }
        public Handedness Bats { get; }
        public Handedness Throws { get; }
        public PlayerPosition PrimaryPosition { get; }
        public RegistrationType RegistrationType { get; }
        public int CareerStartYear { get; }
        public int CareerEndYear { get; }
        public PersonPotentialTrait PotentialTrait { get; }
    }

    /// <summary>두 게임 모드가 공유하는 선수의 한 시즌 Baked 능력치와 Origin을 보관한다.</summary>
    public sealed class PlayerSeasonDefinition
    {
        private readonly AbilityRatings _baseAttributes;
        private readonly AbilityRatings _trainingCeiling;

        public PlayerSeasonDefinition(
            string playerSeasonId,
            string playerPersonId,
            int originYear,
            string originFranchiseId,
            string originTeamSeasonKey,
            PlayerPosition position,
            PitcherRole pitcherRole,
            PlayerType playerType,
            RegistrationType registrationType,
            AbilityRatings baseAttributes,
            int cost,
            AbilityRatings trainingCeiling)
        {
            PlayerSeasonId = RequireId(playerSeasonId, nameof(playerSeasonId));
            PlayerPersonId = RequireId(playerPersonId, nameof(playerPersonId));
            OriginFranchiseId = RequireId(originFranchiseId, nameof(originFranchiseId));
            OriginTeamSeasonKey = RequireId(originTeamSeasonKey, nameof(originTeamSeasonKey));
            if (originYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            if (position == PlayerPosition.Unknown)
                throw new ArgumentException("본래 포지션이 필요합니다.", nameof(position));
            if (cost < 1 || cost > 10)
                throw new ArgumentOutOfRangeException(nameof(cost), "Cost는 1~10이어야 합니다.");

            _baseAttributes = (baseAttributes ?? throw new ArgumentNullException(nameof(baseAttributes))).Clone();
            _trainingCeiling = (trainingCeiling ?? throw new ArgumentNullException(nameof(trainingCeiling))).Clone();
            for (int index = 0; index < PlayerAbilityCatalog.AbilityCount; index++)
            {
                var ability = (PlayerAbility)index;
                if (_trainingCeiling.Get(ability) < _baseAttributes.Get(ability))
                    throw new ArgumentException("TrainingCeiling은 BaseAttributes보다 낮을 수 없습니다.", nameof(trainingCeiling));
            }

            OriginYear = originYear;
            Position = position;
            PitcherRole = pitcherRole;
            PlayerType = playerType;
            RegistrationType = registrationType;
            Cost = cost;
        }

        public string PlayerSeasonId { get; }
        public string PlayerPersonId { get; }
        public int OriginYear { get; }
        public string OriginFranchiseId { get; }
        public string OriginTeamSeasonKey { get; }
        public PlayerPosition Position { get; }
        public PitcherRole PitcherRole { get; }
        public PlayerType PlayerType { get; }
        public RegistrationType RegistrationType { get; }
        public int Cost { get; }

        public AbilityRatings CreateBaseAttributes() => _baseAttributes.Clone();
        public AbilityRatings CreateTrainingCeiling() => _trainingCeiling.Clone();

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>선수 카드가 가질 수 있는 유일한 네 Edition을 정의한다.</summary>
    public enum PlayerCardEdition
    {
        Normal,
        AllStar,
        GoldenGlove,
        Mvp
    }

    /// <summary>한 PlayerSeason과 Edition의 안정 ID 및 고정 능력치 보정을 보관한다.</summary>
    public sealed class PlayerCardDefinition
    {
        private readonly int[] _editionStatModifiers;

        public PlayerCardDefinition(
            string cardId,
            string playerSeasonId,
            PlayerCardEdition edition,
            IReadOnlyList<int> editionStatModifiers)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            if (string.IsNullOrWhiteSpace(playerSeasonId))
                throw new ArgumentException("PlayerSeasonId는 비어 있을 수 없습니다.", nameof(playerSeasonId));
            if (editionStatModifiers == null || editionStatModifiers.Count != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("모든 능력치의 Edition 보정값이 필요합니다.", nameof(editionStatModifiers));

            CardId = cardId.Trim();
            PlayerSeasonId = playerSeasonId.Trim();
            Edition = edition;
            _editionStatModifiers = new int[editionStatModifiers.Count];
            for (int index = 0; index < editionStatModifiers.Count; index++)
                _editionStatModifiers[index] = editionStatModifiers[index];
        }

        public string CardId { get; }
        public string PlayerSeasonId { get; }
        public PlayerCardEdition Edition { get; }
        public int GetModifier(PlayerAbility ability) => _editionStatModifiers[(int)ability];

        public static string CreateStableCardId(string playerSeasonId, PlayerCardEdition edition)
        {
            if (string.IsNullOrWhiteSpace(playerSeasonId))
                throw new ArgumentException("PlayerSeasonId는 비어 있을 수 없습니다.", nameof(playerSeasonId));
            return playerSeasonId.Trim() + ":" + edition;
        }
    }

    /// <summary>Offline Bake된 가상 연도 구단과 변경되지 않는 초기 Core25 참조를 보관한다.</summary>
    public sealed class TeamSeasonDefinition
    {
        private readonly string[] _allNormalCardIds;
        private readonly string[] _core25CardIds;

        public TeamSeasonDefinition(
            string teamSeasonKey,
            string franchiseId,
            int originYear,
            IReadOnlyList<string> allNormalCardIds,
            IReadOnlyList<string> core25CardIds,
            double referenceStrength)
        {
            TeamSeasonKey = RequireId(teamSeasonKey, nameof(teamSeasonKey));
            FranchiseId = RequireId(franchiseId, nameof(franchiseId));
            if (originYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(originYear));
            OriginYear = originYear;
            _allNormalCardIds = CopyIds(allNormalCardIds, nameof(allNormalCardIds));
            _core25CardIds = CopyIds(core25CardIds, nameof(core25CardIds));
            if (_core25CardIds.Length != 25)
                throw new ArgumentException("Core25는 정확히 25장이어야 합니다.", nameof(core25CardIds));
            ReferenceStrength = referenceStrength;
        }

        public string TeamSeasonKey { get; }
        public string FranchiseId { get; }
        public int OriginYear { get; }
        public IReadOnlyList<string> AllNormalCardIds => _allNormalCardIds;
        public IReadOnlyList<string> Core25CardIds => _core25CardIds;
        public double ReferenceStrength { get; }

        private static string[] CopyIds(IReadOnlyList<string> source, string parameterName)
        {
            if (source == null)
                throw new ArgumentNullException(parameterName);
            var result = new string[source.Count];
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Count; index++)
            {
                string id = RequireId(source[index], parameterName);
                if (!unique.Add(id))
                    throw new ArgumentException("카드 ID는 중복될 수 없습니다.", parameterName);
                result[index] = id;
            }
            return result;
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>같은 입력 버전과 Seed에서 재현되는 Offline Bake Manifest다.</summary>
    public sealed class SyntheticContentManifest
    {
        public SyntheticContentManifest(
            string referenceDataVersion,
            string generatorVersion,
            string balanceVersion,
            ulong generationSeed,
            string contentHash)
        {
            ReferenceDataVersion = Require(referenceDataVersion, nameof(referenceDataVersion));
            GeneratorVersion = Require(generatorVersion, nameof(generatorVersion));
            BalanceVersion = Require(balanceVersion, nameof(balanceVersion));
            ContentHash = Require(contentHash, nameof(contentHash));
            GenerationSeed = generationSeed;
        }

        public string ReferenceDataVersion { get; }
        public string GeneratorVersion { get; }
        public string BalanceVersion { get; }
        public ulong GenerationSeed { get; }
        public string ContentHash { get; }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Manifest 값은 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }
}
