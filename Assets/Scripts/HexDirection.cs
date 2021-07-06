using System;
using System.Collections;
using UnityEngine;

public enum HexDirection
{
    NE,
    E,
    SE,
    SW,
    W,
    NW
}

public static class HexDirectionExtensions
{
    public static HexDirection Opposite(this HexDirection direction)
    {
        return (int) direction < 3 ? direction + 3 : direction - 3;
    }

    public static HexDirection Previous(this HexDirection direction)
    {
        return direction == HexDirection.NE ? HexDirection.NW : direction - 1;
    }

    public static HexDirection Next(this HexDirection direction)
    {
        return direction == HexDirection.NW ? HexDirection.NE : direction + 1;
    }

    public static HexDirection Previous2(this HexDirection direction)
    {
        direction -= 2;
        return direction >= HexDirection.NE ? direction : direction + 6;
    }

    public static HexDirection Next2(this HexDirection direction)
    {
        direction += 2;
        return direction <= HexDirection.NW ? direction : direction - 6;
    }

    public static int DistanceTo(this HexDirection direction, HexDirection direction2)
    {
        var i = Math.Abs((int) direction2 - (int) direction) % 6;
        return i > 3 ? 6 - i : i;
    }

    public static HexDirection Move(this HexDirection direction, int steps)
    {
        direction += steps;
        var i = (int) direction % 6;
        return (HexDirection)(i >= 0 ? i : i + 6);
    }

    public static Vector2Int GetNeighborCoordinatesByDirection(this HexDirection direction, int x, int z)
    {
        switch (direction)
        {
            case HexDirection.NE:
                return new Vector2Int(x + (z & 1), z + 1);
            case HexDirection.E:
                return new Vector2Int(x + 1, z);
            case HexDirection.SE:
                return new Vector2Int(x + (z & 1), z - 1);
            case HexDirection.SW:
                return new Vector2Int(x - ((z + 1) & 1), z - 1);
            case HexDirection.W:
                return new Vector2Int(x - 1, z);
            case HexDirection.NW:
                return new Vector2Int(x - ((z + 1) & 1), z + 1);
        }

        return new Vector2Int(x, z);
    }

    public static bool IsValid(this HexDirection direction)
    {
        return (int) direction >= 0 && (int) direction < 6;
    }

    public static Vector2Int GetNeighborCoordinatesByDirection(this HexDirection direction, Vector2Int pos)
    {
        return GetNeighborCoordinatesByDirection(direction, pos.x, pos.y);
    }
}