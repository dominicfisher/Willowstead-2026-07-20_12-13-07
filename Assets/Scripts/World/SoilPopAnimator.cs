using System.Collections.Generic;
using UnityEngine;

namespace Willowstead.World
{
    /// <summary>
    /// Animates a temporary visual block of tilled soil popping up out of the ground
    /// accompanied by a physical particle burst of dirt flying outwards.
    /// Once the animation is complete, it calls back to draw the permanent tile asset
    /// and destroys itself.
    /// </summary>
    public class SoilPopAnimator : MonoBehaviour
    {
        private Sprite _soilSprite;
        private Sprite _particleSprite;
        private Color _particleColor;
        private System.Action _onComplete;

        /// <summary>
        /// Initializes and starts the pop animation.
        /// </summary>
        public void Initialize(Sprite soilSprite, Sprite particleSprite, Color particleColor, System.Action onComplete)
        {
            _soilSprite = soilSprite;
            _particleSprite = particleSprite;
            _particleColor = particleColor;
            _onComplete = onComplete;

            StartCoroutine(AnimateSoilAndParticles());
        }

        private System.Collections.IEnumerator AnimateSoilAndParticles()
        {
            float duration = 0.22f;
            float elapsed = 0f;

            GameObject soilGo = new GameObject("SoilVisual");
            soilGo.transform.SetParent(transform, false);
            SpriteRenderer soilSr = soilGo.AddComponent<SpriteRenderer>();
            soilSr.sprite = _soilSprite;
            soilSr.sortingOrder = -100;

            int particleCount = 6;
            List<Transform> particles = new List<Transform>();
            List<Vector3> velocities = new List<Vector3>();

            if (_particleSprite != null)
            {
                for (int i = 0; i < particleCount; i++)
                {
                    GameObject pGo = new GameObject($"DirtParticle_{i}");
                    pGo.transform.SetParent(transform, false);
                    pGo.transform.localPosition = new Vector3(0f, -0.05f, 0f);

                    SpriteRenderer pSr = pGo.AddComponent<SpriteRenderer>();
                    pSr.sprite = _particleSprite;
                    pSr.color = _particleColor;
                    pSr.sortingOrder = 10;

                    float size = Random.Range(0.08f, 0.16f);
                    pGo.transform.localScale = new Vector3(size, size, 1f);

                    particles.Add(pGo.transform);

                    velocities.Add(new Vector3(
                        Random.Range(-1.3f, 1.3f),
                        Random.Range(1.2f, 2.5f), // upward launch
                        0f
                    ));
                }
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;

                float scaleMultiplier = Mathf.Sin(percent * Mathf.PI * 0.5f); // ease out
                float bounce = Mathf.Sin(percent * Mathf.PI) * 0.12f * (1f - percent); // overshoot bounce
                float currentScale = scaleMultiplier + bounce;
                
                soilGo.transform.localScale = new Vector3(currentScale, currentScale, 1f);

                for (int i = 0; i < particles.Count; i++)
                {
                    if (particles[i] == null) continue;

                    Vector3 vel = velocities[i];
                    vel.y -= 9.81f * Time.deltaTime;
                    velocities[i] = vel;

                    particles[i].localPosition += vel * Time.deltaTime;

                    particles[i].localScale = Vector3.Lerp(particles[i].localScale, Vector3.zero, Time.deltaTime * 5f);
                }

                yield return null;
            }

            _onComplete?.Invoke();

            Destroy(gameObject);
        }
    }
}
