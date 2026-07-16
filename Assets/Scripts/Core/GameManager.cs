using System;
using UnityEngine;
using UnityEngine.Events;

namespace Gazon.Core
{
    public enum GameState
    {
        Menu,
        Play,
        Summary
    }

    // UnityEvent<T> сам по себе не показывается в инспекторе — нужен конкретный сериализуемый подкласс.
    [Serializable] public class FloatEvent : UnityEvent<float> { }
    [Serializable] public class IntEvent : UnityEvent<int> { }

    /// <summary>
    /// День, деньги, рейтинг, таймер смены. Числа взяты 1:1 из MVP — см. Assets/Scripts/README.md.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Состояние")]
        [SerializeField] private GameState state = GameState.Menu;
        [SerializeField] private int day = 1;
        [SerializeField] private float money = 0f;
        [SerializeField] private float rating = 5.0f;
        [SerializeField] private float shiftTimeRemaining;

        [Header("События (для UI, привязка в инспекторе)")]
        public FloatEvent OnMoneyChanged;
        public FloatEvent OnRatingChanged;
        public FloatEvent OnShiftTimeChanged;
        public IntEvent OnDayChanged;
        public UnityEvent OnShiftEnded;

        public GameState State => state;
        public int Day => day;
        public float Money => money;
        public float Rating => rating;
        public float ShiftTimeRemaining => shiftTimeRemaining;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // Временно: в Phase 1 нет меню (см. Docs/GDD.md — btnStart из MVP пока не портирован),
        // поэтому смена стартует сама при запуске сцены. Убрать, когда появится реальный экран меню.
        private void Start()
        {
            StartShift(1);
        }

        public void StartShift(int startDay)
        {
            day = startDay;
            shiftTimeRemaining = ShiftDurationForDay(day);
            state = GameState.Play;
            OnDayChanged?.Invoke(day);
            OnShiftTimeChanged?.Invoke(shiftTimeRemaining);
        }

        public void AddMoney(float amount)
        {
            money += amount;
            OnMoneyChanged?.Invoke(money);
        }

        public void AddRating(float delta)
        {
            rating = Mathf.Clamp(rating + delta, 1f, 5f);
            OnRatingChanged?.Invoke(rating);
        }

        private void Update()
        {
            if (state != GameState.Play) return;

            shiftTimeRemaining -= Time.deltaTime;
            OnShiftTimeChanged?.Invoke(Mathf.Max(0f, shiftTimeRemaining));

            if (shiftTimeRemaining <= 0f)
            {
                EndShift();
            }
        }

        private void EndShift()
        {
            state = GameState.Summary;
            OnShiftEnded?.Invoke();
        }

        public void AdvanceToNextDay()
        {
            StartShift(day + 1);
        }

        /// <summary>MVP: Math.max(120, 180-(day-1)*5).</summary>
        public static float ShiftDurationForDay(int forDay)
        {
            return Mathf.Max(120f, 180f - (forDay - 1) * 5f);
        }

        /// <summary>MVP: Math.max(30, 48-day*2).</summary>
        public static float TruckIntervalForDay(int forDay)
        {
            return Mathf.Max(30f, 48f - forDay * 2f);
        }

        /// <summary>MVP: Math.min(4+day*2, 12).</summary>
        public static int BoxesPerTruckForDay(int forDay)
        {
            return Mathf.Min(4 + forDay * 2, 12);
        }

        /// <summary>MVP: Math.max(3.2, 8-day).</summary>
        public static float CustomerSpawnIntervalForDay(int forDay)
        {
            return Mathf.Max(3.2f, 8f - forDay);
        }

        /// <summary>MVP: 34 - Math.min(day*1.5, 10).</summary>
        public static float BasePatienceForDay(int forDay)
        {
            return 34f - Mathf.Min(forDay * 1.5f, 10f);
        }
    }
}
