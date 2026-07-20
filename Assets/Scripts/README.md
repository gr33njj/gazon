# Assets/Scripts — полный порт MVP (2026-07-20)

Портирует `gamedev/pvz_simulator_mvp.html` (см. `Docs/GDD.md`) целиком: докстанция → стеллажи →
окно выдачи → таймер смены, плюс архетипы клиентов, телефон/донат-магазин, три мини-игры
(крокодил/«Глаз Бога»/QTE), кража, штрафы (штучные и периодические), курьер-конфронтация, перекур,
ночная инвентаризация, увольнение/ребрендинг VILBERIS, ирл-концовка. Сознательные упрощения см. в
шапке `Assets/Editor/SceneBuilder.cs` и в `Docs/Decisions.md` (запись от 2026-07-20).

Контент (реплики, штрафы, мемы, товары бабушки, звания, апгрейды, архетипы) не хардкожен в C# —
он живёт в `Database/project.sqlite` (см. `AI/Rules.md`, «SQLite прежде всего») и экспортируется в
`Assets/Resources/GameContent.json` скриптом `Tools/ai_suite/export_content.py`. После любого
изменения БД нужно перезапустить этот скрипт, иначе `ContentDatabase.cs` продолжит читать старый JSON.

Эти файлы написаны без Unity Editor (на VPS его нет — см. `Docs/Architecture.md`) и не проверены
интерактивно человеком в реальном Unity — проверка компиляции идёт через CI (`webgl-build.yml`),
но фактическую играбельность/баланс должен проверить человек в Editor или через WebGL-превью.

## Структура

- `Core/` — `GameManager` (состояние/экономика/статистика/звания/VILBERIS), `ContentDatabase`
  (загрузчик GameContent.json), `RoomLayout` (координаты комнаты — общий источник для SceneBuilder
  и рантайм-скриптов), `InputLock` (флаги открытых модальных панелей), `MinigameController`
  (крокодил/«Глаз Бога»/QTE), `NightInventoryController` (ночная инвентаризация).
- `World/` — `Box`, `ShelfCell`, `DockSpawner`, `WindowStation`, `Courier`+`CourierSpawner`,
  `SmokeDoor`+`SmokeBreakController`, `ReturnsTable`+`ReturnSpawner`, `FittingRoom`.
- `Customers/` — `Customer` (архетипы Normal/Babka/Shopaholic, очередь, примерочная, терпение),
  `CustomerSpawner` (взвешенный ролл архетипа по весам из БД), `CustomerArchetype`, `CustomerState`.
- `Player/` — `PlayerController` (WASD/мышь/бег), `PlayerInteraction` (raycast+E, удержание E для
  стола возвратов, кража по X), `PlayerBuffs` (дофамин/спидбуст/улыбка/кулдаун перекура).
- `Interaction/` — `IInteractable`, `IHoldInteractable`.
- `UI/GameUI.cs` — весь интерфейс на OnGUI (в проекте нет TextMeshPro/uGUI): HUD, телефон/магазин,
  панели мини-игр, экраны меню/паузы/итогов дня/увольнения/ирл-концовки, роутинг клавиш верхнего
  уровня (Q/R/X/Esc/1-3/f-g-h-j-k-l) — аналог единого `keydown`-обработчика из MVP.

## Как собрать сцену

`Assets/Editor/SceneBuilder.cs` строит всю комнату (стены, стойка, 2 окна, 24 ячейки на 3 стеллажах,
докстанция, примерочная, курилка, стол возвратов, вход/выход) кодом через `UnityEditor` API:

1. **File → New Scene** (обязательно пустая сцена — иначе ошибка «GameManager уже есть»).
2. **Gazon → Собрать сцену вертикального среза**.
3. Сохраните сцену, нажмите **Play**.

CI (`webgl-build.yml` → `BuildScript.BuildWebGLCI`) делает это автоматически перед каждой сборкой —
`SceneBuilder.cs` является источником истины для того, что реально попадает в WebGL-превью; ручные
правки `.unity`-файла, не перенесённые в код `SceneBuilder`, будут перезаписаны следующим CI-прогоном.

## Числа 1:1 из MVP (см. `Docs/GDD.md`)

Экономика, таймеры, шансы и формулы — в `GameManager.cs` (статические методы `*ForDay`, методы
`Earn/Spend/Fine`) и в `ContentDatabase.cs` (контентные таблицы). Смотрите GDD за полным списком —
дублировать числа здесь и там означает гарантированный рассинхрон.
