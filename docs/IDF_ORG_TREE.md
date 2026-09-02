# IDF public peacetime organization dataset

`data/organizations/idf_public_peacetime.json` is a **game organization/reference dataset**, not an operational order of battle.

## Structural decision

The dataset does not put Northern/Central/Southern Command underneath Ground Forces. The public institutional structure is represented with Ground Forces / GOC as the force-building/training branch while the three regional commands are parallel operational command branches under the IDF root.

The dataset then represents public division → brigade → battalion relationships where the cited public pages identify them.

## Sources

The current baseline uses public Wikipedia pages for the overall Ground Forces structure, Northern/Central/Southern Commands, the 36th/91st/146th/210th divisions, 7th and 188th armored brigades, 35th Paratroopers, Oz, Kfir, Judea and Samaria Division, Givati, Nahal, 401st and 460th brigades.

Each JSON source record includes its URL and a boundary statement. This lets later research replace a Wikipedia source with a stronger official source without changing the formation engine.

## Excluded data

The formation dataset intentionally does not contain:

- current deployment positions;
- bases or exact locations;
- current readiness or personnel strength;
- current temporary attachments;
- active patrols/routes;
- tactical tasking;
- live battlefield order of battle.

A unit's organization record and its simulated scenario assignment are separate concepts. Future scenario files can use fictional/local assignments without modifying the public peacetime catalog.
