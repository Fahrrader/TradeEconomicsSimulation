using System;
using System.Collections.Generic;
using ScriptableObjects;
using UnityEngine;

namespace AI
{
    public class PriceFinder
    {
        internal class Node
        {
            public ThingData data;
            public float potentialAmount;
            public float timeToProduce;
            // possible amounts?
            public readonly List<Node> children = new List<Node>();
            public readonly Node parent;

            public Node(ThingData data, float potentialAmount, float timeToProduce, Node parent)
            {
                this.data = data;
                this.potentialAmount = potentialAmount;
                this.timeToProduce = timeToProduce;
                this.parent = parent;
            }

            public Node AddChild(ThingData thing, float potentialAmount, float timeToProduce)
            {
                var child = new Node(thing, potentialAmount, timeToProduce, this);
                children.Add(child);
                return child;
            }
        }
        
        [NonSerialized]
        public AgentState state; // or state?

        // todo traverse tree of possible recipes (or wares?), calculate current usefulness
        // back propagate, update with further recipe outcomes
        // include possibility to trade in trade actions
        // make cities calculate prices whenever a new ware is introduced? too often? every season? before every trade deal?
        // favor?
        // show prices in city interface
        // also complete list of scriptables
        // put on github and add link to the appendix

        public readonly float[] potentials = new float[6];

        private void FillInitialPotentials()
        {
            for (var i = 0; i < potentials.Length; i++) potentials[i] = 0;
            foreach (var pair in state.manufacturablesCount)
                IncreasePotentials(pair.Key, pair.Value, 0);
        }

        private void IncreasePotentials(ManufacturableData manufacturable, float possibleAmount, float timeToProduce)
        {
            // perhaps don't calculate potentials if ware already taken in account
            var t = possibleAmount / (timeToProduce + 1); //  / state.Population
            
            if (manufacturable is WareData ware)
                foreach (var needSatisfaction in ware.needSatisfactionOnConsumption)
                    potentials[(int) needSatisfaction.need] += t * 
                        needSatisfaction.value / state.needs[(int) needSatisfaction.need].depletionRate;

            foreach (var needSatisfaction in manufacturable.needSatisfactionOnHaving)
                potentials[(int) needSatisfaction.need] += t * 
                    2 * needSatisfaction.value / state.needs[(int) needSatisfaction.need].depletionRate;
        }
        
        //public void FindPrice(WareData ware) {} // for when a new ware is offered by traveller

        public float CalculateImmediateValue(ManufacturableData thing, float potentialAmount)
        {
            // actually, calculate it when going back
            if (prices.ContainsKey(thing)) return prices[thing]; 
            //summarize instead? or take the highest value? it is immediate value, same for all repetitions
            // potential amount can be different
            
            var ware = thing as WareData;
            var value = 0f;
            foreach (var needSatisfaction in thing.needSatisfactionOnHaving)
            {
                value += //state.Population / potentialAmount * 
                    2 * needSatisfaction.value / 100 / state.needs[(int) needSatisfaction.need].depletionRate; // * 100 / potentials[(int) needSatisfaction.need]
            }

            if (ware)
            {
                foreach (var needSatisfaction in ware.needSatisfactionOnConsumption)
                {
                    value += //state.Population / potentialAmount * 
                        needSatisfaction.value / 100 / state.needs[(int) needSatisfaction.need].depletionRate; // * 100 / potentials[(int) needSatisfaction.need] 
                    // oh no, don't take potential amount into consideration at all // or take population
                    // / potentials[i]; // potentials could either be gigantic, or equal to 0; what then?   
                }
            }

            var speedBonus = 1f;
            if (ware)
            {
                foreach (var vehicle in ware.vehicle)
                {
                    speedBonus += vehicle.speedMultiplier - 1;
                }
            }
            value += thing.carryingCapacity / Agent.BaseWeight * speedBonus;

            prices.Add(thing, value);
            /*if (prices.ContainsKey(thing))
            {
                prices[thing] = value;
                maxPossibleAmounts[thing] = potentialAmount;
            }
            else
            {
                prices.Add(thing, value);
                maxPossibleAmounts.Add(thing, value);
            }*/

            //Debug.Log("price for " + thing.name + ": " + value);
            return value;
        }

        private Node recipeRoot = new Node(null, 0, 0, null);
        private readonly List<Node> terminals = new List<Node>();

        public readonly Dictionary<ManufacturableData, float> prices = new Dictionary<ManufacturableData, float>();
        //private readonly Dictionary<ManufacturableData, float> maxPossibleAmounts = new Dictionary<ManufacturableData, float>();

        public void Show()
        {
            var p = new List<Node> {recipeRoot};
            while (p.Count != 0)
            {
                var s = p.Count + ": ";
                for (var i = p.Count - 1; i >= 0; i--)
                {
                    var node = p[i];
                    if (node.data != null) s += node.data.name + "; ";
                    p.RemoveAt(i);
                    p.InsertRange(i, node.children);
                }
                Debug.Log(s);
            }
        }

        public void FindPrices()
        {
            prices.Clear();

            foreach (var pair in state.manufacturablesCount)
            {
                CalculateImmediateValue(pair.Key, pair.Value);
            }
            //maxPossibleAmounts.Clear();
            
            // pass in current satisfaction of needs? solves the problem with lack of greed
            FillInitialPotentials(); // only for existing manufacturables
            BuildRecipeTree();
            
            for (var i = 0; i < potentials.Length; i++)
                if (Mathf.Abs(potentials[i]) < 1) potentials[i] = Mathf.Sign(potentials[i]);
            
            BackPropagate();
        }

        private void BackPropagate()
        {
            foreach (var terminal in terminals)
            {
                var node = terminal;
                float value;
                if (node.data is ManufacturableData terminalManufacturable)
                    value = CalculateImmediateValue(terminalManufacturable, node.potentialAmount);
                else
                {
                    value = 0; // eh, sweet looping   
                }

                while (node.parent.data is ManufacturableData manufacturable)
                {
                    var parentValue = CalculateImmediateValue(manufacturable, node.parent.potentialAmount) + 
                                      value / (1 + node.timeToProduce);
                    prices[manufacturable] = parentValue;
                    value = parentValue;
                    node = node.parent;
                }
            }
        }

        private void BuildRecipeTree()
        {
            terminals.Clear();
            recipeRoot.children.Clear();

            foreach (var recipe in state.possibleRecipes)
            {
                foreach (var ingredient in recipe.data.ingredients)
                {
                    var child = recipeRoot.AddChild(ingredient.ingredient, recipe.amount, recipe.data.timeCost);
                    ExpandBranch(child, recipe.data.timeCost); // / workers?
                }
            }
        }

        private void ExpandBranch(Node node, float timeCost)
        {
            //if (node.data is ManufacturableData manufacturable)
                //CalculateImmediateValue(manufacturable, node.potentialAmount);
                
            if (node.data is ManufacturableData manufacturable) 
                IncreasePotentials(manufacturable, node.potentialAmount, timeCost);

            var successfulRecipes = 0;
            foreach (var recipe in node.data.involvingRecipes)
            {
                var ingredient = new RecipeData.Ingredient();
                var isIngredient = false;
                //var amountOfIngredients = 0f;
                foreach (var i in recipe.ingredients)
                {
                    //amountOfIngredients += i.amount;
                    if (i.ingredient == node.data)
                    {
                        ingredient = i;
                        isIngredient = true;
                        break;
                    }
                }
                var tool = new RecipeData.Tool();
                var isTool = false;
                foreach (var t in recipe.tools)
                {
                    if (t.tool == node.data)
                    {
                        tool = t;
                        isTool = true;
                        break;
                    }
                }
                // or a tool, but make the distinction
                if (!isIngredient && !isTool) continue;
                successfulRecipes++;
                // check for other unsatisfied ingredients? too complicated
                // add the recipes involving as ingredient? time to produce, or time reduction

                float amount;
                if (isIngredient)
                {
                    amount = node.potentialAmount / ingredient.amount;//amountOfIngredients; // * (ingredient.amount / amountOfIngredients) / ingredient.amount;
                }
                else
                {
                    // if tool, then what amount to make? occupation! but what if occupation is 0?
                    // if the final result is summed up, then just 1? no, unfair to occupation=.01. population!
                    amount = tool.useOccupation == 0 ? state.Population : Mathf.Min(node.potentialAmount / tool.useOccupation, state.Population);
                    // should have lesser impact than ingredient, this can be 20, 50, 1000 per one tool + reduced time, unfair
                    amount /= 100;
                }

                foreach (var product in recipe.result)
                {
                    var potentialAmount = amount * product.amount;
                    var timeReduced = recipe.timeCost;
                    //if (isIngredient) potentialAmount *= product.amount;
                    if (isTool) timeReduced *= 1 - tool.timeReduction;
                    
                    var childNode = node.AddChild(product.product, potentialAmount, timeReduced);
                    
                    //Debug.Log(node.data.name + " - " + product.product.name);
                    if (product.product is ResourceData || IsThingRepeated(node, product.product)) // || potentialAmount < 0.01f
                    {
                        terminals.Add(childNode); // ignore this child node, start from its parent? -- lots of repetition, but worth it
                        continue;
                    }
                    ExpandBranch(childNode, timeCost + timeReduced);
                }
            }

            if (successfulRecipes == 0) terminals.Add(node);
        }

        private bool IsThingRepeated(Node node, ThingData thing)
        {
            while (true)
            {
                if (node.data == thing) return true;
                if (!node.parent.data) return false;
                node = node.parent;
            }
        }
    }
}
