// Renders the section objects in public/objects.
//   node tools/objects/serve.mjs, then open http://localhost:4319/
// render.html draws each object with three.js and POSTs the PNG back to public/objects/<name>.png.
// http://localhost:4319/?set=reactions renders the guestbook reactions into public/objects/reactions/<name>.png.
import { createServer } from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { join, dirname, extname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const site = join(here, '..', '..');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript' };

createServer(async (req, res) => {
  try {
    const url = new URL(req.url, 'http://localhost');
    if (req.method === 'POST' && url.pathname.startsWith('/save/')) {
      const [set, name] = url.pathname.startsWith('/save/reactions/')
        ? ['reactions', url.pathname.slice(16)] : ['', url.pathname.slice(6)];
      const dir = join(site, 'public', 'objects', set);
      const file = name.replace(/[^a-z0-9-]/g, '');
      const chunks = [];
      for await (const c of req) chunks.push(c);
      await mkdir(dir, { recursive: true });
      await writeFile(join(dir, `${file}.png`), Buffer.concat(chunks));
      res.end('ok');
      console.log('saved', set ? `${set}/${file}` : file);
      return;
    }
    const path = url.pathname === '/' ? join(here, 'render.html')
      : req.url.startsWith('/three/') ? join(site, 'node_modules', 'three', req.url.slice(7))
      : null;
    if (!path) { res.statusCode = 404; return res.end(); }
    res.setHeader('Content-Type', types[extname(path)] ?? 'application/octet-stream');
    res.end(await readFile(path));
  } catch (e) {
    res.statusCode = 500; res.end(String(e));
  }
}).listen(4319, () => console.log('object rig on http://localhost:4319/'));
