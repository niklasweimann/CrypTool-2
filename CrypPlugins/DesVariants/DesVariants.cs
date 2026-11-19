using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace DesVariants
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("DesVariants.Properties.Resources", "PluginCaption", "PluginTooltip", "DesVariants/DetailedDescription/doc.xml", "DesVariants/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernSymmetric)]
    public class DesVariants : ICrypComponent
    {
        private DesVariantsSettings _settings = new DesVariantsSettings();

        [PropertyInfo(Direction.InputData, "InputCaption", "InputTooltip", true)]
        public string Input { get; set; }

        [PropertyInfo(Direction.InputData, "KeyCaption", "KeyTooltip", true)]
        public string Key { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputCaption", "OutputTooltip", false)]
        public string Output
        {
            get => _output;
            set { _output = value; OnPropertyChanged("Output"); }
        }
        private string _output;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (DesVariantsSettings)value;
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
                byte[] result = DesFamily.Process(_settings.Variant, DesFamily.FromHex(Key), DesFamily.FromHex(Input), _settings.Mode == 0);
                Output = DesFamily.ToHex(result);
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
            Input = Key = Output = null;
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
    /// DES (FIPS 46-3) written with its tables, with the eight S-boxes as a parameter. With the standard S-boxes it is DES; with the single S-box of DESL (Leander, Paar, Poschmann and Schramm, 2007)
    /// repeated eight times it is the lightweight variant DESL. DESX and DESXL add whitening keys: C = K3 xor E_K1(P xor K2).
    /// </summary>
    public sealed class DesEngineVariant
    {
        private static readonly int[] Ip = { 58, 50, 42, 34, 26, 18, 10, 2, 60, 52, 44, 36, 28, 20, 12, 4, 62, 54, 46, 38, 30, 22, 14, 6, 64, 56, 48, 40, 32, 24, 16, 8, 57, 49, 41, 33, 25, 17, 9, 1, 59, 51, 43, 35, 27, 19, 11, 3, 61, 53, 45, 37, 29, 21, 13, 5, 63, 55, 47, 39, 31, 23, 15, 7 };
        private static readonly int[] Fp = { 40, 8, 48, 16, 56, 24, 64, 32, 39, 7, 47, 15, 55, 23, 63, 31, 38, 6, 46, 14, 54, 22, 62, 30, 37, 5, 45, 13, 53, 21, 61, 29, 36, 4, 44, 12, 52, 20, 60, 28, 35, 3, 43, 11, 51, 19, 59, 27, 34, 2, 42, 10, 50, 18, 58, 26, 33, 1, 41, 9, 49, 17, 57, 25 };
        private static readonly int[] Expansion = { 32, 1, 2, 3, 4, 5, 4, 5, 6, 7, 8, 9, 8, 9, 10, 11, 12, 13, 12, 13, 14, 15, 16, 17, 16, 17, 18, 19, 20, 21, 20, 21, 22, 23, 24, 25, 24, 25, 26, 27, 28, 29, 28, 29, 30, 31, 32, 1 };
        private static readonly int[] P = { 16, 7, 20, 21, 29, 12, 28, 17, 1, 15, 23, 26, 5, 18, 31, 10, 2, 8, 24, 14, 32, 27, 3, 9, 19, 13, 30, 6, 22, 11, 4, 25 };
        private static readonly int[] Pc1 = { 57, 49, 41, 33, 25, 17, 9, 1, 58, 50, 42, 34, 26, 18, 10, 2, 59, 51, 43, 35, 27, 19, 11, 3, 60, 52, 44, 36, 63, 55, 47, 39, 31, 23, 15, 7, 62, 54, 46, 38, 30, 22, 14, 6, 61, 53, 45, 37, 29, 21, 13, 5, 28, 20, 12, 4 };
        private static readonly int[] Pc2 = { 14, 17, 11, 24, 1, 5, 3, 28, 15, 6, 21, 10, 23, 19, 12, 4, 26, 8, 16, 7, 27, 20, 13, 2, 41, 52, 31, 37, 47, 55, 30, 40, 51, 45, 33, 48, 44, 49, 39, 56, 34, 53, 46, 42, 50, 36, 29, 32 };
        private static readonly int[] Shifts = { 1, 1, 2, 2, 2, 2, 2, 2, 1, 2, 2, 2, 2, 2, 2, 1 };

        /// <summary>The eight S-boxes of DES: four rows of 16 values each (row = first and last input bit, column = the four bits between).</summary>
        public static readonly int[][] StandardBoxes =
        {
            new[] { 14, 4, 13, 1, 2, 15, 11, 8, 3, 10, 6, 12, 5, 9, 0, 7, 0, 15, 7, 4, 14, 2, 13, 1, 10, 6, 12, 11, 9, 5, 3, 8, 4, 1, 14, 8, 13, 6, 2, 11, 15, 12, 9, 7, 3, 10, 5, 0, 15, 12, 8, 2, 4, 9, 1, 7, 5, 11, 3, 14, 10, 0, 6, 13 },
            new[] { 15, 1, 8, 14, 6, 11, 3, 4, 9, 7, 2, 13, 12, 0, 5, 10, 3, 13, 4, 7, 15, 2, 8, 14, 12, 0, 1, 10, 6, 9, 11, 5, 0, 14, 7, 11, 10, 4, 13, 1, 5, 8, 12, 6, 9, 3, 2, 15, 13, 8, 10, 1, 3, 15, 4, 2, 11, 6, 7, 12, 0, 5, 14, 9 },
            new[] { 10, 0, 9, 14, 6, 3, 15, 5, 1, 13, 12, 7, 11, 4, 2, 8, 13, 7, 0, 9, 3, 4, 6, 10, 2, 8, 5, 14, 12, 11, 15, 1, 13, 6, 4, 9, 8, 15, 3, 0, 11, 1, 2, 12, 5, 10, 14, 7, 1, 10, 13, 0, 6, 9, 8, 7, 4, 15, 14, 3, 11, 5, 2, 12 },
            new[] { 7, 13, 14, 3, 0, 6, 9, 10, 1, 2, 8, 5, 11, 12, 4, 15, 13, 8, 11, 5, 6, 15, 0, 3, 4, 7, 2, 12, 1, 10, 14, 9, 10, 6, 9, 0, 12, 11, 7, 13, 15, 1, 3, 14, 5, 2, 8, 4, 3, 15, 0, 6, 10, 1, 13, 8, 9, 4, 5, 11, 12, 7, 2, 14 },
            new[] { 2, 12, 4, 1, 7, 10, 11, 6, 8, 5, 3, 15, 13, 0, 14, 9, 14, 11, 2, 12, 4, 7, 13, 1, 5, 0, 15, 10, 3, 9, 8, 6, 4, 2, 1, 11, 10, 13, 7, 8, 15, 9, 12, 5, 6, 3, 0, 14, 11, 8, 12, 7, 1, 14, 2, 13, 6, 15, 0, 9, 10, 4, 5, 3 },
            new[] { 12, 1, 10, 15, 9, 2, 6, 8, 0, 13, 3, 4, 14, 7, 5, 11, 10, 15, 4, 2, 7, 12, 9, 5, 6, 1, 13, 14, 0, 11, 3, 8, 9, 14, 15, 5, 2, 8, 12, 3, 7, 0, 4, 10, 1, 13, 11, 6, 4, 3, 2, 12, 9, 5, 15, 10, 11, 14, 1, 7, 6, 0, 8, 13 },
            new[] { 4, 11, 2, 14, 15, 0, 8, 13, 3, 12, 9, 7, 5, 10, 6, 1, 13, 0, 11, 7, 4, 9, 1, 10, 14, 3, 5, 12, 2, 15, 8, 6, 1, 4, 11, 13, 12, 3, 7, 14, 10, 15, 6, 8, 0, 5, 9, 2, 6, 11, 13, 8, 1, 4, 10, 7, 9, 5, 0, 15, 14, 2, 3, 12 },
            new[] { 13, 2, 8, 4, 6, 15, 11, 1, 10, 9, 3, 14, 5, 0, 12, 7, 1, 15, 13, 8, 10, 3, 7, 4, 12, 5, 6, 11, 0, 14, 9, 2, 7, 11, 4, 1, 9, 12, 14, 2, 0, 6, 10, 13, 15, 3, 5, 8, 2, 1, 14, 7, 4, 10, 8, 13, 15, 12, 9, 0, 3, 5, 6, 11 }
        };

        /// <summary>The single S-box of DESL, used eight times.</summary>
        public static readonly int[] LightweightBox = { 14, 5, 7, 2, 11, 8, 1, 15, 0, 10, 9, 4, 6, 13, 12, 3, 5, 0, 8, 15, 14, 3, 2, 12, 11, 7, 6, 9, 13, 4, 1, 10, 4, 9, 2, 14, 8, 7, 13, 0, 10, 12, 15, 1, 5, 11, 3, 6, 9, 6, 15, 5, 3, 8, 4, 11, 7, 1, 12, 2, 0, 14, 10, 13 };

        private readonly int[][] _boxes;
        private readonly ulong[] _subkeys = new ulong[16];

        public DesEngineVariant(bool lightweight, byte[] key)
        {
            _boxes = lightweight ? Enumerable.Repeat(LightweightBox, 8).ToArray() : StandardBoxes;
            ulong k = ToNumber(key, 0);
            ulong cd = Permute(k, Pc1, 64);
            uint c = (uint)(cd >> 28), d = (uint)(cd & 0xfffffff);
            for (int round = 0; round < 16; round++)
            {
                for (int s = 0; s < Shifts[round]; s++) { c = ((c << 1) | (c >> 27)) & 0xfffffff; d = ((d << 1) | (d >> 27)) & 0xfffffff; }
                _subkeys[round] = Permute(((ulong)c << 28) | d, Pc2, 56);
            }
        }

        private static ulong ToNumber(byte[] data, int offset) { ulong v = 0; for (int i = 0; i < 8; i++) { v = (v << 8) | data[offset + i]; } return v; }

        /// <summary>Bit i of the result is the bit of the input that the table names (bits are counted from 1 at the most significant end of an input of the given width).</summary>
        private static ulong Permute(ulong input, int[] table, int width)
        {
            ulong result = 0;
            foreach (int position in table) { result = (result << 1) | ((input >> (width - position)) & 1); }
            return result;
        }

        private uint Feistel(uint half, ulong subkey)
        {
            ulong x = Permute(half, Expansion, 32) ^ subkey;
            uint output = 0;
            for (int box = 0; box < 8; box++)
            {
                int six = (int)(x >> (42 - 6 * box)) & 0x3f;
                int row = ((six >> 4) & 2) | (six & 1), column = (six >> 1) & 15;
                output = (output << 4) | (uint)_boxes[box][row * 16 + column];
            }
            return (uint)Permute(output, P, 32);
        }

        public ulong Process(ulong block, bool encrypt)
        {
            ulong ip = Permute(block, Ip, 64);
            uint left = (uint)(ip >> 32), right = (uint)ip;
            for (int round = 0; round < 16; round++)
            {
                uint next = left ^ Feistel(right, _subkeys[encrypt ? round : 15 - round]);
                left = right; right = next;
            }
            return Permute(((ulong)right << 32) | left, Fp, 64);
        }

        public byte[] ProcessBlock(byte[] data, int offset, bool encrypt)
        {
            ulong value = Process(ToNumber(data, offset), encrypt);
            var result = new byte[8];
            for (int i = 7; i >= 0; i--) { result[i] = (byte)value; value >>= 8; }
            return result;
        }

        public static ulong Number(byte[] data, int offset) => ToNumber(data, offset);
    }

    public static class DesFamily
    {
        public static readonly string[] Names = { "DES", "DESL", "DESX", "DESXL" };

        /// <summary>The key length in bytes: 8 for DES and DESL, 24 for DESX and DESXL (DES key, pre-whitening and post-whitening key, 8 bytes each).</summary>
        public static int KeyLength(int variant) => variant < 2 ? 8 : 24;

        public static byte[] Process(int variant, byte[] key, byte[] data, bool encrypt)
        {
            if (key.Length != KeyLength(variant)) { throw new ArgumentException(Names[variant] + " needs a key of " + KeyLength(variant) + " bytes."); }
            if (data.Length % 8 != 0) { throw new ArgumentException("The data must be a whole number of 8-byte blocks."); }
            var engine = new DesEngineVariant(variant == 1 || variant == 3, key);
            bool whitening = variant >= 2;
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i += 8)
            {
                var block = data.Skip(i).Take(8).ToArray();
                if (whitening && encrypt) { block = Xor(block, key.Skip(8).Take(8).ToArray()); }
                if (whitening && !encrypt) { block = Xor(block, key.Skip(16).Take(8).ToArray()); }
                block = engine.ProcessBlock(block, 0, encrypt);
                if (whitening && encrypt) { block = Xor(block, key.Skip(16).Take(8).ToArray()); }
                if (whitening && !encrypt) { block = Xor(block, key.Skip(8).Take(8).ToArray()); }
                Array.Copy(block, 0, result, i, 8);
            }
            return result;
        }

        private static byte[] Xor(byte[] a, byte[] b) => a.Select((x, i) => (byte)(x ^ b[i])).ToArray();

        public static byte[] FromHex(string hex)
        {
            hex = new string(hex.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (hex.Length % 2 != 0) { throw new FormatException("A hexadecimal value needs an even number of digits."); }
            return Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
        }

        public static string ToHex(byte[] data) => BitConverter.ToString(data).Replace("-", "");
    }

}
