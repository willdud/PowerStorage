using System;

namespace PowerStorage.Model
{
    [Serializable]
    public class GridMemberLastTickStats
    {
        // Don't break serialization
        [Obsolete] public ushort BuildingId { get; set; }
        [Obsolete] public int ElectricityGridId { get; set; }

            
        public BuildingAndIndex BuildingPair { get; set; }
        public bool IsOff { get; set; }
        public bool IsActive => ChargeProvidedKw > 0 || ChargeTakenKw > 0;
        public bool IsFull => CurrentChargeKw >= CapacityKw;

        public string NeworkName { get; set; }
        public int ChargeTakenKw { get; set; }
        public int LossKw => (int)(ChargeTakenKw * PowerStorage.LossRatio);
        public int ChargeProvidedKw { get; set; }
        public int CurrentChargeKw { get; set; }
        public int CapacityKw { get; set; }
        public int PotentialOutputKw { get; set; }
    }
}
