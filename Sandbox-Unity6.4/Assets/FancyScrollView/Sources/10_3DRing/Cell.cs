/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using UnityEngine;

namespace Lilja.FancyScrollView.Example10
{
    class Cell : FancyCell<ItemData>
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorProperty = Shader.PropertyToID("_Color");

        [SerializeField] Transform panel = default;
        [SerializeField] MeshRenderer panelRenderer = default;
        [SerializeField] TextMesh label = default;

        MaterialPropertyBlock propertyBlock;
        Vector3 baseScale;
        bool initialized;

        void Awake()
        {
            InitializeCell();
        }

        public override void Initialize()
        {
            InitializeCell();
        }

        void InitializeCell()
        {
            if (initialized)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            baseScale = panel != null ? panel.localScale : Vector3.one;
            initialized = true;
        }

        public override void UpdateContent(ItemData itemData)
        {
            if (label != null)
            {
                label.text = itemData.Index.ToString("00");
            }

            if (panelRenderer != null)
            {
                InitializeCell();
                propertyBlock ??= new MaterialPropertyBlock();
                propertyBlock.Clear();
                propertyBlock.SetColor(BaseColor, itemData.Color);
                propertyBlock.SetColor(ColorProperty, itemData.Color);
                panelRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        public override void UpdatePosition(float position)
        {
            var angle = Mathf.Lerp(-58f, 58f, position);
            var radians = angle * Mathf.Deg2Rad;
            const float radius = 18f;
            var ringCenter = new Vector3(0f, 0f, -18f);
            var radial = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));

            transform.localPosition = ringCenter + radial * radius;
            transform.localRotation = Quaternion.LookRotation(radial, Vector3.up);

            var focus = 1f - Mathf.Abs(position - 0.5f) * 2f;
            var scale = Mathf.Lerp(0.5f, 1.5f, focus);
            transform.localScale = Vector3.one * scale;

            if (panel != null)
            {
                InitializeCell();
                panel.localScale = baseScale;
            }
        }
    }
}
