using System;
using UnityEngine;

namespace Willowstead.World
{
    /// <summary>
    /// Handles the visual flight of a harvested item sprite from its world position
    /// directly into a target UI RectTransform slot on the Canvas screen space.
    /// Tracks the target location dynamically and invokes a callback on arrival.
    /// </summary>
    public class FlyingItemAnimation : MonoBehaviour
    {
        /// <summary>
        /// Spawns a flying item animation instance.
        /// </summary>
        public static void Spawn(Sprite sprite, Vector3 startWorldPos, RectTransform uiTarget, Action onArrival, Color? tintColor = null)
        {
            GameObject go = new GameObject("FlyingItemVisual");
            go.transform.position = startWorldPos;
            go.transform.localScale = Vector3.zero; // Starts invisible / flat

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tintColor ?? Color.white;
            sr.sortingOrder = 1000; // Draw on top of all normal world elements

            FlyingItemAnimation anim = go.AddComponent<FlyingItemAnimation>();
            anim.StartCoroutine(anim.AnimateFly(startWorldPos, uiTarget, onArrival));
        }

        private System.Collections.IEnumerator AnimateFly(Vector3 startPos, RectTransform uiTarget, Action onArrival)
        {
            // Phase 1: Jump up out of the ground bouncily
            float jumpDuration = 0.25f;
            float elapsed = 0f;
            
            // Roll a slight random peak offset
            Vector3 peakPos = startPos + new Vector3(
                UnityEngine.Random.Range(-0.35f, 0.35f),
                UnityEngine.Random.Range(0.45f, 0.75f),
                0f
            );

            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / jumpDuration;

                // Scale up from 0 to 1.0 bouncily
                float scale = Mathf.Lerp(0f, 1.0f, t);
                transform.localScale = new Vector3(scale, scale, 1f);

                // Quadratic parabolic arc
                float height = Mathf.Sin(t * Mathf.PI) * 0.45f;
                transform.position = Vector3.Lerp(startPos, peakPos, t) + new Vector3(0f, height, 0f);

                yield return null;
            }

            // Phase 2: Track and fly directly towards the UI Hotbar slot on the screen
            float flyDuration = 0.45f;
            elapsed = 0f;
            Vector3 flightStartPos = transform.position;

            while (elapsed < flyDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flyDuration;

                // Ease In / Accelerate towards slot
                float tSmooth = t * t * (3f - 2f * t);

                // Dynamically fetch slot's world coordinate (re-evaluating in case camera moves)
                Vector3 targetWorldPos = GetTargetWorldPos(uiTarget);

                transform.position = Vector3.Lerp(flightStartPos, targetWorldPos, tSmooth);

                // Spin the item in mid-air
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 360f, t));

                // Scale down slightly to match slot overlay sizing as it hits the HUD
                float scale = Mathf.Lerp(1.0f, 0.55f, t);
                transform.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            // Phase 3: Arrived
            onArrival?.Invoke();
            Destroy(gameObject);
        }

        private Vector3 GetTargetWorldPos(RectTransform rect)
        {
            if (rect == null)
            {
                // Fallback: bottom-center region of screen
                Vector3 fallbackScreenPos = new Vector3(Screen.width / 2f, 50f, 10f);
                Vector3 fallbackWorldPos = Camera.main.ScreenToWorldPoint(fallbackScreenPos);
                fallbackWorldPos.z = 0f;
                return fallbackWorldPos;
            }

            // ScreenSpaceOverlay canvas translates coordinates directly using camera screen point
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, rect.position);
            Vector3 screenPos3D = new Vector3(screenPos.x, screenPos.y, 10f);
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos3D);
            worldPos.z = 0f;
            return worldPos;
        }
    }
}
