# Project Overview

Flamong(플라몽) — 사내 감정 캐릭터 키우기 웹 게임. Unity 6000.6.0f1, URP 2D, **WebGL 빌드**.
1인 개발, 일정 없음. 서버는 **Node.js 24(Fastify) + 내장 `node:sqlite`**로 직접 구현 (`Server/`). 학습 목적이므로 프레임워크·ORM을 더 얹지 않는다. 의존성은 `fastify`·`@fastify/cors` 2개.
2.0에서 Android 빌드 + Unity Ads 추가 예정 (Unity Ads는 WebGL 미지원).

-   **타겟 해상도**: 1080 x 1920 (세로). QR로 접속하는 **모바일 브라우저**가 주 대상.
-   **타겟 환경**: 사내 임직원 폰. 저사양 안드로이드까지 동작해야 함.
-   **기획서**: https://seobukcorp.atlassian.net/wiki/spaces/Plinqer/pages/74711134 (로그인 필요)

------------------------------------------------------------------------

# 문서

폴더 구조, 씬 흐름, 서버 API, 데이터 모델은 `README.md`가 단일 출처다.
작업 전에 README를 보고, 구조나 API를 바꿨으면 README도 같이 고친다.

------------------------------------------------------------------------

# 도구

-   **Unity MCP 연결됨**: `Unity_RunCommand`로 에디터 안에서 C# 실행, `Unity_GetConsoleLogs`로 콘솔 확인.
    씬·프리팹·에셋 수정은 파일을 직접 고치지 말고 MCP로 에디터를 통해 한다.
    -   `AssetDatabase.DeleteAsset`·`MoveAsset`은 에디터 확인창 때문에 MCP에서 실패한다. 파일과 `.meta`를 함께 직접 옮기거나 지운 뒤 `AssetDatabase.Refresh()`.
    -   MCP 코드는 래퍼 네임스페이스 안에서 돈다. `Image`·`Button`은 이름이 겹치므로 `using UImage = UnityEngine.UI.Image;`처럼 별칭을 쓴다. `System.Reflection`·`Regex`는 쓸 수 없다.
    -   꺼진 오브젝트의 Canvas `overrideSorting`·`sortingOrder`는 프로퍼티로 넣으면 저장되지 않는다. `SerializedObject`로 넣는다.
    -   스크립트를 고치면 재컴파일 동안 MCP 연결이 잠시 끊긴다. 확인은 컴파일이 끝난 뒤에 한다.
    -   확인용으로 씬을 바꿨다면 저장하지 말고 `EditorSceneManager.OpenScene`으로 다시 열어 버린다.
-   빌드: File → Build Profiles → Web. 빌드 후 `Build Report`의 크기와 힙 피크를 README에 기록.
-   서버: `Server/`에서 `npm start` (실행) · `npm test` (node:test). 패키지 추가는 사용자 확인 후.

------------------------------------------------------------------------

# 코드 컨벤션

-   **필드 노출**: `[SerializeField] private` 대신 **`public` 필드**를 사용한다.
-   **필드명**: 언더바(`_`) 접두사 사용 **금지**. `camelCase` 그대로 사용.
-   **네임스페이스**: 전역 네임스페이스. 폴더명과 같은 네임스페이스를 만들지 않는다.
-   **Unity 오브젝트 null 검사**: `UnityEngine.Object`에는 `??`·`?.`·`is null`을 쓰지 않는다. 파괴된 오브젝트를 null로 보지 못한다. `== null` 또는 `if (obj)`.
-   **중괄호**: `if` · `else` · `for` · `foreach` · `while`은 본문이 한 줄이어도 항상 중괄호로 감싼다. `if (x) return;`처럼 한 줄로 쓰지 않는다.
-   **주석**: 한국어, WHY가 비자명할 때만 작성.
-   **서버 통신**: `UnityWebRequest` + JSON만. WebSocket·gRPC·서드파티 HTTP 라이브러리 금지.
-   **서버(Node.js)**: ESM(`import`), `async/await`, 플레인 JS + JSDoc 타입 주석. TypeScript·ORM·Express 미들웨어 생태계 도입 금지.
    SQL은 `node:sqlite`(`DatabaseSync`) prepared statement로 직접 쓴다. 규칙은 UNIQUE 제약과 `BEGIN`/`COMMIT` 트랜잭션으로 푼다.
    규칙(검증·계산)은 `rules/`의 순수 함수. `now()`는 `index.js`에서 주입받고 `new Date()` 직접 호출 금지.

------------------------------------------------------------------------

# 메모리 관리 (최우선 규칙)

WebGL은 브라우저 탭 하나의 힙 안에서 돈다. 모바일 브라우저는 힙이 커지면 경고 없이 탭을 죽인다.
**GC는 있지만 WebGL에선 GC가 프레임 사이에만 돌고, 네이티브 메모리는 GC 대상이 아니다.**

## 예산 (넘으면 기능이 아니라 예산을 지킨다)

| 항목 | 상한 |
|---|---|
| WASM 힙 피크 | **256 MB** (ProjectSettings: Initial 32 / Maximum 512, Growth = Geometric) |
| 빌드 다운로드 크기 (.data + .wasm, 압축 후) | **15 MB** |
| 개별 텍스처 | 배경 1024², UI 스프라이트 512² 이하 |
| 씬당 동시 로드 텍스처 합 | 48 MB 이하 |
| 오디오 클립 | BGM 1개 Streaming, 효과음 5개 이하 CompressedInMemory |

## 절대 금지

-   **`Update`/`LateUpdate`/코루틴 루프 안에서 할당**: `new`, LINQ, `string +`, `foreach` on 인터페이스, 박싱, `GetComponent`, `Find`.
    필요한 참조는 `Awake`에서 캐시. 문자열은 `TMP_Text.SetText(format, value)` 사용.
-   **`Thread`, `Task.Run`, `Parallel`**: WebGL은 단일 스레드. 코루틴 또는 메인 스레드 `async/await`만.
-   **`Texture2D.isReadable = true`**: CPU·GPU 양쪽에 복사본이 생겨 메모리 2배. 임포트 설정에서 Read/Write 끈다.
-   **`Resources.Load` 남용**: `Resources/` 폴더는 전부 빌드에 포함되어 다운로드 크기를 늘린다. 씬 참조 또는 Addressables.
-   **비압축 텍스처**: 임포트 시 반드시 압축(WebGL 빌드 설정 Texture Compression = ASTC, Fallback DXT). UI는 Mipmap 끈다.
-   **`DontDestroyOnLoad` 남용**: `Singleton<T>`인 `NetworkManager` · `SessionState` · `SoundManager` 3개만 허용. 셋 다 Title 씬에 둔다. `SoundManager`는 배경음이 씬을 넘어가도 끊기지 않게 하려고 둔다. 화면 오브젝트는 씬과 함께 죽는다.
-   **`Instantiate`/`Destroy` 반복**: 하트·파티클 같은 반복 생성물은 **오브젝트 풀**. 풀 크기는 상한을 두고 초과 시 재사용.
-   **예외 지원 Full**: `webGLExceptionSupport`는 1(명시적 throw만) 유지. Full은 코드 크기 30% 증가.

## 반드시 해제 (소유한 쪽이 `OnDestroy`에서)

-   **런타임에 만든 `UnityEngine.Object`** (`Texture2D`, `Material`, `Mesh`, `RenderTexture`, `AudioClip`, `Sprite`): `Destroy()`.
    `renderer.material` 접근은 인스턴스를 만든다 → `sharedMaterial` 사용 또는 만든 인스턴스를 `Destroy()`.
-   **`UnityWebRequest`**: `using` 또는 `Dispose()`. `DownloadHandlerTexture`로 받은 텍스처는 별도로 `Destroy()`.
-   **이벤트 `+=`**: static·싱글톤·서버 클라이언트 이벤트는 `OnDisable`/`OnDestroy`에서 `-=`. `Button.onClick.AddListener`는 `RemoveAllListeners`.
-   **코루틴**: 소유 오브젝트가 사라지기 전에 `StopAllCoroutines()`. 코루틴 안에서 `WaitForSeconds`는 필드에 캐시해 재사용.
-   **트윈·애니메이션**: 시작한 쪽이 `OnDestroy`에서 Kill. Forever 루프는 대상이 사라져도 계속 돈다.
-   **`AudioClip`**: `Streaming`이 아닌 클립을 코드로 로드했으면 `AudioClip.UnloadAudioData()` 후 `Destroy()`.

## 화면 전환 규칙

1.  화면마다 **씬 1개**, `Assets/Content/<씬>/01_Scenes/<씬>.unity`. `LoadSceneMode.Single`만 사용. Additive 금지.
    씬 스크립트는 자기 `02_Scripts/`와 `Common/02_Scripts/`만 참조한다. 폴더 이름에 `Resources`를 쓰지 않는다.
2.  씬 로드 직후 `Resources.UnloadUnusedAssets()` → `GC.Collect()` 순서로 1회 호출. 그 외 위치에서 `GC.Collect()` 금지.
3.  씬 간 데이터는 `SessionState`(싱글톤) 하나로만 전달. 값(문자열 · 숫자 · DTO)만 담고 씬 오브젝트 참조는 담지 않는다.
    싱글톤이 Title 씬에 있으므로, 중간 씬을 에디터에서 바로 플레이하면 `Instance`가 null이다. Title부터 플레이한다.
4.  씬 진입 시 필요한 에셋만 로드. 12개 씬 배경을 한 아틀라스에 넣지 않는다 (씬별 아틀라스).

## UI 캔버스 규칙

-   **매 프레임 움직이는 UI**(스크롤 목록, 회전)는 `Canvas - Dynamic`(그리기 순서 1)에 따로 둔다. 캔버스 안의 하나가 바뀌면 그 캔버스 전체가 다시 그려지기 때문이다.
-   **팝업**은 `Canvas - Main` 아래 하위 Canvas(Override Sorting, 순서 2) + GraphicRaycaster로 만들고 기본은 꺼 둔다. GraphicRaycaster가 없으면 버튼이 눌리지 않는다.
    공통 프리팹은 확인 창(`Panel - Confirm`) 3, 오류 창(`Panel - Error`) 4. 같은 순서끼리는 위아래가 보장되지 않으므로 겹쳐 뜰 수 있는 창은 숫자를 나눈다.
-   누를 것이 없는 캔버스에는 GraphicRaycaster를 붙이지 않는다. 장식 이미지·글자는 Raycast Target을 끈다.
-   스크롤 뷰포트는 Mask 대신 RectMask2D. 가장자리 페이드는 RectMask2D Softness로 한다.
-   UI에 Animator를 붙이지 않는다. 값이 그대로여도 매 프레임 UI를 다시 그리게 만든다. 움직임은 코루틴이나 `Update`로 직접 준다.
-   레이아웃 그룹은 바뀔 때만 계산되므로 정적 목록에는 써도 된다. 중첩은 하지 않는다.
-   구성 현황은 README 「UI 캔버스 구성」.

## 검증 (기능 하나 끝날 때마다)

-   Editor: **Memory Profiler** 패키지로 씬 진입 → 3회 전환 → 스냅샷. Native `Texture2D` 합과 Managed 힙이 전환마다 늘지 않아야 한다.
-   Build: `webGLAnalyzeBuildSize` 켜고 빌드 → 상위 10개 에셋 크기 확인. 예산 초과 시 기능 커밋 전에 줄인다.
-   Browser: Chrome DevTools → Performance monitor → JS heap. 화면 10회 순환 후 초기값의 1.5배 이내.
-   `Unity_GetConsoleLogs`로 빌드·실행 경고 0 확인.

------------------------------------------------------------------------

# 작업 방식

-   **수정 전 확인**: 파일을 고치기 전에 바꿀 내용을 먼저 보여주고 승인을 받는다.
-   **Simplicity First**: 최소 변경, 근본 원인 해결. 필요한 곳만 건드림. 요청 범위 밖 리팩터링 금지.
-   **메모리 먼저**: 모든 구현은 위 예산표 안에서 설계한다. 예산과 기능이 충돌하면 기능을 줄이고 그 사실을 알린다.
-   **서버 우선 계산**: 성장·랭킹·일일 제한은 서버가 계산한다. 클라이언트는 표시만.
-   **커밋은 사용자가 직접**: 명시 요청 없이 커밋하지 않는다.
