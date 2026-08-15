using TMPro;
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
        
        /// <summary>
        /// Initializes the notification bar layout, text content, and animations.
        /// </summary>
        public void Initialize(Sprite icon, string textContent, Color iconColor)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            Sprite slicedBg = UIResourceHelper.GetBackgroundSprite();
            Sprite innerBg = UIResourceHelper.GetInputFieldBackgroundSprite();

            // ── Outer Shadow ──
            GameObject shadowGo = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(transform, false);
            RectTransform shadowRt = (RectTransform)shadowGo.transform;
            shadowRt.anchorMin = Vector2.zero; shadowRt.anchorMax = Vector2.one;
            shadowRt.offsetMin = new Vector2(-4f, -4f); shadowRt.offsetMax = new Vector2(4f, 4f);
            Image shadowImg = shadowGo.GetComponent<Image>();
            shadowImg.sprite = slicedBg;
            shadowImg.type = Image.Type.Sliced;
            shadowImg.color = new Color(0f, 0f, 0f, 0.5f);

            // ── Wood Frame ──
            _bgImage = gameObject.AddComponent<Image>();
            _bgImage.sprite = slicedBg;
            _bgImage.type = Image.Type.Sliced;
            _bgImage.color = new Color(0.24f, 0.17f, 0.11f, 0.98f); // Rich walnut wood

            // ── Gold Border Trim ──
            GameObject trimGo = new GameObject("GoldTrim", typeof(RectTransform), typeof(Image));
            trimGo.transform.SetParent(transform, false);
            RectTransform trimRt = (RectTransform)trimGo.transform;
            trimRt.anchorMin = Vector2.zero; trimRt.anchorMax = Vector2.one;
            trimRt.offsetMin = new Vector2(2f, 2f); trimRt.offsetMax = new Vector2(-2f, -2f);
            Image trimImg = trimGo.GetComponent<Image>();
            trimImg.sprite = slicedBg;
            trimImg.type = Image.Type.Sliced;
            trimImg.color = new Color(0.72f, 0.58f, 0.32f, 0.65f); // Warm gold trim

            // ── Inner Dark Board ──
            GameObject innerGo = new GameObject("InnerBoard", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(trimGo.transform, false);
            RectTransform innerRt = (RectTransform)innerGo.transform;
            innerRt.anchorMin = Vector2.zero; innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(2f, 2f); innerRt.offsetMax = new Vector2(-2f, -2f);
            Image innerBoardImg = innerGo.GetComponent<Image>();
            innerBoardImg.sprite = innerBg;
            innerBoardImg.type = Image.Type.Sliced;
            innerBoardImg.color = new Color(0.10f, 0.08f, 0.07f, 0.96f);

            // ── Icon Badge ──
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(innerGo.transform, false);
            _iconImage = iconGo.GetComponent<Image>();
            _iconImage.sprite = icon;
            _iconImage.color = iconColor;
            
            RectTransform iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(24f, 24f);

            // ── Text Label ──
            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(innerGo.transform, false);
            var tmpText = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmpText.font = font;
            tmpText.text = textContent;
            tmpText.fontSize = 13.5f;
            tmpText.fontStyle = FontStyles.Bold;
            tmpText.color = new Color(1f, 0.92f, 0.78f, 1f); // Warm ivory/parchment
            tmpText.alignment = TextAlignmentOptions.MidlineLeft;
            tmpText.richText = true;

            RectTransform textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(38f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);

            StartCoroutine(AnimateToast());
        }

        private System.Collections.IEnumerator AnimateToast()
        {
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
                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t * (2f - t));
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            rect.anchoredPosition = targetPos;

            yield return new WaitForSeconds(2.2f);

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
