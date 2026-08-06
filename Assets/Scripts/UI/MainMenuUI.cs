using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Input;
using Willowstead.Persistence;
using Willowstead.Player;

namespace Willowstead.UI
{
    /// <summary>
    /// "Play" entry screen — covers the world until the player picks New / Continue / Load.
    /// Self-bootstraps at BeforeSceneLoad so the menu is ready the instant the scene becomes active.
    /// Features an opaque stylized background, ambient particle dots, and smooth fade transitions.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        public static MainMenuUI Instance { get; private set; }

        private GameObject _panelGo;
        private CanvasGroup _canvasGroup;
        private TMP_Text _continueLabel;
        private Button _continueButton;
        private Coroutine _fadeCoroutine;

        private struct ParticleData
        {
            public RectTransform rect;
            public Image img;
            public Vector2 pos;
            public Vector2 speed;
            public float baseAlpha;
            public float phase;
        }

        private readonly List<ParticleData> _particles = new List<ParticleData>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[MainMenuUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<MainMenuUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas", "UIRoot");
            UIResourceHelper.EnsureEventSystem();
            BuildPanel(canvas);

            _panelGo.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Only auto-show when there's no save in progress already.
            if (SaveGameManager.IsLoadingFromSave) return;
            Show(instant: true);
        }

        private void Update()
        {
            if (_panelGo == null || !_panelGo.activeSelf) return;

            // Animate floating ambient particle dots softly
            float dt = Time.deltaTime;
            float time = Time.time;

            for (int i = 0; i < _particles.Count; i++)
            {
                ParticleData p = _particles[i];
                p.pos.y += p.speed.y * dt;
                p.pos.x += Mathf.Sin(time * 1.2f + p.phase) * p.speed.x * dt;

                // Wrap vertically
                if (p.pos.y > 540f)
                {
                    p.pos.y = -540f;
                    p.pos.x = Random.Range(-900f, 900f);
                }

                p.rect.anchoredPosition = p.pos;

                // Gentle pulse alpha
                if (p.img != null)
                {
                    Color c = p.img.color;
                    c.a = p.baseAlpha * (0.6f + 0.4f * Mathf.Sin(time * 2f + p.phase));
                    p.img.color = c;
                }

                _particles[i] = p;
            }
        }

        private void OnDisable()
        {
            InputReader.BlockGameplayInput = false;
        }

        public void Show(bool instant = false)
        {
            if (_panelGo == null) return;

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            InputReader.BlockGameplayInput = true;
            RefreshContinue();
            _panelGo.SetActive(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
                if (instant)
                {
                    _canvasGroup.alpha = 1f;
                }
                else
                {
                    _fadeCoroutine = StartCoroutine(FadeInCoroutine(0.25f));
                }
            }
        }

        public void Hide(bool instant = false)
        {
            if (_panelGo == null) return;

            // Immediately unblock gameplay input so WASD and player actions work instantly
            InputReader.BlockGameplayInput = false;

            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (instant || _canvasGroup == null || !_panelGo.activeInHierarchy)
            {
                _panelGo.SetActive(false);
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1f;
                    _canvasGroup.blocksRaycasts = true;
                }
            }
            else
            {
                _fadeCoroutine = StartCoroutine(FadeOutCoroutine(0.35f));
            }
        }

        /// <summary>True when the Play menu panel is on screen.</summary>
        public bool IsVisible => _panelGo != null && _panelGo.activeSelf && (_canvasGroup == null || _canvasGroup.alpha > 0.05f);

        // ─── Transition Coroutines ──────────────────────────────────────

        private IEnumerator FadeInCoroutine(float duration)
        {
            float elapsed = 0f;
            float startAlpha = _canvasGroup.alpha;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, t);
                yield return null;
            }
            _canvasGroup.alpha = 1f;
            _fadeCoroutine = null;
        }

        private IEnumerator FadeOutCoroutine(float duration)
        {
            _canvasGroup.blocksRaycasts = false;
            float elapsed = 0f;
            float startAlpha = _canvasGroup.alpha;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            _panelGo.SetActive(false);
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1f;
            InputReader.BlockGameplayInput = false;
            _fadeCoroutine = null;
        }

        // ─── Panel construction ────────────────────────────────────────

        private void BuildPanel(Canvas canvas)
        {
            _panelGo = new GameObject("MainMenuPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)_panelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _canvasGroup = _panelGo.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 1f;

            // 1. Base Opaque Background (hides terrain completely)
            Image bg = _panelGo.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 1.0f);
            bg.raycastTarget = true;

            // 2. Soft Ambient Radial Glow behind center
            GameObject glowGo = new GameObject("AmbientGlow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(_panelGo.transform, false);
            RectTransform glowRt = (RectTransform)glowGo.transform;
            glowRt.anchorMin = new Vector2(0.5f, 0.5f);
            glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            glowRt.sizeDelta = new Vector2(900f, 700f);
            glowRt.anchoredPosition = Vector2.zero;
            Image glowImg = glowGo.GetComponent<Image>();
            glowImg.color = new Color(0.85f, 0.72f, 0.38f, 0.09f);
            glowImg.raycastTarget = false;

            // 3. Floating Ambient Particles Container
            GameObject particlesGo = new GameObject("ParticlesContainer", typeof(RectTransform));
            particlesGo.transform.SetParent(_panelGo.transform, false);
            RectTransform pContainerRt = (RectTransform)particlesGo.transform;
            pContainerRt.anchorMin = Vector2.zero;
            pContainerRt.anchorMax = Vector2.one;
            pContainerRt.offsetMin = Vector2.zero;
            pContainerRt.offsetMax = Vector2.zero;
            BuildParticles(particlesGo.transform, 20);

            // 4. Vignette Shadow around screen edges
            GameObject vigGo = new GameObject("VignetteOverlay", typeof(RectTransform), typeof(Image));
            vigGo.transform.SetParent(_panelGo.transform, false);
            RectTransform vigRt = (RectTransform)vigGo.transform;
            vigRt.anchorMin = Vector2.zero;
            vigRt.anchorMax = Vector2.one;
            vigRt.offsetMin = Vector2.zero;
            vigRt.offsetMax = Vector2.zero;
            Image vigImg = vigGo.GetComponent<Image>();
            vigImg.color = new Color(0.02f, 0.03f, 0.05f, 0.38f);
            vigImg.raycastTarget = false;

            // 5. Framed Center Menu Card Container
            GameObject cardGo = new GameObject("MenuCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(_panelGo.transform, false);
            RectTransform cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(560f, 620f);
            cardRt.anchoredPosition = new Vector2(0f, -10f);
            Image cardBg = cardGo.GetComponent<Image>();
            cardBg.sprite = UIResourceHelper.GetBackgroundSprite();
            cardBg.color = new Color(0.11f, 0.10f, 0.08f, 0.94f);
            cardBg.raycastTarget = true;

            // Title
            GameObject titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(cardGo.transform, false);
            RectTransform titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(520f, 75f);
            titleRt.anchoredPosition = new Vector2(0f, -30f);
            BuildText(titleGo, "Willowstead",
                new Color(0.96f, 0.89f, 0.64f, 1f), fontSize: 58, style: FontStyles.Bold);

            // Subtitle
            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(cardGo.transform, false);
            RectTransform subRt = (RectTransform)subGo.transform;
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.sizeDelta = new Vector2(500f, 32f);
            subRt.anchoredPosition = new Vector2(0f, -96f);
            BuildText(subGo, "a little farm in a deterministic world",
                new Color(0.85f, 0.80f, 0.72f, 1f), fontSize: 19, style: FontStyles.Italic);

            // Buttons inside the Card
            _continueButton = BuildMenuButton(cardGo.transform, "Continue (Most Recent)",
                new Vector2(0f, -170f), OnContinueClicked);
            var newWorldBtn = BuildMenuButton(cardGo.transform, "New World",
                new Vector2(0f, -250f), OnNewWorldClicked);
            var loadBtn = BuildMenuButton(cardGo.transform, "Load Saves",
                new Vector2(0f, -330f), OnLoadSavesClicked);
            var quitBtn = BuildMenuButton(cardGo.transform, "Quit",
                new Vector2(0f, -410f), OnQuitClicked);
            _continueLabel = _continueButton.GetComponentInChildren<TMP_Text>();

            // Helper tip line at bottom of Card
            GameObject hintGo = new GameObject("Hint", typeof(RectTransform));
            hintGo.transform.SetParent(cardGo.transform, false);
            RectTransform hintRt = (RectTransform)hintGo.transform;
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(500f, 30f);
            hintRt.anchoredPosition = new Vector2(0f, 24f);
            BuildText(hintGo, "Tip: press F5 anywhere in-game to save to slot 1.",
                new Color(0.78f, 0.72f, 0.62f, 0.80f), fontSize: 14, style: FontStyles.Italic);
        }

        private void BuildParticles(Transform parent, int count)
        {
            _particles.Clear();
            for (int i = 0; i < count; i++)
            {
                GameObject pGo = new GameObject($"Particle_{i}", typeof(RectTransform), typeof(Image));
                pGo.transform.SetParent(parent, false);
                RectTransform pRt = (RectTransform)pGo.transform;
                pRt.anchorMin = new Vector2(0.5f, 0.5f);
                pRt.anchorMax = new Vector2(0.5f, 0.5f);

                float size = Random.Range(3f, 7f);
                pRt.sizeDelta = new Vector2(size, size);

                Image img = pGo.GetComponent<Image>();
                img.raycastTarget = false;
                float baseAlpha = Random.Range(0.20f, 0.55f);
                img.color = new Color(0.95f, 0.88f, 0.60f, baseAlpha);

                Vector2 pos = new Vector2(Random.Range(-800f, 800f), Random.Range(-500f, 500f));
                pRt.anchoredPosition = pos;

                _particles.Add(new ParticleData
                {
                    rect = pRt,
                    img = img,
                    pos = pos,
                    speed = new Vector2(Random.Range(10f, 25f), Random.Range(20f, 45f)),
                    baseAlpha = baseAlpha,
                    phase = Random.Range(0f, 6.28f)
                });
            }
        }

        private static TMP_Text BuildText(GameObject parent, string text, Color color, float fontSize, FontStyles style)
        {
            var t = parent.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.richText = false;
            return t;
        }

        private static Button BuildMenuButton(Transform parent, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(440f, 58f);
            rt.anchoredPosition = anchoredPos;
            Image img = go.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            img.color = new Color(0.18f, 0.16f, 0.12f, 1f);
            img.raycastTarget = true;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.20f, 0.18f, 0.14f, 1f);
            cb.highlightedColor = new Color(0.36f, 0.30f, 0.22f, 1f);
            cb.pressedColor = new Color(0.10f, 0.09f, 0.07f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.18f, 0.16f, 0.12f, 0.6f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            BuildText(lblGo, label, new Color(0.96f, 0.92f, 0.78f, 1f), 22f, FontStyles.Bold);
            return btn;
        }

        // ─── Button handlers ───────────────────────────────────────────

        private void RefreshContinue()
        {
            if (_continueButton == null) return;
            SaveSlotSummary best = null;
            if (SaveGameManager.Instance != null) best = SaveGameManager.Instance.FindMostRecent();
            bool enabled = best != null && best.exists;
            _continueButton.interactable = enabled;
            if (_continueLabel != null)
            {
                if (enabled)
                    _continueLabel.text = $"Continue ({(string.IsNullOrEmpty(best.saveName) ? "Untitled" : best.saveName)})";
                else
                    _continueLabel.text = "Continue (no saves yet)";
            }
        }

        private void OnNewWorldClicked()
        {
            if (WorldSetupUI.Instance != null)
                WorldSetupUI.Instance.Show();
        }

        private void OnContinueClicked()
        {
            if (SaveGameManager.Instance == null) return;
            SaveSlotSummary best = SaveGameManager.Instance.FindMostRecent();
            if (best == null || !best.exists) return;
            SaveGameManager.Instance.LoadFromPath(best.fullPath);
            Hide();
        }

        private void OnLoadSavesClicked()
        {
            if (SaveSlotsUI.Instance != null)
                SaveSlotsUI.Instance.Show();
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

