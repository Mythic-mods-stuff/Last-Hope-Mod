from pathlib import Path
import hashlib
import json

project = Path(__file__).resolve().parent.parent
original = Path.home() / 'Projects/TheLastHope/src/LastHope'
variant = project / 'src/LastHopeAlt'
results = []
for path in sorted(original.rglob('*')):
    if not path.is_file() or any(p in ('bin', 'obj') for p in path.relative_to(original).parts):
        continue
    relative = path.relative_to(original)
    alternate = variant / ('LastHopeAlt.csproj' if str(relative) == 'LastHope.csproj' else relative)
    before, after = path.read_bytes(), alternate.read_bytes()
    status = 'copied unchanged' if before == after else 'changed for alternate behavior or identity'
    if path.suffix == '.cs' and before != after:
        normalized = after.decode().replace('using LastHopeAlt.', 'using LastHope.').replace('namespace LastHopeAlt;', 'namespace LastHope;').replace('namespace LastHopeAlt.', 'namespace LastHope.')
        if normalized == before.decode():
            status = 'logic copied unchanged; namespace renamed for independent mod identity'
    results.append(dict(File=str(relative), Status=status,
                        OriginalSHA256=hashlib.sha256(before).hexdigest(), AltSHA256=hashlib.sha256(after).hexdigest()))
added = [str(p.relative_to(variant)) for p in sorted(variant.rglob('*')) if p.is_file()
         and not any(part in ('bin','obj') for part in p.relative_to(variant).parts)
         and not (original/p.relative_to(variant)).exists() and p.name != 'LastHopeAlt.csproj']
report = dict(Original=str(original), Alternate=str(variant), Files=results, Added=added)
(project/'verification').mkdir(exist_ok=True)
(project/'verification/source-comparison.json').write_text(json.dumps(report,indent=2)+'\n')
print('Source comparison saved; original project is unchanged.')
