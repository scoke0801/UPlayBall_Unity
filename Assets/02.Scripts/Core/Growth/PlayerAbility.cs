using System;

namespace Baseball.Core.Growth
{
    /// <summary>
    /// 영구 성장과 Potential을 개별적으로 추적하는 선수 능력치 식별자다.
    /// </summary>
    public enum PlayerAbility
    {
        Contact,
        Power,
        Speed,
        Arm,
        Defense,
        BatterMental,
        Stamina,
        Velocity,
        Stuff,
        Breaking,
        Control,
        PitcherMental,
        Count
    }

    /// <summary>
    /// 노쇠 적용 시 서로 다른 감소 곡선을 사용하는 능력치 계통이다.
    /// </summary>
    public enum AbilityFamily
    {
        Physical,
        Technical,
        Mental
    }

    /// <summary>
    /// 훈련 반복과 프로그램 적합도를 판정하는 활동 계통이다.
    /// </summary>
    public enum TrainingCategory
    {
        Rest,
        Rehabilitation,
        Strength,
        Batting,
        Defense,
        Pitching,
        Partner,
        StudyTechnical,
        StudyPhysical,
        Count
    }

    public enum WorkEthicGrade
    {
        Inconsistent,
        Normal,
        Diligent,
        VeryDiligent
    }

    public enum TrainingFitGrade
    {
        Low,
        Normal,
        High,
        VeryHigh
    }

    public enum CareerPhase
    {
        Growth,
        Prime,
        Skilled,
        Decline,
        LateCareer
    }

    public enum GrowthSourceType
    {
        NaturalDevelopment,
        PersonalTraining,
        TrainingPartner,
        Study,
        Aging,
        Injury
    }

    public enum GrowthInjuryResult
    {
        None,
        Discomfort
    }

    /// <summary>
    /// 능력치별 계통과 타자·투수 구분을 한곳에서 제공한다.
    /// </summary>
    public static class PlayerAbilityCatalog
    {
        public static int AbilityCount => (int)PlayerAbility.Count;

        public static AbilityFamily GetFamily(PlayerAbility ability)
        {
            return ability switch
            {
                PlayerAbility.Power => AbilityFamily.Physical,
                PlayerAbility.Speed => AbilityFamily.Physical,
                PlayerAbility.Stamina => AbilityFamily.Physical,
                PlayerAbility.Velocity => AbilityFamily.Physical,
                PlayerAbility.BatterMental => AbilityFamily.Mental,
                PlayerAbility.PitcherMental => AbilityFamily.Mental,
                PlayerAbility.Contact => AbilityFamily.Technical,
                PlayerAbility.Arm => AbilityFamily.Physical,
                PlayerAbility.Defense => AbilityFamily.Technical,
                PlayerAbility.Stuff => AbilityFamily.Technical,
                PlayerAbility.Breaking => AbilityFamily.Technical,
                PlayerAbility.Control => AbilityFamily.Technical,
                _ => throw new ArgumentOutOfRangeException(nameof(ability))
            };
        }

        public static bool IsBatterAbility(PlayerAbility ability)
        {
            return ability >= PlayerAbility.Contact && ability <= PlayerAbility.BatterMental;
        }

        public static bool IsPitcherAbility(PlayerAbility ability)
        {
            return ability >= PlayerAbility.Stamina && ability <= PlayerAbility.PitcherMental;
        }
    }
}
