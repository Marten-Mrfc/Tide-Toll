# The owner's review page, night 3

- The page: https://claude.ai/artifact/Gs8asamHMnQmLug2aLPSwz (private to the owner; shared from its Share menu).
- The same list as text, numbered as on the page: [list.md](list.md).
- The lead's honest view: [honest.md](honest.md).
- The build it shows: commit `e02da4e`, Windows build https://github.com/Marten-Mrfc/Tide-Toll/actions/runs/45939301484 (artifact `TideToll-windows-x64`).

## How it is made

`python3 docs/review/n3/build_review.py` reads `manifest.json` (every item: what it shows, its question, its pictures), the before pictures in `before/`, the after pictures in `after/` and the closing lists (`closing.json`, made by `make_closing.py` from `docs/incremental-log.md` and `honest.md`). It writes `build/review-n3/index.html` with its pictures and sounds beside it, `list.md`, and `files-1.json` and `files-2.json` (the files to publish with the page, at most 255 per publish).

A picture that could not be taken from the final build is shown as a stand-in from the round that made it, and says so on the page.
