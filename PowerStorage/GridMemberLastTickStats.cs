using ColossalFramework.IO;
using System;

namespace PowerStorage
{
    [Serializable]
    public class GridMemberLastTickStats : IDataContainer
    {
        public GridMemberLastTickStats(ushort buildingId)
        {
            BuildingId = buildingId;
        }

        public ushort BuildingId { get; set; }
        public bool IsOff { get; set; }
        public bool IsActive => ChargeProvidedKw > 0 || ChargeTakenKw > 0;
        public bool IsFull => CurrentChargeKw >= CapacityKw;

        public int ChargeTakenKw { get; set; }
        public int LossKw => (int)(ChargeTakenKw * PowerStorage.LossRatio);
        public int ChargeProvidedKw { get; set; }
        public int CurrentChargeKw { get; set; }
        public int CapacityKw { get; set; }
        public int PotentialOutputKw { get; set; }

        public void Deserialize(DataSerializer s)
        {
            BuildingId = (ushort)s.ReadUInt16();

            ChargeTakenKw = s.ReadInt32();
            ChargeProvidedKw = s.ReadInt32();
            CurrentChargeKw = s.ReadInt32();
            CapacityKw = s.ReadInt32();
            PotentialOutputKw = s.ReadInt32();

            IsOff = s.ReadBool();
            PowerStorageLogger.Log($"Loading Inner - {BuildingId} - {CurrentChargeKw}Kw of {CapacityKw}Kw");
        }

        public void Serialize(DataSerializer s)
        {            
            s.WriteUInt16(BuildingId);

            s.WriteInt32(ChargeTakenKw);
            s.WriteInt32(ChargeProvidedKw);
            s.WriteInt32(CurrentChargeKw);
            s.WriteInt32(CapacityKw);
            s.WriteInt32(PotentialOutputKw);

            s.WriteBool(IsOff);
            PowerStorageLogger.Log($"Saving Inner - {BuildingId} - {CurrentChargeKw}Kw of {CapacityKw}Kw");
        }

        public void AfterDeserialize(DataSerializer s) {}
    }
}
