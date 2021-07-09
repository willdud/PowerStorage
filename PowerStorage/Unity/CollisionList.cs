using System.Collections.Generic;
using UnityEngine;

namespace PowerStorage.Unity
{
    public class CollisionList : MonoBehaviour 
    {
 
        public List <GameObject> CurrentCollisions = new List <GameObject>(byte.MaxValue);

        private void OnTriggerEnter(Collider col) 
        {
            // Add the GameObject collided with to the list.
            CurrentCollisions.Add(col.gameObject);
        }

        private void OnTriggerExit(Collider col) 
        {
            // Remove the GameObject collided with from the list.
            CurrentCollisions.Remove(col.gameObject);
        }
    }
}
