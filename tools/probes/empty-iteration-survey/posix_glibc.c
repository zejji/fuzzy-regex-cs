/* glibc regcomp/regexec (POSIX ERE, leftmost-longest) on the battery's POSIX column.
   Build: gcc posix_glibc.c -o posix_glibc */
#include <stdio.h>
#include <string.h>
#include <regex.h>
#include <gnu/libc-version.h>
static void field(char *line, int n, char *out) {
  char *p = line; for (int i = 0; i < n; i++) { p = strchr(p, '\t'); if (!p) { *out = 0; return; } p++; }
  size_t len = strcspn(p, "\t\r\n"); memcpy(out, p, len); out[len] = 0; }
int main(void) {
  char line[512], id[16], pat[128], subj[64], grp[8];
  FILE *f = fopen("battery.tsv", "r");
  while (fgets(line, sizeof line, f)) {
    if (line[0] == '#') continue;
    field(line, 0, id); field(line, 2, subj); field(line, 3, pat); field(line, 4, grp);
    regex_t re; regmatch_t m[4]; int g = grp[0] - '0';
    if (regcomp(&re, pat, REG_EXTENDED)) { printf("glibc %s\t%s\tcompile error\n", gnu_get_libc_version(), id); continue; }
    if (regexec(&re, subj, 4, m, 0)) printf("glibc %s\t%s\tnomatch\n", gnu_get_libc_version(), id);
    else if (m[g].rm_so < 0) printf("glibc %s\t%s\tspan=%d,%d g%d=unset\n", gnu_get_libc_version(), id, (int)m[0].rm_so, (int)m[0].rm_eo, g);
    else printf("glibc %s\t%s\tspan=%d,%d g%d='%.*s'\n", gnu_get_libc_version(), id, (int)m[0].rm_so, (int)m[0].rm_eo, g,
                (int)(m[g].rm_eo - m[g].rm_so), subj + m[g].rm_so);
    regfree(&re);
  }
  return 0;
}
