namespace MasterServerToolkit.MasterServer
{
    public partial class MstArgs
    {
        public bool IsBotClient { get; private set; }
        public int RoomBotNumner { get; private set; }
        public int GameId { get; private set; }
        public string ClientExecutablePath { get; private set; }

        void ConstructExtra()
        {
            RoomBotNumner = AsInt(Names.RoomBotNumner);
            GameId = AsInt(Names.GameId);
            ClientExecutablePath = AsString(Names.ClientExecutablePath);
            IsBotClient = AsBool(Names.IsBotClient);
        }
    }
}