using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Encodings;

namespace HybridEncryption
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("HybridEncryption.Properties.Resources", "PluginCaption", "PluginTooltip", "HybridEncryption/DetailedDescription/doc.xml", "HybridEncryption/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernAsymmetric)]
    public class HybridEncryption : ICrypComponent
    {
        private HybridEncryptionSettings _settings = new HybridEncryptionSettings();

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
            set => _settings = (HybridEncryptionSettings)value;
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
                AsymmetricKeyParameter key = HybridEncryptionScheme.ReadKey(Key);
                if (_settings.Mode == 0)
                {
                    byte[] packet = HybridEncryptionScheme.Encrypt(Encoding.UTF8.GetBytes(Input), key is RsaPrivateCrtKeyParameters crt ? new RsaKeyParameters(false, crt.Modulus, crt.PublicExponent) : key);
                    Output = Convert.ToBase64String(packet);
                }
                else { Output = Encoding.UTF8.GetString(HybridEncryptionScheme.Decrypt(Convert.FromBase64String(Input.Trim()), key)); }
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
    /// Hybrid encryption: a random AES-256 key encrypts the message with AES-GCM (fast, authenticated), and the AES key is encrypted with the RSA public key of the receiver
    /// (RSA-OAEP with SHA-256). The packet is: length of the wrapped key (2 bytes), wrapped key, nonce (12 bytes), ciphertext and tag.
    /// </summary>
    public static class HybridEncryptionScheme
    {
        public static AsymmetricKeyParameter ReadKey(string pem)
        {
            using (var reader = new System.IO.StringReader(pem))
            {
                object read = new Org.BouncyCastle.OpenSsl.PemReader(reader).ReadObject();
                if (read is AsymmetricCipherKeyPair pair) { return pair.Private; }
                var key = read as RsaKeyParameters;
                if (key == null) { throw new ArgumentException("The PEM block is not an RSA key."); }
                return key;
            }
        }

        private static OaepEncoding Oaep(bool encrypt, AsymmetricKeyParameter key)
        {
            var oaep = new OaepEncoding(new RsaEngine(), new Sha256Digest(), new Sha256Digest(), null);
            oaep.Init(encrypt, key);
            return oaep;
        }

        public static byte[] Encrypt(byte[] message, AsymmetricKeyParameter publicKey)
        {
            var random = new SecureRandom();
            byte[] aesKey = random.GenerateSeed(32), nonce = random.GenerateSeed(12);
            byte[] wrapped = Oaep(true, publicKey).ProcessBlock(aesKey, 0, aesKey.Length);
            var gcm = new GcmBlockCipher(new AesEngine());
            gcm.Init(true, new AeadParameters(new KeyParameter(aesKey), 128, nonce));
            byte[] ciphertext = new byte[gcm.GetOutputSize(message.Length)];
            gcm.DoFinal(ciphertext, gcm.ProcessBytes(message, 0, message.Length, ciphertext, 0));
            return new[] { (byte)(wrapped.Length >> 8), (byte)wrapped.Length }.Concat(wrapped).Concat(nonce).Concat(ciphertext).ToArray();
        }

        public static byte[] Decrypt(byte[] packet, AsymmetricKeyParameter privateKey)
        {
            if (!privateKey.IsPrivate) { throw new ArgumentException("The private key is needed to decrypt."); }
            int length = packet[0] << 8 | packet[1];
            byte[] wrapped = packet.Skip(2).Take(length).ToArray(), nonce = packet.Skip(2 + length).Take(12).ToArray(), ciphertext = packet.Skip(14 + length).ToArray();
            byte[] aesKey;
            try { aesKey = Oaep(false, privateKey).ProcessBlock(wrapped, 0, wrapped.Length); }
            catch (InvalidCipherTextException) { throw new ArgumentException("The key does not fit: the wrapped AES key cannot be decrypted."); }
            var gcm = new GcmBlockCipher(new AesEngine());
            gcm.Init(false, new AeadParameters(new KeyParameter(aesKey), 128, nonce));
            byte[] plaintext = new byte[gcm.GetOutputSize(ciphertext.Length)];
            try { gcm.DoFinal(plaintext, gcm.ProcessBytes(ciphertext, 0, ciphertext.Length, plaintext, 0)); }
            catch (InvalidCipherTextException) { throw new ArgumentException("The message was changed: the authentication tag is wrong."); }
            return plaintext;
        }
    }

}
