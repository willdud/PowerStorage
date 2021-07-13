using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ColossalFramework.Threading;
using PowerStorage.Model;
using PowerStorage.Supporting;
using UnityEngine;

namespace PowerStorage.Unity
{
    public class CollisionAdder : MonoBehaviour
    {
        public static bool InProgress;
        public static int Progress;
        public static int Total;

        public void BeginAdding()
        {
            if (!InProgress)
            {
                InProgress = true;
                StartCoroutine(AddColliderToBuildings());
            }
        }
        
        public static IEnumerator AddColliderToBuildings()
        {
            var watch = PowerStorageProfiler.Start("Add Collider To Buildings");
            var buffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer.ToList();
            var buildingPairs = new Dictionary<ushort, Building>();
            for (ushort k = 0; k < buffer.Count; k++)
            {
                var building = buffer[k];

                if (building.m_infoIndex == 0 && building.m_position == default)
                    continue;

                if (!GridsBuildingsRollup.IncludedTypes.Any(it => it.IsInstanceOfType(building.Info.m_buildingAI)))
                    continue;

                buildingPairs.Add(k, building);
            }
            
            Total = buildingPairs.Count;
            PowerStorageLogger.Log($"Add Collider To Buildings ({Total})", PowerStorageMessageType.Loading);

            var i = 0;
            ushort j = 0;
            foreach (var pair in buildingPairs)
            {
                Progress = ++j;

                if (pair.Value.m_infoIndex == 0 && pair.Value.m_position == default)
                    continue;
                
                PowerStorageLogger.Log($"#{i++} index:{j}", PowerStorageMessageType.Loading);
                if(!GridsBuildingsRollup.MasterBuildingList.Any(mbl => mbl.Index == pair.Key))
                    RegisterBuilding(pair.Key, pair.Value);

                yield return new WaitForFixedUpdate(); //zomg so good. Any other value is stutter city.
            }
            
            GridsBuildingsRollup.CollidersAdded = true;
            PowerStorageProfiler.Stop("Add Collider To Buildings", watch);
        }

        
        public static BuildingAndIndex RegisterBuilding(ushort index, Building building)
        {
            if (!GridsBuildingsRollup.Enabled)
                return null;

            var item = new BuildingAndIndex(index, building, null);

            Dispatcher.CreateSafeAction(() => {
                var networkGameObject = new GameObject();
                networkGameObject.SetActive(true);
                
                networkGameObject.AddComponent<CollisionList>();
                networkGameObject.AddComponent<Rigidbody>();
                networkGameObject.AddComponent<SphereCollider>(); 
                
                var sphereCollider = networkGameObject.GetComponent<SphereCollider>();
                sphereCollider.enabled = true;
                sphereCollider.isTrigger = true;
                sphereCollider.transform.position = building.m_position;
                sphereCollider.radius = building.Info.m_buildingAI.ElectricityGridRadius() + (19.125f * 2); // 19.125f comes from logic in ElectricityManager
                
                var rigid = sphereCollider.gameObject.GetComponent<Rigidbody>();
                if (rigid != null)
                    rigid.isKinematic = true;

                item.GridGameObject = networkGameObject;
                PowerStorageLogger.Log($"building: {building.m_infoIndex}, {building.Info.name}, {building.m_position} | SphereCollider e: {sphereCollider.enabled}, p: {sphereCollider.transform.position} | {sphereCollider.transform.localPosition}", PowerStorageMessageType.Loading);

            
                var existingMatch = GridsBuildingsRollup.MasterBuildingList.FirstOrDefault(p => p.Index == index);
                if (existingMatch != null)
                {
                    PowerStorageLogger.Log($"Found, about to destroy {item.Building.Info.name}.", PowerStorageMessageType.Loading);
                    GridsBuildingsRollup.MasterBuildingList.Remove(existingMatch);
                    Destroy(existingMatch.GridGameObject);
                }
                
                GridsBuildingsRollup.MasterBuildingList.Add(item);
            });

            return item;
        }
    }
}
