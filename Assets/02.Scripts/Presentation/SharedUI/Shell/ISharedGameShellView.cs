using System;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// Presenter가 Unity 계층을 몰라도 공용 셸을 갱신할 수 있게 하는 View 계약이다.
    /// </summary>
    public interface ISharedGameShellView
    {
        /// <summary>
        /// 사용자가 공용 Navigation 항목을 선택했을 때 Route를 전달한다.
        /// </summary>
        event Action<string> NavigationRequested;

        /// <summary>Context Screen에서 이전 진입 화면으로 돌아가기를 요청한다.</summary>
        event Action BackRequested;

        /// <summary>
        /// 모드 표시 이름, Navigation, 기능 집합을 셸에 반영한다.
        /// </summary>
        void BindProfile(GameModeUiProfile profile);

        /// <summary>
        /// 상단 공통 상태와 모드 전용 슬롯을 셸에 반영한다.
        /// </summary>
        void BindStatus(ShellStatusModel status);

        /// <summary>
        /// 현재 Workspace의 Context Header와 선택 Route를 반영한다.
        /// </summary>
        void BindContext(ShellContextModel context);
    }
}
