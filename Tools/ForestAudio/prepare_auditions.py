#!/usr/bin/env python3
"""Build traceable A/B/C audition previews for the Forest audio pilot."""

from __future__ import annotations

import argparse
import csv
import json
import warnings
from argparse import Namespace
from dataclasses import dataclass
from pathlib import Path

from forest_audio_wav import analyze, read_wav, render, sha256_file, write_pcm24


DEFAULT_WORK_ROOT = Path(r"D:\Unity\NewFPG_AudioWork\Forest")
DEFAULT_INDEX = DEFAULT_WORK_ROOT / "index" / "forest_candidate_shortlist.csv"
APPROVED_ROOT = DEFAULT_WORK_ROOT / "approved"
SIGNAL_ROOT = Path(
    r"F:\Audio\BOOM LIBRARY\FX65-未来科幻界面-Future Technology_Boom Library"
)
INSECT_FOLEY_PATH = Path(
    r"F:\Audio\BOOM LIBRARY\FX25-怪兽行动拟音-Creature Foley_Boom Library"
    r"\06-源音频工具-Creature CK\04-活动-Movement"
    r"\【昆虫运动 竹棍 敲击快】MOVEMENT INSECT Bamboo Stick Tap Fast.wav"
)
KHRON_SPELLS_ROOT = Path(
    r"F:\Audio\KHRON STUDIO（中译）\【KHRON】魔法咒语召唤法术第一辑-Spells Variations Vol 1_Khron Studio"
)
KHRON_SPELLS_V2_ROOT = Path(
    r"F:\Audio\KHRON STUDIO（中译）\【KHRON】魔法咒语召唤法术第二辑-Spells Variations Vol 1_Khron Studio"
)
KHRON_ELECTRIFIED_IMPACT_ROOT = KHRON_SPELLS_ROOT / "电击冲击-Electrified Impact"
KHRON_RAPID_AIR_SLASH_ROOT = KHRON_SPELLS_ROOT / "快速空气斩击-Rapid Air Slash"
KHRON_ARCANE_MINI_WHOOSH_ROOT = KHRON_SPELLS_ROOT / "奥术迷你呼啸-Arcane mini Whoosh"
KHRON_LITTLE_ARCANE_BLAST_ROOT = KHRON_SPELLS_ROOT / "小奥术冲击-Little Arcane Blast"
KHRON_BLOODLIGHT_PIERCE_ROOT = KHRON_SPELLS_V2_ROOT / "血光穿刺-Bloodlight Pierce"
EPIC_RETRO_GAME_IMPACTS_ROOT = Path(
    r"F:\Audio\Epic Stock Media\【ESM】复古游戏类-Retro Game_Epic Stock Media"
    r"\03-打击和武器-Impacts And Weapon"
)
EPIC_RETRO_GAME_UI_ROOT = Path(
    r"F:\Audio\Epic Stock Media\【ESM】复古游戏类-Retro Game_Epic Stock Media"
    r"\09-复古界面-UI"
)
EPIC_METROIDVANIA_EFFECT_ROOT = Path(
    r"F:\Audio\Epic Stock Media\【ESM】横轴动作游戏类-Metroidvania Game SFX_Epic Stock Media"
    r"\12-效果-Effect"
)
EPIC_METROIDVANIA_UI_ROOT = Path(
    r"F:\Audio\Epic Stock Media\【ESM】横轴动作游戏类-Metroidvania Game SFX_Epic Stock Media"
    r"\13-用户界面-UI"
)
EPIC_FANTASY_GAME_UI_ROOT = Path(
    r"F:\Audio\Epic Stock Media\【ESM】奇幻冒险类第二辑-Fantasy Game 2_Epic Stock Media"
    r"\17-奇幻冒险组件-Fantasy Game\14-用户界面-UI"
)
EPIC_FANTASY_GAME_MAGIC_ELECTRIC_ROOT = Path(
    r"F:\Audio\Epic Stock Media\【ESM】奇幻冒险类第二辑-Fantasy Game 2_Epic Stock Media"
    r"\17-奇幻冒险组件-Fantasy Game\10-魔法-Magic\Electric【电子产品】"
)


EPIC_BATTLE_ROYALE_AMBIENCE_ROOT = Path(
    r"F:\Audio\Epic Stock Media\【ESM】枪战大逃杀类-Battle Royale Game_Epic Stock Media"
    r"\01-环境循环-Ambience Loops"
)
BOOM_ANIME_ESSENTIALS_ROOT = Path(
    r"F:\Audio\BOOM LIBRARY\FX13-动漫设计合集-Anime Essentials_Boom Library"
)
BOOM_MAGIC_WISP_CK_ROOT = Path(
    r"F:\Audio\BOOM LIBRARY\FX26-光明黑暗魔法-Magic Wisp Designed_Boom Library"
    r"\源音频工具-Construction Kit"
)
BOOM_MAGIC_ALCHEMY_CK_ROOT = Path(
    r"F:\Audio\BOOM LIBRARY\FX43-炼金药剂魔法-Magic Alchemy_Boom Library"
    r"\源音频文件-Construction Kit"
)


@dataclass(frozen=True)
class IndexedSource:
    event_id: str
    rank: int
    gain_db: float = 0.0
    trim_start: float = 0.0
    pitch_semitones: float = 0.0
    delay_ms: float = 0.0
    repair_truncated_tail: bool = False


@dataclass(frozen=True)
class DirectSource:
    database: str
    recid: int
    path: Path
    gain_db: float = 0.0
    trim_start: float = 0.0
    pitch_semitones: float = 0.0
    delay_ms: float = 0.0
    repair_truncated_tail: bool = False
    description: str = ""
    keywords: str = ""
    category: str = ""
    sub_category: str = ""
    library: str = ""
    audio_crc: str = ""


@dataclass(frozen=True)
class AuditionOption:
    event_id: str
    label: str
    output_name: str
    intent: str
    sources: tuple[IndexedSource | DirectSource, ...]
    channels: str
    trim_start: float = 0.0
    duration: float | None = None
    fade_in_ms: float = 2.0
    fade_out_ms: float = 10.0
    loop_crossfade_ms: float = 0.0
    peak_dbfs: float = -3.0
    transient_boost_db: float = 0.0
    transient_window_ms: float = 0.0
    saturation_drive: float = 0.0


def signal_source(number: int, insect_rank: int) -> tuple[DirectSource, IndexedSource]:
    filename = f"FX_Signal_Warning_Fast_{number:02d}【信号 警告快速{number:02d}】.wav"
    return (
        DirectSource("BOOM_LIBRARY", 19930 + number, SIGNAL_ROOT / filename, 0.0),
        IndexedSource("event.burstbug.fast.telegraph", insect_rank, -10.0),
    )


def burstbug_warning_source(
    number: int,
    insect_trim_start: float,
) -> tuple[DirectSource, DirectSource]:
    filename = f"FX_Signal_Warning_Fast_{number:02d}【信号 警告快速{number:02d}】.wav"
    return (
        DirectSource("BOOM_LIBRARY", 19930 + number, SIGNAL_ROOT / filename),
        DirectSource(
            "BOOM_LIBRARY",
            4342,
            INSECT_FOLEY_PATH,
            gain_db=-14.0,
            trim_start=insect_trim_start,
        ),
    )


REALISTIC_AUDITIONS = (
    AuditionOption(
        "event.fei.primary.attack.0",
        "A",
        "01_Fei_Primary_Attack_A.wav",
        "compact muzzle 1 with clean mechanical layer 2",
        (IndexedSource("event.fei.primary.attack.0", 6), IndexedSource("event.fei.primary.attack.0", 1, -8.0)),
        "mono",
    ),
    AuditionOption(
        "event.fei.primary.attack.0",
        "B",
        "01_Fei_Primary_Attack_B.wav",
        "compact muzzle 2 with crisp mechanical layer 1",
        (IndexedSource("event.fei.primary.attack.0", 7), IndexedSource("event.fei.primary.attack.0", 3, -9.0)),
        "mono",
    ),
    AuditionOption(
        "event.fei.primary.attack.0",
        "C",
        "01_Fei_Primary_Attack_C.wav",
        "compact muzzle 3 with clean mechanical layer 2",
        (IndexedSource("event.fei.primary.attack.0", 8), IndexedSource("event.fei.primary.attack.0", 1, -8.0)),
        "mono",
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "A",
        "02_Fei_Primary_Hit_A.wav",
        "dry low generic hit 01",
        (IndexedSource("event.fei.primary.hit.base", 1),),
        "mono",
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "B",
        "02_Fei_Primary_Hit_B.wav",
        "dry generic hit 10",
        (IndexedSource("event.fei.primary.hit.base", 2),),
        "mono",
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "C",
        "02_Fei_Primary_Hit_C.wav",
        "shorter low generic hit 03",
        (IndexedSource("event.fei.primary.hit.base", 3),),
        "mono",
    ),
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        "A",
        "03_Fei_Primary_Weakpoint_A.wav",
        "metallic point boost 1 over a constant dry hit body",
        (IndexedSource("event.fei.primary.hit.base", 1, -7.0), IndexedSource("event.fei.primary.hit.weakpoint", 6)),
        "mono",
        duration=0.75,
    ),
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        "B",
        "03_Fei_Primary_Weakpoint_B.wav",
        "metallic no-tone point boost over a constant dry hit body",
        (IndexedSource("event.fei.primary.hit.base", 1, -7.0), IndexedSource("event.fei.primary.hit.weakpoint", 7)),
        "mono",
        duration=0.75,
    ),
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        "C",
        "03_Fei_Primary_Weakpoint_C.wav",
        "bright swept impact over a constant dry hit body",
        (IndexedSource("event.fei.primary.hit.base", 1, -7.0), IndexedSource("event.fei.primary.hit.weakpoint", 1)),
        "mono",
        duration=0.75,
    ),
    AuditionOption(
        "event.burstbug.fast.telegraph",
        "A",
        "04_Burstbug_Fast_Telegraph_A.wav",
        "fast warning signal 1 with short insect growl 2",
        signal_source(1, 7),
        "mono",
        duration=0.34,
        fade_out_ms=6.0,
    ),
    AuditionOption(
        "event.burstbug.fast.telegraph",
        "B",
        "04_Burstbug_Fast_Telegraph_B.wav",
        "fast warning signal 2 with short insect growl 1",
        signal_source(2, 6),
        "mono",
        duration=0.34,
        fade_out_ms=6.0,
    ),
    AuditionOption(
        "event.burstbug.fast.telegraph",
        "C",
        "04_Burstbug_Fast_Telegraph_C.wav",
        "fast warning signal 3 with short insect growl 3",
        signal_source(3, 8),
        "mono",
        duration=0.34,
        fade_out_ms=6.0,
    ),
    AuditionOption(
        "event.forest.ambience.loop",
        "A",
        "05_Forest_Ambience_A.wav",
        "steady forest wind with sparse insects and no authored bird call",
        (IndexedSource("event.forest.ambience.loop", 1),),
        "stereo",
        trim_start=10.0,
        duration=15.0,
        fade_in_ms=100.0,
        fade_out_ms=100.0,
        peak_dbfs=-12.0,
    ),
    AuditionOption(
        "event.forest.ambience.loop",
        "B",
        "05_Forest_Ambience_B.wav",
        "lighter summer forest wind and insects",
        (IndexedSource("event.forest.ambience.loop", 5),),
        "stereo",
        trim_start=10.0,
        duration=15.0,
        fade_in_ms=100.0,
        fade_out_ms=100.0,
        peak_dbfs=-12.0,
    ),
    AuditionOption(
        "event.forest.ambience.loop",
        "C",
        "05_Forest_Ambience_C.wav",
        "heavier summer forest wind and insects",
        (IndexedSource("event.forest.ambience.loop", 4),),
        "stereo",
        trim_start=10.0,
        duration=15.0,
        fade_in_ms=100.0,
        fade_out_ms=100.0,
        peak_dbfs=-12.0,
    ),
)

STYLIZED_AUDITIONS = (
    AuditionOption(
        "event.fei.primary.attack.0",
        "A",
        "01_Fei_Primary_Attack_A.wav",
        "bright glitchy zap shot 1 with an immediate arcade onset",
        (IndexedSource("event.fei.primary.attack.0", 1, trim_start=0.12),),
        "mono",
        duration=0.65,
    ),
    AuditionOption(
        "event.fei.primary.attack.0",
        "B",
        "01_Fei_Primary_Attack_B.wav",
        "bright glitchy zap shot 2 with a tighter pulse contour",
        (IndexedSource("event.fei.primary.attack.0", 3, trim_start=0.11),),
        "mono",
        duration=0.65,
    ),
    AuditionOption(
        "event.fei.primary.attack.0",
        "C",
        "01_Fei_Primary_Attack_C.wav",
        "bright glitchy zap shot 3 with the strongest tonal motion",
        (IndexedSource("event.fei.primary.attack.0", 5, trim_start=0.22),),
        "mono",
        duration=0.7,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "A",
        "02_Fei_Primary_Hit_A.wav",
        "magic electric projectile impact 2, tightened for rapid fire",
        (IndexedSource("event.fei.primary.hit.base", 2, trim_start=0.20),),
        "mono",
        duration=0.5,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "B",
        "02_Fei_Primary_Hit_B.wav",
        "magic electric projectile impact 4 with a sharper crackle",
        (IndexedSource("event.fei.primary.hit.base", 3, trim_start=0.27),),
        "mono",
        duration=0.5,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "C",
        "02_Fei_Primary_Hit_C.wav",
        "scratched electric impact with a more playful texture",
        (IndexedSource("event.fei.primary.hit.base", 8, trim_start=0.10),),
        "mono",
        duration=0.5,
    ),
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        "A",
        "03_Fei_Primary_Weakpoint_A.wav",
        "tight electric body plus a bass magic reward chime",
        (IndexedSource("event.fei.primary.hit.base", 2, -8.0, 0.20), IndexedSource("event.fei.primary.hit.weakpoint", 1)),
        "mono",
        duration=0.65,
    ),
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        "B",
        "03_Fei_Primary_Weakpoint_B.wav",
        "tight electric body plus a clearer reward bell",
        (IndexedSource("event.fei.primary.hit.base", 2, -8.0, 0.20), IndexedSource("event.fei.primary.hit.weakpoint", 3)),
        "mono",
        duration=0.65,
    ),
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        "C",
        "03_Fei_Primary_Weakpoint_C.wav",
        "tight electric body plus the shortest glitter bell",
        (IndexedSource("event.fei.primary.hit.base", 2, -8.0, 0.20), IndexedSource("event.fei.primary.hit.weakpoint", 5, trim_start=0.045)),
        "mono",
        duration=0.65,
    ),
    AuditionOption(
        "event.burstbug.fast.telegraph",
        "A",
        "04_Burstbug_Fast_Telegraph_A.wav",
        "compact futuristic bass warning 2",
        (IndexedSource("event.burstbug.fast.telegraph", 2),),
        "mono",
    ),
    AuditionOption(
        "event.burstbug.fast.telegraph",
        "B",
        "04_Burstbug_Fast_Telegraph_B.wav",
        "shorter futuristic bass warning 3",
        (IndexedSource("event.burstbug.fast.telegraph", 3),),
        "mono",
    ),
    AuditionOption(
        "event.burstbug.fast.telegraph",
        "C",
        "04_Burstbug_Fast_Telegraph_C.wav",
        "very short glitchy laser chirp",
        (IndexedSource("event.burstbug.fast.telegraph", 6),),
        "mono",
    ),
    AuditionOption(
        "event.forest.ambience.loop",
        "A",
        "05_Forest_Ambience_A.wav",
        "steady forest wind with sparse insects and no authored bird call",
        (IndexedSource("event.forest.ambience.loop", 1),),
        "stereo",
        trim_start=10.0,
        duration=15.0,
        fade_in_ms=100.0,
        fade_out_ms=100.0,
        peak_dbfs=-12.0,
    ),
    AuditionOption(
        "event.forest.ambience.loop",
        "B",
        "05_Forest_Ambience_B.wav",
        "lighter summer forest wind and insects",
        (IndexedSource("event.forest.ambience.loop", 5),),
        "stereo",
        trim_start=10.0,
        duration=15.0,
        fade_in_ms=100.0,
        fade_out_ms=100.0,
        peak_dbfs=-12.0,
    ),
    AuditionOption(
        "event.forest.ambience.loop",
        "C",
        "05_Forest_Ambience_C.wav",
        "heavier summer forest wind and insects",
        (IndexedSource("event.forest.ambience.loop", 4),),
        "stereo",
        trim_start=10.0,
        duration=15.0,
        fade_in_ms=100.0,
        fade_out_ms=100.0,
        peak_dbfs=-12.0,
    ),
)

HIT_V3_AUDITIONS = (
    AuditionOption(
        "event.fei.primary.hit.base",
        "A",
        "02_Fei_Primary_Hit_A.wav",
        "digital liquid hit-pop 1 with a 31ms main peak",
        (IndexedSource("event.fei.primary.hit.base", 2),),
        "mono",
        fade_out_ms=6.0,
        peak_dbfs=-4.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "B",
        "02_Fei_Primary_Hit_B.wav",
        "tighter digital liquid hit-pop 2 with a 26ms main peak",
        (IndexedSource("event.fei.primary.hit.base", 3),),
        "mono",
        fade_out_ms=6.0,
        peak_dbfs=-4.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "C",
        "02_Fei_Primary_Hit_C.wav",
        "shortest digital bubble hit-pop with a 5ms main peak",
        (IndexedSource("event.fei.primary.hit.base", 6),),
        "mono",
        fade_out_ms=6.0,
        peak_dbfs=-4.0,
    ),
)

SEMANTIC_HIT_V5_AUDITIONS = (
    AuditionOption(
        "event.fei.primary.hit.base",
        "A",
        "02_Fei_Primary_Hit_A.wav",
        "short crunchy processed energy impact from BOOM Magic/Arcane",
        (IndexedSource("event.fei.primary.hit.base", 29, trim_start=0.24),),
        "mono",
        duration=0.27,
        fade_out_ms=6.0,
        peak_dbfs=-4.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "B",
        "02_Fei_Primary_Hit_B.wav",
        "immediate arcane energy snap from the KHRON MAGIC/SPELL family",
        (IndexedSource("event.fei.primary.hit.base", 7),),
        "mono",
        duration=0.25,
        fade_out_ms=8.0,
        peak_dbfs=-4.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "C",
        "02_Fei_Primary_Hit_C.wav",
        "small arcane projectile impact with its main peak moved to the hit frame",
        (
            DirectSource(
                "KHRON",
                2696,
                KHRON_SPELLS_ROOT / "小奥术冲击-Little Arcane Blast" / "【MAGSpel_小型奥术冲击_03_KRST】MAGSpel_Little Arcane Blast 03_KRST.wav",
                trim_start=0.08,
            ),
        ),
        "mono",
        duration=0.28,
        fade_out_ms=8.0,
        peak_dbfs=-4.0,
    ),
)

SEMANTIC_HIT_V6_AUDITIONS = (
    AuditionOption(
        "event.fei.primary.hit.base",
        "A",
        "02_Fei_Primary_Hit_A.wav",
        "short crunchy processed energy impact from BOOM Magic/Arcane",
        (IndexedSource("event.fei.primary.hit.base", 29, trim_start=0.32),),
        "mono",
        duration=0.20,
        fade_out_ms=6.0,
        peak_dbfs=-4.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "B",
        "02_Fei_Primary_Hit_B.wav",
        "immediate arcane energy snap from the KHRON MAGIC/SPELL family",
        (IndexedSource("event.fei.primary.hit.base", 7),),
        "mono",
        duration=0.20,
        fade_out_ms=8.0,
        peak_dbfs=-4.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "C",
        "02_Fei_Primary_Hit_C.wav",
        "tight electric weapon impact from the KHRON MAGIC/SPELL family",
        (IndexedSource("event.fei.primary.hit.base", 16, trim_start=0.05),),
        "mono",
        duration=0.20,
        fade_out_ms=8.0,
        peak_dbfs=-4.0,
    ),
)

HIT_VARIATIONS_V1_AUDITIONS = (
    AuditionOption(
        "event.fei.primary.hit.base",
        "V02",
        "SFX_Fei_Primary_Hit_02_Audition.wav",
        "Arcane Snap 01 sibling, tightened to match the approved hit envelope",
        (IndexedSource("event.fei.primary.hit.base", 5, trim_start=0.065),),
        "mono",
        duration=0.20,
        fade_out_ms=8.0,
        peak_dbfs=-4.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "V03",
        "SFX_Fei_Primary_Hit_03_Audition.wav",
        "Arcane Snap 02 sibling, tightened to match the approved hit envelope",
        (IndexedSource("event.fei.primary.hit.base", 4, trim_start=0.105),),
        "mono",
        duration=0.20,
        fade_out_ms=8.0,
        peak_dbfs=-4.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.base",
        "V04",
        "SFX_Fei_Primary_Hit_04_Audition.wav",
        "Arcane Snap 03 sibling, tightened to match the approved hit envelope",
        (IndexedSource("event.fei.primary.hit.base", 6, trim_start=0.24),),
        "mono",
        duration=0.20,
        fade_out_ms=8.0,
        peak_dbfs=-4.0,
    ),
)

WEAKPOINT_V1_AUDITIONS = (
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        "A",
        "SFX_Fei_Primary_Weakpoint_A.wav",
        "approved hit body plus the immediate Reward 3 bell layer",
        (
            DirectSource(
                "ForestApproved",
                0,
                APPROVED_ROOT / "SFX_Fei_Primary_Hit_01.wav",
                gain_db=-5.0,
            ),
            IndexedSource("event.fei.primary.hit.weakpoint", 3),
        ),
        "mono",
        duration=0.32,
        fade_out_ms=12.0,
        peak_dbfs=-4.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        "B",
        "SFX_Fei_Primary_Weakpoint_B.wav",
        "approved hit body plus the clear Reward 4 bell layer",
        (
            DirectSource(
                "ForestApproved",
                0,
                APPROVED_ROOT / "SFX_Fei_Primary_Hit_02.wav",
                gain_db=-5.0,
            ),
            IndexedSource(
                "event.fei.primary.hit.weakpoint",
                4,
                trim_start=0.01,
            ),
        ),
        "mono",
        duration=0.32,
        fade_out_ms=12.0,
        peak_dbfs=-4.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        "C",
        "SFX_Fei_Primary_Weakpoint_C.wav",
        "approved hit body plus the shortest Reward 5 glitter-bell layer",
        (
            DirectSource(
                "ForestApproved",
                0,
                APPROVED_ROOT / "SFX_Fei_Primary_Hit_03.wav",
                gain_db=-5.0,
            ),
            IndexedSource(
                "event.fei.primary.hit.weakpoint",
                5,
                trim_start=0.045,
            ),
        ),
        "mono",
        duration=0.32,
        fade_out_ms=12.0,
        peak_dbfs=-4.0,
    ),
)


def approved_hit_source(number: int, **kwargs: float) -> DirectSource:
    return DirectSource(
        "ForestApproved",
        0,
        APPROVED_ROOT / f"SFX_Fei_Primary_Hit_{number:02d}.wav",
        **kwargs,
    )


WEAKPOINT_V2_BODY_AUDITIONS = tuple(
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        str(number),
        f"SFX_Fei_Primary_Weakpoint_{number:02d}.wav",
        "approved base-hit variant reinforced with a lower-pitched copy and stronger transient",
        (
            approved_hit_source(number),
            approved_hit_source(number, gain_db=-8.0, pitch_semitones=-4.0, delay_ms=2.0),
        ),
        "mono",
        duration=0.20,
        fade_out_ms=8.0,
        peak_dbfs=-3.0,
        transient_boost_db=3.0,
        transient_window_ms=55.0,
        saturation_drive=1.4,
    )
    for number in range(1, 5)
)


WEAKPOINT_V3_REINFORCED_AUDITIONS = tuple(
    AuditionOption(
        "event.fei.primary.hit.weakpoint",
        str(number),
        f"SFX_Fei_Primary_Weakpoint_{number:02d}.wav",
        "approved base-hit variant reinforced without adding a new sound layer",
        (approved_hit_source(number),),
        "mono",
        duration=0.20,
        fade_out_ms=8.0,
        peak_dbfs=-2.5,
        transient_boost_db=4.0,
        transient_window_ms=45.0,
        saturation_drive=1.25,
    )
    for number in range(1, 5)
)


ATTACK_VARIATIONS_V1_AUDITIONS = (
    AuditionOption(
        "event.fei.primary.attack.0",
        "1",
        "SFX_Fei_Primary_Attack_01.wav",
        "Alien Game Zap Gun Shot 1 Light, tightened for the primary-fire cadence",
        (IndexedSource("event.fei.primary.attack.0", 1, trim_start=0.12),),
        "mono",
        duration=0.38,
        fade_out_ms=10.0,
        peak_dbfs=-3.0,
    ),
    AuditionOption(
        "event.fei.primary.attack.0",
        "2",
        "SFX_Fei_Primary_Attack_02.wav",
        "Alien Game Zap Gun Shot 2 Light, tightened for the primary-fire cadence",
        (IndexedSource("event.fei.primary.attack.0", 3, trim_start=0.11),),
        "mono",
        duration=0.38,
        fade_out_ms=10.0,
        peak_dbfs=-3.0,
    ),
    AuditionOption(
        "event.fei.primary.attack.0",
        "3",
        "SFX_Fei_Primary_Attack_03.wav",
        "Alien Game Zap Gun Shot 3 Light, tightened for the primary-fire cadence",
        (IndexedSource("event.fei.primary.attack.0", 5, trim_start=0.22),),
        "mono",
        duration=0.38,
        fade_out_ms=10.0,
        peak_dbfs=-3.0,
    ),
    AuditionOption(
        "event.fei.primary.attack.0",
        "4",
        "SFX_Fei_Primary_Attack_04.wav",
        "Alien Game Zap Gun Shot 4 Light, tightened for the primary-fire cadence",
        (IndexedSource("event.fei.primary.attack.0", 9, trim_start=0.55),),
        "mono",
        duration=0.38,
        fade_out_ms=10.0,
        peak_dbfs=-3.0,
    ),
)


ENVIRONMENT_HIT_V1_AUDITIONS = (
    AuditionOption(
        "event.fei.primary.hit.environment",
        "1",
        "SFX_Fei_Primary_EnvironmentHit_01.wav",
        "Electrified Impact 05 tightened into a dry environmental energy strike",
        (
            DirectSource(
                "KHRON",
                2847,
                KHRON_ELECTRIFIED_IMPACT_ROOT
                / "【MAGSpel_电击冲击 05_KRST】MAGSpel_Electrified Impact 05_KRST.wav",
                trim_start=0.065,
            ),
        ),
        "mono",
        duration=0.22,
        fade_out_ms=8.0,
        peak_dbfs=-5.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.environment",
        "2",
        "SFX_Fei_Primary_EnvironmentHit_02.wav",
        "Electrified Impact 02 tightened into a dry environmental energy strike",
        (
            DirectSource(
                "KHRON",
                2843,
                KHRON_ELECTRIFIED_IMPACT_ROOT
                / "【MAGSpel_带电冲击 02_KRST】MAGSpel_Electrified Impact 02_KRST.wav",
                trim_start=0.080,
            ),
        ),
        "mono",
        duration=0.22,
        fade_out_ms=8.0,
        peak_dbfs=-5.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.environment",
        "3",
        "SFX_Fei_Primary_EnvironmentHit_03.wav",
        "Electrified Impact 01 tightened into a dry environmental energy strike",
        (
            DirectSource(
                "KHRON",
                2844,
                KHRON_ELECTRIFIED_IMPACT_ROOT
                / "【MAGSpel_电击冲击 01_KRST】MAGSpel_Electrified Impact 01_KRST.wav",
                trim_start=0.140,
            ),
        ),
        "mono",
        duration=0.22,
        fade_out_ms=8.0,
        peak_dbfs=-5.0,
    ),
    AuditionOption(
        "event.fei.primary.hit.environment",
        "4",
        "SFX_Fei_Primary_EnvironmentHit_04.wav",
        "Electrified Impact 07 tightened into a dry environmental energy strike",
        (
            DirectSource(
                "KHRON",
                2849,
                KHRON_ELECTRIFIED_IMPACT_ROOT
                / "【MAGSpel_电击冲击 07_KRST】MAGSpel_Electrified Impact 07_KRST.wav",
                trim_start=0.145,
            ),
        ),
        "mono",
        duration=0.22,
        fade_out_ms=8.0,
        peak_dbfs=-5.0,
    ),
)


BURSTBUG_TELEGRAPH_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug.fast.telegraph",
        str(number),
        f"SFX_Burstbug_Fast_Telegraph_{number:02d}.wav",
        "BOOM Warning Fast signal with a restrained high-frequency insect click texture",
        burstbug_warning_source(number, insect_trim_start),
        "mono",
        duration=0.32,
        fade_out_ms=6.0,
        peak_dbfs=-4.0,
    )
    for number, insect_trim_start in ((1, 10.24), (2, 7.60), (3, 1.12))
)


BURSTBUG_RELEASE_V1_SOURCES = (
    (1, 2732, "02", 0.168),
    (2, 2733, "03", 0.167),
    (3, 2734, "04", 0.165),
    (4, 2731, "01", 0.166),
)


BURSTBUG_RELEASE_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug.fast.release",
        str(output_number),
        f"SFX_Burstbug_Fast_Release_{output_number:02d}.wav",
        "Rapid Air Slash sibling tightened around its forward energy peak",
        (
            DirectSource(
                "KHRON",
                recid,
                KHRON_RAPID_AIR_SLASH_ROOT
                / f"【MAGSpel_快速空气斩击 {source_number}_KRST】MAGSpel_Rapid Air Slash {source_number}_KRST.wav",
                trim_start=trim_start,
            ),
        ),
        "mono",
        duration=0.22,
        fade_out_ms=8.0,
        peak_dbfs=-5.0,
    )
    for output_number, recid, source_number, trim_start in BURSTBUG_RELEASE_V1_SOURCES
)


BURSTBUG_VOLLEY_TELEGRAPH_V1_SOURCES = (
    (
        1,
        21994,
        "【效果-抬头显示器_扫描_抬头显示器_数字_科幻_升起】Effect-hud_scan_hud_digital_sci_fi_rise.wav",
    ),
    (
        2,
        22007,
        "【效果-界面扫描_1_界面数字科幻升起】Effect-hud_scan_1_hud_digital_sci_fi_rise.wav",
    ),
    (
        3,
        22008,
        "【效果-界面扫描_2_界面数字科幻升起】Effect-hud_scan_2_hud_digital_sci_fi_rise.wav",
    ),
)

BURSTBUG_VOLLEY_SIGNAL_PULSES = (
    (
        19962,
        "HIT_Signal_Single_Beep_Mid_01【信号 哔哔声 中01】.wav",
        -12.0,
        0.0,
        80.0,
    ),
    (
        19963,
        "HIT_Signal_Single_Beep_Mid_02【信号 哔哔声 中02】.wav",
        -10.0,
        2.0,
        360.0,
    ),
    (
        19964,
        "HIT_Signal_Single_Beep_Mid_03【信号 哔哔声 中03】.wav",
        -8.0,
        4.0,
        640.0,
    ),
)

BURSTBUG_VOLLEY_TELEGRAPH_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug-interceptable-volley.telegraph",
        str(output_number),
        f"SFX_Burstbug_Volley_Telegraph_{output_number:02d}.wav",
        "Metroidvania digital scan sibling with three ascending BOOM signal pulses marking the interceptable volley",
        (
            DirectSource(
                "Epic_Stock_Media",
                scan_recid,
                EPIC_METROIDVANIA_EFFECT_ROOT / scan_filename,
                gain_db=-2.0,
                repair_truncated_tail=True,
                description=(
                    "designed, magic, retro arcade sci-fi game alert"
                ),
                keywords="HUD digital sci-fi scan rise",
                category="DESIGNED",
                library="Metroidvania Game SFX",
            ),
        )
        + tuple(
            DirectSource(
                "BOOM_LIBRARY",
                recid,
                SIGNAL_ROOT / filename,
                gain_db=gain_db,
                pitch_semitones=pitch_semitones,
                delay_ms=delay_ms,
                keywords="Signal single beep mid",
                category="USER INTERFACE",
                library="Future Technology",
            )
            for recid, filename, gain_db, pitch_semitones, delay_ms
            in BURSTBUG_VOLLEY_SIGNAL_PULSES
        ),
        "mono",
        duration=0.95,
        fade_in_ms=2.0,
        fade_out_ms=12.0,
        peak_dbfs=-5.0,
        saturation_drive=1.04,
    )
    for output_number, scan_recid, scan_filename
    in BURSTBUG_VOLLEY_TELEGRAPH_V1_SOURCES
)

BURSTBUG_VOLLEY_RELEASE_V1_SOURCES = (
    (
        1,
        8937,
        "【魔法元素 魔法电击快速狙击攻击精确短哨声01】MAGElem_Magic Electric Quick Snipe Attack Precise Short Whistle 01_ESM_FG2.wav",
    ),
    (
        2,
        8938,
        "【魔法元素 魔法电击快速狙击攻击精确短哨声02】MAGElem_Magic Electric Quick Snipe Attack Precise Short Whistle 02_ESM_FG2.wav",
    ),
    (
        3,
        8939,
        "【魔法元素 魔法电击快速狙击攻击精确短哨声03】MAGElem_Magic Electric Quick Snipe Attack Precise Short Whistle 03_ESM_FG2.wav",
    ),
    (
        4,
        8967,
        "【魔法元素 魔电快速射击精准短哨声 04 2】MAGElem_Magic Electric Quick Snipe Attack Precise Short Whistle 04_ESM_FG2.wav",
    ),
    (
        5,
        8968,
        "【魔法元素 魔电快速射击精准短哨声 05 2】MAGElem_Magic Electric Quick Snipe Attack Precise Short Whistle 05_ESM_FG2.wav",
    ),
)

BURSTBUG_VOLLEY_RELEASE_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug-interceptable-volley.release",
        str(output_number),
        f"SFX_Burstbug_Volley_Release_{output_number:02d}.wav",
        "Three-shot magical-electric volley built from one Fantasy Game 2 Quick Snipe sibling",
        tuple(
            DirectSource(
                "Epic_Stock_Media",
                recid,
                EPIC_FANTASY_GAME_MAGIC_ELECTRIC_ROOT / filename,
                gain_db=gain_db,
                trim_start=0.11,
                pitch_semitones=pitch_semitones,
                delay_ms=delay_ms,
                description=(
                    "sharp, punchy, intense, focused, crisp, spark, crackle"
                ),
                keywords=(
                    "Magic Electric Quick Snipe Attack Precise Short Whistle"
                ),
                category="MAGIC",
                sub_category="ELEMENTAL",
                library="Fantasy Game 2",
            )
            for gain_db, pitch_semitones, delay_ms
            in (
                (-6.0, -1.5, 0.0),
                (-3.0, 0.0, 72.0),
                (0.0, 1.5, 144.0),
            )
        ),
        "mono",
        duration=0.42,
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-4.5,
        transient_boost_db=1.0,
        transient_window_ms=45.0,
        saturation_drive=1.06,
    )
    for output_number, recid, filename
    in BURSTBUG_VOLLEY_RELEASE_V1_SOURCES
)


BURSTBUG_VOLLEY_PROJECTILE_C02_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug-interceptable-volley.projectile",
        str(output_number),
        f"SFX_Burstbug_Volley_Projectile_{output_number:02d}.wav",
        "Little Chime of Enchanted Speed sibling shaped as a light magical projectile-flight identity",
        (
            IndexedSource(
                "event.burstbug-interceptable-volley.projectile",
                candidate_rank,
                gain_db=-3.0,
            ),
        ),
        "mono",
        duration=0.62,
        fade_in_ms=2.0,
        fade_out_ms=18.0,
        peak_dbfs=-9.0,
        transient_boost_db=0.5,
        transient_window_ms=35.0,
        saturation_drive=1.02,
    )
    for output_number, candidate_rank in enumerate(range(1, 7), start=1)
)


BURSTBUG_VOLLEY_INTERCEPTION_C02_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug-interceptable-volley.interception",
        str(output_number),
        f"SFX_Burstbug_Volley_Interception_{output_number:02d}.wav",
        "Retro Game Simple Impact sibling reinforced by a bright enchanted-speed chime for positive interception punctuation",
        (
            IndexedSource(
                "event.burstbug-interceptable-volley.interception",
                impact_rank,
                gain_db=-2.0,
                pitch_semitones=1.5,
            ),
            IndexedSource(
                "event.burstbug-interceptable-volley.projectile",
                chime_rank,
                gain_db=-9.0,
                pitch_semitones=7.0,
                delay_ms=12.0,
            ),
        ),
        "mono",
        duration=0.30,
        fade_in_ms=1.0,
        fade_out_ms=10.0,
        peak_dbfs=-5.0,
        transient_boost_db=1.5,
        transient_window_ms=32.0,
        saturation_drive=1.04,
    )
    for output_number, impact_rank, chime_rank in (
        (1, 37, 1),
        (2, 38, 2),
        (3, 39, 3),
    )
)


BURSTBUG_VOLLEY_IMPACT_C02_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug-interceptable-volley.impact",
        str(output_number),
        f"SFX_Burstbug_Volley_Impact_{output_number:02d}.wav",
        "Little Arcane Blast sibling tightened into a compact magical projectile explosion without the Fast Hit needle layer",
        (
            IndexedSource(
                "event.burstbug-interceptable-volley.impact",
                candidate_rank,
                gain_db=-2.0,
            ),
        ),
        "mono",
        duration=0.36,
        fade_in_ms=1.0,
        fade_out_ms=10.0,
        peak_dbfs=-5.0,
        transient_boost_db=1.25,
        transient_window_ms=42.0,
        saturation_drive=1.05,
    )
    for output_number, candidate_rank in enumerate(range(12, 18), start=1)
)


BURSTBUG_VOLLEY_C02_AUDITIONS = (
    BURSTBUG_VOLLEY_PROJECTILE_C02_AUDITIONS
    + BURSTBUG_VOLLEY_INTERCEPTION_C02_AUDITIONS
    + BURSTBUG_VOLLEY_IMPACT_C02_AUDITIONS
)


BURSTBUG_PROJECTILE_V1_SOURCES = (
    (1, 2684, "04"),
    (2, 2688, "08"),
    (3, 2681, "01"),
    (4, 2689, "09"),
)


BURSTBUG_PROJECTILE_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug.fast.projectile",
        str(output_number),
        f"SFX_Burstbug_Fast_Projectile_{output_number:02d}.wav",
        "Arcane Mini Whoosh sibling kept subtle for projectile motion",
        (
            DirectSource(
                "KHRON",
                recid,
                KHRON_ARCANE_MINI_WHOOSH_ROOT
                / f"【MAGSpel_奥术迷你呼啸声{source_number}_KRST】MAGSpel_Arcane Mini Whoosh {source_number}_KRST.wav",
            ),
        ),
        "mono",
        duration=0.55,
        fade_out_ms=20.0,
        peak_dbfs=-10.0,
    )
    for output_number, recid, source_number in BURSTBUG_PROJECTILE_V1_SOURCES
)


BURSTBUG_HIT_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug.fast.hit",
        str(output_number),
        f"SFX_Burstbug_Fast_Hit_{output_number:02d}.wav",
        "Retro Game Low Take Damage sibling used as a restrained projectile-contact chirp",
        (IndexedSource("event.burstbug.fast.hit", candidate_rank),),
        "mono",
        duration=0.24,
        fade_out_ms=8.0,
        peak_dbfs=-8.0,
    )
    for output_number, candidate_rank in enumerate(range(3, 7), start=1)
)


KHRON_BLOODLIGHT_PIERCE_DESCRIPTION = (
    "Magic, Spell, Impact, Sharp, Strike, Arcane, Energy, Blade, Hit, Power, "
    "Magical Impact, Slash, Mystic, Force, Burst, Attack, Piercing, Magical "
    "Strike, Enchantment, Surge, Magic Blade, Clash, Sound Design, Sorcery, "
    "Cut, Aura, Intensity, Fantasy, Magical Combat, Precision, Swift, Shinny, "
    "Shimmer"
)
KHRON_LITTLE_ARCANE_BLAST_DESCRIPTION = (
    "Missile, Magic, Fast, Speed, Light, Strike, Arcane, Lightning, Surge, "
    "Dash, Luminous, Photon, Dart, Swift, Spell, Projectile, Rush, Energy, "
    "Radiant, Magical, Throw, Magic, Explosion, Impact, Strike, Energy Ball, "
    "Enchantment, Sorcery, Strength, Wand, Mystic, Power Blast, Elemental, "
    "Focused Strike, Charm Impact, Arc of Light, Rumble, Sci-Fi, punch, hit"
)


def bloodlight_pierce_source(
    recid: int,
    source_number: str,
    filename: str,
    trim_start: float,
) -> DirectSource:
    return DirectSource(
        "KHRON",
        recid,
        KHRON_BLOODLIGHT_PIERCE_ROOT / filename,
        gain_db=-3.0,
        trim_start=trim_start,
        description=KHRON_BLOODLIGHT_PIERCE_DESCRIPTION,
        keywords=f"MAGSpel Bloodlight Pierce {source_number} KRST NONE",
        category="MAGIC",
        sub_category="SPELL",
        library="Spells Variations Vol 2",
    )


def little_arcane_blast_source(
    recid: int,
    source_number: str,
    filename: str,
    gain_db: float,
    delay_ms: float,
) -> DirectSource:
    return DirectSource(
        "KHRON",
        recid,
        KHRON_LITTLE_ARCANE_BLAST_ROOT / filename,
        gain_db=gain_db,
        delay_ms=delay_ms,
        description=KHRON_LITTLE_ARCANE_BLAST_DESCRIPTION,
        keywords=f"MAGSpel Little Arcane Blast {source_number} KRST",
        category="MAGIC",
        sub_category="SPELL",
        library="Spells Variations Vol 1",
    )


BURSTBUG_HIT_V2_PIERCE_BURST_SOURCES = (
    (
        1,
        bloodlight_pierce_source(
            2506,
            "16",
            "【MAGSpel_血光穿刺 16_KRST_无】MAGSpel_Bloodlight Pierce 16_KRST_NONE.wav",
            0.060,
        ),
        little_arcane_blast_source(
            2697,
            "06",
            "【MAGSpel_小型奥术冲击_06_KRST】MAGSpel_Little Arcane Blast 06_KRST.wav",
            0.0,
            8.0,
        ),
    ),
    (
        2,
        bloodlight_pierce_source(
            2494,
            "03",
            "【MAGSpel_血光穿刺 03_KRST_无】MAGSpel_Bloodlight Pierce 03_KRST_NONE.wav",
            0.105,
        ),
        little_arcane_blast_source(
            2693,
            "04",
            "【MAGSpel_小型奥术冲击 04_KRST】MAGSpel_Little Arcane Blast 04_KRST.wav",
            0.0,
            10.0,
        ),
    ),
    (
        3,
        bloodlight_pierce_source(
            2498,
            "08",
            "【MAGSpel_血光穿刺 08_KRST_无】MAGSpel_Bloodlight Pierce 08_KRST_NONE.wav",
            0.095,
        ),
        little_arcane_blast_source(
            2700,
            "09",
            "【MAGSpel_小型奥术冲击_09_KRST】MAGSpel_Little Arcane Blast 09_KRST.wav",
            0.0,
            8.0,
        ),
    ),
    (
        4,
        bloodlight_pierce_source(
            2493,
            "02",
            "【MAGSpel_血光穿刺 02_KRST_无】MAGSpel_Bloodlight Pierce 02_KRST_NONE.wav",
            0.190,
        ),
        little_arcane_blast_source(
            2694,
            "10",
            "【MAGSpel_小型奥术冲击 10_KRST】MAGSpel_Little Arcane Blast 10_KRST.wav",
            1.0,
            8.0,
        ),
    ),
    (
        5,
        bloodlight_pierce_source(
            2504,
            "14",
            "【MAGSpel_血光穿刺 14_KRST_无】MAGSpel_Bloodlight Pierce 14_KRST_NONE.wav",
            0.215,
        ),
        little_arcane_blast_source(
            2696,
            "03",
            "【MAGSpel_小型奥术冲击_03_KRST】MAGSpel_Little Arcane Blast 03_KRST.wav",
            0.0,
            10.0,
        ),
    ),
    (
        6,
        bloodlight_pierce_source(
            2492,
            "01",
            "【MAGSpel_血光穿刺 01_KRST_无】MAGSpel_Bloodlight Pierce 01_KRST_NONE.wav",
            0.245,
        ),
        little_arcane_blast_source(
            2692,
            "05",
            "【MAGSpel_小型奥术冲击05_KRST】MAGSpel_Little Arcane Blast 05_KRST.wav",
            0.0,
            8.0,
        ),
    ),
)


BURSTBUG_HIT_V2_PIERCE_BURST_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug.fast.hit",
        str(output_number),
        f"SFX_Burstbug_Fast_Hit_{output_number:02d}.wav",
        "Bloodlight Pierce needle transient followed by a compact Little Arcane Blast impact",
        (pierce_source, blast_source),
        "mono",
        duration=0.36,
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-5.5,
        transient_boost_db=1.5,
        transient_window_ms=45.0,
        saturation_drive=1.05,
    )
    for output_number, pierce_source, blast_source in BURSTBUG_HIT_V2_PIERCE_BURST_SOURCES
)


PLAYER_DAMAGED_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.combat.player.damaged",
        str(number),
        f"SFX_Player_Damaged_{number:02d}.wav",
        "Retro Game Gauntlet Punch sibling used as the global low-mid damage body",
        (
            DirectSource(
                "Epic_Stock_Media",
                5559 + number,
                EPIC_RETRO_GAME_IMPACTS_ROOT
                / f"【复古游戏武器拳击手套{number}】Retro_Game_Weapon-Gauntlet_Punch_{number}.wav",
            ),
        ),
        "mono",
        duration=0.28,
        fade_out_ms=10.0,
        peak_dbfs=-4.5,
    )
    for number in range(1, 4)
)


PLAYER_DAMAGED_VOICE_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.combat.player.damaged",
        str(number),
        f"VO_Fei_Damaged_{number:02d}.wav",
        "Fantasy Game 2 sibling: short human pain vocal, trimmed as a character hurt response",
        (IndexedSource("event.combat.player.damaged", number),),
        "mono",
        duration=None,
        fade_in_ms=2.0,
        fade_out_ms=18.0,
        peak_dbfs=-8.0,
    )
    for number in range(1, 5)
)


PLAYER_DAMAGED_FEMALE_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.combat.player.damaged",
        str(number),
        f"VO_Fei_Damaged_{number:02d}.wav",
        "KHRON Forged in Fury female pain sibling tightened into a short Fei damage reaction",
        (IndexedSource("event.combat.player.damaged", number),),
        "mono",
        duration=0.72,
        fade_in_ms=2.0,
        fade_out_ms=18.0,
        peak_dbfs=-8.0,
    )
    for number in range(1, 9)
)


PLAYER_DAMAGED_FEMALE_SHORT_V2_SOURCES = (
    (1, 4, 0.150, 0.440),
    (2, 4, 3.325, 0.270),
    (3, 5, 1.415, 0.350),
    (4, 5, 2.890, 0.375),
    (5, 6, 3.555, 0.480),
    (6, 6, 5.300, 0.345),
    (7, 7, 0.050, 0.395),
    (8, 7, 3.140, 0.365),
)


PLAYER_DAMAGED_FEMALE_SHORT_V2_AUDITIONS = tuple(
    AuditionOption(
        "event.combat.player.damaged",
        str(output_number),
        f"VO_Fei_Damaged_{output_number:02d}.wav",
        "BOOM Close Combat closed-mouth female soft-hit take cropped to one concise reaction",
        (
            IndexedSource(
                "event.combat.player.damaged",
                source_rank,
                trim_start=trim_start,
                repair_truncated_tail=True,
            ),
        ),
        "mono",
        duration=duration,
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-8.0,
    )
    for output_number, source_rank, trim_start, duration
    in PLAYER_DAMAGED_FEMALE_SHORT_V2_SOURCES
)


FEI_RELOAD_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.fei.reload.commit.0",
        str(number),
        f"SFX_Fei_Reload_{number:02d}.wav",
        "Fantasy Game 2 mechanical sibling tightened into a compact game reload commit; click/ratchet/snap without firearm identity",
        (IndexedSource("event.fei.reload.commit.0", number),),
        "mono",
        duration=0.32,
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-6.0,
    )
    for number in range(1, 6)
)


FEI_SECONDARY_IMMEDIATE_LAUNCH_V1_SOURCES = (
    (1, 1, 0.210),
    (2, 2, 0.200),
    (3, 3, 0.250),
    (4, 4, 0.220),
    (5, 5, 0.160),
)


FEI_SECONDARY_IMMEDIATE_LAUNCH_V1_AUDITIONS = tuple(
    AuditionOption(
        "presentation.fei.secondary.execute.audio.0",
        str(output_number),
        f"SFX_Fei_Secondary_Immediate_Launch_{output_number:02d}.wav",
        "KHRON Luminous Projectile sibling tightened into a compact arcane-energy launch with the main accent near 0.13 seconds",
        (
            IndexedSource(
                "presentation.fei.secondary.execute.audio.0",
                source_rank,
                trim_start=trim_start,
            ),
        ),
        "mono",
        duration=0.32,
        fade_in_ms=1.0,
        fade_out_ms=14.0,
        peak_dbfs=-5.0,
        transient_boost_db=2.5,
        transient_window_ms=70.0,
        saturation_drive=1.05,
    )
    for output_number, source_rank, trim_start
    in FEI_SECONDARY_IMMEDIATE_LAUNCH_V1_SOURCES
)


FEI_SECONDARY_IMMEDIATE_IMPACT_V1_SOURCES = (
    (1, 8),
    (2, 7),
    (3, 26),
    (4, 27),
    (5, 9),
    (6, 28),
    (7, 1),
)


FEI_SECONDARY_IMMEDIATE_IMPACT_V1_BASE_AUDITIONS = tuple(
    AuditionOption(
        "event.fei.secondary.execute.impact.base",
        str(output_number),
        f"SFX_Fei_Secondary_Immediate_Hit_{output_number:02d}.wav",
        "KHRON Electrified Impact sibling tightened into a compact magical projectile burst with a stronger body than Fei primary hit",
        (
            IndexedSource(
                "event.fei.secondary.execute.impact.base",
                source_rank,
            ),
        ),
        "mono",
        duration=0.34,
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-5.0,
        transient_boost_db=1.5,
        transient_window_ms=70.0,
        saturation_drive=1.05,
    )
    for output_number, source_rank
    in FEI_SECONDARY_IMMEDIATE_IMPACT_V1_SOURCES
)


FEI_SECONDARY_IMMEDIATE_IMPACT_V1_WEAKPOINT_AUDITIONS = tuple(
    AuditionOption(
        "event.fei.secondary.execute.impact.weakpoint",
        str(output_number),
        f"SFX_Fei_Secondary_Immediate_Weakpoint_{output_number:02d}.wav",
        "reinforced version of the paired KHRON Electrified Impact base candidate without introducing a different sound family",
        (
            IndexedSource(
                "event.fei.secondary.execute.impact.base",
                source_rank,
            ),
            IndexedSource(
                "event.fei.secondary.execute.impact.base",
                source_rank,
                gain_db=-9.0,
                pitch_semitones=-3.0,
                delay_ms=2.0,
            ),
        ),
        "mono",
        duration=0.34,
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-3.5,
        transient_boost_db=3.5,
        transient_window_ms=60.0,
        saturation_drive=1.25,
    )
    for output_number, source_rank
    in FEI_SECONDARY_IMMEDIATE_IMPACT_V1_SOURCES
)


FEI_SECONDARY_IMMEDIATE_IMPACT_V1_AUDITIONS = (
    FEI_SECONDARY_IMMEDIATE_IMPACT_V1_BASE_AUDITIONS
    + FEI_SECONDARY_IMMEDIATE_IMPACT_V1_WEAKPOINT_AUDITIONS
)


FEI_SECONDARY_CHARGE_START_HOLD_V1_SOURCES = (
    (1, 1, 0.48, 1.45),
    (2, 2, 0.62, 1.45),
    (3, 3, 0.42, 1.35),
    (4, 4, 0.58, 1.35),
    (5, 5, 0.20, 0.68),
)


FEI_SECONDARY_CHARGE_START_V1_AUDITIONS = tuple(
    AuditionOption(
        "presentation.fei.secondary.charge.audio.0",
        str(output_number),
        f"SFX_Fei_Secondary_Charge_Start_{output_number:02d}.wav",
        "Fantasy Game 2 Magic Ice Charging sibling tightened into a bright game-style charge ignition",
        (
            IndexedSource(
                "presentation.fei.secondary.charge.hold.0",
                source_rank,
            ),
        ),
        "mono",
        duration=0.48 if output_number == 5 else 0.65,
        fade_in_ms=2.0,
        fade_out_ms=24.0,
        peak_dbfs=-6.0,
        transient_boost_db=1.5,
        transient_window_ms=85.0,
        saturation_drive=1.03,
    )
    for output_number, source_rank, _, _
    in FEI_SECONDARY_CHARGE_START_HOLD_V1_SOURCES
)


FEI_SECONDARY_CHARGE_HOLD_V1_AUDITIONS = tuple(
    AuditionOption(
        "presentation.fei.secondary.charge.hold.0",
        str(output_number),
        f"SFX_Fei_Secondary_Charge_Hold_{output_number:02d}.wav",
        "restrained seamless loop derived from the paired Magic Ice Charging sibling for the held charge lifecycle",
        (
            IndexedSource(
                "presentation.fei.secondary.charge.hold.0",
                source_rank,
                trim_start=loop_trim_start,
            ),
        ),
        "mono",
        duration=loop_duration,
        fade_in_ms=0.0,
        fade_out_ms=0.0,
        loop_crossfade_ms=80.0,
        peak_dbfs=-12.0,
        saturation_drive=1.02,
    )
    for output_number, source_rank, loop_trim_start, loop_duration
    in FEI_SECONDARY_CHARGE_START_HOLD_V1_SOURCES
)


FEI_SECONDARY_CHARGE_START_HOLD_V1_AUDITIONS = (
    FEI_SECONDARY_CHARGE_START_V1_AUDITIONS
    + FEI_SECONDARY_CHARGE_HOLD_V1_AUDITIONS
)


FEI_SECONDARY_CHARGE_RELEASE_V1_SOURCES = (
    (1, 6, 0.52),
    (2, 7, 0.40),
    (3, 8, 0.55),
    (4, 9, 0.62),
    (5, 3, 0.62),
    (6, 4, 0.58),
    (7, 26, 0.68),
)


FEI_SECONDARY_CHARGE_RELEASE_V1_AUDITIONS = tuple(
    AuditionOption(
        "presentation.fei.secondary.release.audio.0",
        str(output_number),
        f"SFX_Fei_Secondary_Charge_Release_{output_number:02d}.wav",
        (
            "tight KHRON Arcane Snap sibling shaped as the charged muzzle release"
            if output_number <= 4
            else "Anime Game Electric Magic Attack sibling shaped as the charged muzzle release"
        ),
        (
            IndexedSource(
                "presentation.fei.secondary.release.audio.0",
                source_rank,
            ),
        ),
        "mono",
        duration=duration,
        fade_in_ms=1.0,
        fade_out_ms=14.0,
        peak_dbfs=-4.0,
        transient_boost_db=2.0,
        transient_window_ms=75.0,
        saturation_drive=1.06,
    )
    for output_number, source_rank, duration
    in FEI_SECONDARY_CHARGE_RELEASE_V1_SOURCES
)


FEI_SECONDARY_CHARGE_CANCEL_V1_SOURCES = (
    (1, 1, 0.00, 0.58, True),
    (2, 2, 0.32, 0.66, True),
    (3, 3, 0.00, 0.64, True),
    (4, 4, 0.00, 0.46, False),
    (5, 7, 0.00, 0.58, True),
)


FEI_SECONDARY_CHARGE_CANCEL_V1_AUDITIONS = tuple(
    AuditionOption(
        "presentation.fei.secondary.cancel.audio.0",
        str(output_number),
        f"SFX_Fei_Secondary_Charge_Cancel_{output_number:02d}.wav",
        (
            "Metroidvania deactivated synth-energy sibling tightened into a restrained charge cutoff"
            if output_number <= 3
            else "retro electronic drain/deactivate punctuation kept clearly weaker than release"
        ),
        (
            IndexedSource(
                "presentation.fei.secondary.cancel.audio.0",
                source_rank,
                trim_start=trim_start,
                repair_truncated_tail=repair_truncated_tail,
            ),
        ),
        "mono",
        duration=duration,
        fade_in_ms=2.0,
        fade_out_ms=18.0,
        peak_dbfs=-8.0,
        transient_boost_db=0.75,
        transient_window_ms=55.0,
        saturation_drive=1.02,
    )
    for output_number, source_rank, trim_start, duration, repair_truncated_tail
    in FEI_SECONDARY_CHARGE_CANCEL_V1_SOURCES
)


FEI_SECONDARY_CHARGE_RELEASE_CANCEL_V1_AUDITIONS = (
    FEI_SECONDARY_CHARGE_RELEASE_V1_AUDITIONS
    + FEI_SECONDARY_CHARGE_CANCEL_V1_AUDITIONS
)


FEI_SECONDARY_CHARGE_IMPACT_V1_SOURCES = (
    (1, 1, 1.05),
    (2, 2, 0.49),
    (3, 3, 0.49),
)


FEI_SECONDARY_CHARGE_IMPACT_V1_BASE_AUDITIONS = tuple(
    AuditionOption(
        "event.fei.secondary.release.impact.base",
        str(output_number),
        f"SFX_Fei_Secondary_Charge_Hit_{output_number:02d}.wav",
        "Fantasy Game 2 Magic Ice charged-impact sibling trimmed directly onto its main hit for a compact heavy magical burst",
        (
            IndexedSource(
                "event.fei.secondary.release.impact.base",
                source_rank,
                trim_start=trim_start,
            ),
        ),
        "mono",
        duration=0.68,
        fade_in_ms=1.0,
        fade_out_ms=18.0,
        peak_dbfs=-4.0,
        transient_boost_db=1.75,
        transient_window_ms=75.0,
        saturation_drive=1.08,
    )
    for output_number, source_rank, trim_start
    in FEI_SECONDARY_CHARGE_IMPACT_V1_SOURCES
)


FEI_SECONDARY_CHARGE_IMPACT_V1_WEAKPOINT_AUDITIONS = tuple(
    AuditionOption(
        "event.fei.secondary.release.impact.weakpoint",
        str(output_number),
        f"SFX_Fei_Secondary_Charge_Weakpoint_{output_number:02d}.wav",
        "reinforced version of the paired Magic Ice charged impact with a quiet same-source bright layer and stronger transient",
        (
            IndexedSource(
                "event.fei.secondary.release.impact.base",
                source_rank,
                trim_start=trim_start,
            ),
            IndexedSource(
                "event.fei.secondary.release.impact.base",
                source_rank,
                gain_db=-10.0,
                trim_start=trim_start,
                pitch_semitones=5.0,
                delay_ms=2.0,
            ),
        ),
        "mono",
        duration=0.68,
        fade_in_ms=1.0,
        fade_out_ms=18.0,
        peak_dbfs=-2.75,
        transient_boost_db=3.5,
        transient_window_ms=65.0,
        saturation_drive=1.18,
    )
    for output_number, source_rank, trim_start
    in FEI_SECONDARY_CHARGE_IMPACT_V1_SOURCES
)


FEI_SECONDARY_CHARGE_IMPACT_V1_AUDITIONS = (
    FEI_SECONDARY_CHARGE_IMPACT_V1_BASE_AUDITIONS
    + FEI_SECONDARY_CHARGE_IMPACT_V1_WEAKPOINT_AUDITIONS
)


ENEMY_BREAK_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.combat.enemy.break",
        str(number),
        f"SFX_Enemy_Break_{number:02d}.wav",
        "Retro Game Slinky Laser Burst sibling used as enemy-break punctuation",
        (
            DirectSource(
                "Epic_Stock_Media",
                5563 + number,
                EPIC_RETRO_GAME_IMPACTS_ROOT
                / f"【复古游戏武器滑溜激光爆发{number}】Retro_Game_Weapon-Slinky_Laser_Burst_{number}.wav",
            ),
        ),
        "mono",
        duration=0.45,
        fade_out_ms=10.0,
        peak_dbfs=-3.5,
        transient_boost_db=2.0,
        transient_window_ms=80.0,
        saturation_drive=1.1,
    )
    for number in range(1, 4)
)


RETICLE_LOCK_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.combat.reticle.lock",
        str(number),
        f"UI_Combat_TargetLock_{number:02d}.wav",
        "Fantasy Game UI Select Tiny Digital Plop sibling tightened into a target-lock tick",
        (
            DirectSource(
                "Epic_Stock_Media",
                9571 + number,
                EPIC_FANTASY_GAME_UI_ROOT
                / (
                    f"【点击 选择短促轻触微型按钮合成数字滴答声{number:02d} 2】"
                    f"UIClick_UI Select Short Tap Tiny Button Synth Digital Plop {number:02d}_ESM_FG2.wav"
                ),
            ),
        ),
        "mono",
        duration=0.095,
        fade_in_ms=1.0,
        fade_out_ms=5.0,
        peak_dbfs=-7.0,
    )
    for number in range(1, 7)
)


INTERACTION_FOCUS_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.interaction.focus",
        str(number),
        f"UI_Interaction_Focus_{number:02d}.wav",
        "Retro Game Menu Navigation sibling kept subtle for interaction focus",
        (
            DirectSource(
                "Epic_Stock_Media",
                5918 + number,
                EPIC_RETRO_GAME_UI_ROOT
                / f"【复古游戏菜单导航{number}】Retro_Game_Menu_Navigation_{number}.wav",
            ),
        ),
        "mono",
        duration=0.10,
        fade_in_ms=1.0,
        fade_out_ms=5.0,
        peak_dbfs=-10.5,
    )
    for number in range(1, 4)
)


INTERACTION_CONFIRM_V1_SOURCES = (
    (
        1,
        9603,
        "【界面点击 正面点击短中性通用按钮按压轻触01 2】"
        "UIClick_UI Positive Click Short Neutral Generic Button Press Tap 01_ESM_FG2.wav",
    ),
    (
        2,
        9535,
        "【点击 正面点击短中性和通用的按钮按压轻触02 2】"
        "UIClick_UI Positive Click Short Neutral Generic Button Press Tap 02_ESM_FG2.wav",
    ),
    (
        3,
        9536,
        "【点击 正面点击短中性和通用的按钮按压轻触03 2】"
        "UIClick_UI Positive Click Short Neutral Generic Button Press Tap 03_ESM_FG2.wav",
    ),
)


INTERACTION_CONFIRM_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.interaction.confirm",
        str(number),
        f"UI_Interaction_Confirm_{number:02d}.wav",
        "Fantasy Game UI Positive Click sibling used as a neutral interaction confirm",
        (
            DirectSource(
                "Epic_Stock_Media",
                recid,
                EPIC_FANTASY_GAME_UI_ROOT / filename,
            ),
        ),
        "mono",
        duration=0.13,
        fade_in_ms=1.0,
        fade_out_ms=8.0,
        peak_dbfs=-7.5,
    )
    for number, recid, filename in INTERACTION_CONFIRM_V1_SOURCES
)


INTERACTION_REJECT_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.interaction.reject",
        str(number),
        f"UI_Interaction_Reject_{number:02d}.wav",
        "Fantasy Game UI Negative Denied sibling used as a compact reject cue",
        (
            DirectSource(
                "Epic_Stock_Media",
                9545 + number,
                EPIC_FANTASY_GAME_UI_ROOT
                / (
                    f"【点击 负面拒绝短促简单敲击合成打击快速{number:02d}】"
                    f"UIClick_UI Negative Denied Short Simple Knocks Synth Hit Quick {number:02d}_ESM_FG2.wav"
                ),
            ),
        ),
        "mono",
        duration=0.19,
        fade_in_ms=1.0,
        fade_out_ms=10.0,
        peak_dbfs=-6.5,
    )
    for number in range(1, 4)
)


ROOM_ENTERED_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.room.entered",
        str(number),
        f"UI_Room_Entered_{number:02d}.wav",
        "Fantasy Game UI Short Synth Knock sibling kept soft for room entry",
        (
            DirectSource(
                "Epic_Stock_Media",
                9539 + number,
                EPIC_FANTASY_GAME_UI_ROOT
                / (
                    f"【点击 短合成敲击哔哔声 小挤压按钮{number:02d} 2】"
                    f"UIClick_UI Short Synth Knock Blips Small Squeezed Button {number:02d}_ESM_FG2.wav"
                ),
            ),
        ),
        "mono",
        duration=0.15,
        fade_in_ms=1.0,
        fade_out_ms=8.0,
        peak_dbfs=-11.0,
    )
    for number in range(1, 4)
)


ROOM_EXIT_UNLOCKED_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.room.exit.unlocked",
        str(number),
        f"UI_Exit_Unlocked_{number:02d}.wav",
        "Fantasy Game UI Positive Glassy Riser sibling used for exit unlock",
        (
            DirectSource(
                "Epic_Stock_Media",
                9536 + number,
                EPIC_FANTASY_GAME_UI_ROOT
                / (
                    f"【点击 正面确认简单音符上升玻璃质{number:02d} 2】"
                    f"UIClick_UI Positive Confirm Simple Note Sweep Riser Glassy {number:02d}_ESM_FG2.wav"
                ),
            ),
        ),
        "mono",
        duration=0.30,
        fade_in_ms=2.0,
        fade_out_ms=20.0,
        peak_dbfs=-5.5,
    )
    for number in range(1, 4)
)


ROOM_EXIT_CONFIRMED_V1_SOURCES = (
    (
        1,
        22067,
        "【用户界面-基础_确认_1_点击声_短促_数字】UI-basic_confirm_1_clicky_short_digital.wav",
    ),
    (
        2,
        22068,
        "【用户界面-基础_确认_2_点击声_短促_数字】UI-basic_confirm_2_clicky_short_digital.wav",
    ),
    (
        3,
        22069,
        "【用户界面-基础_确认_点击声_短促_数字】UI-basic_confirm_clicky_short_digital.wav",
    ),
)


ROOM_EXIT_CONFIRMED_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.room.exit.confirmed",
        str(number),
        f"UI_Exit_Confirmed_{number:02d}.wav",
        "Metroidvania UI Basic Confirm sibling tightened for exit confirmation",
        (
            DirectSource(
                "Epic_Stock_Media",
                recid,
                EPIC_METROIDVANIA_UI_ROOT / filename,
                repair_truncated_tail=True,
            ),
        ),
        "mono",
        duration=0.22,
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-6.5,
    )
    for number, recid, filename in ROOM_EXIT_CONFIRMED_V1_SOURCES
)


FOREST_AMBIENCE_SPARSE_V1_AUDITIONS = (
    AuditionOption(
        "event.forest.ambience.loop",
        "1",
        "AMB_Forest_Bed_01.wav",
        "Quest Game steady forest wind with gusts and sparse insects",
        (IndexedSource("event.forest.ambience.loop", 1),),
        "stereo",
        trim_start=10.0,
        duration=20.0,
        fade_in_ms=0.0,
        fade_out_ms=0.0,
        loop_crossfade_ms=1000.0,
        peak_dbfs=-16.0,
    ),
    AuditionOption(
        "event.forest.ambience.loop",
        "2",
        "AMB_Forest_Bed_02.wav",
        "Farm Game soft breeze through leaves with no authored bird call",
        (IndexedSource("event.forest.ambience.loop", 4),),
        "stereo",
        trim_start=10.0,
        duration=20.0,
        fade_in_ms=0.0,
        fade_out_ms=0.0,
        loop_crossfade_ms=1000.0,
        peak_dbfs=-16.0,
    ),
    AuditionOption(
        "event.forest.ambience.loop",
        "3",
        "AMB_Forest_Bed_03.wav",
        "Fantasy Game dark magical forest with calm insect texture",
        (IndexedSource("event.forest.ambience.loop", 12),),
        "stereo",
        trim_start=10.0,
        duration=20.0,
        fade_in_ms=0.0,
        fade_out_ms=0.0,
        loop_crossfade_ms=1000.0,
        peak_dbfs=-16.0,
    ),
)


FOREST_MUSIC_EXPLORATION_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.forest.music.exploration",
        str(number),
        f"MUS_Forest_Exploration_{number:02d}.wav",
        f"Fantasy Game 2 Light Enchanted Forest sibling: {variant}",
        (IndexedSource("event.forest.music.exploration", rank),),
        "stereo",
        fade_in_ms=0.0,
        fade_out_ms=0.0,
        loop_crossfade_ms=100.0,
        peak_dbfs=-12.0,
    )
    for number, rank, variant in (
        (1, 3, "Full Instrumental"),
        (2, 4, "Full Percussion"),
        (3, 5, "Base"),
        (4, 6, "Solo Harp"),
    )
)


FOREST_AMBIENCE_POINTS_V3_AUDITIONS = (
    AuditionOption(
        "event.forest.ambience.point.0",
        "1",
        "AMB_Forest_Point_01.wav",
        "dry solo cricket chirp for a sparse natural point",
        (IndexedSource("event.forest.ambience.point.0", 1),),
        "mono",
        fade_out_ms=20.0,
        peak_dbfs=-16.0,
    ),
    AuditionOption(
        "event.forest.ambience.point.0",
        "2",
        "AMB_Forest_Point_02.wav",
        "short cartoon bat chirp variation 1",
        (IndexedSource("event.forest.ambience.point.0", 2),),
        "mono",
        fade_in_ms=1.0,
        fade_out_ms=8.0,
        peak_dbfs=-16.0,
    ),
    AuditionOption(
        "event.forest.ambience.point.0",
        "3",
        "AMB_Forest_Point_03.wav",
        "short cartoon bat chirp variation 2",
        (IndexedSource("event.forest.ambience.point.0", 3),),
        "mono",
        fade_in_ms=1.0,
        fade_out_ms=8.0,
        peak_dbfs=-16.0,
    ),
    AuditionOption(
        "event.forest.ambience.point.0",
        "4",
        "AMB_Forest_Point_04.wav",
        "small dry wooden-rune rustle variation 1",
        (IndexedSource("event.forest.ambience.point.0", 4),),
        "mono",
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-16.0,
    ),
    AuditionOption(
        "event.forest.ambience.point.0",
        "5",
        "AMB_Forest_Point_05.wav",
        "small dry wooden-rune rustle variation 2",
        (IndexedSource("event.forest.ambience.point.0", 5),),
        "mono",
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-16.0,
    ),
    AuditionOption(
        "event.forest.ambience.point.0",
        "6",
        "AMB_Forest_Point_06.wav",
        "restrained Magic Wisp twinkle-dust fragment",
        (
            DirectSource(
                "BOOM_LIBRARY",
                5547,
                BOOM_MAGIC_WISP_CK_ROOT
                / "【恒定星光尘】MAGShim_METAL PROCESSED-Sparkle Constant Twinkle Dust.wav",
                trim_start=2.40,
                description="constant sparkle twinkle dust",
                keywords="magic shimmer sparkle twinkle dust",
                category="MAGIC",
                sub_category="SHIMMER",
                library="Magic Wisp",
            ),
        ),
        "mono",
        duration=0.60,
        fade_in_ms=5.0,
        fade_out_ms=25.0,
        peak_dbfs=-16.0,
    ),
    AuditionOption(
        "event.forest.ambience.point.0",
        "7",
        "AMB_Forest_Point_07.wav",
        "restrained Magic Alchemy dust-sweetener fragment",
        (
            DirectSource(
                "BOOM_LIBRARY",
                9544,
                BOOM_MAGIC_ALCHEMY_CK_ROOT
                / "【磁力垫片_微光-粉尘甜味剂】MAGShim_SHIMMER-Dust Sweetener.wav",
                trim_start=0.95,
                description="dust sweetener shimmer",
                keywords="magic shimmer dust subtle",
                category="MAGIC",
                sub_category="SHIMMER",
                library="Magic Alchemy Construction Kit",
            ),
        ),
        "mono",
        duration=0.75,
        fade_in_ms=5.0,
        fade_out_ms=30.0,
        peak_dbfs=-16.0,
    ),
    AuditionOption(
        "event.forest.ambience.point.0",
        "8",
        "AMB_Forest_Point_08.wav",
        "thin high sparkle-dust fragment for a distant magical point",
        (
            DirectSource(
                "BOOM_LIBRARY",
                9545,
                BOOM_MAGIC_ALCHEMY_CK_ROOT
                / "【磁力垫片_微光-薄型高闪粉】MAGShim_SHIMMER-Thin High Sparkle Dust.wav",
                trim_start=0.80,
                description="thin high sparkle dust",
                keywords="magic shimmer sparkle dust high",
                category="MAGIC",
                sub_category="SHIMMER",
                library="Magic Alchemy Construction Kit",
            ),
        ),
        "mono",
        duration=0.60,
        fade_in_ms=5.0,
        fade_out_ms=25.0,
        peak_dbfs=-16.0,
    ),
)


FOREST_MUSIC_COMBAT_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.forest.music.combat",
        str(number),
        f"MUS_Forest_Combat_{number:02d}.wav",
        f"Fantasy Game 2 Dark Combat sibling: {variant}",
        (IndexedSource("event.forest.music.combat", rank),),
        "stereo",
        fade_in_ms=0.0,
        fade_out_ms=0.0,
        loop_crossfade_ms=100.0,
        peak_dbfs=-10.0,
    )
    for number, rank, variant in (
        (1, 2, "Neutral"),
        (2, 3, "Slowed"),
        (3, 8, "Powerful"),
        (4, 9, "Hopeful"),
    )
) + (
    AuditionOption(
        "event.forest.music.combat",
        "5",
        "MUS_Forest_Combat_05.wav",
        "Battle Royale retro sci-fi pulsing loop alternative",
        (
            DirectSource(
                "Epic_Stock_Media",
                19183,
                EPIC_BATTLE_ROYALE_AMBIENCE_ROOT
                / (
                    "【氛围循环_科幻_奇幻_脉动节奏_菜单音乐_大厅_神秘_谜题_复古_太空_1】"
                    "Ambience_Loop_Sci_fi_Fantasy_Pulsing_Rhythmn_Menu_Music_Lobby_"
                    "Mysterical_Mystery_Retro_Space_1.wav"
                ),
            ),
        ),
        "stereo",
        fade_in_ms=0.0,
        fade_out_ms=0.0,
        loop_crossfade_ms=100.0,
        peak_dbfs=-10.0,
    ),
)


FOREST_MUSIC_VICTORY_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.forest.music.victory",
        str(number),
        f"MUS_Forest_Victory_{number:02d}.wav",
        f"Farm Game task-complete musical sibling {number}",
        (IndexedSource("event.forest.music.victory", rank),),
        "stereo",
        fade_in_ms=2.0,
        fade_out_ms=30.0,
        peak_dbfs=-5.0,
    )
    for number, rank in enumerate((1, 2, 3, 4, 5), start=1)
)


FOREST_MUSIC_DEFEAT_V1_AUDITIONS = (
    AuditionOption(
        "event.forest.music.defeat",
        "1",
        "MUS_Forest_Defeat_01.wav",
        "Anime Essentials bright magical downer",
        (
            DirectSource(
                "BOOM_LIBRARY",
                2626,
                BOOM_ANIME_ESSENTIALS_ROOT
                / "05-尖锐-STINGER"
                / "【声音设计-电子合成 尖锐 亮 魔法 下降】DSGNMisc_STINGER-Light Magic Downer.wav",
                trim_start=0.55,
            ),
        ),
        "stereo",
        duration=2.75,
        fade_out_ms=40.0,
        peak_dbfs=-5.0,
    ),
    AuditionOption(
        "event.forest.music.defeat",
        "2",
        "MUS_Forest_Defeat_02.wav",
        "Anime Essentials short descending synth wobble",
        (
            DirectSource(
                "BOOM_LIBRARY",
                2723,
                BOOM_ANIME_ESSENTIALS_ROOT
                / "06-摇摆-WOBBLE"
                / "【声音设计-电子合成 摇摆 降序 短】DSGNSynth_WOBBLE-Descending Short.wav",
                trim_start=0.10,
            ),
        ),
        "stereo",
        duration=1.35,
        fade_out_ms=30.0,
        peak_dbfs=-5.0,
    ),
    AuditionOption(
        "event.forest.music.defeat",
        "3",
        "MUS_Forest_Defeat_03.wav",
        "Anime Essentials concise tonal negative",
        (
            DirectSource(
                "BOOM_LIBRARY",
                2727,
                BOOM_ANIME_ESSENTIALS_ROOT
                / "07-音乐性-TONAL"
                / "【声音设计 音乐性 否定】DSGNMisc_TONAL-Negative.wav",
                trim_start=0.04,
            ),
        ),
        "stereo",
        duration=0.85,
        fade_out_ms=25.0,
        peak_dbfs=-5.0,
    ),
    AuditionOption(
        "event.forest.music.defeat",
        "4",
        "MUS_Forest_Defeat_04.wav",
        "Anime Essentials game-like tonal chirp fall",
        (
            DirectSource(
                "BOOM_LIBRARY",
                2729,
                BOOM_ANIME_ESSENTIALS_ROOT
                / "07-音乐性-TONAL"
                / "【声音设计 音乐性 调频 下降】DSGNMisc_TONAL-Chirp Fall.wav",
                trim_start=0.06,
            ),
        ),
        "stereo",
        duration=2.20,
        fade_out_ms=40.0,
        peak_dbfs=-5.0,
    ),
)


BARRIER_BROKEN_V1_SOURCES = (
    (1, 22019, "1", 0.170),
    (2, 22020, "2", 0.230),
    (3, 22018, "", 0.280),
)


def barrier_break_source_path(source_number: str) -> Path:
    if source_number:
        return EPIC_METROIDVANIA_EFFECT_ROOT / (
            f"【效果-能量护盾破碎_{source_number}_掉落_呼啸_扫掠_缓慢】"
            f"Effect-energy_shield_break_{source_number}_drop_whoosh_sweep_slow.wav"
        )
    return EPIC_METROIDVANIA_EFFECT_ROOT / (
        "【效果-能量护盾破碎掉落呼啸扫掠缓慢】"
        "Effect-energy_shield_break_drop_whoosh_sweep_slow.wav"
    )


BARRIER_BROKEN_V1_AUDITIONS = tuple(
    AuditionOption(
        "event.combat.player.barrier_broken",
        str(output_number),
        f"SFX_Player_BarrierBreak_{output_number:02d}.wav",
        "Metroidvania Energy Shield Break sibling tightened around its break peak",
        (
            DirectSource(
                "Epic_Stock_Media",
                recid,
                barrier_break_source_path(source_number),
                trim_start=trim_start,
                repair_truncated_tail=True,
            ),
        ),
        "mono",
        duration=0.65,
        fade_out_ms=20.0,
        peak_dbfs=-3.0,
    )
    for output_number, recid, source_number, trim_start in BARRIER_BROKEN_V1_SOURCES
)


BURSTBUG_HEAVY_C03_SOURCE_WINDOWS = (
    # output, shortlist rank, telegraph trim/duration, release trim/duration
    (1, 4, 0.26, 0.40, 0.70, 0.46),
    (2, 5, 0.30, 0.52, 0.88, 0.46),
    (3, 7, 0.22, 0.42, 0.69, 0.46),
    (4, 8, 0.06, 0.65, 0.76, 0.46),
)


BURSTBUG_HEAVY_C03_TELEGRAPH_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug-heavy-weakpoint-break.telegraph",
        str(output_number),
        f"SFX_Burstbug_Heavy_Telegraph_{output_number:02d}.wav",
        "Anime Game Water Heavy Charge sibling cropped before its main burst as a compact magical heavy-warning ignition",
        (
            IndexedSource(
                "event.burstbug-heavy-weakpoint-break.telegraph",
                shortlist_rank,
                trim_start=telegraph_trim,
            ),
        ),
        "mono",
        duration=telegraph_duration,
        fade_in_ms=2.0,
        fade_out_ms=24.0,
        peak_dbfs=-5.0,
        transient_boost_db=0.75,
        transient_window_ms=55.0,
        saturation_drive=1.03,
    )
    for (
        output_number,
        shortlist_rank,
        telegraph_trim,
        telegraph_duration,
        _,
        _,
    ) in BURSTBUG_HEAVY_C03_SOURCE_WINDOWS
)


BURSTBUG_HEAVY_C03_DANGER_TICK_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug-heavy-weakpoint-break.danger-tick",
        str(output_number),
        f"SFX_Burstbug_Heavy_DangerTick_{output_number:02d}.wav",
        "BOOM Future Technology fast-warning sibling kept dry and concise for the committed Heavy 3/2/1 countdown",
        (
            IndexedSource(
                "event.burstbug-heavy-weakpoint-break.danger-tick",
                shortlist_rank,
            ),
        ),
        "mono",
        duration=0.31,
        fade_in_ms=1.0,
        fade_out_ms=10.0,
        peak_dbfs=-6.0,
        transient_boost_db=0.75,
        transient_window_ms=35.0,
        saturation_drive=1.02,
    )
    for output_number, shortlist_rank in enumerate(range(1, 4), start=1)
)


BURSTBUG_HEAVY_C03_RELEASE_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug-heavy-weakpoint-break.release",
        str(output_number),
        f"SFX_Burstbug_Heavy_Release_{output_number:02d}.wav",
        "main burst from the paired Anime Game Water Heavy Charge sibling, separated from the later C04 impact identity",
        (
            IndexedSource(
                "event.burstbug-heavy-weakpoint-break.telegraph",
                shortlist_rank,
                trim_start=release_trim,
            ),
        ),
        "mono",
        duration=release_duration,
        fade_in_ms=1.0,
        fade_out_ms=14.0,
        peak_dbfs=-4.0,
        transient_boost_db=1.75,
        transient_window_ms=60.0,
        saturation_drive=1.07,
    )
    for (
        output_number,
        shortlist_rank,
        _,
        _,
        release_trim,
        release_duration,
    ) in BURSTBUG_HEAVY_C03_SOURCE_WINDOWS
)


BURSTBUG_HEAVY_C03_AUDITIONS = (
    BURSTBUG_HEAVY_C03_TELEGRAPH_AUDITIONS
    + BURSTBUG_HEAVY_C03_DANGER_TICK_AUDITIONS
    + BURSTBUG_HEAVY_C03_RELEASE_AUDITIONS
)


BURSTBUG_HEAVY_C04_SOURCE_RANKS = (1, 2, 3, 4)


BURSTBUG_HEAVY_C04_BASE_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug-heavy-weakpoint-break.impact.base",
        str(output_number),
        f"SFX_Burstbug_Heavy_Impact_{output_number:02d}.wav",
        "Fantasy Game 2 Magic Electric Punch sibling tightened into an explosion-forward arcade heavy impact",
        (
            IndexedSource(
                "event.burstbug-heavy-weakpoint-break.impact.base",
                shortlist_rank,
            ),
        ),
        "mono",
        duration=0.52,
        fade_in_ms=1.0,
        fade_out_ms=14.0,
        peak_dbfs=-4.0,
        transient_boost_db=2.0,
        transient_window_ms=70.0,
        saturation_drive=1.08,
    )
    for output_number, shortlist_rank in enumerate(
        BURSTBUG_HEAVY_C04_SOURCE_RANKS,
        start=1,
    )
)


BURSTBUG_HEAVY_C04_WEAKPOINT_AUDITIONS = tuple(
    AuditionOption(
        "event.burstbug-heavy-weakpoint-break.impact.weakpoint",
        str(output_number),
        f"SFX_Burstbug_Heavy_Weakpoint_{output_number:02d}.wav",
        "reinforced version of the paired Magic Electric Punch impact with a brighter same-source energy layer",
        (
            IndexedSource(
                "event.burstbug-heavy-weakpoint-break.impact.base",
                shortlist_rank,
            ),
            IndexedSource(
                "event.burstbug-heavy-weakpoint-break.impact.base",
                shortlist_rank,
                gain_db=-9.0,
                pitch_semitones=5.0,
                delay_ms=2.0,
            ),
        ),
        "mono",
        duration=0.52,
        fade_in_ms=1.0,
        fade_out_ms=14.0,
        peak_dbfs=-2.75,
        transient_boost_db=3.5,
        transient_window_ms=65.0,
        saturation_drive=1.18,
    )
    for output_number, shortlist_rank in enumerate(
        BURSTBUG_HEAVY_C04_SOURCE_RANKS,
        start=1,
    )
)


BURSTBUG_HEAVY_C04_AUDITIONS = (
    BURSTBUG_HEAVY_C04_BASE_AUDITIONS
    + BURSTBUG_HEAVY_C04_WEAKPOINT_AUDITIONS
)


HUDIE_C05_LAUNCH_SOURCE_RANKS = (
    (1, 12),
    (2, 13),
    (3, 14),
    (4, 15),
)


HUDIE_C05_LAUNCH_AUDITIONS = tuple(
    AuditionOption(
        "event.hudie-projectile.launch",
        str(output_number),
        f"SFX_Hudie_Projectile_Launch_{output_number:02d}.wav",
        "Phantom Magic cast-whoosh with a restrained paired electric flutter layer for Hudie's magical butterfly projectile",
        (
            IndexedSource(
                "event.hudie-projectile.launch",
                launch_rank,
            ),
            IndexedSource(
                "event.hudie-projectile.flight",
                flutter_rank,
                gain_db=-13.0,
                pitch_semitones=3.0,
                delay_ms=6.0,
            ),
        ),
        "mono",
        duration=0.42,
        fade_in_ms=1.0,
        fade_out_ms=14.0,
        peak_dbfs=-6.0,
        transient_boost_db=1.5,
        transient_window_ms=50.0,
        saturation_drive=1.04,
    )
    for output_number, (launch_rank, flutter_rank) in enumerate(
        HUDIE_C05_LAUNCH_SOURCE_RANKS,
        start=1,
    )
)


HUDIE_C05_FLIGHT_AUDITIONS = tuple(
    AuditionOption(
        "event.hudie-projectile.flight",
        str(output_number),
        f"SFX_Hudie_Projectile_Flight_{output_number:02d}.wav",
        "Anime Game electric flutter sibling shaped into a light magical insect projectile flight trace",
        (
            IndexedSource(
                "event.hudie-projectile.flight",
                shortlist_rank,
            ),
        ),
        "mono",
        duration=0.58,
        fade_in_ms=3.0,
        fade_out_ms=24.0,
        peak_dbfs=-9.0,
        transient_boost_db=0.5,
        transient_window_ms=45.0,
        saturation_drive=1.02,
    )
    for output_number, shortlist_rank in enumerate(range(12, 17), start=1)
)


HUDIE_C05_AUDITIONS = (
    HUDIE_C05_LAUNCH_AUDITIONS
    + HUDIE_C05_FLIGHT_AUDITIONS
)


HUDIE_C06_SOURCE_TRIMS = (
    (1, 0.25),
    (2, 0.22),
    (3, 0.20),
    (4, 0.17),
)


HUDIE_C06_BASE_AUDITIONS = tuple(
    AuditionOption(
        "event.hudie-projectile.impact.base",
        str(output_number),
        f"SFX_Hudie_Projectile_Impact_{output_number:02d}.wav",
        "Anime Game Bubbly Deep Tonal Debuff Impact sibling tightened into a compact piercing magical projectile hit",
        (
            IndexedSource(
                "event.hudie-projectile.impact.base",
                shortlist_rank,
                trim_start=trim_start,
            ),
        ),
        "mono",
        duration=0.34,
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-6.0,
        transient_boost_db=1.5,
        transient_window_ms=45.0,
        saturation_drive=1.04,
    )
    for output_number, (shortlist_rank, trim_start) in enumerate(
        HUDIE_C06_SOURCE_TRIMS,
        start=1,
    )
)


HUDIE_C06_WEAKPOINT_AUDITIONS = tuple(
    AuditionOption(
        "event.hudie-projectile.impact.weakpoint",
        str(output_number),
        f"SFX_Hudie_Projectile_Weakpoint_{output_number:02d}.wav",
        "reinforced version of the paired Hudie impact with a quiet same-source bright layer and stronger transient",
        (
            IndexedSource(
                "event.hudie-projectile.impact.base",
                shortlist_rank,
                trim_start=trim_start,
            ),
            IndexedSource(
                "event.hudie-projectile.impact.base",
                shortlist_rank,
                gain_db=-10.0,
                trim_start=trim_start,
                pitch_semitones=5.0,
                delay_ms=2.0,
            ),
        ),
        "mono",
        duration=0.34,
        fade_in_ms=1.0,
        fade_out_ms=12.0,
        peak_dbfs=-5.0,
        transient_boost_db=2.0,
        transient_window_ms=45.0,
        saturation_drive=1.06,
    )
    for output_number, (shortlist_rank, trim_start) in enumerate(
        HUDIE_C06_SOURCE_TRIMS,
        start=1,
    )
)


HUDIE_C06_AUDITIONS = (
    HUDIE_C06_BASE_AUDITIONS
    + HUDIE_C06_WEAKPOINT_AUDITIONS
)


LUAN_C07_SOURCE_WINDOWS = (
    # output, shortlist rank, telegraph trim/duration, commit trim/duration
    (1, 24, 0.34, 0.72, 1.04, 0.68),
    (2, 27, 0.12, 0.72, 0.82, 0.68),
    (3, 28, 0.09, 0.72, 0.80, 0.68),
    (4, 29, 0.00, 0.70, 0.67, 0.68),
    (5, 38, 0.35, 0.72, 1.06, 0.68),
    (6, 39, 0.00, 0.54, 0.55, 0.68),
    (7, 40, 0.18, 0.72, 0.89, 0.68),
)


LUAN_C07_TELEGRAPH_AUDITIONS = tuple(
    AuditionOption(
        "event.luan-summon.telegraph",
        str(output_number),
        f"SFX_Luan_Summon_Telegraph_{output_number:02d}.wav",
        "Anime Game Summon Shine sibling split before its main burst into a compact magical summon warning",
        (
            IndexedSource(
                "event.luan-summon.commit",
                shortlist_rank,
                trim_start=telegraph_trim,
            ),
        ),
        "mono",
        duration=telegraph_duration,
        fade_in_ms=2.0,
        fade_out_ms=14.0,
        peak_dbfs=-6.0,
        transient_boost_db=0.5,
        transient_window_ms=45.0,
        saturation_drive=1.02,
    )
    for (
        output_number,
        shortlist_rank,
        telegraph_trim,
        telegraph_duration,
        _,
        _,
    ) in LUAN_C07_SOURCE_WINDOWS
)


LUAN_C07_COMMIT_AUDITIONS = tuple(
    AuditionOption(
        "event.luan-summon.commit",
        str(output_number),
        f"SFX_Luan_Summon_Commit_{output_number:02d}.wav",
        "main appearance burst from the paired Anime Game Summon Shine sibling",
        (
            IndexedSource(
                "event.luan-summon.commit",
                shortlist_rank,
                trim_start=commit_trim,
            ),
        ),
        "mono",
        duration=commit_duration,
        fade_in_ms=1.0,
        fade_out_ms=18.0,
        peak_dbfs=-5.0,
        transient_boost_db=1.5,
        transient_window_ms=55.0,
        saturation_drive=1.04,
    )
    for (
        output_number,
        shortlist_rank,
        _,
        _,
        commit_trim,
        commit_duration,
    ) in LUAN_C07_SOURCE_WINDOWS
)


LUAN_C07_AUDITIONS = (
    LUAN_C07_TELEGRAPH_AUDITIONS
    + LUAN_C07_COMMIT_AUDITIONS
)


LUAN_C08_SELF_DESTRUCT_AUDITIONS = tuple(
    AuditionOption(
        "event.luan-summon.self-destruct.1",
        str(output_number),
        f"SFX_Luan_SelfDestruct_{output_number:02d}.wav",
        "short Anime Game magical fire-burst punctuation for Luan's owner self-destruction",
        (
            IndexedSource(
                "event.luan-summon.self-destruct.1",
                shortlist_rank,
            ),
        ),
        "mono",
        duration=0.62,
        fade_in_ms=1.0,
        fade_out_ms=20.0,
        peak_dbfs=-5.0,
        transient_boost_db=1.0,
        transient_window_ms=55.0,
        saturation_drive=1.04,
    )
    for output_number, shortlist_rank in enumerate(
        (11, 18, 19, 22),
        start=1,
    )
)


ENEMY_LIFECYCLE_D01_SPAWN_SOURCES = (
    # output, semantic shortlist rank, compact render duration
    (1, 1, 0.36),
    (2, 2, 0.44),
    (3, 5, 0.43),
    (4, 6, 0.43),
)


ENEMY_LIFECYCLE_D01_SPAWN_AUDITIONS = tuple(
    AuditionOption(
        "event.enemy.spawn",
        str(output_number),
        f"SFX_Enemy_Spawn_{output_number:02d}.wav",
        "compact Retro Game enemy appearance sibling with the long padded tail removed",
        (IndexedSource("event.enemy.spawn", shortlist_rank),),
        "mono",
        duration=duration,
        fade_in_ms=1.0,
        fade_out_ms=14.0,
        peak_dbfs=-6.0,
        transient_boost_db=0.75,
        transient_window_ms=45.0,
        saturation_drive=1.02,
    )
    for output_number, shortlist_rank, duration in ENEMY_LIFECYCLE_D01_SPAWN_SOURCES
)


ENEMY_LIFECYCLE_D01_DEATH_SOURCES = (
    # output, semantic shortlist rank, compact render duration
    (1, 12, 0.56),
    (2, 13, 0.56),
    (3, 14, 0.58),
    (4, 15, 0.54),
)


ENEMY_LIFECYCLE_D01_DEATH_AUDITIONS = tuple(
    AuditionOption(
        "event.enemy.death",
        str(output_number),
        f"SFX_Enemy_Death_{output_number:02d}.wav",
        "compact Retro Game enemy defeat sibling with a clear arcade punctuation",
        (IndexedSource("event.enemy.death", shortlist_rank),),
        "mono",
        duration=duration,
        fade_in_ms=1.0,
        fade_out_ms=16.0,
        peak_dbfs=-5.0,
        transient_boost_db=1.25,
        transient_window_ms=50.0,
        saturation_drive=1.04,
    )
    for output_number, shortlist_rank, duration in ENEMY_LIFECYCLE_D01_DEATH_SOURCES
)


ENEMY_LIFECYCLE_D01_AUDITIONS = (
    ENEMY_LIFECYCLE_D01_SPAWN_AUDITIONS
    + ENEMY_LIFECYCLE_D01_DEATH_AUDITIONS
)


ENEMY_DEATH_CREATURE_V2_SOURCES = (
    # output, semantic shortlist rank, onset trim, compact render duration
    (1, 36, 0.04, 1.14),
    (2, 15, 0.005, 0.94),
    (3, 37, 0.015, 1.15),
    (4, 1, 0.04, 0.76),
    (5, 2, 0.005, 0.63),
    (6, 3, 0.005, 0.58),
)


ENEMY_DEATH_CREATURE_V2_AUDITIONS = tuple(
    AuditionOption(
        "event.enemy.death",
        str(output_number),
        f"SFX_Enemy_Death_Creature_{output_number:02d}.wav",
        "short Roc creature death vocal from one sibling family; no synthetic defeat cue or added explosion",
        (
            IndexedSource(
                "event.enemy.death",
                shortlist_rank,
                trim_start=trim_start,
            ),
        ),
        "mono",
        duration=duration,
        fade_in_ms=1.0,
        fade_out_ms=20.0,
        peak_dbfs=-6.0,
        transient_boost_db=0.75,
        transient_window_ms=55.0,
        saturation_drive=1.02,
    )
    for output_number, shortlist_rank, trim_start, duration in ENEMY_DEATH_CREATURE_V2_SOURCES
)


PRESETS = {
    "realistic_v1": REALISTIC_AUDITIONS,
    "stylized_v2": STYLIZED_AUDITIONS,
    "hit_v3": HIT_V3_AUDITIONS,
    "semantic_hit_v5": SEMANTIC_HIT_V5_AUDITIONS,
    "semantic_hit_v6": SEMANTIC_HIT_V6_AUDITIONS,
    "hit_variations_v1": HIT_VARIATIONS_V1_AUDITIONS,
    "weakpoint_v1": WEAKPOINT_V1_AUDITIONS,
    "weakpoint_v2_body": WEAKPOINT_V2_BODY_AUDITIONS,
    "weakpoint_v3_reinforced": WEAKPOINT_V3_REINFORCED_AUDITIONS,
    "attack_variations_v1": ATTACK_VARIATIONS_V1_AUDITIONS,
    "environment_hit_v1": ENVIRONMENT_HIT_V1_AUDITIONS,
    "burstbug_telegraph_v1": BURSTBUG_TELEGRAPH_V1_AUDITIONS,
    "burstbug_release_v1": BURSTBUG_RELEASE_V1_AUDITIONS,
    "burstbug_volley_warning_release_v1":
        BURSTBUG_VOLLEY_TELEGRAPH_V1_AUDITIONS
        + BURSTBUG_VOLLEY_RELEASE_V1_AUDITIONS,
    "burstbug_volley_c02_v1": BURSTBUG_VOLLEY_C02_AUDITIONS,
    "burstbug_heavy_c03_v1": BURSTBUG_HEAVY_C03_AUDITIONS,
    "burstbug_heavy_c04_v1": BURSTBUG_HEAVY_C04_AUDITIONS,
    "hudie_projectile_c05_v1": HUDIE_C05_AUDITIONS,
    "hudie_impact_c06_v1": HUDIE_C06_AUDITIONS,
    "luan_summon_c07_v1": LUAN_C07_AUDITIONS,
    "luan_self_destruct_c08_v1": LUAN_C08_SELF_DESTRUCT_AUDITIONS,
    "enemy_lifecycle_d01_v1": ENEMY_LIFECYCLE_D01_AUDITIONS,
    "enemy_death_creature_v2": ENEMY_DEATH_CREATURE_V2_AUDITIONS,
    "burstbug_projectile_v1": BURSTBUG_PROJECTILE_V1_AUDITIONS,
    "burstbug_hit_v1": BURSTBUG_HIT_V1_AUDITIONS,
    "burstbug_hit_v2_pierce_burst": BURSTBUG_HIT_V2_PIERCE_BURST_AUDITIONS,
    "player_damaged_v1": PLAYER_DAMAGED_V1_AUDITIONS,
    "player_damaged_voice_v1": PLAYER_DAMAGED_VOICE_V1_AUDITIONS,
    "player_damaged_female_v1": PLAYER_DAMAGED_FEMALE_V1_AUDITIONS,
    "player_damaged_female_short_v2": PLAYER_DAMAGED_FEMALE_SHORT_V2_AUDITIONS,
    "fei_reload_v1": FEI_RELOAD_V1_AUDITIONS,
    "fei_secondary_immediate_launch_v1": FEI_SECONDARY_IMMEDIATE_LAUNCH_V1_AUDITIONS,
    "fei_secondary_immediate_impact_v1": FEI_SECONDARY_IMMEDIATE_IMPACT_V1_AUDITIONS,
    "fei_secondary_charge_start_hold_v1": FEI_SECONDARY_CHARGE_START_HOLD_V1_AUDITIONS,
    "fei_secondary_charge_release_cancel_v1": FEI_SECONDARY_CHARGE_RELEASE_CANCEL_V1_AUDITIONS,
    "fei_secondary_charge_impact_v1": FEI_SECONDARY_CHARGE_IMPACT_V1_AUDITIONS,
    "enemy_break_v1": ENEMY_BREAK_V1_AUDITIONS,
    "reticle_lock_v1": RETICLE_LOCK_V1_AUDITIONS,
    "interaction_focus_v1": INTERACTION_FOCUS_V1_AUDITIONS,
    "interaction_confirm_v1": INTERACTION_CONFIRM_V1_AUDITIONS,
    "interaction_reject_v1": INTERACTION_REJECT_V1_AUDITIONS,
    "room_entered_v1": ROOM_ENTERED_V1_AUDITIONS,
    "room_exit_unlocked_v1": ROOM_EXIT_UNLOCKED_V1_AUDITIONS,
    "room_exit_confirmed_v1": ROOM_EXIT_CONFIRMED_V1_AUDITIONS,
    "forest_ambience_sparse_v1": FOREST_AMBIENCE_SPARSE_V1_AUDITIONS,
    "forest_ambience_points_v3": FOREST_AMBIENCE_POINTS_V3_AUDITIONS,
    "barrier_broken_v1": BARRIER_BROKEN_V1_AUDITIONS,
    "forest_music_exploration_v1": FOREST_MUSIC_EXPLORATION_V1_AUDITIONS,
    "forest_music_combat_v1": FOREST_MUSIC_COMBAT_V1_AUDITIONS,
    "forest_music_victory_v1": FOREST_MUSIC_VICTORY_V1_AUDITIONS,
    "forest_music_defeat_v1": FOREST_MUSIC_DEFEAT_V1_AUDITIONS,
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--work-root", type=Path, default=DEFAULT_WORK_ROOT)
    parser.add_argument("--index", type=Path, default=DEFAULT_INDEX)
    parser.add_argument("--batch", default="audition_v1")
    parser.add_argument("--preset", choices=tuple(PRESETS), default="realistic_v1")
    return parser.parse_args()


def load_index(path: Path) -> dict[tuple[str, int], dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    return {(row["stableEventId"], int(row["candidateRank"])): row for row in rows}


def resolve_source(
    source: IndexedSource | DirectSource,
    index: dict[tuple[str, int], dict[str, str]],
    work_root: Path,
) -> dict[str, object]:
    if isinstance(source, IndexedSource):
        row = index[(source.event_id, source.rank)]
        path = Path(row["resolvedPath"])
        metadata: dict[str, object] = {
            "database": row["database"],
            "recid": int(row["recid"]),
            "candidateEventId": source.event_id,
            "candidateRank": source.rank,
            "filename": row["filename"],
            "description": row["description"],
            "keywords": row["keywords"],
            "category": row["category"],
            "subCategory": row["subCategory"],
            "library": row["library"],
            "audioCrc": row["audioCrc"],
        }
    else:
        path = source.path
        metadata = {
            "database": source.database,
            "recid": source.recid,
            "candidateEventId": "curated-direct",
            "candidateRank": 0,
            "filename": path.name,
            "description": source.description,
            "keywords": source.keywords,
            "category": source.category,
            "subCategory": source.sub_category,
            "library": source.library,
            "audioCrc": source.audio_crc,
        }
    if not path.is_file():
        raise FileNotFoundError(f"Audition source is missing: {path}")
    with warnings.catch_warnings(record=True) as caught:
        warnings.simplefilter("always")
        source_analysis = analyze(path)
    source_warnings = [str(item.message) for item in caught]
    render_path = path
    repair: dict[str, object] | None = None
    if any("EOF prematurely" in message for message in source_warnings):
        if not source.repair_truncated_tail:
            raise ValueError(f"Audition source has a truncated WAV data chunk: {path}")
        original_hash = sha256_file(path)
        render_path = work_root / "edits" / "sanitized_sources" / f"{original_hash}.wav"
        sample_rate, data, _ = read_wav(path)
        write_pcm24(render_path, sample_rate, data)
        with warnings.catch_warnings(record=True) as repaired_warnings:
            warnings.simplefilter("always")
            repaired_analysis = analyze(render_path)
        repaired_warning_text = [str(item.message) for item in repaired_warnings]
        if any("EOF prematurely" in message for message in repaired_warning_text):
            raise ValueError(f"Sanitized WAV still has a truncated data chunk: {render_path}")
        repair = {
            "reason": "source WAV data length exceeded the available tail",
            "path": str(render_path),
            "sha256": sha256_file(render_path),
            "analysis": repaired_analysis,
            "warnings": repaired_warning_text,
        }
    metadata.update(
        {
            "path": str(path),
            "renderPath": str(render_path),
            "gainDb": source.gain_db,
            "trimStart": source.trim_start,
            "pitchSemitones": source.pitch_semitones,
            "delayMs": source.delay_ms,
            "sha256": sha256_file(path),
            "sourceAnalysis": source_analysis,
            "sourceWarnings": source_warnings,
        }
    )
    if repair is not None:
        metadata["sourceRepair"] = repair
    return metadata


def write_csv(path: Path, rows: list[dict[str, object]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def main() -> int:
    args = parse_args()
    index = load_index(args.index)
    output_root = args.work_root / "candidates" / args.batch
    output_root.mkdir(parents=True, exist_ok=True)

    manifest_options: list[dict[str, object]] = []
    summary_rows: list[dict[str, object]] = []
    playlists: dict[str, list[str]] = {}

    for option in PRESETS[args.preset]:
        layers = [resolve_source(source, index, args.work_root) for source in option.sources]
        output = output_root / option.output_name
        report = render(
            Namespace(
                input=[Path(layer["renderPath"]) for layer in layers],
                input_gain_db=[float(layer["gainDb"]) for layer in layers],
                input_trim_start=[float(layer["trimStart"]) for layer in layers],
                input_pitch_semitones=[float(layer["pitchSemitones"]) for layer in layers],
                input_delay_ms=[float(layer["delayMs"]) for layer in layers],
                output=output,
                report=output.with_suffix(".render.json"),
                sample_rate=48000,
                channels=option.channels,
                trim_start=option.trim_start,
                duration=option.duration,
                fade_in_ms=option.fade_in_ms,
                fade_out_ms=option.fade_out_ms,
                loop_crossfade_ms=option.loop_crossfade_ms,
                peak_dbfs=option.peak_dbfs,
                transient_boost_db=option.transient_boost_db,
                transient_window_ms=option.transient_window_ms,
                saturation_drive=option.saturation_drive,
            )
        )
        rendered = report["output"]
        manifest_options.append(
            {
                "stableEventId": option.event_id,
                "label": option.label,
                "intent": option.intent,
                "approvalStatus": "PendingHumanApproval",
                "output": rendered,
                "layers": layers,
                "settings": report["settings"],
            }
        )
        summary_rows.append(
            {
                "stableEventId": option.event_id,
                "label": option.label,
                "intent": option.intent,
                "previewPath": str(output),
                "previewSha256": rendered["sha256"],
                "sampleRate": rendered["sampleRate"],
                "bitDepth": rendered["bitDepth"],
                "channels": rendered["channels"],
                "durationSeconds": round(float(rendered["durationSeconds"]), 3),
                "peakTimeMs": rendered["peakTimeMs"],
                "activeDurationMs": rendered["activeDurationMs"],
                "peakDbfs": rendered["peakDbfs"],
                "clippedSamples": rendered["clippedSamples"],
                "approvalStatus": "PendingHumanApproval",
            }
        )
        playlists.setdefault(option.event_id, []).append(output.name)

    manifest = {
        "batch": args.batch,
        "sourceIndex": str(args.index),
        "sourceIndexSha256": sha256_file(args.index),
        "outputRoot": str(output_root),
        "readOnlySources": True,
        "previewFormat": "48kHz/24-bit PCM WAV",
        "approvalStatus": "PendingHumanApproval",
        "options": manifest_options,
    }
    manifest_path = args.work_root / "manifests" / f"{args.batch}.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    write_csv(args.work_root / "reports" / f"{args.batch}.csv", summary_rows)

    for event_id, filenames in playlists.items():
        safe_name = event_id.replace(".", "_")
        playlist = "#EXTM3U\n" + "\n".join(filenames) + "\n"
        (output_root / f"{safe_name}.m3u8").write_text(playlist, encoding="utf-8")

    print(json.dumps({"manifest": str(manifest_path), "previews": len(summary_rows)}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
