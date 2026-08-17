---
name: breakpoint-advisor
description: Say which Mechabellum attack/hp technology is worth buying against a given set of units, ranked by effect, by running the melper console tool. Use when the user asks what breakpoints there are, what to upgrade, or names units to advise on - in English or Russian ("какие есть брейкпоинты на кроллеров, фэнгов и тарантулов?", "что качать против...", "стоит ли брать атаку").
---

# Breakpoint advice from the melper console tool

The user names the units on the board and asks which attack or hp technology actually
crosses a breakpoint. Run the tool, do not calculate anything yourself, and read the
answer back in the language the question was asked in.

## Run it

From the repository root:

```bash
dotnet run --project Melper.Cli -c Release -- advise --units "Crawler, Fang, Sabertooth, Arclight, Tarantula"
```

The first run builds; later ones are instant. `--help` prints the full surface.

| what the user said | flag |
|---|---|
| named units, no side stated | `--units "A, B, C"` — both halves of the board |
| "у меня X, у него Y" / "mine … versus …" | `--mine "X" --vs "Y"` |
| "никаких апгрейдов нету" / nothing said about tech | nothing — 0 is the default |
| "у меня атака 1" / "I have attack 2" | `--attack 1`, `--attack 2` |
| "у него уже хп" | `--vs-hp 1` |
| "я на fortified" / "он на cost control" | `--fortified mine`, `--cost-control vs` |
| "покажи всё" / "дай побольше" | `-n 30`, or `--all` for the uncapped list |

Two rules the tool enforces, so do not fight them:

- Only the **next** buy on each ladder is advised on — Attack2 needs Attack1 first.
- A side with no units named is the **whole roster**, not an empty board. If the user
  names units without saying whose they are, put them on both sides with `--units`.

## Names

The tool matches English names loosely: case, spacing, punctuation and an English plural
are all ignored (`crawlers`, `steel ball`, `SteelBall`, `arc` all resolve). It refuses
anything ambiguous rather than guessing — `wa` comes back as "Wasp, War Factory".

Russian is **not** matched by the tool. Translate first:

| ru | en | | ru | en |
|---|---|---|---|---|
| кроллер, краулер | Crawler | | фарсир | Farseer |
| фэнг, фанг | Fang | | вулкан | Vulcan |
| хаунд, гончая | Hound | | мелтинг (поинт), плавилка, МП | Melting Point |
| войд ай, глаз | Void eye | | фортресс, крепость, форт | Fortress |
| арклайт, ярклайт, арка | Arclight | | сэндворм, червь | Sandworm |
| марксман, маркс | Marksman | | райден | Raiden |
| мустанг | Mustang | | оверлорд, овер | Overlord |
| следж, кувалда | Sledgehammer | | вар фактори, фабрика, завод | War Factory |
| сторм, стормкаллер | Stormcaller | | абисс, бездна | Abyss |
| стилбол, шары | Steel Ball | | маунтин, гора | Mountain |
| тарантул | Tarantula | | файр баджер, барсук | Fire Badger |
| саблезуб, сэйбертус, сайбер тус | Sabertooth | | тайфун | Typhoon |
| рино, носорог | Rhino | | вортекс, вихрь | Vortex |
| хакер | Hacker | | васп, оса | Wasp |
| феникс | Phoenix | | фантом (рэй), скат | Phantom Ray |
| рейф, врайт | Wraith | | скорпион | Scorpion |

Dictated names arrive mangled — "сайбер тус" is Sabertooth. Take the nearest unit rather
than stalling, but if two are genuinely plausible, ask which one instead of picking.

If the tool answers `"X" is not a unit`, that is your translation to fix, not the user's
mistake — retranslate and rerun. Do not silently drop the name.

## Reading the answer back

Each line already reads as a sentence; keep the ranking and the tier word.

```
  1. decisive    Attack1  Sabertooth kills Sabertooth in 2 attacks instead of 3   (x1.5 from a x1.12 buy)
```

- **decisive** — the pairing is decided, not just shortened. These are the ones to name first.
- **noticeable** — a threshold was crossed, but it is a shortening.
- The tail that only banks the technology's own percentage is dropped; it is back under `--all`.
- `x1.5 from a x1.12 buy` is the point: a 12% technology bought 50%. That surplus is the
  ranking.

When the user asked in Russian, answer in Russian. Lead with the verdict — which of the
buys on offer is worth taking and against whom — then the top pairings. Do not read out
more than about eight lines unless asked; the tool prints 15 so you can pick.

State the roster date the tool prints if the numbers are being relied on for a real game.

## Do not

- Do not compute breakpoints by hand or from memory, and do not open the web app for this.
  The tool is the calculation, and its ranking is the same one the Advisor page uses.
- Do not edit `Melper.Core/Data/units.json` to answer a question.
