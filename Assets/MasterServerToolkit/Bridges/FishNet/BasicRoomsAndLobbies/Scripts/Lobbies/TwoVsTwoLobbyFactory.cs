using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace Assets.App.Scripts.Lobbies
{
    internal class TwoVsTwoLobbyFactory : ILobbyFactory
    {
        public string Id => "TwoVsTwo";
        public static string DefaultName = "TwoVsTwo Lobby";
        private LobbiesModule _module;

        public TwoVsTwoLobbyFactory(LobbiesModule module)
        {
            _module = module;
        }

        public ILobby CreateLobby(MstProperties options, IPeer creator)
        {
            var properties = options.ToDictionary();

            // Create the teams
            var teamA = new LobbyTeam("Team Blue")
            {
                MaxPlayers = 2,
                MinPlayers = 1
            };
            var teamB = new LobbyTeam("Team Red")
            {
                MaxPlayers = 2,
                MinPlayers = 1
            };

            // Set their colors
            teamA.SetProperty("color", "0000FF");
            teamB.SetProperty("color", "FF0000");

            var config = new LobbyConfig();
            config.PlayAgainEnabled = true; // If this is true, then the lobby state will not go into "LobbyState.GameOver", instead it will go into "LobbyState.Preparations". I'm not sure what I really should use at this time though...

            // Create the lobby
            var lobby = new BaseLobby(_module.NextLobbyId(), new[] { teamA, teamB }, _module, config)
            {
                Name = ExtractLobbyName(properties)
            };

            // Override properties with what user provided
            lobby.SetLobbyProperties(properties);

            // Add control for the game speed
            lobby.AddControl(new LobbyPropertyData()
            {
                Label = "Game Speed",
                Options = new List<string>() { "1x", "2x", "3x" },
                PropertyKey = "speed"
            }, "2x"); // Default option

            // Add control to enable/disable gravity
            lobby.AddControl(new LobbyPropertyData()
            {
                Label = "Gravity",
                Options = new List<string>() { "On", "Off" },
                PropertyKey = "gravity",
            });

            return lobby;
        }

        public static string ExtractLobbyName(Dictionary<string, string> properties)
        {
            return properties.ContainsKey(MstDictKeys.LOBBY_NAME) ? properties[MstDictKeys.LOBBY_NAME] : DefaultName;
        }
    }
}