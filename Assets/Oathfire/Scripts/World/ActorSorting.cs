using UnityEngine;

namespace Oathfire.World
{
    /// <summary>
    /// Sorts and sizes people.
    ///
    /// Characters use the order the tileset's artist gave them: 1, above walls and props (which sort against
    /// them along Y) and below the roofs on 2, 11 and 15. An earlier order of 20 drew everyone over the roofs,
    /// so a villager standing behind a house appeared on top of it. The hero is kept visible around buildings
    /// by RoofFader instead, which lifts the roof they are inside or behind.
    /// </summary>
    public static class ActorSorting
    {
        /// <summary>The pack's own character order: over walls and props, under roofs.</summary>
        public const int ActorOrder = 1;

        /// <summary>
        /// How much larger people are drawn than the pack ships them.
        ///
        /// Every character in the pack is 128px at 100 pixels per unit, which reads well with a close
        /// camera. Ours sits back far enough to show a house and its street, and at that distance an
        /// unscaled character is a smudge. Everyone is enlarged by the same amount, so the cast stays
        /// consistent with itself.
        /// </summary>
        public const float ActorScale = 1.5f;

        public static void Raise(GameObject actor, int order = ActorOrder)
        {
            if (!actor)
                return;
            foreach (SpriteRenderer renderer in actor.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.sortingOrder = order;
            actor.transform.localScale = Vector3.one * ActorScale;
        }
    }
}
