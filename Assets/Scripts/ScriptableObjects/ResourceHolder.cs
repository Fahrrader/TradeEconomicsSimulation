using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScriptableObjects
{
    public static class ResourceHolder
    {
        public static List<BiomeData> biomes;
    
        public static List<ResourceData> resources;

        public static List<WareData> wares;

        public static List<InfrastructureData> infrastructure;

        public static List<RecipeData> recipes;

        public static void Populate(SceneResourceHolder anotherResourceHolder)
        {
            biomes = anotherResourceHolder.biomes; //new List<BiomeData>();
            recipes = anotherResourceHolder.recipes; // new List<RecipeData>();
            resources = anotherResourceHolder.resources; // new List<ResourceData>();
            infrastructure = anotherResourceHolder.infrastructure; // new List<InfrastructureData>();
            wares = anotherResourceHolder.wares; // new List<WareData>();

            /*biomes = GetAllObjectsOfType<BiomeData>().ToList();
            resources = GetAllObjectsOfType<ResourceData>().ToList();
            wares = GetAllObjectsOfType<WareData>().ToList();
            infrastructure = GetAllObjectsOfType<InfrastructureData>().ToList();
            recipes = GetAllObjectsOfType<RecipeData>().ToList();*/

            foreach (var ware in wares) ware.Cache();
            foreach (var resource in resources) resource.Cache();
            foreach (var infrastructure in infrastructure) infrastructure.Cache();
            foreach (var recipe in recipes) recipe.Cache();
        }
    
        /*private static IEnumerable<T> GetAllObjectsOfType<T>() where T : ScriptableObject
        {
            //Resources.Load<T>();
            var guids = AssetDatabase.FindAssets("t:"+typeof(T).Name);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                yield return AssetDatabase.LoadAssetAtPath<T>(path);
            }
        }*/
    }
}
