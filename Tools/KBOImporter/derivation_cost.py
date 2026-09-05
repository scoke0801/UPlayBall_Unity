"""Source와 Replacement가 공유하는 능력치 기반 Cost 경계다."""

from __future__ import annotations

import math
from typing import Iterable


def composite_cost(composite: float, thresholds: Iterable[tuple[float, int]]) -> int:
    """출전량·모집단 크기와 무관하게 같은 종합 능력치에는 같은 가격을 부여한다."""
    if not math.isfinite(composite):
        raise ValueError("Cost 종합 능력치는 유한해야 합니다.")
    for upper_exclusive, cost in thresholds:
        if composite < upper_exclusive:
            return cost
    raise ValueError("Cost 종합 능력치 구간이 입력값을 덮지 않습니다.")
