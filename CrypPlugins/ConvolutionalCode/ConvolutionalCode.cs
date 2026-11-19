using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace ConvolutionalCode
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("ConvolutionalCode.Properties.Resources", "PluginCaption", "PluginTooltip", "ConvolutionalCode/DetailedDescription/doc.xml", "ConvolutionalCode/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsCodes)]
    public class ConvolutionalCode : ICrypComponent
    {
        private ConvolutionalCodeSettings _settings = new ConvolutionalCodeSettings();

        [PropertyInfo(Direction.InputData, "InputBitsCaption", "InputBitsTooltip", true)]
        public string InputBits { get; set; }

        [PropertyInfo(Direction.InputData, "ErrorPositionsCaption", "ErrorPositionsTooltip", false)]
        public string ErrorPositions { get; set; }

        [PropertyInfo(Direction.OutputData, "EncodedCaption", "EncodedTooltip", false)]
        public string Encoded
        {
            get => _encoded;
            set { _encoded = value; OnPropertyChanged("Encoded"); }
        }
        private string _encoded;

        [PropertyInfo(Direction.OutputData, "ReceivedCaption", "ReceivedTooltip", false)]
        public string Received
        {
            get => _received;
            set { _received = value; OnPropertyChanged("Received"); }
        }
        private string _received;

        [PropertyInfo(Direction.OutputData, "DecodedCaption", "DecodedTooltip", false)]
        public string Decoded
        {
            get => _decoded;
            set { _decoded = value; OnPropertyChanged("Decoded"); }
        }
        private string _decoded;

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
            set => _settings = (ConvolutionalCodeSettings)value;
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
                string bits = new string(InputBits.Where(c => c == '0' || c == '1').ToArray());
                if (bits.Length == 0) { throw new ArgumentException("The input must contain bits (0 and 1)."); }
                var positions = (ErrorPositions ?? "").Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                Encoded = ConvolutionalCoding.Encode(bits);
                Received = ConvolutionalCoding.Flip(Encoded, positions);
                Decoded = ConvolutionalCoding.Decode(Received);
                int errors = Enumerable.Range(0, Encoded.Length).Count(i => Encoded[i] != Received[i]);
                Report = string.Format(Properties.Resources.CodeReport, bits, Encoded, errors, Received, Decoded, Decoded == bits ? Properties.Resources.Corrected : Properties.Resources.NotCorrected);
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
            InputBits = ErrorPositions = Encoded = Received = Decoded = Report = null;
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
    /// A convolutional code with constraint length 3 and the generators 7 and 5 (octal), rate 1/2, and its Viterbi decoder. The encoder has two bits of memory;
    /// the two zero bits that are appended bring it back to the start state. The free distance is 5, so any two bit errors are corrected.
    /// </summary>
    public static class ConvolutionalCoding
    {
        /// <summary>Encodes the bits (a string of 0 and 1) followed by two zero bits; every input bit gives two output bits.</summary>
        public static string Encode(string bits)
        {
            int u1 = 0, u2 = 0;
            var result = new StringBuilder();
            foreach (char c in bits + "00")
            {
                int u = c - '0';
                result.Append(u ^ u1 ^ u2).Append(u ^ u2);   // the generators 111 (7) and 101 (5)
                u2 = u1;
                u1 = u;
            }
            return result.ToString();
        }

        /// <summary>The Viterbi algorithm: the path through the trellis of the four states whose output differs from the received bits in the fewest places (Hamming distance).</summary>
        public static string Decode(string received)
        {
            int steps = received.Length / 2;
            const int States = 4, Infinity = int.MaxValue / 2;
            var cost = new int[steps + 1, States];
            var previous = new int[steps + 1, States];
            var input = new int[steps + 1, States];
            for (int t = 0; t <= steps; t++) { for (int s = 0; s < States; s++) { cost[t, s] = Infinity; } }
            cost[0, 0] = 0;
            for (int t = 0; t < steps; t++)
            {
                int r1 = received[2 * t] - '0', r2 = received[2 * t + 1] - '0';
                for (int s = 0; s < States; s++)
                {
                    if (cost[t, s] >= Infinity) { continue; }
                    int u1 = s >> 1 & 1, u2 = s & 1;
                    for (int u = 0; u <= 1; u++)
                    {
                        int next = u << 1 | u1;
                        int distance = ((u ^ u1 ^ u2) != r1 ? 1 : 0) + ((u ^ u2) != r2 ? 1 : 0);
                        if (cost[t, s] + distance < cost[t + 1, next]) { cost[t + 1, next] = cost[t, s] + distance; previous[t + 1, next] = s; input[t + 1, next] = u; }
                    }
                }
            }
            var decoded = new char[steps];
            int state = 0;   // the two zero bits at the end lead back to state 0
            for (int t = steps; t >= 1; t--) { decoded[t - 1] = (char)('0' + input[t, state]); state = previous[t, state]; }
            return new string(decoded, 0, Math.Max(0, steps - 2));
        }

        public static string Flip(string bits, IEnumerable<int> positions)
        {
            var chars = bits.ToCharArray();
            foreach (int p in positions) { if (p >= 1 && p <= chars.Length) { chars[p - 1] = chars[p - 1] == '0' ? '1' : '0'; } }
            return new string(chars);
        }
    }

}
