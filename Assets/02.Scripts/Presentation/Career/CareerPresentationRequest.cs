using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baseball.Presentation.Career
{
    public enum CareerPresentationGrade
    {
        Major,
        Activity,
        Compact
    }

    public enum CareerPresentationMode
    {
        Full,
        Simplified,
        ResultOnly
    }

    /// <summary>연출 한 장에 표시할 라벨과 확정 값을 묶는다.</summary>
    public readonly struct PresentationStat
    {
        public PresentationStat(string label, string value, bool isEmphasized = false)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            IsEmphasized = isEmphasized;
        }

        public string Label { get; }
        public string Value { get; }
        public bool IsEmphasized { get; }
    }

    /// <summary>도메인 결과를 변경하지 않고 한 번의 커리어 연출에 필요한 표시 값만 전달한다.</summary>
    public sealed class CareerPresentationRequest
    {
        public CareerPresentationRequest(
            string requestId,
            CareerPresentationType type,
            CareerPresentationGrade grade,
            int seasonYear,
            string category,
            string title,
            string playerName,
            string description,
            PresentationStat[] stats,
            int startWeek = 0,
            int endWeek = 0,
            Action completed = null)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                throw new ArgumentException("연출 RequestId는 비어 있을 수 없습니다.", nameof(requestId));
            RequestId = requestId;
            Type = type;
            Grade = grade;
            SeasonYear = seasonYear;
            Category = category ?? string.Empty;
            Title = title ?? string.Empty;
            PlayerName = playerName ?? string.Empty;
            Description = description ?? string.Empty;
            Stats = stats ?? Array.Empty<PresentationStat>();
            StartWeek = startWeek;
            EndWeek = endWeek;
            Completed = completed;
        }

        public string RequestId { get; }
        public CareerPresentationType Type { get; }
        public CareerPresentationGrade Grade { get; }
        public int SeasonYear { get; }
        public string Category { get; }
        public string Title { get; }
        public string PlayerName { get; }
        public string Description { get; }
        public PresentationStat[] Stats { get; }
        public int StartWeek { get; }
        public int EndWeek { get; }
        public Action Completed { get; }
        public bool HasWeekProgress => StartWeek > 0 && EndWeek >= StartWeek;
    }

    /// <summary>대기·재생 완료 ID를 함께 보관해 같은 상태 변경에서 중복 연출이 쌓이지 않게 한다.</summary>
    public sealed class CareerPresentationQueue
    {
        private readonly Queue<CareerPresentationRequest> _requests = new();
        private readonly HashSet<string> _knownRequestIds = new(StringComparer.Ordinal);

        public int Count => _requests.Count;

        public bool Enqueue(CareerPresentationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (!_knownRequestIds.Add(request.RequestId))
                return false;
            _requests.Enqueue(request);
            return true;
        }

        public bool TryDequeue(out CareerPresentationRequest request)
        {
            if (_requests.Count == 0)
            {
                request = null;
                return false;
            }
            request = _requests.Dequeue();
            return true;
        }

        public void Clear()
        {
            _requests.Clear();
            _knownRequestIds.Clear();
        }
    }

    /// <summary>설정 화면이 준비되기 전에도 세 가지 연출 밀도를 동일 계약으로 선택하게 한다.</summary>
    public static class CareerPresentationSettings
    {
        private const string PresentationModeKey = "CareerPresentation.Mode";

        public static CareerPresentationMode Mode
        {
            get => (CareerPresentationMode)Mathf.Clamp(
                PlayerPrefs.GetInt(PresentationModeKey, (int)CareerPresentationMode.Full),
                (int)CareerPresentationMode.Full,
                (int)CareerPresentationMode.ResultOnly);
            set
            {
                PlayerPrefs.SetInt(PresentationModeKey, (int)value);
                PlayerPrefs.Save();
            }
        }
    }
}
