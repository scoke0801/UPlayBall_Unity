using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Game.Shop
{
    /// <summary>동일한 후보 범위에 스카우터 비용·정밀도를 적용해 공개 확률과 실제 구매가 공유할 풀을 만든다.</summary>
    public static class ScoutStaffPoolFactory
    {
        public static IReadOnlyList<ScoutPoolDefinition> Create(
            IReadOnlyList<ScoutPoolDefinition> source, IReadOnlyList<ScoutStaffDefinition> staff)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (staff == null || staff.Count == 0) throw new ArgumentException("스카우터가 필요합니다.", nameof(staff));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ScoutStaffDefinition definition in staff)
                if (definition == null || !ids.Add(definition.Id))
                    throw new ArgumentException("스카우터 정의가 비었거나 중복되었습니다.", nameof(staff));
            var result = new List<ScoutPoolDefinition>(source.Count * staff.Count);
            foreach (ScoutPoolDefinition pool in source)
            {
                for (int index = 0; index < staff.Count; index++)
                {
                    ScoutStaffDefinition definition = staff[index];
                    var costs = new double[11];
                    var editions = new double[8];
                    // 코스트가 한 단계 높아질 때마다 상대 가중치를 조정한다. 후보가 없는 코스트는
                    // 기존 ScoutRoller가 제외·재정규화하므로 범위 밖 선수를 섞지 않는다.
                    for (int cost = 1; cost <= 10; cost++)
                        costs[cost] = pool.GetCostWeight(cost) * Math.Pow(definition.CostWeightStep, cost - 1);
                    for (int edition = 0; edition < editions.Length; edition++)
                        editions[edition] = pool.GetEditionWeight((PlayerCardEdition)edition);
                    int price = checked((int)Math.Ceiling(pool.PriceSp * definition.PriceMultiplier));
                    // 첫 타입은 기존 상품 ID를 유지해 도감의 직접 이동과 구매 이력 참조를 보존한다.
                    string id = index == 0 ? pool.ScoutPoolId : pool.ScoutPoolId + ".staff_" + definition.Id;
                    result.Add(new ScoutPoolDefinition(id, pool.ScoutType, costs, editions, price,
                        pool.FranchiseFilter, pool.YearFilter, pool.EditionFilter, pool.RosterScope, definition));
                }
            }
            return result;
        }
    }
}
