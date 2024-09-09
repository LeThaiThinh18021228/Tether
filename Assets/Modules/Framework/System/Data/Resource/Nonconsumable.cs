using System.Collections.Generic;

namespace Framework
{
    public class Nonconsumable : ResourceUnit, Equipable<Dictionary<int, int>, KeyValuePair<int, int>>
    {
        public ObservableHashset<int> Value { get; set; }
        public Dictionary<int, int> InUseItems { get; set; }

        public override void Add(int value)
        {
            Value.Add(value);
        }

        public override void Subtract(int value)
        {
            Value.Remove(value);
        }

        public override bool IsAffordable(int cost)
        {
            return Value.Value.Contains(cost);
        }

        public void Use(KeyValuePair<int, int> value)
        {
            throw new System.NotImplementedException();
        }

        public override bool IsAffordable(IEnumerable<int> costs)
        {
            List<int> list = new List<int>();
            list.AddRange(Value.Value);
            foreach (int cost in costs)
            {
                if (!list.Remove(cost))
                {
                    return false;
                }
            }
            return true;
        }
    }

}
