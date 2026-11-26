---
name: vishamos-git
description: Vishamos에서 커밋할 때 visha-common:git-commit에 더하는 항목. 서브모듈 Assets/Imports, Unity 파일(.meta, 씬·프리팹, 생성 파일) 커밋 범위를 다룬다. 커밋 절차·메시지 형식·이슈는 공통 스킬을 따른다.
---

# Vishamos 커밋 보충

절차·메시지 형식·금지 명령은 `visha-common:git-commit`, 이슈는 `visha-common:git-issue`를 따른다. 여기에는 이 저장소에만 있는 것만 둔다.

## 서브모듈 `Assets/Imports/`
- 비공개 서브모듈(내부 Gitea)이다. 서브모듈 **안**에서는 커밋·push하지 않는다(사용자가 한다).
- 본 저장소의 서브모듈 참조 갱신(`git add Assets/Imports`)은 사용자가 서브모듈을 커밋했다고 알려 준 뒤에만 한다. 메시지는 `[ADD] : 서브모듈 Assets/Imports 참조 갱신(<서브모듈 커밋 해시> <요약>)`.
- 서브모듈의 파일 이름·경로는 커밋 메시지에 적어도 되지만, 서브모듈 서버 주소는 적지 않는다.

## Unity 파일
- 새 `.cs`·에셋을 커밋할 때 Unity가 만든 짝 `.meta`도 함께 넣는다. `.meta`가 아직 없으면(에디터가 아직 읽지 않음) 커밋하지 않고 사용자에게 에디터 포커스를 요청한다.
- 씬(`*.unity`)·프리팹은 사용자가 에디터에서 바꾼 것이다. 이번 작업으로 사용자가 바꿨다고 확인된 것만 넣는다. 작업 폴더에 바뀐 채 있어도 묻지 않고 섞지 않는다.
- `*.csproj`, `*.sln`, `Library/`, `Temp/`, `obj/`, `Logs/`는 커밋하지 않는다(`.gitignore`).
