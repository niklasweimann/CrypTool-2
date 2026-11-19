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

namespace PasswordBasedEncryption
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("PasswordBasedEncryption.Properties.Resources", "PluginCaption", "PluginTooltip", "PasswordBasedEncryption/DetailedDescription/doc.xml", "PasswordBasedEncryption/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernSymmetric)]
    public class PasswordBasedEncryption : ICrypComponent
    {
        private PasswordBasedEncryptionSettings _settings = new PasswordBasedEncryptionSettings();

        [PropertyInfo(Direction.InputData, "InputCaption", "InputTooltip", true)]
        public string Input { get; set; }

        [PropertyInfo(Direction.InputData, "PasswordCaption", "PasswordTooltip", true)]
        public string Password { get; set; }

        [PropertyInfo(Direction.InputData, "SaltCaption", "SaltTooltip", false)]
        public string Salt { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputCaption", "OutputTooltip", false)]
        public string Output
        {
            get => _output;
            set { _output = value; OnPropertyChanged("Output"); }
        }
        private string _output;

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
            set => _settings = (PasswordBasedEncryptionSettings)value;
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
                if (_settings.Mode == 0)
                {
                    var random = new SecureRandom();
                    byte[] salt = string.IsNullOrWhiteSpace(Salt) ? random.GenerateSeed(16) : Enumerable.Range(0, Salt.Trim().Length / 2).Select(i => Convert.ToByte(Salt.Trim().Substring(i * 2, 2), 16)).ToArray();
                    byte[] encoded = Pbes2.Encrypt(Encoding.UTF8.GetBytes(Input), Password, salt, random.GenerateSeed(16), _settings.Iterations, _settings.Hash);
                    Output = Convert.ToBase64String(encoded);
                    Report = Pbes2.Describe(encoded);
                }
                else
                {
                    byte[] encoded = Convert.FromBase64String(Input.Trim());
                    Report = Pbes2.Describe(encoded);
                    Output = Encoding.UTF8.GetString(Pbes2.Decrypt(encoded, Password));
                }
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
            Input = Password = Salt = Output = Report = null;
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
    /// Password-based encryption scheme 2 (PBES2) of PKCS #5 / RFC 8018: the key is derived from the password with PBKDF2 (HMAC with SHA-1, SHA-256 or SHA-512, salt and iteration count),
    /// the data is encrypted with AES-256 in CBC mode with PKCS #7 padding, and the parameters are stored in an ASN.1 structure (EncryptedPrivateKeyInfo), the format of encrypted PKCS #8 keys.
    /// </summary>
    public static class Pbes2
    {
        private static IDigest Digest(int hash) => hash == 0 ? (IDigest)new Sha1Digest() : hash == 1 ? new Sha256Digest() : new Sha512Digest();

        private static DerObjectIdentifier PrfOid(int hash) => hash == 0 ? PkcsObjectIdentifiers.IdHmacWithSha1 : hash == 1 ? PkcsObjectIdentifiers.IdHmacWithSha256 : PkcsObjectIdentifiers.IdHmacWithSha512;

        private static byte[] DeriveKey(string password, byte[] salt, int iterations, IDigest digest, int keyBits)
        {
            var generator = new Pkcs5S2ParametersGenerator(digest);
            generator.Init(PbeParametersGenerator.Pkcs5PasswordToUtf8Bytes(password.ToCharArray()), salt, iterations);
            return ((KeyParameter)generator.GenerateDerivedParameters("AES" + keyBits, keyBits)).GetKey();
        }

        private static byte[] Crypt(bool encrypt, byte[] key, byte[] iv, byte[] data)
        {
            var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new AesEngine()), new Pkcs7Padding());
            cipher.Init(encrypt, new ParametersWithIV(new KeyParameter(key), iv));
            return cipher.DoFinal(data);
        }

        /// <summary>The DER encoding of an EncryptedPrivateKeyInfo with the PBES2 parameters (PBKDF2 and AES-256-CBC) and the encrypted data.</summary>
        public static byte[] Encrypt(byte[] data, string password, byte[] salt, byte[] iv, int iterations, int hash)
        {
            byte[] key = DeriveKey(password, salt, iterations, Digest(hash), 256);
            var kdf = new KeyDerivationFunc(PkcsObjectIdentifiers.IdPbkdf2, new Pbkdf2Params(salt, iterations, new AlgorithmIdentifier(PrfOid(hash), DerNull.Instance)));
            var scheme = new EncryptionScheme(NistObjectIdentifiers.IdAes256Cbc, new DerOctetString(iv));
            var algorithm = new AlgorithmIdentifier(PkcsObjectIdentifiers.IdPbeS2, new PbeS2Parameters(kdf, scheme));
            return new EncryptedPrivateKeyInfo(algorithm, Crypt(true, key, iv, data)).GetEncoded();
        }

        /// <summary>Reads the parameters from the ASN.1 structure, derives the key from the password and decrypts (AES-128, AES-192 and AES-256 in CBC mode).</summary>
        public static byte[] Decrypt(byte[] encoded, string password)
        {
            var info = EncryptedPrivateKeyInfo.GetInstance(Asn1Object.FromByteArray(encoded));
            if (!info.EncryptionAlgorithm.Algorithm.Equals(PkcsObjectIdentifiers.IdPbeS2)) { throw new ArgumentException("The data is not encrypted with PBES2."); }
            var parameters = PbeS2Parameters.GetInstance(info.EncryptionAlgorithm.Parameters);
            if (!parameters.KeyDerivationFunc.Algorithm.Equals(PkcsObjectIdentifiers.IdPbkdf2)) { throw new ArgumentException("Only PBKDF2 is supported."); }
            var pbkdf2 = Pbkdf2Params.GetInstance(parameters.KeyDerivationFunc.Parameters);
            var scheme = parameters.EncryptionScheme;
            string oid = scheme.Algorithm.Id;
            int keyBits = oid == NistObjectIdentifiers.IdAes128Cbc.Id ? 128 : oid == NistObjectIdentifiers.IdAes192Cbc.Id ? 192 : oid == NistObjectIdentifiers.IdAes256Cbc.Id ? 256 : 0;
            if (keyBits == 0) { throw new ArgumentException("Only AES in CBC mode is supported."); }
            string prf = pbkdf2.Prf.Algorithm.Id;
            IDigest digest = prf == PkcsObjectIdentifiers.IdHmacWithSha256.Id ? new Sha256Digest() : prf == PkcsObjectIdentifiers.IdHmacWithSha512.Id ? new Sha512Digest() : prf == PkcsObjectIdentifiers.IdHmacWithSha1.Id ? (IDigest)new Sha1Digest() : null;
            if (digest == null) { throw new ArgumentException("The hash function of PBKDF2 is not supported."); }
            byte[] key = DeriveKey(password, pbkdf2.GetSalt(), pbkdf2.IterationCount.IntValue, digest, keyBits);
            try { return Crypt(false, key, Asn1OctetString.GetInstance(scheme.Parameters).GetOctets(), info.GetEncryptedData()); }
            catch (InvalidCipherTextException) { throw new ArgumentException("Wrong password or damaged data."); }
        }

        public static string Describe(byte[] encoded)
        {
            var info = EncryptedPrivateKeyInfo.GetInstance(Asn1Object.FromByteArray(encoded));
            var parameters = PbeS2Parameters.GetInstance(info.EncryptionAlgorithm.Parameters);
            var pbkdf2 = Pbkdf2Params.GetInstance(parameters.KeyDerivationFunc.Parameters);
            return "Salt (hex): " + BitConverter.ToString(pbkdf2.GetSalt()).Replace("-", "") + "\nIterations: " + pbkdf2.IterationCount + "\nPRF: " + pbkdf2.Prf.Algorithm.Id + "\nCipher: " + parameters.EncryptionScheme.Algorithm.Id
                + "\nIV (hex): " + BitConverter.ToString(Asn1OctetString.GetInstance(parameters.EncryptionScheme.Parameters).GetOctets()).Replace("-", "") + "\nEncrypted data: " + info.GetEncryptedData().Length + " bytes";
        }
    }

}
