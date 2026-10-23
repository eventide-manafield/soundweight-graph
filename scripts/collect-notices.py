"""Collect exact locally restored package notices without network or project changes.

Usage: python scripts/collect-notices.py --project <project> --output <document-root>
Requires a Release win-x64 build and previously fetched docs/licenses/upstream files.
Does not certify redistribution compliance or publish anything.
"""
import argparse
import hashlib
import json
import re
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[1])
p.add_argument('--output', type=Path)
a = p.parse_args()
project = a.project.resolve()
out = (a.output or project).resolve()
docs = out / 'docs'
licenses = docs / 'licenses'
licenses.mkdir(parents=True, exist_ok=True)
cache = Path.home() / '.nuget/packages'

def load(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

deps_path = project / 'src/SoundWeightGraph.App/bin/Release/net10.0-windows10.0.19041.0/win-x64/SoundWeightGraph.deps.json'
deps = load(deps_path)
target = deps['targets'][deps['runtimeTarget']['name']]
lock_path = project / 'src/SoundWeightGraph.App/packages.lock.json'
lock = load(lock_path)['dependencies']['net10.0-windows10.0.19041']
packages = {k: {'version': v['resolved'], 'lockContentHash': v.get('contentHash')}
            for k, v in lock.items() if v['type'] != 'Project'}
for key in target:
    name, version = key.rsplit('/', 1)
    if name.startswith('runtimepack.'):
        packages[name.removeprefix('runtimepack.')] = {'version': version}

provenance = []
upstream = load(licenses / 'upstream-provenance.json')
for record in upstream:
    path = licenses / record['File']
    if sha(path) != record['SHA256']:
        raise ValueError(f'Upstream license hash mismatch: {path.name}')
    provenance.append({'file': path.relative_to(docs).as_posix(), 'source': record['Url'], 'sha256': sha(path), 'transformation': record.get('Transformation')})

inventory = []
for name, meta in sorted(packages.items(), key=lambda x: x[0].lower()):
    version = meta['version']
    folder = cache / name.lower() / version
    nuspec = folder / (name.lower() + '.nuspec')
    xml = ET.parse(nuspec).getroot()
    # NuGet package manifests use different XML namespace versions.
    fields = next(e for e in xml if e.tag.split('}')[-1] == 'metadata')
    fields = {e.tag.split('}')[-1]: e for e in fields}
    license_node = fields.get('license')
    license_text = license_node.text if license_node is not None else None
    license_type = license_node.attrib.get('type') if license_node is not None else None
    source_nodes = target.get(name + '/' + version, target.get('runtimepack.' + name + '/' + version, {}))
    assets = sorted(list(source_nodes.get('runtime', {})) + list(source_nodes.get('native', {})))
    row = dict(package=name, version=version, license=license_text, licenseType=license_type,
               shippedAssets=assets, scope='runtime' if assets else 'restore-only', notices=[], **{k:v for k,v in meta.items() if k != 'version'})
    row['repository'] = fields['repository'].attrib if 'repository' in fields else None
    row['licenseUrl'] = fields['licenseUrl'].text if 'licenseUrl' in fields else None
    local_files = [f for f in folder.iterdir() if f.is_file() and re.search(r'license|copying|third.party.notices', f.name, re.I)]
    for source in sorted(local_files):
        destination = licenses / 'packages' / (name + '-' + version) / source.name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        relative = destination.relative_to(docs).as_posix()
        row['notices'].append(relative)
        provenance.append(dict(file=relative, source=f'nuget:{name}/{version}/{source.name}', sha256=sha(destination)))
    if name.startswith('ScottPlot'):
        row['notices'].append('licenses/upstream/ScottPlot-5.1.59-LICENSE.txt')
    elif name == 'OpenTK.GLWpfControl':
        row['license'] = 'MIT (official version tag)'
        row['notices'].append('licenses/upstream/OpenTK.GLWpfControl-4.3.3-LICENSE.md')
    elif name.startswith('OpenTK') and name != 'OpenTK.redist.glfw':
        row['notices'] += ['licenses/upstream/OpenTK-4.9.4-LICENSE.md', 'licenses/upstream/OpenTK-4.9.4-THIRD_PARTIES.md']
    elif name == 'Microsoft.Windows.SDK.NET.Ref':
        row['license'] = 'Microsoft Windows SDK terms (not MIT)'
        row['notices'] += ['licenses/upstream/WindowsSDK-license.rtf']
        if (licenses / 'upstream/WindowsSDK-license.txt').exists():
            row['notices'] += ['licenses/upstream/WindowsSDK-license.txt']
    row['noticeStatus'] = 'collected' if row['notices'] else 'missing'
    inventory.append(row)

manifest = load(project / 'third_party/tools/manifest.json')
ffmpeg_build = project / 'third_party/tools/build-info.txt'
shutil.copyfile(ffmpeg_build, licenses / 'FFmpeg-build-info.txt')
configured = sorted(set(re.findall(r'--enable-([A-Za-z0-9_-]+)', ffmpeg_build.read_text(encoding='utf-8-sig'))))
result = dict(schemaVersion=1, inspectedDate='2026-10-23',
              depsSha256=sha(deps_path), lockSha256=sha(lock_path),
              packages=inventory,
              ffmpeg=dict(version=manifest['Version'], executableHashes=manifest['Hashes'],
                          sourceCommit='5c4f5744cd60943125a78db692b728210102f255',
                          license='LGPL-3.0-or-later', enabledConfigureOptions=configured,
                          correspondingSourceBundleComplete=False),
              publicRedistributionCleared=False)
(docs / 'dependency-inventory.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
(licenses / 'provenance.json').write_text(json.dumps(provenance, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')

lines = ['# SoundWeight Graph — Third-party notices', '',
         'Prepared 2026-10-23 for SoundWeight Graph v0.1.0.', '',
         '**Public binary redistribution is not yet cleared.** Exact FFmpeg and linked dependency corresponding sources remain incomplete.', '',
         'Project-owned code: Apache-2.0. Copyright 2026 eventide-manafield. Original project: https://github.com/eventide-manafield/soundweight-graph', '',
         'Third-party components retain their own licenses. The project Apache license does not replace their terms.', '',
         '## Project LICENSE', '', (out / 'LICENSE').read_text(encoding='utf-8-sig').rstrip(), '',
         '## Project NOTICE', '', (out / 'NOTICE').read_text(encoding='utf-8-sig').rstrip(), '',
         '## Included packages and runtime components', '',
         '| Component | Version | Terms | Notice |', '|---|---|---|---|']
for row in inventory:
    if row['scope'] != 'runtime':
        continue
    notice = row['notices'][0] if row['notices'] else ''
    lines.append(f"| {row['package']} | {row['version']} | {row['license'] or 'See notice'} | [{Path(notice).name}]({notice}) |")
lines += ['', 'Restore-only dependencies, asset lists, package hashes and exact versions are recorded in [dependency-inventory.json](dependency-inventory.json).', '',
          '## FFmpeg / ffprobe', '',
          'FFmpeg copyright (c) 2000-2026 the FFmpeg developers.',
          'Version: n8.1.3-23-g5c4f5744cd-20261022, BtbN Windows x64 static LGPL build.',
          'License: LGPL-3.0-or-later, including the incorporated GPL version 3 terms.',
          'No --enable-gpl or --enable-nonfree option is present in the recorded configure command.',
          'The app runs unmodified executables as separate child processes using standard pipes.',
          'This does not remove the redistribution obligations for the embedded executables themselves.', '',
          'FFmpeg source commit: https://github.com/FFmpeg/FFmpeg/commit/5c4f5744cd60943125a78db692b728210102f255',
          'Build recipe snapshot: https://github.com/BtbN/FFmpeg-Builds/tree/937868360a21d711ec94a33f5591d345ad9b7754',
          'The recipe snapshot and a commit link alone are not a complete corresponding-source bundle.',
          'Original archive SHA-256: D33EAD5ED152CE6E2BEB6D8B0B209984ACFD9E9513E2A55B6855E6AB0956B5EF', '',
          'See [LICENSING.md](LICENSING.md) for outstanding release work. No source offer or download link is promised until it actually exists.', '',
          '## Full license and notice texts', '',
          'The following upstream texts are preserved in their original language. Package-level notices can cover third-party subcomponents with different terms.', '']
files = ['licenses/upstream/FFmpeg-COPYING.LGPLv3.txt', 'licenses/upstream/FFmpeg-COPYING.GPLv3.txt']
for row in inventory:
    if row['scope'] == 'runtime':
        files += row['notices']
files += ['licenses/FFmpeg-build-info.txt']
seen = set()
for rel in dict.fromkeys(files):
    path = docs / rel
    if path.suffix.lower() == '.rtf':
        continue
    digest = sha(path)
    if digest in seen:
        continue
    seen.add(digest)
    lines += ['---', '', '### ' + rel, '', path.read_text(encoding='utf-8-sig').rstrip(), '']
(docs / 'THIRD_PARTY_NOTICES.md').write_text('\n'.join(lines), encoding='utf-8')
missing = [row['package'] for row in inventory if row['scope']=='runtime' and row['noticeStatus']=='missing']
print(json.dumps(dict(packages=len(inventory), runtimePackages=sum(r['scope']=='runtime' for r in inventory),
                     licenseFiles=len(provenance), missingRuntimeNotices=missing), ensure_ascii=False))
if missing:
    raise SystemExit(1)
