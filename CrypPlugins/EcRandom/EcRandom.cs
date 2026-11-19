using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using BcInt = Org.BouncyCastle.Math.BigInteger;

namespace EcRandom
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("EcRandom.Properties.Resources", "PluginCaption", "PluginTooltip", "EcRandom/DetailedDescription/doc.xml", "EcRandom/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsRandomNumbers)]
    public class EcRandom : ICrypComponent
    {
        private EcRandomSettings _settings = new EcRandomSettings();

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
            set => _settings = (EcRandomSettings)value;
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
                var generator = new EcGenerator(_settings.Curve, _settings.Seed, _settings.Seed);
                int blocks = _settings.Blocks;
                var output = Enumerable.Range(0, blocks).Select(i => generator.Next()).ToList();
                Output = string.Join(Environment.NewLine, output.Select(b => b.ToString(16).PadLeft(generator.BlockBits / 4, '0')));
                var predicted = generator.Predict(output, blocks);
                int hits = predicted.Skip(2).Zip(output.Skip(2), (a, b) => a.Equals(b)).Count(x => x);
                Report = string.Format(Properties.Resources.PredictReport, generator.BlockBits, blocks - 2, hits);
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
            Output = Report = null;
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
    /// A random number generator on an elliptic curve in the style of Dual_EC_DRBG: the state is s, the next state is x(s * P), and the output block is x(state * Q)
    /// without its leading 16 bits. Whoever knows the number e with P = e * Q can compute the next state from one output block and so predict all further output.
    /// </summary>
    public sealed class EcGenerator
    {
        public static readonly string[] Curves = { "secp256r1", "secp384r1", "secp521r1" };

        public Org.BouncyCastle.Asn1.X9.X9ECParameters Curve { get; }
        public Org.BouncyCastle.Math.EC.ECPoint P { get; }
        public Org.BouncyCastle.Math.EC.ECPoint Q { get; }
        public BcInt Backdoor { get; }
        public int BlockBits { get; }
        private BcInt _state;

        /// <summary>Q is the base point, P = e * Q with a secret e that follows from the seed: the number e is the backdoor.</summary>
        public EcGenerator(int curve, int seed, int stateSeed)
        {
            Curve = Org.BouncyCastle.Asn1.X9.ECNamedCurveTable.GetByName(Curves[curve]);
            Q = Curve.G;
            var random = new Random(seed);
            Backdoor = Derive(random, Curve.N);
            P = Q.Multiply(Backdoor).Normalize();
            _state = Derive(new Random(stateSeed * 7919 + 1), Curve.N);
            BlockBits = Curve.Curve.FieldSize - 16;
        }

        private static BcInt Derive(Random random, BcInt order)
        {
            var bytes = new byte[(order.BitLength + 7) / 8 + 8];
            random.NextBytes(bytes);
            return new BcInt(1, bytes).Mod(order.Subtract(BcInt.One)).Add(BcInt.One);
        }

        public static BcInt X(Org.BouncyCastle.Math.EC.ECPoint point) => point.Normalize().AffineXCoord.ToBigInteger();

        /// <summary>One step: the next state is x(s P), the output is x(s' Q) without the leading 16 bits.</summary>
        public BcInt Next()
        {
            _state = X(P.Multiply(_state));
            BcInt r = X(Q.Multiply(_state));
            return r.And(BcInt.One.ShiftLeft(BlockBits).Subtract(BcInt.One));
        }

        /// <summary>
        /// The attack: the 16 missing bits of the first block are tried; about half of the 65536 values are the x coordinate of a point R on the curve, and for each of them
        /// x(e R) is a candidate for the next state. The second observed block tells which candidate is right (the others give another block). Then all further blocks follow.
        /// </summary>
        public List<BcInt> Predict(IList<BcInt> observed, int total)
        {
            int fieldBytes = (Curve.Curve.FieldSize + 7) / 8;
            BcInt mask = BcInt.One.ShiftLeft(BlockBits).Subtract(BcInt.One);
            for (int top = 0; top < 1 << 16; top++)
            {
                BcInt x = observed[0].Or(BcInt.ValueOf(top).ShiftLeft(BlockBits));
                byte[] xBytes = x.ToByteArrayUnsigned();
                if (xBytes.Length > fieldBytes) { continue; }
                byte[] encoded = new byte[fieldBytes + 1];
                encoded[0] = 2;
                Array.Copy(xBytes, 0, encoded, 1 + fieldBytes - xBytes.Length, xBytes.Length);
                Org.BouncyCastle.Math.EC.ECPoint point;
                try { point = Curve.Curve.DecodePoint(encoded); } catch (Exception) { continue; }
                BcInt state = X(point.Multiply(Backdoor));
                if (!X(Q.Multiply(state)).And(mask).Equals(observed[1])) { continue; }
                var blocks = new List<BcInt> { observed[0] };
                for (int i = 1; i < total; i++)
                {
                    blocks.Add(X(Q.Multiply(state)).And(mask));
                    state = X(P.Multiply(state));
                }
                return blocks;
            }
            return new List<BcInt>();
        }
    }

}
