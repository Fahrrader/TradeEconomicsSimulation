using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    public abstract class ManufacturableData : ThingData
    {
        [Tooltip("Time in years required for decay to reach 0% state (dry under roof with room temperature)")] 
        public float decayTime = 5;

        [Tooltip("Percentage of health lost")]
        public float decayOnUse;

        [NonSerialized]
        public float decayRate; //25 / decayTime / WorldManager.SeasonDuration; // 100 / 4
    
        [Tooltip("Mass of a single unit, in kg")]
        public float mass = 1;

        [Tooltip("How much can this unit hold, in kg")]
        public float carryingCapacity;

        public int maxUsers; // todo change to float, floor when applied, if a traveller can have more than 1 pop to govern a ship etc.

        [SerializeField, Tooltip("How much having of the ware satisfies a need per unit")]
        public List<NeedSatisfaction> needSatisfactionOnHaving;
    
        [NonSerialized] 
        public readonly float[] satisfactionOnHaving = new float[Enum.GetValues(typeof(NeedType)).Length];

        public override void Cache()
        {
            base.Cache();
            
            decayRate = 25 / decayTime / WorldManager.SeasonDuration;
        
            foreach (var satisfaction in needSatisfactionOnHaving)
            {
                satisfactionOnHaving[(int) satisfaction.need] = satisfaction.value;
            }
            /*TranslateSatisfactionToDict(satisfactionOnConsumption, SatisfactionOnConsumption);
            TranslateSatisfactionToDict(satisfactionOnHaving, SatisfactionOnHaving);*/
        }
    }

    [Serializable]
    public struct NeedSatisfaction
    {
        public NeedType need;
        public float value;
    }
    
    public class Manufacturable : Thing
    {
        public new ManufacturableData Data
        {
            get => (ManufacturableData) base.Data;
            protected set => base.Data = value;
        }

        public float AmountInUse { get; private set; }

        protected float state;
        public float State
        {
            get => state;
            set => state = Mathf.Clamp(value, 0, 100);
        }

        public int index;
    
        public const float StateSimilarityUpperBound = 10;
        public const float StateSimilarityLowerBound = 3;

        protected Manufacturable()
        {
            state = 100;
        }
        
        public Manufacturable(ManufacturableData data, float amount, int index)
        {
            Data = data;
            Amount = amount;
            state = 100;
            this.index = index;
        }
        
        public Manufacturable(ManufacturableData data, float amount, float state, int index)
        {
            Data = data;
            Amount = amount;
            State = state;
            this.index = index;
        }

        public Manufacturable(Manufacturable manufacturable)
        {
            Data = manufacturable.Data;
            amount = manufacturable.amount;
            state = manufacturable.state;
            index = manufacturable.index;
        }

        /*public Manufacturable(ManufacturableData data, int amount)
        {
            this.data = data;
            this.amount = Mathf.Clamp(amount, 0, data.maxAmount);
            state = 100;
        }

        public Manufacturable(ManufacturableData data, int amount, float state)
        {
            this.data = data;
            this.amount = Mathf.Clamp(amount, 0, data.maxAmount);
            State = state;
        }*/

        /** Returns the added amount (maxAmount - amount). */
        public float SetAmount(float value)
        {
            var oldAmount = amount;
            amount = Mathf.Clamp(value, 0, Data.maxAmount);//value > Data.maxAmount ? Data.maxAmount : value;
            return amount - oldAmount;
        }

        public float SetAmountInUse(float value)
        {
            var oldAmountInUse = AmountInUse;
            AmountInUse = Mathf.Clamp(value, 0, amount);
            var change = AmountInUse - oldAmountInUse;
            if (change > 0) Use(change);
            return change;
        }

        public void Decay(float deltaTime)
        {
            if (Data.decayTime != 0) State -= Data.decayRate * deltaTime;
        }

        public void Use(float amountUsed)
        {
            State -= amountUsed * Data.decayOnUse;
        }
    }
}
