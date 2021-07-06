using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ScriptableObjects
{
    public abstract class ThingData : ScriptableObject
    {
        public string label;
        
        [Tooltip("Per stack of units to decay together")]
        public int maxAmount = 10000;

        [NonSerialized]
        public List<RecipeData> involvingRecipes;

        public virtual void Cache()
        {
            involvingRecipes = new List<RecipeData>();
            foreach (var recipe in ResourceHolder.recipes.Where(recipe =>
                recipe.ingredients.Any(ingredient => ingredient.ingredient == this) ||
                recipe.tools.Any(tool => tool.tool == this))) 
                involvingRecipes.Add(recipe);
        }
    }

    public abstract class Thing
    {
        public ThingData Data { get; set; }
        protected float amount;

        public float Amount
        {
            get => amount;
            set => amount = Mathf.Clamp(value, 0, Data.maxAmount);
        }

        public int AmountInt => Mathf.FloorToInt(amount);
    }
}
