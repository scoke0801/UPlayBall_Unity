# 매니저 리포트 콘솔 검증

Unity 에디터를 실행하지 않고 실제 Core/Simulation/Game 소스를 참조해 안건과 저장 상태를 검증한다.

```powershell
dotnet run --project Tools/ManagerReportValidation/ManagerReportValidation.csproj -c Release
```

묶음·대상별 판단·열람·변경·보류·재발·보관·JSON 저장·장기 이력 정책과 48건 합성 중복 사례를 검사한다.
총 38건을 검사한다. 성장·공식 기록·주간 회고·기용 관찰·발행 제한과 손상된 저장 근거도 포함한다.
실제 훈련 명령과 유학 완료의 저장을 확인하고, 실제 경기 12회씩 두 경로를 실행해 점수·전체 이벤트·주간 집계가 같음을 확인한다.
두 경로는 소식 정책이 다르며 한쪽은 중간에 저장 복원한다. `RuntimeFixture`는 기존 저장 테스트의 합성 구단 구성만 옮긴 것으로 NUnit 테스트를 실행하지 않는다.
20시즌 검증은 안내 재평가·480건 소식 보존 검사이며 실제 경기 엔진 대량 실행은 아니다.
변경과 UI 검수 제한은 [적용 보고서](../../docs/reports/owner-manager-report-cases.md)를 따른다.
