using System;

namespace Baseball.Game.Historical
{
    /// <summary>저장된 World History를 복원하며 새 Historical Simulation 경로를 갖지 않는 진입점이다.</summary>
    public sealed class ManagerHistoricalLoadService
    {
        private readonly ManagerHistoricalSaveAdapter _saveAdapter;

        public ManagerHistoricalLoadService(ManagerHistoricalSaveAdapter saveAdapter)
        {
            _saveAdapter = saveAdapter ?? throw new ArgumentNullException(nameof(saveAdapter));
        }

        public ManagerHistoricalRuntimeState Restore(ManagerHistoricalSaveData saveData)
        {
            return _saveAdapter.Restore(saveData);
        }
    }
}
