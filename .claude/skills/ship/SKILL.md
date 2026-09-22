---
name: ship
description: 릴리즈 — 검증 통과 확인 후 버전을 올리고 Web 빌드를 만들어 크기·힙을 기록합니다. 배포할 준비가 됐을 때 사용합니다.
---

# /ship - 릴리즈

1인 프로젝트다. PR·리뷰 승인 절차는 없다. **빌드가 예산 안에 들어오는지**가 릴리즈 게이트다.

## 프로젝트 컨텍스트

> - 버전: `ProjectSettings/ProjectSettings.asset`의 `bundleVersion` (단일 출처). 서버는 `Server/package.json`의 `version`.
> - 빌드: Unity Editor → Build Profiles → Web. 출력 폴더 `Builds/Web/`(gitignore).
> - 예산: CLAUDE.md 예산표 (다운로드 15MB · 힙 256MB).
> - 배포 방식(호스팅)은 README 「배포」절. 없으면 NEEDS_CONTEXT.

## 워크플로우

### Phase 1: 선행 조건
- [ ] `Unity_GetConsoleLogs` 에러 0
- [ ] 서버 `npm test` 통과 · `npm audit` high 0
- [ ] `/review` 메모리 판정 "위험 없음"
- [ ] README가 현재 씬·API와 일치
하나라도 아니면 BLOCKED.

### Phase 2: 버전
1. `git log`로 마지막 릴리즈 이후 변경 분류: 기능 / 수정.
2. MINOR(새 씬·API) / PATCH(수정). MAJOR는 서버 API 호환이 깨질 때.
3. `bundleVersion`과 서버 `package.json` `version`을 같은 값으로 올린다 (사용자 확인 후).

### Phase 3: 빌드
1. Build Profiles → Web. 릴리즈 설정 확인: Development Build 끔 · Compression Brotli · Exception Support = Explicitly Thrown · Texture Compression ASTC.
2. `webGLAnalyzeBuildSize` 켠 상태로 빌드 → 상위 10개 에셋 크기 확인.
3. `.data + .wasm` 압축 크기 측정. **15MB 초과면 릴리즈하지 않는다.** 큰 에셋을 줄이고 재빌드.

### Phase 4: 실기기 확인
1. 로컬 서버(`python -m http.server` 등, Brotli 헤더 필요 시 Unity 제공 템플릿 사용)로 빌드 서빙.
2. 모바일 Chrome 1대: 첫 로딩 시간 · 12씬 순환 · 탭 종료 없음.
3. DevTools JS heap 피크 기록.

### Phase 5: 기록 · 태그
1. README 「빌드 기록」에 한 줄: `날짜 | 버전 | 다운로드 MB | 힙 피크 MB | 비고`.
2. 커밋·태그(`v1.0.0`)는 **사용자가 한다.** 커밋 메시지 초안만 제시.

## 리포트
```
## Ship Report
### 버전: vX → vY
### 빌드: 다운로드 N MB (예산 15) · 힙 피크 N MB (예산 256)
### 상위 에셋: 이름 - MB (5개)
### 실기기: 로딩 Ns · 순환 ✅/❌
### 커밋 메시지 초안 / 다음: 배포 절차 (README 「배포」)
```

## 완료 상태
- **DONE**: 예산 내 빌드 완료, 기록 갱신
- **DONE_WITH_CONCERNS**: 빌드 완료했으나 예산 근접(90% 이상)
- **BLOCKED**: 선행 조건 미충족 또는 예산 초과
- **NEEDS_CONTEXT**: 배포 대상(호스팅) 미정

## 안전장치
- 예산 초과 빌드는 배포하지 않는다. 기능 축소를 제안한다
- Development Build가 켜진 채로 릴리즈하지 않는다
- 커밋·태그·푸시는 하지 않는다. 사용자에게 넘긴다
