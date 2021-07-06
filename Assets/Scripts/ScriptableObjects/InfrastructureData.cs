using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Infrastructure", menuName = "ScriptableObjects/Infrastructure", order = 2)]
    public class InfrastructureData : ManufacturableData
    {
        
    }
    
    public class Infrastructure : Manufacturable
    {
        public new InfrastructureData Data
        {
            get => (InfrastructureData) base.Data;
            private set => base.Data = value;
        }

        public Infrastructure(InfrastructureData data, float amount, int index)
        {
            Data = data;
            Amount = amount;
            state = 100;
            this.index = index;
        }

        public Infrastructure(InfrastructureData data, float amount, float state, int index)
        {
            Data = data;
            Amount = amount;
            State = state;
            this.index = index;
        }
    }
}
