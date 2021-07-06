using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Recipe", menuName = "ScriptableObjects/Recipe", order = 0)]
    public class RecipeData : ScriptableObject
    {
        [SerializeField, Tooltip("In seasons")]
        private float timeCostInSeasons;
        [NonSerialized] 
        public float timeCost;
        public float maxWorkers = 1;
        public bool carriesOnState;
        public Ingredient[] ingredients;
        public Tool[] tools;
        public Product[] result;

        [NonSerialized] public bool suitableForTraveller = true; // false if recipe contains infrastructure
        [NonSerialized] public bool isHarvest;

        public void Cache()
        {
            timeCost = timeCostInSeasons * WorldManager.SeasonDuration;

            suitableForTraveller = !tools.Any(tool => tool.isRequired && tool.tool is InfrastructureData) &&
                                   !result.Any(ware => ware.product is InfrastructureData) &&
                                   !ingredients.Any(ingredient => ingredient.ingredient is InfrastructureData);

            isHarvest = ingredients.Any(ingredient => ingredient.ingredient is ResourceData);
                        // || result.Any
        }

        [Serializable]
        public struct Tool
        {
            public ManufacturableData tool;
            [Tooltip("In fraction of total time cost reduction"), Range(0, 1)]
            public float timeReduction;
            [Tooltip("Tools of the same layers cannot be used together")]
            public byte layer;
            [Tooltip("Occupation of the tool by this recipe and the number of times this tool can be used for this purpose"), Range(0, 1)]
            public float useOccupation; // amountInUse += useOccupation; // maxInUse != 0 ? 1 / maxInUse : 1;
            public bool isRequired;
        }

        [Serializable]
        public struct Ingredient
        {
            public ThingData ingredient;
            public int amount;
            public bool stateCarriedOn;
        }

        [Serializable]
        public struct Product
        {
            public ThingData product;
            public int amount;
            public bool stateReceived;
        }
    }

    public struct Recipe
    {
        public RecipeData data;
        public int amount;
        public int amountOfWorkers; // population segment
        public readonly List<ResourceCell> resources;
        public readonly List<ManufacturablePortion> ingredients;
        public readonly List<ManufacturablePortion> tools;

        public Recipe(RecipeData data, int amount = 1, int amountOfWorkers = 1)
        {
            this.data = data;
            this.amount = amount;
            this.amountOfWorkers = amountOfWorkers;
            resources = new List<ResourceCell>();
            ingredients = new List<ManufacturablePortion>();
            tools = new List<ManufacturablePortion>();
        }

        public float CalculateOutState()
        {
            if (!data.carriesOnState) return 100f;
            
            var iCounter = 0;
            var state = 0f;
            var totalMass = 0f;
            foreach (var ingredient in ingredients)
            {
                if (ingredient.manufacturable.Data != data.ingredients[iCounter].ingredient) iCounter++;
                if (!data.ingredients[iCounter].stateCarriedOn) continue;
                state += ingredient.manufacturable.State * ingredient.manufacturable.Data.mass;
                totalMass += ingredient.manufacturable.Data.mass;
            }

            if (state == 0) return 100f;
            return state / totalMass;
        }

        public struct ManufacturablePortion
        {
            public readonly Manufacturable manufacturable;
            public float amount;

            public ManufacturablePortion(Manufacturable manufacturable, float amount = 1)
            {
                this.manufacturable = manufacturable;
                this.amount = amount;
            }
        }

        public readonly struct ResourceCell
        {
            public readonly Cell cell;
            public readonly Resource resource;

            public ResourceCell(Cell cell, Resource resource)
            {
                this.cell = cell;
                this.resource = resource;
            }
        }
    }
}