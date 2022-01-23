﻿using ColossalFramework.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PowerStorage
{
    [Serializable]
    public class ConcurrentMap : IDataContainer
    {
        private object _lock = new object();
        private GridMemberLastTickStats[] deserializationHolder;
        public Dictionary<ushort, GridMemberLastTickStats> Map = new Dictionary<ushort, GridMemberLastTickStats>();

        public bool TryGetValue(ushort key, out GridMemberLastTickStats value)
        {
            lock (_lock)
            {
                return Map.TryGetValue(key, out value);
            }
        }

        public bool TryAdd(ushort key, GridMemberLastTickStats value)
        {
            lock (_lock)
            {
                if (!Map.ContainsKey(key))
                {
                    Map.Add(key, value);
                    return true;
                }
                return false;
            }
        }

        public bool TryRemove(ushort key)
        {
            lock (_lock)
            {
                return Map.Remove(key);
            }
        }

        public void Serialize(DataSerializer s)
        {                      
            var values = Map.Values.ToArray();
            foreach (var value in values)
            {
                PowerStorageLogger.Log($"Saving - {value.BuildingId} - {value.CurrentChargeKw}Kw of {value.CapacityKw}Kw");
            }
            s.WriteObjectArray(values);
        }

        public void Deserialize(DataSerializer s)
        {
            _lock = new object();
            deserializationHolder = s.ReadObjectArray<GridMemberLastTickStats>();           
        }

        public void AfterDeserialize(DataSerializer s) 
        { 
            var savedFileMap = new Dictionary<ushort, GridMemberLastTickStats>();
            foreach(var value in deserializationHolder)
            {
                if (savedFileMap.ContainsKey(value.BuildingId))
                    savedFileMap[value.BuildingId] = value;
                else
                    savedFileMap.Add(value.BuildingId, value);

                PowerStorageLogger.Log($"Loading - {value.BuildingId} - {value.CurrentChargeKw}Kw of {value.CapacityKw}Kw");
            }

            Map = savedFileMap;

            foreach (var p in Map)
            {
                PowerStorageLogger.Log($"Post Load - {p.Value.BuildingId} - {p.Value.CurrentChargeKw}Kw of {p.Value.CapacityKw}Kw");
            }
        }
    }
}
