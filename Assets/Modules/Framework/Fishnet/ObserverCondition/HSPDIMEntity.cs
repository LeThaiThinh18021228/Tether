using FishNet.Object;
using Framework;
using System.Linq;
using TMPro;
using UnityEngine;
namespace HSPDIMAlgo
{
    public class HSPDIMEntity : NetworkBehaviour
    {
        public Vector3Bool Modified = Vector3Bool.@true;
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
                    Modified = Vector3Bool.@true;
                    UpRange = new(upRange, this, HSPDIM.upTreeDepth);
                    UpRange.UpdateRange(HSPDIM.upTreeDepth);
                    HSPDIM.Instance.upRanges.Add(UpRange);
                    Debug.Log($"Create UpRange {name}_{UpRange.GetHashCode()}");
                    if (subRange != Vector3.zero)
                    {
                        SubRange = new(subRange, this, HSPDIM.subTreeDepth);
                        SubRange.UpdateRange(HSPDIM.subTreeDepth);
                        HSPDIM.Instance.subRanges.Add(SubRange);
                        Debug.Log($"Create SupRange {name}_{SubRange.GetHashCode()}");
                        SubRange.OnUpdateIntersection += OnUpdateIntersection;
                    }
                }
            }
        }
        public override void OnStartServer()
        {
            base.OnStartServer();
            Modified = Vector3Bool.@true;
            Debug.Log($"Add HSPDIM UpRange {Modified} " + name);
            HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
            if (subRange != Vector3.zero)
            {
                Debug.Log("Add HSPDIM SupRange" + name);
                HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
            }
        }
        public override void OnStopServer()
        {
            base.OnStopServer();
            if (HSPDIM.Instance.isRunning)
            {
                Modified = Vector3Bool.@true;
                Debug.Log($"Remove HSPDIM UpRange {Modified}" + UpRange);
                HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
                if (subRange != Vector3.zero)
                {
                    Debug.Log("Remove HSPDIM SupRange" + UpRange);
                    HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
                }
            }
        }
        private void OnUpdateIntersection()
        {
            SubBoxCol = Physics.OverlapBox(transform.position, subRange / 2, Quaternion.identity, LayerMask.GetMask("HSPDIMUp"));
            //intersectText.text = $"{(SubRange.intersection.DefaultIfEmpty().Count() - 1)}";
            intersectText.text = $"{SubBoxCol?.Count() - 1}:{(SubRange.intersection.DefaultIfEmpty().Count() - 1)}";
            var miss = SubBoxCol?.Select(c => c.GetComponentInParent<HSPDIMEntity>().UpRange).Where(r => !SubRange.intersection.Contains(r) && r.entity.IsServerInitialized
            && Mathf.Abs(r.Bounds[0, 0].boundValue - SubRange.Bounds[0, 1].boundValue) > 0.4f
            && Mathf.Abs(r.Bounds[1, 0].boundValue - SubRange.Bounds[1, 1].boundValue) > 0.4f
            && Mathf.Abs(r.Bounds[0, 1].boundValue - SubRange.Bounds[0, 0].boundValue) > 0.4f
            && Mathf.Abs(r.Bounds[1, 1].boundValue - SubRange.Bounds[1, 0].boundValue) > 0.4f
            );
            var redundant = SubRange.intersection.Where(r => !SubBoxCol.Select(c => c.GetComponentInParent<HSPDIMEntity>().UpRange).Contains(r) && r.entity.IsServerInitialized
            && Mathf.Abs(r.Bounds[0, 0].boundValue - SubRange.Bounds[0, 1].boundValue) > 0.4f
            && Mathf.Abs(r.Bounds[1, 0].boundValue - SubRange.Bounds[1, 1].boundValue) > 0.4f
            && Mathf.Abs(r.Bounds[0, 1].boundValue - SubRange.Bounds[0, 0].boundValue) > 0.4f
            && Mathf.Abs(r.Bounds[1, 1].boundValue - SubRange.Bounds[1, 0].boundValue) > 0.4f
            );
            if (miss.Count() > 0 || redundant.Count() > 0)
            {
                PDebug.Log($"{SubRange}\nRange miss:\n{string.Join(",", miss.Select(r => r))}\nRange redundant:\n{string.Join("\n", redundant.Select(r => r))} \n{string.Join("\n", SubRange.overlapSets.Select((rs, i) => $"Dimension {i}:\n" + string.Join("\n", rs.Select(r => r))))}");
                Time.timeScale = 0;
            }
        }
    }
}