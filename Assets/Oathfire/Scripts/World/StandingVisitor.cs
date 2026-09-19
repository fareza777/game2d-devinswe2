using System.Collections;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// A visitor who has stopped waiting at the edge of the square and simply stays in the settlement until
    /// someone speaks to them. The event director hands them over to this so that one ignored rider cannot
    /// hold up every other arrival in the chapter; when their conversation finishes they leave on their own.
    /// </summary>
    public class StandingVisitor : MonoBehaviour
    {
        const float FadeSeconds = 1.5f;

        string doneFlag;
        System.Action onLeft;
        bool leaving;

        public static void Hold(GameObject actor, string doneFlag, System.Action onLeft)
        {
            var standing = actor.AddComponent<StandingVisitor>();
            standing.doneFlag = doneFlag;
            standing.onLeft = onLeft;
        }

        void Update()
        {
            if (leaving)
                return;
            Save.SaveData save = Core.GameServices.Save.Current;
            if (save == null || !save.HasFlag(doneFlag))
                return;
            // Never vanish mid-sentence: wait for the conversation that set the flag to close.
            if (Dialogue.DialogueRunner.IsConversationActive)
                return;
            leaving = true;
            StartCoroutine(Leave());
        }

        IEnumerator Leave()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
            for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
            {
                float alpha = 1f - t / FadeSeconds;
                foreach (SpriteRenderer renderer in renderers)
                    if (renderer)
                        renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, alpha);
                yield return null;
            }
            onLeft?.Invoke();
            Destroy(gameObject);
        }
    }
}
