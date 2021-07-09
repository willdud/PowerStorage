using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ColossalFramework.Threading;
using ICities;
using PowerStorage.Model;
using PowerStorage.Supporting;
using PowerStorage.Unity;
using UnityEngine;

namespace PowerStorage
{
    public class GridsBuildingsRollup : ThreadingExtensionBase
    {
        public static bool Enabled = false;
        public static bool CollidersAdded = false;
        
        private static GameObject _collisionAdderObj;
        private static int _buildingUpdatesLastTick;
        private static int _buildingUpdates;
        public static List<BuildingAndIndex> MasterBuildingList { get; set; }
        public static List<BuildingElectricityGroup> BuildingsGroupedToNetworks { get; set; }

        public static BuildingElectricityGroup GetGroupContainingBuilding(BuildingAndIndex pair) => BuildingsGroupedToNetworks.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.Building.m_position == pair.Building.m_position));

        static GridsBuildingsRollup()
        {
            MasterBuildingList = new List<BuildingAndIndex>(ushort.MaxValue);
            BuildingsGroupedToNetworks = new List<BuildingElectricityGroup>(ushort.MaxValue);
        }

        public static void AddCapacity(Vector3 pos, int kw)
        {
            var buildingGroup = BuildingsGroupedToNetworks.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.Building.m_position == pos));
            if (!buildingGroup?.BuildingsList.Any() ?? true)
            {
                PowerStorageLogger.LogWarning($"Building list was empty for AddCapacity {kw}KW. Power providing buildings should be on some kind of grid.", PowerStorageMessageType.Grid);
                return;
            }
            
            buildingGroup.CapacityKw += kw;
            PowerStorageLogger.Log($"{kw}KW added to {buildingGroup.CodeName} with {buildingGroup.BuildingsList.Count} members", PowerStorageMessageType.Grid);
        }
        
        public static void AddConsumption(Vector3 pos, int kw)
        {
            var buildingGroup = BuildingsGroupedToNetworks.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.Building.m_position == pos));
            if (!buildingGroup?.BuildingsList.Any() ?? true)
            {
                PowerStorageLogger.Log($"Building list was empty for AddConsumption {kw}KW", PowerStorageMessageType.Grid);
                return;
            }
            
            buildingGroup.ConsumptionKw += kw;
            PowerStorageLogger.Log($"{kw}KW removed from {buildingGroup.CodeName} with {buildingGroup.BuildingsList.Count} members", PowerStorageMessageType.Grid);
        }
        
        public override void OnBeforeSimulationFrame()
        {
            base.OnBeforeSimulationFrame();
            var frameLoopedAt256 = (int) Singleton<SimulationManager>.instance.m_currentFrameIndex & (int)byte.MaxValue;
            var basicallyHalf = frameLoopedAt256 * 128 >> 8; // Logic stolen from DistrictManager, every 512 frames, will come through here twice as each number 
            if (basicallyHalf != 0 || frameLoopedAt256 % 2 == 0) 
                return;
            
            PowerStorageLogger.Log($"{DateTime.Now:HH:mm:ss} - {frameLoopedAt256} - {basicallyHalf}", PowerStorageMessageType.Simulation);

            foreach (var bg in BuildingsGroupedToNetworks)
            {
                HandleSimulationLoop(bg);
            }
            
            if (_buildingUpdates == _buildingUpdatesLastTick)
                return;
            
            if (Enabled && !CollidersAdded)
            {
                _collisionAdderObj = new GameObject { name = "PowerStorageCollisionAdderObj" };
                var adder = _collisionAdderObj.AddComponent<CollisionAdder>();
                adder.BeginAdding();
            }

            if (Enabled && CollidersAdded)
            {
                _buildingUpdatesLastTick = _buildingUpdates; // only update if we are correcting the networks

                var watch = PowerStorageProfiler.Start("Whole network process");
                var unvisitedPoints = MasterBuildingList.ToList();

                var task = new Task<bool>(() =>
                {
                    MapNetworks(unvisitedPoints, out var networks);
                    MergeNewNetworksWithMaster(networks);
                    PowerStorageProfiler.Stop("Whole network process", watch);
                }).Run();

                PowerStorageLogger.Log($"Task {task.result}", PowerStorageMessageType.Simulation);
            }
        }

        private static void HandleSimulationLoop(BuildingElectricityGroup beg)
        {
            beg.LastCycleTotalCapacityKw = beg.CapacityKw;
            beg.LastCycleTotalConsumptionKw = beg.ConsumptionKw;
            beg.CapacityKw = 0;
            beg.ConsumptionKw = 0;
            PowerStorageLogger.Log($"This Group made: {beg.LastCycleTotalCapacityKw} and spent: {beg.LastCycleTotalConsumptionKw}", PowerStorageMessageType.Grid);
        }

        public static readonly Type[] IncludedTypes =
        {
            typeof(PlayerBuildingAI), 
            typeof(ResidentialBuildingAI), 
            typeof(CommercialBuildingAI), 
            typeof(IndustrialBuildingAI), typeof(IndustrialExtractorAI), 
            typeof(OfficeBuildingAI),
            typeof(PowerPoleAI)
        };
        public static void UpdateGrid()
        {
            _buildingUpdates++;
        }

        private static void MergeNewNetworksWithMaster(List<List<BuildingAndIndex>> newNetworks)
        {
            PowerStorageLogger.Log($"Merging networks ({BuildingsGroupedToNetworks.Count})", PowerStorageMessageType.NetworkMerging);
            var watch = PowerStorageProfiler.Start("MergeNetworksWithMaster");
            var now = Singleton<SimulationManager>.instance.m_currentGameTime;
            var matchedMasterNetworks = new List<BuildingElectricityGroup>(ushort.MaxValue);
            foreach (var network in newNetworks)
            {
                BuildingElectricityGroup bestMatch = null;
                foreach (var bg in BuildingsGroupedToNetworks)
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
                    BuildingsGroupedToNetworks.Add(new BuildingElectricityGroup
                    {
                        BuildingsList = network,
                        LastBuildingUpdate = now
                    });
                }
                PowerStorageProfiler.Lap("MergeNetworksWithMaster", watch);
            }
            for (var i = BuildingsGroupedToNetworks.Count - 1; i >= 0; i--)
            {
                if(BuildingsGroupedToNetworks[i].LastBuildingUpdate != now)
                    BuildingsGroupedToNetworks.RemoveAt(i);
            }
            PowerStorageProfiler.Stop("MergeNetworksWithMaster", watch);
            PowerStorageLogger.Log($"Done Merging networks ({BuildingsGroupedToNetworks.Count})", PowerStorageMessageType.NetworkMerging);
        }
        
        /// <summary>
        /// From a list of buildings (`unmappedPoints`), group them by electricity conductivity.
        /// </summary>
        private static void MapNetworks(List<BuildingAndIndex> unmappedPoints, out List<List<BuildingAndIndex>> networks) 
        {
            var watch = PowerStorageProfiler.Start("MapNetworks");
            networks = new List<List<BuildingAndIndex>>(byte.MaxValue);
            var unmappedDict = unmappedPoints.ToDictionary(k => k.GridGameObject, v => v);

            while(unmappedDict.Any())
            {
                PowerStorageLogger.Log($"MapNetworks unmappedPoints({unmappedPoints.Count})", PowerStorageMessageType.NetworkMapping);
                var point = unmappedPoints.First();
                var network = MapNetwork(point, ref unmappedDict);
                unmappedPoints = unmappedPoints.Except(network).ToList();
                if(!network.All(b => b.Building.Info.m_buildingAI is PowerPoleAI))
                    networks.Add(network);
                PowerStorageProfiler.Lap("MapNetworks", watch);
            }
            PowerStorageProfiler.Stop("MapNetworks", watch);

            networks = JoinNetworksByNodes(networks);
        }

        /// <summary>
        /// Each 'network' in `networks` is a group of buildings withing 'ElectricityRadius()' of each other.
        /// Some of the buildings are power poles which conduct by segments (lines) not by radius.
        /// This method will combine all networks that should conduct electricity via power lines.
        /// </summary>
        private static List<List<BuildingAndIndex>> JoinNetworksByNodes(List<List<BuildingAndIndex>> networks)
        {
            var watch = PowerStorageProfiler.Start("JoinNetworksByNodes");
            
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
                    
                PowerStorageProfiler.Lap("JoinNetworksByNodes", watch);
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
            }
            
            PowerStorageProfiler.Stop("JoinNetworksByNodes", watch);
            return networks;
        }

        private static int GlobalIndex;

        /// <summary>
        /// Each power pole has a building and a network node, each power pole is connected by segments (lines).
        /// Follow the lines of the pole 'node' (recursively) to collect the whole network that `node` is a part of.
        /// </summary>
        private static List<ushort> CollectBuildingsOnNetwork(NetNode node, ref List<ushort> visitedBuildings)
        {
            GlobalIndex++;

            PowerStorageLogger.Log($"{GlobalIndex}: NodeBuilding: {node.m_building}", PowerStorageMessageType.Saving);
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
                PowerStorageLogger.Log($"{GlobalIndex}: Skip. Currently have: {visitedBuildings.Count}", PowerStorageMessageType.Saving);
            }
            
            return visitedBuildings;
        }

        private static void GetEdges(ushort segmentIndex, ref List<ushort> visitedBuildings)
        {
            if (segmentIndex <= 0) 
                return;

            var segment = Singleton<NetManager>.instance.m_segments.m_buffer[segmentIndex];
            var nodes = Singleton<NetManager>.instance.m_nodes.m_buffer;

            PowerStorageLogger.Log($"{GlobalIndex}: Segment: {segment}", PowerStorageMessageType.Saving);
            
            var s = segment.m_startNode;
            var startNode = nodes[s];
            CollectBuildingsOnNetwork(startNode, ref visitedBuildings);

            var e = segment.m_endNode;
            var endNode = nodes[e];
            CollectBuildingsOnNetwork(endNode, ref visitedBuildings);
        }


        /// <summary>
        /// From a single building collect its neighbours, for each neighbour collect its neighbours.
        /// Return a list of all buildings in range of a building in range of `point`.
        /// I've called the collection of buildings a 'network'.
        /// </summary>
        private static List<BuildingAndIndex> MapNetwork(BuildingAndIndex point, ref Dictionary<GameObject, BuildingAndIndex> unmappedDict)
        {
            var network = new List<BuildingAndIndex>(short.MaxValue) { point };
            if(unmappedDict.ContainsKey(point.GridGameObject))
                unmappedDict.Remove(point.GridGameObject);

            var inRange = new Queue<BuildingAndIndex>();
            inRange.Enqueue(point);
            do
            {
                List<GameObject> nearPoints = null;
                Dispatcher.CreateSafeAction(() =>
                {
                    nearPoints = inRange.Dequeue().GridGameObject.GetComponent<CollisionList>().CurrentCollisions;
                }).Invoke();

                if (nearPoints == null)
                    throw new Exception("Too optimistic");

                foreach (var gameObject in nearPoints)
                {
                    if (unmappedDict.ContainsKey(gameObject))
                    {
                        var match = unmappedDict[gameObject];
                        if(network.Contains(match))
                            continue;

                        inRange.Enqueue(match);
                        network.Add(match);
                        unmappedDict.Remove(match.GridGameObject);
                    }
                }
            } while (inRange.Any());

            PowerStorageLogger.Log($"MapNetwork ({network.Count})", PowerStorageMessageType.NetworkMapping);
            
            return network;
        }
    }
}
