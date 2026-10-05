# 필수 검증

로비부터 실제 전장까지의 연결 검사는 별도 `run-match.ps1 -Players 2` 또는 `-Players 4`로 실행합니다. Go 서버를 로컬에서 실행하는 선택적 통합 검사이며 기본 검사에 포함되지 않습니다. 사용법은 [지인 테스트 안내](../docs/friend-test.md#검증)를 참고하세요.

실제 서버 없이 Main 메시지 분기, 입력, 상태 변경을 확인한다. 상태를 바꾸는 명령·소유권·동기화·재접속과 주요 게임 규칙을 남겼다. 외형·재질·색상·스크린샷 검사는 미리보기 장면으로 확인한다.

PowerShell 7에서 Godot .NET 실행 파일을 지정한다. 처음 받은 프로젝트는 Godot 에디터에서 리소스를 가져오고 `dotnet restore`를 한 번 실행한다.

```powershell
$env:GODOT_BIN = 'C:\tools\Godot\Godot_console.exe' # 실제 설치 경로
./tests/run.ps1                    # 빌드 + 기본 4개
./tests/run.ps1 -Suite Buildings   # 건설·생산·랠리 변경
./tests/run.ps1 -Check RallyChecks # 단일 기능 변경
```

| 선택 | 남긴 검증 | 실행할 변경 |
| --- | --- | --- |
| Smoke (기본) | MapSync, UnitScene, CommandPanel, Stock | 여러 핵심 흐름을 간단히 확인할 때 |
| Units | UnitScene, Interpolation, MinionSync | 이동·공격·채집 명령, 선택 소유권, 미니언, 보간 |
| Buildings | BuildingPlacement, ConstructionSync, CommandPanel, Rally, TowerSync | 배치·공사·생산·취소·랠리·건물 전투 표시 |
| World | MapSync, MapZones, FogSync, check_arena | 맵 해시·나무·도로·차폐·지형 충돌 |
| UI | RtsControl, SelectionDetails, CommandPanel, MatchResult | 부대 지정·미니맵 조작·초상화 선택·생산 UI·승패와 로비 복귀 |
| All | 중복을 제거한 16개 전체 | 테스트 구성 변경 또는 시스템 전반의 변경 |

표에서 `check_arena`를 제외한 이름에는 `Checks` 접미사가 붙는다. 한 기능만 바꿨으면 `-Check 이름`이 우선이며, 관련 그룹을 실행했다면 기본 검사까지 추가 실행할 필요는 없다. 문서·외형만 바꿨다면 자동 테스트는 생략할 수 있다.

실행기는 빌드 한 번 후 선택한 검사만 실행하고, 성공은 한 줄로 요약한다. 상세 출력은 Git에서 제외된 `.godot/check-logs/`에 저장한다. 실패·엔진 오류·PASS 누락·제한 시간 초과는 실패로 처리하며 즉시 멈춘다. 기본 제한은 검사당 30초다. 이미 같은 소스를 빌드했다면 `-SkipBuild`를 사용할 수 있다.

독립 외형 검사 7개, 중복된 사망 연출·미니맵 검사 2개, 외부 서버 접속 검사 2개를 제거했다. 사망·HIDE·오래된 메시지는 UnitScene/Interpolation/FogSync, 미니맵 입력은 RtsControl, 건물 생성·완공은 ConstructionSync에서 계속 확인한다. 실제 서버의 전투·비용·환급·자동 채집 결과는 이 클라이언트 검증 범위에 포함되지 않는다.
