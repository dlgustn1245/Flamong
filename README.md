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

| # | 씬 | 하는 일 | 서버 호출 | 버전 |
|---|---|---|---|---|
| 1 | Title | 타이틀. 탭하면 다음 | - | 1.0 |
| 2 | Intro | 페르소나 소개 문구. 「다시 안 보기」는 PlayerPrefs | - | 1.0 |
| 3 | Login | 팀 · 이름 · 닉네임 입력. 닉네임 중복확인 | `POST /players`, `GET /players/check` | 1.0 |
| 4 | EggSelect | 알 4종 중 선택 | - | 1.0 |
| 5 | Hatch | 서버가 뽑은 캐릭터 공개 연출 | `POST /characters` | 1.0 |
| 6 | Main | 캐릭터 · 하트 탭 · 애정도 게이지 · Lv. 상단: 홈 · 꽃(→꽃밭) · 기어(→설정). 하단: 도감 · 묘약 · 게임 | `GET /me`, `POST /tap` | 1.0 |
| 7 | Settings | 정보 수정 · 소리 · 로그아웃 · 캐릭터 새로 키우기 · 문의 | `PATCH /players/me`, `POST /characters/reset` | 1.0 |
| 8 | Collection | 내 알 계열 3종의 진화 5단계. 미해금은 `?` | `GET /collection` | 1.1 |
| 9 | Game | 미니게임 1개 + 이번 달 · 지난달 랭킹 | `POST /scores`, `GET /rankings?month=` | 1.1 |
| 10 | Potion | 히든 미션 달성 시 묘약 획득 | `GET /missions`, `POST /potions/claim` | 1.2 |
| 11 | Garden | 상단 탭 2개. **꽃밭** 탭: 하루 1회 익명 한마디 (대나무숲). **소식** 탭: 공지 · 소식, 닉네임 표시, 작성권 있는 사람만 하루 1회 | `GET/POST /posts?kind=garden\|news` | 1.2 |

씬 1개 = 화면 1개, `LoadSceneMode.Single`. 씬 간 데이터는 `Session`(static POCO) 하나로만 전달.

---

## 규칙 (전부 서버 계산 · 클라이언트는 표시만)

### 성장
| 항목 | 값 |
|---|---|
| 하트 탭 | 1회 = 애정도 +1. **하루 5회** (KST 00:00 리셋) |
| 레벨 | Lv1 0 · Lv2 15 · Lv3 40 · Lv4 80 · **Lv5 150 (완성)** |
| 미니게임 | 하루 첫 플레이 시 애정도 +2 |
| 묘약 | 1회 +20. 미션 1개로 시작 (7일 연속 접속) |
| 진화 이미지 | 캐릭터마다 Lv1~5 = 5장 |

매일 5탭만 하면 Lv2 3일 · Lv3 8일 · Lv4 16일 · Lv5 30일.
수치는 서버 `rules/growth.js` 상수 1곳. 클라이언트에 하드코딩하지 않는다.

### 캐릭터
- 알 이미지 **8종** (`Common/03_Art/Egg/`): 선택 가능 4종(핑크 · 퍼플 · 블루 · 그린) + 부화 연출·도감용 4종(그레이 · 오렌지 · 라벤더 · 크림).
- 캐릭터는 선택 4종 × 3 = **12종**. 알이 색 계열을 정하고, 3종 중 서버가 균등 랜덤. 클라이언트 랜덤 금지(새로고침 리롤 방지).
- 도감은 내 알 계열 3종만. Lv5 완성한 캐릭터만 해금, 나머지 `?`.

### 캐릭터 새로 키우기 (리셋)
- 새 `character` 행 생성, 이전 행은 `ended_at` 기록 후 보관. 유저 삭제 아님.
- 유지: 도감 해금 · 랭킹 · 로그 · 프로필. 초기화: 애정도 · 레벨 · 묘약.
- Lv5 전 리셋은 도감에 남지 않음. 경고 팝업 1회.

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
- type: `login · tap · game_play · game_score · potion · reset · post · news · delete`.
- 탈퇴(`DELETE /players/me`) 시에도 `event`는 지우지 않는다. 탈퇴자 포함 활동 통계가 대시보드 목적이고, `player` 행이 사라지면 이름·팀과 더 이상 연결되지 않는다.

### 인증 (추후)
- 지금은 서버 발급 `player_id`(GUID)를 PlayerPrefs에 저장하고 헤더 `X-Player-Id`로 전송.
- 팀 · 이름은 프로필일 뿐 검증하지 않는다. 슬랙 OAuth 등은 2.0 이후 결정.

---

## 데이터 모델 (`Server/schema.sql`)

```sql
player     (id TEXT PK, team, name, nickname UNIQUE, can_post_news INT DEFAULT 0, created_at)
character  (id INTEGER PK, player_id, species_id INT, affection INT DEFAULT 0,
            started_at, ended_at NULL)              -- ended_at NULL 인 것이 활성
daily_tap  (player_id, date, count INT,            UNIQUE(player_id, date))
score      (player_id, month, best_score INT,      UNIQUE(player_id, month))
post       (id INTEGER PK, player_id, date, kind, body,  UNIQUE(player_id, date, kind))
potion     (player_id, mission_id, claimed_at,     UNIQUE(player_id, mission_id))
event      (id INTEGER PK, player_id, type, ts, payload TEXT)
```

---

## API

모든 요청: `X-Player-Id` 헤더 (players 생성 제외). 규칙 위반은 409, 검증 실패 400.

모든 응답은 `{ "statusCode": N, "body": {...} }` 형식이며 HTTP 상태 코드도 같은 값이다. 아래 표의 「응답」은 `body` 내용.
에러는 `body: { error, message? }`. 500은 메시지를 감추고 서버 로그에만 남긴다.

| 메서드 | 경로 | 요청 | 응답 | 버전 |
|---|---|---|---|---|
| GET | `/players/check?nickname=` | - | `{ available }` | 1.0 |
| POST | `/players` | `{ team, name, nickname }` | `{ playerId }` | 1.0 |
| PATCH | `/players/me` | `{ team?, name? }` | `{ ok }` | 1.0 |
| DELETE | `/players/me` | - | `{ ok }` · 캐릭터·탭·점수·글·묘약 삭제, `event`는 유지 | 1.0 |
| GET | `/me` | - | `{ player, character: { speciesId, affection, level, tapsToday } }` | 1.0 |
| POST | `/characters` | `{ eggId }` | `{ speciesId }` | 1.0 |
| POST | `/characters/reset` | - | `{ ok }` | 1.0 |
| POST | `/tap` | - | `{ affection, level, tapsToday }` · 6번째는 409 | 1.0 |
| GET | `/collection` | - | `{ eggId, species: [{ id, unlocked }] }` | 1.1 |
| POST | `/scores` | `{ score }` | `{ best, rank, affectionBonus }` | 1.1 |
| GET | `/rankings?month=YYYY-MM` | - | `[{ nickname, best }]` 상위 20 | 1.1 |
| GET | `/missions` | - | `[{ id, done, claimed }]` | 1.2 |
| POST | `/potions/claim` | `{ missionId }` | `{ affection, level }` | 1.2 |
| GET | `/posts?kind=&date=` | - | `[{ body, nickname? }]` | 1.2 |
| POST | `/posts` | `{ kind, body }` | `{ ok }` · 중복은 409 | 1.2 |

---

## 폴더 구조

```
Assets/Content/<씬>/
  01_Scenes/<씬>.unity
  02_Scripts/                   씬 컨트롤러 1개. 다른 씬 폴더 참조 금지
  03_Art/                       씬별 텍스처·오디오 (씬별 아틀라스)
Assets/Content/Common/
  02_Scripts/                   Session · ApiClient · DTO · ObjectPool
  03_Art/                       공용 버튼·아이콘·폰트
Assets/Settings/                URP · Input System (Unity 생성)
Server/
  index.js                      Fastify 조립 · now() 주입
  db.js  schema.sql
  routes/<기능>.js              라우트 + JSON 스키마
  rules/<기능>.js               순수 함수 (DB · HTTP 모름)
  test/*.test.js                node:test
```

---

## 로드맵

| 버전 | 내용 |
|---|---|
| **1.0** | 씬 1~7. 서버: players · characters · tap · me. WebGL 빌드 → QR 배포 |
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

---

## 미정 (확인 필요)

- 캐릭터 12종 · 진화 5단계 = 이미지 60장. 디자인 리소스 확보 여부.
- 손글씨 폰트 파일과 라이선스.
- 서버 호스팅 위치 (사내 서버 / Fly.io / Railway).
- 임직원 명단 없이 아무나 가입 가능한 상태를 1.0에서 허용할지.
- 연출용 알 4종(그레이 · 오렌지 · 라벤더 · 크림)에도 캐릭터가 있는지. 있으면 캐릭터 24종.

---

## 빌드 기록

| 날짜 | 버전 | 다운로드 (MB) | 힙 피크 (MB) | 비고 |
|---|---|---|---|---|
| | | | | |
