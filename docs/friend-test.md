# 지인 테스트: 로비에서 전장까지

## 매칭 규칙

큐에 들어온 순서대로 아래 자리를 배정합니다. 접속·로딩이 끝나는 순서로 역할이 바뀌지 않습니다.

| 순서 | 팀 | 역할 |
| --- | --- | --- |
| 1 | 1팀 | 지휘관 (RTS) |
| 2 | 1팀 | 영웅 (AOS, 로비에서 선택) |
| 3 | 2팀 | 지휘관 |
| 4 | 2팀 | 영웅 |

- 1명은 계속 대기합니다. 2명이 되면 기본 15초 동안 추가 참가자를 기다리고 2~3명으로도 시작합니다. 4명은 즉시 시작합니다.
- 2명이면 **둘 다 같은 팀**이며 상대는 AI 2명입니다. 3명이면 2팀 영웅을 AI가 맡습니다. 항상 지휘관·영웅 한 명씩 두 팀을 구성합니다.
- **혼자 연습 · 영웅/지휘관**은 선택한 1팀 역할에 사람 한 명, 나머지 세 자리에 AI를 배정합니다. 일반 대기자와 합쳐지지 않으며, 다른 경기가 진행 중이면 서버가 비기를 기다립니다.
- 로딩 중 한 사람이 취소하면 해당 매치를 취소합니다. 나머지 사람도 로비로 돌아가 다시 매칭합니다.
- 참가자 전원의 맵 해시 확인·초기 상태 수신이 끝나야 게임 틱과 조작을 시작합니다. 준비 제한은 90초입니다.
- 시작 후에는 중도 참가·재접속을 지원하지 않습니다. 상단의 **로비로 나가기**를 사용하거나 연결이 끊기면 로비로 돌아갑니다. 남은 사람은 계속 플레이할 수 있습니다.
- 적 본진 회관을 파괴하면 승리하며 같은 틱의 양쪽 본진 파괴는 무승부입니다. 결과 확정 후 조작과 시뮬레이션을 멈춥니다. 결과 화면에서 로비로 돌아갑니다.
- 한 번에 한 판만 운영합니다. 사람이 모두 퇴장하면 게임을 정리합니다. 승패 또는 최대 2시간 제한에 따른 무승부가 확정되면 결과 전송 후 10초 뒤 서버를 정리하고 다음 큐를 처리합니다. 결과 화면은 연결 종료 뒤에도 유지됩니다. 이전 판의 상태는 다음 판에 남지 않습니다.

## 서버 구성

```text
Godot 클라이언트 ── HTTP :8080 ── 로비 서버 (계속 실행)
       │                              │ 매칭 확정 시 실행 / 종료 감시
       └──────── TCP :7777 ──── 게임 서버 (판마다 별도 프로세스)
```

로비 실행 파일은 `project-w-server/cmd/lobby`, 게임 실행 파일은 기존 Go 모듈 루트입니다. 둘 다 같은 라즈베리파이에서 실행합니다. 게임 서버를 미리 따로 켜지 마세요. 로비가 동일 포트에 매치별 게임 서버를 띄웁니다. 별도 DB나 Redis는 필요 없습니다. 로비를 재시작하면 대기열도 초기화됩니다.

로비는 임의의 128비트 큐 ID를 입장권으로 사용합니다. 팀·역할·입장권 목록은 자식 게임 프로세스의 표준 입력으로 전달하며, 각 클라이언트는 자기 입장권만 받습니다. 게임 서버는 맵 해시와 입장권을 확인하고 한 번만 입장시킵니다. 클라이언트가 초기 상태를 적용한 뒤 `LOADED`로 확인하면 전원 준비 후 `MATCH_START`를 보냅니다. 기존 `MAP_READY ... COMMANDER/HERO` 방식으로 매칭 서버에 직접 입장할 수 없습니다.

로비 HTTP API는 `PUT /queue/{id}`(참가/재시도), `GET /queue/{id}`(상태), `DELETE /queue/{id}`(취소), `GET /health`입니다. 클라이언트는 0.5초 간격으로 확인하며, 응답 없는 대기자는 15초 뒤 제거됩니다. 이것은 계정·로그인·TLS가 없는 지인 테스트용 서버입니다. 같은 LAN이나 공유 VPN으로 먼저 테스트할 수 있습니다.

## Windows에서 로컬 실행

서버 폴더에서:

```powershell
go build -o project-w-game.exe .
go build -o project-w-lobby.exe ./cmd/lobby
./project-w-lobby.exe -game-bin ./project-w-game.exe -game-host 127.0.0.1
```

Godot .NET으로 클라이언트 `project.godot`를 열고 리소스를 가져온 뒤 C#을 빌드합니다. F5로 로비를 열고 주소에 `http://127.0.0.1:8080`을 입력합니다. 창 하나에서는 **혼자 연습**을, 두 개 이상에서는 **일반 매칭**을 선택합니다. 로비 주소는 `user://network.cfg`에 저장됩니다.

`-wait 0s`는 2명부터 즉시 시작, `-wait 30s`는 추가 참가자를 30초 기다립니다. 4명 즉시 출발은 동일합니다. 서버 포트를 바꾸려면 로비의 `-listen :8080`, `-game-port 7777`을 사용합니다.

기존 개발용 F6 직접 실행도 유지합니다. 이때만 게임 서버를 `go run .`으로 별도로 실행하고 Main/AOS 씬에서 `127.0.0.1:7777`로 접속합니다. 로비 운영과 동시에 사용하면 포트가 겹칩니다.

## 라즈베리파이 5 배포 (64비트 Linux)

Windows의 서버 폴더에서:

```powershell
./deploy/build-pi.ps1
```

`dist/pi/`에 게임 서버, 로비 서버, 맵, 서비스 예시가 생성됩니다. 이 폴더 전체를 Pi의 `/opt/project-w`로 복사합니다. ARM64 빌드이므로 64비트 OS가 필요합니다. Pi에서 직접 빌드하려면 `go build -o project-w-server .`와 `go build -o project-w-lobby ./cmd/lobby`를 사용할 수도 있습니다.

Pi에서 수동 실행:

```sh
cd /opt/project-w
chmod +x project-w-server project-w-lobby
./project-w-lobby -game-bin ./project-w-server -game-host 친구가_접속할_IP
```

`-game-host`에는 **친구 PC에서 접근할 Pi의 IP/DNS**를 입력합니다. 같은 집에서는 LAN IP, 공유 VPN에서는 VPN IP, 포트포워딩을 사용하면 공인 IP/DNS입니다. `127.0.0.1`을 넣으면 친구 PC 자신의 주소로 연결하므로 외부 테스트에 사용할 수 없습니다. 외부 직접 접속 시 로비 **TCP 8080**, 게임 **TCP 7777** 두 포트가 Pi로 연결되어야 합니다. 친구에게는 `http://접속주소:8080`을 전달합니다.

자동 시작이 필요하면 서버의 `deploy/project-w-lobby.service`와 `deploy/project-w.env.example`을 사용합니다. 서비스 계정 `projectw`를 만들고 `/etc/project-w.env`에 `GAME_HOST`를 설정한 뒤 서비스 파일을 `/etc/systemd/system/`에 설치합니다. 서비스는 `/opt/project-w`에서 실행하며, 종료 시 자식 게임 프로세스도 함께 정리합니다.

```sh
sudo useradd --system --no-create-home projectw
sudo cp /opt/project-w/project-w.env.example /etc/project-w.env
sudo nano /etc/project-w.env
sudo cp /opt/project-w/project-w-lobby.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now project-w-lobby
journalctl -u project-w-lobby -f
```

Pi의 실제 수용 성능은 플레이 인원보다 유닛 수·경로 탐색·시야 계산에 영향을 받습니다. 로그의 `[tick]`이 **30 tps**를 유지하는지, 최대 계산 시간이 **약 33.3ms**를 자주 넘는지 보세요. PC에서의 기능 검증은 Pi 성능 측정을 대신하지 않습니다.

## 클라이언트 배포 빌드

Windows에서는 클라이언트 프로젝트 폴더에서 다음 명령으로 내보냅니다. Godot 에디터에서 한 번 연 프로젝트는 설치 경로를 자동으로 읽으며, 필요하면 `-GodotPath 'C:\tools\Godot\Godot.exe'`로 .NET 에디터 경로를 지정합니다.

```powershell
./tools/export-windows.ps1
```

스크립트는 C# Debug 어셈블리를 먼저 빌드하고, `.godot/exported`의 변환 장면 캐시를 `.godot/export-backups`로 보관한 뒤 새 에디터 프로세스로 Release를 내보냅니다. C#의 `[Export]` 필드를 추가했을 때 오래된 장면 캐시에 노드 연결이 누락되는 문제를 방지합니다. 결과는 프로젝트 옆 `build/` 폴더이며, 로그는 `.godot/check-logs/export-windows/`에서 확인합니다. ZIP 파일은 자동 갱신되지 않으므로 배포할 때 새 `build/` 폴더를 압축하세요.

Godot 4.7.2 **.NET** 에디터와 같은 버전의 내보내기 템플릿을 사용합니다. Export에 Windows Desktop 프리셋을 만들고 x86_64로 내보냅니다. 비리소스 포함 필터에 **`*.json`**을 추가해 `maps/test.json`과 건설 미리보기 목록을 포함합니다. 로비와 로딩은 기본 메인 씬에서 참조되며 실제 Main 씬도 내보내기에 포함해야 하므로 모든 리소스 내보내기를 사용합니다.

실행 파일만 보내지 말고 내보내기 폴더 전체를 압축해서 전달하세요. 클라이언트와 서버의 `maps/test.json`은 바이트 단위로 같아야 합니다. 빌드 파일에서 두 PC로 같은 팀 협동을 확인한 뒤 4인 대전을 확인합니다.

## 검증

서버의 `go test ./...`는 기존 게임 검사와 함께 큐·2/3/4인 배정·취소·만료·매치 교체·입장권·로딩 장벽을 확인합니다.

클라이언트의 기본 검증은 계속 서버 없이 실행합니다. 실제 연결 검증은 별도 명령입니다:

```powershell
./tests/run.ps1 -Check MapSyncChecks -GodotPath 'C:\tools\Godot\Godot_console.exe'
./tests/run-match.ps1 -Players 2 -SkipBuild -GodotPath 'C:\tools\Godot\Godot_console.exe'
./tests/run-match.ps1 -Players 1 -SkipBuild -GodotPath 'C:\tools\Godot\Godot_console.exe'
./tests/run-match.ps1 -Players 4 -SkipBuild -GodotPath 'C:\tools\Godot\Godot_console.exe'
```

`run-match.ps1`은 사용하지 않는 로컬 포트에 실제 로비·게임 서버와 Godot 클라이언트를 띄우고, 로딩·역할 확정·전장 진입·퇴장·새 매치를 확인합니다. 2인 검사에서는 빠른 큐 취소·재참가도 확인합니다. 프로세스는 검사 후 정리되며 로그는 `.godot/check-logs/match-2` 또는 `match-4`에 저장됩니다. 서버 빌드를 위해 Go가 필요합니다. 리소스 가져오기와 C# 빌드를 끝냈다면 `-SkipBuild`로 중복 빌드를 피합니다.

## 변경 경계

로비/로딩/세션이 배정, 장면 전환, 결과 화면과 복귀를 관리합니다. 서버의 봇 판단 계층은 기존 이동·채집·건설·생산·스킬 명령을 사용하며 동일한 비용·사거리·시야·쿨다운 검사를 받습니다. 본진 승패 판정은 틱 종료 시 한 번 확정합니다. 중도 퇴장한 사람의 자리를 봇이 인계하는 기능은 아직 없습니다.
