import http from 'node:http';
import fs from 'node:fs/promises';
const html = await fs.readFile(new URL('../docs/prototype/home-energy-prototype.html',import.meta.url));
http.createServer((_,res) => {res.writeHead(200,{'Content-Type':'text/html; charset=utf-8'});res.end(html);}).listen(3001,'127.0.0.1',() => console.log('Prototype reference: http://localhost:3001'));
