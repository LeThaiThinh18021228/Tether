#if FISHNET
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Framework.FishNet
{
    public static class NetworkObjectExtension
    {
        public static T InstantiateNetworked<T>(this GameObject obj, NetworkConnection conn, Transform parent, Vector3 pos, Quaternion rot) where T : NetworkBehaviour
        {
            T nob = InstanceFinder.NetworkManager.GetPooledInstantiated(obj, pos, rot, parent, true).GetComponent<T>();
            InstanceFinder.ServerManager.Spawn(nob.gameObject, conn);
            return nob;
        }
        public static T InstantiateNetworked<T>(this GameObject obj, NetworkConnection conn, Transform parent, Vector3 pos) where T : NetworkBehaviour
        {
            T nob = InstanceFinder.NetworkManager.GetPooledInstantiated(obj, pos, Quaternion.identity, parent, true).GetComponent<T>();
            InstanceFinder.ServerManager.Spawn(nob.gameObject, conn);
            return nob;
        }
        public static T InstantiateNetworked<T>(this GameObject obj, NetworkConnection conn, Transform parent = null) where T : NetworkBehaviour
        {
            T nob = InstanceFinder.NetworkManager.GetPooledInstantiated(obj, parent, true).GetComponent<T>();
            InstanceFinder.ServerManager.Spawn(nob.gameObject, conn);
            return nob;
        }
    }

}
#endif