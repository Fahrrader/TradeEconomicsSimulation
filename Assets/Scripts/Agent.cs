using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AI;
using ScriptableObjects;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using WorldGen;
using Action = AI.Action;
using Debug = UnityEngine.Debug;
using Random = System.Random;

public abstract class Agent : MonoBehaviour
{
    protected const int SightRadius = 5; // displace to appropriate class?
    protected const int SightDiameter = SightRadius * 2;
    protected const float SightAltitudeThreshold = 500f;

    public const float BaseCarryingCapacity = 40; // in kg, per 1 pop
    public const float BaseWeight = 70; // in kg, of 1 pop; for horses and other vehicles
    public const float MaxCarryingCapacityMul = 2; // can't collect wares if more than that * base

    public string moniker;

    public WorldManager manager;

    public Agent founder;
    public float creationTime; // in seconds

    public AIBase ai = new MonteCarloTreeSearch();

    protected Queue<ActionArguments> plan = new Queue<ActionArguments>();

    public Queue<string> actionSequence = new Queue<string>();

    public bool executingAction;
    
    // zone of visibility;
    // knowledge;

    public AgentState state;

    [NonSerialized] 
    public bool waresChanged = true;

    // public Queue<Action> plan;

    // skills?
    
    // favor to each agent traded with

    protected void InitializeNeeds()
    {
        var prevSum = 0f;
        var total = state.needs.Length;
        
        for (var i = state.needs.Length - 1; i >= 0; i--)
        {
            var t = (total - prevSum) / (i + 1);
            if (i >= (int) NeedType.Greed)
            {
                // randomize depletion and happiness gain for non-vital needs
                if (founder)
                    state.needs[i] = new Need(
                        Rand.Gaussian(
                            Need.DepletionBase,
                            Need.DepletionDistributionWidthBase *
                            (founder.state.needs[i].depletionRate / Need.DepletionBase)),
                        Rand.Gaussian(
                            founder.state.needs[i].happinessWeight 
                            + (t - founder.state.needs[i].happinessWeight) * Need.HappinessDistributionWidth,
                            Need.HappinessDistributionWidth));
                else
                    state.needs[i] = new Need(
                        Rand.Gaussian(
                            Need.DepletionBase,
                            Need.DepletionDistributionWidthBase),
                        Rand.Gaussian(t, Need.HappinessDistributionWidth));
            }
            else
            {
                // if a vital need, should deplete equally for all
                if (founder)
                    state.needs[i] = new Need(
                        Need.DepletionBase, 
                        Mathf.Clamp(
                            Rand.Gaussian(
                                founder.state.needs[i].happinessWeight + (t - founder.state.needs[i].happinessWeight) *
                                Need.HappinessDistributionWidth,
                                Need.HappinessDistributionWidth),
                            0.5f, 5));
                else
                    state.needs[i] = new Need(
                        Need.DepletionBase, 
                        Mathf.Clamp(
                            Rand.Gaussian(t, Need.HappinessDistributionWidth), 
                            0.5f, 5));
            }
            prevSum += state.needs[i].happinessWeight;
        }
    }

    protected virtual void OnUpdate()
    {
        state.DecayWares(Time.deltaTime);
        state.DecayNeeds(Time.deltaTime);
        //state.CalculateHappiness();
        state.Health = state.CalculateHealth();
        // some trigger (low health) to urgently re-plan
    }

    protected virtual void MakePlan()
    {
        var sw = new Stopwatch();
        sw.Start();
        plan = ai.Plan();
        sw.Stop();
        //Debug.Log(name + " planned in " + sw.Elapsed);

        actionSequence.Clear();
        string s = "";
        foreach (var action in plan)
        {
            actionSequence.Enqueue(DescribeAction(action));
            s += DescribeAction(action);
        }
        //Debug.Log(s);

        StartCoroutine(nameof(FollowPlan));
    }

    protected virtual IEnumerator FollowPlan()
    {
        if (plan.Count == 0) yield break;
        var action = plan.Dequeue();
        var time = ExecuteAction(action);
        if (time < 0)
        {
            Debug.LogWarning("problem with the following action - " + action.action);
            plan.Clear(); // ?
            yield break;
        }
        
        executingAction = true;
        yield return new WaitForSeconds(time);
        actionSequence.Dequeue();
        executingAction = false;
        
        StartCoroutine(nameof(FollowPlan));
    }

    protected virtual float ExecuteAction(ActionArguments action)
    {
        switch (action.action)
        {
            case Action.Consume:
                var wareToConsume = state.FindManufacturable(action.ware);
                if (wareToConsume == null) return -1;
                state.Consume(wareToConsume, action.amount);
                break;
            case Action.Produce:
                for (var i = 0; i < action.recipe.ingredients.Count; i++)
                {
                    var ingredient = state.FindManufacturable(action.recipe.ingredients[i].manufacturable);
                    if (ingredient == null) return -1;
                    action.recipe.ingredients[i] = new Recipe.ManufacturablePortion(ingredient, action.recipe.ingredients[i].amount);
                }
                
                for (var i = 0; i < action.recipe.tools.Count; i++)
                {
                    var tool = state.FindManufacturable(action.recipe.tools[i].manufacturable);
                    if (tool == null) return -1;
                    action.recipe.tools[i] = new Recipe.ManufacturablePortion(tool, action.recipe.tools[i].amount);
                }
                
                var timeToProduce = state.FollowRecipe(action.recipe, action.amount, action.amountOfWorkers);
                if (timeToProduce != action.time) Debug.LogWarning("Different times to PRODUCE from plan to action: " + action.time + " " + timeToProduce);
                return timeToProduce;
            case Action.Relinquish:
                var wareToRelinquish = state.FindManufacturable(action.ware);
                if (wareToRelinquish == null) return -1;
                state.Relinquish(wareToRelinquish, action.amount);
                break;
            default:
                return -1;
        }
        
        return 0;
    }

    protected string DescribeAction(ActionArguments action)
    {
        //Debug.Log(action.action + " " + action.amount + " " + action.ware);
        //if (state.Population > 1 && action.action == Action.Produce) Debug.Log("produce " + action.amount + " " + action.amountOfWorkers);
        switch (action.action)
        {
            case Action.Consume:
                return "consume " + action.amount + " of " + action.ware.Data.name + "; ";
            case Action.Produce:
                return "follow " + action.amount + " of recipe " + action.recipe.data.name + 
                       (action.amountOfWorkers > 1 ? " with " + action.amountOfWorkers + " workers; " : "; ");
            case Action.Trade:
                return "try to trade, apparently; ";
            case Action.Relinquish:
                return "relinquish " + action.ware.Data.name + "; ";
            case Action.Travel:
                return "move to " + action.cell.position + ", " + action.amount + " cells away; ";
            default:
                return "do a weird action; ";
        }
    }

    protected virtual void Observe()
    {
    }

    [BurstCompile(CompileSynchronously = true)]
    protected struct PossibleRecipeFinder : IJob
    {
        [ReadOnly] 
        public NativeArray<float> input;
        
        
        
        public void Execute()
        {
            
        }
    }

    public virtual void MakeFinalPreparations()
    {
    }
}