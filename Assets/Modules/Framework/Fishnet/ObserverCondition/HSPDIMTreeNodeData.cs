using Framework;
using Framework.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Framework.HSPDIMAlgo
{
    public class HSPDIMTreeNodeData
    {
        public float lowerBound;
        public float upperBound;
        public List<HSPDIMBound> lowers = new();
        public List<HSPDIMBound> uppers = new();
        public List<HSPDIMBound> covers = new();
        public List<HSPDIMBound> insides = new();
        public override string ToString()
        {
            JSONNode tree = new JSONClass();
            tree.Add("node", $"[{lowerBound},{upperBound}]");
            if (lowers.Count > 0)
            {
                JSONArray l = new JSONArray();
                lowers.ForEach(x => l.Add(new JSONData(x.boundValue)));
                tree.Add("l", l);
            }
            if (uppers.Count > 0)
            {
                JSONArray u = new JSONArray();
                uppers.ForEach(x => u.Add(new JSONData(x.boundValue)));
                tree.Add("u", u);
            }
            if (covers.Count > 0)
            {
                JSONArray c = new JSONArray();
                covers.ForEach(x => c.Add(new JSONData(x.boundValue)));
                tree.Add("co", c);
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
            return lowers.Count == 0 && uppers.Count == 0 && covers.Count == 0 && insides.Count == 0;
        }
    }
    
}
