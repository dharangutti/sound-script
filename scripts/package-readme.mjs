import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { markerBlocks, semver } from './docs-state.mjs';

export function packageReadme(template, version) {
    if (!semver(version) || version.includes('+')) throw new Error('Expected a NuGet semantic version without build metadata.');
    const block = markerBlocks(template, ['DOTNET_REQUIREMENT', 'LIBRARY_INSTALL'], 'packaging/README.md')
        .find(block => block.id === 'LIBRARY_INSTALL');
    const newline = template.includes('\r\n') ? '\r\n' : '\n';
    const install = [
        '', 'Install this version of the library:', '', '```bash',
        `dotnet add package SoundScript --version ${version}`, '```', '',
        `[SoundScript ${version} on NuGet](https://www.nuget.org/packages/SoundScript/${version}).`, '',
    ].join(newline);
    return template.slice(0, block.start) + install + template.slice(block.end);
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    const [version, output, ...extra] = process.argv.slice(2);
    if (!output || extra.length) throw new Error('Usage: node scripts/package-readme.mjs VERSION OUTPUT');
    const templatePath = fileURLToPath(new URL('../packaging/README.md', import.meta.url));
    if (path.resolve(output) === templatePath) throw new Error('Package README output must not overwrite the public template.');
    const result = packageReadme(fs.readFileSync(templatePath, 'utf8'), version);
    fs.mkdirSync(path.dirname(output), { recursive: true });
    fs.writeFileSync(output, result);
}
