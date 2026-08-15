using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Input;
using Willowstead.Networking;
using Willowstead.Player;

namespace Willowstead.UI
{
    /// <summary>
    /// UI modal dialog for Hosting a Multiplayer Session (displaying the Join Code)
    /// or Joining an existing session by entering a code.
    /// </summary>
    public class MultiplayerLobbyUI : MonoBehaviour
    {
        public static MultiplayerLobbyUI Instance { get; private set; }

        private GameObject _panelGo;
        private TMP_InputField _codeInputField;
        private TextMeshProUGUI _hostCodeDisplay;
        private TextMeshProUGUI _statusLabel;
        private GameObject _hostView;
        private GameObject _joinView;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[MultiplayerLobbyUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<MultiplayerLobbyUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas", "UIRoot");
            UIResourceHelper.EnsureEventSystem();
            BuildPanel(canvas);
            _panelGo.SetActive(false);

            NetworkSessionManager.OnHostStarted += HandleHostStarted;
            NetworkSessionManager.OnClientConnected += HandleClientConnected;
            NetworkSessionManager.OnConnectionFailed += HandleConnectionFailed;
        }

        private void OnDestroy()
        {
            NetworkSessionManager.OnHostStarted -= HandleHostStarted;
            NetworkSessionManager.OnClientConnected -= HandleClientConnected;
            NetworkSessionManager.OnConnectionFailed -= HandleConnectionFailed;
            if (Instance == this) Instance = null;
        }

        public void ShowHostLobby()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(true);
            _hostView.SetActive(true);
            _joinView.SetActive(false);
            _statusLabel.text = "Generating room code…";
            InputReader.BlockGameplayInput = true;

            // Trigger host session
            if (NetworkSessionManager.Instance != null)
            {
                _ = NetworkSessionManager.Instance.StartHostSessionAsync();
            }
        }

        public void ShowJoinLobby()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(true);
            _hostView.SetActive(false);
            _joinView.SetActive(true);
            _statusLabel.text = "Enter a 6-character Join Code from your friend.";
            if (_codeInputField != null)
            {
                _codeInputField.text = string.Empty;
                _codeInputField.ActivateInputField();
            }
            InputReader.BlockGameplayInput = true;
        }

        public void Hide()
        {
            if (_panelGo == null) return;
            _panelGo.SetActive(false);
            InputReader.BlockGameplayInput = false;
        }

        private void HandleHostStarted(string joinCode)
        {
            if (_hostCodeDisplay != null)
            {
                _hostCodeDisplay.text = joinCode;
            }
            if (_statusLabel != null)
            {
                _statusLabel.text = "<color=#80D27F>Session active!</color> Share this code with friends.";
            }
        }

        private void HandleClientConnected()
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = "<color=#80D27F>Connected successfully!</color> Loading world…";
            }
            Hide();
            if (MainMenuUI.Instance != null) MainMenuUI.Instance.Hide();
            MainMenuUI.StartGameSession();
        }

        private void HandleConnectionFailed(string error)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = $"<color=#FF8585>Failed:</color> {error}";
            }
        }

        private void BuildPanel(Canvas canvas)
        {
            _panelGo = new GameObject("MultiplayerLobbyPanel", typeof(RectTransform));
            _panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rt = (RectTransform)_panelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Cozy card
            GameObject cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(_panelGo.transform, false);
            RectTransform cardRt = (RectTransform)cardGo.transform;
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(520f, 400f);
            Image cardBg = cardGo.GetComponent<Image>();
            cardBg.sprite = UIResourceHelper.GetBackgroundSprite();
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.22f, 0.16f, 0.11f, 0.98f); // Walnut wood

            // Title Banner
            GameObject bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(Image));
            bannerGo.transform.SetParent(cardGo.transform, false);
            RectTransform bannerRt = (RectTransform)bannerGo.transform;
            bannerRt.anchorMin = new Vector2(0.5f, 1f);
            bannerRt.anchorMax = new Vector2(0.5f, 1f);
            bannerRt.pivot = new Vector2(0.5f, 1f);
            bannerRt.sizeDelta = new Vector2(380f, 54f);
            bannerRt.anchoredPosition = new Vector2(0f, -20f);
            Image bannerBg = bannerGo.GetComponent<Image>();
            bannerBg.sprite = UIResourceHelper.GetBackgroundSprite();
            bannerBg.type = Image.Type.Sliced;
            bannerBg.color = new Color(0.35f, 0.24f, 0.15f, 1f);

            GameObject titleTextGo = new GameObject("TitleText", typeof(RectTransform));
            titleTextGo.transform.SetParent(bannerGo.transform, false);
            RectTransform titleRt = (RectTransform)titleTextGo.transform;
            titleRt.anchorMin = Vector2.zero; titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = Vector2.zero; titleRt.offsetMax = Vector2.zero;
            var t = titleTextGo.AddComponent<TextMeshProUGUI>();
            t.text = "MULTIPLAYER CO-OP";
            t.fontSize = 22f;
            t.fontStyle = FontStyles.Bold;
            t.color = new Color(1f, 0.88f, 0.45f, 1f);
            t.alignment = TextAlignmentOptions.Center;

            // Status label
            GameObject statusGo = new GameObject("StatusText", typeof(RectTransform));
            statusGo.transform.SetParent(cardGo.transform, false);
            RectTransform statusRt = (RectTransform)statusGo.transform;
            statusRt.anchorMin = new Vector2(0.5f, 1f);
            statusRt.anchorMax = new Vector2(0.5f, 1f);
            statusRt.pivot = new Vector2(0.5f, 1f);
            statusRt.sizeDelta = new Vector2(460f, 32f);
            statusRt.anchoredPosition = new Vector2(0f, -85f);
            _statusLabel = statusGo.AddComponent<TextMeshProUGUI>();
            _statusLabel.fontSize = 15f;
            _statusLabel.color = new Color(0.92f, 0.86f, 0.74f, 1f);
            _statusLabel.alignment = TextAlignmentOptions.Center;

            // --- HOST VIEW ---
            _hostView = new GameObject("HostView", typeof(RectTransform));
            _hostView.transform.SetParent(cardGo.transform, false);
            RectTransform hostRt = (RectTransform)_hostView.transform;
            hostRt.anchorMin = Vector2.zero; hostRt.anchorMax = Vector2.one;
            hostRt.offsetMin = Vector2.zero; hostRt.offsetMax = Vector2.zero;

            GameObject codeBoxGo = new GameObject("CodeBox", typeof(RectTransform), typeof(Image));
            codeBoxGo.transform.SetParent(_hostView.transform, false);
            RectTransform cbRt = (RectTransform)codeBoxGo.transform;
            cbRt.anchorMin = new Vector2(0.5f, 0.5f);
            cbRt.anchorMax = new Vector2(0.5f, 0.5f);
            cbRt.pivot = new Vector2(0.5f, 0.5f);
            cbRt.sizeDelta = new Vector2(320f, 64f);
            cbRt.anchoredPosition = new Vector2(0f, 10f);
            Image cbBg = codeBoxGo.GetComponent<Image>();
            cbBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            cbBg.type = Image.Type.Sliced;
            cbBg.color = new Color(0.12f, 0.09f, 0.06f, 0.95f);

            GameObject codeTextGo = new GameObject("CodeText", typeof(RectTransform));
            codeTextGo.transform.SetParent(codeBoxGo.transform, false);
            RectTransform ctRt = (RectTransform)codeTextGo.transform;
            ctRt.anchorMin = Vector2.zero; ctRt.anchorMax = Vector2.one;
            ctRt.offsetMin = Vector2.zero; ctRt.offsetMax = Vector2.zero;
            _hostCodeDisplay = codeTextGo.AddComponent<TextMeshProUGUI>();
            _hostCodeDisplay.text = "------";
            _hostCodeDisplay.fontSize = 32f;
            _hostCodeDisplay.fontStyle = FontStyles.Bold;
            _hostCodeDisplay.color = new Color(1f, 0.88f, 0.45f, 1f);
            _hostCodeDisplay.alignment = TextAlignmentOptions.Center;

            BuildButton(_hostView.transform, "Copy Code to Clipboard", new Vector2(0f, -60f), new Vector2(280f, 44f), () =>
            {
                if (!string.IsNullOrEmpty(_hostCodeDisplay.text))
                {
                    GUIUtility.systemCopyBuffer = _hostCodeDisplay.text;
                    _statusLabel.text = "<color=#80D27F>Copied join code to clipboard!</color>";
                }
            });

            BuildButton(_hostView.transform, "Start Game (Host)", new Vector2(0f, -120f), new Vector2(280f, 48f), () =>
            {
                Hide();
                if (MainMenuUI.Instance != null) MainMenuUI.Instance.Hide();
                MainMenuUI.StartGameSession();
            });

            // --- JOIN VIEW ---
            _joinView = new GameObject("JoinView", typeof(RectTransform));
            _joinView.transform.SetParent(cardGo.transform, false);
            RectTransform joinRt = (RectTransform)_joinView.transform;
            joinRt.anchorMin = Vector2.zero; joinRt.anchorMax = Vector2.one;
            joinRt.offsetMin = Vector2.zero; joinRt.offsetMax = Vector2.zero;

            GameObject inputGo = new GameObject("JoinInput", typeof(RectTransform), typeof(Image));
            inputGo.transform.SetParent(_joinView.transform, false);
            RectTransform inRt = (RectTransform)inputGo.transform;
            inRt.anchorMin = new Vector2(0.5f, 0.5f);
            inRt.anchorMax = new Vector2(0.5f, 0.5f);
            inRt.pivot = new Vector2(0.5f, 0.5f);
            inRt.sizeDelta = new Vector2(300f, 56f);
            inRt.anchoredPosition = new Vector2(0f, 20f);
            Image inBg = inputGo.GetComponent<Image>();
            inBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            inBg.type = Image.Type.Sliced;
            inBg.color = new Color(0.12f, 0.09f, 0.06f, 0.95f);

            GameObject inTextGo = new GameObject("Text", typeof(RectTransform));
            inTextGo.transform.SetParent(inputGo.transform, false);
            RectTransform intRt = (RectTransform)inTextGo.transform;
            intRt.anchorMin = Vector2.zero; intRt.anchorMax = Vector2.one;
            intRt.offsetMin = new Vector2(12f, 0f); intRt.offsetMax = new Vector2(-12f, 0f);
            var inTxt = inTextGo.AddComponent<TextMeshProUGUI>();
            inTxt.fontSize = 26f;
            inTxt.fontStyle = FontStyles.Bold;
            inTxt.color = new Color(1f, 0.88f, 0.45f, 1f);
            inTxt.alignment = TextAlignmentOptions.Center;

            _codeInputField = inputGo.AddComponent<TMP_InputField>();
            _codeInputField.textComponent = inTxt;
            _codeInputField.characterLimit = 12;
            _codeInputField.lineType = TMP_InputField.LineType.SingleLine;

            BuildButton(_joinView.transform, "Connect to Host", new Vector2(0f, -50f), new Vector2(260f, 48f), () =>
            {
                if (_codeInputField != null && NetworkSessionManager.Instance != null)
                {
                    _statusLabel.text = "Connecting to Relay server…";
                    _ = NetworkSessionManager.Instance.JoinSessionAsync(_codeInputField.text);
                }
            });

            BuildButton(_joinView.transform, "Paste from Clipboard", new Vector2(0f, -110f), new Vector2(260f, 40f), () =>
            {
                if (_codeInputField != null)
                {
                    _codeInputField.text = GUIUtility.systemCopyBuffer?.Trim() ?? string.Empty;
                }
            });

            // Close button
            BuildButton(cardGo.transform, "Back", new Vector2(0f, -160f), new Vector2(160f, 36f), () =>
            {
                Hide();
                if (MainMenuUI.Instance != null) MainMenuUI.Instance.Show();
            });
        }

        private static Button BuildButton(Transform parent, string label, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            Image img = go.GetComponent<Image>();
            img.sprite = UIResourceHelper.GetBackgroundSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.38f, 0.26f, 0.16f, 1f); // Warm wood button

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.38f, 0.26f, 0.16f, 1f);
            cb.highlightedColor = new Color(0.54f, 0.38f, 0.24f, 1f);
            cb.pressedColor = new Color(0.24f, 0.16f, 0.10f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            GameObject lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = (RectTransform)lblGo.transform;
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            lbl.text = label;
            lbl.fontSize = 16f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.color = new Color(1f, 0.94f, 0.82f, 1f);
            lbl.alignment = TextAlignmentOptions.Center;

            return btn;
        }
    }
}
