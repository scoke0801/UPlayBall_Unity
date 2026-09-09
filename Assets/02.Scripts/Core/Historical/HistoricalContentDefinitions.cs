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

    /// <summary>PlayerSeason 능력치가 공식 Source 시즌에서 왔는지 명시적 보충 선수에서 왔는지 구분한다.</summary>
    public enum PlayerDataProvenance
    {
        SourceBacked,
        ReplacementGenerated
    }

    /// <summary>해당 시즌의 Natural PitcherRole을 뒷받침하는 기록의 신뢰 수준이다.</summary>
    public enum PitcherRoleConfidence
    {
        Low,
        Medium,
        High
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

    /// <summary>Source Person과 1:1로 대응하며 표시 이름을 포함하지 않는 읽기 전용 Canonical 정의다.</summary>
    public sealed class PlayerPersonDefinition
    {
        /// <summary>표시 이름 없이 Canonical Person을 만든다.</summary>
        public PlayerPersonDefinition(
            string playerPersonId,
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
            if (birthYear <= 0)
                throw new ArgumentOutOfRangeException(nameof(birthYear));
            if (throws == Handedness.Switch)
                throw new ArgumentException("투구 손은 Switch일 수 없습니다.", nameof(throws));
            if (primaryPosition == PlayerPosition.Unknown)
                throw new ArgumentException("주 포지션이 필요합니다.", nameof(primaryPosition));
            if (careerStartYear <= 0 || careerEndYear < careerStartYear)
                throw new ArgumentOutOfRangeException(nameof(careerEndYear));

            PlayerPersonId = playerPersonId.Trim();
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
        private readonly IReadOnlyList<PitchRepertoireEntry> _pitchRepertoire;
        private readonly IReadOnlyList<PositionProficiency> _secondaryPositions;

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
            AbilityRatings trainingCeiling,
            PlayerDataProvenance dataProvenance = PlayerDataProvenance.SourceBacked,
            PitcherRoleConfidence pitcherRoleConfidence = PitcherRoleConfidence.High,
            IReadOnlyList<PitchRepertoireEntry> pitchRepertoire = null,
            PitchDataSourceKind pitchDataSourceKind = PitchDataSourceKind.Synthetic,
            string pitchBalanceVersion = "",
            bool isPositionEvidenceMissing = false,
            IReadOnlyList<PositionProficiency> secondaryPositions = null)
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
            DataProvenance = dataProvenance;
            PitcherRoleConfidence = pitcherRoleConfidence;
            if (!Enum.IsDefined(typeof(PitchDataSourceKind), pitchDataSourceKind))
                throw new ArgumentOutOfRangeException(nameof(pitchDataSourceKind));
            var pitches = new PitchRepertoireEntry[pitchRepertoire?.Count ?? 0];
            if (pitches.Length > 6 || (playerType != PlayerType.Pitcher && pitches.Length != 0))
                throw new ArgumentException("투수만 최대 6구종을 보유할 수 있습니다.", nameof(pitchRepertoire));
            int primaryCount = 0;
            for (int index = 0; index < pitches.Length; index++)
            {
                PitchRepertoireEntry entry = pitchRepertoire[index];
                if (entry.DevelopmentAffinity <= 0d || entry.UsagePreference <= 0d)
                    throw new ArgumentException("구종 성장 적성과 사용 선호가 필요합니다.", nameof(pitchRepertoire));
                for (int previous = 0; previous < index; previous++)
                    if (pitches[previous].PitchType == entry.PitchType)
                        throw new ArgumentException("구종이 중복되었습니다.", nameof(pitchRepertoire));
                if (entry.IsPrimary) primaryCount++;
                pitches[index] = entry;
            }
            if (pitches.Length != 0 && (pitches.Length < (cost >= 4 ? 3 : 2) || primaryCount != 1))
                throw new ArgumentException("Cost별 최소 구종 수와 주력 구종 하나가 필요합니다.", nameof(pitchRepertoire));
            _pitchRepertoire = Array.AsReadOnly(pitches);
            PitchDataSourceKind = pitchDataSourceKind;
            PitchBalanceVersion = pitchBalanceVersion ?? string.Empty;
            IsPositionEvidenceMissing = isPositionEvidenceMissing && playerType == PlayerType.Batter;
            var positions = new PositionProficiency[secondaryPositions?.Count ?? 0];
            for (int index = 0; index < positions.Length; index++)
            {
                PositionProficiency entry = secondaryPositions[index];
                if (playerType != PlayerType.Batter || entry.Position < PlayerPosition.Catcher ||
                    entry.Position > PlayerPosition.RightField || entry.Position == position)
                    throw new ArgumentException("부포지션은 주 포지션과 다른 야수 수비 위치여야 합니다.", nameof(secondaryPositions));
                for (int previous = 0; previous < index; previous++)
                    if (positions[previous].Position == entry.Position)
                        throw new ArgumentException("부포지션은 중복될 수 없습니다.", nameof(secondaryPositions));
                positions[index] = entry;
            }
            _secondaryPositions = Array.AsReadOnly(positions);
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
        public PlayerDataProvenance DataProvenance { get; }
        public PitcherRoleConfidence PitcherRoleConfidence { get; }
        public IReadOnlyList<PitchRepertoireEntry> PitchRepertoire => _pitchRepertoire;
        public PitchDataSourceKind PitchDataSourceKind { get; }
        public string PitchBalanceVersion { get; }
        /// <summary>시즌 수비 기록과 보조 출처가 모두 없어 임시 포지션을 사용했는지 나타낸다.</summary>
        public bool IsPositionEvidenceMissing { get; }
        /// <summary>원기록으로 검증된 부포지션 적응도를 경기 입력까지 보존한다.</summary>
        public IReadOnlyList<PositionProficiency> SecondaryPositions => _secondaryPositions;

        public AbilityRatings CreateBaseAttributes() => _baseAttributes.Clone();
        public AbilityRatings CreateTrainingCeiling() => _trainingCeiling.Clone();

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("식별자는 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }

    /// <summary>저장된 기존 숫자를 유지하면서 선수 카드 Edition을 정의한다.</summary>
    public enum PlayerCardEdition
    {
        Normal,
        AllStar,
        GoldenGlove,
        Mvp,
        Rare,
        Ex,
        CareerHigh,
        Legend
    }

    /// <summary>한 PlayerSeason과 Edition의 안정 ID 및 고정 능력치 보정을 보관한다.</summary>
    public sealed class PlayerCardDefinition
    {
        private readonly int[] _editionStatModifiers;

        public PlayerCardDefinition(
            string cardId,
            string playerSeasonId,
            PlayerCardEdition edition,
            IReadOnlyList<int> editionStatModifiers,
            PreferredBattingOrder preferredBattingOrder = PreferredBattingOrder.None,
            string teamColorLineageId = "")
        {
            if (string.IsNullOrWhiteSpace(cardId))
                throw new ArgumentException("CardId는 비어 있을 수 없습니다.", nameof(cardId));
            if (string.IsNullOrWhiteSpace(playerSeasonId))
                throw new ArgumentException("PlayerSeasonId는 비어 있을 수 없습니다.", nameof(playerSeasonId));
            if (editionStatModifiers == null || editionStatModifiers.Count != PlayerAbilityCatalog.AbilityCount)
                throw new ArgumentException("모든 능력치의 Edition 보정값이 필요합니다.", nameof(editionStatModifiers));

            if (!Enum.IsDefined(typeof(PlayerCardEdition), edition))
                throw new ArgumentOutOfRangeException(nameof(edition));
            bool isWildcard = edition == PlayerCardEdition.CareerHigh || edition == PlayerCardEdition.Legend;
            if (isWildcard && string.IsNullOrWhiteSpace(teamColorLineageId))
                throw new ArgumentException("특수 영입 카드에는 사전 Bake한 팀컬러 계보가 필요합니다.", nameof(teamColorLineageId));
            TeamColorLineageId = teamColorLineageId?.Trim() ?? string.Empty;
            CardId = cardId.Trim();
            PlayerSeasonId = playerSeasonId.Trim();
            Edition = edition;
            PreferredBattingOrder = preferredBattingOrder;
            _editionStatModifiers = new int[editionStatModifiers.Count];
            for (int index = 0; index < editionStatModifiers.Count; index++)
                _editionStatModifiers[index] = editionStatModifiers[index];
        }

        public string CardId { get; }
        public string PlayerSeasonId { get; }
        public PlayerCardEdition Edition { get; }
        public string TeamColorLineageId { get; }
        public bool IsFranchiseWildcard => Edition == PlayerCardEdition.CareerHigh || Edition == PlayerCardEdition.Legend;
        public bool IsUniqueOwnedCard => IsFranchiseWildcard;
        public double SkillBlockEffectMultiplier => Edition == PlayerCardEdition.Rare || Edition == PlayerCardEdition.Ex ? 2d : 1d;
        public bool CanAcquireFromScout => Edition != PlayerCardEdition.Ex && !IsFranchiseWildcard;
        public PreferredBattingOrder PreferredBattingOrder { get; }
        public int GetModifier(PlayerAbility ability) => _editionStatModifiers[(int)ability];

        public static string CreateStableCardId(string playerSeasonId, PlayerCardEdition edition, string teamColorLineageId = "")
        {
            if (string.IsNullOrWhiteSpace(playerSeasonId))
                throw new ArgumentException("PlayerSeasonId는 비어 있을 수 없습니다.", nameof(playerSeasonId));
            string id = playerSeasonId.Trim() + ":" + edition;
            if (edition == PlayerCardEdition.CareerHigh || edition == PlayerCardEdition.Legend)
            {
                if (string.IsNullOrWhiteSpace(teamColorLineageId))
                    throw new ArgumentException("특수 영입 카드의 계보가 필요합니다.", nameof(teamColorLineageId));
                id += ":" + teamColorLineageId.Trim();
            }
            return id;
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
    public sealed class HistoricalSourceContentManifest
    {
        public HistoricalSourceContentManifest(
            string referenceDataVersion,
            string generatorVersion,
            string balanceVersion,
            ulong generationSeed,
            string contentHash,
            string sourceIdentityPolicyVersion = "",
            string sourceAllocationPolicyVersion = "",
            string replacementGeneratorVersion = "",
            string replacementPopulationPolicyVersion = "",
            int sourceBackedPlayerPersonCount = 0,
            int sourceBackedPlayerSeasonCount = 0,
            int replacementGeneratedPlayerPersonCount = 0,
            int replacementGeneratedPlayerSeasonCount = 0,
            bool generationSeedAffectsCanonicalBake = false,
            string sourceFranchiseIdentityPolicyVersion = "",
            string sourceTeamSeasonIdentityPolicyVersion = "",
            string pitchBalanceVersion = "",
            ulong pitchGenerationSeed = 0)
        {
            ReferenceDataVersion = Require(referenceDataVersion, nameof(referenceDataVersion));
            GeneratorVersion = Require(generatorVersion, nameof(generatorVersion));
            BalanceVersion = Require(balanceVersion, nameof(balanceVersion));
            ContentHash = Require(contentHash, nameof(contentHash));
            GenerationSeed = generationSeed;
            SourceIdentityPolicyVersion = sourceIdentityPolicyVersion?.Trim() ?? string.Empty;
            SourceAllocationPolicyVersion = sourceAllocationPolicyVersion?.Trim() ?? string.Empty;
            ReplacementGeneratorVersion = replacementGeneratorVersion?.Trim() ?? string.Empty;
            ReplacementPopulationPolicyVersion = replacementPopulationPolicyVersion?.Trim() ?? string.Empty;
            SourceBackedPlayerPersonCount = sourceBackedPlayerPersonCount;
            SourceBackedPlayerSeasonCount = sourceBackedPlayerSeasonCount;
            ReplacementGeneratedPlayerPersonCount = replacementGeneratedPlayerPersonCount;
            ReplacementGeneratedPlayerSeasonCount = replacementGeneratedPlayerSeasonCount;
            GenerationSeedAffectsCanonicalBake = generationSeedAffectsCanonicalBake;
            SourceFranchiseIdentityPolicyVersion = sourceFranchiseIdentityPolicyVersion?.Trim() ?? string.Empty;
            SourceTeamSeasonIdentityPolicyVersion = sourceTeamSeasonIdentityPolicyVersion?.Trim() ?? string.Empty;
            PitchBalanceVersion = pitchBalanceVersion?.Trim() ?? string.Empty;
            PitchGenerationSeed = pitchGenerationSeed;
        }

        public string ReferenceDataVersion { get; }
        public string GeneratorVersion { get; }
        public string BalanceVersion { get; }
        public ulong GenerationSeed { get; }
        public string ContentHash { get; }
        public string SourceIdentityPolicyVersion { get; }
        public string SourceAllocationPolicyVersion { get; }
        public string ReplacementGeneratorVersion { get; }
        public string ReplacementPopulationPolicyVersion { get; }
        public int SourceBackedPlayerPersonCount { get; }
        public int SourceBackedPlayerSeasonCount { get; }
        public int ReplacementGeneratedPlayerPersonCount { get; }
        public int ReplacementGeneratedPlayerSeasonCount { get; }
        public bool GenerationSeedAffectsCanonicalBake { get; }
        public string SourceFranchiseIdentityPolicyVersion { get; }
        public string SourceTeamSeasonIdentityPolicyVersion { get; }
        public string PitchBalanceVersion { get; }
        public ulong PitchGenerationSeed { get; }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Manifest 값은 비어 있을 수 없습니다.", parameterName);
            return value.Trim();
        }
    }
}
