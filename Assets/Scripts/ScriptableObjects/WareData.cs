using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Ware", menuName = "ScriptableObjects/Ware", order = 1)]
    public class WareData : ManufacturableData
    {
        [NonSerialized] 
        public int index;

        [SerializeField, Tooltip("How much consumption of the ware satisfies a need per unit")]
        public List<NeedSatisfaction> needSatisfactionOnConsumption;
        
        [NonSerialized] 
        public readonly float[] satisfactionOnConsumption = new float[Enum.GetValues(typeof(NeedType)).Length];

        public List<Vehicle> vehicle;

        public float absoluteWorth;

        /*public Dictionary<Type, float> SatisfactionOnConsumption = new Dictionary<Type, float>();
        public Dictionary<Type, float> SatisfactionOnHaving = new Dictionary<Type, float>();*/

            /*private void TranslateSatisfactionToDict(IEnumerable<NeedSatisfaction> list, IDictionary<Type, float> dict)
        {
            foreach (var sat in list) dict.Add(sat.need, sat.satisfaction);
        }*/

        //[Tooltip("Indigestion and the like")]
        //public float damageOnConsumption;

        public List<string> properties; // make interfaces? other scriptable objects?
        // burnable, animal?, vehicle, beautiful?, comfortable?, 
        // how would you address uselessness of gold - shiny, another satisfaction on having?
        // how to address speed of vehicle? exception - might put a variable increasing speed or carry capacity
        // no need to have smeltable - recipes. animal, though?
        // too many burnable things. could also use soft materials for pot making, rough materials for construction, etc. 

        public override void Cache()
        {
            base.Cache();
            
            foreach (var satisfaction in needSatisfactionOnConsumption)
            {
                satisfactionOnConsumption[(int) satisfaction.need] = satisfaction.value;
            }
        }

        [Serializable]
        public struct Vehicle
        {
            public VehicleType type;
            public float speedMultiplier;
            
            public enum VehicleType { Land, Water, Air }
        }
    }

    public class Ware : Manufacturable
    {
        public new WareData Data
        {
            get => (WareData) base.Data;
            private set => base.Data = value;
        }

        public Ware(WareData data, float amount, int index)
        {
            Data = data;
            Amount = amount;
            state = 100;
            this.index = index;
        }

        public Ware(WareData data, float amount, float state, int index)
        {
            Data = data;
            Amount = amount;
            State = state;
            this.index = index;
        }
    }
}