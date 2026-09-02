using System.Globalization;
using System.Text;
using Baseball.Core.Rules;
using UnityEngine;

namespace Baseball.Game.Career
{
    /// <summary>Unity Player 성능 결과와 재현 환경을 함께 복사할 수 있는 텍스트로 만든다.</summary>
    public static class SeasonFastForwardRuntimeReport
    {
        public static string Create(
            SeasonFastForwardPerformanceReport performance,
            SimulationVersionStamp versionStamp)
        {
            var text = new StringBuilder(640);
            Append(text, "UnityVersion", Application.unityVersion);
            Append(text, "BuildGuid", Application.buildGUID);
            Append(text, "BuildConfiguration", Debug.isDebugBuild ? "Development" : "Release");
            Append(text, "ScriptingBackend", GetScriptingBackend());
            Append(text, "OperatingSystem", SystemInfo.operatingSystem);
            Append(text, "Processor", SystemInfo.processorType);
            Append(text, "ProcessorCount", SystemInfo.processorCount);
            Append(text, "SystemMemoryMb", SystemInfo.systemMemorySize);
            Append(text, "EngineKind", "Detailed");
            Append(text, "OutputProfile", "BackgroundSummary");
            Append(text, "ExecutionMode", SeasonFastForwardExecutionMode.CooperativeMainThread);
            Append(text, "BalanceVersion", versionStamp.BalanceVersion);
            Append(text, "EngineVersion", versionStamp.EngineVersion);
            Append(text, "RulesVersion", versionStamp.RulesVersion);
            Append(text, "RngVersion", versionStamp.RngAlgorithmVersion);
            Append(text, "ContentHash", versionStamp.ContentHash);
            Append(text, "TargetPhase", performance.TargetPhase);
            Append(text, "Status", performance.Status);
            Append(text, "CompletedSteps", performance.CompletedSteps);
            Append(text, "ProcessedWorldGames", performance.ProcessedWorldGames);
            Append(text, "ElapsedMs", performance.ElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            Append(text, "MaximumStepMs", performance.MaximumStepMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            Append(text, "MillisecondsPerWorldGame", performance.MillisecondsPerWorldGame.ToString("0.######", CultureInfo.InvariantCulture));
            Append(text, "AllocatedBytes", performance.AllocatedBytes);
            Append(text, "AllocatedBytesPerWorldGame", performance.AllocatedBytesPerWorldGame);
            Append(text, "AllocationCounter", performance.UsesExactAllocationCounter
                ? "ThreadAllocatedBytes"
                : "ManagedHeapGrowthFallback");
            Append(text, "Gen0Collections", performance.GenerationZeroCollections);
            return text.ToString();
        }

        private static void Append(StringBuilder text, string key, object value)
        {
            text.Append(key).Append('=').Append(value).AppendLine();
        }

        private static string GetScriptingBackend()
        {
#if ENABLE_IL2CPP
            return "IL2CPP";
#elif ENABLE_MONO
            return "Mono";
#else
            return "Unknown";
#endif
        }
    }
}
