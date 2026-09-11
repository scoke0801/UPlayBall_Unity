using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 공용 정보 화면의 데이터 의미를 구분한다.
    /// </summary>
    public enum SharedScreenKind
    {
        Schedule = 0,
        LeagueStandings = 1,
        SeasonRecords = 2,
        TeamOverview = 3,
        TeamRoster = 4,
        PlayerDetail = 5
    }

    /// <summary>
    /// Route별 공용 Layout 사용 방식과 접근 Capability를 정의한다.
    /// </summary>
    public sealed class SharedScreenProfile
    {
        /// <summary>
        /// 공용 Route와 Layout 옵션으로 화면 Profile을 만든다.
        /// </summary>
        public SharedScreenProfile(
            string routeId,
            string displayName,
            SharedScreenKind kind,
            UiCapability requiredCapability = UiCapability.None,
            bool usesRightInspector = false,
            bool usesActionBar = true)
        {
            if (string.IsNullOrWhiteSpace(routeId))
                throw new ArgumentException("공용 화면 Route는 비어 있을 수 없습니다.", nameof(routeId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("공용 화면 이름은 비어 있을 수 없습니다.", nameof(displayName));

            RouteId = routeId;
            DisplayName = displayName;
            Kind = kind;
            RequiredCapability = requiredCapability;
            UsesRightInspector = usesRightInspector;
            UsesActionBar = usesActionBar;
        }

        public string RouteId { get; }
        public string DisplayName { get; }
        public SharedScreenKind Kind { get; }
        public UiCapability RequiredCapability { get; }
        public bool UsesRightInspector { get; }
        public bool UsesActionBar { get; }
    }

    /// <summary>
    /// 모드가 아닌 현재 대상·선택·필터 관점을 공용 화면에 전달한다.
    /// </summary>
    public sealed class SharedScreenContext
    {
        /// <summary>
        /// Route와 현재 대상 식별자를 묶어 화면 Context를 만든다.
        /// </summary>
        public SharedScreenContext(
            string routeId,
            string subjectId = null,
            string focusedEntityId = null,
            string defaultFilterId = null)
        {
            if (string.IsNullOrWhiteSpace(routeId))
                throw new ArgumentException("공용 화면 Context Route는 비어 있을 수 없습니다.", nameof(routeId));

            RouteId = routeId;
            SubjectId = subjectId ?? string.Empty;
            FocusedEntityId = focusedEntityId ?? string.Empty;
            DefaultFilterId = defaultFilterId ?? string.Empty;
        }

        public string RouteId { get; }
        public string SubjectId { get; }
        public string FocusedEntityId { get; }
        public string DefaultFilterId { get; }
    }

    /// <summary>
    /// 동일 Snapshot에 모드별 Action Provider만 합성해 최종 공용 화면 상태를 만든다.
    /// </summary>
    public sealed class SharedScreenPresentationModel<TSnapshot> where TSnapshot : class
    {
        private readonly ISharedScreenActionProvider _actionProvider;
        private readonly SharedScreenActionModel[] _actions;

        /// <summary>
        /// 공용 Snapshot과 모드별 Action Provider를 결합한다.
        /// </summary>
        public SharedScreenPresentationModel(
            SharedScreenProfile profile,
            SharedScreenContext context,
            TSnapshot snapshot,
            UiContentStateModel contentState,
            UiCapabilitySet capabilities,
            ISharedScreenActionProvider actionProvider)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            ContentState = contentState ?? throw new ArgumentNullException(nameof(contentState));
            _actionProvider = actionProvider ?? throw new ArgumentNullException(nameof(actionProvider));

            if (!string.Equals(profile.RouteId, context.RouteId, StringComparison.Ordinal))
                throw new ArgumentException("Profile과 Context의 Route가 일치해야 합니다.", nameof(context));
            if (!capabilities.Has(profile.RequiredCapability))
                throw new InvalidOperationException($"현재 Capability로 열 수 없는 공용 화면입니다: {profile.RouteId}");
            if (contentState.Kind == UiContentStateKind.Ready && snapshot == null)
                throw new ArgumentNullException(nameof(snapshot), "Ready 상태에는 Snapshot이 필요합니다.");

            Snapshot = snapshot;
            _actions = FilterActions(actionProvider.GetActions(context), capabilities);
        }

        public SharedScreenProfile Profile { get; }
        public SharedScreenContext Context { get; }
        public TSnapshot Snapshot { get; }
        public UiContentStateModel ContentState { get; }
        public IReadOnlyList<SharedScreenActionModel> Actions => _actions;

        /// <summary>
        /// 현재 표시된 활성 Action만 모드별 Provider에 전달한다.
        /// </summary>
        public bool TryExecuteAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return false;

            for (int i = 0; i < _actions.Length; i++)
            {
                if (string.Equals(_actions[i].ActionId, actionId, StringComparison.Ordinal) &&
                    _actions[i].IsEnabled)
                {
                    return _actionProvider.TryExecute(actionId, Context);
                }
            }

            return false;
        }

        private static SharedScreenActionModel[] FilterActions(
            IReadOnlyList<SharedScreenActionModel> actions,
            UiCapabilitySet capabilities)
        {
            if (actions == null || actions.Count == 0)
                return Array.Empty<SharedScreenActionModel>();

            var filtered = new List<SharedScreenActionModel>(actions.Count);
            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < actions.Count; i++)
            {
                SharedScreenActionModel action = actions[i] ??
                    throw new InvalidOperationException("Action Provider는 null Action을 반환할 수 없습니다.");
                if (!actionIds.Add(action.ActionId))
                    throw new InvalidOperationException($"Action Provider가 중복 ID를 반환했습니다: {action.ActionId}");
                if (capabilities.Has(action.RequiredCapability))
                    filtered.Add(action);
            }
            return filtered.ToArray();
        }
    }
}
