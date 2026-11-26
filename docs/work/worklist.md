# 작업 목록

## 현재 진행 (갱신: 2026-11-26 21:00)
- 진행 중: Char-3(developer)
- 마지막 완료: Char-5 정지 Idle 복귀(`bfe64a8`, 컴파일만 확인. 실행 확인 대기), 명세 01 외부 규약 참조 갱신(`0f8263c`), 이슈 #3 PowerShell gh 허용 규칙(`b96f2f1`, 확인필요). 사용자 캐릭터 `Assets/Imports/Models/Vishamos_Default.fbx` 추가(서브모듈 커밋 전, 뼈 49개 Humanoid 이름, 블렌드셰이프·표정 뼈 없음)
- 대기: 사용자 — Char-5 에디터 연결(Idle Clip 지정)과 실행 확인, 새 모델 Humanoid 확인 결과(축·크기·Blender 내보내기 설정 → 명세 03 4a), 커밋 안 한 Unity 변경(MainScene, Animations, LipSyncManager) 커밋 여부, push 6건
- 다음: Char-3(developer) → Doc-3(캐릭터 적용 명세 02) → Char-2

## 운영 방식
- 형식·완료 처리는 `visha-common:design-work` 5절. worklist는 메인 스레드만 고친다.
- 영역: Net(연결), Data(수신 데이터), Char(캐릭터 적용), Doc(문서), Fix(명세 밖 버그), QA(검증 도구). 다음 번호: Net-6, Data-5, Char-6, Doc-7, Fix-5, QA-2.
- 기획 근거가 없으면 `기획: 없음(AI 발견)` 또는 `기획: 없음(사용자 요청, plan_review <번호>)`.

## 올린 이슈
- 없음

## Doc. 문서
- [ ] Doc-4 [P2] 노션 반영(사용자 작업, 기획 수준만. 세부 값·결정은 대화로 정하고 plan_review·명세에 둔다). 노션 작업 때 Claude가 대조한다 — 기획: 없음 / 명세: 없음
  - 노션 개편 맞춤: plan_review "다음 세션 시작점" 1∼8 중 기획에 해당하는 것(새 태그 체계, JSON 함수 호출, Controller 설정, 캐릭터 컨셉 영향, 제스처, Space)
  - 기획으로 적을 큰 방향: Body 미러, Face 머리 회전 사용(정면 보정·끊김 시 복귀), 루트 이동 기본 고정
- [ ] Doc-2 [P2] 명세 01 승인 (`해석`·`AI 제안` 21건 확인, plan_review). 태그를 노션 새 이름으로 다시 연결 — 기획: `[Vishamos-Net-…]`, `[Vishamos-Data-…]` / 명세: 01 (보류 대기: plan_review 0-1)
- [ ] Doc-3 [P1] 캐릭터 적용 명세(02) 작성: 지금 테스트 구현(휴머노이드 리타기팅, 팔 놓침 시 내리기, 뼈 회전 보간, 루트 원점·배율·기본 고정·데드존, 손 필터)을 기준으로 노션 정리 후. 명세로 옮긴 뒤 `HumanoidCharacter` 머리 주석은 줄인다(주석 최소 방침, 2026-11-24) — 기획: 없음(테스트 선행, 0-2 결정 필요)

- [ ] Doc-5 [P2] 명세 03 사람형 모델 기술적 제약 승인(`해석`·`AI 제안` 확인), 첫 FBX 내보내기 때 4a 축·크기 설정 실측 기록 — 기획: `[Vishamos-Character]` / 명세: 03
- [ ] Doc-6 [P2] 명세 04 캐릭터 연결 승인(초안 2026-11-25: 연결 지점 = Humanoid·블렌드셰이프·표정 뼈·머티리얼 값·모음·흔들림 뼈, 60fps). 1d·1e 대응표 형식은 `AI 제안` — 기획: `[Vishamos-Character]` / 명세: 04

## Data. 수신 데이터
- [ ] Data-4 [P2] Face 값 기록·범위 측정: 방송처럼 10∼20분 받은 Face 52개 값을 기록(CSV 등)하고 값마다 최대값·p95를 낸다. 결과는 모델 쪽에서 쓸 Face 값을 고를 때와 4-2 보정 `inMax`에 쓴다. 기록 파일·화면 표시는 방송 화면에 나오지 않게(안전 규칙 1) — 기획: `[Vishamos-Character-Concept]` / 명세: 04 §1

## Char. 캐릭터 적용 (테스트 구현, 명세 없음)
- [x] Char-5 [P1] 놓친 팔·손·손가락을 정지 Idle 포즈로 복귀(`HumanoidCharacter`) — 기획: 없음(사용자 요청, plan_review 4-1) / 명세: 없음(Doc-3에서 02로)
  - Idle: 인스펙터 필드로 클립 받기(`Assets/Imports/Animation/Idle.fbx`의 `Idle_OneFrame`, Humanoid). `Start`에서 ① T자세 저장(`CaptureRestPose`) ② 클립 1회 샘플링(`SampleAnimation`, 안 되면 Playables `Evaluate(0)`) ③ 팔·손·손가락 로컬 회전 저장 ④ T자세로 되돌림. 순서가 바뀌면 방향 계산 기준이 틀어진다(03 1c). Controller는 넣지 않는다
  - 손: 지금 `ApplyHand`(`HumanoidCharacter.cs:486`)는 손 미검출 또는 손목 안 보임이면 이전 회전 유지 → 키보드 아래에서 꺾인 채 남음. 팔과 같이 안 보인 시간을 재 0.5초(`armDownDelay`) 뒤 `armDownDuration`에 걸쳐 Idle 로컬 회전으로 보간(아래팔 기준이라 팔이 내려가도 붙어 감)
  - 팔: 지금 "내린 자세" 계산(`ArmDownDir`, `armDownOutAngle`, 아래팔 = 편 팔)을 Idle 회전으로 바꾼다
  - 확인: 손을 키보드 아래로 넣으면 0.5초 뒤 손목·손가락이 Idle 모양으로 풀림, 다시 꺼내면 추적으로 돌아옴. 클립이 비어 있으면 경고 한 줄 후 지금 동작(T자세 기준)
- [ ] Char-2 [P1] 표정 마무리(`FaceExpression`): uLipSync 마이크 세팅·본인 목소리 프로필 보정, 입을 무엇과 무엇으로 블렌딩할지 결정(지금: 음소 비율 × 음량 × Face jawOpen, 미검증. 지금은 "아"만 동작). 세팅 시 uLipSync `Output Sound Gain` 0(마이크 소리가 방송에 다시 나가지 않게, 안전 규칙 1)·`MicManager` 끄기 — 기획: 없음(사용자 요청, plan_review 4-2·4-3)
  - 모델 한계(유니티짱): 한쪽 눈 감기·한쪽 눈썹 없음, 눈 뼈 없음(동공 안 움직임). 최종 모델 요구사항으로 Doc-3에 적는다
- [ ] Char-3 [P1] 시선(동공 UV): `EyeGaze`(계산) + `EyeGazeDriver`(추상) + `EyeGazeUvDriver`(`_BaseMap` Offset). 새 파일만, `HumanoidCharacter`·`FaceExpression`은 고치지 않음 — 기획: `[Vishamos-Character-Concept]` / 명세: 04 1e, §1a
  - 확인: 새 모델 Eye 메시에 붙이고 시선을 옮기면 동공이 따라옴, 깜빡여도 안 튐, Face 끊기면 가운데로
- [ ] Char-4 [P2] 뼈 표정 채널: Face 값 → 표정 뼈 회전·이동·크기 대응표(`FaceExpression`은 지금 블렌드셰이프만). 모델 나온 뒤 — 기획: `[Vishamos-Character-Concept]` / 명세: 04 1d
- [ ] Char-1 [P2] `MainCharacter`(기즈모)와 `HumanoidCharacter` 정리: 필터 중복, `MainCharacter` 내부 HandTrack 중복. 기즈모 테스트를 계속 쓸지 정한 뒤 — 기획: 없음(AI 발견)

## QA. 검증 도구
- [ ] QA-1 [P2] 시나리오 로그 기록기: `Logs/scenario.log`에 `[HH:mm:ss.fff] <event>` 한 줄씩(파일에만, 화면·Console 없음), 이벤트를 남길 정적 메서드 하나. 규칙은 `qa-test` 2절 — 기획: 없음(visha-common 적용, 2026-11-26) / 명세: 없음
