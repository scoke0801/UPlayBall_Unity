using System;
using System.Collections.Generic;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        /// <summary>최종 확인한 재료를 재검증하고 영입 결과를 공통 런타임에 통지한다.</summary>
        public string RecruitSpecialCard(string transactionId, string targetCardId, IReadOnlyList<string> materialCardIds)
        {
            EnsureRegularSeasonSimulationIsNotRunning();
            var runtime = RequireRuntime();
            runtime.ReserveSpecialRecruit(transactionId, targetCardId, materialCardIds);
            try
            {
                string result = runtime.CommitSpecialRecruit(transactionId);
                NotifyRuntimeChanged();
                return result;
            }
            catch
            {
                runtime.CancelSpecialRecruit(transactionId);
                throw;
            }
        }
    }
}
