using CrypTool.PluginBase;
using CrypTool.PluginBase.IO;
using CrypTool.PluginBase.Miscellaneous;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace CipherMAC
{
    /// <summary>
    /// Message authentication codes made of a block cipher: CMAC (OMAC1, NIST SP 800-38B) and the classic CBC-MAC
    /// (FIPS 113: CBC encryption with a zero IV, the last block is the MAC; the message is padded with zero bytes).
    /// The MAC is as long as one block of the cipher.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("CipherMAC.Properties.Resources", "PluginCaption", "PluginTooltip", "CipherMAC/DetailedDescription/doc.xml", "CipherMAC/icon.png")]
    [ComponentCategory(ComponentCategory.HashFunctions)]
    public class CipherMAC : ICrypComponent
    {
        private CipherMACSettings _settings = new CipherMACSettings();
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
            set => _settings = (CipherMACSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        /// <summary>The cipher and the key sizes (in bytes) it accepts; null means any size from 1 to 255 bytes.</summary>
        private (IBlockCipher Engine, int[] KeySizes) GetCipher()
        {
            switch (_settings.Cipher)
            {
                case 1: return (new DesEngine(), new[] { 8 });
                case 2: return (new CamelliaEngine(), new[] { 16, 24, 32 });
                case 3: return (new IdeaEngine(), new[] { 16 });
                case 4: return (new RC2Engine(), Enumerable.Range(1, 128).ToArray());
                case 5: return (new RC532Engine(), Enumerable.Range(1, 255).ToArray());
                case 6: return (new RC6Engine(), new[] { 16, 24, 32 });
                case 7: return (new SerpentEngine(), new[] { 16, 24, 32 });
                case 8: return (new TwofishEngine(), new[] { 16, 24, 32 });
                case 9: return (new ShacalEngine(false), Enumerable.Range(16, 49).ToArray());
                case 10: return (new ShacalEngine(true), Enumerable.Range(16, 49).ToArray());
                default: return (new AesEngine(), new[] { 16, 24, 32 });
            }
        }

        public void Execute()
        {
            if (InputData == null || Key == null)
            {
                GuiLogMessage("Both the data and the key are needed.", NotificationLevel.Error);
                return;
            }

            var (engine, keySizes) = GetCipher();
            if (!keySizes.Contains(Key.Length))
            {
                GuiLogMessage(string.Format("Wrong key size: {0} bytes given, but {1} needed.", Key.Length,
                    keySizes.Length > 8 ? keySizes.Min() + " to " + keySizes.Max() + " bytes are" : string.Join(" or ", keySizes) + " bytes are"), NotificationLevel.Error);
                return;
            }

            int macSize = engine.GetBlockSize() * 8;
            if (_settings.Mode == 0 && engine.GetBlockSize() != 8 && engine.GetBlockSize() != 16)
            {
                GuiLogMessage("CMAC is defined only for ciphers with a 64-bit or 128-bit block; choose the CBC-MAC for this cipher.", NotificationLevel.Error);
                return;
            }
            IMac mac = _settings.Mode == 1 ? (IMac)new CbcBlockCipherMac(engine, macSize) : new CMac(engine, macSize);
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
