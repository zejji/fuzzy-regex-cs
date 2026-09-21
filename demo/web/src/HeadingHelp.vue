<script setup lang="ts">
// One input heading with a `(?)` beside it, and the sentence that button opens.
//
// A component and not six copies in App.vue: the button carries four handlers, a name, an
// `aria-expanded` and an `aria-controls`, and the sentence carries the id those point at. Written
// out six times, one of them would eventually be written out wrong. The state stays in App.vue,
// which owns the one-open-at-a-time rule that the flag sentences share.
//
// No prose lives here. Every word a visitor reads comes from `HEADING_NOTES`, which the copy linter
// reads - a string typed into this template would be prose nothing lints.
import { noteButtonId, noteId, type HeadingAsk, type HeadingNote } from './lib/help-notes';

withDefaults(
    defineProps<{
        note: HeadingNote;
        /** Whether this note is the open one. */
        open: boolean;
        /**
         * Whether the visitor asked for it, rather than the note being open under the pointer or
         * the focus. A peeked note goes away the moment the focus leaves the `(?)`, which is why
         * its link is not a tab stop.
         */
        pinned?: boolean;
        /**
         * The element the heading row is. `legend` for the Mode fieldset, whose legend must be the
         * fieldset's first child and so cannot be wrapped in anything.
         */
        as?: 'div' | 'legend';
    }>(),
    { as: 'div', pinned: false },
);

/** What the visitor did, for App.vue to answer: the four states WCAG 1.4.13 asks a note to have. */
const emit = defineEmits<{ ask: [HeadingAsk] }>();
</script>

<template>
    <component :is="as" class="field-heading">
        <slot />
        <button
            :id="noteButtonId(note.id)"
            class="flag-help-button"
            type="button"
            :aria-expanded="open"
            :aria-controls="noteId(note.id)"
            :aria-label="note.label"
            @click="emit('ask', 'toggle')"
            @mouseenter="emit('ask', 'peek')"
            @mouseleave="emit('ask', 'unpeek')"
            @focus="emit('ask', 'peek')"
            @blur="emit('ask', 'unpeek')"
        >
            ?
        </button>
    </component>
    <!--
      Hidden rather than dropped, as the flag sentences are: `aria-controls` must name something
      that is in the page, and `hidden` takes it out of the accessibility tree as well as off the
      screen.
    -->
    <!--
      The note holds itself open while the pointer is on it, which with the grace in App.vue is
      WCAG 1.4.13 "Hoverable": the pointer can cross the gap from the `(?)` and read the sentence.
      Its own asks, not the button's, so that the button losing the focus or the pointer does not
      shut a sentence somebody is reading.
    -->
    <p
        :id="noteId(note.id)"
        class="flag-help"
        :hidden="!open"
        @mouseenter="emit('ask', 'peek-note')"
        @mouseleave="emit('ask', 'unpeek-note')"
    >
        {{ note.note }}
        <!--
          Out of the tab order while the note is only peeked. The `(?)` opens the note when it
          takes the focus and shuts it when it loses it, so a link that was a tab stop was chosen
          as the next stop and then removed from the page before the focus arrived - one Tab that
          landed on the document and appeared to do nothing. Measured in Chrome, 2026-09-21.
        -->
        <button
            class="note-link"
            type="button"
            :tabindex="pinned ? 0 : -1"
            @click="emit('ask', 'follow')"
        >
            {{ note.linkText }}
        </button>
    </p>
</template>
