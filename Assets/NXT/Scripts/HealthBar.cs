using System;
using UnityEngine;

namespace NXT.Scripts
{
    [RequireComponent(typeof(Renderer))]
    public class HealthBar : MonoBehaviour
    {
        private Renderer _renderer;
        private MaterialPropertyBlock _materialProperty;
        private static readonly int Fill = Shader.PropertyToID("_Fill");

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _materialProperty = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_materialProperty);
        }

        public void Reset()
        {
            #if UNITY_EDITOR
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();
            _materialProperty ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_materialProperty);
            #endif
            _materialProperty.SetFloat(Fill, 1);
            _renderer.SetPropertyBlock(_materialProperty);
        }

        public void UpdateBarValue(float currentHealth, float maxHealth)
        {
            if (maxHealth == 0) return;
            float healthPercent = currentHealth / maxHealth;
            _materialProperty.SetFloat(Fill, healthPercent);
            _renderer.SetPropertyBlock(_materialProperty);
        }

    }
}
