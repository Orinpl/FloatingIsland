# -*- coding: utf-8 -*-
"""最小 FBX 7400 二进制解析：抽 Vertices / PolygonVertexIndex 数组长度，得到真实顶点数与面数。
仅用于产出后的质检（确认模型非空、各不相同、面数在 low poly 区间）。"""
import glob
import os
import struct
import sys
import zlib


def read_record(buf, pos, version):
    # 记录头：EndOffset / NumProperties / PropertyListLen / NameLen
    if version >= 7500:
        end, nprop, plen = struct.unpack_from('<QQQ', buf, pos)
        pos += 24
    else:
        end, nprop, plen = struct.unpack_from('<III', buf, pos)
        pos += 12
    nlen = buf[pos]
    pos += 1
    if end == 0:
        return None, None, None, None
    name = buf[pos:pos + nlen]
    pos += nlen
    return name, pos, pos + plen, end


def array_len(buf, pos):
    """读一个数组属性，返回元素个数（不解全部数据，只要长度）。"""
    n, encoding, clen = struct.unpack_from('<III', buf, pos)
    return n, pos + 12 + clen


def scan(buf, pos, limit, version, out):
    while pos < limit - 13:
        name, ppos, pend, end = read_record(buf, pos, version)
        if name is None:
            break
        if name in (b'Vertices', b'PolygonVertexIndex') and ppos < pend:
            t = buf[ppos]
            if t in (ord('d'), ord('i'), ord('f'), ord('l')):
                n, _ = array_len(buf, ppos + 1)
                out.setdefault(name.decode(), []).append(n)
        if pend < end:  # 有嵌套子节点
            scan(buf, pend, end, version, out)
        pos = end
    return out


def probe(path):
    buf = open(path, 'rb').read()
    if not buf.startswith(b'Kaydara FBX Binary'):
        return None
    version = struct.unpack_from('<I', buf, 23)[0]
    out = scan(buf, 27, len(buf), version, {})
    verts = sum(out.get('Vertices', [])) // 3
    idx = sum(out.get('PolygonVertexIndex', []))
    return version, verts, idx, len(out.get('Vertices', []))


print('%-16s %8s %10s %10s %6s' % ('asset', 'ver', 'vertices', 'polyIdx', 'meshes'))
rows = []
for f in sorted(glob.glob(os.path.join(sys.argv[1] if len(sys.argv) > 1 else '.', 'Assets/Res/*/fbx/*.fbx'))):
    aid = os.path.basename(os.path.dirname(os.path.dirname(f)))
    r = probe(f)
    if r is None:
        print('%-16s  NOT A BINARY FBX' % aid)
        continue
    version, verts, idx, meshes = r
    rows.append((aid, verts, idx))
    print('%-16s %8d %10d %10d %6d' % (aid, version, verts, idx, meshes))

if rows:
    uniq = len(set(v for _, v, _ in rows))
    print('\n%d 个模型，顶点数去重后 %d 种（相同=可能重复产出）' % (len(rows), uniq))
    print('顶点数区间: %d ~ %d' % (min(v for _, v, _ in rows), max(v for _, v, _ in rows)))
