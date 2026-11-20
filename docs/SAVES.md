# Saves: several campaigns, several saves each

Built 2026-11-19. Before this there were two global files, `autosave.json` and `quicksave.json`: a new campaign overwrote the last
one's autosave and CONTINUE loaded whichever was newer.

## The layout on disk

`CampaignState.SaveDir` (`IRONFIELD_SAVES`, else `~/.config/Ironfield/saves`) holds one folder per campaign:

```
saves/
  the-lantern-company-20261119-201455/     <- CampaignState.CampaignId, given on the first save (company + start time)
    auto-20261119-203000-123.json          <- rotating autosaves (newest Saves.AutoKeep = 3 kept)
    quick.json                             <- F5 / QUICK SAVE (one per campaign)
    named-before-the-siege.json            <- SAVE AS... (file name = Saves.Slug(name); the typed name is in the card)
    *.meta                                 <- each save's card (SaveMeta: captain, company, service, day, gold, men, palette, ironman, saved at)
    *.png                                  <- the map as it was, 320 px wide, taken the frame after a save made on the map
    *.json.bak                             <- the previous copy of an overwritten save (crash safety, as before)
```

- Autosaves: each new week on the map, before a campaign battle, on QUIT TO TITLE and TO THE TITLE after a career beat.
- Ironman: one save only. Every save of an Ironman campaign is an autosave and the folder keeps just the newest. QUICK LOAD and
  SAVE AS are off; LOAD... still opens other campaigns. The war's end (the GameOver panel) deletes an Ironman campaign's folder;
  for a campaign that is not Ironman (retiring), only its autosaves go, so CONTINUE does not reopen a finished war.
- Old saves: on the first listing, `autosave.json` / `quicksave.json` in the save folder move into a campaign folder (one folder when
  both are the same campaign: same seed, captain and company). Saves too old to read move too and show as UNREADABLE, so they can be deleted.

## The code

| What | Where |
|---|---|
| Store: paths, rotation, cards, listing (cached, `Saves.Changed()` drops it), latest, load, delete, migration, thumbnails | `Campaign/Saves.cs` (`Saves`, `SaveMeta`, `SaveEntry`, `CampaignEntry`) |
| Writing and reading one file (tmp + .bak, version check, .bak fallback) | `CampaignState.SaveFile`, `CampaignState.LoadFile` |
| The save screen (LOAD on the title; SAVE AS... and LOAD... in the map's menu) | `SaveScreen.cs` |
| CONTINUE, loading a file behind the loading screen | `ScreenHost.ContinueCampaign`, `ScreenHost.LoadSave` |
| Map: quick save/load, weekly autosave, the thumbnail grab, the menu | `MapScreen.Save`/`Write`/`QuickLoad`/`GrabThumb`, `MapScreen.Dialogs.cs` `MenuPanel`, `SavesPanel` |
| Test | `Tests/CampaignTests.cs` `SaveFolders` |
| Shots | `IRONFIELD_SAVES_ONLY=1` with `IRONFIELD_CAMPAIGN_SHOTS` (`campaign-save-as.png`, `campaign-load.png`); title shots add `title-load.png` when saves exist |

Loading from the map always goes through the host's loading screen (another campaign may be another map); QUICK LOAD (F9) loads
this campaign's `quick.json` in place, as before.

## Tester saves

`dotnet run -c Release --no-build -- --tester-saves [filter]` (`TesterSaves.cs`) writes twelve playtest campaigns into the
save folder (`IRONFIELD_SAVES`, else the real one), each in its own `test-NN-...` folder with one named save, company named
after the scenario: fresh start, first month, seasoned sellsword, late career (the stand-in captain plays 30-200 days for
those), rich sandbox, big battle ahead, at the walls, circle and aura, at the chapter house, monster country, at a port,
castle muster. It prints each one as it loads back and what stands close by. Running it again replaces only the `test-`
folders. No map pictures (headless): the card shows the arms.

## Not done (ideas)

- Steam/cloud sync, save import/export.
- A captain portrait on the card (the card has no face data; the map picture stands in).
- A picture for the autosave made as a battle begins (the map is not drawn then): it shows the arms instead.
