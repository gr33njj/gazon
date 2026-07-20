using System.Collections.Generic;
using UnityEngine;

namespace Gazon.Core
{
    /// <summary>Ночная инвентаризация в конце смены: 4 фиксированные "камеры", 1 верная улика.</summary>
    public class NightInventoryController : MonoBehaviour
    {
        public static NightInventoryController Instance { get; private set; }

        public struct Frame
        {
            public string time;
            public string text;
            public bool isCorrect;
            public Frame(string time, string text, bool isCorrect)
            {
                this.time = time; this.text = text; this.isCorrect = isCorrect;
            }
        }

        private static readonly Frame[] BaseFrames =
        {
            new Frame("03:12", "Курьер зевает 14 секунд подряд. Впечатляет, но не улика.", false),
            new Frame("07:45", "Кот. Просто кот. Откуда в ПВЗ кот?", false),
            new Frame("12:30", "Напарник Санёк что-то прячет в шкафчик. Похоже на чехол Poco X7 Pro Max 48px.", true),
            new Frame("18:02", "Вы залипаете в телефон. Это улика, но против вас.", false),
        };

        public List<Frame> CurrentFrames { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnShiftEnded.AddListener(OnShiftEnded);
        }

        private void OnShiftEnded()
        {
            CurrentFrames = new List<Frame>(BaseFrames);
            Shuffle(CurrentFrames);
        }

        public void Answer(int index)
        {
            if (CurrentFrames == null || index < 0 || index >= CurrentFrames.Count) return;
            bool correct = CurrentFrames[index].isCorrect;
            CurrentFrames = null;

            if (correct)
            {
                GameManager.Instance.Earn(50f);
                GameManager.Instance.Toast("📹 Товар найден в шкафчике Санька. Санёк говорит «это не то, чем кажется». +50 ₽", "good");
            }
            else
            {
                GameManager.Instance.Fine(100f, "Недостача не найдена. Вычли из ЗП");
            }

            GameManager.Instance.ShowDaySummary();
        }

        private static void Shuffle(IList<Frame> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
