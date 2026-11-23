# 작업 목록

> **현재 진행 (2026-11-23):** Body 규약 v2 반영(명세 01 §4.3). 사용자가 BodyReceiver(Data-1의 Body 부분 + Data-2)를 먼저 만든다. 노션 정리·Doc-2는 뒤로 미룸.
> 다음 순서: Net-1 → Net-2 → Net-3 → Net-4 → Net-5 → Data-1(Face 부분) → Data-3.

## 운영 방식
- **해야 할 작업만 적는다.** 끝난 항목은 지운다. 진행도는 명세의 "구현 현황" 표에 남긴다.
- 항목 형식: `- [ ] <번호> <내용> — 기획: [태그] / 명세: <문서 번호> §<절>` (근거가 없으면 `기획: 없음(AI 발견)`)
- 번호는 `영역-번호`. 영역 안에서 계속 늘리고 다시 쓰지 않는다. 영역: Net(연결), Data(수신 데이터), Char(캐릭터 적용), Doc(문서), Fix(명세 밖 버그). 다음 번호: Net-6, Data-4, Char-1, Doc-3, Fix-5.

## 결정 대기
- 명세 01의 `해석`·`AI 제안` 항목 승인 (plan_review)

## Doc. 문서
- [ ] Doc-2 명세 01 승인 (`해석`·`AI 제안` 21건 확인, plan_review) — 기획: `[Vishamos-Net-…]`, `[Vishamos-Data-…]` / 명세: 01

## Net. 연결 (명세 01 §2∼§3)
- [ ] Net-1 설정 파일 읽기·기본값 생성 — 기획: `[Vishamos-Net-Connect]` / 명세: 01 §2
- [ ] Net-2 연결 상태 4개, 접속(시간 제한 5초)·JSON 등록 — 기획: `[Vishamos-Net-Status]`, `[Vishamos-Net-Register]` / 명세: 01 §3.1, §3.2
- [ ] Net-3 재접속 3초, 루프 하나, 모든 종료 상태 — 기획: `[Vishamos-Net-Reconnect]` / 명세: 01 §3.3
- [ ] Net-4 수신 루프(항상 대기, 조각 이어 붙이기, JSON 처리, 버퍼 재사용) — 기획: `[Vishamos-Net-Receive]` / 명세: 01 §3.4
- [ ] Net-5 종료 처리(정상 종료 1초, 플레이 중지 후 잔여 작업 없음) — 기획: `[Vishamos-Net-Receive]` / 명세: 01 §3.5

## Data. 수신 데이터 (명세 01 §4)
- [ ] Data-1 구분 표, 송신원별 최신 프레임·수신 상태·재시작 신호 — 기획: `[Vishamos-Data-Route]`, `[Vishamos-Data-Latest]`, `[Vishamos-Data-Timeout]` / 명세: 01 §4.1, §4.2
- [ ] Data-2 Body v2 해석(루트 위치 포함, v1은 버림) — 기획: `[Vishamos-Data-Body]` / 명세: 01 §4.3
- [ ] Data-3 Face iOS v1 해석 — 기획: `[Vishamos-Data-Face]` / 명세: 01 §4.4

## Fix. 명세 밖 (2026-11-22 점검에서 발견)
- [ ] Fix-1 `CharacterManager` `Resources.Load` 경로가 그림 이동 후 null (그림을 `Assets/Imports/Resources/` 아래로 옮기거나 `[SerializeField] Sprite`로 연결) — 기획: 없음(AI 발견)
- [ ] Fix-2 `MicManager` 마이크가 없으면 `while (GetPosition <= 0)` 무한 대기로 앱 정지, 매 프레임 대기·`Play()` 재호출 — 기획: 없음(AI 발견)
- [ ] Fix-3 `MicManager.CheckDelay` 위치 보정에 44100(1초) 대신 클립 길이(10초 × 44100)를 써야 함 — 기획: 없음(AI 발견)
- [ ] Fix-4 `Assets/Resources/` 빈 폴더와 `.meta` 정리 — 기획: 없음(AI 발견)
