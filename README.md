# Flamong (플라몽)

사내 감정 캐릭터 키우기 웹 게임. 구성원의 감정을 먹고 자라는 조직의 페르소나.
Unity 6 (6000.6.0f1) · URP 2D · **WebGL** · 세로 1080x1920 · 모바일 브라우저 대상.
1인 개발, 일정 없음. 서버는 Node.js 24(Fastify) + 내장 SQLite(`node:sqlite`)로 직접 구현.

- 규칙·예산: [CLAUDE.md](CLAUDE.md)
- 이 문서가 씬 · 규칙 · API · 데이터 모델의 **단일 출처**다. 기획서와 다르게 정한 것은 「기획서와 다른 점」에 적는다.

---

## 씬 흐름

```
1 홈 → 2 안내문 → 3 로그인 → 4 알 선택 → 5 캐릭터 랜덤 → 6 홈화면
                                                          ├─ 7 설정
                                                          ├─ 8 도감
                                                          ├─ 9 게임 (이달의 게임 달력 → 마음 정리)
                                                          ├─ 10 묘약
                                                          ├─ 11 꽃밭
                                                          └─ 12 소식
```

| # | 씬 | 하는 일 | 서버 호출 | 버전 | 현재 상태 |
|---|---|---|---|---|---|
| 1 | Title | 타이틀. 탭하면 Intro, 「다시 안 보기」를 눌렀던 사람은 바로 들어갈 곳으로(아래 「들어갈 곳」) | `GET /me` | 1.0 | 구현. NetworkManager · SessionState · SoundManager 싱글톤이 이 씬에 있음. 좌상단 `Text - Version`에 `v{Player Settings Version} · {buildGUID 앞 6자리}`(에디터는 `editor`). buildGUID는 빌드마다 바뀌어 배포된 빌드가 최신인지 알아볼 수 있다. Version은 Build 버튼을 누를 때 뜨는 입력 창(`BuildVersionPrompt`)에서 정한다(지금 1.0.0) |
| 2 | Intro | 페르소나 소개 문구. 「다시 안 보기」는 PlayerPrefs `NeverShowIntro`. 다음 → 들어갈 곳으로 | `GET /me` | 1.0 | 구현 |
| 3 | Login | 「가입 \| 로그인」 탭. **가입**: 팀 · 이름 · 숫자 비밀번호 · 닉네임 입력 → 닉네임 중복확인 → 결과 팝업(네/아니요 · 닫기) → 가입 → EggSelect. **로그인**(다른 기기 · 브라우저): 닉네임 + 비밀번호 → `/me`로 갈 곳 결정 | `GET /players/check`, `POST /players`, `POST /players/login` | 1.0 | 구현. 가입 · 로그인 모두 `playerId`를 `SessionState`와 PlayerPrefs에 저장하고 앱 시작 때 불러온다. 로그인 실패는 401 · 429 · 연결 실패를 다른 문구로 안내. PC 브라우저(`!Application.isMobilePlatform`)에서는 아래쪽에 「PC에서는 한글을 입력할 수 없어요. 휴대폰으로 접속해 주세요.」(`Text - PcNotice`)를 띄운다 |
| 4 | EggSelect | 알 8종 스크롤 목록(화면당 4개, 2페이지) → 확인 팝업 → Hatch | - | 1.0 | 구현. 고른 알은 `SessionState.eggIdx`로 Hatch에 넘긴다 |
| 5 | Hatch | 알 8개가 원형으로 회전 → 멈춘 뒤 서버가 뽑은 캐릭터 공개 | `POST /characters` | 1.0 | 원형 배치 · 회전 · 「시작」 → 서버 호출 · 고른 알에서 멈춤 → 커짐 · 흔들림 · 번쩍임 → 결과 창(캐릭터 그림) · 오류 창 구현(「캐릭터 › Hatch 연출」) |
| 6 | Main | 하트 터치(10번 = 하트 1개) · 오늘의 감정 팝업 · 애정도 게이지 · 레벨업 연출. 상단: 꽃(→꽃밭) · 확성기(→소식) · 기어(→설정). 하단: 도감 · 묘약 · 게임. 동작은 「규칙 › 홈화면」 | `GET /me`, `POST /tap`, `POST /emotions` | 1.0 | UI · 하트 파티클 · `/me` · `/tap` 연결 · 캐릭터 그림(`speciesId * 5 + level - 1`번 진화 그림, 레벨업하면 바로 바뀜) · 서버 오류 창 · 하트 아래 상태 문구 · 쓰다 만 터치 수 기기 저장(`HeartsTaps`) · 레벨업/완성 안내(`Panel - Notice`) · 게임 버튼(→ Game 달력) · 오늘의 감정(`Panel - Emotion` → `POST /emotions`, 레벨업이면 안내 창) · 감정 버튼 아이콘(코드로 그린 그림, `Common/03_Resources/Emotion`) · 묘약 버튼 빨간 점(`/me`의 `potionReady`) · 확성기 빨간 점(`/me`의 `newsReady`) · 플라몽 한마디 말풍선(`/me`의 `lines` 3줄, 캐릭터를 누르면 다음 줄, 감정을 고르면 맞장구 `reply`) |
| 7 | 설정 (Main 안의 창) | 정보 수정 · 소리 · 로그아웃 · 캐릭터 새로 키우기 · 문의 · 회원 탈퇴. 그림이 거의 없고 Main에서만 열어서 씬이 아니라 `Panel - Setting` 창으로 둔다 | `PATCH /players/me`, `POST /characters/reset`, `DELETE /players/me` | 1.0 | 창 UI · 열기/닫기 · 정보 수정(확인 창 → PATCH) · 로그아웃(확인 창 → `playerId` · `HeartsTaps` 지우고 Title) · 새로 키우기(확인 창 → `/characters/reset` → EggSelect, 완성 전이면 도감에 남지 않는다는 경고 문구) 구현. 회원 탈퇴는 맨 아래 흐린 밑줄 글씨(실수로 누르지 않게) → 되돌릴 수 없다는 확인 창 → `DELETE /players/me` → 로그아웃과 같이 저장값을 지우고 Title, 이미 없는 계정(401)이면 Login 구현. 소리는 배경음 · 효과음 슬라이더 · `SoundManager`(Title) 연결, 배경음 1곡 · 효과음 5개(「에셋 › 배경음 · 효과음」). 문의하기는 「손편지로 부탁드립니다」 안내 창(`Panel - Contact`, 설정 창 안. 아주 작은 글씨로 주소를 적은 이스터 에그, 화면 아무 곳이나 누르면 닫힘) 구현 |
| 8 | Collection | 알 탭 8개 × (계열 3종 × 진화 5단계). 도달한 레벨까지 그림, 나머지는 검은 실루엣. 상단 뒤로가기 · 완성 개수. 규칙은 「캐릭터」 | `GET /collection` | 1.1 | 구현. 처음엔 지금 키우는 알 탭을 연다. 탭 글로우 이동 · 칸 그림과 실루엣 · 완성 개수 · 알 이름 · 401 · 오류 창 |
| 9 | Game | 「이달의 게임」 달력 12칸. 이번 달 칸 → 그달 게임, 지난 달 칸 → 그달 랭킹. 규칙 · 화면은 「미니게임 · 랭킹」 | `GET /games/months`, `GET /rankings?month=` | 1.1 | 서버 · 달력 칸 채우기 · 지난 달 랭킹 창(공용 `RankingPanel`) 구현 |
| 9-1 | Tidy | 미니게임 「마음 정리」. 30초 동안 감정을 서랍에 정리 → 결과 · 이번 달 랭킹 | `POST /games/start`, `POST /scores`, `GET /rankings` | 1.1 | 서버 · 한 판(`TidyGame`) · 멈춤 · 결과 창 · 랭킹 구현. 에디터 봇 · 웹 빌드 · 폰 확인 |
| 10 | Potion | 히든 미션 달성 시 묘약 획득 | `GET /missions`, `POST /potions/claim` | 1.2 | 구현. 방문 묘약 카드(병이 7분의 1씩 차오름, 7 / 7이고 마실 수 있을 때만 [받기]) · 히든 미션 카드 3장(숨김 · 받기 · 대기 · 받음) · 받으면 안내 창(레벨업 포함) 뒤 `/missions` 다시 부름 · 409도 다시 부름 · 401 · 오류 창. 상단은 뒤로가기만(홈 아이콘 뺌) |
| 11 | Garden | 꽃밭: 하루 1회 익명 한마디(대나무숲). 위에 쓰기 카드, 아래에 최신 30개. 규칙 · 화면은 「꽃밭 · 소식」 | `GET /posts?kind=garden`, `POST /posts` | 1.2 | 구현(`Common/02_Scripts/PostScene`). 응답 전엔 쓰기 · 다 씀 카드 둘 다 끔 · 공백만이면 버튼 꺼짐 · 보내는 동안 버튼 꺼짐 · 400 안내 창 · 409 다시 부름 · 401 · 오류 창(다시 보내기) |
| 12 | News | 소식: 공지 · 소식, 닉네임 표시, 작성권 있는 사람만 하루 1회. 화면은 꽃밭과 같다 | `GET /posts?kind=news`, `POST /posts` | 1.2 | 구현. 꽃밭과 같은 `PostScene`(`kind = news`) |

**들어갈 곳** (`Common/02_Scripts/Util/EntryRouter.cs`, Title · Intro · Login 로그인 성공 후 모두 이 규칙을 쓴다)

| 상태 | 가는 곳 |
|---|---|
| 기기에 저장된 `playerId` 없음 | Login |
| `/me` 200, `speciesId` -1 (가입만 하고 알을 안 고름) | EggSelect |
| `/me` 200 | Main (팀 · 이름 · 닉네임을 `SessionState`에. `speciesId` · `level`은 Main이 `/me`로 넣는다) |
| `/me` 401 (서버가 모르는 계정: DB 초기화 · 탈퇴) | 저장된 `playerId`와 쓰다 만 터치 수(`HeartsTaps`)를 지우고 Login. Main · Hatch에서 요청이 401이어도 같다(`EntryRouter.GoToLogin`) |
| `/me` 404 (없는 주소: `BASE_URL` 실수 등) | 오류 창 → 다시 시도. 계정 문제가 아니므로 `playerId`는 지우지 않는다 |
| 서버에 닿지 못함 (응답 코드 0 등) | 오류 창 → 다시 시도. **`playerId`는 지우지 않는다**(서버가 잠깐 꺼졌을 뿐일 수 있음) |

씬 1개 = 화면 1개, `LoadSceneMode.Single`. 씬 간 데이터는 `SessionState`(싱글톤, Title 씬) 하나로만 전달한다. 중간 씬을 에디터에서 바로 플레이하면 싱글톤이 없으니 Title부터 플레이한다.

### 화면 레이아웃 기준
- 시안은 566px 폭의 세로가 긴 폰 기준이다. 1080 캔버스로 옮길 때 1.908배 한다.
- 세로 위치는 화면 중앙 기준으로 둔다. 시안 그대로면 16:9에서 잘리는 화면(Hatch)은 전체를 아래로 150 내렸다.
- EggSelect는 스크롤 영역 높이에 맞춰 알 크기와 간격이 비례한다. 칸 높이의 70%가 알이고, 화면비와 상관없이 한 화면에 4개가 보인다.

---

## 규칙 (전부 서버 계산 · 클라이언트는 표시만)

### 성장 밸런스

**설계 목표**
- 하루 한 번, 30초 안팎이면 끝나는 짧은 일과.
- 가입한 날 바로 첫 진화(Lv2)를 본다. 첫인상이 다음 날 재방문을 만든다.
- 매일 하면 25일, 주 3회만 와도 약 한 달(31일)이면 완성된다. 월간 미니게임 · 랭킹 주기와 맞물린다.
- 못 온 날의 손해를 줄이고 애정도는 깎지 않는다. 바쁜 직장인에게 숙제가 되지 않게 한다.
- 「감정을 먹고 자란다」는 컨셉을 규칙으로 만든다(오늘의 감정).

**애정도를 얻는 방법**

| 방법 | 애정도 | 제한 | 버전 |
|---|---|---|---|
| 하트 | 1개당 +1. 하트 버튼을 **10번** 누르면 1개 전달 | 매일 KST 0시에 **5개 충전, 최대 15개까지 저장** | 1.0 |
| 오늘의 감정 | +2 | 하루 1회 | 1.0 |
| 미니게임 | +2 | 하루 첫 플레이 | 1.1 |
| 묘약 | +10 | 오늘의 감정을 고른 날 **누적 7일**마다 1개(반복) + 히든 미션 3개(1번씩). 「규칙 › 묘약」 | 1.2 |

하루 최대는 1.0 기준 7, 1.1 이후 9(묘약 제외)다.

**레벨**

| 레벨 | 누적 애정도 | 게이지 (필요량) | 이 단계에 머무는 기간 (하트 + 감정, 매일) |
|---|---|---|---|
| Lv1 | 0 ~ 4 | 5 | 첫날 안에 끝 |
| Lv2 | 5 ~ 29 | 25 | 약 4일 |
| Lv3 | 30 ~ 64 | 35 | 약 5일 |
| Lv4 | 65 ~ 114 | 50 | 약 7일 |
| Lv5 | 115 ~ 174 | 60 | 약 8일 |
| **완성** | **175** | Lv5 게이지 가득 | - |

- 뒤로 갈수록 구간이 길어진다(5 → 25 → 35 → 50 → 60). 초반은 빠르게 보상하고, 완성에는 무게를 둔다.
- **Lv5에도 게이지가 있다.** Lv5가 되면 마지막 진화 모습으로 바뀌고, 그 게이지를 다 채우면 완성이다. 완성한 캐릭터를 한동안 보며 키우게 하기 위해서다.
- 받은 성장표(5 / 40 / 60 / 80 / 100, 누적 285)와 같은 구조로 수치만 줄였다. 원안대로면 지금 하루 공급량으로 매일 해도 41일이 걸린다. **이 수치로 확정했다**(2026-11-01). 원안 작성자에게 확인할 수 없어 우리가 정했다. 받은 표는 2~5단계 누적 범위가 1점씩 어긋나 있어(예: 2단계 6~46은 41칸인데 표시 점수는 1~40) 표시 점수 쪽을 맞다고 보았다.

**완성까지 걸리는 기간** (시뮬레이션 값, 그 단계에 도달하는 날)

| 플레이 방식 | Lv2 | Lv3 | Lv4 | Lv5 | 완성 |
|---|---|---|---|---|---|
| 매일 · 하트만 | 1일 | 6일 | 13일 | 23일 | 35일 |
| 매일 · 하트 + 감정 (1.0 기본) | 1일 | 5일 | 10일 | 17일 | **25일** |
| 매일 · + 방문 묘약 | 1일 | 5일 | 8일 | 14일 | 21일 |
| 매일 · + 묘약 + 미니게임 (1.1) | 1일 | 4일 | 7일 | 12일 | 18일 |
| 평일만 · 하트 + 감정 | 1일 | 5일 | 10일 | 18일 | 29일 |
| 평일만 · + 방문 묘약 | 1일 | 5일 | 9일 | 17일 | 24일 |
| 주 3회(월·수·금) · 하트 + 감정 | 1일 | 5일 | 12일 | 22일 | **31일** |
| 주 3회(월·수·금) · + 방문 묘약 | 1일 | 5일 | 12일 | 19일 | 29일 |
| 주 1회 · 하트 + 감정 | 1일 | 15일 | 29일 | 50일 | 71일 |

주 3회 방문자가 매일 방문자에 크게 뒤처지지 않는 이유는 하트 저장(최대 3일치) 때문이다. 주 1회는 저장 상한에 걸려 느려진다. 완전히 손을 놓은 사람을 따라잡게 하지는 않는다.

**세부 규칙**
- 가입 직후 하트 5개로 시작한다. 터치 50번이면 첫날 Lv2가 된다.
- 하트는 **플레이어 소유**다. 「새로 키우기」를 해도 남은 하트는 유지된다.
- 터치 10번은 클라이언트 연출이다. 서버는 「하트 1개 사용」 요청만 받고 저장량만 검사한다. 터치 수를 조작해도 저장량 이상은 얻을 수 없다.
- 만들다 만 터치 수(예: 7/10)는 기기에 저장한다. 규칙과 무관한 표시용 값이다.
- **애정도는 줄지 않는다.**
- **완성(175)에서 멈춘다.** 넘친 양은 버린다. 이후 하트 버튼은 연출만 나오고 하트를 쓰지 않는다. 도감에 등록하고 「새로 키우기」를 안내한다.
- 완성 뒤에도 오늘의 감정은 기록한다(2.0 대시보드 데이터). 애정도만 더 오르지 않는다.
- 수치는 서버 `rules/growth.js` 한 곳에 둔다. 서버는 누적 애정도 하나만 저장하고, 응답 때 레벨 안 진행값(`levelProgress` / `levelNeed`)을 계산해 내려준다. 클라이언트에 레벨 경계값을 하드코딩하지 않는다.
- 진화 이미지는 캐릭터마다 Lv1~5, 5장(`Common/03_Resources/Character/char_XX_lv1~5`, 24종 × 5 = 120장).
  시안의 Lv1(같은 몸통을 색만 바꾼 24종) 위에 같은 선으로 장식을 코드로 덧그린 그림을 정식 리소스로 쓴다(2026-10-24 확정). 몸 색은 단계가 올라도 그대로다.
  - 공통: Lv1 알껍데기 속 아기 → Lv2 작은 손 → Lv3 손발 · 커진 지느러미 · 밝은 배 → Lv4 귀 · 날개 · 볼터치 · 무늬 → Lv5 왕관류 · 큰 흰 날개 · 후광 · 눈 반짝임. 몸 크기 55% → 88%.
  - 알별 컨셉(머리 장식 Lv2 → Lv3 → Lv4 / Lv5): 핑크 꽃(새싹 → 꽃봉오리 → 꽃 / 금 왕관) · 퍼플 밤하늘(별 더듬이 → 큰 별 → 초승달 / 별 왕관) · 블루 바다(물방울 → 큰 물방울 → 거품 / 진주) · 그린 숲(잎 → 클로버 → 잎 두 장 / 잎 화관) · 그레이 구름(구름 → 큰 구름 → 무지개 / 천사 링) · 오렌지 햇살(불꽃 → 큰 불꽃 → 해 / 햇살 왕관) · 라벤더 나비(더듬이 → 하트 더듬이 → 리본 / 티아라) · 크림 디저트(체리 → 생크림 → 도넛 / 체리 왕관).

### 홈화면 (Main)

**오늘의 감정**
- 그날 처음 Main에 들어오면(`/me`의 `emotionToday`가 false) 팝업으로 묻는다. 보기는 기획서의 「감정연료」 예시 5개다. 괄호는 API 값.
  피로(`tired`) · 스트레스(`stress`) · 성취감(`achievement`) · 몰입(`focus`) · 평온(`calm`)
- 고르면 애정도 +2. **닫기 버튼은 없고 반드시 하나를 고른다.** 닫기는 서버에 남지 않아서, 같은 날 Main에 다시 들어오면 또 묻게 되기 때문이다.
  - 그래서 알을 고르고 처음 들어오면 고르자마자 게이지가 40%(2 / 5)가 된다. 0에서 차오르는 연출로 보상이 보이게 했다(아래 「게이지」).
- 고른 뒤 응답이 올 때까지 버튼 5개를 잠근다(연타 방지). 409 `emotion_done`(다른 기기에서 먼저 고름 · 응답을 못 받고 다시 보냄)이면 창을 닫고 「오늘은 이미 골랐어요.」만 띄운다. 연결 실패는 오류 창 → 같은 감정으로 다시 시도.
- 개인 응답은 본인 외에는 보지 않는다. 2.0 대시보드는 **5명 이상 모인 묶음만** 익명 집계로 보여준다. 작은 팀에서 개인이 드러나지 않게 하기 위해서다.

**어제의 마음 날씨** (2026-11-18 구현 · 에디터 확인. 웹 빌드 · 폰 확인 전)
- 오늘의 감정을 고르면 같은 창이 「어제의 마음 날씨」로 바뀐다. 어제 모두가 고른 감정이 막대 5개로 보이고, [확인]을 누르면 닫힌다. 「나만 그런 게 아니구나」를 주려는 것이고 보상은 없다.
- **어제 것만** 보여 준다. 오늘 집계는 하루 중에 계속 바뀌고, 아침에는 누가 골랐는지 짐작될 수 있다.
- **고른 뒤에만** 보여 준다(`POST /emotions`의 `weather`). 먼저 보면 남을 따라 고른다. 그래서 다시 보는 버튼도 두지 않는다.
- **숫자 · 퍼센트 없이 막대만.** 가장 많이 고른 감정이 꽉 찬 막대(1)이고 나머지는 그에 대한 비율이다. 인원은 전체(「어제 23명이 마음을 골랐어요」)만 적는다.
- **어제 5명 미만이면 막대를 숨기고** 인원과 「5명이 모이면 모두의 마음 날씨가 보여요」만 띄운다(`rules/weather.js` `WEATHER_MIN`, 2.0 대시보드의 「5명 이상」과 같은 기준).
- 화면: `Panel - Emotion/Image - Window` 아래 `Group - Weather`(`Row - Tired`~`Calm`: 아이콘 · 이름 · `Image - Bar/Image - Fill`), `Text - WeatherEmpty`, `Button - WeatherOk`. 셋 다 기본 꺼짐. 막대 채움은 Main 게이지와 같은 `gauge_fill`(Filled · Horizontal)이라 코드는 `fillAmount`만 넣는다. 새 그림은 없다.

**플라몽 한마디** (2026-11-18 구현 · 웹 빌드 · 폰 확인)
- 캐릭터 머리 위 말풍선에 오늘의 한 줄이 뜬다. 캐릭터를 누르면 그날 몫 3줄을 차례로 넘겨 본다.
- 3줄은 **요일 → 레벨 또는 알 계열(완성 뒤에는 완성 대사) → 공통** 순서다. 서버가 `sha256(날짜 + playerId)`로 골라 같은 날에는 몇 번 들어와도 같은 줄이다(`/me`의 `lines`). 레벨이 오르면 둘째 줄은 다음에 들어올 때 바뀐다.
- 오늘의 감정을 고르면 그 감정에 맞장구치는 한 줄로 바뀐다(`POST /emotions`의 `reply`). 감정마다 「공감 · 먹는 맛 · 함께」 3줄로 수와 톤을 맞췄다. 어떤 감정의 말만 귀여우면 그 말을 보려고 실제와 다른 감정을 고르게 되기 때문이다. 같은 이유로 대사 모으기 · 대사 도감은 만들지 않는다.
- 대사 표는 서버 `rules/lines.js`에만 있다(57줄). 웹 빌드 없이 고치고 늘린다. 한 대사는 한두 줄(`\n`), 줄마다 13자 이하, 폰트가 그리는 글자만 쓴다(`test/lines.test.js`가 막는다).
- 공감하는 말만 쓴다. 직장 농담(야근 · 상사)과 훈계(「힘내야지!」)는 넣지 않는다.
- 화면: 말풍선은 `Canvas - Main/Group - Center/Image - Bubble`(기본 꺼짐, `frame_bubble` 한 장, 580×270) 아래 `Text - Line`과 `Text - Page`(「1/3」, 맞장구일 때는 비움)다. 줄이 없으면(캐릭터 없음) 켜지 않는다. 캐릭터(`Canvas - Dynamic/Group - Center/Image - Character`)에 Button · `ButtonSound`를 붙여 누르면 다음 줄로 넘어간다.
  - 말풍선은 몸통과 꼬리를 한 선으로 이어 그린 그림 한 장이다(`Common/03_Resources/UI/frame_bubble.png`, 348×162, 9-slice 아님). 선은 새로 그리지 않고 `frame_card_white` 왼쪽 세로 선의 픽셀을 윤곽을 따라 옮겨 붙여, 굵기 · 색 · 거친 가장자리가 다른 테두리와 같다(2026-11-18). 네모 조각을 덧댄 꼬리, 매끈한 선으로 따로 그려 붙인 꼬리는 테두리와 어울리지 않아 버렸다. 크기를 바꾸려면 그림을 다시 만든다.
  - 폰트에 「● ○」가 없어 몇 번째 줄인지는 숫자(「1/3」)로 보여 준다.

**하트 버튼**

| 상황 | 화면 |
|---|---|
| 매 터치 | 버튼이 살짝 눌렸다 돌아오고 작은 하트 1개가 떠오름. 하트 안쪽이 10분의 1씩 차오름 |
| 10번째 터치 | 하트 4~6개가 터지듯 떠오르고 「+1」. 게이지가 차오르고 남은 하트 수가 줄어듦 |
| 레벨업 | 캐릭터 이미지가 다음 단계로 바뀌고 「Lv 3이 되었어요!」 팝업 |
| 남은 하트 0 | 버튼이 회색. 「내일 0시에 하트 5개가 채워져요」 |
| 완성 (Lv5 게이지 가득) | 「다 자랐어요! 도감에 등록됐어요」 팝업과 「새로 키우기」 안내 |
| 전송 실패 | 하트를 줄이지 않고 찬 상태로 둠. 다음 터치 때 다시 보냄 |

- 「횟수: N」은 **지금 쓸 수 있는 하트 수**다(최대 15).
- 레벨은 게이지 옆 「Lv」 하나로만 보여준다. 시안의 왼쪽 위 「Lv」는 중복이라 뺐다.
- 게이지는 **현재 레벨 안에서의 진행률** `levelProgress ÷ levelNeed`다. 레벨이 오르면 0에서 다시 시작한다. 눈금 3개는 25 · 50 · 75% 장식이다.
  - 감정 · 하트로 애정도가 오르면 이전 값에서 `gaugeFillTime`(0.5초) 동안 차오른다. 레벨업이면 끝까지 채운 뒤 0에서 새 레벨 값까지 다시 차오른다. Main에 들어올 때(`/me`)는 바로 맞춘다.
- 게임 버튼은 Game(「이달의 게임」 달력)으로 간다. 꽃밭 · 소식 버튼은 Garden · News로 간다.
- 하트 파티클은 Main의 **20개 풀**을 돌려 쓴다. 터치마다 1개, 하트 전달 때 5개, 레벨업 때 10개를 더 띄운다. 초당 4번 연타 중 레벨업해도 떠 있던 4개 + 16개 = 20개로 딱 맞는다. 더 빠르게 누르면 가장 오래된(거의 사라진) 하트부터 다시 쓴다.

### 캐릭터
- 알 이미지 **8종** (`Common/03_Resources/Egg/`). EggSelect에서 **8종 모두 고를 수 있게** 배치했다.
- `eggId`는 EggSelect 목록 순서다. 순서를 바꾸면 서버 값도 바뀐다.

  | eggId | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
  |---|---|---|---|---|---|---|---|---|
  | 알 | 핑크 | 퍼플 | 블루 | 그린 | 그레이 | 오렌지 | 라벤더 | 크림 |

- Hatch의 원형 배치는 시안 순서(12시부터 시계 방향: 핑크 · 그레이 · 오렌지 · 라벤더 · 크림 · 그린 · 퍼플 · 블루)라 `eggId` 순서와 다르다. `HatchScene.slotOfEgg`가 `eggId` → 칸 번호를 바꾼다.
- **Hatch 연출**: 알은 천천히 돈다. [시작]을 누르면 서버 요청과 동시에 `EggRotater.StopAt`이 `stopTime`(2초) 동안 감속해 **EggSelect에서 고른 알을 원 아래 6시, 포인터(▲) 위에 세운다**(바퀴는 알 색을 뽑는 게 아니라 내 알을 찾아가는 연출). 그다음 그 알이 원 가운데로 가며 커지고 → 흔들리고(응답이 늦으면 올 때까지 계속) → 화면이 하얗게 번쩍이며 결과 창이 드러난다.
- 알이 색 계열을 정하고, 그 안에서 서버가 균등 랜덤으로 캐릭터를 뽑는다. 클라이언트 랜덤 금지(새로고침 리롤 방지).
- `speciesId = eggId × 3 + (0~2)`. 캐릭터는 **알 8종 × 3종 = 24종으로 확정**했다(2026-11-01). 알당 종 수는 `rules/species.js`의 `SPECIES_PER_EGG` 하나다.
- **도감**: 리셋 때 다른 알을 고를 수 있어서 「내 알 계열」이 하나로 정해지지 않는다. 그래서 알 8종을 탭으로 모두 보여 주고, 처음에는 지금 키우는 알 탭을 연다.
  - 종마다 **도달한 최고 레벨까지** 그림을 보여 주고 나머지는 검은 실루엣으로 둔다. 실루엣은 같은 그림에 색만 입혀 추가 그림이 없다(남색 `#3A4A7A`도 시험했지만 어색해서 검정).
  - 세는 캐릭터는 **지금 키우는 캐릭터와 완성한 캐릭터**뿐이다. 완성 전에 새로 키운 캐릭터는 도감에 남지 않는다(새로 키우기 경고 문구와 같다).
  - 개수는 완성(애정도 175)한 종 수다. Lv5여도 게이지를 다 채워야 완성이라 레벨과 따로 센다.

### 닉네임
- 1~12자. 허용 글자는 **ASCII + 한글 자모(ㄱ~ㅣ 51자) + 꾸불림체가 실제로 그리는 한글 2,780자**(`Server/rules/hangul.txt`)다. 폰트(정적 아틀라스 + 자모 · 추가 폴백)가 그릴 수 있는 범위와 같다. 꽃밭 · 소식 글도 같은 규칙이다.
  - 꾸불림체 TTF는 한글 11,172자 모두에 자리는 있지만, 8,392자(「탷」 「쀍」 「갂」 등)는 모양이 비어 공백처럼 보인다(2026-11-03 확인). 그래서 그 글자는 받지 않는다.
  - `hangul.txt`는 TTF에서 너비 · 높이가 0이 아닌 한글을 뽑은 목록이다. 폰트를 바꾸면 다시 뽑는다.
- 서버 `rules/text.js`가 검사한다. 범위 밖 글자는 400 `nickname_charset`, 중복은 409 `nickname_taken`.
- 앞뒤 공백 · 이어진 공백은 받지 않는다(400 `nickname_charset`). 대소문자만 다른 닉네임(「Max」 「max」)은 같은 닉네임으로 보고 막는다(`schema.sql`의 `player_nickname_nocase` 인덱스, 2026-11-03). 소식 카드에서 똑같이 보이는 다른 계정이 생기지 않게 하려는 것이다.
  - 가입 화면은 앞뒤 공백을 지우고 보낸다. 로그인은 앞뒤 공백 · 대소문자를 봐준다.
  - 가입 화면 힌트: 「소식 · 랭킹에 보이고, 로그인할 때도 써요. 본인이 드러나지 않게 지어 주세요.」 가입 확인 창에는 「다른 기기에서는 이 닉네임으로 로그인해요.」를 함께 띄운다.
- 팀 · 이름(1~20자)도 같은 글자 규칙이다(2026-11-03). 범위 밖이면 가입 · 정보 수정 모두 400 `profile_charset`. 가입 화면은 「쓸 수 없는 글자가 있어요」 결과 창, 설정 창은 안내 창을 띄운다.
  - 「테스트」를 치는 도중에 잠깐 생기는 「테슽」처럼, 입력 중 글자가 빈칸으로 보였다가 다 치면 제대로 보이는 것은 정상이다.
- 이모지 · 전각 문자는 폰트에 없어 □로 보이므로 쓸 수 없다. Login 입력칸도 12자로 제한했다.
- 2026-11-01까지는 완성형 2350자만 받았고, 11-01 ~ 11-03에는 11,172자를 받았다. 그 사이 배포 서버에 실제 가입자는 없었다.

### 캐릭터 새로 키우기 (리셋)
- 지금 캐릭터 행에 `ended_at`을 기록해 보관한다(지우지 않음, 유저 삭제 아님). 새 `character` 행은 알을 다시 골라 `POST /characters`에서 만든다.
- 리셋 뒤에는 EggSelect로 가서 **다른 알도 고를 수 있다**. 그 사이 `/me`의 `speciesId`는 -1이라, 앱을 껐다 켜도 EggSelect로 간다.
- 클라이언트는 `eggIdx`를 따로 지우지 않는다. EggSelect에서 알을 고를 때 새 값으로 덮어쓴다. 쓰다 만 터치 수(`HeartsTaps`)도 하트처럼 이어진다.
- 응답을 못 받고 다시 보내도 `{ ok }`(이미 끝났으면 그대로). `event`의 `reset`에는 끝낸 캐릭터의 `speciesId` · 애정도 · 완성 여부를 남긴다.
- 유지: 도감 해금 · 랭킹 · 로그 · 프로필 · **남은 하트**. 초기화: 애정도 · 레벨.
- 완성 전 리셋은 도감에 남지 않음. 경고 팝업 1회.

### 묘약 (Potion)
- 미션을 달성하고 [받기]를 누르면 묘약을 **바로 마셔 애정도 +10**. 보관해 두고 골라 쓰는 기능은 없다.
- **방문 묘약**: 오늘의 감정을 고른 날이 **누적 7일**이 될 때마다 1개, 계속 받는다. 연속이 아니라서 주말 · 휴가로 빠져도 끊기지 않는다(연속이면 평일만 오는 사람은 하나도 못 받는다). 방문 일수는 `emotion_log` 행 수로 세서 따로 저장하지 않는다.
  - 게이지는 7에서 멈춰 [받기]를 기다린다. 그사이 방문은 쌓아 두었다가 받은 뒤 이어진다(늦게 받아도 손해 없음).
- **히든 미션** 3개, 한 번씩. 달성 전에는 「???」와 힌트만 보인다. 순서가 카드 순서다.

  | id | 조건 | 판정 |
  |---|---|---|
  | `first_complete` | 캐릭터를 처음 완성 | `character.affection` 175인 행이 있다 |
  | `first_game` | 미니게임 첫 플레이 (1.1) | `score` 행이 있다 |
  | `first_garden` | 꽃밭에 첫 한마디 (1.2) | `post`(garden) 행이 있다 |

- 캐릭터가 없거나 완성했으면 마실 수 없다(409). 묘약은 남아 있다가 새로 키울 때 마신다. 첫 완성 묘약이 이 경우다.
- 고른 감정의 종류로 받는 미션(감정 5종 모두 고르기 등)은 두지 않는다. 묘약을 받으려고 실제와 다른 감정을 고르면 2.0 대시보드 데이터가 틀어진다.
- 받은 기록만 `potion`에 남긴다. 방문 묘약은 `visit:1`, `visit:2`처럼 몇 번째인지 적어 UNIQUE로 두 번 받기를 막는다. 수치는 `rules/growth.js`(`POTION_AFFECTION` · `VISIT_PER_POTION` · `HIDDEN_MISSIONS`).
- Main 「묘약」 버튼은 `/me`의 `potionReady`가 true면 빨간 점을 띄운다(마실 수 있고 받을 것이 있을 때). 점은 `Button - Potion/Image - PotionDot`(기본 꺼짐, 그림 `Main/03_Resources/dot_red` 64²)이다.

### 미니게임 · 랭킹
- **운영은 「이달의 게임」**(2026-11-03 확정). 게임은 「마음 정리」(`tidy`) 하나로 시작해 하나씩 늘린다.
  - 한 달에 한 게임만 랭킹에 오른다(달마다 상위 20). 새 게임이 준비된 달에만 바꾸고, 없으면 같은 게임을 잇는다. 지난 게임은 연습용으로 남긴다.
  - 그달의 게임은 `rules/game.js`의 `GAME_OF_MONTH` 표(`[시작 달, 게임]`)가 정한다. 지금은 `['2026-11', 'tidy']` 한 줄이라 2026-10 이전(출시 전)은 게임이 없다. 새 게임을 올릴 때 한 줄을 더해 배포한다. 연습용 점수(랭킹에 오르지 않음)는 두 번째 게임 때 만든다.
- **화면은 씬 2개**(2026-11-03, 기획서 「이달의 게임」 12칸 화면을 따름).
  - **Game = 「이달의 게임」 달력**: 올해(KST) 1~12월 칸 12개를 3 × 4로 깔아 둔다. Main 게임 버튼이 여기로 온다. `GET /games/months` 한 번으로 칸을 채운다.

    | 달 | 칸 | 누르면 |
    |---|---|---|
    | 이번 달 | 흰 카드(`Image - Now`) + 게임 이름 + 「도전 중 · 최고 N점」(기록이 없으면 「도전 중 · 기록 없음」) | 그 게임 씬(지금은 Tidy) |
    | 지난 달 | 민트 카드 + 게임 이름 + 「N등 · M점」(기록이 없으면 「기록 없음」) | 그달 랭킹 창(`GET /rankings?month=`) |
    | 출시 전 달 | 흐리게(`CanvasGroup` 알파 0.45), 비움 | 없음 |
    | 다음 달 이후 | 흐리게 「?」 | 없음 |

    - 해가 바뀌면 새 12칸이다. 지난해 기록은 DB에 남고, 연도 넘기기는 필요해지면 더한다.
    - 세로 위치는 화면 가운데 기준이라 화면비가 달라도 칸이 가운데에 모인다. 시안은 제목이 뒤로가기 아래 줄이지만, 다른 화면처럼 뒤로가기 줄에 두었다.
  - **Tidy = 「마음 정리」 플레이**(아래). 뒤로가기는 달력으로 간다. [랭킹]은 이번 달만 보여 준다(지난 달은 달력에서 본다).
    - [랭킹]은 준비 화면 · 결과 창에서만 보인다. **[뒤로]는 늘 보이고, 판 중(처음 3 · 2 · 1이 끝난 뒤 ~ 30초 끝)에는 멈춤 버튼이다.** 그 밖에는 바로 달력으로 간다(처음 세는 중에는 아직 누른 것이 없어 잃는 게 없다).
    - **멈춤**: `Time.timeScale = 0`으로 판 전체(판 시계 · 연출)를 멈추고, 줄(`Group - Field`)을 가린 채 확인 창(「그만할까요? 이번 판은 기록되지 않아요」)을 띄운다.
      - [네]: 점수를 보내지 않고 달력으로 간다. 서버에는 시작 기록만 남고 다음 시작이 덮는다. 씬이 넘어가는 몇 프레임 사이에 판이 끝나 점수가 가지 않게 `TidyGame`을 먼저 끈다.
      - [아니요]: 실제 시간으로 3 · 2 · 1을 센 뒤 이어서 한다. 세는 동안에도 줄은 가린 채이고 누름 · [뒤로]는 무시한다.
      - 줄을 가리는 이유: 멈춘 채 줄이 보이면 멈췄다 이어 가기를 되풀이하며 다음 감정을 외울 수 있다.
      - `TidyScene.OnDestroy`가 `Time.timeScale = 1`로 되돌린다(멈춘 채 다른 길로 씬을 떠나도 다음 씬이 멈춰 있지 않게).
      - 10분 넘게 멈춰 두면 서버가 그 판을 받지 않는다(409 → 「이번 판은 기록되지 않았어요」).
  - 랭킹 창은 두 씬이 같이 쓰는 공통 프리팹 `Panel - Ranking`(순서 2)이다. 제목 「N월 랭킹」 · 게임 이름 · 20줄(`Row - 0`~`19`를 미리 깔아 둠) · 내 줄 표시(`Image - MyRow`) · 「내 기록」. Dim은 Raycast를 켜 아래 버튼을 막는다.
- **하루 첫 판 애정도 +2**(`GAME_AFFECTION`): 오늘(KST) 받은 점수가 없으면 첫 판이다. 게임 종류와 상관없이 하루 한 번이고, 시작만 하고 그만둔 판은 세지 않는다. 캐릭터가 없으면(새로 키우는 중) 점수만 남는다. 레벨은 점수에 영향이 없다.
- **오늘 고른 감정 보너스는 없다.** 고른 감정이 게임에 영향을 주면 실제와 다른 감정을 고를 이유가 생겨 2.0 대시보드가 틀어진다. 같은 이유로 서랍 감정은 5종 균등 무작위이고, 게임 기록(`event` payload)에 감정 이름을 남기지 않는다.

**마음 정리 규칙.** 수치는 `rules/game.js`의 `TIDY` 한 곳에 있다. 시작 응답 `rules`로 클라이언트에 내려 주어 웹 빌드 없이 서버만 바꿔 조정한다.

| 수치 | 값 |
|---|---|
| 판 | 카운트다운 3초(`countdownSec`) + 30초(`durationSec`) |
| 하는 일 | 가운데 세로 줄 맨 아래 감정을, 같은 감정이 붙은 왼쪽 · 오른쪽 서랍 쪽(화면 절반)을 눌러 넣는다 |
| 점수 | 정답마다, 맞히기 전 콤보가 0~9면 1점 · 10~29면 2점 · 30 이상이면 3점(`comboAt` [10, 30]). 틀려도 빼지 않는다 |
| 오답 | 콤보 0, 입력 잠금 0.6초(`lockMs`) |
| 서랍 | 10개 맞힐 때마다(`changeEvery`) 한쪽 감정이 바뀐다. 좌우를 번갈아 바꾸고(첫 쪽만 무작위), 새 감정은 지금 두 서랍에 없는 3종 중 하나 |
| 줄 | 좌우 50:50, 같은 쪽 연속 최대 4개(`maxRun`), 첫 두 개는 서로 다른 쪽. 보이는 칸은 5개(맨 아래 + 미리 보기 4) |
| 입력 | 누르는 순간 판정. 받아들이는 입력 사이는 최소 0.1초(`minTapMs`). PC는 마우스만 |

- **줄 만드는 법**: 아이콘은 줄에 들어갈 때 자기 순번 i로 단계(i ÷ 10)가 정해지고, 그 단계의 서랍 짝 중 하나로 만든다. 서랍이 바뀌어도 이미 줄에 있는 아이콘은 다시 칠하지 않는다. 맨 아래 아이콘의 순번은 지금까지 맞힌 수와 같아서, 맨 아래는 늘 지금 두 서랍 중 하나와 맞는다.
- **서랍 예고**: 다음 단계 아이콘(새 감정)이 줄 맨 위 칸에 보이는 순간(맞힌 수를 10으로 나눈 나머지가 6 이상)부터 바뀔 서랍이 깜빡인다. 맞힌 개수로 바뀌어서 「1초 전」을 미리 알 수 없다. 처음 하는 사람에겐 약 4초, 고수에겐 약 1초 예고다.
- **잠금이 0.6초인 이유**: 0.4초면 보지 않고 「왼쪽부터 누르고 틀리면 오른쪽」으로 막누르는 쪽이 보통 실력과 같은 점수(71점)가 나온다. 0.6초면 막누르기 57 · 보통 70으로 갈린다.
- **판 시계는 `Time.deltaTime`을 더한 값**이다(timeScale을 따른다). 남은 시간 · 입력 잠금 · 연타 간격 · 연출이 모두 이 시계를 써서 멈춤 한 줄로 판 전체가 멈춘다. 카운트다운만 실제 시간(`WaitForSecondsRealtime`)이다(멈춘 중에 다시 세야 해서).
  - 판 시계는 실제 시간보다 빨라질 일이 없어(렉에도 `deltaTime`은 0.333초에서 잘린다) 서버의 「시작 뒤 32초 이상」 검사는 그대로 성립한다.
  - 브라우저 탭을 숨기면 Unity가 멈춰 판도 멈춘다. 화면이 안 보이니 미리 읽을 수도 없다.
- **한 판 흐름**(`TidyGame`): `StartGame(rules, onEnd)` → 판 만들기 → 3 · 2 · 1(줄은 보임) → 「시작!」 0.5초 → 30초 → 「그만!」 · `onEnd(점수)` 한 번. `TidyScene`이 `POST /scores` → 결과 창. [다시 하기]는 결과 창만 닫고 준비 화면을 거치지 않고 바로 시작을 보내며, 같은 판 배열을 다시 써서 새 판을 만든다. 날기 시간은 `minTapMs` 이하여야 한다(날던 아이콘이 도착 전에 맨 위 칸으로 다시 쓰이지 않게).
  - 연출(날기 0.1초 · 내려오기 0.08초 · 서랍 팝 · 누른 쪽 서랍 눌림 · 오답 흔들기 · 바뀔 서랍 깜빡임 · 배율 팝)은 모두 `Update`의 float 타이머로 하고, 탭마다 코루틴을 만들지 않는다.
  - 2026-11-04 에디터에서 테스트 봇으로 확인:
    - 점수: 화면 점수가 봇이 따로 계산한 점수와 같다(193개 정답 · 517점).
    - 판 진행: 오답 잠금, 멈춤 중 시간 · 누름 정지, 다시 세는 중 [뒤로] 무시, 다시 세기 3.0초, 결과 창.
    - 다시 하기: [다시 하기]는 준비 화면 깜빡임 없이 판을 초기화한다.
    - 버튼 순서: [시작] 직후 [랭킹]은 숨겨진다. [랭킹] 직후 [시작]을 누르면 랭킹 창이 열리지 않는다.
    - 나가기: 멈춤 → [네]로 나가면 `TidyGame`이 꺼진다. 서버에는 `game_play`만 남고 점수는 없다.
  - 할당: Profiler의 `BehaviourUpdate`(스크립트 `Update` 전체)로 판 한 번(2,392프레임)을 쟀다.
    - `TidyGame.Update`는 1초에 한 번 62B만 할당한다. TMP `SetText(형식, 값)`이 에디터에서만 인스펙터용 문자열을 만들기 때문이다(`TMP_Text.cs`의 `#if UNITY_EDITOR`). 그래서 빌드에서는 0이다.
    - 판 끝(점수 보내기 · 로그)에서 한 번 약 5KB를 할당한다. 누름 경로의 `new`는 구조체뿐이다.
  - 봇이 Profiler 분석으로 에디터를 멈춘 직후 같은 프레임에 두 번 누르면, 둘째 누름은 0.1초 연타 제한에 막힌다(정상). 그래서 봇으로 점수를 맞출 때는 분석을 멈춤 중에만 한다.
- 예상 점수(시뮬레이션 2,000판): 처음 약 30 · 보통 70 · 잘함 120 · 고수 210 · 사람 한계 약 320.
- **화면**(Tidy 씬): 준비(규칙 카드 · 이번 달 기록 · [시작]) → 카운트다운 → 플레이 → 결과 창(점수 · 이번 달 최고 · 등수 · 「다음 등수까지 N점」 · 애정도) → 랭킹 창(이번 달). 캐릭터는 준비 화면에서만 보이고 판 · 결과 창 중에는 숨긴다(레벨이 오르면 준비 화면으로 돌아갈 때 새 그림). 새 그림은 없다(배경 `grad_pink`).
  - `Canvas - Main`(움직이지 않는 것): 배경, 화면 전체를 덮는 투명 누름 영역 `Area - Tap`(여기에 `TidyGame`이 붙어 누른 x가 화면 절반보다 왼쪽이면 왼쪽 서랍으로 본다. BG 바로 뒤라 뒤의 버튼이 먼저 눌린다. 버튼 대신 화면 절반인 이유는 30초 연타에 겨누기가 필요 없고, 손을 뗄 때가 아니라 누르는 순간 판정하기 때문), [뒤로] · 제목 · [랭킹], `Group - Board`(서랍 틀 2개 · 안내 한 줄, 판 · 결과 창 중에 켬), `Group - Ready`(규칙 카드 · 이번 달 기록 · [시작]), `Panel - Result`(2), 랭킹 창 2 · 확인 창 3 · 안내 창 4 · 오류 창 5.
  - `Canvas - Dynamic`(움직이는 것, Raycaster 없음): 캐릭터, `Group - Play`(점수 · 시간 막대(Main 게이지 복사) · 남은 초 · 콤보 · 「2배」 · `Group - Field`(서랍 감정 아이콘과 이름 · 줄 아이콘 `Image - Icon 0`~`5`) · 카운트다운). `Group - Field`는 화면 아래 가운데 기준 묶음이라 줄 칸 위치가 화면비와 상관없다. 키가 큰 폰에서 늘어난 높이는 시간 막대와 캐릭터 사이 빈칸이 받는다.
  - 줄 칸(맨 아래 = 0번): y 330 · 530 · 680 · 830 · 980, 크기 1 · 0.7 · 0.7 · 0.7 · 0.7, 투명도 1 · 1 · 0.8 · 0.6 · 0.4. 아이콘 6개 중 남는 1개는 서랍으로 날아가는 중인 것이다.

**랭킹 · 점수 검증** (`routes/games.js`)
- `score(player_id, game, month, best_score)`: 게임 · 달마다 개인 최고 한 줄이고, 높은 점수일 때만 바뀐다. 점수는 서버가 받은 시각(KST)의 달에 들어간다. 그래서 지난달 랭킹은 1일 0시 정각에 확정된다(스케줄러 없음).
- 동점은 같은 등수(1, 1, 3)이고, 목록 안에서는 그달 먼저 참여한 사람이 위다. 20위 밖이어도 내 등수 · 최고 · 다음 등수까지 점수(바로 위 점수 − 내 최고, 1등이면 0)를 준다. 탈퇴하면 점수도 지워져 지난달 목록에서도 빠진다.
- **한 번 시작에 점수 한 번**: 새 표 없이 `event`로 판정한다.
  - `POST /games/start`가 `game_play`를 남긴다. 새 시작은 앞의 시작을 덮는다.
  - `POST /scores`는 마지막 게임 기록이 `game_play`이고, 그때부터 32초 이상 10분 이하일 때만 받는다. 32초는 카운트다운 3 + 30에서 서버 시계 보정 여유 1초를 뺀 값이다. 클라이언트는 시작 응답을 받은 뒤 카운트다운을 시작하므로 서버가 잰 시간이 늘 더 길다.
  - 점수는 0~863이다. 863은 입력 간격 0.1초로 30초에 누를 수 있는 301번(+1은 프레임 오차 여유)을 모두 맞힌 규칙상 최대다. 상한 안의 조작은 막지 못하고 운영자가 지운다(「인증 › 운영자 복구」).
  - 응답을 못 받고 다시 보낸 점수(마지막 기록이 이미 `game_score`)는 쓰지 않고 200 · 애정도 0으로 그 점수가 들어간 달의 기록을 돌려준다(말일 밤에 보내고 1일에 다시 보내도 지난달 기록). 첫 요청이 이미 기록됐는데 「기록되지 않았어요」가 뜨지 않게 한다. 클라이언트는 `levelUp`과 상관없이 응답의 `growth.level`로 캐릭터 그림을 맞춘다(첫 요청이 올린 레벨).
  - 판 상태가 `event`에 있어 서버가 다시 켜져도 이어진다. 시작에서 10분 안에 다시 켜지면 기록되고, 넘으면 409 `expired`다.
  - 0점 판(시작하고 그냥 기다림)도 기록되어 +2 · 묘약 `first_game`을 받는다. 하루 한 번이라 막지 않는다.
- 게임 시작은 횟수 제한이 없다(`PATCH /players/me`와 같다). 도배가 실제로 생기면 플레이어별 제한을 넣는다.
- 판 수치를 달 중간에 바꾸면 그달 랭킹에 규칙이 다른 기록이 섞인다. 실가입자가 생긴 뒤에는 1일에 맞춰 배포한다.

### 꽃밭 · 소식
- 꽃밭 · 소식 각각 `UNIQUE(player_id, date, kind)` 제약으로 하루 1회.
- 글은 1~100자, 앞뒤 공백은 지운다. 허용 글자는 닉네임과 같다(이모지 · 줄바꿈은 400). 입력칸은 `MultiLineSubmit`(자동 줄바꿈, 엔터로는 줄이 바뀌지 않음)과 100자 제한.
- 꽃밭은 익명 표시, 소식은 닉네임 표시. 서버에는 둘 다 작성자 저장(신고 대응).
- 목록은 각각 최신 30개, 날짜와 상관없이 보여 주고 넘기기는 없다.
- 「하루」는 한국 시간 0시에 바뀐다. 매일 초기화되는 것은 쓸 수 있는 권리뿐이고, 글은 지우지 않는다(작성자가 탈퇴하면 그 사람 글만 지운다).
- **소식 작성권**: `player.can_post_news`가 1인 사람(운영진, DB에서 직접 켠다. 화면 · API 없음. 명령은 「인증」 → 「운영자 복구」) + **오늘의 담당 1명**.
  - 담당 후보는 **어제까지 7일 안에 오늘의 감정을 고른 사람** 중 작성권이 없는 사람이다. 그날 날짜의 해시로 1명을 뽑고 저장하지 않는다(`routes/posts.js` `newsPickOf`).
  - 전체에서 뽑으면 오지 않는 사람이 뽑혀 작성권이 버려진다. 오늘을 빼서 하루 동안 담당이 바뀌지 않는다(그날 후보가 탈퇴하는 경우만 예외).
  - 담당이 되면 Main 확성기에 빨간 점(`/me`의 `newsReady`, `Group - Top/Button - News/Image - NewsDot`, 기본 꺼짐). 소식을 쓰면 꺼진다.
- 꽃밭 첫 글은 히든 미션 `first_garden`(묘약). 글로 애정도는 오르지 않는다(성장표는 한 달 기준으로 맞춰 두었고, 도배 유인이 생긴다).
- 신고 · 삭제 버튼은 없다. 문제 글은 관리자가 DB에서 `DELETE FROM post WHERE id = ?`로 지운다.
- **화면**: Garden(꽃밭) · News(소식) 씬 2개. Main 상단의 꽃은 Garden, 확성기는 News로 바로 간다. 입구가 따로 있어 탭으로 묶지 않았다.
  - 두 씬은 배치가 같고(News는 Garden을 복사해 제목 · 배경 · 아이콘 · 문구만 바꿈) 동작도 같아서 스크립트는 `Common/02_Scripts`의 공용 하나로 쓴다. 씬마다 다른 것은 `kind`와 카드 아이콘 · 안내 문구뿐이다.
  - 쓰기 카드(`Canvas - Main/Image - WriteCard`): `Group - Write`(입력칸 `Input - Body` · 고정 안내 `Text - Hint` · 글자 수 `Text - Count` 「N / 100」 · [심기] / [알리기])와 `Group - Done`(못 쓸 때 안내) 중 하나만 켠다. 입력칸은 Login 입력칸을 복제해 키보드 · 커서 설정이 같다. 입력칸 뒤에 글줄 간격(줄 간격 22, 약 52)에 맞춘 연한 물결 줄 5개(`Image - Line 1`~`5`, 짝수 줄은 좌우 반전)를 깔아 편지지처럼 글이 줄 위에 앉는다. 100자는 4줄을 넘지 않아 입력칸이 스크롤되지 않는다(스크롤되면 글과 줄이 어긋난다).
  - 목록(`Canvas - Dynamic/Scroll - List/Content`): 꺼 둔 `Item - 0`~`29`를 받은 개수만큼 켜서 다시 쓴다. 카드 높이는 `-(Text - Body의 y) + 본문 높이 + 64`(최소 160)로 코드에서 정하고, Content의 VerticalLayoutGroup 하나가 쌓는다. 글이 없으면 `Text - Empty`.
  - 글 · 닉네임 글자(`Text - Body` · `Text - Nickname`)는 Rich Text와 `\n` 해석을 껐다. 사람이 쓴 `<size=200>` 같은 태그가 화면을 깨지 않게 한다.

### 로그
- 모든 쓰기 API가 `event(player_id, type, ts, payload)` 한 줄. 클라이언트 별도 로그 전송 없음.
- type: `login · login_fail · profile(정보 수정, 바뀐 뒤의 팀) · hatch · tap · emotion · game_play · game_score · potion · reset · post · news · delete`.
- 게임: `game_play`는 `{ game }`, `game_score`는 `{ game, score, ms }`(ms = 서버가 잰 시작부터 점수까지). 감정 이름은 넣지 않는다(「미니게임 · 랭킹」).
- 탈퇴(`DELETE /players/me`) 시에도 `event`는 지우지 않는다. 탈퇴자 포함 활동 통계가 대시보드 목적이고, `player` 행이 사라지면 이름·팀과 더 이상 연결되지 않는다.

### 인증
- 평소에는 서버 발급 `player_id`(GUID)를 PlayerPrefs에 저장하고 헤더 `X-Player-Id`로 전송한다.
- 다른 기기 · 브라우저에서는 **닉네임 + 숫자 비밀번호(4~6자리)**로 로그인해서 `player_id`를 다시 받는다(`POST /players/login`).
  - 비밀번호는 `node:crypto` scrypt 해시로만 저장한다(`rules/password.js`). 원문은 어디에도 남지 않는다.
  - 계정마다 **5번 틀리면 10분 잠금**(429 `locked`). 4자리는 1만 가지뿐이라 마구 넣어 보기를 막는다. 잠그면서 횟수는 0으로 돌린다.
  - 없는 닉네임도 틀린 비밀번호와 같은 401 `login_failed`를 준다.
  - 잊어버리면 화면에서 되찾는 방법은 없고, 운영자가 DB에서 처리한다(아래 「운영자 복구」). 로그인 실패 창이 「기억나지 않으면 운영자에게 알려 주세요」를 안내한다. 잠금은 남이 일부러 틀려서 내 계정을 10분 막을 수도 있다는 한계가 있다. 다만 막히는 것은 다시 로그인할 때뿐이고, 쓰던 기기는 playerId로 계속 들어간다.
  - 로그인 아이디를 닉네임과 따로 두는 안 · 이름(실명) 로그인 안은 검토 후 기각했다(2026-11-03). 실명은 동명이인 · 추측 · PC 한글 입력 문제가 있고, 따로 둔 아이디는 가입 중복 확인으로 훑을 수 있어 숨기는 효과가 작고 외울 것만 늘어난다. 닉네임 로그인을 유지하고 위 「닉네임」의 정규화(공백 · 대소문자)로 보강했다. 아이디를 다시 볼 때는 닉네임 바꾸기 기능 · PC 정식 지원 · 실제 계정 도용이 생겼을 때다.
- **운영자 복구**(서버에 SSH로 접속해 실행)
  - 명령 끝의 `--`는 지우지 않는다. `-`로 시작하는 닉네임을 node가 자기 옵션으로 읽지 않게 한다.
  - 쓰는 명령이 `database is locked`로 끝나면 서버가 마침 쓰던 순간이라 다시 실행하면 된다. 서버를 멈출 필요는 없다.
  - 닉네임 찾아 주기: 본인 확인(직접 만나서) 뒤 이름으로 찾는다. 이름은 앞뒤 공백까지 그대로 저장돼 있어 `trim`으로 비교한다. 동명이인은 팀 · 가입일로 가린다.
    ```bash
    sudo -u flamong node -e "const db=new (require('node:sqlite').DatabaseSync)('/var/lib/flamong/flamong.db',{readOnly:true});console.log(db.prepare('SELECT nickname, team, created_at FROM player WHERE trim(name) = trim(?)').all(process.argv[1]))" -- '이름'
    ```
  - 비밀번호 새로 정해 주기(잠금도 함께 푼다): `/opt/flamong/server`에서 실행한다. 가입과 같은 규칙(숫자 4~6자리)이 아니면 바꾸지 않고 멈춘다.
    ```bash
    cd /opt/flamong/server && sudo -u flamong node --input-type=module -e "import { DatabaseSync } from 'node:sqlite'; import { hashPassword, PASSWORD_PATTERN } from './rules/password.js'; const [nick, pw] = process.argv.slice(1); if (!new RegExp(PASSWORD_PATTERN).test(pw)) throw new Error('비밀번호는 숫자 4~6자리'); const db = new DatabaseSync('/var/lib/flamong/flamong.db', { timeout: 5000 }); console.log(db.prepare('UPDATE player SET password_hash = ?, login_fails = 0, locked_until = NULL WHERE nickname = ? COLLATE NOCASE').run(hashPassword(pw), nick).changes)" -- '닉네임' '새비밀번호'
    ```
    - `1`이 나오면 바뀐 것이다. `0`이면 그 닉네임이 없다. 새 비밀번호는 본인에게 직접 알려 준다. 비밀번호 변경 화면은 없다.
  - 소식 작성권 켜기(운영진): 끄려면 `1`을 `0`으로.
    ```bash
    sudo -u flamong node -e "const db=new (require('node:sqlite').DatabaseSync)('/var/lib/flamong/flamong.db',{timeout:5000});console.log(db.prepare('UPDATE player SET can_post_news = 1 WHERE nickname = ? COLLATE NOCASE').run(process.argv[1]).changes)" -- '닉네임'
    ```
  - 게임 점수 지우기(조작 의심): 그달(KST `YYYY-MM`) 한 사람의 기록을 지운다. `1`이면 지운 것, `0`이면 없다. 다시 플레이하면 새로 쌓인다.
    ```bash
    sudo -u flamong node -e "const db=new (require('node:sqlite').DatabaseSync)('/var/lib/flamong/flamong.db',{timeout:5000});console.log(db.prepare('DELETE FROM score WHERE month = ? AND player_id = (SELECT id FROM player WHERE nickname = ? COLLATE NOCASE)').run(process.argv[1], process.argv[2]).changes)" -- '2026-11' '닉네임'
    ```
- **IP당 횟수 제한**(`rules/rateLimit.js`, `index.js`의 `LIMITS`): 가입 10분 30번 · 닉네임 확인 1분 60번 · 로그인 10분 60번. 넘으면 429 `rate_limited`. 가짜 계정 도배와 닉네임 훑기를 막는다. 회사 와이파이는 직원이 모두 같은 IP로 나가서 넉넉하게 잡았다. 메모리에만 세므로 서버를 다시 켜면 초기화된다.
- 팀 · 이름은 프로필일 뿐 검증하지 않는다. 슬랙 OAuth 등은 2.0 이후 결정.
- **명단 대조 없이 누구나 가입한다**(2026-11-01 확정). 임직원 명단을 받을 방법이 없다. 사내에 QR로만 알리는 주소라 외부 유입이 적고, 문제 계정은 관리자가 DB에서 정리한다.

---

## 데이터 모델 (`Server/schema.sql`)

모든 테이블이 `schema.sql`에 있다.

```sql
player      (id TEXT PK, team, name, nickname UNIQUE, can_post_news INT DEFAULT 0, created_at,
             hearts INT, hearts_date TEXT,           -- 저장된 하트와 그 기준일(KST). 가입 때 5개 · 가입일
             password_hash TEXT, login_fails INT DEFAULT 0, locked_until TEXT NULL)   -- 로그인 (「인증」)
             UNIQUE INDEX player_nickname_nocase (nickname COLLATE NOCASE)  -- 대소문자만 다른 닉네임 금지 (「닉네임」)
character   (id INTEGER PK, player_id, species_id INT, affection INT DEFAULT 0,
             started_at, ended_at NULL)              -- ended_at NULL 인 것이 활성
             UNIQUE INDEX one_active_character (player_id) WHERE ended_at IS NULL  -- 활성은 1개
emotion_log (player_id, date, emotion,              UNIQUE(player_id, date))   -- 오늘의 감정, 하루 1회
score       (player_id, game, month, best_score INT,  UNIQUE(player_id, game, month))   -- 게임 · 달(KST)마다 개인 최고 (「미니게임 · 랭킹」)
post       (id INTEGER PK, player_id, date, kind, body,  UNIQUE(player_id, date, kind))
potion     (player_id, mission_id, claimed_at,     UNIQUE(player_id, mission_id))   -- 받은 묘약. 방문 묘약은 'visit:N'
event      (id INTEGER PK, player_id, type, ts, payload TEXT)
            INDEX event_player (player_id)          -- 플레이어의 마지막 게임 기록 찾기
```

- 하트 충전은 스케줄러 없이 `min(15, hearts + 5 × (오늘 − hearts_date))`로 계산한다(`rules/growth.js` `rechargeHearts`). 언제 계산해도 같은 값이라 `GET /me`는 계산만 하고 저장하지 않는다. 하트를 쓰는 `POST /tap`이 (계산값 − 1, 오늘)을 함께 저장한다.
- 스키마를 바꾼 뒤에는 기존 `Server/flamong.db*`를 지우고 서버를 다시 켠다. `CREATE TABLE IF NOT EXISTS`는 이미 있는 테이블에 열을 더하지 않는다. 마이그레이션 도구는 두지 않는다(실사용자 생기기 전까지).
  - 비어 있는 표 하나만 바뀌면 그 표만 `DROP TABLE`하고 다시 켜도 된다. 테스트 계정이 남는다. 2026-11-03 `score`에 `game` 열을 넣을 때 이렇게 했다.

---

## API

모든 요청: `X-Player-Id` 헤더 (players 생성 · check · login 제외). 헤더가 없으면 401 `no_player`, 모르는 id면 401 `unknown_player`. **404 `not_found`는 없는 주소에만** 쓴다. 클라이언트는 401을 「계정 없음」으로 보고 기기의 `playerId`를 지우므로, 주소 실수(404)와 섞이지 않게 나눴다. 규칙 위반은 409, 검증 실패 400. 가입 · 닉네임 확인 · 로그인은 IP당 횟수를 넘으면 429 `rate_limited`(「인증」).
본문이 없는 POST(`/tap` 등)도 `{}`를 보낸다. `Content-Type: application/json`에 빈 본문이면 Fastify가 400 `FST_ERR_CTP_EMPTY_JSON_BODY`로 거절한다.

응답 본문은 아래 표의 「응답」 그대로다. 성공은 200, 에러는 HTTP 상태 코드와 `{ error, message? }`. 500은 메시지를 감추고 서버 로그에만 남긴다.
클라이언트(`NetworkManager`)는 400 이상이면 `onError(상태 코드, 본문)`으로 보낸다(본문이 없는 연결 실패 · 시간 초과는 Unity 오류 문구). 본문의 `error` 값은 로그로 찍기만 하고, 화면 분기는 코드 숫자로만 나눈다.
- 씬 스크립트는 성공이면 받은 값(`JsonUtility.ToJson`)을, 실패면 코드와 본문을 `print`로 찍는다. 가입 · 로그인 응답의 `playerId`는 웹 빌드 브라우저 콘솔에 남지 않게 앞 6자리만 찍는다.

**1.0 서버 API 10개, 1.1의 `/collection` · 게임 4개, 1.2의 묘약 2개 · 꽃밭 2개가 모두 구현되어 있다** (`routes/players.js` · `routes/character.js` · `routes/games.js` · `routes/potion.js` · `routes/posts.js`, `npm test` 47개 통과).
수동 테스트용 요청은 `Server/requests.http`(VS Code REST Client)에 있다. 이 파일은 gitignore 대상이라 로컬에만 있다.

| 메서드 | 경로 | 요청 | 응답 | 버전 |
|---|---|---|---|---|
| GET | `/players/check?nickname=` | - | `{ available }` · 중복(대소문자 무시)이거나, 허용 글자 밖 · 앞뒤 공백 · 이어진 공백이면 false **(구현)** | 1.0 |
| POST | `/players` | `{ team, name, nickname, password }` | `{ playerId }` · 중복 409 · 닉네임 글자 400 `nickname_charset` · 팀 · 이름 글자 400 `profile_charset` · 비밀번호가 숫자 4~6자리가 아니면 400 **(구현)** | 1.0 |
| POST | `/players/login` | `{ nickname, password }` | `{ playerId }` · 닉네임은 앞뒤 공백을 지우고 대소문자를 무시해 찾는다 · 틀리거나 없는 닉네임 401 `login_failed` · 5번 틀리면 10분 동안 429 `locked` **(구현)** | 1.0 |
| PATCH | `/players/me` | `{ team?, name? }` | `{ ok }` · 글자 400 `profile_charset` **(구현)** | 1.0 |
| DELETE | `/players/me` | - | `{ ok }` · 캐릭터·탭·점수·글·묘약 삭제, `event`는 유지 **(구현)** | 1.0 |
| GET | `/me` | - | `{ team, name, nickname, speciesId, emotionToday, growth, lines, potionReady, newsReady }` · 알을 아직 안 골랐으면 `speciesId` -1 · `lines` 빈 배열. `lines`는 오늘의 「플라몽 한마디」 3줄(「규칙 › 홈화면」). `potionReady`는 지금 마실 수 있는 묘약이 있는지(Main 묘약 빨간 점), `newsReady`는 오늘의 소식 담당이고 아직 안 썼는지(Main 확성기 빨간 점). 가입 여부 확인(401이면 서버에 없는 계정)과 설정 창의 현재 정보에도 쓴다 **(구현)** | 1.0 |
| POST | `/characters` | `{ eggId }` (0~7) | `{ speciesId }` · 활성 캐릭터가 이미 있으면 새로 뽑지 않고 그 `speciesId`를 200으로 돌려준다(시간 초과 후 재시도해도 같은 결과) **(구현)** | 1.0 |
| POST | `/characters/reset` | `{}` | `{ ok }` · 지금 캐릭터를 끝내 보관(`ended_at`). 남은 하트는 그대로. 캐릭터가 없으면(재시도) 그대로 ok **(구현)** | 1.0 |
| POST | `/tap` | `{}` | `{ levelUp, growth }` · 하트 1개 사용. 캐릭터 없음 409 `no_character`, 완성 409 `completed`, 하트 0 409 `no_hearts` **(구현)** | 1.0 |
| POST | `/emotions` | `{ emotion }` | `{ levelUp, growth, reply, weather }` · `reply`는 고른 감정에 맞장구치는 한 줄 · `weather`는 어제의 마음 날씨 `{ total, bars }`(`bars`는 감정 5종 순서의 막대 길이 0~1, 어제 5명 미만이면 빈 배열) · 하루 2번째는 409 `emotion_done`, 캐릭터 없음 409 `no_character` **(구현)** | 1.0 |
| GET | `/collection` | - | `{ levels: [24], completed: [speciesId…], speciesId }` · `levels[speciesId]` = 도달한 최고 레벨(0 = 없음, 지금 캐릭터 · 완성한 캐릭터만), `completed` = 완성한 종 오름차순, `speciesId` = 지금 캐릭터(없으면 -1) **(구현)** | 1.1 |
| POST | `/games/start` | `{ game }` (`tidy`) | `{ rules }` · 한 판의 수치(`durationSec` · `countdownSec` · `minTapMs` · `lockMs` · `changeEvery` · `maxRun` · `comboAt`). `game_play`를 남긴다. 새 시작은 앞의 시작을 덮는다. 모르는 게임 400 **(구현)** | 1.1 |
| POST | `/scores` | `{ score }` (0~863 정수) | `{ best, rank, gap, gain, levelUp, growth }` · 이번 달(KST) 이 게임의 내 최고 · 등수(동점은 같은 등수) · 다음 등수까지 점수(1등 0) · 오른 애정도(하루 첫 판 +2, 게임 무관). 409: 시작 없음 `not_started` · 시작 뒤 32초 전 `too_fast` · 10분 넘음 `expired`. 이미 받은 판을 다시 보내면 쓰지 않고 200 · `gain` 0 **(구현)** | 1.1 |
| GET | `/rankings?month=YYYY-MM` | - | `{ month, game, top: [{ rank, nickname, best }], rank, best, gap }` · 그달 이달의 게임 랭킹. `month`를 빼면 이번 달(KST). `top`은 상위 20, `rank` · `best` · `gap`은 내 기록(없으면 0). 출시 전 달 · 다음 달 이후는 `game` `""` · 빈 목록. 형식이 틀린 달 400 **(구현)** | 1.1 |
| GET | `/games/months` | - | `{ current, months: [{ month, game, best, rank }] }` · 「이달의 게임」 달력 12칸. 올해(KST) 1~12월의 게임과 내 최고 · 등수(없으면 0). 출시 전 달 · 다음 달 이후는 `game` `""`. `current`는 이번 달 **(구현)** | 1.1 |
| GET | `/missions` | - | `{ canDrink, visit: { progress, need }, hidden: [{ id, done, claimed }] }` · `canDrink`는 캐릭터가 있고 완성 전인지. `hidden`은 `first_complete` · `first_game` · `first_garden` 순서 **(구현)** | 1.2 |
| POST | `/potions/claim` | `{ missionId }` (`visit` · `first_complete` · `first_game` · `first_garden`) | `{ levelUp, growth }` · 애정도 +10. 409: 달성 전 `not_ready` · 이미 받음 `claimed` · 캐릭터 없음 `no_character` · 완성 `completed`. 응답을 못 받고 다시 보내면 409가 오니 `/missions`를 다시 부른다 **(구현)** | 1.2 |
| GET | `/posts?kind=garden\|news` | - | `{ canWrite, posts: [{ body, date, nickname }] }` · 최신순 30개. `nickname`은 소식만, 꽃밭은 `""`. `canWrite`는 오늘 아직 안 썼고 (꽃밭이거나 소식 작성권이 있는지) **(구현)** | 1.2 |
| POST | `/posts` | `{ kind, body }` (`body` 1~100자) | `{ ok }` · 허용 밖 글자 · 공백만 400 `body_charset`, 100자 초과는 스키마 검사 400, 오늘 이미 씀 409 `posted`, 소식 작성권 없음 409 `no_right`. `event`는 꽃밭 `post` · 소식 `news` **(구현)** | 1.2 |

### `growth` (성장 상태)

애정도나 하트가 바뀌는 응답은 모두 이 묶음을 담는다. 클라이언트는 이것 하나로 「횟수」 · 게이지 · 「Lv」를 갱신한다. C# 정의는 `ServerData.cs`의 `Growth`.

| 필드 | 뜻 |
|---|---|
| `hearts` | 지금 쓸 수 있는 하트 (0~15) |
| `heartsMax` | 저장 상한 (15) |
| `affection` | 누적 애정도 (0~175). 참고용 |
| `level` | 1~5 |
| `levelProgress` | 이 레벨이 된 뒤 모은 애정도. 받은 성장표의 「화면 표시 점수」 |
| `levelNeed` | 이 레벨을 다 채우는 데 필요한 양 (5 / 25 / 35 / 50 / 60) |
| `completed` | Lv5 게이지를 다 채웠는지. true면 하트 버튼을 끈다 |

| 누적 애정도 | `level` | `levelProgress` / `levelNeed` | 게이지 | `completed` |
|---|---|---|---|---|
| 0 | 1 | 0 / 5 | 0% | false |
| 4 | 1 | 4 / 5 | 80% | false |
| 5 | 2 | 0 / 25 | 0% (레벨업 직후) | false |
| 20 | 2 | 15 / 25 | 60% | false |
| 30 | 3 | 0 / 35 | 0% | false |
| 115 | 5 | 0 / 60 | 0% | false |
| 175 | 5 | **60 / 60** | 100% | **true** |

- 게이지는 `levelProgress ÷ levelNeed`. `levelNeed`는 항상 1 이상이라 0으로 나누는 일이 없다.
- 서버는 누적 애정도만 저장한다. 레벨과 진행값은 응답을 만들 때 계산한다. 그래서 한 번에 2 이상 올라 넘친 양은 자동으로 다음 레벨로 이월되고, 175를 넘친 양은 버린다.

```json
{ "levelUp": false,
  "growth": { "hearts": 4, "heartsMax": 15, "affection": 1,
              "level": 1, "levelProgress": 1, "levelNeed": 5,
              "completed": false } }
```

---

## 폴더 구조

```
Assets/Content/<씬>/
  01_Scenes/<씬>.unity
  02_Scripts/                   씬 컨트롤러(<씬>Scene.cs) + 그 씬에서만 쓰는 컴포넌트. 다른 씬 폴더 참조 금지
                                두 씬 이상에서 쓰게 되면 Common/02_Scripts 로 옮긴다
  03_Resources/                 씬별 텍스처·오디오 (씬별 아틀라스). Unity 특수 폴더 `Resources`와 이름만 비슷하고 동작은 다르다
Assets/Content/Common/
  02_Scripts/Manager/           SessionState(씬 간 데이터 · playerId 저장 · 터치 수 저장 열쇠 `HEART_TAPS_KEY`, 싱글톤)
                                · SoundManager(배경음 · 효과음 볼륨과 재생, 싱글톤. 볼륨은 PlayerPrefs `BgmVolume` · `SfxVolume`)
  02_Scripts/Manager/Network/   NetworkManager(UnityWebRequest GET·POST·PATCH, 싱글톤, 서버 주소 `BASE_URL`은 여기 한 곳) · ServerData(요청·응답 DTO)
  02_Scripts/Util/              SceneLoader(SceneName 상수 · 중복 로드 방지) · SceneFader · Singleton · ErrorPanel · ConfirmPanel · NoticePanel · EntryRouter(들어갈 곳)
                                · GameList(게임 id → 이름 · 씬. 이 빌드가 모르는 게임이면 null) · RankingPanel(한 달 랭킹 창)
                                · ButtonSound(버튼에 붙이면 누를 때 pop. 모든 버튼에 붙인다, Main 하트 버튼만 뺌)
                                · WebKeyboard(.cs + .jslib, 웹 빌드 전용. Unity 폰 키보드는 비밀번호 칸이면 NumberPad를 무시하므로, type=password 가 되는 순간 inputmode=numeric 을 붙여 숫자 키패드를 띄운다)
  02_Scripts/Editor/            에디터 전용(빌드에 안 들어감). BuildVersionPrompt: Build 버튼을 누르면 버전 입력 창을 띄우고 그 값을 Player Settings Version 에 넣은 뒤 빌드
  03_Resources/                 Font · Egg · Heart · Icon · UI · Background · Character · Emotion · Sound
  04_Prefabs/                   두 씬 이상에서 쓰는 UI. Panel - Error(서버 오류 안내 창, ErrorPanel.Show(다시 시도할 일))
                                · Panel - Confirm(네/아니요 확인 창, ConfirmPanel.Show(문구, [네]를 누르면 할 일, [아니요]를 누르면 할 일(없으면 닫기만)))
                                · Panel - Notice(문구와 [확인]만 있는 안내 창, NoticePanel.Show(문구))
                                · Panel - Ranking(한 달 랭킹 창 20줄, RankingPanel.ShowRankingBoard(Board). Game 달력 · Tidy가 같이 쓴다)
Assets/Settings/                URP · Input System (Unity 생성)
Server/
  index.js                      Fastify 조립 · 에러 형식 { error } · now() 주입
  db.js  schema.sql             node:sqlite · logEvent · tx(트랜잭션) · playerOf(X-Player-Id → 플레이어, 없으면 401)
  routes/players.js             가입 · 닉네임 확인 · 수정 · 탈퇴 (라우트 + JSON 스키마)
  routes/character.js           /me · /characters · /tap · /emotions · /collection
  routes/potion.js              /missions · /potions/claim · 미션 상태(missionsOf)
  routes/posts.js               /posts · 오늘의 소식 담당(newsPickOf)
  routes/games.js               /games/start · /scores · /rankings · /games/months (한 판 검증은 event 로)
  rules/text.js · hangul.txt    닉네임 · 글 허용 글자 (순수 함수, DB · HTTP 모름). hangul.txt 는 폰트가 그리는 한글 2,780자
  rules/password.js             비밀번호 해시 · 확인 · 로그인 잠금
  rules/growth.js               성장 수치 단일 출처 · 레벨 · 하트 충전 · KST 날짜
  rules/species.js              알 → 캐릭터 뽑기
  rules/lines.js                플라몽 한마디 대사 표 · 그날의 3줄(linesOf) · 감정 맞장구(replyOf)
  rules/weather.js              어제의 마음 날씨: 감정별 인원 → 막대 길이(weatherOf) · 최소 인원(WEATHER_MIN)
  rules/game.js                 미니게임 수치 단일 출처(TIDY) · 점수 상한 · 최소 시간 · 이달의 게임 표(GAME_OF_MONTH)
  rules/rateLimit.js            IP당 횟수 제한 (가입 · 닉네임 확인 · 로그인)
  deploy/                       Oracle VM 설정: setup.sh(처음 한 번) · flamong.service(systemd) · Caddyfile(HTTPS · /api · 웹 빌드)
  test/*.test.js                node:test
  requests.http                 수동 테스트 (gitignore, 로컬 전용)
```

공용 오브젝트 풀은 아직 없다(Main 하트는 `HeartParticles`가 자체 풀을 쓴다). 두 씬 이상에서 필요해질 때 `Common/02_Scripts`에 만든다.

### UI 캔버스 구성
| 캔버스 | 그리기 순서 | 담는 것 |
|---|---|---|
| Canvas - Main | 0 | 배경 · 제목 · 고정 버튼 |
| Canvas - Dynamic | 1 | 스크롤 목록, 회전하는 알처럼 매 프레임 움직이는 것만 |
| 팝업 (Main 아래 하위 Canvas) | 2 (Override Sorting) | 씬마다의 창(설정 · 오늘의 감정 · 결과 등)과 공통 프리팹 `Panel - Ranking`(Game · Tidy). 기본 꺼짐. GraphicRaycaster 필수 |
| 확인 창 (공통 프리팹 `Panel - Confirm`) | 3 | 팝업 위에서 「정말 할까요?」를 묻는다(Main 설정 창 · Tidy 판 중 [뒤로]) |
| 안내 창 (공통 프리팹 `Panel - Notice`) | 4 | 레벨업 · 완성(Main), 「이번 판은 기록되지 않았어요」(Tidy)처럼 알리기만 한다. 하트 응답이 늦게 와서 확인 창이 떠 있는 동안 레벨업 안내가 뜰 수 있어 확인 창과 숫자를 나눴다 |
| 오류 창 (공통 프리팹 `Panel - Error`) | 5 | 무엇이 떠 있든 맨 위. Title · Intro · Hatch · Main · Collection · Game · Tidy · Potion · Garden · News |

같은 순서끼리는 하이어라키에서 뒤에 있어도 위에 그려진다는 보장이 없다(설정 창 뒤에 확인 창이 숨은 적이 있음). 겹쳐 뜰 수 있는 창은 순서 숫자로 나눈다.

움직이는 UI를 따로 두면 그것이 움직여도 배경과 버튼까지 다시 그려지지 않는다. 누를 것이 없는 캔버스에는 GraphicRaycaster를 붙이지 않는다. Main의 `Canvas - Dynamic`은 캐릭터가 버튼(플라몽 한마디)이라 GraphicRaycaster를 붙였다. 누를 수 있는 것은 캐릭터 하나뿐이고, 팝업(순서 2 이상)이 뜨면 팝업의 어두운 바탕이 먼저 받는다.

모든 캔버스(씬 · 공통 프리팹)는 `Vertex Color Always In Gamma Color Space`를 켜 둔다. 색 공간이 Linear라서, 꺼져 있으면 고른 색이 8비트 Linear로 바뀌며 어두운 색이 조금 달라진다.

---

## 아트 리소스

- **디자이너가 따로 없어 지금 그림을 정식으로 쓴다**(2026-11-01). 시안에서 잘라낸 그림은 원본이 약 100px 안팎이라 화면에서 1.7~2.6배로 늘어나 흐리다. 이런 그림은 같은 파일명 · 같은 스프라이트 ID로, 보이는 크기의 1.25배 해상도로 코드로 다시 그린다(시트 방식 그림은 잘라내는 영역도 새 크기로 바꾼다).
  - 1단계 Main (2026-11-01 완료): `heart_tap` · `heart_fill`, `gauge_fill` · `gauge_frame`, `btn_flower_sky · mint · pink`, `icon_flower` · `icon_gear` · `icon_mail` · `icon_close`. 바탕색은 원본에서 그대로 뽑았다.
  - 2단계 알 고르기 · 부화 (2026-11-01 완료): 알 8종은 모양 · 무늬가 제각각이라 새로 그리지 않고, 원본을 3배로 키운 뒤 원본에서 뽑은 색 사이 경계를 다시 세우고 윤곽선만 같은 파란색 · 두께로 새로 그었다. `egg_unknown_pink · yellow`, `icon_triangle`, `btn_oval_yellow`는 새로 그렸다.
  - 3단계 (2026-11-01 완료): 작은 하트 4종(`heart_pink · yellow · lavender · mint`)은 알과 같은 방식, `icon_back`은 새로 그렸다. 배경 그라데이션 6장(`grad_*`, 4×256)은 폭 4px를 블록 압축(ASTC)하면 계단 띠가 생겨 압축을 껐다(장당 4KB). 남는 옅은 줄은 8비트 색 단계(약 24px마다 1단계)라 그대로 둔다.
  - 그대로 두는 것: `bg_main`(Main 배경, 256×512)은 흐린 빛 번짐 그림이라 키워도 차이가 없다.
  - 진화 그림(Main에서 1.73배)은 다운로드 예산 때문에 256²를 유지한다.
- 시안이 없는 화면(오늘의 감정 팝업 · 묘약 · 꽃밭 · 소식)은 기존 톤(파란 손그림 테두리, 흰 카드, 노란 버튼)에 맞춰 직접 그린다. 시안 원본은 기획서(Confluence)에 있고 저장소에는 넣지 않는다.
- 물결 밑줄(`line_wavy`), 노란 라벨 글로우(`label_glow_yellow`), 흰 박스 버튼(`btn_box_white`), 노란 타원 버튼(`btn_oval_yellow`), 크림색 라벨(`label_glow`)은 시안을 보고 새로 그렸다.
- 9-slice로 쓰는 것: `frame_card`, `frame_card_white`, `line_wavy`, `label_glow_yellow`, `btn_box_white`.
- **캐릭터**: `Character/char_{speciesId 두 자리}_lv{레벨}.png` (예: `char_07_lv3`). 24종 × Lv1~5 = 120장, 256², 단일 스프라이트, 웹 빌드(ASTC)에서 장당 약 29KB(합계 약 3.5MB). 구성과 알별 컨셉은 「규칙 › 성장 밸런스」. 같은 알 안의 3종은 몸통 톤(기본 · 밝게 · 진하게)과 지느러미 색(연두 · 하늘 · 분홍)으로 구분한다.
- **감정 아이콘 5종**(`Common/03_Resources/Emotion/icon_emotion_*`)과 묘약 버튼 빨간 점(`Main/03_Resources/dot_red`, 64²)은 코드로 그렸다. 감정 아이콘은 Main 감정 버튼과 Tidy 서랍 · 줄이 같이 써서 2026-11-03 Common으로 옮겼다(GUID 그대로).
- **묘약** (`Potion/03_Resources`, 코드로 그림): `potion_bottle`(병 테두리 · 눈금 6개, 속은 비움) 위로 `potion_liquid`(흰 몸통 모양)를 보라로 칠해 Filled · Vertical로 7분의 1씩 채운다. 눈금은 내용물 높이를 7등분한 자리다. `icon_potion`은 달성한 히든 미션 아이콘이다.
- **흰 카드** `frame_card_white`(`Common/03_Resources/UI`): `frame_card`의 민트 바탕만 흰색으로 바꾼 9-slice 카드. 묘약 · 꽃밭 · 소식이 같이 써서 Common으로 옮겼다.
- **꽃밭 · 소식**은 새 그림이 없다. 꽃밭 카드는 Main 버튼의 꽃 그림(`btn_flower_pink · sky · mint`)을 돌려 쓰고, 소식 카드는 `icon_megaphone`, 배경은 꽃밭 `grad_green` · 소식 `grad_yellow`.
- **확성기 아이콘**(`Common/03_Resources/Icon/icon_megaphone`, 142², 코드로 그림): Main 상단 소식 버튼. 다른 아이콘보다 가로로 길어 버튼 크기를 165로 키워 무게를 맞췄다(꽃 · 기어는 130).
- **게이지**는 이미지 두 장으로 세 겹을 만든다.
  - `gauge_fill`은 흰색 모양이다. Image Color로 칠해 빈 바탕(`#D7DBDC`)과 채움(`#FFFEB1`, Filled · Horizontal)에 한 번씩 쓴다.
  - 맨 위에 `gauge_frame`(파란 테두리 + 눈금)을 올린다.
- 공통 색: 글자·외곽선 파랑 `#2F60F6`, 노란 버튼 `#EEFD88`, 스크롤 막대 `#F6EF8C`.
- **폰트**: 배달의민족 꾸불림체(`BMKkubulimTTF SDF`). 2048² 정적 아틀라스이고, 문자 목록은 `Font/CharSet_KSX1001.txt`(ASCII 95 + 완성형 2350 + 기호 24)다.
  - 폴백 1 `BMKkubulimTTF Jamo SDF`: 단독 자모 ㄱ~ㅣ 51자, 512×256 정적 아틀라스. 입력 중인 「ㄱ」 같은 글자가 □로 보이지 않게 한다. 본 아틀라스와 같은 35pt · 여백 5로 구워 글자 크기가 같다.
  - 폴백 2 `BMKkubulimTTF Extra SDF`(2026-11-03): 완성형 밖에서 꾸불림체가 실제로 그리는 한글 430자(「됬」 「뷁」 「똠」 「햏」 등), 1024² 정적 아틀라스. 같은 35pt · 여백 5라 본 아틀라스와 글자 크기가 같다.
    - 11-01에는 원본 TTF에서 실행 중에 굽는 동적 폴백을 썼다. 그런데 TTF가 그리는 한글이 2,780자뿐이라(「닉네임」), 동적으로 그릴 글자도 이 430자가 전부였다. 정적으로 바꿔 TTF(압축 전 2.8MB)를 빌드에서 뺐다.
    - 그 결과 다운로드 · 힙이 줄고, 실행 중에 글자를 굽지 않는다.
    - 입력 중에 잠깐 생기는 글자 중 폰트에 모양이 없는 것(「이해」를 칠 때의 「잏」 등)은 그 순간만 빈칸으로 보인다. 동적 폴백 때도 같았다.
  - 한글 줄바꿈(2026-11-02): `TMP Settings`의 `Use Modern Hangul Line Breaking Rules`를 켜서 칸 끝에서 글자 단위가 아니라 띄어쓰기 단위로 줄을 바꾼다(「키웠 / 어요」 → 「끝까지 / 키웠어요」). 전체 씬 글자를 켜기 전후로 비교해 바뀐 곳은 묘약 히든 미션 설명 3개뿐이었다.
  - 라이선스: SIL OFL 1.1 (`Font/OFL.txt`). 게임에 넣고 수정 · 재배포할 수 있고, 폰트 파일만 따로 파는 것은 금지다. 빌드를 배포할 때 이 파일을 빌드 폴더에 같이 올린다.
- **배경음** `Sound/bgm_clockwork_ballerina`(2026-11-04): Geoff Harvey 「Clockwork Ballerina」([Pixabay](https://pixabay.com/music/cartoons-clockwork-ballerina-154531/)), 오르골 · 첼레스타, 1분 21초. 「몽글몽글하고 따뜻한」 방향으로 골랐다.
  - 한 곡을 모든 화면(마음 정리 포함)에 쓴다. 예산이 BGM 1개이고, `SoundManager`가 씬을 넘어 이어 틀어서 화면을 바꿔도 끊기지 않는다.
  - Title이 열릴 때 `TitleScene.Start`가 튼다. 하지만 브라우저(폰 Chrome · Safari, PC Chrome)는 사용자가 페이지를 한 번 누르기 전에는 소리를 막아서, 폰에서는 Title을 처음 누를 때부터 들린다(2026-11-04 폰 확인). 코드로 피할 수 없는 브라우저 규칙이고, Title을 누르는 것이 곧 게임에 들어가는 것이라 그대로 둔다. 로그아웃으로 Title이 다시 열려도 같은 곡이면 처음으로 돌아가지 않는다.
  - 가져오기 설정: CompressedInMemory(Web은 Streaming을 지원하지 않고, Unity가 배경음에 권하는 설정), 모노, 품질 0.1. 웹 빌드에서는 AAC로 바뀐다. 원본은 MP3 256kbps 스테레오 2.6MB다.
    - 빌드에 들어간 크기: 품질 0.5면 1.31MB(약 128kbps), 0.1이면 0.99MB(약 96kbps). AAC는 Brotli로 더 줄지 않아 다운로드에 그대로 더해진다. 폰 스피커로는 차이가 거의 없어 0.1로 둔다.
  - 라이선스: Pixabay Content License(무료 · 출처 표기 의무 없음). 이 곡은 Content ID가 등록되어 있어, 플레이 영상을 유튜브에 올리면 저작권 표시가 붙을 수 있다. 작곡가는 설명란에 「Geoff Harvey - Pixabay」라고 적으면 표시가 붙지 않는다고 안내한다.
- **효과음 5개** `Sound/sfx_*`(2026-11-05): 예산 상한 5개. 배경음(오르골)과 어울리는 말랑하고 따뜻한 소리로 골랐다. 모두 Freesound CC0(출처 표기 의무 없음, 아래는 기록용)이다.

  | 파일 | 쓰는 곳 | 길이 | 원본 |
  |---|---|---|---|
  | `sfx_pop` | 하트 터치 · 마음 정리 정답 · 모든 버튼 | 0.18초 | arttim 「Bubble_Pop」 [733264](https://freesound.org/people/arttim/sounds/733264/) |
  | `sfx_sparkle` | 하트 전달(+1) · 오늘의 감정 · 묘약 받기 · 마음 정리 「시작!」 | 1.31초 | ZenithInfinitiveStudios 「Fantasy_ui_Button_3」 [376745](https://freesound.org/people/ZenithInfinitiveStudios/sounds/376745/) |
  | `sfx_levelup` | 레벨업 · 다 자랐을 때(반짝임 대신) · 마음 정리 결과 창 | 2.53초 | FunWithSound 「Short Success Sound Glockenspiel」 [456965](https://freesound.org/people/FunWithSound/sounds/456965/) |
  | `sfx_wrong` | 마음 정리 오답 | 0.31초 | ertfelda 「incorrect」 [243700](https://freesound.org/people/ertfelda/sounds/243700/) |
  | `sfx_count` | 마음 정리 3 · 2 · 1(멈춘 뒤 다시 셀 때도) | 1.10초 | moodyfingers 「Wind Up Music Box [E6 Note]」 [732946](https://freesound.org/people/moodyfingers/sounds/732946/) |

  - `SoundManager`(Title)가 다섯 개를 들고 있고, 각 씬은 `SoundManager.Instance.PlaySfx(SoundManager.Instance.pop)`처럼 부른다. 하나의 `sfxSource`에서 `PlayOneShot`으로 겹쳐 낸다.
  - 가져오기 설정: **DecompressOnLoad**, 모노, 품질 0.1. Web은 압축된 채 재생하면 늦게 나서, 누르는 순간 나야 하는 소리는 풀어 둔다(다섯 개를 다 풀어도 메모리 약 1MB). 받은 파일은 Freesound 미리듣기 MP3(원본은 로그인이 필요)다.
  - 레벨업 · 완성이 되는 응답에서는 반짝임을 내지 않고 레벨업 소리만 낸다. 마음 정리의 「그만!」은 곧 결과 창 소리가 나서 따로 소리를 두지 않는다.
  - **버튼 소리**: 버튼마다 `ButtonSound`(공용 컴포넌트)를 붙여 누르면 pop이 난다. 씬 스크립트는 고치지 않는다. 2026-11-05에 공통 프리팹 4개(버튼 5개)와 씬 12개(버튼 77개)에 붙였다. Main 하트 버튼은 자기 pop이 있어 두 번 나지 않게 뺐다. 감정 고르기 · 묘약 받기는 누를 때 pop, 응답이 오면 반짝임(또는 레벨업)이 난다. 눌리지 않는 버튼(달력의 지난 · 다음 해 칸 등)은 소리도 나지 않는다. **새 버튼을 만들면 `ButtonSound`를 붙인다.**

---

## 로드맵

| 버전 | 내용 |
|---|---|
| **1.0** | 씬 1~7. 서버: players · characters · me · tap(하트 저장) · emotions(오늘의 감정). WebGL 빌드 → QR 배포 |
| 1.1 | 도감 · 미니게임 「마음 정리」 · 「이달의 게임」 랭킹 |
| 1.2 | 묘약 · 꽃밭 · 소식 |
| 2.0 | Android 빌드 + **Unity Ads**(WebGL 미지원이라 필요) · 대시보드(event 테이블 쿼리) · 인증 |

---

## 기획서와 다른 점

| 기획서 | 여기 | 이유 |
|---|---|---|
| 1.0에 12씬 + 게임 + 랭킹 + 꽃밭 전부 | 1.0은 씬 1~7 | 1인 개발. 성장 루프부터 |
| 매달 새 미니게임 | 「이달의 게임」: 「마음 정리」로 시작해 하나씩 늘린다. 랭킹은 한 달에 한 게임이고, 새 게임은 준비된 달에만 바꾸며 없으면 같은 게임을 잇는다 | 1인 개발이라 매달 새로 만들 수 없다 |
| 탭 5회마다 로그 수집 | 서버가 모든 쓰기를 event로 기록 | 하루 5탭이면 배치 의미 없음 |
| 임직원 리스트 대조 로그인 | 프로필 입력만, 인증은 추후 | 이름만으로는 인증이 안 됨. 슬랙 OAuth를 2.0에 검토 |
| 광고 연계(2.0) | Android 빌드 + Unity Ads | Unity Ads가 WebGL 미지원 |
| 묘약 · 꽃밭 · 소식 · 감정 팝업 시안 없음 | 기존 톤으로 직접 그림 | 디자이너가 따로 없음. 묘약을 이렇게 만들었고 꽃밭 · 소식도 같은 방식 |
| Main 하단 메뉴 「기록」 | 「묘약」 | 「기록」 정의가 없음. 상단 꽃 아이콘이 꽃밭 진입점 |
| Main 상단 홈 아이콘 | 없음 | 이미 홈 화면이라 의미가 겹침 |
| 묘약 상단 홈 아이콘 | 없음 | Main에서만 들어와서 뒤로가기와 같은 곳으로 감. 도감도 뒤로가기만 있음 |
| 이달의 게임 상단 홈 아이콘 · 뒤로가기 아래 줄의 제목 | 홈 아이콘 없음, 제목은 뒤로가기 줄 | 묘약과 같은 이유. 제목 줄은 다른 화면과 맞춤 |
| (없음) | 오늘의 감정 체크인 추가 | 안내문의 「하루에 한 번, 고른 감정 하나가」와 「감정연료」 컨셉을 규칙으로 옮김. 2.0 대시보드의 핵심 데이터 |
| 하루 고정 횟수 | 하트 하루 5개 충전, 최대 15개 저장 | 못 온 날 손해를 줄여 주 3회 방문자도 약 한 달에 완성 |
| 성장표 5 / 40 / 60 / 80 / 100 (누적 285) | 같은 구조, 5 / 25 / 35 / 50 / 60 (누적 175) | 원안은 매일 해도 41일. 한 달 주기인 미니게임 · 랭킹에 맞춤 |

---

## 미정 (폰 테스트 · 배포 때 정할 것)

- **입력 설정은 「둘 다」(`activeInputHandler: 2`) 유지(2026-11-03 결정).**
  - 코드에는 옛 `Input`이 없다. 하지만 이 설정으로 **폰 브라우저 한글 입력이 되는 것을 확인했다**(배포 빌드 `v1.2.0`). 새 Input System 하나로 바꾸면 얻는 것은 작고, 바꿀 때마다 폰 확인을 다시 해야 해서 그대로 둔다.
  - **PC 브라우저에서는 한글이 입력되지 않는다.** Unity 웹 빌드가 데스크톱 IME를 지원하지 않는 한계다(Unity 6 매뉴얼 「IME Input」에서 Web은 미지원). 주 대상이 폰이라 받아들이고, Login 화면에 PC 안내를 띄운다. PC 지원이 필요해지면 입력칸 위에 브라우저 입력칸을 겹치는 방식(WebGLInput 패키지 또는 직접 만든 `.jslib`)을 검토한다.
- **진화 그림 크기.** Main의 `characterSprites`가 120장(웹 빌드 약 3.5MB)을 모두 참조하고, WebGL은 `.data`를 통째로 메모리에 둔다. 씬 예산(48MB) 안이라 어드레서블 없이 두었다. 최근 빌드 다운로드는 12.98MB(여유 2.02MB)다. 11-03에 한글 동적 폰트 폴백(TTF)을 정적 아틀라스로 바꿔 1.18MB 줄였다. Unity 스플래시와 쓰지 않던 TMP 기본 폰트 LiberationSans는 뺐다. 배경음을 넣은 뒤 15MB를 넘으면 Crunch 압축 → 어드레서블(플레이어당 5장만 받음) 순으로 줄인다.

---

## 빌드 기록

| 날짜 | 버전 | 다운로드 (MB) | 힙 피크 (MB) | 비고 |
|---|---|---|---|---|
| 2026-10-26 | 1.0 첫 빌드 (소리 없음) | 13.07 (`.data.br` 6.36 + `.wasm.br` 6.71) | 143 | `Web - Mobile` 프로필(ASTC · Disk Size LTO · Brotli · 씬 6개). 힙은 PC Chrome에서 Title → 가입 → 알 → Hatch → Main을 거친 뒤의 WASM 힙(사용 137.1). PC Chrome은 ASTC를 못 읽어 텍스처를 풀어 쓰므로 폰보다 크게 나온다. 큰 에셋(압축 전): 꾸불림체 SDF 아틀라스 4MB · Unity 스플래시 로고 1.2MB · LiberationSans SDF+TTF 1.3MB(안 씀) · 진화 그림 120장 약 3.5MB(ASTC 장당 29KB) |
| 2026-10-26 | 1.0 정리 빌드 (소리 없음) | 12.55 (`.data.br` 5.84 + `.wasm.br` 6.71) | 138.1 | 빌드 오류 · 경고 0. Global Light 2D · LiberationSans · Unity 로고를 뺐다. PC Chrome JS 힙: 새로 키우기 → 알 → Hatch → Main 10회 순환 전후 24.24 → 24.18MB(1.0배, 기준 1.5배 이내), WASM 힙 그대로. 에디터에서 Main · Hatch · EggSelect 3회 전환 시 텍스처 · Mono 힙 증가 없음(Memory Profiler 대신 직접 측정) |
| 2026-10-28 | 1.1 도감 빌드 (소리 없음) | 12.52 (`.data.br` 5.84 + `.wasm.br` 6.68) | 138.1 | 씬 7개(도감 추가, 빈 씬 Game · Potion · Garden 제외). 빌드 오류 0, 경고 3은 IL2CPP가 TMP의 큰 함수 3개를 따로 컴파일했다는 안내. 힙은 PC Chrome에서 Title → 안내 → 가입 → 알 → Hatch → Main → 도감을 거친 뒤의 WASM 힙(사용 136.9), JS 힙 24.2MB. 도감은 Main과 같은 진화 그림을 써서 크기가 거의 늘지 않았다. 에디터에서 Main ↔ 도감 3회 전환 시 텍스처는 매번 같고(Main 110.66 · 도감 108.06MB, 에디터 텍스처 포함) Mono 힙은 92.2~93.0MB 안에서 오르내릴 뿐 늘지 않음(10-29, 직접 측정) |
| 2026-11-01 | 1.2 묘약 빌드 (소리 없음) | 12.53 (`.data.br` 5.85 + `.wasm.br` 6.67) | 138.1 | 씬 8개(묘약 추가). 빌드 오류 0, 경고 3은 도감 빌드와 같은 TMP 안내. 새 그림 5장(묘약 4 · 빨간 점 1)이 작아 다운로드는 0.01MB만 늘었다. 힙은 PC Chrome에서 Title → 안내 → 로그인 → Main → 묘약(받기 2번) → Main → 도감을 거친 뒤의 WASM 힙(사용 136.9), JS 힙 24.6MB. 에디터에서 Main ↔ 묘약 3회 전환 시 텍스처는 Main 116.5~117.3 · 묘약 80.8~81.1MB로 그대로(에디터 텍스처 포함), Mono 힙은 109.3~112.9MB 안에서 오르내릴 뿐 늘지 않음 |
| 2026-11-01 | 1.2 그림 선명화 빌드 (소리 없음) | 12.55 (`.data.br` 5.85 + `.wasm.br` 6.70) | 138.1 | 씬 8개. 빌드 오류 0, 경고 3은 같은 TMP 안내. 그림 30여 장을 보이는 크기에 맞춰 키웠지만(예: 알 36KB · 게이지 테두리 110KB로 빌드에 들어감) 새 그림은 단색 면이 넓어 Brotli가 잘 줄여 `.data`는 그대로다. `.wasm` +0.02MB는 게이지 연출 · 소식 버튼 코드. 그라데이션 6장은 압축을 꺼 장당 4KB. 힙은 PC Chrome에서 Title → 안내 → 로그인 → Main → 묘약(받기 2번) → Main → 도감을 거친 뒤의 WASM 힙(사용 136.9), JS 힙 24.2MB |
| 2026-11-02 | 1.2 꽃밭 · 소식 빌드 (소리 없음) | 14.16 (`.data.br` 7.45 + `.wasm.br` 6.70) | 140.2 | 씬 10개(꽃밭 · 소식 추가). 빌드 오류 0, 경고 3은 같은 TMP 안내. `.data` +1.60MB는 대부분 한글 동적 폰트 폴백용 TTF(압축 전 2.8MB)다. 동적 SDF 자체는 1.9KB로 비어 있다. 예산 15MB까지 여유는 0.84MB. 힙은 PC Chrome에서 Title → 안내 → 로그인 → Main → 꽃밭(글 심기) → Main → 소식 → Main → 묘약(첫 한마디 받기) → Main → 도감을 거친 뒤의 WASM 힙(사용 138.6), JS 힙 27.0MB. PC 키보드로는 영문만 넣어 봤고, 한글 조합(IME)과 동적 폰트 글자는 폰에서 확인해야 한다. 에디터에서 Main ↔ 꽃밭 · 소식 3회 전환 시 텍스처는 Main 127.91 · 꽃밭 88.84 · 소식 88.14MB로 그대로(에디터 텍스처 포함), 오브젝트 수도 같음. Mono 힙은 명령 1번으로 5회 순환한 측정에서 106.2~106.6MB로 늘지 않음. Memory Profiler 스냅샷 Main_0 · Main_3 비교 |
| 2026-11-03 | 1.2 첫 배포 빌드 (소리 없음) | 14.14 (`.data.br` 7.47 + `.wasm.br` 6.67) | - | `https://flamong.lhsdev.info`에 처음 올린 빌드. `BASE_URL`을 배포 주소로 나누고 Diagnostics Overlay를 껐다. 빌드 오류 0, 경고 3은 같은 TMP 안내. 배포 사이트에서 닉네임 확인 · 가입 동작 확인 |
| 2026-11-03 | 1.2 폰트 정적화 빌드 (소리 없음) | **12.96** (`.data.br` 6.29 + `.wasm.br` 6.67) | - | 동적 폰트 폴백(TTF 압축 전 2.8MB)을 정적 `Extra SDF`(430자, 1024², 압축 전 1.0MB)로 바꿔 `.data` −1.18MB. 예산 15MB까지 여유 2.04MB. 코드가 그대로라 `.wasm`은 다시 만들지 않았고 경고 0. 배포 사이트 꽃밭 카드에서 「됬 뷁 똠 햏」 확인. JS 힙 24.2MB(PC Chrome, Title → 안내 → 로그인 → Main → 꽃밭). WASM 힙은 Diagnostics를 꺼서 이번엔 재지 못했다(폰트 원본이 빠져 지난번 140.2MB보다 줄었을 것으로 본다) |
| 2026-11-03 | 1.2.0 버전 표시 빌드 (소리 없음) | 12.98 (`.data.br` 6.29 + `.wasm.br` 6.68) | - | Title 좌상단 버전 표시(`v1.2.0 · 빌드 ID`), 가입 · 정보 수정 400 안내. 빌드 오류 0, 경고 3은 같은 TMP 안내. 배포 사이트 Title에서 버전 글자 확인 |
| 2026-11-03 | 1.2.0 PC 안내 빌드 (소리 없음) | 12.96 (`.data.br` 6.28 + `.wasm.br` 6.68) | - | Login 화면 PC 한글 입력 불가 안내(`Text - PcNotice`). 배포 사이트를 PC 브라우저로 열어 안내 확인 |
| 2026-11-03 | 1.2.0 해시 파일 이름 빌드 (소리 없음) | 12.96 (`.data.br` 6.28 + `.wasm.br` 6.68) | - | iPhone Safari 로딩 중 `TypeError: … GLctx.depthMask` 대응. 빌드 파일 이름이 늘 같고 서버에 캐시 지시가 없어서, Safari가 예전 `framework.js`와 새 `wasm`을 섞어 불러온 것으로 보인다(사이트 데이터를 지우니 해결). `Name Files As Hashes`를 켜 빌드마다 파일 이름이 바뀌게 하고, Caddy에서 `/Build/*`는 1년 캐시(`immutable`), 나머지는 `no-cache`로 했다. 예전 이름은 404. iPhone Safari에서 사이트 데이터를 지우지 않고 새 빌드 ID가 뜨는 것과, 다시 열 때 더 빨리 뜨는 것(캐시)을 확인했다 |
| 2026-11-03 | 1.0.0 버전 입력 창 빌드 (소리 없음) | 12.96 (`.data.br` 6.28 + `.wasm.br` 6.68) | - | Build 버튼 → 버전 입력 창(`BuildVersionPrompt`, 에디터 전용이라 빌드 크기 영향 없음)으로 Version을 1.0.0으로 넣고 빌드. 코드가 그대로라 `.wasm`은 다시 만들지 않았다. 폰에서 Title 버전 글자가 바뀐 것을 확인 |
| 2026-11-03 | 1.0.0 닉네임 보강 빌드 (소리 없음) | 12.96 (`.data.br` 6.28 + `.wasm.br` 6.68) | - | 가입 · 로그인 닉네임 공백 정리와 안내 문구(「닉네임」 · 「인증」). 빌드 오류 0, 경고 3은 같은 TMP 안내. 배포 사이트에서 `index.html`이 가리키는 해시 파일 4개가 모두 200. 폰에서 힌트 · 이어진 공백 안내 · 가입 확인 문구 · 대문자와 뒤 공백 로그인 · 로그인 실패 문구를 모두 확인 |
| 2026-11-03 | 1.0.0 숫자 키패드 빌드 (소리 없음) | 12.96 (`.data.br` 6.29 + `.wasm.br` 6.67) | - | 비밀번호 칸 숫자 키패드(`WebKeyboard.jslib`). `.jslib`가 들어가 `framework.js`가 0.2KB 늘었다. 빌드 오류 0, 경고 3은 같은 TMP 안내. 폰에서 가입 · 로그인 비밀번호 칸에 숫자 키패드가 뜨는 것을 확인 |
| 2026-11-04 | 1.0.0 이달의 게임 빌드 (소리 없음) | 12.98 (`.data.br` 6.29 + `.wasm.br` 6.69) | - | 씬 12개(Game 달력 · Tidy 마음 정리 추가). 빌드 오류 0, 경고 3은 같은 TMP 안내. `.wasm` +0.02MB는 게임 · 달력 · 랭킹 코드, `.data`는 새 씬 둘(압축 전 97 · 93KB)이고 감정 아이콘은 Main 것을 같이 쓴다. 예산 15MB까지 여유 2.02MB. 배포 사이트에서 `index.html`이 가리키는 해시 파일 4개가 모두 200. 에디터에서 봇 없이 Main → Game → Tidy를 돌 때 텍스처는 Main 145.64 · Game 105.78 · Tidy 142.91MB(에디터 텍스처 포함)로 매번 같고, Mono 힙은 82.5~83.4MB. 봇으로 3바퀴 더 돈 측정도 늘지 않음(Memory Profiler 스냅샷 `Flamong_Tidy_Main_0` · `_4`). 폰에서 Title 버전 글자 · 달력 · 한 판(멈춤 · 이어 하기) · 결과 창 · 랭킹 확인. PC 크로미움(Claude 앱 안 브라우저, Chrome 152) JS 힙: Main → 달력 → 마음 정리 → 달력 → Main 10회 순환 전후 24.15 → 26.2~26.9MB(1.1배, 기준 1.5배 이내). WASM 힙은 `unityInstance`가 전역에 없어 재지 못했다 |
| 2026-11-04 | 1.0.0 배경음 빌드 | 13.95 (`.data.br` 7.28 + `.wasm.br` 6.67) | - | 배경음 1곡(「Clockwork Ballerina」 81초, 모노 AAC 약 96kbps, 빌드 안 0.99MB). AAC는 Brotli로 줄지 않아 `.data`가 그대로 +0.99MB. 예산 15MB까지 여유 1.05MB. 빌드 오류 0, 경고 3은 같은 TMP 안내. 에디터에서 Title → Intro 전환에도 끊기지 않고, 로그아웃처럼 Title로 돌아와도 처음으로 돌아가지 않으며 `SoundManager`는 하나임을 확인. 폰에서 Title 첫 터치부터 배경음이 나오는 것을 확인(브라우저 자동 재생 차단). 배포 사이트 해시 파일 4개 확인 |
| 2026-11-05 | 1.0.0 효과음 빌드 | 14.02 (`.data.br` 7.33 + `.wasm.br` 6.69) | - | 효과음 5개(Freesound CC0, 모노 · DecompressOnLoad, 합 약 5초)와 모든 버튼 소리(`ButtonSound`). 배경음 빌드보다 `.data` +0.05 · `.wasm` +0.02MB. 예산 15MB까지 여유 0.98MB. 폰에서 효과음 · 버튼 소리 확인 |
| 2026-11-18 | 1.0.0 플라몽 한마디 빌드 | 14.02 (`.data.br` 7.34 + `.wasm.br` 6.68) | - | Main 말풍선(「플라몽 한마디」)과 말풍선 그림 `frame_bubble` 한 장. 대사 표가 서버에만 있어 효과음 빌드보다 +0.002MB(`.data` +0.004 · `.wasm` −0.003)뿐이다. 예산 15MB까지 여유 0.98MB. 배포 사이트가 내주는 해시 파일 4개가 이 빌드와 같음을 확인. 빌드 폴더의 `deploy.bat`(올리기 · 확인)으로 처음 배포. 폰에서 말풍선 · 캐릭터 누름 · 감정 맞장구 확인 |
