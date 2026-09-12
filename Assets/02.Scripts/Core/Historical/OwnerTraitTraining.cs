using System;
using Baseball.Core.Players;

namespace Baseball.Core.Historical
{
    /// <summary>성장 블록·커리어 레거시와 독립적인 카드 특성의 안정적인 식별자다.</summary>
    public enum CardTraitKind { None, Contact, Clutch, Leadoff, Power, Bunt, Defense, Running,
        EarlyStarter, PitchClutch, Strikeout, Groundball, Endurance, Setup, Closer }
    public enum CardTraitRank { None, C, B, A, S }

    /// <summary>후보 추첨과 파트너 사용량을 카드별로 보존한다.</summary>
    [Serializable]
    public sealed class PlayerTraitProgress
    {
        public CardTraitKind trait;
        public CardTraitRank rank;
        public int experience;
        public int partnerSeason = -1, partnerUses;
        public int trainingSeason = -1;
        public int freeRerollSeason = -1, freeChangeSeason = -1;
        public int revision, candidateSequence;
        public ulong candidateSeed;
        public CardTraitKind[] candidates = Array.Empty<CardTraitKind>();
        public bool HasCandidates => candidates != null && candidates.Length > 0;

        public PlayerTraitProgress Copy()
        {
            var copy = (PlayerTraitProgress)MemberwiseClone();
            copy.candidates = candidates == null ? Array.Empty<CardTraitKind>() : (CardTraitKind[])candidates.Clone();
            return copy;
        }

        public void Validate()
        {
            if (!Enum.IsDefined(typeof(CardTraitKind), trait) || !Enum.IsDefined(typeof(CardTraitRank), rank)
                || experience < 0 || revision < 0 || candidateSequence < 0 || partnerUses < 0
                || partnerSeason < -1 || trainingSeason < -1 || freeRerollSeason < -1 || freeChangeSeason < -1
                || (trait == CardTraitKind.None) != (rank == CardTraitRank.None))
                throw new ArgumentException("카드 특성 저장 상태가 올바르지 않습니다.");
            if (candidates == null || candidates.Length != 0 && candidates.Length != 3)
                throw new ArgumentException("특성 후보는 세 개여야 합니다.");
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == CardTraitKind.None || !Enum.IsDefined(typeof(CardTraitKind), candidates[i]))
                    throw new ArgumentException("알 수 없는 특성 후보입니다.");
                for (int j = 0; j < i; j++) if (candidates[j] == candidates[i])
                    throw new ArgumentException("특성 후보가 중복되었습니다.");
            }
        }
    }

    /// <summary>특성 포인트와 시즌 보상의 중복 수령 방지를 저장한다.</summary>
    [Serializable]
    public sealed class OwnerTraitTrainingState
    {
        public int points, rewardedSeason = -1, rewardedGames, revision;
        public bool hasSeenGuide, skipAnimation, skipConfirmation;
        public OwnerTraitTrainingState Copy() => (OwnerTraitTrainingState)MemberwiseClone();
        public void Validate()
        {
            if (points < 0 || rewardedSeason < -1 || rewardedGames < 0 || revision < 0)
                throw new ArgumentException("특성 훈련 재화 저장 상태가 올바르지 않습니다.");
        }
    }

    /// <summary>카드 특성의 후보 자격·표시명·경기 보정을 데이터로 정의한다.</summary>
    [Serializable]
    public sealed class CardTraitDefinition
    {
        public CardTraitKind kind;
        public string name, description;
        public PlayerType playerType;
        public PlayerPosition position;
        public int minimumAbility;
        public Baseball.Core.Growth.PlayerAbility ability;
        public double effect;
    }

    /// <summary>v1.0 기획 표와 미정 경제·효과의 검증 가능한 초깃값이다. 실제 게임은 JSON을 주입한다.</summary>
    [Serializable]
    public sealed class OwnerTraitTrainingBalance
    {
        public int[] experience = { 100, 250, 500, 900 };
        public int[] slots = { 1, 1, 2, 3 };
        public double[] multipliers = { 1, 1.25, 1.55, 1.9 };
        public double[] costMultipliers = { 1, 1.5, 2.2, 3.2 };
        public int reserveExperience = 40, starterExperience = 60, partnerUses = 2;
        public int samePositionPercent = 20, sameTeamPercent = 10, performancePercent = 10;
        public int basePointCost = 20, rerollCost = 10, changeCost = 30;
        public long baseMoneyCost = 1000;
        public int offseasonReward = 400, gameReward = 1;
        public int seasonGoalReward = 100, seasonGoalWinPercent = 50;
        public int performancePlateAppearances = 60, performancePitchingOuts = 60;
        public double performanceOps = .8, performanceEra = 3.5;
        public CardTraitDefinition[] definitions = CreateDefinitions();

        public CardTraitRank GetRank(int value)
        {
            int rank = 0;
            while (rank < experience.Length && value >= experience[rank]) rank++;
            return (CardTraitRank)rank;
        }

        public CardTraitDefinition Get(CardTraitKind kind)
        {
            foreach (var definition in definitions) if (definition.kind == kind) return definition;
            throw new InvalidOperationException("특성 정의를 찾을 수 없습니다.");
        }

        public void Validate()
        {
            if (experience == null || slots == null || multipliers == null || costMultipliers == null
                || experience.Length != 4 || slots.Length != 4 || multipliers.Length != 4 || costMultipliers.Length != 4)
                throw new ArgumentException("특성훈련은 네 등급의 설정이 필요합니다.");
            for (int i = 0; i < 4; i++)
                if (experience[i] <= (i == 0 ? 0 : experience[i - 1]) || slots[i] < 1 || slots[i] > 3
                    || !IsPositive(multipliers[i]) || !IsPositive(costMultipliers[i]))
                    throw new ArgumentException("특성 등급 설정이 올바르지 않습니다.");
            if (basePointCost <= 0 || baseMoneyCost <= 0 || reserveExperience <= 0 || starterExperience <= 0
                || partnerUses <= 0 || rerollCost <= 0 || changeCost <= 0 || offseasonReward < 0 || gameReward < 0
                || samePositionPercent < 0 || sameTeamPercent < 0 || performancePercent < 0 || definitions == null
                || seasonGoalReward < 0 || seasonGoalWinPercent < 0 || seasonGoalWinPercent > 100
                || performancePlateAppearances < 1 || performancePitchingOuts < 1 || !IsPositive(performanceOps) || !IsPositive(performanceEra))
                throw new ArgumentException("특성 경제 설정이 올바르지 않습니다.");
            var seen = new System.Collections.Generic.HashSet<CardTraitKind>();
            foreach (var definition in definitions)
                if (definition == null || definition.kind == CardTraitKind.None || !seen.Add(definition.kind)
                    || !Enum.IsDefined(typeof(CardTraitKind), definition.kind) || string.IsNullOrWhiteSpace(definition.name)
                    || string.IsNullOrWhiteSpace(definition.description) || !IsPositive(definition.effect)
                    || definition.effect * multipliers[3] > (definition.kind == CardTraitKind.Power || definition.kind == CardTraitKind.Groundball
                        || definition.kind == CardTraitKind.Endurance || definition.kind == CardTraitKind.Running ? .5 : 30))
                    throw new ArgumentException("특성 정의가 올바르지 않습니다.");
            if (seen.Count != Enum.GetValues(typeof(CardTraitKind)).Length - 1)
                throw new ArgumentException("특성 정의가 누락되었습니다.");
        }

        private static bool IsPositive(double value) => value > 0 && !double.IsInfinity(value) && !double.IsNaN(value);

        private static CardTraitDefinition[] CreateDefinitions()
        {
            // 조건부 보정은 작은 능력치·확률 변화부터 대량 대조하며 기존 블록 계수를 전용하지 않는다.
            return new[] {
                Define(CardTraitKind.Contact, "타격의 정석", "타격 시 교타", false, 3),
                Define(CardTraitKind.Clutch, "해결사", "득점권에서 교타", false, 5),
                Define(CardTraitKind.Leadoff, "선두 출루", "주자가 없을 때 교타", false, 4),
                Define(CardTraitKind.Power, "장타 본능", "강한 타구 확률", false, .015),
                Define(CardTraitKind.Bunt, "작전 수행", "번트 시 번트 능력", false, 5),
                Define(CardTraitKind.Defense, "철벽 수비", "수비 시 수비 능력", false, 4),
                Define(CardTraitKind.Running, "도루 감각", "도루 성공 확률", false, .025),
                Define(CardTraitKind.EarlyStarter, "에이스 본능", "선발로 1~3회 투구 시 제구", true, 4, PlayerPosition.StartingPitcher),
                Define(CardTraitKind.PitchClutch, "위기관리", "득점권에서 제구", true, 4),
                Define(CardTraitKind.Strikeout, "탈삼진 본능", "2스트라이크에서 구위", true, 4),
                Define(CardTraitKind.Groundball, "땅볼 유도", "강한 타구 확률 억제·땅볼 확률", true, .015),
                Define(CardTraitKind.Endurance, "이닝이터", "투구 피로 증가량 감소", true, .05, PlayerPosition.StartingPitcher),
                Define(CardTraitKind.Setup, "셋업맨", "7~8회 투구 시 제구", true, 4, PlayerPosition.ReliefPitcher),
                Define(CardTraitKind.Closer, "끝판왕", "9회 이후 리드 상황에서 제구", true, 5, PlayerPosition.ReliefPitcher) };
        }

        private static CardTraitDefinition Define(CardTraitKind kind, string name, string description,
            bool pitcher, double effect, PlayerPosition position = PlayerPosition.Unknown) => new CardTraitDefinition {
                kind = kind, name = name, description = description, playerType = pitcher ? PlayerType.Pitcher : PlayerType.Batter,
                effect = effect, position = position };
    }

    /// <summary>정적 정의를 경기 준비 단계에서 해석한 무할당 특성 입력이다.</summary>
    public readonly struct CardTraitEffect
    {
        public CardTraitEffect(CardTraitKind kind, double strength) { Kind = kind; Strength = strength; }
        public CardTraitKind Kind { get; }
        public double Strength { get; }
        public double Get(CardTraitKind kind) => Kind == kind ? Strength : 0;
    }
}
