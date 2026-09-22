---
name: security
description: 보안 감사 — 서버 API 신뢰 경계·시크릿·CORS·WebGL 노출 정보를 훑습니다. 서버 엔드포인트나 배포 설정을 건드렸을 때 사용합니다.
---

# /security - 보안 감사

## 프로젝트 컨텍스트

> - 구성: Unity WebGL 클라이언트(브라우저, 코드 전부 노출) → HTTPS → Node.js 24(Fastify) 서버 → SQLite(`node:sqlite`)
> - 인증은 **추후**. 현재는 서버 발급 `player_id`(GUID)만. 이 상태의 한계를 알고 리뷰한다.
> - 사내 임직원 대상이지만 URL은 QR로 퍼진다. 외부 접근 가능하다고 가정.

## 역할 원칙

- **WebGL 클라이언트는 전부 공개 코드다.** 클라에 있는 것은 비밀이 아니다.
- 클라이언트가 보낸 값은 검증 대상이지 사실이 아니다.
- 보안 수정은 자동으로 하지 않는다. 항상 사용자 확인 후.

## 워크플로우

### Phase 1: 클라이언트 노출
- 서버 주소 외 시크릿(API 키·DB 문자열·관리자 토큰)이 `Assets/`에 있는가. 있으면 즉시 제거 권고.
- 소식 작성권·관리자 기능을 클라 플래그로만 막는가 → 서버 검사 필요.
- `webGLExceptionSupport` · Development Build가 릴리즈 빌드에 켜져 있는가 (스택·심볼 노출).

### Phase 2: 서버 신뢰 경계
- `player_id`가 헤더로 오는데 존재하지 않는 ID를 서버가 자동 생성해 주는가 (스팸 계정).
- 탭·점수·게시글 쓰기가 **서버 계산**인가. 클라가 `affection=9999`를 보내면 어떻게 되는가.
- 게시글 `body` 길이 상한 · HTML 이스케이프(다른 유저 화면에 표시된다).
- 닉네임 중복확인 엔드포인트가 열거 공격(전 직원 닉네임 훑기)에 쓰일 수 있는가 → 레이트 리밋.
- SQL에 템플릿 문자열·`+` 연결이 있는가 (prepared statement `?`·`@name`만).
- 라우트에 JSON 스키마 없이 `request.body`를 쓰는가 (타입·길이 상한이 스키마에 있어야 함).

### Phase 3: 서버 설정
- `.env`·코드에 시크릿이 있는가 → `process.env`로만 읽고 `.gitignore`에 `.env`·`*.db`.
- CORS(`@fastify/cors`): `origin: true`/`*`인가. 배포 도메인만 허용.
- 리버스 프록시(Caddy·nginx) 뒤에서 HTTPS인가. Node가 80/443을 직접 여는가.
- SQLite 파일이 정적 서빙 경로(`@fastify/static` root) 아래인가 → 밖으로.
- 에러 응답에 스택이 나가는가 (Fastify 기본 에러 핸들러는 500에 메시지만. 커스텀 핸들러가 `err.stack`을 보내는지).
- `fastify.listen({ host })`가 `0.0.0.0`인데 프록시 없이 노출되는가.
- 레이트 리밋(`@fastify/rate-limit`)이 닉네임 중복확인·게시글 쓰기에 있는가.

### Phase 4: 의존성
- `npm audit` 결과 high 이상. `package-lock.json`이 커밋돼 있는가.
- Unity 패키지는 클라 코드라 보안 영향 낮음. 스킵.

### Phase 5: 리포트
```
## Security Audit Report
### 등급: CRITICAL / HIGH / MEDIUM / LOW / CLEAN
### 클라 노출: 시크릿 N건 · 클라 전용 권한 검사 N건
### 신뢰 경계: 클라 값 신뢰 쓰기 N건 · 검증 없는 입력 N건
### 서버 설정: CORS / HTTPS / 시크릿 / DB 경로 ✅/❌
### 의존성: 취약 N
### 즉시 조치: [등급] 설명 → 조치
```

## 완료 상태
- **DONE**: 이슈 없음
- **DONE_WITH_CONCERNS**: 인증 부재 등 알려진 한계만 남음
- **BLOCKED**: 클라 시크릿 노출 또는 클라 값 신뢰 쓰기 존재
- **NEEDS_CONTEXT**: 배포 도메인·호스팅 방식 확인 필요

## 안전장치
- `Assets/` 안 시크릿은 다른 결과와 무관하게 CRITICAL
- 클라이언트가 계산한 수치를 저장하는 엔드포인트는 HIGH
- 자동 수정 금지. 커밋된 시크릿은 키 교체까지 권고
