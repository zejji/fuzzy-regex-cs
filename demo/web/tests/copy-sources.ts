/**
 * Where the demo's user-facing strings live, and how they are lifted out of the files holding them.
 *
 * Its own module, and not part of `copy.test.ts`, so the word count the slice records before and
 * after the rewrite is measured over exactly the strings the linter checks. Two extractions would
 * be two different answers to "how many words does this page ask a visitor to read".
 *
 * Out of scope on purpose:
 *   - the help panels' prose, which `tools/build-demo-help.ps1` lifts out of `docs/COMPARISON.md`.
 *     That is Phase 8's file and is fixed there.
 *   - `demo/README.md`, and every other document in the repository. A visitor to the page never
 *     reads them, the word count is a count of what the page asks of a visitor, and linting prose
 *     written for maintainers under interface-copy rules would ban the words that belong in it
 *     ("deploy", "key difference").
 */
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const read = (relative: string): string =>
    readFileSync(fileURLToPath(new URL(relative, import.meta.url)), 'utf8');

/** A string and where it came from, so a failure names the file and not only the words. */
export interface Copy {
    readonly where: string;
    readonly text: string;
}

export const tidy = (text: string): string => text.replace(/\s+/g, ' ').trim();

/**
 * Worth linting. A label in a template is one word ("Pattern") and still copy; a one-word literal
 * in a script is an identifier, a class name or a key, so those need two words to qualify.
 */
export const isProse = (text: string, words = 1): boolean =>
    /[a-z]{3}/i.test(text) && (words === 1 || /\s/.test(text));

/**
 * Comments, in both the `//` and block forms, and never inside a string.
 *
 * A scan rather than a replace, because a regex cannot tell a `//` inside a string literal from one
 * starting a comment. The guard it replaces matched a `//` and the rest of its line unless the
 * character before it was a colon, a quote or a backslash, so a `//` after a space inside a string
 * literal looked exactly like a comment: `'read the notes // and the rest'` was cut at the slashes, the
 * quote was left open, and the whole string then dropped out of the linted set rather than being
 * linted (measured 2026-09-20, blind pass finding 5).
 *
 * The scan knows one escape rule, backslash, which is what JavaScript, TypeScript and the C# in
 * `DemoEngine.cs` all use; none of the linted sources holds a verbatim (`@"..."`) or raw (`"""`)
 * string, where `""` doubles instead.
 */
const withoutComments = (source: string): string => {
    let out = '';
    let quote = '';
    for (let index = 0; index < source.length; index += 1) {
        const character = source[index] as string;

        if (quote === '') {
            if (character === '/' && source[index + 1] === '/') {
                while (index < source.length && source[index] !== '\n') index += 1;
                out += '\n';
                continue;
            }
            if (character === '/' && source[index + 1] === '*') {
                const end = source.indexOf('*/', index + 2);
                index = end === -1 ? source.length : end + 1;
                out += ' ';
                continue;
            }
            if (character === "'" || character === '"' || character === '`') quote = character;
            out += character;
            continue;
        }

        out += character;
        if (character === '\\') {
            out += source[index + 1] ?? '';
            index += 1;
        } else if (character === quote) {
            quote = '';
        } else if (character === '\n' && quote !== '`') {
            // An unterminated `'` or `"` ends at the line break, the same rule the literal pattern
            // below applies, so a stray apostrophe cannot swallow the rest of the file.
            quote = '';
        }
    }
    return out;
};

/** How a source spells an interpolation hole: `${...}` in a script, `{...}` in C#. */
export type Interpolation = 'script' | 'csharp';

const HOLES: Record<Interpolation, RegExp> = {
    script: /\$\{[\s\S]*?\}/g,
    // C# only, because `{e<=2}` in a script string is fuzzy-regex syntax the page shows a visitor,
    // not a hole. Left in, `$"{unexpected.GetType().Name}: {unexpected.Message}"` was linted as
    // prose (blind pass finding 6).
    csharp: /\{[^{}]*\}/g,
};

/**
 * Every string literal in a script, in all three quotes.
 *
 * An interpolation hole is removed for the same reason `{{ }}` is removed from a template: it is an
 * expression, and linting it means linting code. The ternary inside `markLabel` was read as a
 * rhetorical question - "? 'empty'" - until this took the interpolations out.
 */
export function literals(source: string, where: string, interpolation: Interpolation = 'script'): Copy[] {
    const found: Copy[] = [];
    const pattern = /'((?:[^'\\\n]|\\.)*)'|"((?:[^"\\\n]|\\.)*)"|`((?:[^`\\]|\\.)*)`/g;
    for (const match of withoutComments(source).matchAll(pattern)) {
        const text = tidy((match[1] ?? match[2] ?? match[3] ?? '').replace(HOLES[interpolation], ' '));
        if (isProse(text, 2)) found.push({ where, text });
    }
    return found;
}

/**
 * The prose a visitor reads in a Vue template: its text nodes, plus the attributes that are read
 * out rather than shown. Comments and `{{ }}` interpolations are removed first - a comment is for
 * whoever maintains the page, and an interpolation is an expression, not copy.
 */
export function templateStrings(source: string, where: string): Copy[] {
    const template = /<template>([\s\S]*)<\/template>/.exec(source)?.[1] ?? '';
    const body = template.replace(/<!--[\s\S]*?-->/g, ' ');

    const found: Copy[] = [];
    for (const match of body.matchAll(/\b(?:aria-label|title|placeholder)="([^"{]*)"/g)) {
        const text = tidy(match[1] ?? '');
        if (isProse(text)) found.push({ where: `${where} (attribute)`, text });
    }

    // A BOUND label is copy too: `:aria-label="'Groups in match ' + (selected + 1) + ', scrollable
    // sideways'"` is read out to a screen reader exactly as a static one is. Its value is an
    // expression, so the strings are lifted out of it the way they are lifted out of a script.
    for (const match of body.matchAll(/\s:(?:aria-label|title|placeholder)="([^"]*)"/g)) {
        found.push(...literals(match[1] ?? '', `${where} (bound attribute)`));
    }

    // Text nodes: whatever sits between tags, with the interpolations taken out. Splitting on the
    // tags rather than parsing is enough here, because the only `<` in this file's copy is escaped.
    for (const node of body.replace(/\{\{[\s\S]*?\}\}/g, ' ').split(/<[^>]*>/)) {
        const text = tidy(node);
        if (isProse(text)) found.push({ where, text });
    }
    return found;
}

const appSource = read('../src/App.vue');
const script = /<script setup lang="ts">([\s\S]*?)<\/script>/.exec(appSource)?.[1] ?? '';

const examples = JSON.parse(read('../../FuzzyRegex.Demo.Wasm/wwwroot/examples.json')) as {
    title: string;
    note: string;
}[];

const indexHtml = read('../index.html');

/** Every user-facing string in the demo, by the file it lives in. */
export const SOURCES: Record<string, Copy[]> = {
    'examples.json': examples.flatMap((example) => [
        { where: `examples.json "${example.title}"`, text: example.title },
        { where: `examples.json "${example.title}"`, text: example.note },
    ]),
    'App.vue template': templateStrings(appSource, 'App.vue'),
    'App.vue script': literals(script, 'App.vue script'),
    'demo.ts': literals(read('../src/demo.ts'), 'demo.ts'),
    // `shapes.ts` and `caps.ts` write into `failure`, which the page renders in the same
    // `role="status"` paragraph as demo.ts's own messages, so their text is copy a visitor reads.
    'src/lib': [
        ...literals(read('../src/lib/shapes.ts'), 'shapes.ts'),
        ...literals(read('../src/lib/caps.ts'), 'caps.ts'),
    ],
    // The engine's own refusals - the caps, the timeout, the named-list errors - arrive as
    // `answer.error` and land in that same paragraph. Linting the C# keeps the whole channel
    // covered: a message a visitor reads is copy wherever it is written.
    'DemoEngine.cs': literals(read('../../FuzzyRegex.Demo.Wasm/DemoEngine.cs'), 'DemoEngine.cs', 'csharp'),
    'index.html': [
        { where: 'index.html <title>', text: tidy(/<title>([^<]*)<\/title>/.exec(indexHtml)?.[1] ?? '') },
        {
            where: 'index.html description',
            text: tidy(/name="description"\s*content="([^"]*)"/.exec(indexHtml)?.[1] ?? ''),
        },
        {
            where: 'index.html <noscript>',
            text: tidy((/<noscript>([\s\S]*?)<\/noscript>/.exec(indexHtml)?.[1] ?? '').replace(/<[^>]*>/g, ' ')),
        },
    ],
    // The one piece of prose the help generator writes itself; the rest is COMPARISON.md's.
    'help generator': [...read('../../../tools/build-demo-help.ps1').matchAll(/note\s*=\s*'([^']*)'/g)].map(
        (match) => ({ where: 'tools/build-demo-help.ps1', text: match[1] ?? '' }),
    ),
};

/** Words a visitor is asked to read, counted over the same strings the linter checks. */
export const wordCount = (): Record<string, number> => {
    const counted = Object.entries(SOURCES).map(
        ([source, copy]) =>
            [source, copy.reduce((total, { text }) => total + text.split(/\s+/).filter(Boolean).length, 0)] as const,
    );
    return Object.fromEntries([...counted, ['TOTAL', counted.reduce((total, [, n]) => total + n, 0)]]);
};
