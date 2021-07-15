using UnityEngine;

namespace PowerStorage.Unity
{
    [RequireComponent(typeof(CollisionList))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(Rigidbody))]
    public class GridMesh : MonoBehaviour
    {
        public GridMesh()
        {
            var rigid = GetComponent<Rigidbody>();
            rigid.isKinematic = true;

            var collider = GetComponent<MeshCollider>();
            collider.convex = true;
            collider.isTrigger = true;
            collider.enabled = true;
        }
    }
}
