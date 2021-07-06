using UnityEngine;

public class MeshData
{
    private Vector3[] vertices;
    private readonly int[] triangles;
    private Vector2[] uvs;
    private Vector3[] bakedNormals;

    private readonly Vector3[] borderVertices;
    private readonly int[] borderTriangles;

    private int triangleIndex;
    private int borderTriangleIndex;

    private readonly bool useFlatShading;

    public MeshData(int verticesPerLine, bool useFlatShading)
    {
        this.useFlatShading = useFlatShading;

        vertices = new Vector3[verticesPerLine * verticesPerLine];
        uvs = new Vector2[verticesPerLine * verticesPerLine];
        triangles = new int[(verticesPerLine - 1) * (verticesPerLine - 1) * 6];

        borderVertices = new Vector3[verticesPerLine * 4 + 4];
        borderTriangles = new int[24 * verticesPerLine];
    }

    public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex)
    {
        if (vertexIndex < 0)
        {
            borderVertices[-vertexIndex - 1] = vertexPosition;
        }
        else
        {
            vertices[vertexIndex] = vertexPosition;
            uvs[vertexIndex] = uv;
        }
    }

    public void AddTriangle(int a, int b, int c)
    {
        if (a < 0 || b < 0 || c < 0)
        {
            borderTriangles[borderTriangleIndex] = a;
            borderTriangles[borderTriangleIndex + 1] = b;
            borderTriangles[borderTriangleIndex + 2] = c;
            borderTriangleIndex += 3;
        }
        else
        {
            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }
    }

    private Vector3[] CalculateNormals()
    {
        var vertexNormals = new Vector3[vertices.Length];
        var triangleCount = triangles.Length / 3;
        for (var i = 0; i < triangleCount; i++)
        {
            var normalTriangleIndex = i * 3;
            var vertexIndexA = triangles[normalTriangleIndex];
            var vertexIndexB = triangles[normalTriangleIndex + 1];
            var vertexIndexC = triangles[normalTriangleIndex + 2];

            var triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
            vertexNormals[vertexIndexA] += triangleNormal;
            vertexNormals[vertexIndexB] += triangleNormal;
            vertexNormals[vertexIndexC] += triangleNormal;
        }

        var borderTriangleCount = borderTriangles.Length / 3;
        for (var i = 0; i < borderTriangleCount; i++)
        {
            var normalTriangleIndex = i * 3;
            var vertexIndexA = borderTriangles[normalTriangleIndex];
            var vertexIndexB = borderTriangles[normalTriangleIndex + 1];
            var vertexIndexC = borderTriangles[normalTriangleIndex + 2];

            var triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
            if (vertexIndexA >= 0) vertexNormals[vertexIndexA] += triangleNormal;
            if (vertexIndexB >= 0) vertexNormals[vertexIndexB] += triangleNormal;
            if (vertexIndexC >= 0) vertexNormals[vertexIndexC] += triangleNormal;
        }

        for (var i = 0; i < vertexNormals.Length; i++) vertexNormals[i].Normalize();

        return vertexNormals;
    }

    private Vector3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC)
    {
        var pointA = indexA < 0 ? borderVertices[-indexA - 1] : vertices[indexA];
        var pointB = indexB < 0 ? borderVertices[-indexB - 1] : vertices[indexB];
        var pointC = indexC < 0 ? borderVertices[-indexC - 1] : vertices[indexC];

        var sideAB = pointB - pointA;
        var sideAC = pointC - pointA;
        return Vector3.Cross(sideAB, sideAC).normalized;
    }

    public void ProcessMesh()
    {
        if (useFlatShading)
            FlatShading();
        else
            BakeNormals();
    }

    private void BakeNormals()
    {
        bakedNormals = CalculateNormals();
    }

    private void FlatShading()
    {
        var flatShadedVertices = new Vector3[triangles.Length];
        var flatShadedUvs = new Vector2[triangles.Length];

        for (var i = 0; i < triangles.Length; i++)
        {
            flatShadedVertices[i] = vertices[triangles[i]];
            flatShadedUvs[i] = uvs[triangles[i]];
            triangles[i] = i;
        }

        vertices = flatShadedVertices;
        uvs = flatShadedUvs;
    }

    public Mesh CreateMesh()
    {
        var mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        if (useFlatShading)
            mesh.RecalculateNormals();
        else
            mesh.normals = bakedNormals;
        return mesh;
    }
}