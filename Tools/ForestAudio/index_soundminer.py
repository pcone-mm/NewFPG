#!/usr/bin/env python3
"""Build a read-only Soundminer candidate index for the Forest audio slice.

The Soundminer databases are opened with SQLite's read-only URI mode. This
tool only writes derived CSV reports into the external work directory.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sqlite3
from pathlib import Path
from typing import Iterable


DEFAULT_DATABASES = (
    Path(r"C:\Users\11418\AppData\Roaming\SMDataBeta\Databases\BOOM_LIBRARY.sqlite"),
    Path(r"C:\Users\11418\AppData\Roaming\SMDataBeta\Databases\Epic_Stock_Media.sqlite"),
    Path(r"C:\Users\11418\AppData\Roaming\SMDataBeta\Databases\KHRON.sqlite"),
)
DEFAULT_AUDIO_ROOT = Path(r"F:\Audio")
PILOT_EVENT_IDS = {
    "event.fei.primary.attack.0",
    "event.fei.primary.hit.base",
    "event.fei.primary.hit.weakpoint",
    "event.burstbug.fast.telegraph",
    "event.forest.ambience.loop",
}

EVENT_TUNING = {
    "event.fei.primary.attack.0": {
        "positive": ("game", "arcade", "cartoon", "magic", "energy", "laser", "plasma", "blaster", "zap", "pulse", "spark", "short"),
        "negative": ("pistol", "handgun", "firearm", "gunshot", "rifle", "shotgun", "distant", "realistic", "reload", "shell"),
    },
    "event.fei.reload.commit.0": {
        "positive": (
            "game", "arcade", "cartoon", "designed", "mechanical", "mechanism",
            "click", "clack", "ratchet", "snap", "lock", "switch", "lever",
            "button", "short", "tight", "sci fi", "scifi", "robot",
        ),
        "negative": (
            "gunshot", "firearm", "pistol", "handgun", "rifle", "shotgun",
            "military", "shell", "bullet", "magazine", "ammo", "reload",
            "realistic", "foley", "cloth", "bag", "backpack", "long", "roomy",
            "whoosh", "wash", "impact", "explosion", "voice", "vocal",
        ),
        "hardExclude": (
            "ui_", "interface", "menu", "notification", "music", "gunshot",
            "firearm", "pistol", "rifle", "shotgun", "military", "bullet",
            "shell", "magazine", "ammo", "weapon reload", "reload gun",
        ),
        "requiredGroups": (
            ("click", "clack", "ratchet", "snap", "lock", "mechanical", "mechanism"),
            ("game", "arcade", "cartoon", "designed", "sci fi", "scifi", "robot"),
        ),
        "minimumDuration": 0.12,
        "idealDuration": 0.32,
        "maximumDuration": 0.8,
        "hardMaximumDuration": 2.0,
    },
    "presentation.fei.secondary.execute.audio.0": {
        "positive": (
            "game", "arcade", "cartoon", "designed", "magic", "arcane",
            "energy", "spell", "sci fi", "scifi", "plasma", "laser",
            "projectile", "launch", "release", "cast", "shoot", "shot",
            "blast", "burst", "pulse", "zap", "spark", "short", "tight",
        ),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "pistol", "handgun", "rifle",
            "shotgun", "machine gun", "military", "shell", "bullet", "reload",
            "distant", "roomy", "long", "drone", "ambience", "music",
            "impact", "collision", "ricochet", "hit", "explosion", "debris",
            "voice", "vocal",
        ),
        "hardExclude": (
            "ui_", "interface", "menu", "notification", "collect", "pickup",
            "achievement", "music", "ambience", "voice", "vocal", "firearm", "gun",
            "gunshot", "pistol", "handgun", "rifle", "shotgun", "military",
            "shell", "reload", "impact", "collision", "ricochet", "debris",
        ),
        "requiredGroups": (
            (
                "launch", "release", "cast", "shoot", "shot", "blast",
                "burst", "pulse", "projectile", "weapon", "spell",
            ),
            (
                "game", "arcade", "cartoon", "designed", "magic", "arcane",
                "energy", "spell", "sci fi", "scifi", "plasma", "laser",
            ),
        ),
        "minimumDuration": 0.08,
        "idealDuration": 0.55,
        "maximumDuration": 1.2,
        "hardMaximumDuration": 3.0,
    },
    "event.fei.secondary.execute.impact.base": {
        "positive": (
            "game", "arcade", "cartoon", "designed", "magic", "arcane",
            "energy", "spell", "sci fi", "scifi", "plasma", "electric",
            "projectile", "impact", "hit", "blast", "burst", "explosion",
            "pop", "punchy", "short", "tight",
        ),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "pistol", "rifle",
            "shotgun", "flesh", "blood", "bone", "body", "punch", "kick",
            "smack", "wood", "stone", "metal", "debris", "collapse",
            "distant", "roomy", "long", "wash", "delay", "whoosh", "swish",
            "voice", "vocal", "ambience", "music",
        ),
        "hardExclude": (
            "ui_", "interface", "menu", "notification", "collect", "pickup",
            "achievement", "music", "ambience", "voice", "vocal", "firearm",
            "gun", "gunshot", "pistol", "rifle", "shotgun", "flesh", "blood",
            "bone", "body", "punch", "kick", "smack", "wood", "stone",
            "debris", "collapse", "whoosh", "swish", "swoosh", "flow",
        ),
        "requiredGroups": (
            ("impact", "hit", "blast", "burst", "explosion", "collision"),
            (
                "game", "arcade", "cartoon", "designed", "magic", "arcane",
                "energy", "spell", "sci fi", "scifi", "plasma", "electric",
                "projectile",
            ),
        ),
        "minimumDuration": 0.08,
        "idealDuration": 0.55,
        "maximumDuration": 1.2,
        "hardMaximumDuration": 3.0,
    },
    "event.fei.secondary.release.impact.base": {
        "positive": (
            "game", "arcade", "cartoon", "anime", "designed", "magic",
            "arcane", "energy", "spell", "sci fi", "scifi", "plasma",
            "electric", "charged", "powerful", "projectile", "impact",
            "hit", "blast", "burst", "explosion", "detonation", "crystal",
            "ice", "icy", "shatter", "shattering", "massive", "crackling",
            "heavy", "boom", "punchy", "short", "tight",
        ),
        "excludeRecids": (2843, 2844, 2845, 2846, 2847, 2848, 2849),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "pistol", "rifle",
            "shotgun", "bullet", "flesh", "blood", "bone", "body", "punch",
            "kick", "smack", "stab", "pierce", "needle", "dart", "arrow",
            "wood", "stone", "debris", "collapse", "distant", "roomy",
            "long", "wash", "delay", "whoosh", "swish", "voice", "vocal",
            "ambience", "music",
        ),
        "hardExclude": (
            "ui_", "interface", "menu", "notification", "collect", "pickup",
            "achievement", "music", "ambience", "voice", "vocal", "firearm",
            "gunshot", "pistol", "rifle", "shotgun", "flesh", "blood", "bone",
            "body", "punch", "kick", "smack", "stab", "pierce", "needle",
            "dart", "arrow", "wood", "stone", "debris", "collapse", "whoosh",
            "swish", "swoosh", "flow",
        ),
        "requiredGroups": (
            ("impact", "hit", "blast", "burst", "explosion", "detonation", "collision"),
            (
                "game", "arcade", "cartoon", "anime", "designed", "magic",
                "arcane", "energy", "spell", "sci fi", "scifi", "plasma",
                "electric", "charged", "crystal",
            ),
        ),
        "minimumDuration": 0.1,
        "idealDuration": 0.8,
        "maximumDuration": 1.6,
        "hardMaximumDuration": 4.0,
    },
    "event.fei.secondary.release.impact.weakpoint": {
        "positive": (
            "game", "arcade", "cartoon", "anime", "designed", "magic",
            "arcane", "energy", "spell", "sci fi", "scifi", "plasma",
            "electric", "charged", "powerful", "projectile", "impact", "hit",
            "blast", "burst", "crystal", "glass", "metallic", "bright",
            "spark", "shimmer", "ice", "icy", "shatter", "shattering",
            "massive", "crackling", "heavy", "boom", "critical", "punchy",
            "short",
        ),
        "excludeRecids": (2843, 2844, 2845, 2846, 2847, 2848, 2849),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "pistol", "rifle",
            "shotgun", "bullet", "flesh", "blood", "bone", "body", "punch",
            "kick", "smack", "stab", "pierce", "needle", "dart", "arrow",
            "wood", "stone", "debris", "collapse", "distant", "roomy", "long",
            "wash", "delay", "whoosh", "swish", "voice", "vocal", "ambience",
            "music",
        ),
        "hardExclude": (
            "ui_", "interface", "menu", "notification", "collect", "pickup",
            "achievement", "music", "ambience", "voice", "vocal", "firearm",
            "gunshot", "pistol", "rifle", "shotgun", "flesh", "blood", "bone",
            "body", "punch", "kick", "smack", "stab", "pierce", "needle",
            "dart", "arrow", "wood", "stone", "debris", "collapse", "whoosh",
            "swish", "swoosh", "flow",
        ),
        "requiredGroups": (
            ("impact", "hit", "blast", "burst", "explosion", "detonation", "collision"),
            (
                "game", "arcade", "cartoon", "anime", "designed", "magic",
                "arcane", "energy", "spell", "sci fi", "scifi", "plasma",
                "electric", "charged", "crystal",
            ),
        ),
        "minimumDuration": 0.1,
        "idealDuration": 0.8,
        "maximumDuration": 1.6,
        "hardMaximumDuration": 4.0,
    },
    "presentation.fei.secondary.charge.audio.0": {
        "positive": (
            "game", "arcade", "cartoon", "designed", "magic", "arcane",
            "energy", "spell", "sci fi", "scifi", "electric", "plasma",
            "charge", "charging", "power up", "powerup", "build", "buildup",
            "rise", "riser", "start", "ignite", "ignition", "short",
        ),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "vehicle", "engine",
            "ambience", "music", "voice", "vocal", "impact", "hit",
            "explosion", "debris", "long", "distant", "roomy",
        ),
        "hardExclude": (
            "ui_", "interface", "menu", "notification", "collect", "pickup",
            "achievement", "music", "ambience", "voice", "vocal", "firearm",
            "gunshot", "vehicle", "engine", "impact", "collision", "debris",
        ),
        "requiredGroups": (
            ("charge", "charging", "power up", "powerup", "buildup", "riser"),
            (
                "game", "arcade", "cartoon", "designed", "magic", "arcane",
                "energy", "spell", "sci fi", "scifi", "electric", "plasma",
            ),
        ),
        "minimumDuration": 0.18,
        "idealDuration": 1.2,
        "maximumDuration": 2.5,
        "hardMaximumDuration": 5.0,
    },
    "presentation.fei.secondary.charge.hold.0": {
        "positive": (
            "game", "arcade", "cartoon", "designed", "magic", "arcane",
            "energy", "spell", "sci fi", "scifi", "electric", "plasma",
            "charge", "charging", "power", "loop", "hum", "pulse", "pulsing",
            "sustain", "sustained", "hold", "drone", "tone",
        ),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "vehicle", "engine",
            "ambience", "music", "voice", "vocal", "impact", "hit",
            "explosion", "debris", "distant", "roomy", "aggressive", "alarm",
        ),
        "hardExclude": (
            "ui_", "interface", "menu", "notification", "collect", "pickup",
            "achievement", "music", "ambience", "voice", "vocal", "firearm",
            "gunshot", "vehicle", "engine", "impact", "collision", "debris",
        ),
        "requiredGroups": (
            ("loop", "hum", "pulse", "pulsing", "sustain", "sustained", "hold", "drone", "tone"),
            (
                "charge", "charging", "power", "magic", "arcane", "energy",
                "spell", "sci fi", "scifi", "electric", "plasma",
            ),
        ),
        "minimumDuration": 0.5,
        "idealDuration": 4.0,
        "maximumDuration": 10.0,
        "hardMaximumDuration": 20.0,
    },
    "presentation.fei.secondary.release.audio.0": {
        "positive": (
            "game", "arcade", "cartoon", "anime", "designed", "magic",
            "arcane", "ice", "crystal", "energy", "spell", "sci fi",
            "scifi", "plasma", "electric", "charged", "release", "launch",
            "shoot", "shot", "cast", "blast", "burst", "snap", "quick",
            "short", "tight",
        ),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "pistol", "rifle",
            "shotgun", "military", "shell", "reload", "flesh", "blood",
            "body", "voice", "vocal", "creature", "monster", "ambience",
            "music", "distant", "roomy", "glued", "long", "wash", "delay",
        ),
        "hardExclude": (
            "ui_", "interface", "menu", "notification", "collect", "pickup",
            "achievement", "music", "ambience", "voice", "vocal", "creature",
            "monster", "firearm", "gunshot", "pistol", "rifle", "shotgun",
            "military", "shell", "reload", "flesh", "blood", "body",
        ),
        "requiredGroups": (
            (
                "release", "launch", "shoot", "shot", "cast", "blast",
                "burst", "attack", "spell",
            ),
            (
                "game", "arcade", "cartoon", "anime", "designed", "magic",
                "arcane", "energy", "sci fi", "scifi", "plasma", "electric",
            ),
        ),
        "minimumDuration": 0.08,
        "idealDuration": 0.75,
        "maximumDuration": 1.6,
        "hardMaximumDuration": 4.0,
    },
    "presentation.fei.secondary.cancel.audio.0": {
        "positive": (
            "game", "arcade", "cartoon", "anime", "designed", "magic",
            "arcane", "ice", "crystal", "energy", "spell", "sci fi",
            "scifi", "electric", "electronic", "synth", "charge", "cancel", "abort", "stop",
            "cutoff", "power down", "powerdown", "deactivate", "deactivated", "shutdown",
            "drain", "discharge", "fail", "failure",
            "fizzle", "dissipate", "poof", "short", "tight",
        ),
        "negative": (
            "release", "launch", "shoot", "shot", "attack", "blast",
            "explosion", "impact", "hit", "success", "reward", "positive",
            "collect", "pickup", "realistic", "firearm", "gun", "voice",
            "vocal", "creature", "monster", "ambience", "music", "long",
            "distant", "roomy", "wash", "gas",
        ),
        "hardExclude": (
            "ui_", "interface", "menu", "notification", "collect", "pickup",
            "achievement", "success", "reward", "music", "ambience", "voice",
            "vocal", "creature", "monster", "zombie", "banshee", "scream",
            "growl", "pain", "hurt", "firearm", "gunshot", "pistol", "rifle",
            "shotgun", "flesh", "blood", "body", "impact", "collision",
            "coin", "money", "bag", "leather", "cloth", "medical",
            "armor", "keys", "chest", "crate", "laser gun",
        ),
        "requiredGroups": (
            (
                "cancel", "abort", "stop", "cutoff", "power down",
                "powerdown", "deactivate", "deactivated", "shutdown", "drain", "discharge",
                "fail", "failure", "fizzle", "dissipate",
            ),
            (
                "game", "arcade", "cartoon", "anime", "designed", "magic",
                "arcane", "energy", "spell", "sci fi", "scifi", "electric",
                "electronic", "synth",
            ),
        ),
        "minimumDuration": 0.08,
        "idealDuration": 0.45,
        "maximumDuration": 0.9,
        "hardMaximumDuration": 2.5,
    },
    "event.fei.primary.hit.base": {
        "positive": ("designed", "game", "arcade", "weapon", "projectile", "energy", "magic", "arcane", "electric", "laser", "plasma", "snap", "spark", "zap", "ricochet", "impact", "hit", "short", "tight"),
        "negative": ("punch", "body", "flesh", "bone", "blood", "realistic", "smack", "kick", "foley", "big", "gritty", "heavy", "delay", "wash", "glued", "long"),
        "hardExclude": ("ui_", "ui ", "interface", "menu", "button", "notification", "collect", "positive hit", "bubble", "liquid", "potion", "click", "firearm", "distant", "rifle", "submachine", "sniper", "shotgun", "military", "battle royale", "shield", "wood", "armor", "bludgeon", "whoosh", "swish", "swoosh", "flow"),
        "requiredGroups": (
            ("impact", "hit", "ricochet", "collision"),
            ("weapon", "projectile", "bullet", "energy", "laser", "plasma", "arcade", "game"),
        ),
        "idealDuration": 0.45,
        "maximumDuration": 1.0,
    },
    "event.fei.primary.hit.weakpoint": {
        "positive": ("game", "critical", "reward", "sparkle", "chime", "bright", "magic", "energy", "crystal", "metallic", "ping", "impact"),
        "negative": ("body", "punch", "flesh", "cloth", "soft", "voice", "vocal", "wash", "long tail"),
    },
    "event.fei.primary.hit.environment": {
        "positive": (
            "game", "arcade", "cartoon", "designed", "ricochet", "impact",
            "hit", "magic", "energy", "electric", "spark", "laser", "plasma",
            "projectile", "short", "dry",
        ),
        "negative": (
            "metal", "stone", "wood", "flesh", "body", "blood", "punch",
            "kick", "debris", "collapse", "explosion", "distant", "long",
            "heavy", "gritty", "wash", "delay", "whoosh",
        ),
        "hardExclude": (
            "ui_", "interface", "menu", "notification", "collect", "pickup",
            "voice", "vocal", "ambience", "music", "blunt", "club", "smack",
            "firearm", "gunshot", "rifle", "shotgun", "reload", "armor",
            "cloth", "arcane snap",
        ),
        "requiredGroups": (
            ("impact", "hit", "ricochet", "collision"),
            (
                "game", "arcade", "cartoon", "designed", "magic", "energy",
                "electric", "laser", "plasma", "projectile", "scifi", "sci fi",
            ),
        ),
        "idealDuration": 0.65,
        "maximumDuration": 1.2,
    },
    "event.burstbug.fast.telegraph": {
        "positive": ("game", "arcade", "sci fi", "electronic", "signal", "warning", "chirp", "charge", "alert", "beep", "fast", "short"),
        "negative": ("vocal", "voice", "roar", "growl", "realistic", "ambience", "long tail", "collect", "item", "pickup", "achievement", "reward", "positive"),
        "hardExclude": ("collect item", "pickup", "achievement", "success", "notification positive", "music"),
    },
    "event.burstbug.fast.release": {
        "positive": ("game", "arcade", "sci fi", "insect", "projectile", "release", "launch", "snap", "energy", "fast", "short"),
        "negative": ("voice", "vocal", "roar", "growl", "realistic", "ambience", "long", "heavy"),
        "hardExclude": ("ui_", "interface", "menu", "notification", "music"),
        "requiredGroups": (("release", "launch", "attack", "whoosh", "snap", "projectile"),),
        "idealDuration": 0.45,
        "maximumDuration": 1.2,
    },
    "event.burstbug-interceptable-volley.telegraph": {
        "positive": (
            "game", "arcade", "cartoon", "designed", "sci fi", "scifi",
            "electronic", "energy", "magic", "signal", "warning", "alert",
            "alarm", "charge", "charging", "buildup", "riser", "pulse",
            "chirp", "beep", "sequence", "triple", "short", "dry",
        ),
        "negative": (
            "voice", "vocal", "roar", "growl", "scream", "pain", "hurt",
            "realistic", "firearm", "gun", "gunshot", "pistol", "rifle",
            "shotgun", "impact", "hit", "explosion", "wet", "liquid",
            "ambience", "music", "distant", "roomy", "long",
        ),
        "hardExclude": (
            "voice", "vocal", "banshee", "zombie", "monster pain",
            "creature pain", "firearm", "gunshot", "pistol", "rifle",
            "shotgun", "wet", "liquid", "hose", "spray", "collect",
            "pickup", "achievement", "success", "reward", "music",
            "ambience", "impact", "collision",
        ),
        "requiredGroups": (
            (
                "warning", "alert", "alarm", "signal", "charge",
                "charging", "buildup", "riser", "telegraph",
            ),
            (
                "game", "arcade", "cartoon", "designed", "sci fi",
                "scifi", "electronic", "energy", "magic",
            ),
        ),
        "minimumDuration": 0.18,
        "idealDuration": 1.1,
        "maximumDuration": 1.5,
        "hardMaximumDuration": 3.0,
    },
    "event.burstbug-interceptable-volley.release": {
        "positive": (
            "game", "arcade", "cartoon", "anime", "designed", "sci fi",
            "scifi", "electronic", "energy", "magic", "spell", "plasma",
            "laser", "projectile", "release", "launch", "shoot", "shot",
            "attack", "burst", "volley", "triple", "pulse", "zap", "snap",
            "short", "tight", "dry",
        ),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "pistol", "rifle",
            "shotgun", "machine gun", "military", "shell", "bullet",
            "reload", "wet", "liquid", "hose", "spray", "gas", "voice",
            "vocal", "roar", "growl", "impact", "collision", "debris",
            "ambience", "music", "distant", "roomy", "long",
        ),
        "hardExclude": (
            "firearm", "gunshot", "pistol", "rifle", "shotgun",
            "machine gun", "military", "shell", "bullet", "reload",
            "wet", "liquid", "hose", "spray", "gas", "voice", "vocal",
            "monster", "creature", "impact", "collision", "debris",
            "collect", "pickup", "achievement", "success", "reward",
            "music", "ambience",
        ),
        "requiredGroups": (
            (
                "release", "launch", "shoot", "shot", "attack", "burst",
                "volley", "projectile", "cast",
            ),
            (
                "game", "arcade", "cartoon", "anime", "designed",
                "sci fi", "scifi", "electronic", "energy", "magic",
                "spell", "plasma", "laser",
            ),
        ),
        "minimumDuration": 0.08,
        "idealDuration": 0.55,
        "maximumDuration": 0.9,
        "hardMaximumDuration": 2.0,
    },
    "event.burstbug-interceptable-volley.projectile": {
        "positive": (
            "game", "arcade", "cartoon", "designed", "sci fi", "scifi",
            "energy", "magic", "spell", "electric", "plasma", "projectile",
            "orb", "bolt", "dart", "missile", "flight", "flyby", "whoosh",
            "whiz", "hum", "buzz", "short", "light", "subtle",
        ),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "pistol", "rifle",
            "shotgun", "bullet", "impact", "hit", "explosion", "debris",
            "voice", "vocal", "roar", "growl", "ambience", "music",
            "distant", "roomy", "long", "heavy", "large",
        ),
        "hardExclude": (
            "firearm", "gunshot", "pistol", "rifle", "shotgun", "bullet",
            "shell", "reload", "impact", "collision", "explosion", "debris",
            "flesh", "blood", "bone", "gore", "voice", "vocal", "monster",
            "creature", "ui_", "interface", "menu", "notification", "music",
            "ambience",
        ),
        "requiredGroups": (
            (
                "projectile", "orb", "bolt", "dart", "missile", "flight",
                "flyby", "whoosh", "whiz", "hum", "buzz",
            ),
            (
                "game", "arcade", "cartoon", "designed", "sci fi", "scifi",
                "energy", "magic", "spell", "electric", "plasma",
            ),
        ),
        "minimumDuration": 0.08,
        "idealDuration": 0.55,
        "maximumDuration": 1.2,
        "hardMaximumDuration": 2.5,
    },
    "event.burstbug-interceptable-volley.interception": {
        "positive": (
            "game", "arcade", "cartoon", "designed", "sci fi", "scifi",
            "energy", "magic", "spell", "electric", "plasma", "projectile",
            "intercept", "deflect", "parry", "block", "shield", "destroy",
            "dispel", "shatter", "burst", "pop", "spark", "impact", "hit",
            "success", "positive", "short", "crisp", "bright", "punchy",
        ),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "pistol", "rifle",
            "shotgun", "bullet", "sword", "blade", "metal weapon", "flesh",
            "blood", "bone", "gore", "glass", "debris", "voice", "vocal",
            "roar", "growl", "ambience", "music", "distant", "roomy", "long",
        ),
        "hardExclude": (
            "firearm", "gunshot", "pistol", "rifle", "shotgun", "bullet",
            "shell", "reload", "flesh", "blood", "bone", "gore", "glass",
            "voice", "vocal", "monster", "creature", "ui_", "interface",
            "menu", "notification", "music", "ambience",
        ),
        "requiredGroups": (
            (
                "intercept", "deflect", "parry", "block", "shield", "destroy",
                "dispel", "shatter", "burst", "pop", "spark", "impact", "hit",
            ),
            (
                "game", "arcade", "cartoon", "designed", "sci fi", "scifi",
                "energy", "magic", "spell", "electric", "plasma",
            ),
        ),
        "minimumDuration": 0.05,
        "idealDuration": 0.35,
        "maximumDuration": 0.75,
        "hardMaximumDuration": 1.5,
    },
    "event.burstbug-interceptable-volley.impact": {
        "positive": (
            "game", "arcade", "cartoon", "anime", "designed", "sci fi",
            "scifi", "energy", "magic", "spell", "electric", "plasma",
            "projectile", "impact", "hit", "blast", "burst", "explosion",
            "detonation", "spark", "pop", "punch", "short", "tight", "dry",
        ),
        "negative": (
            "realistic", "firearm", "gun", "gunshot", "pistol", "rifle",
            "shotgun", "bullet", "flesh", "blood", "bone", "gore", "wet",
            "liquid", "debris", "voice", "vocal", "roar", "growl", "ambience",
            "music", "distant", "roomy", "long", "huge", "massive",
        ),
        "hardExclude": (
            "firearm", "gunshot", "pistol", "rifle", "shotgun", "bullet",
            "shell", "reload", "flesh", "blood", "bone", "gore", "wet",
            "liquid", "voice", "vocal", "monster", "creature", "ui_",
            "interface", "menu", "notification", "music", "ambience",
        ),
        "requiredGroups": (
            (
                "impact", "hit", "blast", "burst", "explosion", "detonation",
                "spark", "pop",
            ),
            (
                "game", "arcade", "cartoon", "anime", "designed", "sci fi",
                "scifi", "energy", "magic", "spell", "electric", "plasma",
            ),
        ),
        "minimumDuration": 0.05,
        "idealDuration": 0.38,
        "maximumDuration": 0.8,
        "hardMaximumDuration": 1.5,
    },
    "event.burstbug.fast.projectile": {
        "positive": ("game", "arcade", "sci fi", "insect", "projectile", "fly", "flyby", "dart", "buzz", "subtle", "short"),
        "negative": ("gunshot", "firearm", "explosion", "heavy", "large", "voice", "vocal", "ambience", "long"),
        "hardExclude": ("ui_", "interface", "menu", "notification", "music"),
        "requiredGroups": (("projectile", "fly", "flyby", "whoosh", "dart", "buzz"),),
        "idealDuration": 0.8,
        "maximumDuration": 2.5,
    },
    "event.burstbug.fast.hit": {
        "positive": ("game", "arcade", "damage", "impact", "hit", "projectile", "energy", "sting", "short", "punchy"),
        "negative": ("flesh", "blood", "bone", "gore", "realistic", "voice", "vocal", "ambience", "long", "heavy"),
        "hardExclude": ("ui_", "interface", "menu", "notification", "music"),
        "requiredGroups": (("impact", "hit", "damage", "collision"),),
        "idealDuration": 0.5,
        "maximumDuration": 1.2,
    },
    "event.combat.player.damaged": {
        # Player damage is a character-voice cue. Keep the semantic gate
        # narrow so weapon impacts and creature vocals do not leak into the
        # audition set.
        "positive": ("game", "arcade", "player", "hurt", "voice", "voices", "vocal", "voxfem", "human", "female", "woman", "girl", "short", "quick", "reaction", "effort", "exertion", "grunt", "gasp", "breath", "exhale", "closed mouth"),
        "negative": ("male", "flesh", "blood", "bone", "gore", "realistic", "impact", "punch", "weapon", "explosion", "ambience", "music", "creature", "monster", "alien", "robot", "hard", "long", "pain", "beaten"),
        "hardExclude": ("ui_", "interface", "menu", "notification", "music", "male", "creature", "creatures", "monster", "alien", "robot", "animal", "zombie", "undead", "horror", "roar", "scream", "cry", "groan", "shout", "shouts", "war", "suffering", "roomy", "food", "eating", "chew", "crunch", "gulp", "wet", "feed", "satisfaction"),
        "requiredGroups": (("voice", "voices", "vocal", "voxfem"), ("hurt", "pain", "reaction", "effort", "exertion", "grunt", "gasp", "breath", "exhale"), ("female", "woman", "girl")),
        "excludeRecids": (1994, 1995, 1996, 1997, 1998, 1999, 2012, 2017, 5560, 5561, 5562, 9440, 9441, 9442, 9443),
        "minimumDuration": 0.12,
        "idealDuration": 0.5,
        "maximumDuration": 0.9,
        # BOOM source collections contain several separated 0.25-0.60s takes.
        # The final audition renderer extracts only one take per candidate.
        "hardMaximumDuration": 15.0,
    },
    "event.combat.player.barrier_broken": {
        "positive": ("game", "arcade", "energy", "barrier", "shield", "break", "shatter", "crystal", "spark", "short"),
        "negative": ("window", "bottle", "ceramic", "debris", "collapse", "realistic", "ambience", "long"),
        "hardExclude": ("ui_", "interface", "menu", "notification", "music"),
        "requiredGroups": (("break", "broken", "shatter", "crack"), ("shield", "barrier", "energy", "magic", "game")),
        "idealDuration": 0.8,
        "maximumDuration": 2.0,
    },
    "event.combat.enemy.break": {
        "positive": ("game", "arcade", "enemy", "stagger", "break", "armor", "shield", "energy", "magic", "crystal", "shatter", "crack", "impact", "burst", "short"),
        "negative": ("vehicle", "building", "debris", "collapse", "realistic", "voice", "vocal", "ambience", "long"),
        "hardExclude": ("ui_", "interface", "menu", "notification", "music", "bone", "flesh", "gore", "wood", "door", "firearm", "gunshot"),
        "requiredGroups": (
            ("break", "broken", "crack", "shatter", "burst", "stagger"),
            ("game", "arcade", "armor", "shield", "energy", "magic", "crystal"),
        ),
        "idealDuration": 0.8,
        "maximumDuration": 2.0,
    },
    "event.combat.reticle.lock": {
        "positive": ("game", "ui", "interface", "target", "lock", "confirm", "digital", "tick", "beep", "short"),
        "negative": ("ambience", "music", "voice", "vocal", "long", "heavy", "cinematic"),
        "requiredGroups": (("ui", "interface", "target", "lock", "confirm", "beep", "tick"),),
        "idealDuration": 0.2,
        "maximumDuration": 0.8,
    },
    "event.room.entered": {
        "positive": ("game", "ui", "room", "enter", "arrival", "confirm", "soft", "magic", "short"),
        "negative": ("error", "reject", "negative", "alarm", "voice", "vocal", "ambience", "music", "long"),
        "idealDuration": 0.6,
        "maximumDuration": 1.8,
    },
    "event.room.exit.unlocked": {
        "positive": ("game", "ui", "exit", "unlock", "open", "portal", "positive", "confirm", "magic", "short"),
        "negative": ("lock", "error", "reject", "negative", "alarm", "voice", "vocal", "ambience", "music", "long", "backpack", "inventory", "cloth", "bag", "page", "door", "lever", "chest", "wooden"),
        "hardExclude": ("backpack", "inventory", "cloth", "bag", "page turn", "door open", "lever", "chest open"),
        "requiredGroups": (("unlock", "open", "portal", "exit", "gate"),),
        "idealDuration": 0.9,
        "maximumDuration": 2.5,
    },
    "event.room.exit.confirmed": {
        "positive": ("game", "ui", "exit", "confirm", "select", "positive", "travel", "magic", "short"),
        "negative": ("error", "reject", "negative", "alarm", "voice", "vocal", "ambience", "music", "long"),
        "idealDuration": 0.55,
        "maximumDuration": 1.5,
    },
    "event.interaction.focus": {
        "positive": ("game", "ui", "interface", "focus", "hover", "tick", "move", "short", "subtle"),
        "negative": ("confirm", "success", "error", "reject", "alarm", "voice", "vocal", "ambience", "music", "long"),
        "idealDuration": 0.15,
        "maximumDuration": 0.5,
    },
    "event.interaction.confirm": {
        "positive": ("game", "ui", "interface", "confirm", "accept", "select", "click", "positive", "short"),
        "negative": ("error", "reject", "negative", "alarm", "voice", "vocal", "ambience", "music", "long"),
        "idealDuration": 0.25,
        "maximumDuration": 0.8,
    },
    "event.interaction.reject": {
        "positive": ("game", "ui", "interface", "reject", "error", "negative", "deny", "muted", "short"),
        "negative": ("success", "victory", "positive", "voice", "vocal", "ambience", "music", "long", "alarm"),
        "idealDuration": 0.3,
        "maximumDuration": 0.9,
    },
    "event.luan-summon.telegraph": {
        "positive": (
            "game", "anime", "arcade", "magic", "spell", "summon",
            "portal", "charge", "rise", "rising", "shimmer", "pulse",
            "warning", "energy", "cast", "conjure", "teleport", "short",
        ),
        "negative": (
            "realistic", "voice", "vocal", "creature", "monster", "roar",
            "scream", "ambience", "music", "long", "distant", "roomy",
            "firearm", "gun", "weapon", "impact", "debris", "collapse",
        ),
        "hardExclude": (
            "ui_", "interface", "notification", "music", "ambience",
            "voice", "vocal", "creature", "monster", "roar", "scream",
            "flesh", "blood", "gore", "firearm", "gunshot", "pistol",
            "rifle", "shotgun", "footstep", "door", "vehicle",
        ),
        "requiredGroups": (
            (
                "summon", "portal", "charge", "rise", "rising", "cast",
                "conjure", "teleport", "spell", "power up",
            ),
            (
                "game", "anime", "arcade", "magic", "spell", "energy",
                "fantasy", "sci fi", "scifi",
            ),
        ),
        "minimumDuration": 0.25,
        "idealDuration": 0.85,
        "maximumDuration": 2.0,
        "hardMaximumDuration": 4.0,
    },
    "event.luan-summon.commit": {
        "positive": (
            "game", "anime", "arcade", "magic", "spell", "summon",
            "appearance", "materialize", "teleport", "portal", "warp",
            "burst", "sparkle", "poof", "energy", "short", "arrival",
        ),
        "negative": (
            "realistic", "voice", "vocal", "creature", "monster", "roar",
            "scream", "ambience", "music", "long", "distant", "roomy",
            "firearm", "gun", "weapon", "flesh", "blood", "debris",
        ),
        "hardExclude": (
            "ui_", "interface", "notification", "music", "ambience",
            "voice", "vocal", "creature", "monster", "roar", "scream",
            "flesh", "blood", "gore", "firearm", "gunshot", "pistol",
            "rifle", "shotgun", "footstep", "door", "vehicle",
        ),
        "requiredGroups": (
            (
                "summon", "appearance", "materialize", "teleport", "portal",
                "warp", "burst", "poof", "arrival", "spawn",
            ),
            (
                "game", "anime", "arcade", "magic", "spell", "energy",
                "fantasy", "sci fi", "scifi",
            ),
        ),
        "minimumDuration": 0.08,
        "idealDuration": 0.7,
        "maximumDuration": 1.6,
        "hardMaximumDuration": 3.0,
    },
    "event.luan-summon.self-destruct.1": {
        "positive": (
            "game", "anime", "arcade", "magic", "spell", "energy",
            "self destruct", "destruct", "explode", "explosion", "blast",
            "burst", "detonate", "vanish", "death", "pop", "crash",
            "short", "cartoon", "sci fi", "scifi",
        ),
        "negative": (
            "realistic", "military", "war", "firearm", "gun", "grenade",
            "bomb", "cannon", "artillery", "debris", "rubble", "collapse",
            "building", "distant", "roomy", "large", "massive", "long",
            "voice", "vocal", "creature", "monster", "flesh", "blood",
        ),
        "hardExclude": (
            "ui_", "interface", "notification", "music", "ambience",
            "voice", "vocal", "creature", "monster", "roar", "scream",
            "flesh", "blood", "gore", "firearm", "gunshot", "pistol",
            "rifle", "shotgun", "grenade", "artillery", "footstep",
            "door", "vehicle",
        ),
        "requiredGroups": (
            (
                "self destruct", "destruct", "explode", "explosion",
                "blast", "burst", "detonate", "vanish", "death", "pop",
                "crash",
            ),
            (
                "game", "anime", "arcade", "magic", "spell", "energy",
                "fantasy", "sci fi", "scifi", "cartoon",
            ),
        ),
        "minimumDuration": 0.08,
        "idealDuration": 0.45,
        "maximumDuration": 1.1,
        "hardMaximumDuration": 2.0,
    },
    "event.hudie-projectile.impact.base": {
        "positive": (
            "game", "anime", "magic", "spell", "projectile", "attack",
            "hit", "impact", "short", "bubbly", "tonal", "debuff",
            "piercing", "rich", "layered", "crisp", "pop", "burst",
        ),
        "negative": (
            "realistic", "flesh", "blood", "gore", "body", "creature",
            "voice", "vocal", "heavy", "bass", "stomp", "water", "wet",
            "fire", "huge", "massive", "long", "wash", "roomy", "distant",
        ),
        "hardExclude": (
            "ui_", "interface", "notification", "collect", "pickup", "music",
            "ambience", "voice", "vocal", "creature", "monster", "flesh",
            "blood", "gore", "firearm", "gunshot", "pistol", "rifle",
            "shotgun", "military", "footstep", "door", "vehicle",
        ),
        "requiredGroups": (
            ("impact", "hit", "collision", "burst", "pop"),
            ("magic", "spell", "arcane", "anime", "game"),
        ),
        "idealDuration": 1.35,
        "maximumDuration": 2.5,
        "hardMaximumDuration": 4.0,
    },
    "event.hudie-projectile.impact.weakpoint": {
        "positive": (
            "game", "anime", "magic", "spell", "projectile", "attack",
            "hit", "impact", "short", "bubbly", "tonal", "debuff",
            "piercing", "rich", "layered", "bright", "crisp", "spark",
        ),
        "negative": (
            "realistic", "flesh", "blood", "gore", "body", "creature",
            "voice", "vocal", "heavy", "bass", "stomp", "water", "wet",
            "fire", "huge", "massive", "long", "wash", "roomy", "distant",
        ),
        "hardExclude": (
            "ui_", "interface", "notification", "collect", "pickup", "music",
            "ambience", "voice", "vocal", "creature", "monster", "flesh",
            "blood", "gore", "firearm", "gunshot", "pistol", "rifle",
            "shotgun", "military", "footstep", "door", "vehicle",
        ),
        "requiredGroups": (
            ("impact", "hit", "collision", "burst", "pop"),
            ("magic", "spell", "arcane", "anime", "game"),
        ),
        "idealDuration": 1.35,
        "maximumDuration": 2.5,
        "hardMaximumDuration": 4.0,
    },
    "event.enemy.spawn": {
        "positive": (
            "game", "arcade", "retro", "cartoon", "designed", "magic",
            "energy", "sci fi", "scifi", "spawn", "materialize",
            "appear", "appearance", "teleport", "portal", "summon",
            "pop", "burst", "zap", "spark", "short", "tight",
        ),
        "negative": (
            "realistic", "military", "war", "firearm", "gunshot",
            "pistol", "rifle", "shotgun", "grenade", "artillery",
            "destruction", "debris", "rubble", "collapse", "building",
            "vehicle", "flesh", "blood", "gore", "long", "roomy",
            "distant", "ambience", "music",
        ),
        "hardExclude": (
            "ui_", "interface", "notification", "music", "ambience",
            "voice", "vocal", "creature", "monster", "scream", "moan",
            "groan", "howl", "roar", "flesh", "blood", "gore",
            "firearm", "gunshot", "pistol", "rifle", "shotgun",
            "grenade", "artillery", "footstep", "door", "vehicle",
        ),
        "requiredGroups": (
            (
                "spawn", "materialize", "appear", "appearance",
                "teleport", "portal", "summon",
            ),
            (
                "game", "arcade", "retro", "cartoon", "designed",
                "magic", "energy", "sci fi", "scifi",
            ),
        ),
        "requiredFilenameGroups": ((
            "spawn", "materialize", "appear", "appearance", "teleport",
            "portal", "summon",
        ),),
        "minimumDuration": 0.08,
        "idealDuration": 0.75,
        "maximumDuration": 1.35,
        "hardMaximumDuration": 2.5,
    },
    "event.enemy.death": {
        "positive": (
            "game", "arcade", "cartoon", "stylized", "designed",
            "creature", "monster", "beast", "animal", "alien",
            "dinosaur", "dragon", "insect", "bug", "killed", "death",
            "die", "scream", "squeal", "screech", "cry", "yelp",
            "chirp", "vocalization", "howl", "roar", "growl", "short",
            "tight",
        ),
        "negative": (
            "human", "male", "female", "man", "woman", "boy", "girl",
            "dialog", "dialogue", "speech", "sentence", "words", "laugh",
            "realistic", "military", "war", "firearm", "gunshot",
            "pistol", "rifle", "shotgun", "grenade", "artillery",
            "bomb", "destruction", "debris", "rubble", "collapse",
            "building", "vehicle", "noisy", "crash", "riser", "delay",
            "flesh", "blood", "gore", "squish", "crunch", "moan",
            "groan", "long", "roomy", "distant", "ambience", "music",
        ),
        "hardExclude": (
            "ui_", "interface", "notification", "music", "ambience",
            "human", "male", "female", "man", "woman", "boy", "girl",
            "dialog", "dialogue", "speech", "sentence", "spoken word",
            "flesh", "blood", "gore", "dismember", "decapitation",
            "firearm", "gunshot", "pistol", "rifle", "shotgun",
            "grenade", "artillery", "footstep", "door", "vehicle",
        ),
        "requiredGroups": (
            (
                "creature", "monster", "beast", "animal", "alien",
                "dinosaur", "dragon", "insect", "bug",
            ),
            (
                "scream", "squeal", "screech", "cry", "yelp", "chirp",
                "vocal", "vocalization", "howl", "roar", "growl",
            ),
        ),
        "requiredFilenameGroups": ((
            "killed", "death", "die",
        ),),
        "minimumDuration": 0.18,
        "idealDuration": 1.1,
        "maximumDuration": 1.8,
        "hardMaximumDuration": 3.5,
    },
    "event.forest.ambience.loop": {
        "positive": ("forest", "wind", "leaves", "insects", "nature", "loop", "ambience"),
        "negative": ("birds", "bird", "thunder", "haunted", "creepy", "impulse response"),
        "hardExclude": ("bird", "birds", "birdsong", "songbird"),
    },
    "event.forest.ambience.point.0": {
        "positive": (
            "forest", "nature", "insect", "cricket", "chirp", "click",
            "twig", "branch", "leaf", "leaves", "rustle", "flutter",
            "creature", "frog", "owl", "magic", "magical", "enchanted",
            "sparkle", "distant", "subtle", "short", "one shot",
        ),
        "negative": (
            "continuous", "dense", "wind", "rain", "thunder", "river",
            "water", "crow", "birdsong", "flock", "ui", "pickup",
            "collect", "game point", "monster", "pain", "attack", "zombie",
            "cloth", "medical", "metal", "fireball", "weapon", "combat",
        ),
        "hardExclude": (
            "loop", "music", "user interface", "ui_", "pickup", "collect",
            "game point", "multi point", "impulse response", "monster",
            "pain", "attack", "zombie", "undead", "cloth", "fabric",
            "medical", "bandage", "health", "metal", "metallic", "fireball",
            "fire ball", "spit fire", "weapon", "gun", "projectile",
            "explosion", "explode", "combat", "battle", "scream", "death",
            "hurt", "grunt", "growl", "roar", "howl", "banshee", "ghost",
            "squish", "gore", "blood", "shot", "impact", "punch", "buff",
            "power up", "interface", "stinger", "transition", "foley",
        ),
        "requiredGroups": ((
            "forest", "nature", "insect", "cricket", "chirp", "twig",
            "branch", "leaf", "leaves", "rustle", "flutter", "creature",
            "frog", "owl", "magic", "magical", "enchanted", "sparkle",
        ),),
        "idealDuration": 2.5,
        "maximumDuration": 6.0,
    },
    "event.forest.music.exploration": {
        "positive": (
            "game", "music", "forest", "exploration", "adventure", "loop",
            "restrained", "light", "retro", "arcade", "electronic", "synth",
            "chiptune", "8bit", "pixel", "rhythmic",
        ),
        "negative": (
            "combat", "battle", "boss", "trailer", "cinematic", "intense",
            "victory", "defeat", "stinger", "orchestral", "epic", "medieval",
        ),
        "hardExclude": ("sound effect", "sfx", "ambience", "dialog", "voice", "vocal"),
        "requiredGroups": (("music", "track", "theme", "loop", "score"),),
    },
    "event.forest.music.combat": {
        "positive": (
            "game", "music", "forest", "combat", "battle", "rhythmic", "loop",
            "urgent", "action", "retro", "arcade", "electronic", "synth",
            "chiptune", "8bit", "pixel", "drums", "percussion",
        ),
        "negative": (
            "exploration", "ambient", "sleep", "calm", "victory", "defeat",
            "stinger", "trailer", "cinematic", "orchestral", "epic", "slow",
        ),
        "hardExclude": ("sound effect", "sfx", "ambience", "dialog", "voice", "vocal"),
        "requiredGroups": (("music", "track", "theme", "loop", "score"),),
    },
    "event.forest.music.victory": {
        "positive": ("game", "music", "victory", "win", "success", "complete", "positive", "stinger", "short"),
        "negative": ("defeat", "lose", "failure", "dark", "sad", "loop", "ambient", "long"),
        "hardExclude": ("ambience", "dialog", "voice", "vocal", "user interface", "ui achievement", "designed"),
        "requiredGroups": (("music", "musical"), ("stinger", "jingle", "fanfare", "victory", "win", "success", "complete")),
        "idealDuration": 8.0,
        "maximumDuration": 20.0,
    },
    "event.forest.music.defeat": {
        "positive": ("game", "music", "defeat", "lose", "failure", "negative", "dark", "stinger", "short"),
        "negative": ("victory", "win", "success", "positive", "loop", "ambient", "long"),
        "hardExclude": ("ambience", "dialog", "voice", "vocal", "user interface", "ui achievement", "designed"),
        "requiredGroups": (("music", "musical"), ("stinger", "jingle", "fanfare", "defeat", "lose", "loss", "failure")),
        "idealDuration": 8.0,
        "maximumDuration": 20.0,
    },
}

FIELDS = (
    "Filename",
    "Description",
    "Keywords",
    "Category",
    "SubCategory",
    "ShowCategory",
    "ShowSubCategory",
    "Library",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--requirements",
        type=Path,
        default=Path("Assets/FPGDemo/Audio/ForestAudioRequirements.csv"),
    )
    parser.add_argument("--work-root", type=Path, required=True)
    parser.add_argument("--audio-root", type=Path, default=DEFAULT_AUDIO_ROOT)
    parser.add_argument("--database", type=Path, action="append")
    parser.add_argument("--all-events", action="store_true")
    parser.add_argument("--event-id", action="append")
    parser.add_argument("--limit", type=int, default=8)
    parser.add_argument("--batch", default="")
    return parser.parse_args()


def parse_duration(value: object) -> float:
    if value is None:
        return 0.0
    text = str(value).strip()
    match = re.fullmatch(r"(?:(\d+):)?(\d+):(\d+)(?:\.(\d+))?", text)
    if not match:
        return 0.0
    hours = int(match.group(1) or 0)
    minutes = int(match.group(2))
    seconds = int(match.group(3))
    fraction = float(f"0.{match.group(4)}") if match.group(4) else 0.0
    return hours * 3600.0 + minutes * 60.0 + seconds + fraction


def normalize_text(value: object) -> str:
    return re.sub(r"\s+", " ", str(value or "")).strip()


def normalize_search_text(value: object) -> str:
    text = normalize_text(value).lower()
    return re.sub(r"[^a-z0-9]+", " ", text).strip()


def contains_term(text: str, term: str) -> bool:
    normalized_term = normalize_search_text(term)
    if not normalized_term:
        return False
    return f" {normalized_term} " in f" {text} "


def tokenize_query(query: str) -> list[str]:
    tokens = re.findall(r"[a-z0-9]+", query.lower())
    return [token for token in tokens if len(token) >= 3]


def resolve_soundminer_path(value: object, audio_root: Path) -> Path:
    raw = normalize_text(value).replace("\\", "/")
    if not raw:
        return Path()
    candidate = Path(raw)
    if candidate.exists():
        return candidate

    parts = [part for part in raw.split("/") if part]
    if len(parts) >= 4 and parts[0].lower() == "volumes":
        # Soundminer stores macOS-style paths as /Volumes/<volume>/Audio/...
        return audio_root / Path(*parts[3:])
    return audio_root / Path(*parts) if not candidate.is_absolute() else candidate


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_requirements(
    path: Path,
    all_events: bool,
    event_ids: list[str] | None,
) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        rows = list(csv.DictReader(stream))
    if event_ids:
        requested = set(event_ids)
        selected = [row for row in rows if row.get("stableEventId") in requested]
        missing = requested.difference(row["stableEventId"] for row in selected)
        if missing:
            raise ValueError(
                "Unknown --event-id value(s): " + ", ".join(sorted(missing))
            )
        return selected
    if all_events:
        return rows
    return [row for row in rows if row.get("stableEventId") in PILOT_EVENT_IDS]


def query_database(path: Path) -> Iterable[dict[str, object]]:
    uri = f"file:{path.as_posix()}?mode=ro"
    connection = sqlite3.connect(uri, uri=True)
    connection.row_factory = sqlite3.Row
    columns = [
        "recid",
        "Filename",
        "Description",
        "Keywords",
        "Category",
        "SubCategory",
        "ShowCategory",
        "ShowSubCategory",
        "Library",
        "Duration",
        "Channels",
        "SampleRate",
        "BitDepth",
        "FilePath",
        "_AudioCRC",
    ]
    sql = "select " + ", ".join(f'"{column}"' for column in columns)
    sql += ' from "justinmetadata"'
    try:
        for row in connection.execute(sql):
            yield dict(row)
    finally:
        connection.close()


def candidate_score(
    row: dict[str, object],
    tokens: list[str],
    expected: str,
    event_id: str,
) -> tuple[float, list[str]]:
    text = normalize_search_text(
        " ".join(normalize_text(row.get(field)) for field in FIELDS)
    )
    filename = normalize_search_text(row.get("Filename"))
    score = 0.0
    reasons: list[str] = []
    for token in tokens:
        if contains_term(filename, token):
            score += 4.0
            reasons.append(f"filename:{token}")
        elif contains_term(text, token):
            score += 1.5
            reasons.append(f"metadata:{token}")

    tuning = EVENT_TUNING.get(event_id, {})
    excluded_recids = tuning.get("excludeRecids", ())
    try:
        recid = int(row.get("recid") or 0)
    except (TypeError, ValueError):
        recid = 0
    if recid in excluded_recids:
        return -1000.0, ["hard-exclude:recid"]
    for token in tuning.get("hardExclude", ()):
        if contains_term(text, token):
            return -1000.0, [f"hard-exclude:{token}"]
    for group_index, group in enumerate(tuning.get("requiredFilenameGroups", ())):
        matched = next(
            (token for token in group if contains_term(filename, token)),
            None,
        )
        if matched is None:
            return -1000.0, [f"missing-required-filename-group:{group_index}"]
        reasons.append(f"filename-required:{matched}")
    for group_index, group in enumerate(tuning.get("requiredGroups", ())):
        matched = next(
            (token for token in group if contains_term(text, token)),
            None,
        )
        if matched is None:
            return -1000.0, [f"missing-required-group:{group_index}"]
        reasons.append(f"required:{matched}")
    for token in tuning.get("positive", ()):
        if contains_term(text, token):
            score += 2.0
            reasons.append(f"intent:{token}")
    for token in tuning.get("negative", ()):
        if contains_term(text, token):
            score -= 4.0
            reasons.append(f"exclude:{token}")

    channels = int(row.get("Channels") or 0)
    sample_rate = int(row.get("SampleRate") or 0)
    duration = parse_duration(row.get("Duration"))
    minimum_duration = float(tuning.get("minimumDuration", 0.0))
    hard_maximum_duration = float(tuning.get("hardMaximumDuration", 0.0))
    if minimum_duration > 0.0 and duration < minimum_duration:
        return -1000.0, ["hard-exclude:too-short"]
    if hard_maximum_duration > 0.0 and duration > hard_maximum_duration:
        return -1000.0, ["hard-exclude:too-long"]
    ideal_duration = float(tuning.get("idealDuration", 0.0))
    maximum_duration = float(tuning.get("maximumDuration", 0.0))
    if ideal_duration > 0.0 and 0.0 < duration <= ideal_duration:
        score += 6.0
        reasons.append("ideal-duration")
    elif maximum_duration > 0.0 and duration > maximum_duration:
        score -= 8.0
        reasons.append("duration-penalty")
    if expected == "stereo":
        if channels == 2:
            score += 3.0
            reasons.append("stereo")
        elif channels > 2:
            score -= 8.0
            reasons.append("multichannel-penalty")
        if duration >= 20.0:
            score += 2.0
            reasons.append("loop-length")
    else:
        if channels == 1:
            score += 3.0
            reasons.append("mono")
        elif channels > 2:
            score -= 8.0
            reasons.append("multichannel-penalty")
        if duration <= 3.0:
            score += 2.0
            reasons.append("short-tail")
        elif duration > 10.0:
            score -= 2.0
            reasons.append("long-tail-penalty")

    if sample_rate == 48000:
        score += 1.5
        reasons.append("48khz")
    elif sample_rate not in (0, 96000):
        score -= 0.5

    return score, reasons


def write_csv(path: Path, rows: list[dict[str, object]], fieldnames: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def main() -> int:
    args = parse_args()
    args.work_root.mkdir(parents=True, exist_ok=True)
    for folder in ("index", "candidates", "edits", "approved", "manifests", "reports"):
        (args.work_root / folder).mkdir(exist_ok=True)

    requirements = load_requirements(
        args.requirements,
        args.all_events,
        args.event_id,
    )
    databases = tuple(args.database) if args.database else DEFAULT_DATABASES
    database_rows: list[tuple[str, dict[str, object]]] = []
    for database in databases:
        if not database.exists():
            raise FileNotFoundError(f"Soundminer database not found: {database}")
        database_rows.extend((database.stem, row) for row in query_database(database))

    candidates: list[dict[str, object]] = []
    for requirement in requirements:
        event_id = requirement["stableEventId"]
        expected = (
            "stereo"
            if requirement["module"] in ("Environment", "Music")
            else "mono"
        )
        tokens = tokenize_query(requirement.get("soundminerQuery", ""))
        ranked: list[tuple[float, str, dict[str, object], list[str], Path]] = []
        for database_name, row in database_rows:
            score, reasons = candidate_score(row, tokens, expected, event_id)
            resolved = resolve_soundminer_path(row.get("FilePath"), args.audio_root)
            if not resolved.exists():
                score -= 20.0
                reasons.append("missing-file")
            if score > 0:
                ranked.append((score, database_name, row, reasons, resolved))

        ranked.sort(key=lambda item: (-item[0], item[1], normalize_text(item[2].get("Filename"))))
        selected: list[dict[str, object]] = []
        seen: set[str] = set()
        for score, database_name, row, reasons, resolved in ranked:
            dedupe = normalize_text(row.get("_AudioCRC")) or str(resolved).lower()
            if dedupe in seen:
                continue
            seen.add(dedupe)
            source_hash = sha256_file(resolved) if resolved.exists() else ""
            selected.append(
                {
                    "stableEventId": event_id,
                    "database": database_name,
                    "recid": row.get("recid", ""),
                    "filename": normalize_text(row.get("Filename")),
                    "description": normalize_text(row.get("Description")),
                    "keywords": normalize_text(row.get("Keywords")),
                    "category": normalize_text(row.get("Category")),
                    "subCategory": normalize_text(row.get("SubCategory")),
                    "library": normalize_text(row.get("Library")),
                    "durationSeconds": f"{parse_duration(row.get('Duration')):.3f}",
                    "channels": row.get("Channels", ""),
                    "sampleRate": row.get("SampleRate", ""),
                    "bitDepth": row.get("BitDepth", ""),
                    "soundminerFilePath": normalize_text(row.get("FilePath")),
                    "resolvedPath": str(resolved),
                    "audioCrc": normalize_text(row.get("_AudioCRC")),
                    "sourceSha256": source_hash,
                    "exists": str(resolved.exists()).lower(),
                    "score": f"{score:.2f}",
                    "reasons": ";".join(reasons),
                    "candidateRank": len(selected) + 1,
                }
            )
            if len(selected) >= args.limit:
                break
        candidates.extend(selected)

    fields = [
        "stableEventId", "candidateRank", "database", "recid", "filename",
        "description", "keywords", "category", "subCategory", "library",
        "durationSeconds", "channels", "sampleRate", "bitDepth",
        "soundminerFilePath", "resolvedPath", "audioCrc", "sourceSha256",
        "exists", "score", "reasons",
    ]
    if args.batch and not re.fullmatch(r"[A-Za-z0-9_-]+", args.batch):
        raise ValueError("--batch may only contain letters, numbers, underscores and hyphens")
    suffix = f"_{args.batch}" if args.batch else ""
    output = args.work_root / "index" / f"forest_candidate_shortlist{suffix}.csv"
    write_csv(output, candidates, fields)
    manifest = {
        "requirements": str(args.requirements),
        "audioRoot": str(args.audio_root),
        "databases": [str(path) for path in databases],
        "events": [row["stableEventId"] for row in requirements],
        "candidateCount": len(candidates),
        "maxCandidatesPerEvent": args.limit,
        "readOnly": True,
        "output": str(output),
    }
    manifest_path = args.work_root / "index" / f"forest_index_manifest{suffix}.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(manifest, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
