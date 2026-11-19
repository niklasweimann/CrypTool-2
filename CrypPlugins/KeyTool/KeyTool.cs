using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using BcInt = Org.BouncyCastle.Math.BigInteger;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO.Pem;
using Org.BouncyCastle.X509;
using System.IO;

namespace KeyTool
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("KeyTool.Properties.Resources", "PluginCaption", "PluginTooltip", "KeyTool/DetailedDescription/doc.xml", "KeyTool/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class KeyTool : ICrypComponent
    {
        private KeyToolSettings _settings = new KeyToolSettings();

        [PropertyInfo(Direction.InputData, "InputCaption", "InputTooltip", false)]
        public string Input { get; set; }

        [PropertyInfo(Direction.OutputData, "PrivateKeyCaption", "PrivateKeyTooltip", false)]
        public string PrivateKey
        {
            get => _privateKey;
            set { _privateKey = value; OnPropertyChanged("PrivateKey"); }
        }
        private string _privateKey;

        [PropertyInfo(Direction.OutputData, "PublicKeyCaption", "PublicKeyTooltip", false)]
        public string PublicKey
        {
            get => _publicKey;
            set { _publicKey = value; OnPropertyChanged("PublicKey"); }
        }
        private string _publicKey;

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
            set => _settings = (KeyToolSettings)value;
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
                if (_settings.Mode == 2)
                {
                    AsymmetricKeyParameter publicKey;
                    AsymmetricKeyParameter privateKey = KeyTools.ReadKey(Input, out publicKey);
                    PrivateKey = privateKey == null ? "" : KeyTools.PrivatePem(privateKey);
                    PublicKey = publicKey == null ? "" : KeyTools.PublicPem(publicKey);
                    Report = KeyTools.Describe(privateKey, publicKey);
                }
                else
                {
                    AsymmetricCipherKeyPair pair = _settings.Mode == 0 ? KeyTools.GenerateRsa(_settings.KeySize) : KeyTools.GenerateEc(KeyTools.Curves[_settings.Curve]);
                    PrivateKey = KeyTools.PrivatePem(pair.Private);
                    PublicKey = KeyTools.PublicPem(pair.Public);
                    Report = KeyTools.Describe(pair.Private, pair.Public);
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
            Input = PrivateKey = PublicKey = Report = null;
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


    public static class KeyTools
    {
        public static readonly string[] Curves = { "secp256r1", "secp384r1", "secp521r1", "secp256k1" };

        public static AsymmetricCipherKeyPair GenerateRsa(int bits)
        {
            var generator = new RsaKeyPairGenerator();
            generator.Init(new KeyGenerationParameters(new SecureRandom(), bits));
            return generator.GenerateKeyPair();
        }

        public static AsymmetricCipherKeyPair GenerateEc(string curve)
        {
            var generator = new ECKeyPairGenerator();
            generator.Init(new ECKeyGenerationParameters(Org.BouncyCastle.Asn1.X9.ECNamedCurveTable.GetOid(curve), new SecureRandom()));
            return generator.GenerateKeyPair();
        }

        private static string Pem(string type, byte[] content)
        {
            using (var writer = new StringWriter())
            {
                new Org.BouncyCastle.Utilities.IO.Pem.PemWriter(writer).WriteObject(new PemObject(type, content));
                return writer.ToString().Replace("\r\n", "\n").TrimEnd();
            }
        }

        /// <summary>The private key as PKCS#8 ("PRIVATE KEY").</summary>
        public static string PrivatePem(AsymmetricKeyParameter key) => Pem("PRIVATE KEY", Org.BouncyCastle.Pkcs.PrivateKeyInfoFactory.CreatePrivateKeyInfo(key).GetEncoded());

        /// <summary>The public key as SubjectPublicKeyInfo ("PUBLIC KEY").</summary>
        public static string PublicPem(AsymmetricKeyParameter key) => Pem("PUBLIC KEY", SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(key).GetEncoded());

        /// <summary>An EC public key whose curve is given by its parameters gets the name (OID) of the standard curve, so that it is written in the usual compact form.</summary>
        private static AsymmetricKeyParameter Named(AsymmetricKeyParameter key)
        {
            if (key is ECPublicKeyParameters ec && ec.PublicKeyParamSet == null)
            {
                foreach (string name in Curves)
                {
                    var curve = Org.BouncyCastle.Asn1.X9.ECNamedCurveTable.GetByName(name);
                    if (curve.Curve.Equals(ec.Parameters.Curve) && curve.G.Equals(ec.Parameters.G)) { return new ECPublicKeyParameters("EC", ec.Q, Org.BouncyCastle.Asn1.X9.ECNamedCurveTable.GetOid(name)); }
                }
            }
            return key;
        }

        /// <summary>Reads a key from a PEM block (PKCS#8, PKCS#1, SEC1 or SubjectPublicKeyInfo); a private key gives the key pair.</summary>
        public static AsymmetricKeyParameter ReadKey(string pem, out AsymmetricKeyParameter publicKey)
        {
            using (var reader = new StringReader(pem))
            {
                object read = new Org.BouncyCastle.OpenSsl.PemReader(reader).ReadObject();
                if (read is AsymmetricCipherKeyPair pair) { publicKey = Named(pair.Public); return pair.Private; }
                var key = read as AsymmetricKeyParameter;
                if (key == null) { throw new ArgumentException("The PEM block is not a key."); }
                if (key.IsPrivate)
                {
                    if (key is RsaPrivateCrtKeyParameters rsa) { publicKey = new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent); }
                    else if (key is ECPrivateKeyParameters ec) { publicKey = Named(new ECPublicKeyParameters(ec.AlgorithmName, ec.Parameters.G.Multiply(ec.D).Normalize(), ec.PublicKeyParamSet)); }
                    else { publicKey = null; }
                    return key;
                }
                publicKey = Named(key);
                return null;
            }
        }

        public static string Describe(AsymmetricKeyParameter priv, AsymmetricKeyParameter pub)
        {
            var report = new StringBuilder();
            report.AppendLine(priv != null ? "Private key and public key" : "Public key");
            if (pub is RsaKeyParameters rsa)
            {
                report.AppendLine("Algorithm: RSA, modulus of " + rsa.Modulus.BitLength + " bits");
                report.AppendLine("Public exponent e: " + rsa.Exponent);
                report.AppendLine("Modulus n (hex): " + rsa.Modulus.ToString(16).ToUpperInvariant());
                if (priv is RsaPrivateCrtKeyParameters crt) { report.AppendLine("Prime p: " + crt.P.BitLength + " bits, prime q: " + crt.Q.BitLength + " bits"); }
            }
            else if (pub is ECPublicKeyParameters ec)
            {
                string name = ec.PublicKeyParamSet != null ? Org.BouncyCastle.Asn1.X9.ECNamedCurveTable.GetName(ec.PublicKeyParamSet) : "unnamed curve";
                report.AppendLine("Algorithm: EC, curve " + name + " (" + ec.Parameters.Curve.FieldSize + " bits)");
                report.AppendLine("Public point Q (hex): " + BitConverter.ToString(ec.Q.GetEncoded(false)).Replace("-", ""));
            }
            if (pub != null)
            {
                byte[] spki = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pub).GetEncoded();
                using (var sha = System.Security.Cryptography.SHA256.Create()) { report.Append("SHA-256 fingerprint of the public key: " + BitConverter.ToString(sha.ComputeHash(spki)).Replace("-", "")); }
            }
            return report.ToString();
        }
    }

}
