using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace Framework.HSPDIMAlgo
{
    public class HSPDIMTest : SingletonMono<HSPDIMTest>
    {
        public static float rangeValue;
        [SerializeField] private float _alpha; public static float alpha { get { return Instance._alpha; } set { Instance._alpha = value; } }
        [SerializeField] private int _countRange; public static int countRange { get { return Instance._countRange; } set { Instance._countRange = value; } }
        [SerializeField] private int _threadCount; public static int threadCount { get { return Instance._threadCount; } set { Instance._threadCount = value; } }
        [SerializeField] private float _modifyRatio; public static float modifyRatio { get { return Instance._modifyRatio; } set { Instance._modifyRatio = value; } }
        [SerializeField] private float _mapWidth; public static float mapWidth { get { return Instance._mapWidth; } set { Instance._mapWidth = value; } }
        [SerializeField] private float _mapHeight; public static float mapHeight { get { return Instance._mapHeight; } set { Instance._mapHeight = value; } }

        public HSPDIMEntityTest[] entityTests;
        private void Start()
        {
            entityTests = new HSPDIMEntityTest[countRange];
            JobsUtility.JobWorkerCount = threadCount;
            rangeValue = Mathf.Sqrt((mapWidth * mapHeight) * alpha / countRange);
            Debug.Log($"range value:{rangeValue}");
            for (int i = 0; i < countRange; i++)
            {
                entityTests[i] = new HSPDIMEntityTest(i, i < countRange / 2, new Vector3(rangeValue, 0, rangeValue));
            }
            HSPDIM.Instance.InitMappingAndMatching();
        }
        // Update is called once per frame
        void Update()
        {
            if (!HSPDIM.Instance.gameObject.activeSelf) HSPDIM.Instance.gameObject.SetActive(true);
            if (HSPDIM.UpdateInterval() && HSPDIM.Instance.isRunning)
            {
                for (int i = 0; i < countRange; i++)
                {
                    if (Random.Range(0, 1) < modifyRatio)
                    {
                        entityTests[i].ChangePos();
                    }
                }
                //PDebug.Log(string.Join(",", entityTests.ForEach(e => e.ToString())));
            }
        }
    }

}
