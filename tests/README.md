# 필수 검증

학습 모델 경로·관전 스냅샷 실행기 변경은 `python -B tests/test_watch_snapshot.py`로
이전 저장소의 완료 기록 경로 재해석, 최신 체크포인트 선택, 정책 대체 경로를 검사합니다.
이 검사는 Godot 빌드와 PyTorch 없이 실행됩니다.

관전은 `run.ps1 -Check SpectatorChecks`로 중립 역할·전체 시야·명령 차단·대상 조회·따라가기/사망·역할 초기화를 검사합니다. 실제 학습 모델 관전은 `run-match.ps1 -Players 1 -Spectate -PolicyDirectory <hero.json과 commander.json이 있는 폴더> -SkipBuild`로 관전 방에서 두 번의 새 경기와 방 복귀를 확인합니다. `-RenderCapture`는 숨김 렌더 창에서 실제 경기 화면을 `docs/images/spectator-trained-ai.png`에 저장합니다. 기본 검사는 서버 메시지를 모의 주입하고, 실제 경기 검사는 Go 로비·서버를 실행합니다. All은 현재 24개 검사입니다.

로비·방부터 실제 전장까지의 연결 검사는 별도 `run-match.ps1 -Players 2` 또는 `-Players 8`로 실행합니다. Go 서버를 로컬에서 실행하는 선택적 통합 검사이며 기본 검사에 포함되지 않습니다. 사용법은 [지인 테스트 안내](../docs/friend-test.md#검증)를 참고하세요.

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
| Units | UnitScene, Interpolation, MinionSync, RecoveryClient, SupportUpdate | 이동·공격·채집 명령, 선택 소유권, 미니언, 보간, 사령관·상점·귀환, 마석 슬롯 추가 |
| Buildings | BuildingPlacement, ConstructionSync, CommandPanel, Rally, TowerSync | 배치·공사·생산·취소·랠리·건물 전투 표시, 생산·연구 중 톱니·연기 |
| World | MapSync, MapZones, FogSync, Tribute, SiegeRam, check_arena | 맵 해시·나무·도로·차폐·지형 충돌·마석 이벤트·공성추 |
| UI | RtsControl, SelectionDetails, CommandPanel, MatchResult, Spectator, Settings | 부대 지정·미니맵 조작·초상화 선택·편성 순서·생산 UI·승패·관전과 로비 복귀·설정(언어) |
| Network | NetClient, Reconnect | 비동기 전송 순서·대기열 상한·쓰기 시간 제한·세션 취소와 재접속 격리·메인 스레드 오류 전달·마지막 `LEAVE` 전송, 경기 중 끊김의 제자리 재접속·무응답 감지·AI 대행 팀원 표시·방의 경기 복귀 제안 |
| 단일 검사 `-Check RtsControlChecks` | RtsControl | 입력·편성 구매·탭 전환·네이티브 드래그 순서 변경·취소·오래된 리비전·역할 변경 |
| 단일 검사 `-Check TributeChecks` | Tribute | 마석 스냅샷·대기/수집 시간·영웅 우클릭·역할 제한·재동기화/연결 종료 |
| 단일 검사 `-Check SiegeRamChecks` | SiegeRam | 자동 공성추 소환·체력·실제 우클릭 공격·서버 충돌 연출·정리 |
| 단일 검사 `-Check RecoveryClientChecks` | RecoveryClient | 사령관, 상점·장비, 아이템 상자 우클릭·일꾼 배달, 귀환·효과 타이머, 액티브·패시브 강화 및 포인트 동기화, 역할·맵·연결 초기화 |
| 단일 검사 `-Check SupportUpdateChecks` | SupportUpdate | 인력 사무소의 돌팔이 의사(비허가 실험실 필요)·`HEALING`·의사 마나, 마석 가격·잔액과 + 버튼, HUD 표시 폭·역할/맵 초기화 |
| 단일 검사 `-Check SettingsChecks` | Settings | 설정 기본값(영어·100%)·저장과 다시 읽기·버스 음량·언어 즉시 반영, 로비·경기 HUD 문장의 영어 번역 누락·자리 표시, 상점 아이템 이름(`key`)·효과(`effects`)의 영어와 한국어 대체 |
| All | 중복을 제거한 24개 전체 | 테스트 구성 변경 또는 시스템 전반의 변경 |

표에서 `check_arena`를 제외한 이름에는 `Checks` 접미사가 붙는다. 한 기능만 바꿨으면 `-Check 이름`이 우선이며, 관련 그룹을 실행했다면 기본 검사까지 추가 실행할 필요는 없다. 문서·외형만 바꿨다면 자동 테스트는 생략할 수 있다.

`ReconnectChecks`는 실제 Main 장면에 서버 메시지를 주입합니다. 시작한 경기에서 연결이 끊기면 경기 정보를 유지한 채 같은 입장권으로 다시 접속하고 새 스냅샷의 `MATCH_START`로 끝나는지, 5초 무응답을 끊김으로 보는지, `AWAY`로 AI가 조종하는 팀원을 표시하고 다시 동기화할 때 지우는지, `방으로 나가기`가 `LEAVE`를 보내고, 그 경기가 아직 진행 중인 방이 경기로 끌어들이지 않고 **경기로 돌아가기**를 띄우는지 확인합니다. 서버의 자리 유지·AI 인계 판정은 서버 검사 범위입니다.

`NetClientChecks`는 소켓 대신 쓰기를 정지할 수 있는 스트림을 사용합니다. 실제 서버 없이 전송 중 UI 호출이 즉시 반환하는지, 명령 순서와 5초 제한 시간, 연결 교체 후 이전 오류가 새 세션에 영향을 주지 않는지 확인합니다.

`CommandPanelChecks`는 비허가 실험실 Q/W 연구(서버 `ARMY_UPGRADE`의 비용·진행·잘못된 스냅샷 무시)와 HQ Z 업그레이드의 서버 제공 목재·기름 비용, 자원 부족 시 송신 차단, 비용이 없는 이전 메시지의 잠금, 생산 대기열과 업그레이드 중 생산 차단, 서버 진행률, 최대 3티어, 적·영웅 역할 제한과 재접속·초기화를 검사합니다. 비용·시간은 검사에서 임의로 주입하며 실제 밸런스 수치를 복제하지 않습니다. `RecoveryClientChecks`는 3단계 아이템 계보와 HQ 티어 구매 조건, 배송 중 강화 차단, 같은 계열 중복 방지, 누적 구매가의 50% 판매 표시, 기존 12필드 인벤토리의 안전한 읽기도 검사합니다.

`run-match.ps1 -Players 1`은 실제 복원 서버로 혼자 만든 방에서 사령관 영웅 연습 → 방 복귀 → 지휘관 연습을 확인합니다. 영웅 경기는 아이템 목록 수신, 실제 구매·장착 응답과 W 요청을 포함합니다. 전체 전투 밸런스나 학습 모델 성능 검사는 아닙니다. `RecoveryClientChecks.tscn -- --capture`는 사령관·상자·상점의 렌더 미리보기를 `docs/images/recovered-client.png`에 저장하며, 이 장면의 상점 목록은 3개 테스트 항목입니다.

실행기는 빌드 한 번 후 선택한 검사만 실행하고, 성공은 한 줄로 요약한다. 상세 출력은 Git에서 제외된 `.godot/check-logs/`에 저장한다. 실패·엔진 오류·PASS 누락·제한 시간 초과는 실패로 처리하며 즉시 멈춘다. 기본 제한은 검사당 30초다. 이미 같은 소스를 빌드했다면 `-SkipBuild`를 사용할 수 있다.

`TributeChecks`는 실제 Main 장면에 서버 스냅샷을 주입하고 Viewport 우클릭이 메모리 네트워크 출력에 정확한 마석 명령 하나만 보내는지 확인한다. 최초 2분·공성추 진군 중 대기·공성추 모두 소멸 후 서버가 예약한 3분 카운트다운, 진군 중 재접속, 7초 채널의 표시와 완료 응답 대기, RTS·다른 소유 영웅의 수집 거부를 포함한다. 서버의 실제 접근 거리·채널 중단·마석 보상 판정은 별도 서버 검사 범위다. 이미 빌드한 뒤 렌더 미리보기만 저장하려면 Godot를 화면 렌더 모드로 실행한다.

```powershell
& $env:GODOT_BIN --path . --resolution 1152x648 res://tests/TributeChecks.tscn -- --capture
```

위 실행은 검증 중 중앙 마석과 영웅, HUD, 절반 진행된 수집 채널을 `docs/images/tribute-preview.png`에 저장한다.

`SiegeRamChecks`는 실제 Main 장면에 공성추 소환·체력·충돌 메시지를 주입한다. 자율 유닛 소유권, 아군 관찰과 적 우클릭 공격의 분리, 명시적인 `RAM_IMPACT`의 중복 방지와 `HP`·`REMOVE` 이전에 피해나 삭제를 예측하지 않는지 확인한다. `HIDE`·전체 정리·맵 재동기화도 포함하며 서버의 실제 이동·충돌 피해 판정은 별도 서버 검사 범위다.

```powershell
& $env:GODOT_BIN --path . --resolution 1152x648 res://tests/SiegeRamChecks.tscn -- --capture
```

위 실행은 검증 후 실제 위쪽 라인의 양팀 공성추와 호위병을 가까이 촬영하여 `docs/images/siege-ram-preview.png`에 저장한다. UI는 모델을 가리지 않도록 숨기며 유닛 체력바는 유지한다.

편성 검사는 RtsControl에서 한 번만 실행한다. UnitScene·TowerSync·SiegeRam의 모델 장식·이펙트 크기·연출 종료 대기와 MapZones의 색상 검사를 제거했다. MapZones와 check_arena는 모든 타일 종류를 담은 12×10 고정 맵으로 건설 규칙과 지형 충돌을 확인한다. check_arena의 충돌 샘플은 실제 맵 전체 86,418개에서 고정 맵 480개로 줄였으며, 실제 맵의 대칭·경로·확장 부지 규칙은 서버 검사에 맡긴다.

사망·HIDE·오래된 메시지는 UnitScene/Interpolation/FogSync, 미니맵 입력은 RtsControl, 건물 생성·완공은 ConstructionSync에서 계속 확인한다. 실제 서버의 전투·비용·환급·자동 채집 결과는 이 클라이언트 검증 범위에 포함되지 않는다.

`CommandPanelChecks`는 서버 `REQUIRES_TIER`의 임의 티어 조건, 정의 미수신, 업그레이드 미완료·적 팀 상태, 잘못된 메시지, Z/X/C 조작, 배치 중 재잠금과 재접속 초기화를 검사합니다. `ConstructionSyncChecks`는 공장·기술연구소를 포함한 실제 Main 씬의 타입별 생성 연결도 검사합니다.

`BuildingPlacementChecks`는 인력 사무소·비허가 실험실·공장의 배치 이미지 카탈로그가 실제 씬의 점유 크기와 일치하는지 확인합니다. 인력 사무소·비허가 실험실의 좌표 스냅·명령 전송·주변 건설 가능 영역도 씬에서 읽은 크기를 기준으로 검사합니다.

CommandPanelChecks의 공장 검사는 임의 TRAIN_DEF의 목재·기름·인구·시간, 부족 자원·미완공·대기열 잠금, 잘못된 정의·세션 초기화, 생산·취소·랠리와 신규 유닛 생성 경로를 확인합니다. 공성 모드 검사는 최초 미수신 잠금, 잘못된 상태, 전환 중 재요청 차단, 혼합 선택·소유권·관전·일꾼 Z 우선순위, HIDE 이후 재등장 초기화를 포함합니다.

`UnitSceneChecks`는 증기포격차의 모드별 총구·반동·표적 조준과 소형 포탑의 이동 모드 공격 조건을 확인합니다. 전개·복귀 스냅샷에 따른 하부 증기, 고정된 효과 풀의 재사용, 전환 만료·숨김·죽음 시 정리, 지난 전환의 재생 방지도 검사합니다. 서버의 실제 피해·이동 판정은 포함하지 않습니다.

공압사수 검사는 최초·중복 공격 스냅샷의 탄환 재생 방지, 실제 총구에서의 발사, 서로 다른 모의 STATS 간격에서 밸브 누르기와 총 지지 자세 복귀, 호스 양끝 연결, 자동 추가 발사 방지, 정지·숨김 시 정리를 확인합니다. 원거리 미니언의 기존 화살 동작도 함께 유지되는지 확인합니다.

`CommandPanelChecks`의 기술연구소 검사는 `KNIGHT_RESEARCH` 파싱과 잘못된 상태 거부, 지휘관·아군·완공 조건, 서버 제공 티어·목재·기름·시간·효과 수치, Q와 직접 요청의 동일한 잠금, 같은 팀의 중복 연구 차단, 완료·취소·진행 표시, 재접속·초기화를 확인합니다. 요청만으로 자원을 미리 차감하거나 연구 상태를 앞서 변경하지 않는지도 검사합니다. 입력은 임의 서버 값이며 실제 밸런스를 고정하지 않습니다.

`UnitSceneChecks`의 스팀돌격병 검사는 `KNIGHT_CHARGE`의 틱·대상·좌표 파싱, 잘못된 타입·중복 스냅샷 거부, 고정 앵커 방향과 현재 시야의 표적 연결, 진행 중 재접속·재등장 복원, 과거 발사음 재생 방지, 만료·중단·숨김·죽음·팀 변경 시 정리를 확인합니다. 표시 패킷이 유닛 위치를 직접 이동시키지 않는지도 확인하며 실제 자동 발동·경로·충돌·전투 성능은 서버 검사 범위입니다.
