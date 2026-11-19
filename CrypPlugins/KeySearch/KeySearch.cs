using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace KeySearch
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("KeySearch.Properties.Resources", "PluginCaption", "PluginTooltip", "KeySearch/DetailedDescription/doc.xml", "KeySearch/icon.png")]
    [ComponentCategory(ComponentCategory.CryptanalysisGeneric)]
    public class KeySearch : ICrypComponent
    {
        private KeySearchSettings _settings = new KeySearchSettings();

        [PropertyInfo(Direction.InputData, "PlaintextCaption", "PlaintextTooltip", true)]
        public string Plaintext { get; set; }

        [PropertyInfo(Direction.InputData, "CiphertextCaption", "CiphertextTooltip", true)]
        public string Ciphertext { get; set; }

        [PropertyInfo(Direction.InputData, "KnownKeyCaption", "KnownKeyTooltip", true)]
        public string KnownKey { get; set; }

        [PropertyInfo(Direction.OutputData, "FoundKeyCaption", "FoundKeyTooltip", false)]
        public string FoundKey
        {
            get => _foundKey;
            set { _foundKey = value; OnPropertyChanged("FoundKey"); }
        }
        private string _foundKey;

        [PropertyInfo(Direction.OutputData, "TriesCaption", "TriesTooltip", false)]
        public string Tries
        {
            get => _tries;
            set { _tries = value; OnPropertyChanged("Tries"); }
        }
        private string _tries;

        [PropertyInfo(Direction.OutputData, "ReportCaption", "ReportTooltip", false)]
        public string Report
        {
            get => _report;
            set { _report = value; OnPropertyChanged("Report"); }
        }
        private string _report;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (KeySearchSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            try
            {
                byte[] key = KeySearching.FromHex(KnownKey);
                if (key.Length != KeySearching.KeyLength(_settings.Cipher)) { throw new ArgumentException("The key of " + KeySearching.Names[_settings.Cipher] + " has " + KeySearching.KeyLength(_settings.Cipher) + " bytes."); }
                byte[] plaintext = KeySearching.FromHex(Plaintext), ciphertext = KeySearching.FromHex(Ciphertext);
                long tries;
                byte[] found = KeySearching.Search(_settings.Cipher, key, _settings.UnknownBits, plaintext, ciphertext, out tries);
                Tries = tries.ToString();
                FoundKey = found == null ? "" : KeySearching.ToHex(found);
                Report = found == null ? string.Format(Properties.Resources.NotFound, tries) : string.Format(Properties.Resources.Found, KeySearching.ToHex(found), tries);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is InvalidOperationException || ex is OverflowException)
            {
                GuiLogMessage(ex.Message, NotificationLevel.Error);
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
            Plaintext = Ciphertext = KnownKey = FoundKey = Tries = Report = null;
        }

        public void PostExecution()
        {
        }

        public void Dispose()
        {
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


    /// <summary>
    /// Exhaustive key search with a partly known key, the attack of the key search programs of CrypTool 1: the key is known except for its last bits,
    /// plaintext and ciphertext are known, and every possible value of the unknown bits is tried.
    /// </summary>
    public static class KeySearching
    {
        public static readonly string[] Names = { "IDEA", "RC2", "RC4", "RC6", "Serpent", "Twofish", "DESX", "AES", "DES" };

        /// <summary>The key length in bytes (RC2 and RC4 accept more, this is the usual one).</summary>
        public static int KeyLength(int cipher) => new[] { 16, 16, 16, 16, 16, 16, 24, 16, 8 }[cipher];

        /// <summary>Encrypts the data (a whole number of blocks for the block ciphers) in ECB mode, RC4 as a stream cipher.</summary>
        public static byte[] Encrypt(int cipher, byte[] key, byte[] data)
        {
            if (cipher == 2)
            {
                var rc4 = new RC4Engine();
                rc4.Init(true, new KeyParameter(key));
                var result = new byte[data.Length];
                rc4.ProcessBytes(data, 0, data.Length, result, 0);
                return result;
            }
            IBlockCipher engine;
            ICipherParameters parameters = new KeyParameter(key);
            switch (cipher)
            {
                case 0: engine = new IdeaEngine(); break;
                case 1: engine = new RC2Engine(); break;
                case 3: engine = new RC6Engine(); break;
                case 4: engine = new SerpentEngine(); break;
                case 5: engine = new TwofishEngine(); break;
                case 7: engine = new AesEngine(); break;
                case 8: engine = new DesEngine(); break;
                case 6:
                    engine = new DesEngine();
                    parameters = new KeyParameter(key, 0, 8);
                    break;
                default: throw new ArgumentException("Unknown cipher.");
            }
            int block = engine.GetBlockSize();
            if (data.Length % block != 0) { throw new ArgumentException("The data must be a whole number of " + block + "-byte blocks."); }
            engine.Init(true, parameters);
            var output = new byte[data.Length];
            for (int i = 0; i < data.Length; i += block)
            {
                var input = (byte[])data.Clone();
                if (cipher == 6) { for (int k = 0; k < block; k++) { input[i + k] ^= key[8 + k]; } }   // DESX: pre-whitening with the second key
                engine.ProcessBlock(input, i, output, i);
                if (cipher == 6) { for (int k = 0; k < block; k++) { output[i + k] ^= key[16 + k]; } }   // post-whitening with the third key
            }
            return output;
        }

        public static byte[] FromHex(string hex)
        {
            hex = new string(hex.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (hex.Length % 2 != 0) { throw new FormatException("A hexadecimal value needs an even number of digits."); }
            return Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
        }

        public static string ToHex(byte[] data) => BitConverter.ToString(data).Replace("-", "");

        /// <summary>Tries all values of the last unknownBits bits of the key; returns the key or null, and the number of tries.</summary>
        public static byte[] Search(int cipher, byte[] knownKey, int unknownBits, byte[] plaintext, byte[] ciphertext, out long tries)
        {
            tries = 0;
            var key = (byte[])knownKey.Clone();
            long count = 1L << unknownBits;
            for (long value = 0; value < count; value++)
            {
                for (int bit = 0; bit < unknownBits; bit++)
                {
                    int index = key.Length - 1 - bit / 8;
                    byte mask = (byte)(1 << (bit % 8));
                    if ((value >> bit & 1) == 1) { key[index] |= mask; } else { key[index] &= (byte)~mask; }
                }
                tries++;
                if (Encrypt(cipher, key, plaintext).SequenceEqual(ciphertext)) { return key; }
            }
            return null;
        }
    }

}
