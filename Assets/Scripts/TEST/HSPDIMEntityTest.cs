using Framework.HSPDIMAlgo;
using UnityEngine;

public class HSPDIMEntityTest : IHSPDIMEntity
{
    public int Id { get; set; }
    public bool Enable { get; set; }
    public Vector3 Position { get; set; }
    public Vector3Bool Modified { get; set; }
    public Vector3 subRange { get; set; }
    public Vector3 upRange { get; set; }
    public HSPDIMRange SubRange { get; set; }
    public HSPDIMRange UpRange { get; set; }
    void Init(int id)
    {
        if (UpRange == null)
        {
            Enable = true;
            Id = id;
            Modified = Vector3Bool.@true;
            UpRange = new(upRange, this, HSPDIM.upTreeDepth);
            HSPDIM.Instance.upRanges.Add(UpRange);
            if (subRange != Vector3.zero)
            {
                SubRange = new(subRange, this, HSPDIM.subTreeDepth);
                HSPDIM.Instance.subRanges.Add(SubRange);
                //SubRange.OnUpdateIntersection += OnUpdateIntersection;
            }
        }
    }
    void OnEnable()
    {
        Enable = true;
        //HSPDIM.Instance.HSPDIMEntities.Add(Id, this);
        Modified = Vector3Bool.@true;
        HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
        if (subRange != Vector3.zero)
        {
            HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
        }
    }
    void OnDisable()
    {
        Enable = false;
        //HSPDIM.Instance.HSPDIMEntities.Remove(Id);
        if (HSPDIM.Instance.isRunning)
        {
            Modified = Vector3Bool.@true;
            HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
            if (subRange != Vector3.zero)
            {
                HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
            }
        }
    }
    void Update()
    {

    }
    private void OnUpdateIntersection()
    {

    }

    public void UpdatePos()
    {
        Position = new Vector3(1, 0, 1);
        if (HSPDIM.Instance.isRunning)
        {
            Modified = Vector3Bool.@true;
            HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
            if (subRange != Vector3.zero)
            {
                HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
            }
        }
    }

}
