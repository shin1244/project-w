# 효과음

스타크래프트 1처럼 짧고 세게 때리는 소리를 목표로 합니다. 이 게임의 대표 소리는 **압력이 빠지는 증기**라, 무기·기계·건물 소리 대부분이 "치이익" 꼬리로 끝납니다. 모든 소리는 `tools/synth_sfx.py`로 합성하며 녹음 소스는 쓰지 않습니다. 소리는 화면 표시만 바꾸고 서버 메시지와 판정은 그대로입니다.

## 소리 만들기

```text
python tools/synth_sfx.py            # audio/sfx/<이름>-<번호>.wav를 다시 씁니다 (numpy 필요)
python tools/synth_sfx.py --preview  # audio/sfx-preview.html에서 모두 들어 봅니다 (Git 제외)
```

22 kHz 모노 16비트이며 강한 포화로 거칠게 만듭니다. 소리마다 시드가 고정돼 같은 파일이 나옵니다. 자주 나는 소리는 변형을 2~3개 두고 재생할 때 하나를 고릅니다. 이름·변형 수를 바꾸면 `audio/SoundBank.cs`의 표도 함께 바꾸고, Godot에서 가져오기를 다시 해 `.import` 파일을 커밋합니다.

## 재생 규칙

`audio/Sfx.cs`(자동 로드 `Sfx`)가 재생합니다.

- **화면 기준 거리:** 전장 소리는 화면 안에서는 원래 크기이고, 화면 밖으로 반 화면의 2.2배 거리까지 점점 작아지다 들리지 않습니다. 줌에 따라 들리는 범위도 함께 바뀌며, 좌우 위치에 따라 소리가 치우칩니다.
- **동시 재생 제한:** 소리마다 동시에 낼 수 있는 수(`MaxVoices`)와 최소 간격(`MinGap`)이 있어 큰 교전에서도 소리가 뭉개지지 않습니다. 전장 소리는 24개, 화면 소리는 6개까지 동시에 나고, 모두 차면 우선순위(`Priority`)가 같거나 낮은 가장 오래된 소리만 끊습니다.
- **변화:** 재생마다 변형 하나를 고르고 음높이를 조금(기본 ±5%) 흔듭니다.
- **음량 버스:** `default_bus_layout.tres`의 `SFX`(압축기), `UI`, `Music` 버스가 `Master`(리미터)로 모입니다. 설정 화면이 생기면 버스 음량을 조절합니다.
- 헤드리스 실행에서는 같은 규칙을 거치되 실제 재생은 하지 않습니다.

## 언제 나는가

| 상황 | 소리 | 연결 위치 |
| --- | --- | --- |
| 공격·작업 한 번 (`STATE`의 공격 번호가 바뀔 때) | 공압사수 `rifle`, 스팀돌격병·근접 미니언 `sword`, 사령관 `pistol`, 레인저·원거리 미니언 `bow`, 화염기계 `flame`, 늑대·멧돼지 `claw`, 골렘·고목 `stone`, 일꾼 벌목 `chop`·건설 `hammer` | `Unit.ApplyServerState`, `SoundBank.Swing` |
| 증기포격차 발사·착탄 | `artillery-fire`, `shell-impact` | `WorkshopMachineVisual`, `WorkshopShellFlight` |
| 증기포격차 전개·해제 시작 | `artillery-deploy`, `artillery-pack` | `Unit.ApplySiege` |
| 와이어 돌진 시작 | `knight-charge` | `Unit.ApplyKnightCharge` |
| 영웅 스킬 (`SKILL`) | `skill-cast`, 궁극기 `skill-ultimate`, 사령관 첫 스킬 `tower-shot` | `Main` |
| HQ·요새·감시탑 사격 | `tower-shot` | `Building.ApplyServerState` |
| 공성추 충돌 | `ram-impact` | `UnitManager.HandleRamImpact` |
| 사망 (`REMOVE`) | 사람 `death-soldier`, 기계 `death-machine`, 늑대·멧돼지 `death-beast`, 골렘·고목 `death-heavy` | `UnitManager.HandleRemove`, `SoundBank.Death` |
| 완성된 건물 제거 | `building-collapse` (공사 취소는 소리 없음) | `BuildingManager.HandleRemove` |
| 내 건물 공사 시작·완성 | `build-start`, `build-complete` | `BuildingManager.HandleConstruction` |
| 내 유닛 생산 완료 | `unit-ready` | `UnitManager.HandleSpawn` |
| 내 명령 전송 | 이동·채집·건설 등 `order-move`, 공격 `order-attack` | `Main.SendCommand`, `SoundBank.Order` |
| 내 유닛 선택 | `select` | `Main.SelectUnit`·`SelectBox` |
| 모든 버튼 | `ui-click` | `Sfx`가 트리에 들어오는 버튼마다 연결 |
| 요청 거절 (`ERR`) | `ui-error` | `Main` |

접속 직후 받는 초기 스냅샷(첫 `TICK` 전)은 지난 일이므로 공사·생산 소리를 내지 않고, 공격은 첫 `STATE` 이후 번호가 바뀔 때만 냅니다. 보이지 않는 유닛·건물의 소리는 내지 않습니다.

## 확인

`./tests/run.ps1 -Check SoundChecks`가 서버 없이 위 규칙(초기 스냅샷 무음, 공격 1회, 최소 간격, 화면 밖 거리, 내 건물 공사만 알림, 공사 취소 무음, 생산 완료, 사망 종류, 전개, 오류, 동시 재생 제한)을 확인합니다.
