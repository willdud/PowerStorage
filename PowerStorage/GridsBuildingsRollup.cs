using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ICities;
using UnityEngine;

namespace PowerStorage
{
    public class GridsBuildingsRollup : ThreadingExtensionBase
    {
        private static int _buildingUpdatesLastTick = 0;
        private static int _buildingUpdates = 0;
        public static List<BuildingElectricityGroup> MasterBuildingsList { get; set; }

        public static BuildingElectricityGroup GetGroupContainingBuilding(BuildingAndIndex pair) => MasterBuildingsList.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.Building.m_position == pair.Building.m_position));

        static GridsBuildingsRollup()
        {
            MasterBuildingsList = new List<BuildingElectricityGroup>(ushort.MaxValue);
        }

        public static void AddCapacity(Vector3 pos, int kw)
        {
            var buildingGroup = MasterBuildingsList.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.Building.m_position == pos));
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
            var buildingGroup = MasterBuildingsList.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.Building.m_position == pos));
            if (!buildingGroup?.BuildingsList.Any() ?? true)
            {
                PowerStorageLogger.Log($"Building list was empty for AddConsumption {kw}KW", PowerStorageMessageType.Network);
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

            foreach (var bg in MasterBuildingsList)
            {
                HandleSimulationLoop(bg);
            }

            if (_buildingUpdates == _buildingUpdatesLastTick)
                return;

            _buildingUpdatesLastTick = _buildingUpdates;
            
            var watch = PowerStorageProfiler.Start("Whole network process");
            var buildings = Singleton<BuildingManager>.instance.m_buildings;
            var unvisitedPoints = buildings.m_buffer
                .Select((b, i) => new BuildingAndIndex((ushort)i, b))
                .Where(o =>
                    {
                        if (o.Building.m_buildIndex <= 0)
                            return false;

                        var aiType = o.Building.Info.m_buildingAI.GetType();
                        return IncludedTypes.Any(it => it.IsAssignableFrom(aiType));
                    }
                )
                .ToList();
            MapNetworks(unvisitedPoints, out var networks);
            MergeNetworksWithMaster(networks);
            PowerStorageProfiler.Stop("Whole network process", watch);
        }

        private static void HandleSimulationLoop(BuildingElectricityGroup beg)
        {
            beg.LastCycleTotalCapacityKw = beg.CapacityKw;
            beg.LastCycleTotalConsumptionKw = beg.ConsumptionKw;
            beg.CapacityKw = 0;
            beg.ConsumptionKw = 0;
            PowerStorageLogger.Log($"This Group made: {beg.LastCycleTotalCapacityKw} and spent: {beg.LastCycleTotalConsumptionKw}", PowerStorageMessageType.Grid);
        }

        private static readonly Type[] IncludedTypes =
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

        private static void MergeNetworksWithMaster(List<List<BuildingAndIndex>> networks)
        {
            PowerStorageLogger.Log($"Merging networks ({MasterBuildingsList.Count})", PowerStorageMessageType.NetworkMerging);
            var watch = PowerStorageProfiler.Start("MergeNetworksWithMaster");
            var now = Singleton<SimulationManager>.instance.m_currentGameTime;
            var matchedMasterNetworks = new List<BuildingElectricityGroup>();
            foreach (var network in networks)
            {
                BuildingElectricityGroup bestMatch = null;
                foreach (var bg in MasterBuildingsList)
                {
                    if (matchedMasterNetworks.Contains(bg))
                        continue;

                    var match = network.Any(b => bg.BuildingsList.Any(ib => b.Index == ib.Index));
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
                    MasterBuildingsList.Add(new BuildingElectricityGroup
                    {
                        BuildingsList = network,
                        LastBuildingUpdate = now
                    });
                }
                PowerStorageProfiler.Lap("MergeNetworksWithMaster", watch);
            }
            for (var i = MasterBuildingsList.Count - 1; i >= 0; i--)
            {
                if(MasterBuildingsList[i].LastBuildingUpdate != now)
                    MasterBuildingsList.RemoveAt(i);
            }
            PowerStorageProfiler.Stop("MergeNetworksWithMaster", watch);
            PowerStorageLogger.Log($"Done Merging networks ({MasterBuildingsList.Count})", PowerStorageMessageType.NetworkMerging);
        }

        private static float DistanceBetweenPoints(BuildingAndIndex one, BuildingAndIndex two, float performanceSavingMaxValue)
        {	
            var a = Mathf.Abs(one.Building.m_position.x - two.Building.m_position.x);
            var b = Mathf.Abs(one.Building.m_position.z - two.Building.m_position.z);

            // if any one edge is further than our compare value don't bother with sqrt
            if (performanceSavingMaxValue < a || performanceSavingMaxValue < b)
                return float.MaxValue;
            
            return Mathf.Sqrt(a*a + b*b);
        }

        private static void MapNetworks(List<BuildingAndIndex> unmappedPoints, out List<List<BuildingAndIndex>> networks) 
        {
            var watch = PowerStorageProfiler.Start("MapNetworks");
            networks = new List<List<BuildingAndIndex>>();
            while(unmappedPoints.Any())
            {
                PowerStorageLogger.Log($"MapNetworks unmappedPoints({unmappedPoints.Count})", PowerStorageMessageType.NetworkMapping);
                var point = unmappedPoints.First();
                var network = MapNetwork(point, unmappedPoints);
                unmappedPoints = unmappedPoints.Except(network).ToList();
                networks.Add(network);
                PowerStorageProfiler.Lap("MapNetworks", watch);
            }
            PowerStorageProfiler.Stop("MapNetworks", watch);

            networks = JoinNetworksByNodes(networks);
        }

        private static List<List<BuildingAndIndex>> JoinNetworksByNodes(List<List<BuildingAndIndex>> networks)
        {
            var watch = PowerStorageProfiler.Start("JoinNetworksByNodes");

            var buildings = Singleton<BuildingManager>.instance.m_buildings;
            var nodes = Singleton<NetManager>.instance.m_nodes;
            var networksToAdd = new List<List<BuildingAndIndex>>();
            var networksToRemove = new List<int>();

            foreach (var network in networks)
            foreach (var buildingPair in network)
            {
                if (!(buildingPair.Building.Info.m_buildingAI is PowerPoleAI))
                        continue;

                if(networksToRemove.Contains(networks.IndexOf(network)))
                    continue;
                
                var node = nodes.m_buffer.FirstOrDefault(n => n.m_building == buildingPair.Index);
                PowerStorageLogger.Log($"Building {buildingPair.Building.Info.name}. b:{buildingPair.Index} x:{buildingPair.Building.m_position.x} z:{buildingPair.Building.m_position.z}", PowerStorageMessageType.NetworkMapping);
                PowerStorageLogger.Log($"Node {node.Info.name}. b:{node.m_building} x:{node.m_position.x} z:{node.m_position.z}", PowerStorageMessageType.NetworkMapping);
                
                var watch2 = PowerStorageProfiler.Start("CollectBuildingIdsOnNetwork");
                var nodesExplored = CollectBuildingIdsOnNetwork(node, new List<ushort>());
                PowerStorageProfiler.Stop("CollectBuildingIdsOnNetwork", watch2);

                var networksToCombine = new List<int> { networks.IndexOf(network) };
                for (var i = 0; i < networks.Count; i++)
                {
                    if (network == networks[i])
                        continue;

                    if (networks[i].Any(n => nodesExplored.Any(b => n.Building.m_position == b.m_position)))
                        networksToCombine.Add(i);
                }

                if (networksToCombine.Count <= 1) 
                    continue;
                
                var newMegaNetwork = new List<BuildingAndIndex>();
                foreach (var n in networksToCombine)
                {
                    if(newMegaNetwork.Any())
                        networksToRemove.Add(n);
                    newMegaNetwork.AddRange(networks[n]);
                }
                networksToAdd.Add(newMegaNetwork);
                PowerStorageProfiler.Lap("JoinNetworksByNodes", watch);
            }

            PowerStorageLogger.Log($"Removing Networks-A ({networksToRemove.Count}) {string.Join(", ", networksToRemove.Select(i => i.ToString()).ToArray())}", PowerStorageMessageType.NetworkMapping);
            PowerStorageLogger.Log($"Removing Networks-B ({networks.Count})", PowerStorageMessageType.NetworkMapping);
            foreach (var i in networksToRemove.Distinct().OrderByDescending(n => n))
            {
                networks.RemoveAt(i);
            }

            foreach (var network in networksToAdd)
            {
                networks.Add(network);
            }
            
            
            PowerStorageProfiler.Stop("JoinNetworksByNodes", watch);
            return networks;
        }
        
        private static List<Building> CollectBuildingIdsOnNetwork(NetNode node, List<ushort> visitedSegments)
        {
            var segments = Singleton<NetManager>.instance.m_segments.m_buffer;
            var nodes = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var connectedNodesBuildings = new List<Building>();
            
            void Get(ushort segment)
            {
                if (segment <= 0 || visitedSegments.Contains(segment)) return;

                visitedSegments.Add(segment);
                var s = segments[segment].m_startNode;
                var e = segments[segment].m_endNode;
                var startNode = nodes[s];
                var endNode = nodes[e];
                connectedNodesBuildings.AddRange(CollectBuildingIdsOnNetwork(startNode, visitedSegments));
                connectedNodesBuildings.AddRange(CollectBuildingIdsOnNetwork(endNode, visitedSegments));
            }

            Get(node.m_segment0);
            Get(node.m_segment1);
            Get(node.m_segment2);
            Get(node.m_segment3);
            Get(node.m_segment4);
            Get(node.m_segment5);
            Get(node.m_segment6);
            Get(node.m_segment7);

            var building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[node.m_building];
            if(!connectedNodesBuildings.Any(cnb => cnb.m_position == building.m_position))
                connectedNodesBuildings.Add(building);

            return connectedNodesBuildings;
        }

        private static List<BuildingAndIndex> MapNetwork(BuildingAndIndex point, List<BuildingAndIndex> unmappedPoints)
        {
            var network = new List<BuildingAndIndex> { point };
            if(unmappedPoints.Contains(point))
                unmappedPoints.Remove(point);

            BuildingAndIndex newPoint = null;
            while (unmappedPoints.Any(p =>
            {
                // 19.125 added to radius in ElectricityManager, we must copy
                var electricityConductDistance = point.Building.Info.m_buildingAI.ElectricityGridRadius() + p.Building.Info.m_buildingAI.ElectricityGridRadius() + (19.125f * 2f);
                var hasMatch = DistanceBetweenPoints(point, p, electricityConductDistance) <= electricityConductDistance; 
                if(hasMatch)		
                    newPoint = p;
                return hasMatch;
            }))
            {
                unmappedPoints.Remove(newPoint);
                network.AddRange(MapNetwork(newPoint, unmappedPoints));
            }
            PowerStorageLogger.Log($"MapNetwork ({network.Count})", PowerStorageMessageType.NetworkMapping);
            return network;
        }
    }
}
