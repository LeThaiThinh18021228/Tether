using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace Framework.HSPDIMAlgo
{
    public class HSPDIMTest : SingletonMono<HSPDIMTest>
    {
        public static List<float> sizes = new();
        public static List<int> counts = new();
        [SerializeField] private float _alpha; public static float alpha { get { return Instance._alpha; } set { Instance._alpha = value; } }
        [SerializeField] private int _countRange; public static int countRange { get { return Instance._countRange; } set { Instance._countRange = value; } }
        [SerializeField] private int _threadCount; public static int threadCount { get { return Instance._threadCount; } set { Instance._threadCount = value; } }
        [SerializeField] private float _modifyRatio; public static float modifyRatio { get { return Instance._modifyRatio; } set { Instance._modifyRatio = value; } }
        [SerializeField] private float _mapWidth; public static float mapWidth { get { return Instance._mapWidth; } set { Instance._mapWidth = value; } }
        [SerializeField] private float _mapHeight; public static float mapHeight { get { return Instance._mapHeight; } set { Instance._mapHeight = value; } }
        public bool alphaActive;
        public int sizeVariant;
        public float size;
        public float preallocateExp;

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
                float size = (mapWidth * mapHeight) * alpha;
                int totalCountPortion = (int)(((long)Mathf.Pow(4, sizeVariant) - 1) / (4 - 1));
                int countPerSize = countRange / sizeVariant;
                for (int i = 0; i < sizeVariant; i++)
                {
                    sizes.Add(Mathf.Sqrt(size * (long)Mathf.Pow(4, i) / totalCountPortion / countPerSize));
                    if (i == sizeVariant - 1 & sizeVariant > 1)
                    {
                        counts.Add(countRange - i * countPerSize);
                    }
                    else
                    {
                        counts.Add(countPerSize);
                    }
                }
            }
            else
            {
                sizes.Add(size);
            }
            int preallocateHash = (int)Mathf.Pow(countRange, preallocateExp);
            Debug.Log($"range value:{string.Join(",", sizes)} preallocateHash {preallocateHash}");
            HSPDIM.minEntitySubRegSize = HSPDIM.minEntityUpRegSize = HSPDIMTest.sizes[^1];
            HSPDIM.entityCountEstimate = HSPDIMTest.countRange;
            HSPDIM.upTreeDepth = HSPDIM.DepthCal(HSPDIM.minEntityUpRegSize);
            HSPDIM.subTreeDepth = HSPDIM.DepthCal(HSPDIM.minEntitySubRegSize);
            int offset = 0;
            for (int i = 0; i < counts.Count; i++)
            {
                float size = sizes[i];
                for (int j = 0; j < counts[i]; j++)
                {
                    entityTests[offset] = new HSPDIMEntityTest(offset, offset % 2 == 0, new Vector3(size, size, size), preallocateHash);
                    offset++;
                }
            }


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
                    if (UnityEngine.Random.Range(0f, 1f) < modifyRatio)
                    {
                        entityTests[i].ChangePos();
                    }
                }
            }
        }
        protected override void OnDestroy()
        {
            base.OnDestroy();
            HSPDIM.Instance.Dispose();
        }
    }

}
