namespace Pathfinding
{
    public class PathNode
    {
        public Cell Cell { get; }

        public PathNode(Cell cell)
        {
            Cell = cell;
        }

        public int Distance { get; set; }

        public int SearchHeuristic { get; set; }
    
        public int SearchPriority => Distance + SearchHeuristic;

        public int SearchPhase { get; set; }

        public PathNode NextWithSamePriority { get; set; }

        public PathNode PathFrom { get; set; }
        
        public Cell GetNeighbor(HexDirection direction) => Cell.GetNeighbor(direction);
    }
}
