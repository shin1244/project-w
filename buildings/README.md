# 건물

| 씬 | 타입 | 점유 크기 | 높이 | 외형 |
| --- | --- | --- | --- | --- |
| `TownHall.tscn` | 0 | 4×4 | 약 4.78 | 석조 기단·목조 회관·푸른 슬레이트 지붕·종탑 |
| `Tower.tscn` | 1 | 3×3 | 약 4.47 | 석조 망루·총안·진영 깃발·상부 대형 쇠뇌 |

둘 다 루트 스케일은 1, 바닥은 Y=0, 정면은 -Z입니다. 메시 자체가 점유 크기에 맞춰져 있으며 지붕·쇠뇌를 포함해 X/Z 점유 영역을 넘지 않습니다. 선택용 Area3D는 물리 레이어 4(값 8), footprint 메타데이터는 각각 Vector2i(4,4), Vector2i(3,3)입니다.

회관 입구 Entrance는 (0,0,-2)에 있습니다. 중심이 현재 본진 좌표 (-65,0), (65,0)이므로 서버 충돌도 중심 ±2의 4×4입니다. 서버의 일꾼 생성 위치와 자원 반납 위치는 이 크기로 계산합니다.

포탑은 라인에 여러 개 배치할 수 있는 재사용 씬입니다. Visual은 고정 석조 구조이며, Turret/Ballista는 독립된 회전부입니다. Turret/Muzzle은 발사 위치 표시점입니다. 정지 상태의 쇠뇌는 회전축으로부터 반경 1.5 안에 있어 어느 방향으로 돌려도 3×3 안에 머뭅니다.

현재 서버는 진영별 상·하단 라인에 각각 2기씩, 총 8기의 포탑을 생성합니다. `maps/test.json`의 `towers` 배치를 포함한 파일 전체를 서버와 동일하게 유지합니다. 현재 SHA-256은 `70a3d46d09bb0af92eae07f5f50908812795d4d47bdfb3527b0d2f766ccfefcf`입니다. 클라이언트는 `towers`로 건물을 자체 생성하지 않고 서버 스냅샷을 기다립니다.

## 표시와 서버 연결

```text
BUILDING type id sideId x z yaw
HP id current maximum
STATE id IDLE|ATTACK 0 targetId swingSequence
REMOVE id
```

BuildingManager는 서버의 타입 0을 회관, 타입 1을 포탑으로 표시합니다. 접속 시 클라이언트가 자체적으로 건물을 생성하지 않습니다. 현재 서버 최대 체력은 회관 1000, 포탑 1500이며 UI는 수치를 고정하지 않고 HP 메시지를 사용합니다.

sideId는 진영이며 접속 플레이어 ID와 다릅니다. banner 재질은 건물 인스턴스별로 복제하여 1진영 파랑, 2진영 빨강으로 표시합니다. 같은 ID의 중복 스냅샷은 위치와 진영을 갱신하고, 체력은 서버 HP, 제거는 REMOVE로 반영합니다. 건물은 REMOVE 수신 즉시 화면과 대상 조회에서 제거합니다.

STATE는 유닛과 포탑이 공유합니다. 포탑은 서버의 targetId를 따라 쇠뇌만 수평 회전하고, swingSequence가 바뀔 때 반동과 짧은 금색 발사 궤적을 한 번 표시합니다. 첫 스냅샷이나 중복 메시지로 지난 발사를 재생하지 않습니다. 대상 UNIT이 늦게 오면 다음 프레임부터 조준합니다. 피해·사거리·재장전·대상 선택은 서버가 계산하며, 화면의 궤적은 피해 판정과 별개입니다.

건물 좌클릭은 선택 표시와 체력바를 켜고 유닛 선택을 해제합니다. 내 유닛을 선택한 상태에서 적 건물을 우클릭하거나 A → 좌클릭하면 `ATTACK 건물ID 내유닛ID...`를 보냅니다. 같은 진영 건물은 공격할 수 없으며 우클릭은 이동입니다. 유닛의 공격 대상도 건물 ID를 조회하므로 건물을 바라봅니다. 생산 UI와 승패 처리는 아직 없습니다.

## 미리보기

- `previews/Buildings.tscn`: 두 건물을 동일한 화면 배율로 비교.
- `previews/TownHall.tscn`: 회관. 좌클릭 드래그 회전, 휠 확대/축소.
- `previews/Tower.tscn`: 포탑. 좌클릭 드래그 회전, 휠 확대/축소.

Godot에서 장면을 열고 F6으로 실행합니다. 금색 테두리는 점유 영역, 격자는 1×1입니다. `-- --capture`로 실행하면 docs/images/에 미리보기 이미지를 저장합니다.

## 재생성과 검증

C# 프로젝트를 빌드한 뒤 실행합니다. Godot은 설치된 .NET 실행 파일 경로로 바꿉니다.

```text
Godot --headless --path . --script tools/build_town_hall.gd
Godot --headless --path . --script tools/build_tower.gd
Godot --headless --path . res://tests/BuildingSceneChecks.tscn
Godot --headless --path . res://tests/TowerSyncChecks.tscn
```

생성 도구는 해당 씬과 buildings/meshes/의 메시를 덮어쓰므로 외형 수정은 생성 스크립트에 반영합니다. 포탑 도구는 회관 도구의 메시 생성 함수를 재사용합니다. 검증은 메시·선택 영역 크기, 진영 색 분리, 쇠뇌의 회전 범위, 서버 메시지 연결·갱신·제거·재접속과 건물 공격 입력을 확인합니다. 실제 서버 검증 장면은 `tests/ServerConnectionChecks.tscn`입니다.
