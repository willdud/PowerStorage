using System.Collections.Generic;
using UnityEngine;

namespace PowerStorage.Supporting
{
    public static class GameObjectExtensions
    {
        public static List<GameObject> GetAllChildren(this GameObject go)
        {
            var list = new List<GameObject>();
            for (int i = 0; i< go.transform.childCount; i++)
            {
                list.Add(go.transform.GetChild(i).gameObject);
            }
            return list;
        }
    }
}
