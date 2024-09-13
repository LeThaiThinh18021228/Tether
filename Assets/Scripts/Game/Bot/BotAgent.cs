using Bot;
using FishNet;
using Sirenix.Utilities;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
public class BotAgent : Agent
{
    const int defaultNumberOfPlayer = 10;
    const int defaultNumberOfBot = 100;
    const int defaultNumberOfLink = 1000;
    [SerializeField] Player player;
    BehaviorParameters behaviorParameters;
    protected override void OnEnable()
    {
        base.OnEnable();
        behaviorParameters = GetComponent<BehaviorParameters>();
#if !UNITY_SERVER
        behaviorParameters.BrainParameters.VectorObservationSize = 0;
#endif
        player.OnElectrocute += BotPlayer_OnElectrocute;
        player.OnCollectCurrency += Player_OnCollectCurrency;
    }
    protected override void OnDisable()
    {
        base.OnDisable();
        player.OnElectrocute -= BotPlayer_OnElectrocute;
        player.OnCollectCurrency -= Player_OnCollectCurrency;
    }

    private void Player_OnCollectCurrency(int value)
    {
        AddReward(value / 100f);
    }

    private void BotPlayer_OnElectrocute(Player player2)
    {
        AddReward(1);
        player2.GetComponent<BotAgent>().AddReward(-1);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!InstanceFinder.IsHostStarted)
        {
            if (InstanceFinder.IsOffline)
            {
#if !UNITY_SERVER
                return;
#endif
            }
        }
        if (player.IsBot) sensor.AddObservation(((BotPlayer)player).Id);
        else sensor.AddObservation(player.OwnerId);
        sensor.AddObservation(player.IsBot);

        var chars = GameManager.Instance.RoomServerManager.Characters;
        var bots = GameManager.Instance.BotManager.bots;
        var currencies = GameManager.Instance.CurrencyGenerator.currencies.Value;
        bots.ForEach((bot) =>
        {
            sensor.AddObservation(bot.Id);
            sensor.AddObservation(1);
            sensor.AddObservation((int)bot.StateInput.Value);
            sensor.AddObservation(bot.transform.position);
            sensor.AddObservation(bot.Data.Currency.Value);
        });
        for (int i = 0; i < defaultNumberOfBot - bots.Count; i++)
        {
            sensor.AddObservation(-1);
            sensor.AddObservation(-1);
            sensor.AddObservation(-1);
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0);
        }
        chars.Select(player => (Player)player.Value).ForEach((player) =>
        {
            sensor.AddObservation(player.OwnerId);
            sensor.AddObservation(0);
            sensor.AddObservation((int)player.StateInput.Value);
            sensor.AddObservation(player.transform.position);
            sensor.AddObservation(player.Data.Currency.Value);
        });
        for (int i = 0; i < defaultNumberOfPlayer - chars.Count; i++)
        {
            sensor.AddObservation(-1);
            sensor.AddObservation(-1);
            sensor.AddObservation(-1);
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0);
        }
        //currency
        for (int i = 0; i < currencies.Count; i++)
        {
            sensor.AddObservation(currencies[i].transform.position);
        }
        // ward 
        int linkCount = 0;
        bots.ForEach((bot) =>
        {
            bot.Links.ForEach((link) =>
            {
                linkCount++;
                sensor.AddObservation(((BotPlayer)link.Player).Id);
                sensor.AddObservation(1);
                sensor.AddObservation(link.wardEnd.transform.position);
                sensor.AddObservation(link.wardStart.transform.position);
                sensor.AddObservation(link.CurTime);
                sensor.AddObservation(link.Duration);
            });
        });
        chars.Select(player => (Player)player.Value).ForEach((player) =>
        {
            player.Links.ForEach((link) =>
            {
                linkCount++;
                sensor.AddObservation(link.wardEnd.OwnerId); //Id
                sensor.AddObservation(0); // IsBot
                sensor.AddObservation(link.wardEnd.transform.position);
                sensor.AddObservation(link.wardStart.transform.position);
                sensor.AddObservation(link.CurTime);
                sensor.AddObservation(link.Duration);
            });
        });
        for (int i = 0; i < defaultNumberOfLink - linkCount; i++)
        {
            sensor.AddObservation(-1);
            sensor.AddObservation(-1);
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(-1);
            sensor.AddObservation(-1);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        if (player.IsOwner && !player.IsBot)
        {
            Vector3 dir = Movable.DirInput();
            var cA = actionsOut.ContinuousActions;
            var dA = actionsOut.DiscreteActions;
            cA[0] = dir.x;
            cA[1] = dir.z;
            dA[0] = 0;
            if (player.StateInput.Value == PlayerInputState.NONE)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                    dA[0] = 1;
            }
            else if (player.StateInput.Value == PlayerInputState.WARDING)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    dA[0] = 1;
                }
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    dA[0] = 2;
                }
            }
        }
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        if (player.IsBot && player.HasAuthority)
        {
            Debug.Log($"{actions.ContinuousActions[0]}_{actions.ContinuousActions[1]}_{actions.DiscreteActions[0]}");
            player.Movable.Dir.Value = new Vector3(actions.ContinuousActions[0], 0, actions.ContinuousActions[1]);
            if (actions.DiscreteActions[0] == 1)
            {
                player.PlaceWard();
            }
            else if (actions.DiscreteActions[0] == 2)
            {
                player.UnplaceWard();
            }
        }
    }
}
