# MCP-серверы — план (не реализовано)

Ничего из этого не работает как MCP-сервер прямо сейчас — см. `Docs/Architecture.md`, фаза 2.
Здесь фиксируется контракт инструментов заранее, чтобы код, который будет их реализовывать,
не выдумывал имена/сигнатуры на ходу и чтобы Design-документы могли уже сейчас ссылаться на них.

## Project MCP (Memory) — первый кандидат на реализацию
Обёртка над `Database/project.sqlite` (см. `schema.sql`).
- `query_knowledge_base(sql_or_filter)` — прочитать сущности, чтобы не дублировать контент.
- `reserve_entity_id(kind)` — получить новый UUID/id для сущности до того, как сгенерирован код на неё.
- `create_customer_archetype(...)`, `create_shop_upgrade(...)`, `create_fine(...)`, `create_dialogue_line(...)`
  — по одному create-тулу на таблицу схемы, не общий «create_entity» (проще валидировать поля).

## Git MCP (Guard) — второй кандидат
Тонкая обёртка над GitPython:
- `git_get_status()`, `git_create_branch(name)`, `git_commit_files(paths, message)`.
- Никаких `git push --force` / `reset --hard` тулов — если нужно что-то деструктивное, это делает человек руками.

## Unity MCP (Actuator) — третий кандидат, самый сложный
- Bridge: `HttpListener` на `localhost:12000`, поднимается изнутри `EditorApplication.update`.
- Тулы: `instantiate_prefab`, `execute_menu_item`, `get_active_scene_hierarchy`, `read_editor_console`,
  `trigger_play_mode`.
- Инвариант: Unity Editor API однопоточный — каждый вызов должен ставиться в очередь и выполняться
  на главном потоке редактора, никогда напрямую из потока Python-сервера.

## Docs MCP (Brain) — по мере роста Docs/Design
- Локальная векторная БД (ChromaDB/Qdrant + SentenceTransformers) поверх `Docs/` и `Design/`.
- Единственный тул: `semantic_search(query)`.
- Не заводить раньше, чем `Design/` реально перестанет помещаться в голову/grep (ориентир — когда там
  будет больше ~10 файлов с содержательным контентом, а не структурными заглушками).
