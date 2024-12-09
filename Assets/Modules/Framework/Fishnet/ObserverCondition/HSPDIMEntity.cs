using FishNet.Object;
using Framework;
using Sirenix.Utilities;
using System.Linq;
using TMPro;
using UnityEngine;
namespace HSPDIMAlgo
{
    public class HSPDIMEntity : NetworkBehaviour
    {
        public Vector3Bool Modified = new(true, true, false);
        [SerializeField] Vector3 subRange;
        [SerializeField] Vector3 upRange;
        public Range SubRange;
        public Range UpRange;
        [SerializeField] TextMeshPro intersectText;
        [SerializeField] GameObject upIntersectRect;
        [SerializeField] GameObject subIntersectRect;
        public Collider[] UpBoxCol;
        public Collider[] SubBoxCol;
        [SerializeField] BoxCollider UpBox;
        [SerializeField] BoxCollider SubBox;
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (UpRange == null)
            {
                upIntersectRect.SetActive(true);
                upIntersectRect.transform.localScale = new Vector3(upRange.x, upRange.z, 0);
                UpBox.size = new Vector3(upRange.x, 1, upRange.z);
                if (subRange != Vector3.zero)
                {
                    subIntersectRect.SetActive(true);
                    intersectText.gameObject.SetActive(true);
                    intersectText.text = "0";
                    subIntersectRect.transform.localScale = new Vector3(subRange.x, subRange.z, 0);
                    SubBox.size = new Vector3(subRange.x, 1, subRange.z);
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
            intersectText.text = $"{SubBoxCol.Count() - 1}:{(SubRange.intersection.DefaultIfEmpty().Count() - 1)}";
            var miss = SubBoxCol.Select(c => c.GetComponentInParent<HSPDIMEntity>().UpRange).Where(r => !SubRange.intersection.Contains(r));
            var redundant = SubRange.intersection.Where(r => !SubBoxCol.Select(c => c.GetComponentInParent<HSPDIMEntity>().UpRange).Contains(r));
            if (miss.Count() > 0 || redundant.Count() > 0)
            {
                PDebug.Log($"{SubRange}\nRange miss:{string.Join(",", miss.Select(r => r))}\nRange redundant:{string.Join(",", redundant.Select(r => r))} \n");
                Time.timeScale = 0;
            }
        }

        protected virtual void Update()
        {
            if (IsServerInitialized && HSPDIM.UpdateInterval() && HSPDIM.Instance.isRunning)
            {
                Modified = new(true, true, false);
                HSPDIM.Instance.upRanges.Add(UpRange);
                if (subRange != Vector3.zero)
                {
                    HSPDIM.Instance.subRanges.Add(SubRange);
                }
                for (int i = 0; i < HSPDIM.dimension; i++)
                {
                    UpRange.overlapSets[i].Clear();
                    SubRange?.overlapSets[i].Clear();
                }
                PDebug.Log("overlapset clear");

                ////unity physic
                SubBoxCol = Physics.OverlapBox(transform.position, subRange / 2, Quaternion.identity, LayerMask.GetMask("HSPDIMUp"));

            }
        }

        protected void LateUpdate()
        {
            if (IsServerInitialized && HSPDIM.UpdateInterval() && HSPDIM.Instance.isRunning)
            {
                //SubRange?.UpdateIntersection();
            }
        }
    }
}