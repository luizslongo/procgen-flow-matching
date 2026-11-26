// Renderiza os diagramas Mermaid de docs/diagramas/*.mmd em SVG e PNG com Chrome/Edge headless.
// Uso: node ferramentas/diagramas.mjs [nome ...]   (MERMAID_JS=<mermaid.min.js> CHROME=<navegador>)
import { execFileSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const DIAGRAMAS = resolve(HERE, '..', 'docs', 'diagramas');
const MERMAID = process.env.MERMAID_JS ?? resolve(HERE, '..', '..', 'DSM1', 'ferramentas', 'node_modules', 'mermaid', 'dist', 'mermaid.min.js');
if (!existsSync(MERMAID)) throw new Error(`mermaid.min.js não encontrado em ${MERMAID}; defina MERMAID_JS`);

const inWsl = process.platform === 'linux' && existsSync('/mnt/c/Windows');
const nativo = (p) => (inWsl ? execFileSync('wslpath', ['-w', p]).toString().trim() : p);
const url = (p) => (inWsl ? 'file:///' + nativo(p).replace(/\\/g, '/') : 'file://' + p);
const chrome = process.env.CHROME ?? [
  '/mnt/c/Program Files/Google/Chrome/Application/chrome.exe', '/mnt/c/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser',
].find((c) => existsSync(c));
if (!chrome) throw new Error('Chrome/Edge não encontrado; defina CHROME=<caminho>');

const tmp = join(DIAGRAMAS, '.render');
rmSync(tmp, { recursive: true, force: true });
mkdirSync(join(tmp, 'perfil'), { recursive: true });
copyFileSync(MERMAID, join(tmp, 'mermaid.min.js'));
const comum = ['--headless=new', '--disable-gpu', '--no-first-run', `--user-data-dir=${nativo(join(tmp, 'perfil'))}`,
  '--virtual-time-budget=15000', '--run-all-compositor-stages-before-draw', '--hide-scrollbars'];
const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

const nomes = process.argv.slice(2).length ? process.argv.slice(2)
  : readdirSync(DIAGRAMAS).filter((f) => f.endsWith('.mmd')).map((f) => f.slice(0, -4));
for (const nome of nomes) {
  const fonte = readFileSync(join(DIAGRAMAS, `${nome}.mmd`), 'utf8');
  const pagina = join(tmp, `${nome}.html`);
  writeFileSync(pagina, `<!doctype html><html><head><meta charset="utf-8"></head><body style="margin:0;background:#fff">
<pre class="mermaid">${esc(fonte)}</pre><script src="mermaid.min.js"></script><script>
mermaid.initialize({ startOnLoad: false, theme: 'neutral', securityLevel: 'loose', fontFamily: '"Segoe UI", Calibri, Arial, sans-serif',
  themeVariables: { fontSize: '14px' }, flowchart: { htmlLabels: false, curve: 'basis' },
  sequence: { mirrorActors: false, actorMargin: 40, messageMargin: 22 } });
mermaid.run({ querySelector: 'pre.mermaid' }).then(() => {
  const o = document.createElement('script'); o.type = 'text/plain'; o.id = 'svg';
  o.textContent = new XMLSerializer().serializeToString(document.querySelector('svg')); document.body.appendChild(o); });
</script></body></html>`);
  const dom = execFileSync(chrome, [...comum, '--dump-dom', url(pagina)], { maxBuffer: 64 * 1024 * 1024 }).toString();
  const svg = dom.match(/<script type="text\/plain" id="svg">([\s\S]*?)<\/script>/)?.[1]
    ?.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&');
  if (!svg) throw new Error(`falha ao renderizar ${nome}`);
  writeFileSync(join(DIAGRAMAS, `${nome}.svg`), svg);
  const [, , w, h] = (svg.match(/viewBox="([\d.\s-]+)"/)?.[1] ?? '0 0 1200 800').trim().split(/\s+/).map(Number);
  const larg = Math.ceil(w) + 16, alt = Math.ceil(h) + 16;
  const soSvg = join(tmp, `${nome}-svg.html`);
  writeFileSync(soSvg, `<!doctype html><html><body style="margin:8px;background:#fff">${svg.replace(/style="max-width:[^"]*"/, `width="${Math.ceil(w)}" height="${Math.ceil(h)}"`)}</body></html>`);
  const png = join(DIAGRAMAS, `${nome}.png`);
  execFileSync(chrome, [...comum, '--force-device-scale-factor=2', `--window-size=${larg},${alt}`, `--screenshot=${nativo(png)}`, url(soSvg)], { stdio: 'pipe' });
  console.log(`${nome}: ${nome}.svg, ${nome}.png (${larg}×${alt} px a 2×)`);
}
rmSync(tmp, { recursive: true, force: true });
