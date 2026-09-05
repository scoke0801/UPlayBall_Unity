using System;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// 현재 Workspace의 Route와 짧은 업무 맥락을 Context Header에 전달한다.
    /// </summary>
    public sealed class ShellContextModel
    {
        /// <summary>
        /// Route와 제목, 현재 상태를 설명하는 한 줄 요약으로 Context를 만든다.
        /// </summary>
        public ShellContextModel(
            string routeId,
            string title,
            string summary = null,
            string eyebrow = null,
            bool canGoBack = false,
            string backLabel = null)
        {
            if (string.IsNullOrWhiteSpace(routeId))
                throw new ArgumentException("Context Route는 비어 있을 수 없습니다.", nameof(routeId));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Context 제목은 비어 있을 수 없습니다.", nameof(title));

            RouteId = routeId;
            Title = title;
            Summary = summary ?? string.Empty;
            Eyebrow = eyebrow ?? string.Empty;
            CanGoBack = canGoBack;
            BackLabel = string.IsNullOrWhiteSpace(backLabel) ? "이전" : backLabel;
        }

        /// <summary>
        /// 현재 Workspace를 식별하는 Route다.
        /// </summary>
        public string RouteId { get; }

        /// <summary>
        /// 현재 화면의 짧은 제목이다.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 인원, 필터, Cost처럼 현재 업무 상태를 설명하는 한 줄 요약이다.
        /// </summary>
        public string Summary { get; }

        /// <summary>
        /// 제목 앞에 붙일 선택적 상위 맥락이다.
        /// </summary>
        public string Eyebrow { get; }

        /// <summary>현재 화면이 진입 원점으로 돌아갈 수 있는 Context Screen인지 나타낸다.</summary>
        public bool CanGoBack { get; }

        /// <summary>Context Header의 뒤로가기 버튼에 표시할 짧은 문구다.</summary>
        public string BackLabel { get; }
    }
}
