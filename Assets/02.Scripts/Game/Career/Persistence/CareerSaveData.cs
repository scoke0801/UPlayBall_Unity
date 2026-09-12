using System;

namespace Baseball.Game.Career.Persistence
{
    /// <summary>현재 Bake와 세이브가 같은 정적 콘텐츠를 기준으로 하는지 판정하는 헤더다.</summary>
    [Serializable]
    public sealed class CareerSaveContentReference
    {
        public int assetFormatVersion;
        public int contentSchemaVersion;
        public string assetArchiveHash;
        public int normalizedSchemaVersion;
        public string normalizedContentHash;
        public string referenceDataVersion;
        public string generatorVersion;
        public string historicalBalanceVersion;
        public string contentHash;
        public int balanceVersion;
        public string balanceContentHash;
        public int worldRecordMode;
    }

    /// <summary>전체 Payload를 열지 않고 설정 화면에 표시할 수 있는 슬롯 요약이다.</summary>
    [Serializable]
    public sealed class CareerSaveSummaryData
    {
        public string playerName;
        public int playerId;
        public string playerPersonId;
        public string position;
        public string teamName;
        public int teamId;
        public string teamSeasonKey;
        public string franchiseId;
        public int originYear;
        public string leagueName;
        public int seasonId;
        public int year;
        public string seasonPhase;
        public long savedAtUtcTicks;
    }

    /// <summary>선수 커리어 디스크 포맷 v1의 최상위 DTO다.</summary>
    [Serializable]
    public sealed class CareerSaveData
    {
        public int saveVersion;
        public string buildVersion;
        public CareerSaveContentReference content;
        public CareerSaveSummaryData summary;
        public string careerChecksum;
        public string payloadSha256;
        public CareerSaveGraphData graph;
    }

    /// <summary>저장 대상 Aggregate의 루트와 시즌 전환 체크포인트를 묶는다.</summary>
    internal sealed class CareerSaveRuntimeRoot
    {
        public CareerState career;
        public CareerSeasonTransitionService seasonTransition;
    }

    /// <summary>복원 완료 후 Manager가 인수할 런타임 Aggregate다.</summary>
    public readonly struct CareerSaveRestoreResult
    {
        public CareerSaveRestoreResult(
            CareerState career,
            CareerSeasonTransitionService seasonTransition)
        {
            Career = career;
            SeasonTransition = seasonTransition;
        }

        public CareerState Career { get; }
        public CareerSeasonTransitionService SeasonTransition { get; }
    }

    public enum CareerSaveSlotStatus
    {
        Empty,
        Ready,
        Incompatible,
        Damaged
    }

    /// <summary>저장하지 않은 UI 위치 대신 복원된 도메인 상태에서 결정하는 진입 목적지다.</summary>
    public enum CareerResumeDestination
    {
        Home,
        PendingReaction,
        ContractDecision,
        SeasonReview,
        Offseason,
        RetirementRecap
    }

    /// <summary>설정 UI가 파일 구현을 모르고 슬롯 상태만 표시하도록 만든 읽기 모델이다.</summary>
    public readonly struct CareerSaveSlotView
    {
        public CareerSaveSlotView(
            CareerSaveSlotStatus status,
            CareerSaveSummaryData summary,
            string message,
            bool hasBackup)
        {
            Status = status;
            Summary = summary;
            Message = message ?? string.Empty;
            HasBackup = hasBackup;
        }

        public CareerSaveSlotStatus Status { get; }
        public CareerSaveSummaryData Summary { get; }
        public string Message { get; }
        public bool HasBackup { get; }
        public bool CanLoad => Status == CareerSaveSlotStatus.Ready;
    }

    /// <summary>저장 Command의 성공 여부와 플레이어용 설명을 함께 반환한다.</summary>
    public readonly struct CareerSaveCommandResult
    {
        public CareerSaveCommandResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess { get; }
        public string Message { get; }

        public static CareerSaveCommandResult Success(string message) => new(true, message);
        public static CareerSaveCommandResult Failure(string message) => new(false, message);
    }

    /// <summary>현재 Bake와 다른 세이브를 명시적인 호환 실패로 구분한다.</summary>
    public sealed class CareerSaveCompatibilityException : Exception
    {
        public CareerSaveCompatibilityException(string message) : base(message)
        {
        }
    }
}
