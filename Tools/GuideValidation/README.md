# 프런트 매니저 검증

`Invoke-GuideValidation.ps1`은 메인 에디터와 분리된 `output/guide-validation/UnityProject`에 현재 소스·필요 리소스·EditMode 테스트를 복사하고 Unity 테스트를 시작한다. Unity 6000.3.21f1 설치와 저장소의 복원된 `Library/PackageCache`가 필요하다. 최초 리소스 import에는 시간이 걸릴 수 있다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/GuideValidation/Invoke-GuideValidation.ps1
```

프로세스 PID 출력은 성공 판정이 아니다. 프로세스 종료 후 `output/guide-validation/editmode-results.xml`의 실패 수와 `unity.log`를 확인한다. `-TestFilter`로 NUnit 필터, `-UnityPath`로 에디터 경로를 지정할 수 있다. 결과 화면은 격리 프로젝트의 `GuideScreenshots`에 저장된다.

`CurrentSources.targets`는 Unity가 생성한 csproj의 파일 목록이 오래된 경우 현재 어셈블리 경계별 소스를 포함하는 보조 컴파일용이다.

```powershell
dotnet build Baseball.Presentation.Tests.csproj --no-restore -v quiet /p:CustomAfterMicrosoftCommonTargets=C:/UsingProject/UnityProject/UPlayBall_Unity/Tools/GuideValidation/CurrentSources.targets
```

다른 경로에서 실행할 때는 targets의 절대 경로를 바꾼다. 최종 Unity 빌드·Play Mode 검증을 대신하지 않는다. 기능 범위와 실제 검증 기록은 `docs/todo/매니저 모드/프런트_매니저_단계별_구현_결과.md`를 따른다.
