/*
 * FancyScrollView Test Implementation
 */

using UnityEngine;
using System.Collections.Generic;
using FancyScrollView;

namespace ScrollViewTest
{
    public class ScrollView : FancyScrollView<ItemData>, IFancyScrollPreviewDataSource<ItemData>
    {
        [SerializeField] Scroller scroller = default;
        [SerializeField] GameObject cellPrefab = default;

        protected override GameObject CellPrefab => cellPrefab;

        public int PreviewItemCount => EditorPreviewItemCount;

        // Properties for editor setup script access
        public Scroller TestScroller
        {
            get => scroller;
            set => scroller = value;
        }

        public GameObject TestCellPrefab
        {
            get => cellPrefab;
            set => cellPrefab = value;
        }

        public Transform TestCellContainer
        {
            get => cellContainer;
            set => cellContainer = value;
        }

        protected override void Initialize()
        {
            base.Initialize();
            if (scroller != null)
            {
                scroller.OnValueChanged(UpdatePosition);
            }
        }

        public void UpdateData(IList<ItemData> items)
        {
            UpdateContents(items);
            if (scroller != null)
            {
                scroller.SetTotalCount(items.Count);
            }
        }

        public ItemData CreatePreviewItem(FancyScrollPreviewItemContext context)
        {
            return new ItemData(string.Format("Preview Item {0:00}", context.Index + 1));
        }
    }
}
