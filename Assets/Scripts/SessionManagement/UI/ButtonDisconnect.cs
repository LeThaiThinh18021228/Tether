using MasterServerToolkit.Bridges.FishNetworking;
using Utilities;

public class ButtonDisconnect : ButtonBase
{
    protected override void Button_OnClicked()
    {
        base.Button_OnClicked();
        RoomClientManager.Disconnect();
    }
}
