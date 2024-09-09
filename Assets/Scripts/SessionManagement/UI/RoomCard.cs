using MasterServerToolkit.Bridges;
using MasterServerToolkit.MasterServer;
using TMPro;
using UnityEngine;
using Utilities;

public class RoomInfo : IDataUnit<RoomInfo>
{
    public GameInfoPacket Packet { get; set; }
    public int Id { get; set; }
}
public class RoomCard : ButtonCardBase<RoomInfo>
{
    [SerializeField] TextMeshProUGUI id;
    [SerializeField] TextMeshProUGUI name;
    [SerializeField] TextMeshProUGUI address;
    [SerializeField] TextMeshProUGUI region;
    [SerializeField] TextMeshProUGUI isPasswordProtected;
    [SerializeField] TextMeshProUGUI onlinePlayersByMax;

    public override void BuildView(RoomInfo info)
    {
        base.BuildView(info);
        id.text = info.Packet.Id.ToString();
        name.text = info.Packet.Name.ToString();
        address.text = info.Packet.Address.ToString();
        region.text = info.Packet.Region.ToString();
        isPasswordProtected.text = info.Packet.IsPasswordProtected ? "T" : "F";
        onlinePlayersByMax.text = $"{info.Packet.OnlinePlayers.ToString()}/{info.Packet.MaxPlayers.ToString()}";
    }

    protected override void Card_OnClicked()
    {
        MatchmakingBehaviour.Instance.StartMatch(info.Packet, () => { });
    }
}
