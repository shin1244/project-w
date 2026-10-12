# Project W

Godot .NET 클라이언트입니다. Go 서버가 유닛을 생성하고 이동·전투를 계산하며, 클라이언트는 서버 상태를 표시하고 입력을 명령으로 보냅니다.

| 저장소 | 역할 |
| --- | --- |
| `project-w` (이 저장소) | Godot(.NET) 클라이언트와 관전 도구 |
| `project-w-server` | 게임 서버·로비·규칙·RL 환경. **게임 규칙과 수치의 기준** |
| `project-w-ai` | Python PPO 학습·리그·평가·정책 내보내기 |

세 저장소를 같은 부모 폴더 아래에 둡니다. 서버 메시지 형식이 바뀌면 서버와 클라이언트를 함께 갱신해야 합니다.

## 실행

1. Godot .NET에서 `project.godot`를 엽니다.
2. C#을 빌드하고 F5로 실행하면 `lobby/Lobby.tscn`이 열립니다. 로비 서버 주소를 입력하고 매칭을 시작합니다. [지인 테스트 실행·Pi 배포 안내](docs/friend-test.md)를 참고하세요.
3. 일반 매칭은 2명이 모이면 15초 뒤, 8명이면 즉시 시작합니다. 각 팀 지휘관 1명·영웅 3명 순서로 배정하고 빈자리는 AI가 맡습니다. **혼자 연습 · 영웅/지휘관**을 고르면 AI 7명과 시작합니다. 두 모드 모두 로비 서버 연결이 필요합니다. 모든 사람의 맵 동기화가 끝나면 함께 시작하며 큐·로딩에서 취소/Esc로 돌아갈 수 있습니다.
4. 실제 전장을 실행하려면 Go 서버를 `127.0.0.1:7777`에서 실행합니다. RTS는 `game/Main.tscn`, AOS는 `game/AOS.tscn`을 열어 F6으로 실행합니다. AOS는 같은 전장을 상속하며 `MAP_READY <해시> HERO 200`으로 임시 영웅을 요청합니다. Main 루트의 `RequestedRole`·`RequestedHeroType`으로 접속 요청을 바꿀 수 있고 실제 역할·진영은 서버의 WELCOME으로 확정합니다. 내 유닛 좌클릭으로 단일 선택, 드래그로 여러 유닛 선택, 우클릭으로 이동·공격·채집·이어 짓기를 요청합니다.

유닛 이동은 서버 30Hz 기준 1.5틱(50ms) 지연 보간합니다. 모든 유닛이 같은 재생 시점을 사용하며, 패킷이 늦으면 마지막 확정 위치에서 기다립니다. [이동 보간과 메시지 형식](docs/units.md#이동-보간)을 참고하세요.

## 학습된 AI 관전

`tools/Watch-Latest-AI.cmd`는 두 역할의 최신 체크포인트를 별도 관전 스냅샷으로 복사한 뒤
관전 실행본을 엽니다(기본 `../build-league5-watch`). `tools/Use-Trained-AI.ps1 -Latest`는 스냅샷만 준비합니다.
경로가 다른 경우 `-AIRepo`, `-ServerRepo`, `-RunsRoot`, `-OutputDirectory`를 지정하세요.
각각 `PROJECT_W_AI_ROOT`, `PROJECT_W_SERVER_ROOT`, `PROJECT_W_RUNS_ROOT` 환경 변수도 지원합니다.
`-ServerRepo`는 서버 소스 경로이며 모델은 AI 저장소에서 읽습니다.
관전용 `Project W.exe`·서버 바이너리와 모델 데이터는 Git 소스와 별도로 준비해야 합니다.

## 폴더

```text
game/             메인 게임 장면, 입력·통신 연결과 메시지 분기
lobby/            로비 서버 매칭·실제 전장 로딩
input/            마우스 입력과 맵 카메라
network/          TCP 연결과 명령 문자열
events/           서버 상태로 표시하는 공물과 수집 진행 효과
units/            UnitManager, 공통 유닛 스크립트와 일꾼·기사·궁수 씬
resources/        ResourceManager, 자원 공통 스크립트와 나무 외형 3종
buildings/        회관·요새·저장소·합숙소·병영·대장간·포탑 씬, 공통 공사 부지 표시
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

현재 동작은 주제별 문서에 있고, 변경 이력은 [CHANGELOG.md](CHANGELOG.md)에 날짜순으로 적습니다. 작업 규칙은 [AGENTS.md](AGENTS.md)에 있습니다.

- [게임 모드와 화면](docs/game-modes.md): 3 대 3, 팀 레벨, 로비, 승패, 공물, 미니언 편성, 회관 티어
- [영웅 화면과 조작](docs/heroes.md): AOS 패널, 늑대·골렘·사령관 모델과 미리보기
- [RTS 조작](docs/controls.md): 선택, 공격, 정지·홀드, 카메라, 건설
- [부대 지정·선택 확장·미니맵 조작](docs/rts-controls.md)
- [선택 상세정보 창과 다중 선택 조작](docs/selection-details.md)
- [보유 자원 UI와 STOCK 메시지](docs/stock.md)
- [미니언과 진영 동기화](docs/minions.md)
- [아군·적군 의상과 건물 지붕 색상](docs/team-colors.md)
- [회관·요새·포탑과 건물 전투 동기화](buildings/README.md)
- [전장의 안개 동기화와 서버 성능 측정](docs/fog-of-war.md)
- [유닛과 메시지 형식](docs/units.md)
- [맵 구성과 재생성](docs/map.md)
- [맵 해시와 나무 변경분 동기화](docs/map-sync.md)

## 검증

검증은 [필수 테스트 안내](tests/README.md)에 따라 변경한 기능만 실행합니다. 기본 실행은 빌드와 핵심 검사 4개이며, 실제 서버에는 접속하지 않습니다. 상세 로그는 파일에 남기고 통과 요약만 출력합니다.
CI는 C# 빌드와 `tests/test_watch_snapshot.py`를 실행합니다. Godot 장면 검사(`tests/run.ps1`)는 Godot 실행 파일이 필요해 로컬에서 실행합니다.
