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
import { noteButtonId, noteId, type HeadingNote } from './lib/help-notes';

withDefaults(
    defineProps<{
        note: HeadingNote;
        /** Whether this note is the open one. */
        open: boolean;
        /**
         * The element the heading row is. `legend` for the Mode fieldset, whose legend must be the
         * fieldset's first child and so cannot be wrapped in anything.
         */
        as?: 'div' | 'legend';
    }>(),
    { as: 'div' },
);

/** What the visitor did, for App.vue to answer: the four states WCAG 1.4.13 asks a note to have. */
const emit = defineEmits<{ ask: ['toggle' | 'peek' | 'unpeek' | 'follow'] }>();
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
    <p :id="noteId(note.id)" class="flag-help" :hidden="!open">
        {{ note.note }}
        <button class="note-link" type="button" @click="emit('ask', 'follow')">
            {{ note.linkText }}
        </button>
    </p>
</template>
