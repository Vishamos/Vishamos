# Vishamos 구조

## 스크립트 (2026-11-22 기준, 명세 01 구현 전)

```
Assets/Scripts/
  SingletonMonoBehaviour.cs   제네릭 싱글턴 기반 클래스 (Awake에서 Instance 지정)
  MicManager.cs               마이크 입력 → AudioSource 재생, 음량(dB)·피치 계산. 출력은 믹서 MasterVolume으로 끔
  CharacterManager.cs         2D 스프라이트 캐릭터. MicManager.DbValue로 입 위치, 방향키로 몸통 스프라이트 교체(Resources.Load)
  Network/
    Socket.cs                 VishaSynapse WebSocket 접속·수신 (옛 규약: [0,0] 대기 후 바이너리 등록, 시그니처 10)
    Data/MessagePacker.cs     옛 규약용 바이너리 생성·해석 (호출하는 곳 없는 함수 포함)
```

- `Socket.cs`·`MessagePacker.cs`는 VishaSynapse 새 규약(2026-11-18판)과 맞지 않는다. 명세 01로 다시 만든다.
- `CharacterManager`의 `Resources.Load("Texture/...")`는 그림을 `Assets/Imports/Texture/`로 옮긴 뒤 `Resources` 폴더 밖이라 항상 null이다.

## 씬
- `Assets/Scenes/MainScene.unity`: Main Camera(배경 알파 0), MicManager(AudioSource, 믹서), Socket, Character(스프라이트 계층)

## 렌더링
- URP 17.3. Quality 1단계(`Default`) → `Assets/URPDefaultResources/Ultra.asset`. Graphics 기본 파이프라인도 같은 에셋.

## 외부 규약
접속·데이터 형식은 다른 저장소가 기준이다(`CLAUDE.md` 지도). 이 문서에는 옮겨 적지 않는다.
