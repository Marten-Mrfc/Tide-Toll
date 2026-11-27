# Rules for every builder working in this repository at the same time (27 November)

Several builders work at once, each in its own git worktree under `build/wt/`, on a machine with 4 cores.

1. **Your checkout only.** Work only in the worktree you were given. Never touch `/home/user/tide-toll` itself or another worktree. Never push, never open a pull request. Commit on your branch with messages starting "Snapshot: " and the attribution lines your own session's rules give you. If a commit is refused by the permission check, do not work around it: go on and say so in your report.
2. **Never use `git stash`.** The stash is shared between all worktrees: a `git stash pop` in one checkout can apply another builder's work there (it happened on 27 November). To set work aside, commit it on your branch or copy files to a folder under your worktree's `build/`.
2a. **A fresh checkout** needs `tools/sync-assets.sh` and `godot --headless --path game --import` before the first build.
3. **The heavy-run lock.** Tests that measure time fail when the machine is busy, and picture runs slow everyone down. Before any of these, take the lock; give it back the moment the run ends:
   - timing checks: `game-perf`, `game-motion`, `game-work`, `game-playthrough`, `game-river`, `game-layout`, and any check of your own that samples frames;
   - any Godot picture run longer than two minutes; `./check` as a whole.
   - Take it: `mkdir /home/user/tide-toll/build/locks/heavy` (it succeeds only if nobody holds it). Give it back: `rmdir /home/user/tide-toll/build/locks/heavy`.
   - If you cannot get it, do other work and try again; wait with `python3 -c "import time; time.sleep(30)"`. Never a busy loop; foreground `sleep` is blocked.
   - If the folder is more than 25 minutes old (`stat -c %Y`), its holder has probably died: say so in your report, `rmdir` it and take it.
   - Building (`dotnet build`), the simulation's tests (`dotnet test sim.tests ...`) and short picture runs do not need the lock. Two builders (the places and the ink research) started before this rule and do not know it: a timing failure may still be their load, so run a failed timing check once more under the lock before you believe it.
4. **Display numbers** for xvfb are given per builder; use only yours.
5. **Never weaken a test.** A test that only fits the old look or the old rule may be replaced by the equal test for the new one; write each down (old assertion, new assertion, why). Report failures plainly, with the output.
6. **No allocation per frame** in drawing code that runs every frame; nothing changes in one frame (0.15 s or longer), at 30, 60 and 120 frames a second.
7. **Assets** are made in code by generators under `assets/src/`, each with a ledger row; `python3 tools/check-ledger.py` must pass. Nothing is fetched from the network. The style sheet is `docs/plans/n3/style-sheet.md`.
8. **Files stay under about 400 lines, one system per file** (AGENTS.md).
9. **Clock:** check it with `date -u`. Your brief gives a time to be finished by; if you see you will not make it, do the most visible things first and say plainly what is left.
