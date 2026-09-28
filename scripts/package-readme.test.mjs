import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { packageReadme } from './package-readme.mjs';

const template = fs.readFileSync(new URL('../packaging/README.md', import.meta.url), 'utf8');
const installBlock = /(?<=<!-- GENERATED:LIBRARY_INSTALL_START -->)[\s\S]*?(?=<!-- GENERATED:LIBRARY_INSTALL_END -->)/;

test('package version replaces older public onboarding without changing other README content', () => {
    for (const version of ['16.0.1', '17.2.3-rc.2']) {
        for (const newline of ['\n', '\r\n']) {
            const source = template.replaceAll('\r\n', '\n').replaceAll('\n', newline);
            const stale = source.replace(installBlock, '\nInstall public version 15.0.0\n');
            const result = packageReadme(stale, version);
            assert.ok(result.includes(`dotnet add package SoundScript --version ${version}`));
            assert.ok(result.includes(`[SoundScript ${version} on NuGet](https://www.nuget.org/packages/SoundScript/${version})`));
            assert.equal(result.replace(installBlock, ''), source.replace(installBlock, ''));
            assert.equal(packageReadme(result, version), result);
        }
    }
});

test('unpublished public template still produces package-specific onboarding', () => {
    const source = template.replace(installBlock, '\nThis library is not published.\n');
    assert.ok(packageReadme(source, '16.0.1').includes('--version 16.0.1'));
});

test('invalid versions and ambiguous or missing install blocks fail closed', () => {
    for (const version of ['', 'latest', '16.0', '16.0.1+build', '16.0.1-01', '../README'])
        assert.throws(() => packageReadme(template, version), /semantic version/);
    assert.throws(() => packageReadme(template.replace('LIBRARY_INSTALL_END', 'WRONG_END'), '16.0.1'));
    assert.throws(() => packageReadme(template + template, '16.0.1'));
});

test('validate-nuget rejects stale, missing and duplicate README install versions and stale links', () => {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'soundscript-readme-test-'));
    const fixture = path.join(root, 'fixture');
    fs.mkdirSync(fixture);
    fs.writeFileSync(path.join(fixture, 'SoundScript.nuspec'), `<package><metadata>
      <id>SoundScript</id><version>16.0.1</version><description>Fixture</description>
      <authors>Test</authors><tags>test</tags><projectUrl>https://soundscript.net</projectUrl>
      <repository type="git" url="https://github.com/dharangutti/sound-script" />
      <license type="expression">MIT</license><readme>README.md</readme><icon>icon.png</icon>
    </metadata></package>`);
    fs.writeFileSync(path.join(fixture, 'icon.png'), 'fixture');
    const good = packageReadme(template, '16.0.1');
    const cases = [
        [good.replace('--version 16.0.1', '--version 15.0.0'), /README install version/],
        [good.replace(/^dotnet add package.*$/m, ''), /README install version/],
        [good + '\ndotnet add package SoundScript --version 16.0.1\n', /README install version/],
        [good.replace('[SoundScript 16.0.1 on NuGet]', '[SoundScript 16.0.0 on NuGet]'), /README NuGet link/],
        [good.replace('/SoundScript/16.0.1)', '/SoundScript/15.0.0)'), /README NuGet link/],
    ];
    // The validator intentionally retains its diagnostic workspace. Leave this
    // small fixture beside those diagnostics, including on Windows.
    for (const [index, [readme, diagnostic]] of cases.entries()) {
        fs.writeFileSync(path.join(fixture, 'README.md'), readme);
        const packagePath = path.join(root, `SoundScript.fixture-${index}.nupkg`);
        const zip = spawnSync('pwsh', ['-NoProfile', '-Command',
            'Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::CreateFromDirectory($env:README_FIXTURE, $env:README_PACKAGE)'],
            { encoding: 'utf8', env: { ...process.env, README_FIXTURE: fixture, README_PACKAGE: packagePath } });
        assert.equal(zip.status, 0, zip.stdout + zip.stderr);
        const result = spawnSync('pwsh', ['-NoProfile', '-File',
            fileURLToPath(new URL('./validate-nuget.ps1', import.meta.url)), '-PackagePath', packagePath], { encoding: 'utf8' });
        assert.equal(result.status, 1, result.stdout + result.stderr);
        assert.match(result.stdout, diagnostic);
        assert.match(result.stdout, /failed before consumer restore/);
    }
});
