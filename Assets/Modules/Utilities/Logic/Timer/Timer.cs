using System;
using UnityEngine;

namespace Utilities
{
    public abstract class Timee<U> : MonoBehaviour where U : Timee<U>
    {
        protected virtual void Awake()
        {
            Timer<U>.OnTrigger += OnTrigger;
            Timer<U>.OnElapse += OnElapse;
        }
        protected virtual void OnDestroy()
        {
            Timer<U>.OnTrigger -= OnTrigger;
            Timer<U>.OnElapse -= OnElapse;
        }
        protected virtual void Update()
        {
            Timer<U>.Elaspe();
        }
        protected abstract void OnTrigger();
        protected abstract void OnElapse();
    }
    public static class Timer<T> where T : Timee<T>
    {
        static private int triggerInterval_Sec; public static int TriggerInterval_Sec
        {
            get { return triggerInterval_Sec; }
            set { triggerInterval_Sec = Mathf.Clamp(value, 1, int.MaxValue); }
        }
        private static long beginPoint; public static long BeginPoint
        {
            get { return beginPoint; }
            set
            {
                beginPoint = value;
                elapsed = 0;
                MarkedPoint = value;
            }
        }
        private static long markedPoint; public static long MarkedPoint
        {
            get { return markedPoint; }
            set { markedPoint = value; }
        }

        private static float elapsed; public static float ELasped
        {
            get { return elapsed; }
            set
            {
                elapsed = value;
                if (elapsed >= 1)
                {
                    elapsed += 1;
                    if (Remain_Sec - 1 == 0)
                    {
                        OnTrigger?.Invoke();
                    }
                }
            }
        }

        public static long Elasped_Tick { get { return DateTime.UtcNow.Ticks - beginPoint; } }
        public static long Residal_Sec { get { return Elasped_Tick.ToSecond() % triggerInterval_Sec; } }
        public static long Remain_Sec { get { return Math.Clamp(TriggerInterval_Sec - Residal_Sec, 0, TriggerInterval_Sec); } }
        public static int TotalTriggers { get { return Elasped_Tick.ToSecond() / triggerInterval_Sec; } }
        public static int MarkedTriggers { get { return TotalTriggers - ((markedPoint - beginPoint).ToSecond() / triggerInterval_Sec); } }

        public static Callback OnTrigger;
        public static Callback OnElapse;

        public static void Elaspe()
        {
            ELasped += Time.deltaTime;
            OnElapse?.Invoke();
        }
        public static void Init(long? tick = null)
        {
            if (tick.HasValue)
            {
                BeginPoint = tick.Value;
            }
            else
            {
                BeginPoint = DateTime.UtcNow.Ticks;
            }
        }
        public static void Mark()
        {
            MarkedPoint = DateTime.UtcNow.Ticks;
        }
    }
}