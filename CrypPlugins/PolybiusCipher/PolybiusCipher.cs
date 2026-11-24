using ClassicCiphers;
using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace PolybiusCipher
{
    /// <summary>The Polybius cipher replaces every letter by the number of its row and of its column in a 5x5 square; I and J share a cell. The algorithm and the picture are in LibSource/ClassicCipherLib.</summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("PolybiusCipher.Properties.Resources", "PluginCaption", "PluginTooltip", "PolybiusCipher/DetailedDescription/doc.xml")]
    [ComponentCategory(ComponentCategory.CiphersClassic)]
    public class PolybiusCipher : ICrypComponent
    {
        private PolybiusCipherSettings _settings = new PolybiusCipherSettings();

        [PropertyInfo(Direction.InputData, "InputTextCaption", "InputTextTooltip", true)]
        public string InputText { get; set; }

        [PropertyInfo(Direction.InputData, "KeyCaption", "KeyTooltip", false)]
        public string Key { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputTextCaption", "OutputTextTooltip", false)]
        public string OutputText { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (PolybiusCipherSettings)value;
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
            string title = new System.Resources.ResourceManager(typeof(PolybiusCipher).Namespace + ".Properties.Resources", typeof(PolybiusCipher).Assembly).GetString("PluginCaption");
            string lang = TraceBuilder.Language();
            try
            {
                OutputText = ClassicCiphers.SubstitutionCiphers.Polybius(InputText, Key, decrypt);
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
                ((ClassicCipherView)Presentation).Show(ClassicCiphers.CipherTraces.Polybius(title, Key, InputText, OutputText, decrypt, lang));
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
