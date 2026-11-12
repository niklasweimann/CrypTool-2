using System.Collections.ObjectModel;

namespace System.Windows.Input
{
    // ICommand itself comes from .NET (System.ObjectModel), as in WPF.

    /// <summary>A command whose handlers are found through the command bindings of the elements.</summary>
    public class RoutedCommand : ICommand
    {
        public RoutedCommand() { }

        public RoutedCommand(string name, Type ownerType)
        {
            Name = name;
            OwnerType = ownerType;
        }

        public RoutedCommand(string name, Type ownerType, InputGestureCollection inputGestures) : this(name, ownerType) { }

        public string Name { get; }
        public Type OwnerType { get; }
        public InputGestureCollection InputGestures { get; } = new InputGestureCollection();

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => true;

        public bool CanExecute(object parameter, IInputElement target) => true;

        public void Execute(object parameter) { }

        public void Execute(object parameter, IInputElement target)
        {
            for (UIElement element = target as UIElement; element != null; element = element.ParentElement)
            {
                foreach (CommandBinding binding in element.CommandBindings)
                {
                    if (binding.Command == this)
                    {
                        binding.Invoke(element, parameter);
                        return;
                    }
                }
            }
        }
    }

    public class RoutedUICommand : RoutedCommand
    {
        public RoutedUICommand() { }

        public RoutedUICommand(string text, string name, Type ownerType) : base(name, ownerType) => Text = text;

        public RoutedUICommand(string text, string name, Type ownerType, InputGestureCollection inputGestures) : base(name, ownerType) => Text = text;

        public string Text { get; set; }
    }

    public abstract class InputGesture { }

    public class KeyGesture : InputGesture
    {
        public KeyGesture(Key key) => Key = key;

        public KeyGesture(Key key, ModifierKeys modifiers) : this(key) => Modifiers = modifiers;

        public Key Key { get; }
        public ModifierKeys Modifiers { get; }
    }

    public class InputGestureCollection : Collection<InputGesture> { }

    public class ExecutedRoutedEventArgs : RoutedEventArgs
    {
        internal ExecutedRoutedEventArgs(ICommand command, object parameter)
        {
            Command = command;
            Parameter = parameter;
        }

        public ICommand Command { get; }
        public object Parameter { get; }
    }

    public class CanExecuteRoutedEventArgs : RoutedEventArgs
    {
        internal CanExecuteRoutedEventArgs(ICommand command, object parameter)
        {
            Command = command;
            Parameter = parameter;
        }

        public ICommand Command { get; }
        public object Parameter { get; }
        public bool CanExecute { get; set; }
        public bool ContinueRouting { get; set; }
    }

    public delegate void ExecutedRoutedEventHandler(object sender, ExecutedRoutedEventArgs e);

    public delegate void CanExecuteRoutedEventHandler(object sender, CanExecuteRoutedEventArgs e);

    public class CommandBinding
    {
        public CommandBinding() { }

        public CommandBinding(ICommand command) => Command = command;

        public CommandBinding(ICommand command, ExecutedRoutedEventHandler executed) : this(command) => Executed += executed;

        public CommandBinding(ICommand command, ExecutedRoutedEventHandler executed, CanExecuteRoutedEventHandler canExecute) : this(command, executed) => CanExecute += canExecute;

        public ICommand Command { get; set; }

        public event ExecutedRoutedEventHandler Executed;

        public event CanExecuteRoutedEventHandler CanExecute;

        internal void Invoke(object sender, object parameter) => Executed?.Invoke(sender, new ExecutedRoutedEventArgs(Command, parameter));
    }

    public class CommandBindingCollection : Collection<CommandBinding> { }

    public class InputBinding
    {
        public ICommand Command { get; set; }
        public object CommandParameter { get; set; }
        public InputGesture Gesture { get; set; }
    }

    public class KeyBinding : InputBinding
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
    }

    public class InputBindingCollection : Collection<InputBinding> { }

    /// <summary>Re-evaluation of command states is requested by code; commands here are always executable.</summary>
    public static class CommandManager
    {
        public static event EventHandler RequerySuggested;

        public static void InvalidateRequerySuggested() => RequerySuggested?.Invoke(null, EventArgs.Empty);
    }

    public static class ApplicationCommands
    {
        public static RoutedUICommand Copy { get; } = new RoutedUICommand("Copy", "Copy", typeof(ApplicationCommands));
        public static RoutedUICommand Cut { get; } = new RoutedUICommand("Cut", "Cut", typeof(ApplicationCommands));
        public static RoutedUICommand Paste { get; } = new RoutedUICommand("Paste", "Paste", typeof(ApplicationCommands));
        public static RoutedUICommand Undo { get; } = new RoutedUICommand("Undo", "Undo", typeof(ApplicationCommands));
        public static RoutedUICommand SelectAll { get; } = new RoutedUICommand("Select All", "SelectAll", typeof(ApplicationCommands));
        public static RoutedUICommand Redo { get; } = new RoutedUICommand("Redo", "Redo", typeof(ApplicationCommands));
        public static RoutedUICommand Delete { get; } = new RoutedUICommand("Delete", "Delete", typeof(ApplicationCommands));
        public static RoutedUICommand Find { get; } = new RoutedUICommand("Find", "Find", typeof(ApplicationCommands));
        public static RoutedUICommand Help { get; } = new RoutedUICommand("Help", "Help", typeof(ApplicationCommands));
        public static RoutedUICommand New { get; } = new RoutedUICommand("New", "New", typeof(ApplicationCommands));
        public static RoutedUICommand Open { get; } = new RoutedUICommand("Open", "Open", typeof(ApplicationCommands));
        public static RoutedUICommand Save { get; } = new RoutedUICommand("Save", "Save", typeof(ApplicationCommands));
        public static RoutedUICommand Close { get; } = new RoutedUICommand("Close", "Close", typeof(ApplicationCommands));
        public static RoutedUICommand Print { get; } = new RoutedUICommand("Print", "Print", typeof(ApplicationCommands));
        public static RoutedUICommand Properties { get; } = new RoutedUICommand("Properties", "Properties", typeof(ApplicationCommands));
        public static RoutedUICommand Stop { get; } = new RoutedUICommand("Stop", "Stop", typeof(ApplicationCommands));
    }

    /// <summary>Commands named in XAML ("Help", "ApplicationCommands.Copy"): the built-in command of that name.</summary>
    internal static class CommandLookup
    {
        internal static ICommand Find(string text)
        {
            string name = text.Substring(text.LastIndexOf('.') + 1);
            foreach (Type owner in new[] { typeof(ApplicationCommands), typeof(Documents.EditingCommands) })
            {
                if (owner.GetProperty(name, Reflection.BindingFlags.Public | Reflection.BindingFlags.Static)?.GetValue(null) is ICommand command)
                {
                    return command;
                }
            }
            return new RoutedUICommand(name, name, typeof(ApplicationCommands));
        }
    }
}

namespace System.Windows.Documents
{
    public static class EditingCommands
    {
        public static Input.RoutedUICommand ToggleBold { get; } = new Input.RoutedUICommand("Bold", "ToggleBold", typeof(EditingCommands));
        public static Input.RoutedUICommand ToggleItalic { get; } = new Input.RoutedUICommand("Italic", "ToggleItalic", typeof(EditingCommands));
        public static Input.RoutedUICommand ToggleUnderline { get; } = new Input.RoutedUICommand("Underline", "ToggleUnderline", typeof(EditingCommands));
        public static Input.RoutedUICommand Backspace { get; } = new Input.RoutedUICommand("Backspace", "Backspace", typeof(EditingCommands));
        public static Input.RoutedUICommand Delete { get; } = new Input.RoutedUICommand("Delete", "Delete", typeof(EditingCommands));
        public static Input.RoutedUICommand ToggleInsert { get; } = new Input.RoutedUICommand("Insert", "ToggleInsert", typeof(EditingCommands));
        public static Input.RoutedUICommand EnterParagraphBreak { get; } = new Input.RoutedUICommand("Enter", "EnterParagraphBreak", typeof(EditingCommands));
    }
}
