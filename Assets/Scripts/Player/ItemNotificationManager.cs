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
        private static ItemNotificationManager _instance;
        public static ItemNotificationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<ItemNotificationManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[ItemNotificationManager]");
                        _instance = go.AddComponent<ItemNotificationManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Assets")]
        [Tooltip("The icon sprite to represent Carrot Seeds.")]
        [SerializeField] private Sprite _seedIcon;

        [Tooltip("The icon sprite to represent Carrots.")]
        [SerializeField] private Sprite _carrotIcon;

        [Tooltip("The icon/circle sprite to represent Gold.")]
        [SerializeField] private Sprite _coinIcon;

        [Tooltip("The icon sprite to represent a stack of Logs (dropped from trees).")]
        [SerializeField] private Sprite _logIcon;

        private GameObject _canvasGo;
        private GameObject _containerGo;
        private RectTransform _containerRect;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_seedIcon == null) _seedIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/CarrotSeed.png");
            if (_carrotIcon == null) _carrotIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Carrot.png");
            if (_coinIcon == null) _coinIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/gold coin.png");
            if (_logIcon == null) _logIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/log.png");
        }
#endif

        private void Awake()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
                CreateContainer();
            }
            else
            {
                Destroy(this);
            }
        }

        private void CreateContainer()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            _canvasGo = canvas != null ? canvas.gameObject : null;

            _containerGo = new GameObject("NotificationContainer");
            _containerGo.transform.SetParent(_canvasGo.transform, false);

            _containerRect = _containerGo.AddComponent<RectTransform>();
            _containerRect.anchorMin = new Vector2(1f, 0f);
            _containerRect.anchorMax = new Vector2(1f, 0f);
            _containerRect.pivot = new Vector2(1f, 0f);
            
            _containerRect.anchoredPosition = new Vector2(-15f, 110f); 
            _containerRect.sizeDelta = new Vector2(200f, 300f);

            VerticalLayoutGroup layout = _containerGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.LowerRight; // Stack from bottom up
        }

        /// <summary>
        /// Triggers a toast notification popup in the bottom right of the screen with arbitrary text and icon.
        /// </summary>
        public void TriggerNotification(string textContent, Sprite icon = null, Color? iconColor = null)
        {
            if (_containerRect == null) CreateContainer();

            if (icon == null) icon = UIResourceHelper.GetSaveIconSprite();
            Color col = iconColor ?? Color.white;

            GameObject rowGo = new GameObject("NotificationRow");
            rowGo.transform.SetParent(_containerRect, false);

            RectTransform rect = rowGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(210f, 38f);

            NotificationItem rowItem = rowGo.AddComponent<NotificationItem>();
            rowItem.Initialize(icon, textContent, col);
        }

        /// <summary>
        /// Triggers a toast notification popup in the bottom right of the screen.
        /// </summary>
        public void TriggerPickupNotification(string itemName, int amount)
        {
            if (_containerRect == null) CreateContainer();

            Sprite icon = null;
            Color iconColor = Color.white;

            // Check inspector overrides first, then global ItemDatabase / UIResourceHelper
            if (itemName == "Carrot Seeds" && _seedIcon != null) icon = _seedIcon;
            else if (itemName == "Carrot" && _carrotIcon != null) icon = _carrotIcon;
            else if (itemName == "Gold" && _coinIcon != null)
            {
                icon = _coinIcon;
                iconColor = new Color(1.0f, 0.82f, 0.0f, 1f);
            }
            else if (itemName == "Log" && _logIcon != null) icon = _logIcon;
            else
            {
                icon = UIResourceHelper.GetItemIconSprite(itemName);
                if (itemName == "Gold") iconColor = new Color(1.0f, 0.82f, 0.0f, 1f);
            }

            if (icon == null) icon = UIResourceHelper.GetInputFieldBackgroundSprite();

            GameObject rowGo = new GameObject("NotificationRow");
            rowGo.transform.SetParent(_containerRect, false);

            RectTransform rect = rowGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(210f, 38f);

            NotificationItem rowItem = rowGo.AddComponent<NotificationItem>();
            string sign = amount > 0 ? "+" : "";
            rowItem.Initialize(icon, $"{sign}{amount} {itemName}", iconColor);
        }
    }
}
