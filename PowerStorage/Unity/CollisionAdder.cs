using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using PowerStorage.Model;
using PowerStorage.Supporting;
using UnityEngine;

namespace PowerStorage.Unity
{
    public class CollisionAdder : MonoBehaviour
    {
        private static GameObject _gameObject;
        public static bool InProgress;
        public static int Progress;
        public static int Total;

        public CollisionAdder()
        {
            _gameObject = gameObject;
        }

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
            if (!GridsBuildingsRollup.Enabled || _gameObject == null)
                return null;

            var item = new BuildingAndIndex(index, building, null);
            
            var networkGameObject = new GameObject("PowerStorageBuildingObj" + index);
            var rigid = networkGameObject.AddComponent<Rigidbody>();
            if (rigid != null)
                rigid.isKinematic = true;

            var meshFilter = networkGameObject.AddComponent<MeshFilter>();
            var capsuleMesh = MakeCapsule();
            meshFilter.mesh = capsuleMesh;
            
            var meshCollider = networkGameObject.AddComponent<MeshCollider>();
            meshCollider.convex = true;
            meshCollider.enabled = true;
            meshCollider.isTrigger = true;
            meshCollider.transform.position = building.m_position;

            networkGameObject.AddComponent<MeshRenderer>();

            item.GridGameObject = networkGameObject;
            PowerStorageLogger.Log($"building: {building.m_infoIndex}, {building.Info.name}, {building.m_position} | SphereCollider e: {meshCollider.enabled}, p: {meshCollider.transform.position} | {meshCollider.transform.localPosition}", PowerStorageMessageType.Loading);

            networkGameObject.transform.parent = _gameObject.transform;

            var masterList = GridsBuildingsRollup.MasterBuildingList.ToArray();
            var existingMatch = masterList.FirstOrDefault(p => p.Index == index);
            if (existingMatch != null)
            {
                PowerStorageLogger.Log($"Found, about to destroy {item.Building.Info.name}.", PowerStorageMessageType.All);
                GridsBuildingsRollup.MasterBuildingList.Remove(existingMatch);
                Destroy(existingMatch.GridGameObject);
            }
            
            GridsBuildingsRollup.MasterBuildingList.Add(item);

            return item;
        }

        private static float _height = 2000f;
	    private static float _radius = 0.5f;
        private static int _segments = 12;

        private static Mesh MakeCapsule() 
	    {
		    // make segments an even number
		    if ( _segments % 2 != 0 )
			    _segments ++;
		    
		    // extra vertex on the seam
		    var points = _segments + 1;
		    
		    // calculate points around a circle
		    var pX = new float[ points ];
		    var pZ = new float[ points ];
		    var pY = new float[ points ];
		    var pR = new float[ points ];
		    
		    var calcH = 0f;
		    var calcV = 0f;
		    
		    for ( var i = 0; i < points; i ++ )
		    {
			    pX[ i ] = Mathf.Sin( calcH * Mathf.Deg2Rad ); 
			    pZ[ i ] = Mathf.Cos( calcH * Mathf.Deg2Rad );
			    pY[ i ] = Mathf.Cos( calcV * Mathf.Deg2Rad ); 
			    pR[ i ] = Mathf.Sin( calcV * Mathf.Deg2Rad ); 
			    
			    calcH += 360f / (float)_segments;
			    calcV += 180f / (float)_segments;
		    }

            // - Vertices and UVs -
            var vertices = new Vector3[ points * ( points + 1 ) ];
		    var uvs = new Vector2[ vertices.Length ];
		    var ind = 0;
		    
		    // Y-offset is half the height minus the diameter
		    var yOff = ( _height - ( _radius * 2f ) ) * 0.5f;
		    if ( yOff < 0 )
			    yOff = 0;
		    
		    // uv calculations
		    var stepX = 1f / ( (float)(points - 1) );
		    float uvX, uvY;
		    
		    // Top Hemisphere
		    var top = Mathf.CeilToInt( (float)points * 0.5f );
		    
		    for ( var y = 0; y < top; y ++ ) 
		    {
			    for ( var x = 0; x < points; x ++ ) 
			    {
				    vertices[ ind ] = new Vector3( pX[ x ] * pR[ y ], pY[ y ], pZ[ x ] * pR[ y ] ) * _radius;
				    vertices[ ind ].y = yOff + vertices[ ind ].y;
				    
				    uvX = 1f - ( stepX * (float)x );
				    uvY = ( vertices[ ind ].y + ( _height * 0.5f ) ) / _height;
				    uvs[ ind ] = new Vector2( uvX, uvY );
				    
				    ind ++;
			    }
		    }
		    
		    // Bottom Hemisphere
		    var btm = Mathf.FloorToInt( (float)points * 0.5f );
		    
		    for ( var y = btm; y < points; y ++ ) 
		    {
			    for ( var x = 0; x < points; x ++ ) 
			    {
				    vertices[ ind ] = new Vector3( pX[ x ] * pR[ y ], pY[ y ], pZ[ x ] * pR[ y ] ) * _radius;
				    vertices[ ind ].y = -yOff + vertices[ ind ].y;
				    
				    uvX = 1f - ( stepX * (float)x );
				    uvY = ( vertices[ ind ].y + ( _height * 0.5f ) ) / _height;
				    uvs[ ind ] = new Vector2( uvX, uvY );
				    
				    ind ++;
			    }
		    }

            // - Triangles -
            var triangles = new int[ ( _segments * (_segments + 1) * 2 * 3 ) ];
		    
		    for ( int y = 0, t = 0; y < _segments + 1; y ++ ) 
		    {
			    for ( var x = 0; x < _segments; x ++, t += 6 ) 
			    {
				    triangles[ t + 0 ] = ( (y + 0) * ( _segments + 1 ) ) + x + 0;
				    triangles[ t + 1 ] = ( (y + 1) * ( _segments + 1 ) ) + x + 0;
				    triangles[ t + 2 ] = ( (y + 1) * ( _segments + 1 ) ) + x + 1;
				    
				    triangles[ t + 3 ] = ( (y + 0) * ( _segments + 1 ) ) + x + 1;
				    triangles[ t + 4 ] = ( (y + 0) * ( _segments + 1 ) ) + x + 0;
				    triangles[ t + 5 ] = ( (y + 1) * ( _segments + 1 ) ) + x + 1;
			    }
		    }

            var mesh = new Mesh
            {
                vertices = vertices, 
                uv = uvs, 
                triangles = triangles
            };
            mesh.RecalculateBounds();
		    mesh.RecalculateNormals();

            return mesh;
        }
    }
}
