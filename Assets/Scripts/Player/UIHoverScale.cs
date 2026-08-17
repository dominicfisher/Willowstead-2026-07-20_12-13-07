using UnityEngine;
using UnityEngine.EventSystems;

namespace Willowstead.Player
{
    /// <summary>
    /// A simple UI component that triggers a smooth scaling bounce effect and plays menu audio
    /// when hovered or clicked by the mouse pointer.
    /// Reusable on all UI buttons, cards, and modal elements.
    /// </summary>
    public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private bool _playHoverSound = true;
        [SerializeField] private bool _playClickSound = true;
        [SerializeField] private float _hoverScale = 1.06f;

        private Vector3 _targetScale = Vector3.one;

        private void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.unscaledDeltaTime * 16f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _targetScale = new Vector3(_hoverScale, _hoverScale, _hoverScale);
            if (_playHoverSound)
            {
                UIResourceHelper.PlayMenuHoverSound();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _targetScale = Vector3.one;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_playClickSound)
            {
                UIResourceHelper.PlayMenuClickSound();
            }
        }
    }
}
