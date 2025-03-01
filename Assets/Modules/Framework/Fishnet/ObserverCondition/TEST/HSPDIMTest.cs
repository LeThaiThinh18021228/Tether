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
        public bool alphaActive;
        public float size;

        public HSPDIMEntityTest[] entityTests;
        protected override void Awake()
        {
            base.Awake();
            entityTests = new HSPDIMEntityTest[countRange];
            JobsUtility.JobWorkerCount = threadCount;
        }
        private void Start()
        {
            if (alphaActive)
            {
                rangeValue = Mathf.Sqrt((mapWidth * mapHeight) * alpha / countRange);
            }
            else
            {
                rangeValue = size;
            }
            int preallocateHash = (int)Mathf.Pow(countRange, 0.75f);
            Debug.Log($"range value:{rangeValue} preallocateHash {preallocateHash}");
            for (int i = 0; i < countRange; i++)
            {
                entityTests[i] = new HSPDIMEntityTest(i, i < countRange / 2, new Vector3(rangeValue, 0, rangeValue), preallocateHash);
            }
            HSPDIM.minEntitySubRegSize = HSPDIM.minEntityUpRegSize = HSPDIMTest.rangeValue;
            HSPDIM.entityCountEstimate = HSPDIMTest.countRange;
            HSPDIM.Instance.InitMappingAndMatching();
        }
        // Update is called once per frame
        void Update()
        {
            if (!HSPDIM.Instance.gameObject.activeSelf) HSPDIM.Instance.gameObject.SetActive(true);
            if (HSPDIM.UpdateInterval(5) && HSPDIM.Instance.isRunning)
            {
                for (int i = 0; i < countRange; i++)
                {
                    if (Random.Range(0f, 1f) < modifyRatio)
                    {
                        entityTests[i].ChangePos();
                    }
                }
                //PDebug.Log(string.Join(",", entityTests.ForEach(e => e.ToString())));
            }
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            HSPDIM.Instance.Dispose();
        }
    }

}
