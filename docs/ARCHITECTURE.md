# Vishamos 구조

## 스크립트 (2026-11-23 기준)

```
Assets/Scripts/
  SingletonMonoBehaviour.cs   제네릭 싱글턴 기반 클래스 (Awake에서 Instance 지정)
  MicManager.cs               마이크 입력 → AudioSource 재생, 음량(dB)·피치 계산. 출력은 믹서 MasterVolume으로 끔
  Core/
    FileIO.cs                 설정 파일(%LOCALAPPDATA%/VishaSTD/vishamos/config.json) 읽기, 없으면 기본값 생성
    MainManager.cs            MainCharacter 하나를 등록해 두는 싱글턴
  Math/
    OneEuro.cs                One Euro 필터(OneEuro), 3축 묶음(EuroVector3). 네임스페이스 MyMath
  Network/
    SocketManager.cs          VishaSynapse 접속·JSON 등록·재접속 3초·수신(64KB 이어 붙이기). from 31 → BodyReceiver
    SynapseProtocol.cs        JSON 메시지 구조체(to, from, type, data)
    Data/BodyReceiver.cs      Body v2 검증·해석·보관(BodyFrame), 항상 미러, FrameId 이벤트, 수신 상태. EBodyParts·EHandParts
  Character/
    SimpleCharacter.cs        2D 스프라이트 캐릭터의 마이크 입 움직임
    MainCharacter.cs          [테스트] Body 점 필터 + 에디터 기즈모 표시(몸·손·루트). 빌드에는 안 나옴
    HumanoidCharacter.cs      [테스트] Humanoid 모델에 Body 적용: 팔·척추·손·손가락(FromToRotation), 루트 위치(원점·배율·F9·F10)
    HandTrack.cs              손 21점 필터(손별 초기화, 반복 값 건너뜀). HumanoidCharacter가 씀
```

- 데이터 흐름: `SocketManager` → `BodyReceiver.Receive` → `OnChangeFrameID` 이벤트 → 각 캐릭터가 필터 → `LateUpdate`에서 적용
- `BodyFrame`의 Left/Right는 **미러 후 캐릭터 기준**이다(명세 01 4.3i). 캐릭터는 +z를 바라보는 기준이라, 모델 루트를 y 180° 돌려 카메라를 보게 한다.
- `MainCharacter`와 `HumanoidCharacter`는 서로 독립이고 필터를 각자 가진다. `MainCharacter`에는 `HandTrack`과 같은 내부 클래스가 따로 있다(정리 대상, worklist Char-1).
- 캐릭터 적용(휴머노이드, 루트, 손)은 **명세 없이 진행한 테스트 구현**이다. 최종 캐릭터 명세는 노션 정리 후(worklist Doc-3).

## 씬·에셋
- `Assets/Scenes/MainScene.unity`: Main Camera(배경 알파 0), 매니저(`Prefab/Core/FileIO.prefab` 안에 SocketManager·BodyReceiver·MainManager 등), 캐릭터 프리팹
- `Assets/Prefab/TestCaptureCharacter.prefab`: 기즈모 테스트용(옛 MainCharacter 프리팹, GUID 유지)
- `Assets/Prefab/InfectedUnitychan.prefab`: Unity-chan + HumanoidCharacter 테스트
- `Assets/Imports/`(비공개 서브모듈): 그림, 테스트용 Unity-chan(머티리얼은 URP로 교체)

## 렌더링
- URP 17.3. Quality 1단계(`Default`) → `Assets/URPDefaultResources/Ultra.asset`. Graphics 기본 파이프라인도 같은 에셋. OBS 투명 배경을 위해 HDR 끔.

## 입력
- Input System 패키지(1.18), 입력 설정은 새 Input System만(`activeInputHandler: 1`). 옛 `Input.*`는 예외가 난다.

## 외부 규약
접속·데이터 형식은 다른 저장소가 기준이다(`CLAUDE.md` 지도). 이 문서에는 옮겨 적지 않는다.
