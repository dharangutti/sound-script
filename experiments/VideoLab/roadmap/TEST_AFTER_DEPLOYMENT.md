# v0.4 smoke checklist after the publication PR is merged

1. Open https://soundscript.net/labs/videolab/ and refresh. Expect ten demo cards.
   publication.json should name labs-videolab-mvp-web-v0.4.0 and commit
   e5ec72604c1dcb23c859f2674824dbcc7a834990.
2. Load “Titles, callouts and familiar edits.” Play both bindings and formats;
   inspect frame 30. Verify title, callout, crossfade and readable timeline lanes.
3. Load “One composition, three audiences.” Choose shopfloor, qa and engineering.
   The schematic remains identical while instructions, labels and colors change.
4. Load “Review notes become video.” Switch the three annotation datasets. Open
   View Composition → Result: base JSON stays the same; annotation data and generated
   primitives change. Play and download an MP4 and a WebM.
5. On a phone-width window, verify no horizontal overflow. Public arbitrary-value
   controls remain disabled; My Files explains the optional private local workbench.

Public hosting is a static gallery. For free bindings or your own video/music,
run dotnet run -c Release -- serve from experiments/VideoLab and use localhost.
Phase 4 starts only after the user's feedback and explicit next-phase instruction.
