using FishNet.Object;
using System.Linq;
using System.Threading;
using TMPro;
using Unity.Collections;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
namespace Framework.HSPDIMAlgo
{
    public interface IHSPDIMEntity
    {
        public int Id { get; set; }
        //public int OldId { get; set; }
        public bool Enable { get; set; }
        public Vector3 Position { get; set; }
        public Vector3Bool Modified { get; set; }
        public Vector3 subRange { get; set; }
        public Vector3 upRange { get; set; }
        public HSPDIMRange SubRange { get; set; }
        public HSPDIMRange UpRange { get; set; }
        public void UpdatePos();
    }
    public class HSPDIMEntity : NetworkBehaviour, IHSPDIMEntity
    {
        public int Id { get; set; }
        //public int OldId { get; set; }
        public bool Enable { get; set; }
        public Vector3 Position { get; set; }
        public Vector3Bool Modified { get; set; }
        [SerializeField] private Vector3 _subrange; public Vector3 subRange { get { return _subrange; } set { _subrange = value; } }
        [SerializeField] private Vector3 _upRange; public Vector3 upRange { get { return _upRange; } set { _upRange = value; } }
        public HSPDIMRange SubRange { get; set; }
        public HSPDIMRange UpRange { get; set; }

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
                if (upRange != Vector3.zero)
                {
                    upIntersectRect.SetActive(true);
                    upIntersectRect.transform.localScale = new Vector3(upRange.x, upRange.z, 0);
                    UpBox.size = new Vector3(upRange.x, 1, upRange.z);
                }
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
                Id = ObjectId;
                //OldId = Id;
                if (UpRange == null)
                {
                    Modified = Vector3Bool.@true;
                    if (upRange != Vector3.zero)
                    {
                        UpRange = new(upRange, this, HSPDIM.upTreeDepth, false);
                        HSPDIM.Instance.upRanges.Add(UpRange);
                    }
                    if (subRange != Vector3.zero)
                    {
                        SubRange = new(subRange, this, HSPDIM.subTreeDepth, true);
                        HSPDIM.Instance.subRanges.Add(SubRange);
                        SubRange.OnUpdateIntersection += OnUpdateIntersection;
                    }
                }
            }
        }
        public override void OnStartServer()
        {
            base.OnStartServer();
            Id = ObjectId;
            //PDebug.Log("Remove "+ OldId + " Add " + Id);
            Enable = true;
            HSPDIM.Instance.HSPDIMEntities.Add(Id, this);
            Modified = Vector3Bool.@true;
            if (upRange != Vector3.zero)
            {
                HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
            }
            if (subRange != Vector3.zero)
            {
                HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
            }
        }
        public override void OnStopServer()
        {
            base.OnStopServer();
            Enable = false;
            //PDebug.Log("Remove " + Id);
            if (HSPDIM.Instance!=null && HSPDIM.Instance.isRunning)
            {
                HSPDIM.Instance.RemovedEntities.Add(Id);
                Modified = Vector3Bool.@true;
                if (upRange != Vector3.zero)
                {
                    HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
                }
                if (subRange != Vector3.zero)
                {
                    HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
                }
            }
        }
        private void OnUpdateIntersection()
        {
            if (!IsServerInitialized && HSPDIM.Instance.debugId) return;
            SubBoxCol = Physics.OverlapBox(transform.position, subRange / 2, Quaternion.identity, LayerMask.GetMask("HSPDIMUp"));
            //intersectText.text = $"{(SubRange.intersection.DefaultIfEmpty().Count() - 1)}";
            //intersectText.text = $"{SubBoxCol?.Count() - 1}:{(HSPDIM.Instance.FinalMatchingResult[ObjectId].Count - 1)}";
            intersectText.text = $"{SubBoxCol?.Count() - 1}:{SubRange.intersectionId.Count - 1}";
            //var miss = SubBoxCol?.Select(c => c.GetComponentInParent<HSPDIMEntity>().UpRange).Where(r => !SubRange.intersection.Contains(r.entity.ObjectId) && r.entity.IsServerInitialized
            //&& Mathf.Abs(r.Bounds[0, 0].boundValue - SubRange.Bounds[0, 1].boundValue) > 0.4f
            //&& Mathf.Abs(r.Bounds[1, 0].boundValue - SubRange.Bounds[1, 1].boundValue) > 0.4f
            //&& Mathf.Abs(r.Bounds[0, 1].boundValue - SubRange.Bounds[0, 0].boundValue) > 0.4f
            //&& Mathf.Abs(r.Bounds[1, 1].boundValue - SubRange.Bounds[1, 0].boundValue) > 0.4f
            //);
            //var redundant = SubRange.intersection.Select(i => HSPDIM.Instance.HSPDIMEntities[i].UpRange).Where(r => !SubBoxCol.Select(c => c.GetComponentInParent<HSPDIMEntity>().UpRange).Contains(r) && r.entity.IsServerInitialized
            //&& Mathf.Abs(r.Bounds[0, 0].boundValue - SubRange.Bounds[0, 1].boundValue) > 0.4f
            //&& Mathf.Abs(r.Bounds[1, 0].boundValue - SubRange.Bounds[1, 1].boundValue) > 0.4f
            //&& Mathf.Abs(r.Bounds[0, 1].boundValue - SubRange.Bounds[0, 0].boundValue) > 0.4f
            //&& Mathf.Abs(r.Bounds[1, 1].boundValue - SubRange.Bounds[1, 0].boundValue) > 0.4f
            //);
            //if (miss.Count() > 0 || redundant.Count() > 0)
            //{
            //    //PDebug.Log($"{SubRange}\nRange miss:\n{string.Join(",", miss.Select(r => r))}\nRange redundant:\n{string.Join("\n", redundant.Select(r => r))} \n{string.Join("\n", SubRange.overlapSets.Select((rs, i) => $"Dimension {i}:\n" + string.Join("\n", rs.Select(r => r))))}");
            //    //Time.timeScale = 0;
            //}
        }

        public void UpdatePos()
        {
            Position = new Vector3(transform.position.x, transform.position.z); ;
        }
    }
}