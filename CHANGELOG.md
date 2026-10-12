# 변경 이력

새 항목은 위에 추가합니다. 현재 동작은 `docs/`에 있고, 여기에는 무엇이 언제 바뀌었는지만 적습니다.
게임 규칙의 변경 이력은 `project-w-server/CHANGELOG.md`에 있습니다.

## 2026-10-12

- 네트워크 쓰기가 멈춰도 화면이 멈추지 않도록 전송을 비동기 대기열로 바꿨습니다(`NetClientChecks` 추가).
- README를 주제별 `docs/` 문서와 이 변경 이력으로 나눴습니다. 공물 정신집중(8초), 미니언 슬롯 추가(무료권 전용) 등 README에 남아 있던 오래된 설명을 서버에 맞췄습니다.
- CI(C# 빌드, 관전 스냅샷 검사)와 작업 규칙(`AGENTS.md`, `CLAUDE.md`)을 추가했습니다.

## 2026-10-11

- 영웅 3 대 3(팀마다 지휘관 1명 + 늑대·골렘·사령관), 팀 공통 레벨 HUD, 회관·아이템 티어 UI, 학습 AI 관전을 추가했습니다.
- 학습 기록과 모델은 `../project-w-ai/rl/runs/league5`에 보존되어 있으며 학습은 중지 상태입니다. 최신 모델 관전 실행본은 `../build-league5-watch`, 이전 실행본과 Pi 복구 자료는 `../_archive/20261009`에 정리했습니다(저장소 밖 로컬 폴더).

## 2026-10-09

- **Pi 학습 AI 관전**: `../_archive/20261009/legacy-builds/build-updated/Watch-AI.cmd`. 10월 9일 02:18 KST에 가져온 영웅 480판 / 지휘관 400판 체크포인트 복사본으로 AI 경기를 관전합니다. 실행본 `Server/policies/manifest.json`에 시각과 해시가 있습니다. [화면](docs/images/spectator-trained-ai.png)
- **치유사·공물 무료 추가권**: 당시 실행본은 `../_archive/20261009/legacy-builds/build-updated/Play-Local.cmd`입니다. 로컬 게임에만 적용했고 Pi의 학습·바이너리는 비교 기준으로 보관했습니다.
- **복원 서버 연결**: 사령관(202), 상점·장비 4칸, 아이템 상자·일꾼 배달, 귀환, 본진 보급 구역 메시지를 연결했습니다. 사령관은 임시 모델이며 잃어버린 회사 작업본의 모델·UI를 복원한 것은 아닙니다. 당시 실행본은 `../_archive/20261009/legacy-builds/build-recovered/Play-Local.cmd`입니다.
- Windows 실행본은 `tools/export-windows.ps1 -OutputDirectory <폴더>`로 만들고 폴더 전체를 배포합니다.

## 2026-09-30 ~ 2026-10-08

- 이동 보간, 건물·인구·생산·랠리·취소, 정지·홀드, 로비 서버 연결, AOS 영웅 모드, 미니언 편성을 추가했습니다.
