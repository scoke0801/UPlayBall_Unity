using System;
using System.Diagnostics;

namespace Baseball.Game.Career
{
    /// <summary>시즌 자동 진행의 시간·할당·GC 측정 결과를 런타임 독립 값으로 전달한다.</summary>
    public readonly struct SeasonFastForwardPerformanceReport
    {
        public SeasonFastForwardPerformanceReport(
            SeasonPhase targetPhase,
            SeasonFastForwardStatus status,
            int completedSteps,
            int processedWorldGames,
            long elapsedTicks,
            long maximumStepElapsedTicks,
            long allocatedBytes,
            int generationZeroCollections,
            bool usesExactAllocationCounter)
        {
            TargetPhase = targetPhase;
            Status = status;
            CompletedSteps = completedSteps;
            ProcessedWorldGames = processedWorldGames;
            ElapsedTicks = elapsedTicks;
            MaximumStepElapsedTicks = maximumStepElapsedTicks;
            AllocatedBytes = allocatedBytes;
            GenerationZeroCollections = generationZeroCollections;
            UsesExactAllocationCounter = usesExactAllocationCounter;
        }

        public SeasonPhase TargetPhase { get; }
        public SeasonFastForwardStatus Status { get; }
        public int CompletedSteps { get; }
        public int ProcessedWorldGames { get; }
        public long ElapsedTicks { get; }
        public long MaximumStepElapsedTicks { get; }
        public long AllocatedBytes { get; }
        public int GenerationZeroCollections { get; }
        public bool UsesExactAllocationCounter { get; }
        public double ElapsedMilliseconds => ToMilliseconds(ElapsedTicks);
        public double MaximumStepMilliseconds => ToMilliseconds(MaximumStepElapsedTicks);
        public double MillisecondsPerWorldGame => ProcessedWorldGames <= 0
            ? 0d
            : ElapsedMilliseconds / ProcessedWorldGames;
        public long AllocatedBytesPerWorldGame => ProcessedWorldGames <= 0
            ? 0L
            : AllocatedBytes / ProcessedWorldGames;

        private static double ToMilliseconds(long ticks)
        {
            return ticks * 1000d / Stopwatch.Frequency;
        }
    }
}
