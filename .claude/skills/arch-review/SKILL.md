---
name: arch-review
description: 구조 검토 — 씬 단위 분리·Session 데이터 흐름·클라/서버 책임 경계·리소스 정리를 실제 코드와 대조합니다. 구조를 바꾸기 전에 사용합니다.
---

# /arch-review - 구조 검토

## 프로젝트 컨텍스트

> - **CLAUDE.md** 「화면 전환 규칙」「서버 우선 계산」: 기대 구조의 정본
> - **README.md**: 씬 목록 · 폴더 구조 · API · 데이터 모델
> 기대 구조를 스킬에 박제하지 않는다. 위 소스와 실제 트리를 대조.

## 기대 구조 (CLAUDE.md 요약 · 여기서 어긋나면 지적)

```
Assets/Content/<씬>/01_Scenes/    씬 1개 = 화면 1개, Single 로드
Assets/Content/<씬>/02_Scripts/   그 씬의 컨트롤러 (다른 씬 폴더에서 참조 금지)
Assets/Content/<씬>/03_Art/       씬별 텍스처·오디오
Assets/Content/Common/02_Scripts/ Session(static POCO, 아직 없음) · Manager/Network(NetworkManager · ServerData DTO) · Util(SceneLoader · SceneFader · Singleton)
Assets/Content/Common/03_Art/     공용 UI 에셋
Server/
  index.js                        Fastify 앱 조립 · 포트 · now() 주입 (진입점)
  db.js                           node:sqlite 열기 · schema.sql 적용 · logEvent
  schema.sql                      테이블 · UNIQUE 제약 (단일 출처)
  routes/<기능>.js                라우트 + JSON 스키마
  rules/<기능>.js                 순수 함수 규칙 (DB·HTTP 몰라야 함)
  test/*.test.js                  node:test
```
의존 방향: `씬 스크립트 → Common → (HTTP) → Server`. 역방향·씬 간 직접 참조 금지.

## 워크플로우

### Phase 1: 트리 대조
1. `Assets/Content/*` · `Server` 실제 목록을 본다. 접두 번호 없는 폴더나 `Resources` 폴더가 있으면 지적.
2. 기대 구조와 다른 곳을 나열한다. README와도 대조.

### Phase 2: 씬 경계
1. 씬 A의 스크립트가 씬 B의 타입을 참조하는가.
2. `DontDestroyOnLoad`가 NetworkManager(Singleton) 외에 있는가.
3. static 필드에 `GameObject`·`Component`·`Texture` 참조가 있는가 (씬 언로드 후 누수).
4. `Additive` 로드가 있는가.

### Phase 3: 책임 경계
1. 애정도·레벨·랭킹·일일 제한을 계산하는 코드가 클라이언트에 있는가.
2. 서버 엔드포인트가 클라이언트가 보낸 결과값을 그대로 저장하는가.
3. 하루 1회 규칙이 DB 제약 없이 코드에만 있는가.

### Phase 4: 리소스 흐름
```
서버 → UnityWebRequest → DTO → Session → 씬 컨트롤러 → UI
                          ↑ Dispose      ↑ 씬 언로드 시 참조 소멸
```
1. 위 흐름에서 생성된 것이 어디서 해제되는지 추적. 해제 지점이 없는 생성이 있으면 지적.
2. 에셋 로드 경로: 씬 참조 / Addressables 외(`Resources.Load`)가 있는가.

### Phase 5: 의존성
1. `Packages/manifest.json`: 쓰지 않는 패키지(2D Animation · Aseprite · PSD Importer · SpriteShape · Tilemap · Visual Scripting · Timeline · IET Framework · AI Assistant)가 빌드 크기·컴파일 시간을 늘린다. 실제 사용 여부 확인 후 제거 권고.
2. `Server/package.json`: `fastify` · `@fastify/cors` 외 의존성이 있으면 이유 확인. `rules/`가 `db`나 `request`를 import하면 지적.

## 리포트
```
## Architecture Review Report
### 씬 경계: ✅/⚠️/❌ (위반 목록)
### 책임 경계(서버 vs 클라): ✅/⚠️/❌
### 리소스 흐름: 해제 누락 N건
### 의존성: 미사용 패키지 N개 (제거 권고 목록)
### 권고: [우선순위] 조치
```

## 완료 상태
- **DONE**: 구조 건전
- **DONE_WITH_CONCERNS**: 진행 가능하나 부채 있음
- **BLOCKED**: 씬 간 직접 참조 또는 클라 규칙 계산이 구조적으로 박혀 있음
- **NEEDS_CONTEXT**: README에 구조가 없어 기대치를 세울 수 없음

## 안전장치
- 씬 간 직접 참조 3건 이상이면 BLOCKED
- 클라이언트 규칙 계산이 1건이라도 있으면 ⚠️ 이상
- 패키지 제거는 권고만. 실제 제거는 사용자 승인 후
