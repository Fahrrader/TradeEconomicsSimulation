using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AI;
using ScriptableObjects;
using UnityEngine;
using Debug = UnityEngine.Debug;

[Serializable]
public class AgentState
{
    public const float DangerNeedThreshold = 67;
    
    public const float GrowthRatePerSeason = 0.25f;

    public static float growthRatePerSecond =
        GrowthRatePerSeason / (100 - DangerNeedThreshold) / 10 / WorldManager.SeasonDuration; 

    public Agent agent;

    public PriceFinder priceFinder = new PriceFinder();

    public bool isNotSimulated = true;
    
    private float health = 100;
    
    public float Health
    {
        get => health;
        set
        {
            health = Mathf.Clamp(value, 0, 100); 
            if (isNotSimulated && health == 0) agent.MakeFinalPreparations();
        }
    }

    public float happiness;

    private float population = 1;

    public float Population
    {
        get => population;
        set
        {
            populationFree += (int) (value - population);
            population = value;
            if (isNotSimulated && population < 1) agent.MakeFinalPreparations();
        }
    }
    
    public int PopulationInt => Mathf.FloorToInt(Population);

    public int populationFree = 1;

    public float carryingCapacity;

    public float infrastructureSpaceLeft;

    public float weight;

    public float[] speedMultipliers = new float[] {1, 1, 1};

    [NonSerialized]
    public bool isTraveller;
    public bool CantMove => weight > carryingCapacity || !isTraveller;

    public readonly HashSet<WareData>[] vehicleSets = new HashSet<WareData>[3];

    public readonly Need[] needs = new Need[6];

    public readonly float[] passiveNeedSatisfaction = new float[6];

    public readonly Dictionary<ManufacturableData, List<Manufacturable>> manufacturables =
        new Dictionary<ManufacturableData, List<Manufacturable>>();

    public readonly Dictionary<ManufacturableData, float> manufacturablesCount =
        new Dictionary<ManufacturableData, float>();

    public int manufacturableIndex;

    public Manufacturable lastAddedTo;

    public readonly List<Recipe> possibleRecipes = new List<Recipe>();

    public AgentState(Agent agent, int population)
    {
        this.agent = agent;
        priceFinder.state = this;
        isTraveller = agent is Traveller;
        
        Health = 100;
        Population = population;
        //populationFree = PopulationInt;
        carryingCapacity += Population * Agent.BaseCarryingCapacity; // + stuff from vehicles - wares
        for (var i = 0; i < vehicleSets.Length; i++)
            vehicleSets[i] = new HashSet<WareData>();
        //weight += Population * Agent.BaseWeight; don't need that, sum up agent weight and vehicle weight when need be
    }
    
    public AgentState(AgentState anotherState)
    {
        agent = anotherState.agent;
        priceFinder.state = this; // todo calculate prices again? naw, but will need to transfer the prices
        isTraveller = anotherState.isTraveller;
        isNotSimulated = anotherState.isNotSimulated;
        
        Health = anotherState.Health;
        happiness = anotherState.happiness;
        Population = anotherState.Population;
        //populationFree = anotherState.populationFree;
        
        for (var i = 0; i < needs.Length; i++)
        {
            needs[i] = new Need(anotherState.needs[i]);
            AdjustPassiveNeedSatisfaction(i, anotherState.passiveNeedSatisfaction[i], true);
        }

        carryingCapacity = anotherState.carryingCapacity;
        weight = anotherState.weight;
        infrastructureSpaceLeft = anotherState.infrastructureSpaceLeft;
        manufacturableIndex = anotherState.manufacturableIndex;
        foreach (var pair in anotherState.manufacturables)
        {
            manufacturablesCount.Add(pair.Key, anotherState.manufacturablesCount[pair.Key]);
            manufacturables.Add(pair.Key, new List<Manufacturable>());
            foreach (var manufacturable in pair.Value)
            {
                var newManufacturable = new Manufacturable(manufacturable); 
                // mayhaps make manufacturables and the rest as structs? - easier to operate with burst, but data is still a class
                manufacturables[pair.Key].Add(newManufacturable);
                if (anotherState.lastAddedTo == manufacturable) lastAddedTo = newManufacturable;
            }
        }

        for (var i = 0; i < vehicleSets.Length; i++)
            vehicleSets[i] = anotherState.vehicleSets[i]; // should be fine? refs to static data

        foreach (var recipe in anotherState.possibleRecipes)
        {
            possibleRecipes.Add(new Recipe(recipe.data, recipe.amount, recipe.amountOfWorkers));
            if (recipe.resources.Count > 0) possibleRecipes[possibleRecipes.Count - 1].resources.AddRange(recipe.resources);
        }
    }

    public float CalculateHealth()
    {
        var health = 0f;
        var i = 0;
        for (; i < (int) NeedType.Greed; i++)
        {
            if (needs[i].Value <= 0)
            {
                health = 0;
                return health;
            }
            health += needs[i].Value;
        }
        health /= i;
        return health;
    }
    
    public float CalculateHappiness()
    {
        var happiness = 0f;
        for (var i = 0; i < (int) NeedType.Greed; i++)
        {
            happiness += (needs[i].Value - DangerNeedThreshold) * needs[i].happinessWeight;
            if (needs[i].Value == 0)
            {
                happiness = -1000f;
                return happiness;
            }
        }
        for (var i = (int) NeedType.Greed; i < needs.Length; i++)
        {
            happiness += needs[i].Value * needs[i].happinessWeight;
        }

        return happiness;
    }
    
    public void GrowPopulation(float deltaTime)
    {
        var prevPop = Population;
        Population *= Mathf.Exp(deltaTime * growthRatePerSecond * (Health - DangerNeedThreshold));
        /*GrowthRatePerSeason * (Health - DangerNeedThreshold) / 
                                (100 - DangerNeedThreshold) / 10);*/
        var popDiff = Mathf.FloorToInt(Population) - Mathf.FloorToInt(prevPop);
        populationFree += popDiff;
        carryingCapacity += popDiff * Agent.BaseCarryingCapacity;
    }
    
    public void DecayWares(float deltaTime)
    {
        var listToRemove = new List<ManufacturableData>();
        foreach (var pair in manufacturables)
        {
            if (manufacturablesCount[pair.Key] <= 0)
            {
                listToRemove.Add(pair.Key);
                continue;
            }

            if (pair.Key.decayRate == 0) continue;
            
            for (var i = pair.Value.Count - 1; i >= 0; i--)
            {
                pair.Value[i].Decay(deltaTime);
                if (pair.Value[i].State == 0)
                {
                    /*manufacturablesCount[pair.Key] -= pair.Value[i].Amount;
                    pair.Value.RemoveAt(i);
                    if (isNotSimulated) agent.waresChanged = true;*/
                    RemoveManufacturable(pair.Value[i], pair.Value[i].Amount, i);
                }
            }
            if (manufacturablesCount[pair.Key] <= 0) listToRemove.Add(pair.Key);
        }

        foreach (var md in listToRemove)
        {
            RemoveManufacturablesOfType(md);
        }
    }

    private float[] passiveNeedDepletion = {1, 1, 1, 1, 1, 1};

    private float[] passiveNeedSatisfactionAdjusted = new float[6];

    public void AdjustPassiveNeedSatisfaction(int i, float valueToAdd, bool ignoreNeed = false)
    {
        // todo also account for potential prices to add to Greed or Comfort
        valueToAdd /= population;
        passiveNeedSatisfaction[i] += valueToAdd;
        if (valueToAdd > 0 && !ignoreNeed) needs[i].Add(valueToAdd);
        if (i < (int) NeedType.Greed)
        {
            passiveNeedSatisfactionAdjusted[i] = passiveNeedSatisfaction[i];
            return;
        }
        passiveNeedDepletion[i] = (100 - Mathf.Min(passiveNeedSatisfaction[i], 95)) / 100;
        passiveNeedSatisfactionAdjusted[i] = Mathf.Min(50, passiveNeedSatisfaction[i] * 0.01f);
    }

    public void DecayNeeds(float deltaTime)
    {
        for (var i = 0; i < (int) NeedType.Greed; i++)
        {
            needs[i].Deplete(deltaTime, passiveNeedSatisfaction[i]);
        }
        
        for (var i = (int) NeedType.Greed; i < needs.Length; i++)
        {
            needs[i].Deplete(deltaTime * passiveNeedDepletion[i], passiveNeedSatisfactionAdjusted[i]);
        }
    }

    public void Merge(AgentState state)
    {
        foreach (var pair in state.manufacturables)
        {
            if (!manufacturables.ContainsKey(pair.Key))
            {
                manufacturables.Add(pair.Key, new List<Manufacturable>(pair.Value));
                manufacturablesCount.Add(pair.Key, state.manufacturablesCount[pair.Key]);
            }
            else
            {
                manufacturables[pair.Key].AddRange(pair.Value);
                manufacturablesCount[pair.Key] = state.manufacturablesCount[pair.Key];
            }

            foreach (var manufacturable in pair.Value)
                manufacturable.index = manufacturableIndex++;
        }
        
        carryingCapacity += state.carryingCapacity;
        weight += state.weight;
        infrastructureSpaceLeft += state.infrastructureSpaceLeft;

        for (var i = 0; i < needs.Length; i++)
        {
            needs[i].Set((needs[i].Value * Population + state.needs[i].Value * state.Population) / (Population + state.Population));
        }

        Population += state.Population;
    }

    public Manufacturable FindManufacturable(Manufacturable ware)
    {
        return manufacturables[ware.Data].Find(manufacturable => manufacturable.index == ware.index);
        /*return manufacturables[ware.Data].Find(manufacturable =>
            manufacturable.Amount == ware.Amount &&
            Math.Abs(manufacturable.State - ware.State) < 0.5f);*/
    }

    public float Consume(Manufacturable wareToConsume, float amount = 1)
    {
        if (!(wareToConsume.Data is WareData wareData)) return -1;
        amount = Math.Min(amount, wareToConsume.Amount);
        foreach (var satisfaction in wareData.needSatisfactionOnConsumption)
        {
            needs[(int) satisfaction.need].Add(satisfaction.value * amount / Population);
        }
        return Relinquish(wareToConsume, amount);
    }

    public float Relinquish(Manufacturable manufacturable, float amount = 1)
    {
        //var change = manufacturable.Amount > amount ? manufacturable.Amount - amount : 0;//Math.Max(manufacturable.Amount - amount, 0) + amount - manufacturable.Amount;
        /*if (isNotSimulated) agent.waresChanged = true;
        manufacturablesCount[manufacturable.Data] -= amount;
        amount += manufacturable.SetAmount(manufacturable.Amount - amount);
        if (manufacturable.Amount <= 0) manufacturables[manufacturable.Data].Remove(manufacturable);*/
        var remainder = RemoveManufacturable(manufacturable, amount);
        if (manufacturablesCount[manufacturable.Data] <= 0)//manufacturables[manufacturable.Data].Count == 0)
        {
            RemoveManufacturablesOfType(manufacturable.Data);
        }
        return remainder;
    }
    
    protected float RemoveManufacturable(Manufacturable ware, float wareAmount, int i = -1)
    {
        var remainder = 0f;
        if (wareAmount >= ware.Amount)
        {
            remainder = wareAmount - ware.Amount;
            wareAmount = ware.Amount;
            if (i >= 0)
                manufacturables[ware.Data].RemoveAt(i);
            else 
                manufacturables[ware.Data].Remove(ware);
            if (lastAddedTo == ware) lastAddedTo = null;
        }
        ware.SetAmount(ware.Amount - wareAmount);

        foreach (var satisfaction in ware.Data.needSatisfactionOnHaving)
        {
            AdjustPassiveNeedSatisfaction((int) satisfaction.need, -satisfaction.value * wareAmount);
            //needs[(int) satisfaction.need].Add(-satisfaction.value); // no need for passive padding, won't deplete beyond that point, anyway
        }

        manufacturablesCount[ware.Data] -= wareAmount;
        weight -= ware.Data.mass * wareAmount;
        carryingCapacity -= Math.Min(population, ware.Data.maxUsers * wareAmount) * ware.Data.carryingCapacity;
        if (ware.Data is InfrastructureData) infrastructureSpaceLeft += wareAmount * ware.Data.mass;
        if (agent is Traveller && ware.Data is WareData wareData)
        {
            foreach (var vehicle in wareData.vehicle)
            {
                vehicleSets[(int) vehicle.type].Remove(wareData);
                FindBestVehicle((int) vehicle.type);
            }
        }
        
        if (isNotSimulated) agent.waresChanged = true;
        return remainder; //manufacturablesCount[ware.Data] <= 0;
    }

    protected void RemoveManufacturablesOfType(ManufacturableData data)
    {
        manufacturables.Remove(data);
        manufacturablesCount.Remove(data);
        if (data is WareData wareData)
            foreach (var vehicle in wareData.vehicle)
                vehicleSets[(int) vehicle.type].Remove(wareData);
    }

    public void FindBestVehicle(int i)
    {
        speedMultipliers[i] = 1;
        foreach (var ware in vehicleSets[i])
        {
            var vehicle = ware.vehicle.First(v => (int) v.type == i);
            if (vehicle.speedMultiplier <= speedMultipliers[i]) continue;
            if (weight + population * Agent.BaseWeight - manufacturablesCount[ware] * ware.mass <=
                Math.Min(population, ware.maxUsers * manufacturablesCount[ware]) * ware.carryingCapacity)
                speedMultipliers[i] = vehicle.speedMultiplier;
        }
    }
    
    public void Collect(ManufacturableData wareToCollect, float amount = 1, float state = 100)
    {
        var isInfrastructure = wareToCollect is InfrastructureData;
        if (isTraveller && isInfrastructure) return;

        //var newWeight = weight + amount * wareToCollect.mass;
        var newCarryingCapacity = carryingCapacity + Math.Min(population, wareToCollect.maxUsers * amount) * wareToCollect.carryingCapacity;
        // this can reduce amount only if carrying capacity is less than 1/2 weight
        amount = Math.Min(amount, isInfrastructure 
            ? Mathf.Floor(infrastructureSpaceLeft / wareToCollect.mass) 
            : Mathf.Floor((newCarryingCapacity * Agent.MaxCarryingCapacityMul - weight) / wareToCollect.mass));
        if (amount < 1) return;
        
        if (isNotSimulated) agent.waresChanged = true;

        var isNew = false;

        if (!manufacturables.ContainsKey(wareToCollect))
        {
            isNew = true;
            manufacturables.Add(wareToCollect, new List<Manufacturable>());
            manufacturablesCount.Add(wareToCollect, 0);
        }

        manufacturablesCount[wareToCollect] += amount;
        weight += amount * wareToCollect.mass;
        carryingCapacity += Math.Min(population, wareToCollect.maxUsers * amount) * wareToCollect.carryingCapacity;
        if (isInfrastructure) infrastructureSpaceLeft -= amount * wareToCollect.mass;
        if (isTraveller && wareToCollect is WareData ware)
        {
            foreach (var vehicle in ware.vehicle)
            {
                if (isNew) vehicleSets[(int) vehicle.type].Add(ware);
                FindBestVehicle((int) vehicle.type);
            }
        }

        foreach (var satisfaction in wareToCollect.needSatisfactionOnHaving)
        {
            AdjustPassiveNeedSatisfaction((int) satisfaction.need, satisfaction.value * amount);
            //passiveNeedSatisfaction[(int) satisfaction.need] += satisfaction.value * amount;
            //needs[(int) satisfaction.need].Add(satisfaction.value);
        }
        
        if (lastAddedTo != null && lastAddedTo.Data == wareToCollect && 
            lastAddedTo.Amount < lastAddedTo.Data.maxAmount &&
            lastAddedTo.State >= state - Manufacturable.StateSimilarityLowerBound &&
            lastAddedTo.State <= state + Manufacturable.StateSimilarityUpperBound)
        {
            amount -= lastAddedTo.SetAmount(lastAddedTo.Amount + amount);
        }
        
        foreach (var manufacturable in manufacturables[wareToCollect].Where(manufacturable =>
                //ware.data == wareToCollect &&
                manufacturable.Amount < manufacturable.Data.maxAmount &&
                manufacturable.State >= state - Manufacturable.StateSimilarityLowerBound &&
                manufacturable.State <= state + Manufacturable.StateSimilarityUpperBound))
            //.OrderBy(ware => Mathf.Abs(ware.State - state))) // descending?
        {
            amount -= manufacturable.SetAmount(manufacturable.Amount + amount);
            if (amount <= 0)
            {
                lastAddedTo = manufacturable;
                return;
            }
        }
        
        while (amount > 0)
        {
            var amountToAdd = amount >= wareToCollect.maxAmount ? wareToCollect.maxAmount : amount;
            lastAddedTo = new Manufacturable(wareToCollect, amountToAdd, state, manufacturableIndex++);
            manufacturables[wareToCollect].Add(lastAddedTo);
            amount -= amountToAdd;
        }
    }

    public float FollowRecipe(Recipe recipe, int overrideAmount = 0, int overrideWorkers = 0)
        // just better to send orders individually, the current way the uniformity is not ideal
    {
        var amount = overrideAmount > 0 ? overrideAmount : recipe.amount;
        var amountWorkers = overrideWorkers > 0 ? overrideWorkers : recipe.amountOfWorkers;
        amountWorkers = Mathf.Clamp(amountWorkers, 1, 
                                            Math.Min((int) recipe.data.maxWorkers * amount, populationFree));
        if (amountWorkers < 1) amountWorkers = 1;
        if (amountWorkers > populationFree) return -1;
        //recipe.amount = amount;
        //recipe.amountOfWorkers = amountWorkers;
        foreach (var ingredient in recipe.data.ingredients)
        {
            switch (ingredient.ingredient)
            {
                case ResourceData resourceData:
                    /*if (recipe.resources.Count == 0 || recipe.resources[resourceCount].resource.Amount < recipe.amount)
                    {
                        if (isNotSimulated) Debug.LogWarning(agent.name + " tried to harvest a resource that is too small or doesn't exist");
                        return false;
                    }
                    recipe.resources[resourceCount].resource.Amount -= recipe.amount;
                    resourceCount++;*/
                    float resourceAmount = ingredient.amount * amount;
                    foreach (var resourceCell in recipe.resources.Where(resource =>
                        resource.resource.Data == resourceData))
                    {
                        var resourceLeft = resourceCell.resource.Amount - resourceAmount;
                        if (resourceLeft < 0) resourceLeft = 0;
                        resourceAmount -= resourceCell.resource.Amount - resourceLeft;
                        if (isNotSimulated) resourceCell.resource.Amount = resourceLeft;
                        if (resourceAmount <= 0) break;
                    }

                    if (resourceAmount > 0)
                    {
                        //Debug.LogWarning(recipe.data.name + " " + resourceAmount + " tried to harvest a resource that is too small or doesn't exist");
                        return -1;
                    }
                    
                    break;
                case ManufacturableData manufacturableData:
                    float manufacturableAmount = ingredient.amount * amount;
                    /*for (var i = 0; i < recipe.ingredients.Count; i++)
                    {
                        var portion = recipe.ingredients[i];
                        if (portion.manufacturable.Data != manufacturableData || portion.amount == 0) continue;
                        var remainder = Relinquish(portion.manufacturable, portion.amount);
                        recipe.ingredients[i] = new Recipe.ManufacturablePortion(portion.manufacturable, remainder);
                        manufacturableAmount -= recipe.ingredients[i].amount;*/
                    foreach (var portion in recipe.ingredients.Where(ingredient => ingredient.manufacturable.Data == manufacturableData))
                    {
                        manufacturableAmount -= portion.amount - Relinquish(portion.manufacturable, portion.amount);
                        //manufacturableAmount += portion.manufacturable.SetAmount(portion.manufacturable.Amount - Mathf.Min(portion.amount, manufacturableAmount));
                        if (manufacturableAmount <= 0) break;
                    }

                    if (manufacturableAmount > 0)
                    {
                        //Debug.LogWarning("There's no such manufacturable as " + manufacturableData.label + " in ");
                        return -1;
                    }
                    
                    break;
                    /*if (!manufacturables.TryGetValue(manufacturableData, out var manufacturablesList))
                    {
                        if (isNotSimulated) Debug.LogWarning("There's no such manufacturable as " + manufacturableData.label + " in " + agent.name);
                        return false;
                    }
                    
                    float amount = recipe.amount;
                    for (var i = manufacturablesList.Count - 1; i >= 0 && amount > 0; i--)
                    {
                        amount = Relinquish(manufacturablesList[i], amount);
                    }

                    if (amount > 0)
                    {
                        if (isNotSimulated) Debug.LogWarning("There aren't enough manufacturables of " + manufacturableData.label + " in " + agent.name);
                        return false;
                    }*/
                default:
                    Debug.LogWarning("Tried to make a strange thing: " + ingredient.ingredient.label + ", from recipe " + recipe.data.name);
                    break;
            }
        }

        var timeReduction = 0f;
        var workersEquipped = 0f;
        var layer = -1;
        var wasRequired = false;
        foreach (var tool in recipe.data.tools)
        {
            if (tool.layer != layer)
            {
                if (wasRequired && layer >= 0 && workersEquipped < 1) // todo last tool; check if works
                {
                    if (isNotSimulated) Debug.LogWarning(agent.name + " is missing a required instrument " + tool.tool.label);
                    return -1;
                }
                layer = tool.layer;
                workersEquipped = 0;
                wasRequired = tool.isRequired;
            }
            
            var useOccupation = (amountWorkers - workersEquipped) * tool.useOccupation;
            if (!manufacturables.TryGetValue(tool.tool, out var manufacturablesList))
            {
                /*if (tool.isRequired)
                {
                    if (isNotSimulated) Debug.LogWarning(agent.name + " is missing a required instrument " + tool.tool.label);
                    return -1;
                }*/
                continue;
            }
            
            foreach (var manufacturable in manufacturablesList)
            {
                // if tool is required, there should be as much workers as there are available tools
                if (manufacturable.Amount >= manufacturable.AmountInUse + tool.useOccupation)
                {
                    if (tool.useOccupation == 0)
                    {
                        workersEquipped += amountWorkers;
                        continue;
                    }
                    var amountUsed = manufacturable.SetAmountInUse(useOccupation);
                    workersEquipped += amountUsed / tool.useOccupation;
                    useOccupation -= amountUsed;
                }

                if (useOccupation <= 0 && workersEquipped > 0) break;
            }
            timeReduction += tool.timeReduction * workersEquipped / amountWorkers; // or multiplication?
        }
        var timeToProduce = recipe.data.timeCost * (1 - timeReduction) * recipe.amount / amountWorkers;
        
        // add self to plan
        populationFree -= amountWorkers;
        if (isNotSimulated) agent.StartCoroutine(ProcessProduction(recipe, timeToProduce, amount, amountWorkers));
        else Produce(recipe, amount, amountWorkers);
        return timeToProduce;
    }

    public IEnumerator ProcessProduction(Recipe recipe, float timeToProduce, int overrideAmount = 0, int overrideWorkers = 0)
    {
        yield return new WaitForSeconds(timeToProduce);
        Produce(recipe, overrideAmount, overrideWorkers);
    }

    public void Produce(Recipe recipe, int overrideAmount = 0, int overrideWorkers = 0)
    {
        var amount = overrideAmount > 0 ? overrideAmount : recipe.amount;
        var workers = overrideWorkers > 0 ? overrideWorkers : recipe.amountOfWorkers;
        // free tools and population after use
        foreach (var tool in recipe.tools)
        {
            tool.manufacturable.SetAmountInUse(tool.manufacturable.AmountInUse - tool.amount);
        }
        populationFree = Mathf.Clamp(populationFree + workers, 0, PopulationInt);
        //populationFree += workers;
        
        var state = recipe.CalculateOutState();
        foreach (var result in recipe.data.result)
        {
            switch (result.product)
            {
                case ManufacturableData wareData:
                    Collect(wareData, result.amount * amount, result.stateReceived ? state : 100f);
                    break;
                case ResourceData resourceData:
                    var pair = recipe.resources.First(resource => resource.resource.Data == resourceData);
                    if (pair.resource != null) pair.resource.Amount += result.amount * amount;
                    else pair.cell.AddResource(resourceData, result.amount * amount);
                    break;
                /*default:
                    Debug.LogWarning("Tried to Produce a strange manufacturable: " + result.product.label + ", from recipe " + recipe.data.name);
                    break;*/
            }
        }
    }
    
    private void AddRecipeIfPossible(RecipeData recipe, List<Cell> cells)
    {
        var resourceList = new List<Recipe.ResourceCell>();
        var maxPossibleAmount = PopulationInt; // int.MaxValue;
        var satisfies = true;
        foreach (var ingredient in recipe.ingredients)
        {
            var resourceCount = 0f;
            switch (ingredient.ingredient)
            {
                case ManufacturableData manufacturableData:
                    if (!manufacturablesCount.ContainsKey(manufacturableData))
                    {
                        satisfies = false;
                        break;
                    }
                    maxPossibleAmount = Math.Min(maxPossibleAmount,
                        Mathf.FloorToInt(manufacturablesCount[manufacturableData] / ingredient.amount));
                    break;
                case ResourceData resourceData:
                    satisfies = false;
                    foreach (var cell in cells)
                    {
                        foreach (var resource in cell.resources)
                        {
                            if (resource.Amount < ingredient.amount || resource.Data != resourceData) continue;
                            satisfies = true;
                            resourceList.Add(new Recipe.ResourceCell(cell, resource));
                            resourceCount += resource.Amount;
                            break;
                        }
                    }
                    maxPossibleAmount = Math.Min(maxPossibleAmount,
                        Mathf.FloorToInt(resourceCount / ingredient.amount));
                    break;
            }

            if (!satisfies || maxPossibleAmount == 0) break;
        }

        if (!satisfies || maxPossibleAmount == 0) return;

        foreach (var tool in recipe.tools)
        {
            if (!tool.isRequired || manufacturables.ContainsKey(tool.tool)) continue;
            satisfies = false;
            break;
        }

        if (!satisfies) return;
        
        possibleRecipes.Add(new Recipe(recipe, maxPossibleAmount, PopulationInt));
        if (resourceList.Count > 0) possibleRecipes[possibleRecipes.Count - 1].resources.AddRange(resourceList);
        // check that it doesn't exceed maximum weight? nah
    }

    public void FindNewPossibleRecipes(ThingData data, List<Cell> cells = null)
    {
        var newRecipes = data.involvingRecipes
            .Where(recipeData => possibleRecipes.All(recipe => recipe.data != recipeData)).ToList();

        foreach (var recipe in newRecipes)
        {
            AddRecipeIfPossible(recipe, cells);
        }
    }

    public void FindAllPossibleRecipes(List<Cell> cells = null)
    {
        //availableRecipes.Clear();
        possibleRecipes.Clear();
        var isHarvestPossible = cells != null && cells.Count > 0;
        var recipes = new HashSet<RecipeData>();
        foreach (var pair in manufacturables)
        {
            foreach (var recipe in pair.Key.involvingRecipes)
            {
                if ((!isTraveller || recipe.suitableForTraveller) &&
                    (!isHarvestPossible || !recipe.isHarvest)) recipes.Add(recipe);
            }
        }

        if (isHarvestPossible)
        {
            foreach (var cell in cells)
            {
                foreach (var resource in cell.resources)
                {
                    foreach (var recipe in resource.Data.involvingRecipes)
                    {
                        if (!isTraveller || recipe.suitableForTraveller) recipes.Add(recipe);
                    }
                }
            }
        }

        foreach (var recipe in recipes)
        {
            AddRecipeIfPossible(recipe, cells);
        }
        //var job = new PossibleRecipeFinder();
        //job.Schedule().Complete();
    }
}
