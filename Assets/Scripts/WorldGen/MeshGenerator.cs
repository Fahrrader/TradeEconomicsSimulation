using UnityEngine;

public static class MeshGenerator
{
    public static MeshData GenerateTerrainMesh(float[,] heightMap, float heightMultiplier, AnimationCurve _heightCurve,
        int levelOfDetail, bool useFlatShading)
    {
        var heightCurve = new AnimationCurve(_heightCurve.keys);

        var meshSimplificationIncrement = levelOfDetail == 0 ? 1 : levelOfDetail * 2;

        var borderedSize = heightMap.GetLength(0);
        var meshSize = borderedSize - 2 * meshSimplificationIncrement;
        var meshSizeUnsimplified = borderedSize - 2;

        var topLeftX = (meshSizeUnsimplified - 1) / -2f;
        var topLeftZ = (meshSizeUnsimplified - 1) / 2f;

        var verticesPerLine = (meshSize - 1) / meshSimplificationIncrement + 1;

        var meshData = new MeshData(verticesPerLine, useFlatShading);

        var vertexIndicesMap = new int[borderedSize, borderedSize];
        var meshVertexIndex = 0;
        var borderVertexIndex = -1;

        for (var y = 0; y < borderedSize; y += meshSimplificationIncrement)
            for (var x = 0; x < borderedSize; x += meshSimplificationIncrement)
            {
                var isBorderVertex = y == 0 || y == borderedSize - 1 || x == 0 || x == borderedSize - 1;

                if (isBorderVertex)
                {
                    vertexIndicesMap[x, y] = borderVertexIndex;
                    borderVertexIndex--;
                }
                else
                {
                    vertexIndicesMap[x, y] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }

        for (var y = 0; y < borderedSize; y += meshSimplificationIncrement)
            for (var x = 0; x < borderedSize; x += meshSimplificationIncrement)
            {
                var vertexIndex = vertexIndicesMap[x, y];
                var percent = new Vector2((x - meshSimplificationIncrement) / (float) meshSize,
                    (y - meshSimplificationIncrement) / (float) meshSize);
                var height = heightCurve.Evaluate(heightMap[x, y]) * heightMultiplier;
                var vertexPosition = new Vector3(topLeftX + percent.x * meshSizeUnsimplified, height,
                    topLeftZ - percent.y * meshSizeUnsimplified);

                meshData.AddVertex(vertexPosition, percent, vertexIndex);

                if (x < borderedSize - 1 && y < borderedSize - 1)
                {
                    var a = vertexIndicesMap[x, y];
                    var b = vertexIndicesMap[x + meshSimplificationIncrement, y];
                    var c = vertexIndicesMap[x, y + meshSimplificationIncrement];
                    var d = vertexIndicesMap[x + meshSimplificationIncrement, y + meshSimplificationIncrement];
                    meshData.AddTriangle(a, d, c);
                    meshData.AddTriangle(d, a, b);
                }

                vertexIndex++;
            }

        meshData.ProcessMesh();

        return meshData;
    }
}