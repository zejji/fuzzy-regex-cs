# Scoped-encoding metamorphic grid. Emits rows: scoped pattern, global pattern, subject.
# Rule: a scoped (?a:X)/(?u:X) must answer exactly as the same encoding set globally.
import json, sys
atoms = [r'k', r's', r'kx', r'[a-z]', r'[k]', r'[^k]', r'[^[^k]]', r'[\p{Lu}x]', r'[k-l]', r'[[k]--[x]]',
         r'(k)\1', r'(?:k|x)', r'k+', r'k{2}', r'k?x', r'(?:kx){e<=0}', r'(?:kz){s<=1}', r'(?:k){i<=1}x',
         r'\mx', r'x\M', r'\bx', r'x\b', r'\Bx', r'.', r'x$', r'(?m)^x', r'\X', r'\w', r'\W', r'\d',
         r'ss', r'\xdf', r'\u0130', r'i', r'I', r'\u0131', r'\xe9', r'(?b)(?:kx){e<=1}', r'\L<w>', r'(?>k+)', r'(?=k)\w', r'(?<=k)x']
subjects = ['k', 'K', '\u212a', 's', '\u017f', 'kx', '\u212ax', '\u212a\u212a', 'kk', 'x', '\xe9x', 'x\xe9', 'e\u0301', '\u2028', 'x\u2028', '\u2028x',
            'ss', '\xdf', '\u1e9e', 'i', 'I', '\u0130', '\u0131', '\xe9', '\xc9', '1', '\uff19', 'k\u212a', '\u212ak', 'a', 'z']
# (scoped template, global flags)
forms = [
  ('(?i)(?a:{X})', '(?ai){X}'), ('(?a:(?i:{X}))', '(?ai){X}'), ('(?a)(?i:{X})', '(?ai){X}'),
  ('(?ai)(?u:{X})', '(?iu){X}'), ('(?a)(?iu:{X})', '(?iu){X}'), ('(?a)(?u:{X})', '(?u){X}'), ('(?a:{X})', '(?a){X}'),
  ('(?w)(?a:{X})', '(?aw){X}'), ('(?aw)(?u:{X})', '(?uw){X}'), ('(?iw)(?a:{X})', '(?aiw){X}'),
  ('(?if)(?a:{X})', '(?aif){X}'), ('(?aif)(?u:{X})', '(?uif){X}'),
  ('(?r)(?i)(?a:{X})', '(?r)(?ai){X}'), ('(?r)(?ai)(?u:{X})', '(?r)(?iu){X}'),
  ('(?i)(?a:(?s:{X}))', '(?ai){X}'), ('(?a:(?i)(?s:{X}))', '(?ai){X}'),
]
for v in ('(?V0)', '(?V1)'):
    for a in atoms:
        if v == '(?V0)' and ('--' in a or '[^[^' in a): continue
        for sf, gf in forms:
            if '(?m)' in a and '(?r)' in sf: pass
            for s in subjects:
                print(json.dumps({'s': v + sf.replace('{X}', a), 'g': v + gf.replace('{X}', a), 't': s, 'a': a, 'f': sf}))
