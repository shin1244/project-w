# Project W

Godot .NET 클라이언트 프로토타입입니다. Go 서버가 유닛을 생성하고 이동을 계산하며, 클라이언트는 서버 상태를 표시합니다.

유닛 이동은 서버 20Hz 기준 2틱(100ms) 지연 보간합니다. 모든 유닛이 같은 재생 시점을 사용하며, 패킷이 늦으면 마지막 확정 위치에서 기다립니다. [이동 보간과 메시지 형식](docs/units.md#이동-보간)을 참고하세요.

## 폴더

```text
game/             메인 게임 장면, 입력·통신 연결과 메시지 분기
input/            마우스 입력과 맵 카메라
network/          TCP 연결과 명령 문자열
units/            UnitManager, 공통 유닛 스크립트와 일꾼·기사·궁수 씬
resources/        ResourceManager, 자원 공통 스크립트와 나무 외형 3종
buildings/        4×4 회관·3×3 포탑·2×2 서플라이·3×2 병영 씬과 메시
maps/             맵 장면과 서버와 공유하는 격자 데이터(test.json)
  resources/      지형 메시와 충돌 리소스
previews/         서버 없이 보는 맵·유닛 미리보기
tools/            맵 생성 도구
tests/            유닛·입력·맵 검증
docs/             설명과 미리보기 이미지
```

각 스크립트 옆의 `.uid`는 Godot의 리소스 식별 파일입니다. 스크립트와 함께 유지합니다. `.godot/`은 자동 생성 캐시이며 Git에서 제외합니다.

## 실행

1. Godot .NET에서 `project.godot`를 엽니다.
2. Go 서버를 `127.0.0.1:7777`에서 실행합니다.
3. C#을 빌드하고 F5로 실행합니다. 시작 장면은 `game/Main.tscn`입니다.
4. 내 유닛 좌클릭으로 단일 선택, 좌클릭 드래그로 여러 유닛 선택, 우클릭으로 선택한 유닛 전체에 이동 요청. 빈 곳이나 상대 유닛을 좌클릭하면 선택이 해제됩니다.

드래그 박스에 몸통 중심이 들어온 내 유닛을 최대 64개 선택합니다. 일반 클릭·드래그는 이전 선택을 대체하며, 빈 박스는 선택을 해제합니다. Shift+클릭은 추가/제외, Shift+드래그는 새 유닛이 있으면 추가하고 박스 안이 전부 선택돼 있으면 제외합니다. 더블클릭은 화면 안의 같은 종류인 내 유닛을 선택하며, Shift+더블클릭은 기존 선택에 추가합니다. 드래그 중 Esc 또는 우클릭으로 취소할 수 있습니다.

Ctrl+1~9로 선택한 부대를 저장하고, 숫자로 다시 선택합니다. 같은 숫자를 빠르게 두 번 누르면 부대 중심으로 카메라를 옮깁니다. 미니맵 좌클릭·드래그는 카메라 이동, 우클릭은 선택 부대 이동이며 밝은 사각형으로 현재 카메라 범위를 표시합니다. 서버 변경 없이 기존 이동 명령을 사용합니다.

하단 미니맵과 명령 패널 사이에 높이 186px의 선택 상세정보를 표시합니다. 단일 선택은 이름·초상화·체력·행동 상태, 다중 선택은 개별 초상화·체력바를 보여줍니다. 초상화 클릭은 단일 선택, Shift+클릭은 제외, Ctrl+클릭은 같은 종류만 선택합니다. 많은 유닛은 페이지 버튼으로 확인하며 전체 선택은 유지됩니다.

선택 후 적 유닛·건물을 우클릭하거나 A를 누르고 적을 좌클릭하면 `ATTACK 대상ID 내유닛ID...`를 보냅니다. A를 누르면 십자 커서가 표시되고, 한 번 클릭하거나 Esc를 누르면 종료됩니다. A 이후 빈 땅을 좌클릭하면 공격 이동(`ATTACK_MOVE`), 같은 진영 유닛·건물은 명령 없이 종료합니다. 땅 우클릭은 일반 이동입니다. 적 판정은 서버가 보낸 진영으로 구분하며 공격 판정은 서버에서 처리합니다.

건물을 좌클릭하면 유닛 선택이 해제되고 건물 체력바를 확인할 수 있습니다. 회관 2개와 포탑 8개는 서버 배치대로 표시하며, 포탑의 조준·발사·피해·파괴는 서버 STATE/HP/REMOVE에 맞춰 반영합니다.

전장의 안개는 같은 진영 유닛·미니언·건물의 시야를 공유합니다. 나무·벽·다른 건물은 시야를 막으며, 나무 제거와 건물 파괴 시 뒤쪽 시야가 열립니다. 시야 밖 적은 HIDE로 사라지고, 다시 보이면 최신 상태로 등장합니다. 시야 반경은 서버 SIGHT 메시지로 받고, 화면에서는 0.75칸 작은 부드러운 원을 장애물 경계에서 잘라 표시합니다. 최신 서버와 클라이언트를 함께 사용하세요.

맵 카메라는 W/S(위/아래), Q/D(왼쪽/오른쪽), 방향키·가운데 드래그로 이동, 휠로 확대/축소, Home으로 전체 보기입니다. A는 공격 대상 지정에 사용합니다.

서버 없이 외형을 확인하려면 `previews/Units.tscn`, `previews/Trees.tscn`, `previews/Arena.tscn`, `previews/Forest.tscn`, `previews/TownHall.tscn`을 열고 F6으로 실행합니다. 새 서플라이·병영은 `previews/ProductionBuildings.tscn`에서 함께 보고 회전·확대할 수 있습니다. 두 건물의 인구수·생산 기능은 서버 연동 전입니다.

나무는 `resources/trees/Tree.tscn`을 Oak/Pine/Birch 씬이 상속합니다. 자원 ID와 선택 표시(`ResourceNode.cs`), 클릭 영역(물리 레이어 3)은 공통이며 `Visual` 아래 외형만 다릅니다. `VisualVariant`는 외형 번호로, 자원 종류나 채집 능력치를 구분하지 않습니다. 나무는 맵 파일에서 생성하며 서버는 남은 자원량과 제거 변경분만 보냅니다. 유닛 선택 후 나무를 우클릭하면 `GATHER 자원ID 내유닛ID...`를 보냅니다. 실제 채집 가능 여부는 서버가 검사합니다.

## 코드 역할

- `game/Main.cs`: PlayerInput 이벤트를 관리자에 연결하고, 서버 메시지를 분기합니다. 관리자가 요청한 명령을 NetClient로 전송합니다.
- `units/UnitManager.cs`: 유닛 목록, UNIT/POS/REMOVE 적용, 선택, 소유권 확인, MOVE/ATTACK/GATHER 명령 생성을 담당합니다. 생성한 유닛은 Main/Units 아래에 둡니다.
- `resources/ResourceManager.cs`: 자원 ID별 나무 생성·갱신(`SpawnOrUpdate`), 조회(`TryGetResource`), 제거(`Remove`)를 담당합니다. 생성한 나무는 Main/Resources 아래에 둡니다.
- `units/Unit.cs`, `resources/ResourceNode.cs`: 유닛 한 개·자원 한 개의 ID와 화면 표시를 담당합니다.

맵은 `maps/MapWorld.cs`가 JSON에서 지형과 나무를 생성합니다. 서버와 SHA-256을 확인한 뒤 변경된 나무만 `TREE id amount`로 적용하며, 0이면 제거합니다. 맵 불일치 시 게임 진행을 막습니다. 양쪽에 같은 `maps/test.json`을 두세요.

## 문서와 검증

- [부대 지정·선택 확장·미니맵 조작](docs/rts-controls.md)
- [선택 상세정보 창과 다중 선택 조작](docs/selection-details.md)
- [보유 자원 UI와 STOCK 메시지](docs/stock.md)
- [미니언과 진영 동기화](docs/minions.md)
- [아군·적군 의상과 건물 지붕 색상](docs/team-colors.md)
- [회관·포탑과 건물 전투 동기화](buildings/README.md)
- [전장의 안개 동기화와 서버 성능 측정](docs/fog-of-war.md)
- [유닛과 메시지 형식](docs/units.md)
- [맵 구성과 재생성](docs/map.md)
- [맵 해시와 나무 변경분 동기화](docs/map-sync.md)

```text
dotnet build
Godot --headless --path . res://tests/UnitSceneChecks.tscn
Godot --headless --path . res://tests/MapSyncChecks.tscn
Godot --headless --path . --script tests/check_arena.gd
```

`Godot`은 설치한 Godot .NET 실행 파일 경로로 바꿉니다. 검증은 실제 서버에 접속하지 않습니다.
