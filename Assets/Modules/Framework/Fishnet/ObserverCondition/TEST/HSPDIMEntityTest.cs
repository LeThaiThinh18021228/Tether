using Framework.HSPDIMAlgo;
using UnityEngine;

public class HSPDIMEntityTest : IHSPDIMEntity
{
    public int Id { get; set; }
    public bool Enable { get; set; }
    public Vector3 Position { get; set; }
    public Vector3Bool Modified { get; set; }
    public Vector3 subRange { get; set; } = Vector3.zero;
    public Vector3 upRange { get; set; } = Vector3.zero;
    public HSPDIMRange SubRange { get; set; }
    public HSPDIMRange UpRange { get; set; }
    public HSPDIMEntityTest(int id, bool IsUp, Vector3 range)
    {
        Enable = true;
        Id = id;
        Position = new Vector3(Random.Range(0, -HSPDIMTest.mapWidth / 2), 0, Random.Range(0, HSPDIMTest.mapWidth / 2));
        Modified = Vector3Bool.@true;
        if (IsUp)
        {
            upRange = range;
            UpRange = new(range, this, HSPDIM.upTreeDepth);
            HSPDIM.Instance.upRanges.Add(UpRange);
            HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
        }
        else
        {
            subRange = range;
            SubRange = new(range, this, HSPDIM.subTreeDepth);
            HSPDIM.Instance.subRanges.Add(SubRange);
            HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
        }
    }
    private void OnUpdateIntersection()
    {

    }

    public void ChangePos()
    {
        bool modify = Random.Range(0, 1f) < HSPDIMTest.modifyRatio;
        Modified = new(modify, modify, false);
        if (modify)
        {
            Position = new Vector3(Random.Range(0, -HSPDIMTest.mapWidth / 2), 0, Random.Range(0, HSPDIMTest.mapWidth / 2));
            if (subRange != Vector3.zero)
            {
                HSPDIM.Instance.modifiedSubRanges.Add(SubRange);
            }
            if (upRange != Vector3.zero)
            {
                HSPDIM.Instance.modifiedUpRanges.Add(UpRange);
            }
        }

    }
    public void UpdatePos()
    {

    }

    public override string ToString()
    {
        return $"{Id}_{Position}_{Modified}";
    }

}
