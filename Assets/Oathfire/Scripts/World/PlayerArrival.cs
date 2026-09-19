using System.Linq;
using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Puts the hero at the road they came in by. Every map used to start the player at one fixed spot, so
    /// coming back from Greymarch or the Hollow dropped them at the far end of the trade road from the path
    /// they had just walked. A fresh start or a loaded save keeps the map's own starting point.
    /// </summary>
    public class PlayerArrival : MonoBehaviour
    {
        const float StandOff = 1.8f;

        /// <summary>Where the hero was placed on arrival, or null when the map's own start was kept.</summary>
        public static string ArrivedBy { get; private set; }

        void Start()
        {
            ArrivedBy = null;
            string from = Core.SceneFlow.PreviousScene;
            if (string.IsNullOrEmpty(from))
                return;
            SceneExit exit = FindObjectsByType<SceneExit>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.Destination == from);
            if (!exit)
                return;

            // A few steps in from the road, far enough that the travel prompt is not the first thing offered.
            Vector2 road = exit.transform.position;
            Vector3 spot = SpawnPlanner.FindOpenPoint(road, road, StandOff * 0.8f, StandOff * 1.4f);
            transform.position = new Vector3(spot.x, spot.y, transform.position.z);
            var body = GetComponent<Rigidbody2D>();
            if (body)
                body.position = spot;
            Camera camera = Camera.main;
            if (camera)
                camera.transform.position = new Vector3(spot.x, spot.y, camera.transform.position.z);
            GetComponent<SmallScale.FantasyKingdomTileset.PlayerHealth>()?.SetSpawnPoint(transform.position, transform.rotation);
            ArrivedBy = from;
        }
    }
}
