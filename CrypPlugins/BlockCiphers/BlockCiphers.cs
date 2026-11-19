/*
   Copyright 2013 Nils Kopal, University of Kassel

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using CrypTool.PluginBase;
using CrypTool.PluginBase.IO;
using CrypTool.PluginBase.Miscellaneous;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace BlockCiphers
{

    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("BlockCiphers.Properties.Resources", "PluginCaption", "PluginTooltip", "BlockCiphers/DetailedDescription/doc.xml", "BlockCiphers/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernSymmetric)]
    public class BlockCiphers : ICrypComponent
    {
        private BlockCiphersSettings _settings = new BlockCiphersSettings();

        [PropertyInfo(Direction.InputData, "InputStreamCaption", "InputStreamTooltip", true)]
        public ICrypToolStream InputStream
        {
            get;
            set;
        }

        [PropertyInfo(Direction.InputData, "InputKeyCaption", "InputKeyTooltip", true)]
        public byte[] InputKey
        {
            get;
            set;
        }

        [PropertyInfo(Direction.InputData, "InputIVCaption", "InputIVTooltip", false)]
        public byte[] InputIV
        {
            get;
            set;
        }

        [PropertyInfo(Direction.OutputData, "OutputStreamCaption", "OutputStreamTooltip", true)]
        public ICrypToolStream OutputStream
        {
            get;
            set;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        public void Dispose()
        {
            InputIV = null;
            InputKey = null;
            InputStream = null;
            OutputStream = null;
        }

        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (BlockCiphersSettings)value;
        }

        public UserControl Presentation { get; private set; }

        /// <summary>The block cipher engine, its block size in bytes and the key sizes (in bytes) it accepts.</summary>
        private static (IBlockCipher Engine, int BlockSize, int[] KeySizes) GetAlgorithm(int algorithm)
        {
            switch (algorithm)
            {
                case 1:
                    // RC5 with 32-bit words and 12 rounds (BouncyCastle's RC532Engine), any key of 1 to 255 bytes
                    return (new RC532Engine(), 8, Enumerable.Range(1, 255).ToArray());
                case 2:
                    return (new RC6Engine(), 16, new[] { 16, 24, 32 });
                case 3:
                    return (new SerpentEngine(), 16, new[] { 16, 24, 32 });
                case 4:
                    return (new DesxEngine(), 8, new[] { 24 });
                case 5:
                    return (new ShacalEngine(false), 20, Enumerable.Range(16, 49).ToArray());
                case 6:
                    return (new ShacalEngine(true), 32, Enumerable.Range(16, 49).ToArray());
                default:
                    return (new IdeaEngine(), 8, new[] { 16 });
            }
        }

        private static BlockCipherHelper.PaddingType GetPaddingType(int padding)
        {
            switch (padding)
            {
                case 1: return BlockCipherHelper.PaddingType.Zeros;
                case 2: return BlockCipherHelper.PaddingType.PKCS7;
                case 3: return BlockCipherHelper.PaddingType.ANSIX923;
                case 4: return BlockCipherHelper.PaddingType.ISO10126;
                case 5: return BlockCipherHelper.PaddingType.OneZeros;
                default: return BlockCipherHelper.PaddingType.None;
            }
        }

        public void Execute()
        {
            var (engine, blockSize, keySizes) = GetAlgorithm(_settings.Algorithm);

            if (!keySizes.Contains(InputKey.Length))
            {
                GuiLogMessage(string.Format("Wrong key size given. Key size was {0} bits but needed is {1}.", InputKey.Length * 8,
                    keySizes.Length > 3 ? keySizes.Min() + " to " + keySizes.Max() + " bytes" : string.Join(" or ", keySizes.Select(k => k * 8 + " bits"))), NotificationLevel.Error);
                return;
            }

            if (InputIV == null)
            {
                if (_settings.Mode > 0)
                {
                    GuiLogMessage(string.Format("NOTE: No IV provided. Using 0x00...00 ({0} bytes)!", blockSize), NotificationLevel.Info);
                }
                InputIV = new byte[blockSize];
            }

            if (_settings.Mode > 0 && InputIV.Length != blockSize)
            {
                GuiLogMessage(string.Format("NOTE: Wrong IV size given. IV size was {0} bits but needed is {1} bits. {2}", InputIV.Length * 8, blockSize * 8,
                    InputIV.Length < blockSize ? "Appending with zeros." : "Removing the bytes at the end."), NotificationLevel.Info);
                byte[] newIV = new byte[blockSize];
                Array.Copy(InputIV, 0, newIV, 0, Math.Min(InputIV.Length, blockSize));
                InputIV = newIV;
            }

            KeyParameter keyParameter = new KeyParameter(InputKey);
            ParametersWithIV keyParameterWithIv = new ParametersWithIV(keyParameter, InputIV);

            BufferedBlockCipher cipher;
            switch (_settings.Mode)
            {
                case 1:
                    cipher = new BufferedBlockCipher(new CbcBlockCipher(engine));
                    break;
                case 2:
                    cipher = new BufferedBlockCipher(new CfbBlockCipher(engine, blockSize * 8));
                    break;
                case 3:
                    cipher = new BufferedBlockCipher(new OfbBlockCipher(engine, blockSize * 8));
                    break;
                default:
                    cipher = new BufferedBlockCipher(engine);
                    break;
            }

            if (_settings.Mode > 0)
            {
                cipher.Init(_settings.Action == 0, keyParameterWithIv);
            }
            else
            {
                cipher.Init(_settings.Action == 0, keyParameter);
            }

            var paddingType = GetPaddingType(_settings.Padding);
            if (_settings.Action == 0 && _settings.Padding > 0)
            {
                InputStream = BlockCipherHelper.AppendPadding(InputStream, paddingType, blockSize);
            }

            byte[] inputText = InputStream.CreateReader().ReadFully();
            byte[] outputText = new byte[cipher.GetOutputSize(inputText.Length)];
            int outputLen = cipher.ProcessBytes(inputText, 0, inputText.Length, outputText, 0);
            cipher.DoFinal(outputText, outputLen);
            OutputStream = new CStreamWriter();

            int length = outputText.Length;
            if (_settings.Action == 1 && _settings.Padding > 0)
            {
                length = BlockCipherHelper.StripPadding(outputText, outputText.Length - outputText.Length % blockSize, paddingType, outputText.Length);
            }

            ((CStreamWriter)OutputStream).Write(outputText, 0, length);
            ((CStreamWriter)OutputStream).Close();
            OnPropertyChanged("OutputStream");
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

        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        private void OnPropertyChanged(string name)
        {
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }

        private void GuiLogMessage(string p, NotificationLevel notificationLevel)
        {
            EventsHelper.GuiLogMessage(OnGuiLogNotificationOccured, this, new GuiLogEventArgs(p, this, notificationLevel));
        }
    }
}
