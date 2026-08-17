using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Willowstead.Player
{
    /// <summary>
    /// Displays all 8 player skills inside an open quest book journal modal:
    /// Farming, Fishing, Mining, Woodcutting, Cooking, Ranching, Building, Exploring.
    /// Shows real-time Level, XP progress bar, exact XP fraction, and skill description.
    /// Uses standard Unity UI Text and fallback font loading to guarantee 100% reliable rendering.
    /// </summary>
    public class SkillsUI : MonoBehaviour
    {
        private static SkillsUI _instance;
        public static SkillsUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<SkillsUI>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[SkillsUI]");
                        DontDestroyOnLoad(go);
                        _instance = go.AddComponent<SkillsUI>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        private GameObject _canvasGo;
        private GameObject _panelGo;
        private bool _isOpen = false;
        private Coroutine _bounceCoroutine;

        private readonly Dictionary<SkillType, Image> _xpFillBars = new Dictionary<SkillType, Image>();
        private readonly Dictionary<SkillType, Text> _levelTexts = new Dictionary<SkillType, Text>();
        private readonly Dictionary<SkillType, Text> _xpFractionTexts = new Dictionary<SkillType, Text>();

        public bool IsOpen => _isOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            GameObject go = new GameObject("[SkillsUI]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SkillsUI>();
        }

        private void Awake()
        {
            if (_instance == null || _instance == this)
            {
                _instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }
        }

        private void Start()
        {
            CreateSkillsUI();
            SetUIActive(false);

            if (SkillsManager.Instance != null)
            {
                SkillsManager.Instance.OnSkillXPAdded += (type, cur, req) => RefreshSkillData();
                SkillsManager.Instance.OnSkillLevelUp += (type, lvl) => RefreshSkillData();
            }
        }

        private void Update()
        {
            // Toggle with 'K' key (Skills shortcut)
            bool kPressed = Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame;
            if (kPressed)
            {
                if (!Input.InputReader.BlockGameplayInput || _isOpen)
                {
                    ToggleUI();
                }
            }

            if (_isOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseUI();
            }
        }

        public void ToggleUI()
        {
            if (_panelGo == null)
            {
                CreateSkillsUI();
            }

            _isOpen = !_isOpen;
            SetUIActive(_isOpen);

            if (_isOpen)
            {
                InventoryUI invUI = Object.FindAnyObjectByType<InventoryUI>();
                if (invUI != null && invUI.IsOpen) invUI.CloseUI();

                ShopUI shopUI = Object.FindAnyObjectByType<ShopUI>();
                if (shopUI != null && shopUI.IsOpen) shopUI.CloseUI();

                RefreshSkillData();

                if (Willowstead.World.ObjectiveManager.Instance != null)
                {
                    Willowstead.World.ObjectiveManager.Instance.ReportProgress(Willowstead.World.ObjectiveId.CheckSkills, 1);
                }

                if (_panelGo != null)
                {
                    _panelGo.transform.SetAsLastSibling(); // Ensure on top of all other HUD elements
                    if (_bounceCoroutine != null) StopCoroutine(_bounceCoroutine);
                    _bounceCoroutine = StartCoroutine(PlayBounceAnimation(_panelGo.transform));
                }
            }
        }

        public void OpenUI()
        {
            if (!_isOpen) ToggleUI();
        }

        public void CloseUI()
        {
            if (_isOpen)
            {
                _isOpen = false;
                SetUIActive(false);
            }
        }

        private void SetUIActive(bool active)
        {
            if (_panelGo != null) _panelGo.SetActive(active);
            Input.InputReader.BlockGameplayInput = active;
        }

        private IEnumerator PlayBounceAnimation(Transform panelTransform)
        {
            float duration = 0.22f;
            float elapsed = 0f;
            panelTransform.localScale = new Vector3(0.75f, 0.75f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = t > 0.7f
                    ? Mathf.Lerp(1.08f, 1.0f, (t - 0.7f) / 0.3f)
                    : Mathf.Lerp(0.75f, 1.08f, t);
                panelTransform.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            panelTransform.localScale = Vector3.one;
            _bounceCoroutine = null;
        }

        public void RefreshSkillData()
        {
            if (SkillsManager.Instance == null) return;

            foreach (var kvp in SkillsManager.Instance.GetAllSkills())
            {
                SkillType type = kvp.Key;
                SkillData data = kvp.Value;

                if (_levelTexts.TryGetValue(type, out var lvlTxt) && lvlTxt != null)
                {
                    lvlTxt.text = $"Lvl {data.level}";
                }

                int req = data.XPForNextLevel;
                float fill = Mathf.Clamp01((float)data.currentXP / Mathf.Max(1, req));

                if (_xpFillBars.TryGetValue(type, out var fillImg) && fillImg != null)
                {
                    fillImg.fillAmount = fill;
                }

                if (_xpFractionTexts.TryGetValue(type, out var fracTxt) && fracTxt != null)
                {
                    fracTxt.text = $"{data.currentXP} / {req} XP";
                }
            }
        }

        private void CreateSkillsUI()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas();
            _canvasGo = canvas != null ? canvas.gameObject : null;
            UIResourceHelper.EnsureEventSystem();

            if (_canvasGo == null) return;

            Transform existing = _canvasGo.transform.Find("SkillsPanelRoot");
            if (existing != null) DestroyImmediate(existing.gameObject);

            Font font = UIResourceHelper.GetPixelFont();

            _panelGo = new GameObject("SkillsPanelRoot", typeof(RectTransform), typeof(Image));
            _panelGo.transform.SetParent(_canvasGo.transform, false);

            float panelW = 760f;
            float panelH = 540f;

            RectTransform rootRt = (RectTransform)_panelGo.transform;
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(panelW, panelH);
            rootRt.anchoredPosition = Vector2.zero;

            Image rootBg = _panelGo.GetComponent<Image>();
            rootBg.sprite = UIResourceHelper.GetQuestBookSprite();
            rootBg.type = Image.Type.Sliced;
            rootBg.color = Color.white;
            rootBg.raycastTarget = true;

            // Close 'X' Button on Top Right Corner of the Book
            GameObject closeGo = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button), typeof(UIHoverScale));
            closeGo.transform.SetParent(_panelGo.transform, false);
            RectTransform closeRt = (RectTransform)closeGo.transform;
            closeRt.anchorMin = new Vector2(1f, 1f); closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.sizeDelta = new Vector2(32f, 32f);
            closeRt.anchoredPosition = new Vector2(-28f, -22f);
            Image closeBg = closeGo.GetComponent<Image>();
            closeBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            closeBg.type = Image.Type.Sliced;
            closeBg.color = new Color(0.85f, 0.40f, 0.35f, 1f);

            GameObject closeLbl = new GameObject("Label", typeof(RectTransform));
            closeLbl.transform.SetParent(closeGo.transform, false);
            RectTransform cLblRt = (RectTransform)closeLbl.transform;
            cLblRt.anchorMin = Vector2.zero; cLblRt.anchorMax = Vector2.one;
            cLblRt.offsetMin = Vector2.zero; cLblRt.offsetMax = Vector2.zero;
            var cTxt = closeLbl.AddComponent<Text>();
            cTxt.font = font;
            cTxt.text = "✕";
            cTxt.fontSize = 15;
            cTxt.fontStyle = FontStyle.Bold;
            cTxt.color = Color.white;
            cTxt.alignment = TextAnchor.MiddleCenter;
            cTxt.raycastTarget = false;
            closeGo.GetComponent<Button>().onClick.AddListener(CloseUI);

            // Left Page Header (Title)
            GameObject leftHeadGo = new GameObject("LeftPageHeader", typeof(RectTransform));
            leftHeadGo.transform.SetParent(_panelGo.transform, false);
            RectTransform lHeadRt = (RectTransform)leftHeadGo.transform;
            lHeadRt.anchorMin = new Vector2(0f, 1f); lHeadRt.anchorMax = new Vector2(0.5f, 1f);
            lHeadRt.pivot = new Vector2(0.5f, 1f);
            lHeadRt.anchoredPosition = new Vector2(12f, -38f);
            lHeadRt.sizeDelta = new Vector2(0f, 30f);
            var lHeadTxt = leftHeadGo.AddComponent<Text>();
            lHeadTxt.font = font;
            lHeadTxt.text = "FIELD SKILLS";
            lHeadTxt.fontSize = 22;
            lHeadTxt.fontStyle = FontStyle.Bold;
            lHeadTxt.color = new Color(0.35f, 0.22f, 0.16f, 1f);
            lHeadTxt.alignment = TextAnchor.MiddleCenter;
            lHeadTxt.raycastTarget = false;

            // Right Page Header (Title)
            GameObject rightHeadGo = new GameObject("RightPageHeader", typeof(RectTransform));
            rightHeadGo.transform.SetParent(_panelGo.transform, false);
            RectTransform rHeadRt = (RectTransform)rightHeadGo.transform;
            rHeadRt.anchorMin = new Vector2(0.5f, 1f); rHeadRt.anchorMax = new Vector2(1f, 1f);
            rHeadRt.pivot = new Vector2(0.5f, 1f);
            rHeadRt.anchoredPosition = new Vector2(-12f, -38f);
            rHeadRt.sizeDelta = new Vector2(0f, 30f);
            var rHeadTxt = rightHeadGo.AddComponent<Text>();
            rHeadTxt.font = font;
            rHeadTxt.text = "CRAFT & DISCOVERY";
            rHeadTxt.fontSize = 22;
            rHeadTxt.fontStyle = FontStyle.Bold;
            rHeadTxt.color = new Color(0.35f, 0.22f, 0.16f, 1f);
            rHeadTxt.alignment = TextAnchor.MiddleCenter;
            rHeadTxt.raycastTarget = false;

            // ── Left Page Container (4 Skills: Farming, Fishing, Mining, Woodcutting) ──
            GameObject leftPageGo = new GameObject("LeftPageContainer", typeof(RectTransform));
            leftPageGo.transform.SetParent(_panelGo.transform, false);
            RectTransform leftRt = (RectTransform)leftPageGo.transform;
            leftRt.anchorMin = new Vector2(0f, 0f); leftRt.anchorMax = new Vector2(0.5f, 1f);
            leftRt.offsetMin = new Vector2(48f, 40f); leftRt.offsetMax = new Vector2(-28f, -74f);

            // ── Right Page Container (4 Skills: Cooking, Ranching, Building, Exploring) ──
            GameObject rightPageGo = new GameObject("RightPageContainer", typeof(RectTransform));
            rightPageGo.transform.SetParent(_panelGo.transform, false);
            RectTransform rightRt = (RectTransform)rightPageGo.transform;
            rightRt.anchorMin = new Vector2(0.5f, 0f); rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.offsetMin = new Vector2(28f, 40f); rightRt.offsetMax = new Vector2(-48f, -74f);

            SkillType[] skillList = new SkillType[]
            {
                SkillType.Farming,
                SkillType.Fishing,
                SkillType.Mining,
                SkillType.Woodcutting,
                SkillType.Cooking,
                SkillType.Ranching,
                SkillType.Building,
                SkillType.Exploring
            };

            string[] skillIcons = new string[] { "🌱", "🎣", "⛏", "🪓", "🍳", "🐄", "🔨", "🧭" };
            string[] skillDescriptions = new string[]
            {
                "Hoeing, watering & harvesting crops",
                "Catching fish from rivers & lakes",
                "Quarrying stone & precious ores",
                "Chopping timber from forest trees",
                "Preparing delicious farm recipes",
                "Caring for farmstead livestock",
                "Crafting & raising structures",
                "Traversing the frontier wilderness"
            };

            float cardW = 296f;
            float cardH = 86f;
            float gapY = 12f;

            _xpFillBars.Clear();
            _levelTexts.Clear();
            _xpFractionTexts.Clear();

            for (int i = 0; i < skillList.Length; i++)
            {
                SkillType skill = skillList[i];
                bool isLeftPage = i < 4;
                Transform parentPage = isLeftPage ? leftPageGo.transform : rightPageGo.transform;
                int pageRow = i % 4;
                float posY = -pageRow * (cardH + gapY);

                // Card Background (Parchment card with warm trim)
                GameObject cardGo = new GameObject($"SkillCard_{skill}", typeof(RectTransform), typeof(Image));
                cardGo.transform.SetParent(parentPage, false);
                RectTransform cRt = (RectTransform)cardGo.transform;
                cRt.anchorMin = new Vector2(0.5f, 1f); cRt.anchorMax = new Vector2(0.5f, 1f);
                cRt.pivot = new Vector2(0.5f, 1f);
                cRt.sizeDelta = new Vector2(cardW, cardH);
                cRt.anchoredPosition = new Vector2(0f, posY);

                Image cBg = cardGo.GetComponent<Image>();
                cBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                cBg.type = Image.Type.Sliced;
                cBg.color = new Color(0.96f, 0.90f, 0.82f, 0.92f); // Warm parchment card

                // Skill Badge (Left Emoji/Icon)
                GameObject badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(Image));
                badgeGo.transform.SetParent(cardGo.transform, false);
                RectTransform bRt = (RectTransform)badgeGo.transform;
                bRt.anchorMin = new Vector2(0f, 0.5f); bRt.anchorMax = new Vector2(0f, 0.5f);
                bRt.pivot = new Vector2(0f, 0.5f);
                bRt.sizeDelta = new Vector2(34f, 34f);
                bRt.anchoredPosition = new Vector2(8f, 6f);
                Image bBg = badgeGo.GetComponent<Image>();
                bBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                bBg.type = Image.Type.Sliced;
                bBg.color = new Color(0.85f, 0.70f, 0.55f, 1f);

                GameObject badgeTxtGo = new GameObject("Emoji", typeof(RectTransform));
                badgeTxtGo.transform.SetParent(badgeGo.transform, false);
                RectTransform bTRt = (RectTransform)badgeTxtGo.transform;
                bTRt.anchorMin = Vector2.zero; bTRt.anchorMax = Vector2.one;
                bTRt.offsetMin = Vector2.zero; bTRt.offsetMax = Vector2.zero;
                var emojiTxt = badgeTxtGo.AddComponent<Text>();
                emojiTxt.font = font;
                emojiTxt.text = skillIcons[i];
                emojiTxt.fontSize = 18;
                emojiTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                emojiTxt.verticalOverflow = VerticalWrapMode.Overflow;
                emojiTxt.alignment = TextAnchor.MiddleCenter;
                emojiTxt.raycastTarget = false;

                // Skill Name
                GameObject nameGo = new GameObject("SkillName", typeof(RectTransform));
                nameGo.transform.SetParent(cardGo.transform, false);
                RectTransform nRt = (RectTransform)nameGo.transform;
                nRt.anchorMin = new Vector2(0f, 1f); nRt.anchorMax = new Vector2(0f, 1f);
                nRt.pivot = new Vector2(0f, 1f);
                nRt.sizeDelta = new Vector2(140f, 24f);
                nRt.anchoredPosition = new Vector2(48f, -6f);
                var nTxt = nameGo.AddComponent<Text>();
                nTxt.font = font;
                nTxt.text = skill.ToString();
                nTxt.fontSize = 14;
                nTxt.fontStyle = FontStyle.Bold;
                nTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                nTxt.verticalOverflow = VerticalWrapMode.Overflow;
                nTxt.color = new Color(0.32f, 0.22f, 0.16f, 1f);
                nTxt.alignment = TextAnchor.MiddleLeft;
                nTxt.raycastTarget = false;

                // Level Tag
                GameObject lvlGo = new GameObject("LevelTag", typeof(RectTransform));
                lvlGo.transform.SetParent(cardGo.transform, false);
                RectTransform lRt = (RectTransform)lvlGo.transform;
                lRt.anchorMin = new Vector2(1f, 1f); lRt.anchorMax = new Vector2(1f, 1f);
                lRt.pivot = new Vector2(1f, 1f);
                lRt.sizeDelta = new Vector2(80f, 24f);
                lRt.anchoredPosition = new Vector2(-8f, -6f);
                var lTxt = lvlGo.AddComponent<Text>();
                lTxt.font = font;
                lTxt.text = "Lvl 1";
                lTxt.fontSize = 13;
                lTxt.fontStyle = FontStyle.Bold;
                lTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                lTxt.verticalOverflow = VerticalWrapMode.Overflow;
                lTxt.color = new Color(0.65f, 0.42f, 0.18f, 1f);
                lTxt.alignment = TextAnchor.MiddleRight;
                lTxt.raycastTarget = false;
                _levelTexts[skill] = lTxt;

                // Subtitle / Description
                GameObject descGo = new GameObject("Desc", typeof(RectTransform));
                descGo.transform.SetParent(cardGo.transform, false);
                RectTransform dRt = (RectTransform)descGo.transform;
                dRt.anchorMin = new Vector2(0f, 1f); dRt.anchorMax = new Vector2(1f, 1f);
                dRt.pivot = new Vector2(0f, 1f);
                dRt.sizeDelta = new Vector2(-56f, 20f);
                dRt.anchoredPosition = new Vector2(48f, -25f);
                var dTxt = descGo.AddComponent<Text>();
                dTxt.font = font;
                dTxt.text = skillDescriptions[i];
                dTxt.fontSize = 10;
                dTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
                dTxt.verticalOverflow = VerticalWrapMode.Overflow;
                dTxt.color = new Color(0.50f, 0.40f, 0.35f, 1f);
                dTxt.alignment = TextAnchor.MiddleLeft;
                dTxt.raycastTarget = false;

                // XP Progress Bar Background
                GameObject barTrackGo = new GameObject("XPBarTrack", typeof(RectTransform), typeof(Image));
                barTrackGo.transform.SetParent(cardGo.transform, false);
                RectTransform trackRt = (RectTransform)barTrackGo.transform;
                trackRt.anchorMin = new Vector2(0f, 0f); trackRt.anchorMax = new Vector2(1f, 0f);
                trackRt.pivot = new Vector2(0.5f, 0f);
                trackRt.sizeDelta = new Vector2(-16f, 18f);
                trackRt.anchoredPosition = new Vector2(0f, 8f);
                Image trackImg = barTrackGo.GetComponent<Image>();
                trackImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                trackImg.type = Image.Type.Sliced;
                trackImg.color = new Color(0.35f, 0.28f, 0.24f, 0.95f);

                // XP Fill Image
                GameObject barFillGo = new GameObject("XPBarFill", typeof(RectTransform), typeof(Image));
                barFillGo.transform.SetParent(barTrackGo.transform, false);
                RectTransform fillRt = (RectTransform)barFillGo.transform;
                fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = new Vector2(2f, 2f); fillRt.offsetMax = new Vector2(-2f, -2f);
                Image fillImg = barFillGo.GetComponent<Image>();
                fillImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImg.color = new Color(0.35f, 0.78f, 0.42f, 1f); // Vibrant grass-green XP fill
                fillImg.fillAmount = 0f;
                _xpFillBars[skill] = fillImg;

                // XP Fraction Text Overlay
                GameObject fracGo = new GameObject("XPFraction", typeof(RectTransform));
                fracGo.transform.SetParent(barTrackGo.transform, false);
                RectTransform fracRt = (RectTransform)fracGo.transform;
                fracRt.anchorMin = Vector2.zero; fracRt.anchorMax = Vector2.one;
                fracRt.offsetMin = Vector2.zero; fracRt.offsetMax = Vector2.zero;
                var fracTxt = fracGo.AddComponent<Text>();
                fracTxt.font = font;
                fracTxt.text = "0 / 100 XP";
                fracTxt.fontSize = 11;
                fracTxt.fontStyle = FontStyle.Bold;
                fracTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                fracTxt.verticalOverflow = VerticalWrapMode.Overflow;
                fracTxt.color = Color.white;
                fracTxt.alignment = TextAnchor.MiddleCenter;
                fracTxt.raycastTarget = false;
                _xpFractionTexts[skill] = fracTxt;
            }

            // Bottom Hint
            GameObject hintGo = new GameObject("Hint", typeof(RectTransform));
            hintGo.transform.SetParent(_panelGo.transform, false);
            RectTransform hintRt = (RectTransform)hintGo.transform;
            hintRt.anchorMin = new Vector2(0.5f, 0f); hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(panelW - 60f, 24f);
            hintRt.anchoredPosition = new Vector2(0f, 12f);
            var hTxt = hintGo.AddComponent<Text>();
            hTxt.font = font;
            hTxt.text = "Press 'K' or 'Esc' to close · Gain XP by performing related farm activities";
            hTxt.fontSize = 11;
            hTxt.fontStyle = FontStyle.Italic;
            hTxt.color = new Color(0.92f, 0.85f, 0.78f, 0.85f);
            hTxt.alignment = TextAnchor.MiddleCenter;
            hTxt.raycastTarget = false;

            RefreshSkillData();
        }
    }
}
