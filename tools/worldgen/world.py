"""HOLY PEAK - the whole mountain, room by room.

Run:  python3 world.py ../../src/HolyPeak/World/world.txt

Cell grid (x right, y down). Each cell is one screen of 40 x 23 tiles.
  village   : the foot of the mountain (row 9)         start in v-east
  thornwood : west of the village (cols 0-4)           Thornback -> Iron Grips
  abbey     : above and east of the village            Bell of Resonance, the Hollow Abbot
  frost     : above the abbey                           Pilgrim's Kite, the Frost Wyrm
  summit    : the top                                   the Bellkeeper, the great bell
"""
import sys
from gen import *

TABLETS = {
    'belfry': "The village bell was cast from the same bronze as the great bell above. "
              "When the great bell fell silent, so did this one.|We hung it here anyway. A bell remembers.",
    'smithy': "The smiths of Thornwood forged grips of iron for the abbey's bell-ringers, so they could climb the tower in winter."
              "|The last pair lies past the boar's den. The boar will not part with it.",
    'anvil': "Iron for the hands that would hold the mountain.",
    'chapter': "The Hollow Abbot rang the bells of this abbey for three hundred years."
               "|When the great bell above fell silent, he kept ringing. He rings still, for no one.",
    'crypt': "Here lies the abbey's hand-bell, cast to break the windows of the faithless."
             "|Every pane of this abbey answers to its voice.",
    'cliff': "I left my kite here. The wind wanted me to follow it.|I was afraid. Perhaps you will not be.",
    'stairs': "The keeper was made to guard the bell, and it guards it still."
              "|A bell's voice can still command it. Ring yours close, and its ward will break."
              "|And when it rings the great bell itself, do not run. Answer it.",
}

# ============================================================== VILLAGE

# The east road: where the journey starts. A grate in the overhang leads up to the abbey gate.
r = Room('v-east', (8, 9), region='village', gatekey='a-gate',
         hint1='{MOVE} to walk.   {JUMP} to jump.', hint2='Hold {JUMP} to jump higher.')
r.fill('.')
r.r(0, 0, 39, 9, '#')                       # the overhang
for y, (a, b) in enumerate([(17, 22), (16, 23), (14, 25), (12, 27), (9, 30), (6, 33)], start=4):
    r.carve(a, y, b, y)
r.carve(18, 0, 21, 9)                        # the shaft up to the abbey
r.r(18, 2, 21, 2, '|')                       # a grate, opened from above
r.r(0, 10, 0, 10, '#')
r.r(36, 10, 39, 20, '#')                     # rockfall: the road ends here
r.r(0, 21, 39, 22, '#')
r.r(8, 17, 12, 20, '=')                      # a stack of crates
r.r(24, 19, 29, 20, '#')
r.r(30, 20, 33, 20, '#')
r.p(34, 20, '@')
r.p(31, 14, '1').p(10, 13, '2')

# The village square: shrine, the peddler, Sister Ione, and a cleft too sheer to climb bare-handed.
r = Room('v-square', (6, 9), size=(2, 1), region='village', sky='yes', npc='ione-village',
         hint1='{UP} to rest at a shrine.', hint2='The cleft is too sheer to climb bare-handed.')
r.fill('.')
r.r(0, 21, 79, 22, '#')
r.r(0, 0, 0, 10, '#')
r.r(76, 0, 79, 10, '#')                      # the overhang continues from the east road
# bakehouse with a smoking chimney (an updraft for anyone with a kite)
r.over(3, 15, 12, 20, ':')
r.plat(2, 13, 14)
r.r(8, 12, 9, 13, '#')
r.over(7, 0, 10, 11, 'u')
r.p(17, 20, 'N')
# a cottage
r.over(21, 14, 30, 20, ':')
r.plat(20, 31, 13)
r.p(36, 20, 'G').p(36, 15, '1')
r.p(45, 20, 'M')
r.over(48, 15, 56, 20, ':')
r.plat(47, 57, 14)
# the cleft up to the abbey steps
r.r(59, 0, 62, 11, '#')
r.r(68, 0, 71, 11, '#')
r.plat(60, 64, 18).plat(64, 70, 15)          # a pilgrims' scaffold under the overhang
r.p(56, 12, '2')

# The west gate: the elder keeps watch over the road to Thornwood.
r = Room('v-west', (5, 9), region='village', sky='yes', npc='elder')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.r(39, 0, 39, 10, '#')
r.over(2, 10, 4, 20, ':')                    # the old palisade
r.over(5, 9, 7, 20, ':')
r.plat(6, 13, 15)
r.r(30, 19, 33, 20, '=')
r.p(25, 20, 'N')

# The belfry loft above the bakehouse, reached on the chimney's updraft.
r = Room('v-belfry', (6, 8), region='village', dark='0.35', tablet=TABLETS['belfry'])
r.fill('#')
r.carve(2, 3, 37, 9, ':')
r.carve(7, 10, 10, 22)
r.over(7, 10, 10, 22, 'u')
r.p(14, 9, 'T').p(23, 9, '$').p(31, 9, 'F')

# ============================================================== THORNWOOD

r = Room('t-edge', (4, 9), region='thornwood', sky='yes', hint1='{STRIKE} to strike.')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.r(17, 19, 24, 20, '#')
r.r(10, 18, 11, 20, '#')
r.plat(3, 9, 15)
r.p(6, 14, '$')
r.p(14, 20, 'c').p(29, 20, 'c')
r.p(34, 15, '1')

r = Room('t-brambles', (3, 9), region='thornwood', sky='yes',
         hint1='Strike {DOWN} in the air to bounce off thorns and foes.')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.r(20, 21, 26, 21, '^')                     # the bramble bed
r.carve(9, 21, 13, 22)                       # a hole down into the hollow
r.plat(29, 35, 15)
r.plat(21, 26, 11)
r.p(16, 20, 'h').p(23, 7, 'w')
r.p(23, 14, '1')

r = Room('t-smithy', (2, 9), region='thornwood', sky='yes', tablet=TABLETS['smithy'])
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.r(10, 21, 15, 21, '-')                     # rotten boards over the hollow
r.carve(10, 22, 15, 22)
r.r(0, 0, 2, 3, '#')
r.r(0, 9, 6, 20, '#')                        # the high ledge to the root bridge
r.r(10, 9, 11, 17, '#')                      # ...climbed by the chimney between
r.over(14, 11, 30, 20, ':')                  # the ruined smithy
r.r(18, 20, 20, 20, '=')                     # the anvil
r.p(24, 20, 'G').p(32, 20, 'T')
r.r(25, 0, 27, 13, '#')                      # a split trunk leads up into the canopy
r.r(34, 0, 36, 13, '#')
r.plat(28, 33, 18)

r = Room('t-hollow', (2, 10), size=(2, 1), region='thornwood', dark='0.62')
r.fill('#')
r.carve(1, 3, 78, 20, ':')
r.carve(10, 0, 15, 2)                        # up to the smithy
r.carve(49, 0, 53, 2)                        # down from the bramble path
r.carve(0, 16, 0, 20)
for y, a, b in [(18, 4, 9), (15, 11, 16), (12, 4, 9), (9, 11, 16), (6, 5, 10), (3, 9, 14)]:
    r.plat(a, b, y)
r.plat(40, 46, 16).plat(56, 62, 13)
r.r(69, 13, 78, 14, '#')                     # a sealed alcove...
r.r(78, 15, 78, 20, '#')
r.r(70, 15, 71, 20, 'x')                     # ...behind cracked stone
r.p(75, 20, 'F')
r.p(7, 17, '$')
r.p(30, 8, 'w').p(60, 7, 'w').p(24, 20, 'c').p(64, 20, 'c').p(44, 20, 'h')

r = Room('t-den', (1, 10), region='thornwood', boss='thornback', dark='0.45')
r.fill('#')
r.carve(1, 4, 38, 20, ':')
r.carve(0, 16, 0, 20).carve(39, 16, 39, 20)
r.r(1, 16, 2, 20, '!').r(37, 16, 38, 20, '!')
r.plat(7, 12, 15).plat(27, 32, 15)
r.p(20, 20, 'B')

r = Room('t-anvil', (0, 10), region='thornwood', ability='grips', dark='0.5', tablet=TABLETS['anvil'])
r.fill('#')
r.carve(1, 13, 38, 20, ':')
r.carve(39, 16, 39, 20)
r.carve(17, 0, 21, 12)
r.plat(16, 22, 18)
r.p(8, 20, 'A').p(30, 20, 'T')

r = Room('t-chimney', (0, 9), region='thornwood', dark='0.45')
r.fill('#')
r.carve(17, 9, 21, 22)
r.carve(3, 3, 38, 8, ':')
r.carve(39, 4, 39, 8)
r.p(6, 8, '$').p(28, 5, 'w')

r = Room('t-bridge', (1, 9), region='thornwood', dark='0.4')
r.fill('#')
r.carve(1, 3, 38, 20, ':')
r.carve(0, 4, 0, 8).carve(39, 4, 39, 8)
r.r(1, 9, 8, 9, '#').r(14, 9, 20, 9, '#').r(26, 9, 38, 9, '#')
r.r(1, 20, 38, 20, '^')
r.p(30, 8, 'c').p(17, 5, 'w')

r = Room('t-canopy', (2, 8), size=(2, 1), region='thornwood', sky='yes')
r.fill('.')
r.r(0, 21, 79, 22, '#')
r.carve(28, 21, 33, 22)
r.r(25, 15, 27, 20, '#').r(34, 15, 36, 20, '#')
r.r(0, 0, 0, 20, '#')
r.r(10, 15, 20, 15, '#').r(4, 11, 12, 11, '#').r(16, 8, 24, 8, '#')
r.r(40, 12, 50, 12, '#').r(52, 9, 58, 9, '#').r(57, 15, 63, 15, '#')
r.r(64, 0, 79, 20, '#')                      # the great trunk
r.carve(66, 10, 73, 14)
r.r(64, 11, 65, 14, 'b')                     # its hollow is sealed with old bell-glass
r.p(70, 14, 'H')
r.p(20, 7, '$')
r.p(30, 6, 'w').p(55, 4, 'w').p(14, 14, 'h')

# ============================================================== ABBEY

r = Room('a-steps', (7, 8), region='abbey', dark='0.45')
r.fill('#')
r.carve(23, 12, 27, 22)                      # the cleft continues up
r.carve(15, 3, 38, 11, ':')
r.carve(39, 4, 39, 8)
r.r(32, 9, 39, 11, '#')
r.p(36, 3, 's').p(18, 11, 'c')

r = Room('a-gate', (8, 8), region='abbey', dark='0.5')
r.fill('#')
r.carve(0, 4, 12, 8, ':')                    # the gallery from the steps
r.carve(4, 0, 9, 3)                          # a chimney up to the chapter house
r.carve(13, 4, 38, 20, ':')
r.carve(39, 16, 39, 20)
r.r(17, 9, 18, 17, '#')                      # pillar: climb between it and the gallery
r.carve(18, 21, 21, 22)                      # the shaft down to the east road
r.p(24, 20, 'L').p(31, 20, 'k')

r = Room('a-chapter', (8, 7), region='abbey', dark='0.35', tablet=TABLETS['chapter'])
r.fill('#')
r.carve(4, 13, 9, 22)
r.carve(1, 3, 38, 12, ':')
r.carve(39, 8, 39, 12)
r.p(20, 12, 'G').p(30, 12, 'T')

r = Room('a-cloister', (9, 7), size=(2, 1), region='abbey', dark='0.5',
         hint1='Ring the bell with {TOLL}. Resonance stones answer it for a moment.')
r.fill('#')
r.carve(1, 3, 78, 12, ':')
r.carve(0, 8, 0, 12).carve(79, 8, 79, 12)
r.carve(12, 13, 33, 19, ':')
r.r(12, 20, 33, 20, '^')
for a in (15, 21, 27):
    r.r(a, 13, a + 2, 13, 'r')
r.r(60, 13, 65, 15, 'b')                     # a bell-glass floor over the nave
r.carve(60, 16, 65, 22)
r.p(44, 12, 'k').p(54, 3, 's')
r.p(7, 9, '1')

r = Room('a-nave', (9, 8), size=(2, 1), region='abbey', dark='0.45', npc='ione-abbey')
r.fill('#')
r.carve(1, 3, 78, 20, ':')
r.carve(0, 16, 0, 20).carve(79, 16, 79, 20)
r.carve(60, 0, 65, 2)
for a in (10, 22, 34, 46):
    r.plat(a, a + 5, 17)
r.plat(4, 16, 11).plat(58, 70, 11)
r.p(6, 20, 'N').p(8, 10, '$')
r.p(30, 20, 'k').p(54, 20, 'k').p(40, 3, 's').p(70, 8, 'w')

r = Room('a-crypt', (11, 8), size=(1, 2), region='abbey', dark='0.6', ability='bell', tablet=TABLETS['crypt'])
r.fill('#')
r.carve(3, 3, 36, 43, ':')
r.carve(0, 16, 2, 20)
r.r(3, 21, 9, 22, '#')
for y, a, b in [(25, 12, 24), (29, 18, 30), (33, 8, 20), (37, 16, 28), (41, 6, 18)]:
    r.plat(a, b, y)
r.carve(0, 39, 2, 43)
r.r(1, 39, 2, 43, 'b')                       # bell-glass to the ossuary
r.p(20, 43, 'A').p(30, 43, 'T')
r.p(36, 30, 's').p(20, 30, 'w').p(12, 43, 'c')

r = Room('a-ossuary', (10, 9), region='abbey', dark='0.65')
r.fill('#')
r.carve(2, 8, 38, 20, ':')
r.carve(39, 16, 39, 20)
r.r(6, 16, 9, 20, '#')                       # the reliquary pillar
r.p(7, 15, 'H').p(24, 20, '$').p(16, 20, 'c').p(30, 8, 's')

r = Room('a-sanctum', (11, 7), region='abbey', boss='abbot', dark='0.55')
r.fill('#')
r.carve(1, 2, 38, 20, ':')
r.carve(0, 8, 0, 12)
r.r(1, 13, 6, 20, '#')
r.r(1, 8, 2, 12, '!')
r.carve(17, 0, 22, 1)
r.r(17, 1, 22, 1, '!')
r.plat(9, 14, 17).plat(26, 32, 17).plat(16, 23, 13).plat(8, 13, 9).plat(27, 32, 9).plat(16, 23, 5)
r.p(24, 20, 'B')

r = Room('a-tower', (11, 5), size=(1, 2), region='abbey', dark='0.45',
         hint1='Ring the bell in the air for one more lift.')
r.fill('#')
r.carve(4, 2, 35, 43, ':')
r.carve(17, 0, 22, 1).carve(17, 44, 22, 45)
r.plat(9, 15, 41).plat(24, 30, 41)
r.plat(4, 11, 37).plat(14, 21, 33).plat(26, 35, 29)
r.r(20, 25, 23, 25, 'r')
r.plat(10, 17, 21).plat(22, 30, 17)
r.r(14, 13, 18, 13, 'r')
r.plat(24, 33, 9).plat(12, 21, 5)
r.p(30, 28, '$').p(20, 20, 'w').p(14, 8, 'w').p(35, 30, 's')
r.p(18, 36, '1')

# ============================================================== FROST

r = Room('f-ledge', (11, 4), region='frost', sky='yes')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.carve(17, 21, 22, 22)
r.r(37, 0, 39, 20, '#')
r.r(4, 17, 9, 20, '#')
r.p(30, 20, 'G')

r = Room('f-icefall', (10, 4), region='frost', sky='yes')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.r(0, 0, 14, 3, '#')
r.r(0, 16, 12, 20, '#')
r.r(13, 19, 15, 20, '#')
r.over(29, 3, 31, 20, 'u')                   # a rising wind, for anyone who can ride it
r.r(33, 4, 38, 5, '#')
r.p(36, 3, 'H')
r.p(4, 4, 'd').p(10, 4, 'd').p(20, 20, 'h').p(26, 20, 'h')

r = Room('f-crevasse', (8, 4), size=(2, 1), region='frost', npc='ione-frost')
r.fill('.')
r.r(0, 0, 79, 3, '#')
r.r(0, 16, 23, 22, '#').r(56, 16, 79, 22, '#')
r.r(24, 21, 55, 22, '#')
r.r(24, 20, 55, 20, '^')
for y, a in [(14, 28), (12, 35), (14, 42), (13, 49)]:
    r.r(a, y, a + 2, y, '=')
r.p(31, 4, 'd').p(46, 4, 'd').p(10, 15, 'h')
r.p(68, 15, 'N').p(36, 11, '$')

r = Room('f-cliff', (7, 4), region='frost', ability='kite', tablet=TABLETS['cliff'])
r.fill('.')
r.r(0, 0, 39, 1, '#')
r.r(0, 0, 1, 9, '#').r(39, 0, 39, 3, '#')
r.r(0, 16, 39, 22, '#')
r.r(0, 10, 12, 22, '#')
r.carve(17, 0, 22, 1)
r.over(17, 0, 22, 15, 'u')
r.r(23, 13, 27, 13, '=')
r.r(13, 11, 16, 11, 'r')
r.p(6, 9, 'A').p(34, 15, 'T').p(30, 15, 'h')

r = Room('f-windway', (7, 3), region='frost', dark='0.3')
r.fill('#')
r.carve(17, 3, 22, 22)
r.over(17, 3, 22, 22, 'u')
r.carve(23, 14, 38, 20, ':')
r.carve(39, 16, 39, 20)
r.carve(10, 3, 16, 6, ':')
r.p(12, 6, '$').p(30, 16, 'w')

r = Room('f-hollow', (8, 3), region='frost', boss='wyrm', dark='0.3')
r.fill('#')
r.carve(1, 4, 38, 20)
r.carve(0, 16, 0, 20).carve(39, 16, 39, 20)
r.r(1, 16, 2, 20, '!').r(37, 16, 38, 20, '!')
r.r(7, 15, 12, 15, '=').r(27, 15, 32, 15, '=')
r.p(20, 20, 'B')

r = Room('f-ascent', (9, 3), size=(2, 1), region='frost', sky='yes',
         hint1='Updrafts lift you while your kite is open.')
r.fill('.')
r.r(0, 0, 1, 15, '#')
r.r(0, 21, 10, 22, '#')
r.r(11, 22, 69, 22, '#').r(11, 21, 69, 21, '^')
r.r(26, 12, 28, 21, '=').r(41, 10, 43, 21, '=')
r.over(16, 5, 20, 20, 'u').over(32, 3, 36, 20, 'u').over(47, 5, 51, 20, 'u').over(60, 0, 65, 20, 'u')
r.r(70, 0, 79, 22, '#')
r.p(29, 14, 's').p(44, 12, 's')
r.p(6, 16, '1')

# ============================================================== SUMMIT

r = Room('s-shrine', (10, 2), region='summit', sky='yes', npc='ione-summit')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.carve(20, 21, 25, 22)
r.over(20, 15, 25, 22, 'u')
r.r(37, 0, 39, 20, '#')
r.p(8, 20, 'G').p(31, 20, 'N')

r = Room('s-stairs', (8, 2), size=(2, 1), region='summit', sky='yes', tablet=TABLETS['stairs'])
r.fill('.')
r.r(60, 21, 79, 22, '#')
r.r(50, 18, 59, 22, '#').r(40, 15, 49, 22, '#')
r.r(26, 21, 39, 22, '#').r(26, 20, 39, 20, '^')
r.r(29, 15, 31, 15, 'r').r(34, 15, 36, 15, 'r')
r.r(0, 14, 25, 22, '#')
r.r(0, 0, 1, 13, '#')
r.over(17, 0, 22, 13, 'u')
r.p(52, 17, 'k').p(10, 13, 'k').p(24, 13, 's')
r.p(5, 13, 'T').p(3, 13, '$')

r = Room('s-gate', (8, 1), region='summit', boss='bellkeeper', dark='0.25')
r.fill('#')
r.carve(1, 2, 38, 20)
r.carve(17, 21, 22, 22)
r.r(17, 21, 22, 21, '!')
r.carve(17, 0, 22, 1)
r.r(17, 1, 22, 1, '!')
r.plat(4, 10, 17).plat(29, 35, 17).plat(15, 24, 13).plat(4, 10, 9).plat(29, 35, 9).plat(15, 24, 5)
r.p(27, 20, 'B')

r = Room('s-bell', (8, 0), region='summit', sky='yes')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.carve(17, 21, 22, 22)
r.r(0, 0, 2, 20, '#').r(37, 0, 39, 20, '#')
r.over(8, 12, 9, 20, ':').over(30, 12, 31, 20, ':')
r.p(28, 20, 'K')

if __name__ == '__main__':
    write(sys.argv[1])
    print(f'{len(ROOMS)} rooms written to {sys.argv[1]}')
