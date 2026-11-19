using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Numerics;

namespace NumberTools
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("NumberTools.Properties.Resources", "PluginCaption", "PluginTooltip", "NumberTools/DetailedDescription/doc.xml", "NumberTools/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsMisc)]
    public class NumberTools : ICrypComponent
    {
        private NumberToolsSettings _settings = new NumberToolsSettings();

        [PropertyInfo(Direction.InputData, "InputACaption", "InputATooltip", true)]
        public string InputA { get; set; }

        [PropertyInfo(Direction.InputData, "InputBCaption", "InputBTooltip", false)]
        public string InputB { get; set; }

        [PropertyInfo(Direction.OutputData, "ResultCaption", "ResultTooltip", false)]
        public string Result
        {
            get => _result;
            set { _result = value; OnPropertyChanged("Result"); }
        }
        private string _result;

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
            set => _settings = (NumberToolsSettings)value;
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
                BigInteger a = NumberFacts.Parse(InputA);
                if (_settings.Mode == 0)
                {
                    int numberBase = _settings.Base;
                    Result = NumberFacts.Digits(a, numberBase).ToString();
                    Report = string.Format(Properties.Resources.LengthReport, Result, numberBase, NumberFacts.Bits(a), NumberFacts.Bytes(a), NumberFacts.Digits(a, 10));
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(InputB)) { throw new ArgumentException("The second number is needed."); }
                    var steps = new List<NumberFacts.Step>();
                    BigInteger gcd = NumberFacts.ReciprocalSubtraction(a, NumberFacts.Parse(InputB), steps);
                    Result = gcd.ToString();
                    Report = string.Join(Environment.NewLine, steps.Select(s => s.Larger + " = " + s.Quotient + " * " + s.Smaller + " + " + s.Remainder)) + Environment.NewLine + "gcd = " + gcd;
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
            InputA = InputB = Result = Report = null;
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


    public static class NumberFacts
    {
        public static BigInteger Parse(string text)
        {
            if (!BigInteger.TryParse(text.Trim(), out BigInteger value) || value < 0) { throw new FormatException("Not a natural number: " + text); }
            return value;
        }

        /// <summary>The number of digits of the number in the base (the number 0 has one digit).</summary>
        public static int Digits(BigInteger value, int numberBase)
        {
            int digits = 1;
            for (; value >= numberBase; value /= numberBase) { digits++; }
            return digits;
        }

        public static int Bits(BigInteger value) => value.IsZero ? 1 : (int)value.GetBitLength();

        public static int Bytes(BigInteger value) => (Bits(value) + 7) / 8;

        public sealed class Step { public BigInteger Larger, Smaller, Quotient, Remainder; }

        /// <summary>
        /// Reciprocal subtraction (Euclid's anthyphairesis): the smaller number is subtracted from the larger one again and again, and then the roles are swapped; the
        /// last difference that is not zero is the greatest common divisor. A step shows how many times the smaller number was subtracted.
        /// </summary>
        public static BigInteger ReciprocalSubtraction(BigInteger a, BigInteger b, List<Step> steps)
        {
            if (a.IsZero || b.IsZero) { return a + b; }
            while (!b.IsZero)
            {
                if (a < b) { var t = a; a = b; b = t; }
                var step = new Step { Larger = a, Smaller = b, Quotient = a / b, Remainder = a % b };
                steps.Add(step);
                a = b;
                b = step.Remainder;
            }
            return a;
        }
    }

}
