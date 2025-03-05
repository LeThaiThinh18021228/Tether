using Framework.HSPDIMAlgo;
using UnityEngine;

public class HSPDIMEntityTest : IHSPDIMEntity
{
    public int Id { get; set; }
    public int OldId { get; set; }
    public bool Enable { get; set; }
    public Vector3 Position { get; set; }
    public Vector3Bool Modified { get; set; }
    public Vector3 subRange { get; set; } = Vector3.zero;
    public Vector3 upRange { get; set; } = Vector3.zero;
    public HSPDIMRange SubRange { get; set; }
    public HSPDIMRange UpRange { get; set; }
    public HSPDIMEntityTest(int id, bool IsSub, Vector3 range, int preallocateHash)
    {
        Enable = true;
        Id = id;
        float validPosRange = HSPDIMTest.mapWidth / 2 - range.x;
        Position = new Vector3(Random.Range(-validPosRange, validPosRange), Random.Range(-validPosRange, validPosRange));
        Modified = Vector3Bool.@true;
        HSPDIM.Instance.HSPDIMEntities.Add(Id, this);
        if (!IsSub)
        {
            upRange = range;
            UpRange = new(range, this, HSPDIM.upTreeDepth, IsSub, preallocateHash);
            HSPDIM.Instance.upRanges.Add(UpRange);
            HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
        }
        else
        {
            subRange = range;
            SubRange = new(range, this, HSPDIM.subTreeDepth, IsSub, preallocateHash);
            HSPDIM.Instance.subRanges.Add(SubRange);
            HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
        }
    }
    private void OnUpdateIntersection()
    {

    }

    public void ChangePos()
    {
        Modified = Vector3Bool.@true;
        float validPosRange = HSPDIMTest.mapWidth / 2 - (upRange.x + subRange.x) / 2;
        Position = new Vector3(Random.Range(-validPosRange, validPosRange), Random.Range(-validPosRange, validPosRange));
        if (subRange != Vector3.zero)
        {
            HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
        }
        if (upRange != Vector3.zero)
        {
            HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
        }
    }
    public void UpdatePos()
    {

    }

    public override string ToString()
    {
        return $"{Id}_{Position}_{Modified}_{upRange}_{subRange}";
    }

}
