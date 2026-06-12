/*
 * FancyScrollView Test Implementation
 */

using UnityEngine;
using System.Collections.Generic;
using FancyScrollView;

namespace ScrollViewTest
{
    [ExecuteAlways] // Enable editor execution for real-time preview
    public class ScrollView : FancyScrollView<ItemData>
    {
        [SerializeField] Scroller scroller = default;
        [SerializeField] GameObject cellPrefab = default;

        protected override GameObject CellPrefab => cellPrefab;

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
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                CleanUpEditorCells();
            }
#endif
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

#if UNITY_EDITOR
        private bool enableEditorPreview = false;
        public bool EnableEditorPreview
        {
            get => enableEditorPreview;
            set
            {
                if (enableEditorPreview != value)
                {
                    enableEditorPreview = value;
                    if (!value)
                    {
                        CleanUpEditorCells();
                    }
                    else
                    {
                        SetPreviewPosition(0.5f);
                    }
                }
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                if (enableEditorPreview)
                {
                    // Keep the cells marked as DontSave so they don't get saved into the scene file
                    if (cellContainer != null)
                    {
                        foreach (Transform child in cellContainer)
                        {
                            child.gameObject.hideFlags = HideFlags.DontSave;
                        }
                    }
                }
                else
                {
                    if (cellContainer != null && cellContainer.childCount > 0)
                    {
                        CleanUpEditorCells();
                    }
                }
            }
        }

        public void SetPreviewPosition(float position)
        {
            if (Application.isPlaying || !enableEditorPreview) return;

            UpdateMockData();
            initialized = false;
            CleanUpEditorCells();
            base.UpdatePosition(position);

            if (cellContainer != null)
            {
                foreach (Transform child in cellContainer)
                {
                    child.gameObject.hideFlags = HideFlags.DontSave;
                }
            }
        }

        private void UpdateMockData()
        {
            if (ItemsSource == null || ItemsSource.Count == 0)
            {
                ItemsSource = new List<ItemData>
                {
                    new ItemData("Preview Item 01"),
                    new ItemData("Preview Item 02"),
                    new ItemData("Preview Item 03"),
                    new ItemData("Preview Item 04"),
                    new ItemData("Preview Item 05"),
                };
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (enableEditorPreview)
                {
                    // When inspector values change in edit mode, force-refresh the layout preview
                    UpdateMockData();
                    
                    // Reset initialization flag to apply new settings
                    initialized = false;

                    CleanUpEditorCells();
                    base.UpdatePosition(0.5f); // Preview at middle position (0.5)

                    if (cellContainer != null)
                    {
                        foreach (Transform child in cellContainer)
                        {
                            child.gameObject.hideFlags = HideFlags.DontSave;
                        }
                    }
                }
                else
                {
                    CleanUpEditorCells();
                }
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                CleanUpEditorCells();
            }
        }


        private void CleanUpEditorCells()
        {
            if (cellContainer == null) return;

            // Destroy existing instantiated children immediately
            for (int i = cellContainer.childCount - 1; i >= 0; i--)
            {
                var child = cellContainer.GetChild(i).gameObject;
                DestroyImmediate(child);
            }

            // Clear the base private pool list using reflection to prevent pool reference leaks
            var poolField = typeof(FancyScrollView<ItemData, NullContext>).GetField("pool", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (poolField != null)
            {
                var poolList = poolField.GetValue(this) as System.Collections.IList;
                if (poolList != null)
                {
                    poolList.Clear();
                }
            }
        }
#endif
    }
}
