using FishNet.Connection;
using FishNet.Observing;
using Framework.HSPDIMAlgo;
using UnityEngine;
namespace HSPDIMAlgo
{
    [CreateAssetMenu(menuName = "FishNet/Observers/HSPDIM Distance Condition", fileName = "New HSPDIM Distance Condition")]
    public class HSPDIMCondition : ObserverCondition
    {
        HSPDIMEntity entity;
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            if (!entity)
            {
                entity = NetworkObject.GetComponent<HSPDIMEntity>();
            }
            bool isMet = true;
            notProcessed = false;
            return isMet;
        }

        public override ObserverConditionType GetConditionType()
        {
            return ObserverConditionType.Normal;
        }


    }
}