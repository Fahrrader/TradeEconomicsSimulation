using UnityEngine;

namespace Pathfinding
{
    public class PathNodeOld : IHeapItem<PathNodeOld>
    {
        public Vector3 worldPos;
        public int gridX;
        public int gridY;
        public float movementCost;

        public PathNodeOld parent;
        public float gCost; // cost from start to this
        public float hCost; // heuristic from this to end
 
        public float FCost => gCost + hCost;

        public PathNodeOld(Vector3 worldPos, bool walkable, int x, int y, float cost)
        {
            this.worldPos = worldPos;
            gridX = x;
            gridY = y;
            movementCost = cost;
        }

        public int HeapIndex { get; set; }

        public int CompareTo(PathNodeOld nodeToCompare)
        {
            var compare = FCost.CompareTo(nodeToCompare.FCost);
            if (compare == 0)
                compare = hCost.CompareTo(nodeToCompare.hCost);
            return -compare;
        }
    }
}
