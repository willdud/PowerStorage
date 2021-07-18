using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ColossalFramework.Threading;
using HarmonyLib;
using PowerStorage.Geometry.Geometry;
using PowerStorage.Geometry.IO;
using PowerStorage.Geometry.Meshing;
using PowerStorage.Geometry.Voronoi;
using PowerStorage.Model;
using PowerStorage.Supporting;
using UnityEngine;
using Mesh = UnityEngine.Mesh;

namespace PowerStorage.Unity
{
    /// <summary>
    /// Map the electricity manager conductivity grid to poly. May be concave :(
    /// Split poly into faces so they will be convex meshes :) 
    /// Create child gameobjects with collider for each mesh.
    /// Use collisions with buildings (CollisionAdder.cs) to be aware of all buildings that are connected by electricity.
    /// Follow the network of electricity pole buildings to combine grids.
    /// </summary>
    public class GridMesher : MonoBehaviour
    {
        public static bool InProgress;
        private static float _min = -5019.125f;
        private static float _max = 5019.125f;
        private static int _y = 225;
        
        public void BeginAdding()
        {
            if (!InProgress)
            {
                InProgress = true;
                StartCoroutine(MakeMeshes());
            }
        }
        
        // I hate the implementation, but to keep things responsive I need to use a coroutine :(
        public IEnumerator MakeMeshes()
        {
            var watch = PowerStorageProfiler.Start("Make Meshes");

            #region Plot Conductivity
            var emCells = Traverse.Create(Singleton<ElectricityManager>.instance).Field("m_electricityGrid").GetValue() as ElectricityManager.Cell[] ?? new ElectricityManager.Cell[0];
            var emCellCopy = new ElectricityManager.Cell[emCells.Length];
            Array.Copy(emCells, emCellCopy, emCells.Length);
            yield return new WaitForFixedUpdate();

            var pointsForMeshes = new Dictionary<List<Vector2>, List<Vector2>>();
            var taskPlot = new Task<bool>(() =>
            {
                var indexesUsed = new List<int>();
                //TODO: capture holes and add to poly as contours
                var index = 0;
                for (float y = _min; y <= _max; y += 38.250f)
                for (float x = _min; x <= _max; x += 38.250f)
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
            }).Run();
            while (!taskPlot.hasEnded)
                yield return new WaitForFixedUpdate();
            

            PowerStorageProfiler.Lap("Make Meshes: Done with EM", watch);
            PowerStorageLogger.Log($"Mesher, meshes: {pointsForMeshes.Count}.", PowerStorageMessageType.Saving);
            #endregion Plot Conductivity


            #region Meshes from points
            var iterator = 0;
            foreach (var pointsForMesh in pointsForMeshes)
            {
                var subMeshes = new List<Mesh>();
                
                PowerStorageLogger.LogWarning($"Mesher, pre-triangulate points: {pointsForMesh.Key.Count}.", PowerStorageMessageType.Saving);
                PowerStorageLogger.Log($"Mesher, pre-triangulate perimiter: {pointsForMesh.Value.Count}.", PowerStorageMessageType.Saving);

                var polygon = MapPerimeterOfPoly(pointsForMesh);
                
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
                    var tMesh = triangulator.Triangulate(polygon, new ConstraintOptions { Convex = false }) as Geometry.Mesh;
                    FileProcessor.Write(tMesh, "C:\\temp\\poly"+iterator+".poly");
                    
                    var voronoi = new BoundedVoronoi(tMesh);
                    PowerStorageLogger.Log($"Mesher, voronoi faces:{voronoi.Faces.Count} verts:{voronoi.Vertices.Count}", PowerStorageMessageType.Saving);
                    foreach (var face in voronoi.Faces)
                    {
                        if (face == null)
                            continue;

                        try
                        {
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
                        catch (Exception ex)
                        {
                            PowerStorageLogger.LogError("---Mesher Error---", PowerStorageMessageType.Saving);
                            PowerStorageLogger.LogError(ex.Message, PowerStorageMessageType.Saving);
                            PowerStorageLogger.LogError(ex.StackTrace, PowerStorageMessageType.Saving);
                            continue;
                        }
                    }

                    yield return new WaitForFixedUpdate();
                }
                
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
                    var renderer = childGridMesh.AddComponent<MeshRenderer>();
                    renderer.enabled = PowerStorage.DebugRenders;

                    yield return new WaitForFixedUpdate();
                }

                var meshCollider = gridMesh.GetComponent<MeshCollider>();
                meshCollider.enabled = true;
                meshCollider.convex = true;
                meshCollider.isTrigger = true;
            }
            #endregion Meshes from points

            
            #region Map Networks from collisions
            var networks = new List<List<BuildingAndIndex>>(byte.MaxValue);
            var watchMapNetworks = PowerStorageProfiler.Start("MapNetworks");
            PowerStorageLogger.Log($"Mesher Children: {gameObject.transform.childCount}", PowerStorageMessageType.NetworkMapping);
            foreach (var childObj in gameObject.GetAllChildren())
            {
                var childLists = childObj.GetComponent<CollisionList>();
                var networkMembers = childLists.CurrentCollisions.ToList();
                PowerStorageLogger.Log($"Mesher Child Children: {childObj.transform.childCount}", PowerStorageMessageType.NetworkMapping);

                foreach (var doubleChildObj in childObj.GetAllChildren())
                {
                    var doubleChildLists = doubleChildObj.GetComponent<CollisionList>();
                    PowerStorageLogger.Log($"Mesher Double Child Children: {doubleChildObj.transform.childCount}", PowerStorageMessageType.NetworkMapping);
                    networkMembers.AddRange(doubleChildLists.CurrentCollisions);
                    yield return new WaitForFixedUpdate();
                }
                networks.Add(GridsBuildingsRollup.MasterBuildingList.Where(p => networkMembers.Contains(p.GridGameObject)).ToList());
                
                yield return new WaitForFixedUpdate();
            }
            PowerStorageProfiler.Lap("MapNetworks", watchMapNetworks);
            #endregion Map Networks from collisions

            
            #region Join networks by powerpole networks
            var watchJoinNetworksByNodes = PowerStorageProfiler.Start("JoinNetworksByNodes");
            var nodes = Singleton<NetManager>.instance.m_nodes;
            var powerPoleNetworks = new List<List<ushort>>(byte.MaxValue);
            
            foreach (var network in networks)
            foreach (var buildingPair in network)
            {
                if (!(buildingPair.Building.Info.m_buildingAI is PowerPoleAI))
                        continue;

                var node = nodes.m_buffer.FirstOrDefault(n => n.m_building == buildingPair.Index);
                PowerStorageLogger.Log($"Building {buildingPair.Building.Info.name}. b:{buildingPair.Index} x:{buildingPair.Building.m_position.x} z:{buildingPair.Building.m_position.z}", PowerStorageMessageType.NetworkMapping);
                PowerStorageLogger.Log($"Node {node.Info.name}. b:{node.m_building} x:{node.m_position.x} z:{node.m_position.z}", PowerStorageMessageType.NetworkMapping);
                
                var watch2 = PowerStorageProfiler.Start("CollectBuildingIdsOnNetwork");
                var visitedBuildings = new List<ushort>(ushort.MaxValue);
                var nodesExplored = CollectBuildingsOnNetwork(node, ref visitedBuildings);
                PowerStorageProfiler.Stop("CollectBuildingIdsOnNetwork", watch2);

                powerPoleNetworks.Add(nodesExplored);
                PowerStorageProfiler.Lap("JoinNetworksByNodes", watchJoinNetworksByNodes);
                yield return new WaitForFixedUpdate();
            }
            
            foreach (var powerPoleNetwork in powerPoleNetworks)
            {
                var networksToRemove = new List<int>(byte.MaxValue);
                var newMegaNetwork = new List<BuildingAndIndex>(ushort.MaxValue);

                for (var i = 0; i < networks.Count; i++)
                {
                    if (!networks[i].Any(n => powerPoleNetwork.Any(b => n.Index == b))) 
                        continue;

                    networksToRemove.Add(i);
                    newMegaNetwork.AddRange(networks[i]);
                }

                if (networksToRemove.Count <= 1) 
                    continue;
                
                foreach (var i in networksToRemove.Distinct().OrderByDescending(n => n))
                {
                    networks.RemoveAt(i);
                }
                networks.Add(newMegaNetwork);

                yield return new WaitForFixedUpdate();
            }
            PowerStorageProfiler.Stop("JoinNetworksByNodes", watchJoinNetworksByNodes);
            PowerStorageProfiler.Stop("MapNetworks", watchMapNetworks);
            #endregion Join networks by powerpole networks


            #region Update Master network list with new networks
            PowerStorageLogger.Log($"Merging networks ({GridsBuildingsRollup.BuildingsGroupedToNetworks.Count})", PowerStorageMessageType.NetworkMerging);
            var watchMergeNetworksWithMaster = PowerStorageProfiler.Start("MergeNetworksWithMaster");
            var now = Singleton<SimulationManager>.instance.m_currentGameTime;
            var matchedMasterNetworks = new List<BuildingElectricityGroup>(ushort.MaxValue);
            foreach (var network in networks)
            {
                BuildingElectricityGroup bestMatch = null;
                foreach (var bg in GridsBuildingsRollup.BuildingsGroupedToNetworks)
                {
                    if (matchedMasterNetworks.Contains(bg))
                        continue;

                    var match = network.Any(b => bg.BuildingsList.Any(ib => b.GridGameObject == ib.GridGameObject));
                    if (match)
                    {
                        matchedMasterNetworks.Add(bg);
                        bestMatch = bg;
                        break;
                    }
                }

                if (bestMatch != null)
                {
                    PowerStorageLogger.Log($"Existing network updated - Cap: {bestMatch.LastCycleTotalCapacityKw}KW - Con: {bestMatch.LastCycleTotalConsumptionKw}KW", PowerStorageMessageType.NetworkMerging);
                    bestMatch.BuildingsList = network;
                    bestMatch.LastBuildingUpdate = now;
                }
                else
                {
                    PowerStorageLogger.Log("New Network Added", PowerStorageMessageType.NetworkMerging);
                    GridsBuildingsRollup.BuildingsGroupedToNetworks.Add(new BuildingElectricityGroup
                    {
                        BuildingsList = network,
                        LastBuildingUpdate = now
                    });
                }
                PowerStorageProfiler.Lap("MergeNetworksWithMaster", watchMergeNetworksWithMaster);
                yield return new WaitForFixedUpdate();
            }
            for (var i = GridsBuildingsRollup.BuildingsGroupedToNetworks.Count - 1; i >= 0; i--)
            {
                if(GridsBuildingsRollup.BuildingsGroupedToNetworks[i].LastBuildingUpdate != now)
                    GridsBuildingsRollup.BuildingsGroupedToNetworks.RemoveAt(i);
            }

            PowerStorageProfiler.Stop("MergeNetworksWithMaster", watchMergeNetworksWithMaster);
            PowerStorageLogger.Log($"Done Merging networks ({GridsBuildingsRollup.BuildingsGroupedToNetworks.Count})", PowerStorageMessageType.NetworkMerging);
            #endregion Update Master network list with new networks
            
            PowerStorageProfiler.Stop("Make Meshes", watch);
            InProgress = false;
            DestroyObject(gameObject);
        }

        private Polygon MapPerimeterOfPoly(KeyValuePair<List<Vector2>, List<Vector2>> pointsForMesh)
        {
            var polygon = new Polygon();
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

            return polygon;
        }

        private bool IsConductive(ElectricityManager.Cell cell, float x, float y)
        {
            return cell.m_conductivity >= 64 && x != 0 && (y != 0 && (int)x != byte.MaxValue) && (int)y != byte.MaxValue;
        }

        private int GetNewIndex(double x, double y)
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
        
        private int _collectOnNetworkIndex;

        /// <summary>
        /// Each power pole has a building and a network node, each power pole is connected by segments (lines).
        /// Follow the lines of the pole 'node' (recursively) to collect the whole network that `node` is a part of.
        /// </summary>
        private List<ushort> CollectBuildingsOnNetwork(NetNode node, ref List<ushort> visitedBuildings)
        {
            _collectOnNetworkIndex++;

            PowerStorageLogger.Log($"{_collectOnNetworkIndex}: NodeBuilding: {node.m_building}", PowerStorageMessageType.Saving);
            if (!visitedBuildings.Contains(node.m_building))
            {
                visitedBuildings.Add(node.m_building);
                GetEdges(node.m_segment0, ref visitedBuildings);
                GetEdges(node.m_segment1, ref visitedBuildings);
                GetEdges(node.m_segment2, ref visitedBuildings);
                GetEdges(node.m_segment3, ref visitedBuildings);
                GetEdges(node.m_segment4, ref visitedBuildings);
                GetEdges(node.m_segment5, ref visitedBuildings);
                GetEdges(node.m_segment6, ref visitedBuildings);
                GetEdges(node.m_segment7, ref visitedBuildings);
            }
            else
            {
                PowerStorageLogger.Log($"{_collectOnNetworkIndex}: Skip. Currently have: {visitedBuildings.Count}", PowerStorageMessageType.Saving);
            }
            
            return visitedBuildings;
        }

        private void GetEdges(ushort segmentIndex, ref List<ushort> visitedBuildings)
        {
            if (segmentIndex <= 0) 
                return;

            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentIndex];
            var nodes = Singleton<NetManager>.instance.m_nodes.m_buffer;

            PowerStorageLogger.Log($"{_collectOnNetworkIndex}: Segment: {segment}", PowerStorageMessageType.Saving);
            
            var s = segment.m_startNode;
            var startNode = nodes[s];
            CollectBuildingsOnNetwork(startNode, ref visitedBuildings);

            var e = segment.m_endNode;
            var endNode = nodes[e];
            CollectBuildingsOnNetwork(endNode, ref visitedBuildings);
        }
    }
}
