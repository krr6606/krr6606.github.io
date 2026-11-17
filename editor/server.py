#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""포트폴리오 편집 서버 — 내 컴퓨터에서만 쓰는 미리보기 + 저장 서버입니다.

  python editor/server.py [포트]        (Windows에서 python이 없으면 py editor/server.py)
  → http://localhost:4601 을 열면 세 페이지 오른쪽 아래에 [편집] 단추가 생깁니다.

하는 일
  - 레포 폴더를 그대로 보여 줍니다(python -m http.server와 같음).
  - index.html · project.html · doc.html을 내보낼 때만 편집 도구(editor/edit.js)를 끼워 넣습니다.
    파일 자체는 바꾸지 않으므로 GitHub Pages에 올라간 페이지에는 편집 도구가 없습니다.
  - 저장 요청(/__edit/…)을 받아 정해진 파일만 고칩니다:
      src/<폴더>/<기술 문서>.md · src/<폴더>/images/(그림 올리기) · src/<폴더>/deck-edits.json
      profile.md · theme/strings.tsv · theme/theme.css
      data/portfolio.json의 profile·settings·projects(profile.md 값을 그대로 옮김)
      data/projects/<폴더>.json의 deck(.claude/skills/portfolio-build/deck-edits.py apply가 씀)
  - 127.0.0.1에서만 듣고, 다른 사이트가 보낸 요청은 받지 않습니다.
  - Python 3.8 이상, 표준 라이브러리만.
"""
import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
DECK_PY = ROOT / '.claude' / 'skills' / 'portfolio-build' / 'deck-edits.py'
PAGES = {'/', '/index.html', '/project.html', '/doc.html'}
INJECT = b'<link rel="stylesheet" href="editor/edit.css" />\n<script src="editor/edit.js"></script>\n'
IMG_EXT = {'.png', '.jpg', '.jpeg', '.gif', '.webp', '.svg', '.mp4', '.webm'}
MAX_UPLOAD = 60 * 1024 * 1024
ID_RE = re.compile(r'^[^/\\_.][^/\\]*$')                          # src/ 바로 아래 폴더 이름(_·.으로 시작하지 않음)
LAYOUTS = ('overview', 'visual', 'code', 'full', 'screens', 'problem')
TABS = ('개요', '구조', '흐름', '구현', '선택', '문제 해결', '결과', '')
KINDS = ('mermaid', 'code', 'table', 'image')


class Fail(Exception):
    def __init__(self, msg, status=400, **extra):
        super().__init__(msg)
        self.status, self.extra = status, extra


# ── 파일 읽기·쓰기(인코딩·BOM·줄 끝을 그대로 유지) ──────────────────
def read_raw(path):
    b = path.read_bytes()
    if b[:2] in (b'\xff\xfe', b'\xfe\xff'):
        enc, text = 'utf-16', b.decode('utf-16')
    else:
        try:
            enc, text = ('utf-8-sig' if b[:3] == b'\xef\xbb\xbf' else 'utf-8'), b.decode('utf-8-sig')
        except UnicodeDecodeError:
            enc, text = 'cp949', b.decode('cp949', errors='replace')
    eol = '\r\n' if '\r\n' in text else '\n'
    return {'text': text.replace('\r\n', '\n'), 'enc': enc, 'eol': eol, 'hash': hashlib.sha1(b).hexdigest()}


def write_raw(path, text, enc='utf-8', eol='\n'):
    text = text.replace('\r\n', '\n')
    if eol == '\r\n':
        text = text.replace('\n', '\r\n')
    data = text.encode(enc)
    tmp = path.with_name(path.name + '.pf-tmp')
    tmp.write_bytes(data)
    os.replace(tmp, path)
    return hashlib.sha1(data).hexdigest()


def project_ids():
    src = ROOT / 'src'
    return sorted(p.name for p in src.iterdir() if p.is_dir() and ID_RE.match(p.name)) if src.is_dir() else []


def check_id(pid):
    if not isinstance(pid, str) or not ID_RE.match(pid) or not (ROOT / 'src' / pid).is_dir():
        raise Fail('프로젝트 폴더 "%s"가 src/에 없습니다' % pid, 404)
    return pid


def text_target(rel):
    """편집 서버가 통째로 읽고 쓸 수 있는 글 파일인지 확인하고 경로를 돌려줌"""
    rel = str(rel or '').replace('\\', '/')
    parts = rel.split('/')
    if rel in ('profile.md', 'theme/strings.tsv', 'theme/theme.css'):
        return ROOT / rel
    if len(parts) == 3 and parts[0] == 'src' and ID_RE.match(parts[1]) and parts[2].lower().endswith('.md') and '..' not in parts[2]:
        return ROOT / 'src' / parts[1] / parts[2]
    raise Fail('이 파일은 편집 서버로 고칠 수 없습니다: %s' % rel, 403)


# ── profile.md ───────────────────────────────────────────────────
PROFILE_KEYS = {'name': '이름', 'nameEn': '영문 이름', 'headline': '한 줄 정체성', 'email': '이메일', 'github': 'GitHub',
                'layout': '구성 방식', 'accent': '강조색', 'order': '프로젝트 순서'}


def _key_line(lines, label):
    pat = re.compile(r'^(\s*[-*]\s*' + re.escape(label) + r'\s*:)[ \t]*(.*)$')
    for i, ln in enumerate(lines):
        m = pat.match(ln)
        if m:
            return i, m
    return -1, None


def _order_line(lines, i, ids):
    """'- 프로젝트 순서:' 줄이 비어 있고, 값이 아래 줄(주석 뒤)에 따로 적혀 있으면 그 줄 번호"""
    for j in range(i + 1, len(lines)):
        s = lines[j].strip()
        if not s or s.startswith('<!--'):
            continue
        names = [x.strip() for x in s.split(',') if x.strip()]
        return j if names and all(n in ids for n in names) else -1
    return -1


def _intro_span(lines):
    start = next((i for i, ln in enumerate(lines) if re.match(r'^##\s*자기소개\s*$', ln)), -1)
    if start < 0:
        return None
    end = next((j for j in range(start + 1, len(lines)) if lines[j].startswith('## ')), len(lines))
    body = [k for k in range(start + 1, end) if lines[k].strip() and not lines[k].lstrip().startswith('>')]
    return start, end, body


def read_profile():
    raw = read_raw(ROOT / 'profile.md')
    lines = raw['text'].split('\n')
    ids = project_ids()
    out = {}
    for k, label in PROFILE_KEYS.items():
        i, m = _key_line(lines, label)
        v = re.sub(r'<!--.*?-->', '', m.group(2)).strip() if m else ''
        if k == 'order' and m and not v:
            j = _order_line(lines, i, ids)
            v = lines[j].strip() if j >= 0 else ''
        out[k] = v
    span = _intro_span(lines)
    out['intro'] = '\n'.join(lines[k] for k in span[2]) if span else ''
    out['order'] = [x.strip() for x in out['order'].split(',') if x.strip()]
    return out, raw


def save_profile(fields, base_hash):
    path = ROOT / 'profile.md'
    cur, raw = read_profile()
    if base_hash and base_hash != raw['hash']:
        raise Fail('profile.md가 그사이 바뀌었습니다. 새로고침한 뒤 다시 고쳐 주세요', 409)
    ids = project_ids()
    lines = raw['text'].split('\n')
    for k, label in PROFILE_KEYS.items():
        if k not in fields:
            continue
        v = fields[k]
        if k == 'order':
            v = [x for x in v if x in ids]
            v = ', '.join(v + [x for x in ids if x not in v])
        v = str(v).replace('\n', ' ').strip()
        if k == 'accent' and v and not re.match(r'^#[0-9a-fA-F]{6}$', v):
            raise Fail('강조색은 #RRGGBB 형식이어야 합니다(예: #2F6FEB)')
        if k == 'layout' and v not in ('project', 'stack'):
            raise Fail('구성 방식은 project 또는 stack입니다')
        i, m = _key_line(lines, label)
        if i < 0:
            raise Fail('profile.md에서 "- %s:" 줄을 찾지 못했습니다. 그 줄을 직접 넣은 뒤 다시 저장해 주세요' % label)
        if k == 'order' and not re.sub(r'<!--.*?-->', '', m.group(2)).strip():
            j = _order_line(lines, i, ids)
            if j >= 0:                                             # 값이 아래 줄에 따로 적혀 있던 경우: 그 줄을 바꿈
                lines[j] = v
                continue
        tail = re.search(r'\s*<!--.*-->\s*$', m.group(2))
        lines[i] = '%s %s%s' % (m.group(1), v, tail.group(0) if tail else '')
    if 'intro' in fields:
        span = _intro_span(lines)
        if not span:
            raise Fail('profile.md에서 "## 자기소개" 절을 찾지 못했습니다')
        start, end, body = span
        new = [ln.rstrip() for ln in str(fields['intro']).replace('\r\n', '\n').split('\n') if ln.strip()]
        if body:
            lines[body[0]:body[-1] + 1] = new
        else:
            at = end
            while at > start + 1 and not lines[at - 1].strip():
                at -= 1
            lines[at:at] = ([''] if lines[at - 1].strip() else []) + new
    h = write_raw(path, '\n'.join(lines), raw['enc'], raw['eol'])
    # data/portfolio.json: profile.md 값을 그대로 옮기는 칸만(빌드 규칙과 같음). 다른 값은 건드리지 않음
    now, _ = read_profile()
    dp = ROOT / 'data' / 'portfolio.json'
    synced = False
    if dp.exists():
        draw = read_raw(dp)
        data = json.loads(draw['text'])
        prof = data.setdefault('profile', {})
        for k in ('name', 'nameEn', 'headline', 'intro', 'email', 'github'):
            prof[k] = now[k]
        st = data.setdefault('settings', {})
        st['layout'] = now['layout'] if now['layout'] in ('project', 'stack') else 'project'
        if re.match(r'^#[0-9a-fA-F]{6}$', now['accent'] or ''):
            st['accent'] = now['accent']
        built = [p for p in data.get('projects', [])]
        order = [x for x in now['order'] if x in built]
        data['projects'] = order + [x for x in built if x not in order]
        write_raw(dp, json.dumps(data, ensure_ascii=False, indent=2) + ('\n' if draw['text'].endswith('\n') else ''), draw['enc'], draw['eol'])
        synced = True
    return {'hash': h, 'synced': synced}


# ── theme/strings.tsv: 그 키 줄의 맨 끝 "내 문구" 칸만 ───────────────
def save_string(key, value, where='', default=''):
    path = ROOT / 'theme' / 'strings.tsv'
    if not re.match(r'^[a-z][a-z0-9.-]+$', str(key or '')):
        raise Fail('문구 키가 올바르지 않습니다')
    value = re.sub(r'[\t\r\n]+', ' ', str(value)).strip()
    raw = read_raw(path) if path.exists() else {'text': '키\t보이는 곳 (권장 길이)\t기본 문구\t내 문구\n', 'enc': 'utf-8-sig', 'eol': '\n'}
    lines = raw['text'].split('\n')
    hit = [i for i, ln in enumerate(lines) if ln.split('\t')[0].strip() == key]
    if hit:
        c = lines[hit[-1]].split('\t')
        if len(c) == 2:
            c[1] = value
        else:
            c = (c + ['', '', ''])[:max(4, len(c))]
            c[3] = value
        lines[hit[-1]] = '\t'.join(c)
    else:
        row = '\t'.join([key, re.sub(r'[\t\r\n]+', ' ', str(where)), re.sub(r'[\t\r\n]+', ' ', str(default)), value])
        at = len(lines) - 1 if lines and lines[-1] == '' else len(lines)
        lines.insert(at, row)
    path.parent.mkdir(exist_ok=True)
    return {'hash': write_raw(path, '\n'.join(lines), raw['enc'], raw['eol'])}


# ── theme/theme.css: 이미 있는 손잡이(--t-*)의 값만 ──────────────────
def save_theme(changes):
    path = ROOT / 'theme' / 'theme.css'
    raw = read_raw(path)
    text = raw['text']
    done = []
    for name, value in changes.items():
        value = str(value).strip()
        if not re.match(r'^--t-[a-z0-9-]+$', name) or not value or re.search(r'[;{}<>\n]|/\*|\*/', value):
            raise Fail('테마 값이 올바르지 않습니다: %s' % name)
        pat = re.compile(r'(^[ \t]*' + re.escape(name) + r'[ \t]*:[ \t]*)([^;\n]*)(;)', re.M)
        if not pat.search(text):
            raise Fail('theme/theme.css에 %s 손잡이가 없습니다' % name)
        text = pat.sub(lambda m: m.group(1) + value + m.group(3), text, count=1)
        done.append(name)
    return {'hash': write_raw(path, text, raw['enc'], raw['eol']), 'saved': done}


# ── 발표 덱 장면: src/<폴더>/deck-edits.json + deck-edits.py ─────────
def run_deck(*args):
    env = dict(os.environ, PYTHONIOENCODING='utf-8', PYTHONUTF8='1')
    r = subprocess.run([sys.executable, str(DECK_PY), *args], cwd=str(ROOT), capture_output=True, env=env, timeout=120)
    out = (r.stdout.decode('utf-8', 'replace') + r.stderr.decode('utf-8', 'replace')).strip()
    return r.returncode, out


def _dump_edits(edits):
    try:                                                          # 스크립트와 같은 줄 모양으로(없으면 보통 JSON)
        spec = importlib.util.spec_from_file_location('pf_deck_edits', str(DECK_PY))
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        return mod.dump(edits) + '\n'
    except Exception:
        return json.dumps(edits, ensure_ascii=False, indent=2) + '\n'


def _piece(new, old):
    """글 조각: 글자가 그대로면 예전 표시(verbatim)를 유지, 바꿨으면 직접 고친 글(verbatim: false)"""
    text = re.sub(r'\s+', ' ', str((new or {}).get('text', '') if isinstance(new, dict) else new or '')).strip()
    if isinstance(old, dict) and old.get('text') == text:
        return {'text': text, 'verbatim': bool(old.get('verbatim'))}
    return {'text': text, 'verbatim': False}


def _clean_slide(patch, pin):
    if 'layout' in patch:
        if patch['layout'] not in LAYOUTS:
            raise Fail('장면 틀이 올바르지 않습니다')
        pin['layout'] = patch['layout']
    if 'tab' in patch:
        if patch['tab'] not in TABS:
            raise Fail('탭이 올바르지 않습니다')
        pin['tab'] = patch['tab']
    if 'title' in patch:
        t = _piece(patch['title'], pin.get('title'))
        if not t['text']:
            raise Fail('장면 제목이 비어 있습니다')
        pin['title'] = t
    if 'lead' in patch:
        t = _piece(patch['lead'], pin.get('lead'))
        pin['lead'] = t if t['text'] else None
    if 'points' in patch:
        old = {(p.get('text') or ''): p for p in pin.get('points') or [] if isinstance(p, dict)}
        pts = []
        for p in patch['points'] or []:
            t = _piece(p, old.get(re.sub(r'\s+', ' ', str(p.get('text', ''))).strip()))
            if t['text']:
                pts.append({'label': str(p.get('label') or '').strip(), **t})
        if len(pts) > 4:
            raise Fail('요점은 4개까지입니다')
        pin['points'] = pts
    if 'visuals' in patch:
        vs = []
        for v in patch['visuals'] or []:
            if not isinstance(v, dict) or v.get('kind') not in KINDS:
                raise Fail('시각 자료 종류가 올바르지 않습니다')
            o = {'kind': v['kind']}
            if v['kind'] == 'image':
                o['src'] = str(v.get('src') or '')
                for k in ('w', 'h'):
                    if isinstance(v.get(k), int):
                        o[k] = v[k]
            if isinstance(v.get('n'), int) and (v['kind'] != 'image' or v.get('n')):
                o['n'] = v['n']
            elif v['kind'] != 'image':
                o['n'] = 1
            if v['kind'] == 'code' and isinstance(v.get('lines'), list) and len(v['lines']) == 2 and all(isinstance(x, int) and x > 0 for x in v['lines']) and v['lines'][0] <= v['lines'][1]:
                o['lines'] = v['lines']
            if v['kind'] != 'image':
                o['fp'] = str(v.get('fp') or '')
            if isinstance(v.get('sectionPath'), list) and v['sectionPath']:
                o['sectionPath'] = [str(x) for x in v['sectionPath']]
            if isinstance(v.get('caption'), dict) and v['caption'].get('text'):
                o['caption'] = {'text': str(v['caption']['text']), 'verbatim': bool(v['caption'].get('verbatim'))}
            vs.append(o)
        if len(vs) > 3:
            raise Fail('시각 자료는 한 장면에 3개까지입니다')
        pin['visuals'] = vs


def deck_op(body):
    pid = check_id(body.get('id'))
    op, key = body.get('op'), str(body.get('key') or '')
    dp, ep = ROOT / 'data' / 'projects' / (pid + '.json'), ROOT / 'src' / pid / 'deck-edits.json'
    if not DECK_PY.exists():
        raise Fail('장면 고치기 스크립트(.claude/skills/portfolio-build/deck-edits.py)가 없습니다', 500)
    if not dp.exists():
        raise Fail('data/projects/%s.json이 없습니다 → 먼저 /portfolio-build' % pid, 404)
    backup = {p: (p.read_bytes() if p.exists() else None) for p in (dp, ep)}

    def restore():
        for p, b in backup.items():
            if b is None:
                if p.exists():
                    p.unlink()
            else:
                p.write_bytes(b)

    def must(*args):
        code, out = run_deck(*args)
        if code:
            raise Fail(out or '장면 고치기 스크립트가 실패했습니다')
        return out

    def load():
        return json.loads(ep.read_bytes().decode('utf-8-sig'))

    def pin_of(edits, k):
        return next((x for x in edits.get('pin', []) if isinstance(x, dict) and x.get('key') == k), None)

    def ensure_pin(k):
        must('pin', pid, k)
        edits = load()
        p = pin_of(edits, k)
        if p is None:
            raise Fail('장면 "%s"를 고정하지 못했습니다' % k)
        return edits, p

    def to_end(edits, p):                                          # 순서 옮기기는 맨 나중에 적용되게
        edits['pin'].remove(p)
        edits['pin'].append(p)

    try:
        if op == 'undo-all':
            must('undo', pid, '--all')
        elif not key:
            raise Fail('장면이 필요합니다')
        elif op == 'hide':
            must('hide', pid, key)
        elif op == 'undo':
            must('undo', pid, key)
        elif op == 'update':
            edits, p = ensure_pin(key)
            _clean_slide(body.get('slide') or {}, p)
            ep.write_bytes(_dump_edits(edits).encode('utf-8'))
        elif op in ('up', 'down'):
            data = json.loads(dp.read_bytes().decode('utf-8-sig'))
            slides = (data.get('deck') or {}).get('slides') or []
            me = next((s for s in slides if s.get('key') == key), None)
            if not me:
                raise Fail('장면 "%s"가 없습니다' % key)
            sib = [s['key'] for s in slides if s.get('chapter') == me.get('chapter')]
            i = sib.index(key)
            if op == 'down':
                if i == len(sib) - 1:
                    raise Fail('이 장의 마지막 장면입니다(장면은 같은 장 안에서만 옮길 수 있습니다)')
                mover, after = key, sib[i + 1]
            elif i == 0:
                raise Fail('이 장의 첫 장면입니다(장면은 같은 장 안에서만 옮길 수 있습니다)')
            elif i == 1:
                mover, after = sib[0], key
            else:
                mover, after = key, sib[i - 2]
            edits, p = ensure_pin(mover)
            p['after'] = after
            other = pin_of(edits, after)
            if other is not None and other.get('after') == mover:   # 서로를 가리키는 옛 순서는 지움
                other.pop('after')
            to_end(edits, p)
            ep.write_bytes(_dump_edits(edits).encode('utf-8'))
        else:
            raise Fail('모르는 작업입니다: %s' % op)
        code, out = run_deck('apply', pid)
        if code:                                                   # 이유([필수] 줄)만 보여 줌
            must_lines = [ln.strip() for ln in out.splitlines() if '[필수]' in ln]
            raise Fail('\n'.join(must_lines) or out or '반영하지 못했습니다')
        return {'ok': True, 'log': out}
    except Exception:
        restore()
        raise


# ── 그림 ─────────────────────────────────────────────────────────
def list_images(pid):
    d = ROOT / 'src' / check_id(pid) / 'images'
    if not d.is_dir():
        return []
    return sorted('images/' + p.relative_to(d).as_posix() for p in d.rglob('*') if p.is_file() and p.suffix.lower() in IMG_EXT)


def save_upload(pid, name, data):
    d = ROOT / 'src' / check_id(pid) / 'images'
    name = re.sub(r'[\\/:*?"<>|\s]+', '-', os.path.basename(str(name or 'image.png'))).strip('-.') or 'image.png'
    stem, ext = os.path.splitext(name)
    if ext.lower() not in IMG_EXT:
        raise Fail('올릴 수 있는 파일: %s' % ' '.join(sorted(IMG_EXT)))
    if not data or len(data) > MAX_UPLOAD:
        raise Fail('파일이 비었거나 너무 큽니다(60MB까지)')
    d.mkdir(exist_ok=True)
    out, n = d / name, 1
    while out.exists():
        if out.read_bytes() == data:                               # 같은 그림을 또 올리면 새로 만들지 않음
            return {'path': 'images/' + out.name, 'reused': True}
        n += 1
        out = d / ('%s-%d%s' % (stem, n, ext))
    out.write_bytes(data)
    return {'path': 'images/' + out.name}


# ── git: 바뀐 파일 보기·되돌리기 ────────────────────────────────────
GIT_SCOPE = ['src', 'profile.md', 'theme', 'data']


def git(*args):
    try:
        r = subprocess.run(['git', '-c', 'core.quotepath=off', *args], cwd=str(ROOT), capture_output=True, timeout=30)
        return r.returncode, r.stdout.decode('utf-8', 'replace'), r.stderr.decode('utf-8', 'replace')
    except (OSError, subprocess.TimeoutExpired) as e:
        return 1, '', str(e)


def changes():
    code, out, err = git('status', '--porcelain', '--', *GIT_SCOPE)
    if code:
        return {'git': False, 'files': [], 'note': err.strip()}
    files = []
    for ln in out.splitlines():
        st, path = ln[:2], ln[3:].strip().strip('"')
        if ' -> ' in path:
            path = path.split(' -> ')[1]
        files.append({'path': path, 'state': 'new' if '?' in st or 'A' in st else 'deleted' if 'D' in st else 'modified'})
    return {'git': True, 'files': files}


def revert(rel):
    rel = str(rel or '').replace('\\', '/')
    if '..' in rel.split('/') or not any(rel == s or rel.startswith(s + '/') for s in GIT_SCOPE):
        raise Fail('되돌릴 수 없는 경로입니다', 403)
    if not any(f['path'] == rel and f['state'] == 'modified' for f in changes()['files']):
        raise Fail('마지막 커밋에서 바뀐 파일이 아닙니다: %s' % rel)
    code, _, err = git('checkout', 'HEAD', '--', rel)
    if code:
        raise Fail(err.strip() or '되돌리지 못했습니다')
    return {'ok': True}


# ── HTTP ─────────────────────────────────────────────────────────
class Handler(SimpleHTTPRequestHandler):
    server_version = 'PortfolioEdit/1'

    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(ROOT), **kw)

    def log_message(self, fmt, *args):
        if self.path.startswith('/__edit/') or (args and str(args[1] if len(args) > 1 else '').startswith(('4', '5'))):
            sys.stderr.write('%s\n' % (fmt % args))

    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def local_host(self):
        host = (self.headers.get('Host') or '').rsplit(':', 1)[0].strip('[]').lower()
        return host in ('localhost', '127.0.0.1', '::1')

    def same_origin(self):
        origin = self.headers.get('Origin')
        if origin is None:
            return False
        return urlsplit(origin).netloc.lower() == (self.headers.get('Host') or '').lower()

    def send_json(self, obj, status=200):
        data = json.dumps(obj, ensure_ascii=False).encode('utf-8')
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def guard(self, fn):
        try:
            self.send_json(fn())
        except Fail as e:
            self.send_json({'error': str(e), **e.extra}, e.status)
        except Exception as e:                                     # 편집 서버가 죽지 않게: 이유를 화면에 보여 줌
            self.send_json({'error': '%s: %s' % (type(e).__name__, e)}, 500)

    def do_GET(self):
        if not self.local_host():
            return self.send_error(403, 'local only')
        u = urlsplit(self.path)
        path, q = unquote(u.path), {k: v[0] for k, v in parse_qs(u.query).items()}
        if path.startswith('/__edit/'):
            return self.guard(lambda: self.api_get(path[8:], q))
        if any(seg.startswith('.') for seg in path.split('/') if seg):
            return self.send_error(404)
        if path in PAGES:
            f = ROOT / (path.strip('/') or 'index.html')
            if f.exists():
                html = f.read_bytes()
                i = html.rfind(b'</body>')
                html = html[:i] + INJECT + html[i:] if i >= 0 else html + INJECT
                self.send_response(200)
                self.send_header('Content-Type', 'text/html; charset=utf-8')
                self.send_header('Content-Length', str(len(html)))
                self.end_headers()
                self.wfile.write(html)
                return
        return super().do_GET()

    def api_get(self, name, q):
        if name == 'state':
            docs = {}
            for pid in project_ids():
                dp = ROOT / 'data' / 'projects' / (pid + '.json')
                doc = ''
                if dp.exists():
                    try:
                        doc = json.loads(dp.read_bytes().decode('utf-8-sig')).get('doc') or ''
                    except ValueError:
                        pass
                if not doc or not (ROOT / 'src' / pid / doc).exists():
                    mds = sorted(p.name for p in (ROOT / 'src' / pid).glob('*.md'))
                    doc = mds[0] if mds else ''
                docs[pid] = doc
            return {'ok': True, 'projects': project_ids(), 'docs': docs, 'deckScript': DECK_PY.exists()}
        if name == 'file':
            p = text_target(q.get('path'))
            if not p.exists():
                raise Fail('파일이 없습니다: %s' % q.get('path'), 404)
            return read_raw(p)
        if name == 'profile':
            prof, raw = read_profile()
            return {'profile': prof, 'hash': raw['hash'], 'projects': project_ids()}
        if name == 'images':
            return {'images': list_images(q.get('p'))}
        if name == 'changes':
            return changes()
        raise Fail('없는 주소입니다', 404)

    def do_POST(self):
        if not self.local_host() or not self.same_origin() or self.headers.get('X-PF-Edit') != '1':
            return self.send_error(403, 'forbidden')
        u = urlsplit(self.path)
        path, q = unquote(u.path), {k: v[0] for k, v in parse_qs(u.query).items()}
        if not path.startswith('/__edit/'):
            return self.send_error(404)
        n = int(self.headers.get('Content-Length') or 0)
        if n > MAX_UPLOAD + 1024:
            return self.send_error(413)
        raw = self.rfile.read(n) if n else b''
        self.guard(lambda: self.api_post(path[8:], q, raw))

    def api_post(self, name, q, raw):
        if name == 'upload':
            return save_upload(q.get('p'), q.get('name'), raw)
        body = json.loads(raw.decode('utf-8')) if raw else {}
        if name == 'file':
            p = text_target(body.get('path'))
            if not p.exists():
                raise Fail('파일이 없습니다: %s' % body.get('path'), 404)
            cur = read_raw(p)
            if body.get('baseHash') and body['baseHash'] != cur['hash'] and not body.get('force'):
                raise Fail('파일이 다른 곳에서 바뀌었습니다', 409, conflict=True)
            return {'hash': write_raw(p, str(body.get('text', '')), cur['enc'], cur['eol'])}
        if name == 'profile':
            return save_profile(body.get('fields') or {}, body.get('baseHash'))
        if name == 'string':
            return save_string(body.get('key'), body.get('value', ''), body.get('where', ''), body.get('default', ''))
        if name == 'theme':
            return save_theme(body.get('vars') or {})
        if name == 'deck':
            return deck_op(body)
        if name == 'revert':
            return revert(body.get('path'))
        raise Fail('없는 주소입니다', 404)


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 4601
    for s in (sys.stdout, sys.stderr):
        try:
            s.reconfigure(encoding='utf-8')
        except (AttributeError, ValueError):
            pass
    httpd = ThreadingHTTPServer(('127.0.0.1', port), Handler)
    print('포트폴리오 편집 서버: http://localhost:%d  (끝내기: Ctrl+C)' % port)
    print('이 주소는 내 컴퓨터에서만 열립니다. 페이지 오른쪽 아래 [편집] 단추를 누르세요.')
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == '__main__':
    main()
