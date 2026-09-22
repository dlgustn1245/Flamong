---
name: test
description: 검증 — 서버는 node:test, 클라이언트 순수 로직은 Unity Test Framework EditMode, 화면은 에디터 스모크 + Memory Profiler로 확인합니다. 변경한 동작을 검증할 때 사용합니다.
---

# /test - 검증

## 프로젝트 컨텍스트

> - **CLAUDE.md** 「검증」절: Memory Profiler 스냅샷 · 빌드 크기 · 브라우저 힙 기준
> - `com.unity.test-framework`가 설치되어 있다. EditMode 테스트는 가능하지만 **순수 C# 로직에만** 쓴다. MonoBehaviour·씬 테스트는 만들지 않는다.
> - 서버는 `Server/test/*.test.js` + 내장 `node:test`. jest·vitest 등 추가 러너 도입 금지.

## 역할 원칙

- 규칙(성장·제한·랭킹)은 서버에 있으므로 **테스트도 서버에 집중**한다.
- 클라이언트는 스모크(에디터에서 화면 흐름 1회)와 메모리 스냅샷이 테스트다.
- 커버리지 숫자를 목표로 하지 않는다. 깨지면 손해가 큰 경로 하나씩.

## 워크플로우

### Phase 1: 현황
1. `Server/test/` · `Assets/Content/Common/Tests/` 존재 여부와 기존 테스트 목록을 직접 스캔한다. 박제하지 않는다.

### Phase 2: 서버 테스트 (규칙이 바뀌었을 때)
대상: 탭 상한(하루 5회) · 레벨 경계값(15/40/80/150) · 월 키 랭킹 · UNIQUE 제약 위반 시 409.
1. `new Database(':memory:')`로 인메모리 SQLite + `schema.sql` 적용. HTTP 없이 라우트 핸들러가 부르는 함수 단위로 테스트. 라우트 자체는 `app.inject()`로 1~2개만.
2. 날짜는 `now()` 주입. `Date.now()`·`new Date()` 직접 호출이 규칙 코드에 있으면 먼저 지적.
3. `npm test` (`node --test`) 실행.

### Phase 3: 클라이언트 스모크 (씬이 바뀌었을 때)
1. MCP `Unity_RunCommand`로 해당 씬 열기 → Play → 화면 흐름 1회 → `Unity_GetConsoleLogs` 에러 0.
2. **Memory Profiler**: 씬 진입 → 다른 씬 → 되돌아오기 3회 → 스냅샷 2장 비교. Native `Texture2D` 합·Managed 힙이 늘지 않아야 한다.
3. 순수 로직(DTO 파싱·표시 문자열 포맷)만 EditMode 테스트 후보.

### Phase 4: 빌드 검증 (릴리즈 전)
1. Web 빌드 → `.data + .wasm` 압축 크기 15MB 이하.
2. Chrome DevTools → Performance monitor → 화면 10회 순환 → JS heap 초기값 1.5배 이내.
3. 모바일 Chrome 1대에서 실제 접속 확인 (탭 종료 없음).

### Phase 5: 리포트
```
## QA Report
### 서버 테스트: 총 N / 통과 N / 신규 N
### 스모크: 씬 [이름] 흐름 ✅/❌ · 콘솔 에러 N
### 메모리: 스냅샷 전 N MB → 후 N MB (Texture N MB)
### 빌드: 크기 N MB · 브라우저 힙 N→N MB
### 버그: [SEV] 재현 / 기대 vs 실제
```

## 완료 상태
- **DONE**: 서버 테스트 통과 + 스모크 통과 + 메모리 증가 없음
- **DONE_WITH_CONCERNS**: 통과했으나 메모리가 소폭 증가 또는 빌드 미실행
- **BLOCKED**: 메모리 누수 확인 또는 Critical 버그
- **NEEDS_CONTEXT**: 기대 동작 불명확

## 안전장치
- 스냅샷 간 Texture 합이 늘면 다른 항목이 다 통과해도 BLOCKED
- MonoBehaviour를 테스트하려고 씬을 테스트 코드에서 로드하지 않는다
- `Date.now()`가 규칙 코드에 직접 있으면 테스트를 쓰기 전에 주입으로 바꾸라고 먼저 요청
