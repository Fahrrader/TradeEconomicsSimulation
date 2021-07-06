using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Biome", menuName = "ScriptableObjects/Biome", order = 6)]
public class BiomeData : ScriptableObject
{
    public Biome biome;
    public Color color;
    public bool aquatic;
    
    public int baseMovementCost;
    
    public float frequency = 1f;
    public List<SpawnCondition> spawnConditions = new List<SpawnCondition>{new SpawnCondition(0)};
}

[Serializable]
public struct SpawnCondition
{
    public SpawnFactor factor;
    public int min;
    public int max;
    //public int importance;

    public SpawnCondition(byte _)
    {
        factor = SpawnFactor.Altitude;
        min = int.MinValue;
        max = int.MaxValue;
        //importance = 1;
    }

    public bool SatisfiedBy(Cell cell)
    {
        int attribute;
        switch (factor)
        {
            case SpawnFactor.Altitude:
                attribute = Mathf.FloorToInt(cell.AltitudeWithDepth);
                break;
            case SpawnFactor.Rainfall:
                attribute = Mathf.FloorToInt(cell.rainfall);
                break;
            case SpawnFactor.Temperature:
                attribute = Mathf.FloorToInt(cell.temperature);
                break;
            case SpawnFactor.WaterLevel:
                attribute = Mathf.FloorToInt(cell.WaterLevel);
                break;
            case SpawnFactor.LowerThanNeighbors:
                return cell.IsLowerThanNonAquaticNeighbors;
            default:
                return false;
        }

        return attribute >= min && attribute <= max;
    } 
}

public enum SpawnFactor
{
    Altitude,
    Rainfall,
    Temperature,
    WaterLevel,
    LowerThanNeighbors
}
