using System.Collections.Generic;
using Gazon.Core;
using Gazon.Player;
using Gazon.World;
using UnityEngine;

namespace Gazon.Customers
{
    /// <summary>
    /// Клиент. Архетипы (обычный/бабушка/шопоголичка), очередь у стойки, терпение, примерочная,
    /// Глаз Бога/крокодил — см. Docs/GDD.md. Пузырь реплики читается GameUI через CurrentBubble().
    /// </summary>
    public class Customer : MonoBehaviour
    {
        private static readonly List<Customer> ActiveList = new List<Customer>();
        public static IReadOnlyList<Customer> Active => ActiveList;
        public static int ActiveCount => ActiveList.Count;

        /// <summary>Для HUD: клиенты в зале, не считая тех, кто уже уходит (MVP: state!=='leave').</summary>
        public static int CountNotLeaving()
        {
            int n = 0;
            foreach (var c in ActiveList)
                if (c.State != CustomerState.Leaving) n++;
            return n;
        }

        [SerializeField] private float moveSpeed = 2.2f;

        public CustomerState State { get; private set; } = CustomerState.Queue;
        public Box OrderBox { get; set; }
        public WindowStation AssignedWindow { get; private set; }
        public CustomerArchetype Archetype { get; private set; }
        public BabkaItemData Item { get; private set; }
        public string ColorName { get; private set; }
        public bool NeedsCode { get; private set; }
        public bool CodeFixed { get; set; }
        public bool Guessed { get; set; }
        public int Wrongs { get; set; }
        public float Patience { get; private set; }
        public float MaxPatience { get; private set; }

        private float fitTimer;
        private string overridePhrase = "";
        private float overridePhraseTimer;
        private Vector3 target;

        private void OnEnable() => ActiveList.Add(this);
        private void OnDisable() => ActiveList.Remove(this);

        public void Initialize(Box orderBox, CustomerArchetype archetype, float basePatience,
            string colorName, bool needsCode, BabkaItemData item, int queueIndex)
        {
            OrderBox = orderBox;
            orderBox.OrderedBy = this;
            Archetype = archetype;
            Patience = basePatience;
            MaxPatience = basePatience;
            ColorName = colorName;
            NeedsCode = needsCode;
            Item = item;
            Guessed = archetype != CustomerArchetype.Babka;

            State = CustomerState.Queue;
            target = RoomLayout.QueueSlot(queueIndex);

            switch (archetype)
            {
                case CustomerArchetype.Babka:
                    SetPhrase("Внученька, я заказала… что-то…", 4f);
                    break;
                case CustomerArchetype.Shopaholic:
                    SetPhrase("Я на минуточку. Всё перемеряю.", 4f);
                    break;
                default:
                    SetPhrase(ContentDatabase.RandomDialogueLine("spawn", "normal"), 4f);
                    break;
            }
        }

        private void SetPhrase(string phrase, float seconds)
        {
            overridePhrase = phrase;
            overridePhraseTimer = seconds;
        }

        private void Update()
        {
            if (overridePhraseTimer > 0f) overridePhraseTimer -= Time.deltaTime;

            switch (State)
            {
                case CustomerState.Queue: UpdateQueue(); break;
                case CustomerState.AtWindow: UpdateAtWindow(); break;
                case CustomerState.Fitting: UpdateFitting(); break;
                case CustomerState.Leaving: UpdateLeaving(); break;
            }

            MoveTowards(target);
        }

        private void UpdateQueue()
        {
            var window = WindowStation.FindFreeWindow();
            if (window != null)
            {
                AssignedWindow = window;
                window.AssignCustomer(this);
                State = CustomerState.AtWindow;
                target = window.CustomerStandPoint.position;
                return;
            }

            target = RoomLayout.QueueSlot(IndexAmongQueued());
        }

        private int IndexAmongQueued()
        {
            int i = 0;
            foreach (var c in ActiveList)
            {
                if (c == this) return i;
                if (c.State == CustomerState.Queue) i++;
            }
            return i;
        }

        private void UpdateAtWindow()
        {
            // MVP: во время перекура терпение всех клиентов у окна тает вдвое быстрее.
            float mult = SmokeBreakController.Instance != null && SmokeBreakController.Instance.IsActive ? 2f : 1f;
            Patience -= Time.deltaTime * mult;
            if (Patience <= 0f) LeaveAngry();
        }

        private void UpdateFitting()
        {
            if (Vector3.Distance(transform.position, target) > 0.2f) return;
            fitTimer -= Time.deltaTime;
            if (fitTimer <= 0f) ResolveFitting();
        }

        private void UpdateLeaving()
        {
            if (Vector3.Distance(transform.position, target) < 0.15f)
                Destroy(gameObject);
        }

        private void MoveTowards(Vector3 targetPos)
        {
            // Раньше клиент двигался без поворота модели — с капсулой это было незаметно,
            // но с реальным гуманоидом (см. SceneBuilder.CreateCustomerPrefab) он бы скользил
            // лицом в одну сторону при любом направлении движения.
            var direction = targetPos - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0004f)
                transform.rotation = Quaternion.LookRotation(direction.normalized);

            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
        }

        /// <summary>Для мини-игр (крокодил/глаз бога) — неверный ответ подъедает терпение.</summary>
        public void ApplyPatiencePenalty(float amount)
        {
            Patience -= amount;
        }

        /// <summary>Клиент уходит, не получив заказ (например, бабушка не угадала товар дважды).</summary>
        public void LeaveWithoutOrder()
        {
            if (OrderBox != null) OrderBox.OrderedBy = null;
            AssignedWindow?.ClearCustomer(this);
            SetPhrase("Ай, сама вспомню. Завтра.", 2.5f);
            Leave();
        }

        /// <summary>Вызывается WindowStation-ом после успешной выдачи заказа.</summary>
        public void CompleteOrderAndLeave()
        {
            AssignedWindow?.ClearCustomer(this);

            if (Archetype == CustomerArchetype.Shopaholic)
            {
                EnterFitting();
                return;
            }

            // MVP: 28% шанс зайти в примерочную даже для обычных/бабушек после выдачи.
            if (Random.value < 0.28f)
            {
                EnterFitting();
                return;
            }

            SetPhrase(ContentDatabase.RandomDialogueLine("happy"), 2f);
            Leave();
        }

        private void EnterFitting()
        {
            State = CustomerState.Fitting;
            fitTimer = Archetype == CustomerArchetype.Shopaholic ? 4.0f : 2.6f;
            target = RoomLayout.FittingStandPosition;

            if (Archetype == CustomerArchetype.Shopaholic)
            {
                SetPhrase("Сейчас всё-всё перемеряю!", 3f);
                GameManager.Instance.Toast("👗 Она пошла ВСЁ перемерять. Улыбайся [R], пока не поздно!", "warn");
            }
            else
            {
                SetPhrase(ContentDatabase.RandomDialogueLine("fitting"), 3f);
            }
        }

        private void ResolveFitting()
        {
            bool playerSmiling = PlayerBuffs.Instance != null && PlayerBuffs.Instance.IsSmiling;

            if (Archetype == CustomerArchetype.Shopaholic)
            {
                int n = 4 + Random.Range(0, 3);
                for (int i = 0; i < n; i++)
                    ReturnSpawner.Instance.SpawnReturn();

                SetPhrase("Всё не подошло! Оформляйте!", 2.5f);
                if (playerSmiling)
                    GameManager.Instance.Toast($"😁 Вскрыла {n} коробок. Но вы улыбались — рейтинг спасён!", "good");
                else
                {
                    GameManager.Instance.AddRating(-0.3f);
                    GameManager.Instance.Toast($"📦×{n} Вскрыла всё, вернула всё. Вы не улыбались. −0.3 ★", "bad");
                }
            }
            else
            {
                ReturnSpawner.Instance.SpawnReturn();
                SetPhrase("Оформите возврат!", 2f);
                GameManager.Instance.Toast("📦 Возврат на стойке. Кто-то «просто глянул».", "warn");
            }

            Leave();
        }

        private void LeaveAngry()
        {
            SetPhrase(ContentDatabase.RandomDialogueLine("angry"), 2.5f);
            GameManager.Instance.RecordAngryCustomer();
            if (OrderBox != null) OrderBox.OrderedBy = null;

            if (GameManager.Instance.MatUpgrade && Random.value < 0.5f)
            {
                GameManager.Instance.AddRating(0.05f);
                GameManager.Instance.Toast("🗣 Вы ответили. [цензура]. Клиент проникся уважением. +0.05 ★", "good");
            }
            else
            {
                GameManager.Instance.AddRating(-0.2f);
                if (GameManager.Instance.MatUpgrade)
                    GameManager.Instance.Fine(40f, "Жалоба на лексику сотрудника");
                GameManager.Instance.Toast("😡 Клиент ушёл злой. Рейтинг падает.", "bad");
            }

            AssignedWindow?.ClearCustomer(this);
            Leave();
        }

        private void Leave()
        {
            State = CustomerState.Leaving;
            target = new Vector3(RoomLayout.ExitX, 0f, (RoomLayout.EntranceZ0 + RoomLayout.EntranceZ1) / 2f);
        }

        /// <summary>Текст в пузыре над головой — читается GameUI (billboard через OnGUI).</summary>
        public string CurrentBubble()
        {
            if (overridePhraseTimer > 0f) return overridePhrase;

            if (State == CustomerState.AtWindow)
            {
                if (Archetype == CustomerArchetype.Babka && !Guessed) return "Ну это… такое…";
                if (NeedsCode && !CodeFixed) return ContentDatabase.RandomDialogueLine("nocode", "normal");
                if (OrderBox != null)
                {
                    if (OrderBox.AssignedCell != null) return "Ячейка " + OrderBox.AssignedCell.Label;
                    if (OrderBox.State == BoxState.Carried) return "Уже несут!";
                }
            }

            return "…";
        }

        public bool BubbleIsAlert()
        {
            return (NeedsCode && !CodeFixed) || (Archetype == CustomerArchetype.Babka && !Guessed);
        }
    }
}
