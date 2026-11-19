using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace AsciiCodes
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("AsciiCodes.Properties.Resources", "PluginCaption", "PluginTooltip", "AsciiCodes/DetailedDescription/doc.xml", "AsciiCodes/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsCodes)]
    public class AsciiCodes : ICrypComponent
    {
        private AsciiCodesSettings _settings = new AsciiCodesSettings();

        [PropertyInfo(Direction.InputData, "InputCaption", "InputTooltip", false)]
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
            set => _settings = (AsciiCodesSettings)value;
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
                switch (_settings.Mode)
                {
                    case 0: Output = AsciiConversion.Encode(Input, 10); break;
                    case 1: Output = AsciiConversion.Encode(Input, 16); break;
                    case 2: Output = AsciiConversion.Encode(Input, 2); break;
                    case 3: Output = AsciiConversion.Decode(Input, 10); break;
                    case 4: Output = AsciiConversion.Decode(Input, 16); break;
                    case 5: Output = AsciiConversion.Decode(Input, 2); break;
                    default: Output = AsciiConversion.Table(); break;
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


    public static class AsciiConversion
    {
        public static readonly string[] Names = { "NUL", "SOH", "STX", "ETX", "EOT", "ENQ", "ACK", "BEL", "BS", "TAB", "LF", "VT", "FF", "CR", "SO", "SI", "DLE", "DC1", "DC2", "DC3", "DC4", "NAK", "SYN", "ETB", "CAN", "EM", "SUB", "ESC", "FS", "GS", "RS", "US" };

        public static string Binary(int value) => Convert.ToString(value, 2).PadLeft(8, '0');

        /// <summary>The ASCII codes of the text in the base (10, 16 or 2), separated by spaces; characters beyond 127 are refused.</summary>
        public static string Encode(string text, int numberBase)
        {
            return string.Join(" ", text.Select(c =>
            {
                if (c > 127) { throw new ArgumentException("The character '" + c + "' is not in ASCII (codes 0 to 127)."); }
                return numberBase == 10 ? ((int)c).ToString() : numberBase == 16 ? ((int)c).ToString("X2") : Binary(c);
            }));
        }

        public static string Decode(string codes, int numberBase)
        {
            var result = new StringBuilder();
            foreach (string token in codes.Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int value = Convert.ToInt32(token, numberBase);
                if (value < 0 || value > 127) { throw new ArgumentException("The code " + token + " is not in ASCII (0 to 127)."); }
                result.Append((char)value);
            }
            return result.ToString();
        }

        public static string Table()
        {
            var lines = new List<string> { "Dec  Hex  Binary    Character" };
            for (int i = 0; i < 128; i++) { lines.Add(i.ToString().PadLeft(3) + "  " + i.ToString("X2") + "   " + Binary(i) + "  " + (i < 32 ? Names[i] : i == 32 ? "space" : i == 127 ? "DEL" : ((char)i).ToString())); }
            return string.Join(Environment.NewLine, lines);
        }
    }

}
