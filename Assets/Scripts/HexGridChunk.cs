using System;
using UnityEngine;

public class HexGridChunk : MonoBehaviour
{
    public HexMesh terrain, rivers, roads, water, waterShore, estuaries;

    public HexFeatureManager features;

    public bool shouldTriangulate = true;

    private HexCell[] cells;

    private Canvas gridCanvas;

    private int cellUpdateIndex;

    private void Awake()
    {
        gridCanvas = GetComponentInChildren<Canvas>();

        cells = new HexCell[HexMetrics.ChunkSizeX * HexMetrics.ChunkSizeZ];
    }

    public void AddCell(int index, HexCell cell)
    {
        cells[index] = cell;
        cell.chunk = this;
        cell.transform.SetParent(transform, false);
        cell.uiRect.SetParent(gridCanvas.transform, false);
    }

    public void Refresh()
    {
        shouldTriangulate = true;
        //enabled = true;
    }

    private void Update()
    {
        cells[cellUpdateIndex].dataCell.Regrow();
        cellUpdateIndex = (cellUpdateIndex + 1) % cells.Length;
    }

    private void LateUpdate()
    {
        if (!shouldTriangulate) return;
        
        Triangulate();
        shouldTriangulate = false;
        //enabled = false;
    }

    public void Triangulate()
    {
        terrain.Clear();
        rivers.Clear();
        roads.Clear();
        water.Clear();
        waterShore.Clear();
        estuaries.Clear();
        features.Clear();

        foreach (var cell in cells)
            Triangulate(cell);

        terrain.Apply();
        rivers.Apply();
        roads.Apply();
        water.Apply();
        waterShore.Apply();
        estuaries.Apply();
        features.Apply();
    }

    private void Triangulate(HexCell cell)
    {
        for (var d = HexDirection.NE; d <= HexDirection.NW; d++)
            Triangulate(d, cell);
        if (!cell.IsUnderwater && !cell.HasRiver && !cell.HasRoads)
            features.AddFeature(cell, cell.Position);
    }

    private void Triangulate(HexDirection direction, HexCell cell)
    {
        var center = cell.Position;
        var e = new EdgeVertices(
            center + HexMetrics.GetFirstSolidCorner(direction),
            center + HexMetrics.GetSecondSolidCorner(direction)
        );

        if (cell.HasRiver)
        {
            if (cell.HasRiverThroughEdge(direction))
            {
                e.v[2].y = cell.StreamBedY;
                if (cell.HasRiverBeginOrEnd)
                     TriangulateWithRiverBeginOrEnd(direction, cell, center, e);
                else
                    TriangulateWithRiver(direction, cell, center, e);
            }
            else
            {
                TriangulateAdjacentToRiver(direction, cell, center, e);
            }
            
            if (cell.HasRoads && cell.CanHaveRoads) TriangulateRoadAdjacentToRiver(direction, cell, center, e);
        }
        else
        {
            TriangulateWithoutRiver(direction, cell, center, e);

            if (!cell.IsUnderwater && !(cell.HasRoadThroughEdge(direction) && cell.CanHaveRoads))
                features.AddFeature(cell, (center + e.v[0] + e.v[4]) * (1f / 3f));
        }

        if (direction <= HexDirection.SE) TriangulateConnection(direction, cell, e);

        if (cell.IsUnderwater) TriangulateWater(direction, cell, center);
    }

    private void TriangulateWater(
        HexDirection direction, HexCell cell, Vector3 center
    )
    {
        center.y = cell.WaterSurfaceY;

        var neighbor = cell.GetNeighbor(direction);
        if (neighbor != null && !neighbor.IsUnderwater)
            TriangulateWaterShore(direction, cell, neighbor, center);
        else
            TriangulateOpenWater(direction, cell, neighbor, center);
    }

    private void TriangulateOpenWater(
        HexDirection direction, HexCell cell, HexCell neighbor, Vector3 center
    )
    {
        var c1 = center + HexMetrics.GetFirstWaterCorner(direction);
        var c2 = center + HexMetrics.GetSecondWaterCorner(direction);

        water.AddTriangle(center, c1, c2);

        if (direction <= HexDirection.SE && neighbor != null)
        {
            var bridge = HexMetrics.GetWaterBridge(direction);
            var e1 = c1 + bridge;
            var e2 = c2 + bridge;

            water.AddQuad(c1, c2, e1, e2);

            if (direction <= HexDirection.E)
            {
                var nextNeighbor = cell.GetNeighbor(direction.Next());
                if (nextNeighbor == null || !nextNeighbor.IsUnderwater) return;
                water.AddTriangle(
                    c2, e2, c2 + HexMetrics.GetWaterBridge(direction.Next())
                );
            }
        }
    }

    private void TriangulateWaterShore(
        HexDirection direction, HexCell cell, HexCell neighbor, Vector3 center
    )
    {
        var e1 = new EdgeVertices(
            center + HexMetrics.GetFirstWaterCorner(direction),
            center + HexMetrics.GetSecondWaterCorner(direction)
        );
        for (var i = 1; i < e1.v.Length; i++)
            water.AddTriangle(center, e1.v[i - 1], e1.v[i]);

        var center2 = neighbor.Position;
        center2.y = center.y;
        var e2 = new EdgeVertices(
            center2 + HexMetrics.GetSecondSolidCorner(direction.Opposite()),
            center2 + HexMetrics.GetFirstSolidCorner(direction.Opposite())
        );

        if (cell.HasRiverThroughEdge(direction))
        {
            TriangulateEstuary(e1, e2, cell.HasIncomingRiver(direction)); //cell.IncomingRiver == direction);
        }
        else
        {
            for (var i = 1; i < e1.v.Length; i++)
            {
                waterShore.AddQuad(e1.v[i - 1], e1.v[i], e2.v[i - 1], e2.v[i]);
                waterShore.AddQuadUV(0f, 0f, 0f, 1f);
            }
            
            /*waterShore.AddQuadUV(0f, 0f, 0f, 1f);
            waterShore.AddQuadUV(0f, 0f, 0f, 1f);
            waterShore.AddQuadUV(0f, 0f, 0f, 1f);*/
        }

        var nextNeighbor = cell.GetNeighbor(direction.Next());
        if (nextNeighbor != null)
        {
            var v3 = nextNeighbor.Position + (nextNeighbor.IsUnderwater
                ? HexMetrics.GetFirstWaterCorner(direction.Previous())
                : HexMetrics.GetFirstSolidCorner(direction.Previous()));
            v3.y = center.y;
            waterShore.AddTriangle(e1.v[4], e2.v[4], v3);
            waterShore.AddTriangleUV(
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, nextNeighbor.IsUnderwater ? 0f : 1f)
            );
        }
    }

    private void TriangulateEstuary(
        EdgeVertices e1, EdgeVertices e2, bool incomingRiver
    )
    {
        waterShore.AddTriangle(e2.v[0], e1.v[1], e1.v[0]);
        waterShore.AddTriangle(e2.v[4], e1.v[4], e1.v[3]);
        waterShore.AddTriangleUV(
            new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f)
        );
        waterShore.AddTriangleUV(
            new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f)
        );

        estuaries.AddQuad(e2.v[0], e1.v[1], e2.v[1], e1.v[2]);
        estuaries.AddTriangle(e1.v[2], e2.v[1], e2.v[3]);
        estuaries.AddQuad(e1.v[2], e1.v[3], e2.v[3], e2.v[4]);

        estuaries.AddQuadUV(
            new Vector2(0f, 1f), new Vector2(0f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 0f)
        );
        estuaries.AddTriangleUV(
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(1f, 1f)
        );
        estuaries.AddQuadUV(
            new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f)
        );

        if (incomingRiver)
        {
            estuaries.AddQuadUV2(
                new Vector2(1.5f, 1f), new Vector2(0.7f, 1.15f),
                new Vector2(1f, 0.8f), new Vector2(0.5f, 1.1f)
            );
            estuaries.AddTriangleUV2(
                new Vector2(0.5f, 1.1f),
                new Vector2(1f, 0.8f),
                new Vector2(0f, 0.8f)
            );
            estuaries.AddQuadUV2(
                new Vector2(0.5f, 1.1f), new Vector2(0.3f, 1.15f),
                new Vector2(0f, 0.8f), new Vector2(-0.5f, 1f)
            );
        }
        else
        {
            estuaries.AddQuadUV2(
                new Vector2(-0.5f, -0.2f), new Vector2(0.3f, -0.35f),
                new Vector2(0f, 0f), new Vector2(0.5f, -0.3f)
            );
            estuaries.AddTriangleUV2(
                new Vector2(0.5f, -0.3f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f)
            );
            estuaries.AddQuadUV2(
                new Vector2(0.5f, -0.3f), new Vector2(0.7f, -0.35f),
                new Vector2(1f, 0f), new Vector2(1.5f, -0.2f)
            );
        }
    }

    private void TriangulateWithoutRiver(
        HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e
    )
    {
        TriangulateEdgeFan(center, e, cell.Color);

        if (cell.HasRoads && cell.CanHaveRoads)
        {
            var interpolators = GetRoadInterpolators(direction, cell);
            TriangulateRoad(
                cell, center,
                Vector3.Lerp(center, e.v[0], interpolators.x),
                Vector3.Lerp(center, e.v[4], interpolators.y),
                e, cell.HasRoadThroughEdge(direction)
            );
        }
    }

    private Vector2 GetRoadInterpolators(HexDirection direction, HexCell cell)
    {
        Vector2 interpolators;
        if (cell.HasRoadThroughEdge(direction))
        {
            interpolators.x = interpolators.y = 0.5f;
        }
        else
        {
            interpolators.x =
                cell.HasRoadThroughEdge(direction.Previous()) ? 0.5f : 0.25f;
            interpolators.y =
                cell.HasRoadThroughEdge(direction.Next()) ? 0.5f : 0.25f;
        }

        return interpolators;
    }

    private void TriangulateAdjacentToRiver(
        HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e
    )
    {
        if (!cell.HasRiverBeginOrEnd)
        {
            if (cell.HasRiverThroughEdge(direction.Next()))
            {
                if (cell.HasRiverThroughEdge(direction.Previous()))
                    center += HexMetrics.GetSolidEdgeMiddle(direction) *
                              (HexMetrics.InnerToOuter * 0.5f);
                else if (
                    cell.HasRiverThroughEdge(direction.Previous2())
                )
                    center += HexMetrics.GetFirstSolidCorner(direction) * 0.25f;
            }
            else if (
                cell.HasRiverThroughEdge(direction.Previous()) &&
                cell.HasRiverThroughEdge(direction.Next2())
            )
            {
                center += HexMetrics.GetSecondSolidCorner(direction) * 0.25f; 
            }
        }

        var m = new EdgeVertices(
            Vector3.Lerp(center, e.v[0], 0.5f),
            Vector3.Lerp(center, e.v[4], 0.5f)
        );

        TriangulateEdgeStripFlat(m, cell.Color, e, cell.Color, cell, cell.GetNeighbor(direction));
        TriangulateEdgeFan(center, m, cell.Color);

        if (!cell.IsUnderwater && !cell.HasRoadThroughEdge(direction))
            features.AddFeature(cell, (center + e.v[0] + e.v[4]) * (1f / 3f));
    }

    private void TriangulateRoadAdjacentToRiver(
        HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e
    )
    {
        var hasRoadThroughEdge = cell.HasRoadThroughEdge(direction);
        var interpolators = GetRoadInterpolators(direction, cell);
        
        //for every road center, draw segment of road center if connected to a road
        for (var i = 0; i < cell.roadCenters.Count; i++)
        {
            if (!cell.roadCenterHasRoads[i]) continue;
            
            var roadCenter = cell.roadCenters[i];
            var mL = Vector3.Lerp(roadCenter, e.v[0], interpolators.x);
            var mR = Vector3.Lerp(roadCenter, e.v[4], interpolators.y);
            var leadsToRoadCenter = cell.roadCenterPointers[(int) direction] == i;
            TriangulateRoad(cell, roadCenter, mL, mR, e, hasRoadThroughEdge && leadsToRoadCenter);
        }
    }

    private void TriangulateWithRiverBeginOrEnd(
        HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e
    )
    {
        var m = new EdgeVertices(
            Vector3.Lerp(center, e.v[0], 0.5f),
            Vector3.Lerp(center, e.v[4], 0.5f)
        );
        m.v[2].y = e.v[2].y;

        TriangulateEdgeStripFlat(m, cell.Color, e, cell.Color, cell, cell.GetNeighbor(direction));
        TriangulateEdgeFan(center, m, cell.Color);

        if (cell.IsUnderwater) return;

        var reversed = cell.HasIncomingRiver();
        TriangulateRiverQuad(
            m.v[1], m.v[3], e.v[1], e.v[3], cell.RiverSurfaceY, 0.6f, reversed
        );
        center.y = m.v[1].y = m.v[3].y = cell.RiverSurfaceY;
        rivers.AddTriangle(center, m.v[1], m.v[3]);
        if (reversed)
            rivers.AddTriangleUV(
                new Vector2(0.5f, 0.4f),
                new Vector2(1f, 0.2f), new Vector2(0f, 0.2f)
            );
        else
            rivers.AddTriangleUV(
                new Vector2(0.5f, 0.4f),
                new Vector2(0f, 0.6f), new Vector2(1f, 0.6f)
            );
    }

    private void TriangulateWithRiver(
        HexDirection direction, HexCell cell, Vector3 center, EdgeVertices e
    )
    {
        Vector3 centerL, centerR;
        
        /*if (cell.HasRiverThroughEdge(direction.Previous()))
        {
            centerL = Vector3.Lerp(center, e.v[0], 2f / 3f);
        }
        else if (cell.HasRiverThroughEdge(direction.Previous2()))
        {
            centerL = center +
                      HexMetrics.GetSolidEdgeMiddle(direction.Previous()) *
                      (0.5f * HexMetrics.InnerToOuter);
        } 
        else if (cell.HasRiverThroughEdge(direction.Opposite()))
        {
            centerL = center + HexMetrics.GetFirstSolidCorner(direction.Previous()) * 0.25f;
        }

        if (cell.HasRiverThroughEdge(direction.Next()))
        {
            centerR = Vector3.Lerp(center, e.v[4], 2f / 3f);
        }
        else if (cell.HasRiverThroughEdge(direction.Next2()))
        {
            centerR = center +
                      HexMetrics.GetSolidEdgeMiddle(direction.Next()) *
                      (0.5f * HexMetrics.InnerToOuter);
        }
        else if (cell.HasRiverThroughEdge(direction.Opposite()))
        {
            centerR = center + HexMetrics.GetSecondSolidCorner(direction.Next()) * 0.25f;
        }*/

        var step = 0;
        for (var dir = direction.Previous();; dir = dir.Previous())
        {
            step -= 1;
            if (!cell.HasRiverThroughEdge(dir)) continue;
            centerL = HexMetrics.GetRiverCorner(center, e.v[0], e.v[4], direction, step);
            break;
        }
        
        step = 0;
        for (var dir = direction.Next();; dir = dir.Next())
        {
            step += 1;
            if (!cell.HasRiverThroughEdge(dir)) continue;
            centerR = HexMetrics.GetRiverCorner(center, e.v[0], e.v[4], direction, step);
            break;
        }
        //Debug.Log((centerL - centerR).magnitude);

        //centerM = Vector3.Lerp(centerL, centerR, 0.5f);
        var centerM = cell.riverCenter;

        var m = new EdgeVertices(
            Vector3.Lerp(centerL, e.v[0], 0.5f),
            Vector3.Lerp(centerR, e.v[4], 0.5f),
            1f / 6f
        );
        m.v[2].y = centerM.y = e.v[2].y;

        TriangulateEdgeStripFlat(m, cell.Color, e, cell.Color, cell, cell.GetNeighbor(direction));

        terrain.AddTriangle(centerL, m.v[0], m.v[1]);
        terrain.AddTriangleColor(cell.Color);
        terrain.AddQuad(centerL, centerM, m.v[1], m.v[2]);
        terrain.AddQuadColor(cell.Color);
        terrain.AddQuad(centerM, centerR, m.v[2], m.v[3]);
        terrain.AddQuadColor(cell.Color);
        terrain.AddTriangle(centerR, m.v[3], m.v[4]);
        terrain.AddTriangleColor(cell.Color);

        if (cell.IsUnderwater) return;
        
        if (cell.NumberOfRivers > 2)
        {
            rivers.AddTriangle(
                new Vector3(centerM.x, cell.RiverSurfaceY, centerM.z), 
                new Vector3(centerL.x, cell.RiverSurfaceY, centerL.z),
                new Vector3(centerR.x, cell.RiverSurfaceY, centerR.z));
            
            if (cell.HasIncomingRiver(direction))
                rivers.AddTriangleUV(
                    new Vector2(0.5f, 0.4f),
                    new Vector2(1f, 0.2f), new Vector2(0f, 0.2f)
                );
            else
                rivers.AddTriangleUV(
                    new Vector2(0.5f, 0.4f),
                    new Vector2(0f, 0.6f), new Vector2(1f, 0.6f)
                );
        }
            
        var reversed = cell.HasIncomingRiver(direction);
        TriangulateRiverQuad(
            centerL, centerR, m.v[1], m.v[3], cell.RiverSurfaceY, 0.4f, reversed
        );
        TriangulateRiverQuad(
            m.v[1], m.v[3], e.v[1], e.v[3], cell.RiverSurfaceY, 0.6f, reversed
        );
    }

    private void TriangulateConnection(
        HexDirection direction, HexCell cell, EdgeVertices e1
    )
    {
        var neighbor = cell.GetNeighbor(direction);
        if (neighbor == null) return;

        var bridge = HexMetrics.GetBridge(direction);
        bridge.y = neighbor.Position.y - cell.Position.y;
        var e2 = new EdgeVertices(
            e1.v[0] + bridge,
            e1.v[4] + bridge
        );

        var hasRiver = cell.HasRiverThroughEdge(direction);
        var hasRoad = cell.HasRoadThroughEdge(direction) && (cell.CanHaveRoads || neighbor.CanHaveRoads);

        if (hasRiver)
        {
            e2.v[2].y = neighbor.StreamBedY;

            if (!cell.IsUnderwater)
            {
                if (!neighbor.IsUnderwater)
                    TriangulateRiverQuad(
                        e1.v[1], e1.v[3], e2.v[1], e2.v[3],
                        cell.RiverSurfaceY, neighbor.RiverSurfaceY, 0.8f,
                        cell.HasIncomingRiver() && cell.HasIncomingRiver(direction) //cell.IncomingRiver == direction
                    );
                else if (cell.Elevation > neighbor.WaterLevel)
                    TriangulateWaterfallInWater(
                        e1.v[1], e1.v[3], e2.v[1], e2.v[3],
                        cell.RiverSurfaceY, neighbor.RiverSurfaceY,
                        neighbor.WaterSurfaceY
                    );
            }
            else if (
                !neighbor.IsUnderwater &&
                neighbor.Elevation > cell.WaterLevel
            )
            {
                TriangulateWaterfallInWater(
                    e2.v[3], e2.v[1], e1.v[3], e1.v[1],
                    neighbor.RiverSurfaceY, cell.RiverSurfaceY,
                    cell.WaterSurfaceY
                );
            }
        }

        var nextNeighbor = cell.GetNeighbor(direction.Next());
        var nextNeighborExists = nextNeighbor != null;

        var edgeType = cell.GetEdgeType(direction);
        if (edgeType == HexEdgeType.Slope)
            TriangulateEdgeTerraces(e1, cell, e2, neighbor, hasRoad);
        else if (edgeType == HexEdgeType.Cliff)
        {
            var prevNeighbor = cell.GetNeighbor(direction.Previous());

            var isLeftCliff = prevNeighbor == null || (cell.GetEdgeType(prevNeighbor) == HexEdgeType.Cliff ||
                                                       neighbor.GetEdgeType(prevNeighbor) == HexEdgeType.Cliff);
            var isRightCliff = !nextNeighborExists || (cell.GetEdgeType(nextNeighbor) == HexEdgeType.Cliff ||
                                                       neighbor.GetEdgeType(nextNeighbor) == HexEdgeType.Cliff);

            TriangulateEdgeStripCliff(e1, cell.Color, e2, neighbor.Color, cell, neighbor,
                isLeftCliff, isRightCliff, HexMetrics.cliffColor, hasRoad);
        }
        else
            TriangulateEdgeStripFlat(e1, cell.Color, e2, neighbor.Color, cell, neighbor, hasRoad);

        features.AddWall(e1, cell, e2, neighbor, hasRiver, hasRoad);

        if (direction <= HexDirection.E && nextNeighborExists)
        {
            var v4 = e1.v[4] + HexMetrics.GetBridge(direction.Next());
            v4.y = nextNeighbor.Position.y;

            if (cell.GetEdgeType(neighbor) == HexEdgeType.Cliff || cell.GetEdgeType(nextNeighbor) == HexEdgeType.Cliff)
            {
                TriangulateCliffCorner(
                    e2.v[4], neighbor, e1.v[4], cell, v4, nextNeighbor
                );
            }
            else if (cell.Elevation <= neighbor.Elevation)
            {
                if (cell.Elevation <= nextNeighbor.Elevation)
                    TriangulateCorner(
                        e1.v[4], cell, e2.v[4], neighbor, v4, nextNeighbor
                    );
                else
                    TriangulateCorner(
                        v4, nextNeighbor, e1.v[4], cell, e2.v[4], neighbor
                    );
            }
            else if (neighbor.Elevation <= nextNeighbor.Elevation)
            {
                TriangulateCorner(
                    e2.v[4], neighbor, v4, nextNeighbor, e1.v[4], cell
                );
            }
            else
            {
                TriangulateCorner(
                    v4, nextNeighbor, e1.v[4], cell, e2.v[4], neighbor
                );
            }
        }
    }

    private void TriangulateWaterfallInWater(
        Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
        float y1, float y2, float waterY
    )
    {
        v1.y = v2.y = y1;
        v3.y = v4.y = y2;
        v1 = HexMetrics.Perturb(v1);
        v2 = HexMetrics.Perturb(v2);
        v3 = HexMetrics.Perturb(v3);
        v4 = HexMetrics.Perturb(v4);
        var t = (waterY - y2) / (y1 - y2);
        v3 = Vector3.Lerp(v3, v1, t);
        v4 = Vector3.Lerp(v4, v2, t);
        rivers.AddQuadUnperturbed(v1, v2, v3, v4);
        rivers.AddQuadUV(0f, 1f, 0.8f, 1f);
    }

    private void TriangulateCliffCorner(
        Vector3 left, HexCell leftCell,
        Vector3 center, HexCell centerCell,
        Vector3 right, HexCell rightCell)
    {
        var leftEdgeType = centerCell.GetEdgeType(leftCell);
        var rightEdgeType = centerCell.GetEdgeType(rightCell);
        var intermittentEdgeType = leftCell.GetEdgeType(rightCell);

        features.AddWall(center, centerCell, left, leftCell, right, rightCell);

        if (rightEdgeType == HexEdgeType.Slope && intermittentEdgeType != HexEdgeType.Cliff)
        {
            TriangulateCornerCliffTerraces(center, centerCell, left, leftCell, right, rightCell);
            return;
        }

        if (leftEdgeType == HexEdgeType.Slope && intermittentEdgeType != HexEdgeType.Cliff)
        {
            TriangulateCornerTerracesCliff(center, centerCell, left, leftCell, right, rightCell);
            return;
        }

        if (leftEdgeType == HexEdgeType.Cliff && rightEdgeType == HexEdgeType.Cliff &&
            intermittentEdgeType == HexEdgeType.Cliff)
        {
            TriangulateCliffOnCliffCorner(left, leftCell.Color, center, centerCell.Color, right, rightCell.Color);
            return;
        }

        Vector3 v1, v2, v3;
        Color c1, c2, c3;

        if (leftEdgeType == HexEdgeType.Cliff && rightEdgeType == HexEdgeType.Cliff)
        {
            v1 = HexMetrics.Perturb(right);
            v2 = HexMetrics.Perturb(left);
            v3 = HexMetrics.Perturb(center);
            c1 = rightCell.Color;
            c2 = leftCell.Color;
            c3 = centerCell.Color;
        }
        else if (leftEdgeType == HexEdgeType.Cliff)
        {
            v1 = HexMetrics.Perturb(center);
            v2 = HexMetrics.Perturb(right);
            v3 = HexMetrics.Perturb(left);
            c1 = centerCell.Color;
            c2 = rightCell.Color;
            c3 = leftCell.Color;
        }
        else
        {
            v1 = HexMetrics.Perturb(left);
            v2 = HexMetrics.Perturb(center);
            v3 = HexMetrics.Perturb(right);
            c1 = leftCell.Color;
            c2 = centerCell.Color;
            c3 = rightCell.Color;
        }

        terrain.AddQuadUnperturbed(
            v1,
            v2,
            v1 + (v3 - v1) * HexMetrics.CliffColorOffset,
            v2 + (v3 - v2) * HexMetrics.CliffColorOffset);
        terrain.AddQuadColor(c1, c2, HexMetrics.cliffColor, HexMetrics.cliffColor);

        terrain.AddQuadUnperturbed(
            v1 + (v3 - v1) * HexMetrics.CliffColorOffset,
            v2 + (v3 - v2) * HexMetrics.CliffColorOffset,
            v3 + (v1 - v3) * HexMetrics.CliffColorOffset,
            v3 + (v2 - v3) * HexMetrics.CliffColorOffset);
        terrain.AddQuadColor(HexMetrics.cliffColor);

        terrain.AddTriangleUnperturbed(
            v3 + (v1 - v3) * HexMetrics.CliffColorOffset,
            v3,
            v3 + (v2 - v3) * HexMetrics.CliffColorOffset);
        terrain.AddTriangleColor(HexMetrics.cliffColor, c3, HexMetrics.cliffColor);
    }

    private void TriangulateCliffOnCliffCorner(
        Vector3 left, Color c1,
        Vector3 center, Color c2,
        Vector3 right, Color c3)
    {
        Vector3 v1 = HexMetrics.Perturb(left),
            v2 = HexMetrics.Perturb(center),
            v3 = HexMetrics.Perturb(right);

        terrain.AddTriangleUnperturbed(
            v1 + (v2 - v1) * HexMetrics.CliffColorOffset,
            v1,
            v1 + (v3 - v1) * HexMetrics.CliffColorOffset);
        terrain.AddTriangleColor(HexMetrics.cliffColor, c1, HexMetrics.cliffColor);

        terrain.AddTriangleUnperturbed(
            v2 + (v3 - v2) * HexMetrics.CliffColorOffset,
            v2,
            v2 + (v1 - v2) * HexMetrics.CliffColorOffset);
        terrain.AddTriangleColor(HexMetrics.cliffColor, c2, HexMetrics.cliffColor);

        terrain.AddTriangleUnperturbed(
            v3 + (v1 - v3) * HexMetrics.CliffColorOffset,
            v3,
            v3 + (v2 - v3) * HexMetrics.CliffColorOffset);
        terrain.AddTriangleColor(HexMetrics.cliffColor, c3, HexMetrics.cliffColor);

        terrain.AddQuadUnperturbed(
            v1 + (v3 - v1) * HexMetrics.CliffColorOffset,
            v1 + (v2 - v1) * HexMetrics.CliffColorOffset,
            v2 + (v3 - v2) * HexMetrics.CliffColorOffset,
            v2 + (v1 - v2) * HexMetrics.CliffColorOffset);
        terrain.AddQuadColor(HexMetrics.cliffColor);

        terrain.AddQuadUnperturbed(
            v1 + (v3 - v1) * HexMetrics.CliffColorOffset,
            v2 + (v3 - v2) * HexMetrics.CliffColorOffset,
            v3 + (v1 - v3) * HexMetrics.CliffColorOffset,
            v3 + (v2 - v3) * HexMetrics.CliffColorOffset);
        terrain.AddQuadColor(HexMetrics.cliffColor);
    }

    private void TriangulateCorner(
        Vector3 bottom, HexCell bottomCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell
    )
    {
        var leftEdgeType = bottomCell.GetEdgeType(leftCell);
        var rightEdgeType = bottomCell.GetEdgeType(rightCell);

        if (leftEdgeType == HexEdgeType.Slope)
        {
            if (rightEdgeType == HexEdgeType.Slope)
                TriangulateCornerTerraces(
                    bottom, bottomCell, left, leftCell, right, rightCell
                );
            else if (rightEdgeType == HexEdgeType.Flat)
                TriangulateCornerTerraces(
                    left, leftCell, right, rightCell, bottom, bottomCell
                );
            else
                TriangulateCornerTerracesCliff(
                    bottom, bottomCell, left, leftCell, right, rightCell
                );
        }
        else if (rightEdgeType == HexEdgeType.Slope)
        {
            if (leftEdgeType == HexEdgeType.Flat)
                TriangulateCornerTerraces(
                    right, rightCell, bottom, bottomCell, left, leftCell
                );
            else
                TriangulateCornerCliffTerraces(
                    bottom, bottomCell, left, leftCell, right, rightCell
                );
        }
        else if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            if (leftCell.Elevation < rightCell.Elevation)
                TriangulateCornerCliffTerraces(
                    right, rightCell, bottom, bottomCell, left, leftCell
                );
            else
                TriangulateCornerTerracesCliff(
                    left, leftCell, right, rightCell, bottom, bottomCell
                );
        }
        else
        {
            terrain.AddTriangle(bottom, left, right);
            terrain.AddTriangleColor(
                bottomCell.Color, leftCell.Color, rightCell.Color
            );
        }

        features.AddWall(bottom, bottomCell, left, leftCell, right, rightCell);
    }

    private void TriangulateEdgeTerraces(
        EdgeVertices begin, HexCell beginCell,
        EdgeVertices end, HexCell endCell, 
        bool hasRoad
    )
    {
        var e2 = EdgeVertices.TerraceLerp(begin, end, 1);
        var c2 = HexMetrics.TerraceLerp(beginCell.Color, endCell.Color, 1);

        TriangulateEdgeStripFlat(begin, beginCell.Color, e2, c2, beginCell, endCell, hasRoad);

        for (var i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            var e1 = e2;
            var c1 = c2;
            e2 = EdgeVertices.TerraceLerp(begin, end, i);
            c2 = HexMetrics.TerraceLerp(beginCell.Color, endCell.Color, i);
            TriangulateEdgeStripFlat(e1, c1, e2, c2, beginCell, endCell, hasRoad);
        }

        TriangulateEdgeStripFlat(e2, c2, end, endCell.Color, beginCell, endCell, hasRoad);
    }

    private void TriangulateCornerTerraces(
        Vector3 begin, HexCell beginCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell
    )
    {
        var v3 = HexMetrics.TerraceLerp(begin, left, 1);
        var v4 = HexMetrics.TerraceLerp(begin, right, 1);
        var c3 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, 1);
        var c4 = HexMetrics.TerraceLerp(beginCell.Color, rightCell.Color, 1);

        terrain.AddTriangle(begin, v3, v4);
        terrain.AddTriangleColor(beginCell.Color, c3, c4);

        for (var i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            var v1 = v3;
            var v2 = v4;
            var c1 = c3;
            var c2 = c4;
            v3 = HexMetrics.TerraceLerp(begin, left, i);
            v4 = HexMetrics.TerraceLerp(begin, right, i);
            c3 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, i);
            c4 = HexMetrics.TerraceLerp(beginCell.Color, rightCell.Color, i);
            terrain.AddQuad(v1, v2, v3, v4);
            terrain.AddQuadColor(c1, c2, c3, c4);
        }

        terrain.AddQuad(v3, v4, left, right);
        terrain.AddQuadColor(c3, c4, leftCell.Color, rightCell.Color);
    }

    private void TriangulateCornerTerracesCliff(
        Vector3 begin, HexCell beginCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell
    )
    {
        var b = 1f / (rightCell.Elevation - beginCell.Elevation);
        if (b < 0) b = -b;
        var boundary = Vector3.Lerp(
            HexMetrics.Perturb(begin), HexMetrics.Perturb(right), b
        );
        var boundaryColor = HexMetrics.cliffColor; //Color.Lerp(beginCell.Color, rightCell.Color, b);

        TriangulateCliffBoundaryTriangle(
            begin, beginCell, left, leftCell, boundary, boundaryColor
        );

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateCliffBoundaryTriangle(
                left, leftCell, right, rightCell, boundary, boundaryColor
            );
        }
        else
        {
            terrain.AddTriangleUnperturbed(
                HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary
            );
            terrain.AddTriangleColor(
                boundaryColor // leftCell.Color, rightCell.Color, boundaryColor
            );
        }
    }

    private void TriangulateCornerCliffTerraces(
        Vector3 begin, HexCell beginCell,
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell
    )
    {
        var b = 1f / (leftCell.Elevation - beginCell.Elevation);
        if (b < 0) b = -b;
        var boundary = Vector3.Lerp(
            HexMetrics.Perturb(begin), HexMetrics.Perturb(left), b
        );
        var boundaryColor = HexMetrics.cliffColor; //Color.Lerp(beginCell.Color, leftCell.Color, b);

        TriangulateCliffBoundaryTriangle(
            right, rightCell, begin, beginCell, boundary, boundaryColor
        );

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateCliffBoundaryTriangle(
                left, leftCell, right, rightCell, boundary, boundaryColor
            );
        }
        else
        {
            terrain.AddTriangleUnperturbed(
                HexMetrics.Perturb(left), HexMetrics.Perturb(right), boundary
            );
            terrain.AddTriangleColor(
                boundaryColor // leftCell.Color, rightCell.Color, boundaryColor
            );
        }
    }

    private void TriangulateCliffBoundaryTriangle(
        Vector3 begin, HexCell beginCell,
        Vector3 left, HexCell leftCell,
        Vector3 boundary, Color boundaryColor
    )
    {
        var v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, 1));
        var c2 = HexMetrics.TerraceLerp(boundaryColor, leftCell.Color, 1);

        terrain.AddTriangleUnperturbed(HexMetrics.Perturb(begin), v2, boundary);
        terrain.AddTriangleColor(beginCell.Color, c2, boundaryColor);

        for (var i = 2; i < HexMetrics.TerraceSteps; i++)
        {
            var v1 = v2;
            var c1 = c2;
            v2 = HexMetrics.Perturb(HexMetrics.TerraceLerp(begin, left, i));
            c2 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, i);
            terrain.AddTriangleUnperturbed(v1, v2, boundary);
            terrain.AddTriangleColor(c1, c2, boundaryColor);
        }

        terrain.AddTriangleUnperturbed(v2, HexMetrics.Perturb(left), boundary);
        terrain.AddTriangleColor(c2, leftCell.Color, boundaryColor);
    }

    private void TriangulateEdgeFan(Vector3 center, EdgeVertices edge, Color color)
    {
        for (var i = 1; i < edge.v.Length; i++)
        {
            terrain.AddTriangle(center, edge.v[i - 1], edge.v[i]);
            terrain.AddTriangleColor(color);
        }
    }

    private void TriangulateEdgeStripFlat(
        EdgeVertices e1, Color c1,
        EdgeVertices e2, Color c2,
        HexCell cell, HexCell neighbor,
        bool hasRoad = false
    )
    {
        for (var i = 1; i < e1.v.Length; i++)
        {
            terrain.AddQuad(e1.v[i - 1], e1.v[i], e2.v[i - 1], e2.v[i]);
            terrain.AddQuadColor(c1, c2);
        }
        
        if (hasRoad) TriangulateEdgeStripRoad(e1, e2, cell, neighbor);
        //if (hasRoad) TriangulateRoadSegment(e1.v[1], e1.v[2], e1.v[3], e2.v[1], e2.v[2], e2.v[3]);
    }

    private void TriangulateEdgeStripCliff(
        EdgeVertices e1, Color c1,
        EdgeVertices e2, Color c2,
        HexCell cell, HexCell neighbor,
        bool isLeftCliff, bool isRightCliff, Color cliffColor,
        bool hasRoad = false
    )
    {
        for (var i = 1; i < e1.v.Length; i++)
        {
            Vector3 v1I = HexMetrics.Perturb(e1.v[i - 1]),
                v1J = HexMetrics.Perturb(e1.v[i]),
                v2I = HexMetrics.Perturb(e2.v[i - 1]),
                v2J = HexMetrics.Perturb(e2.v[i]);
            var leftColorOffset = i == 1 && !isLeftCliff ? 0.5f : HexMetrics.CliffColorOffset;
            var rightColorOffset = i == e1.v.Length - 1 && !isRightCliff ? 0.5f : HexMetrics.CliffColorOffset;

            terrain.AddQuadUnperturbed(
                v1I,
                v1J,
                v1I + (v2I - v1I) * leftColorOffset,
                v1J + (v2J - v1J) * rightColorOffset);
            terrain.AddQuadColor(c1, cliffColor);

            terrain.AddQuadUnperturbed(
                v1I + (v2I - v1I) * leftColorOffset,
                v1J + (v2J - v1J) * rightColorOffset,
                v2I + (v1I - v2I) * leftColorOffset,
                v2J + (v1J - v2J) * rightColorOffset);
            terrain.AddQuadColor(cliffColor);

            terrain.AddQuadUnperturbed(
                v2I + (v1I - v2I) * leftColorOffset,
                v2J + (v1J - v2J) * rightColorOffset,
                v2I,
                v2J);
            terrain.AddQuadColor(cliffColor, c2);
        }

        if (hasRoad) TriangulateEdgeStripRoad(e1, e2, cell, neighbor);
    }

    private void TriangulateEdgeStripRoad(EdgeVertices e1, EdgeVertices e2, HexCell cell, HexCell neighbor)
    {
        if (!cell.CanHaveRoads)
            TriangulateEndRoadStrip(e1.v[1], e1.v[2], e1.v[3], e2.v[1], e2.v[2], e2.v[3]);
        else if (!neighbor.CanHaveRoads)
            TriangulateEndRoadStrip(e2.v[3], e2.v[2], e2.v[1], e1.v[3], e1.v[2], e1.v[1]);
        else
            TriangulateRoadSegment(e1.v[1], e1.v[2], e1.v[3], e2.v[1], e2.v[2], e2.v[3]);
    }

    private void TriangulateRiverQuad(
        Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
        float y, float v, bool reversed
    )
    {
        TriangulateRiverQuad(v1, v2, v3, v4, y, y, v, reversed);
    }

    private void TriangulateRiverQuad(
        Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4,
        float y1, float y2, float v, bool reversed
    )
    {
        v1.y = v2.y = y1;
        v3.y = v4.y = y2;
        rivers.AddQuad(v1, v2, v3, v4);
        if (reversed)
            rivers.AddQuadUV(1f, 0f, 0.8f - v, 0.6f - v);
        else
            rivers.AddQuadUV(0f, 1f, v, v + 0.2f);
    }

    private void TriangulateRoad(
        HexCell cell,
        Vector3 center, Vector3 mL, Vector3 mR,
        EdgeVertices e, bool hasRoadThroughCellEdge
    )
    {
        if (hasRoadThroughCellEdge)
        {
            var mC = Vector3.Lerp(mL, mR, 0.5f);
            TriangulateRoadSegment(mL, mC, mR, e.v[1], e.v[2], e.v[3]);
            roads.AddTriangle(center, mL, mC);
            roads.AddTriangle(center, mC, mR);
            roads.AddTriangleUV(
                new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(1f, 0f)
            );
            roads.AddTriangleUV(
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f)
            );
        }
        else
        {
            TriangulateRoadEnd(center, mL, mR);
        }
    }

    private void TriangulateRoadEnd(Vector3 center, Vector3 mL, Vector3 mR)
    {
        roads.AddTriangle(center, mL, mR);
        roads.AddTriangleUV(
            new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f)
        );
    }

    private void TriangulateRoadSegment(
        Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 v4, Vector3 v5, Vector3 v6
    )
    {
        roads.AddQuad(v1, v2, v4, v5);
        roads.AddQuad(v2, v3, v5, v6);
        roads.AddQuadUV(0f, 1f, 0f, 0f);
        roads.AddQuadUV(1f, 0f, 0f, 0f);
    }

    private void TriangulateEndRoadStrip(
        Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 v4, Vector3 v5, Vector3 v6
    )
    {
        roads.AddQuad(v2, v5, v1, v4);
        roads.AddQuad(v5, v2, v6, v3);
        roads.AddQuadUV(
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f)
        );
        roads.AddQuadUV(
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f)
        );
    }
}