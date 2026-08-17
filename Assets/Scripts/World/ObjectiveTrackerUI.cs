using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Willowstead.Player;

namespace Willowstead.World
{
    /// <summary>
    /// HUD overlay displaying current goals & quests on a clean fantasy parchment quest card.
    /// Supports collapsing/expanding with a toggle button or hotkey 'O'.
    /// Auto-refreshes checkmarks and strikethroughs as the player fulfills objectives.
    /// </summary>
    public class ObjectiveTrackerUI : MonoBehaviour
    {
        public static ObjectiveTrackerUI Instance { get; private set; }

        private GameObject _rootGo;
        private GameObject _cardGo;
        private RectTransform _contentRt;
        private Image _cardBg;
        private bool _isCollapsed = false;

        private readonly List<GameObject> _entryGameObjects = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[ObjectiveTrackerUI]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ObjectiveTrackerUI>();
        }

        private void Awake()
        {
            if (Instance == null || Instance == this)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            CreateHUD();

            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnObjectivesUpdated += RefreshEntries;
            }

            RefreshEntries();
        }

        private void OnDestroy()
        {
            if (ObjectiveManager.Instance != null)
            {
                ObjectiveManager.Instance.OnObjectivesUpdated -= RefreshEntries;
            }
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.oKey.wasPressedThisFrame &&
                !Input.InputReader.BlockGameplayInput)
            {
                ToggleCollapse();
            }
        }

        private void CreateHUD()
        {
            Canvas canvas = UIResourceHelper.GetOrCreateHUDCanvas("HUDCanvas");
            if (canvas == null) return;

            Transform existing = canvas.transform.Find("ObjectiveTrackerHUD");
            if (existing != null) DestroyImmediate(existing.gameObject);

            Font font = UIResourceHelper.GetPixelFont();

            _rootGo = new GameObject("ObjectiveTrackerHUD", typeof(RectTransform));
            _rootGo.transform.SetParent(canvas.transform, false);

            RectTransform rootRt = (RectTransform)_rootGo.transform;
            rootRt.anchorMin = new Vector2(1f, 1f);
            rootRt.anchorMax = new Vector2(1f, 1f);
            rootRt.pivot = new Vector2(1f, 1f);
            rootRt.anchoredPosition = new Vector2(-16f, -16f);
            rootRt.sizeDelta = new Vector2(230f, 210f);

            // Card Parchment
            _cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            _cardGo.transform.SetParent(_rootGo.transform, false);
            RectTransform cardRt = (RectTransform)_cardGo.transform;
            cardRt.anchorMin = Vector2.zero; cardRt.anchorMax = Vector2.one;
            cardRt.offsetMin = Vector2.zero; cardRt.offsetMax = Vector2.zero;
            _cardBg = _cardGo.GetComponent<Image>();
            _cardBg.sprite = UIResourceHelper.GetInputFieldBackgroundSprite();
            _cardBg.type = Image.Type.Sliced;
            _cardBg.color = new Color(0.96f, 0.90f, 0.82f, 0.94f); // Warm parchment

            // Header Banner
            GameObject headGo = new GameObject("Header", typeof(RectTransform));
            headGo.transform.SetParent(_cardGo.transform, false);
            RectTransform headRt = (RectTransform)headGo.transform;
            headRt.anchorMin = new Vector2(0f, 1f); headRt.anchorMax = new Vector2(1f, 1f);
            headRt.pivot = new Vector2(0.5f, 1f);
            headRt.anchoredPosition = new Vector2(0f, -4f);
            headRt.sizeDelta = new Vector2(0f, 24f);

            Text headTxt = headGo.AddComponent<Text>();
            headTxt.font = font;
            headTxt.text = "📜 TASKS & OBJECTIVES";
            headTxt.fontSize = 11;
            headTxt.fontStyle = FontStyle.Bold;
            headTxt.color = new Color(0.35f, 0.22f, 0.16f, 1f);
            headTxt.alignment = TextAnchor.MiddleCenter;
            headTxt.raycastTarget = false;

            // Collapse Toggle Button
            GameObject togGo = new GameObject("ToggleBtn", typeof(RectTransform), typeof(Button), typeof(UIHoverScale));
            togGo.transform.SetParent(_cardGo.transform, false);
            RectTransform togRt = (RectTransform)togGo.transform;
            togRt.anchorMin = new Vector2(1f, 1f); togRt.anchorMax = new Vector2(1f, 1f);
            togRt.pivot = new Vector2(1f, 1f);
            togRt.anchoredPosition = new Vector2(-4f, -4f);
            togRt.sizeDelta = new Vector2(22f, 22f);
            var togBtn = togGo.GetComponent<Button>();
            togBtn.onClick.AddListener(ToggleCollapse);

            GameObject togTxtGo = new GameObject("Icon", typeof(RectTransform));
            togTxtGo.transform.SetParent(togGo.transform, false);
            RectTransform ttRt = (RectTransform)togTxtGo.transform;
            ttRt.anchorMin = Vector2.zero; ttRt.anchorMax = Vector2.one;
            ttRt.offsetMin = Vector2.zero; ttRt.offsetMax = Vector2.zero;
            var tt = togTxtGo.AddComponent<Text>();
            tt.font = font;
            tt.text = "▲";
            tt.fontSize = 9;
            tt.alignment = TextAnchor.MiddleCenter;
            tt.color = new Color(0.45f, 0.32f, 0.24f, 1f);
            tt.raycastTarget = false;

            // Content Container
            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_cardGo.transform, false);
            _contentRt = (RectTransform)contentGo.transform;
            _contentRt.anchorMin = Vector2.zero; _contentRt.anchorMax = Vector2.one;
            _contentRt.offsetMin = new Vector2(8f, 6f); _contentRt.offsetMax = new Vector2(-8f, -28f);
        }

        public void ToggleCollapse()
        {
            _isCollapsed = !_isCollapsed;
            if (_contentRt != null) _contentRt.gameObject.SetActive(!_isCollapsed);

            RectTransform rootRt = (RectTransform)_rootGo.transform;
            if (rootRt != null)
            {
                rootRt.sizeDelta = _isCollapsed ? new Vector2(230f, 32f) : new Vector2(230f, 210f);
            }
        }

        public void RefreshEntries()
        {
            if (ObjectiveManager.Instance == null || _contentRt == null) return;

            foreach (var go in _entryGameObjects)
            {
                if (go != null) Destroy(go);
            }
            _entryGameObjects.Clear();

            Font font = UIResourceHelper.GetPixelFont();
            var list = ObjectiveManager.Instance.Objectives;
            float rowH = 24f;

            for (int i = 0; i < list.Count; i++)
            {
                var obj = list[i];

                GameObject rowGo = new GameObject($"ObjRow_{i}", typeof(RectTransform));
                rowGo.transform.SetParent(_contentRt, false);
                RectTransform rowRt = (RectTransform)rowGo.transform;
                rowRt.anchorMin = new Vector2(0f, 1f); rowRt.anchorMax = new Vector2(1f, 1f);
                rowRt.pivot = new Vector2(0.5f, 1f);
                rowRt.anchoredPosition = new Vector2(0f, -i * rowH);
                rowRt.sizeDelta = new Vector2(0f, rowH);

                // Checkbox Box
                GameObject checkGo = new GameObject("Check", typeof(RectTransform));
                checkGo.transform.SetParent(rowGo.transform, false);
                RectTransform cRt = (RectTransform)checkGo.transform;
                cRt.anchorMin = new Vector2(0f, 0.5f); cRt.anchorMax = new Vector2(0f, 0.5f);
                cRt.pivot = new Vector2(0f, 0.5f);
                cRt.anchoredPosition = new Vector2(2f, 0f);
                cRt.sizeDelta = new Vector2(16f, 16f);

                Text cTxt = checkGo.AddComponent<Text>();
                cTxt.font = font;
                cTxt.fontSize = 11;
                cTxt.text = obj.isCompleted ? "<color=#2E8B57>✔</color>" : "<color=#8B7355>☐</color>";
                cTxt.alignment = TextAnchor.MiddleCenter;
                cTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                cTxt.verticalOverflow = VerticalWrapMode.Overflow;
                cTxt.raycastTarget = false;

                // Title + Counter Text
                GameObject labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(rowGo.transform, false);
                RectTransform lRt = (RectTransform)labelGo.transform;
                lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
                lRt.offsetMin = new Vector2(20f, 0f); lRt.offsetMax = new Vector2(0f, 0f);

                Text lTxt = labelGo.AddComponent<Text>();
                lTxt.font = font;
                lTxt.fontSize = 10;
                lTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                lTxt.verticalOverflow = VerticalWrapMode.Overflow;
                lTxt.alignment = TextAnchor.MiddleLeft;
                lTxt.raycastTarget = false;

                if (obj.isCompleted)
                {
                    lTxt.text = $"<s>{obj.title}</s>";
                    lTxt.color = new Color(0.55f, 0.50f, 0.45f, 0.85f);
                }
                else
                {
                    string counter = obj.targetCount > 1 ? $" <color=#A0652C>({obj.currentCount}/{obj.targetCount})</color>" : "";
                    lTxt.text = $"{obj.title}{counter}";
                    lTxt.color = new Color(0.28f, 0.18f, 0.12f, 1f);
                }

                _entryGameObjects.Add(rowGo);
            }
        }
    }
}
