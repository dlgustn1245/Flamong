-- 데이터 모델 단일 출처. README 「데이터 모델」과 동일해야 한다.
CREATE TABLE IF NOT EXISTS player (
  id            TEXT PRIMARY KEY,
  team          TEXT NOT NULL,
  name          TEXT NOT NULL,
  nickname      TEXT NOT NULL UNIQUE,
  can_post_news INTEGER NOT NULL DEFAULT 0,
  created_at    TEXT NOT NULL,
  hearts        INTEGER NOT NULL,       -- 저장된 하트. 시작값 · 충전량 · 상한은 rules/growth.js
  hearts_date   TEXT NOT NULL,          -- hearts 가 기준으로 삼는 날 (KST YYYY-MM-DD). 충전은 이 날부터 계산
  password_hash TEXT NOT NULL,          -- rules/password.js (scrypt, "salt:hash"). 원문은 저장하지 않는다
  login_fails   INTEGER NOT NULL DEFAULT 0,
  locked_until  TEXT                    -- 로그인 잠금이 풀리는 시각 (ISO). NULL 이면 잠금 없음
);

CREATE TABLE IF NOT EXISTS character (
  id         INTEGER PRIMARY KEY,
  player_id  TEXT NOT NULL REFERENCES player(id),
  species_id INTEGER NOT NULL,
  affection  INTEGER NOT NULL DEFAULT 0,
  started_at TEXT NOT NULL,
  ended_at   TEXT                     -- NULL 인 행이 활성 캐릭터
);
-- 활성 캐릭터는 플레이어당 1개. POST /characters 는 이미 있으면 기존 캐릭터를 돌려주고, 이 제약은 마지막 안전장치
CREATE UNIQUE INDEX IF NOT EXISTS one_active_character ON character(player_id) WHERE ended_at IS NULL;

CREATE TABLE IF NOT EXISTS emotion_log (
  player_id TEXT NOT NULL,
  date      TEXT NOT NULL,            -- KST YYYY-MM-DD
  emotion   TEXT NOT NULL,            -- rules/growth.js EMOTIONS
  UNIQUE(player_id, date)             -- 오늘의 감정은 하루 1회
);

CREATE TABLE IF NOT EXISTS score (
  player_id  TEXT NOT NULL,
  month      TEXT NOT NULL,           -- YYYY-MM
  best_score INTEGER NOT NULL,
  UNIQUE(player_id, month)
);

CREATE TABLE IF NOT EXISTS post (
  id        INTEGER PRIMARY KEY,
  player_id TEXT NOT NULL,
  date      TEXT NOT NULL,
  kind      TEXT NOT NULL,            -- garden | news
  body      TEXT NOT NULL,
  UNIQUE(player_id, date, kind)       -- 하루 1회 규칙은 이 제약으로 푼다
);

CREATE TABLE IF NOT EXISTS potion (
  player_id  TEXT NOT NULL,
  mission_id TEXT NOT NULL,
  claimed_at TEXT NOT NULL,
  UNIQUE(player_id, mission_id)
);

CREATE TABLE IF NOT EXISTS event (
  id        INTEGER PRIMARY KEY,
  player_id TEXT,
  type      TEXT NOT NULL,            -- login login_fail hatch tap emotion game_play game_score potion reset post news delete
  ts        TEXT NOT NULL,
  payload   TEXT
);
