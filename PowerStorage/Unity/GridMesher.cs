using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using HarmonyLib;
using PowerStorage.Supporting;
using TriangleNet.Geometry;
using TriangleNet.IO;
using TriangleNet.Meshing;
using TriangleNet.Voronoi;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

namespace PowerStorage.Unity
{
    public class GridMesher : MonoBehaviour
    {
        public static bool InProgress;
        private static int _min = -5000;
        private static int _max = 5000;
        private static int _y = 200;
        
        public void BeginAdding()
        {
            if (!InProgress)
            {
                InProgress = true;
                StartCoroutine(MakeMeshes());
            }
        }
        
        public IEnumerator MakeMeshes()
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
            
            yield return new WaitForFixedUpdate();
            PowerStorageLogger.Log($"Mesher, meshes: {pointsForMeshes.Count}.", PowerStorageMessageType.Saving);

            var iterator = 0;
            foreach (var pointsForMesh in pointsForMeshes)
            {
                var subMeshes = new List<Mesh>();
                var polygon = new Polygon();

                PowerStorageLogger.LogWarning($"Mesher, pre-triangulate points: {pointsForMesh.Key.Count}.", PowerStorageMessageType.Saving);
                PowerStorageLogger.Log($"Mesher, pre-triangulate perimiter: {pointsForMesh.Value.Count}.", PowerStorageMessageType.Saving);

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
                
                yield return new WaitForFixedUpdate();

                var triangulator = new GenericMesher();
                if (pointsForMesh.Key.Count < 3)
                {
                    continue;
                }
                if (pointsForMesh.Key.Count == 3)
                {
                    var faceMesh = new Mesh();
                    faceMesh.SetVertices(pointsForMesh.Key.Select(v => new Vector3(v.x, _y, v.y)).ToList());
                    faceMesh.SetTriangles(new[] { 2, 1, 0 }.ToArray(), 0);
                    faceMesh.RecalculateNormals();
                    faceMesh.RecalculateBounds();
                    subMeshes.Add(faceMesh);
                }
                else
                {
                    try
                    {
                        var tMesh = triangulator.Triangulate(polygon, new ConstraintOptions { Convex = false }) as TriangleNet.Mesh;
                        FileProcessor.Write(tMesh, "C:\\temp\\poly"+iterator+".poly");
                        
                        var voronoi = new BoundedVoronoi(tMesh);
                        PowerStorageLogger.Log($"Mesher, voronoi faces:{voronoi.Faces.Count} verts:{voronoi.Vertices.Count}", PowerStorageMessageType.Saving);
                        foreach (var face in voronoi.Faces)
                        {
                            if (face == null)
                                continue;

                            var facePoly = new Polygon();
                            foreach (var edge in face.EnumerateEdges())
                            {
                                if (edge == null)
                                    continue;
                                facePoly.Add(new Vertex(edge.Origin.X, edge.Origin.Y));
                            }

                            if (facePoly.Points.Count < 3)
                                continue;

                            var faceTriMesh = triangulator.Triangulate(facePoly);
                            var faceVerts = faceTriMesh.Vertices.Select(c => new Vector3((int)c.X, _y, (int)c.Y)).ToArray();
                            var faceTris = faceTriMesh.Triangles.SelectMany(t =>
                            {
                                var vertIndexList = new List<int>();
                                for (var i = 0; i <= 2; i++)
                                {
                                    var vert = t.GetVertex(i);
                                    vertIndexList.Add(Array.IndexOf(faceTriMesh.Vertices.ToArray(), vert));
                                }

                                return vertIndexList;
                            }).ToArray();

                            var faceMesh = new Mesh();
                            faceMesh.SetVertices(faceVerts.ToList());
                            faceMesh.SetTriangles(faceTris.ToArray().Reverse().ToArray(), 0);
                            faceMesh.RecalculateNormals();
                            faceMesh.RecalculateBounds();
                            subMeshes.Add(faceMesh);
                        }
                    }
                    catch (Exception ex)
                    {
                        PowerStorageLogger.LogError("---Mesher Error---", PowerStorageMessageType.Saving);
                        PowerStorageLogger.LogError(ex.Message, PowerStorageMessageType.Saving);
                        PowerStorageLogger.LogError(ex.StackTrace, PowerStorageMessageType.Saving);
                        continue;
                    }
                }
                
                yield return new WaitForFixedUpdate();

                var gridMesh = new GameObject("PowerStorageGridMeshObj"+ ++iterator);
                gridMesh.AddComponent<GridMesh>();
                gridMesh.AddComponent<CollisionList>(); 
                var mFilter = gridMesh.GetComponent<MeshFilter>();
                mFilter.mesh = subMeshes.First();
                gridMesh.transform.parent = gameObject.transform;

                foreach (var mesh in subMeshes.Skip(1))
                {
                    var childGridMesh = new GameObject("PowerStorageChildGridMeshObj"+ mesh.GetHashCode());
                    childGridMesh.transform.parent = gridMesh.transform;
                    childGridMesh.AddComponent<CollisionList>();
                    var filter = childGridMesh.AddComponent<MeshFilter>();
                    filter.mesh = mesh;
                    var rigid = childGridMesh.GetComponent<Rigidbody>();
                    rigid.isKinematic = true;
                    var collider = childGridMesh.AddComponent<MeshCollider>();
                    collider.convex = true;
                    collider.isTrigger = true;
                    collider.enabled = true;
                    childGridMesh.AddComponent<MeshRenderer>();

                    yield return new WaitForFixedUpdate();
                }

                var meshCollider = gridMesh.GetComponent<MeshCollider>();
                meshCollider.enabled = true;
                meshCollider.convex = true;
                meshCollider.isTrigger = true;
            }
            
            GridsBuildingsRollup.GridMeshed = true;

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

        private static List<Vector2> FollowNeighbours(float x, float y, ref List<int> visitedIndexes, ref ElectricityManager.Cell[] cells, ref List<Vector2> perimeterPoints)
        {
            var connectedGroup = new List<Vector2>();
            var ux = x + 38.250f;
            var dx = x - 38.250f;
            var ly = y + 38.250f;
            var ry = y - 38.250f;

            var neighbours = 0;

            void CheckConnectivityInCell(float ix, float iy, ref List<int> vi, ref ElectricityManager.Cell[] c, ref List<Vector2> pp)
            {
                var upIndex = GetNewIndex(ix, iy);
                if (!IsConductive(c[upIndex], ix, iy)) 
                    return;

                neighbours++;
                if (vi.Contains(upIndex)) 
                    return;

                connectedGroup.Add(new Vector2(ix, iy));
                vi.Add(upIndex);
                connectedGroup.AddRange(FollowNeighbours(ix, iy, ref vi, ref c, ref pp));
            }
            
            //check up
            CheckConnectivityInCell(ux, y, ref visitedIndexes, ref cells, ref perimeterPoints);

            //check up left
            CheckConnectivityInCell(ux, ly, ref visitedIndexes, ref cells, ref perimeterPoints);

            //check left
            CheckConnectivityInCell(x, ly, ref visitedIndexes, ref cells, ref perimeterPoints);

            //check down left
            CheckConnectivityInCell(dx, ly, ref visitedIndexes, ref cells, ref perimeterPoints);

            //check down
            CheckConnectivityInCell(dx, y, ref visitedIndexes, ref cells, ref perimeterPoints);

            //check down right
            CheckConnectivityInCell(dx, ry, ref visitedIndexes, ref cells, ref perimeterPoints);

            //check right
            CheckConnectivityInCell(x, ry, ref visitedIndexes, ref cells, ref perimeterPoints);

            //check up right
            CheckConnectivityInCell(ux, ry, ref visitedIndexes, ref cells, ref perimeterPoints);

            if (neighbours != 8)
            {
                perimeterPoints.Add(new Vector2(x, y));
            }

            return connectedGroup;
        }
    }
}
