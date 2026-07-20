using System.Collections.Generic;
using Gazon.Core;
using Gazon.Customers;
using Gazon.Player;
using Gazon.World;
using UnityEngine;

namespace Gazon.UI
{
    /// <summary>
    /// Весь UI на OnGUI (IMGUI) — в проекте нет TextMeshPro/uGUI, а DebugHud уже был на OnGUI,
    /// так что это тот же подход, доведённый до полного покрытия MVP: HUD, телефон/магазин,
    /// три мини-игры, перекур, ночная инвентаризация, экраны меню/итогов/увольнения/ирл/паузы.
    /// Здесь же — роутинг клавиш верхнего уровня (Q/R/X/Esc/1-3/f-g-h-j-k-l), 1:1 с единым
    /// keydown-обработчиком MVP.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        private class ToastEntry
        {
            public string message;
            public string kind;
            public float expire;
        }

        private static readonly (string letter, KeyCode code)[] QteKeyCodes =
        {
            ("f", KeyCode.F), ("g", KeyCode.G), ("h", KeyCode.H),
            ("j", KeyCode.J), ("k", KeyCode.K), ("l", KeyCode.L),
        };

        private readonly List<ToastEntry> toasts = new List<ToastEntry>();
        private string currentPrompt = "";
        private string firedReason = "";
        private bool phoneTabIsShop;
        private MemeData currentReel;

        private void Start()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.OnToast.AddListener((msg, kind) =>
            {
                toasts.Add(new ToastEntry { message = msg, kind = kind, expire = Time.time + 3.4f });
                if (toasts.Count > 6) toasts.RemoveAt(0);
            });
            gm.OnFired.AddListener(reason => firedReason = reason);

            if (PlayerInteraction.Instance != null)
                PlayerInteraction.Instance.OnPromptChanged.AddListener(p => currentPrompt = p);
        }

        // ==================== ВВОД ====================

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (InputLock.PhoneOpen) ClosePhone();
                else if (gm.State == GameState.Play || gm.State == GameState.Paused) gm.TogglePause();
                return;
            }

            if (gm.State != GameState.Play) return;

            var mc = MinigameController.Instance;

            if (mc.QteTarget != null)
            {
                foreach (var k in QteKeyCodes)
                    if (Input.GetKeyDown(k.code))
                    {
                        mc.ResolveQte(k.letter == mc.QteKey);
                        return;
                    }
                return;
            }

            if (mc.BabkaTarget != null)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) mc.AnswerBabka(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) mc.AnswerBabka(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) mc.AnswerBabka(2);
                return;
            }

            if (mc.EyeTarget != null)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) mc.AnswerEye(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) mc.AnswerEye(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) mc.AnswerEye(2);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                if (InputLock.PhoneOpen) ClosePhone(); else OpenPhone();
                return;
            }

            if (InputLock.PhoneOpen)
            {
                if (Input.GetKeyDown(KeyCode.F)) SwipeReel();
                return;
            }

            if (InputLock.SmokeActive) return;

            if (Input.GetKeyDown(KeyCode.R)) PlayerBuffs.Instance?.TrySmile();
            if (Input.GetKeyDown(KeyCode.X)) PlayerInteraction.Instance?.TrySteal();
        }

        private void OpenPhone()
        {
            InputLock.PhoneOpen = true;
            phoneTabIsShop = false;
            currentReel = ContentDatabase.RandomMeme();
        }

        private void ClosePhone()
        {
            InputLock.PhoneOpen = false;
        }

        private void SwipeReel()
        {
            currentReel = ContentDatabase.RandomMeme();
            if (Random.value < 0.2f)
            {
                PlayerBuffs.Instance.GrantDopamine(45f);
                GameManager.Instance.Toast("📱 Дофамин! Улыбка доступна по [R] (45с)", "good");
            }
        }

        // ==================== РЕНДЕР ====================

        private void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            switch (gm.State)
            {
                case GameState.Menu: DrawMenu(); break;
                case GameState.Play:
                    DrawHud();
                    DrawCustomerBubbles();
                    DrawPanels();
                    break;
                case GameState.Paused:
                    DrawHud();
                    DrawPause();
                    break;
                case GameState.NightInventory: DrawNightInventory(); break;
                case GameState.DaySummary: DrawSummary(); break;
                case GameState.Fired: DrawFired(); break;
                case GameState.IrlEnding: DrawIrlEnding(); break;
            }

            DrawToasts();
        }

        private void DrawMenu()
        {
            float w = 520, h = 360;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            GUILayout.Space(10);
            GUILayout.Label(GameManager.Instance.Purple ? "vilberis" : "gazon", BigLabel());
            GUILayout.Label("ПВЗ СИМУЛЯТОР · порт в Unity");
            GUILayout.Label("Клиент всегда прав. К сожалению.");
            GUILayout.Space(10);
            if (GUILayout.Button("Начать смену", GUILayout.Height(36)))
                GameManager.Instance.StartNewGame();
            GUILayout.Space(10);
            GUILayout.Label("Мышь — осмотр · WASD ходить · Shift бег · E действие");
            GUILayout.Label("Q телефон · R улыбка · X спиздить заказ · 1-3 мини-игры · Esc пауза");
            GUILayout.EndArea();
        }

        private void DrawHud()
        {
            var gm = GameManager.Instance;
            GUILayout.BeginArea(new Rect(8, 8, Screen.width - 16, 30));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"📅 День {gm.Day}");
            GUILayout.Label($"⏱ {Mathf.Max(0, Mathf.FloorToInt(gm.ShiftTimeRemaining / 60))}:{Mathf.Max(0, Mathf.FloorToInt(gm.ShiftTimeRemaining % 60)):00}");
            GUILayout.Label($"💰 {Mathf.RoundToInt(gm.Money)} ₽");
            GUILayout.Label($"⭐ {gm.Rating:0.0}");
            GUILayout.Label($"📦 {Box.CountOnDock()}");
            GUILayout.Label($"🧍 {Customer.CountNotLeaving()}");
            GUILayout.Label($"📱 Чехлы: {gm.CasesInventory}");
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            DrawBuffs();
            DrawObjective();
            DrawAimPrompt();
            DrawHoldBar();

            if (PlayerBuffs.Instance != null && PlayerBuffs.Instance.IsSmiling)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(Screen.width / 2f - 60, Screen.height * 0.35f - 30, 120, 60), "😁", style);
            }
        }

        private void DrawBuffs()
        {
            var b = PlayerBuffs.Instance;
            if (b == null) return;
            var lines = new List<string>();
            if (b.Dopamine > 0) lines.Add($"💊 Дофамин {Mathf.CeilToInt(b.Dopamine)}с — улыбка [R]");
            if (b.IsSmiling) lines.Add($"😁 Улыбаешься {Mathf.CeilToInt(b.Smiling)}с");
            if (b.HasSpeedBuff) lines.Add($"⚡ Энергетик {Mathf.CeilToInt(b.SpeedBuff)}с");

            float y = 44;
            foreach (var line in lines)
            {
                var size = GUI.skin.label.CalcSize(new GUIContent(line));
                GUI.Box(new Rect(Screen.width - size.x - 24, y, size.x + 16, 24), line);
                y += 28;
            }
        }

        private void DrawObjective()
        {
            string text = ComputeObjective();
            if (string.IsNullOrEmpty(text)) return;
            var size = GUI.skin.box.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(14, 44, Mathf.Min(340, size.x + 20), 30), text);
        }

        private string ComputeObjective()
        {
            var mc = MinigameController.Instance;
            if (mc.BabkaTarget != null) return "Крокодил: жми [1-3]";
            if (mc.EyeTarget != null) return "«Глаз Бога»: жми [1-3]";
            if (mc.QteTarget != null) return "QTE: жми показанную клавишу!";
            if (InputLock.SmokeActive) return "Перекур. Очередь копит ненависть.";

            var carried = PlayerInteraction.Instance != null ? PlayerInteraction.Instance.CarriedBox : null;
            if (carried != null && carried.IsReturn) return "Задача: возврат → оранжевый стол";
            if (carried != null && carried.AssignedCell != null) return $"Задача: коробка → ячейка {carried.AssignedCell.Label}";
            if (carried != null) return "Задача: выдай заказ с синей стойки";

            foreach (var c in Customer.Active)
                if (c.State == CustomerState.AtWindow && c.Archetype == CustomerArchetype.Babka && !c.Guessed)
                    return "🧓 Бабушка у окна ждёт крокодила";

            var waiting = new List<string>();
            foreach (var c in Customer.Active)
                if (c.State == CustomerState.AtWindow && c.OrderBox != null && c.OrderBox.AssignedCell != null && c.Guessed)
                    waiting.Add(c.OrderBox.AssignedCell.Label);
            if (waiting.Count > 0) return "Ждут: " + string.Join(", ", waiting);

            int dock = Box.CountOnDock();
            return dock > 0 ? $"На приёмке коробок: {dock}" : "Передышка. Ненадолго.";
        }

        private void DrawAimPrompt()
        {
            if (string.IsNullOrEmpty(currentPrompt)) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            var size = style.CalcSize(new GUIContent("[E] " + currentPrompt));
            GUI.Box(new Rect((Screen.width - size.x) / 2f, Screen.height * 0.56f, size.x + 10, 28), "[E] " + currentPrompt, style);
        }

        private void DrawHoldBar()
        {
            var pi = PlayerInteraction.Instance;
            if (pi == null || !pi.IsHolding) return;
            var back = new Rect(Screen.width / 2f - 60, Screen.height * 0.62f, 120, 10);
            GUI.Box(back, "");
            GUI.Box(new Rect(back.x, back.y, back.width * pi.HoldProgress01, back.height), "");
        }

        private void DrawCustomerBubbles()
        {
            var cam = Camera.main;
            if (cam == null) return;

            foreach (var c in Customer.Active)
            {
                if (c.State == CustomerState.Leaving) continue;
                var world = c.transform.position + Vector3.up * 2.0f;
                var screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;

                string text = c.CurrentBubble();
                if (string.IsNullOrEmpty(text)) continue;

                var style = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                if (c.BubbleIsAlert())
                {
                    style.normal.textColor = new Color(1f, 0.3f, 0.5f);
                }

                float bw = 150, bh = 34;
                var rect = new Rect(screen.x - bw / 2f, Screen.height - screen.y - bh - 10, bw, bh);
                GUI.Box(rect, text, style);
            }
        }

        private void DrawPanels()
        {
            if (InputLock.PhoneOpen) DrawPhone();

            // Перекур блэкаутит экран целиком — рисовать под ним диалоги мини-игр
            // означает наложение текста "Перекур" на текст крокодила/Глаза Бога.
            if (InputLock.SmokeActive) { DrawSmoke(); return; }

            var mc = MinigameController.Instance;
            bool showBabka = mc.BabkaTarget != null;
            bool showEye = mc.EyeTarget != null;

            // Крокодил и «Глаз Бога» раньше рисовались в одном и том же Rect — если два разных
            // окна выдачи одновременно триггерили обе мини-игры для разных клиентов, их тексты
            // полностью накладывались друг на друга. Теперь активные панели складываются в стопку.
            const float dialogW = 460f, dialogH = 220f, gap = 14f;
            int count = (showBabka ? 1 : 0) + (showEye ? 1 : 0);
            float totalH = count * dialogH + Mathf.Max(0, count - 1) * gap;
            float y = Screen.height - totalH - 70f;

            if (showBabka)
            {
                DrawBabka(new Rect((Screen.width - dialogW) / 2f, y, dialogW, dialogH));
                y += dialogH + gap;
            }
            if (showEye)
                DrawEye(new Rect((Screen.width - dialogW) / 2f, y, dialogW, dialogH));

            if (mc.QteTarget != null) DrawQte();
        }

        private void DrawPhone()
        {
            float w = 300, h = 460;
            var rect = new Rect(Screen.width - w - 20, Screen.height - h - 20, w, h);
            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("📱 Рилс")) phoneTabIsShop = false;
            if (GUILayout.Button("💳 Донат")) phoneTabIsShop = true;
            GUILayout.EndHorizontal();

            if (!phoneTabIsShop) DrawReelsTab();
            else DrawShopTab();

            GUILayout.FlexibleSpace();
            GUILayout.Label("[Q] — убрать телефон. Пока листаешь — ПВЗ живёт своей жизнью.");
            GUILayout.EndArea();
        }

        private void DrawReelsTab()
        {
            GUILayout.Label(currentReel.emoji, new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter });
            GUILayout.Label(currentReel.text, new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.MiddleCenter });
            if (GUILayout.Button("Свайп [F]", GUILayout.Height(34)))
                SwipeReel();
        }

        private void DrawShopTab()
        {
            var gm = GameManager.Instance;
            DrawUpgradeRow("mat", "🗣 Отвечать матом", 500);
            DrawUpgradeRow("mic", "🎤 Микрофон для репа", 1500);
            DrawUpgradeRow("troika", "🚌 Карта «Тройка»", 800);

            GUILayout.Space(6);
            GUILayout.Label($"📱 Чехлы: {gm.CasesInventory} шт. По 150 ₽ за штуку.");
            GUI.enabled = gm.CasesInventory > 0;
            if (GUILayout.Button("Продать чехол (+150 ₽)")) gm.SellCase();
            GUI.enabled = true;

            GUILayout.Space(6);
            GUI.enabled = gm.Money >= GameManager.IrlEndingPrice;
            if (GUILayout.Button($"🏪 Купить точку ПВЗ ирл ({GameManager.IrlEndingPrice:0} ₽)"))
                gm.BuyIrlEnding();
            GUI.enabled = true;
        }

        private void DrawUpgradeRow(string key, string label, int price)
        {
            var gm = GameManager.Instance;
            bool bought = gm.IsUpgradeBought(key);
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(180));
            GUI.enabled = !bought && gm.Money >= price;
            if (GUILayout.Button(bought ? "Куплено" : $"{price} ₽"))
                gm.BuyUpgrade(key);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawBabka(Rect rect)
        {
            var mc = MinigameController.Instance;
            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            GUILayout.Label("🧓 Крокодил с бабушкой", BoldLabel());
            GUILayout.Label($"«Ну это… {mc.BabkaTarget.Item.hint}»");
            for (int i = 0; i < mc.BabkaOptions.Count; i++)
                if (GUILayout.Button($"[{i + 1}] {mc.BabkaOptions[i].name}"))
                    mc.AnswerBabka(i);
            if (!string.IsNullOrEmpty(mc.BabkaFeedback))
                GUILayout.Label(mc.BabkaFeedback);
            GUILayout.EndArea();
        }

        private void DrawEye(Rect rect)
        {
            var mc = MinigameController.Instance;
            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            GUILayout.Label("👁 «Глаз Бога» (пиратская версия)", BoldLabel());
            GUILayout.Label($"Клиент без кода. Стоит в {mc.EyeTarget.ColorName} кофте. Найди профиль:");
            for (int i = 0; i < mc.EyeOptions.Count; i++)
                if (GUILayout.Button($"[{i + 1}] +7 9** ***-**-** · кофта {mc.EyeOptions[i].name}"))
                    mc.AnswerEye(i);
            GUILayout.EndArea();
        }

        private void DrawQte()
        {
            var mc = MinigameController.Instance;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 72, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(Screen.width / 2f - 60, Screen.height * 0.3f, 120, 90), mc.QteKey.ToUpperInvariant(), style);
            float progress = 1f - Mathf.Clamp01(mc.QteElapsed / MinigameController.QteDuration);
            var back = new Rect(Screen.width / 2f - 80, Screen.height * 0.3f + 90, 160, 10);
            GUI.Box(back, "");
            GUI.Box(new Rect(back.x, back.y, back.width * progress, back.height), "");
        }

        private void DrawSmoke()
        {
            var controller = SmokeBreakController.Instance;
            var rect = new Rect(0, 0, Screen.width, Screen.height);
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.75f);
            GUI.Box(rect, "");
            GUI.color = old;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height / 2f - 40, Screen.width, 30), "🚬 Перекур. ПВЗ «закрыт»", style);
            var back = new Rect(Screen.width / 2f - 110, Screen.height / 2f, 220, 10);
            GUI.Box(back, "");
            GUI.Box(new Rect(back.x, back.y, back.width * controller.Progress01, back.height), "");
        }

        private void DrawPause()
        {
            DrawOverlay("пауза", "Очередь не рассасывается даже на обеде.", () =>
            {
                if (GUILayout.Button("Продолжить", GUILayout.Height(34)))
                    GameManager.Instance.TogglePause();
            });
        }

        private void DrawNightInventory()
        {
            var inv = NightInventoryController.Instance;
            float w = 560, h = 360;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            GUILayout.Label("ночная смена", BigLabel());
            GUILayout.Label("Инвентаризация: не сходится 1 товар. Пересмотри записи с камер и найди подозрительный кадр.");
            if (inv.CurrentFrames != null)
                for (int i = 0; i < inv.CurrentFrames.Count; i++)
                {
                    var f = inv.CurrentFrames[i];
                    if (GUILayout.Button($"[{i + 1}] 📹 {f.time} — {f.text}"))
                        inv.Answer(i);
                }
            GUILayout.EndArea();
        }

        private void DrawSummary()
        {
            var gm = GameManager.Instance;
            float w = 480, h = 300;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            GUILayout.Label("смена окончена", BigLabel());
            GUILayout.Label($"Выдано заказов: {gm.ServedCount}");
            GUILayout.Label($"Возвратов обработано: {gm.ReturnsDone}");
            GUILayout.Label($"Злых клиентов: {gm.AngryCount} · Спижжено: {gm.StolenCount} 📱");
            GUILayout.Label($"Штрафы: −{Mathf.RoundToInt(gm.FinesTotal)} ₽");
            GUILayout.Label($"Заработано за день: {Mathf.RoundToInt(gm.DayEarned)} ₽ | Рейтинг: {gm.Rating:0.0} ★");
            GUILayout.Label("Звание: " + gm.CurrentRankTitle());
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Следующий день →", GUILayout.Height(32))) gm.AdvanceToNextDay();
            if (GUILayout.Button("В меню", GUILayout.Height(32))) gm.GoToMenu();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawFired()
        {
            DrawOverlay("вы уволены", firedReason, () =>
            {
                GUILayout.Label("Единственная вакансия в городе — VILBERIS. Весь прогресс сброшен.");
                if (GUILayout.Button("Устроиться в Vilberis", GUILayout.Height(34)))
                    GameManager.Instance.RehireAtVilberis();
            });
        }

        private void DrawIrlEnding()
        {
            DrawOverlay("поздравляем", "Вы купили точку ПВЗ в своём городе. Ирл.\n" +
                "Теперь у вас: ИП, кассовая дисциплина, договор субаренды,\n" +
                "проверки, штрафы за дыхание и настоящая газель по вторникам.\n\n" +
                "Игра пройдена. Или только началась?", () =>
            {
                if (GUILayout.Button("Проснуться", GUILayout.Height(34)))
                    GameManager.Instance.GoToMenu();
            });
        }

        private void DrawOverlay(string title, string tag, System.Action drawButtons)
        {
            float w = 520, h = 300;
            var rect = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(rect, "");
            GUILayout.BeginArea(rect);
            GUILayout.Label(title, BigLabel());
            var tagStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            GUILayout.Label(tag, tagStyle);
            drawButtons();
            GUILayout.EndArea();
        }

        private void DrawToasts()
        {
            float y = Screen.height - 20;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                if (Time.time > toasts[i].expire) { toasts.RemoveAt(i); continue; }
            }
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                var t = toasts[i];
                var size = GUI.skin.box.CalcSize(new GUIContent(t.message));
                float w = Mathf.Min(420, size.x + 16);
                y -= 26;
                var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
                if (t.kind == "bad") style.normal.textColor = new Color(1f, 0.4f, 0.4f);
                else if (t.kind == "warn") style.normal.textColor = new Color(1f, 0.75f, 0.2f);
                else if (t.kind == "good") style.normal.textColor = new Color(0.3f, 0.9f, 0.5f);
                GUI.Box(new Rect(14, y, w, 24), t.message, style);
            }
        }

        private static GUIStyle BigLabel() => new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
        private static GUIStyle BoldLabel() => new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
    }
}
