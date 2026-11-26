---
name: coding-rule
description: Vishamos C#·Unity 코드 표준. 코드를 작성·수정·검토할 때 visha-common:coding-common-rule과 함께 따른다. 명명, 구조, 로그, 방송 화면, 성능(60fps), 비밀 값 참조를 다룬다.
---

# C#·Unity 코드 표준

기존 코드(`Assets/Scripts/`)의 관례를 따른다. 여기 없는 것은 주변 파일을 따른다.

## 1. 형식
- 파일 하나에 주 클래스 하나, 파일 이름 = 클래스 이름. 네임스페이스는 쓰지 않는다(기존과 같게).
- 중괄호는 다음 줄(Allman), 들여쓰기 4칸.
- 명명: 클래스·메서드·프로퍼티 `PascalCase`, private 필드 `camelCase`(접두어 없음), 상수 `PascalCase`, enum은 `E` 접두어(`EFaceBlendShape`).
- 인스펙터 노출은 `[SerializeField] private`. public 필드를 쓰지 않는다.
- 싱글턴 매니저는 `SingletonMonoBehaviour<T>`를 상속하고 `OnAwake`를 쓴다.

## 2. 주석
- 코드나 명세를 다시 말하는 주석은 쓰지 않는다. 쓰는 것: 명세 절 참조(`명세 01 §4.2`, `plan_review 3-4`), 코드만 봐서는 알 수 없는 이유 한 줄.
- 한국어로 쓴다.

## 3. 로그와 방송 화면
- 로그 접두어 `[영역]`(예: `[Humanoid]`, `[Config]`). 사용자가 할 조치를 함께 적는다.
- 매 프레임·매 메시지 로그 금지. 반복되는 경고는 처음 한 번만.
- 서버 주소, 받은 바이너리 전체, 예외 객체 전체를 로그에 넣지 않는다(`e.Message`까지).
- **Game 뷰에 그리는 것**(`OnGUI`, UI, Game 뷰 Gizmos, 디버그 메시)은 기본값 꺼짐 + 인스펙터 토글로 끄고 켠다. 확인용 값은 인스펙터 전용 필드(`view…`)로 보인다.
- qa 시나리오 로그는 `qa-test`의 로그 규칙을 따른다.

## 4. 성능 (목표 60fps, Unity·OBS·VishasBody 동시 실행)
- `Update`·`LateUpdate`에서 할당하지 않는다: 배열·리스트는 미리 만들어 덮어쓰고, LINQ·문자열 결합·`GetComponent`·`Find`를 쓰지 않는다.
- 네트워크 수신 스레드에서 Unity API를 부르지 않는다. 메인 스레드에서 최근 프레임을 읽는다(`SocketManager`·`…Receiver` 구조).

## 5. 외부 입력
- 바이너리는 길이·`kind`·`version`을 확인한 뒤 읽는다. 규약 오프셋은 `const`로 두고 명세 절을 붙인다.
- 엔디언은 `BinaryPrimitives`로 명시한다.

## 6. 의존성·설정
- 패키지 추가는 사용자에게 요청한다(`Packages/manifest.json`을 직접 고치지 않음).
- 의존성 점검 명령: 없음(Unity 패키지). security는 "의존성 점검 명령 없음"으로 보고하고, `manifest.json`의 git URL 패키지 목록만 적는다.
- 서버 주소 등 PC별 값은 `FileIO`를 통해 `config.json`에서 읽는다. 코드에 기본 주소를 넣지 않는다.
