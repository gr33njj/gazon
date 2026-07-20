#!/usr/bin/env python3
"""Экспортирует Database/project.sqlite в Assets/Resources/GameContent.json для Unity.

Unity (тем более WebGL-билд) не может читать sqlite напрямую без нативного плагина —
поэтому контент, зарегистрированный в БД (см. AI/Rules.md, "SQLite прежде всего"),
экспортируется снапшотом в JSON, который ContentDatabase.cs грузит через Resources.Load.

Запуск: python3 Tools/ai_suite/export_content.py
Перезапускать после любого изменения project.sqlite (напрямую или через init_db.py).
"""
import json
import sqlite3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DB = ROOT / "Database" / "project.sqlite"
OUT = ROOT / "Assets" / "Resources" / "GameContent.json"


def rows(con, sql):
    cur = con.execute(sql)
    cols = [d[0] for d in cur.description]
    return [dict(zip(cols, r)) for r in cur.fetchall()]


def main():
    con = sqlite3.connect(DB)
    data = {
        "customerArchetypes": [
            {"name": r["name"], "spawnWeight": r["spawn_weight"], "basePatience": r["base_patience"]}
            for r in rows(con, "SELECT name, spawn_weight, base_patience FROM customer_archetypes")
        ],
        "dialogueLines": [
            {"category": r["category"], "archetype": r["archetype"] or "", "text": r["text"]}
            for r in rows(con, "SELECT category, archetype, text FROM dialogue_lines")
        ],
        "shopUpgrades": [
            {"key": r["key"], "name": r["name"], "description": r["description"], "price": r["price"]}
            for r in rows(con, "SELECT key, name, description, price FROM shop_upgrades")
        ],
        "fines": [
            {"reason": r["reason"], "amount": r["amount"], "isAbsurd": bool(r["is_absurd"])}
            for r in rows(con, "SELECT reason, amount, is_absurd FROM fines")
        ],
        "memes": [
            {"emoji": r["emoji"], "text": r["text"]}
            for r in rows(con, "SELECT emoji, text FROM memes")
        ],
        "babkaItems": [
            {"name": r["name"], "hint": r["hint"]}
            for r in rows(con, "SELECT name, hint FROM babka_items")
        ],
        "ranks": [
            {"title": r["title"], "minDayEarned": r["min_day_earned"], "isPurpleVariant": bool(r["is_purple_variant"])}
            for r in rows(con, "SELECT title, min_day_earned, is_purple_variant FROM ranks")
        ],
    }
    con.close()

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"OK: {OUT.relative_to(ROOT)}")
    for k, v in data.items():
        print(f"  {k}: {len(v)}")


if __name__ == "__main__":
    main()
