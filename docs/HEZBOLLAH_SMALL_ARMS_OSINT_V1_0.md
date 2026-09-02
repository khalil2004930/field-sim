# FieldSim — Lebanese Hezbollah Small-Arms OSINT Report v1.0

**Created:** 2026-09-01  
**Supersedes:** v0.1 seed database  
**Scope:** Firearms and crew-served guns publicly associated with **Lebanese Hezbollah**, with priority given to War Noir / @war_noir identifications and public technical corroboration. Iraqi **Kata'ib Hezbollah is excluded**.

## Executive conclusion

The evidence does **not** support treating Hezbollah infantry as using one standard rifle. The strongest direct War Noir evidence supports a mixed small-arms ecosystem with:

- **5.45×39 mm:** AK-74, AK-74M, AKS-74, AKS-74U.
- **7.62×39 mm:** AK-103 / Iranian KL-133 pattern, AK-104; AKM-pattern rifles are supported more strongly by public secondary proliferation references than by the direct War Noir formal-unit material recovered in this pass.
- **5.56×45 mm NATO:** M4-pattern carbines are directly identified in Hezbollah imagery; M16-family rifles remain provisional in this dataset.
- **7.62×54R:** PKM and Hoshdar-M are directly identified; PKP is supported by public secondary proliferation data.
- **7.62×51 mm NATO:** FN MAG/M240-family support weapons are supported by public secondary data; a possible Mk 14 EBR-RI appears in War Noir's 2023 exercise analysis.
- **Precision rifles:** ORSIS T-5000/T-5000M is directly identified, but its Hezbollah-specific chambering should remain **unresolved** until a photographed rifle/ammunition pairing establishes it.
- **12.7×108 mm:** NSV is retained only as a provisional heavy-support entry from public secondary association data.

The database therefore models **presence and confidence**, not prevalence.

## Method

### Evidence priority

1. War Noir direct identifications in Lebanese Hezbollah or Radwan imagery.
2. War Noir Militant Wire articles.
3. War Noir Telegram mirror / indexed X posts.
4. U.S. Army ODIN / Worldwide Equipment Guide as public technical and proliferation corroboration.
5. Small Arms Survey and other public technical references for model/caliber verification.

### Evidence grades

- **A1** — direct War Noir identification in Hezbollah/Radwan imagery.
- **A2** — direct War Noir identification in a Hezbollah exercise/display.
- **B1** — direct source but the model ID is explicitly tentative or ambiguous.
- **B2** — public authoritative/technical secondary association without matching direct War Noir visual evidence recovered in this pass.
- **C** — supporter/adjacent evidence only; not part of the primary baseline.

No source appearance is converted into a percentage of fighters carrying that weapon.

## Key direct War Noir findings

### Radwan training, February 2022

War Noir's archived thread says the Radwan Unit training shown used **AK-74 and AK-74M rifles mostly**, and also shows an **AKS-74U** and **PKM**. This is the cleanest direct evidence for the 5.45×39 rifle family plus PKM in a named Hezbollah formation.

Source: https://threadreaderapp.com/scrolly/1493922160699654144

### Hezbollah military exercise, May 2023 / published October 2023

War Noir's exercise review identifies rifles as **AK-103 and/or Iranian KL-133**, multiple **ORSIS T-5000-series** precision rifles, and at least one **possible Mk 14 EBR-RI**. The AK-103/KL-133 ambiguity and the tentative Mk 14 identification are preserved in the database rather than silently resolved.

Source: https://www.militantwire.com/p/weapons-used-by-hezbollah-during

### Hezbollah video, August 2024

War Noir identifies an **ORSIS SE T-5000M**, **M4 carbines with M203 under-barrel grenade launchers**, and **AKS-74 rifles**.

Source: https://x.com/war_noir/status/1822684681092919353  
Mirror: https://t.me/s/war_noir/17249

### Haddatha footage

War Noir identifies an Iranian SVD-pattern **Hoshdar-M**, Russian **AK-104 carbines**, and **PKM** machine guns with Hezbollah members.

Source: https://x.com/i/status/2059680222098665714  
Mirror: https://t.me/s/war_noir?before=24146

## Structured inventory

| Weapon | Caliber | Class | Grade | Confidence | Dataset state |
|---|---|---|---|---|---|
| AK-74 | 5.45x39mm | assault_rifle | A1 | high | include |
| AK-74M | 5.45x39mm | assault_rifle | A1 | high | include |
| AKS-74 | 5.45x39mm | assault_rifle | A1 | high | include |
| AKS-74U | 5.45x39mm | compact_carbine | A1 | high | include |
| AK-103 | 7.62x39mm | assault_rifle | A2/B1 | medium | include |
| KL-133 | 7.62x39mm | assault_rifle | A2/B1 | medium | include |
| AK-104 | 7.62x39mm | carbine | A1 | high | include |
| AKM / AKMS-pattern | 7.62x39mm | assault_rifle | B2 | medium | include |
| M4-pattern carbine | 5.56x45mm NATO | carbine | A1 | high | include |
| M16-pattern rifle | 5.56x45mm NATO | assault_rifle | B2 | low_to_medium | provisional |
| PKM | 7.62x54mmR | general_purpose_machine_gun | A1 | high | include |
| PKP Pecheneg | 7.62x54mmR | general_purpose_machine_gun | B2 | medium | secondary_confirmed |
| FN MAG | 7.62x51mm NATO | general_purpose_machine_gun | B2 | medium | secondary_confirmed |
| M240-family | 7.62x51mm NATO | general_purpose_machine_gun | B2 | medium | secondary_confirmed |
| Hoshdar-M | 7.62x54mmR | designated_marksman_sniper_rifle | A1 | high | include |
| ORSIS SE T-5000 / T-5000M | variant-dependent / unresolved in cited Hezbollah imagery | bolt_action_precision_rifle | A1 | high_model_low_chambering | include |
| Mk 14 EBR-RI (possible) | 7.62x51mm NATO | designated_marksman_battle_rifle | B1 | low_to_medium | provisional |
| NSV heavy machine gun (provisional) | 12.7x108mm | heavy_machine_gun | B2 | low_to_medium | provisional |

## What changed from v0.1

The earlier seed list was useful but too loose. v1.0 changes the methodology:

- **AK-74 and AK-74M are promoted** because the 2022 Radwan thread directly identifies both and says they were used mostly in that footage.
- **AKS-74U and PKM are strengthened** with the same direct Radwan evidence.
- **AKS-74, M4 and ORSIS T-5000M are strengthened** by a direct 2024 War Noir Hezbollah post.
- **AK-104 and Hoshdar-M are strengthened** by direct Haddatha identification.
- **AK-103 and KL-133 remain separate but ambiguous** for the 2023 exercise instead of pretending every rifle can be distinguished.
- **ORSIS chambering is deliberately left unresolved.**
- **AKM is retained**, but its strongest basis in this pass is public ODIN/WEG association rather than the stronger direct War Noir formal-unit imagery used for A-grade records.
- **PKP, FN MAG and M240 are added as secondary-confirmed public inventory entries.**
- **M16-family and NSV are kept provisional.**
- **Mk 14 EBR-RI remains provisional** because War Noir's own identification is tentative.
- Supporter-only Type 56 / Arsenal MG-M1 evidence from the earlier seed is **not promoted into the primary formal-unit baseline**.

## FieldSim integration

The main implementation rule should be:

```text
WeaponDefinition
    != CartridgeDefinition
    != ProjectileDefinition
    != MagazineDefinition
    != OpticDefinition
    != UnderBarrelWeaponDefinition
```

A fighter references a weapon instance, and that weapon instance references the ammunition currently loaded. This matters for the ORSIS family, AR-pattern rifles, AK variants, and future projectile/body-armor modeling.

Suggested fields:

```csharp
public sealed record WeaponDefinition(
    string Id,
    string Name,
    string Family,
    string WeaponClass,
    string? DefaultChamberingId,
    string Action,
    string FeedType,
    EvidenceGrade Evidence);

public sealed record FactionEquipmentAvailability(
    string FactionId,
    string WeaponId,
    AvailabilityClass Availability,
    EvidenceGrade Evidence);
```

Do **not** hard-code percentages such as "40% AK-74, 30% AKM." The current OSINT establishes presence much better than frequency. Scenario templates can later use broad synthetic availability classes such as `Common`, `Available`, `Uncommon`, `Specialist`, and `Provisional`.

## Recommended first implementation order

1. AK-74 / AK-74M / AKS-74 / AKS-74U.
2. AK-103 / KL-133 / AK-104 / AKM-pattern.
3. M4-pattern.
4. PKM.
5. Hoshdar-M.
6. ORSIS T-5000 family with chambering unset until explicitly selected.
7. PKP, FN MAG/M240 as secondary-confirmed support weapons.
8. Mk 14 EBR-RI, M16-family and NSV only as provisional scenario options.

## Research limitations

This remains a **best-effort OSINT dataset**, not a complete Hezbollah inventory. X/Twitter and Telegram cannot be exhaustively searched with full historical certainty, mirrors can omit posts, and some imagery is visually ambiguous. The report intentionally does not infer current deployment locations, stockpile sites, quantities, formal issue scales, unit allocations, or tactical procedures.

The database should therefore retain provenance on every entry and allow later evidence to upgrade, downgrade, split, merge, or remove a record without rewriting the combat engine.
