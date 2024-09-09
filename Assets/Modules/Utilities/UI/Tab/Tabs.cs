using Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Utilities
{
    public class Tabs : CacheMonoBehaviour
    {
        protected List<Button> tabs;
        [SerializeField] protected GameObject rootContent;
        protected List<GameObject> contents;
        protected GameObject activeContent;
        protected ObservableDataFull<int> activeIndex;
        private void Awake()
        {
            contents = new List<GameObject>();
            tabs = GetComponentsInChildren<Button>().ToList();
            for (int i = 0; i < rootContent.transform.childCount; i++)
            {
                contents.Add(rootContent.transform.GetChild(i).gameObject);
                contents[i].SetActive(false);
            }
            activeIndex = new ObservableDataFull<int>(-1);
            activeIndex.OnChanged += (oldIndex, newIndex) =>
            {
                InactiveTab(oldIndex);
                ActiveTab(newIndex);
            };
            for (int i = 0; i < tabs.Count; i++)
            {
                int _i = i;
                tabs[i].onClick.AddListener(() =>
                {
                    activeIndex.Value = _i;
                });
            }

            Activate(0);
        }

        protected virtual void InactiveTab(int i)
        {
            activeContent?.SetActive(false);
        }
        protected virtual void ActiveTab(int i)
        {
            activeContent = contents[i];
            activeContent.SetActive(true);
        }
        public void Activate(int i)
        {
            activeIndex.Value = i;
        }
    }
}
