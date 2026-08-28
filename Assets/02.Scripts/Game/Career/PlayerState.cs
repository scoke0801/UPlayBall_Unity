using Baseball.Core.Players;
using Baseball.Core.Growth;
using Baseball.Simulation.Growth;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 세이브 대상이 되는 선수의 커리어 런타임 상태다. 구단은 ID로만 참조한다.
    /// </summary>
    public sealed class PlayerState
    {
        /// <summary>
        /// 새 게임에서 계약이 확정된 직후의 선수 상태를 생성한다.
        /// </summary>
        public PlayerState(
            int saveVersion,
            int playerId,
            string name,
            PlayerPosition primaryPosition,
            Handedness battingHand,
            Handedness throwingHand,
            BatterAttributes batterAttributes,
            PitcherAttributes pitcherAttributes,
            int currentTeamId)
            : this(
                saveVersion,
                playerId,
                name,
                nationality: string.Empty,
                age: 18,
                primaryPosition,
                battingHand,
                throwingHand,
                batterAttributes,
                pitcherAttributes,
                currentTeamId)
        {
        }

        /// <summary>
        /// 국적과 시작 나이를 포함한 계약 직후 선수 상태를 생성한다.
        /// </summary>
        public PlayerState(
            int saveVersion,
            int playerId,
            string name,
            string nationality,
            int age,
            PlayerPosition primaryPosition,
            Handedness battingHand,
            Handedness throwingHand,
            BatterAttributes batterAttributes,
            PitcherAttributes pitcherAttributes,
            int currentTeamId)
        {
            SaveVersion = saveVersion;
            PlayerId = playerId;
            Name = name;
            Nationality = nationality;
            Age = age;
            PrimaryPosition = primaryPosition;
            BattingHand = battingHand;
            ThrowingHand = throwingHand;
            BatterAttributes = batterAttributes;
            PitcherAttributes = pitcherAttributes;
            CurrentTeamId = currentTeamId;
            StudyState = new PlayerStudyState();
            SkillBoardState = new SkillBoardState("standard_4x4");
        }

        public int SaveVersion { get; }
        public int PlayerId { get; }
        public string Name { get; }
        public string Nationality { get; }
        public int Age { get; private set; }
        public PlayerPosition PrimaryPosition { get; }
        public Handedness BattingHand { get; }
        public Handedness ThrowingHand { get; }
        public BatterAttributes BatterAttributes { get; }
        public PitcherAttributes PitcherAttributes { get; }
        public int CurrentTeamId { get; private set; }
        public int Condition { get; private set; }
        public int ManagerEvaluation { get; private set; }
        public PlayerGrowthState GrowthState { get; private set; }
        public PlayerStudyState StudyState { get; }
        public SkillBoardState SkillBoardState { get; }

        /// <summary>
        /// 계약 직후 생성한 Base Ability와 Potential 상태를 커리어 세이브에 연결한다.
        /// </summary>
        public void AttachGrowthState(PlayerGrowthState growthState)
        {
            if (growthState == null)
                throw new System.ArgumentNullException(nameof(growthState));
            if (growthState.PlayerId != PlayerId)
                throw new System.InvalidOperationException("다른 선수의 성장 상태를 연결할 수 없습니다.");
            if (GrowthState != null)
                throw new System.InvalidOperationException("성장 상태는 한 번만 연결할 수 있습니다.");
            GrowthState = growthState;
        }

        /// <summary>
        /// 첫 시즌을 시작할 때 컨디션과 감독 평가의 초기값을 확정한다.
        /// </summary>
        public void InitializeSeasonStatus(int condition, int managerEvaluation)
        {
            Condition = ClampRating(condition);
            ManagerEvaluation = ClampRating(managerEvaluation);
            SynchronizeGrowthCondition();
        }

        /// <summary>
        /// 한 경기의 출장 여부와 수행 결과를 현재 상태에 반영한다.
        /// </summary>
        public void ApplyGameFeedback(int conditionDelta, int managerEvaluationDelta, int minimumCondition)
        {
            Condition = Clamp(Condition + conditionDelta, minimumCondition, 100);
            ManagerEvaluation = ClampRating(ManagerEvaluation + managerEvaluationDelta);
            SynchronizeGrowthCondition();
        }

        /// <summary>
        /// FA·이적으로 소속 구단이 바뀔 때 ID만 갱신한다.
        /// </summary>
        public void TransferTo(int teamId)
        {
            CurrentTeamId = teamId;
        }

        /// <summary>
        /// 시즌이 끝나 선수 나이가 한 살 증가할 때 호출한다.
        /// </summary>
        public void AdvanceAge()
        {
            Age++;
            GrowthState?.AdvanceAge();
        }

        /// <summary>
        /// 오프시즌 성장 활동이 바꾼 컨디션을 기존 커리어 상태에 반영한다.
        /// </summary>
        public void SynchronizeFromGrowthState()
        {
            if (GrowthState == null)
                throw new System.InvalidOperationException("연결된 성장 상태가 없습니다.");
            Age = GrowthState.Age;
            Condition = GrowthState.Condition;
        }

        /// <summary>
        /// 현재 저장 상태를 경기 시뮬레이터가 소비하는 불변 Player 입력으로 변환한다.
        /// </summary>
        public Player ToPlayer()
        {
            BatterAttributes currentBatterAttributes = GrowthState == null
                ? BatterAttributes
                : GrowthState.BaseAbilities.ToBatterAttributes();
            PitcherAttributes currentPitcherAttributes = GrowthState == null
                ? PitcherAttributes
                : GrowthState.BaseAbilities.ToPitcherAttributes();
            return new Player(
                PlayerId,
                Name,
                PrimaryPosition,
                BattingHand,
                ThrowingHand,
                currentBatterAttributes,
                currentPitcherAttributes,
                nationality: Nationality);
        }

        /// <summary>
        /// 장착 성장판의 안정 능력치 보너스까지 합쳐 경기 시뮬레이터 입력을 만든다.
        /// </summary>
        public Player ToPlayer(SkillBoardService skillBoardService)
        {
            if (skillBoardService == null)
                throw new System.ArgumentNullException(nameof(skillBoardService));
            if (GrowthState == null)
                return ToPlayer();

            int[] values = GrowthState.BaseAbilities.ToArray();
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = skillBoardService.GetStableAbility(
                    SkillBoardState,
                    GrowthState,
                    (PlayerAbility)index);
            }
            var stableAbilities = new AbilityRatings(values);
            return new Player(
                PlayerId,
                Name,
                PrimaryPosition,
                BattingHand,
                ThrowingHand,
                stableAbilities.ToBatterAttributes(),
                stableAbilities.ToPitcherAttributes(),
                nationality: Nationality);
        }

        private static int ClampRating(int value) => Clamp(value, 0, 100);

        private void SynchronizeGrowthCondition()
        {
            if (GrowthState != null)
                GrowthState.ChangeCondition(Condition - GrowthState.Condition);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
