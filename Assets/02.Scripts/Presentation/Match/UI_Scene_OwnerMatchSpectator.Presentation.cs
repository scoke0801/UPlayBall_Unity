using System;
using Baseball.Core.Players;
using Baseball.Core.Rules;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Presentation.SharedScreens;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    public sealed partial class UI_Scene_OwnerMatchSpectator
    {
        private static readonly string[] BattingRecordHeaders =
        {
            "선수", "포지션", "타수", "득점", "1루타", "2루타", "3루타",
            "홈런", "타점", "볼넷", "도루", "병살", "주루사"
        };

        private static readonly string[] PitchingRecordHeaders =
        {
            "선수", "보직", "이닝", "피안타", "실점", "볼넷", "탈삼진", "승리", "패전", "홀드", "세이브"
        };

        private void RenderHud(MatchHudPresentationModel model)
        {
            if (_awayLabel == null) return;
            _awayLabel.text = model.AwayTeam.Name + "   " + model.AwayTeam.Score;
            _homeLabel.text = model.HomeTeam.Score + "   " + model.HomeTeam.Name;
            _inningLabel.text = model.Inning + "회 " + (model.Half == MatchHudHalf.Top ? "초" : "말");
            SetLamps(_balls, model.Count.Balls, new Color32(76, 177, 57, 255));
            SetLamps(_strikes, model.Count.Strikes, new Color32(224, 169, 17, 255));
            SetLamps(_outs, model.Count.Outs, new Color32(207, 47, 48, 255));
            SetBase(_bases[0], model.Bases.HasRunnerOnFirst);
            SetBase(_bases[1], model.Bases.HasRunnerOnSecond);
            SetBase(_bases[2], model.Bases.HasRunnerOnThird);
            _pitcherLabel.text = ParticipantName(model.Pitcher, "투수 대기");
            _batterLabel.text = ParticipantName(model.Batter, "타자 대기");
            _pitcherDetail.text = model.IsBetweenInnings ? "다음 수비 준비" : "현재 투구";
            _batterDetail.text = model.IsBetweenInnings ? "다음 공격 준비" : "현재 타석";
            _playVisualizer?.PresentBases(model);
        }

        private void RefreshControls()
        {
            if (_session == null) return;
            OwnerMatchOverlayState state = _session.State;
            bool isComplete = state.IsComplete;
            if (isComplete) ClearHighlightInset();
            bool isNewBoundary = _lastVisibleCount != state.VisibleEventCount;
            _pauseButton.interactable = state.CanTogglePause;
            _pauseLabel.text = state.IsPaused ? "계속 보기" : "일시정지";
            _advanceButton.interactable = state.CanAdvance;
            _revealAllButton.interactable = state.CanAdvance;
            for (int i = 0; i < _speedButtons.Length; i++)
            {
                OwnerMatchPlaybackSpeed speed = i == 0
                    ? OwnerMatchPlaybackSpeed.Normal
                    : i == 1 ? OwnerMatchPlaybackSpeed.Fast : OwnerMatchPlaybackSpeed.FourTimes;
                _speedButtons[i].interactable = state.CanChangeSpeed;
                _speedButtons[i].targetGraphic.color = state.Speed == speed ? Blue : Silver;
                _speedButtons[i].GetComponentInChildren<Text>().color = state.Speed == speed ? Color.white : Ink;
            }
            for (int i = 0; i < _viewingModeButtons.Length; i++)
            {
                OwnerMatchViewingMode mode = (OwnerMatchViewingMode)i;
                Button button = _viewingModeButtons[i];
                button.interactable = state.CanChangeViewingMode;
                button.targetGraphic.color = state.ViewingMode == mode ? Blue : new Color(0.08f, 0.12f, 0.15f, 0.88f);
                button.GetComponentInChildren<Text>().color = Color.white;
            }

            _statusLabel.text = isComplete
                ? "경기 종료"
                : state.IsPaused
                    ? "중계 일시정지"
                    : state.ViewingMode == OwnerMatchViewingMode.KeyMoments
                    ? "승부처 하이라이트"
                    : "생중계";
            SetCompletionControlVisibility(isComplete);
            _resultToggleLabel.text = _showResults ? "경기 화면" : "경기 결과";
            _resultPanel.gameObject.SetActive(isComplete && _showResults);

            if (isNewBoundary)
            {
                UpdateEventPresentation();
                RenderGameCastHistory();
                _lastVisibleCount = state.VisibleEventCount;
            }

            bool showLineScore = !isComplete && CurrentModel != null && CurrentModel.IsBetweenInnings;
            _scorePanel.gameObject.SetActive(showLineScore);
            if (showLineScore)
            {
                _scoreCaption.text = CurrentModel.Inning + "회 " +
                    (CurrentModel.Half == MatchHudHalf.Top ? "초" : "말") + " 종료";
                BuildLineScore(_scoreRows, _session.CreateVisibleLineScore(),
                    CurrentModel.AwayTeam.Name, CurrentModel.HomeTeam.Name, false);
            }

            if (isComplete && !_wasComplete)
            {
                _showResults = true;
                _resultToggleLabel.text = "경기 화면";
                _resultPanel.gameObject.SetActive(true);
                BuildFinalResult();
                _wasComplete = true;
                PresentationCompleted?.Invoke();
            }
        }

        private void SetCompletionControlVisibility(bool isComplete)
        {
            // 구단주는 감독 AI가 확정한 경기를 관전하므로 관전 범위·배속·즉시 결과 외 진행 조작을 제공하지 않는다.
            _pauseButton.gameObject.SetActive(false);
            _revealAllButton.gameObject.SetActive(!isComplete);
            _advanceButton.gameObject.SetActive(false);
            _homeButton.gameObject.SetActive(isComplete);
            _resultButton.gameObject.SetActive(isComplete);
        }

        private void UpdateEventPresentation()
        {
            int visibleCount = _session.State.VisibleEventCount;
            if (visibleCount == 0)
            {
                _commentary.text = "양 팀 선수들이 그라운드에 들어섭니다. 곧 경기가 시작됩니다.";
                _announcement.text = "경기 시작";
                return;
            }

            MatchEvent matchEvent = _session.GetVisibleEvent(visibleCount - 1);
            RenderPlayDetail(matchEvent);
            // Out은 아웃 수 갱신 사건이다. 타자 결과는 타석 종료에서 한 번만 중계한다.
            if (matchEvent.EventType == MatchEventType.Out) return;
            bool repeatedPlateAppearanceResult =
                _resultPresentationState.IsRepeatedPlateAppearanceResult(matchEvent);
            string result = repeatedPlateAppearanceResult && _resultPresentationState.WasDoublePlay
                ? "병살"
                : FormatEventResult(matchEvent);
            int runsScored = CountRunsSincePreviousBoundary(visibleCount);
            if (result != "경기 진행")
                _commentary.text = FormatCommentary(matchEvent, result, runsScored);
            if (IsEmphasized(matchEvent) && !repeatedPlateAppearanceResult) _announcement.text = result;
            else if (matchEvent.EventType == MatchEventType.Contact)
                _announcement.text = string.Empty;
            _resultPresentationState.Observe(matchEvent);
        }

        private void BuildFinalResult()
        {
            MatchResult match = _session.Result.Match;
            string away = CurrentModel?.AwayTeam.Name ?? "원정";
            string home = CurrentModel?.HomeTeam.Name ?? "홈";
            _resultHeading.text = match.IsTie ? "경기 종료 · 무승부" :
                    (match.WinnerTeamId == match.AwayBoxScore.TeamId ? "원정팀 승리" : "홈팀 승리");
            _resultSummary.text = away + "  " + match.AwayBoxScore.Runs + "  :  " +
                                  match.HomeBoxScore.Runs + "  " + home;
            int inningCount = Math.Min(match.InningsPlayed,
                Math.Min(match.AwayBoxScore.RunsByInning.Count, match.HomeBoxScore.RunsByInning.Count));
            var awayRuns = new int[inningCount];
            var homeRuns = new int[inningCount];
            for (int i = 0; i < inningCount; i++)
            {
                awayRuns[i] = match.AwayBoxScore.RunsByInning[i];
                homeRuns[i] = match.HomeBoxScore.RunsByInning[i];
            }
            BuildLineScore(_resultScoreRows, awayRuns,
                homeRuns, away, home, match.AwayBoxScore.Hits,
                match.HomeBoxScore.Hits, match.AwayBoxScore.Errors, match.HomeBoxScore.Errors);
            RenderRecords();
        }

        private void BuildLineScore(RectTransform host, MatchLineScore score, string away, string home, bool final)
        {
            int innings = score.InningCount;
            var awayRuns = new int[innings];
            var homeRuns = new int[innings];
            for (int i = 0; i < innings; i++)
            {
                awayRuns[i] = score.GetAwayRuns(i + 1);
                homeRuns[i] = score.GetHomeRuns(i + 1);
            }
            BuildLineScore(host, awayRuns, homeRuns, away, home, score.AwayTotal, score.HomeTotal, -1, -1);
        }

        private void BuildLineScore(RectTransform host, System.Collections.Generic.IReadOnlyList<int> awayRuns,
            System.Collections.Generic.IReadOnlyList<int> homeRuns, string away, string home,
            int awayHits, int homeHits, int awayErrors, int homeErrors)
        {
            ClearChildren(host);
            int innings = Math.Max(awayRuns.Count, homeRuns.Count);
            float teamWidth = 205f;
            float statWidth = awayErrors >= 0 ? 58f : 72f;
            float inningWidth = Mathf.Min(72f, (host.rect.width - teamWidth - statWidth * (awayErrors >= 0 ? 3 : 1)) / Math.Max(9, innings));
            AddScoreCell(host, "", 0, 0, teamWidth, 38, Silver, Ink, TextAnchor.MiddleLeft);
            for (int i = 0; i < innings; i++)
                AddScoreCell(host, (i + 1).ToString(), teamWidth + i * inningWidth, 0, inningWidth, 38, Silver, Ink, TextAnchor.MiddleCenter);
            float totalsX = teamWidth + innings * inningWidth;
            AddScoreCell(host, "득점", totalsX, 0, statWidth, 38, Ink, Color.white, TextAnchor.MiddleCenter);
            if (awayErrors >= 0)
            {
                AddScoreCell(host, "안타", totalsX + statWidth, 0, statWidth, 38, Ink, Color.white, TextAnchor.MiddleCenter);
                AddScoreCell(host, "실책", totalsX + statWidth * 2, 0, statWidth, 38, Ink, Color.white, TextAnchor.MiddleCenter);
            }
            AddScoreRow(host, away, awayRuns, 38, innings, inningWidth, teamWidth, statWidth, awayHits, awayErrors);
            AddScoreRow(host, home, homeRuns, 81, innings, inningWidth, teamWidth, statWidth, homeHits, homeErrors);
        }

        private static void AddScoreRow(RectTransform host, string team,
            System.Collections.Generic.IReadOnlyList<int> runs, float y, int innings, float inningWidth,
            float teamWidth, float statWidth, int hits, int errors)
        {
            AddScoreCell(host, team, 0, y, teamWidth, 42, Paper, Ink, TextAnchor.MiddleLeft);
            int total = 0;
            for (int i = 0; i < innings; i++)
            {
                int run = i < runs.Count ? runs[i] : MatchLineScore.NotPlayed;
                if (run > 0) total += run;
                AddScoreCell(host, run < 0 ? "-" : run.ToString(), teamWidth + i * inningWidth, y,
                    inningWidth, 42, run > 0 ? new Color32(226, 243, 252, 255) : Paper, Ink, TextAnchor.MiddleCenter);
            }
            float totalsX = teamWidth + innings * inningWidth;
            AddScoreCell(host, total.ToString(), totalsX, y, statWidth, 42, Blue, Color.white, TextAnchor.MiddleCenter);
            if (errors >= 0)
            {
                AddScoreCell(host, hits.ToString(), totalsX + statWidth, y, statWidth, 42, Paper, Ink, TextAnchor.MiddleCenter);
                AddScoreCell(host, errors.ToString(), totalsX + statWidth * 2, y, statWidth, 42, Paper, Ink, TextAnchor.MiddleCenter);
            }
        }

        private void RenderRecords()
        {
            if (_session == null || !_session.State.IsComplete) return;
            ClearChildren(_recordContent);
            MatchResult match = _session.Result.Match;
            TeamBoxScore box = _showHomeRecords ? match.HomeBoxScore : match.AwayBoxScore;
            MatchRosterSnapshot roster = _showHomeRecords ? match.Input.HomeRoster : match.Input.AwayRoster;
            string team = _showHomeRecords ? CurrentModel.HomeTeam.Name : CurrentModel.AwayTeam.Name;
            _recordHeader.text = team + " · " + (_showPitching ? "투구 기록" : "타격 기록");
            float y = 0;
            if (_showPitching)
            {
                AddRecordRow(_recordContent, PitchingRecordHeaders, -1, true);
                for (int i = 0; i < box.PitchingLines.Count; i++)
                {
                    PlayerPitchingLine line = box.PitchingLines[i];
                    if (line.BattersFaced == 0) continue;
                    AddRecordRow(_recordContent,
                        new[]
                        {
                            _session.GetParticipantName(line.PlayerId),
                            FormatPitcherRole(FindPitcherRole(roster, line.PlayerId)),
                            FormatInnings(line.OutsRecorded),
                            line.HitsAllowed.ToString(),
                            line.RunsAllowed.ToString(),
                            line.WalksAllowed.ToString(),
                            line.Strikeouts.ToString(),
                            FormatDecision(line.HasWin),
                            FormatDecision(line.HasLoss),
                            FormatDecision(line.HasHold),
                            FormatDecision(line.HasSave)
                        },
                        y += 35,
                        false);
                }
            }
            else
            {
                AddRecordRow(_recordContent, BattingRecordHeaders, -1, true);
                for (int i = 0; i < box.BattingLines.Count; i++)
                {
                    PlayerBattingLine line = box.BattingLines[i];
                    if (line.PlateAppearances == 0) continue;
                    int singles = line.Hits - line.Doubles - line.Triples - line.HomeRuns;
                    AddRecordRow(_recordContent,
                        new[]
                        {
                            _session.GetParticipantName(line.PlayerId),
                            CareerSharedSnapshotFormatters.FormatPositionName(
                                FindBattingPosition(roster, line.PlayerId)),
                            line.AtBats.ToString(),
                            line.Runs.ToString(),
                            singles.ToString(),
                            line.Doubles.ToString(),
                            line.Triples.ToString(),
                            line.HomeRuns.ToString(),
                            line.RunsBattedIn.ToString(),
                            line.Walks.ToString(),
                            line.StolenBases.ToString(),
                            line.GroundedIntoDoublePlays.ToString(),
                            line.BaserunningOuts.ToString()
                        },
                        y += 35,
                        false);
                }
            }
            float viewportHeight = _recordScroll.viewport.rect.height;
            _recordContent.sizeDelta = new Vector2(1334, Mathf.Max(viewportHeight, y + 42));
            _recordScrollbar.gameObject.SetActive(_recordContent.sizeDelta.y > viewportHeight);
            _recordScroll.verticalNormalizedPosition = 1;
        }

        private static PlayerPosition FindBattingPosition(MatchRosterSnapshot roster, int playerId)
        {
            for (int index = 0; index < roster.StartingLineup.Count; index++)
            {
                LineupSlot slot = roster.StartingLineup[index];
                if (slot.Player.PlayerId == playerId)
                    return slot.FieldingPosition;
            }
            for (int index = 0; index < roster.Bench.Count; index++)
            {
                if (roster.Bench[index].PlayerId == playerId)
                    return roster.Bench[index].PrimaryPosition;
            }
            return PlayerPosition.Unknown;
        }

        private static PitcherRole FindPitcherRole(MatchRosterSnapshot roster, int playerId)
        {
            if (roster.StartingPitcher.Player.PlayerId == playerId)
                return PitcherRole.Starter;
            for (int index = 0; index < roster.Bullpen.Count; index++)
            {
                if (roster.Bullpen[index].Player.PlayerId == playerId)
                    return roster.Bullpen[index].Role;
            }
            return PitcherRole.MiddleRelief;
        }

        private static string FormatPitcherRole(PitcherRole role)
        {
            return role switch
            {
                PitcherRole.Starter => "선발",
                PitcherRole.Setup => "셋업",
                PitcherRole.Closer => "마무리",
                _ => "불펜"
            };
        }

        private static string FormatDecision(bool hasDecision)
        {
            return hasDecision ? "1" : "-";
        }

        private static void AddRecordRow(RectTransform host, string[] values, float y, bool header)
        {
            if (values == null || values.Length < 2)
                throw new ArgumentException("기록 행에는 선수명과 하나 이상의 기록이 필요합니다.", nameof(values));

            Color background = header ? Ink : ((int)(y / 35) % 2 == 0 ? Paper : new Color32(233, 239, 242, 255));
            var row = Panel("Row", host, background, 0, Math.Max(0, y), 1334, 34);
            Color color = header ? Color.white : Ink;
            float nameWidth = values.Length > 8 ? 234f : 334f;
            float valueWidth = (1334f - nameWidth) / (values.Length - 1);
            Label("Name", row, values[0], 14, 10, 0, nameWidth - 20, 34, color);
            for (int i = 1; i < values.Length; i++)
            {
                Text cell = Label("Value", row, values[i], 13,
                    nameWidth + (i - 1) * valueWidth, 0, valueWidth, 34, color);
                cell.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static void AddScoreCell(RectTransform host, string value, float x, float y,
            float width, float height, Color background, Color foreground, TextAnchor alignment)
        {
            var cell = Panel("Cell", host, background, x, y, width - 1, height - 1);
            Text text = Label("Text", cell, value, 14, 8, 0, width - 16, height, foreground);
            text.alignment = alignment;
        }

        private static void ClearChildren(RectTransform host)
        {
            for (int i = host.childCount - 1; i >= 0; i--)
            {
                GameObject child = host.GetChild(i).gameObject;
                child.SetActive(false);
                // 관전 전체 재생을 EditMode에서도 검증하므로 프레임 종료에 의존하지 않고 정리한다.
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private static void SetLamps(Image[] lamps, int value, Color active)
        {
            for (int i = 0; i < lamps.Length; i++)
                lamps[i].color = i < value ? active : Muted;
        }

        private static void SetBase(Image image, bool occupied)
        {
            image.color = occupied ? new Color32(244, 190, 41, 255) : Muted;
        }

        private static string ParticipantName(MatchHudParticipantModel participant, string fallback)
        {
            return participant != null && participant.HasValue && !string.IsNullOrWhiteSpace(participant.Name)
                ? participant.Name
                : fallback;
        }

        private string FormatCommentary(MatchEvent matchEvent, string result, int runsScored)
        {
            string batter = _session.GetParticipantName(matchEvent.BatterId);
            string pitcher = _session.GetParticipantName(matchEvent.PitcherId);
            if (matchEvent.EventType == MatchEventType.HalfInningEnded)
                return matchEvent.Inning + "회 " + (matchEvent.Half == InningHalf.Top ? "초" : "말") +
                       "가 끝납니다. 양 팀 선수들이 공수를 교대합니다.";
            if (matchEvent.EventType is MatchEventType.MatchEnded or MatchEventType.MatchEndedAsDraw)
                return "경기가 종료됐습니다. 최종 기록과 이닝별 득점 흐름을 확인해 보세요.";
            if (matchEvent.EventType == MatchEventType.PlateAppearanceEnded)
            {
                string score = runsScored > 0 ? " " + runsScored + "점이 들어옵니다." : string.Empty;
                return batter + ", " + result + "." + score + " " + pitcher + "와의 승부가 끝났습니다.";
            }
            if (matchEvent.EventType == MatchEventType.Pitch)
            {
                string pitch = FormatPitchDescription(matchEvent);
                return pitcher + "의 " + pitch + ". " + result + " · " +
                       matchEvent.Balls + "B " + matchEvent.Strikes + "S " + matchEvent.Outs + "O";
            }
            return result;
        }

        private static string FormatPitchDescription(in MatchEvent matchEvent)
        {
            if (!matchEvent.PitchPlayData.HasValue)
                return "투구";

            var pitch = matchEvent.PitchPlayData.Pitch;
            return FormatPitchType(pitch.PitchType) + " " + (pitch.VelocityMph * 1.609344d).ToString("0") + "km/h";
        }

        private static string FormatPitchType(PitchType pitchType)
        {
            return pitchType switch
            {
                PitchType.FourSeamFastball => "포심",
                PitchType.TwoSeamFastball => "투심",
                PitchType.Cutter => "커터",
                PitchType.Slider => "슬라이더",
                PitchType.Curveball => "커브",
                PitchType.Changeup => "체인지업",
                PitchType.Splitter => "스플리터",
                PitchType.Sinker => "싱커",
                PitchType.Sweeper => "스위퍼",
                PitchType.Slurve => "슬러브",
                PitchType.KnuckleCurve => "너클커브",
                PitchType.CircleChangeup => "서클체인지업",
                PitchType.Forkball => "포크볼",
                PitchType.Screwball => "스크루볼",
                PitchType.Knuckleball => "너클볼",
                _ => "투구"
            };
        }

        private int CountRunsSincePreviousBoundary(int visibleCount)
        {
            int runs = 0;
            for (int i = visibleCount - 1; i >= 0; i--)
            {
                MatchEvent matchEvent = _session.GetVisibleEvent(i);
                if (i < visibleCount - 1 &&
                    (matchEvent.EventType is MatchEventType.PlateAppearanceEnded or MatchEventType.HalfInningEnded))
                    break;
                if (matchEvent.EventType == MatchEventType.Score)
                    runs++;
            }
            return runs;
        }

        private static string FormatEventResult(MatchEvent matchEvent)
        {
            if (matchEvent.EventType == MatchEventType.Contact) return "타구가 뻗어갑니다";
            if (matchEvent.EventType == MatchEventType.RunnerAdvance)
                return matchEvent.ToBase == 4 ? "주자가 홈에 들어옵니다" : matchEvent.ToBase + "루 진루";
            if (matchEvent.EventType == MatchEventType.Out) return "아웃";
            if (matchEvent.EventType == MatchEventType.DoublePlay) return "병살";
            if (matchEvent.EventType == MatchEventType.RunnerThrownOut) return "주루 아웃";
            if (matchEvent.EventType is MatchEventType.FieldingError or MatchEventType.ThrowingError) return "실책";
            if (matchEvent.EventType == MatchEventType.StealSucceeded) return "도루 성공";
            if (matchEvent.EventType == MatchEventType.CaughtStealing) return "도루 실패";
            if (IsDecisionEvent(matchEvent.EventType)) return DescribeDecision(matchEvent.EventType);
            if (matchEvent.EventType == MatchEventType.HalfInningEnded) return "공수 교대";
            if (matchEvent.EventType is MatchEventType.MatchEnded or MatchEventType.MatchEndedAsDraw) return "경기 종료";
            if (matchEvent.EventType == MatchEventType.Score) return "득점!";
            if (matchEvent.EventType == MatchEventType.PlayerSubstitution) return "선수 교체";
            if (matchEvent.EventType == MatchEventType.PitcherEntered) return "투수 교체";
            if (matchEvent.EventType == MatchEventType.Pitch)
            {
                return matchEvent.PitchResult switch
                {
                    // Pitch의 카운트는 투구 처리 이후 값이다. 일반 2스트라이크 파울은 3이 되지 않는다.
                    PitchResult.Ball when matchEvent.Balls >= BaseballRules.BallsForWalk => "볼넷",
                    PitchResult.CalledStrike when matchEvent.Strikes >= BaseballRules.StrikesForStrikeout => "삼진 아웃",
                    PitchResult.SwingingStrike when matchEvent.Strikes >= BaseballRules.StrikesForStrikeout => "삼진 아웃",
                    PitchResult.Foul when matchEvent.Strikes >= BaseballRules.StrikesForStrikeout => "삼진 아웃",
                    PitchResult.Ball => "볼",
                    PitchResult.CalledStrike => "스트라이크",
                    PitchResult.SwingingStrike => "헛스윙",
                    PitchResult.Foul => "파울",
                    PitchResult.InPlay => "타격",
                    PitchResult.HitByPitch => "몸에 맞는 공",
                    _ => "투구"
                };
            }
            if (matchEvent.EventType != MatchEventType.PlateAppearanceEnded && matchEvent.EventType != MatchEventType.Hit)
                return "경기 진행";
            return matchEvent.PlateAppearanceResult switch
            {
                PlateAppearanceResult.Walk => "볼넷",
                PlateAppearanceResult.Strikeout => "삼진 아웃",
                PlateAppearanceResult.GroundOut => "땅볼 아웃",
                PlateAppearanceResult.FlyOut => "플라이 아웃",
                PlateAppearanceResult.Single => "안타!",
                PlateAppearanceResult.Double => "2루타!",
                PlateAppearanceResult.Triple => "3루타!",
                PlateAppearanceResult.HomeRun => "홈런!",
                PlateAppearanceResult.HitByPitch => "몸에 맞는 공",
                PlateAppearanceResult.ReachedOnError => "실책 출루",
                PlateAppearanceResult.FieldersChoice => "야수 선택",
                PlateAppearanceResult.SacrificeBunt => "희생 번트",
                PlateAppearanceResult.BuntSingle => "번트 안타!",
                PlateAppearanceResult.BuntPopOut => "번트 플라이",
                PlateAppearanceResult.IntentionalWalk => "고의4구",
                _ => "타석 종료"
            };
        }

        private static bool IsEmphasized(MatchEvent matchEvent)
        {
            return matchEvent.EventType is MatchEventType.Pitch or MatchEventType.Score or MatchEventType.RunnerThrownOut or MatchEventType.CaughtStealing or MatchEventType.DoublePlay or MatchEventType.HalfInningEnded or MatchEventType.MatchEnded or
                   MatchEventType.MatchEndedAsDraw or MatchEventType.PlateAppearanceEnded or MatchEventType.Hit;
        }

        private static string FormatInnings(int outs)
        {
            return (outs / 3) + "." + (outs % 3);
        }
    }
}
