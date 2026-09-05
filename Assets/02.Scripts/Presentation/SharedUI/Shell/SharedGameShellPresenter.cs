using System;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// Profile과 상태 공급자를 공용 셸에 연결하고 유효한 Route 요청만 외부 Router에 전달한다.
    /// </summary>
    public sealed class SharedGameShellPresenter : IDisposable
    {
        private readonly ISharedGameShellView _view;
        private readonly GameModeUiProfile _profile;
        private readonly IUiShellStatusProvider _statusProvider;
        private bool _isDisposed;

        /// <summary>
        /// 사용자가 현재 Profile에서 허용된 Route를 요청했을 때 발생한다.
        /// </summary>
        public event Action<string> NavigationRequested;

        /// <summary>
        /// View에 Profile과 최초 상태를 즉시 적용하고 이후 상태 변경을 구독한다.
        /// </summary>
        public SharedGameShellPresenter(
            ISharedGameShellView view,
            GameModeUiProfile profile,
            IUiShellStatusProvider statusProvider)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _statusProvider = statusProvider ?? throw new ArgumentNullException(nameof(statusProvider));

            _view.NavigationRequested += HandleNavigationRequested;
            _statusProvider.StatusChanged += HandleStatusChanged;
            _view.BindProfile(_profile);
            RefreshStatus();
        }

        /// <summary>
        /// 현재 Workspace의 Context를 Route 유효성 확인 후 View에 적용한다.
        /// </summary>
        public void ShowContext(ShellContextModel context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            NavigationEntry entry = _profile.Navigation.FindEntry(context.RouteId);
            if (entry == null)
                throw new ArgumentException($"Profile에 등록되지 않은 Route입니다: {context.RouteId}", nameof(context));
            if (!entry.IsVisible(_profile.Capabilities) || !entry.IsEnabled)
                throw new InvalidOperationException($"현재 Profile에서 열 수 없는 Route입니다: {context.RouteId}");

            _view.BindContext(context);
        }

        /// <summary>
        /// 상태 공급자의 최신 Snapshot을 즉시 다시 표시한다.
        /// </summary>
        public void RefreshStatus()
        {
            ThrowIfDisposed();
            ShellStatusModel status = _statusProvider.GetCurrentStatus();
            if (status == null)
                throw new InvalidOperationException("상태 공급자는 null Snapshot을 반환할 수 없습니다.");
            _view.BindStatus(status);
        }

        /// <summary>
        /// View와 상태 공급자의 이벤트 구독을 해제한다.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _view.NavigationRequested -= HandleNavigationRequested;
            _statusProvider.StatusChanged -= HandleStatusChanged;
        }

        private void HandleNavigationRequested(string routeId)
        {
            if (_isDisposed)
                return;

            NavigationEntry entry = _profile.Navigation.FindEntry(routeId);
            if (entry == null || !entry.IsVisible(_profile.Capabilities) || !entry.IsEnabled)
                return;

            NavigationRequested?.Invoke(routeId);
        }

        private void HandleStatusChanged()
        {
            if (!_isDisposed)
                RefreshStatus();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SharedGameShellPresenter));
        }
    }
}
