using UnityEngine;

namespace UI
{
    public static class TextureGenerator
    {
        public static Texture2D TextureFromColorMap(Color[] colourMap, int width, int height) 
        {
            var texture = new Texture2D(width, height)
            {
                filterMode = FilterMode.Point, 
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(colourMap);
            texture.Apply();
            return texture;
        }
    }
}
