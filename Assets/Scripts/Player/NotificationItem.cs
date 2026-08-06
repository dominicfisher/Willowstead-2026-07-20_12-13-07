using UnityEngine;
using UnityEngine.UI;

namespace Willowstead.Player
{
    /// <summary>
    /// Represents a single active toast notification bar in the bottom right corner.
    /// Programmatically constructs its elements and animates a slide-in, wait, and fade-out transition.
    /// </summary>
    public class NotificationItem : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;
        private Image _bgImage;
        private Image _iconImage;
        private Text _text;
        
        /// <summary>
        /// Initializes the notification bar layout, text content, and animations.
        /// </summary>
        public void Initialize(Sprite icon, string textContent, Color iconColor)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Sprite roundedBg = UIResourceHelper.GetBackgroundSprite();

            // 1. Background sliced panel
            _bgImage = gameObject.AddComponent<Image>();
            _bgImage.sprite = roundedBg;
            _bgImage.type = Image.Type.Sliced;
            _bgImage.color = new Color(0.14f, 0.12f, 0.1f, 0.95f); // Slate dark brown

            // 2. Icon image
            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(transform, false);
            _iconImage = iconGo.AddComponent<Image>();
            _iconImage.sprite = icon;
            _iconImage.color = iconColor;
            
            RectTransform iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(10f, 0f);
            iconRect.sizeDelta = new Vector2(22f, 22f);

            // 3. Quantity / Item Text
            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(transform, false);
            _text = textGo.AddComponent<Text>();
            _text.text = textContent;
            _text.font = legacyFont;
            _text.fontSize = 12;
            _text.fontStyle = FontStyle.Bold;
            _text.color = Color.white;
            _text.alignment = TextAnchor.MiddleLeft;

            // Simple text shadow outline
            textGo.AddComponent<Outline>().effectColor = Color.black;

            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(20f, 0f); // Position next to icon
            textRect.sizeDelta = new Vector2(-46f, 0f);       // Inner paddings

            StartCoroutine(AnimateToast());
        }

        private System.Collections.IEnumerator AnimateToast()
        {
            // Phase 1: Fade & Slide In from the right
            float slideDuration = 0.22f;
            float elapsed = 0f;
            RectTransform rect = GetComponent<RectTransform>();
            Vector2 targetPos = rect.anchoredPosition;
            Vector2 startPos = targetPos + new Vector2(80f, 0f); // Slide offset

            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / slideDuration;

                _canvasGroup.alpha = t;
                // Ease Out sliding curve
                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t * (2f - t));
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            rect.anchoredPosition = targetPos;

            // Phase 2: Stay visible for 2.2 seconds
            yield return new WaitForSeconds(2.2f);

            // Phase 3: Fade out slowly and self-destruct
            float fadeDuration = 0.25f;
            elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                _canvasGroup.alpha = 1f - t;
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
