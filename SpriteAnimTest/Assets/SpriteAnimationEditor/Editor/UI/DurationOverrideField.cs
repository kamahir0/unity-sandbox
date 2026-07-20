using UnityEngine.UIElements;

namespace SpriteAnimationEditor
{
    [UxmlElement]
    internal partial class DurationOverrideField : BaseField<int>
    {
        public const string UssClassName = "sprite-animation-duration-override-field";
        public const string InputContainerUssClassName =
            "sprite-animation-duration-override-field__input";
        public const string ToggleUssClassName =
            "sprite-animation-duration-override-field__toggle";
        public const string IntegerFieldUssClassName =
            "sprite-animation-duration-override-field__integer";

        public Toggle OverrideToggle { get; }

        public IntegerField IntegerField { get; }

        public DurationOverrideField()
            : this(null)
        {
        }

        public DurationOverrideField(string label)
            : this(label, new VisualElement())
        {
        }

        private DurationOverrideField(string label, VisualElement inputContainer)
            : base(label, inputContainer)
        {
            AddToClassList(UssClassName);
            AddToClassList(alignedFieldUssClassName);
            inputContainer.AddToClassList(InputContainerUssClassName);

            OverrideToggle = new Toggle
            {
                name = "duration-override-toggle",
            };
            OverrideToggle.AddToClassList(ToggleUssClassName);
            inputContainer.Add(OverrideToggle);

            IntegerField = new IntegerField
            {
                name = "duration-field",
                isDelayed = false,
            };
            IntegerField.AddToClassList(IntegerFieldUssClassName);
            inputContainer.Add(IntegerField);
        }
    }
}
