# 작업 목록

> **현재 진행 (2026-11-23 세션 끝):** Body v2 수신 완료(명세 01 §4.3, 실측 확인). 그 위에 **명세 없이 테스트 구현**으로 Unity-chan 휴머노이드 적용(팔·척추·손·손가락, 루트 원점 F9·Y 고정 F10)까지 동작 확인. 구조는 `docs/ARCHITECTURE.md`.
> 다음 세션 후보(사용자가 고름):
> 1. 노션 정리(plan_review "다음 세션 시작점" 8개 + 2-6·2-9 결정 반영) → Doc-3 캐릭터 명세 → Doc-2 명세 01 승인
> 2. Net-1∼5 마무리(SocketManager는 일부 구현됨: 루프 하나·3초 재접속·5초 시간 제한·64KB 이어 붙이기. 남은 것: 연결 상태·`client.registered` 처리, 주소 비었을 때 접속 안 함·`ReconnectSeconds`, 정상 종료, 메시지마다 로그 제거)
> 3. Data-1(Face 부분) → Data-3 FaceReceiver(BodyReceiver와 같은 틀, 정렬 주의 4.4e, 미러도 맞춤)
> 4. 휴머노이드 다듬기: 머리(Face 필요), 손가락 품질, Char-1 정리
>
> 외부: VishasBody 이슈 #2(규약 §2 루트 인덱스 오기) 열림. push 안 한 커밋 있음(사용자가 push).

## 운영 방식
- **해야 할 작업만 적는다.** 끝난 항목은 지운다. 진행도는 명세의 "구현 현황" 표에 남긴다.
- 항목 형식: `- [ ] <번호> <내용> — 기획: [태그] / 명세: <문서 번호> §<절>` (근거가 없으면 `기획: 없음(AI 발견)`)
- 번호는 `영역-번호`. 영역 안에서 계속 늘리고 다시 쓰지 않는다. 영역: Net(연결), Data(수신 데이터), Char(캐릭터 적용), Doc(문서), Fix(명세 밖 버그). 다음 번호: Net-6, Data-4, Char-2, Doc-4, Fix-5.

## 결정 대기
- 명세 01의 `해석`·`AI 제안` 항목 승인 (plan_review)

## Doc. 문서
- [ ] Doc-2 명세 01 승인 (`해석`·`AI 제안` 21건 확인, plan_review). 태그를 노션 새 이름으로 다시 연결 — 기획: `[Vishamos-Net-…]`, `[Vishamos-Data-…]` / 명세: 01
- [ ] Doc-3 캐릭터 적용 명세(02) 작성: 지금 테스트 구현(휴머노이드 리타기팅, 루트 원점·배율, 손 필터)을 기준으로 노션 정리 후 — 기획: 없음(테스트 선행, 0-2 결정 필요)

## Net. 연결 (명세 01 §2∼§3)
- [ ] Net-1 설정 파일 읽기·기본값 생성 — 기획: `[Vishamos-Net-Connect]` / 명세: 01 §2
- [ ] Net-2 연결 상태 4개, 접속(시간 제한 5초)·JSON 등록 — 기획: `[Vishamos-Net-Status]`, `[Vishamos-Net-Register]` / 명세: 01 §3.1, §3.2
- [ ] Net-3 재접속 3초, 루프 하나, 모든 종료 상태 — 기획: `[Vishamos-Net-Reconnect]` / 명세: 01 §3.3
- [ ] Net-4 수신 루프(항상 대기, 조각 이어 붙이기, JSON 처리, 버퍼 재사용) — 기획: `[Vishamos-Net-Receive]` / 명세: 01 §3.4
- [ ] Net-5 종료 처리(정상 종료 1초, 플레이 중지 후 잔여 작업 없음) — 기획: `[Vishamos-Net-Receive]` / 명세: 01 §3.5

## Data. 수신 데이터 (명세 01 §4)
- [ ] Data-1 Face 부분(구분·보관·수신 상태). Body 부분은 BodyReceiver로 완료 — 기획: `[Vishamos-Data-Route]`, `[Vishamos-Data-Latest]`, `[Vishamos-Data-Timeout]` / 명세: 01 §4.1, §4.2
- [ ] Data-3 Face iOS v1 해석 — 기획: `[Vishamos-Data-Face]` / 명세: 01 §4.4

## Char. 캐릭터 적용 (테스트 구현, 명세 없음)
- [ ] Char-1 `MainCharacter`(기즈모)와 `HumanoidCharacter` 정리: 필터 중복, `MainCharacter` 내부 HandTrack 중복. 기즈모 테스트를 계속 쓸지 정한 뒤 — 기획: 없음(AI 발견)

## Fix. 명세 밖 (2026-11-22 점검에서 발견)
- [ ] Fix-2 `MicManager` 마이크가 없으면 `while (GetPosition <= 0)` 무한 대기로 앱 정지, 매 프레임 대기·`Play()` 재호출 — 기획: 없음(AI 발견)
- [ ] Fix-3 `MicManager.CheckDelay` 위치 보정에 44100(1초) 대신 클립 길이(10초 × 44100)를 써야 함 — 기획: 없음(AI 발견)