#!/usr/bin/env python3
"""Пересобирает Database/project.sqlite из schema.sql и сид-данных, извлечённых из HTML MVP.

Запуск: python3 Tools/ai_suite/init_db.py
Идемпотентно: удаляет старый project.sqlite и создаёт заново. Это не production MCP-сервер
(см. AI/MCP.md) — просто способ получить рабочую БД без ручного набора INSERT-ов.
"""
import sqlite3
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "Database" / "schema.sql"
DB = ROOT / "Database" / "project.sqlite"

CUSTOMER_ARCHETYPES = [
    ("normal", 0.81, 34, "22% без кода -> мини-игра «Глаз Бога»"),
    ("babka", 0.10, 44, "мини-игра «Крокодил» на угадывание товара"),
    ("shopaholic", 0.09, 34, "гарантированная примерка после выдачи"),
]

DIALOGUE_LINES = [
    ("spawn", "normal", "Мне бы заказик забрать"),
    ("spawn", "normal", "Код: 4-8-15-16-23-42"),
    ("spawn", "normal", "Я быстро, такси ждёт"),
    ("spawn", "normal", "Тут моя посылочка!"),
    ("spawn", "normal", "Только не говорите, что потеряли"),
    ("nocode", "normal", "У меня код не пришёл!!"),
    ("nocode", "normal", "Телефон сел, выдайте так"),
    ("nocode", "normal", "Какой ещё код?!"),
    ("angry", None, "Я буду жаловаться!"),
    ("angry", None, "Одна звезда!"),
    ("angry", None, "Ноги моей тут не будет. До завтра."),
    ("angry", None, "Безобразие!!"),
    ("fitting", None, "Я только гляну…"),
    ("fitting", None, "Это не мой размер!"),
    ("fitting", None, "На фото было другое 😤"),
    ("happy", None, "Спасибо!"),
    ("happy", None, "Наконец-то!"),
    ("happy", None, "Донесу не открывая. Шучу."),
    ("babka_spawn", "babka", "Внученька, я заказала… что-то…"),
]

SHOP_UPGRADES = [
    ("mat", "Отвечать клиентам матом", "Злой клиент? 50/50: уважение улицы или жалоба на лексику.", 500,
     "50% шанс +0.05 рейтинга вместо -0.2 при потере терпения клиента; иначе доп. штраф 40 ₽"),
    ("mic", "Микрофон для репа", "Записывай фристайл прямо на стойке. +5% к флоу (скорости). Иногда клиенты снимают сторис (+20₽).", 1500,
     "+5% скорость передвижения; каждые 16 тактов (~5.3с) 25% шанс +20 ₽"),
    ("troika", "Карта «Тройка»", "На работу не пешком: каждый день +20 секунд к смене.", 800,
     "+20 секунд к длительности каждой последующей смены"),
]

FINES = [
    ("Клиент пожаловался, что вы дышали", 30, 1),
    ("Помятая коробка на фотоотчёте", 20, 1),
    ("Слишком долго смотрели в окно", 10, 1),
    ("Улыбка не соответствует брендбуку", 25, 1),
    ("Скотч отклеен под неправильным углом", 15, 1),
    ("Найден перекур на камере №4", 35, 1),
    ("Помятая коробка (спасибо курьеру)", 20, 0),
    ("Помятая коробка при выдаче", 30, 0),
    ("У курьера кенты в Яндекс.Доставке. Стрелка не задалась", 150, 0),
    ("Спалили на перекуре. Жалоба в поддержку", 60, 0),
    ("Залипание в телефон при клиентах", 25, 0),
    ("Недостача не найдена. Вычли из ЗП", 100, 0),
    ("Жалоба на лексику сотрудника", 40, 0),
]

MEMES = [
    ("🐈", "Кот увидел цену доставки"),
    ("📦", "Когда заказал XS, а пришло ХЗ"),
    ("🧍", "Очередь в ПВЗ 31 декабря в 20:59"),
    ("🤳", "«Я на минутку» — минутка (48 коробок)"),
    ("🚚", "Курьер и надпись «хрупкое»: история вражды"),
    ("💸", "Зарплата пришла → зарплата ушла на возвраты"),
    ("🧾", "Штраф за то, что штрафов мало"),
    ("🐕", "Пёс охраняет ячейку Б4 лучше тебя"),
    ("😴", "Ночная смена. Товар не сходится. Опять."),
    ("🧤", "Примерил перчатки. Вернул перчатки. Купил перчатки."),
    ("📵", "«Код не пришёл» — гимн ПВЗ"),
    ("🥤", "Энергетик на перекуре — это ЗОЖ, если быстро"),
    ("🎤", "Записал реп на стойке. Клиент поставил 1 звезду. Хейтеры."),
    ("🧓", "Бабушка заказала «что-то». Крокодил начался."),
    ("🟣", "Фиолетовый ПВЗ через дорогу опять переманивает"),
    ("📱", "Чехол на Poco X7 Pro Max 48px — валюта улиц"),
]

BABKA_ITEMS = [
    ("Носки, 47 пар", "что-то тёплое… много… мужу…"),
    ("Эпилятор", "жужжит и делает больно…"),
    ("Чехол Poco X7 Pro Max 48px", "на телефон… как у внука…"),
    ("Садовый гном", "маленький мужичок… для дачи…"),
    ("Термос", "чтоб чай не остывал в поликлинике…"),
    ("Пряжа, 3 мотка", "нитки… свитер коту вязать…"),
    ("Тонометр", "давление мерить… ну вы поняли…"),
]

RANKS = [
    ("🥉 Стажёр склада", 0, 0),
    ("🥈 Кладовщик 3 разряда", 250, 0),
    ("🥇 Мастер выдачи", 600, 0),
    ("👑 Легенда синей империи", 1100, 0),
    ("👑 Легенда фиолетовой империи", 1100, 1),
]


def new_uuid():
    return str(uuid.uuid4())


def insert_entity(cur, kind):
    u = new_uuid()
    cur.execute("INSERT INTO entities (uuid, kind) VALUES (?, ?)", (u, kind))
    return u


def main():
    if DB.exists():
        DB.unlink()
    conn = sqlite3.connect(DB)
    cur = conn.cursor()
    cur.executescript(SCHEMA.read_text(encoding="utf-8"))

    for name, weight, patience, notes in CUSTOMER_ARCHETYPES:
        u = insert_entity(cur, "customer_archetypes")
        cur.execute(
            "INSERT INTO customer_archetypes (uuid, name, spawn_weight, base_patience, notes) VALUES (?,?,?,?,?)",
            (u, name, weight, patience, notes),
        )

    for category, archetype, text in DIALOGUE_LINES:
        u = insert_entity(cur, "dialogue_lines")
        cur.execute(
            "INSERT INTO dialogue_lines (uuid, category, archetype, text) VALUES (?,?,?,?)",
            (u, category, archetype, text),
        )

    for key, name, desc, price, effect in SHOP_UPGRADES:
        u = insert_entity(cur, "shop_upgrades")
        cur.execute(
            "INSERT INTO shop_upgrades (uuid, key, name, description, price, effect_notes) VALUES (?,?,?,?,?,?)",
            (u, key, name, desc, price, effect),
        )

    for reason, amount, is_absurd in FINES:
        u = insert_entity(cur, "fines")
        cur.execute(
            "INSERT INTO fines (uuid, reason, amount, is_absurd) VALUES (?,?,?,?)",
            (u, reason, amount, is_absurd),
        )

    for emoji, text in MEMES:
        u = insert_entity(cur, "memes")
        cur.execute("INSERT INTO memes (uuid, emoji, text) VALUES (?,?,?)", (u, emoji, text))

    for name, hint in BABKA_ITEMS:
        u = insert_entity(cur, "babka_items")
        cur.execute("INSERT INTO babka_items (uuid, name, hint) VALUES (?,?,?)", (u, name, hint))

    for title, min_earned, purple in RANKS:
        u = insert_entity(cur, "ranks")
        cur.execute(
            "INSERT INTO ranks (uuid, title, min_day_earned, is_purple_variant) VALUES (?,?,?,?)",
            (u, title, min_earned, purple),
        )

    conn.commit()
    counts = {
        row[0]: row[1]
        for row in cur.execute(
            "SELECT kind, COUNT(*) FROM entities GROUP BY kind"
        ).fetchall()
    }
    conn.close()
    print(f"OK: {DB.relative_to(ROOT)} создан.")
    for kind, n in counts.items():
        print(f"  {kind}: {n}")


if __name__ == "__main__":
    main()
