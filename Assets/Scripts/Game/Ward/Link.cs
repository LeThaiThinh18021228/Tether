using DigitalRuby.LightningBolt;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Framework;
using Sirenix.OdinInspector;
using UnityEngine;

public class Link : NetworkBehaviour
{
    [SerializeField]private LightningBoltScript electric;
    public Ward wardStart;
    public Ward wardEnd;
    public float interval;
    private float curTime;
    public float duration;
    public Color color;
    [ShowInInspector] readonly public SyncVar<bool> isEletrocute = new(false);
    public event Callback OnExecute;
    public BoxCollider boxCollider;
    [SerializeField] SpriteRenderer spriteRenderer;
    private void Awake()
    {
        curTime = 0;
    }
    public override void OnStartServer()
    {
        base.OnStartServer();
    }
    public override void OnStopServer()
    {
        base.OnStopServer();
    }
    public override void OnStartClient()
    {
        base.OnStartClient();
        spriteRenderer.size = new Vector2(0.5f, transform.localScale.z * 4);
        spriteRenderer.gameObject.transform.SetScaleY(1f/ transform.localScale.z / 4);
        LineRenderer lineRenderer = electric.GetComponent<LineRenderer>();
        if (IsOwner)
        {
            color = Color.green;
            lineRenderer.startColor = Color.green;
            lineRenderer.endColor = Color.blue;
        }
        else
        {
            color = Color.red;
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.yellow;
        }
    }
    private void OnEnable()
    {
        isEletrocute.OnChange += IsEletrocute_OnChange;
    }


    private void OnDisable()
    {
        isEletrocute.OnChange -= IsEletrocute_OnChange;
    }

    private void IsEletrocute_OnChange(bool prev, bool next, bool asServer)
    {
        if (asServer)
        {
            if (next)
            {
                boxCollider.enabled = true;
                if (wardStart != null && wardEnd != null)
                {
                    SetPositionElectric(wardStart.transform.position, wardEnd.transform.position);
                }
                OnExecute?.Invoke();
            }
            else
            {
                boxCollider.enabled = false;
            }
        }
        else
        {
            if (next)
            {

            }
        }
    }

    void Update()
    {
        if (IsClientInitialized)
        {
            if (isEletrocute.Value)
            {
                electric.Trigger();
            }
        }
        if (IsServerInitialized)
        {
            curTime += Time.deltaTime;
            if (isEletrocute.Value)
            {
                if (curTime >= duration)
                {
                    curTime -= duration;
                    isEletrocute.Value = false;
                }
            }
            else
            {
                if (curTime >= interval)
                {
                    curTime -= interval;
                    isEletrocute.Value = true;
                }
            }
        }

    }
    [Server(Logging = FishNet.Managing.Logging.LoggingType.Off)]
    public void SetPosition(Ward wardStart, Ward wardEnd)
    {
        Vector3 start = wardStart.transform.position;
        Vector3 end = wardEnd.transform.position;
        transform.position = (start + end) / 2;
        Vector3 dir = end - start;
        transform.eulerAngles = new Vector3(0, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0);
        transform.SetScaleZ(dir.magnitude);
        this.wardStart = wardStart;
        this.wardEnd = wardEnd;
        SetPositionElectric(start, end);
        
    }
    [ObserversRpc(BufferLast = true)]
    public void SetPositionElectric(Vector3 wardStart, Vector3 wardEnd)
    {
        electric.StartObject.transform.position = wardStart + new Vector3(0, 1.5f, 0);
        electric.EndObject.transform.position = wardEnd + new Vector3(0, 1.5f, 0);
    }

    [Server(Logging = FishNet.Managing.Logging.LoggingType.Off)]
    private void OnTriggerEnter(Collider other)
    {
        if (string.Equals(other.tag, "Player", System.StringComparison.Ordinal))
        {
            Player otherPlayer = other.GetComponent<Player>();
            if (Owner != otherPlayer.Owner)
            {
                Owner.FirstObject.GetComponent<Player>().Electrocute(otherPlayer);
            }
        }
    }
}
