/*
 * FancyScrollView Test Implementation
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FancyScrollView;

namespace ScrollViewTest
{
    public class Cell : FancyCell<ItemData>
    {
        [SerializeField] TextMeshProUGUI titleLabel = default;
        [SerializeField] Image bgImage = default;

        public override void UpdateContent(ItemData itemData)
        {
            if (titleLabel != null)
            {
                titleLabel.text = itemData.Title;
            }
        }

        public override void UpdatePosition(float position)
        {
            var parent = transform.parent as RectTransform;
            float viewportHeight = parent != null ? parent.rect.height : 400f;

            // position ranges from 0.0 (top of viewport) to 1.0 (bottom of viewport).
            // scrollOffset is 0.5f, meaning when scroll position is 0, the first cell is at position 0.5 (center).
            // Calculate Y position relative to the center of the container
            float y = (0.5f - position) * viewportHeight;
            transform.localPosition = new Vector2(0f, y);

            // Add smooth scaling and fading based on how close the cell is to the center
            float centerOffset = Mathf.Abs(position - 0.5f); // 0 at center, 0.5 at viewport edges
            float factor = Mathf.Clamp01(1.0f - centerOffset * 2f); // 1 at center, 0 at edges

            // Scale up slightly at center
            transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.15f, factor);

            // Fade the background slightly at edges
            if (bgImage != null)
            {
                var color = bgImage.color;
                color.a = Mathf.Lerp(0.35f, 1.0f, factor);
                bgImage.color = color;
            }
        }
    }
}
