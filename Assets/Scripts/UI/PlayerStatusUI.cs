using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Player;

namespace Willowstead.UI
{
    /// <summary>
    /// Programmatically constructs and drives the Health & Stamina HUD in the bottom-left of the screen.
    /// Features wood-framed containers, ruby crimson health gauge with delayed ghost damage bar,
    /// emerald/amber stamina gauge with trailing drain bar, jewel status icons, and low-stamina pulse warnings.
    /// </summary>
    public class PlayerStatusUI : MonoBehaviour
    {
        public static PlayerStatusUI Instance { get; private set; }

        private GameObject _hudRootGo;
        private CanvasGroup _canvasGroup;

        // Health Bar UI Elements
        private Image _healthFillImage;
        private Image _healthGhostImage;
        private TextMeshProUGUI _healthValueText;

        // Stamina Bar UI Elements
        private Image _staminaFillImage;
        private Image _staminaGhostImage;
        private TextMeshProUGUI _staminaValueText;

        // Pulse warning on low stamina
        private Image _staminaBadgeGlow;
        private Coroutine _staminaPulseCoroutine;

        // Ghost bar smoothing
        private float _targetHealthPercent = 1f;
        private float _currentGhostHealth = 1f;
        private float _targetStaminaPercent = 1f;
        private float _currentGhostStamina = 1f;

        private const float GhostBarSpeed = 1.8f;
        private const float GhostDelay = 0.35f;
        private float _healthGhostTimer = 0f;
        private float _staminaGhostTimer = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[PlayerStatusUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerStatusUI>();
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            CreateStatusUI();
            HookPlayerStats();
        }

        private void OnDestroy()
        {
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.OnHealthChanged -= HandleHealthChanged;
                PlayerStats.Instance.OnStaminaChanged -= HandleStaminaChanged;
                PlayerStats.Instance.OnStaminaExhausted -= HandleStaminaExhausted;
            }
        }

        private void HookPlayerStats()
        {
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.OnHealthChanged += HandleHealthChanged;
                PlayerStats.Instance.OnStaminaChanged += HandleStaminaChanged;
                PlayerStats.Instance.OnStaminaExhausted += HandleStaminaExhausted;

                HandleHealthChanged(PlayerStats.Instance.CurrentHealth, PlayerStats.Instance.MaxHealth);
                HandleStaminaChanged(PlayerStats.Instance.CurrentStamina, PlayerStats.Instance.MaxStamina);
            }
        }

        private void Update()
        {
            if (PlayerStats.Instance != null && (_healthFillImage == null || _staminaFillImage == null))
            {
                HookPlayerStats();
            }

            // Smooth ghost bar transitions
            UpdateGhostBars();
        }

        private void UpdateGhostBars()
        {
            // Health Ghost
            if (_healthGhostTimer > 0f)
            {
                _healthGhostTimer -= Time.deltaTime;
            }
            else if (_currentGhostHealth > _targetHealthPercent)
            {
                _currentGhostHealth = Mathf.MoveTowards(_currentGhostHealth, _targetHealthPercent, GhostBarSpeed * Time.deltaTime);
                if (_healthGhostImage != null) _healthGhostImage.fillAmount = _currentGhostHealth;
            }
            else
            {
                _currentGhostHealth = _targetHealthPercent;
                if (_healthGhostImage != null) _healthGhostImage.fillAmount = _currentGhostHealth;
            }

            // Stamina Ghost
            if (_staminaGhostTimer > 0f)
            {
                _staminaGhostTimer -= Time.deltaTime;
            }
            else if (_currentGhostStamina > _targetStaminaPercent)
            {
                _currentGhostStamina = Mathf.MoveTowards(_currentGhostStamina, _targetStaminaPercent, (GhostBarSpeed * 1.5f) * Time.deltaTime);
                if (_staminaGhostImage != null) _staminaGhostImage.fillAmount = _currentGhostStamina;
            }
            else
            {
                _currentGhostStamina = _targetStaminaPercent;
                if (_staminaGhostImage != null) _staminaGhostImage.fillAmount = _currentGhostStamina;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            float pct = Mathf.Clamp01(max > 0f ? current / max : 0f);
            if (pct < _targetHealthPercent)
            {
                _healthGhostTimer = GhostDelay;
            }
            _targetHealthPercent = pct;

            if (_healthFillImage != null)
            {
                _healthFillImage.fillAmount = _targetHealthPercent;
            }

            if (_healthValueText != null)
            {
                _healthValueText.text = $"{Mathf.CeilToInt(current)}<size=75%>/<color=#D4C2A5>{Mathf.CeilToInt(max)}</color></size>";
            }
        }

        private void HandleStaminaChanged(float current, float max)
        {
            float pct = Mathf.Clamp01(max > 0f ? current / max : 0f);
            if (pct < _targetStaminaPercent)
            {
                _staminaGhostTimer = GhostDelay;
            }
            _targetStaminaPercent = pct;

            if (_staminaFillImage != null)
            {
                _staminaFillImage.fillAmount = _targetStaminaPercent;
            }

            if (_staminaValueText != null)
            {
                _staminaValueText.text = $"{Mathf.CeilToInt(current)}<size=75%>/<color=#D4C2A5>{Mathf.CeilToInt(max)}</color></size>";
            }
        }

        private void HandleStaminaExhausted()
        {
            if (_staminaPulseCoroutine != null) StopCoroutine(_staminaPulseCoroutine);
            _staminaPulseCoroutine = StartCoroutine(PulseStaminaWarningRoutine());
        }

        private IEnumerator PulseStaminaWarningRoutine()
        {
            if (_staminaBadgeGlow == null) yield break;

            Color flashCol = new Color(1f, 0.25f, 0.15f, 0.9f);
            Color normalCol = new Color(0.95f, 0.8f, 0.3f, 0.3f);

            for (int i = 0; i < 2; i++)
            {
                float t = 0f;
                while (t < 0.15f)
                {
                    t += Time.unscaledDeltaTime;
                    _staminaBadgeGlow.color = Color.Lerp(normalCol, flashCol, t / 0.15f);
                    yield return null;
                }
                t = 0f;
                while (t < 0.2f)
                {
                    t += Time.unscaledDeltaTime;
                    _staminaBadgeGlow.color = Color.Lerp(flashCol, normalCol, t / 0.2f);
                    yield return null;
                }
            }
            _staminaBadgeGlow.color = normalCol;
            _staminaPulseCoroutine = null;
        }

        private void CreateStatusUI()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas");
            if (canvas == null) return;
            UIResourceHelper.EnsureEventSystem();

            Transform existing = canvas.transform.Find("PlayerStatusHUD");
            if (existing != null) DestroyImmediate(existing.gameObject);

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;

            // ── Root Bottom-Left Container ───────────────────────────────────────
            _hudRootGo = new GameObject("PlayerStatusHUD", typeof(RectTransform));
            _hudRootGo.transform.SetParent(canvas.transform, false);

            _canvasGroup = _hudRootGo.AddComponent<CanvasGroup>();
            if (MainMenuUI.Instance != null && !MainMenuUI.HasGameStarted)
            {
                _canvasGroup.alpha = 0f;
            }

            RectTransform rootRt = (RectTransform)_hudRootGo.transform;
            rootRt.anchorMin = new Vector2(0f, 0f);
            rootRt.anchorMax = new Vector2(0f, 0f);
            rootRt.pivot = new Vector2(0f, 0f);
            rootRt.anchoredPosition = new Vector2(24f, 24f);
            rootRt.sizeDelta = new Vector2(210f, 80f);

            // ── Drop Shadow ─────────────────────────────────────────────────────
            GameObject shadowGo = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(_hudRootGo.transform, false);
            RectTransform shadowRt = (RectTransform)shadowGo.transform;
            shadowRt.anchorMin = Vector2.zero; shadowRt.anchorMax = Vector2.one;
            shadowRt.offsetMin = new Vector2(-4f, -4f); shadowRt.offsetMax = new Vector2(4f, 3f);
            Image shadowImg = shadowGo.GetComponent<Image>();
            shadowImg.sprite = UIResourceHelper.GetBackgroundSprite();
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0f, 0f, 0f, 0.50f);

            // ── Outer Timber Frame & Cozy Mauve Slate ─────────────────────────
            GameObject woodGo = new GameObject("WoodFrame", typeof(RectTransform), typeof(Image));
            woodGo.transform.SetParent(_hudRootGo.transform, false);
            RectTransform woodRt = (RectTransform)woodGo.transform;
            woodRt.anchorMin = Vector2.zero; woodRt.anchorMax = Vector2.one;
            woodRt.offsetMin = Vector2.zero; woodRt.offsetMax = Vector2.zero;
            Image woodImg = woodGo.GetComponent<Image>();
            woodImg.sprite = UIResourceHelper.GetBackgroundSprite();
            woodImg.type = Image.Type.Sliced;
            woodImg.color = new Color(0.85f, 0.65f, 0.48f, 1f); // Warm peach-gold framing outline

            // ── Inner Parchment / Slate Board ───────────────────────────────────
            GameObject innerGo = new GameObject("InnerBoard", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(woodGo.transform, false);
            RectTransform innerRt = (RectTransform)innerGo.transform;
            innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(4f, 4f); innerRt.offsetMax = new Vector2(-4f, -4f);
            Image innerImg = innerGo.GetComponent<Image>();
            innerImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            innerImg.type = Image.Type.Sliced;
            innerImg.color = new Color(0.48f, 0.35f, 0.32f, 0.98f); // Soft cozy mauve-brown slate

            // ── 1. HEALTH BAR ROW (Top) ─────────────────────────────────────────
            BuildGaugeRow(
                parent: innerGo.transform,
                rowName: "HealthRow",
                yPos: 18f,
                badgeLabel: "❤",
                badgeColor: new Color(0.95f, 0.25f, 0.28f, 1f),
                badgeGlowColor: new Color(0.85f, 0.15f, 0.20f, 0.35f),
                barFillColor: new Color(0.88f, 0.18f, 0.22f, 1f),       // Ruby crimson
                ghostFillColor: new Color(0.98f, 0.65f, 0.45f, 0.85f),   // Warm amber ghost
                trackColor: new Color(0.25f, 0.08f, 0.08f, 0.95f),
                font: font,
                outFill: out _healthFillImage,
                outGhost: out _healthGhostImage,
                outValueText: out _healthValueText,
                outGlow: out _
            );

            // ── 2. STAMINA BAR ROW (Bottom) ──────────────────────────────────────
            BuildGaugeRow(
                parent: innerGo.transform,
                rowName: "StaminaRow",
                yPos: -18f,
                badgeLabel: "⚡",
                badgeColor: new Color(0.35f, 0.92f, 0.45f, 1f),
                badgeGlowColor: new Color(0.30f, 0.85f, 0.40f, 0.30f),
                barFillColor: new Color(0.22f, 0.78f, 0.38f, 1f),       // Emerald stamina
                ghostFillColor: new Color(0.88f, 0.90f, 0.40f, 0.85f),   // Soft lime-yellow ghost
                trackColor: new Color(0.08f, 0.22f, 0.10f, 0.95f),
                font: font,
                outFill: out _staminaFillImage,
                outGhost: out _staminaGhostImage,
                outValueText: out _staminaValueText,
                outGlow: out _staminaBadgeGlow
            );
        }

        private void BuildGaugeRow(
            Transform parent,
            string rowName,
            float yPos,
            string badgeLabel,
            Color badgeColor,
            Color badgeGlowColor,
            Color barFillColor,
            Color ghostFillColor,
            Color trackColor,
            TMP_FontAsset font,
            out Image outFill,
            out Image outGhost,
            out TextMeshProUGUI outValueText,
            out Image outGlow)
        {
            GameObject rowGo = new GameObject(rowName, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = new Vector2(0f, 0.5f);
            rowRt.anchorMax = new Vector2(1f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, yPos);
            rowRt.sizeDelta = new Vector2(0f, 26f);

            // Badge Container (Left Icon)
            GameObject badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(rowGo.transform, false);
            RectTransform badgeRt = (RectTransform)badgeGo.transform;
            badgeRt.anchorMin = new Vector2(0f, 0.5f);
            badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.pivot = new Vector2(0f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(6f, 0f);
            badgeRt.sizeDelta = new Vector2(22f, 22f);
            Image badgeBg = badgeGo.GetComponent<Image>();
            badgeBg.sprite = UIResourceHelper.GetBackgroundSprite();
            badgeBg.type = Image.Type.Sliced;
            badgeBg.color = new Color(0.18f, 0.13f, 0.08f, 1f);

            GameObject badgeGlowGo = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            badgeGlowGo.transform.SetParent(badgeGo.transform, false);
            RectTransform glowRt = (RectTransform)badgeGlowGo.transform;
            glowRt.anchorMin = Vector2.zero; glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = new Vector2(-2f, -2f); glowRt.offsetMax = new Vector2(2f, 2f);
            outGlow = badgeGlowGo.GetComponent<Image>();
            outGlow.sprite = UIResourceHelper.GetBackgroundSprite();
            outGlow.type = Image.Type.Sliced;
            outGlow.color = badgeGlowColor;

            GameObject iconTextGo = new GameObject("Icon", typeof(RectTransform));
            iconTextGo.transform.SetParent(badgeGo.transform, false);
            RectTransform itRt = (RectTransform)iconTextGo.transform;
            itRt.anchorMin = Vector2.zero; itRt.anchorMax = Vector2.one;
            itRt.offsetMin = Vector2.zero; itRt.offsetMax = Vector2.zero;
            var it = iconTextGo.AddComponent<TextMeshProUGUI>();
            it.text = badgeLabel;
            it.font = font;
            it.fontSize = 13f;
            it.alignment = TextAlignmentOptions.Center;
            it.color = badgeColor;

            // Bar Track (Container)
            GameObject trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(rowGo.transform, false);
            RectTransform trackRt = (RectTransform)trackGo.transform;
            trackRt.anchorMin = new Vector2(0f, 0.5f);
            trackRt.anchorMax = new Vector2(1f, 0.5f);
            trackRt.pivot = new Vector2(0f, 0.5f);
            trackRt.offsetMin = new Vector2(34f, -9f);
            trackRt.offsetMax = new Vector2(-8f, 9f);
            Image trackImg = trackGo.GetComponent<Image>();
            trackImg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            trackImg.type = Image.Type.Sliced;
            trackImg.color = trackColor;

            // Ghost Trailing Bar Fill
            GameObject ghostGo = new GameObject("GhostFill", typeof(RectTransform), typeof(Image));
            ghostGo.transform.SetParent(trackGo.transform, false);
            RectTransform ghostRt = (RectTransform)ghostGo.transform;
            ghostRt.anchorMin = Vector2.zero; ghostRt.anchorMax = Vector2.one;
            ghostRt.offsetMin = new Vector2(2f, 2f); ghostRt.offsetMax = new Vector2(-2f, -2f);
            outGhost = ghostGo.GetComponent<Image>();
            outGhost.sprite = UIResourceHelper.GetBackgroundSprite();
            outGhost.type = Image.Type.Filled;
            outGhost.fillMethod = Image.FillMethod.Horizontal;
            outGhost.fillOrigin = (int)Image.OriginHorizontal.Left;
            outGhost.fillAmount = 1f;
            outGhost.color = ghostFillColor;

            // Active Bar Fill
            GameObject fillGo = new GameObject("ActiveFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            RectTransform fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f); fillRt.offsetMax = new Vector2(-2f, -2f);
            outFill = fillGo.GetComponent<Image>();
            outFill.sprite = UIResourceHelper.GetBackgroundSprite();
            outFill.type = Image.Type.Filled;
            outFill.fillMethod = Image.FillMethod.Horizontal;
            outFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            outFill.fillAmount = 1f;
            outFill.color = barFillColor;

            // Highlight Sheen (Top Gloss)
            GameObject glossGo = new GameObject("Gloss", typeof(RectTransform), typeof(Image));
            glossGo.transform.SetParent(fillGo.transform, false);
            RectTransform glossRt = (RectTransform)glossGo.transform;
            glossRt.anchorMin = new Vector2(0f, 0.5f); glossRt.anchorMax = new Vector2(1f, 1f);
            glossRt.offsetMin = Vector2.zero; glossRt.offsetMax = Vector2.zero;
            Image glossImg = glossGo.GetComponent<Image>();
            glossImg.sprite = UIResourceHelper.GetBackgroundSprite();
            glossImg.type = Image.Type.Sliced;
            glossImg.color = new Color(1f, 1f, 1f, 0.20f);

            // Value Text (Centered in bar)
            GameObject valGo = new GameObject("ValueText", typeof(RectTransform));
            valGo.transform.SetParent(trackGo.transform, false);
            RectTransform valRt = (RectTransform)valGo.transform;
            valRt.anchorMin = Vector2.zero; valRt.anchorMax = Vector2.one;
            valRt.offsetMin = new Vector2(6f, 0f); valRt.offsetMax = new Vector2(-6f, 0f);
            outValueText = valGo.AddComponent<TextMeshProUGUI>();
            outValueText.font = font;
            outValueText.fontSize = 11.5f;
            outValueText.fontStyle = FontStyles.Bold;
            outValueText.alignment = TextAlignmentOptions.Center;
            outValueText.color = new Color(0.98f, 0.95f, 0.90f, 1f);
            outValueText.outlineWidth = 0.25f;
            outValueText.outlineColor = new Color(0.1f, 0.05f, 0.02f, 0.95f);
            outValueText.text = "100<size=75%>/<color=#D4C2A5>100</color></size>";
        }
    }
}
