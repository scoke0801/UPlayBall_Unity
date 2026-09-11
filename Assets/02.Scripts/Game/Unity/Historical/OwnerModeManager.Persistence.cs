using System;
using System.IO;
using Baseball.Game.Career.Persistence;
using Baseball.Game.Unity.Persistence;

namespace Baseball.Game.Historical
{
    public sealed partial class OwnerModeManager
    {
        public int ActiveSaveSlot { get; private set; } = 1;
        private int _newGameSaveSlot = 1;

        /// <summary>새 게임은 기존 진행을 덮어쓰지 않는 빈 슬롯을 사용한다.</summary>
        private int FindEmptySaveSlot()
        {
            for (int slot = 1; slot <= SaveSlotPaths.SlotCount; slot++)
                if (!HasSaveInSlot(slot)) return slot;
            throw new InvalidOperationException("빈 구단주 슬롯이 없습니다. 저장·불러오기에서 슬롯을 비운 뒤 시작해 주세요.");
        }

        /// <summary>조회는 현재 진행의 저장 대상을 변경하지 않는다.</summary>
        public bool HasSaveInSlot(int slot) => _saveStore != null && _saveStore.ForSlot(slot).Exists;

        /// <summary>모든 구단주 슬롯의 저장 유무를 확인한다.</summary>
        public bool HasAnySave
        {
            get
            {
                for (int slot = 1; slot <= SaveSlotPaths.SlotCount; slot++)
                    if (HasSaveInSlot(slot)) return true;
                return false;
            }
        }

        /// <summary>실행 중인 상태가 아닌 해당 파일의 저장 시점 정보를 읽는다.</summary>
        public CareerSaveSlotView InspectSave(int slot)
        {
            var store = _saveStore.ForSlot(slot);
            try
            {
                if (!store.Exists)
                    return new CareerSaveSlotView(CareerSaveSlotStatus.Empty, null, "저장된 구단주 진행이 없습니다.", false);
                var data = store.Load();
                if (data.saveVersion < 1 || data.saveVersion > ManagerHistoricalSaveAdapter.CurrentSaveVersion)
                    return new CareerSaveSlotView(CareerSaveSlotStatus.Incompatible, null, "지원하지 않는 저장 버전입니다.", false);
                var season = data.managerMode?.liveSeason;
                if (season == null || string.IsNullOrEmpty(data.playerTeamSeasonKey))
                    throw new InvalidDataException("구단주 진행 정보가 없습니다.");
                var summary = new CareerSaveSummaryData
                {
                    playerName = string.IsNullOrWhiteSpace(data.ownerProfile?.nickname) ? "구단주" : data.ownerProfile.nickname,
                    teamName = data.ownerProfile?.clubName ?? string.Empty,
                    year = season.originYear,
                    seasonPhase = $"{season.seasonNumber}년차 · {season.currentWeekIndex + 1}주차",
                    savedAtUtcTicks = store.SavedAtUtcTicks
                };
                return new CareerSaveSlotView(CareerSaveSlotStatus.Ready, summary, "불러올 수 있는 구단주 진행입니다.", false);
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is IOException || exception is ArgumentException ||
                                               exception is UnauthorizedAccessException || exception is InvalidOperationException)
            {
                return new CareerSaveSlotView(CareerSaveSlotStatus.Damaged, null,
                    "저장 파일을 읽을 수 없습니다. 다른 슬롯을 선택하거나 파일을 확인해 주세요.", false);
            }
        }
    }
}
