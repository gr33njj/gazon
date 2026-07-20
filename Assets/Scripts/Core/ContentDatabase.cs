using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gazon.Core
{
    [Serializable]
    public struct CustomerArchetypeData
    {
        public string name;
        public float spawnWeight;
        public float basePatience;
    }

    [Serializable]
    public struct DialogueLineData
    {
        public string category;
        public string archetype; // "" = общая реплика, не привязана к архетипу
        public string text;
    }

    [Serializable]
    public struct ShopUpgradeData
    {
        public string key;
        public string name;
        public string description;
        public int price;
    }

    [Serializable]
    public struct FineData
    {
        public string reason;
        public int amount;
        public bool isAbsurd;
    }

    [Serializable]
    public struct MemeData
    {
        public string emoji;
        public string text;
    }

    [Serializable]
    public struct BabkaItemData
    {
        public string name;
        public string hint;
    }

    [Serializable]
    public struct RankData
    {
        public string title;
        public int minDayEarned;
        public bool isPurpleVariant;
    }

    [Serializable]
    internal class GameContentFile
    {
        public CustomerArchetypeData[] customerArchetypes;
        public DialogueLineData[] dialogueLines;
        public ShopUpgradeData[] shopUpgrades;
        public FineData[] fines;
        public MemeData[] memes;
        public BabkaItemData[] babkaItems;
        public RankData[] ranks;
    }

    /// <summary>
    /// Загружает Assets/Resources/GameContent.json (экспорт из Database/project.sqlite —
    /// см. Tools/ai_suite/export_content.py и AI/Rules.md, "SQLite прежде всего").
    /// Контент не хардкодится в C# — только читается отсюда.
    /// </summary>
    public static class ContentDatabase
    {
        private static GameContentFile data;

        private static void EnsureLoaded()
        {
            if (data != null) return;

            var json = Resources.Load<TextAsset>("GameContent");
            if (json == null)
            {
                Debug.LogError("GameContent.json не найден в Resources — запусти Tools/ai_suite/export_content.py");
                data = new GameContentFile
                {
                    customerArchetypes = Array.Empty<CustomerArchetypeData>(),
                    dialogueLines = Array.Empty<DialogueLineData>(),
                    shopUpgrades = Array.Empty<ShopUpgradeData>(),
                    fines = Array.Empty<FineData>(),
                    memes = Array.Empty<MemeData>(),
                    babkaItems = Array.Empty<BabkaItemData>(),
                    ranks = Array.Empty<RankData>(),
                };
                return;
            }

            data = JsonUtility.FromJson<GameContentFile>(json.text);
        }

        public static IReadOnlyList<CustomerArchetypeData> CustomerArchetypes
        {
            get { EnsureLoaded(); return data.customerArchetypes; }
        }

        public static IReadOnlyList<ShopUpgradeData> ShopUpgrades
        {
            get { EnsureLoaded(); return data.shopUpgrades; }
        }

        public static ShopUpgradeData GetUpgrade(string key)
        {
            EnsureLoaded();
            foreach (var u in data.shopUpgrades)
                if (u.key == key) return u;
            return default;
        }

        public static string RandomDialogueLine(string category, string archetype = null)
        {
            EnsureLoaded();
            var matches = new List<string>();
            foreach (var line in data.dialogueLines)
            {
                if (line.category != category) continue;
                // archetype == "" в данных значит "общая" — подходит под любой запрошенный архетип.
                if (!string.IsNullOrEmpty(line.archetype) && line.archetype != archetype) continue;
                matches.Add(line.text);
            }
            if (matches.Count == 0) return string.Empty;
            return matches[UnityEngine.Random.Range(0, matches.Count)];
        }

        public static FineData RandomFine(bool onlyAbsurd)
        {
            EnsureLoaded();
            var matches = onlyAbsurd ? data.fines.Where(f => f.isAbsurd).ToList() : data.fines.ToList();
            if (matches.Count == 0) return default;
            return matches[UnityEngine.Random.Range(0, matches.Count)];
        }

        public static MemeData RandomMeme()
        {
            EnsureLoaded();
            if (data.memes.Length == 0) return default;
            return data.memes[UnityEngine.Random.Range(0, data.memes.Length)];
        }

        public static BabkaItemData RandomBabkaItem(BabkaItemData? exclude = null)
        {
            EnsureLoaded();
            var pool = data.babkaItems.AsEnumerable();
            if (exclude.HasValue)
                pool = pool.Where(i => i.name != exclude.Value.name);
            var list = pool.ToList();
            if (list.Count == 0) list = data.babkaItems.ToList();
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public static List<BabkaItemData> RandomBabkaItems(int count, BabkaItemData exclude)
        {
            EnsureLoaded();
            var pool = data.babkaItems.Where(i => i.name != exclude.name).ToList();
            Shuffle(pool);
            return pool.Take(count).ToList();
        }

        /// <summary>
        /// Некоторые пороги (сейчас только верхний, 1100) имеют два варианта звания —
        /// синий и фиолетовый (см. GDD "Итоги дня и звания"). Для таких порогов берём
        /// вариант, соответствующий текущему PURPLE-флагу; для остальных цвет неважен.
        /// </summary>
        public static string RankFor(float dayEarned, bool purple)
        {
            EnsureLoaded();
            RankData best = default;
            bool found = false;
            foreach (var r in data.ranks)
            {
                if (HasBothColorVariants(r.minDayEarned) && r.isPurpleVariant != purple) continue;
                if (dayEarned >= r.minDayEarned && (!found || r.minDayEarned > best.minDayEarned))
                {
                    best = r;
                    found = true;
                }
            }
            return found ? best.title : "Стажёр склада";
        }

        private static bool HasBothColorVariants(int threshold)
        {
            bool hasPurple = false, hasBlue = false;
            foreach (var r in data.ranks)
            {
                if (r.minDayEarned != threshold) continue;
                if (r.isPurpleVariant) hasPurple = true; else hasBlue = true;
            }
            return hasPurple && hasBlue;
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
