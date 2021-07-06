using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class HexCell : MonoBehaviour
{
    public HexCoordinates coordinates;

    public RectTransform uiRect;

    public Image highlight;

    public HexGridChunk chunk;

    public Cell dataCell;

    public Color Color
    {
        get => color;
        set
        {
            if (color == value) return;
            var noise = (HexMetrics.SampleNoise(transform.localPosition).y * 2f - 1f) * HexMetrics.ColorPerturbStrength;
            var colorSum = value.r + value.g + value.b;
            color = new Color(
                value.r + noise * value.r / colorSum, 
                value.g + noise * value.g / colorSum,
                value.b + noise * value.b / colorSum
            );
            Refresh();
        }
    }

    public int Elevation
    {
        get => elevation;
        set
        {
            if (elevation == value) return;
            elevation = value;
            var position = transform.localPosition;
            position.y = value * HexMetrics.ElevationStep;
            transform.localPosition = position;

            var uiPosition = uiRect.localPosition;
            uiPosition.z = -position.y;
            uiRect.localPosition = uiPosition;

            ValidateRivers();
            
            UpdateRiverCenter();
            UpdateRoadCenters();

            Refresh();
        }
    }

    public int WaterLevel
    {
        get => waterLevel;
        set
        {
            if (waterLevel == value) return;
            waterLevel = value;
            ValidateRivers();
            
            UpdateRiverCenter();
            UpdateRoadCenters();
            
            Refresh();
        }
    }

    public bool IsUnderwater => dataCell.IsUnderwater; // waterLevel > elevation; 

    public bool HasIncomingRiver() => rivers.Any(river => river == -1);

    public bool HasIncomingRiver(HexDirection direction) => rivers[(int) direction] == -1;

    public bool HasOutgoingRiver() => rivers.Any(river => river == 1);

    public bool HasOutgoingRiver(HexDirection direction) => rivers[(int) direction] == 1;

    public bool HasRiver => HasOutgoingRiver() || HasIncomingRiver();

    public bool HasRiverBeginOrEnd => HasIncomingRiver() != HasOutgoingRiver();

    public List<HexDirection> OutgoingRivers
    {
        get
        {
            var list = new List<HexDirection>();
            for (var i = 0; i < rivers.Length; i++) if (rivers[i] == 1) list.Add((HexDirection) i);
            return list;
        }
    }

    public List<HexDirection> IncomingRivers 
    {
        get
        {
            var list = new List<HexDirection>();
            for (var i = 0; i < rivers.Length; i++) if (rivers[i] == -1) list.Add((HexDirection) i);
            return list;
        }
    }

    public int NumberOfRivers => rivers.Count(river => river != 0);

    public Vector3 riverCenter;

    public bool CanCrossUninterruptedByRivers(HexDirection directionIn, HexDirection directionOut)
    {
        for (var dir = directionIn.Next();; dir = dir.Next())
        {
            if (rivers[(int) dir] != 0) break;
            if (dir == directionOut) return true;
        }
        
        for (var dir = directionIn.Previous();; dir = dir.Previous())
        {
            if (rivers[(int) dir] != 0) break;
            if (dir == directionOut) return true;
        }
        
        return false;
    }

    public HexDirection ClosestRiver(HexDirection direction)
    {
        //if (!HasRiver) return direction;
        var dir = direction;
        for (var step = 0; step <= 3; step++)
        {
            dir = direction.Move(step);
            if (rivers[(int) dir] != 0) return dir;
            
            dir = direction.Move(-step);
            if (rivers[(int) dir] != 0) return dir;
        }

        return dir;
    }
    
    public HexDirection ClosestRiverNonInterlocking(HexDirection direction)
    {
        //if (!HasRiver) return direction;
        for (var step = 0; step <= 3; step++)
        {
            var dirR = direction.Move(step);
            var dirL = direction.Move(-step);

            if (rivers[(int) dirR] != 0 && rivers[(int) dirL] != 0) break;
            
            if (rivers[(int) dirR] != 0) return dirR;
            if (rivers[(int) dirL] != 0) return dirL;
        }

        return direction.Opposite();
    }

    private HexDirection MedianDirectionBetweenRivers(HexDirection direction)
    {
        HexDirection dirL, dirR;
        int stepsL = 0, stepsR = 0;
        for (dirL = direction.Next(); dirL != direction; dirL = dirL.Next())
        {
            stepsL++;
            if (rivers[(int) dirL] != 0) break;
        }
        
        for (dirR = direction.Previous(); dirR != direction; dirR = dirR.Previous())
        {
            stepsR++;
            if (rivers[(int) dirR] != 0) break;
        }

        return direction.Move((stepsL - stepsR) / 2);
    }

    public bool HasRoads => roads.Any(road => road);

    public bool CanHaveRoads => !walled && !IsUnderwater;

    public List<Vector3> roadCenters;
    public List<bool> roadCenterHasRoads;
    public sbyte[] roadCenterPointers = new sbyte[6];
    //public sbyte[] visibleRoadCenterPointers = new sbyte[6];

    public Vector3 Position => transform.localPosition;

    public float StreamBedY =>
        (elevation + HexMetrics.StreamBedElevationOffset) *
        HexMetrics.ElevationStep;

    public float RiverSurfaceY =>
        (elevation + HexMetrics.WaterElevationOffset) *
        HexMetrics.ElevationStep;

    public float WaterSurfaceY =>
        (waterLevel + HexMetrics.WaterElevationOffset) *
        HexMetrics.ElevationStep;

    public int UrbanLevel
    {
        get => urbanLevel;
        set
        {
            if (urbanLevel != value)
            {
                urbanLevel = value;
                RefreshSelfOnly();
            }
        }
    }

    public int FarmLevel
    {
        get => farmLevel;
        set
        {
            if (farmLevel != value)
            {
                farmLevel = value;
                RefreshSelfOnly();
            }
        }
    }

    public int PlantLevel
    {
        get => plantLevel;
        set
        {
            if (plantLevel != value)
            {
                plantLevel = value;
                RefreshSelfOnly();
            }
        }
    }

    public bool Walled
    {
        get => walled;
        set
        {
            if (walled != value)
            {
                walled = value;
                Refresh();
            }
        }
    }

    private Color color;

    private int elevation = int.MinValue;
    private int waterLevel;

    private int urbanLevel, farmLevel, plantLevel;

    private bool walled;

    [SerializeField] private HexCell[] neighbors;

    [SerializeField] private bool[] roads;
    
    [SerializeField] private sbyte[] rivers;

    public HexCell GetNeighbor(HexDirection direction)
    {
        return neighbors[(int) direction];
    }

    public void SetNeighbor(HexDirection direction, HexCell cell)
    {
        neighbors[(int) direction] = cell;
        cell.neighbors[(int) direction.Opposite()] = this;
    }

    public HexEdgeType GetEdgeType(HexDirection direction)
    {
        return HexMetrics.GetEdgeType(
            elevation, neighbors[(int) direction].elevation
        );
    }

    public HexEdgeType GetEdgeType(HexCell otherCell)
    {
        return HexMetrics.GetEdgeType(
            elevation, otherCell.elevation
        );
    }
    
    public void SetHighlight(bool state) 
    {
        highlight.enabled = state;
        highlight.color = Color.white;
    }
    
    public void SetHighlight(bool state, Color highlightColor) 
    {
        SetHighlight(state);
        highlight.color = highlightColor;
    }

    private void UpdateRiverCenter()
    {
        var changedTimes = 0;
        riverCenter = new Vector3();

        if (NumberOfRivers < 1) return;  
        
        for (var direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
        {
            if (rivers[(int) direction] == 0) continue;
            
            var e = new EdgeVertices(
                Position + HexMetrics.GetFirstSolidCorner(direction),
                Position + HexMetrics.GetSecondSolidCorner(direction)
            );
                
            for (var i = -3; i <= 3; i++)
            {
                if (i == 0) i += 1;
                riverCenter += HexMetrics.GetRiverCorner(Position, e.v[0], e.v[4], direction, i);
                changedTimes ++;
            }
        }

        riverCenter /= changedTimes;
    }

    private void UpdateRoadCenters()
    {
        roadCenters.Clear();
        roadCenterHasRoads.Clear();
        var leveledRiverCenter = riverCenter;
        leveledRiverCenter.y = Position.y;
        if (NumberOfRivers > 0)
        {
            var firstRiver = (HexDirection) Array.FindIndex(rivers, river => river != 0);
            var firstAfterRiver = true;
            roadCenterPointers[(int) firstRiver] = -1;
            for (var direction = firstRiver.Next(); direction != firstRiver; direction = direction.Next())
            {
                if (firstAfterRiver)
                {
                    var edge = Position + HexMetrics.GetSolidEdgeMiddle(MedianDirectionBetweenRivers(direction));
                    roadCenters.Add(
                        Vector3.Lerp(
                            leveledRiverCenter + (edge - leveledRiverCenter).normalized * 4.5f, // river width between 4 and 5
                            edge, 
                            0.33f));
                    roadCenterHasRoads.Add(false);
                    firstAfterRiver = false;
                }
                
                if (HasRiverThroughEdge(direction))
                {
                    if (!HasRiverThroughEdge(direction.Next())) firstAfterRiver = true;
                    roadCenterPointers[(int) direction] = -1;
                    continue;
                }

                roadCenterPointers[(int) direction] = (sbyte) (roadCenters.Count - 1);

                if (HasRoadThroughEdge(direction) && roadCenterHasRoads.Count > 0)
                    roadCenterHasRoads[roadCenterHasRoads.Count - 1] = true;
            }
        }
        else
        {
            roadCenters.Add(Position);
            for (var direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
            {
                roadCenterPointers[(int) direction] = 0;
            }
        }
        
        //if (roadCenters.Count == 0) roadCenters.Add(Position);
    }

    public bool HasRiverThroughEdge(HexDirection direction)
    {
        return rivers[(int) direction] != 0;
    }

    public sbyte GetRiverValue(HexDirection direction)
    {
        return rivers[(int) direction];
    }

    private void SetRiver(int index, sbyte state)
    {
        if (rivers[index] == state) return;
        
        rivers[index] = state;
        UpdateRiverCenter();
        UpdateRoadCenters();
        RefreshSelfOnly();

        neighbors[index].rivers[(int) ((HexDirection) index).Opposite()] = (sbyte) -state;
        neighbors[index].UpdateRiverCenter();
        neighbors[index].UpdateRoadCenters();
        neighbors[index].RefreshSelfOnly();
    }
    
    public void AddRiver(HexDirection direction, sbyte value)
    {
        if (rivers[(int) direction] == value || neighbors[(int) direction] == null) return;

        var neighbor = GetNeighbor(direction);
        if (value == 1)
        {
            if (!IsValidRiverDestination(neighbor)) return;
            SetRoad((int) direction, false);
        } 
        else if (value == -1)
        {
            if (!neighbor.IsValidRiverDestination(this)) return;
            SetRoad((int) direction, false);            
        }

        SetRiver((int) direction, value);
    }

    public void AddOutgoingRiver(HexDirection direction)
    {
        //if (HasOutgoingRiver && OutgoingRiver == direction) return;

        if (rivers[(int) direction] == 1) return;

        var neighbor = GetNeighbor(direction);
        if (!IsValidRiverDestination(neighbor)) return;

        SetRiver((int) direction, 1);

        SetRoad((int) direction, false);
    }

    public void RemoveRivers()
    {
        for (var i = 0; i < neighbors.Length; i++)
            if (rivers[i] != 0)
                SetRiver(i, 0);
    }

    public void RemoveRiver(HexDirection direction)
    {
        SetRiver((int) direction, 0);
    }

    public bool HasRoadThroughEdge(HexDirection direction)
    {
        return roads[(int) direction];
    }

    public void AddRoad(HexDirection direction)
    {
        if (
            !roads[(int) direction] && !HasRiverThroughEdge(direction)
        )
            SetRoad((int) direction, true);
    }

    public void RemoveRoad(HexDirection direction)
    {
        SetRoad((int) direction, false);
    }

    public void RemoveRoads()
    {
        for (var i = 0; i < neighbors.Length; i++)
            SetRoad(i, false);
    }

    public int GetElevationDifference(HexDirection direction)
    {
        var difference = elevation - GetNeighbor(direction).elevation;
        return difference >= 0 ? difference : -difference;
    }

    private bool IsValidRiverDestination(HexCell neighbor)
    {
        return neighbor && (
            elevation >= neighbor.elevation || waterLevel == neighbor.elevation
        );
    }

    private void ValidateRivers()
    {
        for (var i = 0; i < neighbors.Length; i++)
        {
            if (rivers[i] == 1 && !IsValidRiverDestination(GetNeighbor((HexDirection) i)) ||
                rivers[i] == -1 && !GetNeighbor((HexDirection) i).IsValidRiverDestination(this))
                SetRiver(i, 0);
        }
    }

    private void SetRoad(int index, bool state)
    {
        if (roads[index] == state) return;
        
        roads[index] = state;
        UpdateRoadCenters();
        RefreshSelfOnly();
        
        neighbors[index].roads[(int) ((HexDirection) index).Opposite()] = state;
        neighbors[index].UpdateRoadCenters();
        neighbors[index].RefreshSelfOnly();
    }

    public int GetDistance(HexCell dest)
    {
        return coordinates.GetDistance(dest.coordinates);
    }

    private void Refresh()
    {
        if (!chunk) return;
        chunk.Refresh();
        foreach (var neighbor in neighbors)
            if (neighbor != null && neighbor.chunk != chunk)
                neighbor.chunk.Refresh();
    }

    private void RefreshSelfOnly()
    {
        chunk.Refresh();
    }
}