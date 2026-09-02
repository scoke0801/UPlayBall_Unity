using System;
using Baseball.Core.Historical;
using Baseball.Simulation.Historical;

namespace Baseball.Game.Historical
{
    /// <summary>감독모드 세이브 복원 시 저장된 WorldHistorySnapshot을 재사용하는 단일 진입점이다.</summary>
    public sealed class ManagerHistoricalLoadService
    {
        private readonly ManagerHistoricalSaveAdapter _saveAdapter;
        private readonly WorldHistoryInitializer _historyInitializer;

        public ManagerHistoricalLoadService(
            ManagerHistoricalSaveAdapter saveAdapter,
            WorldHistoryInitializer historyInitializer)
        {
            _saveAdapter = saveAdapter ?? throw new ArgumentNullException(nameof(saveAdapter));
            _historyInitializer = historyInitializer ?? throw new ArgumentNullException(nameof(historyInitializer));
        }

        public ManagerHistoricalRuntimeState Restore(ManagerHistoricalSaveData saveData)
        {
            ManagerHistoricalRuntimeState state = _saveAdapter.Restore(saveData);
            WorldHistorySnapshot resolved = _historyInitializer.Initialize(
                new WorldHistoryInitializationRequest(
                    state.WorldHistory.RecordMode,
                    state.WorldHistory.WorldHistorySeed,
                    existingSnapshot: state.WorldHistory));
            if (!ReferenceEquals(resolved, state.WorldHistory))
                throw new InvalidOperationException("저장된 WorldHistorySnapshot 대신 새 기록이 생성되었습니다.");
            return state;
        }
    }
}
