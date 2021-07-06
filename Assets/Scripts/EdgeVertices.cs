using UnityEngine;

public struct EdgeVertices
{
	public Vector3[] v;//v1, v2, v3, v4, v5;

	private EdgeVertices(bool _)
	{
		v = new Vector3[5];
	}

	public EdgeVertices(Vector3 corner1, Vector3 corner2) 
	{
		v = new Vector3[5];
		for (var i = 0; i < v.Length; i++)
		{
			v[i] = Vector3.Lerp(corner1, corner2, i / (v.Length - 1f));
		}
	}

	public EdgeVertices(Vector3 corner1, Vector3 corner2, float outerStep)
	{
		v = new Vector3[5];
		v[0] = corner1;
		v[1] = Vector3.Lerp(corner1, corner2, outerStep);
		v[2] = Vector3.Lerp(corner1, corner2, 0.5f);
		v[3] = Vector3.Lerp(corner1, corner2, 1f - outerStep);
		v[4] = corner2;
	}

	public static EdgeVertices TerraceLerp (
		EdgeVertices a, EdgeVertices b, int step)
	{
		var result = new EdgeVertices(false);
		for (var i = 0; i < a.v.Length; i++)
			result.v[i] = HexMetrics.TerraceLerp(a.v[i], b.v[i], step);
		return result;
	}
}