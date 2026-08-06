using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Willowstead.Player
{
    /// <summary>
    /// Plays a one-shot frame animation for a click effect, then disables itself.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ClickVFX : MonoBehaviour
    {
        private Image _image;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        /// <summary>
        /// Plays the provided sprite frames once and disables the GameObject at the end.
        /// </summary>
        public void Play(Sprite[] frames, float frameDuration)
        {
            if (frames == null || frames.Length == 0) return;
            if (_image == null) _image = GetComponent<Image>();

            // Important: a coroutine can only be started on an active GameObject.
            // The pool keeps these objects disabled between uses, so enable first,
            // then start the coroutine. SetActive also lets Awake initialize _image
            // on the very first use in case the pool was just created.
            gameObject.SetActive(true);

            StopAllCoroutines();
            StartCoroutine(Animate(frames, frameDuration));
        }

        private IEnumerator Animate(Sprite[] frames, float frameDuration)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null && _image != null)
                    _image.sprite = frames[i];

                yield return new WaitForSeconds(frameDuration);
            }

            gameObject.SetActive(false);
        }
    }
}
