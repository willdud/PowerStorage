
namespace PowerStorage
{
    // Often need both of these and using IndexOf on the buffer is slow, so lets just keep the info together.
    public class BuildingAndIndex
    {
        public BuildingAndIndex(ushort index, Building building)
        {
            Index = index;
            Building = building;
        }

        public ushort Index { get; private set; }
        public Building Building { get; private set; }
    }
}
