using CrypTool.PluginBase;
using CrypTool.PluginBase.IO;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Controls;

namespace DataTools
{
    /// <summary>
    /// Small operations on byte data: UU encoding, ZIP archives, byte-wise addition and subtraction of a key, and bit shifts and rotations.
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("DataTools.Properties.Resources", "PluginCaption", "PluginTooltip", "DataTools/DetailedDescription/doc.xml", "DataTools/icon.png")]
    [ComponentCategory(ComponentCategory.ToolsDataInputOutput)]
    public class DataTools : ICrypComponent
    {
        private DataToolsSettings _settings = new DataToolsSettings();

        [PropertyInfo(Direction.InputData, "InputDataCaption", "InputDataTooltip", true)]
        public ICrypToolStream InputData { get; set; }

        [PropertyInfo(Direction.InputData, "KeyCaption", "KeyTooltip", false)]
        public byte[] Key { get; set; }

        [PropertyInfo(Direction.OutputData, "OutputDataCaption", "OutputDataTooltip", false)]
        public ICrypToolStream OutputData { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (DataToolsSettings)value;
        }

        public UserControl Presentation => null;

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            if (InputData == null)
            {
                GuiLogMessage("There is no input data.", NotificationLevel.Error);
                return;
            }

            byte[] data = InputData.CreateReader().ReadFully();
            try
            {
                byte[] result;
                switch (_settings.Operation)
                {
                    case 1: result = Conversions.UUDecode(data); break;
                    case 2: result = Conversions.Zip(data); break;
                    case 3: result = Conversions.Unzip(data); break;
                    case 4: result = Conversions.AddKey(data, Key, false); break;
                    case 5: result = Conversions.AddKey(data, Key, true); break;
                    case 6: result = Conversions.Shift(data, _settings.Amount, left: true, rotate: false); break;
                    case 7: result = Conversions.Shift(data, _settings.Amount, left: false, rotate: false); break;
                    case 8: result = Conversions.Shift(data, _settings.Amount, left: true, rotate: true); break;
                    case 9: result = Conversions.Shift(data, _settings.Amount, left: false, rotate: true); break;
                    default: result = Conversions.UUEncode(data, "data"); break;
                }
                OutputData = new CStreamWriter(result);
                OnPropertyChanged("OutputData");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException || ex is FormatException)
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
            Dispose();
        }

        public void PostExecution()
        {
            Dispose();
        }

        public void Dispose()
        {
            InputData = null;
            Key = null;
            OutputData = null;
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

    /// <summary>The operations, free of plugin infrastructure; invalid input throws ArgumentException, InvalidDataException or FormatException.</summary>
    public static class Conversions
    {
        private static char UUChar(int value) => value == 0 ? '`' : (char)(value + 32);

        /// <summary>The uuencode format: "begin 644 name", lines of up to 45 bytes (a length character and groups of 3 bytes as 4 characters), a zero-length line and "end".</summary>
        public static byte[] UUEncode(byte[] data, string name)
        {
            var text = new StringBuilder("begin 644 " + name + "\n");
            for (int offset = 0; offset < data.Length; offset += 45)
            {
                int count = Math.Min(45, data.Length - offset);
                text.Append(UUChar(count));
                for (int i = 0; i < count; i += 3)
                {
                    int b0 = data[offset + i];
                    int b1 = i + 1 < count ? data[offset + i + 1] : 0;
                    int b2 = i + 2 < count ? data[offset + i + 2] : 0;
                    text.Append(UUChar(b0 >> 2)).Append(UUChar((b0 << 4 | b1 >> 4) & 63)).Append(UUChar((b1 << 2 | b2 >> 6) & 63)).Append(UUChar(b2 & 63));
                }
                text.Append('\n');
            }
            text.Append("`\nend\n");
            return Encoding.ASCII.GetBytes(text.ToString());
        }

        /// <summary>Decodes uuencoded text; the "begin" and "end" lines are optional.</summary>
        public static byte[] UUDecode(byte[] encoded)
        {
            var result = new MemoryStream();
            foreach (string rawLine in Encoding.ASCII.GetString(encoded).Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("begin ", StringComparison.Ordinal)) { continue; }
                if (line == "end") { break; }
                int count = (line[0] - 32) & 63;
                if (count == 0) { continue; }
                int needed = (count + 2) / 3 * 4;
                if (line.Length - 1 < needed) { throw new FormatException("A line is too short for the length given at its start: " + line); }
                var bytes = new byte[(needed / 4) * 3];
                for (int i = 0, k = 0; i < needed; i += 4, k += 3)
                {
                    int c0 = (line[1 + i] - 32) & 63, c1 = (line[2 + i] - 32) & 63, c2 = (line[3 + i] - 32) & 63, c3 = (line[4 + i] - 32) & 63;
                    bytes[k] = (byte)(c0 << 2 | c1 >> 4);
                    bytes[k + 1] = (byte)(c1 << 4 | c2 >> 2);
                    bytes[k + 2] = (byte)(c2 << 6 | c3);
                }
                result.Write(bytes, 0, count);
            }
            return result.ToArray();
        }

        /// <summary>A ZIP archive with one entry named "data".</summary>
        public static byte[] Zip(byte[] data)
        {
            var archive = new MemoryStream();
            using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, true))
            {
                using (Stream entry = zip.CreateEntry("data", CompressionLevel.Optimal).Open())
                {
                    entry.Write(data, 0, data.Length);
                }
            }
            return archive.ToArray();
        }

        /// <summary>The content of the first entry of a ZIP archive.</summary>
        public static byte[] Unzip(byte[] archive)
        {
            using (var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read))
            {
                if (zip.Entries.Count == 0) { throw new InvalidDataException("The ZIP archive is empty."); }
                using (Stream entry = zip.Entries[0].Open())
                {
                    var result = new MemoryStream();
                    entry.CopyTo(result);
                    return result.ToArray();
                }
            }
        }

        /// <summary>Adds (or subtracts) the key bytes to the data bytes modulo 256; the key is repeated as often as necessary.</summary>
        public static byte[] AddKey(byte[] data, byte[] key, bool subtract)
        {
            if (key == null || key.Length == 0) { throw new ArgumentException("The key is needed."); }
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte)(subtract ? data[i] - key[i % key.Length] : data[i] + key[i % key.Length]);
            }
            return result;
        }

        /// <summary>Shifts or rotates every byte by the given number of bits (1 to 7).</summary>
        public static byte[] Shift(byte[] data, int amount, bool left, bool rotate)
        {
            if (amount < 1 || amount > 7) { throw new ArgumentException("The number of bits must be from 1 to 7."); }
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                int b = data[i];
                int shifted = left ? b << amount : b >> amount;
                if (rotate) { shifted |= left ? b >> (8 - amount) : b << (8 - amount); }
                result[i] = (byte)shifted;
            }
            return result;
        }
    }
}
