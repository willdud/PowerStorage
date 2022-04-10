using ColossalFramework.IO;
using System;

namespace PowerStorage
{
    [Serializable]
    public class GridMemberLastTickStats : IDataContainer
    {
        private int? _potentialOutputOverrideKw;
        private int? _potentialDrawOverrideKw;

        public GridMemberLastTickStats(ushort buildingId)
        {
            BuildingId = buildingId;
        }

        public ushort BuildingId { get; set; }
        public bool IsOff { get; set; }
        public bool IsActive => !IsDisconnectedMode && ChargeProvidedKw > 0 || ChargeTakenKw > 0;
        public bool IsFull => CurrentChargeKw >= CapacityKw;
        public bool IsDisconnectedMode { get; set; }

        public int ChargeTakenKw { get; set; }
        public int LossKw => (int)(ChargeTakenKw * PowerStorage.LossRatio);

        public int PulseGroup { get; set; }

        public int ChargeProvidedKw { get; set; }

        public int CurrentChargeKw { get; set; }
        public int CapacityKw { get; set; }

        public int PotentialOutputKw { get; set; }
        public int PotentialOutputOverrideKw
        {
            get => _potentialOutputOverrideKw.HasValue ? Math.Min(_potentialOutputOverrideKw.Value, PotentialOutputKw) : PotentialOutputKw;
            set => _potentialOutputOverrideKw = value;
        }
        public int PotentialDrawKw { get; set; }
        public int PotentialDrawOverrideKw
        {
            get => _potentialDrawOverrideKw.HasValue ? Math.Min(_potentialDrawOverrideKw.Value, PotentialDrawKw) : PotentialDrawKw;
            set => _potentialDrawOverrideKw = value;
        }

        public void Deserialize(DataSerializer s)
        {
            BuildingId = (ushort)s.ReadUInt16();

            ChargeTakenKw = s.ReadInt32();
            ChargeProvidedKw = s.ReadInt32();
            CurrentChargeKw = s.ReadInt32();
            CapacityKw = s.ReadInt32();
            PotentialOutputKw = s.ReadInt32();
            PotentialDrawKw = s.ReadInt32();

            IsOff = s.ReadBool();
            
            _potentialOutputOverrideKw = s.ReadInt32();
            _potentialDrawOverrideKw = s.ReadInt32();

            PulseGroup = s.ReadInt32();
            PowerStorageLogger.Log($"Loading Inner - {BuildingId} - {CurrentChargeKw}Kw of {CapacityKw}Kw", PowerStorageMessageType.Loading);
        }

        public void Serialize(DataSerializer s)
        {            
            s.WriteUInt16(BuildingId);

            s.WriteInt32(ChargeTakenKw);
            s.WriteInt32(ChargeProvidedKw);
            s.WriteInt32(CurrentChargeKw);
            s.WriteInt32(CapacityKw);
            s.WriteInt32(PotentialOutputKw);
            s.WriteInt32(PotentialDrawKw);

            s.WriteBool(IsOff);

            s.WriteInt32(_potentialOutputOverrideKw.HasValue ? _potentialOutputOverrideKw.Value : PotentialOutputKw);
            s.WriteInt32(_potentialDrawOverrideKw.HasValue ? _potentialDrawOverrideKw.Value : PotentialDrawKw);

            s.WriteInt32(PulseGroup);
            PowerStorageLogger.Log($"Saving Inner - {BuildingId} - {CurrentChargeKw}Kw of {CapacityKw}Kw", PowerStorageMessageType.Saving);
        }

        public void AfterDeserialize(DataSerializer s) {}
    }
}
