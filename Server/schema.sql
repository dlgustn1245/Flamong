-- 데이터 모델 단일 출처. README 「데이터 모델」과 동일해야 한다.
CREATE TABLE IF NOT EXISTS player (
  id            TEXT PRIMARY KEY,
  team          TEXT NOT NULL,
  name          TEXT NOT NULL,
  nickname      TEXT NOT NULL UNIQUE,
  can_post_news INTEGER NOT NULL DEFAULT 0,
  created_at    TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS character (
  id         INTEGER PRIMARY KEY,
  player_id  TEXT NOT NULL REFERENCES player(id),
  species_id INTEGER NOT NULL,
  affection  INTEGER NOT NULL DEFAULT 0,
  started_at TEXT NOT NULL,
  ended_at   TEXT                     -- NULL 인 행이 활성 캐릭터
);

CREATE TABLE IF NOT EXISTS daily_tap (
  player_id TEXT NOT NULL,
  date      TEXT NOT NULL,            -- KST YYYY-MM-DD
  count     INTEGER NOT NULL DEFAULT 0,
  UNIQUE(player_id, date)
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
  type      TEXT NOT NULL,            -- login tap game_play game_score potion reset post news
  ts        TEXT NOT NULL,
  payload   TEXT
);
