#!/usr/bin/env python3
"""Rebuild the OFL WebGL font without copying proprietary macOS fonts.

Requires fontTools. Inputs are the unmodified Noto fonts supplied with Android
layoutlib (or matching upstream copies); only their required glyphs are retained.
"""
import argparse
import hashlib
import json
from pathlib import Path
import unicodedata

import fontTools
from fontTools import subset
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cjk', required=True, type=Path)
    parser.add_argument('--symbols', required=True, type=Path)
    parser.add_argument('--font-number', type=int, default=2,
                        help='SC Regular is index 2 in the supplied Noto CJK TTC')
    args = parser.parse_args()
    destination = Path(__file__).resolve().parent
    project = destination.parents[2]
    inputs = sorted((project / 'Assets/Scripts').glob('*.cs'))
    confirmed = project / 'Docs/CONFIRMED_SOURCES.md'
    if confirmed.is_file():
        inputs.append(confirmed)
    if not inputs:
        raise SystemExit('No runtime source files found; refusing an empty font.')
    text = ''.join(path.read_text(encoding='utf-8') for path in inputs)
    wanted = {ord(c) for c in text if not unicodedata.category(c).startswith('C')}
    wanted.update(range(0x20, 0x7F))  # All printable ASCII, including dynamic numbers.
    # These are acceptance-critical even if a future source refactor moves them.
    wanted.update(map(ord, '間久部马克贝斯源水设置名字雷帝朋友空出来的位置少年王无法计算世界的真相之外'))

    cjk = TTFont(args.cjk, fontNumber=args.font_number, recalcTimestamp=False)
    symbols = TTFont(args.symbols, recalcTimestamp=False)
    cjk_map, symbol_map = cjk.getBestCmap(), symbols.getBestCmap()
    missing = wanted - set(cjk_map) - set(symbol_map)
    if missing:
        raise SystemExit('Missing source glyphs: ' + ', '.join(
            f'U+{cp:04X} {chr(cp)}' for cp in sorted(missing)))
    for font in (cjk, symbols):
        licenses = [entry.toUnicode() for entry in font['name'].names if entry.nameID == 13]
        if not any('SIL Open Font License' in value and '1.1' in value for value in licenses):
            raise SystemExit('The input font does not explicitly identify SIL OFL 1.1.')

    # Resolve any CFF subroutines before rebuilding portable TrueType outlines.
    options = subset.Options()
    options.layout_features = []
    options.name_IDs = ['*']
    for font, cmap in ((cjk, cjk_map), (symbols, symbol_map)):
        used = wanted.intersection(cmap)
        if font is symbols:
            used.difference_update(cjk_map)
        job = subset.Subsetter(options=options)
        job.populate(unicodes=sorted(used))
        job.subset(font)

    cjk_map, symbol_map = cjk.getBestCmap(), symbols.getBestCmap()
    glyph_sets = {id(cjk): cjk.getGlyphSet(), id(symbols): symbols.getGlyphSet()}
    units = cjk['head'].unitsPerEm
    order = ['.notdef'] + [f'uni{cp:04X}' for cp in sorted(wanted)]
    cmap = {cp: f'uni{cp:04X}' for cp in sorted(wanted)}
    glyphs, metrics = {}, {}
    symbol_codepoints = []

    def convert(name, source, source_name):
        ratio = units / source['head'].unitsPerEm
        pen = TTGlyphPen(None)
        curves = Cu2QuPen(pen, max_err=1.0, reverse_direction=('CFF ' in source))
        scaled = TransformPen(curves, (ratio, 0, 0, ratio, 0, 0))
        glyph_sets[id(source)][source_name].draw(scaled)
        glyphs[name] = pen.glyph()
        width, bearing = source['hmtx'].metrics[source_name]
        metrics[name] = (round(width * ratio), round(bearing * ratio))

    convert('.notdef', cjk, '.notdef')
    for cp in sorted(wanted):
        source = cjk if cp in cjk_map else symbols
        source_name = (cjk_map if source is cjk else symbol_map)[cp]
        if source is symbols:
            symbol_codepoints.append(f'U+{cp:04X}')
        convert(cmap[cp], source, source_name)

    copyright_notices = []
    for font in (cjk, symbols):
        for entry in font['name'].names:
            if entry.nameID == 0:
                notice = entry.toUnicode()
                if notice not in copyright_notices:
                    copyright_notices.append(notice)
    builder = FontBuilder(units, isTTF=True)
    builder.setupGlyphOrder(order)
    builder.setupCharacterMap(cmap)
    builder.setupGlyf(glyphs)
    builder.setupHorizontalMetrics(metrics)
    builder.setupHorizontalHeader(ascent=cjk['hhea'].ascent,
                                  descent=cjk['hhea'].descent,
                                  lineGap=cjk['hhea'].lineGap)
    builder.setupNameTable({
        'familyName': 'MakubeX UI SC', 'styleName': 'Regular',
        'fullName': 'MakubeX UI SC Regular', 'psName': 'MakubeXUISc-Regular',
        'uniqueFontIdentifier': 'MakubeXUISc-Regular-OFL-PhaseOne',
        'version': 'Version 1.000; Noto CJK SC 1.005 and Noto Symbols subset',
        'copyright': '\n'.join(copyright_notices),
        'licenseDescription': 'This Font Software is licensed under the SIL Open Font License, Version 1.1.',
        'licenseInfoURL': 'https://openfontlicense.org/open-font-license-official-text/',
    })
    original_os2 = cjk['OS/2']
    builder.setupOS2(sTypoAscender=original_os2.sTypoAscender,
                     sTypoDescender=original_os2.sTypoDescender,
                     sTypoLineGap=original_os2.sTypoLineGap,
                     usWinAscent=original_os2.usWinAscent,
                     usWinDescent=original_os2.usWinDescent,
                     usWeightClass=400, usWidthClass=5, fsType=0,
                     fsSelection=0x40,
                     sxHeight=getattr(original_os2, 'sxHeight', 0),
                     sCapHeight=getattr(original_os2, 'sCapHeight', 0))
    builder.setupPost(keepGlyphNames=True)
    builder.setupMaxp()
    builder.font['head'].created = cjk['head'].created
    builder.font['head'].modified = cjk['head'].modified
    builder.font.recalcTimestamp = False
    output = destination / 'Chinese.ttf'
    builder.save(output)

    # Reopen the binary: checking source coverage alone would miss assembly errors.
    reopened = TTFont(output)
    absent = wanted - set(reopened.getBestCmap())
    if absent:
        raise SystemExit('Generated font is missing glyphs: ' + repr(sorted(absent)))
    if reopened['maxp'].numGlyphs != len(order):
        raise SystemExit('Generated glyph count does not match the expected subset.')
    (destination / 'COPYRIGHT.txt').write_text(
        '\n'.join(copyright_notices) + '\n\nModified subset: MakubeX UI SC Regular.\n'
        'Subset, outline conversion and symbol combination remain under SIL OFL 1.1.\n'
        'See LICENSE.txt for the complete license.\n', encoding='utf-8')
    (destination / 'Characters.txt').write_text(
        ''.join(chr(cp) for cp in sorted(wanted)) + '\n', encoding='utf-8')
    report = {
        'font': output.name, 'bytes': output.stat().st_size,
        'sha256': sha256(output), 'fontTools': fontTools.__version__,
        'unicodeCount': len(wanted), 'glyphCount': reopened['maxp'].numGlyphs,
        'missingCodepoints': [], 'supplementalSymbols': symbol_codepoints,
        'outline': 'TrueType; CFF curves converted with maximum error 1/1000 em',
        'sourceFonts': [
            {'path': str(args.cjk), 'fontNumber': args.font_number,
             'bytes': args.cjk.stat().st_size, 'sha256': sha256(args.cjk)},
            {'path': str(args.symbols), 'bytes': args.symbols.stat().st_size,
             'sha256': sha256(args.symbols)},
        ],
        'sourceText': [
            {'path': str(path.relative_to(project)), 'sha256': sha256(path)} for path in inputs
        ],
    }
    (destination / 'Coverage.json').write_text(
        json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
