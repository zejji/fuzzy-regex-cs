// Copying, and what happens when the browser says no.
//
// `navigator.clipboard.writeText` rejects with a `NotAllowedError` when writing is refused - no
// permission, or a call the browser did not believe came from a gesture - and it is missing
// altogether outside a secure context. Both are ordinary, neither is an error the visitor caused,
// and a button that silently does nothing is the worst of the three answers. So the failure path
// selects the text and says which keys to press, and the tests below drive both.

import { expect, test, vi } from 'vitest';

import { copyText } from '../src/lib/clipboard';

const panel = (text: string): HTMLElement => {
    const element = document.createElement('pre');
    element.textContent = text;
    document.body.append(element);
    return element;
};

const selectedText = (): string => window.getSelection()?.toString() ?? '';

test('a clipboard that takes the text says it was copied', async () => {
    const writeText = vi.fn<(text: string) => Promise<void>>().mockResolvedValue();

    await expect(copyText('FuzzyRegex regex = new(...)', panel('x'), { writeText })).resolves.toBe('copied');
    expect(writeText).toHaveBeenCalledWith('FuzzyRegex regex = new(...)');
});

test('a refused write selects the snippet instead', async () => {
    const error = new DOMException('Write permission denied.', 'NotAllowedError');
    const writeText = vi.fn<(text: string) => Promise<void>>().mockRejectedValue(error);

    await expect(copyText('the snippet', panel('the snippet'), { writeText })).resolves.toBe('select');
    expect(selectedText()).toBe('the snippet');
});

// No clipboard object at all is the insecure-context case: `navigator.clipboard` is undefined over
// plain HTTP, which is how somebody serving the built page from a file server sees it.
test('no clipboard at all falls back the same way', async () => {
    await expect(copyText('the snippet', panel('the snippet'), undefined)).resolves.toBe('select');
    expect(selectedText()).toBe('the snippet');
});

test('neither path throws, whatever the rejection carries or the page is missing', async () => {
    const writeText = vi.fn<(text: string) => Promise<void>>().mockRejectedValue(undefined);

    await expect(copyText('x', panel('x'), { writeText })).resolves.toBe('select');
    // Nothing to select is still a refusal to copy, and still not an exception: the visitor is told
    // to press the keys, which is the honest answer either way.
    await expect(copyText('x', null, { writeText })).resolves.toBe('select');
});
