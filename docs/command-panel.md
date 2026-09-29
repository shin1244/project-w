# 명령 패널

`game/Main.tscn`의 `SelectionUI/CommandPanel`에 216×216 크기의 패널을 화면 우측 하단에 붙여 표시합니다. 9칸은 넘패드 순서입니다.

```text
7 8 9
4 5 6
1 2 3
```

- 조종 가능한 내 유닛을 선택하면 4번에 `공격(A)`, 5번에 `정지(S)`, 6번에 `홀드(D)`를 표시합니다. 여러 유닛을 선택해도 같습니다.
- 아군 회관을 선택하면 7번에 `일꾼`을 표시합니다.
- 선택이 없거나 조종할 수 없는 유닛, 적 회관, 포탑을 선택하면 모두 빈칸입니다.

`UnitManager`와 `BuildingManager`의 선택 변경 이벤트로 표시를 갱신합니다. 선택 대상이 제거되거나 소유권·팀이 변경될 때, 접속이 종료되거나 맵이 다시 동기화될 때도 반영합니다.

회관의 `일꾼` 버튼을 클릭하면 기존 통신 경로로 `TRAIN 유닛타입` 한 줄을 보냅니다. 일꾼 타입은 `0`이므로 `TRAIN 0`을 보냅니다. 요청에 사용자 ID를 포함하지 않으며, 서버가 접속 정보로 요청자를 판단합니다. 줄바꿈은 `NetClient.Send`가 붙입니다.

아군 회관을 선택한 상태에서만 요청하며, 맵 동기화가 끝나지 않았으면 전송하지 않습니다. 클릭마다 요청 한 번을 보내고, 클라이언트에서 유닛을 미리 만들거나 자원을 차감하지 않습니다. 서버가 생성 후 기존 형식의 `UNIT 0 id owner x z team`을 보내면 화면에 표시됩니다. 서버는 접속 정보로 확인한 사용자의 생성 권한·비용 등을 최종 검사합니다.

공격·정지·홀드 버튼과 새 단축키는 아직 연결하지 않았습니다. 패널 위의 클릭과 휠은 뒤쪽 전장으로 전달되지 않습니다.

검증: `dotnet build --no-restore`, `tests/CommandPanelChecks.tscn`, `tests/UnitSceneChecks.tscn`, `tests/TowerSyncChecks.tscn`. `CommandPanelChecks.tscn`을 정상 그래픽 렌더러로 실행하고 `-- --command-panel-capture`를 추가하면 `.godot/command-panel-unit.png`와 `.godot/command-panel-hall.png`를 저장합니다.

![유닛 선택](images/command-panel-unit.png)

![회관 선택](images/command-panel-hall.png)
