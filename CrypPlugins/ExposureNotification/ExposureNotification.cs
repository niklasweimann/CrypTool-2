using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace ExposureNotification
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("ExposureNotification.Properties.Resources", "PluginCaption", "PluginTooltip", "ExposureNotification/DetailedDescription/doc.xml", "ExposureNotification/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class ExposureNotification : ICrypComponent
    {
        private ExposureNotificationSettings _settings = new ExposureNotificationSettings();

        [PropertyInfo(Direction.InputData, "TemporaryExposureKeyCaption", "TemporaryExposureKeyTooltip", true)]
        public string TemporaryExposureKey { get; set; }

        [PropertyInfo(Direction.InputData, "IntervalNumberCaption", "IntervalNumberTooltip", true)]
        public string IntervalNumber { get; set; }

        [PropertyInfo(Direction.InputData, "MetadataCaption", "MetadataTooltip", false)]
        public string Metadata { get; set; }

        [PropertyInfo(Direction.OutputData, "RollingProximityIdentifierCaption", "RollingProximityIdentifierTooltip", false)]
        public string RollingProximityIdentifier
        {
            get => _rollingProximityIdentifier;
            set { _rollingProximityIdentifier = value; OnPropertyChanged("RollingProximityIdentifier"); }
        }
        private string _rollingProximityIdentifier;

        [PropertyInfo(Direction.OutputData, "EncryptedMetadataCaption", "EncryptedMetadataTooltip", false)]
        public string EncryptedMetadata
        {
            get => _encryptedMetadata;
            set { _encryptedMetadata = value; OnPropertyChanged("EncryptedMetadata"); }
        }
        private string _encryptedMetadata;

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
            set => _settings = (ExposureNotificationSettings)value;
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
                byte[] key = ExposureNotificationKeys.Hex(TemporaryExposureKey);
                if (key.Length != 16) { throw new ArgumentException("The Temporary Exposure Key has 16 bytes (32 hexadecimal digits)."); }
                uint interval = uint.Parse(IntervalNumber.Trim());
                byte[] identifier = ExposureNotificationKeys.RollingProximityIdentifier(key, interval);
                RollingProximityIdentifier = ExposureNotificationKeys.ToHex(identifier);
                if (!string.IsNullOrWhiteSpace(Metadata))
                {
                    byte[] metadata = ExposureNotificationKeys.Hex(Metadata);
                    if (metadata.Length != 4) { throw new ArgumentException("The metadata have 4 bytes (8 hexadecimal digits)."); }
                    EncryptedMetadata = ExposureNotificationKeys.ToHex(ExposureNotificationKeys.EncryptMetadata(key, identifier, metadata));
                }
                Report = string.Format(Properties.Resources.KeyReport, ExposureNotificationKeys.ToHex(ExposureNotificationKeys.RollingProximityKey(key)), ExposureNotificationKeys.ToHex(ExposureNotificationKeys.MetadataKey(key)), interval, RollingProximityIdentifier);
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
            TemporaryExposureKey = IntervalNumber = Metadata = RollingProximityIdentifier = EncryptedMetadata = Report = null;
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
    /// The cryptography of the Exposure Notification framework of Apple and Google (used by the Corona-Warn-App): from a daily Temporary Exposure Key the keys
    /// RPIK and AEMK are derived with HKDF-SHA-256, and every 10 minutes a new Rolling Proximity Identifier is made with AES-128; the metadata is encrypted with AES-128-CTR.
    /// </summary>
    public static class ExposureNotificationKeys
    {
        public static byte[] Hkdf(byte[] key, byte[] info, int length)
        {
            // HKDF (RFC 5869) with SHA-256 and an empty salt
            byte[] prk;
            using (var extract = new System.Security.Cryptography.HMACSHA256(new byte[32])) { prk = extract.ComputeHash(key); }
            var result = new List<byte>();
            byte[] previous = new byte[0];
            using (var expand = new System.Security.Cryptography.HMACSHA256(prk))
            {
                for (byte counter = 1; result.Count < length; counter++) { previous = expand.ComputeHash(previous.Concat(info).Concat(new[] { counter }).ToArray()); result.AddRange(previous); }
            }
            return result.Take(length).ToArray();
        }

        private static byte[] AesBlock(byte[] key, byte[] block)
        {
            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Key = key;
                aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                aes.Padding = System.Security.Cryptography.PaddingMode.None;
                using (var encryptor = aes.CreateEncryptor()) { return encryptor.TransformFinalBlock(block, 0, 16); }
            }
        }

        public static byte[] RollingProximityKey(byte[] temporaryExposureKey) => Hkdf(temporaryExposureKey, Encoding.ASCII.GetBytes("EN-RPIK"), 16);

        public static byte[] MetadataKey(byte[] temporaryExposureKey) => Hkdf(temporaryExposureKey, Encoding.ASCII.GetBytes("EN-AEMK"), 16);

        /// <summary>RPI = AES-128(RPIK, "EN-RPI" || 6 zero bytes || interval number as 4 bytes, little endian).</summary>
        public static byte[] RollingProximityIdentifier(byte[] temporaryExposureKey, uint interval)
        {
            var block = new byte[16];
            Array.Copy(Encoding.ASCII.GetBytes("EN-RPI"), block, 6);
            block[12] = (byte)interval; block[13] = (byte)(interval >> 8); block[14] = (byte)(interval >> 16); block[15] = (byte)(interval >> 24);
            return AesBlock(RollingProximityKey(temporaryExposureKey), block);
        }

        /// <summary>AES-128-CTR with the identifier as initial counter block: the metadata (4 bytes) is XORed with the first bytes of AES(AEMK, RPI).</summary>
        public static byte[] EncryptMetadata(byte[] temporaryExposureKey, byte[] identifier, byte[] metadata)
        {
            byte[] stream = AesBlock(MetadataKey(temporaryExposureKey), identifier);
            return metadata.Select((b, i) => (byte)(b ^ stream[i])).ToArray();
        }

        public static byte[] Hex(string text)
        {
            text = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (text.Length % 2 != 0) { throw new FormatException("A hexadecimal value needs an even number of digits."); }
            return Enumerable.Range(0, text.Length / 2).Select(i => Convert.ToByte(text.Substring(i * 2, 2), 16)).ToArray();
        }

        public static string ToHex(byte[] data) => BitConverter.ToString(data).Replace("-", "");
    }

}
