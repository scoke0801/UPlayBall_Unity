using System;
using System.Linq;
using System.Reflection;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Game.Career;
using Baseball.Game.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    public sealed class OwnerSupportTests
    {
        private static ManagerHistoricalRuntimeState Runtime(out ManagerHistoricalSaveAdapter adapter)
        {
            var type = typeof(ManagerHistoricalSaveTests).GetNestedType("Fixture", BindingFlags.NonPublic);
            object fixture = type.GetMethod("Create").Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            adapter = (ManagerHistoricalSaveAdapter)fixture.GetType().GetMethod("CreateAdapter").Invoke(fixture, null);
            return adapter.Restore(adapter.CreateSaveData((ManagerHistoricalRuntimeState)fixture.GetType().GetProperty("State").GetValue(fixture)));
        }
        private static OwnerSupportDefinition Definition(OwnerSupportScope scope = OwnerSupportScope.Team) => new OwnerSupportDefinition
        {
            id = "support", displayName = "타격 지원", scope = scope, target = OwnerSupportTarget.All, price = 100,
            bonuses = new[] { 1,0,0,0,0,0,0,0,0,0,0,0 }
        };

        [Test]
        public void 서포트는_저장복원후에도_정확히두경기지속한다()
        {
            var runtime = Runtime(out var adapter); var definition = Definition();
            OwnerSupportService.Purchase(runtime, definition);
            OwnerSupportService.Equip(runtime, definition, "");
            var target = OwnerSupportService.ResolveTargets(runtime, definition, "")[0];
            Assert.That(target.Training.Ledger.Get(OwnerGrowthSource.Support, PlayerAbility.Contact), Is.EqualTo(1));
            Assert.That(runtime.PlayerGrowth.Support.GetCount(definition.id), Is.Zero);
            OwnerSupportService.CompleteMatch(runtime);
            runtime = adapter.Restore(adapter.CreateSaveData(runtime));
            Assert.That(runtime.PlayerGrowth.Support.Assignments[0].RemainingGames, Is.EqualTo(1));
            OwnerSupportService.CompleteMatch(runtime);
            Assert.That(runtime.PlayerGrowth.Support.Assignments, Is.Empty);
            foreach (var owned in runtime.OwnedCards)
                Assert.That(owned.Training.Ledger.Get(OwnerGrowthSource.Support, PlayerAbility.Contact), Is.Zero);
        }
        [Test]
        public void 개인세슬롯과_선수중복은_재고를소비하기전에거부한다()
        {
            var runtime = Runtime(out _); var definition = Definition(OwnerSupportScope.Player);
            runtime.PlayerGrowth.Support.Add(definition.id, 5);
            var targets = OwnerSupportService.ResolveTargets(runtime, Definition(), "");
            for (int i = 0; i < 3; i++) OwnerSupportService.Equip(runtime, definition, targets[i].CardId);
            Assert.Throws<InvalidOperationException>(() => OwnerSupportService.Equip(runtime, definition, targets[0].CardId));
            Assert.Throws<InvalidOperationException>(() => OwnerSupportService.Equip(runtime, definition, targets[3].CardId));
            Assert.That(runtime.PlayerGrowth.Support.GetCount(definition.id), Is.EqualTo(2));
            runtime.PlayerGrowth.Support.Add("team"); var team = Definition(); team.id = "team";
            OwnerSupportService.Equip(runtime, team, "");
            Assert.That(targets[0].Training.Ledger.Get(OwnerGrowthSource.Support, PlayerAbility.Contact), Is.EqualTo(2));
        }
        [Test]
        public void 정확한구매비용과_대상없음은_부분변경없이처리한다()
        {
            var runtime = Runtime(out var adapter); var definition = Definition();
            var save = adapter.CreateSaveData(runtime); save.economy.money = 99; runtime = adapter.Restore(save);
            Assert.Throws<InvalidOperationException>(() => OwnerSupportService.Purchase(runtime, definition));
            Assert.That(runtime.Economy.Money, Is.EqualTo(99));
            runtime.Economy.AddMoney(1); OwnerSupportService.Purchase(runtime, definition);
            Assert.That(runtime.Economy.Money, Is.Zero);
            definition.maximumAge = 1;
            Assert.Throws<InvalidOperationException>(() => OwnerSupportService.Equip(runtime, definition, ""));
            Assert.That(runtime.PlayerGrowth.Support.GetCount(definition.id), Is.EqualTo(1));
        }
    }
}
