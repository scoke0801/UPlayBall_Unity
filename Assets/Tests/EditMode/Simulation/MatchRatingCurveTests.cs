using System;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Match;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>영구·일시 보정 조합에서 상한, 순서 보존 및 기존 확률 입력의 연속성을 검증한다.</summary>
    public sealed class MatchRatingCurveTests
    {
        [Test]
        public void LowerSpread_저능력동급대결의입력을보존하고경계에서역전하지않는다()
        {
            var curve = Baseball.Core.Balance.MatchRatingCurveBalance.CreateDefault();
            Assert.That(MatchRatingCurve.ResolveMatchInput(50, curve), Is.InRange(45, 50));
            Assert.That(MatchRatingCurve.ResolveMatchInput(64, curve),
                Is.LessThanOrEqualTo(MatchRatingCurve.ResolveMatchInput(65, curve)));
            Assert.That(MatchRatingCurve.ResolveMatchInput(65, curve),
                Is.LessThanOrEqualTo(MatchRatingCurve.ResolveMatchInput(66, curve)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Baseball.Core.Balance.MatchRatingCurveBalance(
                45, 1.4, upperSpreadStart: 80, lowerSpreadEnd: 81));
        }

        [Test]
        public void UpperSpread_일반카드격차를확대해도고능력성장여유를보존한다()
        {
            var curve = new Baseball.Core.Balance.MatchRatingCurveBalance(45, 1.5,
                inputOffset: -21.25, pitcherSlope: 1.5, pitcherInputOffset: -18.75,
                upperSpreadStart: 80);
            foreach (var ability in new[] { Baseball.Core.Growth.PlayerAbility.Contact, Baseball.Core.Growth.PlayerAbility.Stuff })
            {
                int previous = 0;
                for (int rating = 1; rating <= 140; rating++)
                {
                    int input = MatchRatingCurve.ResolveMatchInput(rating, ability, curve);
                    Assert.That(input, Is.GreaterThanOrEqualTo(previous));
                    previous = input;
                }
                Assert.That(MatchRatingCurve.ResolveMatchInput(120, ability, curve), Is.LessThan(100));
                Assert.That(MatchRatingCurve.ResolveMatchInput(140, ability, curve), Is.EqualTo(100));
                Assert.That(MatchRatingCurve.ResolveMatchInput(100, ability, curve),
                    Is.GreaterThan(MatchRatingCurve.ResolveMatchInput(90, ability, curve)));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new Baseball.Core.Balance.MatchRatingCurveBalance(
                45, 2, upperSpreadStart: 90));
        }

        [Test]
        public void PitcherCurve_투수능력만별도로변환하고기본값은보존한다()
        {
            var baseline = new Baseball.Core.Balance.MatchRatingCurveBalance(45d, .45d);
            var candidate = new Baseball.Core.Balance.MatchRatingCurveBalance(45d, 1d,
                inputOffset: -13.75d, pitcherSlope: 1.5d, pitcherInputOffset: -26.25d);
            Assert.That(MatchRatingCurve.ResolveMatchInput(80, Baseball.Core.Growth.PlayerAbility.Contact, candidate), Is.EqualTo(66));
            Assert.That(MatchRatingCurve.ResolveMatchInput(80, Baseball.Core.Growth.PlayerAbility.Stamina, candidate), Is.EqualTo(71));
            Assert.That(MatchRatingCurve.ResolveMatchInput(80, Baseball.Core.Growth.PlayerAbility.Stamina, baseline),
                Is.EqualTo(MatchRatingCurve.ResolveMatchInput(80, baseline)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Baseball.Core.Balance.MatchRatingCurveBalance(45, .45, pitcherSlope: double.NaN));
        }

        [Test]
        public void InputOffset_기준점을유지하면서격차만확장한다()
        {
            var baseline = new Baseball.Core.Balance.MatchRatingCurveBalance(45d, .45d);
            var wider = new Baseball.Core.Balance.MatchRatingCurveBalance(45d, 1d, inputOffset: -13.75d);
            Assert.That(MatchRatingCurve.ResolveMatchInput(70, baseline), Is.EqualTo(56));
            Assert.That(MatchRatingCurve.ResolveMatchInput(70, wider), Is.EqualTo(56));
            Assert.That(MatchRatingCurve.ResolveMatchInput(80, wider) - MatchRatingCurve.ResolveMatchInput(60, wider), Is.EqualTo(20));
            Assert.That(MatchRatingCurve.ResolveMatchInput(1, wider), Is.EqualTo(0));
        }

        [Test]
        public void EndgameLayersPreserveOrderingUntilSharedHardCap()
        {
            var caps = EffectiveRatingCapTable.CreateInitial();
            int[] editions = { 0, 1, 3, 5 };
            int[] enhancement = { 0, 5 };
            int[] training = { 0, 3 };
            int[] teamColor = { 0, 17 };
            int[] condition = { -4, 0, 4 };
            foreach (int edition in editions)
            foreach (int level in enhancement)
            foreach (int growth in training)
            foreach (int color in teamColor)
            foreach (int form in condition)
            {
                EffectiveRatingResult low = EffectiveRatingResolver.Resolve(34, edition, growth, level, color, form, 0, caps);
                EffectiveRatingResult high = EffectiveRatingResolver.Resolve(75, edition, growth, level, color, form, 0, caps);
                Assert.That(high.CurveRating, Is.GreaterThan(low.CurveRating));
                Assert.That(high.Rating, Is.LessThanOrEqualTo(140));
            }
            // 서로 다른 투자 수준도 단순 보너스 합산만으로 표본 중앙값 순서를 뒤집지 않는다.
            Assert.That(EffectiveRatingResolver.Resolve(34, 5, 3, 5, 17, 4, 0, caps).CurveRating,
                Is.LessThan(EffectiveRatingResolver.Resolve(75, 0, 0, 0, 0, 0, 0, caps).CurveRating));
        }

        [Test]
        public void CurvePreservesNormalRatingsAndDiminishesOnlyBeyondSoftCap()
        {
            var caps = EffectiveRatingCapTable.CreateInitial();
            for (int rating = 1; rating <= 120; rating++)
                Assert.That(MatchRatingCurve.Resolve(rating, caps), Is.EqualTo(rating));
            Assert.That(MatchRatingCurve.Resolve(121, caps), Is.EqualTo(120.5d));
            Assert.That(MatchRatingCurve.Resolve(140, caps), Is.EqualTo(130d));
            Assert.That(MatchRatingCurve.Resolve(1000, caps), Is.EqualTo(130d));
            Assert.That(MatchRatingCurve.Resolve(120.001d, caps), Is.EqualTo(120.0005d).Within(1e-9));
        }

        [Test]
        public void CurveConsumesInjectedCapsAndRejectsNonFiniteRatings()
        {
            var caps = new EffectiveRatingCapTable(110, 150, 0.25d);
            Assert.That(MatchRatingCurve.Resolve(150, caps), Is.EqualTo(120d));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRatingCurve.Resolve(double.NaN, caps));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchRatingCurve.Resolve(double.PositiveInfinity, caps));
        }
    }
}
