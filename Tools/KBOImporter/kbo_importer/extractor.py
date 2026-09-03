from __future__ import annotations

import copy
import hashlib
import json
import traceback
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Callable

from . import IMPORTER_VERSION, SCHEMA_VERSION
from .awards import parse_all_awards
from .config import (
    ALL_CATEGORIES,
    AWARD_PAGES,
    DEFAULT_TO_YEAR,
    REGULAR_SEASON_VALUE,
    STAT_PAGE_SPECS,
    TRADE_LIST_URL,
    TRADE_PAGE_URL,
    TRADE_SECTION_IDS,
    StatPageSpec,
)
from .errors import KboImporterError, SchemaMismatchError, SeasonDataUnavailableError
from .http import PoliteHttpClient, RawSnapshotCache, RequestPolicy, WebFormsClient
from .models import OverrideCatalog, SeasonAccumulator
from .parsing import parse_stat_pages
from .trades import parse_trade_response
from .validation import validate_normalized_season, validate_saved_document


@dataclass(frozen=True)
class ExtractOptions:
    from_year: int
    to_year: int
    include_current: bool
    force: bool
    force_aggregate: bool
    categories: tuple[str, ...]
    request_policy: RequestPolicy
    data_root: Path | None = None


class KboExtractor:
    """Raw Snapshot을 먼저 완성한 뒤 시즌 단위 정규화 JSON을 원자적으로 저장한다."""

    def __init__(
        self,
        project_root: Path,
        options: ExtractOptions,
        log: Callable[[str], None] = print,
    ) -> None:
        self.project_root = project_root
        self.options = options
        self.log = log
        self.tool_root = project_root / "Tools" / "KBOImporter"
        self.data_root = options.data_root or self.tool_root / ".cache" / "KBOImport"
        self.raw_cache = RawSnapshotCache(self.data_root / "Raw")
        self.normalized_root = self.data_root / "Normalized"
        self.report_root = self.data_root / "Reports"
        self.overrides = OverrideCatalog(self.tool_root / "Overrides")
        self.http = PoliteHttpClient(options.request_policy)
        self.webforms = WebFormsClient(self.http)
        self._base_pages: dict[str, str] = {}
        self._live_aggregate_pages: dict[tuple[int, str], str] = {}
        self._live_team_pages: dict[tuple[int, str, str], str] = {}
        self._used_paths: dict[int, set[Path]] = {}
        self._trade_session_initialized = False
        self._award_snapshot_paths: dict[str, Path] = {}

    def run(self) -> dict[str, object]:
        award_lookup: dict[int, list[dict[str, object]]] = {}
        if "awards" in self.options.categories:
            self.log("[Awards] 공식 수상 페이지 수집 시작")
            try:
                award_lookup = self._load_awards()
            except Exception as error:
                result = {
                    "startedAtUtc": datetime.now(timezone.utc).replace(microsecond=0).isoformat(),
                    "fromYear": self.options.from_year,
                    "toYear": self.options.to_year,
                    "categories": list(self.options.categories),
                    "seasons": [],
                    "failures": [
                        {
                            "year": year,
                            "errorType": type(error).__name__,
                            "error": str(error),
                            "detail": traceback.format_exc(),
                        }
                        for year in range(self.options.from_year, self.options.to_year + 1)
                    ],
                }
                self._write_reports(result)
                return result
            self.log("[Awards] 공식 수상 페이지 수집 완료")

        run_result: dict[str, object] = {
            "startedAtUtc": datetime.now(timezone.utc).replace(microsecond=0).isoformat(),
            "fromYear": self.options.from_year,
            "toYear": self.options.to_year,
            "categories": list(self.options.categories),
            "seasons": [],
            "failures": [],
        }
        for year in range(self.options.from_year, self.options.to_year + 1):
            self.log(f"[{year}] 추출 시작")
            try:
                document = self._extract_year(year, award_lookup.get(year, []))
                output_path = self._output_path(year)
                self._write_json_atomic(output_path, document)
                season_result = self._season_result(document, output_path)
                run_result["seasons"].append(season_result)
                self.log(
                    f"[{year}] 완료: Players={season_result['playerSeasons']}, "
                    f"Teams={season_result['teamSeasons']}, Warnings={season_result['warnings']}"
                )
            except Exception as error:
                failure = {
                    "year": year,
                    "errorType": type(error).__name__,
                    "error": str(error),
                    "detail": traceback.format_exc(),
                }
                run_result["failures"].append(failure)
                self.log(f"[{year}] 실패: {error}")

        run_result["completedAtUtc"] = datetime.now(timezone.utc).replace(microsecond=0).isoformat()
        self._write_reports(run_result)
        return run_result

    def validate_only(self) -> dict[str, object]:
        results: list[dict[str, object]] = []
        failures: list[dict[str, object]] = []
        for year in range(self.options.from_year, self.options.to_year + 1):
            path = self._output_path(year)
            if not path.exists():
                failures.append({"year": year, "error": f"파일이 없습니다: {path}"})
                continue
            try:
                document = json.loads(path.read_text(encoding="utf-8"))
                issues = validate_saved_document(document)
                if not issues:
                    verification = copy.deepcopy(document)
                    verification["validationSummary"] = {
                        "errorCount": 0,
                        "warningCount": 0,
                        "errors": [],
                        "warnings": [],
                    }
                    try:
                        validate_normalized_season(verification)
                    except KboImporterError as error:
                        issues.append(str(error))
                    issues.extend(self._validate_source_snapshots(document))
                if issues:
                    failures.append({"year": year, "error": "; ".join(issues)})
                else:
                    results.append(self._season_result(document, path))
            except (OSError, json.JSONDecodeError) as error:
                failures.append({"year": year, "error": str(error)})
        result = {
            "fromYear": self.options.from_year,
            "toYear": self.options.to_year,
            "categories": list(self.options.categories),
            "seasons": results,
            "failures": failures,
        }
        self._write_reports(result)
        return result

    def _validate_source_snapshots(self, document: dict[str, object]) -> list[str]:
        issues: list[str] = []
        metadata = document.get("sourceMetadata") or {}
        combined = hashlib.sha256()
        for entry in metadata.get("sourceSnapshots", []):
            raw_path = Path(str(entry.get("path") or ""))
            if raw_path.is_absolute() or ".." in raw_path.parts:
                issues.append(f"허용되지 않은 Snapshot 경로입니다: {raw_path}")
                continue
            try:
                actual = self.raw_cache.metadata(raw_path)
            except (OSError, json.JSONDecodeError, KboImporterError) as error:
                issues.append(f"Snapshot 검증 실패: {raw_path}: {error}")
                continue
            if actual.get("sha256") != entry.get("sha256"):
                issues.append(f"Normalized Source Hash가 Raw와 다릅니다: {raw_path}")
            combined.update(raw_path.as_posix().encode("utf-8"))
            combined.update(str(entry.get("sha256") or "").encode("ascii"))
        if combined.hexdigest() != metadata.get("sourceSnapshotHash"):
            issues.append("sourceSnapshotHash가 Snapshot 목록과 일치하지 않습니다.")
        if metadata.get("overrideHash") != self._override_hash():
            issues.append("Normalized Cache의 overrideHash가 현재 Override와 다릅니다.")
        return issues

    def _extract_year(
        self,
        year: int,
        awards: list[dict[str, object]],
    ) -> dict[str, object]:
        accumulator = SeasonAccumulator(year, self.overrides)
        if "team" in self.options.categories and not {
            "hitter", "pitcher", "defense", "runner"
        }.intersection(self.options.categories):
            self._load_team_identity_catalog(year, accumulator)
        for spec in STAT_PAGE_SPECS:
            if spec.category not in self.options.categories:
                continue
            try:
                if spec.scope == "player":
                    row_count = self._process_player_spec(year, spec, accumulator)
                else:
                    row_count = self._process_team_spec(year, spec, accumulator)
            except SeasonDataUnavailableError as error:
                # 역사 페이지마다 제공 시작 연도가 다르다. 이 경우 0으로 채우거나
                # 시즌 전체를 실패시키지 않고 Availability=false를 유지한다.
                accumulator.mark_page_unavailable(spec, str(error))
                self.log(f"[{year}] {spec.key} 미제공: {error}")
                continue
            accumulator.mark_page_available(spec, row_count)
            self.log(f"[{year}] {spec.key} 성공: Rows={row_count}")

        if {"hitter", "pitcher", "defense", "runner"}.intersection(self.options.categories):
            movements = self._load_trade_movements(year)
            accumulator.add_trade_movements(movements)
            accumulator.page_availability["hasTradeMovements"] = True
            # 거래 0건은 정상적인 시즌 값이며 Source 부재가 아니다.
            accumulator.page_statuses["hasTradeMovements"] = "Available"
            self.log(f"[{year}] trade_movements 성공")

        if "awards" in self.options.categories:
            self._link_award_snapshots_to_year(year)
            accumulator.add_awards(awards)

        imported_at, snapshot_hash, snapshots = self.raw_cache.snapshot_metadata_for(
            self._used_paths.get(year, set())
        )
        source_metadata = {
            "source": "KBO Official",
            "importedAtUtc": imported_at,
            "schemaVersion": SCHEMA_VERSION,
            "importerVersion": IMPORTER_VERSION,
            "sourceSnapshotHash": snapshot_hash,
            "sourceSnapshots": snapshots,
            "overrideHash": self._override_hash(),
        }
        current_year = datetime.now(timezone.utc).year
        document = accumulator.build(
            is_season_complete=year < current_year,
            source_metadata=source_metadata,
            selected_categories=list(self.options.categories),
        )
        validate_normalized_season(document)
        return document

    def _load_team_identity_catalog(
        self,
        year: int,
        accumulator: SeasonAccumulator,
    ) -> None:
        spec = next(spec for spec in STAT_PAGE_SPECS if spec.key == "hitter_basic1")
        path = Path(str(year)) / "Identity" / "team_catalog.html"
        html = None if self.options.force else self.raw_cache.read(path)
        if html is None:
            html = self._fetch_aggregate_first(year, spec)
        else:
            self._validate_cached_first(year, spec, None, html)
        self.raw_cache.write(
            path,
            html,
            spec.url,
            "POST",
            self._page_request_parameters(year, spec, None, 1),
        )
        self._mark_used(year, path)
        accumulator.add_team_catalog(self.webforms.select_options(html, "ddlTeam"))

    def _process_player_spec(
        self,
        year: int,
        spec: StatPageSpec,
        accumulator: SeasonAccumulator,
    ) -> int:
        aggregate_pages, first_html = self._fetch_pages(year, spec, None)
        aggregate_rows = parse_stat_pages(spec, aggregate_pages)
        for row in aggregate_rows:
            accumulator.add_player_row(spec, row, None)

        team_options = self.webforms.select_options(first_html, "ddlTeam")
        accumulator.add_team_catalog(team_options)
        team_row_count = 0
        for team_code, team_name in team_options:
            stint_pages, _ = self._fetch_pages(
                year,
                spec,
                (team_code, team_name),
                aggregate_first=first_html,
            )
            stint_rows = parse_stat_pages(spec, stint_pages)
            team_row_count += len(stint_rows)
            for row in stint_rows:
                accumulator.add_player_row(spec, row, (team_code, team_name))
        return len(aggregate_rows) + team_row_count

    def _process_team_spec(
        self,
        year: int,
        spec: StatPageSpec,
        accumulator: SeasonAccumulator,
    ) -> int:
        pages, _ = self._fetch_pages(year, spec, None)
        rows = parse_stat_pages(spec, pages)
        for row in rows:
            accumulator.add_team_row(spec, row)
        return len(rows)

    def _fetch_pages(
        self,
        year: int,
        spec: StatPageSpec,
        team: tuple[str, str] | None,
        aggregate_first: str | None = None,
    ) -> tuple[list[str], str]:
        unavailable_path = self._unavailable_path(year, spec)
        unavailable = None if self.options.force else self.raw_cache.read(unavailable_path)
        if unavailable is not None:
            self._mark_used(year, unavailable_path)
            try:
                reason = str(json.loads(unavailable).get("reason") or "공식 페이지 미제공")
            except json.JSONDecodeError as error:
                raise SchemaMismatchError(
                    f"미제공 Cache Marker가 올바른 JSON이 아닙니다: {unavailable_path}"
                ) from error
            raise SeasonDataUnavailableError(reason)

        first_path = self._raw_page_path(year, spec.key, team, 1)
        force_snapshot = self.options.force or (
            self.options.force_aggregate
            and team is None
            and spec.supports_team_filter
        )
        first_html = None if force_snapshot else self.raw_cache.read(first_path)
        first_was_cached = first_html is not None
        if first_html is not None:
            self._validate_cached_first(year, spec, team, first_html)
        if first_html is None:
            if team is None:
                first_html = self._fetch_aggregate_first(year, spec)
            else:
                if aggregate_first is None:
                    raise ValueError("TeamStint 수집에는 Aggregate 첫 페이지가 필요합니다.")
                first_html = self._fetch_live_first(year, spec, team)
        self.raw_cache.write(
            first_path,
            first_html,
            spec.url,
            "POST",
            self._page_request_parameters(year, spec, team, 1),
        )
        self._mark_used(year, first_path)

        pages = [first_html]
        current_html = first_html
        live_current = None if first_was_cached else first_html
        for _ in range(1, 100):
            next_target = self.webforms.next_page_target(current_html)
            if next_target is None:
                break
            next_page = self.webforms.current_page(current_html) + 1
            next_path = self._raw_page_path(year, spec.key, team, next_page)
            next_html = None if force_snapshot else self.raw_cache.read(next_path)
            if next_html is not None and self.webforms.current_page(next_html) != next_page:
                raise SchemaMismatchError(
                    f"Cached {spec.key} Page가 경로와 일치하지 않습니다: "
                    f"expected={next_page}, actual={self.webforms.current_page(next_html)}"
                )
            if next_html is None:
                if live_current is None:
                    live_current = self._fetch_live_first(year, spec, team)
                    for live_page in range(2, next_page):
                        live_target = self.webforms.next_page_target(live_current)
                        if live_target is None:
                            raise SchemaMismatchError(
                                f"{spec.key} Resume 중 Page {live_page} Target을 찾지 못했습니다."
                            )
                        live_current = self.webforms.postback(
                            spec.url, live_current, live_target
                        )
                live_target = self.webforms.next_page_target(live_current)
                if live_target is None:
                    raise SchemaMismatchError(
                        f"{spec.key} Resume 중 Page {next_page} Target을 찾지 못했습니다."
                    )
                next_html = self.webforms.postback(spec.url, live_current, live_target)
                actual_page = self.webforms.current_page(next_html)
                if actual_page != next_page:
                    raise SchemaMismatchError(
                        f"{spec.key} Pagination이 예상 페이지로 이동하지 않았습니다: expected={next_page}, actual={actual_page}"
                    )
                live_current = next_html
            else:
                # Cached HTML의 ViewState는 이전 Session 소유일 수 있으므로 이후 Hole을
                # 만날 때 한 번만 새 Session에서 직전 페이지까지 재현한다.
                live_current = None
            self.raw_cache.write(
                next_path,
                next_html,
                spec.url,
                "POST",
                self._page_request_parameters(year, spec, team, next_page),
            )
            self._mark_used(year, next_path)
            pages.append(next_html)
            current_html = next_html
        else:
            raise SchemaMismatchError(f"{spec.key} Pagination이 100페이지를 초과했습니다.")
        return pages, first_html

    def _validate_cached_first(
        self,
        year: int,
        spec: StatPageSpec,
        team: tuple[str, str] | None,
        html: str,
    ) -> None:
        year_selector = "ddlYear" if spec.key == "team_rank" else "ddlSeason"
        if self.webforms.selected_value(html, year_selector) != str(year):
            raise SchemaMismatchError(
                f"Cached {spec.key} Season이 경로와 일치하지 않습니다: {year}"
            )
        if self.webforms.selected_value(html, "ddlSeries") != REGULAR_SEASON_VALUE:
            raise SchemaMismatchError(
                f"Cached {spec.key} GameType이 정규시즌이 아닙니다: {year}"
            )
        if team is not None and self.webforms.selected_value(html, "ddlTeam") != team[0]:
            raise SchemaMismatchError(
                f"Cached {spec.key} Team이 경로와 일치하지 않습니다: {team[0]}"
            )

    def _fetch_aggregate_first(self, year: int, spec: StatPageSpec) -> str:
        base_html = self._base_page(spec)
        selector = "ddlYear" if spec.key == "team_rank" else "ddlSeason"
        year_values = {value for value, _ in self.webforms.select_options(base_html, selector)}
        if str(year) not in year_values:
            reason = f"{spec.key} 페이지가 {year} 시즌 선택값을 제공하지 않습니다."
            self._cache_unavailable(year, spec, reason, selector, sorted(year_values))
            raise SeasonDataUnavailableError(reason)
        year_html = self.webforms.select_value(spec.url, base_html, selector, str(year))
        series_values = {
            value for value, _ in self.webforms.select_options(year_html, "ddlSeries")
        }
        if REGULAR_SEASON_VALUE not in series_values:
            reason = f"{spec.key} 페이지가 {year} 정규시즌 선택값을 제공하지 않습니다."
            self._cache_unavailable(year, spec, reason, "ddlSeries", sorted(series_values))
            raise SeasonDataUnavailableError(reason)
        result = self.webforms.select_value(
            spec.url,
            year_html,
            "ddlSeries",
            REGULAR_SEASON_VALUE,
        )
        if spec.supports_team_filter:
            # KBO WebForms는 표시상 Team이 이미 "전체"여도 표 본문은 직전 Team
            # Filter 상태를 유지할 수 있다. 같은 빈 값을 강제로 PostBack해 Server
            # Control 상태와 표를 모두 League Aggregate로 되돌린다.
            result = self.webforms.select_value(
                spec.url,
                result,
                "ddlTeam",
                "",
                force_postback=True,
            )
        if spec.key != "team_rank":
            # KBO Player/Team 통계 페이지는 시즌 변경 PostBack에서 Selector와 Pager만
            # 바꾸고 첫 표 본문은 직전 시즌 값으로 남길 수 있다. 공식 숨은 정렬
            # PostBack으로 현재 Filter의 첫 페이지를 명시적으로 다시 Bind한다.
            result = self.webforms.refresh_statistics(spec.url, result)
        self._live_aggregate_pages[(year, spec.key)] = result
        return result

    def _fetch_live_first(
        self,
        year: int,
        spec: StatPageSpec,
        team: tuple[str, str] | None,
    ) -> str:
        aggregate = self._live_aggregate_pages.get((year, spec.key))
        if aggregate is None:
            aggregate = self._fetch_aggregate_first(year, spec)
        if team is None:
            return aggregate
        key = (year, spec.key, team[0])
        existing = self._live_team_pages.get(key)
        if existing is not None:
            return existing
        result = self.webforms.select_value(spec.url, aggregate, "ddlTeam", team[0])
        self._live_team_pages[key] = result
        return result

    def _base_page(self, spec: StatPageSpec) -> str:
        existing = self._base_pages.get(spec.key)
        if existing is not None:
            return existing
        path = Path("_Bootstrap") / f"{spec.key}.html"
        # KBO WebForms는 새 Process의 Session Cookie와 현재 ViewState를 함께 요구한다.
        # 완성된 시즌 Raw는 재조회하지 않지만, 누락 페이지를 이어 받을 때의 Bootstrap GET은 생략할 수 없다.
        html = self.http.get(spec.url)
        self.raw_cache.write(path, html, spec.url, "GET")
        self._base_pages[spec.key] = html
        return html

    def _load_awards(self) -> dict[int, list[dict[str, object]]]:
        pages: dict[str, str] = {}
        capture_key = f"through_{max(DEFAULT_TO_YEAR, self.options.to_year)}"
        for key, url in AWARD_PAGES.items():
            path = Path("Awards") / capture_key / f"{key}.html"
            html = None if self.options.force else self.raw_cache.read(path)
            if html is None:
                html = self.http.get(url)
                self.raw_cache.write(path, html, url, "GET")
            pages[key] = html
            self._award_snapshot_paths[key] = path
        return parse_all_awards(
            pages["awards_mvp"],
            pages["awards_series_mvp"],
            pages["awards_golden_glove"],
            self.overrides.load_allstar_awards(),
        )

    def _load_trade_movements(self, year: int) -> list[dict[str, object]]:
        movements: list[dict[str, object]] = []
        page_size = 20
        for section_id in TRADE_SECTION_IDS:
            section_movements: list[dict[str, object]] = []
            page = 1
            total_count: int | None = None
            while total_count is None or (page - 1) * page_size < total_count:
                path = Path(str(year)) / f"trade_{section_id}_page_{page:03d}.json"
                request_parameters = {
                    "seasonId": str(year),
                    "monthId": "0",
                    "bdSc": section_id,
                    "teamName": "",
                    "searchIf": "",
                    "pageNo": str(page),
                    "listCount": str(page_size),
                }
                content = None if self.options.force else self.raw_cache.read(path)
                if content is None:
                    self._ensure_trade_session()
                    content = self.http.post(
                        TRADE_LIST_URL,
                        request_parameters,
                        headers={
                            "X-Requested-With": "XMLHttpRequest",
                            "Referer": TRADE_PAGE_URL,
                            "Accept": "application/json, text/javascript, */*; q=0.01",
                        },
                    )
                self.raw_cache.write(
                    path,
                    content,
                    TRADE_LIST_URL,
                    "POST",
                    request_parameters,
                )
                self._mark_used(year, path)
                parsed, parsed_total = parse_trade_response(content, section_id)
                if total_count is not None and parsed_total != total_count:
                    raise SchemaMismatchError(
                        f"선수 이동 Pagination 중 totalCnt가 변경되었습니다: {total_count}/{parsed_total}"
                    )
                total_count = parsed_total
                expected_page_count = min(
                    page_size,
                    max(0, total_count - (page - 1) * page_size),
                )
                if len(parsed) != expected_page_count:
                    raise SchemaMismatchError(
                        f"선수 이동 Page Row 수가 예상과 다릅니다: "
                        f"section={section_id}, page={page}, expected={expected_page_count}, actual={len(parsed)}"
                    )
                section_movements.extend(parsed)
                page += 1
            if total_count is None or len(section_movements) != total_count:
                raise SchemaMismatchError(
                    f"선수 이동 전체 Row 수가 totalCnt와 다릅니다: "
                    f"section={section_id}, expected={total_count}, actual={len(section_movements)}"
                )
            unique_keys = {
                (
                    movement.get("date"),
                    movement.get("movementType"),
                    movement.get("playerName"),
                    movement.get("sourcePosition"),
                    movement.get("sourceNote"),
                )
                for movement in section_movements
            }
            if len(unique_keys) != len(section_movements):
                raise SchemaMismatchError(
                    f"선수 이동 응답에 중복 Row가 있습니다: section={section_id}"
                )
            movements.extend(section_movements)
        return movements

    def _ensure_trade_session(self) -> None:
        if self._trade_session_initialized:
            return
        html = self.http.get(TRADE_PAGE_URL)
        path = Path("_Bootstrap") / "player_trade.html"
        self.raw_cache.write(path, html, TRADE_PAGE_URL, "GET")
        if "GetTradeList" not in html or "bdSc" not in html:
            raise SchemaMismatchError("공식 선수 이동 페이지의 Ajax 계약이 변경되었습니다.")
        self._trade_session_initialized = True

    def _link_award_snapshots_to_year(self, year: int) -> None:
        for key in AWARD_PAGES:
            path = self._award_snapshot_paths.get(key)
            if path is None or self.raw_cache.read(path) is None:
                raise SchemaMismatchError(f"공유 수상 Snapshot이 없습니다: {key}")
            self._mark_used(year, path)

    def _raw_page_path(
        self,
        year: int,
        key: str,
        team: tuple[str, str] | None,
        page: int,
    ) -> Path:
        suffix = "" if page == 1 else f"_page_{page:03d}"
        filename = f"{key}{suffix}.html"
        if team is None:
            return Path(str(year)) / filename
        safe_team = "".join(character for character in team[0] if character.isalnum() or character in "-_")
        if not safe_team:
            safe_team = hashlib.sha256(team[0].encode("utf-8")).hexdigest()[:12]
        return Path(str(year)) / "Teams" / safe_team / filename

    @staticmethod
    def _page_request_parameters(
        year: int,
        spec: StatPageSpec,
        team: tuple[str, str] | None,
        page: int,
    ) -> dict[str, object]:
        return {
            "categoryKey": spec.key,
            "seasonYear": year,
            "gameType": REGULAR_SEASON_VALUE,
            "teamId": None if team is None else team[0],
            "page": page,
        }

    @staticmethod
    def _unavailable_path(year: int, spec: StatPageSpec) -> Path:
        return Path(str(year)) / "Unavailable" / f"{spec.key}.json"

    def _cache_unavailable(
        self,
        year: int,
        spec: StatPageSpec,
        reason: str,
        selector: str,
        available_values: list[str],
    ) -> None:
        path = self._unavailable_path(year, spec)
        content = json.dumps(
            {
                "year": year,
                "categoryKey": spec.key,
                "sourceUrl": spec.url,
                "selector": selector,
                "availableValues": available_values,
                "reason": reason,
            },
            ensure_ascii=False,
            indent=2,
            sort_keys=True,
        ) + "\n"
        self.raw_cache.write(path, content, spec.url, "SCHEMA_OBSERVATION")
        self._mark_used(year, path)

    def _mark_used(self, year: int, path: Path) -> None:
        self._used_paths.setdefault(year, set()).add(path)

    def _output_path(self, year: int) -> Path:
        if set(self.options.categories) == set(ALL_CATEGORIES):
            return self.normalized_root / f"{year}.json"
        category_key = "_".join(sorted(self.options.categories))
        return self.normalized_root / "Partial" / f"{year}_{category_key}.json"

    def _override_hash(self) -> str:
        digest = hashlib.sha256()
        override_root = self.tool_root / "Overrides"
        for path in sorted(override_root.glob("*.csv"), key=lambda item: item.name):
            digest.update(path.name.encode("utf-8"))
            digest.update(path.read_bytes())
        return digest.hexdigest()

    @staticmethod
    def _write_json_atomic(path: Path, document: dict[str, object]) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        temp_path = path.with_suffix(path.suffix + ".tmp")
        temp_path.write_text(
            json.dumps(document, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        temp_path.replace(path)

    @staticmethod
    def _season_result(document: dict[str, object], path: Path) -> dict[str, object]:
        players = document.get("players", [])
        awards = document.get("awards", [])
        statuses = document.get("dataAvailabilityStatus", {})
        missing = sorted(
            key for key, value in statuses.items() if value in {"Unavailable", "AvailableEmpty"}
        )
        not_selected = sorted(key for key, value in statuses.items() if value == "NotSelected")
        return {
            "year": document["year"],
            "path": str(path),
            "isSeasonComplete": document["isSeasonComplete"],
            "playerSeasons": len(players),
            "officialPlayerIds": sum(
                1 for player in players if player.get("identityStatus") == "OfficialPlayerId"
            ),
            "unresolvedPlayerIds": sum(
                1 for player in players if not player.get("sourcePlayerId")
            ),
            "hitters": sum(1 for player in players if player.get("hitterStats") is not None),
            "pitchers": sum(1 for player in players if player.get("pitcherStats") is not None),
            "defensePlayers": sum(1 for player in players if player.get("defenseRecords")),
            "runningPlayers": sum(1 for player in players if player.get("runningStats") is not None),
            "teamSeasons": len(document.get("teams", [])),
            "awards": len(awards),
            "allStarSelections": sum(1 for award in awards if award.get("awardType") == "AllStarSelection"),
            "allStarSelectionAvailability": document.get("awardAvailabilityStatus", {}).get(
                "AllStarSelection", "NotSelected"
            ),
            "goldenGloves": sum(1 for award in awards if award.get("awardType") == "GoldenGlove"),
            "regularSeasonMvps": sum(1 for award in awards if award.get("awardType") == "RegularSeasonMvp"),
            "otherMvps": sum(
                1
                for award in awards
                if award.get("awardType") in {"AllStarGameMvp", "KoreanSeriesMvp"}
            ),
            "resolvedAwards": len(awards)
            - len(document.get("unresolvedAwards", []))
            - len(document.get("ambiguousAwards", [])),
            "unresolvedAwards": len(document.get("unresolvedAwards", [])),
            "ambiguousAwards": len(document.get("ambiguousAwards", [])),
            "tradeMovements": len(document.get("tradeMovements", [])),
            "unresolvedTradeMovements": len(document.get("unresolvedTradeMovements", [])),
            "ambiguousTradeMovements": len(document.get("ambiguousTradeMovements", [])),
            "warnings": document.get("validationSummary", {}).get("warningCount", 0),
            "missingCategories": missing,
            "notSelectedCategories": not_selected,
            "missingCategoryReasons": document.get("missingCategoryReasons", {}),
        }

    def _write_reports(self, run_result: dict[str, object]) -> None:
        self.report_root.mkdir(parents=True, exist_ok=True)
        category_key = "_".join(str(value) for value in run_result.get("categories", [])) or "all"
        report_key = (
            f"{run_result.get('fromYear', 'unknown')}_{run_result.get('toYear', 'unknown')}_"
            f"{category_key}"
        )
        json_path = self.report_root / "KBO_IMPORT_REPORT.json"
        self._write_json_atomic(json_path, run_result)
        self._write_json_atomic(
            self.report_root / f"KBO_IMPORT_REPORT_{report_key}.json",
            run_result,
        )

        seasons = run_result.get("seasons", [])
        failures = run_result.get("failures", [])
        unique_players: set[str] = set()
        total_players = 0
        total_teams = 0
        for season in seasons:
            total_players += int(season.get("playerSeasons", 0))
            total_teams += int(season.get("teamSeasons", 0))
            path = Path(str(season.get("path", "")))
            if path.exists():
                try:
                    document = json.loads(path.read_text(encoding="utf-8"))
                    unique_players.update(
                        str(player["sourcePlayerId"])
                        for player in document.get("players", [])
                        if player.get("sourcePlayerId")
                    )
                except (OSError, json.JSONDecodeError):
                    pass

        lines = [
            "# KBO Import Report",
            "",
            f"- Total Seasons: {len(seasons)}",
            f"- Total PlayerSeasons: {total_players}",
            f"- Total Unique Players: {len(unique_players)}",
            f"- Total TeamSeasons: {total_teams}",
            f"- Parser Errors: {len(failures)}",
            "",
        ]
        for season in seasons:
            lines.extend(
                [
                    f"## {season['year']}",
                    "",
                    f"- Teams: {season['teamSeasons']}",
                    f"- PlayerSeasons: {season['playerSeasons']}",
                    f"- Official PlayerIds: {season['officialPlayerIds']}",
                    f"- Unresolved PlayerIds: {season['unresolvedPlayerIds']}",
                    f"- Hitters: {season['hitters']}",
                    f"- Pitchers: {season['pitchers']}",
                    f"- Defense Players: {season['defensePlayers']}",
                    f"- Running Players: {season['runningPlayers']}",
                    f"- Awards: {season['awards']}",
                    f"- All-Star Selections: {season['allStarSelections']}",
                    f"- All-Star Selection Availability: {season['allStarSelectionAvailability']}",
                    f"- Golden Gloves: {season['goldenGloves']}",
                    f"- Regular Season MVPs: {season['regularSeasonMvps']}",
                    f"- Other MVPs: {season['otherMvps']}",
                    f"- Resolved Awards: {season['resolvedAwards']}",
                    f"- Unresolved Awards: {season['unresolvedAwards']}",
                    f"- Ambiguous Awards: {season['ambiguousAwards']}",
                    f"- Trade Movements: {season['tradeMovements']}",
                    f"- Unresolved Trade Movements: {season['unresolvedTradeMovements']}",
                    f"- Ambiguous Trade Movements: {season['ambiguousTradeMovements']}",
                    f"- Warnings: {season['warnings']}",
                    f"- Missing Categories: {', '.join(season['missingCategories']) or 'None'}",
                    f"- Not Selected Categories: {', '.join(season['notSelectedCategories']) or 'None'}",
                    "",
                ]
            )
        if failures:
            lines.extend(["## Parser Errors", ""])
            for failure in failures:
                lines.append(f"- {failure['year']}: {failure['error']}")
            lines.append("")
        markdown_path = self.report_root / "KBO_IMPORT_REPORT.md"
        markdown_temp = markdown_path.with_suffix(markdown_path.suffix + ".tmp")
        markdown_temp.write_text("\n".join(lines), encoding="utf-8")
        markdown_temp.replace(markdown_path)
        scoped_markdown_path = self.report_root / f"KBO_IMPORT_REPORT_{report_key}.md"
        scoped_markdown_temp = scoped_markdown_path.with_suffix(
            scoped_markdown_path.suffix + ".tmp"
        )
        scoped_markdown_temp.write_text("\n".join(lines), encoding="utf-8")
        scoped_markdown_temp.replace(scoped_markdown_path)
