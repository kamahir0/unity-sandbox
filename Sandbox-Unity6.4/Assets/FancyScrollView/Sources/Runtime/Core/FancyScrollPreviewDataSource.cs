/*
 * FancyScrollView (https://github.com/setchi/FancyScrollView)
 * Copyright (c) 2020 setchi
 * Licensed under MIT (https://github.com/setchi/FancyScrollView/blob/master/LICENSE)
 */

namespace FancyScrollView
{
    /// <summary>
    /// Context passed when creating edit-mode preview data.
    /// </summary>
    public readonly struct FancyScrollPreviewItemContext
    {
        public FancyScrollPreviewItemContext(int index, int count)
        {
            Index = index;
            Count = count;
        }

        public int Index { get; }

        public int Count { get; }
    }

    /// <summary>
    /// Implement this on a scroll view to provide edit-mode preview data.
    /// </summary>
    /// <typeparam name="TItemData">Item data type.</typeparam>
    public interface IFancyScrollPreviewDataSource<TItemData>
    {
        int PreviewItemCount { get; }

        TItemData CreatePreviewItem(FancyScrollPreviewItemContext context);
    }

    /// <summary>
    /// Optional lifecycle callbacks for edit-mode preview.
    /// </summary>
    public interface IFancyScrollPreviewLifecycle
    {
        void OnBeginPreview();

        void OnEndPreview();
    }
}
