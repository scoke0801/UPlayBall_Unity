using System.Collections.Generic;
using Baseball.Core.Shop;
using Baseball.Presentation.Owner;
using UnityEngine.UI;

namespace Baseball.Presentation.Shop
{
    /// <summary>즉시 사용 상품이 지정할 보유 선수의 표시 정보다.</summary>
    public readonly struct ShopTargetSnapshot
    {
        public ShopTargetSnapshot(string cardId, string label) { CardId = cardId; Label = label; }
        public string CardId { get; }
        public string Label { get; }
    }

    public sealed partial class UI_Scene_Shop
    {
        private IReadOnlyList<ShopTargetSnapshot> _targets = new ShopTargetSnapshot[0];
        private Dropdown _targetDropdown;
        public string SelectedTargetCardId => _activeDetails?.Kind == ShopProductKind.StudyReset &&
            _targetDropdown != null && _targetDropdown.value > 0 && _targetDropdown.value <= _targets.Count
                ? _targets[_targetDropdown.value - 1].CardId : null;

        /// <summary>결제할 수 있는 선수만 공급받고 이전 대상 선택을 지운다.</summary>
        public void BindTargets(IReadOnlyList<ShopTargetSnapshot> targets)
        {
            _targets = targets ?? new ShopTargetSnapshot[0];
            if (_targetDropdown == null) return;
            var labels = new List<string> { _targets.Count == 0
                ? "초기화할 선수 없음 · 유학 완료 후 이용" : "유학을 초기화할 선수를 선택하세요" };
            foreach (var target in _targets) labels.Add(target.Label);
            _targetDropdown.ClearOptions();
            _targetDropdown.AddOptions(labels);
            _targetDropdown.SetValueWithoutNotify(0);
            _targetDropdown.template.sizeDelta = new UnityEngine.Vector2(0, System.Math.Min(7, labels.Count) * 28 + 8);
            RefreshTargetPurchase();
        }

        private void RefreshTargetPurchase()
        {
            if (_confirmationPurchaseButton == null) return;
            _confirmationPurchaseButton.interactable = !_isProcessing && _activeDetails != null &&
                _activeDetails.CanPurchase && (_activeDetails.Kind != ShopProductKind.StudyReset || SelectedTargetCardId != null);
        }
    }
}
