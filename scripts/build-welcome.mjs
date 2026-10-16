import { mkdir, readFile, writeFile, copyFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const repo = fileURLToPath(new URL('../', import.meta.url));
const backend = new URL(process.argv[2] || 'https://app-recipelibrary.vasoftware.nl');
if (backend.protocol !== 'https:' || backend.username || backend.password || backend.pathname !== '/' || backend.search || backend.hash) {
    throw new Error('Backend must be an HTTPS origin without credentials, path, query or fragment.');
}
const output = path.resolve(repo, process.argv[3] || '.welcome-dist');
await mkdir(output, { recursive: true });
for (const name of ['index.html', 'style.css', 'staticwebapp.config.json']) {
    const source = await readFile(path.join(repo, 'src/Welcome', name), 'utf8');
    await writeFile(path.join(output, name), source.replaceAll('__BACKEND_URL__', backend.origin));
}
await copyFile(path.join(repo, 'src/Web/RecipeLibrary.Web/wwwroot/startup.js'), path.join(output, 'startup.js'));
console.log(`Welcome page built in ${output}`);
