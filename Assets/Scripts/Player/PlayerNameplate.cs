using TMPro;
using UnityEngine;
using Willowstead.UI;

namespace Willowstead.Player
{
    /// <summary>
    /// Displays the player's username badge / nameplate floating directly above their head in world space.
    /// Also handles real-time typing indicators (... bubbles).
    /// </summary>
    public class PlayerNameplate : MonoBehaviour
    {
        private TextMeshPro _nameText;
        private TextMeshPro _typingIndicator;
        private GameObject _nameplateRoot;

        private void Awake()
        {
            BuildWorldNameplate();
            UpdateAppearance();
        }

        private void BuildWorldNameplate()
        {
            _nameplateRoot = new GameObject("WorldNameplate");
            _nameplateRoot.transform.SetParent(transform, false);
            _nameplateRoot.transform.localPosition = new Vector3(0f, 0.95f, 0f);

            // Name Label
            GameObject nameGo = new GameObject("NameText");
            nameGo.transform.SetParent(_nameplateRoot.transform, false);
            nameGo.transform.localPosition = Vector3.zero;
            _nameText = nameGo.AddComponent<TextMeshPro>();
            _nameText.fontSize = 2.8f;
            _nameText.fontStyle = FontStyles.Bold;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.color = new Color(1f, 0.95f, 0.82f, 1f);
            _nameText.outlineWidth = 0.25f;
            _nameText.outlineColor = new Color32(20, 15, 10, 240);
            _nameText.sortingOrder = 100;

            // Typing Indicator (...)
            GameObject typingGo = new GameObject("TypingIndicator");
            typingGo.transform.SetParent(_nameplateRoot.transform, false);
            typingGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            _typingIndicator = typingGo.AddComponent<TextMeshPro>();
            _typingIndicator.text = "💬 <i>typing...</i>";
            _typingIndicator.fontSize = 2.0f;
            _typingIndicator.alignment = TextAlignmentOptions.Center;
            _typingIndicator.color = new Color(0.9f, 0.9f, 0.5f, 1f);
            _typingIndicator.outlineWidth = 0.2f;
            _typingIndicator.outlineColor = Color.black;
            _typingIndicator.sortingOrder = 101;
            typingGo.SetActive(false);
        }

        public void SetTyping(bool isTyping)
        {
            if (_typingIndicator != null)
            {
                _typingIndicator.gameObject.SetActive(isTyping);
            }
        }

        public void SetUsername(string username)
        {
            if (_nameText != null)
            {
                _nameText.text = username;
            }
        }

        public void UpdateAppearance()
        {
            string name = CharacterCreationUI.GetSavedUsername();
            SetUsername(name);

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = CharacterCreationUI.GetSavedShirtTint();
            }
        }

        public static void UpdateLocalPlayerAppearance()
        {
            if (PlayerController.Instance != null)
            {
                var np = PlayerController.Instance.GetComponent<PlayerNameplate>()
                    ?? PlayerController.Instance.gameObject.AddComponent<PlayerNameplate>();
                np.UpdateAppearance();
            }
        }
    }
}
