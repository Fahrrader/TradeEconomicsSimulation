using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ScriptableObjects
{
    // the one that has to be harvested from cells first
    [CreateAssetMenu(fileName = "New Resource", menuName = "ScriptableObjects/Resource", order = 3)]
    public class ResourceData : ThingData
    {
        public int minimumAmountToSpawn = -1;
    
        [Range(0, 1)]
        public float frequency = 1;

        public AnimationCurve waterLevelSensitivityCurve = new AnimationCurve(
            new Keyframe(0, 1), 
            new Keyframe(WorldManager.MaxRainfall / 100 + 1000, 1));

        public AnimationCurve temperatureSensitivityCurve = new AnimationCurve(
            new Keyframe(WorldManager.MinTemperature, 1),
            new Keyframe(WorldManager.MaxTemperature, 1));
    
        public bool canBeUnderwater = true;

        [Tooltip("Multiplies spawn frequency in given biomes by the amount")]
        public List<BiomePresence> biomePresenceList;
        [Tooltip("Spawns only in the selected biomes")]
        public bool isBiomeExclusive;

        private Dictionary<Biome, float> biomePresence;

        public Dictionary<Biome, float> BiomePresence
        {
            get
            {
                if (biomePresence == null)
                    biomePresence = new Dictionary<Biome, float>();
                foreach (var presence in biomePresenceList)
                {
                    biomePresence[presence.biome] = presence.presence;
                }
                return biomePresence;
            }
        }
    
        public float replenishmentRate;
        public bool Replenishable => replenishmentRate != 0f;

        public float absoluteWorth;

        public float baseHarvestCost;
        // adjust harvest cost by biome?
    }

    public class Resource : Thing
    {
        public new ResourceData Data
        {
            get => (ResourceData) base.Data;
            private set => base.Data = value;
        }
        
        public float harvestCost;
        private float regrowthRate;
        
        /*private float amount;

        public float Amount
        {
            get => amount;
            set => amount = Mathf.Clamp(value, 0, data.maxAmount);
        }*/

        public Resource(ResourceData data, float amount)
        {
            Data = data;
            Amount = amount;
            harvestCost = data.baseHarvestCost;
            CalculateRegrowthRate(amount);
        }
        
        public Resource(ResourceData data, float amount, float balance)
        {
            Data = data;
            Amount = amount;
            harvestCost = data.baseHarvestCost;
            CalculateRegrowthRate(balance);
        }

        public Resource(ResourceData data, float amount, float balance, float harvestCost)
        {
            Data = data;
            Amount = amount;
            this.harvestCost = harvestCost;
            CalculateRegrowthRate(balance);
        }

        public void Regrow(float deltaTime)
        {
            Amount += (regrowthRate * (Data.maxAmount - Amount) / Data.maxAmount - 1) * Amount * Data.replenishmentRate
                      * deltaTime;
        }

        //public int AmountInt => Mathf.FloorToInt(Amount);

        public int Balance => Mathf.FloorToInt(Data.maxAmount * (1 - 1 / regrowthRate));

        private void CalculateRegrowthRate(float balance)
        {
            regrowthRate = 1 / (1 - balance / Data.maxAmount);
        }
    }

    [Serializable]
    public struct BiomePresence
    {
        public Biome biome;
        public float presence;
    }
}