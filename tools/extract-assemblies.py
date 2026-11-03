from pathlib import Path
import io
import struct
import zlib

root = Path(__file__).resolve().parent.parent
stream = io.BytesIO((root/'artifacts/LastHopeAlt.tmod').read_bytes())
def string():
    size = shift = 0
    while True:
        b = stream.read(1)[0]
        size |= (b & 127) << shift
        if b < 128: return stream.read(size).decode()
        shift += 7
assert stream.read(4) == b'TMOD'
string(); stream.read(280); string(); string()
count = struct.unpack('<i', stream.read(4))[0]
table = [(string(), *struct.unpack('<ii', stream.read(8))) for _ in range(count)]
target = root/'artifacts/assemblies'
target.mkdir(exist_ok=True)
for name,size,compressed in table:
    payload = stream.read(compressed)
    if name.endswith(('.dll','.pdb')):
        (target/Path(name).name).write_bytes(zlib.decompress(payload,-15) if size != compressed else payload)
print('Extracted the DLL and symbols from the packaged release.')
