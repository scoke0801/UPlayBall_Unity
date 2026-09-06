using System;
using Baseball.Core.Players;
using Baseball.Game.Career;
using Baseball.Simulation.Match;
using Baseball.Simulation.PlateAppearance;
using UnityEngine;
using UnityEngine.UI;

namespace Baseball.Presentation.Match
{
    public sealed partial class UI_Scene_OwnerMatchSpectator
    {
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
        }

        private void RefreshControls()
        {
            if (_session == null) return;
            OwnerMatchOverlayState state = _session.State;
            bool isComplete = state.IsComplete;
            bool isNewBoundary = _lastVisibleCount != state.VisibleEventCount;
            _pauseButton.interactable = state.CanTogglePause;
            _pauseLabel.text = state.IsPaused ? "계속 보기" : "일시정지";
            _advanceButton.interactable = state.CanAdvance;
            _revealAllButton.interactable = state.CanAdvance;
            for (int i = 0; i < _speedButtons.Length; i++)
            {
                OwnerMatchPlaybackSpeed speed = i == 0
                    ? OwnerMatchPlaybackSpeed.Normal
                    : i == 1 ? OwnerMatchPlaybackSpeed.Fast : OwnerMatchPlaybackSpeed.VeryFast;
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
                : state.IsPaused ? "중계 일시정지" : "생중계 · 감독 AI 자동 진행";
            _homeButton.gameObject.SetActive(isComplete);
            _resultButton.gameObject.SetActive(isComplete);
            _resultToggleLabel.text = _showResults ? "경기 화면" : "경기 결과";
            _resultPanel.gameObject.SetActive(isComplete && _showResults);

            if (isNewBoundary)
            {
                UpdateEventPresentation();
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
                _resultPanel.gameObject.SetActive(true);
                BuildFinalResult();
                _wasComplete = true;
            }
        }

        private void UpdateEventPresentation()
        {
            int visibleCount = _session.State.VisibleEventCount;
            if (visibleCount == 0)
            {
                _commentary.text = "양 팀 선수들이 그라운드에 들어섭니다. 곧 경기가 시작됩니다.";
            _announcement.text = "경기 시작";
                _announcementStartedAt = Time.unscaledTime;
                return;
            }

            MatchEvent matchEvent = _session.GetVisibleEvent(visibleCount - 1);
            StartStadiumAnimation(matchEvent);
            string result = FormatEventResult(matchEvent);
            int runsScored = CountRunsSincePreviousBoundary(visibleCount);
            _commentary.text = FormatCommentary(matchEvent, result, runsScored);
            _announcement.text = IsEmphasized(matchEvent) ? result : string.Empty;
            if (_announcement.text.Length > 0)
                _announcementStartedAt = Time.unscaledTime;
        }

        private void BuildFinalResult()
        {
            MatchResult match = _session.Result.Match;
            string away = CurrentModel?.AwayTeam.Name ?? "원정";
            string home = CurrentModel?.HomeTeam.Name ?? "홈";
            _resultHeading.text = match.IsTie ? "경기 종료 · 무승부" :
                    (match.WinnerTeamId == match.AwayBoxScore.TeamId ? "원정팀 승리" : "홈팀 승리");
            Baseball.Core.Teams.ManagerTacticalProfile profile = _session.Result.EffectiveManagerProfile;
            _resultSummary.text = away + "  " + match.AwayBoxScore.Runs + "  :  " +
                                  match.HomeBoxScore.Runs + "  " + home + "\n" +
                                  _session.Result.ManagerDisplayName + " 감독 · " +
                                  _session.Result.HeadCoachDisplayName + " 수석코치 · " +
                                  $"타격 {profile.BattingApproach} / 주루 {profile.RunningAggression} / " +
                                  $"번트 {profile.SmallBallPreference} / 대타 {profile.PinchHitAggression} / " +
                                  $"선발 훅 {profile.HookSpeed} / 불펜 {profile.BullpenAggression}";
            _managerDecisionSummary.text = FormatManagerDecisionSummary(match.DecisionTrace);
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

        private string FormatManagerDecisionSummary(System.Collections.Generic.IReadOnlyList<DecisionTraceEntry> trace)
        {
            string result = string.Empty;
            int count = 0;
            for (int index = 0; index < trace.Count && count < 3; index++)
            {
                DecisionTraceEntry entry = trace[index];
                if (!_session.IsPlayerTeamParticipant(entry.ActorId)) continue;
                result += (count == 0 ? string.Empty : "  |  ") + entry.Inning + "회" +
                          (entry.Half == InningHalf.Top ? "초 " : "말 ") +
                          _session.GetParticipantName(entry.ActorId) + " · " + FormatDecisionAction(entry.Action) +
                          $" (판단 {entry.Score:0.00} / 기준 {entry.Threshold:0.00})";
                count++;
            }
            return count == 0 ? "오늘의 감독 판단 · 주요 개입 없음" : "오늘의 감독 판단 · " + result;
        }

        private static string FormatDecisionAction(string action)
        {
            return action switch
            {
                "PitchingChange" => "투수 교체",
                "PinchHit" => "대타 기용",
                "PinchRunner" => "대주자 기용",
                "DefensiveReplacement" => "수비 교체",
                "SacrificeBunt" => "희생번트",
                "Steal" => "도루 시도",
                _ => action
            };
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
            string team = _showHomeRecords ? CurrentModel.HomeTeam.Name : CurrentModel.AwayTeam.Name;
            _recordHeader.text = team + " · " + (_showPitching ? "투구 기록" : "타격 기록");
            float y = 0;
            if (_showPitching)
            {
                AddRecordRow(_recordContent, "선수", "이닝", "피안타", "실점", "볼넷", "탈삼진", -1, true);
                for (int i = 0; i < box.PitchingLines.Count; i++)
                {
                    PlayerPitchingLine line = box.PitchingLines[i];
                    if (line.BattersFaced == 0) continue;
                    AddRecordRow(_recordContent, _session.GetParticipantName(line.PlayerId),
                        FormatInnings(line.OutsRecorded), line.HitsAllowed.ToString(), line.RunsAllowed.ToString(),
                        line.WalksAllowed.ToString(), line.Strikeouts.ToString(), y += 35, false);
                }
            }
            else
            {
                AddRecordRow(_recordContent, "선수", "타수", "득점", "안타", "타점", "홈런", -1, true);
                for (int i = 0; i < box.BattingLines.Count; i++)
                {
                    PlayerBattingLine line = box.BattingLines[i];
                    if (line.PlateAppearances == 0) continue;
                    AddRecordRow(_recordContent, _session.GetParticipantName(line.PlayerId),
                        line.AtBats.ToString(), line.Runs.ToString(), line.Hits.ToString(),
                        line.RunsBattedIn.ToString(), line.HomeRuns.ToString(), y += 35, false);
                }
            }
            _recordContent.sizeDelta = new Vector2(1360, Mathf.Max(264, y + 42));
            _recordScroll.verticalNormalizedPosition = 1;
        }

        private static void AddRecordRow(RectTransform host, string name, string a, string b, string c,
            string d, string e, float y, bool header)
        {
            Color background = header ? Ink : ((int)(y / 35) % 2 == 0 ? Paper : new Color32(233, 239, 242, 255));
            var row = Panel("Row", host, background, 0, Math.Max(0, y), 1360, 34);
            Color color = header ? Color.white : Ink;
            Label("Name", row, name, 14, 10, 0, 480, 34, color);
            string[] values = { a, b, c, d, e };
            for (int i = 0; i < values.Length; i++)
            {
                Text cell = Label("Value", row, values[i], 14, 500 + i * 160, 0, 150, 34, color);
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
                host.GetChild(i).gameObject.SetActive(false);
                Destroy(host.GetChild(i).gameObject);
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
            if (matchEvent.EventType == MatchEventType.HalfInningEnded) return "공수 교대";
            if (matchEvent.EventType is MatchEventType.MatchEnded or MatchEventType.MatchEndedAsDraw) return "경기 종료";
            if (matchEvent.EventType == MatchEventType.Score) return "득점!";
            if (matchEvent.EventType == MatchEventType.PlayerSubstitution) return "선수 교체";
            if (matchEvent.EventType == MatchEventType.PitcherEntered) return "투수 교체";
            if (matchEvent.EventType == MatchEventType.Pitch)
            {
                return matchEvent.PitchResult switch
                {
                    PitchResult.Ball => "볼",
                    PitchResult.CalledStrike => "스트라이크",
                    PitchResult.SwingingStrike => "헛스윙",
                    PitchResult.Foul => "파울",
                    PitchResult.InPlay => "타격",
                    PitchResult.HitByPitch => "몸에 맞는 공",
                    _ => "투구"
                };
            }
            if (matchEvent.EventType != MatchEventType.PlateAppearanceEnded)
                return "경기 진행";
            return matchEvent.PlateAppearanceResult switch
            {
                PlateAppearanceResult.Walk => "볼넷",
                PlateAppearanceResult.Strikeout => "삼진",
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
            return matchEvent.EventType is MatchEventType.Pitch or MatchEventType.HalfInningEnded or MatchEventType.MatchEnded or
                   MatchEventType.MatchEndedAsDraw || (matchEvent.EventType == MatchEventType.PlateAppearanceEnded &&
                   matchEvent.PlateAppearanceResult is PlateAppearanceResult.Single or PlateAppearanceResult.Double or
                       PlateAppearanceResult.Triple or PlateAppearanceResult.HomeRun or PlateAppearanceResult.Strikeout);
        }

        private static string FormatInnings(int outs)
        {
            return (outs / 3) + "." + (outs % 3);
        }
    }
}
