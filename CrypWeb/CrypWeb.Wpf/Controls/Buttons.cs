using System.Windows.Input;

namespace System.Windows.Controls.Primitives
{
    public abstract class ButtonBase : ContentControl
    {
        public static readonly DependencyProperty CommandProperty = Dp.Register<ICommand, ButtonBase>(nameof(Command));
        public static readonly DependencyProperty CommandParameterProperty = Dp.Register<object, ButtonBase>(nameof(CommandParameter));

        public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
        public object CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
        public ClickMode ClickMode { get; set; }
        public bool IsPressed => false;

        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
            "Click", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ButtonBase));

        public event RoutedEventHandler Click;

        internal override bool WantsClicks => true;

        internal override void OnClicked(MouseButtonEventArgs e)
        {
            base.OnClicked(e);
            if (IsEnabled && !e.Handled)
            {
                e.Handled = true;      // a button swallows the click, as in WPF
                OnClick();
            }
        }

        protected virtual void OnClick()
        {
            Click?.Invoke(this, new RoutedEventArgs { Source = this });
            RaiseEvent(new RoutedEventArgs(ClickEvent, this));      // handlers added with AddHandler on an ancestor
            if (Command != null && Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
            }
        }
    }

    public class ToggleButton : ButtonBase
    {
        public static readonly DependencyProperty IsCheckedProperty = Dp.Register<bool?, ToggleButton>(nameof(IsChecked), false, OnIsCheckedChanged);

        public bool? IsChecked { get => (bool?)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }
        public bool IsThreeState { get; set; }

        public event RoutedEventHandler Checked;
        public event RoutedEventHandler Unchecked;
        public event RoutedEventHandler Indeterminate;

        protected override void OnClick()
        {
            OnToggle();
            base.OnClick();
        }

        protected virtual void OnToggle()
        {
            bool isChecked = IsChecked == true;
            if (GetStoredValue(IsCheckedProperty) is Data.BindingExpression binding)
            {
                binding.WriteBack(this, !isChecked);
                RenderVersion.Bump();
            }
            else
            {
                IsChecked = !isChecked;
            }
        }

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var button = (ToggleButton)d;
            var args = new RoutedEventArgs { Source = button };
            bool? value = (bool?)e.NewValue;
            (value == true ? button.Checked : value == false ? button.Unchecked : button.Indeterminate)?.Invoke(button, args);
        }
    }

    public class RepeatButton : ButtonBase { }
}

namespace System.Windows.Controls
{
    using System.Windows.Controls.Primitives;

    public enum ClickMode { Release, Press, Hover }

    public class Button : ButtonBase
    {
        public bool IsDefault { get; set; }
        public bool IsCancel { get; set; }
    }

    public class CheckBox : ToggleButton { }

    public class RadioButton : ToggleButton
    {
        public string GroupName { get; set; }

        protected override void OnToggle()
        {
            // selecting a radio button deselects its siblings of the same group
            if (ParentElement != null)
            {
                foreach (UIElement sibling in new System.Collections.Generic.List<UIElement>(ParentElement.ChildElements))
                {
                    if (sibling is RadioButton other && !ReferenceEquals(other, this) && other.GroupName == GroupName)
                    {
                        other.IsChecked = false;
                    }
                }
            }
            IsChecked = true;
        }
    }
}
