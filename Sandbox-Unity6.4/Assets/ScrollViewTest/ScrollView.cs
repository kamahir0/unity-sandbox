/*
 * FancyScrollView Test Implementation
 */

using UnityEngine;
using FancyScrollView;

namespace ScrollViewTest
{
    public class ScrollView : FancyScrollView<ItemData>
    {
        [SerializeField] Cell cellPrefab = default;

        protected override FancyCell<ItemData, NullContext> CellPrefab => cellPrefab;

        // Properties for editor setup script access
        public Scroller TestScroller
        {
            get => Scroller;
            set { }
        }

        public GameObject TestCellPrefab
        {
            get => cellPrefab != null ? cellPrefab.gameObject : null;
            set => cellPrefab = value != null ? value.GetComponent<Cell>() : null;
        }

        public Transform TestCellContainer
        {
            get => cellContainer;
            set => cellContainer = value;
        }

        protected override ItemData CreatePreviewItem(FancyScrollPreviewItemContext context)
        {
            return new ItemData(string.Format("Preview Item {0:00}", context.Index + 1));
        }
    }
}
