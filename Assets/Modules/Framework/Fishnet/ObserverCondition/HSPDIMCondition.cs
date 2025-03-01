using FishNet.Connection;
using FishNet.Object;
using FishNet.Observing;
using Framework.HSPDIMAlgo;
using System.Collections.Generic;
using UnityEngine;
namespace HSPDIMAlgo
{
    [CreateAssetMenu(menuName = "FishNet/Observers/HSPDIM Distance Condition", fileName = "New HSPDIM Distance Condition")]
    public class HSPDIMCondition : ObserverCondition
    {
        HSPDIMEntity e1;
        public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
        {
            notProcessed = false;
            HSPDIM.Instance.stopwatchLookupResult.Start();
            if (e1 == null)
            {
                e1 = NetworkObject.GetComponent<HSPDIMEntity>();
            }
            foreach (NetworkObject nob in connection.Objects)
            {
                var e2 = nob.GetComponent<HSPDIMEntity>();
                if (e1.Id<e2.Id)
                {
                    if (HSPDIM.Instance.Result.Contains(new(e1.Id, e2.Id, 0, 0)) && HSPDIM.Instance.Result.Contains(new(e1.Id, e2.Id, 1, 0)))
                        return true;
                    //if (HSPDIM.Instance.HSPDIMEntities[e1.Id].UpRange.intersectionId.Contains(e2.Id)) return true;
                }
                else
                {
                    if(HSPDIM.Instance.Result.Contains(new(e2.Id, e1.Id, 0, 1)) && HSPDIM.Instance.Result.Contains(new(e2.Id, e1.Id, 1, 1)))
                        return true;
                    //if (HSPDIM.Instance.HSPDIMEntities[e2.Id].SubRange.intersectionId.Contains(e1.Id))return true;
                }
            }
            HSPDIM.Instance.stopwatchLookupResult.Stop();

            return true;
        }

        public override ObserverConditionType GetConditionType()
        {
            return ObserverConditionType.Timed;
        }


    }
}