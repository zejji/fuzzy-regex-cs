/* TRE (libtre 0.8.0) batch runner for needed_sweeps.py: reads lines
   id <TAB> ERE <TAB> subject <TAB> max_subst <TAB> max_ins <TAB> max_del <TAB> max_err
   on stdin and prints id <TAB> nomatch | span=a,b (s,i,d)=(x,y,z).
   Build (WSL): gcc tre_batch.c -ltre -o /tmp/tre_batch */
#include <stdio.h>
#include <string.h>
#include <tre/tre.h>
int main(void) {
  char line[512], id[32], pat[256], subj[64];
  int s, i, d, e;
  while (fgets(line, sizeof line, stdin)) {
    char *f[7]; int n = 0; char *p = line;
    line[strcspn(line, "\r\n")] = 0;
    while (n < 7) { f[n++] = p; p = strchr(p, '\t'); if (!p) break; *p++ = 0; }
    if (n < 7) continue;
    snprintf(id, sizeof id, "%s", f[0]); snprintf(pat, sizeof pat, "%s", f[1]);
    snprintf(subj, sizeof subj, "%s", f[2]);
    sscanf(f[3], "%d", &s); sscanf(f[4], "%d", &i); sscanf(f[5], "%d", &d); sscanf(f[6], "%d", &e);
    regex_t re; regmatch_t m[8]; regamatch_t am; regaparams_t prm;
    memset(&am, 0, sizeof am); am.nmatch = 8; am.pmatch = m;
    tre_regaparams_default(&prm);
    prm.max_subst = s; prm.max_ins = i; prm.max_del = d; prm.max_err = e; prm.max_cost = e;
    if (tre_regcomp(&re, pat, REG_EXTENDED)) { printf("%s\tcompile error\n", id); continue; }
    if (tre_regaexec(&re, subj, &am, prm, 0)) printf("%s\tnomatch\n", id);
    else printf("%s\tspan=%d,%d (s,i,d)=(%d,%d,%d)\n", id, (int)m[0].rm_so, (int)m[0].rm_eo,
                am.num_subst, am.num_ins, am.num_del);
    tre_regfree(&re);
  }
  return 0;
}
