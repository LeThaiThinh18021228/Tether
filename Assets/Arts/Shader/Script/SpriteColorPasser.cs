using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteColorPasser : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        propertyBlock = new MaterialPropertyBlock();
    }
    private void OnEnable()
    {
        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(ColorProperty, spriteRenderer.color);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }
}
