using UnityEngine;
using UnityEngine.UI;

namespace Utilities
{
    public class TabsSwapImage : Tabs
    {
        [SerializeField] Sprite spriteActive;
        [SerializeField] Sprite spriteInactive;
        protected override void ActiveTab(int i)
        {
            base.ActiveTab(i);
            tabs[i].GetComponent<Image>().sprite = spriteActive;
        }
        protected override void InactiveTab(int i)
        {
            if (i < 0)
                return;
            base.InactiveTab(i);
            tabs[i].GetComponent<Image>().sprite = spriteInactive;
        }
    }

}
