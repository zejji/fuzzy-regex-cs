/* TRE (libtre 0.8.0) probe: exact ERE battery with tre_regexec, and the fuzzy battery with
   tre_regaexec allowing only deletions. Build: gcc tre_probe.c -ltre -o tre_probe */
#include <stdio.h>
#include <string.h>
#include <tre/tre.h>
static void field(char *line, int n, char *out) { /* nth tab-separated field */
  char *p = line; for (int i = 0; i < n; i++) { p = strchr(p, '\t'); if (!p) { *out = 0; return; } p++; }
  size_t len = strcspn(p, "\t\r\n"); memcpy(out, p, len); out[len] = 0; }
int main(void) {
  char line[512], id[16], pat[128], subj[64], grp[8], full[160], nmax[8];
  FILE *f = fopen("battery.tsv", "r");
  while (fgets(line, sizeof line, f)) {
    if (line[0] == '#') continue;
    field(line, 0, id); field(line, 2, subj); field(line, 3, pat); field(line, 4, grp);
    regex_t re; regmatch_t m[4]; int g = grp[0] - '0';
    if (tre_regcomp(&re, pat, REG_EXTENDED)) { printf("tre-exact\t%s\tcompile error\n", id); continue; }
    if (tre_regexec(&re, subj, 4, m, 0)) printf("tre-exact\t%s\tnomatch\n", id);
    else if (m[g].rm_so < 0) printf("tre-exact\t%s\tspan=%d,%d g%d=unset\n", id, (int)m[0].rm_so, (int)m[0].rm_eo, g);
    else printf("tre-exact\t%s\tspan=%d,%d g%d='%.*s'\n", id, (int)m[0].rm_so, (int)m[0].rm_eo, g,
                (int)(m[g].rm_eo - m[g].rm_so), subj + m[g].rm_so);
    tre_regfree(&re);
  }
  fclose(f);
  f = fopen("fuzzy.tsv", "r");
  const char *modes[3][2] = {{"search", "%s"}, {"match", "^(%s)"}, {"fullmatch", "^(%s)$"}};
  while (fgets(line, sizeof line, f)) {
    if (line[0] == '#') continue;
    field(line, 0, id); field(line, 1, pat); field(line, 2, subj); field(line, 3, nmax);
    for (int k = 0; k < 3; k++) {
      snprintf(full, sizeof full, modes[k][1], pat);
      regex_t re; regmatch_t m[4]; regamatch_t am; regaparams_t p;
      memset(&am, 0, sizeof am); am.nmatch = 4; am.pmatch = m;
      tre_regaparams_default(&p);
      p.max_del = p.max_err = p.max_cost = nmax[0] - '0'; p.max_ins = 0; p.max_subst = 0;
      if (tre_regcomp(&re, full, REG_EXTENDED)) { printf("tre-fuzzy\t%s\t%s\tcompile error\n", id, modes[k][0]); continue; }
      if (tre_regaexec(&re, subj, &am, p, 0)) printf("tre-fuzzy\t%s %s\t%s\tnomatch\n", id, modes[k][0], full);
      else printf("tre-fuzzy\t%s %s\t%s over '%s'\tspan=%d,%d cost=%d (s,i,d)=(%d,%d,%d)\n", id, modes[k][0], full, subj,
                  (int)m[0].rm_so, (int)m[0].rm_eo, am.cost, am.num_subst, am.num_ins, am.num_del);
      tre_regfree(&re);
    }
  }
  return 0;
}
