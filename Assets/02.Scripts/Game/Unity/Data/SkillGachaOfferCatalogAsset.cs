using System;
using System.Text;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using UnityEngine;

namespace Baseball.Game.Data
{
    /// <summary>등급별 가격·확률·구매 제한을 저작하고 순수 뽑기 밸런스로 변환한다.</summary>
    [CreateAssetMenu(fileName = "SkillGachaOfferCatalog", menuName = "Baseball/Data/Growth/Skill Gacha Offer Catalog")]
    public sealed class SkillGachaOfferCatalogAsset : ScriptableObject
    {
        [Serializable]
        private struct OfferData
        {
            [SerializeField] private SkillGachaPurchaseTier _tier;
            [SerializeField, Min(1)] private long _price;
            [SerializeField, Min(0)] private int _maxPurchasesPerOffseason;
            [SerializeField, Range(0f, 1f)] private double _normal;
            [SerializeField, Range(0f, 1f)] private double _rare;
            [SerializeField, Range(0f, 1f)] private double _elite;
            [SerializeField, Range(0f, 1f)] private double _unique;
            [SerializeField, Range(0f, 1f)] private double _legendary;

            public SkillGachaOfferBalance ToDefinition()
            {
                return new SkillGachaOfferBalance(
                    _tier,
                    (SkillBlockRarity)_tier,
                    _price,
                    _maxPurchasesPerOffseason,
                    _normal,
                    _rare,
                    _elite,
                    _unique,
                    _legendary);
            }

            public void AppendContent(StringBuilder builder)
            {
                builder.Append((int)_tier).Append(':').Append(_price).Append(':')
                    .Append(_maxPurchasesPerOffseason).Append(':');
                GrowthContentHashFormatting.AppendDouble(builder, _normal);
                builder.Append(':');
                GrowthContentHashFormatting.AppendDouble(builder, _rare);
                builder.Append(':');
                GrowthContentHashFormatting.AppendDouble(builder, _elite);
                builder.Append(':');
                GrowthContentHashFormatting.AppendDouble(builder, _unique);
                builder.Append(':');
                GrowthContentHashFormatting.AppendDouble(builder, _legendary);
                builder.Append(';');
            }
        }

        [SerializeField] private bool _replaceBuiltInOffers;
        [SerializeField] private OfferData[] _offers = Array.Empty<OfferData>();
        [SerializeField, Range(0f, 0.99f)] private double _fivePullDiscountRate = 0.05d;
        [SerializeField, Min(1)] private int _elitePity = 10;
        [SerializeField, Min(2)] private int _uniquePity = 30;
        [SerializeField, Min(3)] private int _legendaryPity = 60;
        [SerializeField, Min(0)] private int _legendaryMinimumCareerAwards = 1;
        [SerializeField] private bool _highTierPurchasesRequireOffseason = true;

        public SkillGachaBalanceTable Build(SkillGachaBalanceTable builtIn)
        {
            if (!_replaceBuiltInOffers)
                return builtIn;
            if (_offers == null || _offers.Length != 5)
                throw new InvalidOperationException("Normal부터 Legendary까지 다섯 뽑기 상품이 필요합니다.");
            var definitions = new SkillGachaOfferBalance[5];
            for (int index = 0; index < _offers.Length; index++)
            {
                SkillGachaOfferBalance offer = _offers[index].ToDefinition();
                definitions[(int)offer.Tier] = offer;
            }
            return new SkillGachaBalanceTable(
                definitions[0], definitions[1], definitions[2], definitions[3], definitions[4],
                _fivePullDiscountRate,
                _elitePity,
                _uniquePity,
                _legendaryPity,
                _legendaryMinimumCareerAwards,
                _highTierPurchasesRequireOffseason);
        }

        internal void AppendContent(StringBuilder builder)
        {
            builder.Append("gacha:").Append(_replaceBuiltInOffers).Append('|');
            GrowthContentHashFormatting.AppendDouble(builder, _fivePullDiscountRate);
            builder.Append('|').Append(_elitePity).Append('|')
                .Append(_uniquePity).Append('|').Append(_legendaryPity).Append('|')
                .Append(_legendaryMinimumCareerAwards).Append('|')
                .Append(_highTierPurchasesRequireOffseason).Append('|');
            for (int index = 0; index < (_offers?.Length ?? 0); index++)
                _offers[index].AppendContent(builder);
        }
    }
}
