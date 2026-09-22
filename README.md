# Project W

Godot .NET 클라이언트 프로토타입입니다. Go 서버가 유닛을 생성하고 이동을 계산하며, 클라이언트는 서버 상태를 표시합니다.

## 폴더

```text
game/             메인 게임 장면과 서버 메시지 적용
input/            마우스 입력과 맵 카메라
network/          TCP 연결과 명령 문자열
units/            공통 유닛 스크립트·씬, 기사·궁수 씬
maps/             맵 장면
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
4. 내 유닛 좌클릭으로 선택, 우클릭으로 이동 요청. 빈 곳이나 상대 유닛을 좌클릭하면 선택이 해제됩니다.

맵 카메라는 WASD/방향키·가운데 드래그로 이동, 휠로 확대/축소, Home으로 전체 보기입니다.

서버 없이 외형을 확인하려면 `previews/Units.tscn` 또는 `previews/Arena.tscn`을 열고 F6으로 실행합니다.

## 문서와 검증

- [유닛과 메시지 형식](docs/units.md)
- [맵 구성과 재생성](docs/map.md)

```text
dotnet build
Godot --headless --path . res://tests/UnitSceneChecks.tscn
Godot --headless --path . --script tests/check_arena.gd
```

`Godot`은 설치한 Godot .NET 실행 파일 경로로 바꿉니다. 검증은 실제 서버에 접속하지 않습니다.
