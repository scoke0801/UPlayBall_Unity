using System;
using Baseball.Game.Career.Persistence;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Presentation.SharedUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    public sealed partial class UI_Popup_CareerSettings
    {
        private enum PersistenceConfirmationAction
        {
            None,
            Overwrite,
            Load,
            RecoverBackup,
            Delete,
            OwnerOverwrite,
            OwnerLoad
        }

        private OwnerModeManager _ownerModeManager;
        private PersistenceConfirmationAction _persistenceConfirmation;
        private string _persistenceMessage = string.Empty;

        private void RenderPersistenceSettings(RectTransform body)
        {
            if (UiGameModeSession.IsSelected(UiGameMode.OwnerCareer))
            {
                RenderOwnerPersistenceSettings(body);
                return;
            }

            CareerSaveSlotView slot = _careerManager.InspectCareerSave();
            CreateHeading(body, "선수 커리어 슬롯 1", 295f);

            RectTransform card = CreateImage(
                "SaveSlot", body, CardColor, new Vector2(790f, 238f), new Vector2(0f, 145f));
            string status = slot.Status switch
            {
                CareerSaveSlotStatus.Ready => "저장됨",
                CareerSaveSlotStatus.Incompatible => "호환 불가",
                CareerSaveSlotStatus.Damaged => "손상됨",
                _ => "빈 슬롯"
            };
            Color statusColor = slot.Status == CareerSaveSlotStatus.Ready
                ? AccentColor
                : slot.Status is CareerSaveSlotStatus.Incompatible or CareerSaveSlotStatus.Damaged
                    ? DangerColor
                    : MutedTextColor;
            CreateText("Status", card, status, 16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(180f, 30f), new Vector2(-280f, 80f), statusColor);

            if (slot.Summary != null)
            {
                CareerSaveSummaryData summary = slot.Summary;
                CreateText("Player", card,
                    $"{summary.playerName}  ·  {summary.position}  ·  {summary.teamName}",
                    23, FontStyle.Bold, TextAnchor.MiddleLeft,
                    new Vector2(700f, 38f), new Vector2(0f, 32f), PrimaryTextColor);
                CreateText("Season", card,
                    $"{summary.year}년  ·  {summary.leagueName}  ·  {summary.seasonPhase}",
                    16, FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(700f, 32f), new Vector2(0f, -10f), SecondaryTextColor);
                CreateText("SavedAt", card, "저장 시각  " + FormatSavedAt(summary.savedAtUtcTicks),
                    14, FontStyle.Normal, TextAnchor.MiddleLeft,
                    new Vector2(700f, 28f), new Vector2(0f, -55f), MutedTextColor);
            }
            else
            {
                CreateText("Empty", card, slot.Message, 18, FontStyle.Normal, TextAnchor.MiddleCenter,
                    new Vector2(700f, 90f), new Vector2(0f, 0f), SecondaryTextColor);
            }

            bool canSave = _careerManager.CurrentCareer != null &&
                           !_careerManager.HasActiveMatch &&
                           !_careerManager.IsSeasonFastForwardRunning;
            bool hasPrimary = slot.Status != CareerSaveSlotStatus.Empty;
            Button save = CreateButton(
                "SaveCareer", body, hasPrimary ? "현재 진행 덮어쓰기" : "현재 진행 저장",
                new Vector2(240f, 58f), new Vector2(-260f, -20f), SelectedColor, out _);
            save.interactable = canSave;
            save.onClick.AddListener(() =>
            {
                if (hasPrimary)
                    OpenPersistenceConfirmation(PersistenceConfirmationAction.Overwrite);
                else
                    ExecuteSave();
            });

            Button load = CreateButton(
                "LoadCareer", body, "저장 불러오기",
                new Vector2(240f, 58f), new Vector2(0f, -20f), CardColor, out _);
            load.interactable = slot.CanLoad && !_careerManager.IsSeasonFastForwardRunning;
            load.onClick.AddListener(() => OpenPersistenceConfirmation(PersistenceConfirmationAction.Load));

            Button backup = CreateButton(
                "RecoverBackup", body, "자동 백업 복구",
                new Vector2(240f, 58f), new Vector2(260f, -20f), CardColor, out _);
            backup.interactable = slot.HasBackup && !_careerManager.IsSeasonFastForwardRunning;
            backup.onClick.AddListener(() =>
                OpenPersistenceConfirmation(PersistenceConfirmationAction.RecoverBackup));

            Button delete = CreateButton(
                "DeleteSave", body, "저장 데이터 삭제",
                new Vector2(240f, 54f), new Vector2(260f, -95f), DangerColor, out _);
            delete.interactable = hasPrimary || slot.HasBackup;
            delete.onClick.AddListener(() => OpenPersistenceConfirmation(PersistenceConfirmationAction.Delete));

            string availability = canSave
                ? "경기 사이와 계약 오퍼 선택 화면에서 저장할 수 있습니다. 기존 저장은 자동 백업으로 남습니다."
                : _careerManager.CurrentCareer == null
                    ? "타이틀에서는 저장을 불러오거나 삭제할 수 있습니다."
                    : _careerManager.HasActiveMatch
                        ? "경기 준비·진행 중에는 저장할 수 없습니다. 경기를 마친 뒤 저장해 주세요."
                        : "시즌 자동 진행이 끝난 뒤 저장할 수 있습니다.";
            CreateText("Availability", body, availability, 15, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(790f, 48f), new Vector2(0f, -170f), SecondaryTextColor);

            string feedback = string.IsNullOrWhiteSpace(_persistenceMessage)
                ? slot.Status is CareerSaveSlotStatus.Incompatible or CareerSaveSlotStatus.Damaged
                    ? slot.Message
                    : string.Empty
                : _persistenceMessage;
            CreateText("Feedback", body, feedback, 15, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(790f, 54f), new Vector2(0f, -235f),
                feedback.Contains("저장했습니다") ||
                feedback.StartsWith("자동 백업을", StringComparison.Ordinal)
                    ? AccentColor
                    : SecondaryTextColor);
        }

        private void RenderOwnerPersistenceSettings(RectTransform body)
        {
            OwnerModeManager manager = EnsureOwnerModeManager();
            bool hasSave = manager.HasSave;
            CreateHeading(body, "구단주 모드 슬롯 1", 295f);

            RectTransform card = CreateImage(
                "OwnerSaveSlot", body, CardColor, new Vector2(790f, 238f), new Vector2(0f, 145f));
            CreateText("Status", card, hasSave ? "저장됨" : "빈 슬롯", 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(180f, 30f), new Vector2(-280f, 80f),
                hasSave ? AccentColor : MutedTextColor);
            CreateText("SlotTitle", card,
                hasSave ? "구단주 모드 진행 데이터" : "저장된 구단주 진행이 없습니다.",
                23, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(700f, 38f), new Vector2(0f, 30f), PrimaryTextColor);
            CreateText("CurrentProgress", card, GetOwnerCurrentProgressLabel(manager),
                16, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(700f, 48f), new Vector2(0f, -20f), SecondaryTextColor);
            CreateText("SlotGuide", card,
                hasSave
                    ? "홈·구단 운영 화면에서 저장한 데이터와 같은 슬롯입니다."
                    : "현재 진행을 저장하면 이 슬롯에 구단 전체 상태가 기록됩니다.",
                14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(700f, 30f), new Vector2(0f, -65f), MutedTextColor);

            Button save = CreateButton(
                "SaveOwnerCareer", body, hasSave ? "현재 진행 덮어쓰기" : "현재 진행 저장",
                new Vector2(280f, 58f), new Vector2(-155f, -20f), SelectedColor, out _);
            save.interactable = manager.HasActiveRuntime;
            save.onClick.AddListener(() =>
            {
                if (hasSave)
                    OpenPersistenceConfirmation(PersistenceConfirmationAction.OwnerOverwrite);
                else
                    ExecuteOwnerSave();
            });

            Button load = CreateButton(
                "LoadOwnerCareer", body, "저장 불러오기",
                new Vector2(280f, 58f), new Vector2(155f, -20f), CardColor, out _);
            load.interactable = hasSave;
            load.onClick.AddListener(() => OpenPersistenceConfirmation(PersistenceConfirmationAction.OwnerLoad));

            string availability = manager.HasActiveRuntime
                ? "구단주 모드는 별도 단일 슬롯을 사용합니다. 저장하면 구단·리그·로스터·시즌 진행이 함께 기록됩니다."
                : "현재 구단주 진행이 없어 저장할 수 없습니다. 저장된 슬롯은 불러올 수 있습니다.";
            CreateText("Availability", body, availability, 15, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(790f, 54f), new Vector2(0f, -135f), SecondaryTextColor);
            CreateText("BackupGuide", body,
                "구단주 모드 슬롯은 현재 자동 백업과 설정 화면 삭제를 지원하지 않습니다.",
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(790f, 32f), new Vector2(0f, -185f), MutedTextColor);

            CreateText("Feedback", body, _persistenceMessage, 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(790f, 54f), new Vector2(0f, -235f),
                _persistenceMessage.Contains("저장했습니다") ||
                _persistenceMessage.Contains("불러왔습니다")
                    ? AccentColor
                    : SecondaryTextColor);
        }

        private void OpenPersistenceConfirmation(PersistenceConfirmationAction action)
        {
            _persistenceConfirmation = action;
            Render();
        }

        private void RenderPersistenceConfirmation(RectTransform panel)
        {
            RectTransform modal = CreateModal(panel, "PersistenceConfirmation");
            string title = _persistenceConfirmation switch
            {
                PersistenceConfirmationAction.Overwrite => "기존 저장을 덮어쓸까요?",
                PersistenceConfirmationAction.Load => "저장된 커리어를 불러올까요?",
                PersistenceConfirmationAction.RecoverBackup => "자동 백업을 복구할까요?",
                PersistenceConfirmationAction.Delete => "저장 데이터를 삭제할까요?",
                PersistenceConfirmationAction.OwnerOverwrite => "구단주 진행을 덮어쓸까요?",
                PersistenceConfirmationAction.OwnerLoad => "저장된 구단주 진행을 불러올까요?",
                _ => string.Empty
            };
            string message = _persistenceConfirmation switch
            {
                PersistenceConfirmationAction.Overwrite =>
                    "현재 슬롯은 자동 백업으로 이동하고 지금 진행으로 교체됩니다.",
                PersistenceConfirmationAction.Load =>
                    "현재 저장하지 않은 진행은 사라지고 저장 시점으로 돌아갑니다.",
                PersistenceConfirmationAction.RecoverBackup =>
                    "백업의 무결성을 확인한 뒤 수동 슬롯과 현재 커리어를 교체합니다.",
                PersistenceConfirmationAction.Delete =>
                    "수동 저장과 자동 백업을 모두 삭제합니다. 이 작업은 되돌릴 수 없습니다.",
                PersistenceConfirmationAction.OwnerOverwrite =>
                    "기존 구단주 슬롯을 현재 구단·리그·시즌 진행으로 교체합니다.",
                PersistenceConfirmationAction.OwnerLoad =>
                    "현재 저장하지 않은 구단주 진행은 사라지고 저장 시점으로 돌아갑니다.",
                _ => string.Empty
            };
            CreateText("Title", modal, title, 27, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(620f, 48f), new Vector2(0f, 95f), PrimaryTextColor);
            CreateText("Message", modal, message, 17, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(650f, 70f), new Vector2(0f, 25f), SecondaryTextColor);
            Button cancel = CreateButton("Cancel", modal, "취소", new Vector2(250f, 58f),
                new Vector2(-145f, -92f), CardColor, out _);
            cancel.onClick.AddListener(() =>
            {
                _persistenceConfirmation = PersistenceConfirmationAction.None;
                Render();
            });
            Button confirm = CreateButton(
                "Confirm", modal,
                _persistenceConfirmation == PersistenceConfirmationAction.Delete ? "삭제" : "확인",
                new Vector2(280f, 58f), new Vector2(150f, -92f),
                _persistenceConfirmation == PersistenceConfirmationAction.Delete ? DangerColor : SelectedColor,
                out _);
            confirm.onClick.AddListener(ExecutePersistenceConfirmation);
            EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
        }

        private void ExecutePersistenceConfirmation()
        {
            PersistenceConfirmationAction action = _persistenceConfirmation;
            _persistenceConfirmation = PersistenceConfirmationAction.None;
            CareerSaveCommandResult result = action switch
            {
                PersistenceConfirmationAction.Overwrite => _careerManager.SaveCareer(),
                PersistenceConfirmationAction.Load => _careerManager.LoadCareer(),
                PersistenceConfirmationAction.RecoverBackup => _careerManager.RecoverCareerSaveBackup(),
                PersistenceConfirmationAction.Delete => _careerManager.DeleteCareerSave(),
                PersistenceConfirmationAction.OwnerOverwrite => SaveOwnerCareer(),
                PersistenceConfirmationAction.OwnerLoad => LoadOwnerCareer(),
                _ => CareerSaveCommandResult.Failure("실행할 저장 작업이 없습니다.")
            };
            _persistenceMessage = result.Message;
            if (result.IsSuccess && action == PersistenceConfirmationAction.OwnerLoad)
            {
                OpenRestoredOwnerCareer();
                return;
            }
            if (result.IsSuccess &&
                action is PersistenceConfirmationAction.Load or PersistenceConfirmationAction.RecoverBackup)
            {
                OpenRestoredCareer();
                return;
            }
            Render();
        }

        private void ExecuteOwnerSave()
        {
            CareerSaveCommandResult result = SaveOwnerCareer();
            _persistenceMessage = result.Message;
            Render();
        }

        private CareerSaveCommandResult SaveOwnerCareer()
        {
            try
            {
                EnsureOwnerModeManager().Save();
                return CareerSaveCommandResult.Success("구단주 진행 데이터를 저장했습니다.");
            }
            catch (Exception exception) when (IsExpectedOwnerPersistenceException(exception))
            {
                return CareerSaveCommandResult.Failure(exception.Message);
            }
        }

        private CareerSaveCommandResult LoadOwnerCareer()
        {
            try
            {
                EnsureOwnerModeManager().Load();
                return CareerSaveCommandResult.Success("구단주 진행 데이터를 불러왔습니다.");
            }
            catch (Exception exception) when (IsExpectedOwnerPersistenceException(exception))
            {
                return CareerSaveCommandResult.Failure(exception.Message);
            }
        }

        private void OpenRestoredOwnerCareer()
        {
            Time.timeScale = 1f;
            UiGameModeSession.Select(UiGameMode.OwnerCareer);
            Close();
        }

        private OwnerModeManager EnsureOwnerModeManager()
        {
            if (_ownerModeManager == null)
            {
                _ownerModeManager = GameManager.EnsureExists()
                    .EnsureManager<OwnerModeManager>("OwnerModeManager");
            }
            return _ownerModeManager;
        }

        private static string GetOwnerCurrentProgressLabel(OwnerModeManager manager)
        {
            if (!manager.HasActiveRuntime)
                return "현재 실행 중인 구단주 진행이 없습니다.";

            ManagerLiveSeasonState liveSeason = manager.Runtime.ManagerMode?.LiveSeason;
            if (liveSeason == null)
                return "현재 구단주 진행을 저장할 수 있습니다.";

            return $"현재 진행  ·  {liveSeason.OriginYear} 시즌  ·  " +
                   $"{liveSeason.SeasonNumber}년차  ·  {liveSeason.CurrentWeekIndex + 1}주차";
        }

        private static bool IsExpectedOwnerPersistenceException(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is InvalidOperationException ||
                   exception is System.IO.IOException ||
                   exception is UnauthorizedAccessException;
        }

        private void ExecuteSave()
        {
            CareerSaveCommandResult result = _careerManager.SaveCareer();
            _persistenceMessage = result.Message;
            Render();
        }

        private void OpenRestoredCareer()
        {
            Time.timeScale = 1f;
            UI_Scene_CareerMatch match = UnityEngine.Object.FindFirstObjectByType<UI_Scene_CareerMatch>(
                FindObjectsInactive.Include);
            match?.StopAllCoroutines();
            match?.Hide();
            UnityEngine.Object.FindFirstObjectByType<UI_Scene_NewGame>(FindObjectsInactive.Include)?.Hide();
            UiGameModeSession.Select(UiGameMode.PlayerCareer);
            Close();

            CareerResumeDestination destination = _careerManager.ResolveCareerResumeDestination();
            if (destination == CareerResumeDestination.RetirementRecap)
            {
                UI_Popup_RetirementRecap.ShowRuntime(_careerManager.RetirementRecap);
                return;
            }

            CareerMainTab target = destination switch
            {
                CareerResumeDestination.ContractDecision => CareerMainTab.Contract,
                CareerResumeDestination.Offseason => CareerMainTab.Growth,
                _ => CareerMainTab.Home
            };
            CareerTabNavigation.Show(target);
        }

        private static string FormatSavedAt(long utcTicks)
        {
            try
            {
                return new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy.MM.dd HH:mm");
            }
            catch (ArgumentOutOfRangeException)
            {
                return "알 수 없음";
            }
        }
    }
}
