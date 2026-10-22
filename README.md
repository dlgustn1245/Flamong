# Flamong (플라몽)

사내 감정 캐릭터 키우기 웹 게임. 구성원의 감정을 먹고 자라는 조직의 페르소나.
Unity 6 (6000.6.0f1) · URP 2D · **WebGL** · 세로 1080x1920 · 모바일 브라우저 대상.
1인 개발, 일정 없음. 서버는 Node.js 24(Fastify) + 내장 SQLite(`node:sqlite`)로 직접 구현.

- 기획서 원본: https://seobukcorp.atlassian.net/wiki/spaces/Plinqer/pages/74711134 (로그인 필요)
- 규칙·예산: [CLAUDE.md](CLAUDE.md)
- 이 문서가 씬 · 규칙 · API · 데이터 모델의 **단일 출처**다. 기획서와 다르게 정한 것은 「기획서와 다른 점」에 적는다.

---

## 씬 흐름

```
1 홈 → 2 안내문 → 3 로그인 → 4 알 선택 → 5 캐릭터 랜덤 → 6 홈화면
                                                          ├─ 7 설정
                                                          ├─ 8 도감
                                                          ├─ 9 게임
                                                          ├─ 10 묘약
                                                          └─ 11 꽃밭 (탭: 꽃밭 | 소식)
```

| # | 씬 | 하는 일 | 서버 호출 | 버전 | 현재 상태 |
|---|---|---|---|---|---|
| 1 | Title | 타이틀. 탭하면 Intro, 「다시 안 보기」를 눌렀던 사람은 바로 들어갈 곳으로(아래 「들어갈 곳」) | `GET /me` | 1.0 | 구현. NetworkManager · SessionState · SoundManager 싱글톤이 이 씬에 있음 |
| 2 | Intro | 페르소나 소개 문구. 「다시 안 보기」는 PlayerPrefs `NeverShowIntro`. 다음 → 들어갈 곳으로 | `GET /me` | 1.0 | 구현 |
| 3 | Login | 「가입 \| 로그인」 탭. **가입**: 팀 · 이름 · 숫자 비밀번호 · 닉네임 입력 → 닉네임 중복확인 → 결과 팝업(네/아니요 · 닫기) → 가입 → EggSelect. **로그인**(다른 기기 · 브라우저): 닉네임 + 비밀번호 → `/me`로 갈 곳 결정 | `GET /players/check`, `POST /players`, `POST /players/login` | 1.0 | 구현. 가입 · 로그인 모두 `playerId`를 `SessionState`와 PlayerPrefs에 저장하고 앱 시작 때 불러온다. 로그인 실패는 401 · 429 · 연결 실패를 다른 문구로 안내 |
| 4 | EggSelect | 알 8종 스크롤 목록(화면당 4개, 2페이지) → 확인 팝업 → Hatch | - | 1.0 | 구현. 고른 알은 `SessionState.eggIdx`로 Hatch에 넘긴다 |
| 5 | Hatch | 알 8개가 원형으로 회전 → 멈춘 뒤 서버가 뽑은 캐릭터 공개 | `POST /characters` | 1.0 | 원형 배치 · 회전 · 「시작」 → 서버 호출 · 결과 창(캐릭터 그림) · 오류 창 구현. **룰렛이 멈추는 연출은 아직**(정지 규칙 미정, 지금은 누르면 바로 결과) |
| 6 | Main | 하트 터치(10번 = 하트 1개) · 오늘의 감정 팝업 · 애정도 게이지 · 레벨업 연출. 상단: 홈 · 꽃(→꽃밭) · 기어(→설정). 하단: 도감 · 묘약 · 게임. 동작은 「규칙 › 홈화면」 | `GET /me`, `POST /tap`, `POST /emotions` | 1.0 | UI · 하트 파티클 · `/me` · `/tap` 연결 · `speciesId`로 캐릭터 그림(모든 레벨에 Lv1 그림) · 서버 오류 창 · 하트 아래 상태 문구 · 쓰다 만 터치 수 기기 저장(`HeartsTaps`). 오늘의 감정 · 레벨업/완성 안내 · 「준비 중」 안내는 아직 |
| 7 | 설정 (Main 안의 창) | 정보 수정 · 소리 · 로그아웃 · 캐릭터 새로 키우기 · 문의. 그림이 거의 없고 Main에서만 열어서 씬이 아니라 `Panel - Setting` 창으로 둔다 | `PATCH /players/me`, `POST /characters/reset` | 1.0 | 창 UI · 열기/닫기 · 정보 수정(확인 창 → PATCH) · 로그아웃(확인 창 → `playerId` · `HeartsTaps` 지우고 Title) · 새로 키우기(확인 창 → `/characters/reset` → EggSelect, 완성 전이면 도감에 남지 않는다는 경고 문구) 구현. 소리는 배경음 · 효과음 슬라이더 · `SoundManager`(Title) 연결까지, 소리 파일은 아직. 문의는 아직 |
| 8 | Collection | 알 계열 3종의 진화 5단계. 미해금은 `?`. 어느 계열을 보여 줄지는 「미정」 | `GET /collection` | 1.1 | 빈 씬 |
| 9 | Game | 미니게임 1개 + 이번 달 · 지난달 랭킹 | `POST /scores`, `GET /rankings?month=` | 1.1 | 빈 씬 |
| 10 | Potion | 히든 미션 달성 시 묘약 획득 | `GET /missions`, `POST /potions/claim` | 1.2 | 빈 씬 |
| 11 | Garden | 상단 탭 2개. **꽃밭** 탭: 하루 1회 익명 한마디 (대나무숲). **소식** 탭: 공지 · 소식, 닉네임 표시, 작성권 있는 사람만 하루 1회 | `GET/POST /posts?kind=garden\|news` | 1.2 | 빈 씬 |

**들어갈 곳** (`Common/02_Scripts/Util/EntryRouter.cs`, Title · Intro · Login 로그인 성공 후 모두 이 규칙을 쓴다)

| 상태 | 가는 곳 |
|---|---|
| 기기에 저장된 `playerId` 없음 | Login |
| `/me` 200, `speciesId` -1 (가입만 하고 알을 안 고름) | EggSelect |
| `/me` 200 | Main (팀 · 이름 · 닉네임 · `speciesId`를 `SessionState`에) |
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
| 묘약 | +10 | 7일 연속 방문마다 1개(반복) + 히든 미션(1회성) | 1.2 |

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
- 받은 성장표(5 / 40 / 60 / 80 / 100, 누적 285)와 같은 구조로 수치만 줄였다. 원안대로면 지금 하루 공급량으로 매일 해도 41일이 걸린다.

**완성까지 걸리는 기간** (시뮬레이션 값, 그 단계에 도달하는 날)

| 플레이 방식 | Lv2 | Lv3 | Lv4 | Lv5 | 완성 |
|---|---|---|---|---|---|
| 매일 · 하트만 | 1일 | 6일 | 13일 | 23일 | 35일 |
| 매일 · 하트 + 감정 (1.0 기본) | 1일 | 5일 | 10일 | 17일 | **25일** |
| 매일 · + 7일 연속 묘약 | 1일 | 5일 | 8일 | 14일 | 21일 |
| 매일 · + 묘약 + 미니게임 (1.1) | 1일 | 4일 | 7일 | 12일 | 18일 |
| 주 3회(월·수·금) · 하트 + 감정 | 1일 | 5일 | 12일 | 22일 | **31일** |
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
- 진화 이미지는 캐릭터마다 Lv1~5, 5장.

### 홈화면 (Main)

**오늘의 감정**
- 그날 처음 Main에 들어오면(`/me`의 `emotionToday`가 false) 팝업으로 묻는다. 보기는 기획서의 「감정연료」 예시 5개다. 괄호는 API 값.
  피로(`tired`) · 스트레스(`stress`) · 성취감(`achievement`) · 몰입(`focus`) · 평온(`calm`)
- 고르면 애정도 +2. 닫으면 그날은 다시 묻지 않는다.
- 개인 응답은 본인 외에는 보지 않는다. 2.0 대시보드는 **5명 이상 모인 묶음만** 익명 집계로 보여준다. 작은 팀에서 개인이 드러나지 않게 하기 위해서다.

**하트 버튼**

| 상황 | 화면 |
|---|---|
| 매 터치 | 버튼이 살짝 눌렸다 돌아오고 작은 하트 1개가 떠오름. 하트 안쪽이 10분의 1씩 차오름 |
| 10번째 터치 | 하트 4~6개가 터지듯 떠오르고 「+1」. 게이지가 늘고 남은 하트 수가 줄어듦 |
| 레벨업 | 캐릭터 이미지가 다음 단계로 바뀌고 「Lv 3이 되었어요!」 팝업 |
| 남은 하트 0 | 버튼이 회색. 「내일 0시에 하트 5개가 채워져요」 |
| 완성 (Lv5 게이지 가득) | 「다 자랐어요! 도감에 등록됐어요」 팝업과 「새로 키우기」 안내 |
| 전송 실패 | 하트를 줄이지 않고 찬 상태로 둠. 다음 터치 때 다시 보냄 |

- 「횟수: N」은 **지금 쓸 수 있는 하트 수**다(최대 15).
- 레벨은 게이지 옆 「Lv」 하나로만 보여준다. 시안의 왼쪽 위 「Lv」는 중복이라 뺐다.
- 게이지는 **현재 레벨 안에서의 진행률** `levelProgress ÷ levelNeed`다. 레벨이 오르면 0에서 다시 시작한다. 눈금 3개는 25 · 50 · 75% 장식이다.
- 도감 · 묘약 · 게임 · 꽃밭 버튼은 해당 버전 전까지 「준비 중이에요」 안내만 띄운다.
- 하트 파티클은 Main의 **20개 풀**을 돌려 쓴다. 터치마다 1개, 하트 전달 때 5개, 레벨업 때 10개를 더 띄운다. 초당 4번 연타 중 레벨업해도 떠 있던 4개 + 16개 = 20개로 딱 맞는다. 더 빠르게 누르면 가장 오래된(거의 사라진) 하트부터 다시 쓴다.

### 캐릭터
- 알 이미지 **8종** (`Common/03_Resources/Egg/`). EggSelect에서 **8종 모두 고를 수 있게** 배치했다.
- `eggId`는 EggSelect 목록 순서다. 순서를 바꾸면 서버 값도 바뀐다.

  | eggId | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
  |---|---|---|---|---|---|---|---|---|
  | 알 | 핑크 | 퍼플 | 블루 | 그린 | 그레이 | 오렌지 | 라벤더 | 크림 |

- Hatch의 원형 배치는 시안 순서(12시부터 시계 방향: 핑크 · 그레이 · 오렌지 · 라벤더 · 크림 · 그린 · 퍼플 · 블루)라 `eggId` 순서와 다르다.
- 알이 색 계열을 정하고, 그 안에서 서버가 균등 랜덤으로 캐릭터를 뽑는다. 클라이언트 랜덤 금지(새로고침 리롤 방지).
- `speciesId = eggId × 3 + (0~2)`. 알당 3종은 임시값이고 `rules/species.js`의 숫자 하나로 바뀐다(「미정」 캐릭터 수).
- 처음에는 선택 가능한 알 4종 × 3 = 12종으로 정했다. 8종 모두 고를 수 있게 되면서 캐릭터 수는 다시 정해야 한다(「미정」 참고).
- 도감 범위는 미정이다(「미정」 참고). 리셋 때 다른 알을 고를 수 있어서 「내 알 계열」이 하나로 정해지지 않는다. 완성(Lv5 게이지 가득)한 캐릭터만 해금, 나머지 `?`.

### 닉네임
- 1~12자. 허용 글자는 **KS X 1001 완성형 한글 2350자 + ASCII**다. 꾸불림체 폰트 아틀라스와 같은 범위라서, 아틀라스 밖 글자가 □로 보이는 일을 막는다.
- 서버 `rules/nickname.js`가 검사한다. 범위 밖 글자는 400 `nickname_charset`, 중복은 409 `nickname_taken`.
- 자모 단독(ㅋㅋ), 「뷁」 같은 완성형 밖 글자, 이모지, 전각 문자는 쓸 수 없다. 자모는 입력 중 표시용으로 폰트 폴백에 있지만 닉네임에는 허용하지 않는다. Login 입력칸도 12자로 제한했다.

### 캐릭터 새로 키우기 (리셋)
- 지금 캐릭터 행에 `ended_at`을 기록해 보관한다(지우지 않음, 유저 삭제 아님). 새 `character` 행은 알을 다시 골라 `POST /characters`에서 만든다.
- 리셋 뒤에는 EggSelect로 가서 **다른 알도 고를 수 있다**. 그 사이 `/me`의 `speciesId`는 -1이라, 앱을 껐다 켜도 EggSelect로 간다.
- 클라이언트는 `eggIdx`를 따로 지우지 않는다. EggSelect에서 알을 고를 때 새 값으로 덮어쓴다. 쓰다 만 터치 수(`HeartsTaps`)도 하트처럼 이어진다.
- 응답을 못 받고 다시 보내도 `{ ok }`(이미 끝났으면 그대로). `event`의 `reset`에는 끝낸 캐릭터의 `speciesId` · 애정도 · 완성 여부를 남긴다.
- 유지: 도감 해금 · 랭킹 · 로그 · 프로필 · **남은 하트**. 초기화: 애정도 · 레벨.
- 완성 전 리셋은 도감에 남지 않음. 경고 팝업 1회.

### 미니게임 · 랭킹
- 게임은 **1개**. 매달 새 게임을 만들지 않는다. 「이달의 게임」 12칸 중 이번 달만 활성.
- `score(player_id, month, best_score)`. 랭킹 = 해당 월 정렬.
- 「1일 정각 공개」 = 지난달 month로 조회. 스케줄러 없음.

### 꽃밭 · 소식
- `UNIQUE(player_id, date, kind)` 제약으로 하루 1회.
- 소식 작성권: `player.can_post_news`. 플링커 멤버 true. 「랜덤 1명」은 매일 첫 조회 시 그날 날짜를 시드로 뽑는다(저장 불필요).
- 꽃밭은 익명 표시, 소식은 닉네임 표시. 같은 Garden 씬의 탭 2개. 서버에는 둘 다 작성자 저장(신고 대응).

### 로그
- 모든 쓰기 API가 `event(player_id, type, ts, payload)` 한 줄. 클라이언트 별도 로그 전송 없음.
- type: `login · login_fail · hatch · tap · emotion · game_play · game_score · potion · reset · post · news · delete`.
- 탈퇴(`DELETE /players/me`) 시에도 `event`는 지우지 않는다. 탈퇴자 포함 활동 통계가 대시보드 목적이고, `player` 행이 사라지면 이름·팀과 더 이상 연결되지 않는다.

### 인증
- 평소에는 서버 발급 `player_id`(GUID)를 PlayerPrefs에 저장하고 헤더 `X-Player-Id`로 전송한다.
- 다른 기기 · 브라우저에서는 **닉네임 + 숫자 비밀번호(4~6자리)**로 로그인해서 `player_id`를 다시 받는다(`POST /players/login`).
  - 비밀번호는 `node:crypto` scrypt 해시로만 저장한다(`rules/password.js`). 원문은 어디에도 남지 않는다.
  - 계정마다 **5번 틀리면 10분 잠금**(429 `locked`). 4자리는 1만 가지뿐이라 마구 넣어 보기를 막는다. 잠그면서 횟수는 0으로 돌린다.
  - 없는 닉네임도 틀린 비밀번호와 같은 401 `login_failed`를 준다.
  - 잊어버리면 되찾는 방법은 아직 없다(관리자가 DB에서 처리). 잠금은 남이 일부러 틀려서 내 계정을 10분 막을 수도 있다는 한계가 있다.
- 팀 · 이름은 프로필일 뿐 검증하지 않는다. 슬랙 OAuth 등은 2.0 이후 결정.

---

## 데이터 모델 (`Server/schema.sql`)

`visit`만 1.2 목표이고 나머지는 `schema.sql`에 있다.

```sql
player      (id TEXT PK, team, name, nickname UNIQUE, can_post_news INT DEFAULT 0, created_at,
             hearts INT, hearts_date TEXT,           -- 저장된 하트와 그 기준일(KST). 가입 때 5개 · 가입일
             password_hash TEXT, login_fails INT DEFAULT 0, locked_until TEXT NULL)   -- 로그인 (「인증」)
character   (id INTEGER PK, player_id, species_id INT, affection INT DEFAULT 0,
             started_at, ended_at NULL)              -- ended_at NULL 인 것이 활성
             UNIQUE INDEX one_active_character (player_id) WHERE ended_at IS NULL  -- 활성은 1개
emotion_log (player_id, date, emotion,              UNIQUE(player_id, date))   -- 오늘의 감정, 하루 1회
visit       (player_id, date,                       UNIQUE(player_id, date))   -- 1.2: 연속 방문 묘약용 (아직 없음)
score       (player_id, month, best_score INT,      UNIQUE(player_id, month))
post       (id INTEGER PK, player_id, date, kind, body,  UNIQUE(player_id, date, kind))
potion     (player_id, mission_id, claimed_at,     UNIQUE(player_id, mission_id))
event      (id INTEGER PK, player_id, type, ts, payload TEXT)
```

- 하트 충전은 스케줄러 없이 `min(15, hearts + 5 × (오늘 − hearts_date))`로 계산한다(`rules/growth.js` `rechargeHearts`). 언제 계산해도 같은 값이라 `GET /me`는 계산만 하고 저장하지 않는다. 하트를 쓰는 `POST /tap`이 (계산값 − 1, 오늘)을 함께 저장한다.
- 스키마를 바꾼 뒤에는 기존 `Server/flamong.db*`를 지우고 서버를 다시 켠다. `CREATE TABLE IF NOT EXISTS`는 이미 있는 테이블에 열을 더하지 않는다. 마이그레이션 도구는 두지 않는다(실사용자 생기기 전까지).

---

## API

모든 요청: `X-Player-Id` 헤더 (players 생성 · check 제외). 헤더가 없으면 401 `no_player`, 모르는 id면 401 `unknown_player`. **404 `not_found`는 없는 주소에만** 쓴다. 클라이언트는 401을 「계정 없음」으로 보고 기기의 `playerId`를 지우므로, 주소 실수(404)와 섞이지 않게 나눴다. 규칙 위반은 409, 검증 실패 400.
본문이 없는 POST(`/tap` 등)도 `{}`를 보낸다. `Content-Type: application/json`에 빈 본문이면 Fastify가 400 `FST_ERR_CTP_EMPTY_JSON_BODY`로 거절한다.

모든 응답은 `{ "statusCode": N, "body": {...} }` 형식이며 HTTP 상태 코드도 같은 값이다. 아래 표의 「응답」은 `body` 내용.
에러는 `body: { error, message? }`. 500은 메시지를 감추고 서버 로그에만 남긴다.

**1.0 서버 API 10개가 모두 구현되어 있다** (`routes/players.js` · `routes/character.js`, `npm test` 16개 통과). 1.1 이후는 설계만 있다.
수동 테스트용 요청은 `Server/requests.http`(VS Code REST Client)에 있다. 이 파일은 gitignore 대상이라 로컬에만 있다.

| 메서드 | 경로 | 요청 | 응답 | 버전 |
|---|---|---|---|---|
| GET | `/players/check?nickname=` | - | `{ available }` · 중복이거나 허용 글자 밖이면 false **(구현)** | 1.0 |
| POST | `/players` | `{ team, name, nickname, password }` | `{ playerId }` · 중복 409 · 글자 400 · 비밀번호가 숫자 4~6자리가 아니면 400 **(구현)** | 1.0 |
| POST | `/players/login` | `{ nickname, password }` | `{ playerId }` · 틀리거나 없는 닉네임 401 `login_failed` · 5번 틀리면 10분 동안 429 `locked` **(구현)** | 1.0 |
| PATCH | `/players/me` | `{ team?, name? }` | `{ ok }` **(구현)** | 1.0 |
| DELETE | `/players/me` | - | `{ ok }` · 캐릭터·탭·점수·글·묘약 삭제, `event`는 유지 **(구현)** | 1.0 |
| GET | `/me` | - | `{ team, name, nickname, speciesId, emotionToday, growth }` · 알을 아직 안 골랐으면 `speciesId` -1. 가입 여부 확인(401이면 서버에 없는 계정)과 설정 창의 현재 정보에도 쓴다 **(구현)** | 1.0 |
| POST | `/characters` | `{ eggId }` (0~7) | `{ speciesId }` · 활성 캐릭터가 이미 있으면 새로 뽑지 않고 그 `speciesId`를 200으로 돌려준다(시간 초과 후 재시도해도 같은 결과) **(구현)** | 1.0 |
| POST | `/characters/reset` | `{}` | `{ ok }` · 지금 캐릭터를 끝내 보관(`ended_at`). 남은 하트는 그대로. 캐릭터가 없으면(재시도) 그대로 ok **(구현)** | 1.0 |
| POST | `/tap` | `{}` | `{ levelUp, growth }` · 하트 1개 사용. 캐릭터 없음 409 `no_character`, 완성 409 `completed`, 하트 0 409 `no_hearts` **(구현)** | 1.0 |
| POST | `/emotions` | `{ emotion }` | `{ levelUp, growth }` · 하루 2번째는 409 `emotion_done`, 캐릭터 없음 409 `no_character` **(구현)** | 1.0 |
| GET | `/collection` | - | `{ eggId, species: [{ id, unlocked }] }` · 모양은 도감 범위(「미정」)에 따라 다시 정한다 | 1.1 |
| POST | `/scores` | `{ score }` | `{ best, rank, levelUp, growth }` · 하루 첫 플레이면 애정도 +2 반영 | 1.1 |
| GET | `/rankings?month=YYYY-MM` | - | `[{ nickname, best }]` 상위 20 | 1.1 |
| GET | `/missions` | - | `[{ id, done, claimed }]` | 1.2 |
| POST | `/potions/claim` | `{ missionId }` | `{ levelUp, growth }` | 1.2 |
| GET | `/posts?kind=&date=` | - | `[{ body, nickname? }]` | 1.2 |
| POST | `/posts` | `{ kind, body }` | `{ ok }` · 중복은 409 | 1.2 |

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
{ "statusCode": 200,
  "body": { "levelUp": false,
            "growth": { "hearts": 4, "heartsMax": 15, "affection": 1,
                        "level": 1, "levelProgress": 1, "levelNeed": 5,
                        "completed": false } } }
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
  03_Resources/                 Font · Egg · Heart · Icon · UI · Background · Character
  04_Prefabs/                   두 씬 이상에서 쓰는 UI. Panel - Error(서버 오류 안내 창, ErrorPanel.Show(다시 시도할 일))
                                · Panel - Confirm(네/아니요 확인 창, ConfirmPanel.Show(문구, [네]를 누르면 할 일))
                                · Panel - Notice(문구와 [확인]만 있는 안내 창, NoticePanel.Show(문구))
Assets/Settings/                URP · Input System (Unity 생성)
Server/
  index.js                      Fastify 조립 · 응답 { statusCode, body } 감싸기 · now() 주입
  db.js  schema.sql             node:sqlite · logEvent · tx(트랜잭션)
  routes/players.js             가입 · 닉네임 확인 · 수정 · 탈퇴 (라우트 + JSON 스키마)
  routes/character.js           /me · /characters · /tap · /emotions
  rules/nickname.js             순수 함수 (DB · HTTP 모름)
  rules/password.js             비밀번호 해시 · 확인 · 로그인 잠금
  rules/growth.js               성장 수치 단일 출처 · 레벨 · 하트 충전 · KST 날짜
  rules/species.js              알 → 캐릭터 뽑기
  test/*.test.js                node:test
  requests.http                 수동 테스트 (gitignore, 로컬 전용)
```

공용 오브젝트 풀은 아직 없다(Main 하트는 `HeartParticles`가 자체 풀을 쓴다). 두 씬 이상에서 필요해질 때 `Common/02_Scripts`에 만든다.

### UI 캔버스 구성
| 캔버스 | 그리기 순서 | 담는 것 |
|---|---|---|
| Canvas - Main | 0 | 배경 · 제목 · 고정 버튼 |
| Canvas - Dynamic | 1 | 스크롤 목록, 회전하는 알처럼 매 프레임 움직이는 것만 |
| 팝업 (Main 아래 하위 Canvas) | 2 (Override Sorting) | 씬마다의 창(설정 · 결과 등). 기본 꺼짐. GraphicRaycaster 필수 |
| 확인 창 (공통 프리팹 `Panel - Confirm`) | 3 | 팝업 위에서 「정말 할까요?」를 묻는다(Main 설정 창) |
| 안내 창 (공통 프리팹 `Panel - Notice`) | 4 | 「준비 중이에요」 · 레벨업 · 완성처럼 알리기만 한다(Main). 하트 응답이 늦게 와서 확인 창이 떠 있는 동안 레벨업 안내가 뜰 수 있어 확인 창과 숫자를 나눴다 |
| 오류 창 (공통 프리팹 `Panel - Error`) | 5 | 무엇이 떠 있든 맨 위. Title · Intro · Hatch · Main |

같은 순서끼리는 하이어라키에서 뒤에 있어도 위에 그려진다는 보장이 없다(설정 창 뒤에 확인 창이 숨은 적이 있음). 겹쳐 뜰 수 있는 창은 순서 숫자로 나눈다.

움직이는 UI를 따로 두면 그것이 움직여도 배경과 버튼까지 다시 그려지지 않는다. 누를 것이 없는 캔버스에는 GraphicRaycaster를 붙이지 않는다.

---

## 아트 리소스

- **지금 이미지는 대부분 시안에서 잘라낸 저해상도 플레이스홀더다.** 원본이 약 100px 안팎이라 확대하면 흐리다. 디자이너 원본이 오면 같은 파일명으로 덮어쓴다.
- 물결 밑줄(`line_wavy`), 노란 라벨 글로우(`label_glow_yellow`), 흰 박스 버튼(`btn_box_white`), 노란 타원 버튼(`btn_oval_yellow`), 크림색 라벨(`label_glow`)은 시안을 보고 새로 그렸다.
- 9-slice로 쓰는 것: `frame_card`, `line_wavy`, `label_glow_yellow`, `btn_box_white`.
- **캐릭터**: `Character/char_{speciesId 두 자리}_lv{레벨}.png` (예: `char_07_lv1`). 지금은 Lv1 24장뿐이다. 시안의 `char_mint_lv2`를 알 색으로 다시 칠한 플레이스홀더이고, 같은 알 안의 3종은 몸통 톤(기본 · 밝게 · 진하게)과 지느러미 색(연두 · 하늘 · 분홍)으로 구분한다. 256², 단일 스프라이트.
- **게이지**는 이미지 두 장으로 세 겹을 만든다.
  - `gauge_fill`은 흰색 모양이다. Image Color로 칠해 빈 바탕(`#D7DBDC`)과 채움(`#FFFEB1`, Filled · Horizontal)에 한 번씩 쓴다.
  - 맨 위에 `gauge_frame`(파란 테두리 + 눈금)을 올린다.
- 공통 색: 글자·외곽선 파랑 `#2F60F6`, 노란 버튼 `#EEFD88`, 스크롤 막대 `#F6EF8C`.
- **폰트**: 배달의민족 꾸불림체(`BMKkubulimTTF SDF`). 2048² 정적 아틀라스이고, 문자 목록은 `Font/CharSet_KSX1001.txt`(ASCII 95 + 완성형 2350 + 기호 24)다.
  - 폴백 `BMKkubulimTTF Jamo SDF`: 단독 자모 ㄱ~ㅣ 51자, 512×256 정적 아틀라스. 입력 중인 「ㄱ」 같은 글자가 □로 보이지 않게 한다. 본 아틀라스와 같은 35pt · 여백 5로 구워 글자 크기가 같다.

---

## 로드맵

| 버전 | 내용 |
|---|---|
| **1.0** | 씬 1~7. 서버: players · characters · me · tap(하트 저장) · emotions(오늘의 감정). WebGL 빌드 → QR 배포 |
| 1.1 | 도감 · 미니게임 1개 · 월별 랭킹 |
| 1.2 | 묘약(미션 1개) · 꽃밭(꽃밭 · 소식 탭) |
| 2.0 | Android 빌드 + **Unity Ads**(WebGL 미지원이라 필요) · 대시보드(event 테이블 쿼리) · 인증 |

---

## 기획서와 다른 점

| 기획서 | 여기 | 이유 |
|---|---|---|
| 1.0에 12씬 + 게임 + 랭킹 + 꽃밭 전부 | 1.0은 씬 1~7 | 1인 개발. 성장 루프부터 |
| 매달 새 미니게임 | 게임 1개, 월별 랭킹 리셋만 | 지속 제작 비용 |
| 탭 5회마다 로그 수집 | 서버가 모든 쓰기를 event로 기록 | 하루 5탭이면 배치 의미 없음 |
| 임직원 리스트 대조 로그인 | 프로필 입력만, 인증은 추후 | 이름만으로는 인증이 안 됨. 슬랙 OAuth를 2.0에 검토 |
| 광고 연계(2.0) | Android 빌드 + Unity Ads | Unity Ads가 WebGL 미지원 |
| 묘약 · 꽃밭 · 소식 시안 없음 | 1.2로 미룸 | 시안 나오면 진행 |
| Main 하단 메뉴 「기록」 | 「묘약」 | 「기록」 정의가 없음. 상단 꽃 아이콘이 꽃밭 진입점 |
| 소식 씬 별도 | 꽃밭 씬의 탭 | 데이터·화면이 같고 Main에 진입점이 없음. 필요하면 나중에 분리 |
| (없음) | 오늘의 감정 체크인 추가 | 안내문의 「하루에 한 번, 고른 감정 하나가」와 「감정연료」 컨셉을 규칙으로 옮김. 2.0 대시보드의 핵심 데이터 |
| 하루 고정 횟수 | 하트 하루 5개 충전, 최대 15개 저장 | 못 온 날 손해를 줄여 주 3회 방문자도 약 한 달에 완성 |
| 성장표 5 / 40 / 60 / 80 / 100 (누적 285) | 같은 구조, 5 / 25 / 35 / 50 / 60 (누적 175) | 원안은 매일 해도 41일. 한 달 주기인 미니게임 · 랭킹에 맞춤 |

---

## 미정 (확인 필요)

- **캐릭터 수.** 알 8종을 모두 고를 수 있게 되었으므로 8종 × 3 = 24종인지, 다른 구성인지. 진화 5단계면 이미지는 그 5배다.
- **성장표 작성자 확인.** 받은 표는 2~5단계 누적 범위가 1점씩 어긋나 있어(예: 2단계 6~46은 41칸인데 표시 점수는 1~40) 표시 점수 쪽을 맞다고 보았다. 줄인 수치(5 / 25 / 35 / 50 / 60)도 함께 확인이 필요하다.
- 디자이너 원본 이미지 확보 여부. 지금은 시안에서 잘라낸 플레이스홀더다.
- 시안 12장 보관 위치. 세션 임시 폴더에서 지워졌으니 `Docs/Mockups/` 같은 곳에 두는 것을 권한다.
- 꾸불림체 라이선스 원문 확인과 보관.
- **한글 동적 폰트 폴백.** 아틀라스 밖 한글 8,822자(「슽」, 입력 중의 「잏」 등)가 □로 보인다. TTF(2.9MB)를 동적 폴백으로 넣을지 첫 빌드 크기를 보고 정한다. 넣으면 닉네임 허용 범위를 넓힐지도 함께 정한다.
- **도감 범위.** 리셋 때 다른 알도 고를 수 있으므로, 지금 키우는 알 계열 3종만 보여 줄지 완성한 적 있는 모든 계열을 보여 줄지. `/collection` 응답 모양(`eggId` 하나)도 이에 따라 바뀐다. 1.1 전에 정한다.
- **Hatch 룰렛 정지 규칙.** 포인터 위에 멈춘 알과 서버가 뽑은 결과를 어떻게 맞출지.
- 오늘의 감정 팝업 디자인과 감정 아이콘 5종 이미지. 시안에 없다.
- Main 상단 홈 아이콘의 동작. 이미 홈이라 의미가 겹친다(숨김 또는 새로고침 제안).
- 서버 호스팅 위치. AWS Lightsail $5 플랜(신규 계정 6개월 크레딧)을 검토했다. 미확정.
- 임직원 명단 없이 아무나 가입 가능한 상태를 1.0에서 허용할지.
- 앱 시작 시 서버 헬스체크(`GET /health`) 추가 여부. WebGL은 앱을 종료할 수 없어서, 실패하면 「다시 시도」 화면을 띄우는 방식을 검토했다.

---

## 빌드 기록

| 날짜 | 버전 | 다운로드 (MB) | 힙 피크 (MB) | 비고 |
|---|---|---|---|---|
| | | | | |
