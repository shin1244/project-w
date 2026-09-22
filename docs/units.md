# 서버가 생성하는 유닛

- `units/Knight.tscn`: 타입 0. 갑옷, 검, 방패가 있는 임시 기사 모델.
- `units/Archer.tscn`: 타입 1. 녹색 옷, 활, 화살통이 있는 임시 궁수 모델.
- `previews/Units.tscn`: 두 모델을 서버 없이 확인하는 장면. Godot에서 열고 F6.

두 씬은 `units/Unit.tscn`을 상속합니다. 공통 ID·위치 반영은 `units/Unit.cs`, 외형은 각 씬의 `Visual` 아래에 있습니다. 새 모델이 생기면 `Visual` 아래만 바꾸면 됩니다. 루트 원점은 발밑, 정면은 -Z입니다.

`Unit.cs`에는 이동 속도나 공격 판정을 넣지 않습니다. 서버의 X/Z 위치를 그대로 표시하며 보간, 공격 애니메이션, 경로 탐색은 아직 없습니다.

## 메시지

```text
WELCOME 플레이어ID
UNIT 타입 유닛ID 소유자ID X Z
POS 유닛ID X Z
REMOVE 유닛ID
```

Main은 UNIT을 받았을 때만 유닛을 만듭니다. 같은 ID는 중복 생성하지 않고 정보를 갱신합니다. 유닛 타입은 ID가 유지되는 동안 동일하다고 가정합니다.

내 유닛을 좌클릭하면 선택 원이 켜집니다. 빈 곳이나 다른 플레이어의 유닛을 좌클릭하면 선택이 해제됩니다. 선택 후 우클릭하면 해당 유닛의 MOVE 요청을 전송합니다. 생성 시에는 자동 선택하지 않습니다.

PlayerInput은 UI에서 처리하지 않은 클릭만 받으며, 클릭 순간의 광선을 큐에 보관한 뒤 물리 프레임에서 순서대로 처리합니다. 선택용 Area3D는 충돌 레이어 2입니다. 소유권 확인과 선택 상태는 Main이 담당합니다. 우클릭 목적지는 기존처럼 Y=0 평면 기준이며 맵 경계·통행 가능 여부 검사는 아직 연결되지 않았습니다.

## 검증

```text
dotnet build
Godot --headless --path . res://tests/UnitSceneChecks.tscn
```

Godot은 설치된 .NET 버전 실행 파일 경로로 바꿉니다. 검증 장면은 실제 서버에 접속하지 않고 UNIT/POS/REMOVE 메시지를 전달합니다.
