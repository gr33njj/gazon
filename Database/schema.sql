-- project.sqlite schema — "GAZON" (см. Docs/GDD.md для контекста механик)
-- Источник истины по игровым сущностям контента. Любой новый ряд контента (реплика,
-- архетип клиента, апгрейд, штраф, мем) заводится здесь ДО того, как на него ссылается C#/ScriptableObject.
-- См. AI/Rules.md пункт 2 ("SQLite прежде всего").

PRAGMA foreign_keys = ON;

-- Общий реестр сущностей — для reserve_entity_id() из будущего Project MCP (AI/MCP.md).
-- uuid — то, на что ссылается код/ScriptableObject; kind — имя таблицы ниже.
CREATE TABLE entities (
  uuid        TEXT PRIMARY KEY,
  kind        TEXT NOT NULL,
  created_at  TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE customer_archetypes (
  uuid          TEXT PRIMARY KEY REFERENCES entities(uuid),
  name          TEXT NOT NULL UNIQUE,       -- 'normal' | 'babka' | 'shopaholic' | ...
  spawn_weight  REAL NOT NULL,              -- относительный вес при спавне
  base_patience REAL NOT NULL,
  notes         TEXT
);

CREATE TABLE dialogue_lines (
  uuid          TEXT PRIMARY KEY REFERENCES entities(uuid),
  category      TEXT NOT NULL,              -- 'spawn' | 'nocode' | 'angry' | 'fitting' | 'happy' | 'babka_spawn'
  archetype     TEXT REFERENCES customer_archetypes(name),  -- NULL = общая реплика
  text          TEXT NOT NULL
);

CREATE TABLE shop_upgrades (
  uuid          TEXT PRIMARY KEY REFERENCES entities(uuid),
  key           TEXT NOT NULL UNIQUE,       -- 'mat' | 'mic' | 'troika' | ...
  name          TEXT NOT NULL,
  description   TEXT NOT NULL,
  price         INTEGER NOT NULL,
  effect_notes  TEXT
);

CREATE TABLE fines (
  uuid          TEXT PRIMARY KEY REFERENCES entities(uuid),
  reason        TEXT NOT NULL,
  amount        INTEGER NOT NULL,
  is_absurd     INTEGER NOT NULL DEFAULT 1  -- 0 = штраф за реальную ошибку игрока (напр. помятая коробка)
);

CREATE TABLE memes (
  uuid          TEXT PRIMARY KEY REFERENCES entities(uuid),
  emoji         TEXT NOT NULL,
  text          TEXT NOT NULL
);

CREATE TABLE babka_items (
  uuid          TEXT PRIMARY KEY REFERENCES entities(uuid),
  name          TEXT NOT NULL,
  hint          TEXT NOT NULL               -- реплика-подсказка в мини-игре «Крокодил»
);

CREATE TABLE ranks (
  uuid            TEXT PRIMARY KEY REFERENCES entities(uuid),
  title           TEXT NOT NULL,
  min_day_earned  INTEGER NOT NULL,         -- нижняя граница dayEarned для этого звания
  is_purple_variant INTEGER NOT NULL DEFAULT 0
);
