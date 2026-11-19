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
using Org.BouncyCastle.Asn1.Utilities;

namespace Asn1Viewer
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("Asn1Viewer.Properties.Resources", "PluginCaption", "PluginTooltip", "Asn1Viewer/DetailedDescription/doc.xml", "Asn1Viewer/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class Asn1Viewer : ICrypComponent
    {
        private Asn1ViewerSettings _settings = new Asn1ViewerSettings();

        [PropertyInfo(Direction.InputData, "InputCaption", "InputTooltip", true)]
        public string Input { get; set; }

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
            set => _settings = (Asn1ViewerSettings)value;
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
                byte[] bytes = Asn1Tools.ReadBytes(Input);
                Output = _settings.Mode == 0 ? Asn1Tools.Dump(bytes) : Asn1Tools.Signatures(bytes);
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
            Input = Output = null;
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


    public static class Asn1Tools
    {
        /// <summary>The bytes of a PEM block, of hexadecimal digits or of Base64.</summary>
        public static byte[] ReadBytes(string text)
        {
            text = text.Trim();
            if (text.Contains("-----BEGIN"))
            {
                using (var reader = new StringReader(text))
                {
                    PemObject pem = new Org.BouncyCastle.Utilities.IO.Pem.PemReader(reader).ReadPemObject();
                    if (pem == null) { throw new ArgumentException("No PEM block found."); }
                    return pem.Content;
                }
            }
            string compact = new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (compact.Length > 0 && compact.Length % 2 == 0 && compact.All(Uri.IsHexDigit)) { return Enumerable.Range(0, compact.Length / 2).Select(i => Convert.ToByte(compact.Substring(i * 2, 2), 16)).ToArray(); }
            return Convert.FromBase64String(compact);
        }

        public static string ToHex(byte[] data) => BitConverter.ToString(data).Replace("-", "");

        /// <summary>The structure of a DER or BER encoded ASN.1 object as an indented text.</summary>
        public static string Dump(byte[] encoded) => Asn1Dump.DumpAsString(Asn1Object.FromByteArray(encoded), true);

        /// <summary>The signatures of a CMS (PKCS#7) SignedData: signer, algorithms, signature value, the signed content and the certificates.</summary>
        public static string Signatures(byte[] encoded)
        {
            var cms = new Org.BouncyCastle.Cms.CmsSignedData(encoded);
            var report = new StringBuilder();
            foreach (var signer in cms.GetSignerInfos().GetSigners())
            {
                report.AppendLine("Signer: " + signer.SignerID.Issuer + ", serial number " + signer.SignerID.SerialNumber.ToString(16));
                report.AppendLine("Digest algorithm: " + signer.DigestAlgOid + ", signature algorithm: " + signer.EncryptionAlgOid);
                report.AppendLine("Signature (" + signer.GetSignature().Length + " bytes): " + ToHex(signer.GetSignature()));
            }
            if (cms.SignedContent != null)
            {
                using (var buffer = new MemoryStream())
                {
                    cms.SignedContent.Write(buffer);
                    report.AppendLine("Signed content: " + Encoding.UTF8.GetString(buffer.ToArray()));
                }
            }
            foreach (X509Certificate certificate in cms.GetCertificates().EnumerateMatches(null)) { report.AppendLine("Certificate: " + certificate.SubjectDN); }
            return report.ToString().TrimEnd();
        }
    }

}
