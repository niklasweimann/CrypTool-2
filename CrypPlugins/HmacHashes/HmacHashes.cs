using CrypTool.PluginBase;
using CrypTool.PluginBase.IO;
using CrypTool.PluginBase.Miscellaneous;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using System.ComponentModel;
using System.Windows.Controls;

namespace HmacHashes
{
    /// <summary>
    /// HMAC (RFC 2104) with many hash functions: H((K xor opad) || H((K xor ipad) || message)), with the key padded to the block size of
    /// the hash function (or hashed first if it is longer).
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("HmacHashes.Properties.Resources", "PluginCaption", "PluginTooltip", "HmacHashes/DetailedDescription/doc.xml", "HmacHashes/icon.png")]
    [ComponentCategory(ComponentCategory.HashFunctions)]
    public class HmacHashes : ICrypComponent
    {
        private HmacHashesSettings _settings = new HmacHashesSettings();
        private byte[] _outputData;

        [PropertyInfo(Direction.InputData, "InputDataCaption", "InputDataTooltip", true)]
        public ICrypToolStream InputData { get; set; }

        [PropertyInfo(Direction.InputData, "KeyCaption", "KeyTooltip", true)]
        public byte[] Key { get; set; }

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
            set => _settings = (HmacHashesSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        /// <summary>The hash function with the given index (the order of the list in the resources).</summary>
        public static IDigest CreateDigest(int index)
        {
            switch (index)
            {
                case 0: return new MD2Digest();
                case 1: return new MD4Digest();
                case 2: return new MD5Digest();
                case 3: return new Sha1Digest();
                case 4: return new Sha224Digest();
                case 6: return new Sha384Digest();
                case 7: return new Sha512Digest();
                case 8: return new Sha3Digest(256);
                case 9: return new RipeMD128Digest();
                case 10: return new RipeMD160Digest();
                case 11: return new RipeMD256Digest();
                case 12: return new RipeMD320Digest();
                case 13: return new TigerDigest();
                case 14: return new WhirlpoolDigest();
                case 15: return new SM3Digest();
                default: return new Sha256Digest();
            }
        }

        public void Execute()
        {
            if (InputData == null || Key == null)
            {
                GuiLogMessage("Both the data and the key are needed.", NotificationLevel.Error);
                return;
            }

            var mac = new HMac(CreateDigest(_settings.Hash));
            mac.Init(new KeyParameter(Key));
            byte[] data = InputData.CreateReader().ReadFully();
            mac.BlockUpdate(data, 0, data.Length);
            byte[] result = new byte[mac.GetMacSize()];
            mac.DoFinal(result, 0);
            OutputData = result;
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
            Key = null;
            _outputData = null;
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
