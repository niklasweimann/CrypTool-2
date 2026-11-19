using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace OneTimePassword
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("OneTimePassword.Properties.Resources", "PluginCaption", "PluginTooltip", "OneTimePassword/DetailedDescription/doc.xml", "OneTimePassword/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class OneTimePassword : ICrypComponent
    {
        private OneTimePasswordSettings _settings = new OneTimePasswordSettings();

        [PropertyInfo(Direction.InputData, "SecretCaption", "SecretTooltip", true)]
        public string Secret { get; set; }

        [PropertyInfo(Direction.InputData, "CounterOrTimeCaption", "CounterOrTimeTooltip", true)]
        public string CounterOrTime { get; set; }

        [PropertyInfo(Direction.OutputData, "CodeCaption", "CodeTooltip", false)]
        public string Code
        {
            get => _code;
            set { _code = value; OnPropertyChanged("Code"); }
        }
        private string _code;

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (OneTimePasswordSettings)value;
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
                byte[] key = _settings.SecretFormat == 1 ? OneTimePasswords.Base32(Secret) : _settings.SecretFormat == 2 ? OneTimePasswords.Hex(Secret) : Encoding.ASCII.GetBytes(Secret);
                long number = long.Parse(CounterOrTime.Trim());
                Code = _settings.Mode == 0 ? OneTimePasswords.Hotp(key, number, _settings.Digits, _settings.Algorithm) : OneTimePasswords.Totp(key, number, _settings.Period, _settings.Digits, _settings.Algorithm);
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
            Secret = CounterOrTime = Code = null;
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


    public static class OneTimePasswords
    {
        public static byte[] Base32(string text)
        {
            const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            var bytes = new List<byte>();
            int buffer = 0, bits = 0;
            foreach (char c in text.ToUpperInvariant().Where(ch => ch != '=' && !char.IsWhiteSpace(ch)))
            {
                int value = Alphabet.IndexOf(c);
                if (value < 0) { throw new FormatException("Not a Base32 character: " + c); }
                buffer = buffer << 5 | value;
                bits += 5;
                if (bits >= 8) { bits -= 8; bytes.Add((byte)(buffer >> bits & 0xff)); }
            }
            return bytes.ToArray();
        }

        public static byte[] Hex(string text)
        {
            text = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (text.Length % 2 != 0) { throw new FormatException("A hexadecimal value needs an even number of digits."); }
            return Enumerable.Range(0, text.Length / 2).Select(i => Convert.ToByte(text.Substring(i * 2, 2), 16)).ToArray();
        }

        private static byte[] Hmac(int algorithm, byte[] key, byte[] data)
        {
            using (System.Security.Cryptography.HMAC hmac = algorithm == 0 ? (System.Security.Cryptography.HMAC)new System.Security.Cryptography.HMACSHA1(key) : algorithm == 1 ? new System.Security.Cryptography.HMACSHA256(key) : new System.Security.Cryptography.HMACSHA512(key))
            {
                return hmac.ComputeHash(data);
            }
        }

        /// <summary>HOTP (RFC 4226): HMAC of the counter (8 bytes, big endian), dynamic truncation to 31 bits, modulo 10^digits.</summary>
        public static string Hotp(byte[] key, long counter, int digits, int algorithm)
        {
            var message = new byte[8];
            for (int i = 7; i >= 0; i--) { message[i] = (byte)(counter & 0xff); counter >>= 8; }
            byte[] hash = Hmac(algorithm, key, message);
            int offset = hash[hash.Length - 1] & 0x0f;
            long binary = (hash[offset] & 0x7f) << 24 | hash[offset + 1] << 16 | hash[offset + 2] << 8 | hash[offset + 3];
            long modulus = 1;
            for (int i = 0; i < digits; i++) { modulus *= 10; }
            return (binary % modulus).ToString().PadLeft(digits, '0');
        }

        /// <summary>TOTP (RFC 6238): HOTP with the counter (time - start) / period.</summary>
        public static string Totp(byte[] key, long time, int period, int digits, int algorithm) => Hotp(key, time / period, digits, algorithm);
    }

}
