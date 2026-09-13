using System;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        private bool CommitSkillChange(Func<ManagerHistoricalRuntimeState, bool> change)
        {
            EnsureRegularSeasonSimulationIsNotRunning();
            var runtime = RequireRuntime();
            return OwnerSkillTransaction.Execute(runtime, () => change(runtime),
                () => _saveStore.Save(_saveAdapter.CreateSaveData(runtime)));
        }

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
        public bool CompleteOffseasonStudies(int expectedCompletedWeeks)
        {
            EnsureRegularSeasonSimulationIsNotRunning();
            var runtime = RequireRuntime();
            if (runtime.PlayerGrowth.Offseason.CompletedWeeks != expectedCompletedWeeks)
            {
                NotifyRuntimeChanged();
                return false;
            }
            var candidate = _saveAdapter.CreateSimulationCopy(runtime);
            if (!_coordinator.CompleteOffseasonStudies(candidate, expectedCompletedWeeks))
            {
                NotifyRuntimeChanged();
                return false;
            }
            _saveStore.Save(_saveAdapter.CreateSaveData(candidate));
            Runtime = candidate;
            InvalidatePregame();
            NotifyRuntimeChanged();
            return true;
        }
    }
}
