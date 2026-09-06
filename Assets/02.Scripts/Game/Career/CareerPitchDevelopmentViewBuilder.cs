using System;
using System.Collections.Generic;
using Baseball.Core.Balance;
using Baseball.Core.Players;
using Baseball.Simulation.Match;

namespace Baseball.Game.Career
{
    /// <summary>커리어의 영구 투수 성장분을 경기와 동일한 구종 등급 표시값으로 변환한다.</summary>
    public static class CareerPitchDevelopmentViewBuilder
    {
        public static PitchDevelopmentView[] Build(
            PlayerState playerState,
            IReadOnlyList<PitchRepertoireEntry> repertoire,
            PitchArsenalBalance balance)
        {
            if (playerState == null) throw new ArgumentNullException(nameof(playerState));
            if (balance == null) throw new ArgumentNullException(nameof(balance));
            if (repertoire == null || repertoire.Count == 0)
                return Array.Empty<PitchDevelopmentView>();

            Player player = playerState.ToPlayer().WithPitchRepertoire(repertoire);
            var result = new PitchDevelopmentView[repertoire.Count];
            for (int index = 0; index < repertoire.Count; index++)
            {
                PitchRepertoireEntry entry = repertoire[index];
                double stableQuality = PitchEffectivenessResolver.ResolveStableQuality(
                    entry,
                    player.PermanentPitcherAttributes,
                    balance,
                    player.BakedPitcherAttributes,
                    repertoire.Count,
                    PitchEffectivenessResolver.GetPriority(entry, player));
                PitchGradeProgress progress = balance.Grade.GetProgress(stableQuality);
                result[index] = new PitchDevelopmentView(
                    entry.PitchType,
                    balance.Get(entry.PitchType).DisplayName,
                    entry.IsPrimary,
                    stableQuality,
                    progress.CurrentGrade,
                    progress.NextGrade,
                    progress.Progress01,
                    progress.RemainingToNext);
            }
            return result;
        }
    }
}
