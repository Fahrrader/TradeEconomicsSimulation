using System;
using UnityEngine;
using Random = System.Random;

namespace WorldGen
{
    public static class Rand
    {
        private static Random _random;

        public static void Initialize(int seed)
        {
            _random = new Random(seed);
        }

        // Perlin Noise
        public static float[,] GenerateNoiseMap(
            int mapWidth, int mapHeight,
            int seed, float scale,
            int octaves, float persistance, float lacunarity,
            Vector2 offset, bool isHex)
        {
            var noiseMap = new float[mapWidth, mapHeight];

            var prng = new Random(seed);
            var octaveOffsets = new Vector2[octaves];

            float maxPossibleHeight = 0;
            float amplitude = 1;

            for (var i = 0; i < octaves; i++)
            {
                var offsetX = prng.Next(-100000, 100000) + offset.x;
                var offsetY = prng.Next(-100000, 100000) - offset.y;
                octaveOffsets[i] = new Vector2(offsetX, offsetY);

                maxPossibleHeight += amplitude;
                amplitude *= persistance;
            }

            if (scale <= 0) scale = 0.0001f;

            var maxLocalNoiseHeight = float.MinValue;
            var minLocalNoiseHeight = float.MaxValue;

            var halfWidth = mapWidth / 2f;
            var halfHeight = mapHeight / 2f;

            for (var y = 0; y < mapHeight; y++)
            {
                for (var x = 0; x < mapWidth; x++)
                {
                    amplitude = 1;
                    float frequency = 1;
                    float noiseHeight = 0;

                    for (var i = 0; i < octaves; i++)
                    {
                        var sampleY = (y - halfHeight + octaveOffsets[i].y) / scale * frequency;
                        var sampleX = (x - halfWidth + octaveOffsets[i].x + (isHex ? sampleY % 2 * 0.5f : 0)) / scale * frequency;

                        var perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                        noiseHeight += perlinValue * amplitude;

                        amplitude *= persistance;
                        frequency *= lacunarity;
                    }

                    if (noiseHeight > maxLocalNoiseHeight)
                        maxLocalNoiseHeight = noiseHeight;
                    else if (noiseHeight < minLocalNoiseHeight)
                        minLocalNoiseHeight = noiseHeight;
                    noiseMap[x, y] = noiseHeight;

                    /*var normalizedHeight = (noiseMap[x, y] + 1) / (maxPossibleHeight / 0.9f);
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);*/
                }
            }

            for (var y = 0; y < mapHeight; y++)
            for (var x = 0; x < mapWidth; x++)
                noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);

            return noiseMap;
        }
        
        public static float Gaussian(float mean = 0.0f, float widthFactor = 1f)
        {
            var f = 1 - _random.NextDouble();
            var num = 1 - _random.NextDouble();
            return (float) (Math.Sqrt(-2f * Math.Log(f)) * Math.Sin(Math.PI * 2 * num)) * widthFactor + mean;
        }

        public static float GaussianAsymmetric(
            float mean = 0.0f,
            float lowerWidthFactor = 1f,
            float upperWidthFactor = 1f)
        {
            var f = 1 - _random.NextDouble();
            var num1 = 1 - _random.NextDouble();
            var num2 = (float) (Math.Sqrt(-2f * Math.Log(f)) * Math.Sin(Math.PI * 2 * num1));
            return num2 <= 0.0 ? num2 * lowerWidthFactor + mean : num2 * upperWidthFactor + mean;
        }
    }

    [Serializable]
    public struct NoiseConfiguration
    {
        public float noiseScale;
        public int octaves;
        [Range(0, 1)] public float persistance;
        public float lacunarity;

        public NoiseConfiguration(float noiseScale, int octaves, float persistance, float lacunarity)
        {
            this.noiseScale = noiseScale;
            this.octaves = octaves;
            this.persistance = persistance;
            this.lacunarity = lacunarity;
        }
    }
}