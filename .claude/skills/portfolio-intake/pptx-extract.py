#!/usr/bin/env python3
"""발표 자료(.pptx)에서 글·그림·표·노트를 꺼내 /portfolio-intake 재료로 쓴다.

사용:  python -I pptx-extract.py <발표자료.pptx> <src/<폴더>/_intake> [--replace]

- 표준 라이브러리만(Python 3.8 이상). PowerPoint가 없어도 됩니다. .pptx · .ppsx · .potx · .pptm(매크로는 실행하지 않음)
- 쓰는 것: <_intake>/발표자료.md · <_intake>/pptx/(그림) · <_intake>/intake.json의 "pptx" 항목. 그 밖은 쓰지 않습니다.
  출력 폴더 이름이 `_intake`가 아니면 쓰지 않습니다(`src/<폴더>/` 바로 아래 .md는 빌드가 기술 문서로 읽기 때문).
- 기술 문서가 아닙니다. 글은 슬라이드 원문 그대로이고(이메일·전화번호만 가림) 해석·요약하지 않습니다.
  파일 이름·작성자(문서 속성)는 쓰지 않습니다(팀원 이름이 들어 있을 수 있음).
- 표준 출력: 요약 JSON(개수·경고). 슬라이드 글 원문은 싣지 않습니다(제목만).

종료 코드: 0 성공 · 1 읽지 못함(.ppt·암호·손상·PowerPoint 파일 아님) · 2 쓰지 않음(이미 있음 → --replace, 또는 _intake가 아닌 폴더)
"""
import argparse
import datetime
import hashlib
import json
import os
import posixpath
import re
import struct
import sys
import zipfile
from urllib.parse import unquote
import xml.etree.ElementTree as ET

SHAPES = ('sp', 'pic', 'graphicFrame', 'grpSp', 'cxnSp', 'contentPart')
SKIP_PH = ('dt', 'ftr', 'sldNum', 'hdr', 'sldImg')
WEB_OK = ('png', 'jpg', 'gif', 'svg', 'webp')
CHART_KO = {'barChart': '막대', 'bar3DChart': '막대', 'lineChart': '꺾은선', 'line3DChart': '꺾은선', 'pieChart': '원형', 'pie3DChart': '원형',
            'doughnutChart': '도넛', 'areaChart': '영역', 'area3DChart': '영역', 'scatterChart': '분산형', 'radarChart': '방사형',
            'bubbleChart': '거품형', 'stockChart': '주식형', 'surfaceChart': '표면형', 'surface3DChart': '표면형', 'ofPieChart': '원형'}
DEFAULT_TITLE = re.compile(r'^(powerpoint\s*(프레젠테이션|presentation)|프레젠테이션\s*\d*|presentation\s*\d*|제목 없는 프레젠테이션|untitled presentation)$', re.I)
# 마지막 칸이 글자 2자 이상일 때만 이메일로 봄 — com.unity.inputsystem@1.7.0 같은 "패키지@버전" 표기를 가리지 않게
EMAIL = re.compile(r'(?:mailto:)?[A-Za-z0-9._%+-]+@(?!\d+\.\d)(?:[A-Za-z0-9-]+\.)+[A-Za-z]{2,}(?![A-Za-z0-9-]|\.[A-Za-z0-9])')
PHONE = re.compile(r'(?<![\d.])(?:\+82[-. ]?0?|0)1[016789][-. ]?\d{3,4}[-. ]?\d{4}(?!\d)|(?<![\d.])0(?:2|[3-6]\d)[-. )]\d{3,4}[-. ]\d{4}(?!\d)')
CTRL = re.compile(r'[\x00-\x08\x0b\x0c\x0e-\x1f]')
PAGE_NO = re.compile(r'^[-–(\[]?\s*(?:p\.?\s*)?\d{1,3}\s*(?:/\s*\d{1,3})?\s*[-–)\]]?$', re.I)


class NotPptx(Exception):
    pass


# ── XML 도우미(이름공간은 무시하고 지역 이름으로 — Strict OOXML도 같이 읽힘) ──
def ln(tag):
    return tag.rsplit('}', 1)[-1] if isinstance(tag, str) else ''


def kid(el, *names):
    """el 바로 아래에서 names 중 처음 맞는 자식."""
    if el is None:
        return None
    for c in el:
        if ln(c.tag) in names:
            return c
    return None


def kids(el, name):
    return [] if el is None else [c for c in el if ln(c.tag) == name]


def path(el, *names):
    for n in names:
        el = kid(el, n)
        if el is None:
            return None
    return el


def desc(el, name):
    return [] if el is None else [e for e in el.iter() if ln(e.tag) == name]


def rattr(el, name):
    """이름공간이 붙은 속성(r:id·r:embed·r:link·r:dm)."""
    if el is None:
        return None
    for k, v in el.attrib.items():
        if k.startswith('{') and k.endswith('}' + name):
            return v
    return None


def clean(s):
    s = CTRL.sub(' ', (s or '').replace('\r', ' ').replace('\n', ' / ').replace('\x0b', ' / '))
    return re.sub(r'[ \t ]+', ' ', s).strip()


# ── 그림 형식·크기 ──
def sniff(b, name):
    if b[:8] == b'\x89PNG\r\n\x1a\n':
        return 'png'
    if b[:3] == b'\xff\xd8\xff':
        return 'jpg'
    if b[:6] in (b'GIF87a', b'GIF89a'):
        return 'gif'
    if b[:2] == b'BM':
        return 'bmp'
    if b[:4] in (b'II*\x00', b'MM\x00*'):
        return 'tif'
    if b[:4] == b'RIFF' and b[8:12] == b'WEBP':
        return 'webp'
    if len(b) > 44 and b[40:44] == b' EMF':
        return 'emf'
    if b[:4] == b'\xd7\xcd\xc6\x9a' or b[:6] in (b'\x01\x00\x09\x00\x00\x03', b'\x02\x00\x09\x00\x00\x03'):
        return 'wmf'
    if b[:2] == b'\x1f\x8b':
        return 'emz' if name.lower().endswith('.emz') else 'wmz' if name.lower().endswith('.wmz') else 'gz'
    if b'<svg' in b[:4096].lower():
        return 'svg'
    ext = posixpath.splitext(name)[1].lower().lstrip('.')
    return {'jpeg': 'jpg', 'tiff': 'tif'}.get(ext, ext or 'bin')


def dims(b, fmt):
    try:
        if fmt == 'png':
            return struct.unpack('>II', b[16:24])
        if fmt == 'gif':
            return struct.unpack('<HH', b[6:10])
        if fmt == 'bmp':
            w, h = struct.unpack('<ii', b[18:26])
            return (w, abs(h))
        if fmt == 'webp':
            c = b[12:16]
            if c == b'VP8X':
                return (int.from_bytes(b[24:27], 'little') + 1, int.from_bytes(b[27:30], 'little') + 1)
            if c == b'VP8 ':
                w, h = struct.unpack('<HH', b[26:30])
                return (w & 0x3FFF, h & 0x3FFF)
            if c == b'VP8L':
                v = int.from_bytes(b[21:25], 'little')
                return ((v & 0x3FFF) + 1, ((v >> 14) & 0x3FFF) + 1)
        if fmt == 'jpg':
            i = 2
            while i + 9 < len(b):
                if b[i] != 0xFF:
                    i += 1
                    continue
                m = b[i + 1]
                if m == 0xFF:
                    i += 1
                    continue
                if m in (0xD8, 0x01) or 0xD0 <= m <= 0xD7:
                    i += 2
                    continue
                if 0xC0 <= m <= 0xCF and m not in (0xC4, 0xC8, 0xCC):
                    h, w = struct.unpack('>HH', b[i + 5:i + 9])
                    return (w, h)
                i += 2 + struct.unpack('>H', b[i + 2:i + 4])[0]
    except (struct.error, IndexError):
        pass
    return None


def size_txt(n):
    return f'{n / 1048576:.1f}MB' if n >= 1048576 else f'{max(1, round(n / 1024))}KB'


# ── 패키지(zip) ──
class Pkg:
    def __init__(self, z):
        self.z = z
        self.names = set(z.namelist())
        self.lower = {n.lower(): n for n in self.names}
        self._xml, self._rels = {}, {}

    def find(self, p):
        if p in self.names:
            return p
        for q in (unquote(p), p.lower(), unquote(p).lower()):
            if q in self.names:
                return q
            if q.lower() in self.lower:
                return self.lower[q.lower()]
        return None

    def read(self, p):
        q = self.find(p)
        return self.z.read(q) if q else None

    def xml(self, p):
        if p not in self._xml:
            data = self.read(p)
            self._xml[p] = ET.fromstring(data) if data else None
        return self._xml[p]

    def size(self, p):
        q = self.find(p)
        return self.z.getinfo(q).file_size if q else 0

    def rels(self, part):
        if part not in self._rels:
            d, b = posixpath.split(part)
            out = {}
            root = self.xml(posixpath.join(d, '_rels', b + '.rels'))
            for r in ([] if root is None else root):
                if ln(r.tag) != 'Relationship':
                    continue
                tgt, ext = r.get('Target') or '', (r.get('TargetMode') or '') == 'External'
                if not ext:
                    tgt = tgt[1:] if tgt.startswith('/') else posixpath.normpath(posixpath.join(d, tgt))
                out[r.get('Id')] = {'type': (r.get('Type') or '').rsplit('/', 1)[-1], 'target': tgt, 'external': ext}
            self._rels[part] = out
        return self._rels[part]

    def rel_of(self, part, typ):
        for r in self.rels(part).values():
            if r['type'] == typ and not r['external']:
                return r['target']
        return None


# ── 읽기 ──
class Deck:
    def __init__(self, pkg):
        self.p = pkg
        self.images = {}          # sha1 → 그림 기록
        self.img_order = []
        self.links = []           # (글, 주소, 슬라이드)
        self.videos = []          # {'slide','kind','url'|'file','size'}
        self.masked = 0
        self.skipped = {}         # 옮기지 않은 것 → 개수
        self.warn = []
        self.ph_cache = {}

    def skip(self, what):
        self.skipped[what] = self.skipped.get(what, 0) + 1

    def m(self, s):
        """글 정리 + 이메일·전화번호 가리기."""
        s = clean(s)
        s, a = EMAIL.subn('(이메일 — 가림)', s)
        s, b = PHONE.subn('(전화번호 — 가림)', s)
        self.masked += a + b
        return s

    # 자리 표시자(제목·본문)의 위치: 슬라이드에 없으면 레이아웃 → 마스터에서
    @staticmethod
    def ph_of(el):
        nv = kid(el, 'nvSpPr', 'nvPicPr', 'nvGraphicFramePr', 'nvGrpSpPr')
        ph = path(nv, 'nvPr', 'ph')
        if ph is None:
            return None
        idx = ph.get('idx')
        return (ph.get('type') or 'obj', int(idx) if idx and idx.isdigit() else None)

    @staticmethod
    def off(el):
        x = kid(el, 'xfrm')
        if x is None:
            x = kid(kid(el, 'spPr', 'grpSpPr'), 'xfrm')
        o = kid(x, 'off')
        if o is None:
            return None
        try:
            return (int(o.get('y') or 0), int(o.get('x') or 0))
        except ValueError:
            return None

    def ph_positions(self, part):
        if part in self.ph_cache:
            return self.ph_cache[part]
        res = {'idx': {}, 'type': {}}
        root = self.p.xml(part) if part else None
        for el in (path(root, 'cSld', 'spTree').iter() if root is not None and path(root, 'cSld', 'spTree') is not None else []):
            if ln(el.tag) not in ('sp', 'pic', 'graphicFrame'):
                continue
            ph, pos = self.ph_of(el), self.off(el)
            if ph is None or pos is None:
                continue
            if ph[1] is not None:
                res['idx'].setdefault(ph[1], pos)
            res['type'].setdefault(ph[0], pos)
        self.ph_cache[part] = res
        return res

    def position(self, el, ph, S):
        pos = self.off(el)
        if pos is not None or ph is None:
            return pos if pos is not None else (S['h'] // 2, 0)
        t, i = ph
        lay = self.ph_positions(S['layout'])
        mas = self.ph_positions(S['master'])
        base = {'ctrTitle': 'title', 'subTitle': 'body', 'obj': 'body'}.get(t, t)
        for cand in ((lay['idx'].get(i) if i is not None else None), lay['type'].get(t), lay['type'].get(base), mas['type'].get(base)):
            if cand is not None:
                return cand
        return (0, 0) if base == 'title' else (S['h'] // 2, 0)

    # 링크
    def link_of(self, holder, part):
        h = kid(holder, 'hlinkClick')
        if h is None:
            return None
        if (h.get('action') or '').startswith('ppaction://') and 'hlinkfile' not in (h.get('action') or ''):
            return None                                               # 슬라이드 이동·쇼 동작
        r = self.p.rels(part).get(rattr(h, 'id'))
        return r['target'] if r and r['external'] else None

    def add_link(self, text, url, S):
        url = self.m(url)
        if url and not any(l[1] == url and l[0] == text and l[2] == S['n'] for l in self.links):
            self.links.append((text, url, S['n']))

    # 글 단락 → [(수준, 글)]
    def paras(self, tb, part, S):
        out = []
        for p in kids(tb, 'p'):
            ppr = kid(p, 'pPr')
            lvl = ppr.get('lvl') if ppr is not None else None
            lvl = int(lvl) if lvl and lvl.isdigit() else 0
            buf, link_txt = [], []
            for c in p:
                n = ln(c.tag)
                if n in ('r', 'fld'):
                    if n == 'fld' and (c.get('type') or '').startswith('slidenum'):
                        continue
                    t = ''.join(x.text or '' for x in c if ln(x.tag) == 't')
                    url = self.link_of(kid(c, 'rPr'), part)
                    buf.append(t)
                    if url and t.strip():
                        buf.append(' ↗')
                        link_txt.append((t, url))
                elif n == 'br':
                    buf.append(' / ')
                elif n == 'AlternateContent':                          # 대개 수식(a14:m)
                    buf.append(' (수식) ')
                    self.skip('수식')
            text = self.m(''.join(buf))
            for t, url in link_txt:
                self.add_link(self.m(t), url, S)
            if text and text != '↗':
                out.append((lvl, text))
        return out

    # 그림
    def image(self, blip_holder, S, kind):
        blip = desc(blip_holder, 'blip')
        if not blip:
            return None
        blip = blip[0]
        rels = self.p.rels(S['part'])
        rid = rattr(blip, 'embed')
        svg = [rattr(e, 'embed') for e in desc(blip, 'svgBlip')]
        if not rid and rattr(blip, 'link'):
            r = rels.get(rattr(blip, 'link'))
            self.skip('연결만 된 그림(파일이 들어 있지 않음)')
            return ('note', '연결만 된 그림 — 파일이 들어 있지 않음' + (f': {self.m(r["target"])}' if r and r['external'] else ''))
        recs = []
        for i, rr in enumerate([rid] + svg):
            r = rels.get(rr)
            if not r or r['external']:
                continue
            data = self.p.read(r['target'])
            if not data:
                self.warn.append(f'슬라이드 {S["n"]}: 그림 파일이 없습니다({posixpath.basename(r["target"])})')
                continue
            h = hashlib.sha1(data).hexdigest()
            rec = self.images.get(h)
            if rec is None:
                fmt = sniff(data, r['target'])
                if kind == 'bg':
                    stem = 'bg'
                else:
                    S['img_k'] += 1
                    stem = str(S['img_k'])
                rec = {'data': data, 'fmt': fmt, 'dims': dims(data, fmt), 'bytes': len(data), 'slides': [], 'kinds': set(),
                       'name': f'{S["tag"]}-{stem}.{fmt}', 'anim': fmt == 'gif' and data.count(b'\x21\xf9\x04') > 1, 'svg_pair': bool(i)}
                self.images[h] = rec
                self.img_order.append(h)
            if S['n'] not in rec['slides']:
                rec['slides'].append(S['n'])
            rec['kinds'].add(kind)
            src_rect = path(blip_holder, 'srcRect')
            crop = src_rect is not None and any((src_rect.get(k) or '0') not in ('0', '') for k in 'ltrb')
            if crop:
                rec['kinds'].add('crop')
            recs.append((rec, {'kind': kind, 'crop': crop, 'alt': ''}))     # 쓰인 자리마다(같은 그림을 여러 장에 써도 따로)
        return ('img', recs) if recs else None

    def media_of(self, nvpr, S):
        """영상·소리: a:videoFile·a:audioFile(r:link), p14:media(r:embed·r:link)."""
        found, rels = [], self.p.rels(S['part'])
        for e in nvpr.iter() if nvpr is not None else []:
            n = ln(e.tag)
            if n not in ('videoFile', 'audioFile', 'quickTimeFile', 'media', 'wavAudioFile', 'audioCd'):
                continue
            kind = 'audio' if 'audio' in n.lower() or 'wav' in n.lower() else 'video'
            for a in ('embed', 'link'):
                r = rels.get(rattr(e, a))
                if not r:
                    continue
                if r['type'] in ('audio', 'media') and kind == 'video' and 'audio' in r['target'].lower():
                    kind = 'audio'
                key = r['target']
                if any(f.get('key') == key for f in found):
                    continue
                found.append({'key': key, 'kind': kind, 'url': self.m(key)} if r['external'] else
                             {'key': key, 'kind': kind, 'file': posixpath.basename(key), 'size': self.p.size(key)})
        return found

    # 표
    def table(self, tbl, S):
        rows = []
        for tr in kids(tbl, 'tr'):
            row = []
            for tc in kids(tr, 'tc'):
                if tc.get('hMerge') in ('1', 'true') or tc.get('vMerge') in ('1', 'true'):
                    row.append('')
                    continue
                tb = kid(tc, 'txBody')
                row.append(' / '.join(t for _, t in self.paras(tb, S['part'], S)) if tb is not None else '')
            rows.append(row)
        return rows

    # 차트(값은 PowerPoint가 파일에 같이 넣어 둔 캐시에서)
    @staticmethod
    def pts(el):
        if el is None:
            return []
        vals = {}
        for pt in desc(el, 'pt'):
            i = pt.get('idx')
            v = kid(pt, 'v')
            if i is not None and i.isdigit() and v is not None and int(i) not in vals:
                vals[int(i)] = v.text or ''
        return [vals.get(i, '') for i in range(max(vals) + 1)] if vals else []

    def chart(self, cpart, S):
        root = self.p.xml(cpart)
        ch = kid(root, 'chart')
        if ch is None:
            return None
        title = self.m(' '.join(t.text or '' for t in desc(kid(ch, 'title'), 't')))
        types, series = [], []
        for g in kid(ch, 'plotArea') if kid(ch, 'plotArea') is not None else []:
            n = ln(g.tag)
            if not n.endswith('Chart'):
                continue
            types.append(CHART_KO.get(n, n))
            for s in kids(g, 'ser'):
                name = self.m(' '.join(v.text or '' for v in desc(kid(s, 'tx'), 'v')))
                cat = [self.m(x) for x in self.pts(kid(s, 'cat', 'xVal'))]
                val = [clean(x) for x in self.pts(kid(s, 'val', 'yVal'))]
                series.append((name, cat, val))
        return ('chart', {'types': sorted(set(types), key=types.index), 'title': title, 'series': series})

    # SmartArt(글은 diagrams/data*.xml에 나무 구조로)
    def smartart(self, dpart, S):
        root = self.p.xml(dpart)
        if root is None:
            return None
        texts, doc = {}, None
        for pt in desc(root, 'pt'):
            t, mid = pt.get('type') or 'node', pt.get('modelId')
            if t == 'doc':
                doc = mid
            if t not in ('node', 'doc', 'asst'):
                continue
            tb = kid(pt, 't')
            texts[mid] = ' / '.join(x for _, x in self.paras(tb, dpart, S)) if tb is not None else ''
        ch = {}
        for c in desc(root, 'cxn'):
            if (c.get('type') or 'parOf') == 'parOf':
                o = c.get('srcOrd') or '0'
                ch.setdefault(c.get('srcId'), []).append((int(o) if o.isdigit() else 0, c.get('destId')))
        lines, seen = [], set()

        def dfs(mid, depth):
            for _, d in sorted(ch.get(mid, [])):
                if d in seen or d not in texts:
                    continue
                seen.add(d)
                if texts[d]:
                    lines.append((depth, texts[d]))
                    dfs(d, depth + 1)
                else:
                    dfs(d, depth)
        if doc:
            dfs(doc, 0)
        if not lines:
            lines = [(0, t) for k, t in texts.items() if t and k != doc]
        return ('smartart', lines) if lines else None

    # 도형 하나 → 항목 {pos, title?, blocks}
    def item(self, el, S):
        n = ln(el.tag)
        if n == 'sp':
            ph = self.ph_of(el)
            if ph and ph[0] in SKIP_PH:
                return None
            blocks = []
            tb = kid(el, 'txBody')
            ps = self.paras(tb, S['part'], S) if tb is not None else []
            pos = self.position(el, ph, S)
            if ph and ph[0] in ('title', 'ctrTitle') and ps:
                return {'pos': pos, 'title': ' / '.join(t for _, t in ps)}
            if not ph and len(ps) == 1 and PAGE_NO.match(ps[0][1]) and pos[0] > S['h'] * 0.85:
                self.skip('쪽 번호로 보이는 글상자')                       # 손으로 넣은 "02" 같은 쪽 번호
                return None
            blocks += [('p', lvl, t) for lvl, t in ps]
            fill = path(el, 'spPr', 'blipFill')
            if fill is not None:
                im = self.image(fill, S, 'fill')
                if im:
                    blocks.append(im)
            url = self.link_of(path(el, 'nvSpPr', 'cNvPr'), S['part'])
            if url:
                self.add_link(blocks[0][2] if blocks and blocks[0][0] == 'p' else '(도형)', url, S)
            sz = [int(e.get('sz')) for e in tb.iter() if ln(e.tag) in ('rPr', 'endParaRPr') and (e.get('sz') or '').isdigit()] if tb is not None else []
            return {'pos': pos, 'blocks': blocks, 'sz': max(sz) if sz else 0} if blocks else None
        if n == 'pic':
            nv = kid(el, 'nvPicPr')
            cnv, nvpr = kid(nv, 'cNvPr'), kid(nv, 'nvPr')
            media = self.media_of(nvpr, S)
            pos = self.position(el, self.ph_of(el), S)
            if media:
                for md in media:
                    md['slide'] = S['n']
                    self.videos.append(md)
                return {'pos': pos, 'blocks': [('media', media)]}
            im = self.image(kid(el, 'blipFill'), S, 'pic')
            if not im:
                return None
            alt = self.m((cnv.get('descr') or '') if cnv is not None else '')
            if im[0] == 'img' and alt:
                im[1][0][1]['alt'] = alt
            url = self.link_of(cnv, S['part'])
            if url:
                self.add_link('(그림)', url, S)
            return {'pos': pos, 'blocks': [im]}
        if n == 'graphicFrame':
            gd = path(el, 'graphic', 'graphicData')
            uri = (gd.get('uri') or '') if gd is not None else ''
            pos = self.position(el, self.ph_of(el), S)
            rels = self.p.rels(S['part'])
            if desc(gd, 'tbl'):
                return {'pos': pos, 'blocks': [('table', self.table(desc(gd, 'tbl')[0], S))]}
            if uri.endswith('/chart'):
                c = desc(gd, 'chart')
                r = rels.get(rattr(c[0], 'id')) if c else None
                b = self.chart(r['target'], S) if r and not r['external'] else None
                return {'pos': pos, 'blocks': [b]} if b else None
            if uri.endswith('/diagram'):
                ri = desc(gd, 'relIds')
                r = rels.get(rattr(ri[0], 'dm')) if ri else None
                b = self.smartart(r['target'], S) if r and not r['external'] else None
                return {'pos': pos, 'blocks': [b]} if b else None
            if 'chartex' in uri:
                self.skip('새 차트 형식(폭포·트리맵 등) — 값')
                return {'pos': pos, 'blocks': [('note', '차트(새 형식) — 값은 옮기지 않음')]}
            if uri.endswith('/ole') or desc(gd, 'oleObj'):
                self.skip('다른 프로그램 개체(엑셀·워드 등 붙여 넣은 것)')
                return {'pos': pos, 'blocks': [('note', '다른 프로그램 개체 — 옮기지 않음')]}
            self.skip('그 밖의 개체')
            return None
        if n == 'grpSp':
            inner = self.walk(el, S)
            blocks = []
            for it in inner:
                if 'title' in it:
                    blocks.append(('p', 0, it['title']))
                blocks += it.get('blocks', [])
            pos = self.off(el)
            return {'pos': pos if pos is not None else (S['h'] // 2, 0), 'blocks': blocks} if blocks else None
        if n == 'contentPart':
            self.skip('잉크(펜으로 쓴 것)')
        return None

    def walk(self, tree, S):
        items = []
        for c in tree:
            n = ln(c.tag)
            if n == 'AlternateContent':
                choice, fb = kid(c, 'Choice'), kid(c, 'Fallback')
                src = choice if choice is not None and any(ln(x.tag) in SHAPES for x in choice) else fb
                if src is not None:
                    items += self.walk(src, S)
                continue
            if n not in SHAPES:
                continue
            try:
                it = self.item(c, S)
            except Exception as e:                                        # 개체 하나가 이상해도 나머지는 꺼냄
                self.warn.append(f'슬라이드 {S["n"]}: 개체 하나를 읽지 못했습니다({n}: {type(e).__name__})')
                it = None
            if it:
                items.append(it)
        # 읽는 순서 = 도형 순서(PowerPoint [선택 창]·화면 낭독기의 읽기 순서). 위치로 다시 늘어놓으면 2단 목록이 줄마다 섞임. 제목만 앞으로
        items.sort(key=lambda it: 0 if 'title' in it else 1)
        return items

    def notes(self, part, S):
        npart = self.p.rel_of(part, 'notesSlide')
        root = self.p.xml(npart) if npart else None
        tree = path(root, 'cSld', 'spTree')
        out = []
        for sp in (tree.iter() if tree is not None else []):
            if ln(sp.tag) != 'sp':
                continue
            ph = self.ph_of(sp)
            if ph and ph[0] == 'body' and kid(sp, 'txBody') is not None:
                out += self.paras(kid(sp, 'txBody'), npart, S)
        return out

    def slide(self, part, n, width):
        root = self.p.xml(part)
        if root is None:
            self.warn.append(f'슬라이드 {n}: 파일이 없습니다')
            return {'n': n, 'title': '', 'blocks': [], 'notes': [], 'hidden': False}
        layout = self.p.rel_of(part, 'slideLayout')
        S = {'part': part, 'n': n, 'tag': f's{n:0{width}d}', 'img_k': 0, 'h': self.h,
             'layout': layout, 'master': self.p.rel_of(layout, 'slideMaster') if layout else None}
        blocks, title = [], ''
        bg = path(root, 'cSld', 'bg', 'bgPr', 'blipFill')
        if bg is not None:
            im = self.image(bg, S, 'bg')
            if im:
                blocks.append(im)
        tree = path(root, 'cSld', 'spTree')
        items = self.walk(tree, S) if tree is not None else []
        guessed = False
        if not any('title' in it for it in items):
            # 제목 자리 표시자가 없는 슬라이드(글상자로 만든 학생 자료에 흔함): 위쪽 30% 안에서 가장 큰 한 줄 글(20pt 이상)
            cand = [it for it in items if it.get('sz', 0) >= 2000 and it['pos'][0] < self.h * 0.3 and len(it['blocks']) == 1
                    and it['blocks'][0][0] == 'p' and len(it['blocks'][0][2]) <= 60]
            if cand:
                best = max(cand, key=lambda it: (it['sz'], -it['pos'][0]))
                items.remove(best)
                items.insert(0, {'title': best['blocks'][0][2]})
                guessed = True
        for it in items:
            if 'title' in it:
                if title:
                    blocks.append(('p', 0, it['title']))
                else:
                    title = it['title']
            blocks += it.get('blocks', [])
        try:
            notes = self.notes(part, S)
        except Exception as e:
            self.warn.append(f'슬라이드 {n}: 발표자 노트를 읽지 못했습니다({type(e).__name__})')
            notes = []
        return {'n': n, 'title': title, 'guessed': guessed, 'blocks': blocks, 'notes': notes, 'hidden': root.get('show') in ('0', 'false')}

    def read(self):
        root_rels = self.p.rels('')
        pres = next((r['target'] for r in root_rels.values() if r['type'] == 'officeDocument'), 'ppt/presentation.xml')
        proot = self.p.xml(pres)
        if proot is None or ln(proot.tag) != 'presentation':
            raise NotPptx()
        sz = kid(proot, 'sldSz')
        self.h = int(sz.get('cy')) if sz is not None and (sz.get('cy') or '').isdigit() else 6858000
        prels = self.p.rels(pres)
        ids = [(s.get('id'), prels.get(rattr(s, 'id'))) for s in kids(kid(proot, 'sldIdLst'), 'sldId')]
        ids = [(i, r['target']) for i, r in ids if r and not r['external']]
        sec = {}
        for s in desc(proot, 'section'):
            for x in desc(s, 'sldId'):
                sec[x.get('id')] = clean(s.get('name') or '')
        width = max(2, len(str(len(ids))))
        slides = []
        for k, (sid, part) in enumerate(ids, 1):
            try:
                sl = self.slide(part, k, width)
            except Exception as e:
                self.warn.append(f'슬라이드 {k}: 읽다 멈췄습니다({type(e).__name__}) — 이 장은 비어 보입니다')
                sl = {'n': k, 'title': '', 'blocks': [], 'notes': [], 'hidden': False}
            sl['section'] = sec.get(sid, '')
            slides.append(sl)
        core = self.p.xml('docProps/core.xml')
        app = self.p.xml('docProps/app.xml')
        meta = {}
        for e in (core if core is not None else []):
            if ln(e.tag) in ('title', 'created', 'modified') and (e.text or '').strip():
                meta[ln(e.tag)] = clean(e.text)
        a = kid(app, 'Application')
        if a is not None and (a.text or '').strip():
            meta['app'] = clean(a.text)
        return slides, meta


# ── 쓰기(markdown) ──
def esc(s):
    return s.replace('\\', '\\\\').replace('<', '\\<')


def cell(s):
    return esc(s).replace('|', '\\|') or ' '


def md_table(rows, head=None):
    width = max([len(r) for r in rows] + [len(head or [])])
    rows = [r + [''] * (width - len(r)) for r in rows]
    if head is None:
        head, rows = rows[0], rows[1:]
    head = head + [''] * (width - len(head))
    out = ['| ' + ' | '.join(cell(c) for c in head) + ' |', '|' + '---|' * width]
    out += ['| ' + ' | '.join(cell(c) for c in r) + ' |' for r in rows]
    return out


def img_note(rec, use=None):
    """use가 있으면 그 자리(슬라이드 한 곳)의 표시, 없으면 그림 목록용(모든 자리를 합침)."""
    kinds = {use['kind']} | ({'crop'} if use['crop'] else set()) if use else rec['kinds']
    bits = []
    if rec['fmt'] not in WEB_OK:
        bits.append(f'웹에서 보이지 않는 형식({rec["fmt"].upper()}) — PNG로 바꿔야 씀')
    if rec['anim']:
        bits.append('움직이는 GIF')
    if rec['fmt'] == 'gif' and rec['bytes'] > 8 * 1048576:
        bits.append('8MB 넘음 — 줄여서')
    if rec['bytes'] > 100 * 1048576:
        bits.append('100MB 넘음 — GitHub에 못 올림')
    if 'bg' in kinds:
        bits.append('슬라이드 배경')
    if 'fill' in kinds:
        bits.append('도형 채우기')
    if 'crop' in kinds:
        bits.append('슬라이드에서는 잘라 보였음(원본을 꺼냄)')
    if rec['svg_pair']:
        bits.append('같은 그림의 SVG판')
    return ' · '.join(bits)


def render(slides, meta, deck, sha, today, has_repo_intake):
    imgs = [deck.images[h] for h in deck.img_order]
    first = next((s['title'] for s in slides if s['title']), '')
    t = meta.get('title', '')
    title = first or (t if t and not DEFAULT_TITLE.match(t) else '') or '발표 자료'
    hidden = sum(1 for s in slides if s['hidden'])
    cnt = {k: 0 for k in ('table', 'chart', 'smartart')}
    for s in slides:
        for b in s['blocks']:
            if b[0] in cnt:
                cnt[b[0]] += 1
    with_notes = sum(1 for s in slides if s['notes'])
    bad = [r for r in imgs if r['fmt'] not in WEB_OK]
    vid_file = [v for v in deck.videos if 'file' in v]
    vid_url = [v for v in deck.videos if 'url' in v]
    L = []
    w = L.append
    w(f'# {esc(title)} — 발표 자료 재료')
    w('')
    w('> ⚠ 이 파일은 **기술 문서가 아닙니다.** 발표 자료에서 글·그림·표·노트를 **그대로** 꺼내 놓은 것입니다.')
    w('> 발표 자료는 말로 채우는 자료라 그대로 옮기면 "왜"와 "어떻게"가 빠집니다. 맨 아래 질문에 답하면서 `../기술문서.md`를 **직접** 쓰세요.')
    w('> 이 폴더(`_intake/`)는 git에 올라가지 않습니다. 기술 문서를 다 쓰면 지워도 됩니다.')
    w('> 팀 발표 자료라면 팀원 이름과 팀원이 쓴 글·만든 그림이 섞여 있습니다. 기술 문서와 `images/`에는 **내가 한 일**만 옮기세요.')
    src = f'발표 자료 파일(SHA-256 {sha[:12]}) · 슬라이드 {len(slides)}장'
    if meta.get('modified'):
        src += f' · 파일 속성의 마지막 수정 {meta["modified"][:10]}'
    w(f'> 출처: {src} · 꺼낸 날 {today} · 자세한 점검: `report-intake.md`')
    if has_repo_intake:
        w('> 코드·커밋에서 뽑은 사실과 질문은 같은 폴더의 `재료집.md`에 있습니다.')
    w('')
    w('| 표시 | 뜻 |')
    w('|---|---|')
    w('| ✅ 원문 | 발표 자료에 있는 글 그대로. `### 번호`가 슬라이드 번호 |')
    w('| ❓ 질문 | 여러분만 답할 수 있음. 답이 기술 문서의 본문이 됩니다 |')
    w('| ⚠ 확인 | 가린 개인정보, 웹에서 보이지 않는 그림 형식처럼 직접 확인할 것 |')
    w('')
    w('## 한눈에')
    w('')
    w('| 항목 | 내용 |')
    w('|---|---|')
    w(f'| 슬라이드 | ✅ {len(slides)}장' + (f' (숨긴 슬라이드 {hidden}장 — 발표 때 보이지 않던 장)' if hidden else '') + ' |')
    guessed = [str(s['n']) for s in slides if s.get('guessed')]
    if guessed:
        which = '모든 장' if len(guessed) == len(slides) else f'{len(guessed)}장' + (f'({", ".join(guessed)})' if len(guessed) <= 10 else '')
        w(f'| 슬라이드 제목 | ⚠ 제목 칸 없이 글상자로 만든 {which}은 위쪽에서 가장 큰 글을 제목으로 옮겼습니다(본문에서는 뺌). 글의 순서는 PowerPoint의 읽기 순서(도형 순서)입니다 |')
    w(f'| 그림 | ✅ {len(imgs)}개 → `pptx/`' + (f' · ⚠ 웹에서 보이지 않는 형식 {len(bad)}개' if bad else '') + ' (아래 「그림 목록」) |')
    w(f'| 표 · 차트 · SmartArt | ✅ {cnt["table"]} · {cnt["chart"]} · {cnt["smartart"]} |')
    w(f'| 발표자 노트 | ✅ {with_notes}장에 있음 |' if with_notes else '| 발표자 노트 | ✅ 없음 |')
    w(f'| 링크 | ✅ {len(deck.links)}개' + (' (아래 「링크」)' if deck.links else '') + ' |')
    if deck.videos:
        w(f'| 영상·소리 | ✅ 파일로 들어 있음 {len(vid_file)}개(꺼내지 않음) · 온라인 링크 {len(vid_url)}개 |')
    if meta.get('created') or meta.get('modified'):
        w(f'| 파일 속성의 날짜 | ✅ 만든 날 {meta.get("created", "?")[:10]} · 마지막 수정 {meta.get("modified", "?")[:10]} — 개발 기간이 아니라 이 파일의 날짜입니다 |')
    if deck.masked:
        w(f'| 가린 글 | ⚠ 이메일·전화번호로 보이는 글 {deck.masked}곳을 가렸습니다. 기술 문서에 연락처를 쓸 곳은 `profile.md`뿐입니다 |')
    w('')
    w('## 슬라이드')
    sec = None
    for s in slides:
        w('')
        if s.get('section') and s['section'] != sec:
            sec = s['section']
            w(f'**〔구역: {esc(sec)}〕**')
            w('')
        w(f'### {s["n"]}. {esc(s["title"]) if s["title"] else "(제목 없음)"}' + (' — 숨긴 슬라이드' if s['hidden'] else ''))
        w('')
        if not s['blocks']:
            w('(글·그림 없음)')
            w('')
        prev = None
        for b in s['blocks']:
            k = b[0]
            if k != 'p' and prev == 'p':
                w('')
            if k == 'p':
                w('  ' * min(b[1], 6) + '- ' + esc(b[2]))
            elif k == 'table':
                rows = [r for r in b[1] if any(c.strip() for c in r)]
                if rows:
                    w(f'**표** ({max(len(r) for r in rows)}열 × {len(rows)}행)')
                    w('')
                    L.extend(md_table(rows))
                    w('')
            elif k == 'chart':
                c = b[1]
                w(f'**차트({"·".join(c["types"]) or "?"})**' + (f' — {esc(c["title"])}' if c['title'] else ''))
                w('')
                ser = c['series']
                cats = max((s_[1] for s_ in ser), key=len, default=[])
                n = max([len(cats)] + [len(s_[2]) for s_ in ser]) if ser else 0
                if ser and n:
                    rows = [[cats[i] if i < len(cats) else str(i + 1)] + [(s_[2][i] if i < len(s_[2]) else '') for s_ in ser] for i in range(min(n, 40))]
                    L.extend(md_table(rows, ['항목'] + [s_[0] or f'계열 {j + 1}' for j, s_ in enumerate(ser)]))
                    if n > 40:
                        w(f'(값 {n}개 중 40개까지)')
                else:
                    w('(값이 파일에 저장되어 있지 않음)')
                w('')
            elif k == 'smartart':
                w('**SmartArt**')
                w('')
                for lvl, t in b[1]:
                    w('  ' * min(lvl, 6) + '- ' + esc(t))
                w('')
            elif k == 'img':
                for rec, use in b[1]:
                    d = f'{rec["dims"][0]}×{rec["dims"][1]} ' if rec['dims'] else ''
                    line = f'**그림** `pptx/{rec["name"]}` ({d}{rec["fmt"].upper()}, {size_txt(rec["bytes"])})'
                    if rec['slides'][0] != s['n']:
                        line += f' — {rec["slides"][0]}장과 같은 그림'
                    if use['alt']:
                        line += f' — 대체 텍스트 "{esc(use["alt"])}"'
                    note = img_note(rec, use)
                    w(line + (f' · ⚠ {note}' if note else ''))
                    w('')
            elif k == 'media':
                for md in b[1]:
                    what = '소리' if md['kind'] == 'audio' else '영상'
                    if 'url' in md:
                        w(f'**{what}** 링크 {esc(md["url"])}')
                    else:
                        w(f'**{what}** 파일로 들어 있음(`{esc(md["file"])}`, {size_txt(md["size"])}) — 꺼내지 않음')
                    w('')
            elif k == 'note':
                w(f'({esc(b[1])})')
                w('')
            prev = k
        if prev == 'p':
            w('')
        if s['notes']:
            w('**발표자 노트**')
            w('')
            for lvl, t in s['notes']:
                w('> ' + '  ' * min(lvl, 6) + esc(t))
            w('')
        w('---')

    w('')
    w('## 그림 목록')
    w('')
    if imgs:
        w('쓸 그림만 골라 `../images/`로 **복사**하세요(이 폴더는 git에 올라가지 않아 배포되지 않습니다). 파일 이름은 영문 소문자로 바꾸는 것을 권합니다(예: `boss_intro.gif`).')
        w('')
        w('- 기능이 겹치지 않게 **4~6장**이 좋습니다. 가장 인상적인 화면 하나는 `images/cover.png`로 — 포트폴리오 문서 프로젝트 소개·발표 덱 표지에 크게 나옵니다.')
        w('- **남이 만든 그림은 넣지 마세요**: 레퍼런스 게임 화면, 회사·엔진 로고, 인터넷에서 받은 그림, (팀 자료라면) 팀원이 만든 그림. 배경·도형 채우기 그림은 대개 발표 자료 장식입니다.')
        w('- 다이어그램·구조 그림은 그림 대신 mermaid로 다시 그리면 상세 문서·발표 덱에서 글자가 선명합니다(`format/03-diagrams.md`).')
        w('- 용량(⚠ 제안값): PNG는 1280×720 이상·2MB 이하, GIF는 폭 960 이하·10초·8MB 이하, 프로젝트 `images/` 합계 60MB 이하.')
        w('')
        rows = [[f'`pptx/{r["name"]}`', ', '.join(str(x) for x in r['slides']), f'{r["dims"][0]}×{r["dims"][1]}' if r['dims'] else '?',
                 size_txt(r['bytes']), r['fmt'].upper(), img_note(r)] for r in imgs]
        L.extend(md_table(rows, ['파일', '슬라이드', '크기(px)', '용량', '형식', '비고']))
    else:
        w('(그림 없음)')
    w('')
    if deck.links or vid_url:
        w('## 링크')
        w('')
        rows = [[t, u, str(n)] for t, u, n in deck.links] + [['(영상)' if v['kind'] == 'video' else '(소리)', v['url'], str(v['slide'])] for v in vid_url]
        L.extend(md_table(rows, ['글', '주소', '슬라이드']))
        w('')
        w('시연 영상 링크가 있으면 기술 문서 개요 표의 링크로 쓰세요(포트폴리오 문서·발표 덱 표지에 나옵니다).')
        w('')
    if vid_file:
        w('## 파일로 들어 있는 영상·소리')
        w('')
        w('꺼내지 않았습니다. 시연 영상은 파일로 넣지 말고 YouTube 등에 올려 **링크**로 쓰세요(5분 이내).')
        w('')
        for v in vid_file:
            w(f'- 슬라이드 {v["slide"]}: `{esc(v["file"])}` ({size_txt(v["size"])})')
        w('')
    w('## 질문 — 기술 문서 5단 구조')
    w('')
    w('발표 자료에는 대개 **무엇을 했는지**가 있고, 기술 문서에 필요한 **왜·어떻게·무엇과 비교했는지**는 말로만 했던 경우가 많습니다. 슬라이드 번호를 보며 답해 보세요.')
    w('구조와 예시는 `format/02-tech-document.md`와 샘플 `src/00-example-crafting/기술문서.md`를 보세요.')
    w('')
    w('**0. 개요 표**')
    w('- [ ] ❓ 장르 · 실제 개발 기간 · 인원과 **내 역할** · 개발 환경(Unity 버전·IDE) — 슬라이드에 적혀 있어도 맞는지 다시 확인하세요')
    w('')
    w('**1. 개요 — 무엇을, 왜**')
    w('- [ ] ❓ **무엇을** 만들었나요? (한두 문장)')
    w('- [ ] ❓ **왜** 만들었나요? 어떤 문제·목적이었나요? 가능하면 측정할 수 있는 말로')
    w('- [ ] ❓ 개발 의도 — 포트폴리오 문서의 프로젝트 소개에 들어갑니다')
    w('')
    w('**2. 시스템 구조 — 어떻게 구성**')
    w('- [ ] ❓ 구조를 보여 준 슬라이드가 있나요? 핵심 클래스·시스템마다 맡은 일과 **왜 이렇게 나눴는지**')
    w('')
    w('**3. 핵심 기능 — 어떻게 동작**')
    w('- [ ] ❓ 슬라이드에 나온 기능마다: 밖에서 무엇을 하면 무엇이 일어나나요? 그 기능의 핵심 코드(50줄 안)는 어디인가요?')
    w('')
    w('**4. 고민과 선택 — 왜 이 방법**')
    w('- [ ] ❓ 다른 방법 **두 가지 이상**과 비교했나요? 무엇이 선택의 근거였나요? (비교 표 권장)')
    w('')
    w('**5. 결과 · 배운 점**')
    w('- [ ] ❓ 발표에서 말한 성과·수치의 근거(측정 방법)는 무엇인가요?')
    w('- [ ] ❓ 기억나는 문제마다: 문제 → 원인 → 해결 → 결과 · 아쉬운 점과 개선 계획')
    w('')
    if not has_repo_intake:
        w('> 코드에서 사실(클래스·패키지·커밋 기록)을 뽑으려면 `/portfolio-intake <내 GitHub 레포 주소>`를 같은 폴더 이름으로 실행하세요.')
        w('')
    w('## 옮기지 않은 것')
    w('')
    w('- 위치·크기·색·글꼴·애니메이션·화면 전환, 슬라이드 마스터·레이아웃의 장식, 머리글·바닥글·쪽 번호')
    w('- 파일 이름과 문서 속성의 작성자(팀원 이름이 들어 있을 수 있음)')
    for what, n in deck.skipped.items():
        w(f'- {what}: {n}개')
    w('')
    return '\n'.join(L)


IMG_NAME = re.compile(r'^s\d{2,4}-(?:\d+|bg\d*)\.[a-z0-9]+$')


def main():
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8')
    ap = argparse.ArgumentParser(description='발표 자료(.pptx) → _intake/발표자료.md + _intake/pptx/')
    ap.add_argument('pptx')
    ap.add_argument('out', help='src/<폴더>/_intake')
    ap.add_argument('--replace', action='store_true', help='이미 있는 발표자료.md·pptx/를 바꿈')
    a = ap.parse_args()

    def fail(code, msg, **kw):
        print(json.dumps(dict(ok=False, error=msg, **kw), ensure_ascii=False, indent=1))
        sys.exit(code)

    out = os.path.abspath(a.out)
    if os.path.basename(out) != '_intake':
        fail(2, '출력 폴더 이름이 _intake가 아닙니다(src/<폴더>/_intake로 주세요)')
    md_path, img_dir = os.path.join(out, '발표자료.md'), os.path.join(out, 'pptx')
    if os.path.exists(md_path) and not a.replace:
        fail(2, '이미 발표자료.md가 있습니다 — 바꾸려면 --replace', exists=True)
    if not os.path.isfile(a.pptx):
        fail(1, '파일이 없습니다')
    with open(a.pptx, 'rb') as f:
        head = f.read(8)
    if head.startswith(b'\xd0\xcf\x11\xe0'):
        fail(1, '옛 PowerPoint 형식(.ppt)이거나 암호가 걸린 파일입니다', kind='ole')
    try:
        z = zipfile.ZipFile(a.pptx)
    except zipfile.BadZipFile:
        fail(1, 'zip 형식이 아닙니다(손상되었거나 PowerPoint 파일이 아님)', kind='badzip')
    h = hashlib.sha256()
    with open(a.pptx, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    sha = h.hexdigest()
    deck = Deck(Pkg(z))
    try:
        slides, meta = deck.read()
    except NotPptx:
        fail(1, 'PowerPoint 프레젠테이션이 아닙니다(presentation.xml 없음 — Word·Excel 파일?)', kind='not-pptx')
    except ET.ParseError as e:
        fail(1, f'파일 안의 XML을 읽지 못했습니다({e})', kind='xml')

    today = datetime.date.today().isoformat()
    os.makedirs(img_dir, exist_ok=True)
    for fn in os.listdir(img_dir):                                       # 지난번에 이 스크립트가 쓴 그림만(이름 규칙으로) 지움
        if IMG_NAME.match(fn):
            os.remove(os.path.join(img_dir, fn))
    for hsh in deck.img_order:
        rec = deck.images[hsh]
        with open(os.path.join(img_dir, rec['name']), 'wb') as f:
            f.write(rec['data'])
    has_repo = os.path.exists(os.path.join(out, '재료집.md'))
    text = render(slides, meta, deck, sha, today, has_repo)
    with open(md_path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)

    ij = os.path.join(out, 'intake.json')
    info = {'sha256': sha, 'slides': len(slides), 'extractedAt': today, 'md': '발표자료.md',
            'images': [deck.images[x]['name'] for x in deck.img_order]}
    try:
        data = {}
        if os.path.exists(ij):
            with open(ij, encoding='utf-8-sig') as f:
                data = json.load(f)
        if isinstance(data, dict):
            data['pptx'] = info
            with open(ij, 'w', encoding='utf-8', newline='\n') as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
                f.write('\n')
        else:
            deck.warn.append('intake.json 형식이 달라 pptx 항목을 쓰지 않았습니다')
    except (ValueError, OSError) as e:
        deck.warn.append(f'intake.json을 고치지 못했습니다({type(e).__name__}) — 그대로 둠')

    imgs = [deck.images[x] for x in deck.img_order]
    blocks = [b[0] for s in slides for b in s['blocks']]
    print(json.dumps({
        'ok': True, 'md': md_path, 'images_dir': img_dir,
        'title': next((s['title'] for s in slides if s['title']), ''),
        'slides': len(slides), 'hidden': sum(1 for s in slides if s['hidden']),
        'untitled': [s['n'] for s in slides if not s['title']],
        'titles_guessed': [s['n'] for s in slides if s.get('guessed')],
        'with_notes': sum(1 for s in slides if s['notes']),
        'paragraphs': blocks.count('p'), 'tables': blocks.count('table'), 'charts': blocks.count('chart'), 'smartart': blocks.count('smartart'),
        'images': len(imgs), 'images_bytes': sum(r['bytes'] for r in imgs),
        'images_not_web': [r['name'] for r in imgs if r['fmt'] not in WEB_OK],
        'images_big_gif': [r['name'] for r in imgs if r['fmt'] == 'gif' and r['bytes'] > 8 * 1048576],
        'links': len(deck.links), 'videos_embedded': sum(1 for v in deck.videos if 'file' in v), 'videos_online': sum(1 for v in deck.videos if 'url' in v),
        'masked': deck.masked, 'skipped': deck.skipped, 'app': meta.get('app', ''), 'warnings': deck.warn,
    }, ensure_ascii=False, indent=1))


if __name__ == '__main__':
    main()
