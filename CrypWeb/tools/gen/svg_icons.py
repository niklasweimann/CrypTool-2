"""Icons for the components that have none of their own (used by tools/gen/plugins.py).

Many components of the web edition were written without an icon, or carry the default flower of the plugin template.
Those get a small SVG icon in the style of the application: a rounded tile in the color of the component's category
with a line glyph (the glyphs are the "g-..." entries of CrypWeb.App/Components/Icon.razor, which the category lists
use as well). Which glyph a component gets is chosen in GLYPHS below; a component that is not listed there gets its
initials instead.
"""
import os
import re

ICON_RAZOR = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'CrypWeb.App', 'Components', 'Icon.razor')

# md5 of the flower icon of the plugin template: a plugin that still has it counts as having no icon
FLOWER_MD5 = 'cdcb18a3202bd092bf15be49af7b2c36'

# the category colors of CrypWeb.Core/Theme.cs (DefaultCategoryColor)
CATEGORY_COLORS = {
    'CiphersClassic': '#b8c881', 'CiphersModernSymmetric': '#d49090', 'CiphersModernAsymmetric': '#6789a2',
    'Steganography': '#9acd32', 'HashFunctions': '#8abbbc', 'CryptanalysisSpecific': '#bc8aac',
    'CryptanalysisGeneric': '#8c8abc', 'Protocols': '#ff8c00',
}
TOOLS_COLOR = '#8abc94'
OTHER_COLOR = '#a3d090'

# component type -> glyph (without the "g-")
GLYPHS = {
    'AntColony.AntColony': 'route', 'AsciiCodes.AsciiCodes': 'code', 'Asn1Viewer.Asn1Viewer': 'tree',
    'Barcode.Barcode': 'barcode', 'BlockCiphers.BlockCiphers': 'blocks', 'Camellia.Camellia': 'blocks',
    'CertificateTool.CertificateTool': 'certificate', 'CfsSignature.CfsSignature': 'pen',
    'CipherIdentification.CipherIdentification': 'search', 'CipherMAC.CipherMAC': 'shield',
    'CipherVisualization.CipherVisualization': 'eye', 'ClassicAnalysis.ClassicAnalysis': 'bars',
    'ClassicMcEliece.ClassicMcEliece': 'lock', 'ClassicVariants.ClassicVariants': 'shift',
    'ConvolutionalCode.ConvolutionalCode': 'trellis', 'DataTools.DataTools': 'database',
    'DesVariants.DesVariants': 'blocks', 'DigitalSignatures.DigitalSignatures': 'pen',
    'DivisorGame.DivisorGame': 'divide', 'EcRandom.EcRandom': 'curve',
    'ExposureNotification.ExposureNotification': 'signal', 'ExtraBlockCiphers.ExtraBlockCiphers': 'blocks',
    'HashBasedSignatures.HashBasedSignatures': 'tree', 'HmacHashes.HmacHashes': 'shield',
    'HomomorphicEncryption.HomomorphicEncryption': 'calc', 'HuffmanErrors.HuffmanErrors': 'tree',
    'HybridEncryption.HybridEncryption': 'key', 'KeySearch.KeySearch': 'search', 'KeyTool.KeyTool': 'key',
    'Kleptography.Kleptography': 'hidden', 'MathVisualization.MathVisualization': 'curve',
    'MessageDigests.MessageDigests': 'hash', 'MlKem.MlKem': 'lock', 'MultipartyKeyExchange.MultipartyKeyExchange': 'share',
    'NumberShark.NumberShark': 'calc', 'NumberTheory.NumberTheory': 'sigma', 'NumberTools.NumberTools': 'calc',
    'OneTimePassword.OneTimePassword': 'clock', 'PasswordBasedEncryption.PasswordBasedEncryption': 'lock',
    'PatternLock.PatternLock': 'pattern', 'ProtocolVisualization.ProtocolVisualization': 'sequence',
    'PublicKeyEncryption.PublicKeyEncryption': 'key', 'RainbowTable.RainbowTable': 'table',
    'RandomVisualization.RandomVisualization': 'dice', 'RandomnessTests.RandomnessTests': 'dice',
    'SecretSharing.SecretSharing': 'share', 'ShannonEntropy.ShannonEntropy': 'bars', 'SideChannel.SideChannel': 'wave',
    'SignatureSchemes.SignatureSchemes': 'pen', 'Sudoku.Sudoku': 'grid', 'TextAnalysis.TextAnalysis': 'bars',
    'VerySmoothHash.VerySmoothHash': 'hash', 'CrypTool.Plugins.Collector.Collector': 'table',
    'CrypTool.Plugins.EncryptedVM.EncryptedVMMachine': 'chip', 'CrypTool.Plugins.EncryptedVM.EncryptedVMAssembler': 'chip',
    'CrypTool.Plugins.EncryptedVM.EncryptedVMKeyGen': 'chip', 'CrypTool.Plugins.EncryptedVM.EncryptedVMMemoryDisplay': 'chip',
    'CrypTool.Plugins.SATAttack.SATAttack': 'logic',
}


def glyph_paths():
    """The glyph table of Icon.razor: name (without "g-") -> path data."""
    with open(ICON_RAZOR, encoding='utf-8') as f:
        return dict(re.findall(r'"g-([\w-]+)" => "([^"]*)"', f.read()))


def category_color(category):
    if category.startswith('Tools'):
        return TOOLS_COLOR
    return CATEGORY_COLORS.get(category, OTHER_COLOR)


def mix(color, other, amount):
    """The color moved by amount (0..1) towards other (both #rrggbb)."""
    a = [int(color[i:i + 2], 16) for i in (1, 3, 5)]
    b = [int(other[i:i + 2], 16) for i in (1, 3, 5)]
    return '#%02x%02x%02x' % tuple(round(x + (y - x) * amount) for x, y in zip(a, b))


def initials(name):
    words = re.findall(r'[A-Za-z0-9]+', re.sub(r'\(.*', '', name))
    return ''.join(w[0] for w in words[:3]).upper() or '?'


def make_icon(component_type, name, category, glyphs):
    """The SVG text of the icon of a component."""
    color = category_color(category)
    tile, border, ink = mix(color, '#ffffff', 0.62), mix(color, '#000000', 0.12), mix(color, '#000000', 0.6)
    path = glyphs.get(GLYPHS.get(component_type, ''))
    if path:
        body = ('<g transform="translate(14 14) scale(1.5)" fill="none" stroke="%s" stroke-width="1.7" '
                'stroke-linecap="round" stroke-linejoin="round"><path d="%s"/></g>' % (ink, path))
    else:
        body = ('<text x="32" y="40" text-anchor="middle" font-family="Helvetica,Arial,sans-serif" font-size="24" '
                'font-weight="700" fill="%s">%s</text>' % (ink, initials(name)))
    return ('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">'
            '<rect x="3" y="3" width="58" height="58" rx="14" fill="%s" stroke="%s" stroke-width="2"/>%s</svg>\n'
            % (tile, border, body))
