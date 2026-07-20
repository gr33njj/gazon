using UnityEngine;

namespace Gazon.World
{
    /// <summary>Маркер примерочной — точка, куда часть клиентов заходит перед уходом (см. Customer).</summary>
    public class FittingRoom : MonoBehaviour
    {
        public static FittingRoom Instance { get; private set; }

        [SerializeField] private Transform standPoint;

        public Vector3 StandPosition => standPoint != null ? standPoint.position : transform.position;

        private void Awake()
        {
            Instance = this;
        }
    }
}
