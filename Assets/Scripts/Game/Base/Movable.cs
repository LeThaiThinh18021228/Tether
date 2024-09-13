using FishNet.Managing.Logging;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
public class Movable : NetworkBehaviour
{
    /// <summary>
    /// Movement direction, Vector3.zero if not moving.
    /// </summary>
    public readonly SyncVar<Vector3> Dir = new(Vector3.zero);
    public readonly SyncVar<Vector3?> Des = new(Vector3.zero);
    public float speed;
    public float steer = Mathf.Infinity;
    float toAngle;
    public event Callback OnArrived;
    Transform steerTransform;
    #region Networkbehavior
    private void Awake()
    {
        Dir.UpdateSettings(NetworkConfig.SyncTypeSettingsClientAuthorized);
        Des.UpdateSettings(NetworkConfig.SyncTypeSettingsClientAuthorized);
        steerTransform = transform;
    }
    private void OnEnable()
    {
        Dir.OnChange += Dir_OnDataChanged;
        Des.OnChange += Des_OnChange;
    }


    private void OnDisable()
    {
        Dir.OnChange -= Dir_OnDataChanged;
        Des.OnChange -= Des_OnChange;
    }

    void Update()
    {
        MoveToDes();
        MoveAsDir();
        SteerAsDir();
    }
    #endregion
    #region public
    [ServerRpc(RequireOwnership = true, RunLocally = true)]
    public void SetDirRPC(Vector3 dir)
    {

        if (dir != Vector3.zero)
        {
            Des.Value = null;
        }

        if (dir != Vector3.zero || !Des.Value.HasValue)
        {
            Dir.Value = dir;
            toAngle = Mathf.Atan2(Dir.Value.x, Dir.Value.z) * Mathf.Rad2Deg;
        }
    }
    public static Vector3 DirInput()
    {
        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir.z = 1;
        if (Input.GetKey(KeyCode.S)) dir.z = -1;
        if (Input.GetKey(KeyCode.A)) dir.x = -1;
        if (Input.GetKey(KeyCode.D)) dir.x = 1;

        if (Input.GetKeyUp(KeyCode.W)) dir.z = 0;
        if (Input.GetKeyUp(KeyCode.S)) dir.z = 0;
        if (Input.GetKeyUp(KeyCode.A)) dir.x = 0;
        if (Input.GetKeyUp(KeyCode.D)) dir.x = 0;
        return dir.normalized;
    }
    [ServerRpc(RequireOwnership = true, RunLocally = true)]
    public void SetDesRPC(Vector3? value)
    {
        if (value.HasValue)
        {
            SetDes(value);
        }
    }
    static public Vector3? MouseInput(Camera cam, LayerMask layer)
    {
        Vector3? hitPos = null;
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, layer))
            {
                hitPos = hit.point;
            }
        }
        return hitPos;
    }
    [Server(Logging = LoggingType.Off)]
    public void SetDes(Vector3? des)
    {
        Des.Value = des;
        if (des.HasValue)
        {
            Dir.Value = (des.Value - transform.position).normalized;
            toAngle = Mathf.Atan2(Dir.Value.x, Dir.Value.z) * Mathf.Rad2Deg;
        }
        else
        {
            Dir.Value = Vector3.zero;
        }

    }
    public void MoveTo(Vector3 des)
    {
        if (transform.position != des)
        {
            Dir.Value = (des - transform.position).normalized;
            toAngle = Mathf.Atan2(Dir.Value.x, Dir.Value.z) * Mathf.Rad2Deg;
            Vector3 fromAngle = transform.eulerAngles;
            if (toAngle < 0 && fromAngle.y > 180) { fromAngle.y -= 360; }
            if (Mathf.Abs(fromAngle.y - toAngle) > 0.01f)
            {
                steerTransform.eulerAngles = Vector3.MoveTowards(fromAngle, new Vector3(0, toAngle, 0), steer * Time.deltaTime);
            }
            transform.position = Vector3.MoveTowards(transform.position, des, speed * Time.deltaTime);
            if (transform.position == des)
            {
                Dir.Value = Vector3.zero;
            }
        }
    }

    public void SetSteerTransform(Transform transform)
    {
        steerTransform = transform;
    }
    public void Stop()
    {
        Des.Value = null;
        Dir.Value = Vector3.zero;
    }
    #endregion
    #region private
    [Server(Logging = LoggingType.Off)]
    void MoveToDes()
    {
        if (Des.Value.HasValue)
        {
            transform.position = Vector3.MoveTowards(transform.position, Des.Value.Value, speed * Time.deltaTime);
            if (transform.position == Des.Value)
            {
                OnArrived?.Invoke();
                SetDes(null);
            }
        }
    }
    [Server(Logging = LoggingType.Off)]
    void MoveAsDir()
    {
        if (Dir.Value != Vector3.zero && !Des.Value.HasValue)
        {
            Vector3 offset = Dir.Value * (speed * Time.deltaTime);
            transform.position += offset;
        }
    }
    private void SteerAsDir()
    {
        if (Dir.Value != Vector3.zero)
        {
            toAngle = Mathf.Atan2(Dir.Value.x, Dir.Value.z) * Mathf.Rad2Deg;
            Vector3 fromAngle = steerTransform.eulerAngles;
            if (toAngle < 0 && fromAngle.y > 180) { fromAngle.y -= 360; }
            if (Mathf.Abs(fromAngle.y - toAngle) > 0.01f)
            {
                steerTransform.eulerAngles = Vector3.MoveTowards(fromAngle, new Vector3(0, toAngle, 0), steer * Time.deltaTime);
            }
        }
    }
    private void Des_OnChange(Vector3? prev, Vector3? next, bool asServer)
    {

    }

    private void Dir_OnDataChanged(Vector3 prev, Vector3 next, bool asServer)
    {

    }



    #endregion
}
