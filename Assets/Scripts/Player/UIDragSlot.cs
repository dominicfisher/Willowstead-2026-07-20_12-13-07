using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Willowstead.Player
{
    /// <summary>
    /// Handles drag-and-drop actions and pointer hover animations for individual inventory and hotbar slots.
    /// Programmatically tracks mouse positions, spawns click/drag ghosts, and triggers slot content swaps.
    /// </summary>
    public class UIDragSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("The slot index inside InventoryManager slots array that this UI slot represents.")]
        public int slotIndex;

        private Vector3 _targetScale = Vector3.one;
        private GameObject _ghostGo;
        private Image _originalIcon;
        private Color _originalIconColor;

        private void Start()
        {
            // Find the child icon image
            _originalIcon = transform.Find("Icon")?.GetComponent<Image>();
            if (_originalIcon == null)
            {
                _originalIcon = GetComponentInChildren<Image>();
            }

            if (_originalIcon != null)
            {
                _originalIconColor = _originalIcon.color;
            }
        }

        private void Update()
        {
            // Smoothly animate hover scale transitions
            transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * 14f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Hover scale bounce
            _targetScale = new Vector3(1.08f, 1.08f, 1.08f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _targetScale = Vector3.one;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (InventoryManager.Instance == null) return;

            InventorySlot slot = InventoryManager.Instance.GetSlotItem(slotIndex);
            if (slot == null || slot.IsEmpty) return;

            // Find main canvas to parent the drag ghost
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null) return;

            // Spawn the Drag Ghost GameObject
            _ghostGo = new GameObject("DragGhostIcon");
            _ghostGo.transform.SetParent(rootCanvas.transform, false);
            _ghostGo.transform.SetAsLastSibling(); // Render on top of everything

            Image ghostImage = _ghostGo.AddComponent<Image>();
            if (_originalIcon != null)
            {
                ghostImage.sprite = _originalIcon.sprite;
                ghostImage.color = _originalIcon.color;
            }
            ghostImage.raycastTarget = false; // Don't block raycasting under drop position

            RectTransform ghostRect = _ghostGo.GetComponent<RectTransform>();
            ghostRect.sizeDelta = new Vector2(35f, 35f); // Match slot size

            // Make original icon translucent during drag
            if (_originalIcon != null)
            {
                _originalIcon.color = new Color(_originalIconColor.r, _originalIconColor.g, _originalIconColor.b, 0.4f);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghostGo != null && UnityEngine.InputSystem.Mouse.current != null)
            {
                // Update ghost position to mouse cursor
                Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                _ghostGo.transform.position = mousePos;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Destroy drag ghost icon
            if (_ghostGo != null)
            {
                Destroy(_ghostGo);
                _ghostGo = null;
            }

            // Restore original icon opacity
            if (_originalIcon != null)
            {
                _originalIcon.color = _originalIconColor;
            }

            if (EventSystem.current == null || UnityEngine.InputSystem.Mouse.current == null) return;

            // Raycast UI under cursor to find the drop target slot
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = UnityEngine.InputSystem.Mouse.current.position.ReadValue()
            };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                UIDragSlot targetSlot = result.gameObject.GetComponentInParent<UIDragSlot>();
                if (targetSlot != null)
                {
                    if (targetSlot != this)
                    {
                        // Swap slots
                        InventoryManager.Instance.SwapSlots(slotIndex, targetSlot.slotIndex);

                        // Refresh both UI displays
                        FindAnyObjectByType<InventoryUI>()?.RefreshUI();
                        FindAnyObjectByType<HotbarUI>()?.RefreshUI();
                    }
                    break;
                }
            }
        }
    }
}
