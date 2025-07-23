using Unity.Netcode;
using UnityEngine;

namespace DilmerGames.Core.Singletons
{
    public class NetworkSingleton<T> : NetworkBehaviour
        where T : Component
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    var objs = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
                    if (objs.Length > 0)
                        _instance = objs[0];
                    if (objs.Length > 1)
                    {
                        Debug.LogError("There is more than one " + typeof(T).Name + " in the scene.");
                    }
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        obj.name = $"_{typeof(T).Name}";
                        _instance = obj.AddComponent<T>();
                    }
                }
                return _instance;
            }
        }
    }
}
