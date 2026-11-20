using ClassicCiphers;
using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace NihilistTransposition
{
    /// <summary>The text fills a square; its columns and then its rows are moved by the same key, and the result is read by rows or by columns. The algorithm and the picture are in LibSource/ClassicCipherLib.</summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("NihilistTransposition.Properties.Resources", "PluginCaption", "PluginTooltip", "NihilistTransposition/DetailedDescription/doc.xml")]
    [ComponentCategory(ComponentCategory.CiphersClassic)]
    public class NihilistTransposition : ICrypComponent
    {
        private NihilistTranspositionSettings _settings = new NihilistTranspositionSettings();

        [PropertyInfo(Direction.InputData, "InputTextCaption", "InputTextTooltip", true)]
        public string InputText { get; set; }

        [PropertyInfo(Direction.InputData, "KeyCaption", "KeyTooltip", true)]
        public string Key { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputTextCaption", "OutputTextTooltip", false)]
        public string OutputText { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (NihilistTranspositionSettings)value;
        }

        private ClassicCipherView _view;

        /// <summary>The picture of the calculation: the key tables and the steps (made by the library, shown by <see cref="ClassicCipherView"/>).</summary>
        public UserControl Presentation => _view ?? (_view = new ClassicCipherView());

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            if (InputText == null)
            {
                GuiLogMessage("There is no input text.", NotificationLevel.Error);
                return;
            }
            bool decrypt = _settings.Action == 1;
            string title = new System.Resources.ResourceManager("NihilistTransposition.Properties.Resources", typeof(NihilistTransposition).Assembly).GetString("PluginCaption");
            string lang = TraceBuilder.Language();
            try
            {
                OutputText = ClassicCiphers.TranspositionCiphers.NihilistTransposition(InputText, Key, _settings.ByColumns, decrypt);
                OnPropertyChanged("OutputText");
            }
            catch (ArgumentException ex)
            {
                GuiLogMessage(ex.Message, NotificationLevel.Error);
                _view?.ShowMessage(title, ex.Message);
                return;
            }
            try
            {
                ((ClassicCipherView)Presentation).Show(ClassicCiphers.CipherTraces.NihilistTransposition(title, Key, _settings.ByColumns, InputText, OutputText, decrypt, lang));
            }
            catch (Exception)
            {
                ((ClassicCipherView)Presentation).Show(ClassicCiphers.TraceBuilder.Generic(title, InputText, OutputText, decrypt, lang));
            }
        }

        public void Stop()
        {
        }

        public void Initialize()
        {
        }

        public void PreExecution()
        {
            Dispose();
        }

        public void PostExecution()
        {
            Dispose();
        }

        public void Dispose()
        {
            InputText = Key = OutputText = null;
        }

        private void OnPropertyChanged(string name)
        {
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }

        private void GuiLogMessage(string message, NotificationLevel level)
        {
            EventsHelper.GuiLogMessage(OnGuiLogNotificationOccured, this, message, level);
        }
    }
}
