# S58: does the benchmark filler contain a fuzzy near-occurrence of "needle" at e<=1?
# Ground truth is upstream regex, not the port under test.
import regex

sentence = "the quick brown fox jumps over the lazy dog "
filler = (sentence * ((1024 * 1024) // len(sentence) + 1))[: 1024 * 1024]

for tail in ["", "a pin in a haystack.", "a needle in a haystack."]:
    subject = filler + tail
    for budget in (1, 2):
        pat = regex.compile("(?:needle){e<=%d}" % budget)
        m = pat.search(subject)
        print(
            "tail=%-24r budget=%d -> %s"
            % (tail[:24], budget, "NO MATCH" if m is None else (m.span(), m.group()))
        )
print("filler len", len(filler))
