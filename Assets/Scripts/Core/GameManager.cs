using System;
using Gazon.Customers;
using Gazon.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Gazon.Core
{
    public enum GameState
    {
        Menu,
        Play,
        NightInventory,
        DaySummary,
        Fired,
        IrlEnding,
        Paused
    }

    // UnityEvent<T> сам по себе не показывается в инспекторе — нужен конкретный сериализуемый подкласс.
    [Serializable] public class FloatEvent : UnityEvent<float> { }
    [Serializable] public class IntEvent : UnityEvent<int> { }
    [Serializable] public class StateEvent : UnityEvent<GameState> { }
    [Serializable] public class ToastEvent : UnityEvent<string, string> { } // (сообщение, вид: "" | "bad" | "warn" | "good")
    [Serializable] public class MessageEvent : UnityEvent<string> { }

    /// <summary>
    /// Экономика, состояние смены/дня, статистика, VILBERIS-ветка. Числа и правила — 1:1 из
    /// gamedev/pvz_simulator_mvp.html (см. Docs/GDD.md). Контент (штрафы/звания/апгрейды) — из
    /// ContentDatabase, не хардкожен здесь.
    ///
    /// Важная деталь fidelity: в MVP 3D-мир (вывеска, стойка) строится один раз при загрузке и
    /// НИКОГДА не перекрашивается после ребрендинга в VILBERIS — это осознанная шутка прототипа
    /// ("Стойка осталась синей: ребрендинг не завезли"), а не баг. Здесь она сохранена: Purple
    /// влияет только на HUD/меню (см. GameUI), не на геометрию, которую строит SceneBuilder.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Состояние")]
        [SerializeField] private GameState state = GameState.Menu;
        [SerializeField] private int day = 1;
        [SerializeField] private float money;
        [SerializeField] private float rating = 5.0f;
        [SerializeField] private float shiftTimeRemaining;
        [SerializeField] private bool purple;

        [Header("Статистика текущего дня")]
        [SerializeField] private float dayEarned;
        [SerializeField] private float finesTotal;
        [SerializeField] private int servedCount;
        [SerializeField] private int angryCount;
        [SerializeField] private int returnsDone;
        [SerializeField] private int stolenCount;

        [Header("Персистентно между днями (сбрасывается только в VILBERIS)")]
        [SerializeField] private int casesInventory;
        [SerializeField] private bool matUpgrade;
        [SerializeField] private bool micUpgrade;
        [SerializeField] private bool troikaUpgrade;

        // MVP: G.fineT (16-30с, 35% шанс абсурдного штрафа) и G.phoneCheckT (раз в 1с, 10% шанс
        // штрафа за телефон при клиентах у окна) — оба тикают только пока идёт смена.
        private float ambientFineTimer;
        private float phoneCheckTimer;

        [Header("События (подписка обычно из GameUI.Start(), код, не инспектор)")]
        public FloatEvent OnMoneyChanged;
        public FloatEvent OnRatingChanged;
        public FloatEvent OnShiftTimeChanged;
        public IntEvent OnDayChanged;
        public UnityEvent OnShiftEnded;
        public StateEvent OnStateChanged;
        public ToastEvent OnToast;
        public MessageEvent OnFired;

        public GameState State => state;
        public int Day => day;
        public float Money => money;
        public float Rating => rating;
        public float ShiftTimeRemaining => shiftTimeRemaining;
        public bool Purple => purple;
        public float DayEarned => dayEarned;
        public float FinesTotal => finesTotal;
        public int ServedCount => servedCount;
        public int AngryCount => angryCount;
        public int ReturnsDone => returnsDone;
        public int StolenCount => stolenCount;
        public int CasesInventory => casesInventory;
        public bool MatUpgrade => matUpgrade;
        public bool MicUpgrade => micUpgrade;
        public bool TroikaUpgrade => troikaUpgrade;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void SetState(GameState s)
        {
            state = s;
            OnStateChanged?.Invoke(s);
        }

        public void Toast(string message, string kind = "")
        {
            OnToast?.Invoke(message, kind);
        }

        // ---------- Экономика ----------

        /// <summary>Заработок — засчитывается и в money, и в dayEarned (для звания в конце дня).</summary>
        public void Earn(float amount)
        {
            money += amount;
            dayEarned += amount;
            OnMoneyChanged?.Invoke(money);
        }

        /// <summary>Трата (покупка апгрейда/ирл-концовки) — НЕ считается заработком дня.</summary>
        public void Spend(float amount)
        {
            money -= amount;
            OnMoneyChanged?.Invoke(money);
        }

        public void Fine(float amount, string reason)
        {
            money -= amount;
            finesTotal += amount;
            OnMoneyChanged?.Invoke(money);
            Toast($"⚠ {reason}. −{amount:0} ₽", "bad");
        }

        public void AddRating(float delta)
        {
            rating = Mathf.Clamp(rating + delta, 1f, 5f);
            OnRatingChanged?.Invoke(rating);
        }

        public void RecordServed() => servedCount++;
        public void RecordAngryCustomer() => angryCount++;
        public void RecordReturnProcessed() => returnsDone++;

        public void RecordStolenItem()
        {
            stolenCount++;
            casesInventory++;
        }

        // ---------- Донат-магазин ----------

        public bool BuyUpgrade(string key)
        {
            var data = ContentDatabase.GetUpgrade(key);
            if (string.IsNullOrEmpty(data.key) || money < data.price) return false;
            if (IsUpgradeBought(key)) return false;

            money -= data.price;
            switch (key)
            {
                case "mat": matUpgrade = true; break;
                case "mic": micUpgrade = true; break;
                case "troika": troikaUpgrade = true; break;
            }
            OnMoneyChanged?.Invoke(money);
            Toast("💳 Куплено: " + data.name, "good");
            return true;
        }

        public bool IsUpgradeBought(string key)
        {
            return key switch
            {
                "mat" => matUpgrade,
                "mic" => micUpgrade,
                "troika" => troikaUpgrade,
                _ => false
            };
        }

        public void SellCase()
        {
            if (casesInventory < 1) return;
            casesInventory--;
            Earn(150f);
            Toast("📱 Чехол ушёл за 150 ₽. Улицы помнят.", "good");
        }

        public const float IrlEndingPrice = 400000f;

        public bool BuyIrlEnding()
        {
            if (money < IrlEndingPrice) return false;
            money -= IrlEndingPrice;
            OnMoneyChanged?.Invoke(money);
            SetState(GameState.IrlEnding);
            return true;
        }

        // ---------- Цикл смены/дня ----------

        /// <summary>Вызывается один раз с главного меню ("Начать смену"). MVP: money/rating/day
        /// всегда сбрасываются здесь — апгрейды и чехлы НЕ трогаются (см. класс-комментарий).</summary>
        public void StartNewGame()
        {
            day = 1;
            money = 0f;
            rating = 5f;
            PlayerBuffs.Instance?.ResetForNewGame();
            InputLock.ResetAll();
            StartShift(1);
        }

        public void StartShift(int startDay)
        {
            day = startDay;
            shiftTimeRemaining = ShiftDurationForDay(day) + (troikaUpgrade ? 20f : 0f);
            dayEarned = 0f;
            finesTotal = 0f;
            servedCount = 0;
            angryCount = 0;
            returnsDone = 0;
            stolenCount = 0;

            OnMoneyChanged?.Invoke(money);
            OnRatingChanged?.Invoke(rating);
            OnDayChanged?.Invoke(day);
            OnShiftTimeChanged?.Invoke(shiftTimeRemaining);
            ambientFineTimer = 14f + UnityEngine.Random.value * 10f; // MVP: G.fineT=14+Math.random()*10
            phoneCheckTimer = 1f;
            SetState(GameState.Play);

            if (troikaUpgrade)
                Toast("🚌 Приехал на автобусе. +20 секунд к смене.", "good");
        }

        private void Update()
        {
            if (state != GameState.Play) return;

            shiftTimeRemaining -= Time.deltaTime;
            OnShiftTimeChanged?.Invoke(Mathf.Max(0f, shiftTimeRemaining));

            if (shiftTimeRemaining <= 0f)
            {
                EndShift();
                return;
            }

            UpdateAmbientFine();
            UpdatePhoneRisk();
        }

        /// <summary>MVP: каждые 16-30 сек, 35% шанс случайного абсурдного штрафа.</summary>
        private void UpdateAmbientFine()
        {
            ambientFineTimer -= Time.deltaTime;
            if (ambientFineTimer > 0f) return;

            ambientFineTimer = 16f + UnityEngine.Random.value * 14f;
            if (UnityEngine.Random.value < 0.35f)
            {
                var f = ContentDatabase.RandomFine(onlyAbsurd: true);
                if (!string.IsNullOrEmpty(f.reason)) Fine(f.amount, f.reason);
            }
        }

        /// <summary>MVP: пока открыт телефон, раз в секунду 10% шанс штрафа, если кто-то ждёт у окна.</summary>
        private void UpdatePhoneRisk()
        {
            if (!InputLock.PhoneOpen) return;

            phoneCheckTimer -= Time.deltaTime;
            if (phoneCheckTimer > 0f) return;

            phoneCheckTimer = 1f;
            if (UnityEngine.Random.value < 0.1f && AnyCustomerAtWindow())
                Fine(25f, "Залипание в телефон при клиентах");
        }

        private static bool AnyCustomerAtWindow()
        {
            foreach (var c in Customer.Active)
                if (c.State == CustomerState.AtWindow) return true;
            return false;
        }

        private void EndShift()
        {
            SetState(GameState.NightInventory);
            OnShiftEnded?.Invoke();
        }

        /// <summary>Вызывается NightInventoryController-ом после того, как игрок ответил на камеру.</summary>
        public void ShowDaySummary()
        {
            SetState(GameState.DaySummary);
        }

        public void AdvanceToNextDay()
        {
            StartShift(day + 1);
        }

        public void GoToMenu()
        {
            SetState(GameState.Menu);
        }

        public void TogglePause()
        {
            if (state == GameState.Play) SetState(GameState.Paused);
            else if (state == GameState.Paused) SetState(GameState.Play);
        }

        public void Fire(string reason)
        {
            SetState(GameState.Fired);
            OnFired?.Invoke(reason);
        }

        public void RehireAtVilberis()
        {
            purple = true;
            matUpgrade = micUpgrade = troikaUpgrade = false;
            casesInventory = 0;
            StartNewGame();
        }

        public string CurrentRankTitle() => ContentDatabase.RankFor(dayEarned, purple);

        // ---------- Формулы MVP (см. Docs/GDD.md) ----------

        /// <summary>MVP: Math.max(120, 180-(day-1)*5).</summary>
        public static float ShiftDurationForDay(int forDay) => Mathf.Max(120f, 180f - (forDay - 1) * 5f);

        /// <summary>MVP: Math.max(30, 48-day*2).</summary>
        public static float TruckIntervalForDay(int forDay) => Mathf.Max(30f, 48f - forDay * 2f);

        /// <summary>MVP: Math.min(4+day*2, 12).</summary>
        public static int BoxesPerTruckForDay(int forDay) => Mathf.Min(4 + forDay * 2, 12);

        /// <summary>MVP: Math.max(3.2, 8-day).</summary>
        public static float CustomerSpawnIntervalForDay(int forDay) => Mathf.Max(3.2f, 8f - forDay);
    }
}
