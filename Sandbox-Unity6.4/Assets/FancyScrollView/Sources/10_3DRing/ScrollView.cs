/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

using System.Collections.Generic;
using UnityEngine;

namespace Lilja.FancyScrollView.Example10
{
    class ScrollView : FancyScrollView<ItemData>
    {
        [SerializeField] Cell cellPrefab = default;
        [SerializeField] Scroller scroller = default;

        protected override FancyCell<ItemData, NullContext> CellPrefab => cellPrefab;

        protected override void Initialize()
        {
            base.Initialize();
            scroller.OnValueChanged(UpdatePosition);
        }

        public void UpdateData(IList<ItemData> items)
        {
            UpdateContents(items);
            scroller.SetTotalCount(items.Count);
        }

        protected override bool TryCreatePreviewItem(FancyScrollPreviewItemContext context, out ItemData item)
        {
            item = CreateItem(context.Index);
            return true;
        }

        public static ItemData CreateItem(int index)
        {
            var hue = Mathf.Repeat(index * 0.085f, 1f);
            return new ItemData(index, Color.HSVToRGB(hue, 0.65f, 0.95f));
        }
    }
}
