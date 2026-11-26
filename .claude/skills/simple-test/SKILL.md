---
name: simple-test
description: Vishamos(Unity C#) 최소 확인. 코드를 고친 뒤 developer나 메인 스레드가 컴파일 오류가 없는지 확인할 때 사용한다. Unity 에디터를 띄우지 않고 dotnet build로 Assembly-CSharp를 컴파일한다.
---

# 최소 확인 (컴파일)

## 실행
프로젝트 루트에서:
```bash
dotnet build Assembly-CSharp.csproj -nologo -v q -clp:ErrorsOnly
```
- 통과: 출력에 `오류 0개`(영문 `0 Error(s)`), 종료 코드 0. 경고는 실패로 보지 않는다.
- 실패: `<파일>(<줄>,<열>): error CS…` 줄을 보고 고친다.
- 에디터 스크립트(`Editor/` 폴더)를 고쳤으면 `Assembly-CSharp-Editor.csproj`도 같은 방식으로 빌드한다.
- 출력은 `Temp/Bin/`·`obj/`에 생기며 git에서 제외되어 있다. Unity 에디터가 열려 있어도 실행해도 된다.

## 한계 (보고에 적는다)
- `.csproj`는 Unity 에디터가 만든다. **새 `.cs` 파일**을 추가했는데 `.csproj`에 아직 없으면(`grep -c "<새 파일 이름>" Assembly-CSharp.csproj`가 0) 그 파일은 컴파일되지 않는다. 이때는 통과로 보지 않고 "사용자가 에디터에 포커스해 `.csproj` 갱신 필요"로 보고하고 멈춘다.
- `.csproj`나 `dotnet`이 없으면 우회하지 않고 중단 보고한다(`setup-check`).
- 컴파일만 본다. 실행 동작(씬, 인스펙터 연결, 트래킹 수신)은 사용자가 확인할 것으로 보고에 적는다.
