using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct HexCoordinates
{
    [SerializeField] private int x, z;

    public int X => x;

    public int Z => z;

    public int Y => -X - Z;

    public HexCoordinates(int x, int z)
    {
        this.x = x;
        this.z = z;
    }

    public static HexCoordinates operator -(HexCoordinates a, HexCoordinates b)
    {
        return new HexCoordinates(a.X - b.X, a.Z - b.Z);
    }

    public static HexCoordinates FromOffsetCoordinates(int x, int z)
    {
        return new HexCoordinates(x - z / 2, z);
    }

    public static HexCoordinates FromPosition(Vector3 position)
    {
        var x = position.x / (HexMetrics.InnerRadius * 2f);
        var y = -x;

        var offset = position.z / (HexMetrics.OuterRadius * 3f);
        x -= offset;
        y -= offset;

        var iX = Mathf.RoundToInt(x);
        var iY = Mathf.RoundToInt(y);
        var iZ = Mathf.RoundToInt(-x - y);

        if (iX + iY + iZ != 0)
        {
            var dX = Mathf.Abs(x - iX);
            var dY = Mathf.Abs(y - iY);
            var dZ = Mathf.Abs(-x - y - iZ);

            if (dX > dY && dX > dZ)
                iX = -iY - iZ;
            else if (dZ > dY) iZ = -iX - iY;
        }

        return new HexCoordinates(iX, iZ);
    }

    public static Vector2Int RevertCoordinates(HexCoordinates coordinates)
    {
        return new Vector2Int(coordinates.x + coordinates.z / 2, coordinates.z);
    }

    public HexCoordinates GetNeighbor(HexDirection direction)
    {
        switch (direction)
        {
            case HexDirection.NE:
                return new HexCoordinates(x, z + 1);
            case HexDirection.E:
                return new HexCoordinates(x + 1, z);
            case HexDirection.SE:
                return new HexCoordinates(x + 1, z - 1);
            case HexDirection.SW:
                return new HexCoordinates(x, z - 1);
            case HexDirection.W:
                return new HexCoordinates(x - 1, z);
            case HexDirection.NW:
                return new HexCoordinates(x - 1, z + 1);
        }

        return new HexCoordinates(x, z);
    }
    
    public HexDirection GetNeighborDirection(HexCoordinates destination)
    {
        var difference = destination - this;
        if (difference.X > 0)
            return difference.Z >= 0 ? HexDirection.E : HexDirection.SE;

        if (difference.X == 0)
            return difference.Z > 0 ? HexDirection.NE : HexDirection.SW;

        if (difference.X < 0)
            return difference.Z <= 0 ? HexDirection.W : HexDirection.NW;

        return (HexDirection) 6;
    }

    public HexCoordinates Move(HexDirection direction, int steps)
    {
        var coordinates = new HexCoordinates(x, z);
        for (var i = 0; i < steps; i++) coordinates = coordinates.GetNeighbor(direction);
        return coordinates;
    }
    
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static HexCoordinates CubeRound(Vector3 cube)
    {
        var rx = Mathf.RoundToInt(cube.x);
        var ry = Mathf.RoundToInt(cube.y);
        var rz = Mathf.RoundToInt(cube.z);

        var xDiff = Mathf.Abs(rx - cube.x);
        var yDiff = Mathf.Abs(ry - cube.y);
        var zDiff = Mathf.Abs(rz - cube.z);

        if (xDiff > yDiff && xDiff > zDiff)
            rx = -ry - rz;
        else if (yDiff > zDiff)
            ry = -rx - rz;
        else
            rz = -rx - ry;

        return new HexCoordinates(rx, rz);
    }

    private static Vector3 CubeLerp(HexCoordinates a, HexCoordinates b, float t)
    {
        return new Vector3(
            Lerp(a.X, b.X, t), 
            Lerp(a.Y, b.Y, t),
            Lerp(a.Z, b.Z, t));
    }

    public static List<HexCoordinates> LineThroughHexes(HexCoordinates a, HexCoordinates b)
    {
        var distance = a.GetDistance(b);
        var coordinates = new List<HexCoordinates>();
        for (var i = 0; i <= distance; i++)
        {
            coordinates.Add(CubeRound(CubeLerp(a, b, 1f/distance * i)));
        }

        return coordinates;
    }

    public int GetDistance(HexCoordinates other)
    {
        return ((x < other.x ? other.x - x : x - other.x) +
                (Y < other.Y ? other.Y - Y : Y - other.Y) +
                (z < other.z ? other.z - z : z - other.z)) / 2;
    }

    public override string ToString()
    {
        return "(" + X + ", " /*+ Y.ToString() + ", " */ + Z + ")";
    }

    public string ToStringOnSeparateLines()
    {
        return X + /*"\n" + Y +*/ "\n" + Z;
    }
}