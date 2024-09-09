using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Bridges
{
    public class MatchmakingBehaviour : BaseClientBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Time to wait before match creation process will be aborted
        /// </summary>
        [SerializeField, Tooltip("Time to wait before match creation process will be aborted")]
        protected uint matchCreationTimeout = 30;
        public UnityEvent OnRoomStartedEvent;
        public UnityEvent OnRoomStartFailedEvent;
        #endregion
        private static MatchmakingBehaviour _instance;
        /// <summary>
        /// Properties that will be synced from room to all users
        /// </summary>
        public MstProperties CustomSpawnOptions { get; private set; }

        public static MatchmakingBehaviour Instance
        {
            get
            {
                if (!_instance) Logs.Error("Instance of MatchmakingBehaviour is not found");
                return _instance;
            }
        }

        protected override void Awake()
        {
            if (_instance)
            {
                Destroy(_instance.gameObject);
                return;
            }
            _instance = this;
            base.Awake();
        }

        protected override void OnInitialize()
        {

        }

        /// <summary>
        /// Tries to get access to room
        /// </summary>
        /// <param name="gameInfo"></param>
        /// <param name="password"></param>
        protected virtual void GetAccess(int id, string password = "")
        {
            Mst.Client.Rooms.GetAccess(id, password, (access, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    Logger.Error(error);
                }
            });
        }

        /// <summary>
        /// Sends request to master server to start new room process
        /// </summary>
        /// <param name="spawnOptions"></param>
        public virtual void CreateNewRoom(MstProperties spawnOptions, MstProperties customSpawnOptions, string region = null, string password = null, UnityAction failCallback = null)
        {
            CustomSpawnOptions = customSpawnOptions;
            Mst.Client.Spawners.RequestSpawn(spawnOptions, customSpawnOptions, region, (controller, error) =>
            {
                if (controller == null)
                {
                    Debug.LogError(error);
                    return;
                }
                controller.OnStatusChangedEvent += Controller_OnStatusChangedEvent;
                MstTimer.WaitWhile(() =>
                {
                    return controller.Status != SpawnStatus.Finalized;
                }, (isSuccess) =>
                {
                    controller.OnStatusChangedEvent -= Controller_OnStatusChangedEvent;
                    if (!isSuccess)
                    {
                        Mst.Client.Spawners.AbortSpawn(controller.SpawnTaskId);
                        OnRoomStartFailed();
                        OnRoomStartFailedEvent?.Invoke();
                        Debug.LogError("Failed spawn new room. Time is up!");
                        return;
                    }
                    MstTimer.WaitForSeconds(0.2f, () =>
                    {
                        Mst.Client.Matchmaker.FindGames((games) =>
                        {
                            if (games.Count == 0)
                            {
                                Logger.Error("No games found");
                                OnRoomStartFailed();
                                OnRoomStartFailedEvent?.Invoke();
                                return;
                            }
                            Mst.Client.Rooms.GetAccess(games.Last().Id, password, (access, getAccessError) =>
                            {
                                if (!string.IsNullOrEmpty(getAccessError))
                                {
                                    Logger.Error(getAccessError);
                                    OnRoomStartFailed();
                                    OnRoomStartFailedEvent?.Invoke();
                                    RoomClientManager.Instance.StartDisconnection();
                                }
                                else
                                {
                                    Debug.Log("You have successfully spawned new room");
                                    OnRoomStarted();
                                    OnRoomStartedEvent?.Invoke();
                                }
                            });
                        });
                    });

                }, matchCreationTimeout);
            });
        }

        private void Controller_OnStatusChangedEvent(SpawnStatus status)
        {
            switch (status)
            {
                case SpawnStatus.Finalized:
                case SpawnStatus.Killed:
                case SpawnStatus.Aborted:
                    break;
            }
        }

        protected virtual void OnRoomStarted() { }

        protected virtual void OnRoomStartFailed() { }

        /// <summary>
        /// Sends request to master server to start new room process
        /// </summary>
        /// <param name="spawnOptions"></param>
        public virtual void CreateNewRoom(MstProperties spawnOptions)
        {
            CreateNewRoom(spawnOptions);
        }

        /// <summary>
        /// Starts given match
        /// </summary>
        /// <param name="gameInfo"></param>
        public virtual void StartMatch(GameInfoPacket gameInfo, Action OnPasswordNeeded = null)
        {
            // Save room Id in buffer, may be very helpful
            Mst.Options.Set(MstDictKeys.ROOM_ID, gameInfo.Id);
            // Save max players to buffer, may be very helpful
            Mst.Options.Set(Mst.Args.Names.RoomMaxConnections, gameInfo.MaxPlayers);

            if (gameInfo.IsPasswordProtected)
            {
                OnPasswordNeeded?.Invoke();
            }
            else
            {
                GetAccess(gameInfo.Id);
            }
        }
    }
}