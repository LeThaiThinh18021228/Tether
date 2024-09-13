using DigitalRuby.LightningBolt;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Framework;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

public class Link : NetworkBehaviour
{
    public Player Player { get; private set; }
    [SerializeField] private LightningBoltScript electric;
    public Ward wardStart;
    public Ward wardEnd;
    public float Interval { get; private set; }
    public float CurTime { get; private set; }
    public float Duration { get; private set; }
    public Color Color;
    [ShowInInspector] readonly public SyncVar<bool> isEletrocute = new(false);
    public event Action OnExecute;
    public BoxCollider boxCollider;
    [SerializeField] SpriteRenderer spriteRenderer;
    private void Awake()
    {
        CurTime = 0;
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

        LineRenderer lineRenderer = electric.GetComponent<LineRenderer>();
        if (IsOwner)
        {
            Color = Color.green;
            lineRenderer.startColor = Color.green;
            lineRenderer.endColor = Color.blue;
        }
        else
        {
            Color = Color.red;
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
                    InitObserver(wardStart.transform.position, wardEnd.transform.position);
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
            CurTime += Time.deltaTime;
            if (isEletrocute.Value)
            {
                if (CurTime >= Duration)
                {
                    CurTime -= Duration;
                    isEletrocute.Value = false;
                }
            }
            else
            {
                if (CurTime >= Interval)
                {
                    CurTime -= Interval;
                    isEletrocute.Value = true;
                }
            }
        }

    }
    [Server(Logging = FishNet.Managing.Logging.LoggingType.Off)]
    public void Init(Ward wardStart, Ward wardEnd, Player player, float interval, float duration)
    {
        this.Player = player;
        Interval = interval;
        Duration = duration;
        Vector3 start = wardStart.transform.position;
        Vector3 end = wardEnd.transform.position;
        transform.position = (start + end) / 2;
        Vector3 dir = end - start;
        transform.eulerAngles = new Vector3(0, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0);
        transform.SetScaleZ(dir.magnitude);
        this.wardStart = wardStart;
        this.wardEnd = wardEnd;
        InitObserver(start, end, dir.magnitude);
    }
    [ObserversRpc(BufferLast = true)]
    public void InitObserver(Vector3 wardStart, Vector3 wardEnd, float? size = null)
    {
        if (!size.HasValue)
        {
            size = transform.localScale.z;
        }
        electric.StartObject.transform.position = wardStart + new Vector3(0, 1.5f, 0);
        electric.EndObject.transform.position = wardEnd + new Vector3(0, 1.5f, 0);
        spriteRenderer.size = new Vector2(0.5f, size.Value * 4);
        spriteRenderer.gameObject.transform.SetScaleY(0.25f / size.Value);
    }

    [Server(Logging = FishNet.Managing.Logging.LoggingType.Off)]
    private void OnTriggerEnter(Collider other)
    {
        if (string.Equals(other.tag, "Player", System.StringComparison.Ordinal))
        {
            Player otherPlayer = other.GetComponent<Player>();
            if (Player != otherPlayer)
            {
                Player.Electrocute(otherPlayer);
            }
        }
    }
}
