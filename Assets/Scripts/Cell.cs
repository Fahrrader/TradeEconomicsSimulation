using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ScriptableObjects;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class Cell
{
    private const float RoadAppearanceThreshold = 0.5f;
    private const float RoadDisappearanceThreshold = 0.45f;
    
    public WorldManager manager;
    
    public Vector2Int position;

    private HexCell hexCell;
    public HexCell HexCell
    {
        get => hexCell;
        set
        {
            hexCell = value;
            hexCell.dataCell = this;
            position = HexCoordinates.RevertCoordinates(hexCell.coordinates);
        }
    }

    private City occupyingCity;

    public City OccupyingCity
    {
        get => occupyingCity;
        set
        {
            occupyingCity = value;
            hexCell.Walled = value;
        }
    }
    
    public readonly List<Traveller> occupants = new List<Traveller>();
    
    // visibleBy, update them if the cell is changed?

    private BiomeData biome;

    public BiomeData Biome
    {
        get => biome;
        set
        {
            biome = value;
            Color = value.color;
            Altitude = altitude;
        }
    }

    public Color Color
    {
        get => hexCell.Color;
        set => hexCell.Color = value;
    }

    public void SetBiomeCarefully(BiomeData value)
    {
        if (value.aquatic && !IsLowerThanNonAquaticNeighbors) return;
        Biome = value;
    }

    private float altitude;
    public float Altitude
    {
        get => altitude;
        set
        {
            altitude = value;
            AdjustElevation();
        }
    }

    private float waterLevel;
    public float WaterLevel
    {
        get => waterLevel;
        set
        {
            waterLevel = value;
            AdjustElevation();
        }
    }

    private void AdjustElevation()
    {
        var waterSurface = Mathf.FloorToInt(altitude / WorldManager.MaxAltitude * HexMetrics.ElevationLevels);
        var elevation = Mathf.FloorToInt((altitude - waterLevel) / WorldManager.MaxAltitude * HexMetrics.ElevationLevels);
        elevation = IsUnderwater ? Mathf.Min(elevation, hexCell.WaterLevel - 1) : elevation;
        hexCell.Elevation = elevation;
        hexCell.WaterLevel = waterSurface;

        foreach (var traveller in occupants)
        {
            traveller.AdjustPositionY();
        }
        
        CalculateMoveCosts();
    }
    
    public float AltitudeWithDepth => altitude - waterLevel;

    public float rainfall;
    public float temperature;

    public bool IsUnderwater => IsWaterlogged && IsLowerThanNonAquaticNeighbors;

    public bool IsWaterlogged => Biome.aquatic || waterLevel >= 100;

    public bool IsLowerThanNonAquaticNeighbors =>
        !neighbors.Any(neighbor =>
            neighbor != null && (neighbor.HexCell.WaterLevel != hexCell.WaterLevel && neighbor.hexCell.Elevation < hexCell.Elevation));
    /*(neighbor.IsWaterlogged && neighbor.HexCell.WaterLevel != hexCell.WaterLevel && neighbor.HexCell.WaterLevel != neighbor.HexCell.Elevation || // should be if the neighbor is indeed underwater
                                 !neighbor.IsWaterlogged && neighbor.HexCell.Elevation < HexCell.WaterLevel));*/

    private float updateTimestamp;
    
    public void Regrow()
    {
        var deltaTime = Time.time - updateTimestamp;
        foreach (var resource in resources.Where(resource => resource.Data.Replenishable))
            // balance = 1 - 1/r {5000 = 7500 * (1-1/3)} r = 1/(1 - balance)
            resource.Regrow(deltaTime / WorldManager.SeasonDuration);

        foreach (HexDirection direction in Enum.GetValues(typeof(HexDirection)))
            SetRoad(direction,
                roads[(int) direction] -
                WorldManager.RoadDeteriorationRate * (deltaTime / WorldManager.SeasonDuration));

        updateTimestamp = Time.time;
    }

    public readonly float[] moveCostTo = new float[6];

    public readonly bool[] isAquaticMovementTo = new bool[6];

    private void CalculateMoveCostTo(HexDirection direction)
    {
        var neighbor = GetNeighbor(direction);
        if (neighbor == null) return;

        var moveCost = ((hexCell.Walled ? 1 : biome.baseMovementCost) +
                        (neighbor.hexCell.Walled ? 1 : neighbor.biome.baseMovementCost)) / 2f;
        
        var altitudeDiff = (neighbor.altitude - altitude) / 500;
        moveCost += altitudeDiff > 0 ? altitudeDiff * altitudeDiff : altitudeDiff * altitudeDiff / 4;

        moveCost += hexCell.NumberOfRivers * 2;
        
        moveCost /= 1 + roads[(int) direction];
        
        if (moveCost < 1) moveCost = 1;

        moveCostTo[(int) direction] = moveCost;//Mathf.RoundToInt(moveCost);

        isAquaticMovementTo[(int) direction] = biome.aquatic || neighbor.biome.aquatic;
    }

    private void CalculateMoveCosts()
    {
        foreach (HexDirection direction in Enum.GetValues(typeof(HexDirection)))
        {
            CalculateMoveCostTo(direction);
        }
    }

    public int AdditionalMoveCostThroughRivers(HexDirection dir1, HexDirection dir2)
    {
        return hexCell.roadCenters[hexCell.roadCenterPointers[(int) dir1]] ==
            hexCell.roadCenters[hexCell.roadCenterPointers[(int) dir2]] && hexCell.roadCenterPointers[(int) dir1] != -1
                ? 0 : 1;
    }
    
    /** hot singular thread garbage that doesn't allow for parallelism */
    public int Distance { get; set; }

    public int SearchHeuristic { get; set; }
    
    public int SearchPriority => Distance + SearchHeuristic;

    public int SearchPhase { get; set; }

    public Cell NextWithSamePriority { get; set; }

    public HexDirection PathFrom { get; set; }
    
    /** hot garbage ends here */

    private readonly Cell[] neighbors = new Cell[6];

    public HexEdgeType GetEdgeType(HexDirection direction) => hexCell.GetEdgeType(direction);
    public HexEdgeType GetEdgeType(Cell cell) => hexCell.GetEdgeType(cell.HexCell);

    public readonly List<Resource> resources = new List<Resource>();

    public readonly HashSet<RecipeData> recipes = new HashSet<RecipeData>();

    public readonly float[] needSatisfactionPotential = new float[6]; 

    public Resource AddResource(ResourceData resourceData, int amount)
    {
        foreach (var resource in resources)
        {
            if (resource.Data != resourceData) continue;
            resource.Amount += amount;
            return resource;
        }

        var res = new Resource(resourceData, amount, WorldManager.CalculateResourceBalanceRatio(resourceData, this) * resourceData.maxAmount); 
        resources.Add(res);
        FindAvailableRecipes();
        return res;
    }

    public void FindAvailableRecipes()
    {
        recipes.Clear();
        foreach (var resource in resources)
        {
            foreach (var recipe in resource.Data.involvingRecipes)
            {
                recipes.Add(recipe);
            }
        }
    }

    private float[] roads = new float[6];
    private sbyte[] rivers = new sbyte[6];

    public float GetRoadValue(HexDirection direction) => roads[(int) direction];
    
    public void SetRoad(HexDirection direction, float value)
    {
        if (roads[(int) direction] == value || GetNeighbor(direction) == null) return;
        
        var roadWasPresent = roads[(int) direction] > RoadAppearanceThreshold;
        value = Mathf.Clamp01(value);
        roads[(int) direction] = value;
        GetNeighbor(direction).roads[(int) direction.Opposite()] = value;
        if (value > RoadAppearanceThreshold || roadWasPresent && value > RoadDisappearanceThreshold)
            hexCell.AddRoad(direction);
        else
            hexCell.RemoveRoad(direction);
        
        CalculateMoveCostTo(direction);
    }

    public void OneTravelledHere(HexDirection direction)
    {
        SetRoad(direction, GetRoadValue(direction) + 0.05f);
    }

    public void SetNeighbor(HexDirection direction, Cell cell)
    {
        neighbors[(int) direction] = cell;
        cell.neighbors[(int) direction.Opposite()] = this;
    }

    public Cell GetNeighbor(HexDirection direction) => neighbors[(int) direction];

    public HexDirection GetNeighborDirection(Cell neighbor)
    {
        return HexCell.coordinates.GetNeighborDirection(neighbor.HexCell.coordinates);
    }

    public bool IsNeighborInBounds(HexDirection direction)
    {
        return manager.PositionInBounds(direction.GetNeighborCoordinatesByDirection(position.x, position.y));
    }
    
    public int GetDistance(Cell dest)
    {
        return hexCell.coordinates.GetDistance(dest.HexCell.coordinates);
    }
}
