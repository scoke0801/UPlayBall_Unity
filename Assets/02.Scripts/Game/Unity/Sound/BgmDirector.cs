using Baseball.Game.Career;
using Baseball.Game.Manager;

namespace Baseball.Game.Sound
{
    /// <summary>
    /// 선수 커리어와 구단주 관전 상태를 읽어 지금이 어떤 BGM 국면인지 판정하고 SoundManager에 알린다.
    /// SoundManager가 각 모드의 Runtime과 화면을 직접 알지 않도록 판정만 이쪽에 모아 둔다.
    /// </summary>
    public sealed class BgmDirector : ManagerBehaviour<BgmDirector>
    {
        private CareerManager _careerManager;
        private SoundManager _soundManager;
        private bool _isOwnerMatchBroadcasting;
        private bool _isOwnerMatchAudioSuppressed;

        // SoundManager(-40)와 CareerManager(-20)가 준비된 뒤 판정해야 한다.
        public override int InitializationOrder => 0;

        protected override void OnAfterInitialize()
        {
            _soundManager = GameManager.EnsureExists().EnsureManager<SoundManager>("SoundManager");
            _careerManager = GameManager.EnsureExists().EnsureManager<CareerManager>("CareerManager");
            _careerManager.CareerChanged += HandleCareerChanged;
            ApplyCurrentSituation();
        }

        protected override void OnShutdown()
        {
            if (_careerManager != null)
                _careerManager.CareerChanged -= HandleCareerChanged;

            _isOwnerMatchBroadcasting = false;
            _isOwnerMatchAudioSuppressed = false;
            _careerManager = null;
            _soundManager = null;
        }

        /// <summary>
        /// 구단주 관전 화면의 생명주기를 BGM 국면에 반영한다.
        /// 구단주 경기는 진입 시 이미 결과가 확정되므로 Runtime이 아니라 실제 재생 화면이 이 상태를 알린다.
        /// </summary>
        public void SetOwnerMatchBroadcasting(bool isBroadcasting, bool shouldPlayAudio = true)
        {
            bool isAudioSuppressed = isBroadcasting && !shouldPlayAudio;
            if (_isOwnerMatchBroadcasting == isBroadcasting &&
                _isOwnerMatchAudioSuppressed == isAudioSuppressed)
                return;

            _isOwnerMatchBroadcasting = isBroadcasting;
            _isOwnerMatchAudioSuppressed = isAudioSuppressed;
            ApplyCurrentSituation();
        }

        private void HandleCareerChanged()
        {
            // CareerChanged는 경기 중 타석마다 발생한다. 국면이 그대로면 PlaySituation이 무시하므로
            // 여기서 별도 캐싱 없이 매번 호출해도 곡이 다시 시작되지 않는다.
            ApplyCurrentSituation();
        }

        private void ApplyCurrentSituation()
        {
            if (_soundManager == null)
                return;

            if (_isOwnerMatchAudioSuppressed)
            {
                _soundManager.StopBgm(0f);
                return;
            }

            bool isBroadcasting = _isOwnerMatchBroadcasting || IsMatchBroadcasting(_careerManager?.ActiveMatch);
            _soundManager.PlaySituation(isBroadcasting ? BgmSituation.MatchPlay : BgmSituation.Lobby);
        }

        /// <summary>
        /// 경기 중계 화면이 실제로 돌아가는 중인지 판정한다.
        /// 준비 화면은 아직 로비의 연장이고, 결과만 보기는 중계 없이 결과로 건너뛰므로 둘 다 제외한다.
        /// 판정 기준은 UI_Scene_CareerMatch가 자동 중계를 판단하는 조건과 일부러 같게 맞췄다.
        /// Phase는 중계가 화면에 다 풀리기 전에 Completed로 앞서갈 수 있어, 진행 중 BGM이 끊기지 않도록
        /// Playing이 아니라 "준비가 끝났는가"로 본다.
        /// </summary>
        private static bool IsMatchBroadcasting(CareerMatchSession session)
        {
            return session != null &&
                   session.Phase != CareerMatchPhase.Preparation &&
                   session.Mode != CareerMatchMode.ResultsOnly;
        }
    }
}
