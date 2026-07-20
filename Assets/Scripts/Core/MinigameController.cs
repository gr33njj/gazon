using System.Collections.Generic;
using System.Linq;
using Gazon.Customers;
using Gazon.World;
using UnityEngine;

namespace Gazon.Core
{
    /// <summary>
    /// Три мини-игры у окна выдачи: крокодил с бабушкой, «Глаз Бога» (без кода), QTE при выдаче.
    /// Одновременно активна максимум одна из каждого вида (как в MVP — глобальные babkaGame/eyeGame/qte).
    /// Состояние читается GameUI для рендера панелей и роутинга клавиш 1-3 / f-g-h-j-k-l.
    /// </summary>
    public class MinigameController : MonoBehaviour
    {
        public static MinigameController Instance { get; private set; }

        private static readonly string[] ColorNames =
        {
            "красной", "синей", "зелёной", "жёлтой", "розовой", "фиолетовой"
        };

        // --- Крокодил ---
        public Customer BabkaTarget { get; private set; }
        public List<BabkaItemData> BabkaOptions { get; private set; }
        public int BabkaCorrectIndex { get; private set; }
        public string BabkaFeedback { get; private set; } = "";

        // --- Глаз Бога ---
        public Customer EyeTarget { get; private set; }
        public List<(string name, bool correct)> EyeOptions { get; private set; }

        // --- QTE ---
        public Customer QteTarget { get; private set; }
        public Box QteBox { get; private set; }
        public WindowStation QteWindow { get; private set; }
        public string QteKey { get; private set; }
        public float QteElapsed { get; private set; }
        public const float QteDuration = 1.15f;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            // Автозакрытие, если клиент ушёл из окна, пока была открыта панель (см. MVP: c.state!=='window').
            if (BabkaTarget != null && BabkaTarget.State != CustomerState.AtWindow) CloseBabka();
            if (EyeTarget != null && EyeTarget.State != CustomerState.AtWindow) CloseEye();

            if (QteTarget != null)
            {
                if (QteTarget.State != CustomerState.AtWindow)
                {
                    CancelQte();
                    return;
                }
                QteElapsed += Time.deltaTime;
                if (QteElapsed >= QteDuration) ResolveQte(false);
            }
        }

        // ---------- Крокодил ----------

        public void StartBabka(Customer customer)
        {
            if (BabkaTarget != null) return;
            BabkaTarget = customer;
            BabkaFeedback = "";

            var wrong = ContentDatabase.RandomBabkaItems(2, customer.Item);
            var options = new List<BabkaItemData> { customer.Item };
            options.AddRange(wrong);
            Shuffle(options);
            BabkaOptions = options;
            BabkaCorrectIndex = options.FindIndex(o => o.name == customer.Item.name);

            InputLock.BabkaOpen = true;
        }

        public void AnswerBabka(int index)
        {
            if (BabkaTarget == null) return;
            if (BabkaTarget.State != CustomerState.AtWindow) { CloseBabka(); return; }

            if (index == BabkaCorrectIndex)
            {
                BabkaTarget.Guessed = true;
                GameManager.Instance.Toast("🧓 Угадал! Бабушка довольна.", "good");
                CloseBabka();
                return;
            }

            BabkaTarget.Wrongs++;
            BabkaTarget.ApplyPatiencePenalty(6f);

            if (BabkaTarget.Wrongs >= 2)
            {
                GameManager.Instance.AddRating(-0.1f);
                GameManager.Instance.Toast("🧓 Бабушка ушла вспоминать. −0.1 ★", "bad");
                BabkaTarget.LeaveWithoutOrder();
                CloseBabka();
            }
            else
            {
                BabkaFeedback = "«Не-е-ет, не то…» (осталась 1 попытка)";
            }
        }

        public void CloseBabka()
        {
            BabkaTarget = null;
            BabkaOptions = null;
            InputLock.BabkaOpen = false;
        }

        // ---------- Глаз Бога ----------

        public void StartEye(Customer customer)
        {
            if (EyeTarget != null) return;
            EyeTarget = customer;

            var others = ColorNames.Where(c => c != customer.ColorName)
                .OrderBy(_ => Random.value).Take(2).ToList();
            var opts = new List<(string, bool)> { (customer.ColorName, true) };
            foreach (var o in others) opts.Add((o, false));
            Shuffle(opts);
            EyeOptions = opts;

            InputLock.EyeOpen = true;
        }

        public void AnswerEye(int index)
        {
            if (EyeTarget == null) return;
            if (EyeTarget.State != CustomerState.AtWindow) { CloseEye(); return; }

            if (EyeOptions[index].correct)
            {
                EyeTarget.CodeFixed = true;
                GameManager.Instance.Toast("👁 Пробит. Код восстановлен. Как в старые добрые.", "good");
                CloseEye();
            }
            else
            {
                EyeTarget.ApplyPatiencePenalty(5f);
                GameManager.Instance.Toast("Не тот профиль. Очередь агрится.", "warn");
            }
        }

        public void CloseEye()
        {
            EyeTarget = null;
            EyeOptions = null;
            InputLock.EyeOpen = false;
        }

        // ---------- QTE ----------

        private static readonly string[] QteKeys = { "f", "g", "h", "j", "k", "l" };

        public void StartQte(WindowStation window, Customer customer, Box box)
        {
            QteWindow = window;
            QteTarget = customer;
            QteBox = box;
            QteElapsed = 0f;
            QteKey = QteKeys[Random.Range(0, QteKeys.Length)];
            InputLock.QteOpen = true;
        }

        public void ResolveQte(bool ok)
        {
            var window = QteWindow;
            var customer = QteTarget;
            var box = QteBox;
            CancelQte();

            if (customer == null || customer.State != CustomerState.AtWindow)
            {
                GameManager.Instance.Toast("Клиент уже ушёл. Коробка осталась.", "warn");
                return;
            }

            if (ok)
            {
                GameManager.Instance.Toast("🎯 Идеальная выдача!", "good");
            }
            else
            {
                GameManager.Instance.Fine(30f, "Помятая коробка при выдаче");
            }
            window.CompleteHandOver(box);
        }

        private void CancelQte()
        {
            QteWindow = null;
            QteTarget = null;
            QteBox = null;
            InputLock.QteOpen = false;
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
