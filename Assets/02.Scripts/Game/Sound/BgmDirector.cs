using Baseball.Game.Career;
using Baseball.Game.Manager;

namespace Baseball.Game.Sound
{
    /// <summary>
    /// 커리어 진행 상태를 읽어 지금이 어떤 BGM 국면인지 판정하고 SoundManager에 알린다.
    /// SoundManager가 커리어를 직접 알지 않도록 판정만 이쪽에 모아 둔다.
    /// </summary>
    public sealed class BgmDirector : ManagerBehaviour<BgmDirector>
    {
        private CareerManager _careerManager;
        private SoundManager _soundManager;

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

            _careerManager = null;
            _soundManager = null;
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

            // 준비·진행·결과는 한 경기의 세 단계일 뿐이므로 한 경기 안에서 BGM을 바꾸지 않는다.
            bool isInMatch = _careerManager != null && _careerManager.HasActiveMatch;
            _soundManager.PlaySituation(isInMatch ? BgmSituation.MatchPlay : BgmSituation.Lobby);
        }
    }
}
