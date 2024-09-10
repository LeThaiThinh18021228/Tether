using Bot;
using Sirenix.Utilities;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
public class BotAgent : Agent
{
    [SerializeField] BotPlayer botPlayer;
    public override void Initialize()
    {
        base.Initialize();
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(botPlayer.transform);
        GameManager.Instance.BotManager.bots.ForEach((bot) =>
        {
            sensor.AddObservation((int)bot.StateInput.Value);
            sensor.AddObservation(bot.transform);
            bot.Links.ForEach((link) =>
            {
                sensor.AddObservation(link.wardEnd.transform);
                sensor.AddObservation(link.wardStart.transform);
            });
        });
        GameManager.Instance.RoomServerManager.Characters.Select(bot => (Player)bot.Value).ForEach((player) =>
        {
            sensor.AddObservation((int)player.StateInput.Value);
            sensor.AddObservation(player.transform);
            player.Links.ForEach((link) =>
            {
                sensor.AddObservation(link.wardEnd.transform);
                sensor.AddObservation(link.wardStart.transform);
            });
        });
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        base.Heuristic(actionsOut);
    }
    public override void OnActionReceived(ActionBuffers actions)
    {
        Debug.Log(actions.ContinuousActions[0]);
        Debug.Log(actions.ContinuousActions[1]);
        Debug.Log(actions.DiscreteActions[0]);
        botPlayer.Movable.SetDes(new Vector3(actions.ContinuousActions[0], 0, actions.ContinuousActions[1]));
        if (actions.DiscreteActions[0] == 0)
        {
            botPlayer.PlaceWardRPC();
        }
        else if (actions.ContinuousActions[0] == 1)
        {
            botPlayer.UnplaceWardRPC();
        }
    }
}
