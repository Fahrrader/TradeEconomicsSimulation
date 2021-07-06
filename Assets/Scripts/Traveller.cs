using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using AI;
using ScriptableObjects;
using UI;
using UnityEngine;
using Action = AI.Action;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class Traveller : Agent
{
    // skills
    //public Pathfinder pathfinder = GetComponent<Pathfinder>();
    //public AITraveller ai = GetComponent<AITraveller>();
    
    public Cell occupiedCell;

    private Cell currentDestination;

    public Cell CurrentDestination
    {
        get => currentDestination;
        set
        {
            currentDestination = value;
            StopCoroutine(nameof(Move));
            FindPath();
            if (pathIndex < path.Count) DoMove();
        }
    }

    public int pathIndex;

    public float timeToTravel;

    public readonly HashSet<City> knownCities = new HashSet<City>(); // todo

    private int[][] cellVisibility;
    
    public List<HexDirection> path = new List<HexDirection>();

    public bool isDead; // for external reference holders

    private void Awake()
    {
        if (!manager) manager = FindObjectOfType<WorldManager>();

        state = new AgentState(this, 1) {isNotSimulated = true};
        //state.populationFree = state.PopulationInt;
        ai.agent = this;

        creationTime = WorldManager.timeElapsedSinceBeginning;

        cellVisibility = new int[SightDiameter + 1][];
        for (var i = 0; i <= SightDiameter; i++)
            cellVisibility[i] = new int[SightDiameter + 1];
    }

    private void Start()
    {
        InitializeNeeds();
        
        var sw = new Stopwatch();
        sw.Start();
        //state.Produce(new Recipe(ResourceHolder.recipes[ResourceHolder.recipes.Count - 1]));
        sw.Stop();
        Debug.Log(sw.Elapsed);
        state.Collect(ResourceHolder.wares.Find(ware => ware.label == "Waste"), 10);
        
        sw.Restart();
        foreach (var product in ResourceHolder.recipes[ResourceHolder.recipes.Count - 1].result)
        {
            //state.Collect((ManufacturableData) product.product);
        }
        sw.Stop();
        Debug.Log(sw.Elapsed);
        
        sw.Restart();
        for (var i = 0; i < ResourceHolder.recipes[ResourceHolder.recipes.Count - 1].result.Length; i++)
        {
            //state.Collect((ManufacturableData) ResourceHolder.recipes[ResourceHolder.recipes.Count - 1].result[i].product);
        }
        sw.Stop();
        Debug.Log(sw.Elapsed);

        var recipe1 = new Recipe(ResourceHolder.recipes[ResourceHolder.recipes.Count - 2], 1, 2);
        //recipe.tools.Add(new Recipe.ManufacturablePortion());
        sw.Restart();
        //for (var i = 0; i < 10; i++)
            //state.FollowRecipe(recipe1);
        sw.Stop();
        Debug.Log(sw.Elapsed);
        //StartCoroutine(nameof(Cora));
        sw.Restart();
        sw.Stop();
    }

    private IEnumerator Cora()
    {
        yield return new WaitForSeconds(15);
        var res = manager.hexGrid.GetCell(transform.position).dataCell.AddResource(ResourceHolder.resources[0], 5);
        var resData = ResourceHolder.recipes[ResourceHolder.recipes.Count - 2];
        var recipe2 = new Recipe(ResourceHolder.recipes[ResourceHolder.recipes.Count - 1], 4, 50);
        recipe2.resources.Add(new Recipe.ResourceCell(null, res));
        recipe2.ingredients.Add(new Recipe.ManufacturablePortion(
            state.manufacturables[(ManufacturableData) resData.result[3].product][0], 
            state.manufacturables[(ManufacturableData) resData.result[3].product][0].State));
        var success = state.FollowRecipe(recipe2);
        Debug.Log(success);
    }

    private void Update()
    {
        var newCell = manager.hexGrid.GetCell(transform.position).dataCell;
        if (occupiedCell != newCell)
        {
            UpdateOccupiedCell();
            Observe();
        }
        // only update pathfinding if different/new cells are observed
        
        OnUpdate();
        
        if (waresChanged)
        {
            state.FindAllPossibleRecipes(new List<Cell> {occupiedCell});
            var s = "";
            foreach (var recipe in state.possibleRecipes)
                s += recipe.data.name + "; ";
            
            waresChanged = false;
        }
        
        if (plan.Count == 0 && !executingAction) MakePlan(); // todo potentially dangerous
        // StopCoroutine(nameof(FollowPlan)); if dangerous or unpredicted situation
    }

    protected override float ExecuteAction(ActionArguments action)
    {
        if (action.action != Action.Travel) return base.ExecuteAction(action);
        
        CurrentDestination = action.cell;
        if (timeToTravel != action.time) Debug.LogWarning("Different times of TRAVEL from plan to action: " + action.time + " " + timeToTravel);
        return timeToTravel;
    }

    /*public override void Collect(ManufacturableData wareToCollect, float amount = 1, float state = 100)
    {
        if (wareToCollect is InfrastructureData) return;
        base.Collect(wareToCollect, amount, state);
    }*/

    private void DoMove()
    {
        StopCoroutine(nameof(Move));
        StartCoroutine(nameof(Move));
    }

    public float slowness; // todo delete

    private IEnumerator Move()
    {
        yield return new WaitForSeconds(occupiedCell.moveCostTo[(int) path[pathIndex]] + slowness); // todo

        occupiedCell.OneTravelledHere(path[pathIndex]);
        
        PlaceOnCell(occupiedCell.GetNeighbor(path[pathIndex]), path[pathIndex].Opposite());
        pathIndex++;
    }

    public void PlaceOnCell(Cell cell, HexDirection enterDirection)
    {
        var pointer = cell.HexCell.roadCenterPointers[(int) enterDirection];
        if (pointer == -1)
        {
            var direction = enterDirection;
            var oscillator = 1;
            var step = 1;
            do
            {
                direction = direction.Move(step * oscillator);
                pointer = cell.HexCell.roadCenterPointers[(int) direction];
                
                step++;
                oscillator *= -1;
            } while (step != 5 && pointer == -1);
        }

        var position = (pointer != -1 
                           ? cell.HexCell.roadCenters[pointer] 
                           : cell.HexCell.Position) 
                       + Vector3.up; // capsule height divided by 2 is 1
        if (cell.IsUnderwater) position.y = cell.HexCell.WaterSurfaceY;

        if (cell.occupants.Count > 0)
        {
            var angle = 2 * Mathf.PI * (cell.occupants.Count - 1) / 6;
            position = new Vector3(position.x + Mathf.Cos(angle) * 1.5f, position.y, position.z + Mathf.Sin(angle) * 1.5f);
        }

        position = HexMetrics.Perturb(position);
        transform.position = position;
        
        //UpdateOccupiedCell();
    }

    public void AdjustPositionY()
    {
        var position = transform.position;
        position.y = occupiedCell.HexCell.Position.y + 1; // capsule height divided by 2 is 1
        if (occupiedCell.IsUnderwater) position.y = occupiedCell.HexCell.WaterSurfaceY;
        transform.position = position;
    }

    private void UpdateOccupiedCell()
    {
        LeaveCell();
        occupiedCell = manager.hexGrid.GetCell(transform.position).dataCell;
        occupiedCell.occupants.Add(this);
        
        Minimap.RefreshMinimap();

        if (pathIndex < path.Count)
        {
            DoMove();
        }
    }

    private void LeaveCell()
    {
        //if (occupiedCell != null) PaintItBlack(false);
        occupiedCell?.occupants.Remove(this);
    }

    private float FindPath() => FindPath(occupiedCell, currentDestination);

    public float FindPath(Cell origin, Cell destination)
    {
        pathIndex = 0;
        path.Clear();

        timeToTravel = float.MaxValue;
        if (state.CantMove) return timeToTravel;

        timeToTravel = 0f;

        Pathfinding.Pathfinding.FindPath(origin, destination, state.speedMultipliers);

        for (var cell = destination; cell != origin; cell = cell.GetNeighbor(cell.PathFrom))
        {
            path.Add(cell.PathFrom.Opposite());//GetNeighborDirection(cell.PathFrom).Opposite());
            timeToTravel += cell.moveCostTo[(int) cell.PathFrom.Opposite()];
        }

        path.Reverse();
        return timeToTravel;
    }

    protected override void Observe()
    {
        //cellVisibility[SightRadius][SightRadius] = SightRadius;

        var observerAltitude = occupiedCell.Altitude;
        
        var edgeCoordinates = occupiedCell.HexCell.coordinates.Move(HexDirection.W, SightRadius);
        foreach (HexDirection dir in Enum.GetValues(typeof(HexDirection)))
        {
            for (var i = 0; i < SightRadius; i++)
            {
                var line = HexCoordinates.LineThroughHexes(occupiedCell.HexCell.coordinates, edgeCoordinates);
                
                var highestAltitude = observerAltitude;
                var highestStep = 0;

                for (var step = 0; step < line.Count; step ++)
                {
                    var pos = HexCoordinates.RevertCoordinates(line[step]);
                    if (!manager.PositionInBounds(pos)) break;
                    
                    var cell = manager.cells[pos.x, pos.y];
                    var altitude = cell.Altitude;
                    
                    if (step > 0 && (highestAltitude <= altitude || highestStep == 0))
                    {
                        highestAltitude = altitude;
                        highestStep = step;
                    }
                    
                    var indexX = pos.x - occupiedCell.position.x + SightRadius;
                    var indexY = pos.y - occupiedCell.position.y + SightRadius;
                    var k = (altitude - observerAltitude) / (step == 0 ? 1 : step);
                    cellVisibility[indexX][indexY] = highestAltitude <= k * highestStep + observerAltitude + SightAltitudeThreshold
                        ? Math.Max(SightRadius - step, cellVisibility[indexX][indexY]) : 0;
                    
                    //cell.Color = Color.Lerp(Color.black, Color.white, cellVisibility[indexX][indexY] / 5f);
                }

                edgeCoordinates = edgeCoordinates.GetNeighbor(dir);
            }     
        }
        
        //PaintItBlack(true);
    }

    private void CheckVisibilityRecursively(HexDirection direction, Vector2Int pos, int veerDebt, int step, float highestAltitude, int highestStep)
    {
        if (step >= SightRadius) return;
        
        var observerAltitude = occupiedCell.Altitude;
        var altitude = manager.cells[pos.x, pos.y].Altitude;

        
        if (step > 0 && (highestAltitude >= altitude || highestStep == 0))
        {
            highestAltitude = altitude;
            highestStep = step;
        }
        var indexX = pos.x - occupiedCell.position.x + SightRadius;
        var indexY = pos.y - occupiedCell.position.y + SightRadius;
        var k = (altitude - observerAltitude) / (step == 0 ? 1 : step);
        cellVisibility[indexX][indexY] = highestAltitude <= k * highestStep + observerAltitude
            ? Math.Max(SightRadius - step, cellVisibility[indexX][indexY]) : 0;

        HexDirection[] possibleDirections;
        if (veerDebt == 0) possibleDirections = new[] {direction, direction.Next(), direction.Previous()};
        else if (veerDebt == 1) possibleDirections = new[] {direction.Previous()};
        else if (veerDebt == 2) possibleDirections = new[] {direction.Next(), direction};
        else if (veerDebt == 3) possibleDirections = new[] {direction.Next()};
        else possibleDirections = new[] {direction.Previous(), direction}; // after return after Next
        
        var i = veerDebt - 1;
        foreach (var newDirection in possibleDirections)
        {
            i++;
            var newPos = newDirection.GetNeighborCoordinatesByDirection(pos.x, pos.y);
            if (!manager.PositionInBounds(newPos) /*||
                manager.cells[newPos.x, newPos.y].Altitude - altitude < SightAltitudeThreshold*/) continue;

            CheckVisibilityRecursively(newDirection, newPos, i, step + 1, highestAltitude, highestStep);
        }
    }

    private void PaintItBlack(bool yeah)
    {
        for (var i = 0; i < cellVisibility.Length; i++)
        for (var j = 0; j < cellVisibility[i].Length; j++)
        {
            var x = occupiedCell.position.x + i - SightRadius;
            var z = occupiedCell.position.y + j - SightRadius;
            if (!manager.PositionInBounds(x, z)) continue;
            manager.cells[x, z].Color = yeah
                ? Color.Lerp(Color.black, Color.white, cellVisibility[i][j] / 5f)
                : manager.cells[x, z].Color = manager.cells[x, z].Biome.color;
        }
    }

    public override void MakeFinalPreparations()
    {
        LeaveCell();
        isDead = true;
        Debug.Log("Traveller '" + moniker + "' (" + name + ") has perished.");
        Destroy(gameObject);
    }
}
