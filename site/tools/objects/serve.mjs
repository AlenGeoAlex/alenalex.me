// Renders the section objects in public/objects.
//   node tools/objects/serve.mjs, then open http://localhost:4319/
// render.html draws each object with three.js and POSTs the PNG back to public/objects/<name>.png.
import { createServer } from 'node:http';
import { readFile, writeFile } from 'node:fs/promises';
import { join, dirname, extname } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const site = join(here, '..', '..');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript' };

createServer(async (req, res) => {
  try {
    if (req.method === 'POST' && req.url.startsWith('/save/')) {
      const name = req.url.slice(6).replace(/[^a-z0-9-]/g, '');
      const chunks = [];
      for await (const c of req) chunks.push(c);
      await writeFile(join(site, 'public', 'objects', `${name}.png`), Buffer.concat(chunks));
      res.end('ok');
      console.log('saved', name);
      return;
    }
    const path = req.url === '/' ? join(here, 'render.html')
      : req.url.startsWith('/three/') ? join(site, 'node_modules', 'three', req.url.slice(7))
      : null;
    if (!path) { res.statusCode = 404; return res.end(); }
    res.setHeader('Content-Type', types[extname(path)] ?? 'application/octet-stream');
    res.end(await readFile(path));
  } catch (e) {
    res.statusCode = 500; res.end(String(e));
  }
}).listen(4319, () => console.log('object rig on http://localhost:4319/'));
