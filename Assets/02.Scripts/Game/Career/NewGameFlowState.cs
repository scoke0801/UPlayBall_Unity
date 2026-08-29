using Baseball.Core.Players;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    /// <summary>
    /// 새 게임 마법사의 명시적인 진행 단계를 정의한다.
    /// </summary>
    public enum NewGameStep
    {
        Identity,
        PlayerType,
        Position,
        Handedness,
        AttributeAllocation,
        PlayerCard,
        ContractOffers,
        ContractComplete,
        Completed,
        PlayerDetails,
        MatchSettings,
        FinalConfirmation
    }

    /// <summary>
    /// 새 게임 생성 단계(이름 → 투타 → 포지션 → 능력치 배분 → 구단 오퍼 → 계약)가 끝날 때까지
    /// 중간 선택을 보관하는 draft 객체다. Presentation 화면은 이 값만 읽고 쓴다.
    /// </summary>
    public sealed class NewGameFlowState
    {
        public NewGameStep Step { get; set; }
        public string PlayerName { get; set; }
        public string Nationality { get; set; }
        public PlayerType? PlayerType { get; set; }
        public PlayerPosition PrimaryPosition { get; set; }
        public Handedness BattingHand { get; set; }
        public Handedness ThrowingHand { get; set; }
        public BatterAttributes? BatterAttributes { get; set; }
        public PitcherAttributes? PitcherAttributes { get; set; }
        public ulong RandomSeed { get; set; }
        public NewGameSetupResult SetupResult { get; set; }
        public ContractOffer? SelectedOffer { get; set; }
        public CareerCreationDraft Draft { get; set; }
        public bool UsesGuidedCreation { get; set; }

        /// <summary>
        /// 이름·포지션·능력치까지 채워져 구단 생성 단계로 넘어갈 수 있는지 확인한다.
        /// </summary>
        public bool IsCharacterReady()
        {
            bool hasAttributes = PrimaryPosition is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher
                ? PitcherAttributes.HasValue
                : BatterAttributes.HasValue;

            return !string.IsNullOrWhiteSpace(PlayerName) &&
                   !string.IsNullOrWhiteSpace(Nationality) &&
                   PlayerType.HasValue &&
                   PrimaryPosition != PlayerPosition.Unknown &&
                   hasAttributes;
        }
    }
}
