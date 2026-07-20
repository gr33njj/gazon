using UnityEngine;

namespace Gazon.Core
{
    /// <summary>
    /// Геометрия комнаты 1:1 из gamedev/pvz_simulator_mvp.html (константы ROOM/RACK_DEFS/DOCK/...).
    /// Общий источник координат и для SceneBuilder (строит геометрию), и для рантайм-скриптов
    /// (очередь клиентов, точки входа/выхода и т.п.) — чтобы числа не расходились между собой.
    /// </summary>
    public static class RoomLayout
    {
        public const float RoomWidth = 22f;   // X
        public const float RoomDepth = 13f;   // Z
        public const float RoomHeight = 3.2f;
        public const float EyeHeight = 1.7f;

        public struct RackDef
        {
            public float X0;
            public float Z;
            public string Label;
            public RackDef(float x0, float z, string label) { X0 = x0; Z = z; Label = label; }
        }

        public static readonly RackDef[] Racks =
        {
            new RackDef(6.5f, 3.0f, "А"),
            new RackDef(6.5f, 6.0f, "Б"),
            new RackDef(6.5f, 9.0f, "В"),
        };

        public const float RackLength = 6f;
        public const float RackDepth = 0.7f;
        public const int CellsPerRack = 8;
        public const float CellWidth = RackLength / CellsPerRack;
        public const float ShelfY = 1.15f;

        public const float DockX = 0.5f, DockZ = 2.0f, DockW = 3.8f, DockD = 6.0f;
        public const float DoorZ0 = 3.0f, DoorZ1 = 6.0f;
        public const float SmokeDoorX = 1.4f, SmokeDoorZ = 0f;
        public const float ReturnsX = 0.8f, ReturnsZ = 11.0f, ReturnsW = 2.2f, ReturnsD = 1.1f;
        public const float CounterX = 16.0f, CounterZ = 2.0f, CounterW = 0.6f, CounterD = 9.0f;
        public static readonly float[] WindowZ = { 4.5f, 8.5f };
        public const float EntranceZ0 = 5.2f, EntranceZ1 = 7.8f;
        public const float FittingX = 18.8f, FittingZ = 10.6f, FittingW = 2.4f, FittingD = 1.9f;
        public const float EnterX = 23.5f, ExitX = 24.5f;

        /// <summary>MVP: queuePos(i) — сетка мест в очереди перед стойкой (3 в ряд).</summary>
        public static Vector3 QueueSlot(int index)
        {
            float x = 18.2f + (index % 3) * 1.15f;
            float z = 2.4f + Mathf.Floor(index / 3f) * 1.1f;
            return new Vector3(x, 0f, z);
        }

        public static Vector3 FittingStandPosition => new Vector3(FittingX + FittingW / 2f, 0f, FittingZ - 0.5f);
    }
}
