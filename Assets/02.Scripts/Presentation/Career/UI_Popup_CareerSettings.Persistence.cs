using System;
using Baseball.Game.Career.Persistence;
using Baseball.Game.Historical;
using Baseball.Game.Manager;
using Baseball.Game.Unity.Persistence;
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
            OwnerLoad,
            OwnerDelete
        }

        private int _selectedPlayerSlot = 1;
        private int _selectedOwnerSlot = 1;
        private OwnerModeManager _ownerModeManager;
        private PersistenceConfirmationAction _persistenceConfirmation;
        private string _persistenceMessage = string.Empty;
        private bool _showOwnerPersistenceAtTitle;
        private bool _hasChosenTitlePersistenceMode;

        /// <summary>타이틀에서 지정 모드의 슬롯 선택을 연다.</summary>
        public static UI_Popup_CareerSettings ShowSaveLoadRuntime(UiGameMode mode)
        {
            var popup = ShowSaveLoadRuntime();
            popup._showOwnerPersistenceAtTitle = mode == UiGameMode.OwnerCareer;
            popup._hasChosenTitlePersistenceMode = true;
            popup.Render();
            return popup;
        }

        private void RenderPersistenceSettings(RectTransform body)
        {
            bool isTitle = !UiGameModeSession.CurrentMode.HasValue;
            if (isTitle && !_hasChosenTitlePersistenceMode)
            {
                CareerSaveSlotView playerSlot = _careerManager.InspectCareerSave(_selectedPlayerSlot);
                bool hasPlayerSave = playerSlot.Status != CareerSaveSlotStatus.Empty || playerSlot.HasBackup;
                _showOwnerPersistenceAtTitle = !hasPlayerSave && EnsureOwnerModeManager().HasAnySave;
                _hasChosenTitlePersistenceMode = true;
            }

            if (isTitle)
                RenderTitlePersistenceModeSelector(body);

            if (UiGameModeSession.IsSelected(UiGameMode.OwnerCareer) ||
                (isTitle && _showOwnerPersistenceAtTitle))
            {
                RenderOwnerPersistenceSettings(body);
                return;
            }

            CareerSaveSlotView slot = _careerManager.InspectCareerSave(_selectedPlayerSlot);
            CreateSaveSlotHeading(body, $"선수 모드 · 슬롯 {_selectedPlayerSlot}");
            RenderSaveSlotSelector(body, false);

            RectTransform card = CreateImage(
                "SaveSlot", body, CardColor, new Vector2(790f, 210f), new Vector2(0f, 115f));
            ApplySettingsPanel(card);
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
                new Vector2(240f, 58f), new Vector2(-260f, -45f), SelectedColor, out _);
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
                new Vector2(240f, 58f), new Vector2(0f, -45f), CardColor, out _);
            load.interactable = slot.CanLoad && !_careerManager.IsSeasonFastForwardRunning;
            load.onClick.AddListener(() => OpenPersistenceConfirmation(PersistenceConfirmationAction.Load));

            Button backup = CreateButton(
                "RecoverBackup", body, "자동 백업 복구",
                new Vector2(240f, 58f), new Vector2(260f, -45f), CardColor, out _);
            backup.interactable = slot.HasBackup && !_careerManager.IsSeasonFastForwardRunning;
            backup.onClick.AddListener(() =>
                OpenPersistenceConfirmation(PersistenceConfirmationAction.RecoverBackup));

            Button delete = CreateButton(
                "DeleteSave", body, "저장 데이터 삭제",
                new Vector2(240f, 54f), new Vector2(260f, -120f), DangerColor, out _);
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
                new Vector2(790f, 48f), new Vector2(0f, -184f), SecondaryTextColor);

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

        private static void CreateSaveSlotHeading(RectTransform body, string title)
        {
            CreateText("SaveSlotHeading", body, title, 17, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(350f, 30f), new Vector2(-220f, 315f), AccentColor);
        }

        private static string GetSaveSlotStatusLabel(CareerSaveSlotStatus status) => status switch
        {
            CareerSaveSlotStatus.Ready => "저장됨",
            CareerSaveSlotStatus.Incompatible => "호환 불가",
            CareerSaveSlotStatus.Damaged => "손상됨",
            _ => "빈 슬롯"
        };

        private void RenderSaveSlotSelector(RectTransform body, bool isOwner)
        {
            int selected = isOwner ? _selectedOwnerSlot : _selectedPlayerSlot;
            for (int index = 1; index <= SaveSlotPaths.SlotCount; index++)
            {
                int slot = index;
                bool occupied = isOwner ? EnsureOwnerModeManager().HasSaveInSlot(slot)
                    : _careerManager.HasCareerSaveInSlot(slot);
                Button button = CreateButton("SaveSlot_" + slot, body,
                    $"{(slot == selected ? "선택 · " : string.Empty)}{slot} · {(occupied ? "저장됨" : "비어 있음")}",
                    new Vector2(148f, 42f), new Vector2((slot - 3) * 160f, 260f),
                    slot == selected ? SelectedColor : CardColor, out _);
                button.onClick.AddListener(() =>
                {
                    if (isOwner) _selectedOwnerSlot = slot;
                    else _selectedPlayerSlot = slot;
                    _persistenceMessage = string.Empty;
                    Render();
                    Transform target = _content.Find("SettingsPanel/Body/SaveSlot_" + slot);
                    if (target != null) EventSystem.current?.SetSelectedGameObject(target.gameObject);
                });
            }
        }

        private void RenderTitlePersistenceModeSelector(RectTransform body)
        {
            Button player = CreateButton(
                "PlayerSaveSlotTab", body, "선수 모드",
                new Vector2(180f, 38f), new Vector2(155f, 332f),
                _showOwnerPersistenceAtTitle ? CardColor : SelectedColor, out _);
            player.onClick.AddListener(() =>
            {
                _showOwnerPersistenceAtTitle = false;
                _hasChosenTitlePersistenceMode = true;
                _persistenceMessage = string.Empty;
                Render();
            });

            Button owner = CreateButton(
                "OwnerSaveSlotTab", body, "구단주 모드",
                new Vector2(180f, 38f), new Vector2(350f, 332f),
                _showOwnerPersistenceAtTitle ? SelectedColor : CardColor, out _);
            owner.onClick.AddListener(() =>
            {
                _showOwnerPersistenceAtTitle = true;
                _hasChosenTitlePersistenceMode = true;
                _persistenceMessage = string.Empty;
                Render();
            });
        }

        private void RenderOwnerPersistenceSettings(RectTransform body)
        {
            OwnerModeManager manager = EnsureOwnerModeManager();
            bool hasSave = manager.HasSaveInSlot(_selectedOwnerSlot);
            CareerSaveSlotView slot = manager.InspectSave(_selectedOwnerSlot);
            CreateSaveSlotHeading(body, $"구단주 모드 · 슬롯 {_selectedOwnerSlot}");
            RenderSaveSlotSelector(body, true);

            RectTransform card = CreateImage(
                "OwnerSaveSlot", body, CardColor, new Vector2(790f, 210f), new Vector2(0f, 115f));
            ApplySettingsPanel(card);
            CreateText("Status", card, GetSaveSlotStatusLabel(slot.Status), 16, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(180f, 30f), new Vector2(-280f, 80f),
                hasSave ? AccentColor : MutedTextColor);
            CreateText("SlotTitle", card,
                slot.Summary != null ? (string.IsNullOrWhiteSpace(slot.Summary.teamName) ? "구단주 진행" : slot.Summary.teamName)
                    + " · " + slot.Summary.playerName
                    : hasSave ? "저장 데이터를 확인해 주세요." : "비어 있는 구단주 슬롯",
                23, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(700f, 38f), new Vector2(0f, 30f), PrimaryTextColor);
            CreateText("CurrentProgress", card, slot.Summary != null
                    ? $"{slot.Summary.year} 시즌 · {slot.Summary.seasonPhase}" : slot.Message,
                16, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(700f, 40f), new Vector2(0f, -10f), SecondaryTextColor);
            CreateText("SlotGuide", card,
                slot.Summary != null ? "저장 시각  " + FormatSavedAt(slot.Summary.savedAtUtcTicks)
                    : "현재 진행을 저장하면 이 슬롯에 구단 전체 상태가 기록됩니다.",
                14, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Vector2(700f, 30f), new Vector2(0f, -65f), MutedTextColor);

            Button save = CreateButton(
                "SaveOwnerCareer", body, hasSave ? "현재 진행 덮어쓰기" : "현재 진행 저장",
                new Vector2(280f, 58f), new Vector2(-155f, -45f), SelectedColor, out _);
            bool isBusy = manager.IsRegularSeasonSimulationRunning || manager.IsPostseasonSimulationRunning;
            save.interactable = manager.HasActiveRuntime && !isBusy;
            save.onClick.AddListener(() =>
            {
                if (hasSave)
                    OpenPersistenceConfirmation(PersistenceConfirmationAction.OwnerOverwrite);
                else
                    ExecuteOwnerSave();
            });

            Button load = CreateButton(
                "LoadOwnerCareer", body, "저장 불러오기",
                new Vector2(280f, 58f), new Vector2(155f, -45f), CardColor, out _);
            load.interactable = slot.CanLoad && !isBusy;
            load.onClick.AddListener(() => OpenPersistenceConfirmation(PersistenceConfirmationAction.OwnerLoad));

            if (!UiGameModeSession.CurrentMode.HasValue && !hasSave)
            {
                Button start = CreateButton("NewOwnerCareer", body, "이 슬롯에서 새 구단 시작",
                    new Vector2(280f, 54f), new Vector2(-155f, -120f), CardColor, out _);
                start.onClick.AddListener(() =>
                {
                    try
                    {
                        manager.BeginNewGameFlow(_selectedOwnerSlot);
                        Close();
                        UnityEngine.Object.FindFirstObjectByType<UI_Scene_NewGame>(
                            FindObjectsInactive.Include)?.RefreshTitleSaveState();
                    }
                    catch (Exception exception) when (IsExpectedOwnerPersistenceException(exception))
                    {
                        Debug.LogException(exception);
                        _persistenceMessage = "새 구단을 준비하지 못했습니다. 잠시 후 다시 시도해 주세요.";
                        Render();
                    }
                });
            }

            Button delete = CreateButton(
                "DeleteOwnerSave", body, "저장 데이터 삭제",
                new Vector2(280f, 54f), new Vector2(155f, -120f), DangerColor, out _);
            delete.interactable = hasSave;
            delete.onClick.AddListener(() =>
                OpenPersistenceConfirmation(PersistenceConfirmationAction.OwnerDelete));

            string availability = isBusy ? "시즌 자동 진행이 끝난 뒤 저장하거나 불러올 수 있습니다."
                : manager.HasActiveRuntime
                ? $"현재 진행은 슬롯 {manager.ActiveSaveSlot}에 저장됩니다. 별도로 남기려면 다른 슬롯에 저장하세요."
                : "이어서 할 슬롯을 선택한 뒤 저장 불러오기를 누르세요.";
            CreateText("Availability", body, availability, 15, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(790f, 44f), new Vector2(0f, -182f), SecondaryTextColor);
            CreateText("BackupGuide", body,
                "진행 상황은 직접 저장해 주세요. 덮어쓰기 전 기록은 다른 슬롯에 남겨 둘 수 있습니다.",
                14, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(790f, 40f), new Vector2(0f, -239f), MutedTextColor);

            string feedback = !string.IsNullOrEmpty(_persistenceMessage) ? _persistenceMessage
                : slot.Status is CareerSaveSlotStatus.Incompatible or CareerSaveSlotStatus.Damaged
                    ? "이 저장은 불러올 수 없습니다. 다른 슬롯을 선택해 주세요." : string.Empty;
            CreateText("Feedback", body, feedback, 15, FontStyle.Bold,
                TextAnchor.MiddleCenter, new Vector2(790f, 54f), new Vector2(0f, -307f),
                _persistenceMessage.Contains("저장했습니다") ||
                _persistenceMessage.Contains("불러왔습니다") ||
                _persistenceMessage.Contains("삭제했습니다")
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
            foreach (Button button in panel.GetComponentsInChildren<Button>())
                if (!button.transform.IsChildOf(modal)) button.interactable = false;
            string title = _persistenceConfirmation switch
            {
                PersistenceConfirmationAction.Overwrite => "기존 저장을 덮어쓸까요?",
                PersistenceConfirmationAction.Load => "저장된 커리어를 불러올까요?",
                PersistenceConfirmationAction.RecoverBackup => "자동 백업을 복구할까요?",
                PersistenceConfirmationAction.Delete => "저장 데이터를 삭제할까요?",
                PersistenceConfirmationAction.OwnerOverwrite => "구단주 진행을 덮어쓸까요?",
                PersistenceConfirmationAction.OwnerLoad => "저장된 구단주 진행을 불러올까요?",
                PersistenceConfirmationAction.OwnerDelete => "구단주 저장 데이터를 삭제할까요?",
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
                PersistenceConfirmationAction.OwnerDelete =>
                    "구단·선수단·재정·시즌 진행을 포함한 선택 슬롯을 삭제합니다. 이 작업은 되돌릴 수 없습니다.",
                _ => string.Empty
            };
            CreateText("Title", modal, title, 27, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Vector2(620f, 48f), new Vector2(0f, 95f), PrimaryTextColor);
            bool isOwnerAction = _persistenceConfirmation is PersistenceConfirmationAction.OwnerOverwrite or
                PersistenceConfirmationAction.OwnerLoad or PersistenceConfirmationAction.OwnerDelete;
            message = $"{(isOwnerAction ? "구단주 모드" : "선수 모드")} · 슬롯 {(isOwnerAction ? _selectedOwnerSlot : _selectedPlayerSlot)}\n" + message;
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
                _persistenceConfirmation is PersistenceConfirmationAction.Delete or
                    PersistenceConfirmationAction.OwnerDelete ? "삭제" : "확인",
                new Vector2(280f, 58f), new Vector2(150f, -92f),
                _persistenceConfirmation is PersistenceConfirmationAction.Delete or
                    PersistenceConfirmationAction.OwnerDelete ? DangerColor : SelectedColor,
                out _);
            confirm.onClick.AddListener(ExecutePersistenceConfirmation);
            cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = confirm, selectOnRight = confirm, selectOnUp = confirm, selectOnDown = confirm };
            confirm.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = cancel, selectOnRight = cancel, selectOnUp = cancel, selectOnDown = cancel };
            EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
        }

        private void ExecutePersistenceConfirmation()
        {
            PersistenceConfirmationAction action = _persistenceConfirmation;
            _persistenceConfirmation = PersistenceConfirmationAction.None;
            CareerSaveCommandResult result = action switch
            {
                PersistenceConfirmationAction.Overwrite => _careerManager.SaveCareer(_selectedPlayerSlot),
                PersistenceConfirmationAction.Load => _careerManager.LoadCareer(_selectedPlayerSlot),
                PersistenceConfirmationAction.RecoverBackup => _careerManager.RecoverCareerSaveBackup(_selectedPlayerSlot),
                PersistenceConfirmationAction.Delete => _careerManager.DeleteCareerSave(_selectedPlayerSlot),
                PersistenceConfirmationAction.OwnerOverwrite => SaveOwnerCareer(),
                PersistenceConfirmationAction.OwnerLoad => LoadOwnerCareer(),
                PersistenceConfirmationAction.OwnerDelete => DeleteOwnerCareer(),
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
                EnsureOwnerModeManager().Save(_selectedOwnerSlot);
                return CareerSaveCommandResult.Success("구단주 진행 데이터를 저장했습니다.");
            }
            catch (Exception exception) when (IsExpectedOwnerPersistenceException(exception))
            {
                Debug.LogException(exception);
                return CareerSaveCommandResult.Failure("진행 상황을 저장하지 못했습니다. 저장 공간을 확인하고 다시 시도해 주세요.");
            }
        }

        private CareerSaveCommandResult LoadOwnerCareer()
        {
            try
            {
                EnsureOwnerModeManager().Load(_selectedOwnerSlot);
                return CareerSaveCommandResult.Success("구단주 진행 데이터를 불러왔습니다.");
            }
            catch (Exception exception) when (IsExpectedOwnerPersistenceException(exception))
            {
                Debug.LogException(exception);
                return CareerSaveCommandResult.Failure(
                    exception is System.IO.IOException || exception is UnauthorizedAccessException
                        ? "저장 파일을 읽지 못했습니다. 잠시 후 다시 시도해 주세요."
                        : "저장 내용을 불러오지 못했습니다. 다른 슬롯을 선택해 주세요.");
            }
        }

        private CareerSaveCommandResult DeleteOwnerCareer()
        {
            try
            {
                OwnerModeManager manager = EnsureOwnerModeManager();
                if (!UiGameModeSession.CurrentMode.HasValue)
                    manager.DeleteSaveAndDiscardRuntime(_selectedOwnerSlot);
                else
                    manager.DeleteSave(_selectedOwnerSlot);
                UnityEngine.Object.FindFirstObjectByType<UI_Scene_NewGame>(
                    FindObjectsInactive.Include)?.RefreshTitleSaveState();
                return CareerSaveCommandResult.Success("구단주 모드 저장 데이터를 삭제했습니다.");
            }
            catch (Exception exception) when (IsExpectedOwnerPersistenceException(exception))
            {
                Debug.LogException(exception);
                return CareerSaveCommandResult.Failure(
                    "저장 데이터를 삭제하지 못했습니다. 잠시 후 다시 시도해 주세요.");
            }
        }

        private void OpenRestoredOwnerCareer()
        {
            Time.timeScale = 1f;
            UnityEngine.Object.FindFirstObjectByType<UI_Scene_NewGame>(FindObjectsInactive.Include)?.Hide();
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

        private static bool IsExpectedOwnerPersistenceException(Exception exception)
        {
            return exception is ArgumentException ||
                   exception is InvalidOperationException ||
                   exception is System.IO.IOException ||
                   exception is System.IO.InvalidDataException ||
                   exception is UnauthorizedAccessException;
        }

        private void ExecuteSave()
        {
            CareerSaveCommandResult result = _careerManager.SaveCareer(_selectedPlayerSlot);
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
