using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Need
{
    public float Value { get; private set; }

    public readonly float depletionRate = DepletionBase / WorldManager.SeasonDuration;
    public readonly float happinessWeight = 1;
    
    private const float Min = 0;
    private const float Max = 100;

    public const float DepletionBase = 100;
    public const float DepletionDistributionWidthBase = 6.67f;
    private const float DepletionMin = 33, DepletionMax = 300;

    public const float HappinessDistributionWidth = 0.133f;

    public Need()
    {
        Value = Max;
    }

    public Need(Need need)
    {
        happinessWeight = need.happinessWeight;
        depletionRate = need.depletionRate;
        Value = need.Value;
    }

    public Need(float depletionRate, float happinessFactor)
    {
        this.depletionRate = Mathf.Clamp(depletionRate, DepletionMin, DepletionMax) / WorldManager.SeasonDuration;
        this.happinessWeight = happinessFactor;
        Value = Max;
    }

    /*public Need(float min, float max, float depletionRate)
    {
        this.depletionRate = Mathf.Clamp(depletionRate, DepletionMin, DepletionMax) / WorldManager.SeasonDuration;
        this.min = min;
        this.max = Mathf.Clamp(max, MaxMin, MaxMax);
        
        value = this.max;
    }*/

    public void Deplete(float deltaTime, float passiveMin = Min)
    {
        Set(Mathf.Max(Value - depletionRate * deltaTime, passiveMin));
    }

    public void Add(float newValue/*, float passiveMin = Min*/)
    {
        Set(Value + newValue);//Mathf.Min(Value + newValue, passiveMin));
    }

    public void Set(float newValue)
    {
        Value = Mathf.Clamp(newValue, Min, Max);
    }
}

public enum NeedType
{
    Hunger,
    Thirst,
    Warmth,
    
    Greed,
    Social,
    Comfort
}
