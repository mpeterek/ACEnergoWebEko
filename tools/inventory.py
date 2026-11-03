"""Inventory Classic ASP without reading or exporting database contents."""
import collections, json, pathlib, re, sys
root = pathlib.Path(sys.argv[1]).resolve()
rows=[]
for p in sorted(root.rglob('*')):
    if not p.is_file() or '_vti_cnf' in p.parts or p.suffix.lower() != '.asp': continue
    text=p.read_text(encoding='cp1250', errors='replace')
    rows.append(dict(path=p.relative_to(root).as_posix(), lines=len(text.splitlines()),
      includes=re.findall(r'<!--\s*#include\s+(?:file|virtual)\s*=\s*"([^"]+)"', text, re.I),
      objects=sorted(set(re.findall(r'CreateObject\s*\(\s*"([^"]+)"', text, re.I))),
      tables=sorted(set(re.findall(r'\b(?:FROM|JOIN|UPDATE|INTO)\s+\[?(tbl\w+)', text, re.I))),
      monthly_database=bool(re.search(r'ConnectDBase\s*\(', text, re.I)), status='pending'))
output=pathlib.Path(sys.argv[2]); output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps(dict(count=len(rows), modules=dict(collections.Counter(r['path'].split('/')[0] for r in rows)),pages=rows),ensure_ascii=False,indent=2),encoding='utf-8')
print(f'{len(rows)} ASP pages inventoried: {output}')
