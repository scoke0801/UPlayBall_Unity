using System;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.Owner
{
    /// <summary>Owner Home 갱신을 공용 GlobalTopBar에 전달한다.</summary>
    public sealed class OwnerShellStatusProvider : UiShellStatusProviderBase
    {
        private OwnerHomePresentationModel _current;

        public OwnerShellStatusProvider(OwnerHomePresentationModel initial)
        {
            _current = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        public void Update(OwnerHomePresentationModel current)
        {
            _current = current ?? throw new ArgumentNullException(nameof(current));
            NotifyStatusChanged();
        }

        public override ShellStatusModel GetCurrentStatus() => _current.ShellStatus;
    }
}
