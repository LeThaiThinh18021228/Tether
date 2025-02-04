using Framework.HSPDIMAlgo;
using Framework.SimpleJSON;
using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    public class HSPDIMSortListTreeNodeData
    {
        public List<HSPDIMBound> insides = new();
        public List<HSPDIMBound> crosses = new();
        public override string ToString()
        {
            JSONNode tree = new JSONClass();
            if (crosses.Count > 0)
            {
                JSONArray c = new JSONArray();
                crosses.ForEach(x => c.Add(new JSONData(x.boundValue)));
                tree.Add("cr", c);
            }
            if (insides.Count > 0)
            {
                JSONArray i = new JSONArray();
                insides.ForEach(x => i.Add(new JSONData(x.boundValue)));
                tree.Add("in", i);
            }
            return tree.ToString();
        }
        public bool IsEmpty()
        {
            return crosses.Count == 0 && insides.Count == 0;
        }
    }
}
