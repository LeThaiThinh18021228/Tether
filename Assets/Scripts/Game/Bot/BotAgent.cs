using Bot;
using FishNet;
using Sirenix.Utilities;
using System.Linq;
using UnityEngine;
public class BotAgent : MonoBehaviour
{
    const int defaultNumberOfPlayer = 10;
    const int defaultNumberOfBot = 100;
    const int defaultNumberOfLink = 1000;
    [SerializeField] Player player;
    protected void OnEnable()
    {
#if !UNITY_SERVER
        behaviorParameters.BrainParameters.VectorObservationSize = 0;
#endif
    }
    protected void OnDisable()
    {
    }
}
