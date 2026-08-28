using System;

namespace Baseball.Core.Balance
{
    /// <summary>
    /// 포지션 적합도와 구단 성향이 신인 평가에 미치는 가중치를 보관한다.
    /// </summary>
    public readonly struct PlayerEvaluationBalance
    {
        /// <summary>
        /// 핵심·보조·일반 능력치와 구단 성향의 평가 가중치를 생성한다.
        /// </summary>
        public PlayerEvaluationBalance(
            double keyAttributeWeight,
            double supportingAttributeWeight,
            double generalAttributeWeight,
            double teamPreferenceInfluence)
        {
            if (keyAttributeWeight <= 0d || supportingAttributeWeight <= 0d || generalAttributeWeight <= 0d)
                throw new ArgumentOutOfRangeException(nameof(keyAttributeWeight));
            if (teamPreferenceInfluence < 0d)
                throw new ArgumentOutOfRangeException(nameof(teamPreferenceInfluence));

            KeyAttributeWeight = keyAttributeWeight;
            SupportingAttributeWeight = supportingAttributeWeight;
            GeneralAttributeWeight = generalAttributeWeight;
            TeamPreferenceInfluence = teamPreferenceInfluence;
        }

        public double KeyAttributeWeight { get; }
        public double SupportingAttributeWeight { get; }
        public double GeneralAttributeWeight { get; }
        public double TeamPreferenceInfluence { get; }

        /// <summary>
        /// 같은 총합이라도 포지션에 맞는 배분이 평가에서 드러나는 최초 검증용 값을 만든다.
        /// </summary>
        public static PlayerEvaluationBalance CreateDefault()
        {
            return new PlayerEvaluationBalance(
                keyAttributeWeight: 2d,
                supportingAttributeWeight: 1.35d,
                generalAttributeWeight: 1d,
                teamPreferenceInfluence: 0.15d);
        }
    }
}
