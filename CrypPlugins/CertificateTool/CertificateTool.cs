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
using System.Collections.Generic;

namespace CertificateTool
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("CertificateTool.Properties.Resources", "PluginCaption", "PluginTooltip", "CertificateTool/DetailedDescription/doc.xml", "CertificateTool/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class CertificateTool : ICrypComponent
    {
        private CertificateToolSettings _settings = new CertificateToolSettings();

        [PropertyInfo(Direction.InputData, "InputCaption", "InputTooltip", false)]
        public string Input { get; set; }

        [PropertyInfo(Direction.OutputData, "ChainCaption", "ChainTooltip", false)]
        public string Chain
        {
            get => _chain;
            set { _chain = value; OnPropertyChanged("Chain"); }
        }
        private string _chain;

        [PropertyInfo(Direction.OutputData, "ValidCaption", "ValidTooltip", false)]
        public string Valid
        {
            get => _valid;
            set { _valid = value; OnPropertyChanged("Valid"); }
        }
        private string _valid;

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
            set => _settings = (CertificateToolSettings)value;
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
                var report = new StringBuilder();
                if (_settings.Mode == 0)
                {
                    var demo = Certificates.CreateChain(string.IsNullOrWhiteSpace(Input) ? "www.example.org" : Input.Trim(), DateTime.UtcNow);
                    Chain = demo.Chain;
                    foreach (var certificate in new[] { demo.Leaf, demo.Intermediate, demo.Root }) { report.AppendLine(Certificates.Describe(certificate)); report.AppendLine(); }
                    Valid = Certificates.VerifyChain(new List<X509Certificate> { demo.Leaf, demo.Intermediate, demo.Root }, DateTime.UtcNow, report).ToString();
                }
                else
                {
                    var chain = Certificates.Read(Input);
                    Chain = Input;
                    if (_settings.Mode == 1) { Valid = Certificates.VerifyChain(chain, DateTime.UtcNow, report).ToString(); }
                    else { foreach (var certificate in chain) { report.AppendLine(Certificates.Describe(certificate)); report.AppendLine(); } Valid = ""; }
                }
                Report = report.ToString().TrimEnd();
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
            Input = Chain = Valid = Report = null;
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


    public static class Certificates
    {
        public static X509Certificate Issue(string subject, AsymmetricKeyParameter subjectKey, string issuer, AsymmetricKeyParameter issuerPrivateKey, bool authority, int serial, DateTime from, DateTime to)
        {
            var generator = new X509V3CertificateGenerator();
            generator.SetSerialNumber(BcInt.ValueOf(serial));
            generator.SetIssuerDN(new X509Name(issuer));
            generator.SetSubjectDN(new X509Name(subject));
            generator.SetNotBefore(from);
            generator.SetNotAfter(to);
            generator.SetPublicKey(subjectKey);
            generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(authority));
            generator.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(authority ? KeyUsage.KeyCertSign | KeyUsage.CrlSign : KeyUsage.DigitalSignature));
            string algorithm = issuerPrivateKey is RsaKeyParameters ? "SHA256WITHRSA" : "SHA256WITHECDSA";
            return generator.Generate(new Asn1SignatureFactory(algorithm, issuerPrivateKey, new SecureRandom()));
        }

        public sealed class Demo
        {
            public X509Certificate Root, Intermediate, Leaf;
            public AsymmetricCipherKeyPair LeafKeys;
            public string Chain => ToPem(Leaf) + "\n" + ToPem(Intermediate) + "\n" + ToPem(Root);
        }

        /// <summary>A chain of three certificates (root CA, intermediate CA, server certificate for the host name) with EC keys.</summary>
        public static Demo CreateChain(string host, DateTime now)
        {
            var rootKeys = KeyTools.GenerateEc("secp256r1");
            var intermediateKeys = KeyTools.GenerateEc("secp256r1");
            var demo = new Demo { LeafKeys = KeyTools.GenerateEc("secp256r1") };
            demo.Root = Issue("CN=Demo Root CA,O=CrypTool", rootKeys.Public, "CN=Demo Root CA,O=CrypTool", rootKeys.Private, true, 1, now.AddDays(-1), now.AddYears(10));
            demo.Intermediate = Issue("CN=Demo Intermediate CA,O=CrypTool", intermediateKeys.Public, "CN=Demo Root CA,O=CrypTool", rootKeys.Private, true, 2, now.AddDays(-1), now.AddYears(5));
            demo.Leaf = Issue("CN=" + host + ",O=CrypTool", demo.LeafKeys.Public, "CN=Demo Intermediate CA,O=CrypTool", intermediateKeys.Private, false, 3, now.AddDays(-1), now.AddYears(1));
            return demo;
        }

        public static string ToPem(X509Certificate certificate)
        {
            using (var writer = new StringWriter())
            {
                new Org.BouncyCastle.Utilities.IO.Pem.PemWriter(writer).WriteObject(new PemObject("CERTIFICATE", certificate.GetEncoded()));
                return writer.ToString().Replace("\r\n", "\n").TrimEnd();
            }
        }

        public static List<X509Certificate> Read(string pem)
        {
            var result = new List<X509Certificate>();
            using (var reader = new StringReader(pem))
            {
                var pemReader = new Org.BouncyCastle.Utilities.IO.Pem.PemReader(reader);
                PemObject block;
                while ((block = pemReader.ReadPemObject()) != null) { if (block.Type == "CERTIFICATE") { result.Add(new X509CertificateParser().ReadCertificate(block.Content)); } }
            }
            if (result.Count == 0) { throw new ArgumentException("No certificate found: the input must contain PEM blocks of type CERTIFICATE."); }
            return result;
        }

        public static string Describe(X509Certificate c)
        {
            return "Subject: " + c.SubjectDN + "\nIssuer: " + c.IssuerDN + "\nSerial number: " + c.SerialNumber.ToString(16) + "\nValid from " + c.NotBefore.ToString("yyyy-MM-dd") + " to " + c.NotAfter.ToString("yyyy-MM-dd")
                + "\nSignature algorithm: " + c.SigAlgName + "\nCertification authority: " + (c.GetBasicConstraints() >= 0 ? "yes" : "no")
                + "\nPublic key: " + (c.GetPublicKey() is RsaKeyParameters rsa ? "RSA " + rsa.Modulus.BitLength + " bits" : "EC");
        }

        /// <summary>Checks a chain (server certificate first, root last): every certificate is signed by the next one, is valid at the time, and the issuers are certification authorities; the last one signs itself.</summary>
        public static bool VerifyChain(List<X509Certificate> chain, DateTime now, StringBuilder report)
        {
            bool valid = true;
            for (int i = 0; i < chain.Count; i++)
            {
                var certificate = chain[i];
                var issuer = i + 1 < chain.Count ? chain[i + 1] : certificate;
                string name = certificate.SubjectDN.ToString();
                bool signature = true, dates = now >= certificate.NotBefore && now <= certificate.NotAfter, authority = i + 1 >= chain.Count ? certificate.GetBasicConstraints() >= 0 : issuer.GetBasicConstraints() >= 0;
                try { certificate.Verify(issuer.GetPublicKey()); } catch (Exception) { signature = false; }
                bool names = certificate.IssuerDN.Equivalent(issuer.SubjectDN);
                report.AppendLine(name + ": signature by " + issuer.SubjectDN + (signature ? " correct" : " WRONG") + ", " + (dates ? "valid at the time" : "NOT valid at the time") + ", " + (names ? "issuer name matches" : "issuer name DOES NOT MATCH") + ", " + (authority ? "issuer is a CA" : "issuer is NOT a CA"));
                valid &= signature && dates && authority && names;
            }
            report.AppendLine(valid ? "The chain is valid (the root certificate has to be trusted)." : "The chain is NOT valid.");
            return valid;
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
