using CrypTool.PluginBase;
using CrypTool.PluginBase.IO;
using CrypTool.PluginBase.Miscellaneous;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace DigitalSignatures
{
    /// <summary>
    /// Generates key pairs and creates and verifies digital signatures: RSA (PKCS#1 v1.5 and PSS), DSA, ECDSA and ECNR.
    /// Keys are exchanged as DER encodings (PKCS#8 for private keys, X.509 SubjectPublicKeyInfo for public keys).
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("DigitalSignatures.Properties.Resources", "PluginCaption", "PluginTooltip", "DigitalSignatures/DetailedDescription/doc.xml", "DigitalSignatures/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernAsymmetric)]
    public class DigitalSignatures : ICrypComponent
    {
        private DigitalSignaturesSettings _settings = new DigitalSignaturesSettings();

        [PropertyInfo(Direction.InputData, "InputMessageCaption", "InputMessageTooltip", false)]
        public ICrypToolStream InputMessage { get; set; }

        [PropertyInfo(Direction.InputData, "InputPrivateKeyCaption", "InputPrivateKeyTooltip", false)]
        public byte[] InputPrivateKey { get; set; }

        [PropertyInfo(Direction.InputData, "InputPublicKeyCaption", "InputPublicKeyTooltip", false)]
        public byte[] InputPublicKey { get; set; }

        [PropertyInfo(Direction.InputData, "InputSignatureCaption", "InputSignatureTooltip", false)]
        public byte[] InputSignature { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputPrivateKeyCaption", "OutputPrivateKeyTooltip", false)]
        public byte[] OutputPrivateKey { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputPublicKeyCaption", "OutputPublicKeyTooltip", false)]
        public byte[] OutputPublicKey { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputSignatureCaption", "OutputSignatureTooltip", false)]
        public byte[] OutputSignature { get; set; }

        [PropertyInfo(Direction.OutputData, "ValidCaption", "ValidTooltip", false)]
        public bool Valid { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (DigitalSignaturesSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            switch (_settings.Action)
            {
                case 1:
                    Sign();
                    break;
                case 2:
                    Verify();
                    break;
                default:
                    GenerateKeyPair();
                    break;
            }
        }

        private static readonly string[] CurveNames = { "secp256r1", "secp384r1", "secp521r1", "secp256k1" };

        private IDigest CreateDigest()
        {
            switch (_settings.Hash)
            {
                case 0: return new Sha1Digest();
                case 2: return new Sha384Digest();
                case 3: return new Sha512Digest();
                default: return new Sha256Digest();
            }
        }

        private bool IsEllipticCurve => _settings.Algorithm == 3 || _settings.Algorithm == 4;

        private void GenerateKeyPair()
        {
            var random = new SecureRandom();
            AsymmetricCipherKeyPair pair;
            if (IsEllipticCurve)
            {
                // with the OID of the named curve, so that the encoded keys name the curve instead of listing its parameters
                var generator = new ECKeyPairGenerator();
                generator.Init(new ECKeyGenerationParameters(ECNamedCurveTable.GetOid(CurveNames[_settings.Curve]), random));
                pair = generator.GenerateKeyPair();
            }
            else if (_settings.Algorithm == 2)
            {
                // FIPS 186-3 parameter sizes: (L, N) = (1024, 160), (2048, 256), (3072, 256)
                int l = 1024 + 1024 * _settings.KeySize;
                int n = _settings.KeySize == 0 ? 160 : 256;
                var parameterGenerator = new DsaParametersGenerator();
                parameterGenerator.Init(new DsaParameterGenerationParameters(l, n, 80, random));
                var generator = new DsaKeyPairGenerator();
                generator.Init(new DsaKeyGenerationParameters(random, parameterGenerator.GenerateParameters()));
                pair = generator.GenerateKeyPair();
            }
            else
            {
                var generator = new RsaKeyPairGenerator();
                generator.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(0x10001), random, 1024 + 1024 * _settings.KeySize, 25));
                pair = generator.GenerateKeyPair();
            }

            OutputPrivateKey = PrivateKeyInfoFactory.CreatePrivateKeyInfo(pair.Private).GetDerEncoded();
            OutputPublicKey = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pair.Public).GetDerEncoded();
            OnPropertyChanged("OutputPrivateKey");
            OnPropertyChanged("OutputPublicKey");
        }

        private ISigner CreateSigner()
        {
            switch (_settings.Algorithm)
            {
                case 1:
                    // RSA-PSS with MGF1 of the same hash and a salt as long as the hash value
                    IDigest digest = CreateDigest();
                    return new PssSigner(new RsaBlindedEngine(), digest, digest.GetDigestSize());
                case 2:
                    return new DsaDigestSigner(new DsaSigner(), CreateDigest());
                case 3:
                    return new DsaDigestSigner(new ECDsaSigner(), CreateDigest());
                case 4:
                    return new DsaDigestSigner(new ECNRSigner(), CreateDigest());
                default:
                    return new RsaDigestSigner(CreateDigest());
            }
        }

        private byte[] ReadMessage()
        {
            return InputMessage.CreateReader().ReadFully();
        }

        private void Sign()
        {
            if (InputMessage == null || InputPrivateKey == null)
            {
                GuiLogMessage("To sign, both the message and the private key are needed.", NotificationLevel.Error);
                return;
            }

            AsymmetricKeyParameter key = PrivateKeyFactory.CreateKey(InputPrivateKey);
            ISigner signer = CreateSigner();
            signer.Init(true, key);
            byte[] message = ReadMessage();
            signer.BlockUpdate(message, 0, message.Length);
            OutputSignature = signer.GenerateSignature();
            OnPropertyChanged("OutputSignature");
        }

        private void Verify()
        {
            if (InputMessage == null || InputPublicKey == null || InputSignature == null)
            {
                GuiLogMessage("To verify, the message, the public key and the signature are needed.", NotificationLevel.Error);
                return;
            }

            AsymmetricKeyParameter key = PublicKeyFactory.CreateKey(InputPublicKey);
            ISigner signer = CreateSigner();
            signer.Init(false, key);
            byte[] message = ReadMessage();
            signer.BlockUpdate(message, 0, message.Length);
            bool valid;
            try
            {
                valid = signer.VerifySignature(InputSignature);
            }
            catch (Exception)
            {
                // a signature that cannot even be parsed is not valid
                valid = false;
            }
            Valid = valid;
            OnPropertyChanged("Valid");
            GuiLogMessage(valid ? "The signature is valid." : "The signature is NOT valid.", valid ? NotificationLevel.Info : NotificationLevel.Warning);
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

        public void Dispose()
        {
            InputMessage = null;
            InputPrivateKey = null;
            InputPublicKey = null;
            InputSignature = null;
            OutputPrivateKey = null;
            OutputPublicKey = null;
            OutputSignature = null;
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
}
