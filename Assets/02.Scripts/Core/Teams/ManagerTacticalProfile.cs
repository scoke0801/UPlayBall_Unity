using System;

namespace Baseball.Core.Teams
{
    /// <summary>
    /// 감독의 선택 확률이 아니라 각 전술 판단 임계값을 바꾸는 성향값을 보관한다.
    /// </summary>
    public readonly struct ManagerTacticalProfile
    {
        public ManagerTacticalProfile(
            int hookSpeed,
            int bullpenAggression,
            int bullpenRoleRigidity,
            int smallBallPreference,
            int runningAggression,
            int matchupPreference,
            int defensiveAggression,
            int starTrust)
        {
            HookSpeed = Validate(hookSpeed, nameof(hookSpeed));
            BullpenAggression = Validate(bullpenAggression, nameof(bullpenAggression));
            BullpenRoleRigidity = Validate(bullpenRoleRigidity, nameof(bullpenRoleRigidity));
            SmallBallPreference = Validate(smallBallPreference, nameof(smallBallPreference));
            RunningAggression = Validate(runningAggression, nameof(runningAggression));
            MatchupPreference = Validate(matchupPreference, nameof(matchupPreference));
            DefensiveAggression = Validate(defensiveAggression, nameof(defensiveAggression));
            StarTrust = Validate(starTrust, nameof(starTrust));
        }

        public int HookSpeed { get; }
        public int BullpenAggression { get; }
        public int BullpenRoleRigidity { get; }
        public int SmallBallPreference { get; }
        public int RunningAggression { get; }
        public int MatchupPreference { get; }
        public int DefensiveAggression { get; }
        public int StarTrust { get; }

        public static ManagerTacticalProfile Balanced => new ManagerTacticalProfile(
            50, 50, 50, 50, 50, 50, 50, 50);

        private static int Validate(int value, string parameterName)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }
}
