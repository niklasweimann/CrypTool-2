using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pqc.Crypto.Cmce;
using Org.BouncyCastle.Security;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace ClassicMcEliece
{
    /// <summary>
    /// Classic McEliece, a code-based key encapsulation mechanism: the public key hides a binary Goppa code, the error-correcting code
    /// of the private key. The sender encapsulates a random shared secret as a codeword with errors (Niederreiter's form of McEliece's
    /// idea: the ciphertext is a syndrome); only the owner of the Goppa code can correct the errors and recover the secret.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("ClassicMcEliece.Properties.Resources", "PluginCaption", "PluginTooltip", "ClassicMcEliece/DetailedDescription/doc.xml", "ClassicMcEliece/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernAsymmetric)]
    public class ClassicMcEliece : ICrypComponent
    {
        private ClassicMcElieceSettings _settings = new ClassicMcElieceSettings();

        [PropertyInfo(Direction.InputData, "InputPrivateKeyCaption", "InputPrivateKeyTooltip", false)]
        public byte[] InputPrivateKey { get; set; }

        [PropertyInfo(Direction.InputData, "InputPublicKeyCaption", "InputPublicKeyTooltip", false)]
        public byte[] InputPublicKey { get; set; }

        [PropertyInfo(Direction.InputData, "InputEncapsulationCaption", "InputEncapsulationTooltip", false)]
        public byte[] InputEncapsulation { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputPrivateKeyCaption", "OutputPrivateKeyTooltip", false)]
        public byte[] OutputPrivateKey { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputPublicKeyCaption", "OutputPublicKeyTooltip", false)]
        public byte[] OutputPublicKey { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputEncapsulationCaption", "OutputEncapsulationTooltip", false)]
        public byte[] OutputEncapsulation { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputSecretCaption", "OutputSecretTooltip", false)]
        public byte[] OutputSecret { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (ClassicMcElieceSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        private CmceParameters Parameters => _settings.Parameters == 1 ? CmceParameters.mceliece460896r3 : CmceParameters.mceliece348864r3;

        public void Execute()
        {
            try
            {
                var random = new SecureRandom();
                switch (_settings.Action)
                {
                    case 1:
                        if (InputPublicKey == null) { GuiLogMessage("To encapsulate, the public key is needed.", NotificationLevel.Error); return; }
                        ISecretWithEncapsulation result = new CmceKemGenerator(random).GenerateEncapsulated(new CmcePublicKeyParameters(Parameters, InputPublicKey));
                        OutputSecret = result.GetSecret();
                        OutputEncapsulation = result.GetEncapsulation();
                        OnPropertyChanged("OutputSecret");
                        OnPropertyChanged("OutputEncapsulation");
                        break;
                    case 2:
                        if (InputPrivateKey == null || InputEncapsulation == null) { GuiLogMessage("To decapsulate, the private key and the encapsulation are needed.", NotificationLevel.Error); return; }
                        OutputSecret = new CmceKemExtractor(new CmcePrivateKeyParameters(Parameters, InputPrivateKey)).ExtractSecret(InputEncapsulation);
                        OnPropertyChanged("OutputSecret");
                        break;
                    default:
                        var generator = new CmceKeyPairGenerator();
                        generator.Init(new CmceKeyGenerationParameters(random, Parameters));
                        AsymmetricCipherKeyPair pair = generator.GenerateKeyPair();
                        OutputPrivateKey = ((CmcePrivateKeyParameters)pair.Private).GetPrivateKey();
                        OutputPublicKey = ((CmcePublicKeyParameters)pair.Public).GetPublicKey();
                        OnPropertyChanged("OutputPrivateKey");
                        OnPropertyChanged("OutputPublicKey");
                        break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is InvalidCipherTextException || ex is IndexOutOfRangeException)
            {
                GuiLogMessage("The operation failed: " + ex.Message + " (do the key and the parameter set belong together?)", NotificationLevel.Error);
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
            Dispose();
        }

        public void PostExecution()
        {
            Dispose();
        }

        public void Dispose()
        {
            InputPrivateKey = InputPublicKey = InputEncapsulation = null;
            OutputPrivateKey = OutputPublicKey = OutputEncapsulation = OutputSecret = null;
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
