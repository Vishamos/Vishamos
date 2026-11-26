---
name: vishamos-status
description: Vishamos 상태 보고 때 visha-common:status에 더하는 항목. 기술 명세의 "구현 현황" 진행도와 외부 규약(Synapse·Body·Face)의 새 판 여부를 확인한다. 읽기만 한다.
---

# Vishamos 상태 보고 보충

`visha-common:status`의 수집·보고 형식에 아래를 더한다. 파일을 고치지 않는다.

## 추가 수집
| 대상 | 볼 것 |
|---|---|
| `docs/tech_spec/*.md` "구현 현황" 표 | 명세별 완료·미착수 수 |
| 외부 규약 | 명세 머리에 적힌 참조 커밋과 GitHub HEAD가 다른지. `gh api repos/Vishamos/<repo>/commits?path=docs/protocol/README.md&per_page=1 --jq '.[0].sha[:7]'` (repo: VishaSynapse, VishasBody, VishaFace_IOS) |

## 보고에 더할 줄
```
- 진행도: 01 완료 N / 미착수 N, 03 …
- 외부 규약 변경: <참조 커밋보다 새 커밋이 있는 저장소와 바뀐 절> / 없음
```

## 다음 추천에 더할 것
- 외부 규약이 바뀌었으면 공통 순서의 2번(정리 대기) 앞에 "명세 참조 갱신"을 넣는다.
- 같은 순위 안에서는 실행 불가·크래시 → 연결 → 데이터 → 캐릭터 적용 순서.
