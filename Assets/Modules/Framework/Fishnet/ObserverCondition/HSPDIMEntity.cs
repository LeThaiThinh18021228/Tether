using FishNet.Object;
using Framework;
using System.Linq;
using TMPro;
using UnityEngine;
namespace HSPDIMAlgo
{
    public class HSPDIMEntity : NetworkBehaviour
    {
        [SerializeField] Vector3 subRange;
        [SerializeField] Vector3 upRange;
        public Range SubRange;
        public Range UpRange;
        [SerializeField] TextMeshPro intersectText;
        [SerializeField] GameObject upIntersectRect;
        [SerializeField] GameObject subIntersectRect;
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (UpRange == null)
            {
                upIntersectRect.SetActive(true);
                upIntersectRect.transform.localScale = new Vector3(upRange.x, upRange.z, 0);
                if (subRange != Vector3.zero)
                {
                    subIntersectRect.SetActive(true);
                    intersectText.gameObject.SetActive(true);
                    intersectText.text = "0";
                    subIntersectRect.transform.localScale = new Vector3(subRange.x, subRange.z, 0);
                }
            }
            if (IsServerInitialized)
            {
                if (UpRange == null)
                {
                    UpRange = new(upRange, this, HSPDIM.upTreeDepth);
                    Debug.Log($"Create UpRange {name}_{UpRange.GetHashCode()}");
                    if (subRange != Vector3.zero)
                    {
                        SubRange = new(subRange, this, HSPDIM.subTreeDepth);
                        Debug.Log($"Create SupRange {name}_{SubRange.GetHashCode()}");
                        SubRange.OnUpdateIntersection += OnUpdateIntersection;
                    }
                }
                if (!HSPDIM.Instance.isRunning)
                {
                    Debug.Log("Add UpRange" + name);
                    HSPDIM.Instance.upRanges.Add(UpRange);
                    if (subRange != Vector3.zero)
                    {
                        Debug.Log("Add SupRange" + name);
                        HSPDIM.Instance.subRanges.Add(SubRange);
                    }
                }
            }
        }
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (IsServerInitialized && HSPDIM.Instance.isRunning && false)
            {
                Debug.Log("Destroy" + name);
                if (UpRange == null)
                {
                    HSPDIM.RemoveRangeFromTree(UpRange, HSPDIM.Instance.upTree);
                    if (subRange != Vector3.zero)
                    {
                        HSPDIM.RemoveRangeFromTree(SubRange, HSPDIM.Instance.subTree);
                    }
                }
            }
        }
        private void OnUpdateIntersection()
        {
            intersectText.text = (SubRange.intersection.DefaultIfEmpty().Count() - 1).ToString();
        }

        protected virtual void Update()
        {
            if (IsServerInitialized && subRange != Vector3.zero && HSPDIM.UpdateInterval() && HSPDIM.Instance.isRunning)
            {
                for (int i = 0; i < HSPDIM.dimension; i++)
                {
                    SubRange.overlapSets[i].Clear();
                }
                PDebug.Log("overlapset clear");
            }
        }
    }
}