using UnityEngine;
using UnityEngine.UI;

namespace Willowstead.Player
{
    /// <summary>
    /// Central manager for spawning item pickup/loss toast notifications.
    /// Creates a bottom-right vertical layout container where notifications stack bouncily from bottom to top.
    /// </summary>
    public class ItemNotificationManager : MonoBehaviour
    {
        public static ItemNotificationManager Instance { get; private set; }

        [Header("Assets")]
        [Tooltip("The icon sprite to represent Carrot Seeds.")]
        [SerializeField] private Sprite _seedIcon;

        [Tooltip("The icon sprite to represent Carrots.")]
        [SerializeField] private Sprite _carrotIcon;

        [Tooltip("The icon/circle sprite to represent Gold.")]
        [SerializeField] private Sprite _coinIcon;

        private GameObject _canvasGo;
        private GameObject _containerGo;
        private RectTransform _containerRect;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_seedIcon == null) _seedIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CarrotSeed.png");
            if (_carrotIcon == null) _carrotIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Carrot.png");
            if (_coinIcon == null) _coinIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/gold coin.png");
        }
#endif

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                CreateContainer();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void CreateContainer()
        {
            // Find or setup HUDCanvas
            _canvasGo = GameObject.Find("HUDCanvas");
            if (_canvasGo == null)
            {
                _canvasGo = new GameObject("HUDCanvas");
                Canvas canvas = _canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvasGo.AddComponent<CanvasScaler>();
                _canvasGo.AddComponent<GraphicRaycaster>();
            }

            // Create notification stacking group
            _containerGo = new GameObject("NotificationContainer");
            _containerGo.transform.SetParent(_canvasGo.transform, false);

            _containerRect = _containerGo.AddComponent<RectTransform>();
            _containerRect.anchorMin = new Vector2(1f, 0f);
            _containerRect.anchorMax = new Vector2(1f, 0f);
            _containerRect.pivot = new Vector2(1f, 0f);
            
            // Offset to sit comfortably in the bottom right, just above the Hotbar slot area
            _containerRect.anchoredPosition = new Vector2(-15f, 110f); 
            _containerRect.sizeDelta = new Vector2(200f, 300f);

            // Vertical Layout Group for automatic stacking and shifting
            VerticalLayoutGroup layout = _containerGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.LowerRight; // Stack from bottom up
        }

        /// <summary>
        /// Triggers a toast notification popup in the bottom right of the screen.
        /// </summary>
        public void TriggerPickupNotification(string itemName, int amount)
        {
            if (_containerRect == null) CreateContainer();

            Sprite icon = null;
            Color iconColor = Color.white;

            // Map item name to correct sprite
            if (itemName == "Carrot Seeds")
            {
                icon = _seedIcon;
            }
            else if (itemName == "Carrot")
            {
                icon = _carrotIcon;
            }
            else if (itemName == "Gold")
            {
                icon = _coinIcon;
                iconColor = new Color(1.0f, 0.82f, 0.0f, 1f); // Gold coin tint
            }

            // Default fallback if icon sprite is not loaded/assigned
            if (icon == null) icon = Resources.GetBuiltinResource<Sprite>("UI/Skin/InputFieldBackground.psd");

            // Create row row container
            GameObject rowGo = new GameObject("NotificationRow");
            rowGo.transform.SetParent(_containerRect, false);

            RectTransform rect = rowGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 34f);

            NotificationItem rowItem = rowGo.AddComponent<NotificationItem>();
            string sign = amount > 0 ? "+" : "";
            rowItem.Initialize(icon, $"{sign}{amount} {itemName}", iconColor);
        }
    }
}
