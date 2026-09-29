# 건물

| 씬 | 타입 | 점유 크기 | 높이 | 외형 |
| --- | --- | --- | --- | --- |
| `TownHall.tscn` | 0 | 4×4 | 약 4.78 | 석조 기단·목조 회관·푸른 슬레이트 지붕·종탑 |
| `Tower.tscn` | 1 | 3×3 | 약 4.47 | 석조 망루·총안·진영 깃발·상부 대형 쇠뇌 |
| `Supply.tscn` | 2 (에셋 임시 번호) | 2×2 | 약 2.40 | 목조 보급 창고·곡물 문장·환기탑·상자·곡물 자루·술통 |
| `Barracks.tscn` | 3 (에셋 임시 번호) | 3×2 | 약 3.22 | 석조 기단·목조 훈련소·방패와 교차 검·무기 거치대·훈련 허수아비 |

모두 루트 스케일은 1, 바닥은 Y=0, 정면은 -Z입니다. 메시 자체가 점유 크기에 맞춰져 있으며 지붕·장식·보급품을 포함해 X/Z 점유 영역을 넘지 않습니다. 선택용 Area3D는 물리 레이어 4(값 8), `footprint` 메타데이터는 표의 가로(X)·깊이(Z)입니다. 선택 원은 직사각형 대각선 길이에 맞춰 계산합니다.

서플라이와 병영은 인구 수용량 확장·1티어 유닛 훈련을 나타내는 외형 에셋입니다. 인구 증가량, 생산 유닛 목록·비용·시간은 설정하지 않았으며 서버 연결과 게임 내 배치는 후속 작업입니다. 씬의 타입 2·3은 임시 번호이며 BuildingManager의 서버 타입 매핑에는 아직 등록하지 않았습니다. 서버 연동 시 실제 타입 번호를 맞추고 병영의 3×2 직사각형 점유 영역을 서버 충돌 및 클라이언트 시야 가림 계산에도 적용해야 합니다.

새 건물은 장식을 포함해 하나의 ArrayMesh로 묶었습니다. 서플라이는 4,865개, 병영은 6,327개의 삼각형이며 각각 재질 13개를 사용합니다. 재질의 `banner` 영역은 기존 건물과 동일하게 인스턴스별 진영색을 지원합니다. 두 건물 모두 Entrance는 (0,0,-1), 체력바 위치는 전체 모델 높이 위에 있습니다.

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

sideId는 진영이며 접속 플레이어 ID와 다릅니다. 지붕과 banner 재질은 건물 인스턴스별로 복제하여 내 진영은 옅은 파랑, 상대 진영은 옅은 붉은색으로 표시합니다. 내 진영이 2여도 아군은 파랑이며 석재·목재는 원래 색을 유지합니다. [진영 색상](../docs/team-colors.md)을 참고하세요. 같은 ID의 중복 스냅샷은 위치와 진영을 갱신하고, 체력은 서버 HP, 제거는 REMOVE로 반영합니다. 건물은 REMOVE 수신 즉시 화면과 대상 조회에서 제거합니다.

STATE는 유닛과 포탑이 공유합니다. 포탑은 서버의 targetId를 따라 쇠뇌만 수평 회전하고, swingSequence가 바뀔 때 반동과 짧은 금색 발사 궤적을 한 번 표시합니다. 첫 스냅샷이나 중복 메시지로 지난 발사를 재생하지 않습니다. 대상 UNIT이 늦게 오면 다음 프레임부터 조준합니다. 피해·사거리·재장전·대상 선택은 서버가 계산하며, 화면의 궤적은 피해 판정과 별개입니다.

건물 좌클릭은 선택 표시와 체력바를 켜고 유닛 선택을 해제합니다. 내 유닛을 선택한 상태에서 적 건물을 우클릭하거나 A → 좌클릭하면 `ATTACK 건물ID 내유닛ID...`를 보냅니다. 같은 진영 건물은 공격할 수 없으며 우클릭은 이동입니다. 유닛의 공격 대상도 건물 ID를 조회하므로 건물을 바라봅니다. 회관의 생산 버튼은 [명령 패널](../docs/command-panel.md)을 참고하세요.

## 미리보기

- `previews/ProductionBuildings.tscn`: 서플라이·병영을 동일한 화면 배율로 비교. 드래그 회전, 휠 확대/축소.
- `previews/Supply.tscn`: 2×2 서플라이 개별 미리보기.
- `previews/Barracks.tscn`: 3×2 병영 개별 미리보기.
- `previews/Buildings.tscn`: 기존 회관·포탑을 동일한 화면 배율로 비교.
- `previews/TownHall.tscn`: 회관. 좌클릭 드래그 회전, 휠 확대/축소.
- `previews/Tower.tscn`: 포탑. 좌클릭 드래그 회전, 휠 확대/축소.

Godot에서 장면을 열고 F6으로 실행합니다. 금색 테두리는 점유 영역, 격자는 1×1입니다. `-- --capture`로 실행하면 docs/images/에 미리보기 이미지를 저장합니다.

![서플라이와 병영](../docs/images/production-buildings-preview.png)

## 재생성과 검증

C# 프로젝트를 빌드한 뒤 실행합니다. Godot은 설치된 .NET 실행 파일 경로로 바꿉니다.

```text
Godot --headless --path . --script tools/build_town_hall.gd
Godot --headless --path . --script tools/build_tower.gd
Godot --headless --path . --script tools/build_supply.gd
Godot --headless --path . --script tools/build_barracks.gd
Godot --headless --path . res://tests/ProductionBuildingChecks.tscn
Godot --headless --path . res://tests/BuildingSceneChecks.tscn
Godot --headless --path . res://tests/TowerSyncChecks.tscn
```

생성 도구는 해당 씬과 buildings/meshes/의 메시를 덮어쓰므로 외형 수정은 생성 스크립트에 반영합니다. 포탑·서플라이·병영 도구는 회관 도구의 메시 생성 함수를 재사용합니다. 검증은 메시·선택 영역 크기, 진영 색 분리, 직사각형 선택 원, 쇠뇌의 회전 범위, 서버 메시지 연결·갱신·제거·재접속과 건물 공격 입력을 확인합니다. 실제 서버 검증 장면은 `tests/ServerConnectionChecks.tscn`입니다.
