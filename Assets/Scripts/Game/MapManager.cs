using Framework;
using Framework.FishNet;
using UnityEngine;

public class MapManager : SingletonNetwork<MapManager>
{
    [SerializeField] private int width; public static int Width { get { return Instance.width; } }
    [SerializeField] private int height; public static int Height { get { return Instance.height; } }

    [SerializeField] private GameObject ground;
    public override void OnStartClient()
    {
        base.OnStartClient();
        ground.transform.SetScaleXZ(Width, Height);
    }

    public static Vector3 RandomPositionInsideMap()
    {
        float x = Random.Range(-Width / 2, Width / 2);
        float y = Random.Range(-Height / 2, Height / 2);
        return new Vector3(x, 0, y);
    }
}
