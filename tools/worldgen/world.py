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
    'chapel': "Pilgrims prayed here before the climb, and left their psalms for those who came after."
              "|A psalm is a prayer that remembers how to burn. Read it, and your lantern will answer.",
    'scriptorium': "The brothers copied every psalm twice: once on vellum, and once in light."
                   "|The second copy falls like a star. It breaks what was built to be broken.",
    'spire': "At the top of the spire the frost brothers sang a psalm that rose like a pillar."
             "|They say it still stands up here, waiting for a voice.",
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
         hint1='{UP} to rest at a shrine.', hint2='The cleft is too sheer to climb bare-handed.',
         hint3='Running? Hold {DOWN} and press {STRIKE} to vault on your staff.')
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
r.p(56, 12, '2').p(72, 16, '3')

# The west gate: the elder keeps watch over the road to Thornwood, an old well and a watchtower.
r = Room('v-west', (5, 9), region='village', sky='yes', npc='elder',
         hint1='An old well. Hold {DOWN} and press {JUMP} to drop through its boards.')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.r(39, 0, 39, 10, '#')
r.over(2, 10, 4, 20, ':')                    # the old palisade
r.over(5, 9, 7, 20, ':')
r.over(1, 0, 7, 8, ':')                      # ...and its watchtower
r.plat(6, 13, 17)                            # the palisade walk
r.plat(1, 5, 13).plat(3, 7, 9).plat(1, 5, 5).plat(3, 7, 1)   # the watchtower's ladder, up to the rooftops
r.over(10, 0, 15, 20, ':').over(22, 0, 28, 20, ':').over(31, 0, 37, 20, ':')   # tall houses along the lane
r.r(30, 19, 33, 20, '=')
r.p(26, 20, 'N')
r.carve(15, 21, 22, 22)                      # the well, down to the wayside chapel
r.plat(15, 22, 21)
r.p(18, 17, '1')

# The wayside chapel under the well: the first psalm, and a lesson in every way of moving.
r = Room('v-chapel', (5, 10), size=(2, 1), region='village', look='2', dark='0.32', spell='bolt', charm='prayer',
         tablet=TABLETS['chapel'],
         hint1='Press toward a ledge in the air to pull yourself up onto it.',
         hint2='Running? Hold {DOWN} and press {STRIKE} to vault on your staff.',
         hint3='Ropes catch you. Swing with {MOVE}, and {JUMP} to let go.')
r.fill('#')
r.carve(15, 0, 22, 19, ':')                  # the well shaft
for y, a in [(1, 20), (5, 15), (9, 20), (13, 15), (17, 20)]:
    r.plat(a, a + 2, y)                      # old rungs, staggered
r.carve(6, 10, 29, 19, ':')                  # the chapel hall
r.carve(1, 16, 3, 19, ':')                   # a sealed reliquary...
r.r(4, 16, 5, 19, 'x')                       # ...behind cracked stone
r.p(2, 19, 'C').p(9, 19, 'T')
r.carve(30, 10, 44, 14, ':')                 # the low gallery, up a ledge too tall to jump
r.carve(45, 10, 52, 21, ':')                 # a pit of old thorns
r.r(45, 21, 52, 21, '^')
r.carve(53, 8, 72, 14, ':')                  # the rope hall
r.r(53, 15, 55, 15, 'z')                     # a crumbling landing
r.carve(59, 15, 71, 21, ':')
r.r(59, 21, 71, 21, '^')
r.rope(64, 8, 6)
r.carve(73, 2, 78, 12, ':')                  # the psalm
r.r(76, 10, 78, 12, '#')                     # a step up to the high gallery
r.p(74, 12, 'P')
r.carve(23, 2, 72, 6, ':')                   # the high gallery, back to the well
r.carve(36, 7, 43, 9, ':')                   # a trench of thorns...
r.r(36, 9, 43, 9, '^')
r.p(40, 7, 'O')                              # ...and a lantern to bounce from
r.p(22, 13, '1').p(40, 12, '2').p(62, 12, '3')

# The rooftops above the west gate, up the watchtower.
r = Room('v-roofs', (5, 8), region='village', sky='yes', charm='feather',
         hint1='Strike a lantern and it throws you away. Strike it from above to bounce high.')
r.fill('.')
r.r(39, 0, 39, 22, '#')                      # the belfry's wall
r.r(0, 0, 0, 22, '#')                        # the old town wall
r.over(1, 20, 7, 22, ':')
r.plat(1, 7, 19)                             # the top of the watchtower
r.over(10, 16, 15, 22, ':')                  # a cottage and its chimney
r.r(8, 15, 17, 15, '=')
r.r(12, 11, 13, 14, '#')
r.p(19, 19, 'O')                             # a lantern on a post
r.over(22, 12, 28, 22, ':')                  # the bakers' house, higher
r.r(21, 11, 29, 11, '=')
r.p(25, 10, '$')
r.r(30, 9, 31, 9, 'z')                       # loose shingles
r.over(31, 7, 37, 22, ':')                   # the tall house
r.r(31, 6, 38, 6, '=')
r.r(34, 2, 35, 5, '#')                       # its chimney, where something glints
r.p(34, 1, 'C')
r.p(4, 15, '1')

# The belfry loft above the bakehouse, reached on the chimney's updraft.
r = Room('v-belfry', (6, 8), region='village', dark='0.35', tablet=TABLETS['belfry'])
r.fill('#')
r.carve(2, 3, 37, 9, ':')
r.carve(7, 10, 10, 22)
r.over(7, 10, 10, 22, 'u')
r.p(14, 9, 'T').p(23, 9, '$').p(31, 9, 'F')

# ============================================================== THORNWOOD

r = Room('t-edge', (4, 9), region='thornwood', sky='yes', hint1='{STRIKE} to strike.',
         hint2='Each blow gathers grace. Kneel and hold {HEAL} to turn it into a flame.')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.r(17, 19, 24, 20, '#')
r.r(10, 18, 11, 20, '#')
r.plat(3, 9, 15)
r.over(31, 0, 33, 20, ':')                   # an old oak, its branches a ladder into the canopy
for y, (a, b) in [(17, (26, 31)), (13, (31, 36)), (9, (26, 31)), (5, (31, 36)), (1, (26, 31))]:
    r.plat(a, b, y)
r.p(6, 14, '$')
r.p(14, 20, 'c').p(20, 18, 'c')
r.p(37, 18, '1').p(13, 12, '2')

# The trial of vines, in the canopy above the forest edge. Thorns send you back to the start.
r = Room('t-thicket', (4, 8), region='thornwood', sky='yes', trial='yes', charm='longstaff',
         hint1='A trial: thorns here send you back to where you began.')
r.fill('.')
r.r(0, 0, 39, 1, '#')                        # the canopy overhead
r.r(0, 2, 0, 22, '#')
r.r(39, 2, 39, 22, '#')
r.r(1, 22, 24, 22, '#').r(1, 21, 24, 21, '^')   # far below, a floor of brambles
r.r(34, 20, 38, 22, '#')
r.plat(24, 34, 19)                           # where the oak's branches end
r.r(29, 15, 34, 15, '=').r(24, 11, 30, 11, '=')   # branches up to the vines
r.r(6, 4, 30, 4, '=')                        # a great bough: the vines hang from it, and it leads home
r.rope(18, 5, 9).rope(9, 5, 8)
r.p(4, 13, 'O')
r.r(1, 7, 4, 7, '=')                         # a high knot of roots, and something on it
r.p(2, 6, 'C')
r.p(31, 13, '1')

r = Room('t-brambles', (3, 9), region='thornwood', sky='yes',
         hint1='Strike {DOWN} in the air to bounce off thorns and foes.')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.r(20, 21, 26, 21, '^')                     # the bramble bed
r.carve(9, 21, 13, 22)                       # a hole down into the hollow
r.plat(29, 35, 15)
r.plat(21, 26, 11)
r.p(18, 16, 'O')                             # a lantern hung over the brambles
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
r.rope(33, 3, 8).rope(51, 3, 7).rope(66, 3, 7)   # roots hanging from the forest floor above
r.r(69, 13, 78, 14, '#')                     # a sealed alcove...
r.r(78, 15, 78, 20, '#')
r.r(70, 15, 71, 20, 'x')                     # ...behind cracked stone
r.p(75, 20, 'F')
r.p(7, 17, '$')
r.p(30, 8, 'w').p(60, 7, 'w').p(24, 20, 'l').p(64, 20, 'c').p(44, 20, 'h')

r = Room('t-den', (1, 10), region='thornwood', boss='thornback', dark='0.45', charm='bramble')
r.fill('#')
r.carve(1, 4, 38, 20, ':')
r.carve(0, 16, 0, 20).carve(39, 16, 39, 20)
r.r(1, 16, 2, 20, '!').r(37, 16, 38, 20, '!')
r.plat(7, 12, 15).plat(27, 32, 15)
r.p(20, 20, 'B').p(20, 19, 'C')

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
r.p(30, 6, 'w').p(55, 4, 'w').p(14, 14, 'h').p(45, 20, 'l')

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
r.carve(70, 0, 74, 2, ':')                   # a hatch up to the ringing loft
r.plat(68, 72, 9).plat(71, 75, 5).plat(70, 74, 2)
r.carve(2, 0, 3, 2, ':')                     # where the loft's ropes come down
r.p(44, 12, 'k').p(54, 3, 's').p(52, 7, 'g')
r.p(7, 9, '1')

r = Room('a-nave', (9, 8), size=(2, 1), region='abbey', dark='0.45', npc='ione-abbey')
r.fill('#')
r.carve(1, 3, 78, 20, ':')
r.carve(0, 16, 0, 20).carve(79, 16, 79, 20)
r.carve(60, 0, 65, 2)
for a in (10, 22, 34, 46):
    r.plat(a, a + 5, 17)
r.plat(4, 16, 11).plat(58, 70, 11)
r.rope(20, 3, 9).rope(32, 3, 8).rope(44, 3, 9)   # the nave's old bell ropes
r.p(6, 20, 'N').p(8, 10, '$')
r.p(30, 20, 'k').p(54, 20, 'k').p(40, 3, 's').p(70, 8, 'w').p(72, 20, 'l')

r = Room('a-crypt', (11, 8), size=(1, 2), region='abbey', dark='0.6', ability='bell', tablet=TABLETS['crypt'])
r.fill('#')
r.carve(3, 3, 36, 43, ':')
r.carve(0, 16, 2, 20)
r.r(3, 21, 9, 22, '#')
for y, a, b in [(25, 12, 24), (29, 18, 30), (33, 8, 20), (37, 16, 28), (41, 6, 18)]:
    r.plat(a, b, y)
r.carve(0, 39, 2, 43)
r.r(1, 39, 2, 43, 'b')                       # bell-glass to the ossuary
r.carve(30, 44, 33, 45, ':')                 # steps down to the scriptorium
r.p(20, 43, 'A').p(30, 43, 'T')
r.p(36, 30, 's').p(20, 30, 'w').p(12, 43, 'c')

# The ringing loft: the abbey's bell ropes still hang over the thorned floor.
r = Room('a-loft', (9, 6), size=(2, 1), region='abbey', dark='0.4', charm='clasp',
         hint1='Ring the bell in mid-air, and resonance stones will wake beneath you.')
r.fill('#')
r.carve(2, 2, 77, 21, ':')
r.r(2, 21, 77, 21, '^')                      # the floor fell long ago
r.carve(70, 21, 74, 22, ':')                 # the hatch from the cloister
r.plat(67, 77, 19)
r.r(66, 11, 77, 11, '=').r(71, 15, 76, 15, '=')
r.rope(60, 2, 9).rope(50, 2, 8).rope(32, 2, 9).rope(22, 2, 8)
r.r(41, 12, 43, 12, 'r')
r.r(10, 13, 15, 22, '#').r(16, 14, 16, 20, '>')   # a pillar under the old glass, thorned below
r.r(8, 2, 9, 12, 'b')                        # the reliquary, behind bell-glass
r.r(2, 13, 9, 22, '#')
r.carve(2, 13, 3, 22, ':')                   # ...and a way down to the cloister
r.p(5, 12, 'C').p(13, 12, '$')
r.p(70, 9, '1')

r = Room('a-ossuary', (10, 9), region='abbey', dark='0.65')
r.fill('#')
r.carve(2, 8, 38, 20, ':')
r.carve(39, 16, 39, 20)
r.r(6, 16, 9, 20, '#')                       # the reliquary pillar
r.p(7, 15, 'H').p(24, 20, '$').p(16, 20, 'c').p(30, 8, 's')

# The scriptorium under the crypt: the psalm of the falling star, and what it opens.
r = Room('a-scriptorium', (10, 10), size=(2, 1), region='abbey', dark='0.6', spell='star', charm='quickstaff',
         tablet=TABLETS['scriptorium'], hint1='A heavy slab, marked with a star. Your staff will not break it.')
r.fill('#')
r.carve(2, 2, 77, 11, ':')                   # the upper library
r.carve(70, 0, 73, 1, ':')                   # from the crypt
for y, a in [(1, 71), (5, 67), (9, 71)]:
    r.plat(a, a + 3, y)
r.r(30, 12, 33, 13, 'q')                     # a heavy slab...
r.carve(30, 14, 33, 18, ':')                 # ...over a shaft to climb back by
r.r(28, 14, 29, 18, '#').r(34, 14, 35, 18, '#')
r.carve(2, 14, 77, 20, ':')                  # the lower stacks
r.r(28, 14, 29, 17, '#').r(34, 14, 35, 17, '#')
r.r(14, 12, 18, 13, 'q')
r.p(50, 11, 'P').p(60, 11, 'T')
r.p(5, 20, 'C').p(70, 20, '$').p(16, 11, '$')
r.p(40, 20, 'k').p(55, 6, 'w').p(20, 4, 'w')
r.plat(4, 9, 8).plat(40, 46, 7)
r.p(36, 9, '1')

r = Room('a-sanctum', (11, 7), region='abbey', boss='abbot', dark='0.55', charm='psalter')
r.fill('#')
r.carve(1, 2, 38, 20, ':')
r.carve(0, 8, 0, 12)
r.r(1, 13, 6, 20, '#')
r.r(1, 8, 2, 12, '!')
r.carve(17, 0, 22, 1)
r.r(17, 1, 22, 1, '!')
r.plat(9, 14, 17).plat(26, 32, 17).plat(16, 23, 13).plat(8, 13, 9).plat(27, 32, 9).plat(16, 23, 5)
r.p(24, 20, 'B').p(20, 12, 'C')

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
r.p(7, 27, 'O').p(30, 13, 'O')               # censers, still swinging
r.p(30, 28, '$').p(20, 20, 'w').p(14, 8, 'g').p(35, 30, 's')
r.p(18, 36, '1')

# ============================================================== FROST

r = Room('f-ledge', (11, 4), region='frost', sky='yes')
r.fill('.')
r.r(0, 21, 39, 22, '#')
r.carve(17, 21, 22, 22)
r.r(37, 0, 39, 20, '#')
r.r(4, 17, 9, 20, '#')
r.over(25, 0, 28, 19, 'u')                   # a wind that rises along the spire
r.p(32, 20, 'G')

# The spire above the windswept ledge, and the psalm at its top.
r = Room('f-spire', (11, 3), region='frost', sky='yes', spell='psalm', tablet=TABLETS['spire'])
r.fill('.')
r.r(0, 0, 0, 22, '#').r(37, 0, 39, 22, '#')
r.r(1, 21, 24, 22, '#').r(29, 21, 36, 22, '#')
r.over(25, 10, 28, 22, 'u')
r.r(29, 13, 36, 20, '=')                     # the spire's foot
r.r(30, 9, 32, 9, 'z')
r.p(23, 9, 'O')
r.r(13, 10, 17, 20, '=')
r.r(10, 6, 12, 6, 'z')
r.p(5, 7, 'O')
r.r(1, 4, 9, 20, '=')                        # the high shelf
r.p(4, 3, 'P').p(7, 3, 'T').p(15, 9, 'F')
r.r(18, 5, 26, 5, '=')
r.p(30, 12, '1')

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
for y, a, c in [(14, 28, '='), (12, 35, 'z'), (14, 42, '='), (13, 49, 'z')]:
    r.r(a, y, a + 2, y, c)                   # ice floes, some of them rotten
r.p(31, 4, 'd').p(46, 4, 'd').p(10, 15, 'h').p(62, 9, 'g')
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
r.carve(0, 3, 9, 6, ':')                     # an ice tunnel west, to the trial of chains
r.p(12, 6, '$').p(30, 16, 'w')

# The trial of chains: old chains hang over the ice spikes of a crevasse. Climb it.
r = Room('f-chains', (5, 3), size=(2, 1), region='frost', dark='0.2', trial='yes', charm='echo',
         hint1='A trial: the spikes send you back to where you began.')
r.fill('#')
r.carve(68, 2, 78, 6, ':').carve(79, 3, 79, 6, ':')   # from the windway
r.carve(72, 7, 76, 19, ':')                  # a shaft down into the crevasse
r.carve(2, 5, 71, 20, ':')
r.r(68, 5, 71, 12, '#')                      # the shaft's wall: the only way in is down
r.r(2, 8, 2, 20, '>')                        # spikes on the far wall
r.r(2, 21, 69, 21, '^')
r.r(70, 19, 77, 22, '#')                     # the bottom of the shaft: where the trial begins
r.rope(66, 5, 10)
r.r(52, 12, 54, 12, 'z')
r.rope(47, 5, 7)
r.p(36, 10, 'O')
r.r(26, 8, 31, 8, '=')                       # an ice shelf
r.over(18, 5, 21, 9, 'u')                    # a rising wind, high up
r.r(2, 7, 12, 7, '=')                        # the far ledge
r.p(4, 6, 'C').p(9, 6, 'L')
r.carve(2, 2, 69, 3, ':')                    # the way back, along a ledge under the ice
r.carve(2, 4, 12, 4, ':')
r.r(13, 4, 69, 4, '=')
r.r(66, 2, 67, 3, '|')                       # barred until the lever is struck
r.p(74, 6, '1')

r = Room('f-hollow', (8, 3), region='frost', boss='wyrm', dark='0.3', charm='vigil')
r.fill('#')
r.carve(1, 4, 38, 20)
r.carve(0, 16, 0, 20).carve(39, 16, 39, 20)
r.r(1, 16, 2, 20, '!').r(37, 16, 38, 20, '!')
r.r(7, 15, 12, 15, '=').r(27, 15, 32, 15, '=')
r.p(20, 20, 'B').p(30, 14, 'C')

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
r.p(32, 17, 'O')
r.r(0, 14, 25, 22, '#')
r.r(0, 0, 1, 13, '#')
r.over(17, 0, 22, 13, 'u')
r.over(72, 0, 76, 20, 'u')                   # a wind that climbs to the last trial
r.p(52, 17, 'k').p(10, 13, 'k').p(24, 13, 's').p(56, 8, 'g').p(66, 20, 'l')
r.p(5, 13, 'T').p(3, 13, '$')

# The last trial: every way of moving, in one climb. Thorns send you back down.
r = Room('s-trial', (9, 0), size=(1, 2), region='summit', sky='yes', trial='yes', charm='notch',
         hint1='The last trial. Everything you have learned, at once.')
r.fill('.')
r.r(0, 0, 1, 45, '#').r(38, 0, 39, 45, '#')
r.r(2, 0, 37, 1, '#')
r.r(2, 2, 2, 41, '>').r(37, 2, 37, 29, '<')  # thorned walls: no climbing here
r.r(2, 43, 31, 45, '#').r(37, 30, 37, 45, '#')
r.r(3, 42, 31, 42, '^')
r.over(32, 30, 36, 45, 'u')                  # the wind that brought you here
r.r(24, 31, 31, 31, '=')                     # a ledge beside the wind
r.p(12, 33, 'O')
r.r(3, 28, 10, 28, '=')                      # across the gap
r.r(13, 11, 15, 11, '=')                     # a beam, and a cord hanging from it
r.rope(14, 12, 13)
r.r(22, 19, 29, 19, '=')
r.r(30, 2, 30, 18, 'b')                      # a wall of bell-glass, ceiling to floor
r.r(32, 16, 34, 16, 'r').r(35, 13, 36, 13, 'r')
r.r(31, 10, 34, 10, '=')
r.r(34, 7, 36, 7, 'z')
r.r(31, 4, 33, 4, '=')                       # the top
r.p(32, 3, 'C').p(6, 27, '$')
r.p(28, 29, '1')

r = Room('s-gate', (8, 1), region='summit', boss='bellkeeper', dark='0.25')
r.fill('#')
r.carve(1, 2, 38, 20)
r.carve(17, 21, 22, 22)
r.r(17, 21, 22, 21, '!')
r.carve(17, 0, 22, 1)
r.r(17, 1, 22, 1, '!')
r.plat(4, 10, 17).plat(29, 35, 17).plat(15, 24, 13).plat(4, 10, 9).plat(29, 35, 9).plat(15, 24, 5)
r.plat(17, 22, 2)                            # a last step under the door to the bell
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
