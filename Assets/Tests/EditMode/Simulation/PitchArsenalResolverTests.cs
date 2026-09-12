using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>구종 개성과 성장 변화가 실제 공통 투구 판정까지 전달되는지 검증한다.</summary>
    public sealed class PitchArsenalResolverTests
    {
        [Test]
        public void 실전구종품질은100이후에도성장하지만표시등급은100을유지한다()
        {
            var entry = new PitchRepertoireEntry(PitchType.Slider, 95, true);
            var balance = PitchArsenalBalance.CreateDefault();
            double lower = PitchEffectivenessResolver.ResolveQuality(entry, 150, 150, 150, balance);
            double higher = PitchEffectivenessResolver.ResolveQuality(entry, 250, 250, 250, balance);
            Assert.That(lower, Is.GreaterThan(100d));
            Assert.That(higher, Is.GreaterThan(lower));
            Assert.That(higher, Is.LessThanOrEqualTo(250d));
            Assert.That(PitchEffectivenessResolver.ResolveStableQuality(entry,
                new PitcherAttributes(250, 250, 250, 250, 250, 250), balance,
                new PitcherAttributes(50, 50, 50, 50, 50, 50)), Is.EqualTo(100d));
        }

        [Test]
        public void 경기곡선은원본을보존하고상한이후에효과분산만줄인다()
        {
            // 상한·원본 보존 계약은 기획 기본값 변경과 독립된 명시적 곡선으로 검증한다.
            var curve = new MatchRatingCurveBalance(50d, .3d);
            Assert.That(MatchRatingCurve.ResolveMatchInput(30, curve), Is.EqualTo(44));
            Assert.That(MatchRatingCurve.ResolveMatchInput(100, curve), Is.EqualTo(65));
            Assert.That(MatchRatingCurve.ResolveMatchInput(120, curve), Is.EqualTo(71));
            Assert.That(MatchRatingCurve.ResolveMatchInput(140, curve), Is.EqualTo(77));
            Assert.That(MatchRatingCurve.ResolveMatchInput(200, curve), Is.EqualTo(88));
            Player source = new Player(1, "가상 곡선 선수", PlayerPosition.StartingPitcher,
                Handedness.Right, Handedness.Right, new BatterAttributes(100, 30, 50, 50, 50, 50),
                Ratings(100, 30));
            Player projected = MatchRatingCurve.ProjectPlayer(source, curve);
            Assert.That(source.BatterAttributes.Contact, Is.EqualTo(100));
            Assert.That(projected.BatterAttributes.Contact, Is.EqualTo(65));
            Assert.That(MatchRatingCurve.ProjectPlayer(projected, curve), Is.SameAs(projected));
            Assert.That(projected.WithPitchRepertoire(new[] { new PitchRepertoireEntry(PitchType.Slider, 60, true) })
                .HasResolvedMatchRatings, Is.True);
        }

        [Test]
        public void 실제구속은곡선압축없이카드대표값과일치하고Velocity10에포심은3점2오른다()
        {
            var balance = BalanceTable.CreateDefault();
            var entry = new PitchRepertoireEntry(PitchType.FourSeamFastball, 60, true);
            Player Build(int velocity) => MatchRatingCurve.ProjectPlayer(new Player(1, "가상 구속 투수",
                PlayerPosition.StartingPitcher, Handedness.Right, Handedness.Right,
                new BatterAttributes(50, 50, 50, 50, 50, 50),
                new PitcherAttributes(50, velocity, 50, 50, 50, 50), pitchRepertoire: new[] { entry }), balance.MatchRatingCurve);
            double Center(Player pitcher)
            {
                var resolver = new PitchExecutionResolver(balance, new Pcg32Random(7));
                PitchOption option = resolver.BuildPitchOptions(new PlateAppearanceMatchup(pitcher, pitcher, 50, false))[0];
                return (option.MinimumVelocityMph + option.MaximumVelocityMph) * 0.5d * 1.609344d;
            }
            Assert.That(Center(Build(60)) - Center(Build(50)), Is.EqualTo(3.2d).Within(1e-9));
            Assert.That(Center(Build(60)), Is.EqualTo(PitchEffectivenessResolver.ResolveVelocityKph(entry, 60, balance.PitchArsenal)).Within(1e-9));
            Assert.That(Build(60).UncurvedPitcherAttributes.Velocity, Is.EqualTo(60));
        }

        [Test]
        public void Velocity성장은직구가변화구보다크며개인Offset을유지한다()
        {
            PitchArsenalBalance balance = PitchArsenalBalance.CreateDefault();
            double fastball = VelocityGain(PitchType.FourSeamFastball, balance);
            Assert.That(VelocityGain(PitchType.TwoSeamFastball, balance), Is.EqualTo(fastball));
            foreach (PitchType type in new[] { PitchType.Slider, PitchType.Curveball, PitchType.Changeup })
            {
                Assert.That(VelocityGain(type, balance), Is.GreaterThan(0d));
                Assert.That(VelocityGain(type, balance), Is.LessThan(fastball));
            }
            var regular = new PitchRepertoireEntry(PitchType.Slider, 60, false);
            var faster = new PitchRepertoireEntry(PitchType.Slider, 60, false, velocityOffset: 2d);
            Assert.That(PitchEffectivenessResolver.ResolveVelocityKph(faster, 60, balance) -
                PitchEffectivenessResolver.ResolveVelocityKph(regular, 60, balance), Is.EqualTo(2d).Within(1e-9));
        }

        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        public void Breaking성장은구종별로다르고제구정확도를바꾸지않는다(int gain)
        {
            PitchArsenalBalance balance = PitchArsenalBalance.CreateDefault();
            double fastball = QualityGain(PitchType.FourSeamFastball, gain, balance);
            double twoSeam = QualityGain(PitchType.TwoSeamFastball, gain, balance);
            Assert.That(twoSeam, Is.GreaterThan(fastball));
            Assert.That(QualityGain(PitchType.Slider, gain, balance), Is.GreaterThan(twoSeam));
            Assert.That(QualityGain(PitchType.Curveball, gain, balance), Is.GreaterThan(twoSeam));
            var resolver = new PitchExecutionResolver(BalanceTable.CreateDefault(), new Pcg32Random(7));
            CommandEllipse before = resolver.CalculateCommandEllipse(CreateMatchup(50, 50), PitchType.Slider);
            CommandEllipse after = resolver.CalculateCommandEllipse(CreateMatchup(50 + gain, 50), PitchType.Slider);
            Assert.That(after.RadiusX, Is.EqualTo(before.RadiusX));
            Assert.That(after.RadiusY, Is.EqualTo(before.RadiusY));
        }

        [Test]
        public void Stable등급은초기숙련도를보존하고영구성장만반영한다()
        {
            var entry = new PitchRepertoireEntry(PitchType.Slider, 95, true);
            var balance = PitchArsenalBalance.CreateDefault();
            PitcherAttributes baked = Ratings(50, 50);
            PitcherAttributes trained = Ratings(55, 50);
            Assert.That(PitchEffectivenessResolver.ResolveStableQuality(entry, baked, balance), Is.EqualTo(95));
            Assert.That(PitchEffectivenessResolver.ResolveStableQuality(entry, trained, balance, baked), Is.GreaterThan(95));
            Assert.That(entry.BaseMastery, Is.EqualTo(95));
        }

        [Test]
        public void 등급진행도는현재구간과다음경계사이에서계산한다()
        {
            PitchGradeBalance grade = PitchArsenalBalance.CreateDefault().Grade;
            PitchGradeProgress progress = grade.GetProgress(57.5d);

            Assert.That(progress.CurrentGrade, Is.EqualTo("B"));
            Assert.That(progress.NextGrade, Is.EqualTo("A"));
            Assert.That(progress.Progress01, Is.EqualTo(0.5d).Within(1e-9));
            Assert.That(progress.RemainingToNext, Is.EqualTo(7.5d).Within(1e-9));
            Assert.That(grade.GetProgress(95d).HasNextGrade, Is.False);
        }

        [Test]
        public void 구종성장분산은주력과현재품질을보호한다()
        {
            var balance = PitchArsenalBalance.CreateDefault();
            var primary = new PitchRepertoireEntry(PitchType.Slider, 70, true);
            var auxiliary = new PitchRepertoireEntry(PitchType.Slider, 70, false);
            double three = PitchGrowthResolver.ResolveEfficiency(auxiliary, 3, 2, balance);
            double five = PitchGrowthResolver.ResolveEfficiency(auxiliary, 5, 4, balance);
            double six = PitchGrowthResolver.ResolveEfficiency(auxiliary, 6, 5, balance);
            Assert.That(five, Is.LessThan(three));
            Assert.That(six, Is.LessThan(five));
            Assert.That(PitchGrowthResolver.ResolveEfficiency(primary, 6, 0, balance),
                Is.EqualTo(PitchGrowthResolver.ResolveEfficiency(primary, 3, 0, balance)));
            Assert.That(PitchEffectivenessResolver.ResolveQuality(primary, 60, 60, 60, balance),
                Is.EqualTo(PitchEffectivenessResolver.ResolveQuality(auxiliary, 60, 60, 60, balance)));
        }

        [Test]
        public void 영구성장적성은경기입력에도반영되고초기품질은같다()
        {
            var balance = PitchArsenalBalance.CreateDefault();
            var slow = new PitchRepertoireEntry(PitchType.Slider, 60, true, developmentAffinity: 0.5d);
            var fast = new PitchRepertoireEntry(PitchType.Slider, 60, true, developmentAffinity: 1.5d);
            Player Build(PitchRepertoireEntry entry, int breaking) => new Player(1, "가상 성장 투수",
                PlayerPosition.StartingPitcher, Handedness.Right, Handedness.Right,
                new BatterAttributes(50, 50, 50, 50, 50, 50), Ratings(breaking, 50),
                pitchRepertoire: new[] { entry }, bakedPitcherAttributes: Ratings(50, 50),
                permanentPitcherAttributes: Ratings(breaking, 50));
            Assert.That(PitchEffectivenessResolver.ResolvePlayerQuality(slow, Build(slow, 50), 50, 50, 50, balance),
                Is.EqualTo(PitchEffectivenessResolver.ResolvePlayerQuality(fast, Build(fast, 50), 50, 50, 50, balance)));
            Assert.That(PitchEffectivenessResolver.ResolvePlayerQuality(fast, Build(fast, 60), 50, 60, 50, balance),
                Is.GreaterThan(PitchEffectivenessResolver.ResolvePlayerQuality(slow, Build(slow, 60), 50, 60, 50, balance)));
            Player copy = Build(fast, 60).WithPitchRepertoire(new[] { fast });
            Assert.That(copy.BakedPitcherAttributes.Breaking, Is.EqualTo(50));
            Assert.That(copy.PermanentPitcherAttributes.Breaking, Is.EqualTo(60));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void 미니게임옵션과실제투구는선수보유구종만소비한다(int count)
        {
            var entries = new PitchRepertoireEntry[count];
            for (int i = 0; i < count; i++)
                entries[i] = new PitchRepertoireEntry((PitchType)(8 + i), 60 + i, i == 0);
            Player pitcher = CreatePlayer(entries);
            var matchup = new PlateAppearanceMatchup(pitcher, pitcher, 50, false);
            var resolver = new PitchExecutionResolver(BalanceTable.CreateDefault(), new Pcg32Random(123));
            PitchOption[] options = resolver.BuildPitchOptions(matchup);
            Assert.That(options.Length, Is.EqualTo(count));
            for (int i = 0; i < count; i++)
            {
                Assert.That(options[i].PitchType, Is.EqualTo(entries[i].PitchType));
                Assert.That(options[i].Grade, Is.Not.Empty);
                Assert.That(resolver.Resolve(matchup,
                    new PitchSelectionCommand(i, entries[i].PitchType, new PlatePoint(0, 0))).PitchType,
                    Is.EqualTo(entries[i].PitchType));
            }
            Assert.Throws<ArgumentException>(() => resolver.Resolve(matchup,
                new PitchSelectionCommand(0, PitchType.FourSeamFastball, new PlatePoint(0, 0))));
        }

        [Test]
        public void 모든구종은실행가능한프로필과양수구속을가진다()
        {
            var balance = PitchArsenalBalance.CreateDefault();
            foreach (PitchType type in Enum.GetValues(typeof(PitchType)))
            {
                var entry = new PitchRepertoireEntry(type, 50, true);
                Assert.That(PitchTypeProfileCatalog.Get(type).BreakStartTime01, Is.InRange(0d, 1d));
                Assert.That(PitchEffectivenessResolver.ResolveVelocityKph(entry, 50, balance), Is.GreaterThan(60d));
            }
        }

        private static double VelocityGain(PitchType type, PitchArsenalBalance balance)
        {
            var entry = new PitchRepertoireEntry(type, 60, false);
            return PitchEffectivenessResolver.ResolveVelocityKph(entry, 60, balance) -
                PitchEffectivenessResolver.ResolveVelocityKph(entry, 50, balance);
        }

        private static double QualityGain(PitchType type, int gain, PitchArsenalBalance balance)
        {
            var entry = new PitchRepertoireEntry(type, 60, false);
            return PitchEffectivenessResolver.ResolveQuality(entry, 50, 50 + gain, 50, balance) -
                PitchEffectivenessResolver.ResolveQuality(entry, 50, 50, 50, balance);
        }

        private static PitcherAttributes Ratings(int breaking, int control) =>
            new PitcherAttributes(50, 50, 50, breaking, control, 50);

        private static Player CreatePlayer(PitchRepertoireEntry[] entries) => new Player(1, "가상 투수",
            PlayerPosition.StartingPitcher, Handedness.Right, Handedness.Right,
            new BatterAttributes(50, 50, 50, 50, 50, 50), Ratings(50, 50), pitchRepertoire: entries);

        private static PlateAppearanceMatchup CreateMatchup(int breaking, int control)
        {
            var player = new Player(1, "가상 투수", PlayerPosition.StartingPitcher,
                Handedness.Right, Handedness.Right, new BatterAttributes(50, 50, 50, 50, 50, 50),
                Ratings(breaking, control), pitchRepertoire: new[] { new PitchRepertoireEntry(PitchType.Slider, 60, true) });
            return new PlateAppearanceMatchup(player, player, 50, false);
        }
    }
}
