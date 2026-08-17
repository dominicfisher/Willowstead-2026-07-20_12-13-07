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
        private static Font _cachedPixelFont;

        /// <summary>
        /// Loads the Minecraftia pixel font (Assets/Fonts/Minecraftia-Regular.ttf) with graceful fallbacks.
        /// </summary>
        public static Font GetPixelFont()
        {
            if (_cachedPixelFont != null) return _cachedPixelFont;

#if UNITY_EDITOR
            _cachedPixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Minecraftia-Regular.ttf");
            if (_cachedPixelFont != null) return _cachedPixelFont;

            _cachedPixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/upheavtt.ttf");
            if (_cachedPixelFont != null) return _cachedPixelFont;
#endif
            _cachedPixelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_cachedPixelFont == null) _cachedPixelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _cachedPixelFont;
        }

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

        private static Sprite _cachedSparkleStarSprite;

        public static Sprite GetSparkleStarSprite()
        {
            if (_cachedSparkleStarSprite != null) return _cachedSparkleStarSprite;

            int size = 16;
            Texture2D tex = new Texture2D(size, size);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color transparent = new Color(0, 0, 0, 0);
            Color white = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, transparent);
                }
            }

            // Beautiful 4-pointed radiant sparkle star
            // Center core
            tex.SetPixel(7, 7, white);
            tex.SetPixel(8, 7, white);
            tex.SetPixel(7, 8, white);
            tex.SetPixel(8, 8, white);

            // Vertical rays
            for (int y = 2; y <= 13; y++)
            {
                tex.SetPixel(7, y, white);
                tex.SetPixel(8, y, white);
            }
            // Horizontal rays
            for (int x = 2; x <= 13; x++)
            {
                tex.SetPixel(x, 7, white);
                tex.SetPixel(x, 8, white);
            }

            // Diagonal inner glints
            tex.SetPixel(6, 6, new Color(1f, 1f, 1f, 0.75f));
            tex.SetPixel(9, 6, new Color(1f, 1f, 1f, 0.75f));
            tex.SetPixel(6, 9, new Color(1f, 1f, 1f, 0.75f));
            tex.SetPixel(9, 9, new Color(1f, 1f, 1f, 0.75f));

            tex.Apply();
            _cachedSparkleStarSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
            return _cachedSparkleStarSprite;
        }

        private static Sprite _cachedHpBarSprite;
        private static Sprite _cachedHpBgSprite;
        private static Sprite _cachedManaBarSprite;
        private static Sprite _cachedManaBgSprite;
        private static Sprite _cachedExpBarSprite;
        private static Sprite _cachedExpBgSprite;

        public static Sprite GetHealthBarSprite()
        {
            if (_cachedHpBarSprite != null) return _cachedHpBarSprite;
#if UNITY_EDITOR
            _cachedHpBarSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Bars/2/hp.png");
            if (_cachedHpBarSprite != null) return _cachedHpBarSprite;
#endif
            return GetBackgroundSprite();
        }

        public static Sprite GetHealthBarBackgroundSprite()
        {
            if (_cachedHpBgSprite != null) return _cachedHpBgSprite;
#if UNITY_EDITOR
            _cachedHpBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Bars/2/hp  background.png");
            if (_cachedHpBgSprite != null) return _cachedHpBgSprite;
#endif
            return GetInputFieldBackgroundSprite();
        }

        public static Sprite GetStaminaBarSprite()
        {
            if (_cachedExpBarSprite != null) return _cachedExpBarSprite;
#if UNITY_EDITOR
            // exp.png is green/gold pixel bar or mana.png is blue
            _cachedExpBarSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Bars/2/exp.png");
            if (_cachedExpBarSprite != null) return _cachedExpBarSprite;
#endif
            return GetBackgroundSprite();
        }

        public static Sprite GetStaminaBarBackgroundSprite()
        {
            if (_cachedExpBgSprite != null) return _cachedExpBgSprite;
#if UNITY_EDITOR
            _cachedExpBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Bars/2/exp background.png");
            if (_cachedExpBgSprite != null) return _cachedExpBgSprite;
#endif
            return GetInputFieldBackgroundSprite();
        }

        private static Sprite[] _cachedQuestBookSprites = new Sprite[5];

        public static Sprite GetQuestBookSprite() => GetItemOrQuestBookSprite(1);

        public static Sprite GetItemOrQuestBookSprite(int bookIndex = 1)
        {
            bookIndex = Mathf.Clamp(bookIndex, 1, 4);
            if (_cachedQuestBookSprites[bookIndex] != null) return _cachedQuestBookSprites[bookIndex];

#if UNITY_EDITOR
            string path = $"Assets/Sprites/Book/Item or quest book{bookIndex}.png";
            Object[] subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (subAssets != null)
            {
                foreach (var obj in subAssets)
                {
                    if (obj is Sprite s && (s.name == $"Item or quest book{bookIndex}_0" || s.name == $"Item or quest book{bookIndex}"))
                    {
                        _cachedQuestBookSprites[bookIndex] = s;
                        return s;
                    }
                }
            }

            Sprite single = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (single != null)
            {
                _cachedQuestBookSprites[bookIndex] = single;
                return single;
            }
#endif
            _cachedQuestBookSprites[bookIndex] = GetBackgroundSprite();
            return _cachedQuestBookSprites[bookIndex];
        }

        private static System.Collections.Generic.Dictionary<string, Sprite> _iconCache = new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite GetItemIconSprite(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            // 1. Check Inspector-configured ItemDatabase ScriptableObject first
            if (Willowstead.Inventory.ItemDatabase.Instance != null)
            {
                if (Willowstead.Inventory.ItemDatabase.Instance.TryGetIcon(itemName, out Sprite dbSprite) && dbSprite != null)
                {
                    return dbSprite;
                }
            }

            if (_iconCache.TryGetValue(itemName, out Sprite cached) && cached != null) return cached;

#if UNITY_EDITOR
            string path = null;
            string subSpriteName = null;

            if (itemName == "Hoe") path = "Assets/Sprites/Hoe.png";
            else if (itemName == "Watering Can") path = "Assets/Sprites/Watering can.png";
            else if (itemName == "Axe") path = "Assets/Sprites/Axe.png";
            else if (itemName == "Wood" || itemName == "Log") path = "Assets/Sprites/log.png";
            else if (itemName == "Gold Coin" || itemName == "Coin") path = "Assets/Sprites/gold coin.png";
            
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
            else if (itemName == "Fertilizer") mainColor = new Color(0.35f, 0.85f, 0.35f, 1f); // Vibrant emerald fertilizer
            else if (itemName.StartsWith("Rotten", System.StringComparison.OrdinalIgnoreCase) || itemName.Contains("Bad"))
                mainColor = new Color(0.45f, 0.38f, 0.28f, 1f); // Dark withered brown
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


        /// <summary>
        /// Default name used when looking for an existing HUD canvas.
        /// </summary>
        private const string DefaultHUDCanvasName = "HUDCanvas";

        /// <summary>
        /// Names of overlay UI panels (shop, inventory, etc.) that should
        /// block world interaction when the pointer is over them.
        /// Extend this list when a new modal-style panel ships.
        /// </summary>
        private static readonly string[] KnownUIPanelNames = { "ShopPanel", "InventoryPanel", "BuildMenuPanel", "SkillsJournalPanel", "WorldSetupPanel" };

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
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

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


        /// <summary>
        /// Returns true if the mouse pointer is currently blocked by any UI:
        /// first via the EventSystem raycast (any button selectable, etc.), then
        /// via a bounding-rect test against known overlay panels as a defensive
        /// fallback for cases where a panel doesn't block the raycaster.
        /// </summary>
        public static bool IsPointerOverAnyUI()
        {
            if (UIDragSlot.IsDragging) return true;

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
