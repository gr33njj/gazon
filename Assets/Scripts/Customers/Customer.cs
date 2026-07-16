using Gazon.Core;
using Gazon.World;
using UnityEngine;

namespace Gazon.Customers
{
    /// <summary>
    /// Клиент, привязанный к конкретной коробке на полке в момент спавна (см. CustomerSpawner).
    /// Упрощено относительно MVP: без архетипов (обычный/бабушка/шопоголичка) и без QTE —
    /// это Phase 1, паритет добавляется отдельными шагами (см. Docs/Architecture.md).
    /// </summary>
    public class Customer : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2.2f;

        public static int ActiveCount { get; private set; }

        public CustomerState State { get; private set; } = CustomerState.Entering;
        public Box OrderBox { get; set; }
        public WindowStation AssignedWindow { get; private set; }

        private float patience;
        private float exitZ;

        private void OnEnable() => ActiveCount++;
        private void OnDisable() => ActiveCount--;

        public void Initialize(Box orderBox, WindowStation window, float basePatience, float exitPositionZ)
        {
            OrderBox = orderBox;
            orderBox.OrderedBy = this;
            AssignedWindow = window;
            patience = basePatience;
            exitZ = exitPositionZ;
        }

        private void Update()
        {
            switch (State)
            {
                case CustomerState.Entering:
                    MoveTowards(AssignedWindow.CustomerStandPoint.position);
                    if (Vector3.Distance(transform.position, AssignedWindow.CustomerStandPoint.position) < 0.15f)
                    {
                        State = CustomerState.AtWindow;
                        AssignedWindow.AssignCustomer(this);
                    }
                    break;

                case CustomerState.AtWindow:
                    patience -= Time.deltaTime;
                    if (patience <= 0f)
                    {
                        GameManager.Instance.AddRating(-0.2f);
                        LeaveAngry();
                    }
                    break;

                case CustomerState.Leaving:
                    var exitPoint = new Vector3(transform.position.x, transform.position.y, exitZ);
                    MoveTowards(exitPoint);
                    if (Vector3.Distance(transform.position, exitPoint) < 0.15f)
                        Destroy(gameObject);
                    break;
            }
        }

        private void MoveTowards(Vector3 target)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
        }

        public void CompleteOrderAndLeave()
        {
            AssignedWindow.ClearCustomer(this);
            State = CustomerState.Leaving;
        }

        private void LeaveAngry()
        {
            if (OrderBox != null) OrderBox.OrderedBy = null;
            AssignedWindow.ClearCustomer(this);
            State = CustomerState.Leaving;
        }
    }
}
