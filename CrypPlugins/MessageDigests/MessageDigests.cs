using CrypTool.PluginBase;
using CrypTool.PluginBase.IO;
using CrypTool.PluginBase.Miscellaneous;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using System.ComponentModel;
using System.Windows.Controls;

namespace MessageDigests
{
    /// <summary>The message digests MD2 and MD4 (RFC 1319, RFC 1320).</summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("MessageDigests.Properties.Resources", "PluginCaption", "PluginTooltip", "MessageDigests/DetailedDescription/doc.xml", "MessageDigests/icon.png")]
    [ComponentCategory(ComponentCategory.HashFunctions)]
    public class MessageDigests : ICrypComponent
    {
        private MessageDigestsSettings _settings = new MessageDigestsSettings();
        private byte[] _outputData;

        [PropertyInfo(Direction.InputData, "InputDataCaption", "InputDataTooltip", true)]
        public ICrypToolStream InputData
        {
            get;
            set;
        }

        [PropertyInfo(Direction.OutputData, "OutputDataStreamCaption", "OutputDataStreamTooltip", false)]
        public ICrypToolStream OutputDataStream => _outputData != null ? new CStreamWriter(_outputData) : null;

        [PropertyInfo(Direction.OutputData, "OutputDataCaption", "OutputDataTooltip", false)]
        public byte[] OutputData
        {
            get => _outputData;
            set
            {
                _outputData = value;
                OnPropertyChanged("OutputData");
                OnPropertyChanged("OutputDataStream");
            }
        }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (MessageDigestsSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            if (InputData == null)
            {
                GuiLogMessage("Received null value for the input data.", NotificationLevel.Warning);
                return;
            }

            IDigest digest = _settings.Algorithm == 1 ? (IDigest)new MD4Digest() : new MD2Digest();
            byte[] data = InputData.CreateReader().ReadFully();
            digest.BlockUpdate(data, 0, data.Length);
            byte[] hash = new byte[digest.GetDigestSize()];
            digest.DoFinal(hash, 0);
            OutputData = hash;
            OnPluginProgressChanged?.Invoke(this, new PluginProgressEventArgs(1, 1));
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
            InputData = null;
            _outputData = null;
        }

        private void OnPropertyChanged(string name)
        {
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }

        private void GuiLogMessage(string message, NotificationLevel level)
        {
            EventsHelper.GuiLogMessage(OnGuiLogNotificationOccured, this, new GuiLogEventArgs(message, this, level));
        }
    }
}
