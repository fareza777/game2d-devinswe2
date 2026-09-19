using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Oathfire.Core
{
    /// <summary>Scene changes with a full-screen ink fade, so transitions never pop.</summary>
    public class SceneFlow : MonoBehaviour
    {
        const float DefaultFade = 0.45f;
        static readonly Color Ink = new Color(0.055f, 0.063f, 0.051f);

        Image curtain;
        Canvas canvas;

        public bool IsBusy { get; private set; }

        /// <summary>The scene the player just came from, so they arrive at the road that leads back to it.</summary>
        public static string PreviousScene { get; private set; }

        void Awake()
        {
            var go = new GameObject("FadeCurtain", typeof(Canvas), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            curtain = go.GetComponent<Image>();
            curtain.color = new Color(Ink.r, Ink.g, Ink.b, 0f);
            curtain.raycastTarget = false;
            var rect = curtain.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Waits for the scene change to finish. The work itself always runs on this persistent object:
        /// a caller that gets destroyed by the very scene change it asked for must not abort the transition
        /// half way, or the busy flag never clears and the game locks up.
        /// </summary>
        public IEnumerator LoadSceneRoutine(string sceneName, float fadeSeconds = DefaultFade)
        {
            yield return StartCoroutine(TransitionRoutine(sceneName, fadeSeconds));
        }

        public void LoadScene(string sceneName, float fadeSeconds = DefaultFade) =>
            StartCoroutine(TransitionRoutine(sceneName, fadeSeconds));

        IEnumerator TransitionRoutine(string sceneName, float fadeSeconds)
        {
            // A request that arrives mid-transition waits its turn instead of being dropped: silently
            // ignoring it makes buttons pressed during a fade look broken.
            while (IsBusy)
                yield return null;
            IsBusy = true;

            yield return FadeRoutine(1f, fadeSeconds);
            PreviousScene = SceneManager.GetActiveScene().name;
            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
            while (!load.isDone)
                yield return null;
            yield return null; // let the new scene's Start() run before revealing it
            yield return FadeRoutine(0f, fadeSeconds);

            IsBusy = false;
        }

        public IEnumerator FadeRoutine(float targetAlpha, float seconds)
        {
            float start = curtain.color.a;
            curtain.raycastTarget = targetAlpha > 0.01f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float alpha = Mathf.Lerp(start, targetAlpha, t / seconds);
                curtain.color = new Color(Ink.r, Ink.g, Ink.b, alpha);
                yield return null;
            }
            curtain.color = new Color(Ink.r, Ink.g, Ink.b, targetAlpha);
            curtain.raycastTarget = targetAlpha > 0.01f;
        }
    }
}
