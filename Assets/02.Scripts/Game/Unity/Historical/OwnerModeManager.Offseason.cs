using System;
using System.Collections.Generic;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        private bool CommitGrowthChange(Func<ManagerHistoricalRuntimeState, bool> change)
        {
            EnsureRegularSeasonSimulationIsNotRunning();
            var candidate = _saveAdapter.CreateSimulationCopy(RequireRuntime());
            if (!change(candidate)) return false;
            _saveStore.Save(_saveAdapter.CreateSaveData(candidate));
            Runtime = candidate;
            return true;
        }
        /// <summary>검증된 복사본에서 훈련을 정산하고 저장 성공 이후에만 실제 진행을 교체한다.</summary>
        public bool AdvanceOffseasonWeek(int expectedCompletedWeeks)
        {
            EnsureRegularSeasonSimulationIsNotRunning();
            var runtime = RequireRuntime();
            if (runtime.PlayerGrowth.Offseason.CompletedWeeks != expectedCompletedWeeks)
            {
                NotifyRuntimeChanged();
                return false;
            }
            var candidate = _saveAdapter.CreateSimulationCopy(runtime);
            if (!_coordinator.AdvanceOffseasonWeek(candidate, expectedCompletedWeeks))
            {
                NotifyRuntimeChanged();
                return false;
            }
            var studiesBefore = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in runtime.PlayerGrowth.StudyProjects) studiesBefore.Add(project.CardId);
            _saveStore.Save(_saveAdapter.CreateSaveData(candidate));
            Runtime = candidate;
            InvalidatePregame();
            NotifyRuntimeChanged();
            PublishCompletedStudyFacts(studiesBefore);
            return true;
        }
    }
}
