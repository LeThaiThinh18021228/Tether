using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Framework;
using Framework.FishNet;
using MasterServerToolkit.Bridges.FishNetworking.Character;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;
public enum PlayerInputState
{
    NONE,
    WARDING,
}
public partial class Player : PlayerCharacter
{
    private static Player _playerOwner;
    public static Player PlayerOwner
    {
        get
        {
            if (_playerOwner == null)
                _playerOwner = InstanceFinder.ClientManager.Connection.FirstObject.GetComponent<Player>();
            return _playerOwner;
        }
    }

    public PlayerData Data;

    [ShowInInspector] public readonly SyncVar<PlayerInputState> StateInput = new(PlayerInputState.NONE);
    public StateMachine<PlayerInputState> InputStateMachine = new();
    readonly SyncVar<Vector3> ward1Pos = new();
    readonly SyncVar<Vector3> ward2Pos = new();

    public readonly SyncVar<int> CurrencyAwait = new(0);
    public const float distanceToCurrency = 10f;
    public Movable Movable { get; private set; }
    [SerializeField] Animator animator;
    public List<Link> Links = new();

    public event Action<Player> OnElectrocute;
    public event Action<int> OnCollectCurrency;
    #region Networkbehavior
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
        InputStateMachine.AddDelegate(PlayerInputState.NONE, null, Player_NONE_Update_Server, null);
        InputStateMachine.AddDelegate(PlayerInputState.WARDING, Player_WARDING_Start_Server, Player_WARDING_Update_Server, Player_WARDING_End_Server);
        InputStateMachine.CurrentState = PlayerInputState.NONE;
        Movable.Dir.OnChange += Dir_OnChangeServer;
        StateInput.OnChange += StateInput_OnChangeServer;
        ward1Pos.OnChange += Ward1Pos_OnChangeServer;
        ward2Pos.OnChange += Ward2Pos_OnChangeServer;
        if (!Data)
        {
            Data = gameObject.AddComponent<PlayerData>();
        }
        Data.Init();
    }
    public override void OnStopServer()
    {
        base.OnStopServer();
        Movable.Dir.OnChange -= Dir_OnChangeServer;
        StateInput.OnChange -= StateInput_OnChangeServer;
        ward1Pos.OnChange -= Ward1Pos_OnChangeServer;
        ward2Pos.OnChange -= Ward2Pos_OnChangeServer;
    }
    protected override void Awake()
    {
        base.Awake();
        Movable = GetComponent<Movable>();
    }
    protected void Start()
    {
        Movable.SetSteerTransform(body.transform);
    }
    protected virtual void Update()
    {
        InputStateMachine.Update();
    }
    #endregion
    #region Event
    protected virtual void StateInput_OnChangeServer(PlayerInputState prev, PlayerInputState next, bool asServer)
    {
        if (!asServer) return;
        InputStateMachine.CurrentState = next;
    }
    protected virtual void Ward1Pos_OnChangeServer(Vector3 prev, Vector3 next, bool asServer)
    {
        if (!asServer) return;
    }
    protected virtual void Ward2Pos_OnChangeServer(Vector3 prev, Vector3 next, bool asServer)
    {
        if (!asServer) return;

    }
    protected virtual void Dir_OnChangeServer(Vector3 prev, Vector3 next, bool asServer)
    {
        if (!asServer) return;
        if (next != Vector3.zero)
        {
            animator.SetFloat("speed", 1);
        }
        else
        {
            animator.SetFloat("speed", 0);
        }
    }
    #endregion
    #region StateMachine
    protected virtual void Player_NONE_Update_Server()
    {

    }
    protected virtual void Player_WARDING_Start_Server()
    {
    }
    protected virtual void Player_WARDING_Update_Server()
    {
        if (StateInput.Value != PlayerInputState.WARDING) return;
        if (Movable.Dir.Value != Vector3.zero)
        {
            Vector3 dis = transform.position - ward1Pos.Value;
            if (dis.magnitude > Data.Currency.Value / distanceToCurrency)
            {
                CurrencyAwait.Value = Data.Currency.Value;
            }
            else
            {
                CurrencyAwait.Value = (int)(dis.magnitude * distanceToCurrency);
            }
            ward2Pos.Value = ward1Pos.Value + dis.normalized * CurrencyAwait.Value / distanceToCurrency;
        }
    }
    protected virtual void Player_WARDING_End_Server()
    {

    }

    #endregion
    #region Public
    [Server(Logging = FishNet.Managing.Logging.LoggingType.Off)]
    public void Electrocute(Player player)
    {
        player.Data.AddCurrrency(-100);
        OnElectrocute(player);
    }

    public void CollectCurrency(int value)
    {
        Data.AddCurrrency(value);
        OnCollectCurrency?.Invoke(value);
    }
    #endregion
    #region Private
    [ServerRpc(RunLocally = true)]
    [Server]
    public void PlaceWardRPC()
    {
        PlaceWard();
    }
    [ServerRpc(RunLocally = true)]
    public void UnplaceWardRPC()
    {
        UnplaceWard();
    }
    [Server]
    public void UnplaceWard()
    {
        StateInput.Value = PlayerInputState.NONE;
        UnplaceWardObserver();
    }
    [Server]
    public void PlaceWard()
    {
        if (StateInput.Value == PlayerInputState.NONE)
        {
            StateInput.Value = PlayerInputState.WARDING;
            ward1Pos.Value = transform.position;
        }
        else if (StateInput.Value == PlayerInputState.WARDING)
        {
            CreateWardLink();
            StateInput.Value = PlayerInputState.NONE;
        }
    }
    [Server]
    protected void CreateWardLink()
    {
        if (HasAuthority)
        {
            Vector3 dis = transform.position - ward1Pos.Value;

            Ward nob1 = PrefabFactory.Ward.InstantiateNetworked<Ward>(Owner, GameManager.Instance.WardRoot.transform, ward1Pos.Value);
            Ward nob2 = PrefabFactory.Ward.InstantiateNetworked<Ward>(Owner, GameManager.Instance.WardRoot.transform, ward1Pos.Value + dis.normalized * CurrencyAwait.Value / distanceToCurrency);
            Links.Add(nob1.CreateLink(nob2, this));
            Data.AddCurrrency(-CurrencyAwait.Value);
            CurrencyAwait.Value = 0;
        }
    }

    #endregion
}
