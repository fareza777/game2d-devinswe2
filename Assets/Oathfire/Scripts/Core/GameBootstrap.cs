using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Oathfire.Core
{
    /// <summary>
    /// First object alive in the game. Creates the persistent services, shows the splash, then hands over
    /// to the title screen. Every scene can be entered directly in the editor: services self-create if missing.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        public const string TitleScene = "Title";

        static GameBootstrap instance;

        [SerializeField] float splashSeconds = 4.2f;

        public static bool IsBooted => instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigureRuntime()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
        }

        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            GameServices.EnsureCreated();
        }

        IEnumerator Start()
        {
            var splash = FindAnyObjectByType<UI.SplashScreen>(FindObjectsInactive.Include);
            if (splash)
                yield return splash.PlayRoutine(splashSeconds);

            if (SceneManager.GetActiveScene().name != TitleScene)
                yield return GameServices.Flow.LoadSceneRoutine(TitleScene);
        }
    }
}
