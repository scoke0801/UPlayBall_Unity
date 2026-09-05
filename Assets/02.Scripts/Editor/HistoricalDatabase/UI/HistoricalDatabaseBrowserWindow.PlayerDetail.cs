using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Baseball.Editor.HistoricalDatabase
{
    public sealed partial class HistoricalDatabaseBrowserWindow
    {
        private void ShowPlayerEmptyState()
        {
            _playerDetailContent.Clear();
            var message = new Label("왼쪽 목록에서 선수를 선택하세요.");
            message.AddToClassList("schema-absent");
            _playerDetailContent.Add(message);
        }

        private void BuildPlayerDetail()
        {
            _playerDetailContent.Clear();
            HistoricalPlayerRow row = _selectedPlayer;
            if (row == null)
            {
                ShowPlayerEmptyState();
                return;
            }

            AddPlayerHeader(row);
            AddPersonSection(row);
            AddReferenceSourceSection(row);
            AddOriginSection(row);
            AddCostSection(row);
            AddAbilitySection(row);
            AddPotentialSection(row);
            AddSchemaExtensionSections(row);
            AddSeasonStatisticsSection(row);
            AddAwardSection(row);
            AddCareerSection(row);
            AddCompareSection(row);
        }

        private void AddPlayerHeader(HistoricalPlayerRow row)
        {
            var header = new VisualElement();
            header.AddToClassList("detail-header");
            var heading = new VisualElement();
            heading.AddToClassList("detail-heading-row");
            var text = new VisualElement { style = { flexGrow = 1f } };
            var name = new Label(string.IsNullOrWhiteSpace(row.Name) ? "이름 없음" : row.Name);
            name.AddToClassList("detail-title");
            text.Add(name);
            var subtitle = new Label($"{row.OriginYear} · {row.OriginFranchiseId} · {FormatPosition(row.Position)}{FormatPlayerRoleSuffix(row)}");
            subtitle.AddToClassList("detail-subtitle");
            text.Add(subtitle);
            var personId = new Label($"인물 ID  {row.PlayerPersonId}");
            personId.AddToClassList("id-label");
            text.Add(personId);
            var seasonId = new Label($"선수 시즌 ID  {row.PlayerSeasonId}");
            seasonId.AddToClassList("id-label");
            text.Add(seasonId);
            heading.Add(text);
            var cost = new Label($"비용\n{row.Cost} / 10");
            cost.AddToClassList("cost-chip");
            heading.Add(cost);
            header.Add(heading);
            _playerDetailContent.Add(header);
        }

        private void AddPersonSection(HistoricalPlayerRow row)
        {
            VisualElement section = CreateDetailSection("인물 정보");
            HistoricalPlayerPerson person = row.Person;
            if (person == null)
            {
                AddAbsent(section, "PlayerPerson 참조를 찾을 수 없습니다.");
                return;
            }
            AddKeyValue(section, row.IsOriginalSource ? "실제 선수명" : "에디터 표시 이름", person.DisplayName);
            AddKeyValue(section, "출생 연도", person.BirthYear > 0 ? person.BirthYear.ToString() : "원본에 없음");
            AddKeyValue(section, "해당 시즌 나이", row.Age?.ToString() ?? "—");
            AddKeyValue(section, "타석 / 투구 손", FormatHandednessPair(person.Bats, person.Throws));
            AddKeyValue(section, "주 포지션", FormatPosition(person.PrimaryPosition));
            AddKeyValue(section, "등록 유형", FormatRegistrationType(person.RegistrationType));
            AddKeyValue(section, "커리어 기간", $"{person.CareerStartYear}–{person.CareerEndYear}");
        }

        private void AddReferenceSourceSection(HistoricalPlayerRow row)
        {
            VisualElement section = CreateDetailSection("원본 연결");
            if (row.IsOriginalSource)
            {
                AddKeyValue(section, "실제 선수명", row.OriginalName);
                AddKeyValue(section, "연결 방식", "선수 1명 ↔ 시즌 1개 ↔ 기록 1개");
                var originalNote = new Label("정규화 캐시의 해당 선수 시즌을 평균·혼합하지 않은 에디터 전용 원본 기록입니다.");
                originalNote.AddToClassList("derived-badge");
                section.Add(originalNote);
                return;
            }

            AddKeyValue(section, "Source 선수명", row.OriginalName);
            if (row.SourceReferenceNames.Count == 0)
            {
                AddAbsent(section, "이 아카이브에는 원본 참조 이름이 포함되지 않았습니다.");
                return;
            }

            AddKeyValue(section, "1:1 Source 원본명", string.Join(", ", row.SourceReferenceNames));
            var note = new Label("이 PlayerSeason은 한 Source PlayerSeason의 기록만 정규화하여 능력치로 Bake합니다.");
            note.AddToClassList("derived-badge");
            section.Add(note);
        }

        private void AddOriginSection(HistoricalPlayerRow row)
        {
            VisualElement section = CreateDetailSection("소속과 역할");
            AddKeyValue(section, "시즌 연도", row.OriginYear.ToString());
            AddKeyValue(section, "소속 팀", row.OriginFranchiseId);
            AddLinkValue(section, "팀 시즌", row.OriginTeamSeasonKey, () => SelectTeam(_viewModel.FindTeam(row.OriginTeamSeasonKey), true));
            AddKeyValue(section, "포지션", FormatPosition(row.Position));
            AddKeyValue(section, "투수 역할", row.IsPitcher ? FormatPitcherRole(row.PitcherRole) : "해당 없음");
            AddKeyValue(section, "선수 유형", FormatPlayerType(row.PlayerType));
            AddKeyValue(section, "로스터 역할", FormatRosterRole(row.RosterRole));
            if (!row.IsOriginalSource)
                AddKeyValue(section, "원본과의 유사 거리", $"{row.Season.ReferenceSimilarityDistance:0.000000} (원본값)");

            HistoricalPositionRoleDerivationTrace trace = row.Season.PositionRoleDerivationTrace;
            if (trace == null)
                return;
            var detail = new Foldout { text = "Natural Position / PitcherRole 산출 상세", value = false };
            AddKeyValue(detail, "Classifier", trace.ClassifierVersion);
            AddKeyValue(detail, "선택 근거", trace.Reason);
            if (row.IsPitcher)
            {
                HistoricalPitcherRoleEvidenceTrace evidence = trace.PitcherRoleEvidence;
                if (evidence != null)
                {
                      AddKeyValue(
                          detail,
                          "시즌 기용",
                          $"G {evidence.Games:0} · GS {(evidence.GamesStartedAvailable ? evidence.GamesStarted.ToString("0") : "미제공")} " +
                          $"({(evidence.GamesStartedAvailable ? evidence.GamesStartedRate : evidence.InferredStarterRate):P1}) · " +
                          $"Relief {evidence.ReliefAppearances:0} · GF {evidence.GamesFinished:0}");
                      AddKeyValue(
                          detail,
                          "구원 신호",
                          $"SV {evidence.Saves:0} · HLD {(evidence.HoldsAvailable ? evidence.Holds.ToString("0") : "미제공")} · " +
                          $"CG {evidence.CompleteGames:0} · IP {evidence.Innings:0.0} · IP/G {evidence.InningsPerGame:0.00}");
                      AddKeyValue(detail, "Starter 근거 모드", evidence.StarterEvidenceMode);
                }
                for (int index = 0; index < trace.PitcherRoleScores.Count; index++)
                {
                    HistoricalPitcherRoleScoreTrace score = trace.PitcherRoleScores[index];
                    AddKeyValue(detail, FormatPitcherRole(score.Role), score.Score.ToString("0.000"));
                }
            }
            else
            {
                for (int index = 0; index < trace.PositionCandidates.Count; index++)
                {
                    HistoricalPositionCandidateTrace candidate = trace.PositionCandidates[index];
                    AddKeyValue(
                        detail,
                        FormatPosition(candidate.Position),
                        $"수비 {candidate.InningsOuts / 3d:0.0}이닝 · GS {candidate.GamesStarted:0} · G {candidate.Games:0}");
                }
            }
            section.Add(detail);
        }

        private void AddCostSection(HistoricalPlayerRow row)
        {
            VisualElement section = CreateDetailSection(row.IsOriginalSource ? "비용 · 파생" : "비용");
            AddKeyValue(section, row.IsOriginalSource ? "기본 전력 기준 비용" : "저장값", $"{row.Cost} / 10");
            HistoricalCostDerivationTrace trace = row.Season.CostDerivationTrace;
            if (trace != null)
            {
                AddKeyValue(section, "역할 Composite", $"{trace.Composite:0.0000} · {trace.RoleProfile}");
                AddKeyValue(
                    section,
                    "동일 연도·유형 백분위 (참고)",
                    $"순위 {trace.Rank:N0} / {trace.PopulationCount:N0} · {trace.Percentile:P2}");
                var detail = new Foldout { text = "Cost 산출 상세", value = false };
                AddKeyValue(detail, "계산 기준", "역할별 기본 전력의 고정 구간 · 출전량 추가 할인 없음");
                if (trace.CostEligibility != null)
                    AddKeyValue(detail, "시즌 출전량", trace.CostEligibility.Reason);
                double lower = 0d;
                for (int index = 0; index < trace.CompositeThresholds.Count; index++)
                {
                    HistoricalCostThresholdTrace threshold = trace.CompositeThresholds[index];
                    if (threshold.Cost == trace.Cost)
                        AddKeyValue(detail, "적용 전력 구간", $"{lower:0.##} 이상 · {threshold.UpperExclusive:0.##} 미만");
                    lower = threshold.UpperExclusive;
                }
                for (int index = 0; index < trace.AbilityContribution.Count; index++)
                {
                    HistoricalCostAbilityContributionTrace contribution = trace.AbilityContribution[index];
                    AddKeyValue(
                        detail,
                        FormatAbilityName(contribution.Ability),
                        $"{contribution.Rating} × {contribution.NormalizedWeight:P1} = {contribution.Contribution:0.0000}");
                }
                section.Add(detail);
                return;
            }

            List<HistoricalPlayerRow> pool = _data.PlayerRows
                .Where(candidate => candidate.OriginYear == row.OriginYear && string.Equals(candidate.PlayerType, row.PlayerType, StringComparison.Ordinal))
                .ToList();
            if (pool.Count == 0)
                return;
            int higher = pool.Count(candidate => candidate.Cost > row.Cost);
            int same = pool.Count(candidate => candidate.Cost == row.Cost);
            int topPercent = Mathf.Clamp(Mathf.CeilToInt((higher + same * 0.5f) * 100f / pool.Count), 1, 100);
            AddKeyValue(section, "동일 연도 선수 유형 분포", $"상위 {topPercent}% · {pool.Count:N0}명 기준 (파생)");
        }

        private void AddAbilitySection(HistoricalPlayerRow row)
        {
            VisualElement section = CreateDetailSection(row.IsPitcher ? "투수 능력치" : "타자 능력치");
            if (row.IsOriginalSource)
            {
                var note = new Label("정규화 원본 시즌 기록의 연도 내 분포를 25~95로 환산한 파생 능력치입니다.");
                note.AddToClassList("derived-badge");
                section.Add(note);
            }
            int first = row.IsPitcher ? 6 : 0;
            int end = row.IsPitcher ? 12 : 6;
            for (int index = first; index < end; index++)
            {
                if (row.BaseAttributes.Length <= index)
                {
                    AddAbsent(section, $"{FormatAbilityName(index)}: 기본 능력치가 없습니다.");
                    continue;
                }
                int? ceiling = row.TrainingCeiling.Length > index ? row.GetTrainingCeiling(index) : (int?)null;
                HistoricalAbilityDerivationTrace trace = FindAbilityTrace(row, HistoricalPlayerRow.AbilityNames[index]);
                AddAbilityRow(section, FormatAbilityName(index), row.GetBaseAbility(index), ceiling, trace);
            }
        }

        private static void AddAbilityRow(
            VisualElement section,
            string name,
            int baseValue,
            int? ceiling,
            HistoricalAbilityDerivationTrace trace)
        {
            var row = new VisualElement();
            row.AddToClassList("ability-row");
            var nameLabel = new Label(name);
            nameLabel.AddToClassList("ability-name");
            var valueLabel = new Label(baseValue.ToString());
            valueLabel.AddToClassList("ability-value");
            var track = new VisualElement();
            track.AddToClassList("ability-track");
            var fill = new VisualElement();
            fill.AddToClassList("ability-base");
            fill.style.width = Length.Percent(Mathf.Clamp(baseValue, 0, 100));
            track.Add(fill);
            var headroom = new Label("파생값");
            if (ceiling.HasValue)
            {
                var marker = new VisualElement();
                marker.AddToClassList("ability-ceiling-marker");
                marker.style.left = Length.Percent(Mathf.Clamp(ceiling.Value, 0, 100));
                track.Add(marker);
                headroom.text = $"상한 {ceiling.Value}  (여유 {ceiling.Value - baseValue:+#;-#;0})";
                headroom.tooltip = "훈련 상한과 성장 여유";
            }
            headroom.AddToClassList("ability-headroom");
            row.Add(nameLabel);
            row.Add(valueLabel);
            row.Add(track);
            row.Add(headroom);
            section.Add(row);
            if (trace == null)
                return;

            var detail = new Foldout
            {
                text = $"{name} 산출 상세 · Group {trace.GroupKey}",
                value = false
            };
            AddKeyValue(
                detail,
                "최종 변환",
                $"Combined Z {trace.CombinedZ:0.0000} → {trace.RatingBeforeClamp:0.00} → {trace.RatingAfterClamp}");
            for (int index = 0; index < trace.Components.Count; index++)
            {
                HistoricalAbilityComponentTrace component = trace.Components[index];
                string raw = component.IsAvailable ? component.RawValue.ToString("0.######") : "원본에 없음";
                AddKeyValue(
                    detail,
                    component.Metric,
                    $"Raw {raw} · 표본 {component.SampleSize:0.##} · 신뢰도 {component.Reliability:P1} · " +
                    $"비교 {component.ReferenceGroupKey} · Z {component.RawZ:0.0000} → 제한 {component.BoundedZ:0.0000} · " +
                    $"사전 Z {component.PriorZ:0.00} → 보정 {component.AdjustedZ:0.0000} · 기여 {component.Contribution:0.0000}");
            }
            section.Add(detail);
        }

        private static HistoricalAbilityDerivationTrace FindAbilityTrace(
            HistoricalPlayerRow row,
            string attribute)
        {
            IReadOnlyList<HistoricalAbilityDerivationTrace> traces = row.Season.AbilityDerivationTrace;
            for (int index = 0; index < traces.Count; index++)
            {
                HistoricalAbilityDerivationTrace trace = traces[index];
                if (trace != null && string.Equals(trace.Attribute, attribute, StringComparison.Ordinal))
                    return trace;
            }
            return null;
        }

        private void AddPotentialSection(HistoricalPlayerRow row)
        {
            VisualElement section = CreateDetailSection("성장 잠재 성향");
            if (row.PotentialTrait.Length == 0)
            {
                AddAbsent(section, "선수 잠재 성향 데이터가 없습니다.");
                return;
            }
            int first = row.IsPitcher ? 6 : 0;
            int end = Math.Min(row.IsPitcher ? 12 : 6, row.PotentialTrait.Length);
            for (int index = first; index < end; index++)
                AddKeyValue(section, FormatAbilityName(index) + " 성장 성향", $"{row.GetPotentialTrait(index)} / 100 (원본값)");
        }

        private void AddSchemaExtensionSections(HistoricalPlayerRow row)
        {
            VisualElement hidden = CreateDetailSection("히든 능력치 / 특성");
            AddAbsent(hidden, "히든 능력치와 성격 데이터는 현재 아카이브 스키마에 존재하지 않습니다.");
            if (!row.IsPitcher)
                return;
            VisualElement pitches = CreateDetailSection("보유 구종");
            AddAbsent(pitches, "보유 구종 데이터는 현재 아카이브 스키마에 Bake되지 않았습니다.");
        }

        private void AddSeasonStatisticsSection(HistoricalPlayerRow row)
        {
            VisualElement section = CreateDetailSection("시즌 성적");
            HistoricalSeasonRecord record = row.Record;
            if (record == null)
            {
                AddAbsent(section, "이 선수 시즌에 연결된 원본 시즌 기록이 없습니다.");
                return;
            }
            var storedTitle = new Label(row.IsOriginalSource ? "원본 저장 기록" : "저장 기록");
            storedTitle.AddToClassList("id-label");
            section.Add(storedTitle);
            var stored = new VisualElement();
            stored.AddToClassList("stat-grid");
            if (row.IsPitcher)
            {
                AddStat(stored, "경기", record.Games.ToString());
                AddStat(stored, "선발", record.GamesStarted.ToString());
                AddStat(stored, "이닝", FormatInnings(record.PitchingOuts));
                AddStat(stored, "승", record.Wins.ToString());
                AddStat(stored, "패", record.Losses.ToString());
                AddStat(stored, "세이브", record.Saves.ToString());
                AddStat(stored, "홀드", record.Holds.ToString());
                AddStat(stored, FormatStatisticSourceLabel("평균자책점", record.HasStoredEarnedRunAverage, row.EarnedRunAverage.HasValue), FormatDecimal(row.EarnedRunAverage, "0.00"));
                AddStat(stored, FormatStatisticSourceLabel("이닝당 출루허용", record.HasStoredWhip, row.WalksAndHitsPerInningPitched.HasValue), FormatDecimal(row.WalksAndHitsPerInningPitched, "0.00"));
                AddStat(stored, "피안타", record.HitsAllowed.ToString());
                AddStat(stored, "피홈런", record.HomeRunsAllowed.ToString());
                AddStat(stored, "볼넷", record.PitchingWalks.ToString());
                AddStat(stored, "삼진", record.PitchingStrikeouts.ToString());
                AddStat(stored, "자책점", record.EarnedRuns.ToString());
            }
            else
            {
                AddStat(stored, "경기", record.Games.ToString());
                AddStat(stored, "타석", record.PlateAppearances.ToString());
                AddStat(stored, "타수", record.AtBats.ToString());
                AddStat(stored, "안타", record.Hits.ToString());
                AddStat(stored, "2루타", record.Doubles.ToString());
                AddStat(stored, "3루타", record.Triples.ToString());
                AddStat(stored, "홈런", record.HomeRuns.ToString());
                AddStat(stored, "타점", record.RunsBattedIn.ToString());
                AddStat(stored, "득점", record.Runs.ToString());
                AddStat(stored, "볼넷", record.Walks.ToString());
                AddStat(stored, "삼진", record.Strikeouts.ToString());
                AddStat(stored, "도루", record.StolenBases.ToString());
                AddStat(stored, "도루 실패", record.CaughtStealing.ToString());
                AddStat(stored, FormatStatisticSourceLabel("타율", record.HasStoredBattingAverage, row.BattingAverage.HasValue), FormatRate(row.BattingAverage));
                AddStat(stored, FormatStatisticSourceLabel("출루율", record.HasStoredOnBasePercentage, row.OnBasePercentage.HasValue), FormatRate(row.OnBasePercentage));
                AddStat(stored, FormatStatisticSourceLabel("장타율", record.HasStoredSluggingPercentage, row.SluggingPercentage.HasValue), FormatRate(row.SluggingPercentage));
                AddStat(stored, FormatStatisticSourceLabel("출루율+장타율", record.HasStoredOnBasePlusSlugging, row.OnBasePlusSlugging.HasValue), FormatRate(row.OnBasePlusSlugging));
            }
            if (!row.IsPitcher)
            {
                AddStat(stored, "수비 기회", record.DefensiveChances.ToString());
                AddStat(stored, "실책", record.FieldingErrors.ToString());
            }
            section.Add(stored);

            var derivedTitle = new Label("파생 지표 · 원본 저장값이 아닌 단순 산술 계산");
            derivedTitle.AddToClassList("derived-badge");
            section.Add(derivedTitle);
            var derived = new VisualElement();
            derived.AddToClassList("stat-grid");
            if (row.IsPitcher)
            {
                AddStat(derived, "9이닝당 삼진", FormatDecimal(row.StrikeoutsPerNine, "0.0"));
            }
            else
            {
                AddStat(derived, "타석당 안타", FormatRate(row.HitsPerPlateAppearance));
            }
            section.Add(derived);
        }

        private void AddAwardSection(HistoricalPlayerRow row)
        {
            VisualElement section = CreateDetailSection($"수상 기록 · {row.AwardCount:N0}");
            if (row.Awards.Count == 0)
            {
                AddAbsent(section, "연결된 원본 수상 기록이 없습니다.");
                return;
            }
            for (int index = 0; index < row.Awards.Count; index++)
            {
                HistoricalAwardRecord award = row.Awards[index];
                string source = string.IsNullOrWhiteSpace(award.Source) ? string.Empty : $" · 출처 {award.Source}";
                var label = new Label($"★ {award.SeasonYear}  {FormatAwardType(award.AwardType)}  {FormatPosition(award.Position)}{source}");
                label.AddToClassList("award-row");
                section.Add(label);
            }
        }

        private void AddCareerSection(HistoricalPlayerRow row)
        {
            IReadOnlyList<HistoricalPlayerRow> career = _viewModel.FindPersonCareer(row.PlayerPersonId);
            VisualElement section = CreateDetailSection($"커리어 연표 · {career.Count:N0}시즌");
            for (int index = 0; index < career.Count; index++)
            {
                HistoricalPlayerRow season = career[index];
                var timeline = new VisualElement();
                timeline.AddToClassList("timeline-row");
                var year = new Label(season.OriginYear.ToString());
                year.AddToClassList("timeline-year");
                var team = new Label(season.OriginFranchiseId);
                team.AddToClassList("timeline-team");
                var summary = new Label($"비용 {season.Cost} · {FormatSeasonSummary(season)}");
                summary.AddToClassList("timeline-summary");
                var open = new Button(() => SelectPlayer(season, true)) { text = "열기" };
                open.AddToClassList("link-button");
                timeline.Add(year);
                timeline.Add(team);
                timeline.Add(summary);
                timeline.Add(open);
                section.Add(timeline);
            }
        }

        private void AddCompareSection(HistoricalPlayerRow selected)
        {
            if (_comparePlayers.Count < 2)
                return;
            bool sameFamily = _comparePlayers.All(row => row.IsPitcher == _comparePlayers[0].IsPitcher);
            VisualElement section = CreateDetailSection($"선수 비교 · {_comparePlayers.Count}/4");
            if (!sameFamily)
            {
                AddAbsent(section, "타자와 투수는 같은 능력치 표에서 비교하지 않습니다. 한 역할군만 고정하세요.");
                return;
            }
            int first = _comparePlayers[0].IsPitcher ? 6 : 0;
            int end = _comparePlayers[0].IsPitcher ? 12 : 6;
            var header = new VisualElement();
            header.AddToClassList("summary-row");
            var metric = new Label("항목");
            metric.AddToClassList("summary-name");
            header.Add(metric);
            for (int index = 0; index < _comparePlayers.Count; index++)
            {
                var name = new Label(_comparePlayers[index].Name) { tooltip = _comparePlayers[index].PlayerSeasonId };
                name.AddToClassList("summary-number");
                header.Add(name);
            }
            section.Add(header);
            AddCompareRow(section, "비용", player => player.Cost.ToString());
            for (int ability = first; ability < end; ability++)
            {
                int captured = ability;
                AddCompareRow(section, FormatAbilityName(ability), player => player.GetBaseAbility(captured).ToString());
            }
            if (_comparePlayers[0].IsPitcher)
            {
                AddCompareRow(section, "평균자책점", player => FormatDecimal(player.EarnedRunAverage, "0.00"));
                AddCompareRow(section, "9이닝당 삼진", player => FormatDecimal(player.StrikeoutsPerNine, "0.0"));
            }
            else
            {
                AddCompareRow(section, "타율", player => FormatRate(player.BattingAverage));
                AddCompareRow(section, "홈런", player => player.Record?.HomeRuns.ToString() ?? "—");
            }
        }

        private void AddCompareRow(VisualElement section, string title, Func<HistoricalPlayerRow, string> value)
        {
            var row = new VisualElement();
            row.AddToClassList("summary-row");
            var name = new Label(title);
            name.AddToClassList("summary-name");
            row.Add(name);
            for (int index = 0; index < _comparePlayers.Count; index++)
            {
                var cell = new Label(value(_comparePlayers[index]));
                cell.AddToClassList("summary-number");
                row.Add(cell);
            }
            section.Add(row);
        }

        private VisualElement CreateDetailSection(string title)
        {
            var section = new VisualElement();
            section.AddToClassList("detail-section");
            var label = new Label(title);
            label.AddToClassList("detail-section-title");
            section.Add(label);
            _playerDetailContent.Add(section);
            return section;
        }

        private static void AddStat(VisualElement parent, string name, string value)
        {
            var cell = new VisualElement();
            cell.AddToClassList("stat-cell");
            var nameLabel = new Label(name);
            nameLabel.AddToClassList("stat-name");
            var valueLabel = new Label(value);
            valueLabel.AddToClassList("stat-value");
            cell.Add(nameLabel);
            cell.Add(valueLabel);
            parent.Add(cell);
        }

        private static string FormatStatisticSourceLabel(string name, bool hasStoredValue, bool canDerive)
        {
            if (hasStoredValue)
                return name + " · 저장값";
            return canDerive ? name + " · 파생" : name + " · 원본에 없음";
        }

        private static void AddAbsent(VisualElement parent, string message)
        {
            var label = new Label(message);
            label.AddToClassList("schema-absent");
            parent.Add(label);
        }

        private static void AddLinkValue(VisualElement parent, string key, string value, Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList("key-value-row");
            var keyLabel = new Label(key);
            keyLabel.AddToClassList("key-label");
            var button = new Button(onClick) { text = string.IsNullOrWhiteSpace(value) ? "—" : value };
            button.AddToClassList("link-button");
            row.Add(keyLabel);
            row.Add(button);
            parent.Add(row);
        }

        private static string FormatRole(string role)
        {
            return string.IsNullOrWhiteSpace(role) ? string.Empty : " · " + role;
        }

        private static string FormatPlayerRole(HistoricalPlayerRow row)
        {
            return row != null && row.IsPitcher ? FormatPitcherRole(row.PitcherRole) : "—";
        }

        private static string FormatPlayerRoleSuffix(HistoricalPlayerRow row)
        {
            return row != null && row.IsPitcher ? FormatRole(FormatPitcherRole(row.PitcherRole)) : string.Empty;
        }

        private static string FormatAbilityName(int index)
        {
            return index switch
            {
                0 => "컨택",
                1 => "장타력",
                2 => "주력",
                3 => "송구",
                4 => "수비",
                5 => "타자 멘탈",
                6 => "체력",
                7 => "구속",
                8 => "구위",
                9 => "변화구",
                10 => "제구",
                11 => "투수 멘탈",
                _ => "알 수 없는 능력치",
            };
        }

        private static string FormatAbilityName(string value)
        {
            for (int index = 0; index < HistoricalPlayerRow.AbilityNames.Count; index++)
                if (string.Equals(value, HistoricalPlayerRow.AbilityNames[index], StringComparison.Ordinal))
                    return FormatAbilityName(index);
            return string.IsNullOrWhiteSpace(value) ? "원본에 없음" : value;
        }

        private static string FormatPosition(string value)
        {
            return value switch
            {
                "P" => "투수",
                "C" => "포수",
                "1B" => "1루수",
                "2B" => "2루수",
                "3B" => "3루수",
                "SS" => "유격수",
                "LF" => "좌익수",
                "CF" => "중견수",
                "RF" => "우익수",
                "DH" => "지명타자",
                "OF" => "외야수",
                _ => string.IsNullOrWhiteSpace(value) ? "원본에 없음" : value,
            };
        }

        private static string FormatPitcherRole(string value)
        {
            return value switch
            {
                "Starter" => "선발",
                "Swingman" => "스윙맨",
                "LongRelief" => "롱릴리프",
                "MiddleRelief" => "중간계투",
                "Setup" => "셋업",
                "Closer" => "마무리",
                _ => string.IsNullOrWhiteSpace(value) ? "원본에 없음" : value,
            };
        }

        private static string FormatPlayerType(string value)
        {
            return value switch
            {
                "Hitter" => "타자",
                "Pitcher" => "투수",
                _ => string.IsNullOrWhiteSpace(value) ? "원본에 없음" : value,
            };
        }

        private static string FormatRosterRole(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "원본에 없음";
            if (value.StartsWith("StartingHitter:", StringComparison.Ordinal))
                return "주전 타자 · " + FormatPosition(value.Substring("StartingHitter:".Length));
            if (value.StartsWith("BenchHitter:", StringComparison.Ordinal))
                return "벤치 타자 " + value.Substring("BenchHitter:".Length);
            if (value.StartsWith("StartingPitcher:", StringComparison.Ordinal))
                return "선발 투수 " + value.Substring("StartingPitcher:".Length);
            if (value.StartsWith("Bullpen", StringComparison.Ordinal))
                return "불펜 " + value.Substring("Bullpen".Length);
            if (value.StartsWith("ReserveHitter:", StringComparison.Ordinal))
                return "예비 타자 " + value.Substring("ReserveHitter:".Length);
            if (value.StartsWith("ReservePitcher:", StringComparison.Ordinal))
                return "예비 투수 " + value.Substring("ReservePitcher:".Length);
            return FormatPitcherRole(value);
        }

        private static string FormatRegistrationType(string value)
        {
            return value switch
            {
                "Domestic" => "국내 선수",
                "Foreign" => "외국인 선수",
                "Unknown" => "원본에 없음",
                _ => string.IsNullOrWhiteSpace(value) ? "원본에 없음" : value,
            };
        }

        private static string FormatHandednessPair(string bats, string throwsValue)
        {
            return $"{FormatHandedness(bats)} / {FormatHandedness(throwsValue)}";
        }

        private static string FormatHandedness(string value)
        {
            return value switch
            {
                "Right" => "우",
                "Left" => "좌",
                "Switch" => "양",
                "Unknown" => "원본에 없음",
                _ => string.IsNullOrWhiteSpace(value) ? "원본에 없음" : value,
            };
        }

        private static string FormatAwardType(string value)
        {
            return value switch
            {
                "AllStar" => "올스타",
                "GoldenGlove" => "골든글러브",
                "RegularSeasonMvp" => "정규시즌 MVP",
                "AllStarGameMvp" => "올스타전 MVP",
                "PostseasonMvp" => "포스트시즌 MVP",
                _ => value,
            };
        }

        private static string BuildPlayerSummary(HistoricalPlayerRow row)
        {
            var builder = new StringBuilder();
            builder.Append(row.OriginYear).Append(' ').Append(row.Name).AppendLine();
            builder.Append(row.OriginFranchiseId).Append(" · ").Append(FormatPosition(row.Position)).Append(FormatPlayerRoleSuffix(row)).AppendLine();
            builder.Append("비용 ").Append(row.Cost).AppendLine();
            int first = row.IsPitcher ? 6 : 0;
            int end = row.IsPitcher ? 12 : 6;
            for (int index = first; index < end; index++)
            {
                if (index > first) builder.Append(" / ");
                builder.Append(FormatAbilityName(index)).Append(' ').Append(row.GetBaseAbility(index));
            }
            builder.AppendLine().Append(FormatSeasonSummary(row));
            builder.AppendLine().Append(row.PlayerSeasonId);
            return builder.ToString();
        }
    }
}
