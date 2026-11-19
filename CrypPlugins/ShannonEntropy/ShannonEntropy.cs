using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;

namespace ShannonEntropy
{
    /// <summary>
    /// The Shannon entropy H = -sum p(x) * log2 p(x) of the symbols of the data, in bits per symbol. A symbol is a byte or a pair of bytes.
    /// Texts have a low entropy (about 4 to 4.5 bits per byte for English), good ciphertext and random data one close to 8 bits per byte.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("ShannonEntropy.Properties.Resources", "PluginCaption", "PluginTooltip", "ShannonEntropy/DetailedDescription/doc.xml", "ShannonEntropy/icon.png")]
    [ComponentCategory(ComponentCategory.CryptanalysisGeneric)]
    public class ShannonEntropy : ICrypComponent
    {
        private ShannonEntropySettings _settings = new ShannonEntropySettings();

        [PropertyInfo(Direction.InputData, "InputDataCaption", "InputDataTooltip", true)]
        public byte[] InputData { get; set; }

        [PropertyInfo(Direction.OutputData, "EntropyCaption", "EntropyTooltip", false)]
        public double Value { get; set; }

        [PropertyInfo(Direction.OutputData, "ReportCaption", "ReportTooltip", false)]
        public string Report { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (ShannonEntropySettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            int size = _settings.Symbol == 1 ? 2 : 1;
            if (InputData == null || InputData.Length < size)
            {
                GuiLogMessage("The data is too short.", NotificationLevel.Error);
                return;
            }

            Value = Statistics.Compute(InputData, size, out int symbols, out int distinct);
            double maximum = size * 8;
            Report = string.Format("{0} symbols of {1} byte(s), {2} different; entropy {3:F4} bits per symbol of at most {4} ({5:F1} %); redundancy {6:F4} bits",
                symbols, size, distinct, Value, maximum, 100 * Value / maximum, maximum - Value);
            OnPropertyChanged("Value");
            OnPropertyChanged("Report");
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
            InputData = null;
            Report = null;
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

    public static class Statistics
    {
        /// <summary>The Shannon entropy in bits per symbol of the consecutive, non-overlapping symbols of the given size in bytes.</summary>
        public static double Compute(byte[] data, int symbolSize, out int symbols, out int distinct)
        {
            var counts = new Dictionary<int, int>();
            symbols = data.Length / symbolSize;
            for (int i = 0; i < symbols; i++)
            {
                int symbol = 0;
                for (int j = 0; j < symbolSize; j++) { symbol = symbol << 8 | data[i * symbolSize + j]; }
                counts.TryGetValue(symbol, out int count);
                counts[symbol] = count + 1;
            }
            distinct = counts.Count;
            double entropy = 0;
            foreach (int count in counts.Values)
            {
                double p = (double)count / symbols;
                entropy -= p * Math.Log(p, 2);
            }
            return entropy == 0 ? 0 : entropy;   // avoids -0
        }
    }
}
