using UnityEngine;

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
        public BuildingAndIndex(ushort index, Building building, GameObject gridGameObject)
        {
            Index = index;
            Building = building;
            GridGameObject = gridGameObject;
        }

        public ushort Index { get; private set; }
        public GameObject GridGameObject { get; set; }
        public Building Building { get; private set; }
    }
}
