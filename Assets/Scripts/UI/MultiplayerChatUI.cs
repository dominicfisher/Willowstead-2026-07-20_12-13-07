using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Input;
using Willowstead.Networking;
using Willowstead.Player;

namespace Willowstead.UI
{
    /// <summary>
    /// In-game Co-op Chat and Typing Notification System.
    /// Press Enter / Return (or T) to open chat, type a message, and send.
    /// Broadcasts messages and typing status across players.
    /// </summary>
    public class MultiplayerChatUI : MonoBehaviour
    {
        public static MultiplayerChatUI Instance { get; private set; }

        private GameObject _chatRoot;
        private GameObject _logContainer;
        private TMP_InputField _inputField;
        private TextMeshProUGUI _typingStatusText;
        private CanvasGroup _canvasGroup;

        private readonly List<GameObject> _messageEntries = new List<GameObject>();
        private Coroutine _fadeCoroutine;
        private bool _isTyping = false;
        private float _typingDebounceTimer = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[MultiplayerChatUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<MultiplayerChatUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas", "UIRoot");
            UIResourceHelper.EnsureEventSystem();
            BuildChatPanel(canvas);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Only handle chat hotkey when main menu / modals are not covering
            if (MainMenuUI.Instance != null && MainMenuUI.Instance.IsVisible) return;
            if (WorldSetupUI.Instance != null && WorldSetupUI.Instance.IsVisible) return;
            if (CharacterCreationUI.Instance != null && CharacterCreationUI.Instance.gameObject.activeInHierarchy) return;

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || (!IsOpen && kb.tKey.wasPressedThisFrame)) 
                    && !(Debugging.DevConsole.Instance != null && Debugging.DevConsole.Instance.IsOpen))
                {
                    if (!IsOpen)
                    {
                        OpenChat();
                    }
                    else
                    {
                        SubmitMessage();
                    }
                }
                else if (IsOpen && kb.escapeKey.wasPressedThisFrame)
                {
                    CloseChat();
                }
            }

            if (IsOpen && _isTyping)
            {
                _typingDebounceTimer -= Time.unscaledDeltaTime;
                if (_typingDebounceTimer <= 0f)
                {
                    SetLocalTyping(false);
                }
            }
        }

        public bool IsOpen => _inputField != null && _inputField.gameObject.activeSelf;

        public void OpenChat()
        {
            if (_chatRoot == null || _inputField == null) return;
            _chatRoot.SetActive(true);
            _inputField.gameObject.SetActive(true);
            _inputField.text = string.Empty;
            _inputField.ActivateInputField();

            InputReader.BlockGameplayInput = true;
            ShowChatLogs(true);
            SetLocalTyping(true);
        }

        public void CloseChat()
        {
            if (_chatRoot == null || _inputField == null) return;
            SetLocalTyping(false);
            _inputField.DeactivateInputField();
            _inputField.gameObject.SetActive(false);
            InputReader.BlockGameplayInput = false;

            StartFadeOutCountdown();
        }

        public void SubmitMessage()
        {
            if (_inputField == null) return;
            string text = _inputField.text == null ? string.Empty : _inputField.text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                string sender = CharacterCreationUI.GetSavedUsername();
                AddChatMessage(sender, text, isLocal: true);
            }
            CloseChat();
        }

        public void AddChatMessage(string sender, string message, bool isLocal = false)
        {
            if (_logContainer == null) return;

            ShowChatLogs(true);

            GameObject msgGo = new GameObject("ChatMsg", typeof(RectTransform));
            msgGo.transform.SetParent(_logContainer.transform, false);
            RectTransform rt = (RectTransform)msgGo.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, 26f);

            var t = msgGo.AddComponent<TextMeshProUGUI>();
            string colorHex = isLocal ? "#FFD670" : "#80D27F";
            t.text = $"<color={colorHex}><b>{sender}:</b></color> {message}";
            t.fontSize = 15f;
            t.color = new Color(0.96f, 0.94f, 0.90f, 1f);
            t.outlineWidth = 0.2f;
            t.outlineColor = Color.black;

            _messageEntries.Add(msgGo);
            if (_messageEntries.Count > 25)
            {
                Destroy(_messageEntries[0]);
                _messageEntries.RemoveAt(0);
            }

            if (!IsOpen)
            {
                StartFadeOutCountdown();
            }
        }

        public void ShowRemoteTyping(string username, bool isTyping)
        {
            if (_typingStatusText != null)
            {
                _typingStatusText.gameObject.SetActive(isTyping);
                _typingStatusText.text = isTyping ? $"💬 <i>{username} is typing...</i>" : string.Empty;
            }
        }

        private void SetLocalTyping(bool typing)
        {
            _isTyping = typing;
            if (PlayerController.Instance != null)
            {
                var np = PlayerController.Instance.GetComponent<PlayerNameplate>();
                if (np != null) np.SetTyping(typing);
            }
        }

        private void ShowChatLogs(bool visible)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = visible ? 1f : 0f;
        }

        private void StartFadeOutCountdown()
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutChatRoutine());
        }

        private IEnumerator FadeOutChatRoutine()
        {
            yield return new WaitForSecondsRealtime(5.0f);
            float elapsed = 0f;
            float dur = 0.8f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / dur);
                yield return null;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        private void BuildChatPanel(Canvas canvas)
        {
            _chatRoot = new GameObject("ChatRoot", typeof(RectTransform), typeof(CanvasGroup));
            _chatRoot.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)_chatRoot.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(20f, 85f);
            rt.sizeDelta = new Vector2(420f, 220f);

            _canvasGroup = _chatRoot.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f; // Start hidden till message sent/received

            // Log Scroll / Container
            GameObject logScroll = new GameObject("LogContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            logScroll.transform.SetParent(_chatRoot.transform, false);
            RectTransform lsRt = (RectTransform)logScroll.transform;
            lsRt.anchorMin = new Vector2(0f, 0f);
            lsRt.anchorMax = new Vector2(1f, 1f);
            lsRt.pivot = new Vector2(0f, 0f);
            lsRt.offsetMin = new Vector2(0f, 44f);
            lsRt.offsetMax = new Vector2(0f, 0f);

            var vlg = logScroll.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 3f;
            _logContainer = logScroll;

            // Typing Status Label
            GameObject typingGo = new GameObject("TypingStatus", typeof(RectTransform));
            typingGo.transform.SetParent(_chatRoot.transform, false);
            RectTransform typRt = (RectTransform)typingGo.transform;
            typRt.anchorMin = new Vector2(0f, 0f); typRt.anchorMax = new Vector2(1f, 0f);
            typRt.pivot = new Vector2(0f, 0f);
            typRt.anchoredPosition = new Vector2(0f, 44f);
            typRt.sizeDelta = new Vector2(0f, 20f);
            _typingStatusText = typingGo.AddComponent<TextMeshProUGUI>();
            _typingStatusText.fontSize = 13f;
            _typingStatusText.color = new Color(0.9f, 0.9f, 0.6f, 0.9f);
            typingGo.SetActive(false);

            // Chat Input Row
            GameObject inRow = new GameObject("ChatInputRow", typeof(RectTransform), typeof(Image));
            inRow.transform.SetParent(_chatRoot.transform, false);
            RectTransform inRt = (RectTransform)inRow.transform;
            inRt.anchorMin = new Vector2(0f, 0f);
            inRt.anchorMax = new Vector2(1f, 0f);
            inRt.pivot = new Vector2(0f, 0f);
            inRt.anchoredPosition = Vector2.zero;
            inRt.sizeDelta = new Vector2(0f, 38f);

            Image inBg = inRow.GetComponent<Image>();
            inBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            inBg.type = Image.Type.Sliced;
            inBg.color = new Color(0.08f, 0.06f, 0.04f, 0.95f);

            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inRow.transform, false);
            RectTransform taRt = (RectTransform)textArea.transform;
            taRt.anchorMin = Vector2.zero; taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(10f, 3f); taRt.offsetMax = new Vector2(-10f, -3f);

            GameObject inTextGo = new GameObject("Text", typeof(RectTransform));
            inTextGo.transform.SetParent(textArea.transform, false);
            RectTransform txtRt = (RectTransform)inTextGo.transform;
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
            var inTxt = inTextGo.AddComponent<TextMeshProUGUI>();
            inTxt.fontSize = 15f;
            inTxt.color = new Color(1f, 0.96f, 0.90f, 1f);
            inTxt.alignment = TextAlignmentOptions.MidlineLeft;

            GameObject phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(textArea.transform, false);
            RectTransform phRt = (RectTransform)phGo.transform;
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero; phRt.offsetMax = Vector2.zero;
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            ph.text = "Press Enter to chat (Esc to close)...";
            ph.fontSize = 14f;
            ph.fontStyle = FontStyles.Italic;
            ph.color = new Color(0.6f, 0.58f, 0.52f, 0.5f);
            ph.alignment = TextAlignmentOptions.MidlineLeft;

            _inputField = inRow.AddComponent<TMP_InputField>();
            _inputField.targetGraphic = inBg;
            _inputField.textViewport = taRt;
            _inputField.textComponent = inTxt;
            _inputField.placeholder = ph;
            _inputField.characterLimit = 120;
            _inputField.onValueChanged.AddListener(val =>
            {
                SetLocalTyping(true);
                _typingDebounceTimer = 2.5f;
            });
            _inputField.gameObject.SetActive(false);
        }
    }
}
