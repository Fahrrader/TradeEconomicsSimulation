using ScriptableObjects;
using UnityEngine;

namespace Pathfinding
{
    public static class Pathfinding
    {
        private static PathPriorityQueue _searchFrontier;
        private static int _searchFrontierPhase;

        public static float FindPathTime(Cell start, Cell end, float[] speedMultipliers)
        {
            var timeToTravel = 0f;
        
            FindPath(start, end, speedMultipliers);

            for (var cell = end; cell != start; cell = cell.GetNeighbor(cell.PathFrom))
            {
                timeToTravel += cell.moveCostTo[(int) cell.PathFrom.Opposite()];
            }

            return timeToTravel;
        }

        public static void FindPath(Cell start, Cell end, float[] speedMultipliers)
        {
            _searchFrontierPhase += 2;
            if (_searchFrontier == null) {
                _searchFrontier = new PathPriorityQueue();
            }
            else {
                _searchFrontier.Clear();
            }

            start.SearchPhase = _searchFrontierPhase;
            start.Distance = 0;
            _searchFrontier.Enqueue(start);

            var aquaticMul = speedMultipliers[(int) WareData.Vehicle.VehicleType.Water];
            var landMul = speedMultipliers[(int) WareData.Vehicle.VehicleType.Land];
            if (speedMultipliers[(int) WareData.Vehicle.VehicleType.Air] > aquaticMul) 
                aquaticMul = speedMultipliers[(int) WareData.Vehicle.VehicleType.Air];
            if (speedMultipliers[(int) WareData.Vehicle.VehicleType.Air] > landMul) 
                landMul = speedMultipliers[(int) WareData.Vehicle.VehicleType.Air];

            while (_searchFrontier.Count > 0)
            {
                var current = _searchFrontier.Dequeue();
                current.SearchPhase += 1;

                if (current == end) return;

                for (var dir = HexDirection.NE; dir <= HexDirection.NW; dir++)
                {
                    var neighbor = current.GetNeighbor(dir);
                    if (
                        neighbor == null ||
                        neighbor.SearchPhase > _searchFrontierPhase
                    )
                        continue;

                    var moveCost = Mathf.RoundToInt(current.moveCostTo[(int) dir] /
                                                    (current.isAquaticMovementTo[(int) dir] ? aquaticMul : landMul));

                    var distance = current.Distance + moveCost;

                    if (neighbor.SearchPhase < _searchFrontierPhase)
                    {
                        neighbor.SearchPhase = _searchFrontierPhase;
                        neighbor.Distance = distance;
                        neighbor.PathFrom = dir.Opposite();//current;
                        neighbor.SearchHeuristic =
                            neighbor.GetDistance(end);
                        _searchFrontier.Enqueue(neighbor);
                    }
                    else if (distance < neighbor.Distance)
                    {
                        var oldPriority = neighbor.SearchPriority;
                        neighbor.Distance = distance;
                        neighbor.PathFrom = dir.Opposite();//current;
                        _searchFrontier.Change(neighbor, oldPriority);
                    }
                }
            }
        }
    }
}