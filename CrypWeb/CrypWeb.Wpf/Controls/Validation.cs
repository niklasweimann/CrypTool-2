using System.Collections.ObjectModel;

namespace System.Windows.Controls
{
    /// <summary>
    /// Validation results of a bound element. Bindings here do not validate (no ValidationRules run), so an element
    /// never has errors; the types exist for converters and styles that read them.
    /// </summary>
    public class ValidationError
    {
        public ValidationError(ValidationRule ruleInError, object bindingInError, object errorContent = null, Exception exception = null)
        {
            RuleInError = ruleInError;
            BindingInError = bindingInError;
            ErrorContent = errorContent;
            Exception = exception;
        }

        public ValidationRule RuleInError { get; set; }

        public object BindingInError { get; }

        public object ErrorContent { get; set; }

        public Exception Exception { get; set; }
    }

    public static class Validation
    {
        private static readonly ReadOnlyObservableCollection<ValidationError> NoErrors =
            new ReadOnlyObservableCollection<ValidationError>(new ObservableCollection<ValidationError>());

        public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached("HasError", typeof(bool), typeof(Validation));
        public static readonly DependencyProperty ErrorsProperty = DependencyProperty.RegisterAttached("Errors", typeof(ReadOnlyObservableCollection<ValidationError>), typeof(Validation));
        public static readonly DependencyProperty ErrorTemplateProperty = DependencyProperty.RegisterAttached("ErrorTemplate", typeof(ControlTemplate), typeof(Validation));

        public static bool GetHasError(DependencyObject element) => false;

        public static ReadOnlyObservableCollection<ValidationError> GetErrors(DependencyObject element) => NoErrors;

        public static ControlTemplate GetErrorTemplate(DependencyObject element) => (ControlTemplate)element.GetValue(ErrorTemplateProperty);

        public static void SetErrorTemplate(DependencyObject element, ControlTemplate value) => element.SetValue(ErrorTemplateProperty, value);
    }
}

namespace Microsoft.Win32
{
    /// <summary>
    /// File dialogs. The engine worker has no access to the user's files, so a dialog is never shown and reports
    /// that the user cancelled (ShowDialog returns false). Components with file inputs use the browser's own upload.
    /// </summary>
    public abstract class FileDialog
    {
        public string Filter { get; set; } = "";
        public int FilterIndex { get; set; } = 1;
        public string FileName { get; set; } = "";
        public string[] FileNames { get; set; } = System.Array.Empty<string>();
        public string DefaultExt { get; set; } = "";
        public string InitialDirectory { get; set; } = "";
        public string Title { get; set; } = "";
        public bool AddExtension { get; set; } = true;
        public bool CheckFileExists { get; set; }
        public bool CheckPathExists { get; set; } = true;
        public bool RestoreDirectory { get; set; }

        public bool? ShowDialog() => false;

        public bool? ShowDialog(System.Windows.Window owner) => false;
    }

    public sealed class OpenFileDialog : FileDialog
    {
        public bool Multiselect { get; set; }

        public System.IO.Stream OpenFile() => throw new System.IO.FileNotFoundException("No file was chosen.");
    }

    public sealed class SaveFileDialog : FileDialog
    {
        public System.IO.Stream OpenFile() => throw new System.IO.IOException("No file was chosen.");

        public bool OverwritePrompt { get; set; } = true;
    }
}
