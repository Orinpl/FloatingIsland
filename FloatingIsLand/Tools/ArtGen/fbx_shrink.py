# -*- coding: utf-8 -*-
"""把 FBX 内嵌贴图降分辨率并重写文件（保持几何完全不变）。

FBX 7400 二进制是「记录树 + 绝对偏移」结构：每条记录头里的 EndOffset 是文件绝对位置，
所以改动任何内容都必须整棵树重新序列化、偏移重算 —— 因此这里做完整 parse → mutate → write。

用法：
    python3 fbx_shrink.py <in.fbx> <out.fbx> [最大边长=512] [jpeg质量=85]
校验：脚本自身在写完后重新解析产物，比对顶点数/面数，不一致直接报错退出。
"""
import io
import os
import struct
import sys
import zlib

from PIL import Image

# ---------------- 解析 ----------------

ARRAY_TYPES = {'f': ('f', 4), 'd': ('d', 8), 'l': ('q', 8), 'i': ('i', 4), 'b': ('b', 1)}
SCALAR_TYPES = {'Y': ('h', 2), 'C': ('?', 1), 'I': ('i', 4), 'F': ('f', 4), 'D': ('d', 8), 'L': ('q', 8)}


class Node(object):
    __slots__ = ('name', 'props', 'children')

    def __init__(self, name, props, children):
        self.name = name
        self.props = props          # [(type_char, value)]，数组值保持解码后的 bytes/py 值
        self.children = children


def read_props(buf, pos, nprop):
    props = []
    for _ in range(nprop):
        t = chr(buf[pos]); pos += 1
        if t in SCALAR_TYPES:
            fmt, size = SCALAR_TYPES[t]
            v = struct.unpack_from('<' + fmt, buf, pos)[0]
            pos += size
            props.append((t, v))
        elif t in ARRAY_TYPES:
            n, enc, clen = struct.unpack_from('<III', buf, pos)
            pos += 12
            raw = buf[pos:pos + clen]
            pos += clen
            # 原样保留压缩状态与数据，避免重压缩带来的字节差异
            props.append((t, (n, enc, raw)))
        elif t in ('S', 'R'):
            ln = struct.unpack_from('<I', buf, pos)[0]
            pos += 4
            v = buf[pos:pos + ln]
            pos += ln
            props.append((t, v))
        else:
            raise ValueError('未知属性类型 %r @%d' % (t, pos))
    return props, pos


def read_node(buf, pos, version):
    if version >= 7500:
        end, nprop, plen = struct.unpack_from('<QQQ', buf, pos); pos += 24
        null_len = 25
    else:
        end, nprop, plen = struct.unpack_from('<III', buf, pos); pos += 12
        null_len = 13
    nlen = buf[pos]; pos += 1
    if end == 0:                      # 空记录 = 同级列表结束
        return None, pos
    name = buf[pos:pos + nlen]; pos += nlen
    props, pos = read_props(buf, pos, nprop)
    children = []
    if pos < end:                     # 还有剩余 = 存在子节点列表（以空记录收尾）
        while pos < end - (null_len - 1):
            child, pos = read_node(buf, pos, version)
            if child is None:
                break
            children.append(child)
        pos = end
    return Node(name, props, children), pos


def parse(path):
    buf = open(path, 'rb').read()
    if not buf.startswith(b'Kaydara FBX Binary'):
        raise ValueError('不是二进制 FBX：%s' % path)
    version = struct.unpack_from('<I', buf, 23)[0]
    pos = 27
    roots = []
    while True:
        node, pos = read_node(buf, pos, version)
        if node is None:
            break
        roots.append(node)
    footer = buf[pos:]                # 结尾块原样保留
    return version, roots, footer


# ---------------- 序列化 ----------------

def write_props(props):
    out = bytearray()
    for t, v in props:
        out.append(ord(t))
        if t in SCALAR_TYPES:
            out += struct.pack('<' + SCALAR_TYPES[t][0], v)
        elif t in ARRAY_TYPES:
            n, enc, raw = v
            out += struct.pack('<III', n, enc, len(raw)) + raw
        else:                          # S / R
            out += struct.pack('<I', len(v)) + v
    return bytes(out)


def write_node(node, offset, version):
    """返回该节点序列化后的 bytes；offset 是它在文件中的起始绝对位置。"""
    head_len = 25 if version >= 7500 else 13
    null_rec = b'\x00' * head_len
    props_bin = write_props(node.props)
    body_start = offset + head_len + len(node.name) + len(props_bin)

    children_bin = bytearray()
    cur = body_start
    for child in node.children:
        chunk = write_node(child, cur, version)
        children_bin += chunk
        cur += len(chunk)
    if node.children:
        children_bin += null_rec
        cur += head_len

    end = cur
    if version >= 7500:
        head = struct.pack('<QQQ', end, len(node.props), len(props_bin))
    else:
        head = struct.pack('<III', end, len(node.props), len(props_bin))
    head += bytes([len(node.name)]) + node.name
    return head + props_bin + bytes(children_bin)


def serialize(version, roots, footer):
    out = bytearray(b'Kaydara FBX Binary  \x00\x1a\x00' + struct.pack('<I', version))
    head_len = 25 if version >= 7500 else 13
    for node in roots:
        out += write_node(node, len(out), version)
    out += b'\x00' * head_len
    out += footer
    return bytes(out)


# ---------------- 贴图压缩 ----------------

def shrink_image(blob, max_side, quality):
    """返回压缩后的图片字节；失败或已足够小则原样返回。

    必须保留原始编码格式：FBX 里贴图另有 Filename/RelativeFilename 属性带扩展名，
    内容格式与扩展名不一致时 Unity 提取内嵌贴图会失败。
    """
    try:
        img = Image.open(io.BytesIO(blob))
        fmt = (img.format or '').upper()
        img.load()
    except Exception:
        return blob, None
    if fmt not in ('PNG', 'JPEG'):
        return blob, None
    w, h = img.size
    scale = min(1.0, float(max_side) / max(w, h))
    if scale >= 1.0 and len(blob) < 200 * 1024:
        return blob, (w, h, w, h)
    nw, nh = max(1, int(w * scale)), max(1, int(h * scale))
    if (nw, nh) != (w, h):
        img = img.resize((nw, nh), Image.LANCZOS)
    buf = io.BytesIO()
    if fmt == 'PNG':
        mode = 'RGBA' if img.mode in ('RGBA', 'LA', 'P') else 'RGB'
        img.convert(mode).save(buf, format='PNG', optimize=True)
    else:
        img.convert('RGB').save(buf, format='JPEG', quality=quality, optimize=True)
    new = buf.getvalue()
    if len(new) >= len(blob):
        return blob, (w, h, w, h)
    return new, (w, h, nw, nh)


def walk(node, fn):
    fn(node)
    for c in node.children:
        walk(c, fn)


def shrink_textures(roots, max_side, quality, log):
    stats = {'count': 0, 'before': 0, 'after': 0}

    def visit(node):
        if node.name != b'Content':
            return
        for i, (t, v) in enumerate(node.props):
            if t != 'R' or len(v) < 1024:
                continue
            new, info = shrink_image(v, max_side, quality)
            stats['count'] += 1
            stats['before'] += len(v)
            stats['after'] += len(new)
            if info and (info[0], info[1]) != (info[2], info[3]):
                log.append('    %dx%d -> %dx%d  %dKB -> %dKB' %
                           (info[0], info[1], info[2], info[3], len(v) // 1024, len(new) // 1024))
            node.props[i] = ('R', new)

    for r in roots:
        walk(r, visit)
    return stats


# ---------------- 几何校验 ----------------

def geometry_signature(roots):
    sig = []

    def visit(node):
        if node.name in (b'Vertices', b'PolygonVertexIndex'):
            for t, v in node.props:
                if t in ARRAY_TYPES:
                    sig.append((node.name, v[0]))

    for r in roots:
        walk(r, visit)
    return sorted(sig)


def main():
    src, dst = sys.argv[1], sys.argv[2]
    max_side = int(sys.argv[3]) if len(sys.argv) > 3 else 512
    quality = int(sys.argv[4]) if len(sys.argv) > 4 else 85

    version, roots, footer = parse(src)
    before_sig = geometry_signature(roots)

    log = []
    st = shrink_textures(roots, max_side, quality, log)
    data = serialize(version, roots, footer)

    tmp = dst + '.tmp'
    open(tmp, 'wb').write(data)
    v2, roots2, _ = parse(tmp)          # 回读校验
    if geometry_signature(roots2) != before_sig:
        os.remove(tmp)
        raise SystemExit('[FAIL] %s 几何在重写后发生变化，已丢弃产物' % src)
    os.replace(tmp, dst)

    print('%-16s %4dMB -> %3dMB   贴图 %d 张 %dMB -> %dMB' % (
        os.path.basename(src), os.path.getsize(src) // 1048576, len(data) // 1048576,
        st['count'], st['before'] // 1048576, st['after'] // 1048576))
    for line in log[:4]:
        print(line)


if __name__ == '__main__':
    main()
