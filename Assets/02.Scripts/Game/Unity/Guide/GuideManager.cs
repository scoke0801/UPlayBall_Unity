using System;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Data;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using UnityEngine;

namespace Baseball.Game.Guide
{
    /// <summary>Guide Dataset, Queue, 반복 상태와 Application Fact 어댑터의 런타임 수명을 소유한다.</summary>
    public sealed class GuideManager : ManagerBehaviour<GuideManager>
    {
        /// <summary>아직 어떤 로스터도 알리지 않았음을 나타내는 Revision 값이다.</summary>
        private const int UnknownRosterRevision = -1;

        private readonly CareerPreparedMatchGuideEventAdapter _careerAdapter = new();
        private readonly OwnerRosterGuideEventAdapter _ownerRosterAdapter = new();
        private FrontManagerGuide _guide;
        private CareerManager _careerManager;
        private CareerState _observedCareer;
        private CareerMatchSession _observedMatch;
        private OwnerModeManager _ownerModeManager;
        private ManagerHistoricalRuntimeState _observedOwnerRuntime;
        private int _publishedOwnerRosterRevision = UnknownRosterRevision;

        public override int InitializationOrder => -10;
        public bool IsAvailable => _guide != null;
        public int QueuedCount => _guide?.QueuedCount ?? 0;
        public string LastError { get; private set; } = string.Empty;

        public event Action QueueChanged;

        protected override void OnInitialize()
        {
            if (!FrontManagerGuideDatasetAsset.TryLoadCatalog(out GuideDatasetCatalog catalog, out GuideValidationIssue[] issues))
            {
                LastError = issues.Length > 0 ? issues[0].ToString() : "Guide Dataset을 불러오지 못했습니다.";
                Debug.LogError($"[GuideManager] {LastError}");
                return;
            }
            _guide = new FrontManagerGuide(catalog);
            LastError = string.Empty;
        }

        protected override void OnAfterInitialize()
        {
            GameManager gameManager = GameManager.EnsureExists();
            _careerManager = gameManager.EnsureManager<CareerManager>("CareerManager");
            _careerManager.CareerChanged -= HandleCareerChanged;
            _careerManager.CareerChanged += HandleCareerChanged;
            _ownerModeManager = gameManager.EnsureManager<OwnerModeManager>("OwnerModeManager");
            _ownerModeManager.RuntimeChanged -= HandleOwnerRuntimeChanged;
            _ownerModeManager.RuntimeChanged += HandleOwnerRuntimeChanged;
            HandleCareerChanged();
            HandleOwnerRuntimeChanged();
        }

        protected override void OnShutdown()
        {
            if (_careerManager != null)
                _careerManager.CareerChanged -= HandleCareerChanged;
            if (_ownerModeManager != null)
                _ownerModeManager.RuntimeChanged -= HandleOwnerRuntimeChanged;
            _careerManager = null;
            _ownerModeManager = null;
            _observedCareer = null;
            _observedMatch = null;
            _observedOwnerRuntime = null;
            _publishedOwnerRosterRevision = UnknownRosterRevision;
            _guide = null;
            QueueChanged = null;
        }

        public GuideEnqueueResult Publish(GuideFact fact)
        {
            if (_guide == null)
                return new GuideEnqueueResult(0, 0, LastError.Length > 0 ? LastError : "Guide가 초기화되지 않았습니다.");
            GuideEnqueueResult result = _guide.Enqueue(fact);
            if (!result.IsAccepted)
            {
                LastError = result.Error;
                Debug.LogError($"[GuideManager] Fact 거부: {result.Error}");
            }
            if (result.EnqueuedCount > 0)
                QueueChanged?.Invoke();
            return result;
        }

        public bool TryDequeue(GuideDisplayContext context, out GuideMessage message)
        {
            message = default;
            if (_guide == null || !_guide.TryDequeue(context, out message))
                return false;
            QueueChanged?.Invoke();
            return true;
        }

        public GuideRepeatStateData CaptureRepeatState() =>
            _guide?.RepeatState.Capture() ?? new GuideRepeatStateData();

        public void RestoreRepeatState(GuideRepeatStateData state)
        {
            if (_guide == null)
                throw new InvalidOperationException("Guide가 초기화되지 않았습니다.");
            _guide.RepeatState.Restore(state);
        }

        private void HandleCareerChanged()
        {
            if (_guide == null || _careerManager == null || !_careerManager.HasActiveCareer)
            {
                ClearPendingIfNeeded();
                _observedCareer = null;
                _observedMatch = null;
                return;
            }

            CareerState career = _careerManager.CurrentCareer;
            if (!ReferenceEquals(_observedCareer, career))
            {
                ClearPendingIfNeeded();
                _observedCareer = career;
                _observedMatch = null;
                Publish(_careerAdapter.CreateFirstEntryFact(career, CreateCareerIdentity(
                    career,
                    $"career-first-entry:{career.MyPlayer.PlayerId}")));
            }

            CareerMatchSession match = _careerManager.ActiveMatch;
            if (ReferenceEquals(_observedMatch, match))
                return;
            _observedMatch = match;
            if (match == null || match.Phase != CareerMatchPhase.Preparation)
                return;

            GuideFactIdentity identity = CreateCareerIdentity(
                career,
                $"career-match-role:{match.Input.SeasonId}:{match.Input.GameId}");
            GuideFact[] facts = _careerAdapter.CreatePreparedMatchFacts(career, match, identity);
            for (int index = 0; index < facts.Length; index++)
                Publish(facts[index]);
        }

        /// <summary>
        /// 구단주 Runtime이 바뀔 때마다 1군 구성만 다시 알린다. RuntimeChanged는 시설·재정처럼
        /// 로스터와 무관한 변경에서도 오므로, 실제로 구성이 달라진 경우에만 Fact를 만든다.
        /// </summary>
        private void HandleOwnerRuntimeChanged()
        {
            if (_guide == null || _ownerModeManager == null || !_ownerModeManager.HasActiveRuntime)
            {
                if (_observedOwnerRuntime != null)
                    ClearPendingIfNeeded();
                _observedOwnerRuntime = null;
                _publishedOwnerRosterRevision = UnknownRosterRevision;
                return;
            }

            ManagerHistoricalRuntimeState runtime = _ownerModeManager.Runtime;
            if (!ReferenceEquals(_observedOwnerRuntime, runtime))
            {
                ClearPendingIfNeeded();
                _observedOwnerRuntime = runtime;
                _publishedOwnerRosterRevision = UnknownRosterRevision;
            }

            CurrentRosterState roster = runtime.GetRoster(runtime.PlayerTeamSeasonKey);
            int rosterRevision = OwnerRosterRevision.Compute(roster);
            if (rosterRevision == _publishedOwnerRosterRevision)
                return;
            _publishedOwnerRosterRevision = rosterRevision;

            // 로스터 규칙은 OwnerModeManager가 이미 확정했다. Guide는 그 결과를 문장으로 옮기기만 한다.
            GuideFact[] facts = _ownerRosterAdapter.CreateFacts(
                roster,
                _ownerModeManager.BuildRosterStatus().Validation,
                CreateOwnerIdentity(
                    runtime,
                    $"owner-roster:{runtime.PlayerTeamSeasonKey}:{rosterRevision}"),
                rosterRevision,
                runtime.IdentityRegistry.GetPlayerDisplayName);
            for (int index = 0; index < facts.Length; index++)
                Publish(facts[index]);
        }

        private void ClearPendingIfNeeded()
        {
            if (_guide == null || _guide.QueuedCount == 0)
                return;
            _guide.ClearPending();
            QueueChanged?.Invoke();
        }

        private static GuideFactIdentity CreateCareerIdentity(CareerState career, string eventId)
        {
            ulong worldSeed = career.World.WorldSeed;
            string saveId = $"career:{worldSeed}:{career.MyPlayer.PlayerId}";
            return new GuideFactIdentity(worldSeed, eventId, saveId, sequenceNumber: 0);
        }

        private static GuideFactIdentity CreateOwnerIdentity(
            ManagerHistoricalRuntimeState runtime,
            string eventId)
        {
            ulong worldSeed = runtime.WorldHistory.WorldHistorySeed;
            string saveId = $"owner:{worldSeed}:{runtime.PlayerTeamSeasonKey}";
            return new GuideFactIdentity(worldSeed, eventId, saveId, sequenceNumber: 0);
        }
    }
}
