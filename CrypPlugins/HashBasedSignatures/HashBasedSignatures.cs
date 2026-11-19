using CrypTool.PluginBase;
using CrypTool.PluginBase.IO;
using CrypTool.PluginBase.Miscellaneous;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pqc.Crypto.Lms;
using Org.BouncyCastle.Pqc.Crypto.SphincsPlus;
using Org.BouncyCastle.Security;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace HashBasedSignatures
{
    /// <summary>
    /// Signatures whose security rests on hash functions only, and which therefore resist quantum computers: LMS (RFC 8554), a Merkle tree
    /// over Winternitz one-time signatures, and SPHINCS+. An LMS key can sign only a limited number of messages (every one-time key
    /// once); signing therefore returns the updated private key, and the number of signatures left. SPHINCS+ keeps no state.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("HashBasedSignatures.Properties.Resources", "PluginCaption", "PluginTooltip", "HashBasedSignatures/DetailedDescription/doc.xml", "HashBasedSignatures/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernAsymmetric)]
    public class HashBasedSignatures : ICrypComponent
    {
        private HashBasedSignaturesSettings _settings = new HashBasedSignaturesSettings();

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

        [PropertyInfo(Direction.OutputData, "RemainingCaption", "RemainingTooltip", false)]
        public int Remaining { get; set; }

        [PropertyInfo(Direction.OutputData, "ValidCaption", "ValidTooltip", false)]
        public bool Valid { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (HashBasedSignaturesSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        private bool IsLms => _settings.Scheme == 0;

        private LmsParameters LmsParams()
        {
            // LMS_SHA256_M32_H5 / H10 and LMOTS_SHA256_N32_W1 / W2 / W4 / W8 (RFC 8554 type codes 5, 6 and 1 to 4)
            return new LmsParameters(LMSigParameters.GetParametersByID(5 + _settings.Height), LMOtsParameters.GetParametersByID(1 + _settings.Winternitz));
        }

        private SphincsPlusParameters SphincsParams()
        {
            switch (_settings.Variant)
            {
                case 1: return SphincsPlusParameters.sha2_128s_simple;
                case 2: return SphincsPlusParameters.sha2_192f_simple;
                case 3: return SphincsPlusParameters.sha2_256f_simple;
                default: return SphincsPlusParameters.sha2_128f_simple;
            }
        }

        public void Execute()
        {
            try
            {
                switch (_settings.Action)
                {
                    case 1: Sign(); break;
                    case 2: Verify(); break;
                    default: GenerateKeyPair(); break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is InvalidCipherTextException || ex is InvalidCastException || ex is System.IO.IOException || ex is IndexOutOfRangeException)
            {
                // BouncyCastle reports an unusable key (wrong scheme or parameters, no one-time keys left, damaged encoding) with these exceptions
                GuiLogMessage("The operation failed: " + ex.Message + " (is the key meant for the chosen scheme and parameters? has an LMS key been used up?)", NotificationLevel.Error);
            }
        }

        private void GenerateKeyPair()
        {
            var random = new SecureRandom();
            if (IsLms)
            {
                var generator = new LmsKeyPairGenerator();
                generator.Init(new LmsKeyGenerationParameters(LmsParams(), random));
                AsymmetricCipherKeyPair pair = generator.GenerateKeyPair();
                var privateKey = (LmsPrivateKeyParameters)pair.Private;
                OutputPrivateKey = privateKey.GetEncoded();
                OutputPublicKey = ((LmsPublicKeyParameters)pair.Public).GetEncoded();
                Remaining = (int)privateKey.GetUsagesRemaining();
            }
            else
            {
                var generator = new SphincsPlusKeyPairGenerator();
                generator.Init(new SphincsPlusKeyGenerationParameters(random, SphincsParams()));
                AsymmetricCipherKeyPair pair = generator.GenerateKeyPair();
                OutputPrivateKey = ((SphincsPlusPrivateKeyParameters)pair.Private).GetEncoded();
                OutputPublicKey = ((SphincsPlusPublicKeyParameters)pair.Public).GetEncoded();
                Remaining = -1;
            }
            OnPropertyChanged("OutputPrivateKey");
            OnPropertyChanged("OutputPublicKey");
            OnPropertyChanged("Remaining");
        }

        private void Sign()
        {
            if (InputMessage == null || InputPrivateKey == null)
            {
                GuiLogMessage("To sign, both the message and the private key are needed.", NotificationLevel.Error);
                return;
            }

            byte[] message = InputMessage.CreateReader().ReadFully();
            if (IsLms)
            {
                var key = LmsPrivateKeyParameters.GetInstance(InputPrivateKey);
                if (key.GetUsagesRemaining() <= 0)
                {
                    GuiLogMessage("This LMS key has signed as many messages as it can; every one-time key may be used only once.", NotificationLevel.Error);
                    return;
                }
                var signer = new LmsSigner();
                signer.Init(true, key);
                OutputSignature = signer.GenerateSignature(message);
                OutputPrivateKey = key.GetEncoded();   // the next one-time key is the next one
                Remaining = (int)key.GetUsagesRemaining();
            }
            else
            {
                var key = new SphincsPlusPrivateKeyParameters(SphincsParams(), InputPrivateKey);
                var signer = new SphincsPlusSigner();
                signer.Init(true, key);
                OutputSignature = signer.GenerateSignature(message);
                Remaining = -1;
            }
            OnPropertyChanged("OutputSignature");
            OnPropertyChanged("OutputPrivateKey");
            OnPropertyChanged("Remaining");
        }

        private void Verify()
        {
            if (InputMessage == null || InputPublicKey == null || InputSignature == null)
            {
                GuiLogMessage("To verify, the message, the public key and the signature are needed.", NotificationLevel.Error);
                return;
            }

            byte[] message = InputMessage.CreateReader().ReadFully();
            bool valid;
            try
            {
                if (IsLms)
                {
                    var signer = new LmsSigner();
                    signer.Init(false, LmsPublicKeyParameters.GetInstance(InputPublicKey));
                    valid = signer.VerifySignature(message, InputSignature);
                }
                else
                {
                    var signer = new SphincsPlusSigner();
                    signer.Init(false, new SphincsPlusPublicKeyParameters(SphincsParams(), InputPublicKey));
                    valid = signer.VerifySignature(message, InputSignature);
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is IndexOutOfRangeException || ex is System.IO.IOException)
            {
                valid = false;   // a signature that cannot even be read is not valid
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
            InputPrivateKey = InputPublicKey = InputSignature = null;
            OutputPrivateKey = OutputPublicKey = OutputSignature = null;
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
