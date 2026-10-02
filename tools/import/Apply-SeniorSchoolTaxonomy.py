#!/usr/bin/env python3
"""Remap E93491564 (senior school) assets onto the consolidated school taxonomy."""

from __future__ import annotations

import argparse
from collections import defaultdict
from dataclasses import dataclass

import pyodbc

ORG_ID = 11
ORG_SLUG = "e93491564"
CONNECTION = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=.\\SQLEXPRESS;"
    "DATABASE=AssetManagementModuleDb;"
    "Trusted_Connection=yes;"
    "TrustServerCertificate=yes;"
)

TaxonomyTriple = tuple[str, str, str]


@dataclass(frozen=True)
class TaxonomyTarget:
    category: str
    asset_type: str
    sub_type: str

    def as_tuple(self) -> TaxonomyTriple:
        return (self.category, self.asset_type, self.sub_type)


def t(category: str, asset_type: str, sub_type: str) -> TaxonomyTarget:
    return TaxonomyTarget(category, asset_type, sub_type)


# Explicit remap: (old category, old type, old sub-type name) -> new taxonomy
EXPLICIT_REMAP: dict[tuple[str, str, str], TaxonomyTarget] = {
    # Furniture — desks
    ("Furniture", "Desks", "Students desks"): t("Furniture", "Desks", "Student desk (oval white)"),
    ("Furniture", "Desks", "Teacher's desk"): t("Furniture", "Desks", "Teacher desk"),
    ("Furniture", "Desks", "Teachers Desk"): t("Furniture", "Desks", "Teacher desk"),
    ("Furniture", "Desks", "Office desks"): t("Furniture", "Desks", "Office desk"),
    # Furniture — chairs
    ("Furniture", "Chairs", "student's blue chairs"): t("Furniture", "Chairs", "Student chair"),
    ("Furniture", "Chairs", "Students chairs"): t("Furniture", "Chairs", "Student chair"),
    ("Furniture", "Chairs", "Blue plastic chair"): t("Furniture", "Chairs", "Student chair"),
    ("Furniture", "Chairs", "Teacher's chair"): t("Furniture", "Chairs", "Teacher chair"),
    ("Furniture", "Chairs", "Teacher's chairs"): t("Furniture", "Chairs", "Teacher chair"),
    ("Furniture", "Chairs", "Trs. Chair"): t("Furniture", "Chairs", "Teacher chair"),
    ("Furniture", "Chairs", "office chairs"): t("Furniture", "Chairs", "Office chair"),
    ("Furniture", "Chairs", "Black chairs"): t("Furniture", "Chairs", "Office chair"),
    ("Furniture", "Chairs", "Wooden chairs"): t("Furniture", "Chairs", "Office chair"),
    ("Furniture", "Chairs", "Wooden chairs (PIANO)"): t("Furniture", "Chairs", "Office chair"),
    ("Furniture", "Chairs", "visitor's chair"): t("Furniture", "Chairs", "Visitor chair"),
    ("Furniture", "Chairs", "Visitors chair"): t("Furniture", "Chairs", "Visitor chair"),
    ("Furniture", "Chairs", "visitors chairs"): t("Furniture", "Chairs", "Visitor chair"),
    ("Furniture", "Chairs", "Black foldable chair"): t("Furniture", "Chairs", "Foldable chair"),
    ("Furniture", "Chairs", "Black folderable chair"): t("Furniture", "Chairs", "Foldable chair"),
    ("Furniture", "Chairs", "Black folderble chair"): t("Furniture", "Chairs", "Foldable chair"),
    ("Furniture", "Chairs", "Plastic Stools"): t("Furniture", "Stools", "Plastic stool"),
    ("Furniture", "Chairs", "Stool"): t("Furniture", "Stools", "Plastic stool"),
    ("Furniture", "Chairs", "Glass stools"): t("Furniture", "Stools", "Wooden stool"),
    ("Furniture", "Chairs", "Wooden stools"): t("Furniture", "Stools", "Wooden stool"),
    # Furniture — tables
    ("Furniture", "Tables", "long lunch benches"): t("Furniture", "Tables", "Lunch bench"),
    ("Furniture", "Tables", "long wooden  benches"): t("Furniture", "Tables", "Lunch bench"),
    ("Furniture", "Tables", "Green plastic bench"): t("Furniture", "Tables", "Lunch bench"),
    ("Furniture", "Tables", "Green plastic benches"): t("Furniture", "Tables", "Lunch bench"),
    ("Furniture", "Tables", "Concrete benches"): t("Furniture", "Tables", "Lunch bench"),
    ("Furniture", "Tables", "Waiting bench"): t("Furniture", "Tables", "Lunch bench"),
    ("Furniture", "Tables", "Long lunch table"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "Long table"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "Long wooden table"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "Long wooden tables"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "White table"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "WHite tables"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "White wooden table"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "Big White wooden table"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "Rectangle white table"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "Oval Table"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "Round table"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "Rounded table"): t("Furniture", "Tables", "Lunch table"),
    ("Furniture", "Tables", "Teachers table"): t("Furniture", "Tables", "Teacher table"),
    ("Furniture", "Tables", "Teacher's Table"): t("Furniture", "Tables", "Teacher table"),
    ("Furniture", "Tables", "Teacher's Tables"): t("Furniture", "Tables", "Teacher table"),
    ("Furniture", "Tables", "Student Tables"): t("Furniture", "Tables", "Student table"),
    ("Furniture", "Tables", "Student's tables"): t("Furniture", "Tables", "Student table"),
    ("Furniture", "Tables", "Students brown table"): t("Furniture", "Tables", "Student table"),
    ("Furniture", "Tables", "Office table"): t("Furniture", "Tables", "Office table"),
    ("Furniture", "Tables", "Office tables"): t("Furniture", "Tables", "Office table"),
    ("Furniture", "Tables", "Brown table"): t("Furniture", "Tables", "Office table"),
    ("Furniture", "Tables", "Brown Tables"): t("Furniture", "Tables", "Office table"),
    ("Furniture", "Tables", "coffee table"): t("Furniture", "Tables", "Office table"),
    ("Furniture", "Tables", "Glass table"): t("Furniture", "Tables", "Office table"),
    ("Furniture", "Tables", "Wooden dinning tables"): t("Furniture", "Tables", "Office table"),
    ("Furniture", "Tables", "Green picnic table"): t("Furniture", "Tables", "Outdoor picnic table"),
    ("Furniture", "Tables", "Green Wooden picnic tables"): t("Furniture", "Tables", "Outdoor picnic table"),
    # Furniture — storage
    ("Furniture", "Storage", "Metal cabinet"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "metal cabinets"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "Metal cabinet storage"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "Long metal cabinets"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "Metallic cabinet"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "Metal drawer cabinet"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "Movable cabinet"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "wooden cabinet"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "Cabinet"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "Cabinet Drawer"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "Wooden cupboard"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Storage", "Book shelf"): t("Furniture", "Storage", "Bookshelf"),
    ("Furniture", "Storage", "Wooden bookshelf"): t("Furniture", "Storage", "Bookshelf"),
    ("Furniture", "Storage", "wooden books shelves"): t("Furniture", "Storage", "Bookshelf"),
    ("Furniture", "Storage", "wooden shelf"): t("Furniture", "Storage", "Bookshelf"),
    ("Furniture", "Storage", "Mounted wooden shelves"): t("Furniture", "Storage", "Bookshelf"),
    ("Furniture", "Storage", "Wooden mounted shelf"): t("Furniture", "Storage", "Bookshelf"),
    ("Furniture", "Storage", "Glass shelf"): t("Furniture", "Storage", "Bookshelf"),
    ("Furniture", "Storage", "Mounted shelf"): t("Furniture", "Storage", "Bookshelf"),
    ("Furniture", "Storage", "Metal shelf"): t("Furniture", "Storage", "Bookshelf"),
    ("Furniture", "Storage", "Mounted wooden cabinets"): t("Furniture", "Storage", "Bookshelf"),
    # Furniture — boards
    ("Furniture", "Boards", "White board"): t("Furniture", "Boards", "Whiteboard"),
    ("Furniture", "Boards", "Movable white board"): t("Furniture", "Boards", "Movable whiteboard"),
    # Furniture — misc type redistribution
    ("Furniture", "Furniture", "Computer"): t("IT Equipment", "Desktops", "Desktop computer"),
    ("Furniture", "Furniture", "Dell Computer"): t("IT Equipment", "Desktops", "Desktop computer"),
    ("Furniture", "Furniture", "Laptop"): t("IT Equipment", "Laptops", "Laptop"),
    ("Furniture", "Furniture", "Telephone"): t("Office Equipment", "Telephones", "Desk phone"),
    ("Furniture", "Furniture", "Television Screen"): t("AV Equipment", "Displays", "TV screen"),
    ("Furniture", "Furniture", "Tv Screen"): t("AV Equipment", "Displays", "TV screen"),
    ("Furniture", "Furniture", "Tv stand"): t("Furniture", "Furnishings", "Trolley"),
    ("Furniture", "Furniture", "Fridge"): t("Facilities Equipment", "Building", "Refrigerator"),
    ("Furniture", "Furniture", "Meko Gas"): t("Facilities Equipment", "Building", "Gas cooker"),
    ("Furniture", "Furniture", "Umbrella Sheds"): t("Facilities Equipment", "Outdoor", "Umbrella shed"),
    ("Furniture", "Furniture", "Sofa set"): t("Furniture", "Furnishings", "Sofa set"),
    ("Furniture", "Furniture", "Glass podium"): t("Furniture", "Furnishings", "Podium"),
    ("Furniture", "Furniture", "Trolley"): t("Furniture", "Furnishings", "Trolley"),
    ("Furniture", "Furniture", "Clock"): t("Furniture", "Furnishings", "Clock"),
    ("Furniture", "Furniture", "Squared clock"): t("Furniture", "Furnishings", "Clock"),
    ("Furniture", "Furniture", "Movable drawer"): t("Furniture", "Storage", "Metal cabinet"),
    ("Furniture", "Furniture", "Metal cabintes"): t("Furniture", "Storage", "Metal cabinet"),
    # IT consolidation
    ("IT Equipment", "Desktops", "Comp"): t("IT Equipment", "Desktops", "Desktop computer"),
    ("IT Equipment", "Desktops", "Computer"): t("IT Equipment", "Desktops", "Desktop computer"),
    ("IT Equipment", "Desktops", "Computers"): t("IT Equipment", "Desktops", "Desktop computer"),
    ("IT Equipment", "Desktops", "Dell Computer"): t("IT Equipment", "Desktops", "Desktop computer"),
    ("IT Equipment", "Desktops", "Teacher's computer"): t("IT Equipment", "Desktops", "Desktop computer"),
    ("IT Equipment", "Desktops", "Teachers chair"): t("Furniture", "Chairs", "Teacher chair"),
    ("IT Equipment", "Desktops", "Electric cookers"): t("Facilities Equipment", "Building", "Gas cooker"),
    ("IT Equipment", "Laptops", "Dell Laptop"): t("IT Equipment", "Laptops", "Laptop"),
    ("IT Equipment", "Printers", "Printer"): t("IT Equipment", "Printers", "Printer"),
    ("IT Equipment", "Printers", "Printer  RICO"): t("IT Equipment", "Printers", "Printer"),
    ("IT Equipment", "Printers", "Printer HP"): t("IT Equipment", "Printers", "Printer"),
    ("IT Equipment", "Printers", "Printer/ scanner"): t("IT Equipment", "Printers", "Printer"),
    ("IT Equipment", "Printers", "Printer/Photocopier (Ricoh)"): t("IT Equipment", "Printers", "Photocopier"),
    ("Office Equipment", "Photo copier/printer", "Photocopier"): t("IT Equipment", "Printers", "Photocopier"),
    ("Office Equipment", "Telephones", "Telephone"): t("Office Equipment", "Telephones", "Desk phone"),
    ("Office Equipment", "Telephones", "Samsung SMT P2100 Telephone"): t("Office Equipment", "Telephones", "Desk phone"),
    ("Facilities Equipment", "Climate", "Laminating machine"): t("Office Equipment", "Office machines", "Laminating machine"),
    ("Facilities Equipment", "Climate", "iMac 21''"): t("IT Equipment", "Desktops", "Desktop computer"),
    ("Facilities Equipment", "Climate", 'iMac 21" M1 2021'): t("IT Equipment", "Desktops", "Desktop computer"),
    ("Facilities Equipment", "Climate", "iMac 24'' M3 2023"): t("IT Equipment", "Desktops", "Desktop computer"),
    # AV audio items reassigned
    ("AV Equipment", "Audio", "Violin"): t("Music Equipment", "Orchestral strings", "Violin"),
    ("AV Equipment", "Audio", "Magic Keyboard"): t("IT Equipment", "Peripherals", "Keyboard"),
    ("AV Equipment", "Audio", "Korg PA 300 Keyboard"): t("Music Equipment", "Keyboards", "Portable keyboard"),
    ("AV Equipment", "Audio", "Novation Launchkey 61 MIDI Keyboard"): t("Music Equipment", "Keyboards", "MIDI keyboard"),
    ("AV Equipment", "Audio", "Samson Carbon 61 Keyboard"): t("Music Equipment", "Keyboards", "MIDI keyboard"),
    ("AV Equipment", "Audio", "Yamaha PSR - 453 keyboard"): t("Music Equipment", "Keyboards", "Portable keyboard"),
    ("AV Equipment", "Audio", "Keyboard Stands"): t("Music Equipment", "Accessories", "Music stand"),
    ("AV Equipment", "Audio", "Alto TS315 Speaker"): t("AV Equipment", "Speakers", "PA speaker"),
    ("AV Equipment", "Audio", "Db Technologies F15 Digital Active speaker"): t("AV Equipment", "Speakers", "PA speaker"),
    ("AV Equipment", "Audio", "Laney LX12 Solid-state AMP"): t("AV Equipment", "Speakers", "Amplifier"),
    ("AV Equipment", "Audio", "Laney RB1 Richter Bass AMP"): t("AV Equipment", "Speakers", "Amplifier"),
    ("AV Equipment", "Audio", "MARC AUDIO MA-2140 Advanced System Amplifier"): t("AV Equipment", "Speakers", "Amplifier"),
    ("AV Equipment", "Displays", "Microphone Screen"): t("AV Equipment", "Cables & accessories", "Pop filter"),
    ("AV Equipment", "Displays", "preSonus Studio Monitor (Left)"): t("AV Equipment", "Displays", "Studio monitor"),
    ("AV Equipment", "Displays", "PreSonus Studio Monitor (Right)"): t("AV Equipment", "Displays", "Studio monitor"),
    ("AV Equipment", "Displays", "Tv Screen"): t("AV Equipment", "Displays", "TV screen"),
    ("AV Equipment", "Displays", "Vision+ TV"): t("AV Equipment", "Displays", "TV screen"),
    ("AV Equipment", "Displays", "Yamaha HS8 Studio Monitor(left)"): t("AV Equipment", "Displays", "Studio monitor"),
    ("AV Equipment", "Displays", "yamaha HS8 Studio Monitor(right)1 unit"): t("AV Equipment", "Displays", "Studio monitor"),
    # Facilities mislabels
    ("Facilities Equipment", "Climate", "Chard C43 Semi Acoustic Guitar"): t("Music Equipment", "Guitars & bass", "Acoustic guitar"),
    ("Facilities Equipment", "Climate", "Yamaha FX310A  Acoustic Guitar"): t("Music Equipment", "Guitars & bass", "Acoustic guitar"),
    ("Facilities Equipment", "Climate", "Yamaha FX310A Semi Acoustic Guitar"): t("Music Equipment", "Guitars & bass", "Acoustic guitar"),
    ("Facilities Equipment", "Climate", "DI-ONE Outrack Passive Direct Box Bass"): t("AV Equipment", "Recording", "Direct box"),
    ("Facilities Equipment", "Climate", "DI-ONE Outrack Passive Direct Box Guitar"): t("AV Equipment", "Recording", "Direct box"),
    ("Facilities Equipment", "Climate", "DI-ONE Outrack Passive Direct Box Keys 1"): t("AV Equipment", "Recording", "Direct box"),
    ("Facilities Equipment", "Climate", "DI-ONE Outrack Passive Direct Box Keys 2"): t("AV Equipment", "Recording", "Direct box"),
    ("Facilities Equipment", "Climate", "DI-ONE Outrack Passive Direct Box P.B/EL"): t("AV Equipment", "Recording", "Direct box"),
    ("Facilities Equipment", "Climate", "Out Rack T26 Digital Signal Processor"): t("AV Equipment", "Mixers & processors", "Signal processor"),
    ("Facilities Equipment", "Climate", "Fan heater"): t("Facilities Equipment", "HVAC", "Fan heater"),
    ("Facilities Equipment", "Climate", "Premier Fan"): t("Facilities Equipment", "HVAC", "Fan heater"),
    ("Facilities Equipment", "Climate", "Samsung AC"): t("Facilities Equipment", "HVAC", "Air conditioner"),
    ("Facilities Equipment", "Climate", "Yamaha PA-150 AC Adaptor"): t("Facilities Equipment", "Building", "Battery"),
    # Office telephones mislabels
    ("Office Equipment", "Telephones", "EIKON CM602 Drum microphone"): t("AV Equipment", "Microphones", "Drum microphone"),
    ("Office Equipment", "Telephones", "EIKON DM1 Drum microphone"): t("AV Equipment", "Microphones", "Drum microphone"),
    ("Office Equipment", "Telephones", "EIKON DM12 Drum microphone"): t("AV Equipment", "Microphones", "Drum microphone"),
    ("Office Equipment", "Telephones", "Mcrophone stands"): t("AV Equipment", "Cables & accessories", "Mic stand"),
    ("Office Equipment", "Telephones", "Microphone Stands"): t("AV Equipment", "Cables & accessories", "Mic stand"),
    ("Office Equipment", "Telephones", "PROEL HPAMP106 Headphone AMP"): t("AV Equipment", "Recording", "Headphone amp"),
    ("Office Equipment", "Telephones", "Rode M1 Microphone"): t("AV Equipment", "Microphones", "Wired microphone"),
    ("Office Equipment", "Telephones", "Saxophone - Alto"): t("Music Equipment", "Brass & woodwind", "Saxophone"),
    ("Office Equipment", "Telephones", "Saxophone - Soprano"): t("Music Equipment", "Brass & woodwind", "Saxophone"),
    ("Office Equipment", "Telephones", "Saxophone - Tenor"): t("Music Equipment", "Brass & woodwind", "Saxophone"),
    ("Office Equipment", "Telephones", "Sennheiser e845 Microphone"): t("AV Equipment", "Microphones", "Wired microphone"),
    ("Office Equipment", "Telephones", "SHURE BLX 1 Wireless Microphone A1"): t("AV Equipment", "Microphones", "Wireless microphone"),
    ("Office Equipment", "Telephones", "SHURE BLX 1 Wireless Microphone B2"): t("AV Equipment", "Microphones", "Wireless microphone"),
    ("Office Equipment", "Telephones", "SHURE BLX 1 Wireless Microphone C3"): t("AV Equipment", "Microphones", "Wireless microphone"),
    ("Office Equipment", "Telephones", "SHURE BLX 1 Wireless Microphone D4"): t("AV Equipment", "Microphones", "Wireless microphone"),
    ("Office Equipment", "Telephones", "SHURE BLX 1 Wireless Microphone E5"): t("AV Equipment", "Microphones", "Wireless microphone"),
    ("Office Equipment", "Telephones", "SHURE BLX 1 Wireless Microphone F6"): t("AV Equipment", "Microphones", "Wireless microphone"),
    ("Office Equipment", "Telephones", "Shure SM58 Microphone"): t("AV Equipment", "Microphones", "Wired microphone"),
    ("Office Equipment", "Telephones", "Sontronics STC-3x Microphone"): t("AV Equipment", "Microphones", "Wired microphone"),
    ("Equipment", "General", "Behringer V-Tone GM 108"): t("AV Equipment", "Speakers", "Amplifier"),
    ("Equipment", "General", "Behringer V-Tone GM 109"): t("AV Equipment", "Speakers", "Amplifier"),
    ("Equipment", "General", "Jinbao Hi- Hat"): t("Music Equipment", "Percussion", "Drum kit component"),
    ("Equipment", "General", "Paiste 16'' Cras 101 Brass"): t("Music Equipment", "Percussion", "Cymbals"),
    ("Equipment", "General", "Yamaha Onstage Sustain"): t("Music Equipment", "Accessories", "Sustain pedal"),
}


def normalize_key(value: str) -> str:
    text = (value or "").strip()
    text = text.replace("\u2019", "'").replace("\ufffd", "'")
    return text


def classify_by_name(name: str, old_category: str, old_type: str) -> TaxonomyTarget:
    text = normalize_key(name).lower()
    old_cat = normalize_key(old_category)
    old_typ = normalize_key(old_type)

    # Keep already-correct furniture leaves
    if old_cat == "Furniture":
        keep = {
            ("Desks", "Student desk (oval white)"),
            ("Desks", "Student desk (rectangle brown)"),
            ("Desks", "Student desk (long)"),
            ("Desks", "Computer desk"),
            ("Desks", "Office desk"),
            ("Chairs", "Student chair"),
            ("Chairs", "Office chair"),
            ("Chairs", "Visitor chair"),
            ("Stools", "Plastic stool"),
            ("Tables", "Student table"),
            ("Storage", "Blue metal cabinet"),
            ("Storage", "Bag shelf"),
            ("Boards", "Soft board"),
        }
        if (old_typ, normalize_key(name)) in keep:
            return t(old_cat, old_typ, normalize_key(name))

    # AV cables
    if "xlr" in text:
        return t("AV Equipment", "Cables & accessories", "XLR cable")
    if "dmx" in text or "airstream" in text:
        return t("AV Equipment", "Cables & accessories", "DMX splitter")
    if "rca" in text:
        return t("AV Equipment", "Cables & accessories", "RCA cable")
    if "1/4" in text or "quarter inch" in text:
        return t("AV Equipment", "Cables & accessories", "Quarter-inch cable")
    if "mic stand" in text or "microphone stand" in text or "mcrophone stand" in text:
        return t("AV Equipment", "Cables & accessories", "Mic stand")
    if "pop filter" in text or "microphone screen" in text:
        return t("AV Equipment", "Cables & accessories", "Pop filter")

    # AV displays
    if "tv" in text or "vision+" in text and "remote" not in text:
        return t("AV Equipment", "Displays", "TV screen")
    if "studio monitor" in text or "hs8" in text:
        return t("AV Equipment", "Displays", "Studio monitor")
    if "novastar" in text or "led display" in text:
        return t("AV Equipment", "Displays", "LED video controller")

    # AV microphones / wireless
    if "wireless microphone" in text or "wireless receiver" in text or "xs wireless" in text:
        return t("AV Equipment", "Microphones", "Wireless microphone")
    if "drum microphone" in text or "dm1" in text or "dm12" in text or "cm602" in text:
        return t("AV Equipment", "Microphones", "Drum microphone")
    if "microphone" in text or "sm58" in text or "sm57" in text or "xs 1 wired" in text:
        return t("AV Equipment", "Microphones", "Wired microphone")

    # AV speakers / amps
    if "speaker" in text and "mixer" not in text:
        return t("AV Equipment", "Speakers", "PA speaker")
    if "amplifier" in text or " amp" in text or text.endswith("amp"):
        return t("AV Equipment", "Speakers", "Amplifier")

    # AV mixers / recording
    if "mixer" in text or "mg16" in text or "studiolive" in text:
        return t("AV Equipment", "Mixers & processors", "Audio mixer")
    if "processor" in text or "x2222" in text:
        return t("AV Equipment", "Mixers & processors", "Signal processor")
    if "dj controller" in text or "ddj" in text:
        return t("AV Equipment", "Mixers & processors", "DJ controller")
    if "scarlett" in text or "focusrite" in text:
        return t("AV Equipment", "Recording", "Audio interface")
    if "headphone amp" in text or "hpamp" in text:
        return t("AV Equipment", "Recording", "Headphone amp")
    if "direct box" in text or "outrack passive" in text:
        return t("AV Equipment", "Recording", "Direct box")

    # Music — keyboards
    if "piano" in text or "dgx" in text:
        return t("Music Equipment", "Keyboards", "Digital piano")
    if "midi keyboard" in text or "launchkey" in text:
        return t("Music Equipment", "Keyboards", "MIDI keyboard")
    if "keyboard" in text or "psr" in text:
        return t("Music Equipment", "Keyboards", "Portable keyboard")

    # Music — strings / brass
    if "violin" in text:
        return t("Music Equipment", "Orchestral strings", "Violin")
    if "cello" in text:
        return t("Music Equipment", "Orchestral strings", "Cello")
    if "trumpet" in text:
        return t("Music Equipment", "Brass & woodwind", "Trumpet")
    if "trombone" in text:
        return t("Music Equipment", "Brass & woodwind", "Trombone")
    if "saxophone" in text:
        return t("Music Equipment", "Brass & woodwind", "Saxophone")

    # Music — guitars
    if "bass" in text and ("guitar" in text or "ibanez" in text or "behringer" in text):
        return t("Music Equipment", "Guitars & bass", "Bass guitar")
    if "bass" in text and "amp" in text:
        return t("AV Equipment", "Speakers", "Amplifier")
    if "guitar" in text:
        if "classical" in text or "acoustic" in text or "fx310" in text or "semi acoustic" in text:
            return t("Music Equipment", "Guitars & bass", "Acoustic guitar")
        return t("Music Equipment", "Guitars & bass", "Electric guitar")

    # Music — percussion
    if any(x in text for x in ("cymbal", "crash", "ride", "hi-hat", "hi- hat", "hit-hat", "snare", "kick", "tom", "drum throne", "paiste")):
        if "throne" in text:
            return t("Music Equipment", "Accessories", "Drum throne")
        if "cymbal" in text or "crash" in text or "ride" in text:
            return t("Music Equipment", "Percussion", "Cymbals")
        return t("Music Equipment", "Percussion", "Drum kit component")
    if any(x in text for x in ("conga", "djembe", "cajon", "chimes", "kayamba", "shaker", "tamborine", "african drum")):
        return t("Music Equipment", "Percussion", "Hand percussion")
    if "bow" in text:
        return t("Music Equipment", "Accessories", "Bow")
    if "stand" in text and old_typ.lower() in ("audio", "general"):
        return t("Music Equipment", "Accessories", "Music stand")

    # Facilities
    if "scoreboard" in text:
        return t("Sports & Events", "Sports", "Scoreboard controller")
    if any(x in text for x in ("a/c", "ac remote", "samsung ac", "gree a/c", "fan heater", "premier fan")):
        if "remote" in text:
            return t("Facilities Equipment", "HVAC", "AC remote")
        if "fan" in text:
            return t("Facilities Equipment", "HVAC", "Fan heater")
        return t("Facilities Equipment", "HVAC", "Air conditioner")
    if "curtain" in text or "drapery" in text:
        return t("Facilities Equipment", "Building", "Curtain controller")
    if "power sequencer" in text:
        return t("Facilities Equipment", "Building", "Power sequencer")
    if "battery" in text or "energizer" in text or "piscell" in text or "charger" in text:
        return t("Facilities Equipment", "Building", "Battery")
    if "baofeng" in text or "transceiver" in text:
        return t("Facilities Equipment", "Building", "Two-way radio")
    if "kettle power plug" in text or "adaptor" in text:
        return t("Facilities Equipment", "Building", "Battery")
    if "vision+ remote" in text:
        return t("Facilities Equipment", "Building", "AC remote")

    # IT peripherals
    if "ipad" in text:
        return t("IT Equipment", "Tablets", "Tablet")
    if "magic mouse" in text or text == "magic mouse":
        return t("IT Equipment", "Peripherals", "Mouse")
    if "magic keyboard" in text:
        return t("IT Equipment", "Peripherals", "Keyboard")
    if "audio technica m" in text:
        return t("AV Equipment", "Recording", "Headphone amp")

    # Audio technica headphones -> recording
    if "audio technica" in text:
        return t("AV Equipment", "Recording", "Headphone amp")
    if "v-tone" in text:
        return t("AV Equipment", "Speakers", "Amplifier")
    if "sustain" in text:
        return t("Music Equipment", "Accessories", "Sustain pedal")

    # Default: preserve category/type with cleaned sub-type name
    return t(old_cat, old_typ, normalize_key(name))


def resolve_target(old_category: str, old_type: str, old_sub_type: str) -> TaxonomyTarget:
    key = (
        normalize_key(old_category),
        normalize_key(old_type),
        normalize_key(old_sub_type),
    )
    explicit = EXPLICIT_REMAP.get(key)
    if explicit:
        return explicit
    return classify_by_name(old_sub_type, old_category, old_type)


def connect():
    drivers = [d for d in pyodbc.drivers() if "SQL Server" in d]
    if not drivers:
        raise RuntimeError("No SQL Server ODBC driver found.")
    conn_str = CONNECTION.replace("ODBC Driver 17 for SQL Server", drivers[-1])
    if "TrustServerCertificate" not in conn_str:
        conn_str += ";TrustServerCertificate=yes"
    return pyodbc.connect(conn_str, autocommit=False)


def fetch_assets(cursor, org_id: int):
    cursor.execute(
        """
        SELECT a.Id, c.Name, t.Name, st.Name, a.AssetName
        FROM Asset a
        JOIN AssetSubType st ON st.Id = a.AssetSubTypeId
        JOIN AssetType t ON t.Id = st.AssetTypeId
        JOIN AssetCategory c ON c.Id = t.AssetCategoryId
        WHERE a.OrganizationId = ?
        """,
        org_id,
    )
    return cursor.fetchall()


def ensure_taxonomy(cursor, org_id: int, cache: dict, target: TaxonomyTarget) -> tuple[int, int, int]:
    triple = target.as_tuple()
    if triple in cache:
        return cache[triple]

    cursor.execute(
        "SELECT Id FROM AssetCategory WHERE OrganizationId = ? AND Name = ?",
        org_id,
        target.category,
    )
    row = cursor.fetchone()
    if row:
        category_id = row[0]
    else:
        cursor.execute(
            """
            INSERT INTO AssetCategory (OrganizationId, Name, Description, CreatedAt, IsActive)
            VALUES (?, ?, '', GETUTCDATE(), 1)
            """,
            org_id,
            target.category,
        )
        cursor.execute("SELECT @@IDENTITY")
        category_id = int(cursor.fetchone()[0])

    cursor.execute(
        """
        SELECT Id FROM AssetType
        WHERE OrganizationId = ? AND AssetCategoryId = ? AND Name = ?
        """,
        org_id,
        category_id,
        target.asset_type,
    )
    row = cursor.fetchone()
    if row:
        type_id = row[0]
    else:
        cursor.execute(
            """
            INSERT INTO AssetType (OrganizationId, AssetCategoryId, Name, Description, CreatedAt, IsActive)
            VALUES (?, ?, ?, '', GETUTCDATE(), 1)
            """,
            org_id,
            category_id,
            target.asset_type,
        )
        cursor.execute("SELECT @@IDENTITY")
        type_id = int(cursor.fetchone()[0])

    cursor.execute(
        """
        SELECT Id FROM AssetSubType
        WHERE OrganizationId = ? AND AssetTypeId = ? AND Name = ?
          AND Brand = '' AND Model = ? AND IsActive = 1
        """,
        org_id,
        type_id,
        target.sub_type,
        target.sub_type,
    )
    row = cursor.fetchone()
    if row:
        sub_type_id = row[0]
    else:
        cursor.execute(
            """
            INSERT INTO AssetSubType (
                OrganizationId, AssetTypeId, Name, Brand, Model, CreatedAt, IsActive
            )
            VALUES (?, ?, ?, '', ?, GETUTCDATE(), 1)
            """,
            org_id,
            type_id,
            target.sub_type,
            target.sub_type,
        )
        cursor.execute("SELECT @@IDENTITY")
        sub_type_id = int(cursor.fetchone()[0])

    cache[triple] = (category_id, type_id, sub_type_id)
    return cache[triple]


def delete_orphan_taxonomy(cursor, org_id: int):
    cursor.execute(
        """
        DELETE st
        FROM AssetSubType st
        WHERE st.OrganizationId = ?
          AND NOT EXISTS (SELECT 1 FROM Asset a WHERE a.AssetSubTypeId = st.Id);

        DELETE t
        FROM AssetType t
        WHERE t.OrganizationId = ?
          AND NOT EXISTS (SELECT 1 FROM AssetSubType st WHERE st.AssetTypeId = t.Id);

        DELETE c
        FROM AssetCategory c
        WHERE c.OrganizationId = ?
          AND NOT EXISTS (SELECT 1 FROM AssetType t WHERE t.AssetCategoryId = c.Id);
        """,
        org_id,
        org_id,
        org_id,
    )


def apply_remap(org_id: int, dry_run: bool) -> None:
    conn = connect()
    cursor = conn.cursor()
    cache: dict[TaxonomyTriple, tuple[int, int, int]] = {}
    stats = defaultdict(int)
    unmapped_examples: list[str] = []

    assets = fetch_assets(cursor, org_id)
    print(f"Loaded {len(assets)} assets for org {org_id}")

    for asset_id, old_cat, old_type, old_sub, _asset_name in assets:
        target = resolve_target(old_cat, old_type, old_sub)
        stats[target.as_tuple()] += 1
        category_id, type_id, sub_type_id = ensure_taxonomy(cursor, org_id, cache, target)

        if not dry_run:
            cursor.execute(
                """
                UPDATE Asset
                SET CategoryId = ?, AssetTypeId = ?, AssetSubTypeId = ?,
                    AssetName = ?, UpdatedAt = GETUTCDATE()
                WHERE Id = ?
                """,
                category_id,
                type_id,
                sub_type_id,
                target.sub_type,
                asset_id,
            )

    if dry_run:
        conn.rollback()
        print("\nDry run — no changes committed.")
    else:
        delete_orphan_taxonomy(cursor, org_id)
        conn.commit()
        print("\nRemap committed.")

    print(f"Distinct target sub-types: {len(stats)}")
    print(f"Categories in target: {len({k[0] for k in stats})}")
    print(f"Types in target: {len({(k[0], k[1]) for k in stats})}")

    by_category = defaultdict(int)
    for (cat, _typ, _sub), count in stats.items():
        by_category[cat] += count
    print("\nAssets per category:")
    for cat in sorted(by_category):
        print(f"  {cat}: {by_category[cat]}")

    if unmapped_examples:
        print("\nUnmapped examples:")
        for line in unmapped_examples[:20]:
            print(f"  {line}")

    conn.close()


def report_targets(org_id: int) -> None:
    conn = connect()
    cursor = conn.cursor()
    stats = defaultdict(int)
    for _asset_id, old_cat, old_type, old_sub, _asset_name in fetch_assets(cursor, org_id):
        target = resolve_target(old_cat, old_type, old_sub)
        stats[target.as_tuple()] += 1
    conn.close()

    print(f"Distinct target sub-types: {len(stats)}")
    for (cat, typ, sub), count in sorted(stats.items(), key=lambda item: (-item[1], item[0][0], item[0][1], item[0][2])):
        print(f"{count:4d}  {cat} > {typ} > {sub}")


def main():
    parser = argparse.ArgumentParser(description="Apply senior school taxonomy remap.")
    parser.add_argument("--org-id", type=int, default=ORG_ID)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--report", action="store_true", help="Print target taxonomy distribution and exit.")
    args = parser.parse_args()
    if args.report:
        report_targets(args.org_id)
        return
    apply_remap(args.org_id, args.dry_run)


if __name__ == "__main__":
    main()
