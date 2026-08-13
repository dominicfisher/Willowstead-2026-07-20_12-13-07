using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Willowstead.Player
{
    /// <summary>
    /// Single source of truth for UI plumbing shared by every HUD script:
    ///   • loading built-in Unity sprites without console error spam,
    ///   • creating/normalizing the HUDCanvas + EventSystem,
    ///   • hit-testing the mouse against known overlay panels, and
    ///   • finding a child component by GameObject name.
    ///
    /// Centralised here so adding a new menu, shop, or HUD widget no longer
    /// means copy-pasting the same ~20-line canvas boot-up into another file.
    /// </summary>
    public static class UIResourceHelper
    {
        // ─── Sprite helpers ───────────────────────────────────────────────

        public static Sprite GetBackgroundSprite()
        {
#if UNITY_EDITOR
            Sprite sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            if (sprite != null) return sprite;
#endif
            // Safe fallback at runtime or if AssetDatabase fails to load the resource
            return LoadBuiltinSpriteWithFallback("UI/Skin/Background.psd");
        }

        public static Sprite GetInputFieldBackgroundSprite()
        {
#if UNITY_EDITOR
            Sprite sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
            if (sprite != null) return sprite;
#endif
            // Safe fallback at runtime or if AssetDatabase fails to load the resource
            return LoadBuiltinSpriteWithFallback("UI/Skin/InputFieldBackground.psd");
        }

        private static Sprite LoadBuiltinSpriteWithFallback(string path)
        {
            Sprite sprite = null;
            try
            {
                sprite = Resources.GetBuiltinResource<Sprite>(path);
            }
            catch (System.Exception)
            {
                // Catch any exception to prevent console spam
            }

            if (sprite == null)
            {
                sprite = CreateFallbackSprite();
            }
            return sprite;
        }

        private static Sprite _cachedSaveIconSprite;

        public static Sprite GetSaveIconSprite()
        {
            if (_cachedSaveIconSprite != null) return _cachedSaveIconSprite;

            int width = 16, height = 16;
            Texture2D tex = new Texture2D(width, height);
            tex.filterMode = FilterMode.Point;
            Color transparent = new Color(0, 0, 0, 0);
            Color blue = new Color(0.25f, 0.55f, 0.95f, 1f);
            Color silver = new Color(0.85f, 0.85f, 0.9f, 1f);
            Color darkSilver = new Color(0.45f, 0.45f, 0.5f, 1f);
            Color labelWhite = new Color(0.95f, 0.95f, 0.95f, 1f);
            Color darkBlue = new Color(0.1f, 0.25f, 0.5f, 1f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (x < 1 || x > 14 || y < 1 || y > 14) { tex.SetPixel(x, y, transparent); continue; }
                    if (x == 14 && y == 14) { tex.SetPixel(x, y, transparent); continue; }
                    if (x == 1 || x == 14 || y == 1 || y == 14) { tex.SetPixel(x, y, darkBlue); continue; }

                    if (y >= 10 && y <= 13 && x >= 4 && x <= 11)
                    {
                        if (x >= 6 && x <= 8 && y >= 11 && y <= 12)
                            tex.SetPixel(x, y, darkSilver);
                        else
                            tex.SetPixel(x, y, silver);
                        continue;
                    }

                    if (y >= 3 && y <= 7 && x >= 4 && x <= 11)
                    {
                        tex.SetPixel(x, y, labelWhite);
                        continue;
                    }

                    tex.SetPixel(x, y, blue);
                }
            }
            tex.Apply();
            _cachedSaveIconSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
            return _cachedSaveIconSprite;
        }

        private static Sprite _cachedCircleSprite;

        public static Sprite GetCircleSprite()
        {
            if (_cachedCircleSprite != null) return _cachedCircleSprite;

            int radius = 16;
            int size = radius * 2;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                    if (dist <= radius - 0.5f)
                        tex.SetPixel(x, y, Color.white);
                    else
                        tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
            tex.Apply();
            _cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return _cachedCircleSprite;
        }

        private static System.Collections.Generic.Dictionary<string, Sprite> _iconCache = new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite GetItemIconSprite(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            if (_iconCache.TryGetValue(itemName, out Sprite cached) && cached != null) return cached;

#if UNITY_EDITOR
            string path = null;
            string subSpriteName = null;

            if (itemName == "Hoe") path = "Assets/Sprites/Hoe.png";
            else if (itemName == "Watering Can") path = "Assets/Sprites/Watering can.png";
            else if (itemName == "Axe") path = "Assets/Sprites/Axe.png";
            else if (itemName == "Wood" || itemName == "Log") path = "Assets/Sprites/log.png";
            else if (itemName == "Gold Coin" || itemName == "Coin") path = "Assets/Sprites/gold coin.png";
            
            // Crops.png slices according to the Crops Sprite Sheet Guide
            else if (itemName == "Carrot Seeds" || itemName == "Carrot Seed" || itemName == "Seed")
            {
                path = "Assets/Sprites/Crops.png";
                subSpriteName = "Crops_0_0"; // Row 0, Col 0: Seed Bag
            }
            else if (itemName == "Carrot")
            {
                path = "Assets/Sprites/Crops.png";
                subSpriteName = "Crops_8_0"; // Row 0, Col 8: Yield B
            }
            else if (itemName == "Potato Seeds" || itemName == "Potato Seed")
            {
                path = "Assets/Sprites/Crops.png";
                subSpriteName = "Crops_0_2"; // Row 2, Col 0: Seed Bag
            }
            else if (itemName == "Potato")
            {
                path = "Assets/Sprites/Crops.png";
                subSpriteName = "Crops_8_2"; // Row 2, Col 8: Yield B
            }
            else if (itemName == "Tomato Seeds" || itemName == "Tomato Seed")
            {
                path = "Assets/Sprites/Crops.png";
                subSpriteName = "Crops_0_6"; // Row 6, Col 0: Seed Bag
            }
            else if (itemName == "Tomato")
            {
                path = "Assets/Sprites/Crops.png";
                subSpriteName = "Crops_8_6"; // Row 6, Col 8: Yield B
            }
            else if (itemName == "Corn Seeds" || itemName == "Corn Seed")
            {
                path = "Assets/Sprites/Crops.png";
                subSpriteName = "Crops_0_9"; // Row 9, Col 0: Seed Bag
            }
            else if (itemName == "Corn")
            {
                path = "Assets/Sprites/Crops.png";
                subSpriteName = "Crops_8_9"; // Row 9, Col 8: Yield B
            }

            if (!string.IsNullOrEmpty(path))
            {
                if (!string.IsNullOrEmpty(subSpriteName))
                {
                    Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (Object obj in assets)
                    {
                        if (obj is Sprite sp && sp.name == subSpriteName)
                        {
                            _iconCache[itemName] = sp;
                            return sp;
                        }
                    }
                }
                else
                {
                    Sprite asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (asset != null)
                    {
                        _iconCache[itemName] = asset;
                        return asset;
                    }
                }
            }
#endif

            Sprite generated = CreateProceduralItemIcon(itemName);
            _iconCache[itemName] = generated;
            return generated;
        }

        private static Sprite CreateProceduralItemIcon(string itemName)
        {
            int size = 24;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            Color transparent = new Color(0, 0, 0, 0);

            Color mainColor;
            if (itemName == "Hoe") mainColor = new Color(0.7f, 0.45f, 0.25f, 1f);
            else if (itemName == "Watering Can") mainColor = new Color(0.2f, 0.55f, 0.9f, 1f);
            else if (itemName == "Axe") mainColor = new Color(0.6f, 0.65f, 0.7f, 1f);
            else if (itemName.Contains("Seed")) mainColor = new Color(0.35f, 0.75f, 0.35f, 1f);
            else if (itemName == "Carrot") mainColor = new Color(1.0f, 0.5f, 0.1f, 1f);
            else if (itemName == "Wood" || itemName == "Log") mainColor = new Color(0.55f, 0.35f, 0.2f, 1f);
            else mainColor = new Color(0.85f, 0.75f, 0.45f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x < 2 || x > size - 3 || y < 2 || y > size - 3)
                        tex.SetPixel(x, y, transparent);
                    else
                        tex.SetPixel(x, y, mainColor);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateFallbackSprite()
        {
            // Create a small 2x2 white texture and return it as a sprite
            Texture2D tex = new Texture2D(2, 2);
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    tex.SetPixel(x, y, Color.white);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }

        // ─── Canvas & EventSystem helpers ─────────────────────────────────

        /// <summary>
        /// Default name used when looking for an existing HUD canvas.
        /// </summary>
        private const string DefaultHUDCanvasName = "HUDCanvas";

        /// <summary>
        /// Names of overlay UI panels (shop, inventory, etc.) that should
        /// block world interaction when the pointer is over them.
        /// Extend this list when a new modal-style panel ships.
        /// </summary>
        private static readonly string[] KnownUIPanelNames = { "ShopPanel", "InventoryPanel" };

        /// <summary>
        /// Finds the HUD canvas by searching for any of <paramref name="searchNames"/>.
        /// If none of those exist, creates a new GameObject named "HUDCanvas" with
        /// Canvas (ScreenSpaceOverlay), CanvasScaler (1920x1080, MatchWidthOrHeight 0.5),
        /// and GraphicRaycaster. The CanvasScaler is always normalised, even if the
        /// canvas already existed, so callers don't need to re-tune it themselves.
        /// </summary>
        public static Canvas GetOrCreateHUDCanvas(params string[] searchNames)
        {
            string[] names = (searchNames != null && searchNames.Length > 0)
                ? searchNames
                : new[] { DefaultHUDCanvasName };

            GameObject canvasGo = null;
            for (int i = 0; i < names.Length; i++)
            {
                canvasGo = GameObject.Find(names[i]);
                if (canvasGo != null) break;
            }

            if (canvasGo == null)
            {
                canvasGo = new GameObject(DefaultHUDCanvasName);
                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
            }
            else if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // Normalise the scaler regardless of who created the canvas first.
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Draw HUD above any world-tint canvas (sortingOrder = -1) so the day/night
            // overlay can never darken inventory sprites or other panels.
            Canvas hudCanvas = canvasGo.GetComponent<Canvas>();
            hudCanvas.sortingOrder = 1;
            return hudCanvas;
        }

        /// <summary>
        /// Ensures an EventSystem exists in the scene with the new Input System
        /// input module attached. Returns the existing or newly-created EventSystem.
        /// Safe to call multiple times; idempotent.
        /// </summary>
        public static EventSystem EnsureEventSystem()
        {
            EventSystem es = Object.FindAnyObjectByType<EventSystem>();
            if (es != null) return es;

            GameObject esGo = new GameObject("EventSystem");
            es = esGo.AddComponent<EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            return es;
        }

        // ─── Pointer / hit-test helpers ───────────────────────────────────

        /// <summary>
        /// Returns true if the mouse pointer is currently blocked by any UI:
        /// first via the EventSystem raycast (any button selectable, etc.), then
        /// via a bounding-rect test against known overlay panels as a defensive
        /// fallback for cases where a panel doesn't block the raycaster.
        /// </summary>
        public static bool IsPointerOverAnyUI()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return true;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return false;
            return IsScreenPosOverAnyKnownPanel(mouse.position.ReadValue());
        }

        /// <summary>
        /// Returns true if <paramref name="screenPos"/> lies within any active
        /// overlay panel registered in <see cref="KnownUIPanelNames"/>.
        /// </summary>
        public static bool IsScreenPosOverAnyKnownPanel(Vector2 screenPos)
        {
            for (int i = 0; i < KnownUIPanelNames.Length; i++)
            {
                GameObject panel = GameObject.Find(KnownUIPanelNames[i]);
                if (panel == null || !panel.activeInHierarchy) continue;
                RectTransform rt = panel.GetComponent<RectTransform>();
                if (rt == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
                    return true;
            }
            return false;
        }

        // ─── Component lookup helper ──────────────────────────────────────

        /// <summary>
        /// Searches all child components of <paramref name="root"/> of type T,
        /// preferring GameObjects whose name matches one of <paramref name="names"/>
        /// in the order given. Returns the first match, or the first component of
        /// type T as a fallback. Returns null if root has no T descendants.
        /// </summary>
        public static T FindChildComponentByName<T>(Transform root, string[] names) where T : Component
        {
            if (root == null) return null;
            T[] all = root.GetComponentsInChildren<T>(true);
            if (all == null || all.Length == 0) return null;

            if (names != null)
            {
                for (int n = 0; n < names.Length; n++)
                {
                    string wanted = names[n];
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] != null && all[i].gameObject != null && all[i].gameObject.name == wanted)
                            return all[i];
                    }
                }
            }

            return all[0];
        }
    }
}
