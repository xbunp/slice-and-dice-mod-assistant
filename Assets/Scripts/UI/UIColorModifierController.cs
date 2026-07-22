using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Graphic))]
public class UIColorModifierController : MonoBehaviour
{
    public enum ColorOpType { PaletteSwap = 1, TargetedHue = 2, GlobalHSV = 3 }

    [System.Serializable]
    public class ColorOperation
    {
        public ColorOpType operationType = ColorOpType.PaletteSwap;

        [Header("Palette Swap / Targeted Hue")]
        public Color targetColor = Color.white;
        [Range(0, 99)] public float range = 0;
        public float rangeMultiplier = 1.46f;

        [Header("Palette Swap Specific")]
        public Color replaceColor = Color.white;

        [Header("Targeted Hue Specific")]
        [Range(-99, 99)] public float hueShift = 0f;

        [Header("Global HSV Specific")]
        [Range(-99, 99)] public float globalHue = 0f;
        [Range(-99, 99)] public float globalSaturation = 0f;
        [Range(-99, 99)] public float globalValue = 0f;
    }

    public List<ColorOperation> operations = new List<ColorOperation>();

    private Graphic _graphic;
    private Material _materialInstance;

    // We cache arrays to avoid GC allocation every frame
    private float[] _opTypes = new float[16];
    private Vector4[] _opColorTargets = new Vector4[16];
    private Vector4[] _opColorReplaces = new Vector4[16];
    private Vector4[] _opParams = new Vector4[16];

    private void OnValidate()
    {
        ApplyToMaterial();
    }

    private void Update()
    {
        if (Application.isPlaying) ApplyToMaterial();
    }

    public void ApplyToMaterial()
    {
        if (_graphic == null) _graphic = GetComponent<Graphic>();
        if (_graphic.material == null || _graphic.material.shader.name != "UI/Custom/FlexibleColorModifier") return;

        // Instantiate material to avoid altering shared materials across the whole game
        if (_materialInstance == null || _graphic.material != _materialInstance)
        {
            _materialInstance = new Material(_graphic.material);
            _graphic.material = _materialInstance;
        }

        int count = Mathf.Min(operations.Count, 16); // 16 is the MAX_OPS defined in shader

        for (int i = 0; i < 16; i++)
        {
            if (i < count)
            {
                var op = operations[i];
                _opTypes[i] = (float)op.operationType;
                _opColorTargets[i] = op.targetColor;

                if (op.operationType == ColorOpType.GlobalHSV)
                    _opColorReplaces[i] = new Vector4(op.globalHue, op.globalSaturation, op.globalValue, 0);
                else
                    _opColorReplaces[i] = op.replaceColor;

                _opParams[i] = new Vector4(op.range, op.rangeMultiplier, op.hueShift, 0);
            }
            else
            {
                // Pad empty slots
                _opTypes[i] = 0;
            }
        }

        _materialInstance.SetInt("_OpCount", count);
        _materialInstance.SetFloatArray("_OpTypes", _opTypes);
        _materialInstance.SetVectorArray("_OpColorTargets", _opColorTargets);
        _materialInstance.SetVectorArray("_OpColorReplaces", _opColorReplaces);
        _materialInstance.SetVectorArray("_OpParams", _opParams);
    }
}