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

## Unity MCP (Actuator) — не пишем свою, ставим готовую (обновлено 2026-07-16)
Не строим `HttpListener`-мост с нуля — есть готовое: **MCP for Unity**
(github.com/CoplayDev/unity-mcp), MIT, Unity 2021.3 LTS–6.x. Установка: Package Manager →
Add from git URL → `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`,
дальше `Window → MCP for Unity → Configure All Detected Clients` — настроит клиент (Claude Code
и т.д.) автоматически. Даёт ~47 тулов: сцена/GameObject, редактирование C# (через Roslyn), ассеты,
тесты, профайлинг, билды.

**Ограничение, а не деталь реализации**: агент должен быть запущен на той же машине, что и открытый
Unity Editor (мост слушает localhost). Сессия на VPS до чужого localhost достучаться не может —
значит, кто-то ставит Claude Code локально рядом с Unity и настраивает мост там же.

## Docs MCP (Brain) — по мере роста Docs/Design
- Локальная векторная БД (ChromaDB/Qdrant + SentenceTransformers) поверх `Docs/` и `Design/`.
- Единственный тул: `semantic_search(query)`.
- Не заводить раньше, чем `Design/` реально перестанет помещаться в голову/grep (ориентир — когда там
  будет больше ~10 файлов с содержательным контентом, а не структурными заглушками).
