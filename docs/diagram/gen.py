#!/usr/bin/env python3
"""Generate dotnet-api-platform-architecture.drawio — 5 pages, embedded Azure icons."""
import json, base64, glob, os
import xml.etree.ElementTree as ET

IDX = json.load(open('icon-index.json'))

EXTRA = {}
for f in glob.glob('extra-icons/*.svg'):
    b64 = base64.b64encode(open(f, 'rb').read()).decode()
    EXTRA[os.path.splitext(os.path.basename(f))[0]] = (
        'shape=image;verticalLabelPosition=bottom;verticalAlign=top;imageAspect=0;aspect=fixed;'
        'image=data:image/svg+xml,' + b64)

def icon_style(*candidates):
    for c in candidates:
        exact = [t for t in IDX if t.endswith(c)]
        if exact:
            return IDX[exact[0]]
        subs = sorted([t for t in IDX if c.lower() in t.lower()], key=len)
        if subs:
            return IDX[subs[0]]
    raise KeyError(candidates)

GITHUB_SVG = ('<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 16 16">'
 '<path fill="#24292f" d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27s1.36.09 2 .27c1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8z"/></svg>')
GITHUB_STYLE = ('shape=image;verticalLabelPosition=bottom;verticalAlign=top;imageAspect=0;aspect=fixed;'
                'image=data:image/svg+xml,' + base64.b64encode(GITHUB_SVG.encode()).decode())

INK   = '#24292F'; MUT = '#59666F'
AZ    = '#0078D4'; AZ_F  = '#EAF3FB'
GH    = '#57606A'; GH_F  = '#F6F8FA'
SEC   = '#C50F1F'; SEC_F = '#FDF0F1'
GOV   = '#7B2FBE'; GOV_F = '#F6EFFC'
OK    = '#107C10'; OK_F  = '#EFF8EF'
WARN  = '#C25400'; WARN_F= '#FEF6EC'
STUB  = '#8A949E'; STUB_F= '#FAFBFC'

def cont(fill, stroke, dashed=False, fs=13):
    d = 'dashed=1;dashPattern=6 4;' if dashed else ''
    return (f'rounded=1;arcSize=4;fillColor={fill};strokeColor={stroke};{d}'
            f'verticalAlign=top;align=left;spacing=12;spacingTop=2;fontSize={fs};'
            f'fontStyle=1;fontColor={INK};html=1;')

def box(fill='#FFFFFF', stroke='#C6CDD3', dashed=False, fs=11, align='center', bold=False):
    d = 'dashed=1;dashPattern=6 4;' if dashed else ''
    b = 'fontStyle=1;' if bold else ''
    return (f'rounded=1;arcSize=8;fillColor={fill};strokeColor={stroke};{d}{b}'
            f'verticalAlign=top;align={align};spacing=6;spacingTop=2;fontSize={fs};fontColor={INK};html=1;whiteSpace=wrap;')

def chip(fill, stroke, fc=INK, fs=10, dashed=False):
    d = 'dashed=1;dashPattern=4 3;' if dashed else ''
    return (f'rounded=1;arcSize=40;fillColor={fill};strokeColor={stroke};{d}'
            f'fontSize={fs};fontColor={fc};html=1;whiteSpace=wrap;align=center;verticalAlign=middle;')

def txt(fs=10, fc=MUT, align='left', bold=False):
    b = 'fontStyle=1;' if bold else ''
    return f'text;html=1;fontSize={fs};fontColor={fc};align={align};verticalAlign=top;{b}whiteSpace=wrap;'

def edge(color='#7A8691', dashed=0, width=1.5, dash='8 4', arrow='blockThin', fill=1, fs=10, fc=None):
    d = f'dashed=1;dashPattern={dash};' if dashed else ''
    return (f'edgeStyle=orthogonalEdgeStyle;curved=1;html=1;jettySize=auto;orthogonalLoop=1;'
            f'strokeWidth={width};strokeColor={color};{d}endArrow={arrow};endFill={fill};'
            f'fontSize={fs};fontColor={fc or MUT};labelBackgroundColor=#FFFFFF;rounded=0;')

E_REQ  = edge()
E_OPT  = edge(dashed=1)
E_STUB = edge(color=STUB, dashed=1, dash='2 3', arrow='open', fill=0, width=1.2)
E_SEC  = edge(color=SEC, width=2)
E_GOV  = edge(color=GOV, dashed=1)
E_SCALE= edge(color=WARN, dashed=1, dash='10 4', width=2, fc=WARN)

class Page:
    def __init__(self, name, w=1920, h=1200):
        self.name, self.w, self.h = name, w, h
        self.cells, self.n = [], 0
    def add(self, label, style, x, y, w, h):
        self.n += 1; i = f'n{self.n}'
        self.cells.append(dict(id=i, value=label, style=style, vertex=1, x=x, y=y, w=w, h=h))
        return i
    def ic(self, keys, label, x, y, size=44, fs=10, fc=INK, bold=False):
        if keys == 'GITHUB':
            st, w0, h0 = GITHUB_STYLE, 16, 16
        elif isinstance(keys, str) and keys.startswith('X:'):
            st, w0, h0 = EXTRA[keys[2:]], 48, 48
        else:
            e = icon_style(*(keys if isinstance(keys, tuple) else (keys,)))
            st, w0, h0 = e['style'], e.get('w', 48), e.get('h', 48)
        sc = size / max(w0, h0)
        b = 'fontStyle=1;' if bold else ''
        st = st.rstrip(';') + f';fontSize={fs};fontColor={fc};{b}labelBackgroundColor=none;html=1;'
        return self.add(label, st, x, y, round(w0*sc), round(h0*sc))
    def link(self, s, t, label='', style=E_REQ, sx=None, sy=None, tx=None, ty=None, pts=None):
        self.n += 1; i = f'n{self.n}'
        st = style
        if sx is not None: st += f'exitX={sx};exitY={sy};exitDx=0;exitDy=0;'
        if tx is not None: st += f'entryX={tx};entryY={ty};entryDx=0;entryDy=0;'
        self.cells.append(dict(id=i, value=label, style=st, edge=1, src=s, dst=t, pts=pts or []))
        return i
    def title(self, t, sub):
        self.add(f'<font style="font-size:22px"><b>{t}</b></font><br><font color="{MUT}" style="font-size:12px">{sub}</font>',
                 txt(fs=12, fc=INK), 40, 18, 1300, 60)
    def legend(self, x, y):
        self.add('<b>Legend</b>', txt(fs=11, fc=INK, bold=True), x, y, 560, 18)
        rows = [(E_REQ, 'required / always fires'), (E_OPT, 'optional · config-gated'),
                (E_STUB, 'stub · future · planned seam'), (E_SCALE, 'scale signal / growth path'),
                (E_SEC, 'security control'), (E_GOV, 'governance / audit')]
        for i, (st, lab) in enumerate(rows):
            xx = x + (i % 3) * 190; yy = y + 22 + (i // 3) * 22
            self.add('', 'shape=line;html=1;strokeWidth=2;' +
                     ';'.join(p for p in st.split(';') if p.startswith(('strokeColor', 'dashed', 'dashPattern', 'strokeWidth'))),
                     xx, yy + 6, 34, 8)
            self.add(lab, txt(fs=10), xx + 40, yy, 150, 18)

def emit(pages, path):
    mf = ET.Element('mxfile', dict(host='app.diagrams.net', agent='claude', version='24.0.0'))
    for pi, p in enumerate(pages):
        d = ET.SubElement(mf, 'diagram', dict(id=f'page{pi+1}', name=p.name))
        g = ET.SubElement(d, 'mxGraphModel', dict(dx='800', dy='600', grid='0', gridSize='10',
              guides='1', tooltips='1', connect='1', arrows='1', fold='1', page='1', pageScale='1',
              pageWidth=str(p.w), pageHeight=str(p.h), math='0', shadow='0', background='#FFFFFF'))
        root = ET.SubElement(g, 'root')
        ET.SubElement(root, 'mxCell', dict(id='0'))
        ET.SubElement(root, 'mxCell', dict(id='1', parent='0'))
        for c in p.cells:
            if c.get('vertex'):
                mc = ET.SubElement(root, 'mxCell', dict(id=c['id'], value=c['value'], style=c['style'],
                        vertex='1', parent='1'))
                ET.SubElement(mc, 'mxGeometry', {'x': str(c['x']), 'y': str(c['y']),
                        'width': str(c['w']), 'height': str(c['h']), 'as': 'geometry'})
            else:
                mc = ET.SubElement(root, 'mxCell', dict(id=c['id'], value=c['value'], style=c['style'],
                        edge='1', parent='1', source=c['src'], target=c['dst']))
                geo = ET.SubElement(mc, 'mxGeometry', {'relative': '1', 'as': 'geometry'})
                if c['pts']:
                    arr = ET.SubElement(geo, 'Array', {'as': 'points'})
                    for (px, py) in c['pts']:
                        ET.SubElement(arr, 'mxPoint', dict(x=str(px), y=str(py)))
    ET.ElementTree(mf).write(path, xml_declaration=True, encoding='utf-8')
    print(f'wrote {path}')

def sub(t, s, fs=11, sfs=9):
    return f'<b style="font-size:{fs}px">{t}</b><br><font color="{MUT}" style="font-size:{sfs}px">{s}</font>'

# ================================================================ PAGE 1
p1 = Page('01 · Ecosystem — The Paved Road', 1960, 1000)
p1.title('The Paved Road — platform ecosystem',
         'How azure-platform-iac (engine), azure-project-starter (factory) and dotnet-api-platform (flagship) interlock across GitHub, Azure DevOps and Azure')
p1.legend(1360, 18)

gh = p1.add('GitHub · github.com/jasondostal', cont(GH_F, GH), 40, 130, 700, 760)
p1.ic('GITHUB', '', 700, 142, 26)

eng = p1.add(sub('azure-platform-iac', 'THE ENGINE — single source of truth'), box(bold=True, fs=12), 70, 180, 300, 320)
p1.ic('Templates', 'modules/ · 21 Bicep', 88, 225, 38, 9)
p1.add('compute · data · messaging · networking<br>security · integration · identity · ai · devops', txt(fs=9), 172, 228, 190, 44)
p1.ic('Template-Specs', 'pipeline templates', 88, 300, 38, 9)
p1.add('build-dotnet | go | python | node<br>security-gates (gitleaks = HARD)<br>deploy-environment (approval stage)', txt(fs=9), 172, 300, 190, 54)
p1.ic(('10002-icon-service-Subscriptions',), 'bootstrap/', 88, 378, 38, 9)
p1.add('onboard-subscription.sh — one command per env:<br>① resource plane &nbsp;② identity plane (WIF) &nbsp;③ ADO plane', txt(fs=9), 172, 380, 195, 50)
p1.add('semver contract · PR + validate consumers ·<br>add params, never remove', txt(fs=8.5), 88, 448, 270, 30)

lib = p1.add(sub('module library & proofs', ''), box(fs=11), 70, 540, 300, 130)
p1.add('azure-iac-patterns — à-la-carte modules', txt(fs=9), 90, 572, 260, 16)
p1.add('azure-ref-webapp-sql — private-by-default canary', txt(fs=9), 90, 592, 260, 16)
p1.add('azure-playground — cheap sandbox', txt(fs=9), 90, 612, 260, 16)

ghact = p1.add(sub('GitHub Actions', 'ci.yml: sanitize (no Fox/vendor identifiers land public) → build-test'), box(fs=10), 70, 700, 300, 66)

fac = p1.add(sub('azure-project-starter', 'THE FACTORY — cookiecutter + cruft'), box(bold=True, fs=12), 410, 180, 300, 320)
for i, a in enumerate(['dotnet-api', 'dotnet-web', 'python-function', 'go-web', 'go-desktop', 'node-agent']):
    p1.add(a, chip(AZ_F, AZ, fs=9), 430 + (i % 2) * 135, 225 + (i // 2) * 30, 125, 22)
p1.add('toggles: include_sql · include_apim ·<br>include_foundry · include_cosmos', txt(fs=9), 430, 320, 260, 32)
p1.add('post-gen hook: prune archetype · write .cruft.json ·<br>git init · mint REAL app-reg IDs via az cli<br>(Bicep can\'t create Entra app registrations)', txt(fs=8.5), 430, 358, 265, 46)
p1.add('gitleaks pre-commit in every archetype', txt(fs=8.5, fc=SEC), 430, 412, 260, 16)

gen = p1.add(sub('your-next-service', 'GENERATED — paved road by default'), box(dashed=True, bold=True, fs=12), 410, 540, 300, 130)
p1.add('src + infra/main.bicep + 2 pipelines + pre-commit', txt(fs=9), 430, 590, 270, 18)
p1.add('infra: module ../../azure-platform-iac/modules/*', txt(fs=8.5, fc=AZ), 430, 612, 270, 18)

flag = p1.add(sub('dotnet-api-platform', 'THE FLAGSHIP — TypeSpec-first modular monolith'), box(bold=True, fs=12), 410, 700, 300, 90)
p1.add('.NET 10 · Container Apps · pages 02–05 →', txt(fs=9), 430, 748, 260, 18)

p1.add('every repo ships the same guardrails: gitleaks pre-commit + security-gates in CI + approval-gated promotion', txt(fs=9, fc=GH), 70, 850, 640, 20)

ado = p1.add('Azure DevOps · org / project', cont('#FFFFFF', AZ), 800, 130, 540, 760)
p1.ic('Azure-DevOps', '', 1300, 142, 26)
wif = p1.add(sub('identity plane — WIF / OIDC', 'app reg &lt;project&gt;-ado-&lt;env&gt; + federated credential ↔ service connection sc-&lt;project&gt;-&lt;env&gt; · NO stored secrets · Contributor + UAA · sub- or rg-scoped'), box(fs=10), 830, 180, 480, 76)
p1.ic('Managed-Identities', '', 1268, 190, 34)
mirror = p1.add(sub('platform repo (imported)', 'one-shot snapshot, not a live mirror'), box(fs=10, dashed=True), 830, 286, 480, 54)
pipe = p1.add(sub('app pipeline — azure-pipelines.yml', 'resources.repositories: platform → templates @platform'), box(fs=10, bold=True), 830, 370, 480, 120)
p1.add('Build (archetype template) → security-gates → Deploy ×4', txt(fs=9), 850, 418, 440, 16)
p1.add('build once · promote byte-for-byte · approvals not branches · single main', txt(fs=8.5, fc=AZ), 850, 440, 440, 30)
vg = p1.add('variable groups: vg-&lt;app&gt;-shared · vg-&lt;app&gt;-&lt;env&gt;', chip(AZ_F, AZ, fs=9), 830, 520, 480, 24)

envs_y = 594
p1.add('<b>ADO Environments — approvals are the promotion gate</b>', txt(fs=10, fc=INK), 830, envs_y - 22, 400, 16)
env_ids = []
for i, (e, gate) in enumerate([('dev', 'auto'), ('qa', '✋ QA lead'), ('staging', '✋ tech lead'), ('prod', '✋ VP · biz-hours')]):
    env_ids.append(p1.add(sub(e, gate), box(fs=10, bold=True, fill=OK_F if e == 'dev' else '#FFFFFF',
                    stroke=OK if e == 'dev' else '#C6CDD3'), 830 + i * 122, envs_y, 112, 52))
for i in range(3):
    p1.link(env_ids[i], env_ids[i+1], '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)

agent = p1.add(sub('self-hosted agent pool', 'VNet-injected ACI · required when private-by-default (no public endpoints ⇒ MS-hosted agents can\'t route in)'), box(fs=10, dashed=True), 830, 690, 480, 64)
p1.ic('Container-Instances', '', 1268, 700, 32)
p1.add('two audit surfaces: git (who merged) + ADO (who approved each promotion)', txt(fs=9, fc=AZ), 830, 850, 480, 20)

azc = p1.add('Azure subscription', cont(AZ_F, AZ), 1400, 130, 520, 760)
p1.ic(('10002-icon-service-Subscriptions',), '', 1880, 142, 26)
shared = p1.add(sub('rg-&lt;platform&gt;-shared', 'bootstrap/main.bicep — env-invariant, idempotent re-runs'), box(fs=10, bold=True), 1430, 180, 460, 122)
p1.ic('Container-Registries', 'ACR', 1450, 228, 42, 9)
p1.ic('Log-Analytics-Workspaces', 'Log Analytics', 1535, 228, 42, 9)
p1.ic('Key-Vaults', 'Key Vault', 1632, 228, 42, 9)
p1.add('~17 resource providers<br>registered up-front', txt(fs=8.5), 1712, 232, 170, 36)
reg = p1.add(sub('Bicep module registry', 'br:&lt;acr&gt;.azurecr.io/bicep/modules/app-service:v1.2.0 — versioned pinning'), box(fs=10, dashed=True, stroke=STUB), 1430, 336, 460, 56)
rgs = p1.add(sub('per-environment resource groups', 'rg-&lt;app&gt;-&lt;env&gt; — created by app infra pipelines'), box(fs=10), 1430, 428, 460, 106)
for i, e in enumerate(['dev', 'qa', 'staging', 'prod']):
    p1.ic('Resource-Groups', f'rg-app-{e}', 1452 + i * 110, 476, 36, 9)
net = p1.add(sub('private-by-default mode', 'VNet + private endpoints + private DNS — SQL/KV/App Svc lose public ingress'), box(fs=10, dashed=True), 1430, 570, 460, 62)
p1.ic('Virtual-Networks', '', 1845, 580, 34)
p1.add('governance: PR required on modules · what-if against a consumer repo before merge · deprecate, never remove', txt(fs=9, fc=AZ), 1430, 850, 460, 30)

# edges — top control-plane corridor (staggered y)
p1.link(eng, shared, '①', E_REQ, sx=0.45, sy=0, tx=0.35, ty=0, pts=[(205, 96), (1590, 96)])
p1.link(eng, wif, '②', E_REQ, sx=0.6, sy=0, tx=0.4, ty=0, pts=[(250, 104), (1022, 104)])
p1.link(eng, vg, '③', E_REQ, sx=0.75, sy=0, tx=0.15, ty=0, pts=[(295, 112), (902, 112)])
p1.link(eng, mirror, 'sync-platform-to-ado.sh', E_OPT, sx=0.9, sy=0, tx=0.75, ty=0, pts=[(340, 120), (1190, 120)])
# github internal
p1.link(fac, gen, 'generate', E_REQ, sx=0.3, sy=1, tx=0.3, ty=0)
p1.link(gen, fac, 'cruft update ⟲', E_OPT, sx=0.75, sy=0, tx=0.75, ty=1)
p1.link(gen, eng, 'consumes ../../modules', E_REQ, sx=0, sy=0.3, tx=0.8, ty=1, pts=[(390, 579), (390, 516)])
p1.link(lib, eng, 'reference modules', E_OPT, sx=0.35, sy=0, tx=0.35, ty=1)
p1.link(flag, ghact, '', E_REQ, sx=0, sy=0.5, tx=1, ty=0.5)
# cross-boundary
p1.link(gen, pipe, 'templates @platform', E_REQ, sx=1, sy=0.5, tx=0, ty=0.35)
p1.link(flag, pipe, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.75)
p1.link(env_ids[3], rgs, 'WIF/OIDC deploys — no secrets', E_SEC, sx=1, sy=0.5, tx=0, ty=0.5, pts=[(1372, 620), (1372, 481)])
p1.link(wif, agent, 'agent PAT ← Key Vault', E_SEC, sx=1, sy=0.7, tx=1, ty=0.3, pts=[(1326, 233), (1326, 709)])
p1.link(shared, reg, 'publish (future)', E_STUB, sx=0.5, sy=1, tx=0.5, ty=0)

# ================================================================ PAGE 2
p2 = Page('02 · Runtime — the platform on Azure', 1960, 1150)
p2.title('Runtime — dotnet-api-platform on Azure',
         'Modular monolith on Container Apps · every canonical seam governed · every vendor behind the ACL · scale out before scale apart')
p2.legend(1360, 18)

cons = p2.add('Consumers', cont('#FFFFFF', GH), 40, 130, 240, 420)
c_app = p2.add(sub('internal apps & portals', 'OAuth2 client-credentials'), box(fs=10), 60, 175, 200, 56)
c_ai = p2.add(sub('AI agents', 'MCP toolset — same scopes,<br>same audit, no side door'), box(fs=10), 60, 250, 200, 66)
c_part = p2.add(sub('partners / M2M', 'Entra JWT · scoped'), box(fs=10, dashed=True), 60, 335, 200, 56)
p2.add('6 scopes: account.read · account.detailed.read · transaction.read · customer.read · contact.read · event.publish', txt(fs=9), 60, 410, 200, 90)

rg = p2.add('Azure · rg-apip-dev — single RG, scale-to-zero PoC (rung 0)', cont(AZ_F, AZ), 330, 130, 1150, 800)
cae = p2.add('Container Apps environment (Consumption · logs → Log Analytics)', cont('#FFFFFF', AZ, fs=11), 360, 175, 1090, 470)
p2.ic('Container-Apps-Environments', '', 1400, 187, 30)

api = p2.add(sub('api — Container App', ''), box(fs=12, bold=True, fill='#FDFDFE', stroke=AZ), 390, 225, 600, 390)
p2.ic('Worker-Container-App', '', 940, 235, 34)
p2.add(f'<font color="{WARN}"><b>⤢ 0→2 replicas · KEDA · multiple revisions · blue/green traffic split</b></font>', txt(fs=9), 410, 262, 560, 18)
layers = [
    ('ApiPlatform.Api', 'endpoints /v1/accounts /v1/customers /touch /hooks · CSV formatter · versioning', AZ_F, AZ),
    ('Platform.AspNetCore', 'authN ×3 · scope policies · idempotency · RFC 9457 problem-details', GOV_F, GOV),
    ('Platform', 'audit · PII redaction · Result&lt;T&gt; · scopes · governance proxy core', GOV_F, GOV),
    ('Contracts', 'canonical Account · Customer · WorkItem · Insights (leaf — depends on nothing)', OK_F, OK),
]
for i, (t, s, f, st) in enumerate(layers):
    p2.add(f'<b>{t}</b> — <font color="{MUT}" style="font-size:9px">{s}</font>',
        box(fill=f, stroke=st, fs=10, align='left'), 410, 290 + i * 44, 340, 36)
integ = p2.add(sub('ApiPlatform.Integration — the ACL', 'anti-corruption layer · connectors self-register · only place raw HttpClient/SqlClient is legal (RS0030)'), box(fs=10, bold=True, fill=WARN_F, stroke=WARN), 410, 472, 560, 130)
for i, (c, state, dash) in enumerate([('CoreBanking', 'stub', 0), ('Cards', 'stub', 0), ('Plaid', 'opt', 1),
                                       ('ClickUp', 'stub|live', 1), ('Databricks', 'stub|live', 1)]):
    p2.add(f'<b>{c}</b><br><font style="font-size:8px">{state}</font>',
        chip('#FFFFFF', WARN, fs=9, dashed=dash), 425 + i * 108, 530, 100, 34)
p2.add('RoutingAccountSource aggregates vendors in order — any vendor throw ⇒ 502/503, no partial results', txt(fs=8.5), 425, 572, 530, 24)

mcp = p2.add(sub('mcp', 'governed agent toolset — scope-gate →<br>handler → PII mask → audit'), box(fs=10, bold=True, fill=GOV_F, stroke=GOV), 1030, 225, 190, 80)
poller = p2.add(sub('poller', 'Native AOT · ~6 MB chiseled image<br>creation-feed poll → masked audit'), box(fs=10, bold=True), 1030, 325, 190, 80)
p2.ic('Worker-Container-App', '', 1180, 332, 24)
evsrc = p2.add(sub('eventsource', 'work-item change feed → event sink<br>group-atomic · at-least-once'), box(fs=10, bold=True), 1030, 425, 190, 80)
p2.ic('Worker-Container-App', '', 1180, 432, 24)
apphost = p2.add(sub('AppHost (dev only)', '.NET Aspire orchestrator — api+poller+eventsource+mcp+OTLP'), box(fs=9, dashed=True), 1030, 525, 190, 60)
p2.add('workers ship the same governance core with lean OTel (Platform.Telemetry) — no ASP.NET tax', txt(fs=9, fc=GOV), 1240, 330, 190, 90)

svc_y = 690
p2.add('<b>Azure services (deployed by infra/main.bicep)</b>', txt(fs=11, fc=INK), 360, svc_y - 26, 500, 20)
acr  = p2.ic('Container-Registries', 'ACR (Basic)', 380, svc_y, 48, 10)
appi = p2.ic('Application-Insights', 'App Insights', 500, svc_y, 48, 10)
law  = p2.ic('Log-Analytics-Workspaces', 'Log Analytics', 620, svc_y, 48, 10)
egt  = p2.ic('Event-Grid-Topics', 'Event Grid topic<br>(CloudEvents 1.0)', 780, svc_y, 48, 10)
egs1 = p2.ic('Event-Grid-Subscriptions', 'sub → sink-a', 920, svc_y - 30, 40, 9)
egs2 = p2.ic('Event-Grid-Subscriptions', 'sub → sink-b', 920, svc_y + 40, 40, 9)
q1   = p2.ic('Storage-Queue', 'queue sink-a', 1040, svc_y - 30, 40, 9)
q2   = p2.ic('Storage-Queue', 'queue sink-b', 1040, svc_y + 40, 40, 9)
p2.ic(('Storage-Accounts', 'Storage-Container'), 'blob static site<br>dev portal (Redocly)', 1180, svc_y, 48, 10)
p2.add('fan-out = SNS→SQS shape: one subscription per queue · api can peek via /hooks/queues', txt(fs=8.5), 780, svc_y + 92, 420, 20)

ext = p2.add('Source systems · fictional “Northwind CU”', cont(STUB_F, STUB, dashed=True), 1510, 130, 410, 520)
for i, (v, d) in enumerate([('Core banking system', 'accounts · customers · writer seam (future endpoints)'),
                            ('Card processor', 'card accounts'),
                            ('Plaid API', 'held-away accounts · Plaid:Enabled'),
                            ('ClickUp', 'work items · ClickUp:Mode=Live'),
                            ('Databricks SQL', 'insights · Mode=Live + ConnectionString')]):
    p2.add(sub(v, d), box(fs=10, dashed=True, stroke=STUB), 1535, 180 + i * 90, 360, 70)

fut = p2.add('Optional & future rungs — pre-cut seams, dashed on purpose · climb signals read from App Insights (p95 by operation · dependency latency by vendor · CPU/concurrency)',
             cont('#FFFFFF', STUB, dashed=True), 330, 970, 1590, 150)
fx = 360
for keys, lab, subl in [
    ('API-Management-Services', 'APIM · rung 3', 'per-consumer quota, keys'),
    ('Azure-Service-Bus', 'Service Bus', 'code-ready (Eventing:Mode) — not provisioned'),
    ('Key-Vaults', 'Key Vault', 'secrets today = env vars (PoC); MI + KV is the target'),
    ('Azure-SQL', 'Azure SQL', 'rung 5 — owned state'),
    ('Azure-Cosmos-DB', 'Cosmos DB', 'rung 5 — durable idempotency/cursors'),
    ('Cache-Redis', 'Redis', 'rung 5 — cache before vendor 429s'),
    ('AI-Studio', 'AI Foundry', 'agents consume MCP toolset'),
]:
    p2.ic(keys, f'{lab}<br><font style="font-size:8px" color="{MUT}">{subl}</font>', fx, 1015, 44, 9)
    fx += 225

p2.link(c_app, api, 'HTTPS · JWT · /v1/*', E_REQ, sx=1, sy=0.5, tx=0, ty=0.12)
p2.link(c_ai, mcp, 'MCP', E_GOV, sx=1, sy=0.3, tx=0.5, ty=0, pts=[(305, 270), (305, 112), (1125, 112)])
p2.link(c_part, api, '', E_OPT, sx=1, sy=0.5, tx=0, ty=0.3)
p2.link(integ, ext, 'ALL vendor traffic exits through the ACL — stub by default, config-gated live', E_STUB,
        sx=0.5, sy=1, tx=0, ty=0.6, pts=[(690, 626), (1495, 626)])
p2.link(api, egt, 'account.touched', E_REQ, sx=0.2, sy=1, tx=0.2, ty=0)
p2.link(egt, egs1, '', E_REQ, sx=1, sy=0.3, tx=0, ty=0.5)
p2.link(egt, egs2, '', E_REQ, sx=1, sy=0.7, tx=0, ty=0.5)
p2.link(egs1, q1, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p2.link(egs2, q2, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p2.link(egt, api, 'webhook /hooks/events + secret', E_OPT, sx=0.7, sy=0, tx=0.85, ty=1, pts=[(838, 655)])
p2.link(api, appi, 'OTel', E_GOV, sx=0.12, sy=1, tx=0.5, ty=0)
p2.link(appi, law, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p2.link(acr, api, 'image pull', E_OPT, sx=0.4, sy=0, tx=0.03, ty=1)
p2.link(appi, fut, 'scale signals', E_SCALE, sx=0.3, sy=1, tx=0.13, ty=0)

# ================================================================ PAGE 3
p3 = Page('03 · The Guards — anatomy of one request', 1960, 1080)
p3.title('The Guards — what fires on a single API call',
         'Ordered, verified from PlatformAspNetCoreExtensions.UsePlatform + endpoint code · every failure is RFC 9457 application/problem+json')
p3.legend(1360, 18)

G = [
    ('1', 'Ingress / TLS', 'Container Apps ingress :8080<br>non-root container', 'transport', SEC, SEC_F, ''),
    ('2', 'Exception → Problem', 'UpstreamExceptionHandler<br>maps vendor failure', 'Transient→503 · VendorError/Unauth→502<br>type=ProblemTypes.UpstreamUnavailable', SEC, SEC_F, 'Errors/UpstreamExceptionHandler.cs'),
    ('3', 'StatusCodePages', 'bare status codes become<br>problem+json bodies', 'no naked 404s', SEC, SEC_F, ''),
    ('4', 'AuthN — 3 modes', 'AUTH_MODE: Header (dev) ·<br>LocalJwt HS256 · Entra JWT bearer', 'non-dev + no key ⇒ throws at boot<br><b>fail-closed by construction</b>', SEC, SEC_F, 'PlatformAspNetCoreExtensions.cs:85-166'),
    ('5', 'AuthZ — scope policies', 'one policy per scope ·<br>RequireAuthenticatedUser + scope claim', 'missing scope ⇒ 403 problem+json', SEC, SEC_F, 'ScopePolicies.cs'),
    ('6', 'Idempotency', 'POST/PUT/PATCH + Idempotency-Key<br>principal-scoped {method}:{path}:{sub}:{key}', 'atomic begin · replay ⇒<br>Idempotency-Replayed: true', GOV, GOV_F, 'IdempotencyMiddleware.cs:48-89'),
    ('7', 'Endpoint validation', 'limit 1–200 etc.', '400 · ProblemTypes.InvalidParameter', GOV, GOV_F, 'AccountEndpoints.cs'),
    ('8', 'Scope-gated PII projection', 'account.detailed.read gates account detail ·<br>contact.read gates customer.Contact', 'PII stripped from the payload itself —<br>not just hidden in UI', GOV, GOV_F, 'AccountEndpoints.cs:62-75'),
    ('9', 'Governance proxy', 'Castle DynamicProxy on every IGovernedSource:<br>OTel span + AccessAuditRecord + arg masking', 'actor·op·resource·masked inputs·<br>outcome·traceId — every seam call', GOV, GOV_F, 'AuditInterceptor.cs:36-91'),
    ('10', 'ACL → vendor', 'RoutingAccountSource aggregates IAccountVendor ·<br>Result&lt;T&gt;/UpstreamOutcome — never partial', 'vendor throw ⇒ guard 2 catches ⇒ 502/503', WARN, WARN_F, 'RoutingAccountSource.cs:26-59'),
]
gids = []
for i, (num, name, what, fail, col, fill, cite) in enumerate(G):
    row, cx = divmod(i, 5)
    x = 60 + cx * 375 if row == 0 else 60 + (4 - cx) * 375
    y = 150 + row * 215
    b = p3.add('', box(fill='#FFFFFF', stroke=col, fs=10), x, y, 340, 170)
    p3.add(num, f'ellipse;fillColor={fill};strokeColor={col};fontColor={col};fontSize=14;fontStyle=1;html=1;', x - 14, y - 14, 34, 34)
    p3.add(f'<b style="font-size:12px">{name}</b>', txt(fs=12, fc=INK), x + 28, y + 8, 300, 22)
    p3.add(what, txt(fs=9.5, fc=INK), x + 16, y + 38, 310, 56)
    p3.add(f'<font color="{col}">{fail}</font>', txt(fs=9), x + 16, y + 96, 310, 44)
    if cite:
        p3.add(cite, txt(fs=8, fc=STUB), x + 16, y + 144, 310, 16)
    gids.append(b)
for i in range(9):
    if i == 4:
        p3.link(gids[i], gids[i+1], '', E_SEC, sx=0.5, sy=1, tx=0.5, ty=0)
    else:
        p3.link(gids[i], gids[i+1], '', E_SEC, sx=1 if i < 4 else 0, sy=0.5, tx=0 if i < 4 else 1, ty=0.5)

wh = p3.add('Webhook side door — anonymous BY DESIGN (Event Grid cannot send auth headers) … so it gets its own guards', cont(SEC_F, SEC, fs=11), 60, 590, 900, 120)
w1 = p3.add(sub('OPTIONS handshake', 'CloudEvents abuse-protection'), box(fs=9, stroke=SEC), 85, 630, 200, 50)
w2 = p3.add(sub('WEBHOOK_SECRET', 'query-key check'), box(fs=9, stroke=SEC), 315, 630, 200, 50)
w3 = p3.add(sub('subject masking', 'PII masked before audit log'), box(fs=9, stroke=SEC), 545, 630, 200, 50)
p3.link(w1, w2, '', E_SEC, sx=1, sy=0.5, tx=0, ty=0.5)
p3.link(w2, w3, '', E_SEC, sx=1, sy=0.5, tx=0, ty=0.5)
p3.add('WebhookEndpoints.cs — /hooks/events', txt(fs=8, fc=STUB), 770, 645, 170, 30)

p3.add(sub('scope vocabulary (TypeSpec @useAuth · PlatformScopes.cs)',
    'account.read · account.detailed.read (PII gate) · transaction.read ·<br>customer.read · contact.read (PII gate) · event.publish'),
    box(fs=10, stroke=SEC), 1000, 605, 900, 90)

enf = p3.add('The same rules, enforced three times — you cannot merge, run, or ship around them', cont(GOV_F, GOV, fs=12), 60, 750, 1840, 260)
p3.add(sub('COMPILE TIME — Roslyn analyzers (all severity=error)',
    'APL0001 governed source registered outside a connector module · APL0002 non-public module ·<br>'
    'APL0003 DateTime.Now (use TimeProvider) · APL0004 Console.Write (use ILogger) ·<br>'
    'APL0005 Problem() without type · RS0030 raw HttpClient/SqlConnection/DbContext outside Integration'),
    box(fs=10, stroke=GOV, align='left'), 90, 800, 570, 150)
p3.add(sub('RUNTIME — governance proxy',
    'GovernSources() wraps every IGovernedSource in Castle DynamicProxy —<br>'
    'keyed on the type relationship, not namespace convention.<br>'
    'OTel span + audit record + PII arg-masking on every call.<br>'
    'Sensitive param names (ssn·dob·email·account·member…) ⇒ redactor'),
    box(fs=10, stroke=GOV, align='left'), 690, 800, 570, 150)
p3.add(sub('TEST TIME — fitness functions',
    'NetArchTest ×9: dependency direction is law (Platform ⊬ AspNetCore, Contracts leaf…) ·<br>'
    'vendor sources internal · seams implement IGovernedSource ·<br>'
    'GoldenPathGovernanceTests (DoD): PII absent over HTTP without scope ·<br>'
    'no-scope ⇒ 401/403 · off-path hosts still audit+mask · drift gate on the spec'),
    box(fs=10, stroke=GOV, align='left'), 1290, 800, 580, 150)
p3.add('an engineer cannot add a data source that bypasses audit: the analyzer blocks the merge, the proxy wraps it anyway, the tests fail the build', txt(fs=10, fc=GOV, bold=True), 90, 960, 1500, 20)

# ================================================================ PAGE 4
p4 = Page('04 · Contract-first & CI/CD', 1960, 1100)
p4.title('Contract-first & CI/CD — TypeSpec is law, the pipeline is the courtroom',
         'spec → emit → lint → drift-gate → conformance · build once, promote byte-for-byte, approvals not branches')
p4.legend(1360, 18)

spec = p4.add('TypeSpec — single source of truth (spec/)', cont(OK_F, OK, fs=12), 40, 120, 1880, 310)
tsp = p4.add(sub('spec/*.tsp', 'main · accounts · customers · workitems ·<br>insights · events · streaming · errors · models<br>@versioned · @useAuth OAuth2 client-creds + 6 scopes'), box(fs=10, bold=True, stroke=OK), 70, 165, 300, 110)
emit_b = p4.add(sub('tsp compile', 'make spec'), box(fs=10, stroke=OK), 410, 185, 130, 60)
outp = p4.add(sub('openapi.v1.yaml (3.1) + JSON Schema', 'spec/tsp-output — generated, never hand-edited'), box(fs=10, stroke=OK), 580, 185, 300, 60)
lint = p4.add(sub('Spectral lint', 'kebab-case plural paths · /v&lt;major&gt; · operationId ·<br>4xx/5xx must be problem+json · OAuth2 + scope required'), box(fs=10, stroke=OK), 920, 175, 320, 80)
drift = p4.add(sub('DRIFT GATE', 'boots API on :5081 · captures /openapi/v1.json ·<br>diffs vs emitted spec — code ≠ contract ⇒ build fails'), box(fs=10, bold=True, fill=SEC_F, stroke=SEC), 1280, 175, 330, 80)
portal4 = p4.add(sub('Redocly portal', 'build-portal.sh → blob static site'), box(fs=10, stroke=OK), 1650, 165, 240, 56)
mock = p4.add(sub('Prism mock', 'npm run mock — consumers build before you do'), box(fs=10, stroke=OK, dashed=True), 1650, 240, 240, 56)
schemath = p4.add(sub('schemathesis', 'property-based conformance vs the spec'), box(fs=10, stroke=OK, dashed=True), 1280, 280, 330, 56)
p4.add('runtime endpoint /openapi/v1.json is mapped unconditionally —<br>so the drift gate and tests can always see the truth', txt(fs=9, fc=OK), 70, 300, 480, 34)
p4.add('legacy hand-authored openapi/*.yaml kept in _legacy/ for reference & mock', txt(fs=9), 70, 345, 480, 18)
p4.link(tsp, emit_b, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p4.link(emit_b, outp, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p4.link(outp, lint, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p4.link(lint, drift, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p4.link(drift, portal4, '', E_REQ, sx=1, sy=0.3, tx=0, ty=0.5)
p4.link(outp, mock, '', E_OPT, sx=0.5, sy=1, tx=0.5, ty=1, pts=[(730, 368), (1770, 368)])
p4.link(drift, schemath, '', E_OPT, sx=0.5, sy=1, tx=0.5, ty=0)

gha = p4.add('GitHub Actions — ci.yml (canonical for the public repo)', cont(GH_F, GH, fs=12), 40, 480, 700, 160)
s1 = p4.add(sub('sanitize', 'tools/sanitize.sh — structural regex gate:<br>no Fox/vendor identifiers may land public'), box(fs=10, bold=True, fill=SEC_F, stroke=SEC), 70, 525, 300, 80)
s2 = p4.add(sub('build-test', 'restore --locked-mode · build Release · test'), box(fs=10), 420, 535, 290, 60)
p4.link(s1, s2, 'needs: sanitize', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p4.add('the public repo is a sanitized distillation — the gate is structural, not manual review', txt(fs=9, fc=GH), 70, 612, 640, 18)

ado4 = p4.add('Azure DevOps — azure-pipelines.yml (full reference pipeline)', cont('#FFFFFF', AZ, fs=12), 40, 690, 1880, 340)
st1 = p4.add(sub('BuildTest', 'restore(locked) → build → test →<br>tsp compile → spectral → schemathesis →<br><b>drift gate</b> → publish artifact'), box(fs=10, bold=True, stroke=AZ), 70, 740, 300, 110)
st2 = p4.add(sub('Scan', ''), box(fs=10, bold=True, stroke=AZ), 410, 740, 320, 110)
p4.add('gitleaks — <b>HARD gate</b>', chip(SEC_F, SEC, fs=9), 430, 775, 130, 22)
p4.add('semgrep — advisory', chip(STUB_F, STUB, fs=9), 570, 775, 140, 22)
p4.add('trivy fs — advisory', chip(STUB_F, STUB, fs=9), 430, 805, 130, 22)
p4.add('org templates make these HARD', txt(fs=8.5), 570, 805, 150, 30)
st3 = p4.add(sub('Pack', '6 packable assemblies → NuGet feed:<br>Contracts · Platform · Platform.AspNetCore ·<br>Platform.Telemetry · ServiceDefaults · Analyzers<br>-ci.&lt;BuildId&gt; on main · clean on v* tag'), box(fs=10, bold=True, stroke=AZ), 770, 740, 340, 110)
st4 = p4.add(sub('DeployDev', 'Bicep sub-deploy → az acr build →<br>containerapp update (revision suffix) →<br>/health smoke · ADO Environment gated'), box(fs=10, bold=True, fill=OK_F, stroke=OK), 1150, 740, 320, 110)
env4 = []
for i, (e, g) in enumerate([('qa', '✋'), ('staging', '✋'), ('prod', '✋ VP')]):
    env4.append(p4.add(sub(e, g), box(fs=10, dashed=True, bold=True), 1520 + i * 132, 765, 118, 56))
nug = p4.ic('Azure-DevOps', 'Azure Artifacts (NuGet feed)', 900, 900, 44, 9)
p4.link(st1, st2, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p4.link(st2, st3, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p4.link(st3, st4, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p4.link(st3, nug, 'push', E_REQ, sx=0.5, sy=1, tx=0.5, ty=0)
p4.link(st4, env4[0], 'same artifact', E_OPT, sx=1, sy=0.5, tx=0, ty=0.5)
p4.link(env4[0], env4[1], '', E_OPT, sx=1, sy=0.5, tx=0, ty=0.5)
p4.link(env4[1], env4[2], '', E_OPT, sx=1, sy=0.5, tx=0, ty=0.5)
p4.add('platform templates consumed @platform (build-dotnet.yml, security-gates.yml, deploy-environment.yml) — a scanner added to the platform reaches every repo on its next build', txt(fs=9.5, fc=AZ), 70, 960, 1200, 20)
p4.add('consumers add the feed (nuget.config keeps nuget.org upstream), reference the 6 packages, inherit the governance — including the analyzers', txt(fs=9.5, fc=AZ), 70, 985, 1200, 20)

# ================================================================ PAGE 5
p5 = Page('05 · Scaling Ladder — scale out before scale apart', 1900, 950)
p5.title('The Scaling Ladder — SCALING.md',
         'each rung has an explicit “climb when” signal read from App Insights + Container Apps metrics · never split on RPS alone')
p5.legend(1300, 18)

R = [
    ('0', 'PoC · scale-to-zero', 'CURRENT — min 0 / max 2 replicas,<br>in-memory everything, stub vendors', 'you are here', OK, OK_F, 0),
    ('1', 'Internal GA', 'real JWT · durable audit ·<br>always-on App Insights · minReplicas 1', 'first real consumer/data ·<br>cold-start tax', AZ, AZ_F, 0),
    ('2', 'Scale OUT', 'KEDA maxReplicas + concurrency —<br><b>zero code change</b>', '&gt;70% CPU · p95 drift', AZ, AZ_F, 0),
    ('3', 'APIM gateway', 'per-consumer quota · keys · products', 'uncontrolled consumer', STUB, STUB_F, 1),
    ('4', 'Scale APART', 'split along the pre-cut ACL/domain seam →<br>2nd Container App · contracts stay byte-identical', 'team-blocking · divergent scale ·<br>blast radius · slow CI — <b>never RPS</b>', STUB, STUB_F, 1),
    ('5', 'Owned state', 'Redis cache · SQL/Cosmos ·<br>durable idempotency store', 'vendor 429s', STUB, STUB_F, 1),
    ('6', 'Multi-region / DR', 'active-passive · examiner RTO/RPO', 'regulator asks', STUB, STUB_F, 1),
]
step_w, step_h, rise = 240, 150, 85
rids = []
for i, (num, name, what, when, col, fill, dash) in enumerate(R):
    x = 60 + i * (step_w + 15)
    y = 660 - i * rise
    b = p5.add('', box(fill=fill if not dash else '#FFFFFF', stroke=col, dashed=dash, fs=10), x, y, step_w, step_h)
    p5.add(f'<b style="font-size:20px">{num}</b>', txt(fs=20, fc=col, bold=True), x + 12, y + 8, 40, 30)
    p5.add(f'<b>{name}</b>', txt(fs=12, fc=INK), x + 52, y + 12, step_w - 60, 22)
    p5.add(what, txt(fs=9, fc=INK), x + 14, y + 44, step_w - 26, 60)
    p5.add(f'<font color="{WARN}"><b>climb when:</b> {when}</font>', txt(fs=8.5), x + 14, y + 104, step_w - 26, 40)
    rids.append(b)
for i in range(6):
    p5.link(rids[i], rids[i + 1], '', E_SCALE if i >= 2 else edge(color=AZ, width=2), sx=1, sy=0.25, tx=0, ty=0.75)
appi5 = p5.ic('Application-Insights', 'App Insights — the “when” signals:<br>requests p95 by operation ·<br>dependencies by vendor · CPU/concurrency', 100, 180, 52, 10)
p5.link(appi5, rids[2], '', E_SCALE, sx=1, sy=0.35, tx=0, ty=0.3, pts=[(430, 205), (430, 535)])
p5.add('“The plan is not to be clever later. The seams are already cut —<br>Endpoints/ + Acl/ + shared Domain extract as a column;<br>the canonical Account contract never changes shape.”', txt(fs=11, fc=MUT), 60, 430, 460, 80)

# ================================================================ PAGE 00 — FUTURE STATE (icon-only)
pf = Page('00 · Future State — at a glance', 2720, 1720)
pf.title('dotnet-api-platform — future state',
         'Icon view · every arrow is a flow · boundaries are boxes · pages 01–05 zoom in, A1 holds the dense wiring')
pf.legend(2120, 18)

def corner(page, keys, x, y, size=26):
    return page.ic(keys, '', x, y, size)

# ---- platform engineers ----
f_dev = pf.ic(('10230-icon-service-Users',), 'platform<br>engineers', 70, 300, 48, 9, bold=True)

# ---- GitHub ----
f_gh = pf.add('GitHub', cont(GH_F, GH), 200, 120, 600, 500)
corner(pf, 'GITHUB', 758, 132)
repo = {}
repo_defs = [
    ('azure-platform-iac', 'engine · modules + templates', 250, 190),
    ('azure-project-starter', 'factory · cookiecutter', 440, 190),
    ('azure-iac-patterns', 'pattern library', 630, 190),
    ('dotnet-api-platform', 'flagship · TypeSpec-first', 250, 360),
    ('your-next-service', 'generated service', 440, 360),
    ('azure-ref-webapp-sql', 'reference proofs', 630, 360),
]
for nm, ds, x, y in repo_defs:
    repo[nm] = pf.ic('GITHUB', f'<b>{nm}</b><br>{ds}', x, y, 44, 9)
pf.link(f_dev, f_gh, 'git push', E_REQ, sx=1, sy=0.5, tx=0, ty=0.4)
pf.link(repo['azure-project-starter'], repo['your-next-service'], 'generate', E_REQ, sx=0.5, sy=1, tx=0.5, ty=0)
pf.link(repo['your-next-service'], repo['azure-platform-iac'], 'consumes', E_REQ, sx=0, sy=0.5, tx=0.3, ty=1, pts=[(390, 400)])

# ---- Azure DevOps ----
f_ado = pf.add('Azure DevOps', cont('#FFFFFF', AZ), 860, 120, 560, 500)
corner(pf, 'Azure-DevOps', 1378, 132)
f_mir = pf.ic('TFS-VC-Repository', 'platform repo<br>mirror', 905, 190, 44, 9)
f_pipe = pf.ic('Azure-DevOps', '<b>pipelines</b><br>build → scan → pack', 1090, 190, 48, 9)
f_feed = pf.ic('X:nuget', '<b>Azure Artifacts</b><br>NuGet ×6 + analyzers', 1280, 190, 44, 9)
f_gate = pf.ic('Microsoft-Defender-for-Cloud', 'security gates<br>gitleaks = HARD', 1090, 360, 42, 9)
f_pool = pf.ic('Managed-DevOps-Pools', 'agent pool<br>self-hosted · in VNet', 905, 360, 44, 9)
pf.link(f_mir, f_pipe, 'templates', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
pf.link(f_pipe, f_gate, '', E_SEC, sx=0.5, sy=1, tx=0.5, ty=0)
pf.link(f_pipe, f_feed, 'pack · push', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
pf.link(repo['dotnet-api-platform'], f_pipe, 'push · PR', E_REQ, sx=1, sy=0.3, tx=0, ty=0.3, pts=[(830, 385), (830, 218)])
pf.link(repo['your-next-service'], f_pipe, '', E_REQ, sx=1, sy=0.7, tx=0, ty=0.6, pts=[(838, 405), (838, 230)])
pf.link(repo['azure-platform-iac'], f_mir, 'sync', E_OPT, sx=0.6, sy=0, tx=0.5, ty=0, pts=[(290, 96), (927, 96)])

# ---- WIF ----
f_wif = pf.ic('Managed-Identities', '<b>WIF / OIDC</b><br>zero stored secrets', 1462, 300, 44, 9)
pf.link(f_pipe, f_wif, '', E_SEC, sx=1, sy=0.2, tx=0, ty=0.5, pts=[(1440, 205), (1440, 324)])

# ---- Azure subscription ----
f_az = pf.add('Azure subscription', cont(AZ_F, AZ), 1560, 120, 1120, 1200)
corner(pf, ('10002-icon-service-Subscriptions',), 2630, 132)

# shared platform RG
f_sh = pf.add('rg-platform-shared', cont('#FFFFFF', AZ, fs=11), 1600, 170, 500, 230)
corner(pf, 'Resource-Groups', 2062, 182, 22)
f_pacr = pf.ic('Container-Registries', '<b>module registry</b><br>br: · semver-pinned', 1640, 235, 46, 9)
f_pkv = pf.ic('Key-Vaults', '<b>platform secrets</b><br>agent PAT', 1810, 235, 46, 9)
f_la = pf.ic('Log-Analytics-Workspaces', '<b>central logs</b><br>every workload', 1965, 235, 46, 9)
pf.link(repo['azure-platform-iac'], f_pacr, 'publish modules', E_REQ, sx=0.3, sy=0, tx=0.4, ty=0, pts=[(263, 88), (1658, 88)])
pf.link(f_pkv, f_pool, 'agent PAT', E_SEC, sx=0.5, sy=1, tx=0.5, ty=1, pts=[(1833, 430), (927, 430)])

# release train
f_tr = pf.add('release train', cont('#FFFFFF', AZ, fs=11), 2140, 170, 500, 230)
corner(pf, 'Azure-Deployment-Environments', 2602, 182, 22)
f_art = pf.ic('X:package', '<b>artifact</b><br>immutable', 2165, 240, 40, 8.5)
f_envs = []
for i, e in enumerate(['dev', 'qa', 'staging', 'prod']):
    f_envs.append(pf.ic('Resource-Groups', f'<b>{e}</b>', 2270 + i * 92, 240, 36, 8.5))
pf.link(f_wif, f_art, 'deploy via WIF', E_SEC, sx=0.5, sy=0, tx=0.5, ty=0, pts=[(1484, 108), (2185, 108)])
pf.link(f_art, f_envs[0], '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
for i, gate in enumerate(['✋', '✋', '✋ VP']):
    pf.link(f_envs[i], f_envs[i + 1], gate, E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
pf.link(f_pacr, f_tr, 'resolve br: modules', E_OPT, sx=0.5, sy=0, tx=0.1, ty=0, pts=[(1663, 148), (2190, 148)])

# runtime (inside every environment)
f_rt = pf.add('rg-app-⟨env⟩ — inside every environment', cont('#FFFFFF', AZ, fs=11), 1600, 460, 1040, 800)
corner(pf, 'Resource-Groups', 2602, 472, 22)
f_vnet = pf.ic('Virtual-Networks', '<b>VNet</b><br>private-by-default', 2480, 540, 42, 8.5)
f_pl = pf.ic(('00427-icon-service-Private-Link',), 'private<br>endpoints', 2480, 660, 40, 8.5)
pf.link(f_tr, f_rt, 'deploys into each env', E_REQ, sx=0.5, sy=1, tx=0.75, ty=0)

f_cae = pf.add('Container Apps environment', cont('#FDFDFE', AZ, fs=10), 1780, 540, 560, 400)
corner(pf, 'Container-Apps-Environments', 2302, 552, 22)
f_api = pf.ic('Worker-Container-App', '<b>api</b><br>1→N · KEDA', 1830, 610, 46, 9)
f_mcpi = pf.ic('Worker-Container-App', '<b>mcp</b><br>agent toolset', 2010, 610, 46, 9)
f_pol = pf.ic('Worker-Container-App', '<b>poller</b><br>Native AOT', 1830, 770, 46, 9)
f_evs = pf.ic('Worker-Container-App', '<b>eventsource</b><br>change feeds', 2010, 770, 46, 9)
f_mi = pf.ic('Managed-Identities', '<b>managed identity</b><br>passwordless', 2190, 690, 44, 9)

f_ent = pf.ic(('10231-icon-service-Entra-ID-Protection',), '<b>Entra ID</b><br>tokens + scopes', 1462, 700, 46, 9)
f_apim = pf.ic('API-Management-Services', '<b>APIM</b><br>quotas · products', 1650, 700, 48, 9)
f_akv = pf.ic('Key-Vaults', '<b>app secrets</b><br>MI-only access', 2440, 800, 44, 9)
f_sql = pf.ic('Azure-SQL', '<b>owned state</b><br>MI auth', 2440, 940, 44, 9)
f_aacr = pf.ic('Container-Registries', '<b>images</b><br>MI pull', 2440, 1080, 44, 9)
f_ai = pf.ic('Application-Insights', '<b>App Insights</b><br>p95 · scale signals', 1650, 1090, 46, 9)
f_sb = pf.ic('Azure-Service-Bus', '<b>Service Bus</b><br>sessions · DLQ', 1830, 1090, 46, 9)
f_eg = pf.ic('Event-Grid-Topics', '<b>Event Grid</b><br>CloudEvents', 2010, 1090, 46, 9)
f_q = pf.ic('Storage-Queue', '<b>sink queues</b><br>fan-out ×N', 2180, 1090, 44, 9)
f_por = pf.ic(('Storage-Accounts', 'Storage-Container'), '<b>dev portal</b><br>Redocly · blob', 2350, 1090, 44, 9)

pf.link(f_vnet, f_pl, '', E_OPT, sx=0.5, sy=1, tx=0.5, ty=0)
pf.link(f_pl, f_akv, 'no public ingress', E_OPT, sx=0.5, sy=1, tx=0.5, ty=0, pts=[(2500, 780), (2462, 780)])
pf.link(f_apim, f_api, '', E_REQ, sx=1, sy=0.4, tx=0, ty=0.5)
pf.link(f_api, f_eg, 'events', E_REQ, sx=0.5, sy=1, tx=0.3, ty=0, pts=[(1853, 1010)])
pf.link(f_eg, f_q, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
pf.link(f_evs, f_sb, 'streams', E_REQ, sx=0, sy=0.8, tx=0.5, ty=0, pts=[(1853, 890)])
pf.link(f_mi, f_akv, 'secrets', E_SEC, sx=1, sy=0.5, tx=0, ty=0.3, pts=[(2400, 714), (2400, 815)])
pf.link(f_mi, f_sql, '', E_SEC, sx=1, sy=0.8, tx=0, ty=0.3, pts=[(2380, 730), (2380, 955)])
pf.link(f_aacr, f_cae, 'image pull', E_OPT, sx=0.5, sy=0, tx=1, ty=0.8)
pf.link(f_cae, f_ai, 'OTel', E_GOV, sx=0.1, sy=1, tx=0.5, ty=0)
pf.link(f_ai, f_la, '', E_GOV, sx=0.3, sy=0, tx=0.5, ty=1, pts=[(1620, 1060), (1620, 440), (1988, 440)])
pf.link(f_feed, f_cae, 'PackageReference', E_GOV, sx=0.5, sy=1, tx=0.3, ty=0, pts=[(1302, 500), (1948, 500)])
pf.link(f_feed, repo['your-next-service'], 'analyzers in the feed', E_GOV, sx=0.3, sy=1, tx=0.5, ty=1, pts=[(1290, 470), (463, 470)])
pf.link(f_pipe, f_por, 'publish docs', E_OPT, sx=0.8, sy=1, tx=0.5, ty=1, pts=[(1128, 1235), (2372, 1235)])

# consumers
f_con = pf.add('Consumers', cont('#FFFFFF', GH), 60, 620, 300, 420)
corner(pf, ('10230-icon-service-Users',), 320, 632, 22)
f_web = pf.ic(('10783-icon-service-Browser',), '<b>internal apps</b><br>OAuth2', 110, 690, 44, 9)
f_agents = pf.ic('AI-Studio', '<b>AI agents</b><br>Foundry', 110, 820, 44, 9)
f_m2m = pf.ic(('10230-icon-service-Users',), '<b>partners / M2M</b><br>scoped JWT', 110, 950, 44, 9)
for c in (f_web, f_m2m):
    pf.link(c, f_ent, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5, pts=[(1420, 724)])
pf.link(f_ent, f_apim, 'JWT', E_SEC, sx=1, sy=0.5, tx=0, ty=0.5)
pf.link(f_agents, f_mcpi, 'MCP', E_GOV, sx=1, sy=0.3, tx=0.5, ty=0, pts=[(1530, 838), (1530, 430), (2033, 430)])

# source systems
f_ext = pf.add('Source systems', cont(STUB_F, STUB, dashed=True), 60, 1180, 400, 500)
corner(pf, 'API-Connections', 420, 1192, 22)
for nm, keys, x, y in [('core banking', 'API-Connections', 110, 1250), ('card processor', 'API-Connections', 290, 1250),
                       ('Plaid', 'API-Connections', 110, 1390), ('ClickUp', 'API-Connections', 290, 1390),
                       ('Databricks', 'Azure-Databricks', 110, 1530)]:
    pf.ic(keys, f'<b>{nm}</b>', x, y, 42, 9)
pf.link(f_cae, f_ext, 'ACL connectors only', E_STUB, sx=0, sy=0.85, tx=1, ty=0.3, pts=[(540, 880), (540, 1330)])

# ================================================================ PAGE A1 — dense poster (appendix)
p0 = Page('A1 · Appendix — dense wiring view', 2680, 1800)
p0.title('dotnet-api-platform — the whole system, wired',
         'One canvas: repos → pipelines → packages → runtime → vendors · the paved road from git push to governed API call · pages 01–05 zoom into each zone')
p0.legend(2080, 18)

# ---- Zone: GitHub -------------------------------------------------
z_gh = p0.add('GitHub · github.com/jasondostal', cont(GH_F, GH), 40, 110, 880, 560)
p0.ic('GITHUB', '', 880, 122, 24)
g_eng = p0.add(sub('azure-platform-iac', 'THE ENGINE<br>21 Bicep modules · pipeline templates<br>(security-gates: gitleaks HARD) ·<br>bootstrap: onboard-subscription.sh'), box(bold=True, fs=11, stroke=GH), 70, 155, 250, 150)
g_fac = p0.add(sub('azure-project-starter', 'THE FACTORY — cookiecutter + cruft<br>6 archetypes: dotnet-api · dotnet-web ·<br>python-function · go-web · go-desktop ·<br>node-agent · toggles sql/apim/foundry/cosmos'), box(bold=True, fs=11, stroke=GH), 350, 155, 250, 150)
g_lib = p0.add(sub('module library & proofs', 'azure-iac-patterns — à-la-carte<br>azure-ref-webapp-sql — private canary<br>azure-playground — sandbox'), box(fs=10), 630, 155, 260, 150)
g_flag = p0.add(sub('dotnet-api-platform', 'THE FLAGSHIP — .NET 10 modular monolith'), box(bold=True, fs=11, stroke=AZ), 70, 350, 250, 280)
c_tsp = p0.add('spec/*.tsp — TypeSpec is law', chip(OK_F, OK, fs=9), 90, 400, 210, 26)
c_oas = p0.add('openapi 3.1 + JSON Schema<br><font style="font-size:8px">generated, never hand-edited</font>', chip(OK_F, OK, fs=9), 90, 448, 210, 34)
p0.link(c_tsp, c_oas, '', edge(color=OK, width=1.2), sx=0.5, sy=1, tx=0.5, ty=0)
p0.add('src: Api · Integration (ACL) · Mcp ·<br>Poller · EventSource · AppHost +<br>6 packable governance assemblies', txt(fs=9), 90, 495, 220, 46)
p0.add('Spectral · Redocly · Prism · Makefile', txt(fs=8.5, fc=OK), 90, 548, 220, 16)
p0.add('tools/sanitize.sh — public repo is a<br>sanitized distillation (structural gate)', txt(fs=8.5, fc=SEC), 90, 572, 220, 30)
g_gen = p0.add(sub('your-next-service', 'GENERATED — src + infra + 2 pipelines<br>+ pre-commit gitleaks'), box(dashed=True, bold=True, fs=11), 350, 350, 250, 120)
g_act = p0.add(sub('GitHub Actions', 'sanitize → build-test (locked-mode)'), box(fs=9), 630, 350, 260, 60)
p0.add('runs on dotnet-api-platform pushes', txt(fs=8.5, fc=GH), 630, 414, 260, 14)
p0.add('cruft update ⟲ rebases template drift into every generated repo', txt(fs=8.5, fc=GH), 630, 436, 260, 30)
p0.add('every repo: gitleaks pre-commit + security-gates + approval-gated promotion', txt(fs=9, fc=GH), 70, 640, 560, 18)

# ---- Zone: Azure DevOps -------------------------------------------
z_ado = p0.add('Azure DevOps · org / project', cont('#FFFFFF', AZ), 960, 110, 640, 560)
p0.ic('Azure-DevOps', '', 1560, 122, 24)
a_mir = p0.add(sub('platform repo (imported)', 'sync — one-shot snapshot'), box(fs=9, dashed=True), 990, 155, 280, 60)
a_wif = p0.add(sub('identity plane — WIF/OIDC', 'app reg + federated credential ↔ SC ·<br>NO stored secrets'), box(fs=9), 1290, 155, 280, 60)
a_pipe = p0.add(sub('azure-pipelines.yml — templates @platform', ''), box(fs=10, bold=True, stroke=AZ), 990, 240, 580, 180)
a_st = []
for i, (t, s) in enumerate([('BuildTest', 'restore(locked) →<br>test → tsp →<br>spectral → <b>drift<br>gate</b> → artifact'),
                            ('Scan', '<font color="#C50F1F"><b>gitleaks HARD</b></font> ·<br>semgrep · trivy<br>(advisory)'),
                            ('Pack', '6 NuGet pkgs<br>-ci.&lt;id&gt; on main ·<br>clean on v* tag'),
                            ('DeployDev', 'Bicep → az acr<br>build → containerapp<br>update → /health')]):
    a_st.append(p0.add(sub(t, s), box(fs=9, bold=True, stroke=AZ if i < 3 else OK, fill='#FFFFFF' if i < 3 else OK_F), 1010 + i * 140, 290, 130, 110))
for i in range(3):
    p0.link(a_st[i], a_st[i+1], '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
a_env = []
for i, (e, g) in enumerate([('dev', 'auto'), ('qa', '✋'), ('staging', '✋'), ('prod', '✋ VP')]):
    a_env.append(p0.add(sub(e, g), box(fs=9, bold=True, fill=OK_F if i == 0 else '#FFFFFF', stroke=OK if i == 0 else '#C6CDD3', dashed=i > 0), 990 + i * 140, 460, 120, 50))
for i in range(3):
    p0.link(a_env[i], a_env[i+1], '', E_OPT, sx=1, sy=0.5, tx=0, ty=0.5)
p0.add('build once · promote byte-for-byte · ADO Environment approvals, not branches', txt(fs=8.5, fc=AZ), 990, 522, 580, 16)
a_agent = p0.add(sub('self-hosted agent pool (ACI, VNet)', 'required in private-by-default mode'), box(fs=9, dashed=True), 990, 550, 280, 55)
p0.add('two audit surfaces:<br>git (merge) + ADO (approval)', txt(fs=8.5, fc=AZ), 1290, 555, 280, 34)

# ---- Zone: Azure control plane ------------------------------------
z_az = p0.add('Azure subscription — control plane', cont(AZ_F, AZ), 1640, 110, 620, 560)
p0.ic(('10002-icon-service-Subscriptions',), '', 2216, 122, 24)
x_shared = p0.add(sub('rg-&lt;platform&gt;-shared (bootstrap)', 'env-invariant · idempotent'), box(fs=10, bold=True), 1670, 155, 560, 130)
p0.ic('Container-Registries', 'platform ACR', 1690, 200, 40, 9)
p0.ic('Log-Analytics-Workspaces', 'Log Analytics', 1790, 200, 40, 9)
p0.ic('Key-Vaults', 'Key Vault', 1890, 200, 40, 9)
p0.add('~17 providers<br>pre-registered', txt(fs=8.5), 1990, 205, 120, 30)
x_reg = p0.add(sub('Bicep module registry', 'br:&lt;acr&gt;…/modules:v1.2.0 — versioned pinning'), box(fs=9, dashed=True, stroke=STUB), 1670, 310, 560, 46)
x_rgs = p0.add(sub('per-environment resource groups', 'rg-&lt;app&gt;-&lt;env&gt; — created by app infra pipelines'), box(fs=10), 1670, 380, 560, 110)
for i, e in enumerate(['dev', 'qa', 'staging', 'prod']):
    p0.ic('Resource-Groups', f'rg-app-{e}', 1695 + i * 135, 428, 34, 8)
x_net = p0.add(sub('private-by-default mode', 'VNet + private endpoints + private DNS'), box(fs=9, dashed=True), 1670, 520, 560, 50)
p0.ic('Virtual-Networks', '', 2185, 526, 30)
p0.add('runtime RG below is one of these ↓', txt(fs=8.5, fc=AZ), 1670, 590, 300, 16)

# ---- Band: NuGet governance rail ----------------------------------
rail = p0.add('Governance ships as NuGet — consumers add the feed, inherit the platform (analyzers included)', cont(GOV_F, GOV, fs=11), 700, 700, 1560, 108)
nug0 = p0.ic('Azure-DevOps', 'Azure Artifacts', 1372, 742, 38, 8)
pkg_defs = [('Contracts', 'canonical types'), ('Platform', 'governance core'), ('Platform.AspNetCore', 'web wiring'),
            ('Platform.Telemetry', 'lean OTel'), ('ServiceDefaults', 'aspire defaults'), ('Analyzers', 'Roslyn rules')]
pkg_x = [730, 940, 1150, 1560, 1770, 1980]
for (nm, ds), px in zip(pkg_defs, pkg_x):
    p0.add(f'<b>ApiPlatform.{nm}</b><br><font style="font-size:8px">{ds}</font>', chip('#FFFFFF', GOV, fs=9), px, 748, 195, 40)

# ---- Zone: Consumers ----------------------------------------------
z_con = p0.add('Consumers', cont('#FFFFFF', GH), 40, 820, 300, 480)
k_app = p0.add(sub('internal apps & portals', 'OAuth2 client-credentials'), box(fs=9), 65, 865, 250, 56)
k_ai = p0.add(sub('AI agents', 'MCP — same scopes, same audit,<br>no side door'), box(fs=9), 65, 945, 250, 64)
k_par = p0.add(sub('partners / M2M', 'Entra JWT · scoped'), box(fs=9, dashed=True), 65, 1033, 250, 56)
p0.add('6 scopes: account.read ·<br>account.detailed.read · transaction.read ·<br>customer.read · contact.read · event.publish', txt(fs=8.5), 65, 1115, 250, 60)
p0.add('10 guards fire on every call — page 03', txt(fs=8.5, fc=SEC), 65, 1190, 250, 30)

# ---- Zone: Runtime ------------------------------------------------
z_rt = p0.add('Azure · rg-apip-&lt;env&gt; — runtime (scale-to-zero PoC · rung 0)', cont(AZ_F, AZ), 380, 820, 1560, 660)
z_cae = p0.add('Container Apps environment', cont('#FFFFFF', AZ, fs=11), 410, 865, 1200, 380)
p0.ic('Container-Apps-Environments', '', 1562, 877, 26)
r_api = p0.add(sub('api — Container App', ''), box(fs=11, bold=True, fill='#FDFDFE', stroke=AZ), 480, 905, 620, 300)
p0.add(f'<font color="{WARN}"><b>⤢ 0→2 replicas · KEDA · blue/green revisions</b></font>', txt(fs=8.5), 500, 938, 560, 16)
for i, (t, f, st) in enumerate([('ApiPlatform.Api — endpoints /v1/* · /hooks', AZ_F, AZ),
                                ('Platform.AspNetCore — authN·scopes·idempotency·RFC9457', GOV_F, GOV),
                                ('Platform — audit · PII redaction · governance proxy', GOV_F, GOV),
                                ('Contracts — canonical types (leaf)', OK_F, OK)]):
    p0.add(f'<font style="font-size:9px"><b>{t.split(" — ")[0]}</b> — {t.split(" — ")[1]}</font>',
           box(fill=f, stroke=st, fs=9, align='left'), 500, 962 + i * 36, 340, 30)
r_acl = p0.add(sub('Integration — the ACL', 'connectors self-register · RS0030'), box(fs=9, bold=True, fill=WARN_F, stroke=WARN), 500, 1112, 580, 80)
for i, c in enumerate(['CoreBanking·stub', 'Cards·stub', 'Plaid·opt', 'ClickUp·live?', 'Databricks·live?']):
    p0.add(c, chip('#FFFFFF', WARN, fs=8), 515 + i * 112, 1152, 104, 26)
r_mcp = p0.add(sub('mcp', 'governed agent toolset'), box(fs=9, bold=True, fill=GOV_F, stroke=GOV), 1130, 905, 220, 60)
r_pol = p0.add(sub('poller', 'Native AOT · ~6 MB · feed → audit'), box(fs=9, bold=True), 1130, 985, 220, 60)
r_evs = p0.add(sub('eventsource', 'change feed → sink · at-least-once'), box(fs=9, bold=True), 1130, 1065, 220, 60)
r_ah = p0.add(sub('AppHost (dev)', '.NET Aspire'), box(fs=8, dashed=True), 1130, 1145, 220, 45)
p0.add('workers: same governance,<br>lean OTel — no ASP.NET tax', txt(fs=8.5, fc=GOV), 1380, 985, 200, 40)
svc0 = 1290
p0.add('<b>infra/main.bicep</b>', txt(fs=10, fc=INK), 420, svc0 - 24, 300, 16)
r_acr = p0.ic('Container-Registries', 'app ACR', 420, svc0, 44, 9)
r_ai = p0.ic('Application-Insights', 'App Insights', 560, svc0, 44, 9)
r_la = p0.ic('Log-Analytics-Workspaces', 'Log Analytics', 700, svc0, 44, 9)
r_eg = p0.ic('Event-Grid-Topics', 'Event Grid<br>(CloudEvents)', 950, svc0, 44, 9)
r_s1 = p0.ic('Event-Grid-Subscriptions', 'sub·a', 1090, svc0 - 28, 34, 8)
r_s2 = p0.ic('Event-Grid-Subscriptions', 'sub·b', 1090, svc0 + 34, 34, 8)
r_q1 = p0.ic('Storage-Queue', 'queue sink-a', 1200, svc0 - 28, 34, 8)
r_q2 = p0.ic('Storage-Queue', 'queue sink-b', 1200, svc0 + 34, 34, 8)
r_por = p0.ic(('Storage-Accounts', 'Storage-Container'), 'dev portal<br>(Redocly, blob)', 1340, svc0, 44, 9)
p0.add('fan-out: one subscription per queue (SNS→SQS shape)', txt(fs=8, fc=MUT), 950, svc0 + 92, 300, 16)

# ---- Zone: Source systems -----------------------------------------
z_ext = p0.add('Source systems · “Northwind CU” (fictional)', cont(STUB_F, STUB, dashed=True), 1980, 820, 660, 560)
for i, (v, d) in enumerate([('Core banking system', 'accounts · customers · writer seam'),
                            ('Card processor', 'card accounts'),
                            ('Plaid API', 'Plaid:Enabled'),
                            ('ClickUp', 'ClickUp:Mode=Live'),
                            ('Databricks SQL', 'Mode=Live + ConnectionString')]):
    p0.add(sub(v, d), box(fs=9, dashed=True, stroke=STUB), 2005, 865 + i * 95, 610, 75)
p0.add('stubs by default — the demo runs with zero external dependencies', txt(fs=8.5, fc=STUB), 2005, 1345, 610, 16)

# ---- Band: future shelf -------------------------------------------
z_fut = p0.add('Future rungs — pre-cut seams (climb signals: App Insights p95 · dependency latency · CPU/concurrency)', cont('#FFFFFF', STUB, dashed=True), 380, 1560, 2260, 160)
fut_icons = {}
for j, (keys, lab, ds) in enumerate([
    ('API-Management-Services', 'APIM · rung 3', 'per-consumer quota'),
    ('Key-Vaults', 'Key Vault', 'MI + KV is the target'),
    ('Azure-SQL', 'Azure SQL', 'rung 5 · owned state'),
    ('Azure-Cosmos-DB', 'Cosmos DB', 'durable idempotency'),
    ('Cache-Redis', 'Redis', 'cache before 429s'),
    ('AI-Studio', 'AI Foundry', 'agents ← MCP'),
    ('Azure-Service-Bus', 'Service Bus', 'code-ready, unprovisioned'),
]):
    fut_icons[lab] = p0.ic(keys, f'{lab}<br><font style="font-size:8px" color="{MUT}">{ds}</font>', 420 + j * 300, 1600, 42, 9)

# ---- wires ---------------------------------------------------------
# top corridor: bootstrap planes + sync
p0.link(g_eng, x_shared, '①', E_REQ, sx=0.4, sy=0, tx=0.4, ty=0, pts=[(170, 78), (1894, 78)])
p0.link(g_eng, a_wif, '②', E_REQ, sx=0.55, sy=0, tx=0.5, ty=0, pts=[(195, 86), (1430, 86)])
p0.link(g_eng, z_ado, '③ SC · var groups · envs', E_REQ, sx=0.7, sy=0, tx=0.85, ty=0, pts=[(220, 94), (1504, 94)])
p0.link(g_eng, a_mir, 'sync', E_OPT, sx=0.85, sy=0, tx=0.5, ty=0, pts=[(245, 102), (1130, 102)])
# github internal
p0.link(g_fac, g_gen, 'generate', E_REQ, sx=0.5, sy=1, tx=0.5, ty=0)
p0.link(g_gen, g_eng, 'consumes ../../modules', E_REQ, sx=0, sy=0.5, tx=1, ty=0.8, pts=[(338, 410), (338, 275)])
p0.link(g_lib, g_eng, 'reference', E_OPT, sx=0.3, sy=0, tx=0.2, ty=0, pts=[(708, 140), (120, 140)])
# repos -> pipeline
p0.link(g_flag, a_pipe, 'azure-pipelines.yml', E_REQ, sx=1, sy=0.85, tx=0, ty=0.7, pts=[(945, 588), (945, 366)])
p0.link(g_gen, a_pipe, 'same templates', E_OPT, sx=1, sy=0.5, tx=0, ty=0.4, pts=[(952, 415), (952, 312)])
# deploy + registry
p0.link(a_st[3], x_rgs, 'WIF deploy', E_SEC, sx=1, sy=0.3, tx=0, ty=0.5, pts=[(1618, 323), (1618, 435)])
p0.link(a_st[3], r_acr, 'az acr build', E_REQ, sx=1, sy=0.8, tx=0.5, ty=0, pts=[(1585, 378), (1585, 684), (444, 684)])
p0.link(r_acr, r_api, 'pull', E_OPT, sx=1, sy=0.5, tx=0, ty=0.98, pts=[(466, 1252)])
p0.link(x_shared, x_reg, 'publish (future)', E_STUB, sx=0.5, sy=1, tx=0.5, ty=0)
# packages
p0.link(a_st[2], nug0, 'push', E_REQ, sx=1, sy=0.8, tx=0.5, ty=0, pts=[(1400, 378), (1400, 690)])
p0.link(rail, r_api, 'PackageReference — hosts consume the platform', E_GOV, sx=0.25, sy=1, tx=0.8, ty=0)
p0.link(rail, g_gen, 'analyzers + governance ship in the feed', E_GOV, sx=0, sy=0.5, tx=1, ty=0.8, pts=[(910, 747), (910, 446)])
# spec truth
p0.link(c_oas, a_st[0], '', edge(color=OK, dashed=1, width=1.2), sx=1, sy=0.5, tx=0, ty=0.85, pts=[(330, 482), (958, 482), (958, 384)])
# consumers
p0.link(k_app, r_api, 'HTTPS · JWT · /v1/*', E_REQ, sx=1, sy=0.5, tx=0, ty=0.15, pts=[(420, 893), (420, 950)])
p0.link(k_ai, r_mcp, 'MCP', E_GOV, sx=1, sy=0.3, tx=0.5, ty=0, pts=[(368, 964), (368, 814), (1240, 814)])
p0.link(k_par, r_api, '', E_OPT, sx=1, sy=0.5, tx=0, ty=0.35, pts=[(430, 1061), (430, 1010)])
# runtime wiring
p0.link(r_api, r_eg, 'events', E_REQ, sx=0.6, sy=1, tx=0.3, ty=0)
p0.link(r_eg, r_s1, '', E_REQ, sx=1, sy=0.3, tx=0, ty=0.5)
p0.link(r_eg, r_s2, '', E_REQ, sx=1, sy=0.7, tx=0, ty=0.5)
p0.link(r_s1, r_q1, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p0.link(r_s2, r_q2, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
p0.link(r_eg, r_api, 'webhook /hooks + secret', E_OPT, sx=0.7, sy=0, tx=0.9, ty=1, pts=[(1005, 1240)])
p0.link(r_api, r_ai, 'OTel', E_GOV, sx=0.15, sy=1, tx=0.5, ty=0)
p0.link(r_ai, r_la, '', E_REQ, sx=1, sy=0.5, tx=0, ty=0.5)
# ACL trunk
p0.link(r_acl, z_ext, 'ALL vendor traffic — through the ACL only, stub by default', E_STUB,
        sx=0.5, sy=1, tx=0, ty=0.5, pts=[(790, 1235), (1900, 1235), (1900, 1100)])
# future stubs
p0.link(r_ai, z_fut, 'scale signals', E_SCALE, sx=0.5, sy=1, tx=0.1, ty=0)
p0.link(r_api, fut_icons['APIM · rung 3'], 'future ingress', E_STUB, sx=0, sy=0.9, tx=0.5, ty=0, pts=[(400, 1175), (400, 1560)])
p0.link(r_evs, fut_icons['Service Bus'], 'Eventing:Mode=ServiceBus', E_STUB, sx=1, sy=0.5, tx=0.5, ty=0, pts=[(1630, 1097), (1630, 1520), (2244, 1520)])

emit([pf, p1, p2, p3, p4, p5, p0], 'dotnet-api-platform-architecture.drawio')
