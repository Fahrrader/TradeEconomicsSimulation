using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    public class SceneResourceHolder : MonoBehaviour
    {
        public List<BiomeData> biomes;
    
        public List<ResourceData> resources;

        public List<WareData> wares;

        public List<InfrastructureData> infrastructure;

        public List<RecipeData> recipes;
    }
}
