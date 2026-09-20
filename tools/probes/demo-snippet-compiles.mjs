// Does the C# the demo's snippet panel prints actually compile, and does it print what the page
// printed?
//
//     node tools/probes/demo-snippet-compiles.mjs            (from the repository root)
//
// S73 chunk 3. The panel is a claim about this library's API, and the only thing that can settle a
// claim about C# is a C# compiler: the literal rules in `snippet.ts` - a verbatim string with its
// quotes doubled, a raw string literal whose closing fence sets the indentation, a longer fence
// when the content holds three quotes - are language rules, and reasoning about them is how a demo
// ships a snippet nobody can paste.
//
// What it does, per case: writes the snippet EXACTLY as the panel would hand it over into a plain
// `dotnet new console` project, runs it, and prints what it printed. Cases that exercise a literal
// form append one line comparing the literal's value with the string the page holds, built by
// `JSON.stringify` rather than by `literal()`, so the check does not use the code it is checking.
//
// The project is made in the system temporary directory and not in the repository, deliberately:
// `Directory.Build.props` here turns analyzers into errors and would compile the snippet under
// rules a visitor's own project does not have. What is under test is what happens on their machine.

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const repoRoot = fileURLToPath(new URL('../..', import.meta.url));
const webRoot = join(repoRoot, 'demo/web');
const project = join(tmpdir(), 'fuzzyregex-snippet-probe');

// Vite belongs to `demo/web`, and this script lives in `tools/probes`, where a bare `import 'vite'`
// finds nothing. Resolved from the web project's own package.json instead.
const { createServer } = await import(
    pathToFileURL(createRequire(join(webRoot, 'package.json')).resolve('vite')).href
);

/** The module under test, loaded as TypeScript through Vite - the same transform the page uses. */
const server = await createServer({ root: webRoot, configFile: false, logLevel: 'error' });
const { literal, toCSharp } = await server.ssrLoadModule('/src/lib/snippet.ts');
await server.close();

const walk = (pattern, flags, subject) => ({ pattern, flags, subject, mode: '', replacement: '', namedLists: '' });

/**
 * A C# string literal built the boring way, to compare a clever one against.
 *
 * JSON.stringify writes everything C# writes the same way, bar the three line terminators C# has
 * and JSON has not: U+0085, U+2028 and U+2029 are ordinary characters inside a JSON string and end
 * a line inside a C# one (`error CS1010: Newline in constant`). Spelt out here so that the
 * comparison literal compiles - it is still not built by the code under test.
 */
const plain = (value) =>
    JSON.stringify(value).replaceAll(
        /[\u{85}\u{2028}\u{2029}]/gu,
        (char) => `\\u${char.charCodeAt(0).toString(16).padStart(4, '0')}`,
    );

// The last case's subject holds every shape the raw-string rules have to get right at once: a
// blank line, a whitespace-only line, a run of three quotes, and a trailing newline.
const awkward = 'one\n\n   \nsay """this"""\n';

// Every line terminator a raw string literal cannot carry. C# ends a line on all four of these, so
// a raw literal built by splitting on them hands the compiler a different string: this case is what
// says the escaped form gives the value back and the raw one did not.
const hostile = 'a\r\nb\u{2028}c\u{2029}d\u0085e';

const cases = [
    { name: 'the default case, as the panel hands it over', inputs: walk('(?:colour){e<=2}', '', 'the color of the collar') },
    {
        name: 'two flags, named in full',
        inputs: walk('(foobar){e}', 'BestMatch, IgnoreCase', 'xirefoabralfobarxie'),
    },
    {
        name: 'partial',
        inputs: { ...walk('\\d{4}-\\d{2}-\\d{2}', '', '2026-09'), mode: 'partial' },
    },
    {
        name: 'replace',
        inputs: {
            ...walk('(?<year>\\d{4})-(?<month>\\d{2})', '', '2026-09 and 1999-12'),
            mode: 'replace',
            replacement: '\\g<month>/\\g<year>',
        },
    },
    {
        name: 'named lists, as a dictionary initialiser',
        inputs: { ...walk('\\b(?:\\L<fruit>){e<=1}\\b', '', 'aple bananna cherry'), namedLists: 'fruit: apple, banana, cherry' },
    },
    {
        name: 'quotes and a trailing backslash',
        inputs: walk('say "hi"', '', 'he said "hi" then left\\'),
        check: walk('say "hi"', '', 'he said "hi" then left\\'),
    },
    {
        name: 'newlines, a blank line, a whitespace-only line and three quotes',
        inputs: walk('a', '', awkward),
        check: walk('a', '', awkward),
    },
    {
        name: 'a carriage return, U+2028, U+2029 and U+0085',
        inputs: walk('x\r\ny', '', hostile),
        check: walk('x\r\ny', '', hostile),
    },
];

if (!existsSync(project)) {
    mkdirSync(project, { recursive: true });
    run('dotnet', ['new', 'console', '-o', project]);
    run('dotnet', ['add', project, 'reference', join(repoRoot, 'src/FuzzyRegex/FuzzyRegex.csproj')]);
}

function run(command, args, cwd = repoRoot) {
    const result = spawnSync(command, args, { cwd, encoding: 'utf8' });
    return `${result.stdout ?? ''}${result.stderr ?? ''}`.trim();
}

for (const item of cases) {
    let source = toCSharp(item.inputs);

    // The literal round trip: does the string the compiler builds equal the string the page holds?
    if (item.check !== undefined) {
        source +=
            `\nConsole.WriteLine(regex.Pattern == ${plain(item.check.pattern)} ? "pattern OK" : "pattern DIFFERENT");\n` +
            `Console.WriteLine(${literal(item.check.subject, 0)} == ${plain(item.check.subject)} ? "subject OK" : "subject DIFFERENT");\n`;
    }

    writeFileSync(join(project, 'Program.cs'), source, 'utf8');
    console.log(`\n=== ${item.name}\n`);
    console.log(source);
    console.log('--- output');
    console.log(run('dotnet', ['run', '--project', project, '--nologo', '-v', 'quiet'], project));
}
