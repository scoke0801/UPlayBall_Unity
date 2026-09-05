using System;
using System.Collections.Generic;
using Baseball.Presentation.SharedUI;

namespace Baseball.Presentation.SharedScreens
{
    /// <summary>
    /// 공용 Action Bar에서 버튼의 위험도와 강조 방식을 구분한다.
    /// </summary>
    public enum SharedScreenActionStyle
    {
        Primary = 0,
        Secondary = 1,
        Dangerous = 2
    }

    /// <summary>
    /// 모드별 Provider가 공용 Action Bar에 공급하는 하나의 Command 표현이다.
    /// </summary>
    public sealed class SharedScreenActionModel
    {
        /// <summary>
        /// Action 식별자, 표시 이름, 권한, 활성 상태를 만든다.
        /// </summary>
        public SharedScreenActionModel(
            string actionId,
            string displayName,
            SharedScreenActionStyle style = SharedScreenActionStyle.Secondary,
            UiCapability requiredCapability = UiCapability.None,
            bool isEnabled = true,
            string disabledReason = null)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentException("Action 식별자는 비어 있을 수 없습니다.", nameof(actionId));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Action 표시 이름은 비어 있을 수 없습니다.", nameof(displayName));
            if (!isEnabled && string.IsNullOrWhiteSpace(disabledReason))
                throw new ArgumentException("비활성 Action에는 이유가 필요합니다.", nameof(disabledReason));

            ActionId = actionId;
            DisplayName = displayName;
            Style = style;
            RequiredCapability = requiredCapability;
            IsEnabled = isEnabled;
            DisabledReason = disabledReason ?? string.Empty;
        }

        public string ActionId { get; }
        public string DisplayName { get; }
        public SharedScreenActionStyle Style { get; }
        public UiCapability RequiredCapability { get; }
        public bool IsEnabled { get; }
        public string DisabledReason { get; }
    }

    /// <summary>
    /// Owner와 Player가 같은 Snapshot을 보면서 서로 다른 행동을 공급하도록 분리한다.
    /// </summary>
    public interface ISharedScreenActionProvider
    {
        /// <summary>
        /// 현재 Context에서 노출할 모드별 Action 목록을 반환한다.
        /// </summary>
        IReadOnlyList<SharedScreenActionModel> GetActions(SharedScreenContext context);

        /// <summary>
        /// 선택된 Action을 모드 전용 Command로 전달하고 처리 여부를 반환한다.
        /// </summary>
        bool TryExecute(string actionId, SharedScreenContext context);
    }
}
