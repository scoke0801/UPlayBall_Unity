namespace Baseball.Core.Balance
{
    /// <summary>
    /// 투구별 Ball·Swing·Contact 확률을 계산하는 계수를 보관한다.
    /// </summary>
    public readonly struct PlateDisciplineBalance
    {
        /// <summary>
        /// 타석 확률 모델의 계수를 생성한다.
        /// </summary>
        public PlateDisciplineBalance(
            double strikeZoneProbability,
            double controlStrikeZoneWeight,
            double strikeSwingProbability,
            double mentalStrikeSwingWeight,
            double chaseProbability,
            double mentalChaseWeight,
            double stuffChaseWeight,
            double velocityChaseWeight,
            double strikeContactProbability,
            double chaseContactProbability,
            double contactMatchupWeight,
            double velocityContactWeight,
            double fairContactProbability,
            double sameHandedContactPenalty,
            double oppositeHandedContactBonus)
        {
            StrikeZoneProbability = strikeZoneProbability;
            ControlStrikeZoneWeight = controlStrikeZoneWeight;
            StrikeSwingProbability = strikeSwingProbability;
            MentalStrikeSwingWeight = mentalStrikeSwingWeight;
            ChaseProbability = chaseProbability;
            MentalChaseWeight = mentalChaseWeight;
            StuffChaseWeight = stuffChaseWeight;
            VelocityChaseWeight = velocityChaseWeight;
            StrikeContactProbability = strikeContactProbability;
            ChaseContactProbability = chaseContactProbability;
            ContactMatchupWeight = contactMatchupWeight;
            VelocityContactWeight = velocityContactWeight;
            FairContactProbability = fairContactProbability;
            SameHandedContactPenalty = sameHandedContactPenalty;
            OppositeHandedContactBonus = oppositeHandedContactBonus;
        }

        public double StrikeZoneProbability { get; }
        public double ControlStrikeZoneWeight { get; }
        public double StrikeSwingProbability { get; }
        public double MentalStrikeSwingWeight { get; }
        public double ChaseProbability { get; }
        public double MentalChaseWeight { get; }
        public double StuffChaseWeight { get; }
        public double VelocityChaseWeight { get; }
        public double StrikeContactProbability { get; }
        public double ChaseContactProbability { get; }
        public double ContactMatchupWeight { get; }
        public double VelocityContactWeight { get; }
        public double FairContactProbability { get; }
        public double SameHandedContactPenalty { get; }
        public double OppositeHandedContactBonus { get; }
    }
}
