using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace ExtraBlockCiphers
{
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("ExtraBlockCiphers.Properties.Resources", "PluginCaption", "PluginTooltip", "ExtraBlockCiphers/DetailedDescription/doc.xml", "ExtraBlockCiphers/icon.png")]
    [ComponentCategory(ComponentCategory.CiphersModernSymmetric)]
    public class ExtraBlockCiphers : ICrypComponent
    {
        private ExtraBlockCiphersSettings _settings = new ExtraBlockCiphersSettings();

        [PropertyInfo(Direction.InputData, "InputCaption", "InputTooltip", true)]
        public string Input { get; set; }

        [PropertyInfo(Direction.InputData, "KeyCaption", "KeyTooltip", true)]
        public string Key { get; set; }

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
            set => _settings = (ExtraBlockCiphersSettings)value;
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
                IBlockEngine engine = ExtraCiphers.Create(_settings.Cipher, ExtraCiphers.FromHex(Key));
                byte[] data = ExtraCiphers.FromHex(Input);
                byte[] result = _settings.Mode == 0 ? ExtraCiphers.Ecb(engine, data, true) : _settings.Mode == 1 ? ExtraCiphers.Ecb(engine, data, false) : _settings.Mode == 2 ? ExtraCiphers.Cmac(engine, data) : ExtraCiphers.CbcMac(engine, data);
                Output = ExtraCiphers.ToHex(result);
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
            Input = Key = Output = null;
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
    /// The block ciphers MARS (IBM, AES finalist, 128-bit blocks, keys of 128 to 448 bits, with the key setup that NIST accepted in 1999), MISTY1 (Matsui, RFC 2994, 64-bit blocks, 128-bit keys)
    /// and SAFER+ (Massey, Khachatrian and Kuregian, AES candidate, 128-bit blocks, keys of 128, 192 or 256 bits), written from their published specifications.
    /// </summary>
    public interface IBlockEngine
    {
        int BlockSize { get; }
        byte[] Encrypt(byte[] block);
        byte[] Decrypt(byte[] block);
    }

    public static class Rotation
    {
        public static uint Left(uint x, int n) { n &= 31; return n == 0 ? x : (x << n) | (x >> (32 - n)); }
        public static uint Right(uint x, int n) => Left(x, 32 - (n & 31));
    }

    public sealed class MarsEngine : IBlockEngine
    {
        public static readonly uint[] SBox =
        {
            0x09d0c479u, 0x28c8ffe0u, 0x84aa6c39u, 0x9dad7287u, 0x7dff9be3u, 0xd4268361u, 0xc96da1d4u, 0x7974cc93u,
            0x85d0582eu, 0x2a4b5705u, 0x1ca16a62u, 0xc3bd279du, 0x0f1f25e5u, 0x5160372fu, 0xc695c1fbu, 0x4d7ff1e4u,
            0xae5f6bf4u, 0x0d72ee46u, 0xff23de8au, 0xb1cf8e83u, 0xf14902e2u, 0x3e981e42u, 0x8bf53eb6u, 0x7f4bf8acu,
            0x83631f83u, 0x25970205u, 0x76afe784u, 0x3a7931d4u, 0x4f846450u, 0x5c64c3f6u, 0x210a5f18u, 0xc6986a26u,
            0x28f4e826u, 0x3a60a81cu, 0xd340a664u, 0x7ea820c4u, 0x526687c5u, 0x7eddd12bu, 0x32a11d1du, 0x9c9ef086u,
            0x80f6e831u, 0xab6f04adu, 0x56fb9b53u, 0x8b2e095cu, 0xb68556aeu, 0xd2250b0du, 0x294a7721u, 0xe21fb253u,
            0xae136749u, 0xe82aae86u, 0x93365104u, 0x99404a66u, 0x78a784dcu, 0xb69ba84bu, 0x04046793u, 0x23db5c1eu,
            0x46cae1d6u, 0x2fe28134u, 0x5a223942u, 0x1863cd5bu, 0xc190c6e3u, 0x07dfb846u, 0x6eb88816u, 0x2d0dcc4au,
            0xa4ccae59u, 0x3798670du, 0xcbfa9493u, 0x4f481d45u, 0xeafc8ca8u, 0xdb1129d6u, 0xb0449e20u, 0x0f5407fbu,
            0x6167d9a8u, 0xd1f45763u, 0x4daa96c3u, 0x3bec5958u, 0xababa014u, 0xb6ccd201u, 0x38d6279fu, 0x02682215u,
            0x8f376cd5u, 0x092c237eu, 0xbfc56593u, 0x32889d2cu, 0x854b3e95u, 0x05bb9b43u, 0x7dcd5dcdu, 0xa02e926cu,
            0xfae527e5u, 0x36a1c330u, 0x3412e1aeu, 0xf257f462u, 0x3c4f1d71u, 0x30a2e809u, 0x68e5f551u, 0x9c61ba44u,
            0x5ded0ab8u, 0x75ce09c8u, 0x9654f93eu, 0x698c0ccau, 0x243cb3e4u, 0x2b062b97u, 0x0f3b8d9eu, 0x00e050dfu,
            0xfc5d6166u, 0xe35f9288u, 0xc079550du, 0x0591aee8u, 0x8e531e74u, 0x75fe3578u, 0x2f6d829au, 0xf60b21aeu,
            0x95e8eb8du, 0x6699486bu, 0x901d7d9bu, 0xfd6d6e31u, 0x1090acefu, 0xe0670dd8u, 0xdab2e692u, 0xcd6d4365u,
            0xe5393514u, 0x3af345f0u, 0x6241fc4du, 0x460da3a3u, 0x7bcf3729u, 0x8bf1d1e0u, 0x14aac070u, 0x1587ed55u,
            0x3afd7d3eu, 0xd2f29e01u, 0x29a9d1f6u, 0xefb10c53u, 0xcf3b870fu, 0xb414935cu, 0x664465edu, 0x024acac7u,
            0x59a744c1u, 0x1d2936a7u, 0xdc580aa6u, 0xcf574ca8u, 0x040a7a10u, 0x6cd81807u, 0x8a98be4cu, 0xaccea063u,
            0xc33e92b5u, 0xd1e0e03du, 0xb322517eu, 0x2092bd13u, 0x386b2c4au, 0x52e8dd58u, 0x58656dfbu, 0x50820371u,
            0x41811896u, 0xe337ef7eu, 0xd39fb119u, 0xc97f0df6u, 0x68fea01bu, 0xa150a6e5u, 0x55258962u, 0xeb6ff41bu,
            0xd7c9cd7au, 0xa619cd9eu, 0xbcf09576u, 0x2672c073u, 0xf003fb3cu, 0x4ab7a50bu, 0x1484126au, 0x487ba9b1u,
            0xa64fc9c6u, 0xf6957d49u, 0x38b06a75u, 0xdd805fcdu, 0x63d094cfu, 0xf51c999eu, 0x1aa4d343u, 0xb8495294u,
            0xce9f8e99u, 0xbffcd770u, 0xc7c275ccu, 0x378453a7u, 0x7b21be33u, 0x397f41bdu, 0x4e94d131u, 0x92cc1f98u,
            0x5915ea51u, 0x99f861b7u, 0xc9980a88u, 0x1d74fd5fu, 0xb0a495f8u, 0x614deed0u, 0xb5778eeau, 0x5941792du,
            0xfa90c1f8u, 0x33f824b4u, 0xc4965372u, 0x3ff6d550u, 0x4ca5fec0u, 0x8630e964u, 0x5b3fbbd6u, 0x7da26a48u,
            0xb203231au, 0x04297514u, 0x2d639306u, 0x2eb13149u, 0x16a45272u, 0x532459a0u, 0x8e5f4872u, 0xf966c7d9u,
            0x07128dc0u, 0x0d44db62u, 0xafc8d52du, 0x06316131u, 0xd838e7ceu, 0x1bc41d00u, 0x3a2e8c0fu, 0xea83837eu,
            0xb984737du, 0x13ba4891u, 0xc4f8b949u, 0xa6d6acb3u, 0xa215cdceu, 0x8359838bu, 0x6bd1aa31u, 0xf579dd52u,
            0x21b93f93u, 0xf5176781u, 0x187dfddeu, 0xe94aeb76u, 0x2b38fd54u, 0x431de1dau, 0xab394825u, 0x9ad3048fu,
            0xdfea32aau, 0x659473e3u, 0x623f7863u, 0xf3346c59u, 0xab3ab685u, 0x3346a90bu, 0x6b56443eu, 0xc6de01f8u,
            0x8d421fc0u, 0x9b0ed10cu, 0x88f1a1e9u, 0x54c1f029u, 0x7dead57bu, 0x8d7ba426u, 0x4cf5178au, 0x551a7ccau,
            0x1a9a5f08u, 0xfcd651b9u, 0x25605182u, 0xe11fc6c3u, 0xb6fd9676u, 0x337b3027u, 0xb7c8eb14u, 0x9e5fd030u,
            0x6b57e354u, 0xad913cf7u, 0x7e16688du, 0x58872a69u, 0x2c2fc7dfu, 0xe389ccc6u, 0x30738df1u, 0x0824a734u,
            0xe1797a8bu, 0xa4a8d57bu, 0x5b5d193bu, 0xc8a8309bu, 0x73f9a978u, 0x73398d32u, 0x0f59573eu, 0xe9df2b03u,
            0xe8a5b6c8u, 0x848d0704u, 0x98df93c2u, 0x720a1dc3u, 0x684f259au, 0x943ba848u, 0xa6370152u, 0x863b5ea3u,
            0xd17b978bu, 0x6d9b58efu, 0x0a700dd4u, 0xa73d36bfu, 0x8e6a0829u, 0x8695bc14u, 0xe35b3447u, 0x933ac568u,
            0x8894b022u, 0x2f511c27u, 0xddfbcc3cu, 0x006662b6u, 0x117c83feu, 0x4e12b414u, 0xc2bca766u, 0x3a2fec10u,
            0xf4562420u, 0x55792e2au, 0x46f5d857u, 0xceda25ceu, 0xc3601d3bu, 0x6c00ab46u, 0xefac9c28u, 0xb3c35047u,
            0x611dfee3u, 0x257c3207u, 0xfdd58482u, 0x3b14d84fu, 0x23becb64u, 0xa075f3a3u, 0x088f8eadu, 0x07adf158u,
            0x7796943cu, 0xfacabf3du, 0xc09730cdu, 0xf7679969u, 0xda44e9edu, 0x2c854c12u, 0x35935fa3u, 0x2f057d9fu,
            0x690624f8u, 0x1cb0bafdu, 0x7b0dbdc6u, 0x810f23bbu, 0xfa929a1au, 0x6d969a17u, 0x6742979bu, 0x74ac7d05u,
            0x010e65c4u, 0x86a3d963u, 0xf907b5a0u, 0xd0042bd3u, 0x158d7d03u, 0x287a8255u, 0xbba8366fu, 0x096edc33u,
            0x21916a7bu, 0x77b56b86u, 0x951622f9u, 0xa6c5e650u, 0x8cea17d1u, 0xcd8c62bcu, 0xa3d63433u, 0x358a68fdu,
            0x0f9b9d3cu, 0xd6aa295bu, 0xfe33384au, 0xc000738eu, 0xcd67eb2fu, 0xe2eb6dc2u, 0x97338b02u, 0x06c9f246u,
            0x419cf1adu, 0x2b83c045u, 0x3723f18au, 0xcb5b3089u, 0x160bead7u, 0x5d494656u, 0x35f8a74bu, 0x1e4e6c9eu,
            0x000399bdu, 0x67466880u, 0xb4174831u, 0xacf423b2u, 0xca815ab3u, 0x5a6395e7u, 0x302a67c5u, 0x8bdb446bu,
            0x108f8fa4u, 0x10223edau, 0x92b8b48bu, 0x7f38d0eeu, 0xab2701d4u, 0x0262d415u, 0xaf224a30u, 0xb3d88abau,
            0xf8b2c3afu, 0xdaf7ef70u, 0xcc97d3b7u, 0xe9614b6cu, 0x2baebff4u, 0x70f687cfu, 0x386c9156u, 0xce092ee5u,
            0x01e87da6u, 0x6ce91e6au, 0xbb7bcc84u, 0xc7922c20u, 0x9d3b71fdu, 0x060e41c6u, 0xd7590f15u, 0x4e03bb47u,
            0x183c198eu, 0x63eeb240u, 0x2ddbf49au, 0x6d5cba54u, 0x923750afu, 0xf9e14236u, 0x7838162bu, 0x59726c72u,
            0x81b66760u, 0xbb2926c1u, 0x48a0ce0du, 0xa6c0496du, 0xad43507bu, 0x718d496au, 0x9df057afu, 0x44b1bde6u,
            0x054356dcu, 0xde7ced35u, 0xd51a138bu, 0x62088cc9u, 0x35830311u, 0xc96efca2u, 0x686f86ecu, 0x8e77cb68u,
            0x63e1d6b8u, 0xc80f9778u, 0x79c491fdu, 0x1b4c67f2u, 0x72698d7du, 0x5e368c31u, 0xf7d95e2eu, 0xa1d3493fu,
            0xdcd9433eu, 0x896f1552u, 0x4bc4ca7au, 0xa6d1baf4u, 0xa5a96dccu, 0x0bef8b46u, 0xa169fda7u, 0x74df40b7u,
            0x4e208804u, 0x9a756607u, 0x038e87c8u, 0x20211e44u, 0x8b7ad4bfu, 0xc6403f35u, 0x1848e36du, 0x80bdb038u,
            0x1e62891cu, 0x643d2107u, 0xbf04d6f8u, 0x21092c8cu, 0xf644f389u, 0x0778404eu, 0x7b78adb8u, 0xa2c52d53u,
            0x42157abeu, 0xa2253e2eu, 0x7bf3f4aeu, 0x80f594f9u, 0x953194e7u, 0x77eb92edu, 0xb3816930u, 0xda8d9336u,
            0xbf447469u, 0xf26d9483u, 0xee6faed5u, 0x71371235u, 0xde425f73u, 0xb4e59f43u, 0x7dbe2d4eu, 0x2d37b185u,
            0x49dc9a63u, 0x98c39d98u, 0x1301c9a2u, 0x389b1bbfu, 0x0c18588du, 0xa421c1bau, 0x7aa3865cu, 0x71e08558u,
            0x3c5cfcaau, 0x7d239ca4u, 0x0297d9ddu, 0xd7dc2830u, 0x4b37802bu, 0x7428ab54u, 0xaeee0347u, 0x4b3fbb85u,
            0x692f2f08u, 0x134e578eu, 0x36d9e0bfu, 0xae8b5fcfu, 0xedb93ecfu, 0x2b27248eu, 0x170eb1efu, 0x7dc57fd6u,
            0x1e760f16u, 0xb1136601u, 0x864e1b9bu, 0xd7ea7319u, 0x3ab871bdu, 0xcfa4d76fu, 0xe31bd782u, 0x0dbeb469u,
            0xabb96061u, 0x5370f85du, 0xffb07e37u, 0xda30d0fbu, 0xebc977b6u, 0x0b98b40fu, 0x3a4d0fe6u, 0xdf4fc26bu,
            0x159cf22au, 0xc298d6e2u, 0x2b78ef6au, 0x61a94ac0u, 0xab561187u, 0x14eea0f0u, 0xdf0d4164u, 0x19af70eeu,
        };

        private static readonly uint[] Fixed = { 0xa4a8d57b, 0x5b5d193b, 0xc8a8309b, 0x73f9a978 };
        private readonly uint[] _k = new uint[40];

        public int BlockSize => 16;

        public MarsEngine(byte[] key)
        {
            if (key.Length < 16 || key.Length > 56 || key.Length % 4 != 0) { throw new ArgumentException("A MARS key has 16 to 56 bytes, a multiple of 4."); }
            int n = key.Length / 4;
            var t = new uint[15];
            for (int i = 0; i < n; i++) { t[i] = BitConverter.ToUInt32(key, 4 * i); }
            t[n] = (uint)n;
            for (int j = 0; j < 4; j++)
            {
                for (int i = 0; i < 15; i++) { t[i] ^= Rotation.Left(t[(i + 8) % 15] ^ t[(i + 13) % 15], 3) ^ (uint)(4 * i + j); }
                for (int repeat = 0; repeat < 4; repeat++)
                {
                    for (int i = 0; i < 15; i++) { t[i] = Rotation.Left(t[i] + SBox[t[(i + 14) % 15] & 511], 9); }
                }
                for (int i = 0; i < 10; i++) { _k[10 * j + i] = t[(4 * i) % 15]; }
            }
            for (int i = 5; i <= 35; i += 2)
            {
                int j = (int)(_k[i] & 3);
                uint w = _k[i] | 3;
                uint mask = 0;
                // runs of at least ten equal bits; inside a run the bits whose two neighbours are equal to them (and 2 <= l <= 30)
                for (int start = 0; start < 32;)
                {
                    int end = start;
                    while (end + 1 < 32 && ((w >> (end + 1)) & 1) == ((w >> start) & 1)) { end++; }
                    if (end - start + 1 >= 10) { for (int l = Math.Max(start + 1, 2); l <= Math.Min(end - 1, 30); l++) { mask |= 1u << l; } }
                    start = end + 1;
                }
                uint pattern = Rotation.Left(Fixed[j], (int)(_k[i - 1] & 31));
                _k[i] = w ^ (pattern & mask);
            }
        }

        private static uint E1(uint d0, uint k1, uint k2, out uint m, out uint r)
        {
            m = d0 + k1;
            r = Rotation.Left(d0, 13) * k2;
            uint l = SBox[m & 511];
            r = Rotation.Left(r, 5);
            m = Rotation.Left(m, (int)(r & 31));
            l ^= r;
            r = Rotation.Left(r, 5);
            l ^= r;
            l = Rotation.Left(l, (int)(r & 31));
            return l;
        }

        private static byte[] Pack(uint[] d) => d.SelectMany(BitConverter.GetBytes).ToArray();

        private static uint[] Unpack(byte[] block) => Enumerable.Range(0, 4).Select(i => BitConverter.ToUInt32(block, 4 * i)).ToArray();

        private static void RotateRight(uint[] d) { uint last = d[0]; d[0] = d[1]; d[1] = d[2]; d[2] = d[3]; d[3] = last; }

        private static void RotateLeft(uint[] d) { uint first = d[3]; d[3] = d[2]; d[2] = d[1]; d[1] = d[0]; d[0] = first; }

        private static uint S0(uint x) => SBox[x & 255];

        private static uint S1(uint x) => SBox[256 + (x & 255)];

        public byte[] Encrypt(byte[] block)
        {
            var d = Unpack(block);
            for (int i = 0; i < 4; i++) { d[i] += _k[i]; }
            for (int i = 0; i < 8; i++)
            {
                d[1] ^= S0(d[0]); d[1] += S1(d[0] >> 8); d[2] += S0(d[0] >> 16); d[3] ^= S1(d[0] >> 24);
                d[0] = Rotation.Right(d[0], 24);
                if (i == 0 || i == 4) { d[0] += d[3]; }
                if (i == 1 || i == 5) { d[0] += d[1]; }
                RotateRight(d);
            }
            for (int i = 0; i < 16; i++)
            {
                uint m, r, l = E1(d[0], _k[2 * i + 4], _k[2 * i + 5], out m, out r);
                d[0] = Rotation.Left(d[0], 13);
                d[2] += m;
                if (i < 8) { d[1] += l; d[3] ^= r; } else { d[3] += l; d[1] ^= r; }
                RotateRight(d);
            }
            for (int i = 0; i < 8; i++)
            {
                if (i == 2 || i == 6) { d[0] -= d[3]; }
                if (i == 3 || i == 7) { d[0] -= d[1]; }
                d[1] ^= S1(d[0]); d[2] -= S0(d[0] >> 24); d[3] -= S1(d[0] >> 16); d[3] ^= S0(d[0] >> 8);
                d[0] = Rotation.Left(d[0], 24);
                RotateRight(d);
            }
            for (int i = 0; i < 4; i++) { d[i] -= _k[36 + i]; }
            return Pack(d);
        }

        public byte[] Decrypt(byte[] block)
        {
            var d = Unpack(block);
            for (int i = 0; i < 4; i++) { d[i] += _k[36 + i]; }
            for (int i = 7; i >= 0; i--)
            {
                RotateLeft(d);
                d[0] = Rotation.Right(d[0], 24);
                d[3] ^= S0(d[0] >> 8); d[3] += S1(d[0] >> 16); d[2] += S0(d[0] >> 24); d[1] ^= S1(d[0]);
                if (i == 3 || i == 7) { d[0] += d[1]; }
                if (i == 2 || i == 6) { d[0] += d[3]; }
            }
            for (int i = 15; i >= 0; i--)
            {
                RotateLeft(d);
                d[0] = Rotation.Right(d[0], 13);
                uint m, r, l = E1(d[0], _k[2 * i + 4], _k[2 * i + 5], out m, out r);
                d[2] -= m;
                if (i < 8) { d[1] -= l; d[3] ^= r; } else { d[3] -= l; d[1] ^= r; }
            }
            for (int i = 7; i >= 0; i--)
            {
                RotateLeft(d);
                if (i == 0 || i == 4) { d[0] -= d[3]; }
                if (i == 1 || i == 5) { d[0] -= d[1]; }
                d[0] = Rotation.Left(d[0], 24);
                d[3] ^= S1(d[0] >> 24); d[2] -= S0(d[0] >> 16); d[1] -= S1(d[0] >> 8); d[1] ^= S0(d[0]);
            }
            for (int i = 0; i < 4; i++) { d[i] -= _k[i]; }
            return Pack(d);
        }
    }

    public sealed class MistyEngine : IBlockEngine
    {
        private static readonly int[] S7Table =
        {
            27, 50, 51, 90, 59, 16, 23, 84, 91, 26, 114, 115, 107, 44, 102, 73,
            31, 36, 19, 108, 55, 46, 63, 74, 93, 15, 64, 86, 37, 81, 28, 4,
            11, 70, 32, 13, 123, 53, 68, 66, 43, 30, 65, 20, 75, 121, 21, 111,
            14, 85, 9, 54, 116, 12, 103, 83, 40, 10, 126, 56, 2, 7, 96, 41,
            25, 18, 101, 47, 48, 57, 8, 104, 95, 120, 42, 76, 100, 69, 117, 61,
            89, 72, 3, 87, 124, 79, 98, 60, 29, 33, 94, 39, 106, 112, 77, 58,
            1, 109, 110, 99, 24, 119, 35, 5, 38, 118, 0, 49, 45, 122, 127, 97,
            80, 34, 17, 6, 71, 22, 82, 78, 113, 62, 105, 67, 52, 92, 88, 125,
        };

        private static readonly int[] S9Table =
        {
            451, 203, 339, 415, 483, 233, 251, 53, 385, 185, 279, 491, 307, 9, 45, 211,
            199, 330, 55, 126, 235, 356, 403, 472, 163, 286, 85, 44, 29, 418, 355, 280,
            331, 338, 466, 15, 43, 48, 314, 229, 273, 312, 398, 99, 227, 200, 500, 27,
            1, 157, 248, 416, 365, 499, 28, 326, 125, 209, 130, 490, 387, 301, 244, 414,
            467, 221, 482, 296, 480, 236, 89, 145, 17, 303, 38, 220, 176, 396, 271, 503,
            231, 364, 182, 249, 216, 337, 257, 332, 259, 184, 340, 299, 430, 23, 113, 12,
            71, 88, 127, 420, 308, 297, 132, 349, 413, 434, 419, 72, 124, 81, 458, 35,
            317, 423, 357, 59, 66, 218, 402, 206, 193, 107, 159, 497, 300, 388, 250, 406,
            481, 361, 381, 49, 384, 266, 148, 474, 390, 318, 284, 96, 373, 463, 103, 281,
            101, 104, 153, 336, 8, 7, 380, 183, 36, 25, 222, 295, 219, 228, 425, 82,
            265, 144, 412, 449, 40, 435, 309, 362, 374, 223, 485, 392, 197, 366, 478, 433,
            195, 479, 54, 238, 494, 240, 147, 73, 154, 438, 105, 129, 293, 11, 94, 180,
            329, 455, 372, 62, 315, 439, 142, 454, 174, 16, 149, 495, 78, 242, 509, 133,
            253, 246, 160, 367, 131, 138, 342, 155, 316, 263, 359, 152, 464, 489, 3, 510,
            189, 290, 137, 210, 399, 18, 51, 106, 322, 237, 368, 283, 226, 335, 344, 305,
            327, 93, 275, 461, 121, 353, 421, 377, 158, 436, 204, 34, 306, 26, 232, 4,
            391, 493, 407, 57, 447, 471, 39, 395, 198, 156, 208, 334, 108, 52, 498, 110,
            202, 37, 186, 401, 254, 19, 262, 47, 429, 370, 475, 192, 267, 470, 245, 492,
            269, 118, 276, 427, 117, 268, 484, 345, 84, 287, 75, 196, 446, 247, 41, 164,
            14, 496, 119, 77, 378, 134, 139, 179, 369, 191, 270, 260, 151, 347, 352, 360,
            215, 187, 102, 462, 252, 146, 453, 111, 22, 74, 161, 313, 175, 241, 400, 10,
            426, 323, 379, 86, 397, 358, 212, 507, 333, 404, 410, 135, 504, 291, 167, 440,
            321, 60, 505, 320, 42, 341, 282, 417, 408, 213, 294, 431, 97, 302, 343, 476,
            114, 394, 170, 150, 277, 239, 69, 123, 141, 325, 83, 95, 376, 178, 46, 32,
            469, 63, 457, 487, 428, 68, 56, 20, 177, 363, 171, 181, 90, 386, 456, 468,
            24, 375, 100, 207, 109, 256, 409, 304, 346, 5, 288, 443, 445, 224, 79, 214,
            319, 452, 298, 21, 6, 255, 411, 166, 67, 136, 80, 351, 488, 289, 115, 382,
            188, 194, 201, 371, 393, 501, 116, 460, 486, 424, 405, 31, 65, 13, 442, 50,
            61, 465, 128, 168, 87, 441, 354, 328, 217, 261, 98, 122, 33, 511, 274, 264,
            448, 169, 285, 432, 422, 205, 243, 92, 258, 91, 473, 324, 502, 173, 165, 58,
            459, 310, 383, 70, 225, 30, 477, 230, 311, 506, 389, 140, 143, 64, 437, 190,
            120, 0, 172, 272, 350, 292, 2, 444, 162, 234, 112, 508, 278, 348, 76, 450,
        };

        private readonly int[] _ek = new int[32];

        public int BlockSize => 8;

        public MistyEngine(byte[] key)
        {
            if (key.Length != 16) { throw new ArgumentException("A MISTY1 key has 16 bytes."); }
            for (int i = 0; i < 8; i++) { _ek[i] = key[2 * i] * 256 + key[2 * i + 1]; }
            for (int i = 0; i < 8; i++)
            {
                _ek[i + 8] = FI(_ek[i], _ek[(i + 1) % 8]);
                _ek[i + 16] = _ek[i + 8] & 0x1ff;
                _ek[i + 24] = _ek[i + 8] >> 9;
            }
        }

        private static int FI(int input, int key)
        {
            int d9 = input >> 7, d7 = input & 0x7f;
            d9 = S9Table[d9] ^ d7;
            d7 = S7Table[d7] ^ d9;
            d7 &= 0x7f;
            d7 ^= key >> 9;
            d9 ^= key & 0x1ff;
            d9 = S9Table[d9] ^ d7;
            return (d7 << 9) | d9;
        }

        private uint FO(uint input, int k)
        {
            int t0 = (int)(input >> 16), t1 = (int)(input & 0xffff);
            t0 ^= _ek[k];
            t0 = FI(t0, _ek[(k + 5) % 8 + 8]);
            t0 ^= t1;
            t1 ^= _ek[(k + 2) % 8];
            t1 = FI(t1, _ek[(k + 1) % 8 + 8]);
            t1 ^= t0;
            t0 ^= _ek[(k + 7) % 8];
            t0 = FI(t0, _ek[(k + 3) % 8 + 8]);
            t0 ^= t1;
            t1 ^= _ek[(k + 4) % 8];
            return ((uint)t1 << 16) | (uint)t0;
        }

        private uint FL(uint input, int k, bool inverse)
        {
            int d0 = (int)(input >> 16), d1 = (int)(input & 0xffff);
            if (k % 2 == 0)
            {
                if (!inverse) { d1 ^= d0 & _ek[k / 2]; d0 ^= d1 | _ek[(k / 2 + 6) % 8 + 8]; }
                else { d0 ^= d1 | _ek[(k / 2 + 6) % 8 + 8]; d1 ^= d0 & _ek[k / 2]; }
            }
            else
            {
                if (!inverse) { d1 ^= d0 & _ek[((k - 1) / 2 + 2) % 8 + 8]; d0 ^= d1 | _ek[((k - 1) / 2 + 4) % 8]; }
                else { d0 ^= d1 | _ek[((k - 1) / 2 + 4) % 8]; d1 ^= d0 & _ek[((k - 1) / 2 + 2) % 8 + 8]; }
            }
            return ((uint)d0 << 16) | (uint)d1;
        }

        private static uint Word(byte[] b, int offset) => (uint)(b[offset] << 24 | b[offset + 1] << 16 | b[offset + 2] << 8 | b[offset + 3]);

        public byte[] Encrypt(byte[] block)
        {
            uint d0 = Word(block, 0), d1 = Word(block, 4);
            d0 = FL(d0, 0, false); d1 = FL(d1, 1, false); d1 ^= FO(d0, 0);
            d0 ^= FO(d1, 1);
            d0 = FL(d0, 2, false); d1 = FL(d1, 3, false); d1 ^= FO(d0, 2);
            d0 ^= FO(d1, 3);
            d0 = FL(d0, 4, false); d1 = FL(d1, 5, false); d1 ^= FO(d0, 4);
            d0 ^= FO(d1, 5);
            d0 = FL(d0, 6, false); d1 = FL(d1, 7, false); d1 ^= FO(d0, 6);
            d0 ^= FO(d1, 7);
            d0 = FL(d0, 8, false); d1 = FL(d1, 9, false);
            return Bytes(d1).Concat(Bytes(d0)).ToArray();   // C = (D1 << 32) | D0
        }

        public byte[] Decrypt(byte[] block)
        {
            uint d1 = Word(block, 0), d0 = Word(block, 4);
            d0 = FL(d0, 8, true); d1 = FL(d1, 9, true);
            d0 ^= FO(d1, 7); d1 ^= FO(d0, 6);
            d0 = FL(d0, 6, true); d1 = FL(d1, 7, true);
            d0 ^= FO(d1, 5); d1 ^= FO(d0, 4);
            d0 = FL(d0, 4, true); d1 = FL(d1, 5, true);
            d0 ^= FO(d1, 3); d1 ^= FO(d0, 2);
            d0 = FL(d0, 2, true); d1 = FL(d1, 3, true);
            d0 ^= FO(d1, 1); d1 ^= FO(d0, 0);
            d0 = FL(d0, 0, true); d1 = FL(d1, 1, true);
            return Bytes(d0).Concat(Bytes(d1)).ToArray();   // P = (D0 << 32) | D1
        }

        private static byte[] Bytes(uint x) => new[] { (byte)(x >> 24), (byte)(x >> 16), (byte)(x >> 8), (byte)x };
    }

    public sealed class SaferPlusEngine : IBlockEngine
    {
        private static readonly int[] Exponent = Enumerable.Range(0, 256).Select(i => Power(45, i) & 255).ToArray();
        private static readonly int[] Logarithm = MakeLogarithm();
        private static readonly int[] Shuffle = { 8, 11, 12, 15, 2, 1, 6, 5, 10, 9, 14, 13, 0, 7, 4, 3 };   // the Armenian shuffle: output i is input Shuffle[i]
        private readonly int[][] _key;
        private readonly int _rounds;

        public int BlockSize => 16;

        private static int Power(int b, int e) { int r = 1; for (int i = 0; i < e; i++) { r = r * b % 257; } return r; }

        private static int[] MakeLogarithm() { var l = new int[256]; for (int i = 0; i < 256; i++) { l[Exponent[i]] = i; } return l; }

        /// <summary>The bias words of the key schedule (p from 2 to 33): 45^(45^(17 p + i + 1) mod 257) mod 257 modulo 256 for the subkeys of a 128-bit key (p up to 17), and 45^(17 p + i + 1) mod 257 modulo 256 for the further subkeys of the 192 and 256-bit keys (the table of the reference code).</summary>
        public static int Bias(int p, int i) => p <= 17 ? Power(45, Power(45, 17 * p + i + 1)) & 255 : Power(45, 17 * p + i + 1) & 255;

        public SaferPlusEngine(byte[] key)
        {
            if (key.Length != 16 && key.Length != 24 && key.Length != 32) { throw new ArgumentException("A SAFER+ key has 16, 24 or 32 bytes."); }
            _rounds = key.Length == 16 ? 8 : key.Length == 24 ? 12 : 16;
            int n = key.Length;
            var t = new int[n + 1];
            for (int i = 0; i < n; i++) { t[i] = key[i]; t[n] ^= key[i]; }
            _key = new int[2 * _rounds + 1][];
            _key[0] = t.Take(16).ToArray();
            for (int p = 1; p <= 2 * _rounds; p++)
            {
                for (int i = 0; i <= n; i++) { t[i] = ((t[i] << 3) | (t[i] >> 5)) & 255; }
                _key[p] = new int[16];
                for (int i = 0; i < 16; i++) { _key[p][i] = (t[(p + i) % (n + 1)] + Bias(p + 1, i)) & 255; }
            }
        }

        private static bool XorFirst(int position) => position == 0 || position == 3 || position == 4 || position == 7 || position == 8 || position == 11 || position == 12 || position == 15;

        private static void Pht(int[] b) { for (int i = 0; i < 16; i += 2) { int second = (b[i] + b[i + 1]) & 255; b[i] = (b[i] + second) & 255; b[i + 1] = second; } }

        private static void InversePht(int[] b) { for (int i = 0; i < 16; i += 2) { int first = (b[i] - b[i + 1]) & 255; b[i + 1] = (b[i + 1] - first) & 255; b[i] = first; } }

        private static int[] Permute(int[] b) { var r = new int[16]; for (int i = 0; i < 16; i++) { r[i] = b[Shuffle[i]]; } return r; }

        private static int[] InversePermute(int[] b) { var r = new int[16]; for (int i = 0; i < 16; i++) { r[Shuffle[i]] = b[i]; } return r; }

        public byte[] Encrypt(byte[] block)
        {
            var b = block.Select(x => (int)x).ToArray();
            for (int r = 0; r < _rounds; r++)
            {
                var k1 = _key[2 * r]; var k2 = _key[2 * r + 1];
                for (int i = 0; i < 16; i++) { b[i] = XorFirst(i) ? (Exponent[(b[i] ^ k1[i]) & 255] + k2[i]) & 255 : Logarithm[(b[i] + k1[i]) & 255] ^ k2[i]; }
                Pht(b); b = Permute(b); Pht(b); b = Permute(b); Pht(b); b = Permute(b); Pht(b);
            }
            var last = _key[2 * _rounds];
            for (int i = 0; i < 16; i++) { b[i] = XorFirst(i) ? b[i] ^ last[i] : (b[i] + last[i]) & 255; }
            return b.Select(x => (byte)x).ToArray();
        }

        public byte[] Decrypt(byte[] block)
        {
            var b = block.Select(x => (int)x).ToArray();
            var last = _key[2 * _rounds];
            for (int i = 0; i < 16; i++) { b[i] = XorFirst(i) ? b[i] ^ last[i] : (b[i] - last[i]) & 255; }
            for (int r = _rounds - 1; r >= 0; r--)
            {
                var k1 = _key[2 * r]; var k2 = _key[2 * r + 1];
                InversePht(b); b = InversePermute(b); InversePht(b); b = InversePermute(b); InversePht(b); b = InversePermute(b); InversePht(b);
                for (int i = 0; i < 16; i++) { b[i] = XorFirst(i) ? Logarithm[(b[i] - k2[i]) & 255] ^ k1[i] : (Exponent[(b[i] ^ k2[i]) & 255] - k1[i]) & 255; }
            }
            return b.Select(x => (byte)x).ToArray();
        }
    }

    public static class ExtraCiphers
    {
        public static readonly string[] Names = { "MARS", "MISTY1", "SAFER+" };

        public static IBlockEngine Create(int cipher, byte[] key)
        {
            switch (cipher)
            {
                case 0: return new MarsEngine(key);
                case 1: return new MistyEngine(key);
                default: return new SaferPlusEngine(key);
            }
        }

        public static byte[] FromHex(string hex)
        {
            hex = new string(hex.Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (hex.Length % 2 != 0) { throw new FormatException("A hexadecimal value needs an even number of digits."); }
            return Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
        }

        public static string ToHex(byte[] data) => BitConverter.ToString(data).Replace("-", "");

        /// <summary>ECB mode: every block is encrypted on its own.</summary>
        public static byte[] Ecb(IBlockEngine engine, byte[] data, bool encrypt)
        {
            int size = engine.BlockSize;
            if (data.Length % size != 0) { throw new ArgumentException("The data must be a whole number of " + size + "-byte blocks."); }
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i += size)
            {
                var block = data.Skip(i).Take(size).ToArray();
                Array.Copy(encrypt ? engine.Encrypt(block) : engine.Decrypt(block), 0, result, i, size);
            }
            return result;
        }

        private static byte[] Xor(byte[] a, byte[] b) => a.Select((x, i) => (byte)(x ^ b[i])).ToArray();

        private static byte[] Double(byte[] block)
        {
            var result = new byte[block.Length];
            int carry = 0;
            for (int i = block.Length - 1; i >= 0; i--) { int v = block[i] << 1 | carry; carry = v >> 8; result[i] = (byte)v; }
            if (carry == 1) { result[result.Length - 1] ^= (byte)(block.Length == 8 ? 0x1b : 0x87); }
            return result;
        }

        /// <summary>CMAC (OMAC1, NIST SP 800-38B) for 64-bit and 128-bit blocks.</summary>
        public static byte[] Cmac(IBlockEngine engine, byte[] data)
        {
            int size = engine.BlockSize;
            byte[] k1 = Double(engine.Encrypt(new byte[size])), k2 = Double(k1);
            int blocks = Math.Max(1, (data.Length + size - 1) / size);
            bool complete = data.Length > 0 && data.Length % size == 0;
            var x = new byte[size];
            for (int i = 0; i < blocks - 1; i++) { x = engine.Encrypt(Xor(x, data.Skip(i * size).Take(size).ToArray())); }
            var last = data.Skip((blocks - 1) * size).ToArray();
            if (complete) { last = Xor(last, k1); }
            else { last = last.Concat(new byte[] { 0x80 }).Concat(new byte[size - last.Length - 1]).ToArray(); last = Xor(last, k2); }
            return engine.Encrypt(Xor(x, last));
        }

        /// <summary>The classic CBC-MAC: CBC encryption with a zero IV, the last block is the MAC; the message is padded with zero bytes.</summary>
        public static byte[] CbcMac(IBlockEngine engine, byte[] data)
        {
            int size = engine.BlockSize;
            var padded = data.Concat(new byte[(size - data.Length % size) % size]).ToArray();
            var x = new byte[size];
            for (int i = 0; i < padded.Length; i += size) { x = engine.Encrypt(Xor(x, padded.Skip(i).Take(size).ToArray())); }
            return x;
        }
    }

}
