using System;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 주입받은 아키타입 풀과 이름 후보로 새 게임의 Rookie League 구단을 결정론적으로 생성한다.
    /// </summary>
    public sealed class TeamGenerator
    {
        private static readonly PlayerPosition[] FieldPositions =
        {
            PlayerPosition.Catcher,
            PlayerPosition.FirstBase,
            PlayerPosition.SecondBase,
            PlayerPosition.ThirdBase,
            PlayerPosition.Shortstop,
            PlayerPosition.LeftField,
            PlayerPosition.CenterField,
            PlayerPosition.RightField,
            PlayerPosition.DesignatedHitter,
            PlayerPosition.StartingPitcher,
            PlayerPosition.ReliefPitcher
        };

        private readonly IRandomSource _random;
        private readonly TeamGenerationBalance _balance;

        private static readonly string[] DefaultPlayerNamePool =
        {
            "김도윤", "이준서", "박시우", "최민재", "정우진", "강현우", "조성민", "윤태호",
            "장민준", "임재현", "한승우", "오지훈", "서동현", "신예준", "권민성", "황준혁",
            "안지환", "송재원", "전성훈", "홍민기", "유건우", "고은찬", "문태윤", "양시현"
        };

        /// <summary>
        /// 결정론적 RNG를 주입받아 구단 생성기를 구성한다.
        /// </summary>
        public TeamGenerator(IRandomSource random)
            : this(TeamGenerationBalance.CreateDefault(), random)
        {
        }

        /// <summary>
        /// 구단 생성 계수와 결정론적 RNG를 주입받아 생성기를 구성한다.
        /// </summary>
        public TeamGenerator(TeamGenerationBalance balance, IRandomSource random)
        {
            _balance = balance;
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// archetypePool과 namePool을 조합해 teamCount개의 구단을 생성한다.
        /// namePool은 teamCount 이상이어야 하며, 이름은 중복 없이 소비된다.
        /// </summary>
        public GeneratedTeam[] GenerateLeague(
            int teamCount,
            TeamArchetypeProfile[] archetypePool,
            string[] namePool)
        {
            if (namePool == null || namePool.Length < teamCount)
                throw new ArgumentException("이름 후보가 구단 수보다 적습니다.", nameof(namePool));

            var identities = new TeamIdentityDefinition[namePool.Length];
            for (int index = 0; index < namePool.Length; index++)
                identities[index] = new TeamIdentityDefinition(namePool[index], new TeamColor(128, 128, 128));

            return GenerateLeague(teamCount, archetypePool, identities, DefaultPlayerNamePool);
        }

        /// <summary>
        /// SO에서 변환된 구단 정체성과 선수 이름 풀로 Rookie League를 생성한다.
        /// </summary>
        public GeneratedTeam[] GenerateLeague(
            int teamCount,
            TeamArchetypeProfile[] archetypePool,
            TeamIdentityDefinition[] identityPool,
            string[] playerNamePool)
        {
            if (teamCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(teamCount));
            if (archetypePool == null || archetypePool.Length == 0)
                throw new ArgumentException("아키타입 풀이 비어 있습니다.", nameof(archetypePool));
            if (identityPool == null || identityPool.Length < teamCount)
                throw new ArgumentException("구단 정체성 후보가 구단 수보다 적습니다.", nameof(identityPool));
            if (playerNamePool == null || playerNamePool.Length == 0)
                throw new ArgumentException("선수 이름 후보가 비어 있습니다.", nameof(playerNamePool));

            bool[] identityUsed = new bool[identityPool.Length];
            var teams = new GeneratedTeam[teamCount];

            for (int index = 0; index < teamCount; index++)
            {
                TeamArchetypeProfile archetype = VaryArchetype(
                    archetypePool[(int)(_random.NextDouble() * archetypePool.Length)]);
                TeamIdentityDefinition identity = DrawUnusedIdentity(identityPool, identityUsed);
                int[] positionNeeds = GeneratePositionNeeds(archetype);
                RosterCompetitor[] competitors = GenerateCompetitors(index + 1, positionNeeds, playerNamePool);
                teams[index] = new GeneratedTeam(
                    index + 1,
                    identity.Name,
                    archetype,
                    identity.PrimaryColor,
                    positionNeeds,
                    competitors);
            }

            return teams;
        }

        private TeamIdentityDefinition DrawUnusedIdentity(
            TeamIdentityDefinition[] identityPool,
            bool[] identityUsed)
        {
            int index;
            do
            {
                index = (int)(_random.NextDouble() * identityPool.Length);
            }
            while (identityUsed[index]);

            identityUsed[index] = true;
            return identityPool[index];
        }

        private int[] GeneratePositionNeeds(TeamArchetypeProfile archetype)
        {
            var needs = new int[(int)PlayerPosition.ReliefPitcher + 1];
            for (int index = 0; index < FieldPositions.Length; index++)
            {
                double baseNeed = _balance.PositionNeedBase -
                                  archetype.RosterDepth * _balance.RosterDepthNeedWeight;
                double variance = (_random.NextDouble() - 0.5d) * _balance.PositionNeedVariance;
                int need = (int)Clamp(
                    baseNeed + variance,
                    _balance.MinimumPositionNeed,
                    _balance.MaximumPositionNeed);
                needs[(int)FieldPositions[index]] = need;
            }

            return needs;
        }

        private TeamArchetypeProfile VaryArchetype(TeamArchetypeProfile source)
        {
            return new TeamArchetypeProfile(
                source.Archetype,
                VaryRating(source.Budget),
                VaryRating(source.Development),
                VaryRating(source.RosterDepth),
                VaryRating(source.Scouting));
        }

        private int VaryRating(int source)
        {
            double centered = (_random.NextDouble() * 2d) - 1d;
            return (int)Clamp(source + centered * _balance.ArchetypeVariation, 0d, 100d);
        }

        private RosterCompetitor[] GenerateCompetitors(
            int teamId,
            int[] positionNeeds,
            string[] playerNamePool)
        {
            int competitorCount = FieldPositions.Length * _balance.CompetitorsPerPosition;
            var competitors = new RosterCompetitor[competitorCount];
            int resultIndex = 0;

            for (int positionIndex = 0; positionIndex < FieldPositions.Length; positionIndex++)
            {
                PlayerPosition position = FieldPositions[positionIndex];
                for (int slot = 0; slot < _balance.CompetitorsPerPosition; slot++)
                {
                    double variance = (_random.NextDouble() - 0.5d) * _balance.CompetitorOverallVariance;
                    double rawOverall = _balance.CompetitorOverallBase -
                                        positionNeeds[(int)position] * _balance.PositionNeedCompetitorWeight +
                                        variance;
                    int overall = (int)Clamp(
                        rawOverall,
                        _balance.MinimumCompetitorOverall,
                        _balance.MaximumCompetitorOverall);
                    string name = playerNamePool[(int)(_random.NextDouble() * playerNamePool.Length)];
                    int playerId = teamId * 1000 + resultIndex + 1;
                    competitors[resultIndex++] = new RosterCompetitor(playerId, name, position, overall);
                }
            }

            return competitors;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum)
                return minimum;
            if (value > maximum)
                return maximum;
            return value;
        }
    }

    /// <summary>
    /// 경쟁자 OVR을 유지하면서 포지션 평가 가중치에 맞는 개별 능력치 프로필을 만든다.
    /// </summary>
    public sealed class RosterPlayerAttributeGenerator
    {
        private const int AttributeCount = 6;
        private const int MinimumAttribute = 0;
        private const int MaximumAttribute = 100;
        private readonly TeamGenerationBalance _generationBalance;
        private readonly PlayerEvaluationBalance _evaluationBalance;
        private readonly IRandomSource _random;

        public RosterPlayerAttributeGenerator(
            TeamGenerationBalance generationBalance,
            PlayerEvaluationBalance evaluationBalance,
            IRandomSource random)
        {
            _generationBalance = generationBalance;
            _evaluationBalance = evaluationBalance;
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>야수 포지션의 핵심 능력은 높이고 OVR은 기준값에 맞춘다.</summary>
        public BatterAttributes GenerateBatter(PlayerPosition position, int overall)
        {
            if (position < PlayerPosition.Catcher || position > PlayerPosition.DesignatedHitter)
                throw new ArgumentOutOfRangeException(nameof(position));
            int[] values = Generate(
                overall,
                PlayerValueEvaluator.GetBatterWeights(_evaluationBalance, position));
            return new BatterAttributes(
                values[0], values[1], values[2], values[3], values[4], values[5]);
        }

        /// <summary>선발과 구원의 역할 차이에 맞춰 투수 능력치를 분산한다.</summary>
        public PitcherAttributes GeneratePitcher(PlayerPosition position, int overall)
        {
            if (position is not (PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher))
                throw new ArgumentOutOfRangeException(nameof(position));
            int[] values = Generate(
                overall,
                PlayerValueEvaluator.GetPitcherWeights(_evaluationBalance, position));
            return new PitcherAttributes(
                values[0], values[1], values[2], values[3], values[4], values[5]);
        }

        private int[] Generate(
            int overall,
            PlayerValueEvaluator.AttributeWeightProfile weights)
        {
            if (overall < MinimumAttribute || overall > MaximumAttribute)
                throw new ArgumentOutOfRangeException(nameof(overall));

            var values = new int[AttributeCount];
            double weightedMean = CalculateWeightedMean(weights);
            double weightRange = Math.Max(
                0.0001d,
                _evaluationBalance.KeyAttributeWeight - _evaluationBalance.GeneralAttributeWeight);
            for (int index = 0; index < values.Length; index++)
            {
                double profileOffset = (weights.Get(index) - weightedMean) / weightRange *
                                       _generationBalance.CompetitorAttributeProfileSpread;
                double variance = (_random.NextDouble() * 2d - 1d) *
                                  _generationBalance.CompetitorAttributeVariance;
                values[index] = ClampRating((int)Math.Round(
                    overall + profileOffset + variance,
                    MidpointRounding.AwayFromZero));
            }

            // 모든 능력치를 같은 폭으로 옮기면 프로필의 모양은 보존하면서 가중 OVR만 보정된다.
            int correction = overall - CalculateWeightedOverall(values, weights);
            for (int index = 0; index < values.Length; index++)
                values[index] = ClampRating(values[index] + correction);
            return values;
        }

        private static double CalculateWeightedMean(
            PlayerValueEvaluator.AttributeWeightProfile weights)
        {
            double squareSum = 0d;
            for (int index = 0; index < AttributeCount; index++)
            {
                double weight = weights.Get(index);
                squareSum += weight * weight;
            }
            return squareSum / weights.Total;
        }

        private static int CalculateWeightedOverall(
            int[] values,
            PlayerValueEvaluator.AttributeWeightProfile weights)
        {
            double total = 0d;
            for (int index = 0; index < AttributeCount; index++)
                total += values[index] * weights.Get(index);
            return (int)Math.Round(total / weights.Total, MidpointRounding.AwayFromZero);
        }

        private static int ClampRating(int value)
        {
            if (value < MinimumAttribute) return MinimumAttribute;
            return value > MaximumAttribute ? MaximumAttribute : value;
        }
    }
}
