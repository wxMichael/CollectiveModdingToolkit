# detect plugin merge/rename/compact mistakes. plugin-named-folders with mismatched name/not in masters
# Header versions, hardcoded Form IDs in scripts and MCM, hardcoded plugin names in folders like voice lines/facegen/previs, etc

# TODO: bp42s: PDB checks. Buffout
# TODO: Startup speed (threading?)
# TODO: Warn if complex sorter detected. maybe detect xedit version
# Scanner: Detect NULL form IDs
# TODO: cancel on_row_select if already selected
# TODO: complex sorter check that record types are in the same file when necessary
# TODO: check that CK logging is enabled (suggestions)
# TODO: Detect outdated buffout
# TODO: get_info_modules VerifyFiles: auto-fix: prompt and trigger steam verify?
# TODO: get_info_modules VerifyFiles: mark all problems as solved if using an auto-fix that applies to all like verify. update to say rescan
# Settings: clear update status & recheck for update on source change
# Scanner: note that SADD records are common in weapon mods. may be another culprit(s)
# Settings: Save downgrader options
# TODO: Report DDS with no mips
--------------------

CMT v0.5 Release

--------------------
# Overview: GPU detection for dual GPU Intel/nvidia
# Settings: update check frequency
# Settings: Alternate colors: good: dodger blue. bad: orange. warning: yellow?
# Settings: Always show versions on Overview

# DDSChecks = ("DDS Checks", TOOLTIP_SCAN_DDS)
# BA2Content = ("BA2 Contents", TOOLTIP_SCAN_BA2)
# ModConflicts = ("Mod Conflicts", TOOLTIP_SCAN_CONFLICTS)
# Suggestions = ("Suggestions", TOOLTIP_SCAN_SUGGEST)


# Scanner: two-three x-cell installs
# Scanner: F4SE issues
# TODO: i should add notes for specific detections like facegen tga

# Scanner: Ignore <ProblemType> for <Mod>
# Scanner: Whitelist <EXT> for <Folder>
# Scanner: Checkbox to disable ignores for scan
# Scanner: Count for ignored issues
# Settings: Manage per-mod ignores
# Settings: Manage per-folder whitelists
# TODO: HITMEs

# TODO: Detect blacklisted mo2 python plugin?
# Overview: Implement MO2 settings.ini
# Overview: Detect game store/source
# Tools: Disable downgrader for non-steam
# TODO: Disable F4SE and related features for MS & Epic Versions
# TODO: Linux Support
# TODO: Detect Fallout 4 VR
# TODO: Fallout 4 VR Support
# TODO: Main app loading screen
# Scanner: Cancel scan button

# Scanner: Source files like PSD as separate detection

# TODO: Hash/sizecheck all files
# TODO: Make generic: self.button_files = ttk.Button



# TODO: check for running xedit/CK/game before running patcher/etc
# Scanner: File List button padding
# TODO: Make casing match users files instead of lowercase (patcher)
# TODO: Button to move all source files to Source folder or delete
# Scanner: Detect vortex files in mod stage
# TODO: check if INIs even exist
# TODO: tmr settings check hfpf
# TODO: download bsarch
# GUI: Clickable space to the right of update notification
# TODO: improve nexus/github setup instructions, add images
# TODO: section = "NO-SECTION"
# TODO: handle counting BA2s if plugins.txt not found
# TODO: get_stage_paths(): make sure stage is scanned in proper order, overwrite winning
# Overview: Find mod source. Scan stage Data/-level files just for esp/ba2 and popular dict in deploy order so last wins
# Scanner: F4SE CRCs
# F4SE: Scan in separate thread
# TODO: get_stage_paths(): Warn if manager.overwrite_path doesn't exist
# Overview: Implement MO2 Root Builder
# GUI: Dark UI for messagebox.show*
# TODO: get_info_binaries(): self.ckfixes_found = self.cmc.game.game_path.joinpath("F4CKFixes").exists()
# Overview: Tooltip for NG+steam loader
# Overview: Tooltip and Warning icon for over limits
# Overview: Refresh should refresh INIs
# Overview: trigger F4SE scan and add warning symbol


# Scanner: Timer

# Scanner: Detect BA2s with no plugin
# Overview: Warning icons like car dash
# TODO: If no mod manager detected, prompt to run via manager
# TODO: Archive patcher: revert to original versions
# GUI: Global loading bar shared across tabs
# TODO: Test Texture BA2 limits
# TODO: Check if Magic.DDS should be b"DDS\x00"
# TODO: Patcher shows only enabled loading archives. Add note or change to all. checkbox?
# Scanner: damanding: Remove F4SE files from BA2
# Scanner: Fallout4.exe compatibility settings for tmr with one click fix
# TODO: Handle all raise/exceptions gracefully
# Tools: Archive patcher: specify folder to patch
# Tools: Archive Patcher: Allow choosing v7
# Tools: Archive Patcher: Detect and patch BA2 v7 to v8 (?) What is v7 for?
# Scanner: Double-click tree item to open file location
# Scanner: Indicate if file is hidden
# Scanner: unhide file
# TODO: censor usernames
# BUG: Support non-MO2 for manager detection details
# TODO: restore backup on downgrade failure
# TODO: Link to TMR
# TODO: Casefold instead of lower
# Overview: List popup for error counts or tooltips to direct to scanner/overview issues
# Scanner: speed estimate meter based on selected options
# TODO: check for meta.ini?

# TODO: Offer to add CMT as executable to mod manager? button to do so?
# TODO: C:\Users\Michael\Downloads\Creation Kit
# TODO: C:\Users\Michael\Downloads\CMT-Temp
# TODO: C:\Users\Michael\Desktop\mo2 backup\profiles\Default
# TODO: C:\Users\Michael\Desktop\Apps\Games\Bethesda\MO2\FO4\profiles\Default
# TODO: C:\Users\Michael\Desktop\Apps\Games\Bethesda\MO2\FO4
# TODO: C:\Users\Michael\AppData\Local\ModOrganizer\FO4

# TODO: Catch OSErrors for all open()
# TODO: Validation for CKPE and PJM setups
# TODO: Open folders button a la mo2
# Overview: detect the ML fork of MO2 VER_FILEVERSION_STR
# TODO: Detect windows scaling and set application controlled DPI awareness
# TODO: Consolidate duplicate checks for self.cmc.game.manager in build_tab_overview
# TODO: suggest bethini pie for all/most INI issues
# Overview: game limits info window
# Overview: Perchik: Support multiple/renamed CK exes
# TODO: load_game_inis: Replace with ini_file code?
# TODO: load_game_inis: Warn on invalid language/ValueError. suggest bethini pie
# Tools: innr editor. use export script. automate script?
# Scanner: Make sure right click to copy URL works for others (and not middle click)
# TODO: Launch arguments to go straight to patcher? specify version and filter?
# TODO: Commandline support to patch a folder to a version?
# Scanner: Make only the link clickable. Separate Label.
# Scanner: Variation in text colors. Maybe numbers a different color? parse for characters for tags?
today I learn that even raising the quality of my headphone can potentially cause crash in fo4. thank you, Todd. you're such a blessing in game industry history <:Pain:975849030112378890>

if a file had 253 masters and they all had the maximum number of new records.  and your file had overrides of every record from every master.
4,244,635,648 is the max overrides

BobTheBobCatt — 6/27/2023 10:59 PM
Fallout 4 still has the 2,048 esl/espfe record limit, right? 
Robert [SSE/FO4/SF] — 6/27/2023 11:00 PM
no they fixed that to 4096
if the file has header version 1.0

# Scanner: Solution: WrongVersion






# Scanner: Report known wrong archive/module versions from other games

# TODO: Select relevant file/folder when opening explorer paths
# Scanner: TMR Option
# Scanner: TMR: Detect improper version (NG, pure OG, deleted CC)
# Scanner: Abstract code by setting
# Scanner: Auto-fix detected issues
# Scanner: evildarkarchon: Clean vortex blank/useless files (Only if MO2 Detected)
# Scanner: Snail icon for slow options
# TODO: Detect/Count dummy plugins
# TODO: Detect/Count dummy plugins with no BA2
# TODO: Fallout4 - Nvflex.ba2
# TODO: Replace tab loader with thread handler?
# F4SE: Long Loading Times Fix: https://www.nexusmods.com/fallout4/mods/73469
# F4SE: Mod Configuration Menu: https://www.nexusmods.com/fallout4/mods/21497
# F4SE: PrivateProfileRedirector: https://www.nexusmods.com/fallout4/mods/33947
# F4SE: Improve NG+OG detection. longloadingtimes highfpsfix. check all antonix
# TODO: load_game_inis: Handle docs path not found
# TODO: load_game_inis: Handle no INIs found
# Scanner: Icon/underline/signifier of clickable path. gree/underlined links? dotted UL?
# Overview: present MO2-style launch selector for executables. launch via mo2 shortcuts
# TODO: test disabling modding platform effect on loaded esp/ba2
# Overview: Check steam language setting for get_info_archives?
# Overview: Check gog registry language setting for get_info_archives?
# TODO: F5/ctrl+r to refresh current tab, maybe R
# TODO: Get rid of all reliance on side effects
# Tools: MCM Config Editor
# Tools: https://github.com/fo76utils/fo76utils
# GUI: Tooltip shows as window taskbar on Windows 10
# Overview: Reveal versions while holding shift; screenshot aid?
--------------------

CMT v0.4 Release

--------------------
# TODO: Check if plugins.txt/loadorder.txt/modlist.txt comment doesnt match mod manager
# Scanner: Consistent ordering
# TODO: Option to delete INIs from documents/MO2 profile
# TODO: loose facegen with vefs (if packed) and other pitfalls
# TODO: Move delta patches into a subfolder?
# TODO: Make Tk a base for CMCChecker (?)
# Tools: Downgrader: Test handling of wrong version files like 980
# TODO: Vortex Support
# Overview: Detect game install via Vortex
# TODO: Wrye Bash Support
# TODO: Other mod manager/tool support
# TODO: Warn against Room Bounds, Fixer, etc
# TODO: About text and copy button for stderr window
# TODO: Move Treeview style
# GUI: Scale bd with DPI
# Optimize update_geometry. use width/height globals, maybe single configure bind for x/y?
https://discord.com/channels/1135527122849497149/1308809910695231531
# Conflicts: Wall_SoGB: tldr, use 128x128 cubemaps of the same format and it should be fine
# TODO: Arrow keys to switch tabs
# GUI: Center patcher on parent instead of screen
# F4SE: Detect misplaced DLLs
# Errors: Detect doubled folder structures; Data/Data, Textures/Textures
# TODO: Ungeziefi: if OG exe PluginPreloader should be IpHlpAPI.dll. if NG it should be WinHTTP.dll
# Scanner: DoubleYou: Check material file format (JSON/Binary/FO4/FO76)
# TODO: CyberDanz: Toggle read-only for Steam's appmanifest
# TODO: Compare to TMR load order
--------------------

CMT v0.5 Release

--------------------
# TODO: Open source licenses in About
# TODO: LICENSE file for repo
# Conflicts: Detect F4SE file overrides
# Conflicts: Detect missing BASS on OG/DG if NG archives present. Suggestion if none (?)
# Errors: Detect Root files in Data
# Conflicts: Detect game saves/files/documents in OneDrive. detect cloud-only files.
# TODO: Option to move unmanaged plugin+BA2s to mod manager. option to zip and move to downloads instead?
--------------------

CMT v0.6 Release

--------------------
# Tools: File Inspector
# TODO: Ungeziefi: if you detect an OG exe you should put an exclamation mark next to 4096/4350 saying that you'll run out of handles before that because MSVC 2012 is limited to 2048. only NG can do 8192
--------------------

CMT v0.7 Release

--------------------
# TODO: Python extension pack
# GUI: (?) load_tab_overview after mainloop starts
# GUI: Fix flashbang if game dir not found
# GUI: Translation Support
# GUI: PROCESS_PER_MONITOR_DPI_AWARE
# GUI: Dock location setting for DetailsPane
# GUI: Accessibility: Alternate font setting
# F4SE: Retry building tab if data/f4se folder previously missing
# Overview: Detect CKFixes; ckfixesFound
# Overview: Check Mod Manager versions
# Overview: Detect xEdit via Vortex
# TODO: Reference Handle Limit: Skyrim 2^20 or 1048576; FO4+ 2^21 or 2097152
# Suggestions: Buffout memory manager disabled + no alternative memory manager = very suspicious.
# Suggestions: Read-only flag: check all files; modules, ba2, exe/dll/dg, ini/toml/json/etc
# Tools: Unpack all BA2s. juggle output/already-loose files to preserve
# Tools: Archive Patcher: Show BA2 counts
# Tools: Papyrus Script Compiler
# Tools: Refresh only overview archives when patching
# Tools: xEdit 0.95 Patcher (?)
# Tools: Auto-backup for CK/xEdit. timer-based and on-save. maybe tail log files to see saving. check file in-use
# Tools: Save auto-backup/cleanup/archive
# Tools: INI editor. CKPE/etc
# Tools: Complex Sorter rules editor
# Errors: Detect files with mismatched extension
# Errors: Implement MO2 "cache_directory",
# Errors: Implement MO2 "download_directory",
# Errors: Implement MO2 "profile_local_inis",
# Errors: Implement MO2 "profile_local_saves",
# Errors: INI Syntax checks; check for setting before sections
# TODO: Ungeziefi: you should detect any language mismatch between archives (aka what steam has) and ini otherwise the game hangs on load
# TODO: Ungeziefi: all you should do is use buffout 4 ng for the maxops and if you play at higher than 60 fps make the budgetms dynamic with high fps fix
# TODO: bp42s: r9 7950x 16core most likely has an issue with CCD and starting a new game. 5900x, same story cause its 2 clusters of 6
# TODO: DoubleYou: Warn to remove FOLIP - Before Generation.esp after generating LOD.
# TODO: Ungeziefi/Lucifer: Suggest using the same drive for MO2 and display an error for vortex if hardlinking
# TODO: steam_appid.txt
# TODO: Logs tab? F4SE/CK/Papyrus/etc
# TODO: FO4Edit_log.txt
# TODO: FO4Script_log.txt
--------------------

CMT v0.8 Release

--------------------

download_directory=%BASE_DIR%/downloads
cache_directory=%BASE_DIR%/webcache
mod_directory=%BASE_DIR%/mods
overwrite_directory=%BASE_DIR%/overwrite
profiles_directory=%BASE_DIR%/profiles
base_directory=C:/Mod Organizer 2/.jacked.M2o

    Let's say you want the game menus, dialogues etc. to be in english and the Voices in German, then you have to do the following.

    First of all you'll need to get the Voices.ba2 of the Base game + all the DLC's in English and German. In our example we wanted to make the ingame text to be english, so we'll want to rename the Fallout4 - Voices.ba2 to Fallout4 - Voices_en.ba2 if it isn't like that already. Change all the DLC's Voices_en.ba2 tp Voices.ba2 if this isn't like that already aswell. Your English Files should look like that: Fallout4 - Voices_en.ba2 DLCworkshop03 - Voices.ba2 DLCRobot - Voices.ba2 DLCNukaWorld - Voices.ba2 DLCCoast - Voices.ba2

    Now we want our audio to be in the language we want, in this case it's german For the Vanilla games Voices you'll want to rename it from Fallout4 - Voices_de.ba2 to Fallout4 - Voices.ba2 For the DLC's you'll want to change from Voices_de.ba2 to Voices_en.ba2 DLCworkshop03 - Voices_en.ba2 DLCRobot - Voices_en.ba2 DLCNukaWorld - Voices_en.ba2 DLCCoast - Voices_en.ba2

    And lastly you'll want to change the language to english in your Fallout4.ini which is located under C:\Users\YOURNAME\Documents\My Games\Fallout4. [General] sLanguage=en


Bez — Today at 9:38 AM
Fallen at the final step 😅 everything was working on my test save with PRP and the other new additions from TMR, but I've tried to start a new game and it's now giving me an infinite loading screen before the opening video. I've seen a few people online have had this issue but can't find a solution that works - does anyone have any suggestions as to where I could start?

Ungeziefi — Today at 9:49 AM
infinite or long
if its infinite check that steam has the game language set to english 
if its long try enable the animation removal in high fps fix, or disabling xcell

Ungeziefi — Today at 10:03 AM
re-enable animations, run with the thread setting set to 2
or disable the thread setting in xcell

Ungeziefi — Today at 10:11 AM
the thread setting is high fps fix locking new game loading to 1 thread by default to supposedly fix a bug
but it shouldnt even exist and slows things down
so try remove disabling the thread feature in xcell, or increase the thread count in high fps fix
or set it the count 0 to disable the feature, atleast thats what it's supposed to do according to the code

Ungeziefi — Today at 10:13 AM
perchik has a commit in xcell to undo this stupid mess with threads
dont think its released yet
Bez — Today at 10:14 AM
Cool, so just to double check - High FPS Fix ini, edit NumOfThreadsWhileLoadingNewGame to = 2


# TODO: Prompt to update registry key
# "CM Toolkit will now ask you to locate Fallout4.exe.\n"
# "Do you want the registry to be updated with the location you specify?\n"
# f"Key: {response}\n"
# "Current Value:"



		self.ckng = BooleanVar(value=True)
		self.check_keep_ng_ck = ttk.Checkbutton(
			frame_radio_desired,
			text="Keep NG CK",
			variable=self.ckng,
			padding=(5, 0),
		)
		self.check_keep_ng_ck.grid(column=0, row=2, sticky=NSEW, pady=0)
		ToolTip(self.check_keep_ng_ck, TOOLTIP_DOWNGRADER_NG_CK)

TOOLTIP_DOWNGRADER_NG_CK = """Requires CKPE.
Normally, Creation Kit requires steam_api64.dll to match its version, so it must be downgraded as well.
This will patch it to replace that dependency with CKPE's winhttp.dll."""


			if not self.bv_keep_backups.get():
				for path in {(self.cmc.game.game_path / f).parent for f in self.current_versions}:
					for backup in path.glob("*gradeBackup.*"):
						backup.unlink()

			if self.bv_delete_deltas.get():
				for xdelta in Path.cwd().glob("*.xdelta"):
					xdelta.unlink()







			self.logger.log_message(LogType.Info, "Done")




# TODO: Check for settings that are mismatched or interfere with ENB? Detect ENB stuff?
Bez — Today at 1:52 PM
I'm an idiot - figured it out. At some point, I'd ticked the box to disable the vanilla dof. that was why there was no blur in the enb, but there was in vanilla

# Suggestions: Ungeziefi: Detect Special K for FO4
Ungeziefi — Today at 1:54 PM
SK isn't a good idea on FO4 anywa
SK isn't a good idea on FO4 anyway, iirc it breaks the pipboy input handling
the only big benefits it'd give you are:
- Flip model (done by w11 or high fps fix)
- Best limiter (you can get a good one through RTSS)
- Window management and misc (high fps fix)
so might as well remove it

# Suggestions: Detect GPU(s) and suggest GPU-specific fixes/tools/utilities/tweaks
Weapon Debris Crash Fix (nvidia only?)
https://www.nexusmods.com/fallout4/mods/48078
Fixes crashes caused by Weapon Debris setting




Starhammer — Today at 12:42 PM
The tome of PRP states: "HeavyFlamer-CR in NG is only there for save preservation, in OG, it still has value for CR purposes."


# Scanner: Option "CK Crashers", scan modules for inv count issue or special character in MGEF Name (%)





ffmpeg -i vid.mp4 -vf "fps=15,scale=400:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse" vid.gif



ffmpeg -ss 25 -t 5 -i vid.mp4 -vf "fps=15,scale=400:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse" vid.gif


Ungeziefi — Today at 2:10 PM
pipboy fps limit to 60, should be a default










		label_gnrl_icon = ttk.Label(
			self.frame_info_archives,
			compound="image",
			image=self.cmc.get_image("images/info-16.png"),
		)
		label_gnrl_icon.grid(column=3, row=0, sticky=W, padx=(0, 5), ipady=3)
		ToolTip(label_gnrl_icon, "Detection details")
		label_gnrl_icon.bind(
			"<Button-1>",
			lambda _: AboutWindow(
				self.cmc.root,
				self.cmc,
				750,
				750,
				"Detected General Archives",
				(
					f"Total: {len(self.cmc.game.archives_gnrl)}\n"
					f"{'\n'.join(sorted([f'⚠️ {a.name} ⚠️' if 'Textures' in a.name else a.name for a in self.cmc.game.archives_gnrl]))}"
				),
			),
		)

		label_dx10_icon = ttk.Label(
			self.frame_info_archives,
			compound="image",
			image=self.cmc.get_image("images/info-16.png"),
		)
		label_dx10_icon.grid(column=3, row=1, sticky=W, padx=(0, 5), ipady=3)
		ToolTip(label_dx10_icon, "Detection details")
		label_dx10_icon.bind(
			"<Button-1>",
			lambda _: AboutWindow(
				self.cmc.root,
				self.cmc,
				750,
				750,
				"Detected Texture Archives",
				(
					f"Total: {len(self.cmc.game.archives_dx10)}\n"
					f"{'\n'.join(sorted([a.name for a in self.cmc.game.archives_dx10]))}"
				),
			),
		)




















[tool.coverage.run]
source = ["."]

[tool.pytest.ini_options]
addopts = "--cov --cov-report=lcov:lcov.info --cov-report=term"



mod_manager_info.py
# if "gamePath" not in self.mo2_settings:
# 	msg = "gamePath is not set in ModOrganizer.ini."
# 	raise ValueError(msg)


class Tab(StrEnum):
	Overview = "Overview"
	F4SE = "F4SE"
	Scanner = "Scanner"
	Tools = "Tools"
	Settings = "Settings"
	About = "About"


class ProblemType(IntEnum):
	JunkFile = auto()
	# EmptyFolder = auto()
	UnexpectedFormat = auto()
	MisplacedDLL = auto()
	LoosePrevis = auto()
	AnimTextDataFolder = auto()
	# WrongF4SEVersion = auto()
	# WrongCKVersion = auto()

class SolutionType(Enum):
	ArchiveOrDelete = auto()
	DeleteFile = auto()
	# DeleteFileList = auto()
	DeleteFolder = auto()
	DeleteOrIgnoreFile = auto()
	# DeleteFolderList = auto()


class ProcessDPIAwareness(IntEnum):
	PROCESS_DPI_UNAWARE = 0
	PROCESS_SYSTEM_DPI_AWARE = 1
	PROCESS_PER_MONITOR_DPI_AWARE = 2




























import os
import sys
import winreg
from pathlib import Path
from tkinter import *
from tkinter import filedialog, messagebox
from typing import TYPE_CHECKING, Literal

from enums import InstallType, Language
from utils import (
	find_mod_manager,
	get_registry_value,
	is_fo4_dir,
)

if TYPE_CHECKING:
	from helpers import FileInfo


class GameInfo:
	def __init__(self, install_type_sv: StringVar, game_path_sv: StringVar) -> None:
		self._install_type_sv = install_type_sv
		self._game_path_sv = game_path_sv
		self.name: Literal["Fallout4"]
		self.install_type = InstallType.Unknown
		self._game_path: Path
		self._data_path: Path | None = None
		self._f4se_path: Path | None = None
		self.archives_gnrl: set[Path] = set()
		self.archives_dx10: set[Path] = set()
		self.archives_og: set[Path] = set()
		self.archives_ng: set[Path] = set()
		self.archives_enabled: set[Path] = set()
		self.archives_unreadable: set[Path] = set()
		self.modules_unreadable: set[Path] = set()
		self.modules_v95: set[Path] = set()
		self.modules_enabled: list[Path] = []
		self.file_info: dict[str, FileInfo] = {}
		self.address_library: Path | None = None
		self.ckfixes_found = False
		self.game_settings: dict[str, dict[str, str]] = {}
		self.game_prefs: dict[str, dict[str, str]] = {}
		self.language = Language.English

		self.ba2_count_gnrl = 0
		self.ba2_count_dx10 = 0
		self.module_count_full = 0
		self.module_count_light = 0
		self.module_count_v1 = 0
		self.manager = find_mod_manager()
		self.find_path()
		self.load_game_inis()

	def load_game_inis(self) -> None:
		# TODO: Replace with ini_file code?
		docs_path = Path.home() / R"Documents\My Games\Fallout4"
		section = "NO-SECTION"
		for name in ("Fallout4.ini", "Fallout4Prefs.ini", "Fallout4Custom.ini"):
			ini_path = docs_path / name
			if not ini_path.is_file():
				continue
			ini_dict = self.game_prefs if name == "Fallout4Prefs.ini" else self.game_settings
			# TODO: catch OSErrors
			for line in ini_path.read_text().splitlines():
				if line.startswith("[") and line.endswith("]"):
					section = line[1:-1].lower()
					if section not in ini_dict:
						ini_dict[section] = {}
					continue
				try:
					setting, value = line.split("=", 1)
				except ValueError:
					continue
				ini_dict[section][setting.lower()] = value

		try:
			self.language = Language(self.game_settings.get("general", {}).get("slanguage", "en").lower())
		except ValueError:
			self.language = Language.English
			# TODO: Warn on invalid language. suggest bethini pie
		if self.language == Language.English:
			self.ba2_suffixes: tuple[str, ...] = ("main", "textures", "voices_en")
		else:
			self.ba2_suffixes = ("main", "textures", "voices_en", f"voices_{self.language}")

	def reset_binaries(self) -> None:
		self.install_type = InstallType.Unknown
		self.file_info.clear()
		self.address_library = None
		self.ckfixes_found = False

	def reset_modules(self) -> None:
		self.module_count_full = 0
		self.module_count_light = 0
		self.module_count_v1 = 0
		self.modules_v95.clear()
		self.modules_enabled.clear()
		self.modules_unreadable.clear()

	def reset_archives(self) -> None:
		self.ba2_count_gnrl = 0
		self.ba2_count_dx10 = 0
		self.archives_gnrl.clear()
		self.archives_dx10.clear()
		self.archives_og.clear()
		self.archives_ng.clear()
		self.archives_enabled.clear()
		self.archives_unreadable.clear()

	@property
	def game_path(self) -> Path:
		return self._game_path

	@game_path.setter
	def game_path(self, value: Path) -> None:
		self._game_path = value
		self._game_path_sv.set(str(value))

		data_path = value / "Data"
		if data_path.is_dir():
			self._data_path = data_path
			f4se_path = data_path / "F4SE/Plugins"
			self._f4se_path = f4se_path if f4se_path.is_dir() else None
		else:
			self._data_path = None
			self._f4se_path = None

	@property
	def data_path(self) -> Path | None:
		return self._data_path

	@property
	def f4se_path(self) -> Path | None:
		return self._f4se_path

	@property
	def install_type(self) -> InstallType:
		return self._install_type

	@install_type.setter
	def install_type(self, value: InstallType) -> None:
		self._install_type = value
		self._install_type_sv.set(str(value))

	def find_path(self) -> None:
		if self.manager is not None:
			if self.manager.name == "Mod Organizer":
				portable_ini_path = self.manager.exe_path.parent / "ModOrganizer.ini"
				portable_ini_exists = portable_ini_path.is_file()

				portable_txt_path = self.manager.exe_path.parent / "portable.txt"
				if portable_txt_path.is_file():
					if not portable_ini_exists:
						msg = "portable.txt found but no ModOrganizer.ini found in MO2 install path"
						raise FileNotFoundError(msg)
					self.manager.read_mo2_ini(portable_ini_path)
					self.manager.portable = True
					self.manager.portable_txt_path = portable_txt_path

				else:
					current_instance = get_registry_value(
						winreg.HKEY_CURRENT_USER,
						R"Software\Mod Organizer Team\Mod Organizer",
						"CurrentInstance",
					)
					if current_instance:
						appdata_local = os.getenv("LOCALAPPDATA")
						if appdata_local:
							appdata_ini_path = Path(appdata_local) / "ModOrganizer" / current_instance / "ModOrganizer.ini"
							if appdata_ini_path.is_file():
								self.manager.read_mo2_ini(appdata_ini_path)

				if not self.manager.game_path:
					if not portable_ini_exists:
						msg = "Unable to find ModOrganizer.ini. Please report this along with your MO2 instance details."
						raise FileNotFoundError(msg)
					self.manager.read_mo2_ini(portable_ini_path)
					self.manager.portable = True

			elif self.manager.name == "Vortex":
				pass

			if self.manager.game_path:
				self.game_path = self.manager.game_path
				return

		if is_fo4_dir(Path.cwd()):
			self.game_path = Path.cwd()
			return

		game_path = get_registry_value(
			winreg.HKEY_LOCAL_MACHINE,
			R"SOFTWARE\WOW6432Node\Bethesda Softworks\Fallout4",
			"Installed Path",
		) or get_registry_value(
			winreg.HKEY_LOCAL_MACHINE,
			R"SOFTWARE\WOW6432Node\GOG.com\Games\1998527297",
			"path",
		)

		if game_path is None:
			game_path = filedialog.askopenfilename(
				title="Select Fallout4.exe",
				filetypes=(("Fallout 4", "Fallout4.exe"),),
			)

			if not game_path:
				# Empty string if filedialog cancelled
				messagebox.showerror(
					"Game not found",
					"A Fallout 4 installation could not be found.",
				)
				sys.exit()

		game_path_as_path = Path(game_path)

		if game_path_as_path.is_file():
			game_path_as_path = game_path_as_path.parent

		if not is_fo4_dir(game_path_as_path):
			raise FileNotFoundError

		self.game_path = game_path_as_path

	def is_foog(self) -> bool:
		return self._install_type in {InstallType.OG, InstallType.DG}

	def is_fong(self) -> bool:
		return self._install_type == InstallType.NG

	def is_fodg(self) -> bool:
		return self._install_type == InstallType.DG




# TODO: DoubleYou — 8/27/2024 9:02 PM
BC7 inside BA2 archive --> BC7 loose files

So I found out something annoying... BC7 is ONLY allowed for base face textures if it is packed inside a BA2 archive. If the files are loose, it causes Rusty Face bug when facegen is created. Somehow it is able to read them, however, if they are inside a BA2 archive. This applies to both the game and the Creation Kit. This is why @perchik71 couldn't get BC7 diffuse to work.
