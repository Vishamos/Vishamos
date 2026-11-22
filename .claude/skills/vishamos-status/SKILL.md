---
name: vishamos-status
description: 사용자가 "현재 상태는?", "상태 확인", "status"라고 하면 Vishamos 진행 상황을 보고한다. worklist(남은 작업), 기술 명세의 "구현 현황", plan_review(남은 질문), 외부 규약의 최신 갱신 여부를 바탕으로 정리한다.
---

# 상태 보고

## 1. 수집 (읽기 전용)
- `docs/work/worklist.md` (맨 위 "현재 진행"과 남은 항목)
- `docs/tech_spec/*.md`의 "구현 현황" 표
- `docs/work/plan_review.md` (남은 질문 수)
- 외부 규약: 명세가 참조한 판(갱신 날짜)과 GitHub HEAD의 갱신 날짜가 다른지 (`gh api`)
- `git status`(커밋 안 한 변경), `git log origin/main..main`(push 안 한 커밋)

## 2. 보고 형식
```
## Vishamos 상태 (YYYY-MM-DD)
- 현재 진행: <worklist "현재 진행">
- 진행도: 명세별 "구현 현황" 요약 (예: 01 완료 3 / 미착수 5)
- 남음: N건 (그룹별 개수)
- 기획 질문: N건 (plan_review)
- 외부 규약 변경: <참조한 판보다 새 판이 있으면 저장소와 절>
- git: 커밋 안 한 변경 N개, push 안 한 커밋 N개
- 다음 추천: <항목 번호와 이유>
```

## 3. 추천 기준
1. worklist "현재 진행"의 우선순위를 따른다. 그 안에서는 실행 불가·크래시 → 연결 → 데이터 → 캐릭터 적용 순서
2. 외부 규약이 바뀌었으면 명세 갱신을 먼저 추천한다.

이 스킬은 파일을 수정하지 않는다.
