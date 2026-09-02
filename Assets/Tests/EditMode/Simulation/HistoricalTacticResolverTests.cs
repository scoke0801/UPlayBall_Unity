using System;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Historical;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>전술카드의 장착 규칙과 공통 발동·Counter 순서를 검증한다.</summary>
    public sealed class HistoricalTacticResolverTests
    {
        [Test]
        public void Loadout_슬롯과방해카드제약을강제한다()
        {
            TacticCardDefinition disruptionA = CreateCard("DISRUPT_A", true);
            TacticCardDefinition disruptionB = CreateCard("DISRUPT_B", true);
            TacticCardDefinition buff = CreateCard("BUFF", false);

            Assert.Throws<ArgumentException>(() =>
                new TacticLoadoutState(new[] { disruptionA, disruptionB }));
            Assert.Throws<ArgumentException>(() =>
                new TacticLoadoutState(new[] { buff, buff }));
            Assert.Throws<ArgumentException>(() =>
                new TacticLoadoutState(new[] { disruptionA, buff, disruptionB }));
        }

        [Test]
        public void ConfirmGame_조건미발동이어도카드를소비한다()
        {
            var loadout = new TacticLoadoutState(new[]
            {
                CreateCard(
                    "EIGHTH_INNING",
                    false,
                    new TacticTriggerCondition(TacticTriggerField.Inning, TacticComparison.Equal, 8))
            });

            loadout.ConfirmGame();

            Assert.That(loadout.IsGameConfirmed, Is.True);
            Assert.Throws<InvalidOperationException>(() => loadout.ConfirmGame());
        }

        [Test]
        public void Resolve_조건봉쇄CounterDebuffBuff순서를고정한다()
        {
            TacticCardDefinition homeCounter = CreateCard(
                "HOME_COUNTER",
                false,
                counters: new[] { "AWAY_BUFF" });
            TacticCardDefinition homeDebuff = CreateCard(
                "HOME_DEBUFF",
                true,
                new TacticTriggerCondition(TacticTriggerField.Inning, TacticComparison.GreaterOrEqual, 8));
            TacticCardDefinition awayBuff = CreateCard("AWAY_BUFF", false);
            TacticCardDefinition awayBlocked = CreateCard("AWAY_BLOCKED", false);
            var home = new TacticLoadoutState(new[] { homeCounter, homeDebuff });
            var away = new TacticLoadoutState(new[] { awayBuff, awayBlocked });
            home.ConfirmGame();
            away.ConfirmGame();
            var state = new TacticGameState(8, -2, 4, true, Handedness.Left, PitcherRole.Closer);

            TacticResolution result = new TacticCardResolver().Resolve(
                home,
                away,
                state,
                new[] { "AWAY_BLOCKED" });

            Assert.That(result.Cards.Count, Is.EqualTo(2));
            Assert.That(result.Cards[0].Card.CardId, Is.EqualTo("HOME_DEBUFF"));
            Assert.That(result.Cards[0].Stage, Is.EqualTo(TacticResolutionStage.OpponentDebuff));
            Assert.That(result.Cards[1].Card.CardId, Is.EqualTo("HOME_COUNTER"));
            Assert.That(result.Cards[1].Stage, Is.EqualTo(TacticResolutionStage.AllyBuff));
        }

        [Test]
        public void Resolve_동일입력은동일카드순서를반환한다()
        {
            var first = ResolveStableOrder();
            var second = ResolveStableOrder();

            Assert.That(second, Is.EqualTo(first));
        }

        private static string ResolveStableOrder()
        {
            var home = new TacticLoadoutState(new[] { CreateCard("Z_CARD", false), CreateCard("A_CARD", false) });
            var away = new TacticLoadoutState(Array.Empty<TacticCardDefinition>());
            home.ConfirmGame();
            away.ConfirmGame();
            TacticResolution result = new TacticCardResolver().Resolve(
                home,
                away,
                new TacticGameState(1, 0, 1, false, Handedness.Right, PitcherRole.Starter));
            return result.Cards[0].Card.CardId + "," + result.Cards[1].Card.CardId;
        }

        private static TacticCardDefinition CreateCard(
            string cardId,
            bool isDisruption,
            TacticTriggerCondition? condition = null,
            string[] counters = null)
        {
            TacticTriggerCondition[] conditions = condition.HasValue
                ? new[] { condition.Value }
                : Array.Empty<TacticTriggerCondition>();
            return new TacticCardDefinition(
                cardId,
                cardId,
                TacticCardCategory.Common,
                TacticTier.Normal,
                "테스트 Reference",
                "테스트 Balance",
                conditions,
                isDisruption ? TacticTargetRule.Opponent : TacticTargetRule.BattingTeam,
                new[] { new TacticStatModifier(PlayerAbility.Contact, isDisruption ? -1 : 1) },
                Array.Empty<TacticBehaviorModifier>(),
                TacticDurationRule.UntilInningEnd,
                counters ?? Array.Empty<string>(),
                isDisruption);
        }
    }
}
