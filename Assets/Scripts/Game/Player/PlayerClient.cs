using Framework;
using MasterServerToolkit.Bridges.FishNetworking.Character;
using Unity.Cinemachine;
using UnityEngine;

public partial class Player : PlayerCharacter
{
    [SerializeField] protected Renderer skin;
    [SerializeField] protected GameObject wardingRadius;
    [SerializeField] protected GameObject body;
    GameObject ward1;
    GameObject ward2;
    CinemachineCamera cineCam;
    Camera cam;
    #region Networkbehavior
    public override void OnStartClient()
    {
        base.OnStartClient();
        InputStateMachine.AddDelegate(PlayerInputState.NONE, null, Player_NONE_Update_Client, null);
        InputStateMachine.AddDelegate(PlayerInputState.WARDING, Player_WARDING_Start_Client, Player_WARDING_Update_Client, Player_WARDING_End_Client);
        InputStateMachine.CurrentState = PlayerInputState.NONE;
        Movable.Dir.OnChange += Dir_OnChangeClient;
        StateInput.OnChange += StateInput_OnChangeClient;
        ward1Pos.OnChange += Ward1Pos_OnChangeClient;
        ward2Pos.OnChange += Ward2Pos_OnChangeClient;

        skin.materials[0].color = Data.Color.Value;
        //attach camera
        if (IsOwner & LocalConnection.FirstObject == NetworkObject)
        {
            cam = Camera.main;
            cineCam = cam.GetComponent<CinemachineCamera>();
            if (cineCam)
            {
                cineCam.LookAt = transform;
                cineCam.Follow = transform;
            }
            if (!ward1)
            {
                ward1 = Instantiate(PrefabFactory.WardModel);
                ward1.SetActive(false);
            }
            if (!ward2)
            {
                ward2 = Instantiate(PrefabFactory.WardModel);
                ward2.SetActive(false);
            }
        }
    }
    public override void OnStopClient()
    {
        base.OnStopClient();
        if (IsOwner)
        {
            Destroy(ward1);
            Destroy(ward2);
        }
        Movable.Dir.OnChange -= Dir_OnChangeClient;
        StateInput.OnChange -= StateInput_OnChangeClient;
        ward1Pos.OnChange -= Ward1Pos_OnChangeClient;
        ward2Pos.OnChange -= Ward2Pos_OnChangeClient;
    }
    #endregion
    #region Event
    protected virtual void StateInput_OnChangeClient(PlayerInputState prev, PlayerInputState next, bool asServer)
    {
        if (asServer) return;
        InputStateMachine.CurrentState = next;
    }
    protected virtual void Dir_OnChangeClient(Vector3 prev, Vector3 next, bool asServer)
    {
        if (asServer) return;
    }
    protected virtual void Ward2Pos_OnChangeClient(Vector3 prev, Vector3 next, bool asServer)
    {
        if (asServer) return;
        if (!ward2.activeSelf)
        {
            ward2.SetActive(true);
        }
        ward2.transform.position = next;
    }
    protected virtual void Ward1Pos_OnChangeClient(Vector3 prev, Vector3 next, bool asServer)
    {
        if (asServer) return;
        if (!ward1.activeSelf)
        {
            ward1.SetActive(true);
        }
        ward1.transform.position = next;
    }
    #endregion
    #region StateMachine
    protected virtual void Player_NONE_Update_Client()
    {
        if (IsOwner)
        {
            if (Input.GetKeyDown(KeyCode.Space) && Data.Currency.Value > 0) PlaceWardRPC();
            Movable.SetDirRPC(Movable.DirInput());
            Movable.SetDesRPC(Movable.MouseInput(cam, LayerMask.NameToLayer("Ground")));
        }
    }
    protected virtual void Player_WARDING_Start_Client()
    {
        if (IsOwner)
        {
            wardingRadius.SetActive(true);
            if (!ward2.activeSelf)
            {
                ward2.SetActive(true);
            }
            ward2.transform.position = transform.position;
        }
    }
    protected virtual void Player_WARDING_Update_Client()
    {
        if (StateInput.Value != PlayerInputState.WARDING) return;
        if (IsOwner)
        {
            Movable.SetDirRPC(Movable.DirInput());
            if (Input.GetKeyDown(KeyCode.Space))
            {
                PlaceWardRPC();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                UnplaceWardRPC();
            }
        }
    }
    protected virtual void Player_WARDING_End_Client()
    {

    }

    #endregion
    #region private
    protected void UnplaceWardObserver()
    {
        wardingRadius.SetActive(false);
        ward1.SetActive(false);
        ward2.SetActive(false);
    }
    #endregion
}
