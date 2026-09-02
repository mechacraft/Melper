---
name: replay-search
description: Find a past Mechabellum match in the local replay folder by what happened in it - units and their counts, a technology bought, a commander skill cast, on either side - by running tools/replay-search/replay_search.py. Use whenever the user wants a game found or recalled rather than analysed: "найди реплей где", "в какой игре я", "когда я последний раз играл в", "какая была игра против", "find the game where he used", "which match did I have N of X". This includes matches described only by what the opponent did, with no name and no date to go on.
---

# Finding a match in the replay folder

The user remembers a game by what was in it and wants the file. Everything the search
needs is already recorded in the `.grbr` files; run the tool and report the match. Do
not open the game, and do not write a throwaway parser — see the last section.

## Run it

From the repository root:

```bash
python tools/replay-search/replay_search.py --player MakTpaxep --unit Fortress --min 4 --last 15
```

Stdlib Python, no venv, no build. `--help` prints the full surface, and
`tools/replay-search/README.md` explains the file format if a result looks impossible.

`MakTpaxep` is the user's own name in this folder — "я", "у меня", "мой" all mean that
side. The tool needs a name and refuses a typo with a non-zero exit rather than an empty
list, so "nothing found" always means exactly that. Names of everyone else are in the
file names (`..._[MakTpaxep]VS[Aru].grbr`), so `ls` the replay folder when the user
half-remembers an opponent.

| what the user said | flag |
|---|---|
| "я играл в X" / "у меня было 4 форта" | `--unit X`, `--min 4` |
| "он играл в X" / "у противника были X" | `--side opponent --unit X` |
| "с технологией X" / "качал X" | `--tech "X"` |
| "применил шторм" / "кинул ионку" | `--spell X`, and `--side opponent` if it was theirs |
| шторм **и** бомбардировка **и** ионка | repeat `--spell` — every one of them must be in the match |
| "в последних N играх" | `--last N` |
| "в любом раунде", "когда-нибудь за игру" | `--round any` — the default only looks at the last round |
| "покажи что там было" | `-v` — prints the full army of every hit |

`--player` picks the **matches**; `--side` picks **whose** side inside them to test. That
split is what makes "a game where the enemy did X" expressible at all: the opponent's
name is exactly what the user does not remember.

Technologies and spells are properties of the whole match, not of a round, so `--round`
and `--min` do not apply to them and the round they happened in is printed instead.

## Two units at once

`--unit` takes one unit. For "Abyss **and** Stormcallers", search on the rarer of the two
and read the second off the army:

```bash
python tools/replay-search/replay_search.py --player MakTpaxep --unit Abyss --last 40 -v
```

```
2026-08-27 10:21  round 10  Abyss 2 (at least; final round)  2259_...[MakTpaxep]VS[HYPERCRAB].grbr
       18 x Crawler
        7 x Tarantula
        ...
        3 x Stormcaller
```

Narrowing first and confirming by eye beats looping the tool twice and intersecting file
names, and it shows the user the composition they were actually asking about.

## Time windows

There is no date filter. `--last N` counts matches, not days — roughly 35–40 games a week
in this folder, so "за последнюю неделю" is about `--last 40`. Every line prints the
match date, so overshoot and drop the older lines rather than guessing tight.

## Names

**Units** — English, from `Melper.Core/Data/units.json`; the ru→en table in
`../breakpoint-advisor/SKILL.md` covers the dictated manglings ("сайбер тус" = Sabertooth).

**Technologies** — English from the extract dump. `--tech` also takes the Chinese `name`
and a raw `TechID`. A wrong name exits with the full list of known ones, which is the
fastest way to find the real spelling.

**Spells** — the label is the game's *internal* name and often is not the in-game one, so
translate deliberately instead of passing the user's word through:

| ru | flag value | | ru | flag value |
|---|---|---|---|---|
| шторм, молнии | `"Thunder Storm"` | | ядерка, нюк | `"Nuclear Bomb"` |
| бомбардировка, орбиталка | `Rockets` | | ЭМИ (большой / малый) | `EMP` / `"EMP Little"` |
| ракетный удар, малая бомба | `"Rockets Little"` | | напалм, зажигалка | `Fire` |
| ионка, ионная пушка | `Ion` | | масло | `Oil` |
| копьё, джавелин | `"Orbital Javelin"` | | кислота | `Acid` |
| щит с неба | `"Energy Barrier"` | | дым | `Fog` |
| опыт, обучение | `"Max Exp"` | | фотон | `"Photon Beam"` |
| передислокация | `Move` | | маяк / ложный маяк | `"Moving Beacon"` / `"Moving fakeBeacon"` |
| продажа, утилизация | `Sell`, `"Sell D"` | | эволюция, реорганизация | `Reorganize` |
| черви, паук | `"Summon Spider"` | | носороги | `Rino` |
| самолёты | `"Summon Plane"` | | оверлорд с неба | `Overlord` |

The `Zh *` labels are the newer "call in a unit" spells — `Zh Zj` Phoenix, `Zh El` Wraith,
`Zh Kx` Scorpion, `Zh Tf` Typhoon, `Zh Hh` Fire Badger, `Zh Bl` Fortress, `Zh Rd` Melting
Point — and `"Fire God"` is Vulcan. As with techs, a wrong name lists every known one.

## Reading the answer back

```
2026-08-30 01:01  Мягколап: [Thunder Storm round 8,9; Rockets round 6; Ion round 4,8]  2259_20260830--268656622_[MakTpaxep]VS[Мягколап].grbr
```

The file name **is** the answer — the user opens it in the game, so quote it in full. The
folder is `ProjectDatas/Replay` inside the game install; `--dir` points elsewhere.

Three annotations mean something and should survive into the answer:

- `(at least; final round)` — the last round's army is a lower bound, because reinforcement
  cards hand out units that nothing records. Say "не меньше", not "было ровно".
- `+N cast(s) no snapshot names yet` — that side cast N spells the replay does not let
  anyone name. If the user is asking whether a spell was *absent*, this is the caveat.
- `(round has no actions ...)` — a round that was recorded but never played, usually a bot
  game or the sandbox. Worth flagging rather than presenting as a real match.

Answer in the language the question was asked in, and lead with the match — date, opponent,
file — before the detail.

## Do not

- Do not parse `.grbr` with an ad-hoc script when the tool can express the query. If it
  genuinely cannot, say which part is missing instead of quietly hand-rolling it, so the
  gap gets fixed in the tool where the next question benefits too.
- Do not answer from memory of an earlier search in the conversation. Replays are added
  constantly and `--last N` slides.
- Do not treat an empty result as "the tool did not find it". Names are validated; empty
  means it did not happen.
