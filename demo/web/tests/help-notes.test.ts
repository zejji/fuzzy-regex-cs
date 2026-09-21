// The six heading notes as data: one per input, each pointing at a help panel that exists.
//
// `page.test.ts` covers the buttons and the tab they open. What is here is the half that can go
// wrong without a DOM: a note with no words in it, two notes sharing an id, or a link aimed at a
// section key the help generator does not write a panel for - which reaches a visitor as a press
// that opens an empty tab.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { HEADING_NOTES, noteId } from '../src/lib/help-notes';
import { violations } from './copy-rules';

/**
 * The feature keys `tools/build-demo-help.ps1` writes a panel for, read out of the script itself.
 *
 * Read rather than listed, because a listed copy is a second opinion about the same fact. The map
 * is an `[ordered]@{ ... }` literal of `key = @('### heading')` lines, so the keys are the names on
 * the left of the `=` inside it.
 *
 * A key holding a hyphen has to be quoted in PowerShell, so the quotes are optional here and the
 * name may contain one. Without that, `'fuzzy-test-set'` was invisible to this test (S75, item 3):
 * the extraction skipped it, and a note linking to it would have been reported as a link to a panel
 * the generator does not write.
 */
function generatedHelpKeys(): readonly string[] {
    const source = readFileSync(join(import.meta.dirname, '../../../tools/build-demo-help.ps1'), 'utf8');
    const map = /\$map = \[ordered\]@\{([\s\S]*?)\n\}/.exec(source)?.[1] ?? '';
    return [...map.matchAll(/^\s{4}'?([\w-]+)'?\s*=/gm)].map((match) => match[1] ?? '');
}

describe('the heading notes', () => {
    it('cover the six inputs, in the order the page shows them', () => {
        expect(HEADING_NOTES.map((note) => note.id)).toEqual([
            'pattern',
            'subject',
            'flags',
            'mode',
            'replacement',
            'named-lists',
        ]);
    });

    it('each say something, and name the heading they belong to', () => {
        for (const note of HEADING_NOTES) {
            expect(note.heading.length, note.id).toBeGreaterThan(0);
            // Two sentences at most, and enough words to be one: the owner's complaint was a
            // heading that explained nothing, and a five-word note would be the same complaint.
            expect(note.note.split(/\s+/).length, note.id).toBeGreaterThan(12);
            expect(note.linkText.length, note.id).toBeGreaterThan(8);
            expect(note.label, note.id).toContain(note.heading);
        }
    });

    it('link only to panels the help generator writes', () => {
        const generated = generatedHelpKeys();
        expect(generated).toContain('fuzzy');
        // The guard on the extraction itself: a quoted, hyphenated key is the shape it used to miss,
        // and an extraction that silently finds fewer keys makes this test pass for the wrong reason.
        expect(generated).toContain('fuzzy-test-set');
        for (const note of HEADING_NOTES) expect(generated, note.id).toContain(note.helpKey);
    });

    it('keep to the copy rules', () => {
        const broken = HEADING_NOTES.flatMap((note) =>
            [note.note, note.linkText, note.label].flatMap((text) =>
                violations(text).map((rule) => `${note.id}: [${rule.id}] "${text}"`),
            ),
        );
        expect(broken).toEqual([]);
    });

    it('build one id per note, which is what the button and the note agree on', () => {
        expect(noteId('pattern')).toBe('heading-help-pattern');
        expect(new Set(HEADING_NOTES.map((note) => noteId(note.id))).size).toBe(HEADING_NOTES.length);
    });
});
