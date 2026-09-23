# 서버가 생성하는 유닛

- `units/Worker.tscn`: 타입 0. 작업복, 모자, 배낭, 벌목용 도끼가 있는 임시 일꾼 모델.
- `units/Knight.tscn`: 타입 1. 갑옷, 검, 방패가 있는 임시 기사 모델.
- `units/Archer.tscn`: 타입 2. 녹색 옷, 활, 화살통이 있는 임시 궁수 모델.
- `previews/Units.tscn`: 세 모델을 서버 없이 확인하는 장면. Godot에서 열고 F6.

세 씬은 `units/Unit.tscn`을 상속합니다. 공통 ID·위치 반영은 `units/Unit.cs`, 외형은 각 씬의 `Visual` 아래에 있습니다. 새 모델이 생기면 `Visual` 아래만 바꾸면 됩니다. 루트 원점은 발밑, 정면은 -Z입니다.

`Unit.cs`에는 이동 속도나 공격 판정을 넣지 않습니다. 서버의 X/Z 위치와 행동 상태를 표시하며 보간과 걷기 애니메이션은 아직 없습니다. 일꾼 외형의 애니메이션은 `WorkerAnimation.cs`가 맡습니다.

## 메시지

```text
WELCOME 플레이어ID
UNIT 타입 유닛ID 소유자ID X Z
POS 유닛ID X Z
STATE 유닛ID IDLE|GATHER|ATTACK 운반량 대상ID 공격횟수
REMOVE 유닛ID
```

Main은 UNIT/POS/STATE/REMOVE 메시지를 UnitManager로 전달합니다. UnitManager는 UNIT을 받았을 때만 Main/Units 아래에 유닛을 만듭니다. 같은 ID는 중복 생성하지 않고 정보를 갱신합니다. 유닛 타입은 ID가 유지되는 동안 동일하다고 가정합니다.

REMOVE를 받으면 선택과 클릭 판정에서 즉시 제외하고, 모델은 공통 사망 연출(옆으로 넘어짐 → 서서히 사라짐)을 마친 후 지웁니다. 채집/공격 요청의 대상에는 각각 노란/빨간 원을 표시합니다. 자세한 동작과 미리보기는 [사망 연출과 명령 대상 표시](combat-feedback.md)를 참고하세요.

내 유닛을 좌클릭하면 선택 원이 켜집니다. 좌클릭 드래그는 박스에 몸통 중심이 들어오는 내 유닛을 최대 64개 선택합니다. 새 선택은 기존 선택을 대체합니다. 빈 박스, 빈 곳 클릭, 다른 플레이어의 유닛 클릭은 선택을 해제합니다. 우클릭은 대상을 확인하여 MOVE, ATTACK, GATHER 중 하나를 선택 목록 전체에 요청합니다. 생성 시에는 자동 선택하지 않습니다.

PlayerInput은 UI에서 처리하지 않은 좌클릭으로 선택을 시작합니다. 6픽셀 이상 움직이면 드래그이며, 버튼을 뗄 때 단일 선택 또는 박스 선택을 확정합니다. 시작한 드래그는 UI 위에서 버튼을 떼어도 종료하고, Esc·우클릭·창 포커스 상실 시 취소합니다. SelectionBox는 입력을 가로채지 않고 화면에 박스만 그립니다.

광선과 박스 선택 요청은 같은 큐에 보관해 물리 프레임에서 순서대로 처리합니다. 선택용 Area3D는 충돌 레이어 2입니다. 소유권 확인과 선택 목록은 UnitManager가 담당하며, 유닛이 삭제되거나 소유자가 바뀌면 목록에서도 제외합니다. 우클릭 목적지는 Y=0 평면 기준이며 맵 경계·통행 가능 여부는 서버에서 검사합니다.

UnitManager는 NetClient를 직접 참조하지 않습니다. MOVE/ATTACK/GATHER 문자열을 만든 뒤 CommandRequested 이벤트로 전달하면 Main이 전송합니다. 씬의 유닛 리소스와 카메라는 Units 노드의 인스펙터에 연결되어 있습니다.

## 공격 요청 (클라이언트 → 서버)

```text
ATTACK 대상ID 내유닛ID...
ATTACK 202 101 303
```

위 예시는 선택한 101, 303번 유닛에게 202번 유닛을 공격하도록 요청합니다. `NetClient.Send`가 줄바꿈을 붙입니다. 선택 상한은 이동과 동일하게 64개입니다. 서버에서는 대상 존재 여부, 공격 가능 여부, 명령을 보낸 플레이어의 유닛 소유권을 검증한 뒤 처리해야 합니다. 공격·추적·피해 계산은 아직 클라이언트에 구현하지 않았습니다.

적 우클릭과 A → 적 좌클릭은 같은 요청을 보내며 선택 목록은 유지합니다. 팀 정보가 없으므로 현재는 소유자 ID가 다르면 적으로 판단합니다. A 모드는 클릭 한 번으로 종료하며, 빈 땅이나 내 유닛을 좌클릭하면 아무 명령도 보내지 않습니다. Esc 또는 창 포커스 상실로 취소할 수 있습니다. A 모드에서 우클릭하면 모드를 종료하고 일반 우클릭 동작(적 공격/나무 채집/땅 이동)을 수행합니다. 선택한 유닛이 없거나 대상이 삭제되었으면 공격 요청을 보내지 않습니다.

## 검증

```text
dotnet build
Godot --headless --path . res://tests/UnitSceneChecks.tscn
```

Godot은 설치된 .NET 버전 실행 파일 경로로 바꿉니다. 검증 장면은 실제 서버에 접속하지 않고 UNIT/POS/REMOVE 메시지를 전달합니다.

## 일반 우클릭과 채집

PlayerInput의 `ContextClicked(Node3D target, Vector3 point)` 이벤트를 Main에서 `Units.RequestContextOrder`에 연결합니다. 대상은 Unit, ResourceNode, 또는 null(땅)입니다. 우클릭은 유닛과 자원의 클릭 레이어를 한 번에 광선 조회하므로 화면에서 먼저 닿은 대상을 사용합니다. 좌클릭 유닛 선택과 A 공격 지정은 기존 유닛 전용 조회를 유지합니다.

```text
PlayerInput.HandleRightClick
 → ContextClicked?.Invoke(target, point)
 → UnitManager.RequestContextOrder
   - 적 유닛 → RequestAttack
   - 나무 → RequestGather
   - 땅 / 내 유닛 → RequestMove
 → CommandRequested
 → Main.SendCommand
```

`+=`는 이벤트에 실행할 함수를 등록하고, `Invoke`는 등록된 함수들을 호출합니다. `?.Invoke`는 등록된 함수가 없으면 건너뜁니다. 이벤트 자체가 별도 스레드를 만들지는 않으며, 현재는 물리 프레임에서 광선 조회를 마친 자리에서 연결된 함수를 실행합니다.

```text
GATHER 자원ID 내유닛ID...
GATHER 9900 808 809
```

선택한 유닛이 없거나 자원이 삭제·소진되었으면 보내지 않습니다. 혼합 선택은 선택한 내 유닛 ID를 그대로 보내고, 서버가 소유권·채집 능력·이동 능력·대상 자원을 검사합니다. 클라이언트에서 자원량을 예측해 깎지 않으며 기존 TREE 메시지로 화면을 갱신합니다. 건물 우클릭은 아직 연결하지 않았습니다.
