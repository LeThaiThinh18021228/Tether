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

    public static Vector3 RandomPositionInsideMap(Vector3 bound)
    {
        float x = Random.Range(-(Width / 2f - bound.x/2), (Width / 2f - bound.x / 2));
        float y = Random.Range(-(Height / 2f - bound.y/2), (Height / 2f - bound.y / 2));
        return new Vector3(x, 0, y);
    }
}
