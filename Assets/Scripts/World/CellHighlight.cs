using Gazon.Player;
using UnityEngine;

namespace Gazon.World
{
    /// <summary>
    /// Подсвечивает ячейку, в которую нужно нести несомую (не возвратную) коробку — зелёный
    /// маркер на полке + подпрыгивающий вращающийся индикатор над ним. Аналог hlBox/hlArrow
    /// из MVP (там были полупрозрачными — здесь сплошной цвет, чтобы не настраивать прозрачный
    /// материал URP вслепую без Unity Editor под рукой).
    /// </summary>
    public class CellHighlight : MonoBehaviour
    {
        [SerializeField] private Transform marker;
        [SerializeField] private Transform arrow;

        private void Update()
        {
            var pi = PlayerInteraction.Instance;
            var box = pi != null ? pi.CarriedBox : null;
            bool show = box != null && !box.IsReturn && box.AssignedCell != null;

            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
            if (!show) return;

            var cellPos = box.AssignedCell.transform.position;
            marker.position = cellPos;
            arrow.position = cellPos + new Vector3(0f, 1.1f + Mathf.Sin(Time.time * 4f) * 0.08f, 0f);
            arrow.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);
        }
    }
}
