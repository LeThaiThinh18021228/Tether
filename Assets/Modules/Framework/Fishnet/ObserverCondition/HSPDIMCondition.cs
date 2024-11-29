using FishNet.Connection;
using FishNet.Observing;
using UnityEngine;
namespace HSPDIMAlgo
{
    [CreateAssetMenu(menuName = "FishNet/Observers/HSPDIM Distance Condition", fileName = "New HSPDIM Distance Condition")]
    public class HSPDIMCondition : ObserverCondition
    {
        private void OnDisable()
        {

        }
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;
            return true;
        }

        public override ObserverConditionType GetConditionType()
        {
            return ObserverConditionType.Normal;
        }


    }
}