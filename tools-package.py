"""Build an identity-only, unsigned local-test MSIX without downloading an SDK.
The executable and assets stay at the registered ExternalLocation.
"""
from pathlib import Path
import base64
import hashlib
import zipfile
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
payload = {'AppxManifest.xml': (root / 'Package.appxmanifest').read_bytes()}
ns = 'http://schemas.microsoft.com/appx/2010/blockmap'
ET.register_namespace('', ns)
blockmap = ET.Element(f'{{{ns}}}BlockMap', HashMethod='http://www.w3.org/2001/04/xmlenc#sha256')
for name, data in payload.items():
    file = ET.SubElement(blockmap, f'{{{ns}}}File', Name=name.replace('/', '\\'), Size=str(len(data)), LfhSize=str(30 + len(name.encode('utf-8'))))
    for offset in range(0, len(data), 65536):
        ET.SubElement(file, f'{{{ns}}}Block', Hash=base64.b64encode(hashlib.sha256(data[offset:offset+65536]).digest()).decode())
types = b'<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/AppxManifest.xml" ContentType="application/vnd.ms-appx.manifest+xml"/><Override PartName="/AppxBlockMap.xml" ContentType="application/vnd.ms-appx.blockmap+xml"/></Types>'
out = root / 'dist' / 'OneTaskBar.identity.msix'
out.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(out, 'w', compression=zipfile.ZIP_STORED, allowZip64=False) as package:
    for name, data in payload.items():
        package.writestr(name, data)
    package.writestr('AppxBlockMap.xml', ET.tostring(blockmap, encoding='utf-8', xml_declaration=True))
    package.writestr('[Content_Types].xml', types)
print(out)
