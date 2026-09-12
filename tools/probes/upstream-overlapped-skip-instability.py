# S34 item 1 (2026-09-12): upstream's overlapped (*SKIP) scan is not a function of its inputs.
# Three different answers to one call, varying only in what runs between iterations.
import gc
import regex

p = r'(?:[^\d](*SKIP)){2,3}'
s = '\r\naabb '


def run(label, between=None, keep=False):
    c = regex.compile(p, regex.M)
    spans = []
    held = []
    for m in c.finditer(s, overlapped=True):
        spans.append(m.span())
        if keep:
            held.append(m)
        if between is not None:
            between()
    print(f"{label:28s} {spans}")


run("nothing between")
run("gc.collect() between", between=gc.collect)
run("allocation between", between=lambda: [0] * 10000)
run("keep matches alive", keep=True)
run("gc.disable() + nothing", between=gc.disable)
gc.enable()
run("str formatting between", between=lambda: "%r" % (1.5,))
run("open/close a file", between=lambda: open(__file__, "rb").close())
