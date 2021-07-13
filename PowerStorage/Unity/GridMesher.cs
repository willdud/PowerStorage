using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using HarmonyLib;
using PowerStorage.Supporting;
using TriangleNet.Geometry;
using TriangleNet.IO;
using TriangleNet.Meshing;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

namespace PowerStorage.Unity
{
    public class GridMesher : MonoBehaviour
    {
        public static bool InProgress;
        private int _min = -5000;
        private int _max = 5000;
        private int _y = 200;

        public void BeginAdding()
        {
            if (!InProgress)
            {
                InProgress = true;
                MakeMeshes();
            }
        }
        
        public void MakeMeshes()
        {
            var watch = PowerStorageProfiler.Start("Make Meshes");
            var emCells = Traverse.Create(Singleton<ElectricityManager>.instance).Field("m_electricityGrid").GetValue() as ElectricityManager.Cell[] ?? new ElectricityManager.Cell[0];
            var emCellCopy = new ElectricityManager.Cell[emCells.Length];
            Array.Copy(emCells, emCellCopy, emCells.Length);
            
            var indexesUsed = new List<int>();
            var pointsForMeshes = new Dictionary<List<Vector2>, List<Vector2>>();
            
            for (float y = _min; y <= _max; y = y + 38.250f)
            for (float x = _min; x <= _max; x = x + 38.250f)
            {
                var newIndex = GetNewIndex(x, y);

                if(indexesUsed.Contains(newIndex))
                    continue;

                indexesUsed.Add(newIndex);

                if(newIndex > emCellCopy.Length)
                    continue;

                var cell = emCellCopy[newIndex];
                if (IsConductive(cell, x, y))
                {
                    var perimeterPoints = new List<Vector2>();
                    var thisMesh = FollowNeighbours(x, y, ref indexesUsed, ref emCellCopy, ref perimeterPoints);
                    thisMesh.Add(new Vector2(x, y));
                    pointsForMeshes.Add(thisMesh, perimeterPoints);
                }
            }
            
            PowerStorageLogger.Log($"Mesher, meshes: {pointsForMeshes.Count}.", PowerStorageMessageType.Saving);

            var iterator = 0;
            foreach (var pointsForMesh in pointsForMeshes)
            {
                var polygon = new Polygon();
                PowerStorageLogger.LogWarning($"Mesher, pre-triangulate: {pointsForMesh.Key.Count}.", PowerStorageMessageType.Saving);
                foreach (var vector2 in pointsForMesh.Key)
                {
                    PowerStorageLogger.Log($"point: ({vector2.x}, {vector2.y}).", PowerStorageMessageType.Saving);
                    //polygon.Add(new Vertex(vector2.x, vector2.y));
                }
                
                PowerStorageLogger.Log($"Mesher, perimiter: {pointsForMesh.Value.Count}.", PowerStorageMessageType.Saving);
                var addedSegments = new List<Segment>();
                foreach (var vector2A in pointsForMesh.Value)
                foreach (var vector2B in pointsForMesh.Value)
                {
                    if (vector2A == vector2B)
                    {
                        continue;
                    }

                    if (Math.Abs(vector2A.x - vector2B.x) < 1 && Math.Abs(vector2A.y - (vector2B.y + 38.250)) < 1)
                    {
                        polygon.Add(new Segment(new Vertex(vector2A.x, vector2A.y), new Vertex(vector2B.x, vector2B.y)), true);
                    }
                    else if (Math.Abs(vector2A.y - vector2B.y) < 1 && Math.Abs(vector2A.x - (vector2B.x + 38.250)) < 1)
                    {
                        polygon.Add(new Segment(new Vertex(vector2A.x, vector2A.y), new Vertex(vector2B.x, vector2B.y)), true);
                    }
                    else if (Math.Abs(vector2A.x - (vector2B.x + 38.250)) < 1 && Math.Abs(vector2A.y - (vector2B.y + 38.250)) < 1)
                    {
                        polygon.Add(new Segment(new Vertex(vector2A.x, vector2A.y), new Vertex(vector2B.x, vector2B.y)), true);
                    }
                    else if (Math.Abs(vector2A.x - (vector2B.x - 38.250)) < 1 && Math.Abs(vector2A.y - (vector2B.y + 38.250)) < 1)
                    {
                        polygon.Add(new Segment(new Vertex(vector2A.x, vector2A.y), new Vertex(vector2B.x, vector2B.y)), true);
                    }
                    else if (Math.Abs(vector2A.x - (vector2B.x - 38.250)) < 1 && Math.Abs(vector2A.y - vector2B.y) < 1)
                    {
                        polygon.Add(new Segment(new Vertex(vector2A.x, vector2A.y), new Vertex(vector2B.x, vector2B.y)), true);
                    }
                    else if (Math.Abs(vector2A.x - (vector2B.x - 38.250)) < 1 && Math.Abs(vector2A.y - (vector2B.y - 38.250)) < 1)
                    {
                        polygon.Add(new Segment(new Vertex(vector2A.x, vector2A.y), new Vertex(vector2B.x, vector2B.y)), true);
                    }
                    else if (Math.Abs(vector2A.x - vector2B.x) < 1 && Math.Abs(vector2A.y - (vector2B.y - 38.250)) < 1)
                    {
                        polygon.Add(new Segment(new Vertex(vector2A.x, vector2A.y), new Vertex(vector2B.x, vector2B.y)), true);
                    }
                    else if (Math.Abs(vector2A.x - (vector2B.x + 38.250)) < 1 && Math.Abs(vector2A.y - (vector2B.y - 38.250)) < 1)
                    {
                        polygon.Add(new Segment(new Vertex(vector2A.x, vector2A.y), new Vertex(vector2B.x, vector2B.y)), true);
                    }
                }

                var triangulator = new GenericMesher();

                int[] triangles;
                Vector3[] vertices;
                if (pointsForMesh.Key.Count < 3)
                {
                    continue;
                }
                if (pointsForMesh.Key.Count == 3)
                {
                    vertices = pointsForMesh.Key.Select(v => new Vector3(v.x, _y, v.y)).ToArray();
                    triangles = new[] { 2, 1, 0 };
                }
                else
                {
                    try
                    {
                        var tMesh = triangulator.Triangulate(polygon, new ConstraintOptions {Convex = false});
                        FileProcessor.Write(tMesh, "C:\\temp\\poly"+iterator+".poly");
                        vertices = tMesh.Vertices.Select(c => new Vector3((int) c.X, _y, (int) c.Y)).ToArray();
                        triangles = tMesh.Triangles.SelectMany(t =>
                        {
                            var vertIndexList = new List<int>();
                            for (var i = 0; i <= 2; i++)
                            {
                                var vert = t.GetVertex(i);
                                vertIndexList.Add(Array.IndexOf(tMesh.Vertices.ToArray(), vert));
                            }

                            return vertIndexList;
                        }).ToArray();
                    }
                    catch (Exception ex)
                    {
                        PowerStorageLogger.LogError("---Mesher Error---", PowerStorageMessageType.Saving);
                        PowerStorageLogger.LogError(ex.Message, PowerStorageMessageType.Saving);
                        PowerStorageLogger.LogError(ex.StackTrace, PowerStorageMessageType.Saving);
                        continue;
                    }
                }
                
                var mesh = new Mesh();
                mesh.SetVertices(vertices.ToList());
                mesh.SetTriangles(triangles.Reverse().ToArray(), 0);

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var gridMesh = new GameObject("PowerStorageGridMeshObj"+ ++iterator);
                gridMesh.AddComponent<GridMesh>();
                var mFilter = gridMesh.GetComponent<MeshFilter>();
                mFilter.mesh = mesh;
                gridMesh.transform.parent = gameObject.transform;
            }
            
            PowerStorageProfiler.Stop("Make Meshes", watch);
        }

        private static bool IsConductive(ElectricityManager.Cell cell, float x, float y)
        {
            return cell.m_conductivity >= 64 && x != 0 && (y != 0 && (int)x != byte.MaxValue) && (int)y != byte.MaxValue;
        }

        private static int GetNewIndex(double x, double y)
        {
            var num = Mathf.Clamp((int) (x / 38.25 + 128.0), 0, byte.MaxValue);
            var newIndex = Mathf.Clamp((int) (y / 38.25 + 128.0), 0, byte.MaxValue) * 256 + num;
            return newIndex;
        }

        private List<Vector2> FollowNeighbours(float x, float y, ref List<int> visitedIndexes, ref ElectricityManager.Cell[] cells, ref List<Vector2> perimeterPoints)
        {
            var connectedGroup = new List<Vector2>();
            var ux = x + 38.250f;
            var dx = x - 38.250f;
            var ly = y + 38.250f;
            var ry = y - 38.250f;

            var neighbours = 0;

            //check up
            var upIndex = GetNewIndex(ux, y);
            if (!visitedIndexes.Contains(upIndex) && IsConductive(cells[upIndex], ux, y))
            {
                neighbours++;
                connectedGroup.Add(new Vector2(ux, y));
                visitedIndexes.Add(upIndex);
                connectedGroup.AddRange(FollowNeighbours(ux, y, ref visitedIndexes, ref cells, ref perimeterPoints));
            }

            // check up left
            var upLeftIndex = GetNewIndex(ux, ly);
            if (!visitedIndexes.Contains(upLeftIndex) &&IsConductive(cells[upLeftIndex], ux, ly))
            {
                neighbours++;
                connectedGroup.Add(new Vector2(ux, ly));
                visitedIndexes.Add(upLeftIndex);
                connectedGroup.AddRange(FollowNeighbours(ux, ly, ref visitedIndexes, ref cells, ref perimeterPoints));
            }
            
            //check left
            var leftIndex = GetNewIndex(x, ly);
            if (!visitedIndexes.Contains(leftIndex) &&IsConductive(cells[leftIndex], x, ly))
            {
                neighbours++;
                connectedGroup.Add(new Vector2(x, ly));
                visitedIndexes.Add(leftIndex);
                connectedGroup.AddRange(FollowNeighbours(x, ly, ref visitedIndexes, ref cells, ref perimeterPoints));
            }

            //check down left
            var downLeftIndex = GetNewIndex(dx, ly);
            if (!visitedIndexes.Contains(downLeftIndex) &&IsConductive(cells[downLeftIndex], dx, ly))
            {
                neighbours++;
                connectedGroup.Add(new Vector2(dx, ly));
                visitedIndexes.Add(downLeftIndex);
                connectedGroup.AddRange(FollowNeighbours(dx, ly, ref visitedIndexes, ref cells, ref perimeterPoints));
            }

            //check down
            var downIndex = GetNewIndex(dx, y);
            if (!visitedIndexes.Contains(downIndex) &&IsConductive(cells[downIndex], dx, y))
            {
                neighbours++;
                connectedGroup.Add(new Vector2(dx, y));
                visitedIndexes.Add(downIndex);
                connectedGroup.AddRange(FollowNeighbours(dx, y, ref visitedIndexes, ref cells, ref perimeterPoints));
            }

            //check down right
            var downRightIndex = GetNewIndex(dx, ry);
            if (!visitedIndexes.Contains(downRightIndex) &&IsConductive(cells[downRightIndex], dx, ry))
            {
                neighbours++;
                connectedGroup.Add(new Vector2(dx, ry));
                visitedIndexes.Add(downRightIndex);
                connectedGroup.AddRange(FollowNeighbours(dx, ry, ref visitedIndexes, ref cells, ref perimeterPoints));
            }

            //check right
            var rightIndex = GetNewIndex(x, ry);
            if (!visitedIndexes.Contains(rightIndex) &&IsConductive(cells[rightIndex], x, ry))
            {
                neighbours++;
                connectedGroup.Add(new Vector2(x, ry));
                visitedIndexes.Add(rightIndex);
                connectedGroup.AddRange(FollowNeighbours(x, ry, ref visitedIndexes, ref cells, ref perimeterPoints));
            }

            // check up right
            var upRightIndex = GetNewIndex(ux, ry);
            if (!visitedIndexes.Contains(upRightIndex) &&IsConductive(cells[upRightIndex], ux, ry))
            {
                neighbours++;
                connectedGroup.Add(new Vector2(ux, ry));
                visitedIndexes.Add(upRightIndex);
                connectedGroup.AddRange(FollowNeighbours(ux, ry, ref visitedIndexes, ref cells, ref perimeterPoints));
            }

            if (neighbours != 8)
            {
                perimeterPoints.Add(new Vector2(x, y));
            }

            return connectedGroup;
        }
    }
}
