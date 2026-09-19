using System;
using System.Linq;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Oathfire.Quests
{
    /// <summary>
    /// Answers "where do I go next?" for the quest arrow. Objectives only name flags, counters and items, so
    /// Resources/Quests/guides.json says where each one is done; from another map the answer is the road
    /// that leads toward it. Main quests are guided before side quests.
    /// </summary>
    public static class QuestGuide
    {
        const float EnemyPullRange = 14f;

        [Serializable] class Guide { public string key; public string target; public string id; public string scene; }
        [Serializable] class Route { public string from; public string to; public string via; }
        [Serializable] class Table { public Route[] routes; public Guide[] guides; }

        public struct Waypoint
        {
            public Vector3 position;
            public QuestDefinition quest;
            /// <summary>True when the arrow points at a road out of this map rather than the goal itself.</summary>
            public bool travel;
        }

        static Table table;

        static Table Data
        {
            get
            {
                if (table != null)
                    return table;
                var asset = Resources.Load<TextAsset>("Quests/guides");
                table = asset ? JsonUtility.FromJson<Table>(asset.text) : new Table();
                if (!asset)
                    Debug.LogError("[Oathfire] Missing Resources/Quests/guides.json; the quest arrow has nowhere to point");
                table.routes ??= Array.Empty<Route>();
                table.guides ??= Array.Empty<Guide>();
                return table;
            }
        }

        public static bool TryFind(Vector3 from, out Waypoint waypoint)
        {
            QuestService service = QuestRuntime.Service;
            // A quest the Warden chose to follow is the only thing the arrow answers for. Falling back to the
            // story whenever it has no fixed place would make the choice look ignored.
            QuestDefinition tracked = QuestRuntime.TrackedQuest();
            if (tracked != null)
                return TryQuest(tracked, service, from, out waypoint);

            foreach (bool main in new[] { true, false })
            {
                foreach (QuestDefinition quest in service.Quests)
                {
                    if (quest.mainQuest != main || !service.IsAvailable(quest))
                        continue;
                    QuestStage stage = service.CurrentStage(quest);
                    if (stage?.objectives == null)
                        continue;
                    if (service.IsReadyToHandIn(quest))
                    {
                        if (TryGiver(quest, from, out waypoint.position, out waypoint.travel))
                        {
                            waypoint.quest = quest;
                            return true;
                        }
                        continue;
                    }
                    foreach (QuestObjective objective in stage.objectives)
                    {
                        if (objective.optional || service.IsObjectiveMet(objective))
                            continue;
                        if (TryResolve(objective, from, out waypoint.position, out waypoint.travel))
                        {
                            waypoint.quest = quest;
                            return true;
                        }
                    }
                }
            }
            waypoint = default;
            return false;
        }

        static bool TryQuest(QuestDefinition quest, QuestService service, Vector3 from, out Waypoint waypoint)
        {
            waypoint = default;
            if (service.IsReadyToHandIn(quest))
            {
                waypoint.quest = quest;
                return TryGiver(quest, from, out waypoint.position, out waypoint.travel);
            }
            QuestStage stage = service.CurrentStage(quest);
            if (stage?.objectives == null)
                return false;
            foreach (QuestObjective objective in stage.objectives)
            {
                if (objective.optional || service.IsObjectiveMet(objective))
                    continue;
                if (TryResolve(objective, from, out waypoint.position, out waypoint.travel))
                {
                    waypoint.quest = quest;
                    return true;
                }
            }
            return false;
        }

        static bool TryResolve(QuestObjective objective, Vector3 from, out Vector3 point, out bool travel)
        {
            travel = false;
            // A kill objective is best answered by the enemy already in reach.
            if (objective.key.StartsWith("kills.") && TryNearestEnemy(from, out point))
                return true;

            point = default;
            Guide guide = Data.guides.FirstOrDefault(entry => entry.key == objective.key);
            if (guide == null)
                return false;

            string here = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(guide.scene) && guide.scene != here)
            {
                Route route = Data.routes.FirstOrDefault(entry => entry.from == here && entry.to == guide.scene);
                travel = true;
                return route != null && TryExit(route.via, from, out point);
            }

            switch (guide.target)
            {
                case "npc": return TryNearest(from, FindAll<World.NpcDialogue>().Where(npc => npc.SpeakerKey == guide.id), out point);
                case "hearth": return TryNearest(from, World.OathfireHearth.Instance ? new[] { World.OathfireHearth.Instance } : Array.Empty<World.OathfireHearth>(), out point);
                case "board": return TryNearest(from, FindAll<World.NoticeBoard>(), out point);
                case "gather": return TryNearest(from, FindAll<World.GatherNode>().Where(node => node.ItemId == guide.id && node.IsAvailable), out point);
                case "build": return TryNearest(from, FindAll<World.BuildSite>().Where(site => !site.IsBuilt), out point);
                case "exit": return TryExit(guide.id, from, out point);
                case "spot": return TryNearest(from, FindAll<World.QuestSpot>().Where(spot => spot.Id == guide.id && spot.IsActive), out point);
                default:
                    Debug.LogWarning($"[Oathfire] Unknown quest guide target '{guide.target}' for {guide.key}");
                    return false;
            }
        }

        /// <summary>The person the work goes back to: here if they stand in this map, else the road toward them.</summary>
        static bool TryGiver(QuestDefinition quest, Vector3 from, out Vector3 point, out bool travel)
        {
            travel = false;
            if (TryNearest(from, FindAll<World.NpcDialogue>().Where(npc => npc.SpeakerKey == quest.giver), out point))
                return true;
            string here = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(quest.giverScene) || quest.giverScene == here)
                return false;
            Route route = Data.routes.FirstOrDefault(entry => entry.from == here && entry.to == quest.giverScene);
            travel = true;
            return route != null && TryExit(route.via, from, out point);
        }

        static T[] FindAll<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        static bool TryExit(string destination, Vector3 from, out Vector3 point) =>
            TryNearest(from, FindAll<World.SceneExit>().Where(exit => exit.Destination == destination), out point);

        static bool TryNearestEnemy(Vector3 from, out Vector3 point) =>
            TryNearest(from, EnemyHealth2D.All.Where(enemy => enemy && !enemy.IsDead
                && Vector2.Distance(from, enemy.transform.position) < EnemyPullRange), out point);

        static bool TryNearest<T>(Vector3 from, System.Collections.Generic.IEnumerable<T> candidates, out Vector3 point) where T : Component
        {
            T best = candidates.OrderBy(candidate => (candidate.transform.position - from).sqrMagnitude).FirstOrDefault();
            point = best ? best.transform.position : default;
            return best;
        }
    }
}
