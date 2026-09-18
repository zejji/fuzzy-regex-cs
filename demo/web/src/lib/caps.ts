// Every bound the PAGE enforces, in one file, because a cap that exists in two places is a cap that
// disagrees with itself one day.
//
// The engine enforces its own (DemoEngine.cs): a cap that only exists in the UI is no cap at all
// once somebody drives the worker from the browser console. These are the page's, and they exist
// for a different reason - the engine can answer promptly and the MAIN THREAD can still die
// rendering the answer, which is the one freeze a Web Worker does nothing about.
//
// tests/FuzzyRegex.Tests/Gaps/Demo/DemoCapsTests.cs reads THIS FILE and checks the numbers against
// the engine's constants, so the two cannot drift apart unnoticed.

/**
 * The longest subject the page will send. Mirrors DemoEngine.MaxSubjectLength.
 *
 * Over this the page refuses with a message and sends nothing. It never truncates: a truncated
 * subject gives wrong answers that look right, and "matched at index 40,000" against a subject the
 * visitor can still see the rest of is a lie the demo would be telling about its own engine.
 */
export const MAX_SUBJECT_LENGTH = 100000;

/**
 * The most matches the page will draw. Deliberately far below the engine's own cap of 1,000
 * (DemoEngine.MaxMatches): that one bounds the answer on the wire, this one bounds how much of it
 * becomes DOM. The true total is always shown beside it.
 */
export const MAX_DISPLAYED_MATCHES = 200;

/**
 * The longest fragment the page will write into the address bar.
 *
 * There is no standard limit on a URL's length - RFC 7230 section 3.1.1 asks servers to accept at
 * least 8000 octets of request line and says nothing about fragments, which never reach a server at
 * all (RFC 3986 section 3.5). What does happen is that the things a link passes through - a chat
 * client, an issue tracker, a terminal - cut it silently, and a cut fragment decodes into a
 * DIFFERENT case rather than into an error. Above this the page keeps the inputs, drops the sharing
 * and says so.
 */
export const MAX_FRAGMENT_LENGTH = 8000;
