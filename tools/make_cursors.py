import struct, sys
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
im = Image.open(src).convert('RGBA'); w, h = im.size
px = im.load()
xor = bytearray(); andm = bytearray()
for y in range(h-1, -1, -1):
    for x in range(w):
        r,g,b,a = px[x,y]; xor += bytes((b,g,r,a))
rowb = ((w+31)//32)*4
for y in range(h-1, -1, -1):
    row = bytearray(rowb)
    for x in range(w):
        if px[x,y][3] < 8: row[x//8] |= 0x80 >> (x%8)
    andm += row
hdr = struct.pack('<IiiHHIIiiII', 40, w, h*2, 1, 32, 0, len(xor)+len(andm), 0,0,0,0)
img = hdr + xor + andm
cur = struct.pack('<HHH',0,2,1) + struct.pack('<BBBBHHII', w, h, 0, 0, 1, 1, len(img), 22) + img
open(dst,'wb').write(cur)
