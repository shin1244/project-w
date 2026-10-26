# 개발 안내

Burn Quarter의 Godot .NET 클라이언트를 실행하고 고치는 방법입니다. 게임 소개는 [README](../README.md)에 있습니다.

Godot .NET 클라이언트입니다. Go 서버가 유닛을 생성하고 이동·전투를 계산하며, 클라이언트는 서버 상태를 표시하고 입력을 명령으로 보냅니다.

| 저장소 | 역할 |
| --- | --- |
| `project-w` (이 저장소) | Godot(.NET) 클라이언트와 관전 도구 |
| `project-w-server` | 게임 서버·로비·규칙·RL 환경. **게임 규칙과 수치의 기준** |
| `project-w-ai` | Python PPO 학습·리그·평가·정책 내보내기 |

세 저장소를 같은 부모 폴더 아래에 둡니다. 서버 메시지 형식이 바뀌면 서버와 클라이언트를 함께 갱신해야 합니다.

## 실행

1. Godot .NET에서 `project.godot`를 엽니다.
2. C#을 빌드하고 F5로 실행하면 `lobby/Lobby.tscn`이 열립니다. 이름과 로비 서버 주소를 정해 게스트로 입장합니다. [지인 테스트 실행·Pi 배포 안내](friend-test.md)를 참고하세요.
3. 방 목록에서 방을 만들거나 들어가 자리 줄 왼쪽에서 **대표**(지휘관, 팀에 한 명) 또는 **용병**(영웅)을 고르고, 용병이면 오른쪽에서 어떤 용병인지 고릅니다. 방장이 시작하면 5초 뒤 로딩으로 넘어가고 빈자리는 AI가 맡습니다. 혼자 방을 만들어 시작하면 AI 7명과 연습합니다. 모든 사람의 맵 동기화가 끝나면 함께 시작하며 로딩에서 취소/Esc로 방에 돌아갈 수 있습니다.
4. 실제 전장을 실행하려면 Go 서버를 `127.0.0.1:7777`에서 실행합니다. RTS는 `game/Main.tscn`, AOS는 `game/AOS.tscn`을 열어 F6으로 실행합니다. AOS는 같은 전장을 상속하며 `MAP_READY <해시> HERO 200`으로 임시 영웅을 요청합니다. Main 루트의 `RequestedRole`·`RequestedHeroType`으로 접속 요청을 바꿀 수 있고 실제 역할·진영은 서버의 WELCOME으로 확정합니다. 내 유닛 좌클릭으로 단일 선택, 드래그로 여러 유닛 선택, 우클릭으로 이동·공격·채집·이어 짓기를 요청합니다.

유닛 이동은 서버 30Hz 기준 1.5틱(50ms) 지연 보간합니다. 모든 유닛이 같은 재생 시점을 사용하며, 패킷이 늦으면 마지막 확정 위치에서 기다립니다. [이동 보간과 메시지 형식](units.md#이동-보간)을 참고하세요.

## 학습된 AI 관전

`tools/Watch-Latest-AI.cmd`는 현재 리그(기본 `league6`, `-Run`으로 변경)에서 두 역할의 최신 체크포인트를 별도 관전 스냅샷으로 복사한 뒤
관전 실행본을 엽니다(기본 `../build-league5-watch`). `tools/Use-Trained-AI.ps1 -Latest`는 스냅샷만 준비합니다.
경로가 다른 경우 `-AIRepo`, `-ServerRepo`, `-RunsRoot`, `-OutputDirectory`를 지정하세요.
각각 `PROJECT_W_AI_ROOT`, `PROJECT_W_SERVER_ROOT`, `PROJECT_W_RUNS_ROOT` 환경 변수도 지원합니다.
`-ServerRepo`는 서버 소스 경로이며 모델은 AI 저장소에서 읽습니다.
관전용 `Project W.exe`·서버 바이너리와 모델 데이터는 Git 소스와 별도로 준비해야 합니다.

## README 교전 GIF

README의 교전 GIF는 서버 없이 `previews/BattleStage.tscn`에서 연출한 장면입니다. 실제 지도·건물·유닛 장면 위에 양 팀 병력을 세 곳(상단 라인, 중앙 광장, 하단 요새 공성)에 배치하고, 간단한 전투 시뮬레이션이 서버와 같은 메시지(UNIT·POS·STATE·HP·SKILL·SIEGE_STATE·DEATH)를 클라이언트에 넣습니다. 게임 규칙은 쓰지 않으며 카메라는 장면마다 정한 줌 인·아웃 동선을 따릅니다. 화면 UI는 숨겨서 글자 없이 찍습니다. 좌표는 지금 지도(170×130칸) 기준입니다. 지도를 다시 만들면 장면 좌표와 장면 4의 나무 ID도 손으로 맞춥니다.

- **작은 병력:** 한 편에 예닐곱 기만 두고 줄 간격을 넓혀, 근접 유닛이 한 덩어리로 엉키지 않게 합니다. 유닛은 먼저 같은 부류(영웅↔영웅, 미니언↔미니언, 용병·기계↔용병·기계)의 적을 고르므로 전선이 여러 개의 작은 싸움으로 나뉩니다. 장면 1·2의 영웅은 한쪽 가장자리에서 따로 맞붙습니다(`heroShift`).
- **증기포격차:** 교전 장면의 포격차는 병력 뒤에 고정해 서버의 전환 시간(`artilleryTransitionSeconds`) 동안 공성 모드로 펼친 뒤 포격합니다(`Gun`). 사거리 안에서 적이 가장 많이 모인 곳을 노리고, 포탄은 클라이언트의 포탄 비행 시간 뒤에 떨어져 서버와 같은 폭발 반경(`artilleryBlastRadius`) 안의 적 모두에게 피해를 줍니다. 한 화면에 포와 착탄이 함께 담기도록 사거리·피해는 서버보다 크게 둡니다.
- **사망 연출:** 마지막 타격의 무기로 서버의 `DEATH 유닛 원인`(`death_cause.go`)을 보내므로 칼·총·화염·포격에 따라 쓰러짐·폭사 연출이 다릅니다.

이동은 서버(`project-w-server`)의 규칙을 흉내 냅니다. 병력은 배치를 유지한 채 가장 느린 유닛의 속도로 나아갑니다(서버 `docs/world.md`의 이동 대형). 근접 유닛은 멈춰 선 적의 둘레에 공격 자리를 나눠 받고, 둘레가 차면 한 줄 바깥 대기 원에서 기다리거나 자리가 남은 다른 적으로 옮기며, 무리에 끼어 떨면 잠시 자리를 내놓습니다(`attack_slots.go`). 아군끼리는 밀어내고, 적과 싸우는 아군은 지나가지 못합니다(`move.go`). 사거리는 서버처럼 몸 가장자리 사이로 잽니다. 서버와 다른 점은 둘입니다. 걸어오는 적에게는 자리를 나누지 않고 마주 다가가며, 둘레를 도는 길이 양쪽 다 막혀도 서지 않고 무리를 따라 미끄러집니다. 길찾기가 없는 연출에서는 두 진영이 서로의 자리를 쫓아 함께 흘러가거나 뒷줄이 통째로 서 버렸기 때문입니다. 실제 서버로 같은 장면을 돌려 녹화하지 않는 이유는, 영웅 스킬·고정 포대처럼 규칙 없이 꾸민 장면을 서버에서 만들려면 장면 구성·메시지 녹화·재생기를 따로 만들어야 하기 때문입니다. 그래서 서버의 이동 규칙이 크게 바뀌면 `BattleStage.cs`도 손으로 맞춥니다.

장면 4~6은 README의 게임 설명 GIF입니다. 같은 시뮬레이션 위에 장면마다 짧은 대본을 얹고 카메라는 고정합니다. 4(`teamwork.gif`)는 영웅이 장비를 사면 파란 팀 부활 지점 옆에 상자(`ITEM_CRATE`)가 떨어지고, 일꾼이 상자를 들고 가 싸우는 영웅에게 건넵니다. 장비를 받은 영웅은 미니언을 곧바로 쓰러뜨립니다. 5(`magic-stone.gif`)는 중앙 마석(`TRIBUTE`)에 정신집중하던 빨간 팀 골렘이 파란 팀 공압사수에게 처음 피해를 받자 집중이 끊기고 달아나며, 공압사수가 마석 앞을 차지합니다. 6(`hq-siege.gif`)는 빨간 본진의 북·남 입구로 들어온 파란 병력이 HQ를 둘러싸고 공격하며, 두 입구 뒤의 포격차가 펼쳐져 HQ를 포격하고, HQ도 맞서 쏩니다. HQ는 벽을 덮는 원으로 보고 근접 유닛에게 둘레 자리를 나눕니다. 서버는 건물에 공격 자리를 나누지 않지만, 연출에서는 둘러싼 모습을 보여 주려고 나눕니다. 떠 있는 3D 글자(상자·부활 지점 이름표)도 UI처럼 숨깁니다.

장면 7~8(`death-fall.gif`·`death-blast.gif`)은 전투 시뮬레이션이 아니라 `previews/DeathEffects.tscn`에서 글자 없이 찍습니다. 일꾼·스팀돌격병·공압사수·화염기계가 7에서는 원인 없이 넘어지고 8에서는 포격(`DEATH ... BLAST`)으로 산산조각 납니다. `Godot --path . --resolution 1280x800 --fixed-fps 15 res://previews/DeathEffects.tscn -- --capture=<프레임 폴더>`가 둘을 한 번씩 찍어 `scene-7`·`scene-8`에 두고, 같은 묶음 도구로 묶습니다.

```text
Godot --path . --resolution 1280x800 --fixed-fps 15 res://previews/BattleStage.tscn -- --capture=<프레임 폴더>
python tools/pack-battle-gif.py <프레임 폴더> docs/images/store
```

일부 장면만 다시 찍으려면 `--capture=` 뒤에 `--scenes=4,5,6`처럼 장면 번호를 붙입니다. 묶음 도구는 프레임 폴더에 있는 장면만 묶습니다.

F6으로 열면 여섯 장면을 반복 재생합니다. 유닛 움직임은 실행마다 같고, 탑 연기 같은 입자만 조금씩 다릅니다. `tools/pack-battle-gif.py`(Pillow·NumPy 필요)는 프레임을 한 장씩 건너뛰어(약 7.5fps) 512×320으로 줄이고, 디더링 없이 127색 공용 팔레트로 바뀐 픽셀만 담은 GIF로 묶어 `docs/images/store/`에 장면 1~3은 `battle-1.gif`~`battle-3.gif`, 4~6은 `teamwork.gif`·`magic-stone.gif`·`hq-siege.gif`, 7~8은 `death-fall.gif`·`death-blast.gif`로 둡니다. 앞 프레임과 똑같은 프레임은 GIF가 앞 프레임에 합치므로 도구도 합쳐서 길이를 늘립니다. 팔레트는 옥트리 방식으로 뽑습니다. 중간 절단 방식은 팀 색이 바래고 파일도 더 큽니다. 카메라가 움직이는 교전 GIF는 한 개가 4MB 안팎이며(640×400에 디더링을 넣으면 7MB 가까이 됩니다), 카메라를 고정한 설명 GIF는 0.5MB 안쪽입니다. GIF는 Git 기록에 계속 남으므로 다시 찍을 때도 이 크기를 지킵니다. 레인저는 모델 오류로 아직 연출에 넣지 않았습니다.

## 폴더

```text
game/             메인 게임 장면, 입력·통신 연결과 메시지 분기
lobby/            게스트 입장·방 목록·방·실제 전장 로딩
input/            마우스 입력과 맵 카메라
network/          TCP 연결과 명령 문자열
events/           서버 상태로 표시하는 마석·장비 상자와 그 효과
units/            UnitManager, 공통 유닛 스크립트와 일꾼·기사·궁수 씬
resources/        ResourceManager, 자원 공통 스크립트와 나무 외형 4종
buildings/        HQ·요새·브랜치·숙소·시추탑·인력 사무소·비허가 실험실·감시탑·공장·기술연구소 씬, 공통 공사 부지 표시
maps/             맵 장면과 서버와 공유하는 격자 데이터(test.json)
  resources/      지형 메시와 충돌 리소스
previews/         서버 없이 보는 맵·유닛 미리보기
tools/            맵 생성 도구
tests/            유닛·입력·맵 검증
docs/             설명과 미리보기 이미지
```

각 스크립트 옆의 `.uid`는 Godot의 리소스 식별 파일입니다. 스크립트와 함께 유지합니다. `.godot/`은 자동 생성 캐시이며 Git에서 제외합니다.

## 코드 역할

- `game/Main.cs`: PlayerInput 이벤트를 관리자에 연결하고, 서버 메시지를 분기합니다. 관리자가 요청한 명령을 NetClient로 전송합니다.
- `units/UnitManager.cs`: 유닛 목록, UNIT/POS/REMOVE 적용, 선택, 소유권 확인, MOVE/ATTACK/GATHER/BUILD/CONSTRUCT 명령 생성을 담당합니다. 생성한 유닛은 Main/Units 아래에 둡니다.
- `buildings/BuildingManager.cs`, `buildings/ConstructionSite.cs`: 건물 타입 0~6과 서버 공사 진행률을 반영하고, 공통 테두리·팻말과 완성 모델을 전환합니다.
- `resources/ResourceManager.cs`: 자원 ID별 나무 생성·갱신(`SpawnOrUpdate`), 조회(`TryGetResource`), 제거(`Remove`)를 담당합니다. 생성한 나무는 Main/Resources 아래에 둡니다.
- `units/Unit.cs`, `resources/ResourceNode.cs`: 유닛 한 개·자원 한 개의 ID와 화면 표시를 담당합니다.

맵은 `maps/MapWorld.cs`가 JSON에서 지형과 나무를 생성합니다. 서버와 SHA-256을 확인한 뒤 변경된 나무만 `TREE id amount`로 적용하며, 0이면 제거합니다. 맵 불일치 시 게임 진행을 막습니다. 양쪽에 같은 `maps/test.json`을 두세요.

## 문서

현재 동작은 주제별 문서에 있고, 변경 이력은 [CHANGELOG.md](../CHANGELOG.md)에 날짜순으로 적습니다. 작업 규칙은 [AGENTS.md](../AGENTS.md)에 있습니다.

- [아트 디렉션: 증기, 마법 공학, 소모되는 사람들](art-direction.md): 세계관 레퍼런스, 인명경시 표현, 질감·분위기 규칙과 작업 순서
- [게임 모드와 화면](game-modes.md): 3 대 3, 팀 레벨, 로비, 승패, 마석, 미니언 편성, HQ 티어
- [영웅 화면과 조작](heroes.md): AOS 패널, 늑대·골렘·사령관 모델과 미리보기
- [RTS 조작](controls.md): 선택, 공격, 정지·홀드, 카메라, 건설
- [부대 지정·선택 확장·미니맵 조작](rts-controls.md)
- [선택 상세정보 창과 다중 선택 조작](selection-details.md)
- [보유 자원 UI와 STOCK 메시지](stock.md)
- [미니언과 진영 동기화](minions.md)
- [아군·적군 의상과 건물 지붕 색상](team-colors.md)
- [HQ·요새·감시탑과 건물 전투 동기화](../buildings/README.md)
- [전장의 안개 동기화와 서버 성능 측정](fog-of-war.md)
- [유닛과 메시지 형식](units.md)
- [효과음: 증기 소리와 재생 규칙](sound.md)
- [맵 구성과 재생성](map.md)
- [맵 해시와 나무 변경분 동기화](map-sync.md)

## 검증

검증은 [필수 테스트 안내](../tests/README.md)에 따라 변경한 기능만 실행합니다. 기본 실행은 빌드와 핵심 검사 4개이며, 실제 서버에는 접속하지 않습니다. 상세 로그는 파일에 남기고 통과 요약만 출력합니다.
CI는 C# 빌드와 `tests/test_watch_snapshot.py`를 실행합니다. Godot 장면 검사(`tests/run.ps1`)는 Godot 실행 파일이 필요해 로컬에서 실행합니다.
