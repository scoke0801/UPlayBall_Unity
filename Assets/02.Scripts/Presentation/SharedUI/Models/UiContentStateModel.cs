using System;

namespace Baseball.Presentation.SharedUI
{
    /// <summary>
    /// Workspace가 데이터를 표시할 준비 상태를 구분한다.
    /// </summary>
    public enum UiContentStateKind
    {
        Ready = 0,
        Loading = 1,
        Empty = 2,
        Error = 3
    }

    /// <summary>
    /// Loading, Empty, Error 화면이 같은 표시 계약을 사용하도록 상태 문구를 묶는다.
    /// </summary>
    public sealed class UiContentStateModel
    {
        /// <summary>
        /// 콘텐츠 표시 준비가 끝난 기본 상태다.
        /// </summary>
        public static UiContentStateModel Ready { get; } =
            new UiContentStateModel(UiContentStateKind.Ready, string.Empty, string.Empty, string.Empty, string.Empty);

        private UiContentStateModel(
            UiContentStateKind kind,
            string title,
            string message,
            string actionId,
            string actionLabel)
        {
            Kind = kind;
            Title = title;
            Message = message;
            ActionId = actionId;
            ActionLabel = actionLabel;
        }

        /// <summary>
        /// 현재 콘텐츠 상태 종류다.
        /// </summary>
        public UiContentStateKind Kind { get; }

        /// <summary>
        /// 상태의 핵심 문구다.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 원인이나 다음 행동을 설명하는 보조 문구다.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 재시도나 생성 같은 선택 행동의 안정적인 식별자다.
        /// </summary>
        public string ActionId { get; }

        /// <summary>
        /// 선택 행동 버튼에 표시할 문구다.
        /// </summary>
        public string ActionLabel { get; }

        /// <summary>
        /// 진행 중인 작업을 설명하는 Loading 상태를 만든다.
        /// </summary>
        public static UiContentStateModel CreateLoading(string title, string message = null)
        {
            return new UiContentStateModel(
                UiContentStateKind.Loading,
                RequireTitle(title),
                message ?? string.Empty,
                string.Empty,
                string.Empty);
        }

        /// <summary>
        /// 표시할 항목이 없음을 설명하고 선택 행동을 제공할 수 있는 Empty 상태를 만든다.
        /// </summary>
        public static UiContentStateModel CreateEmpty(
            string title,
            string message = null,
            string actionId = null,
            string actionLabel = null)
        {
            ValidateAction(actionId, actionLabel);
            return new UiContentStateModel(
                UiContentStateKind.Empty,
                RequireTitle(title),
                message ?? string.Empty,
                actionId ?? string.Empty,
                actionLabel ?? string.Empty);
        }

        /// <summary>
        /// 실패 원인과 재시도 행동을 제공할 수 있는 Error 상태를 만든다.
        /// </summary>
        public static UiContentStateModel CreateError(
            string title,
            string message,
            string actionId = null,
            string actionLabel = null)
        {
            ValidateAction(actionId, actionLabel);
            return new UiContentStateModel(
                UiContentStateKind.Error,
                RequireTitle(title),
                message ?? string.Empty,
                actionId ?? string.Empty,
                actionLabel ?? string.Empty);
        }

        private static string RequireTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("상태 제목은 비어 있을 수 없습니다.", nameof(title));
            return title;
        }

        private static void ValidateAction(string actionId, string actionLabel)
        {
            bool hasActionId = !string.IsNullOrWhiteSpace(actionId);
            bool hasActionLabel = !string.IsNullOrWhiteSpace(actionLabel);
            if (hasActionId != hasActionLabel)
                throw new ArgumentException("상태 행동 ID와 표시 이름은 함께 지정해야 합니다.");
        }
    }
}
