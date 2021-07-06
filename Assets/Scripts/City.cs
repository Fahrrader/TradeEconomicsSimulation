using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AI;
using ScriptableObjects;
using UnityEngine;
using Action = AI.Action;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class City : Agent
{
    public readonly List<Cell> occupiedCells = new List<Cell>();

    public readonly HashSet<Cell> zoneOfInfluence = new HashSet<Cell>();

    private List<Cell> zoneOfInfluenceList = new List<Cell>();

    public bool isDead; // for external reference holders

    public const float InfrastructureSpacePerCell = 5000000;

    private static readonly float[] PopulationThresholds = {50, 250, 1250};
    
    // infrastructure
    // population skills
    // skills

    private void Awake()
    {
        if (!manager) manager = FindObjectOfType<WorldManager>();
        
        state = new AgentState(this, 50) {isNotSimulated = true};
        //ai = new Greedy {agent = this};
        ai.agent = this;

        creationTime = WorldManager.timeElapsedSinceBeginning;
        InitializeNeeds(); // was in start
    }

    private void Start()
    {
    }

    private int seasonSinceBirth = -1;
    private float seasonCounter;

    private void Update()
    {
        OnUpdate();

        state.GrowPopulation(Time.deltaTime);
        seasonCounter += Time.deltaTime;
        var currentSeason = Mathf.FloorToInt(seasonCounter / WorldManager.SeasonDuration);
        if (currentSeason != seasonSinceBirth)
        {
            seasonSinceBirth = currentSeason;
            //state.GrowPopulation(WorldManager.SeasonDuration);
            // if population outmatches available housing, and is generally bigger than housing available to build, expand -- for AI
            AdjustUrbanLevel();
            // make plans, assign workers or something
        }
        
        if (waresChanged)
        {
            state.FindAllPossibleRecipes(zoneOfInfluenceList);
            var s = "";
            foreach (var recipe in state.possibleRecipes) 
                s += recipe.data.name + ": " + recipe.amount + "; ";
            Debug.Log(s);

            var sw = new Stopwatch();
            sw.Start();
            state.priceFinder.FindPrices();
            sw.Stop();
            Debug.Log("time to find prices: " + sw.Elapsed);
            //state.priceFinder.Show();
            
            waresChanged = false;
        }
        
        if (plan.Count == 0 && !executingAction) MakePlan(); // state.populationFree > 0
    }

    public void AdjustUrbanLevel()
    {
        foreach (var cell in occupiedCells)
        {
            cell.HexCell.UrbanLevel = Math.Max(PopulationToUrbanLevel(state.Population / occupiedCells.Count), cell.HexCell.UrbanLevel);
        }
        // place farms in zone of influence?
    }
    
    private static int PopulationToUrbanLevel(float pop)
    {
        for (var i = PopulationThresholds.Length - 1; i >= 0; i--)
        {
            if (pop < PopulationThresholds[i]) continue;
            return i + 1;
        }
        return 0;
    }

    private void Expand()
    {
        // some heuristic function to find a suitable cell
        //OccupyCell();
    }
    
    protected override float ExecuteAction(ActionArguments action)
    {
        if (action.action != Action.Trade) return base.ExecuteAction(action);
        
        if (state.manufacturablesCount.Count == 0) return -1;
        var traders = new List<Traveller>();
        foreach (var tradeCell in occupiedCells)
        {
            traders.AddRange(tradeCell.occupants.Where(traveller => traveller.state.manufacturablesCount.Count > 0));
        }
                    
        if (traders.Count == 0) return -1;

        var trader = traders[Random.Range(0, traders.Count)];
        var thing = AIBase.ChooseThing<WareData>(trader.state, _ => true);
        var value = state.priceFinder.CalculateImmediateValue(thing.Data, thing.Amount);
                    
        // find best match, knapsack problem
                    
        var ownThing = AIBase.ChooseThing<WareData>(state, _ => true);
        if (ownThing == null) return -1;

        trader.state.Collect(ownThing.Data, ownThing.Amount, ownThing.State);
        state.Relinquish(ownThing);
        
        state.Collect(thing.Data, thing.Amount, thing.State);
        trader.state.Relinquish(thing);
        
        return 0;
    }

    public void OccupyCell(Cell cell, bool checkForNeighborCities = true)
    {
        //if (cell.OccupyingCity) return;
        // hashset?
        occupiedCells.Add(cell);
        cell.OccupyingCity = this;
        state.infrastructureSpaceLeft += InfrastructureSpacePerCell;
        zoneOfInfluence.Add(cell);
        
        var hasNeighborCities = false;
        foreach (HexDirection direction in Enum.GetValues(typeof(HexDirection)))
        {
            var neighbor = cell.GetNeighbor(direction);
            if (neighbor == null) continue;
            zoneOfInfluence.Add(neighbor);
            if (neighbor.OccupyingCity != this) hasNeighborCities = true;
        }

        zoneOfInfluenceList = zoneOfInfluence.ToList();
        if (checkForNeighborCities && hasNeighborCities) MergeNeighbors(cell);
    }

    private void MergeNeighbors(Cell cell)
    {
        var neighboringCities = new List<City>();
        foreach (HexDirection direction in Enum.GetValues(typeof(HexDirection)))
        {
            var neighborCity = cell.GetNeighbor(direction)?.OccupyingCity;
            if (neighborCity && neighborCity != this)
            {
                neighboringCities.Add(neighborCity);
            }
        }

        foreach (var city in neighboringCities)
        {
            Merge(city);
        }
    }

    public void Merge(City city)
    {
        foreach (var cell in city.occupiedCells)
        {
            OccupyCell(cell, false);
        }

        if (moniker == "" || city.state.Population > state.Population) moniker = city.moniker;
        // creation time?

        state.Merge(city.state);
        
        // also history and workers
        
        Debug.Log("City '" + city.moniker + "' (" + city.name + ") merged with city '" + moniker + "' (" + name + ")");
        city.isDead = true;
        Destroy(city.gameObject);
    }

    public override void MakeFinalPreparations()
    {
        // leave some ruins
        foreach (var cell in occupiedCells)
        {
            cell.OccupyingCity = null;
            
            cell.AddResource(manager.ruinsObjectData, cell.HexCell.UrbanLevel);
            // todo try to convert wares back into resources and place on cell
        }
        isDead = true;
        Debug.Log("City '" + moniker + "' (" + name + ") has perished.");
        Destroy(gameObject);
    }
}
