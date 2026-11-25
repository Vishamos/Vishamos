# 03. 사람형 모델 사양 (기술적 제약)

- 작성: 2026-11-24 (갱신: 2026-11-25 기술적 제약만 남김, 모델링 권장 항목 삭제)
- 상태: **초안** (사용자 승인 전)
- 근거: 노션 `[Vishamos-Character]`(Humanoid FBX 외부 불러오기), 대화 결정 2026-11-24("사람형은 Humanoid + 블렌드셰이프 구조"), 2026-11-25(plan_review 6 범위 결정: 연결에 필요한 것만)
- 범위: 코드(`HumanoidCharacter`, `FaceExpression`)가 동작하려면 모델이 지켜야 하는 것만 적는다. 디자인·모델링 방식은 적지 않는다. 연결 방법 전체는 `04_model_main.md`.
- 번호는 다른 문서에서 참조하므로 지운 항목 번호를 다시 쓰지 않는다.

항목 구분: `확정` 노션·외부 규약에 명확한 것 / `확정(사용자 답변)` 대화로 정한 것 / `해석` 모호한 부분을 보충한 것(승인 필요) / `보류` 해당 작업 때 정함 / `확인 필요` 결정이 필요한 것 / `AI 제안` 근거가 없는 것(승인 필요)

---

## 1. 뼈 (Unity Humanoid)

| # | 내용 | 구분 | 근거 |
|---|---|---|---|
| 1a | FBX 임포트 Rig를 **Humanoid**로 매핑할 수 있어야 한다. 필수 뼈: Hips, Spine, Head, 양쪽 UpperArm·LowerArm·Hand, UpperLeg·LowerLeg·Foot. | 확정(사용자 답변) | Unity Humanoid 필수 뼈. Humanoid가 아니면 `HumanoidCharacter`가 동작하지 않음 |
| 1b | 선택 뼈 Chest·UpperChest·Neck·Shoulder·손가락은 있으면 쓰고 없으면 그 부분만 건너뛴다. Neck이 있으면 머리 회전을 Neck 0.4 / Head 나머지로 나누고, 없으면 Head가 전부 받는다. | 확정 | `HumanoidCharacter` 동작 |
| 1c | 코드는 **시작할 때의 자세를 기본 자세로 저장**하고 그 차이로 뼈를 돌린다. 어떤 기본 자세도 동작하며, 팔·손 비틀림은 기본 자세의 것을 물려받는다. | 확정 | `HumanoidCharacter` 동작 |
| 1d | Unity에서 캐릭터 앞쪽이 **+Z**, 위가 +Y, 단위 미터. 루트 이동만 미터 단위라 크기가 사람과 크게 다르면 `rootScale`로 맞춘다. | 해석 | 코드는 캐릭터가 +Z를 보는 기준(배치할 때 루트를 y 180°). 팔·손은 점 사이 방향만 써서 크기와 무관 |
| 1e | 뼈 이름은 자유(Humanoid 매핑으로 연결). 매핑되지 않는 추가 뼈는 있어도 된다. | 확정(사용자 답변) | Unity Humanoid는 추가 뼈 허용 |

## 2. 프리팹 구성

| # | 내용 | 구분 | 근거 |
|---|---|---|---|
| 2a | 루트(Animator가 있는 오브젝트)에 `HumanoidCharacter`, `FaceExpression`을 붙인다. | 확정(사용자 답변) | 현재 `InfectedUnitychan` 구성 |
| 2b | **Animator Controller는 비워 둔다.** 표정·자세를 덮어쓰는 스크립트(Unity-chan의 `FaceUpdate`, `AutoBlink` 등)는 붙이지 않는다. | 확정 | 코드가 매 프레임 뼈 회전·블렌드셰이프를 직접 넣음. 있으면 덮어써짐 |
| 2c | 흔들림 플러그인을 쓰면 실행 순서를 `HumanoidCharacter` → 흔들림 순으로 둔다(Script Execution Order). | AI 제안 | 반대면 한 프레임 전 자세 기준으로 흔들려 늦게 따라오거나 떨림 |

## 3. 블렌드셰이프

| # | 내용 | 구분 | 근거 |
|---|---|---|---|
| 3a | 같은 이름의 블렌드셰이프가 여러 메시에 있으면 함께 움직인다. | 확정 | `FaceExpression`이 이름으로 모든 메시를 찾음 |
| 3c | 입 모음은 `A`·`I`·`U`·`E`·`O` 이름으로 uLipSync 음소와 연결한다(다른 이름이면 모음 표에서 바꿈). | 확정(사용자 답변) | `FaceExpression` 모음 표, Char-2 |
| 3d | `FaceExpression` 기본 대응표는 **ARKit 이름 그대로 1:1**(Face §4 표, 범위 0∼1)이다. 모델에 없는 이름은 건너뛰고 시작할 때 경고 한 줄. 다른 이름을 쓴 모델은 프리팹의 대응표를 고친다. 이름 비교는 앞에 붙은 접두어(예: `blendShape1.`)를 무시한다. | 확정(사용자 답변) | 2026-11-24 결정, Face 규약 §4 |
| 3f | 블렌드셰이프 100 = 최대. 과하면 대응표 `maxWeight`로 줄인다. | 확정 | `FaceExpression` |

## 4. FBX 내보내기 (Blender)

| # | 내용 | 구분 | 근거 |
|---|---|---|---|
| 4a | 단위 미터, Apply Transform, Add Leaf Bones 끔, 블렌드셰이프(Shape Keys) 포함. 축은 Unity에서 앞이 +Z가 되게 맞춘다(1d). 첫 내보내기 때 Unity에서 축·크기를 확인하고 설정값을 이 절에 적는다. | 확인 필요 | Blender 버전마다 축 옵션 기본값이 다름. 실측으로 확정 |
| 4b | Unity 임포트: Rig = Humanoid(Avatar 생성), Import BlendShapes 켬. | 확정 | 1a, 3a |

---

## 5. 확인 방법

| 상황 | 기대 결과 | 근거 |
|---|---|---|
| FBX 임포트 후 Rig를 Humanoid로 Configure | 필수 뼈가 모두 초록, 경고 없음 | 1a |
| 플레이, Body 수신 중 | `[Humanoid] 뼈 없음` 경고 없음, 팔·상체가 따라옴. 씬에 BodyReceiver가 없으면 에러 후 꺼짐 | 1a, 1b |
| 플레이, Face 수신 중 | `[Expression] 모델에 없는 블렌드셰이프` 경고에 쓰려는 표정이 없음 | 3d |

---

## 구현 현황

| 항목 | 상태 | 비고 |
|---|---|---|
| Unity-chan(테스트 모델) | 충족: 1a, 1b(Chest·Neck·Shoulder·손가락), 1d, 1e, 2a, 2b, 3a, 3c(`MTH_A`∼`O`, 대응표로 연결) | 2026-11-24 FBX 임포트 설정·블렌드셰이프 이름으로 확인 |
