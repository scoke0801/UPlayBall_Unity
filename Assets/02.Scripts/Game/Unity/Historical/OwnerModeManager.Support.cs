using System;
using System.Collections.Generic;
using Baseball.Core.Historical;
using UnityEngine;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        [Serializable]
        private sealed class SupportCatalogData { public OwnerSupportDefinition[] cards; }
        private OwnerSupportDefinition[] _supportCatalog;
        /// <summary>서포트 가격·조건·효과의 저작 데이터를 읽는다.</summary>
        public IReadOnlyList<OwnerSupportDefinition> GetSupportCatalog()
        {
            if (_supportCatalog != null) return _supportCatalog;
            var asset = Resources.Load<TextAsset>("NewGame/OwnerSupportCards");
            if (asset == null) throw new InvalidOperationException("서포트 카드 데이터를 읽을 수 없습니다.");
            var data = JsonUtility.FromJson<SupportCatalogData>(asset.text);
            if (data?.cards == null || data.cards.Length == 0) throw new InvalidOperationException("서포트 목록이 비어 있습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var card in data.cards)
            {
                card.Validate();
                if (!ids.Add(card.id)) throw new InvalidOperationException("서포트 카드 정의가 중복되었습니다.");
            }
            _supportCatalog = data.cards;
            return _supportCatalog;
        }
        private OwnerSupportDefinition GetSupportDefinition(string id)
        {
            foreach (var definition in GetSupportCatalog()) if (definition.id == id) return definition;
            throw new InvalidOperationException("서포트 카드가 없습니다.");
        }
        public void PurchaseSupport(string definitionId)
        {
            var definition = GetSupportDefinition(definitionId);
            CommitGrowthChange(runtime => { OwnerSupportService.Purchase(runtime, definition); return true; });
            NotifyRuntimeChanged();
        }
        public void EquipSupport(string definitionId, string cardId)
        {
            var definition = GetSupportDefinition(definitionId);
            CommitGrowthChange(runtime => { OwnerSupportService.Equip(runtime, definition, cardId); return true; });
            InvalidatePregame();
            NotifyRuntimeChanged();
        }
    }
}
