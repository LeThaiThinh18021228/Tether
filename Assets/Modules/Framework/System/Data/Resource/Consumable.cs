using Sirenix.Utilities;
using System.Collections.Generic;

namespace Framework
{
    public class Consumable : ResourceUnit
    {
        public ObservableDataFull<int> Value { get; set; }

        public override void Add(int value)
        {
            Value.Value += value;
        }
        public override void Subtract(int value)
        {
            Value.Value -= value;
        }
        public override bool IsAffordable(int price)
        {
            return Value.Value > price;
        }

        public override bool IsAffordable(IEnumerable<int> costs)
        {
            int sumCost = 0;
            costs.ForEach(x => sumCost += x);
            if (sumCost < Value.Value)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
