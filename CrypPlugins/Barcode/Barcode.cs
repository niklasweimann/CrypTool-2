using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace Barcode
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("Barcode.Properties.Resources", "PluginCaption", "PluginTooltip", "Barcode/DetailedDescription/doc.xml", "Barcode/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsCodes)]
    public class Barcode : ICrypComponent
    {
        private BarcodeSettings _settings = new BarcodeSettings();

        [PropertyInfo(Direction.InputData, "InputCaption", "InputTooltip", true)]
        public string Input { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputCaption", "OutputTooltip", false)]
        public string Output
        {
            get => _output;
            set { _output = value; OnPropertyChanged("Output"); }
        }
        private string _output;

        [PropertyInfo(Direction.OutputData, "ModulesCaption", "ModulesTooltip", false)]
        public string Modules
        {
            get => _modules;
            set { _modules = value; OnPropertyChanged("Modules"); }
        }
        private string _modules;

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
            set => _settings = (BarcodeSettings)value;
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
                string completed;
                string modules = Ean.Modules(Input.Trim(), _settings.Format == 0, out completed);
                Modules = modules;
                Output = Ean.Draw(modules, completed);
                Report = string.Format(Properties.Resources.BarcodeReport, completed, modules.Length, completed[completed.Length - 1]);
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
            Input = Output = Modules = Report = null;
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


    public static class Ean
    {
        private static readonly string[] L = { "0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011" };
        private static readonly string[] G = { "0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111" };
        private static readonly string[] Parity = { "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL" };

        public static string R(int digit) => new string(L[digit].Select(c => c == '0' ? '1' : '0').ToArray());

        /// <summary>The check digit: the digits are weighted 3 and 1 alternately from the right (starting with 3 at the digit next to the check digit); the sum plus the check digit is a multiple of 10.</summary>
        public static int CheckDigit(string digitsWithoutCheck)
        {
            int sum = 0;
            for (int i = 0; i < digitsWithoutCheck.Length; i++) { sum += (digitsWithoutCheck[digitsWithoutCheck.Length - 1 - i] - '0') * (i % 2 == 0 ? 3 : 1); }
            return (10 - sum % 10) % 10;
        }

        /// <summary>The 95 (EAN-13) or 67 (EAN-8) modules of the barcode as 0 and 1: guard, left digits, middle guard, right digits, guard. Without the check digit it is added.</summary>
        public static string Modules(string digits, bool ean13, out string completed)
        {
            int length = ean13 ? 13 : 8;
            if (digits.Any(c => c < '0' || c > '9')) { throw new ArgumentException("A barcode contains only digits."); }
            if (digits.Length == length - 1) { digits += CheckDigit(digits); }
            if (digits.Length != length) { throw new ArgumentException("An " + (ean13 ? "EAN-13" : "EAN-8") + " has " + length + " digits (or " + (length - 1) + " without the check digit)."); }
            if (CheckDigit(digits.Substring(0, length - 1)) != digits[length - 1] - '0') { throw new ArgumentException("The check digit is wrong: it should be " + CheckDigit(digits.Substring(0, length - 1)) + "."); }
            completed = digits;
            var bars = new StringBuilder("101");
            if (ean13)
            {
                string parity = Parity[digits[0] - '0'];
                for (int i = 0; i < 6; i++) { int d = digits[1 + i] - '0'; bars.Append(parity[i] == 'L' ? L[d] : G[d]); }
                bars.Append("01010");
                for (int i = 0; i < 6; i++) { bars.Append(R(digits[7 + i] - '0')); }
            }
            else
            {
                for (int i = 0; i < 4; i++) { bars.Append(L[digits[i] - '0']); }
                bars.Append("01010");
                for (int i = 0; i < 4; i++) { bars.Append(R(digits[4 + i] - '0')); }
            }
            bars.Append("101");
            return bars.ToString();
        }

        /// <summary>Reads the digits back from the modules (for EAN-13 the first digit follows from the pattern of L and G codes).</summary>
        public static string Decode(string modules, bool ean13)
        {
            int half = ean13 ? 6 : 4;
            if (modules.Length != (ean13 ? 95 : 67) || !modules.StartsWith("101") || !modules.EndsWith("101")) { throw new ArgumentException("This is not an EAN barcode."); }
            var result = new StringBuilder();
            var pattern = new StringBuilder();
            for (int i = 0; i < half; i++)
            {
                string code = modules.Substring(3 + 7 * i, 7);
                int l = Array.IndexOf(L, code), g = Array.IndexOf(G, code);
                if (l < 0 && g < 0) { throw new ArgumentException("The pattern of digit " + (i + 1) + " is not a code."); }
                pattern.Append(l >= 0 ? 'L' : 'G');
                result.Append(l >= 0 ? l : g);
            }
            int rightStart = 3 + 7 * half + 5;
            var right = new StringBuilder();
            for (int i = 0; i < half; i++)
            {
                int d = Enumerable.Range(0, 10).FirstOrDefault(x => R(x) == modules.Substring(rightStart + 7 * i, 7));
                if (R(d) != modules.Substring(rightStart + 7 * i, 7)) { throw new ArgumentException("The pattern of digit " + (half + i + 1) + " is not a code."); }
                right.Append(d);
            }
            string first = ean13 ? Array.IndexOf(Parity, pattern.ToString()).ToString() : "";
            if (ean13 && first == "-1") { throw new ArgumentException("The pattern of the first six digits is not valid."); }
            return first + result + right;
        }

        /// <summary>The barcode as text: two lines of bars (full blocks for 1) and the digits below.</summary>
        public static string Draw(string modules, string digits)
        {
            string bar = string.Concat(modules.Select(c => c == '1' ? "█" : " "));
            return bar + Environment.NewLine + bar + Environment.NewLine + bar + Environment.NewLine + digits;
        }
    }

}
