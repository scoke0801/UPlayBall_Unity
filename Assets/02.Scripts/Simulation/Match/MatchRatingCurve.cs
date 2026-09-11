using System;
using Baseball.Core.Historical;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Core.Teams;

namespace Baseball.Simulation.Match
{
    /// <summary>능력 표시값을 변경하지 않고 기존 SoftCap/HardCap 표로 경기 효과 입력을 변환한다.</summary>
    public static class MatchRatingCurve
    {
        /// <summary>HardCap와 SoftCap을 거친 값의 효과 분산을 저작된 구간별 곡선으로 변환한다.</summary>
        public static int ResolveMatchInput(double effectiveRating, MatchRatingCurveBalance balance)
            => ResolveMatchInput(effectiveRating, balance, false);

        /// <summary>타자·투수 능력의 서로 다른 분포를 같은 경기 소비 경로에서 변환한다.</summary>
        public static int ResolveMatchInput(double effectiveRating, PlayerAbility ability, MatchRatingCurveBalance balance)
            => ResolveMatchInput(effectiveRating, balance, ability >= PlayerAbility.Stamina);

        private static int ResolveMatchInput(double effectiveRating, MatchRatingCurveBalance balance, bool isPitcherAbility)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            double curved = Resolve(effectiveRating, balance.Caps);
            double slope = isPitcherAbility ? balance.PitcherSlope : balance.Slope;
            double offset = isPitcherAbility ? balance.PitcherInputOffset : balance.InputOffset;
            double input = balance.Center + (curved - balance.Center) * slope + offset;
            if (balance.LowerSpreadEnd.HasValue && curved < balance.LowerSpreadEnd.Value)
            {
                // 신인·하위 리그까지 높은 분산을 외삽하면 동급 대결도 과도한 삼진전이 된다.
                double pivot = balance.LowerSpreadEnd.Value;
                input = balance.Center + (pivot - balance.Center) * slope + offset +
                    (curved - pivot) * balance.LowerSlope;
            }
            double? upperSpreadStart = isPitcherAbility ? balance.PitcherUpperSpreadStart : balance.UpperSpreadStart;
            if (upperSpreadStart.HasValue && curved > upperSpreadStart.Value)
            {
                // 일반 카드의 격차를 넓혀도 강화·성장이 중도에 100으로 포화되지 않게 한다.
                double pivot = upperSpreadStart.Value;
                double pivotInput = balance.Center + (pivot - balance.Center) * slope + offset;
                double maximum = Resolve(balance.Caps.HardCap, balance.Caps);
                double upperSlope = Math.Min(slope, Math.Max(0d, 100d - pivotInput) / (maximum - pivot));
                input = pivotInput + (curved - pivot) * upperSlope;
            }
            return (int)Math.Round(Math.Max(0d, Math.Min(100d,
                input)), MidpointRounding.AwayFromZero);
        }

        /// <summary>원본 선수는 보존하고 경기 시작 때만 별도 입력 스냅샷을 만든다.</summary>
        public static Player ProjectPlayer(Player source, MatchRatingCurveBalance balance)
        {
            if (source.HasResolvedMatchRatings) return source;
            int Map(int rating) => ResolveMatchInput(rating, balance);
            int MapPitcher(int rating) => ResolveMatchInput(rating, balance, true);
            BatterAttributes b = source.BatterAttributes;
            PitcherAttributes p = source.PitcherAttributes;
            return new Player(source.PlayerId, source.Name, source.PrimaryPosition, source.BattingHand,
                source.ThrowingHand, new BatterAttributes(Map(b.Contact), Map(b.Power), Map(b.Speed),
                    Map(b.Arm), Map(b.Defense), Map(b.Mental)),
                new PitcherAttributes(MapPitcher(p.Stamina), MapPitcher(p.Velocity), MapPitcher(p.Stuff), MapPitcher(p.Breaking), MapPitcher(p.Control), MapPitcher(p.Mental)),
                source.SecondaryPositions, source.Nationality, source.PitchRepertoire, source.TraitIds,
                source.BakedPitcherAttributes, source.PermanentPitcherAttributes, true, source.UncurvedPitcherAttributes,
                source.IsPositionEvidenceMissing);
        }

        /// <summary>교체 선수까지 같은 곡선을 거쳐 모든 DetailedMatch 소비를 일치시킨다.</summary>
        public static MatchRosterSnapshot ProjectRoster(MatchRosterSnapshot source, MatchRatingCurveBalance balance)
        {
            var slots = new LineupSlot[source.StartingLineup.Count];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new LineupSlot(ProjectPlayer(source.StartingLineup[i].Player, balance), source.StartingLineup[i].FieldingPosition);
            var bench = new Player[source.Bench.Count];
            for (int i = 0; i < bench.Length; i++) bench[i] = ProjectPlayer(source.Bench[i], balance);
            var bullpen = new PitcherRosterEntry[source.Bullpen.Count];
            for (int i = 0; i < bullpen.Length; i++) bullpen[i] = ProjectPitcher(source.Bullpen[i], balance);
            return new MatchRosterSnapshot(source.TeamId, source.TeamName, new Lineup(slots),
                ProjectPitcher(source.StartingPitcher, balance), bullpen, bench, source.ManagerProfile,
                source.RunningApproach, source.PlayerCharacterId, source.PlayerConditions, source.BatteryConditions);
        }

        private static PitcherRosterEntry ProjectPitcher(PitcherRosterEntry source, MatchRatingCurveBalance balance) =>
            new PitcherRosterEntry(ProjectPlayer(source.Player, balance), source.Role, source.Condition,
                source.RecentWorkload, source.PitchLimit, source.NaturalRole, source.ActiveRosterRole,
                source.PlayerSeasonId, source.NaturalRoleConfidence,
                source.CapacityMultiplier, source.RecoveryMultiplier);

        /// <summary>상한 이후 기울기만 줄인다. 반환값은 확률이나 백분율이 아닌 Resolver 입력이다.</summary>
        public static double Resolve(double effectiveRating, EffectiveRatingCapTable caps)
        {
            if (caps == null) throw new ArgumentNullException(nameof(caps));
            if (double.IsNaN(effectiveRating) || double.IsInfinity(effectiveRating))
                throw new ArgumentOutOfRangeException(nameof(effectiveRating));
            double rating = Math.Max(1d, Math.Min(caps.HardCap, effectiveRating));
            return rating <= caps.SoftCap
                ? rating
                : caps.SoftCap + (rating - caps.SoftCap) * caps.PostSoftCapSlope;
        }
    }
}
