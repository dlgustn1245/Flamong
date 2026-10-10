---
name: review
description: 코드 리뷰 — 변경분을 메모리·WebGL 제약·서버 신뢰 경계·컨벤션으로 훑고, 바로 고칠 것과 사람이 판단할 것을 나눕니다. 커밋 전에 사용합니다.
---

# /review - 코드 리뷰

## 프로젝트 컨텍스트

> - **CLAUDE.md**: 메모리 규칙(절대 금지 · 반드시 해제 · 화면 전환) · 코드 컨벤션
> - **README.md**: 씬 흐름 · API · 데이터 모델과 diff가 어긋나는지
> 규칙 본문은 CLAUDE.md가 진실. 스킬에 다시 적지 않는다.

## 역할 원칙

- 스타일보다 **메모리 누수와 규칙 위치(서버 vs 클라)** 에 집중한다.
- 자동으로 고칠 수 있는 것은 직접 고친다. 설계 판단은 묻는다.
- 명확한 이유 없이 변경을 요구하지 않는다.

## 워크플로우

### Phase 1: 범위
1. `git diff`로 변경 파일 확인. `.unity`·`.prefab`·`.asset` 변경은 MCP로 에디터에서 실제 상태를 본다.
2. README가 가리키는 구조와 diff가 맞는지. 새 씬/API/테이블이 README에 없으면 지적.

### Phase 2: 정적 확인
1. 테스트 프레임워크는 있지만(Unity Test Framework · node:test) 커버리지를 강요하지 않는다. 컴파일 + 정적 추론 우선.
2. `Unity_GetConsoleLogs`로 에러·경고 확인. 서버는 `npm test`.

### Phase 3: 이 저장소에서 반드시 보는 것

**메모리 (WebGL · 모바일 브라우저)** — CLAUDE.md 「절대 금지」「반드시 해제」와 대조
- `Update`/`LateUpdate`/코루틴 루프 안에 `new`·LINQ·`string +`·`GetComponent`·`Find`가 있는가
- `Thread`·`Task.Run`·`Parallel`이 있는가
- 이벤트 `+=`에 대응하는 `-=`가 `OnDisable`/`OnDestroy`에 있는가. `AddListener`에 `RemoveAllListeners`가 있는가
- 런타임에 만든 `Texture2D`·`Material`·`Sprite`·`RenderTexture`를 `Destroy`하는가. `renderer.material` 접근이 있는가
- `UnityWebRequest`가 `using` 안에 있는가. `DownloadHandlerTexture` 결과를 `Destroy`하는가
- 코루틴·트윈을 `OnDestroy`에서 멈추는가
- `Instantiate`/`Destroy` 반복이 풀 없이 있는가
- `DontDestroyOnLoad`가 NetworkManager(Singleton) 외에 붙었는가
- static 필드에 씬 오브젝트 참조를 담았는가
- 새 씬이 `LoadSceneMode.Additive`인가. 씬 진입 후 `UnloadUnusedAssets` 외 위치에 `GC.Collect`가 있는가

**에셋**
- 새 텍스처의 임포트 설정: 압축 · Read/Write 끔 · UI Mipmap 끔 · 크기 상한
- `Resources/` 폴더에 들어간 것이 있는가
- 오디오가 `DecompressOnLoad`인가 (BGM은 Streaming)

**서버 신뢰 경계**
- 애정도·레벨·점수·일일 카운트를 **클라이언트가 계산해서 보내는가** → 서버 계산으로
- 서버가 `player_id`를 검증 없이 신뢰하는 쓰기가 있는가 (인증은 추후지만 최소 존재 확인)
- 쓰기 라우트가 `event`를 남기는가
- 하루 1회 규칙이 UNIQUE 제약 없이 코드 검사만으로 있는가
- SQL에 템플릿 문자열·`+` 연결이 있는가 (prepared statement만)
- 라우트에 JSON 스키마 없이 `request.body`를 쓰는가
- 읽고-계산하고-쓰는 경로가 `BEGIN`/`COMMIT` 밖에 있는가
- `Date.now()`·`new Date()`가 진입점 외에 흩어져 있는가 (테스트 불가)
- `package.json`에 새 의존성이 들어왔는가 (`fastify` · `@fastify/cors` 외는 이유 확인. SQLite는 내장 `node:sqlite`)

**컨벤션**
- `[SerializeField] private` · `_camelCase` · 폴더명 네임스페이스가 있는가
- 서드파티 HTTP·WebSocket 라이브러리가 들어왔는가

### Phase 4: 분류
**AUTO-FIX**: 미사용 using, 누락된 `-=`/`Dispose`/`Destroy`, 컨벤션 위반
**ASK**: 규칙 위치 이동(클라→서버), 씬 구조 변경, 예산 초과 에셋 축소

### Phase 5: 리포트
```
## Code Review Report
### 요약: 파일 N / AUTO-FIX N / ASK N
### AUTO-FIX (완료): path:line - 내용
### ASK: [HIGH|MED|LOW] path:line - 현재 → 제안
### 메모리 판정: 누수 위험 없음 / 위험 N건
### 판정: APPROVE / REQUEST_CHANGES
```

## 완료 상태
- **DONE (APPROVE)**: 커밋 가능
- **DONE_WITH_CONCERNS**: 커밋 가능하나 개선 권고
- **BLOCKED (REQUEST_CHANGES)**: 메모리 위반 또는 클라 규칙 계산 있음
- **NEEDS_CONTEXT**: 변경 의도 확인 필요

## 안전장치
- 메모리 「절대 금지」 위반 1건이라도 있으면 APPROVE 하지 않는다
- 씬·프리팹 YAML을 리뷰어가 직접 고치지 않는다 (MCP로)
- 보안 관련은 자동 수정하지 않고 `/security`로 넘긴다
