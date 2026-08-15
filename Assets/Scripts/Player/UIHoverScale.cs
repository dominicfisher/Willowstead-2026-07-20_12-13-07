using UnityEngine;
using UnityEngine.EventSystems;

namespace Willowstead.Player
{
    /// <summary>
    /// A simple UI component that triggers a smooth scaling bounce effect when hovered by the mouse pointer.
    /// Reusable on any UI buttons or panels.
    /// </summary>
    public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Vector3 _targetScale = Vector3.one;

        private void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * 14f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _targetScale = new Vector3(1.08f, 1.08f, 1.08f); // 8% scale pop
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _targetScale = Vector3.one;
        }
    }
}
