<script setup lang="ts">
// The page. Its state is demo.ts, which is testable without a DOM; what is here is layout only.
import { onMounted, proxyRefs } from 'vue';

import { spawnEngineWorker, useDemo } from './demo';
import { createPool } from './lib/pool';

const demo = useDemo();
const {
    pattern,
    flags,
    subject,
    examples,
    engine,
    engineError,
    busy,
    answer,
    failure,
    elapsedMs,
    selected,
    shareable,
    matches,
    view,
    capped,
    current,
    maxSubjectLength,
    load,
    stop,
} = demo;

onMounted(demo.initialise);

// The page's own state, for a driver to read. Same contract as S70's window.__harness: a machine
// can wait on it and read what the page believes rather than scraping rendered text.
//
// `proxyRefs` and not the value `app.mount()` returns: this is the only form that unwraps the refs
// for a reader AND writes through to them for a driver, and it says so at the assignment rather
// than depending on what mount happens to hand back for a component using `<script setup>`.
window.__demo = proxyRefs(demo);

// The two pieces checks.html needs to build a pool of its own, for the respawn measurement. It
// cannot import them: the bundle is one hashed file per build, so there is no stable module URL to
// import from. Published here rather than reached for through the bundle.
window.__demoInternals = { createPool, spawnEngineWorker };
</script>

<template>
    <div class="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
        <header class="mb-8">
            <h1 class="text-2xl font-bold tracking-tight sm:text-3xl">FuzzyRegex</h1>
            <p class="mt-2 max-w-2xl text-sm leading-relaxed text-slate-600 dark:text-slate-400">
                A C# port of Python's <code class="font-mono">regex</code> module, compiled to
                WebAssembly and running in your browser. The engine runs in a Web Worker, so a
                pattern that runs away takes the worker with it and not the page. This is v1: three
                inputs and the answers, nothing more.
            </p>
        </header>

        <div class="grid grid-cols-1 gap-8 lg:grid-cols-[minmax(0,2fr)_minmax(18rem,1fr)]">
            <main class="flex flex-col gap-6">
                <!-- One column, labels above their field, hints below it and persistent. -->
                <section class="card flex flex-col gap-4">
                    <div>
                        <label class="field-label" for="pattern">Pattern</label>
                        <input
                            id="pattern"
                            v-model="pattern"
                            class="field"
                            spellcheck="false"
                            autocapitalize="off"
                            autocomplete="off"
                        />
                    </div>

                    <div>
                        <label class="field-label" for="flags">Flags</label>
                        <input
                            id="flags"
                            v-model="flags"
                            class="field"
                            spellcheck="false"
                            autocapitalize="off"
                            autocomplete="off"
                            aria-describedby="flags-hint"
                        />
                        <p id="flags-hint" class="field-hint">
                            FuzzyRegexOptions member names, separated by commas or spaces - for
                            example <code class="font-mono">IgnoreCase, BestMatch</code>. Names only,
                            so a typo is an error rather than a different set of flags. Version1 is
                            the default.
                        </p>
                    </div>

                    <div>
                        <label class="field-label" for="subject">Subject</label>
                        <textarea
                            id="subject"
                            v-model="subject"
                            class="field min-h-28 resize-y"
                            spellcheck="false"
                            aria-describedby="subject-hint"
                        ></textarea>
                        <p id="subject-hint" class="field-hint">
                            Up to {{ maxSubjectLength.toLocaleString() }} characters. Currently
                            {{ subject.length.toLocaleString() }}.
                        </p>
                    </div>
                </section>

                <div class="status flex min-h-11 flex-wrap items-center gap-3">
                    <span v-if="engine === 'starting'" class="pill pill-busy">starting the engine...</span>
                    <span v-else-if="engine === 'failed'" class="pill pill-alert">engine failed to load</span>
                    <!--
                      `busy`, not `running`: during the 250 ms debounce nothing is with the worker yet,
                      and a pill that went back to "3 matches" in that window would be labelling the
                      previous case's answer with the current case's inputs.
                    -->
                    <span v-else-if="busy" class="pill pill-busy">matching...</span>
                    <span v-else-if="answer" class="pill">
                        {{ view.total }} match{{ view.total === 1 ? '' : 'es' }}
                        <template v-if="elapsedMs !== null">in {{ Math.round(elapsedMs) }} ms</template>
                    </span>

                    <button v-if="busy" class="button button-primary" type="button" @click="stop">Stop</button>

                    <span v-if="answer && answer.truncated" class="pill pill-alert">
                        the engine stopped early at its own cap - this is not the whole answer
                    </span>
                    <span v-if="capped" class="pill">showing the first {{ view.shown }} of {{ view.total }}</span>
                    <span v-if="!shareable" class="pill">
                        too long to put in the address bar, so this case has no link
                    </span>
                </div>

                <p
                    v-if="engine === 'failed' || failure"
                    class="rounded-md border border-red-300 bg-red-50 p-4 text-sm leading-relaxed text-red-900 dark:border-red-800 dark:bg-red-950 dark:text-red-100"
                    role="status"
                >
                    {{ engine === 'failed' ? engineError : failure }}
                </p>

                <template v-if="answer && !failure">
                    <section>
                        <h2 class="mb-2 text-sm font-semibold tracking-wide text-slate-600 uppercase dark:text-slate-400">
                            Subject
                        </h2>
                        <p class="subject-pane"><template v-for="(part, i) in view.segments" :key="i"><mark
                                    v-if="part.match !== null"
                                    class="hit"
                                    :class="{
                                        'hit-alt': part.match % 2 === 1,
                                        'hit-empty': part.text === '',
                                        'hit-current': part.match === selected,
                                    }"
                                    :aria-current="part.match === selected"
                                    :title="'match ' + (part.match + 1)"
                                    @click="selected = part.match"
                                >{{ part.text }}</mark><template v-else>{{ part.text }}</template></template></p>
                        <p v-if="view.total === 0" class="field-hint">No matches.</p>
                    </section>

                    <template v-if="matches.length">
                        <section>
                            <h2 class="mb-2 text-sm font-semibold tracking-wide text-slate-600 uppercase dark:text-slate-400">
                                Matches
                            </h2>
                            <div class="card overflow-x-auto p-0 sm:p-0">
                                <table class="data-table">
                                    <thead>
                                        <tr>
                                            <th scope="col">#</th>
                                            <th scope="col">Index</th>
                                            <th scope="col">Length</th>
                                            <th scope="col">Substitutions</th>
                                            <th scope="col">Insertions</th>
                                            <th scope="col">Deletions</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr
                                            v-for="(match, i) in matches.slice(0, view.shown)"
                                            :key="i"
                                            class="cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800"
                                            :class="{ 'font-semibold': i === selected }"
                                            @click="selected = i"
                                        >
                                            <td>{{ i + 1 }}</td>
                                            <td class="font-mono">{{ match.index }}</td>
                                            <td class="font-mono">{{ match.length }}</td>
                                            <td class="font-mono">{{ match.counts.substitutions }}</td>
                                            <td class="font-mono">{{ match.counts.insertions }}</td>
                                            <td class="font-mono">{{ match.counts.deletions }}</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                        </section>

                        <section v-if="current">
                            <h2 class="mb-2 text-sm font-semibold tracking-wide text-slate-600 uppercase dark:text-slate-400">
                                Groups in match {{ selected + 1 }}
                            </h2>
                            <div class="card overflow-x-auto p-0 sm:p-0">
                                <table class="data-table">
                                    <thead>
                                        <tr>
                                            <th scope="col">Group</th>
                                            <th scope="col">Index</th>
                                            <th scope="col">Length</th>
                                            <th scope="col">Text</th>
                                            <th scope="col">Captures</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr v-for="group in current.groups" :key="group.number">
                                            <td>
                                                {{ group.number
                                                }}<template v-if="group.name !== String(group.number)">
                                                    ({{ group.name }})</template
                                                >
                                            </td>
                                            <!--
                                              A group that did not take part shows as such and not as an
                                              empty string: those are two different answers, and showing
                                              them the same way is the confusion the demo exists to
                                              remove.
                                            -->
                                            <td v-if="!group.success" colspan="3" class="text-slate-500 dark:text-slate-400">
                                                <em>did not participate</em>
                                            </td>
                                            <template v-else>
                                                <td class="font-mono">{{ group.index }}</td>
                                                <td class="font-mono">{{ group.length }}</td>
                                                <td>
                                                    <code class="font-mono">{{
                                                        subject.slice(group.index, group.index + group.length)
                                                    }}</code>
                                                </td>
                                            </template>
                                            <td>
                                                <template v-if="group.captures.length > 1">
                                                    <code v-for="(capture, i) in group.captures" :key="i" class="font-mono"
                                                        >{{ i ? ', ' : ''
                                                        }}{{ subject.slice(capture.index, capture.index + capture.length) }}</code
                                                    >
                                                </template>
                                                <span v-else class="text-slate-500 dark:text-slate-400">-</span>
                                            </td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>
                            <p class="field-hint">
                                A repeated group keeps every capture, not only the last - as
                                <code class="font-mono">regex</code> does and Python's standard
                                <code class="font-mono">re</code> does not.
                            </p>
                        </section>
                    </template>
                </template>
            </main>

            <aside class="flex flex-col gap-3">
                <h2 class="text-sm font-semibold tracking-wide text-slate-600 uppercase dark:text-slate-400">
                    Worked examples
                </h2>
                <p class="field-hint mt-0">
                    Each one fills the three boxes. The answers are checked against Python's
                    <code class="font-mono">regex</code> module in this project's test suite.
                </p>
                <button
                    v-for="example in examples"
                    :key="example.title"
                    class="example-button"
                    type="button"
                    @click="load(example)"
                >
                    <span class="block text-sm font-semibold">{{ example.title }}</span>
                    <span class="mt-1 block text-xs leading-relaxed text-slate-600 dark:text-slate-400">
                        {{ example.note }}
                    </span>
                </button>
                <p class="field-hint">
                    The address bar always holds the current case, so a link to this page is a link
                    to what you are looking at.
                </p>
            </aside>
        </div>

        <footer class="mt-12 border-t border-slate-200 pt-4 text-xs text-slate-600 dark:border-slate-800 dark:text-slate-400">
            <a href="https://github.com/zejji/fuzzy-regex-cs">Source and documentation</a>. The
            library is a port of
            <a href="https://github.com/mrabarnett/mrab-regex">mrab-regex</a>.
        </footer>
    </div>
</template>
