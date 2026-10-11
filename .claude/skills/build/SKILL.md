---
name: build
description: 구현 — Unity 클라이언트와 Node.js(Fastify) 서버 코드를 씁니다. 기존 코드를 먼저 읽고 최소 변경으로, 메모리 예산 안에서 진행합니다.
---

# /build - 구현

## 프로젝트 컨텍스트

> - **CLAUDE.md**: 코드 컨벤션 · 메모리 규칙(최우선) · 화면 전환 규칙
> - **README.md**: 씬 흐름 · API · 데이터 모델. 여기 없는 구조를 새로 만들면 README도 고친다
> - **Unity MCP**: 씬·프리팹·컴포넌트 작업은 `Unity_RunCommand`로 에디터 안에서 한다. `.unity`·`.prefab` YAML 직접 편집 금지
> 계획(`/plan` 결과 또는 사용자 지시)이 불명확하면 추측하지 않고 **NEEDS_CONTEXT**.

## 역할 원칙

- 코드를 쓰기 전에 기존 코드를 읽는다. 같은 일을 하는 스크립트가 있으면 재사용.
- YAGNI. 지금 씬에 필요한 것만. 인터페이스·매니저·싱글톤을 미리 만들지 않는다.
- **메모리 규칙과 기능이 충돌하면 기능을 줄이고 보고한다.**
- 커밋은 사용자가 한다.

## 워크플로우

### Phase 1: 범위 확인
1. 만들 것이 씬 / 스크립트 / 서버 API / 테이블 중 어디에 걸리는지 나열한다.
2. 건드릴 파일 목록을 먼저 보여주고 승인을 받는다 (CLAUDE.md 「수정 전 확인」).

### Phase 2: 서버 (해당 시)
1. `Server/`(Node.js 24 · Fastify · `node:sqlite`)에 테이블(`schema.sql`)·라우트 추가. 라우트 파일은 기능 단위 1개(`routes/tap.js` 등). 검증·계산은 `rules/`의 순수 함수.
2. 규칙은 서버에서 계산. UNIQUE 제약으로 풀 수 있는 규칙(하루 1회 등)은 코드로 검사하지 않고 제약 위반 에러를 409로 매핑한다.
3. 요청 본문은 Fastify JSON 스키마로 검증. 검증 없는 `request.body` 접근 금지.
4. 탭·점수처럼 읽고-계산하고-쓰는 경로는 `db.exec('BEGIN')` … `COMMIT` 안에서 (`try/catch`로 `ROLLBACK`).
5. 모든 쓰기 라우트는 `event` 테이블에 한 줄 남긴다.
6. 날짜는 `now()` 함수 주입으로 받는다. `Date.now()`·`new Date()` 직접 호출은 진입점 1곳만.
7. `npm test` 1회.

### Phase 3: 클라이언트
1. **씬 하나 = 화면 하나.** 씬 안의 루트 오브젝트에 그 화면의 컨트롤러 스크립트 1개.
2. 스크립트 작성 순서: 데이터(SessionState/DTO) → API 호출 → UI 바인딩.
3. `Awake`에서 참조 캐시. `Update`에 할당 없음. `OnDestroy`에 `-=`·`StopAllCoroutines`·런타임 Object `Destroy`.
4. 서버 통신은 `UnityWebRequest` + `JsonUtility`. `using`으로 감싼다.
5. 에셋 임포트 설정: 압축 · Read/Write 끔 · UI Mipmap 끔 · 크기 상한(CLAUDE.md 예산표).
6. 컴파일 결과를 `Unity_GetConsoleLogs`로 확인. 에러 0.

### Phase 4: 셀프 체크
- [ ] CLAUDE.md 「절대 금지」 8항목 위반 없음
- [ ] 「반드시 해제」 6항목 중 해당하는 것 전부 `OnDestroy`에 있음
- [ ] 새 씬이면 `LoadSceneMode.Single` + 진입 후 `UnloadUnusedAssets` 1회
- [ ] 새 에셋 크기가 예산표 안
- [ ] 규칙 계산이 클라이언트에 없음
- [ ] README의 씬/API/모델 절이 현재와 일치

### Phase 5: 보고
```
## Build Report
### 구현: 씬 / 스크립트 / API / 테이블
### 파일: path (신규/수정)
### 메모리: 새 에셋 합 N MB · 해제 처리 위치
### 남은 것 / 우려
```

## 완료 상태
- **DONE**: 컴파일 클린, 셀프 체크 통과
- **DONE_WITH_CONCERNS**: 완료했으나 예산 근접 또는 확인 못 한 항목
- **BLOCKED**: 예산 초과로 설계 변경 필요, 또는 의존 정보 부족
- **NEEDS_CONTEXT**: 요구사항 불명확

## 안전장치
- 승인 없이 파일을 고치지 않는다
- `Thread`·`Task.Run`·`Parallel`을 쓰는 코드는 쓰지 않는다 (WebGL 단일 스레드)
- `Resources/` 폴더에 에셋을 넣지 않는다
- 컴파일 에러가 남아 있으면 DONE 처리하지 않는다
