using MasterServerToolkit.MasterServer;
using System.Collections.Generic;
using Utilities;

public class RoomView : CollectionViewBase<RoomInfo>
{
    private void OnEnable()
    {
        if (Mst.Connection.IsConnected)
        {
            BuildView();
        }
    }
    public override void BuildView()
    {
        Mst.Client.Matchmaker.FindGames((games) =>
        {
            List<RoomInfo> infos = new();
            foreach (var game in games)
            {
                infos.Add(new RoomInfo()
                {
                    Id = game.Id,
                    Packet = game,
                });
            }
            BuildView(infos);
        });
    }
}
