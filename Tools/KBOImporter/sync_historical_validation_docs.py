from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any, Iterable


SNAPSHOT_START = "<!-- HISTORICAL_BAKE_SNAPSHOT:START -->"
SNAPSHOT_END = "<!-- HISTORICAL_BAKE_SNAPSHOT:END -->"


def format_bytes(value: int) -> str:
    return f"{value:,}"


def build_snapshot(report: dict[str, Any]) -> str:
    """ValidationReport의 동일 값을 여러 기준 문서에 넣을 Markdown으로 만든다."""
    verification = report["verification"]
    source = report["sourceArchive"]
    runtime = report["runtimeArchive"]
    lines = [
        SNAPSHOT_START,
        f"## {verification['validationDate']} Source-backed Bake 검증 스냅샷",
        "",
        "이 블록은 `Runtime/validation_report.json`에서 생성한다. 수동으로 숫자를 고치지 않는다.",
        "",
        "| Archive | ContentHash | ArchiveHash | Person | Season | SourceSeason | ReplacementSeason | Payload bytes | Manifest bytes |",
        "| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |",
        (
            f"| Source Audit | `{source['contentHash']}` | `{source['assetArchiveHash']}` | "
            f"{source['summary']['playerPersonCount']:,} | {source['summary']['playerSeasonCount']:,} | "
            f"{source['summary']['sourceBackedPlayerSeasonCount']:,} | "
            f"{source['summary']['replacementGeneratedPlayerSeasonCount']:,} | "
            f"{format_bytes(source['archivePayloadByteLength'])} | {format_bytes(source['manifestByteLength'])} |"
        ),
        (
            f"| Runtime | `{runtime['contentHash']}` | `{runtime['assetArchiveHash']}` | "
            f"{runtime['summary']['playerPersonCount']:,} | {runtime['summary']['playerSeasonCount']:,} | "
            f"{runtime['summary']['sourceBackedPlayerSeasonCount']:,} | "
            f"{runtime['summary']['replacementGeneratedPlayerSeasonCount']:,} | "
            f"{format_bytes(runtime['archivePayloadByteLength'])} | {format_bytes(runtime['manifestByteLength'])} |"
        ),
        "",
        "| 연도 | Source H | Source P | Replacement H | Replacement P | Replacement 비율 | 평균 Cost | 평균 관련 능력치 |",
        "| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for year in report["years"]:
        if int(year["originYear"]) > 1988:
            break
        lines.append(
            f"| {year['originYear']} | {year['sourceHitterCount']} | {year['sourcePitcherCount']} | "
            f"{year['replacementHitterCount']} | {year['replacementPitcherCount']} | "
            f"{year['replacementRatio'] * 100:.1f}% | {year['replacementAverageCost']:.3f} | "
            f"{year['replacementAverageRelevantAbility']:.3f} |"
        )
    python_tests = verification["pythonTests"]
    lines.extend(
        [
            "",
            (
                f"- Python 회귀: {python_tests['passed']}/{python_tests['total']} 통과, "
                f"실패 {python_tests['failed']}, Skip {python_tests['skipped']}"
            ),
            f"- C# 컴파일: {verification['csharpCompileStatus']}",
            f"- Unity EditMode: {verification['unityEditModeStatus']}",
            f"- Historical World: {verification['historicalWorldStatus']}",
            "",
            SNAPSHOT_END,
        ]
    )
    return "\n".join(lines)


def replace_snapshot(document: str, snapshot: str) -> str:
    start = document.find(SNAPSHOT_START)
    end = document.find(SNAPSHOT_END)
    if start < 0 and end < 0:
        return document.rstrip() + "\n\n" + snapshot + "\n"
    if start < 0 or end < start:
        raise ValueError("문서의 Historical Bake snapshot marker가 손상되었습니다.")
    end += len(SNAPSHOT_END)
    return document[:start] + snapshot + document[end:]


def synchronize(
    report_path: Path,
    document_paths: Iterable[Path],
    verification: dict[str, Any],
) -> None:
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report["verification"] = verification
    report_path.write_text(
        json.dumps(report, ensure_ascii=False, sort_keys=True, separators=(",", ":")),
        encoding="utf-8",
    )
    snapshot = build_snapshot(report)
    for path in document_paths:
        updated = replace_snapshot(path.read_text(encoding="utf-8"), snapshot)
        path.write_text(updated, encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="최종 Historical ValidationReport를 기준 문서에 동기화합니다.")
    parser.add_argument("--validation-date", required=True)
    parser.add_argument("--python-tests-passed", type=int, required=True)
    parser.add_argument("--python-tests-total", type=int, required=True)
    parser.add_argument("--python-tests-failed", type=int, default=0)
    parser.add_argument("--python-tests-skipped", type=int, default=0)
    parser.add_argument("--csharp-compile-status", required=True)
    parser.add_argument("--unity-editmode-status", required=True)
    parser.add_argument("--historical-world-status", required=True)
    args = parser.parse_args()

    repository_root = Path(__file__).resolve().parents[2]
    archive_root = repository_root / "Assets" / "Editor Default Resources" / "HistoricalSimulation" / "1982-2025"
    report_path = archive_root / "Runtime" / "validation_report.json"
    document_root = repository_root / "docs" / "todo" / "역사시뮬레이션_감독모드"
    document_paths = (
        repository_root / "Tools" / "KBOImporter" / "README.md",
        document_root / "README.md",
        document_root / "01_시대보정_가상선수생성.md",
        document_root / "08_구현_로드맵_검증기준.md",
    )
    verification = {
        "validationDate": args.validation_date,
        "pythonTests": {
            "passed": args.python_tests_passed,
            "total": args.python_tests_total,
            "failed": args.python_tests_failed,
            "skipped": args.python_tests_skipped,
        },
        "csharpCompileStatus": args.csharp_compile_status,
        "unityEditModeStatus": args.unity_editmode_status,
        "historicalWorldStatus": args.historical_world_status,
    }
    synchronize(report_path, document_paths, verification)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
