using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using WorldGen;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class WorldManager : MonoBehaviour
{
    public int seed;
    
    public int chunkCountX = 4, chunkCountZ = 3;

    public NoiseConfiguration terrainNoiseConfiguration = new NoiseConfiguration(10, 5, 0.5f, 2);
    public NoiseConfiguration rainNoiseConfiguration = new NoiseConfiguration(10, 5, 0.5f, 2);
    public NoiseConfiguration resourceNoiseConfiguration = new NoiseConfiguration(10, 5, 0.5f, 2);

    [SerializeField]
    private float resourceSpawnThreshold = 0.1f;
    [SerializeField]
    private float criticalSpawnAllowance = 0.8f;

    [SerializeField] 
    private int lakeSizeThreshold = 10;
    [SerializeField]
    private int discourageRiverProximityRadius = 5;
    private int DiscourageRiverProximityRadiusSqr => discourageRiverProximityRadius * discourageRiverProximityRadius;

    public const float SeasonDuration = 60; // in seconds
    public static float timeElapsedSinceBeginning;
    public static int season = -1;
    public static bool newSeason;

    public const float RoadDeteriorationRate = 0.1f; // per season

    public const float MaxAltitude = 10000f;

    public const float MinAvgRainfall = 50f, MinRainfall = 20f;
    public const float MaxAvgRainfall = 15000f, MaxRainfall = 17000f;
    public float avgRainfall; // in % or 50 - 15000 with avg 1000 mm
    
    private const float MaxRainfallDeviation = MaxRainfall - MaxAvgRainfall;

    private float RainfallDeviation =>
        Mathf.Clamp(
            Mathf.Min(avgRainfall - MinRainfall, MaxRainfall - avgRainfall) * 0.75f,
            0f, MaxRainfallDeviation);

    [SerializeField]
    private bool forestFeaturesEnabled = true; 

    [Tooltip("At sea level")]
    public float avgTemperature; // 0 to 50 avg 25 oC 
    public const float MinTemperature = -100, MaxTemperature = 100; 
    
    [Range(0, 1)]
    public float seaLevel;

    [NonSerialized] 
    public Cell[,] cells;

    public int WorldSizeX => chunkCountX * HexMetrics.ChunkSizeX;
    public int WorldSizeZ => chunkCountZ * HexMetrics.ChunkSizeZ;
    
    public HexGrid hexGrid;

    [SerializeField]
    private Traveller travellerPrefab;
    [SerializeField]
    private City cityPrefab;

    [SerializeField]
    private ResourceData forestObjectData;

    [SerializeField]
    private ResourceData saltWaterObjectData; // transfer to a static class?

    public ResourceData ruinsObjectData;

    //private readonly Queue<ChunkThreadInfo<CellData[]>> chunkThreadInfoQueue = new Queue<ChunkThreadInfo<CellData[]>>();
    //private Queue<ChunkThreadInfo> meshDataThreadInfoQueue = new Queue<ChunkThreadInfo>();

    private void Awake()
    {
        var sw = new Stopwatch();
        
        sw.Start();
        ResourceHolder.Populate(FindObjectOfType<SceneResourceHolder>());
        sw.Stop();
        Debug.Log("scriptables: " + sw.ElapsedMilliseconds);
        
        sw.Restart();
        Random.InitState(seed);
        Rand.Initialize(seed);
        hexGrid.Initialize(chunkCountX, chunkCountZ, seed, this);
        cells = new Cell[WorldSizeX, WorldSizeZ];
        sw.Stop();
        Debug.Log("hex: " + sw.ElapsedMilliseconds);
        
        sw.Restart();
        GenerateTerrain(WorldSizeX, WorldSizeZ, new Vector2());
        sw.Stop();
        Debug.Log("terrain: " + sw.ElapsedMilliseconds);

        sw.Restart();
        GenerateRivers();
        GenerateRivers();
        sw.Stop();
        Debug.Log("rivers: " + sw.ElapsedMilliseconds);

        sw.Restart();
        GenerateBiomes();
        sw.Stop();
        Debug.Log("biomes: " + sw.ElapsedMilliseconds);

        sw.Restart();
        GenerateLakes();
        sw.Stop();
        Debug.Log("lakes: " + sw.ElapsedMilliseconds);

        sw.Restart();
        GenerateResources();
        sw.Stop();
        Debug.Log("resources: " + sw.ElapsedMilliseconds);
    }
    
    private void Update()
    {
        timeElapsedSinceBeginning += Time.deltaTime;
        var currentSeason = Mathf.FloorToInt(timeElapsedSinceBeginning / SeasonDuration);
        if (currentSeason != season)
        {
            season = currentSeason;
            newSeason = true;
        }
        else newSeason = false;
    }

    private void GenerateTerrain(int width, int length, Vector2 center)
    {
        var altitudeMap = Rand.GenerateNoiseMap(width, length, seed, terrainNoiseConfiguration.noiseScale,
            terrainNoiseConfiguration.octaves, terrainNoiseConfiguration.persistance,
            terrainNoiseConfiguration.lacunarity, center, true);

        var rainSeed = Random.Range(int.MinValue, int.MaxValue);
        var rainfallMap = Rand.GenerateNoiseMap(width, length, rainSeed, rainNoiseConfiguration.noiseScale,
            rainNoiseConfiguration.octaves, rainNoiseConfiguration.persistance, 
            rainNoiseConfiguration.lacunarity, center, true);

        var temperatureMap = new float[width, length];
            
        for (var z = 0; z < length; z++) 
        {
            for (var x = 0; x < width; x++) 
            {
                var height = altitudeMap[x, z];

                var biome = height <= seaLevel ? Biome.Sea : Biome.Grassland;

                var altitude = (height - seaLevel) * MaxAltitude;
                var waterLevel = altitude >= 0 ? 0 : -altitude;
                altitude = altitude > 0 ? altitude : 0;
                
                var rainfall = avgRainfall + (rainfallMap[x, z] - 0.5f) * 2f * RainfallDeviation;
                waterLevel += rainfall / 100;

                cells[x, z] = new Cell
                {
                    HexCell = hexGrid.GetCell(x, z),
                    manager = this,
                    Biome = ResourceHolder.biomes[(int) biome],
                    WaterLevel = waterLevel,
                    Altitude = altitude,
                    temperature = temperatureMap[x, z] =
                        avgTemperature - (altitude + waterLevel * 0.1f) * 0.0065f,
                    rainfall = rainfallMap[x, z] = rainfall
                };
                
                if (x > 0) cells[x, z].SetNeighbor(HexDirection.W, cells[x - 1, z]);
                if (z > 0)
                {
                    if ((z & 1) == 0)
                    {
                        cells[x, z].SetNeighbor(HexDirection.SE, cells[x, z - 1]);
                        if (x > 0) cells[x, z].SetNeighbor(HexDirection.SW, cells[x - 1, z - 1]);
                    }
                    else
                    {
                        cells[x, z].SetNeighbor(HexDirection.SW, cells[x, z - 1]);
                        if (x < WorldSizeX - 1) cells[x, z].SetNeighbor(HexDirection.SE, cells[x + 1, z - 1]);
                    }
                }
            }
        }
    }

    private void GenerateRivers()
    {
        /*if (cell.IsUnderwater) continue;
        if (cell.HexCell.HasOutgoingRiver) continue; //cell.hasRiver
        if (cell.rainfall < avgRainfall + RainfallDeviation * 0.5f) continue;*/
        //if (Random.value < 0.5f) continue; too much rainfall
        // encourage river generation if low rainfall, discourage if high
        // encourage higher altitude
        // chance = higher rainfall * non-presence of nearby rivers * (0.25f + 0.75f * altitude / ((1 - sea level) * max altitude))

        var sortedCells = new Cell[WorldSizeX * WorldSizeZ];
        for (int i = 0, z = 0; z < WorldSizeZ; z++)
        for (var x = 0; x < WorldSizeX; x++)
        {
            sortedCells[i] = cells[x, z];
            i++;
        }

        sortedCells = sortedCells.Where(cell =>
            cell.WaterLevel >= 3 && cell.WaterLevel >= (avgRainfall + RainfallDeviation * 0.5f) / 100 
                                 && !cell.IsWaterlogged && !cell.HexCell.HasRiver// cell.hasRiver
        ).OrderByDescending(cell => cell.rainfall + cell.Altitude).ToArray();
        
        var rivers = 0;
        foreach (var cell in sortedCells)
        {
            //if (cell.rainfall < 500 || cell.rainfall < avgRainfall + RainfallDeviation * 0.5f) return;
            //if (cell.IsUnderwater || cell.HexCell.HasOutgoingRiver) continue; //cell.hasRiver

            var chance = 2f;// - 2f * sortedCells.Length / (WorldSizeX * WorldSizeZ);
            chance *= 0.15f + 1.5f * cell.Altitude / ((1 - seaLevel) * MaxAltitude);

            var startX = cell.position.x - discourageRiverProximityRadius;
            startX = startX >= 0 ? startX : 0;  
            var stopX = cell.position.x + discourageRiverProximityRadius;
            stopX = stopX < WorldSizeX ? stopX : WorldSizeX - 1;

            var startZ = cell.position.y - discourageRiverProximityRadius;
            startZ = startZ >= 0 ? startZ : 0;
            var stopZ = cell.position.y + discourageRiverProximityRadius;
            stopZ = stopZ < WorldSizeZ ? stopZ : WorldSizeZ - 1;
            
            for (var x = startX; x < stopX; x++)
            {
                for (var z = startZ; z < stopZ; z++)
                {
                    if (!cells[x, z].HexCell.HasOutgoingRiver()) continue;
                    var distanceSqr = (cell.position.x - x) * (cell.position.x - x) + 
                                   (cell.position.y - z) * (cell.position.y - z);
                    if (distanceSqr > DiscourageRiverProximityRadiusSqr) continue;
                    chance -= 0.5f / distanceSqr; // play around
                }
            }
            
            if (chance > Random.value)
            {
                TrackRiver(cell, (HexDirection) 7, cell.WaterLevel * 0.5f);
                rivers++;
            }
        }
        Debug.Log(rivers + " rivers");
    }

    private float CalculateOutgoingWaterVolume(float waterVolume, float altitudeDiff)
    {
        var altitudeMultiplier = 0.5f + 0.5f * (altitudeDiff > 2000 ? 1 : altitudeDiff / 2000);
        altitudeMultiplier = altitudeMultiplier > 0 ? altitudeMultiplier : 0;
        return waterVolume * altitudeMultiplier;
    }

    private void TrackRiver(Cell cell, HexDirection direction, float incomingWaterVolume)
    {
        cell.WaterLevel += incomingWaterVolume;// * (Random.Range(0, 1) * 2 - 1);
        
        // propagate to neighbors
        for (var dir = HexDirection.NE; dir <= HexDirection.NW; dir++)
        {
            if (!cell.IsNeighborInBounds(dir) || dir == direction.Opposite()) continue;
            var altitudeDiff = cell.Altitude - cell.GetNeighbor(dir).Altitude;
            cell.GetNeighbor(dir).WaterLevel += CalculateOutgoingWaterVolume(incomingWaterVolume, altitudeDiff) / 6;
        }

        if (cell.HexCell.HasOutgoingRiver())
        {
            foreach (var dir in cell.HexCell.OutgoingRivers)
            {
                var altitudeDiff = cell.Altitude - cell.GetNeighbor(dir).Altitude;
                TrackRiver(cell.GetNeighbor(dir), dir, CalculateOutgoingWaterVolume(incomingWaterVolume, altitudeDiff));
            }
            return;
        } 

        var candidates = new float[6];
        var lowerAltitudePool = 0f;
        
        for (var dir = HexDirection.NE; dir <= HexDirection.NW; dir++)
        {
            if (!cell.IsNeighborInBounds(dir)) continue;
            var neighbor = cell.GetNeighbor(dir);
                        
            if (neighbor.Altitude <= cell.Altitude && !cell.HexCell.HasIncomingRiver(dir))
            {
                candidates[(int) dir] = cell.Altitude - neighbor.Altitude;
                if (dir == direction) candidates[(int) dir] *= 3;
                if (neighbor.HexCell.HasRiver) candidates[(int) dir] *= 6;
                lowerAltitudePool += candidates[(int) dir];
            }
            else candidates[(int) dir] = -1;
        }

        if (lowerAltitudePool > 0f)
        {
            var prevSum = 0f;
            lowerAltitudePool *= Random.value;
            for (var dir = HexDirection.NE; dir <= HexDirection.NW; dir++)
            {
                if (candidates[(int) dir] + prevSum >= lowerAltitudePool)
                {
                    cell.HexCell.AddOutgoingRiver(dir); // and then track to lake or sea
                    var altitudeDiff = cell.Altitude - cell.GetNeighbor(dir).Altitude;
                    TrackRiver(cell.GetNeighbor(dir), dir, CalculateOutgoingWaterVolume(cell.WaterLevel, altitudeDiff));
                    break;
                }

                prevSum += candidates[(int) dir] > 0 ? candidates[(int) dir] : 0;
            }
        }
        else
        {
            cell.WaterLevel += incomingWaterVolume * 4; 
            /*if (cell.WaterLevel >= ResourceHolder.biomes[(int) Biome.Lake].spawnConditions[0].min) 
                cell.Biome = ResourceHolder.biomes[(int) Biome.Lake];*/ // should automatically take care in BiomeGen
        }
    }

    private void GenerateBiomes()
    {
        var center = new Vector2();
        var biomeSeed = Random.Range(int.MinValue, int.MaxValue);
        var noiseMap = Rand.GenerateNoiseMap(WorldSizeX, WorldSizeZ, biomeSeed,
            terrainNoiseConfiguration.noiseScale, terrainNoiseConfiguration.octaves,
            terrainNoiseConfiguration.persistance, terrainNoiseConfiguration.lacunarity, center, true);
        var appropriateBiomeList = new float[ResourceHolder.biomes.Count];

        for (var z = 0; z < cells.GetLength(1); z++)
        {
            for (var x = 0; x < cells.GetLength(0); x++)
            {
                var cell = cells[x, z];
                var totalImportance = 0f;
                foreach (var biome in ResourceHolder.biomes)
                {
                    appropriateBiomeList[(int) biome.biome] = biome.frequency;

                    if (/*!biome.aquatic && cell.WaterLevel >= 100 ||*/ // todo uncomment if don't want to have rainforests underwater
                        biome.spawnConditions.Any(condition => !condition.SatisfiedBy(cell)))
                    {
                        appropriateBiomeList[(int) biome.biome] = -1;
                        continue;
                    }
                    
                    totalImportance += appropriateBiomeList[(int) biome.biome];
                }

                var prevSum = 0f;
                for (var i = 0; i < appropriateBiomeList.Length; i++)
                {
                    if ((appropriateBiomeList[i] + prevSum) / totalImportance >= noiseMap[x, z])
                    {
                        cell.Biome = ResourceHolder.biomes[i];
                        if (ResourceHolder.biomes[i].aquatic) // cell.IsUnderwater
                        {
                            foreach (HexDirection dir in Enum.GetValues(typeof(HexDirection)))
                            {
                                if (!cell.IsNeighborInBounds(dir)) continue;
                                cell.GetNeighbor(dir).WaterLevel += Mathf.Min(10, cell.WaterLevel / 6);
                            }
                        }
                        break;
                    }
                    
                    prevSum += appropriateBiomeList[i] > 0 ? appropriateBiomeList[i] : 0;
                }
            }
        }
    }

    private void GenerateLakes()
    {
        var visited = new bool[WorldSizeX, WorldSizeZ];
        
        for (var z = 0; z < cells.GetLength(1); z++)
        {
            for (var x = 0; x < cells.GetLength(0); x++)
            {
                if (visited[x, z]) continue;
                
                var cell = cells[x, z];
                
                if (!cell.IsUnderwater)
                {
                    visited[x, z] = true;
                    continue;
                }

                if (GetWaterBodySize(visited, x, z, 0) <= lakeSizeThreshold)
                    SetWaterBodyToLake(cell);
            }
        }
    }

    private int GetWaterBodySize(bool[,] visited, int x, int z, int currentSize)
    {
        if (visited[x, z]) return currentSize;
        visited[x, z] = true;

        var newSize = currentSize + 1;

        for (var d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            if (!cells[x, z].IsNeighborInBounds(d) || !cells[x, z].GetNeighbor(d).IsUnderwater) continue;

            newSize = GetWaterBodySize(visited, 
                cells[x, z].GetNeighbor(d).position.x, cells[x, z].GetNeighbor(d).position.y, 
                newSize);
        }

        return newSize;
    }

    private void SetWaterBodyToLake(Cell cell)
    {
        if (cell.Biome.biome == Biome.Lake) return;
        cell.Biome = ResourceHolder.biomes[(int) Biome.Lake];
        
        for (var d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            if (!cell.IsNeighborInBounds(d) || !cell.GetNeighbor(d).IsUnderwater) continue;
            SetWaterBodyToLake(cell.GetNeighbor(d));
        }
    }

    private void GenerateResources()
    {
        int width = WorldSizeX, length = WorldSizeZ;
        var center = new Vector2();

        foreach (var resource in ResourceHolder.resources)
        {
            var spawnAnyway = resource == saltWaterObjectData;
            
            var resSeed = Random.Range(int.MinValue, int.MaxValue);
            var noiseMap = Rand.GenerateNoiseMap(width, length, resSeed, resourceNoiseConfiguration.noiseScale,
                resourceNoiseConfiguration.octaves, resourceNoiseConfiguration.persistance, 
                resourceNoiseConfiguration.lacunarity, center, true);
            
            for (var z = 0; z < cells.GetLength(1); z++)
            for (var x = 0; x < cells.GetLength(0); x++)
            {
                var cell = cells[x, z];

                var clampedRatio = CalculateResourceBalanceRatio(resource, cell, spawnAnyway ? 1f : noiseMap[x, z]);
                if (clampedRatio == 0) continue;
                /*if (!resource.canBeUnderwater && cell.IsUnderwater)
                    continue;

                var present = resource.BiomePresence.TryGetValue(cell.Biome.biome, out var biomePresence);
                if (!present && resource.isBiomeExclusive) continue;
                if (!present && biomePresence == 0f) biomePresence = 1f; // if absent in conditions, but resource not exclusive to biomes, then may spawn

                var rainMul = resource.waterLevelSensitivityCurve.Evaluate(cell.WaterLevel);
                var tempMul = resource.temperatureSensitivityCurve.Evaluate(cell.temperature);
                var ratio = (spawnAnyway ? 1f : noiseMap[x, z]) * 
                            resource.frequency * biomePresence * rainMul * tempMul;
                var clampedRatio = Mathf.Clamp01(ratio);*/
                
                var present = resource.BiomePresence.TryGetValue(cell.Biome.biome, out var biomePresence);
                if (!present && biomePresence == 0f) biomePresence = 1f; 
                
                if (resource.minimumAmountToSpawn < 0)
                {
                    var spawnThresholdAdjusted = Mathf.Max(0.01f,
                        Mathf.Min(resource.frequency * biomePresence * criticalSpawnAllowance, resourceSpawnThreshold));
                    if (clampedRatio < spawnThresholdAdjusted) continue;
                } 
                else// if (resource.minimumAmountToSpawn >= 0)
                {
                    if (clampedRatio * resource.maxAmount < resource.minimumAmountToSpawn) continue;
                }
                
                var amount = (int)(clampedRatio * resource.maxAmount);
                cell.resources.Add(new Resource(resource, amount));

                if (forestFeaturesEnabled && resource == forestObjectData)
                {
                    cell.HexCell.PlantLevel = Mathf.CeilToInt(clampedRatio * HexMetrics.ForestLevels);
                }
            }
        }
    }

    public static float CalculateResourceBalanceRatio(ResourceData resource, Cell cell, float noiseValue = 1f)
    {
        if (!resource.canBeUnderwater && cell.IsUnderwater) return 0;
        
        var present = resource.BiomePresence.TryGetValue(cell.Biome.biome, out var biomePresence);
        if (!present && resource.isBiomeExclusive) return 0;
        if (!present && biomePresence == 0f) biomePresence = 1f; // if absent in conditions, but resource not exclusive to biomes, then may spawn
        
        var rainMul = resource.waterLevelSensitivityCurve.Evaluate(cell.WaterLevel);
        var tempMul = resource.temperatureSensitivityCurve.Evaluate(cell.temperature);
        var ratio = noiseValue * resource.frequency * biomePresence * rainMul * tempMul;
        var clampedRatio = Mathf.Clamp01(ratio);
        return clampedRatio;
    }

    public bool PositionInBounds(Vector2Int position)
    {
        return position.x >= 0 && position.x < WorldSizeX && position.y >= 0 && position.y < WorldSizeZ;
    }
    
    public bool PositionInBounds(int x, int z)
    {
        return x >= 0 && x < WorldSizeX && z >= 0 && z < WorldSizeZ;
    }

    public static int spawnedAgents = 0;
    public void SpawnCity(Cell cell, Agent progenitor = null)
    {
        spawnedAgents++;
        if (cell.OccupyingCity)
        {
            Debug.LogWarning("Tried to spawn a city in an already existing city!");
            return;
        }
        var city = Instantiate(cityPrefab);
        city.founder = progenitor;
        city.OccupyCell(cell);
    }
    
    public void SpawnTraveller(Cell cell, Agent progenitor = null)
    {
        spawnedAgents++;
        Debug.Log(spawnedAgents);
        var traveller = Instantiate(travellerPrefab);
        //var state = Random.state;
        traveller.PlaceOnCell(cell, (HexDirection) Random.Range(0, 6));
        //Random.state = state;
        traveller.founder = progenitor;
    }
}