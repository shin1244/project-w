# 소리: 이동 명령 응답음

게임 소리는 내가 명령을 보낼 때 나는 짧은 응답음 하나뿐입니다. 합성 효과음(공격·사망·공사·포격·스킬·알림·클릭 등)은 2026-10-28에 모두 지웠고, 이동 명령 소리만 사용자 요청으로 남겼습니다.

- 자동 로드 `OrderSound`(`audio/OrderSound.cs`)가 `audio/sfx/order-move-1.wav`·`order-move-2.wav` 중 하나를 높낮이를 조금 흔들어 냅니다.
- `Main.SendCommand`가 서버로 명령을 보낸 뒤 `OrderSound.For(command)`를 부릅니다. 소리는 명령 동사로 정합니다: `MOVE`·`GATHER`·`RETURN`·`BUILD`·`CONSTRUCT`·`STOP`·`HOLD`·`RALLY`·`TUNNEL`·`PICKUP_CRATE`·`DELIVER_ITEM`·`NUGGET`·`TRIBUTE`는 응답음을 내고, 공격(`ATTACK`·`ATTACK_MOVE`)과 생산·연구·업그레이드는 조용합니다.
- 한 번에 하나만 울립니다. 앞 소리가 끝나기 전에 보낸 명령은 소리를 내지 않아, 명령을 연달아 찍어도 겹치지 않습니다.
- 오디오 버스는 `Master` 하나이고 음량 설정은 없습니다([설정](settings.md)).

`./tests/run.ps1 -Check RtsControlChecks`가 서버 없이 미니맵 이동 명령 한 번에 응답음이 한 번 나는지와 동사별 구분을 확인합니다. 헤드리스 실행은 소리를 실제로 재생하지 않고 같은 규칙만 거칩니다.
