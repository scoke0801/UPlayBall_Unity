using System;

namespace Baseball.Presentation.Match
{
    /// <summary>경기 HUD가 표시할 공격 이닝 방향이다.</summary>
    public enum MatchHudHalf
    {
        Top = 0,
        Bottom = 1
    }

    /// <summary>경기 HUD의 한 팀 이름·점수·공격 상태를 묶는다.</summary>
    public sealed class MatchHudTeamModel
    {
        /// <summary>경기 HUD에 필요한 팀 표시 값만 받는다.</summary>
        public MatchHudTeamModel(string name, int score, bool isBatting)
        {
            if (score < 0)
                throw new ArgumentOutOfRangeException(nameof(score));

            Name = name ?? string.Empty;
            Score = score;
            IsBatting = isBatting;
        }

        public string Name { get; }
        public int Score { get; }
        public bool IsBatting { get; }
    }

    /// <summary>경기 HUD가 표시할 현재 타자 또는 투수 정보다.</summary>
    public sealed class MatchHudParticipantModel
    {
        public static MatchHudParticipantModel Empty { get; } = new MatchHudParticipantModel(0, string.Empty);

        /// <summary>시뮬레이션 ID와 이미 해석된 표시 이름을 받는다.</summary>
        public MatchHudParticipantModel(int playerId, string name)
        {
            if (playerId < 0)
                throw new ArgumentOutOfRangeException(nameof(playerId));

            PlayerId = playerId;
            Name = name ?? string.Empty;
        }

        public int PlayerId { get; }
        public string Name { get; }
        public bool HasValue => PlayerId > 0;
    }

    /// <summary>볼·스트라이크·아웃 표시 값을 묶는다.</summary>
    public readonly struct MatchHudCountModel
    {
        public static MatchHudCountModel Empty => default;

        /// <summary>공개된 이벤트가 확정한 카운트를 그대로 받는다.</summary>
        public MatchHudCountModel(int balls, int strikes, int outs)
        {
            if (balls < 0)
                throw new ArgumentOutOfRangeException(nameof(balls));
            if (strikes < 0)
                throw new ArgumentOutOfRangeException(nameof(strikes));
            if (outs < 0)
                throw new ArgumentOutOfRangeException(nameof(outs));

            Balls = balls;
            Strikes = strikes;
            Outs = outs;
        }

        public int Balls { get; }
        public int Strikes { get; }
        public int Outs { get; }
    }

    /// <summary>현재 루상의 주자를 모드와 무관한 형태로 묶는다.</summary>
    public sealed class MatchHudBaseStateModel
    {
        public static MatchHudBaseStateModel Empty { get; } = new MatchHudBaseStateModel(null, null, null);

        /// <summary>각 베이스에 있는 선수를 받으며 빈 베이스에는 null을 허용한다.</summary>
        public MatchHudBaseStateModel(
            MatchHudParticipantModel first,
            MatchHudParticipantModel second,
            MatchHudParticipantModel third)
        {
            First = first ?? MatchHudParticipantModel.Empty;
            Second = second ?? MatchHudParticipantModel.Empty;
            Third = third ?? MatchHudParticipantModel.Empty;
        }

        public MatchHudParticipantModel First { get; }
        public MatchHudParticipantModel Second { get; }
        public MatchHudParticipantModel Third { get; }
        public bool HasRunnerOnFirst => First.HasValue;
        public bool HasRunnerOnSecond => Second.HasValue;
        public bool HasRunnerOnThird => Third.HasValue;
        public bool HasAnyRunner => HasRunnerOnFirst || HasRunnerOnSecond || HasRunnerOnThird;
    }

    /// <summary>Owner와 Player가 함께 사용할 경기 HUD의 불변 표시 모델이다.</summary>
    public sealed class MatchHudPresentationModel
    {
        internal MatchHudPresentationModel(
            int inning,
            MatchHudHalf half,
            MatchHudTeamModel awayTeam,
            MatchHudTeamModel homeTeam,
            MatchHudCountModel count,
            MatchHudBaseStateModel bases,
            MatchHudParticipantModel batter,
            MatchHudParticipantModel pitcher,
            bool isBetweenInnings)
        {
            Inning = inning;
            Half = half;
            AwayTeam = awayTeam;
            HomeTeam = homeTeam;
            Count = count;
            Bases = bases;
            Batter = batter;
            Pitcher = pitcher;
            IsBetweenInnings = isBetweenInnings;
        }

        public int Inning { get; }
        public MatchHudHalf Half { get; }
        public MatchHudTeamModel AwayTeam { get; }
        public MatchHudTeamModel HomeTeam { get; }
        public MatchHudCountModel Count { get; }
        public MatchHudBaseStateModel Bases { get; }
        public MatchHudParticipantModel Batter { get; }
        public MatchHudParticipantModel Pitcher { get; }
        public bool IsBetweenInnings { get; }
        public MatchHudTeamModel BattingTeam => AwayTeam.IsBatting ? AwayTeam : HomeTeam;
    }

    /// <summary>공개된 경기 상태를 HUD 표시 모델로 정규화한다.</summary>
    public sealed class MatchHudPresentationModelBuilder
    {
        /// <summary>이닝 교대 중에는 이전 타석의 카운트와 주자를 노출하지 않는다.</summary>
        public MatchHudPresentationModel Build(
            int inning,
            MatchHudHalf half,
            MatchHudTeamModel awayTeam,
            MatchHudTeamModel homeTeam,
            MatchHudCountModel count,
            MatchHudBaseStateModel bases,
            MatchHudParticipantModel batter,
            MatchHudParticipantModel pitcher,
            bool isBetweenInnings)
        {
            if (inning <= 0)
                throw new ArgumentOutOfRangeException(nameof(inning));
            if (awayTeam == null)
                throw new ArgumentNullException(nameof(awayTeam));
            if (homeTeam == null)
                throw new ArgumentNullException(nameof(homeTeam));
            if (awayTeam.IsBatting == homeTeam.IsBatting)
                throw new ArgumentException("공격 팀은 정확히 하나여야 합니다.");

            return new MatchHudPresentationModel(
                inning,
                half,
                awayTeam,
                homeTeam,
                isBetweenInnings ? MatchHudCountModel.Empty : count,
                isBetweenInnings ? MatchHudBaseStateModel.Empty : bases ?? MatchHudBaseStateModel.Empty,
                isBetweenInnings ? MatchHudParticipantModel.Empty : batter ?? MatchHudParticipantModel.Empty,
                isBetweenInnings ? MatchHudParticipantModel.Empty : pitcher ?? MatchHudParticipantModel.Empty,
                isBetweenInnings);
        }
    }
}
