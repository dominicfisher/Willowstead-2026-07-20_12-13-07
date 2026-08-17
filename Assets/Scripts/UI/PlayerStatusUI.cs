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

            Font font = UIResourceHelper.GetPixelFont();

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
            rootRt.sizeDelta = new Vector2(220f, 68f);

            // ── 1. HEALTH BAR ROW (Top) ─────────────────────────────────────────
            BuildPixelGaugeRow(
                parent: _hudRootGo.transform,
                rowName: "HealthRow",
                yPos: 18f,
                bgSprite: UIResourceHelper.GetHealthBarBackgroundSprite(),
                fillSprite: UIResourceHelper.GetHealthBarSprite(),
                ghostColor: new Color(1f, 0.7f, 0.4f, 0.85f),
                outFill: out _healthFillImage,
                outGhost: out _healthGhostImage,
                outValueText: out _healthValueText,
                outGlow: out _
            );

            // ── 2. STAMINA BAR ROW (Bottom) ──────────────────────────────────────
            BuildPixelGaugeRow(
                parent: _hudRootGo.transform,
                rowName: "StaminaRow",
                yPos: -18f,
                bgSprite: UIResourceHelper.GetStaminaBarBackgroundSprite(),
                fillSprite: UIResourceHelper.GetStaminaBarSprite(),
                ghostColor: new Color(0.9f, 0.95f, 0.4f, 0.85f),
                outFill: out _staminaFillImage,
                outGhost: out _staminaGhostImage,
                outValueText: out _staminaValueText,
                outGlow: out _staminaBadgeGlow
            );
        }

        private void BuildPixelGaugeRow(
            Transform parent,
            string rowName,
            float yPos,
            Sprite bgSprite,
            Sprite fillSprite,
            Color ghostColor,
            out Image outFill,
            out Image outGhost,
            out TextMeshProUGUI outValueText,
            out Image outGlow)
        {
            GameObject rowGo = new GameObject(rowName, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = new Vector2(0f, 0.5f);
            rowRt.anchorMax = new Vector2(0f, 0.5f);
            rowRt.pivot = new Vector2(0f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, yPos);
            rowRt.sizeDelta = new Vector2(220f, 32f);

            // Bar Background Frame
            GameObject trackGo = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(rowGo.transform, false);
            RectTransform trackRt = (RectTransform)trackGo.transform;
            trackRt.anchorMin = Vector2.zero;
            trackRt.anchorMax = Vector2.one;
            trackRt.offsetMin = Vector2.zero;
            trackRt.offsetMax = Vector2.zero;
            Image trackImg = trackGo.GetComponent<Image>();
            trackImg.sprite = bgSprite;
            trackImg.type = Image.Type.Simple;
            trackImg.preserveAspect = false;

            outGlow = trackImg; // Reference for warning pulses if exhausted

            // Fill & Ghost Container positioned inside the bar frame
            // The pixel frame has an inner bar active region (starts after icon ~38px from left to -8px right, 6px top/bottom)
            GameObject fillContainerGo = new GameObject("FillContainer", typeof(RectTransform), typeof(RectMask2D));
            fillContainerGo.transform.SetParent(rowGo.transform, false);
            RectTransform fillContRt = (RectTransform)fillContainerGo.transform;
            fillContRt.anchorMin = Vector2.zero;
            fillContRt.anchorMax = Vector2.one;
            fillContRt.offsetMin = new Vector2(40f, 6f);
            fillContRt.offsetMax = new Vector2(-8f, -6f);

            // Ghost Trailing Bar Fill
            GameObject ghostGo = new GameObject("GhostFill", typeof(RectTransform), typeof(Image));
            ghostGo.transform.SetParent(fillContainerGo.transform, false);
            RectTransform ghostRt = (RectTransform)ghostGo.transform;
            ghostRt.anchorMin = Vector2.zero; ghostRt.anchorMax = Vector2.one;
            ghostRt.offsetMin = Vector2.zero; ghostRt.offsetMax = Vector2.zero;
            outGhost = ghostGo.GetComponent<Image>();
            outGhost.sprite = fillSprite;
            outGhost.type = Image.Type.Filled;
            outGhost.fillMethod = Image.FillMethod.Horizontal;
            outGhost.fillOrigin = (int)Image.OriginHorizontal.Left;
            outGhost.fillAmount = 1f;
            outGhost.color = ghostColor;

            // Active Bar Fill
            GameObject fillGo = new GameObject("ActiveFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillContainerGo.transform, false);
            RectTransform fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            outFill = fillGo.GetComponent<Image>();
            outFill.sprite = fillSprite;
            outFill.type = Image.Type.Filled;
            outFill.fillMethod = Image.FillMethod.Horizontal;
            outFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            outFill.fillAmount = 1f;
            outFill.color = Color.white;

            // Value Text (Centered in the bar track)
            GameObject valGo = new GameObject("ValueText", typeof(RectTransform));
            valGo.transform.SetParent(rowGo.transform, false);
            RectTransform valRt = (RectTransform)valGo.transform;
            valRt.anchorMin = Vector2.zero; valRt.anchorMax = Vector2.one;
            valRt.offsetMin = new Vector2(42f, 0f); valRt.offsetMax = new Vector2(-10f, 0f);
            outValueText = valGo.AddComponent<TextMeshProUGUI>();
            outValueText.fontSize = 11f;
            outValueText.fontStyle = FontStyles.Bold;
            outValueText.alignment = TextAlignmentOptions.Center;
            outValueText.color = new Color(1f, 0.98f, 0.92f, 1f);
            outValueText.outlineWidth = 0.22f;
            outValueText.outlineColor = new Color(0.12f, 0.08f, 0.04f, 0.95f);
            outValueText.text = "100<size=75%>/<color=#D4C2A5>100</color></size>";
        }
    }
}
