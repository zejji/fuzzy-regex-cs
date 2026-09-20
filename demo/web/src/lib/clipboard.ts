// Putting the snippet on the clipboard, and what to do when that is refused.
//
// `navigator.clipboard.writeText` needs a secure context and the browser's permission. It is
// missing entirely over plain HTTP and rejects with a `NotAllowedError` when writing is refused,
// and neither is something a visitor did wrong. The fallback selects the text so that the
// keyboard's own copy works, which is the one route no permission can take away.
//
// Separate from the panel because it is asynchronous and browser-shaped: the tests drive both
// outcomes through a fake, and the page only has to say which sentence to show.

/** The part of `navigator.clipboard` this uses. A whole `Clipboard` would be a fake of a fake. */
export interface ClipboardLike {
    writeText(text: string): Promise<void>;
}

/**
 * What happened, and so what the page announces: the text is on the clipboard, or it is selected
 * and waiting for the visitor's own copy.
 */
export type CopyOutcome = 'copied' | 'select';

/**
 * Copies, or selects and says so. Never throws and never rejects.
 *
 * @param text What to put on the clipboard.
 * @param fallback The element holding the same text on screen, selected when the write is refused.
 * @param clipboard Defaults to the browser's. Passed in by the tests, and absent outside a secure
 *   context - `navigator.clipboard` is undefined there rather than being a clipboard that fails.
 */
export async function copyText(
    text: string,
    fallback: HTMLElement | null,
    clipboard: ClipboardLike | undefined = navigator.clipboard,
): Promise<CopyOutcome> {
    try {
        if (clipboard === undefined) throw new Error('no clipboard in this context');
        await clipboard.writeText(text);
        return 'copied';
    } catch {
        select(fallback);
        return 'select';
    }
}

/** Selects an element's text, so that the keyboard's own copy has something to copy. */
function select(element: HTMLElement | null): void {
    const selection = window.getSelection?.();
    if (element === null || selection == null) return;

    const range = document.createRange();
    range.selectNodeContents(element);
    selection.removeAllRanges();
    selection.addRange(range);
}
