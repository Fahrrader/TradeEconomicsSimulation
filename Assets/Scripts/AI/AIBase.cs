using System;
using System.Collections.Generic;
using System.Linq;
using ScriptableObjects;
using UnityEngine;
using WorldGen;
using Random = UnityEngine.Random;

namespace AI
{
    /* follow recipe
     * consume
     * trade?
     * relinquish??
     * c: expand
     * c: spawn traveller -- what would be the fitness function?
     * t: travel
     * t: found a city -- and give some wares. difficult? should be a separate heuristic
     */
    public enum Action
    {
        Consume, // should definitely put aside (long travel, manufacture);
        // another, simpler heuristic function called on update, but still may be called with after every action to simulate
        Produce,
        Trade, // tries to trade to save time, huh
        Relinquish,
        Travel
    }

    public struct ActionArguments
    {
        public Action action;
        public float time;
        
        public Manufacturable ware;
        public Recipe recipe;
        public Cell cell;
        public int amount;
        public int amountOfWorkers;
    }
    
    public class AIBase
    {
        protected const float MinimumFitness = -10000f;

        protected readonly List<float> actionProbabilities = new List<float> {
            2f,
            3.5f,
            2.5f,
            0.35f,
            0.5f
        };
        
        [NonSerialized]
        public Agent agent;

        public Queue<string> actionSequence;

        public virtual Queue<ActionArguments> Plan()
        {
            return new Queue<ActionArguments>();
        }

        protected ActionArguments IterateAction(AgentState state, List<Cell> cells) // List<Cell>
        {
            var action = PickAction(state.isTraveller);
            //actionsPickNumber[(int) action] ++;
            var actionArguments = Execute(action, state, cells);

            return actionArguments;
        }

        protected static bool AfterEveryAction(AgentState state, float deltaTime, List<Cell> cells/* = null*/)
        {
            state.DecayWares(deltaTime);
            state.DecayNeeds(deltaTime);
            state.happiness = state.CalculateHappiness();
            state.Health = state.CalculateHealth();
            if (state.Health <= 0 || state.Population < 1) return false;
            if (!state.isTraveller) state.GrowPopulation(deltaTime); 
            // if wares changed, update recipes
            state.FindAllPossibleRecipes(cells);
            return true;
        }

        protected float actionProbabilitySum;

        protected Action PickAction(bool isTraveller)
        {
            if (actionProbabilitySum == 0)
            {
                if (isTraveller) actionProbabilities[2] = 0; //.RemoveAt(actionProbabilities.Count - 1);
                else actionProbabilities[4] = 0;
                actionProbabilitySum = actionProbabilities.Sum();
                for (var i = 0; i < actionProbabilities.Count; i++)
                {
                    actionProbabilities[i] /= actionProbabilitySum;
                }
            }

            var r = Random.value;
            var prevSum = 0f;
            for (var i = 0; i < actionProbabilities.Count; i++)
            {
                if (r < actionProbabilities[i] + prevSum) return (Action) i;
                prevSum += actionProbabilities[i];
            }
            
            return 0;
        }
        
        protected static ActionArguments Execute(Action action, AgentState state, List<Cell> cells) // need potentials
        {
            switch (action)
            {
                case Action.Consume:
                    var wareToConsume = ChooseThing<WareData>(state, data => data.needSatisfactionOnConsumption.Count > 0);
                    if (wareToConsume == null) return new ActionArguments{time = -1};
                    
                    // best amount to satisfy needs
                    var amountToConsume = (int) Mathf.Clamp(
                        Rand.Gaussian(state.Population, wareToConsume.Amount / 3f),
                        1, wareToConsume.Amount);
                    
                    state.Consume(wareToConsume, amountToConsume);
                    
                    return new ActionArguments{ware = wareToConsume, amount = amountToConsume, action = action, time = 0};
                
                case Action.Produce:
                    var recipe = ChooseRecipe(state);
                    if (recipe.data == null) return new ActionArguments{time = -1};
                    
                    var amountOfWorkers = //Random.Range(1, recipe.amount);
                        (int) Mathf.Clamp(
                            Rand.Gaussian(state.populationFree / 2f, state.populationFree / 4f),
                            1, state.populationFree);

                    var maxAmount = Mathf.Min(recipe.amount, recipe.amountOfWorkers);

                    var amountOfRecipe =
                        (int) Mathf.Clamp(
                        Rand.Gaussian(amountOfWorkers / 2f, amountOfWorkers / 3f),//maxAmount / 2f, maxAmount / 3f),
                        amountOfWorkers / recipe.data.maxWorkers, Mathf.Min(amountOfWorkers, maxAmount)); // todo fix breaking resources
                    
                    var timeToProduce = state.FollowRecipe(recipe, amountOfRecipe, amountOfWorkers);
                   
                    if (timeToProduce < 0) return new ActionArguments{action = action, time = timeToProduce};
                    return new ActionArguments{recipe = recipe, amount = amountOfRecipe, amountOfWorkers = amountOfWorkers, action = action, time = timeToProduce};
                
                case Action.Trade:
                    // if 0 time
                    if (state.manufacturablesCount.Count == 0) return new ActionArguments {time = -1};
                    var traders = new List<Traveller>();
                    foreach (var tradeCell in cells)
                    {
                        traders.AddRange(tradeCell.occupants.Where(traveller => traveller.state.manufacturablesCount.Count > 0));
                    }
                    
                    if (traders.Count == 0) return new ActionArguments {time = -1};

                    var trader = traders[Random.Range(0, traders.Count)];
                    var thing = ChooseThing<WareData>(trader.state, _ => true);
                    var value = state.priceFinder.CalculateImmediateValue(thing.Data, thing.Amount);
                    
                    // find best match, knapsack problem
                    
                    var ownThing = ChooseThing<WareData>(state, _ => true);
                    if (ownThing == null) return new ActionArguments {time = -1};

                    state.Relinquish(ownThing);
                    //state.Collect(thing.Data, thing.Amount, thing.State);

                    return new ActionArguments{action = action, time = 0};
                
                case Action.Relinquish:
                    var wareToRelinquish = ChooseThing<WareData>(state, _ => true);
                    if (wareToRelinquish == null) return new ActionArguments{time = -1};
                    
                    var amountToRelinquish = wareToRelinquish.AmountInt;
                    state.Relinquish(wareToRelinquish, amountToRelinquish); // change amount
                    
                    return new ActionArguments{ware = wareToRelinquish, amount = amountToRelinquish, action = action, time = 0};
                
                case Action.Travel:
                    // are there cities / travellers? no, don't chase travellers
                    if (state.CantMove) return new ActionArguments{time = -1};
                    var occupiedCell = cells[0];
                    Cell cell;
                    do 
                        cell = ChooseCellInRange(occupiedCell, 5);
                    while (cell == occupiedCell);
                    
                    var timeToTravel = Pathfinding.Pathfinding.FindPathTime(occupiedCell, cell, state.speedMultipliers);
                    var distance = cell.GetDistance(occupiedCell);
                    
                    return new ActionArguments{time = timeToTravel, action = action, cell = cell, amount = distance};
            }

            return new ActionArguments{action = action, time = 0};
        }

        public static Manufacturable ChooseThing<TY>(AgentState state, Func<TY, bool> satisfactionFunc) 
            where TY: ManufacturableData// amount? already chosen wares? heuristic?
        {
            var randomRange = state.manufacturables.Count;
            if (randomRange == 0) return null;
            
            for (var tryCounter = 0; tryCounter < randomRange * 2.5f; tryCounter++)
            {
                var data = RandomKey(state.manufacturables);
                if (!(data is TY) || !satisfactionFunc(data as TY)) continue;
                var list = state.manufacturables[data];
                return list[Random.Range(0, list.Count)];
            }

            foreach (var pair in state.manufacturables)
            {
                if (!(pair.Key is TY)) continue;
                var list = state.manufacturables[pair.Key];
                return list[Random.Range(0, list.Count)];
            }

            return null;
        }

        protected static Recipe ChooseRecipe(AgentState state) // amount? already chosen recipes? heuristic?
        {
            var randomRange = state.possibleRecipes.Count;
            if (randomRange == 0f) return new Recipe();
            var recipe = state.possibleRecipes[Random.Range(0, randomRange)];
            //if ((recipe.ingredients.Count > 0 || recipe.tools.Count > 0) && Random.value < 0.25f) return recipe; // todo place random value into a constant

            var manufacturableSet = new HashSet<Manufacturable>();
            recipe.ingredients.Clear();
            foreach (var ingredient in recipe.data.ingredients)
            {
                switch (ingredient.ingredient)
                {
                    case ManufacturableData manufacturableData:
                        if (!state.manufacturables.TryGetValue(manufacturableData, out var list)) return new Recipe();
                        var amountCollected = 0f;
                        for (var i = 0; amountCollected < ingredient.amount && i < list.Count * 5f; i++)
                        {
                            var manufacturable = list[Random.Range(0, list.Count)];
                            if (!manufacturableSet.Add(manufacturable)) continue;
                            var amountLeft = manufacturable.Amount - ingredient.amount; // amountFree
                            var amountExtracted = amountLeft >= 0 ? manufacturable.Amount - amountLeft : manufacturable.Amount;
                            recipe.ingredients.Add(new Recipe.ManufacturablePortion(manufacturable, amountExtracted));
                            amountCollected += amountExtracted;
                        }
                        
                        if (amountCollected < ingredient.amount) return new Recipe();
                        
                        break;
                    case ResourceData resourceData:
                        // todo check that we have enough resource
                        //recipe.resources.Where(resourceCell => resourceCell.resource.Data == resourceData)
                        break;
                }
            }

            /*recipe.tools.Clear();
            foreach (var tool in recipe.data.tools) // todo
            {
                var isAbsent = !state.manufacturables.TryGetValue(tool.tool, out var list);
                if (isAbsent && tool.isRequired) return new Recipe();
                if (!isAbsent && Random.value < 0.25f) continue; // todo const
                var amountCollected = 0f;
                for (var i = 0; amountCollected < tool. && i < list.Count * 5f; i++)
                {
                    var manufacturable = list[Random.Range(0, list.Count)];
                    if (!manufacturableSet.Add(manufacturable)) continue;
                    var amountLeft = manufacturable.Amount - ingredient.amount;
                    var amountExtracted = amountLeft >= 0 ? manufacturable.Amount - amountLeft : manufacturable.Amount;
                    recipe.ingredients.Add(new Recipe.ManufacturablePortion(manufacturable, amountExtracted));
                    amountCollected += amountExtracted;
                }
            }*/

            return recipe;
        }

        protected static Cell ChooseCellInRange(Cell cell, int range = 1)
        {
            for (var i = 0; i < range; i++)
            {
                Cell newCell;
                do
                {
                    newCell = cell.GetNeighbor((HexDirection) Random.Range(0, 5));
                } while (newCell == null);
                cell = newCell;
            }

            return cell;
        }

        public static TKey RandomKey<TKey, TValue>(IDictionary<TKey, TValue> dict) // todo displace somewhere appropriate
        {
            var values = dict.Keys.ToList();
            var size = dict.Count;
            return values[Mathf.FloorToInt(Random.value * size)];
        }
        
        /*public IEnumerable<TValue> RandomValues<TKey, TValue>(IDictionary<TKey, TValue> dict)
        {
            var values = dict.Values.ToList(); // Keys
            var size = dict.Count;
            while(true)
            {
                yield return values[Mathf.FloorToInt(Random.value * size)];
            }
        }*/
    }
}
