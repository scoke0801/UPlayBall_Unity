using System;

namespace Baseball.Game.Input
{
    /// <summary>
    /// 중첩 UI가 입력 context를 안전하게 원복하도록 push/pop 수명을 묶는다.
    /// </summary>
    public sealed class InputContextLease : IDisposable
    {
        private InputManager _owner;
        private readonly int _leaseId;

        internal InputContextLease(InputManager owner, int leaseId)
        {
            _owner = owner;
            _leaseId = leaseId;
        }

        public void Dispose()
        {
            if (_owner == null)
                return;

            _owner.ReleaseContext(_leaseId);
            _owner = null;
        }
    }
}
