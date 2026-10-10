# A tiny world for bringing up the engine.
import sys
from gen import *

a = Room('test-a', (0, 0), region='village', hint1='Press {STRIKE} to strike.')
a.frame().door('R', 15, 20)
a.r(0, 21, 39, 22)
a.p(5, 20, '@').p(10, 20, 'G').p(16, 20, 'M').p(22, 20, 'N').p(28, 20, 'T')
a.hline(30, 35, 15).p(32, 14, '1')
a.props['npc'] = 'elder'
a.props['tablet'] = 'A line of old writing.|And another.'

b = Room('test-b', (1, 0), region='thornwood')
b.frame().door('L', 15, 20).door('R', 15, 20)
b.r(0, 21, 79 // 2, 22)
b.r(12, 21, 16, 21, '^')
b.p(6, 20, 'c').p(25, 20, 'h').p(20, 10, 'w').p(34, 20, '$')
b.r(36, 12, 38, 20, 'x')
b.p(30, 2, 'd').r(28, 1, 32, 1, '#')
b.hline(18, 24, 14)

c = Room('test-c', (2, 0), region='thornwood', boss='thornback')
c.frame().door('L', 15, 20)
c.r(0, 21, 39, 22)
c.r(2, 15, 2, 20, '!')
c.p(26, 20, 'B')
write(sys.argv[1])
