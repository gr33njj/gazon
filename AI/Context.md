# Контекст проекта для агента

**Игра:** «GAZON» — симулятор работы в ПВЗ (пункт выдачи заказов), чёрная комедия, от первого лица.
**Текущий статус:** есть играбельный HTML/Three.js MVP (`gamedev/GAZON PVZ Simulator.dc.html`),
портируется в Unity 6 (C#). Полное описание механик — `Docs/GDD.md`.

**Команда:** два разработчика, оба работают через Claude Code. Финальная платформа — PC/Steam.

**Где что искать:**
- Что делает игра прямо сейчас → `Docs/GDD.md`.
- Куда мы идём инфраструктурно и почему в таком порядке → `Docs/Architecture.md`.
- Кто что решил и почему (история решений) → `Docs/Decisions.md`.
- Правила поведения агента → `AI/Rules.md`.
- Черновики контента/баланса вне кода → `Design/`.
- Схема и содержимое базы игровых сущностей → `Database/schema.sql`, `Database/project.sqlite`.

**Чего пока нет** (не выдумывай, что это уже работает): Unity MCP, Project MCP как живой сервер,
Docs MCP (semantic search), Git MCP, веб-панель на ai.gr33njj.dev, CI/CD. Всё это — roadmap
в `Docs/Architecture.md`, фазы 2–4. Пока агент работает напрямую с файлами и `Database/project.sqlite`
через обычные SQL-запросы (Bash + python3's sqlite3, без сервера).
