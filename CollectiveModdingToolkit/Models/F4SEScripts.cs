/*
 *
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025, 2026  wxMichael
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, see <https://www.gnu.org/licenses/>.
 *
 */

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CollectiveModdingToolkit.Models;

/// <summary>CRC32s of the vanilla F4SE scripts, per game version.</summary>
internal static class F4SEScripts
{
	private static readonly FrozenDictionary<string, FrozenDictionary<string, string>> Hashes = Build();

	/// <summary>Gets the expected CRC32 of an F4SE script for a game version.</summary>
	/// <param name="gameVersion">A <see cref="GameVersion" /> value. Down-Grade uses the Old-Gen scripts.</param>
	/// <param name="fileName">The script's file name, e.g. <c>actor.pex</c>. Case-insensitive.</param>
	/// <param name="crc">The expected CRC32 as 8 hex digits. Case-insensitive.</param>
	/// <returns>
	///     <see langword="false" /> if the file isn't an F4SE script, or there is no data for the game version.
	/// </returns>
	internal static bool TryGetHash(string gameVersion, string fileName, [NotNullWhen(true)] out string? crc)
	{
		if (gameVersion == GameVersion.DownGrade) gameVersion = GameVersion.OldGen;

		if (Hashes.TryGetValue(gameVersion, out var scripts) && scripts.TryGetValue(fileName, out crc)) return true;

		crc = null;
		return false;
	}

	private static FrozenDictionary<string, FrozenDictionary<string, string>> Build()
	{
		var raw = Raw();
		return raw.ToFrozenDictionary(p => p.Key, p => p.Value.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
			StringComparer.OrdinalIgnoreCase);
	}

	private static Dictionary<string, Dictionary<string, string>> Raw() =>
		new(StringComparer.OrdinalIgnoreCase)
		{
			[GameVersion.OldGen] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["actor.pex"] = "c250936e",
				["actorbase.pex"] = "0681e1f5",
				["armor.pex"] = "c145d146",
				["armoraddon.pex"] = "b0d1b057",
				["cell.pex"] = "0cb47bbe",
				["component.pex"] = "ee3b04a6",
				["constructibleobject.pex"] = "9cdd93be",
				["defaultobject.pex"] = "9df10d4b",
				["encounterzone.pex"] = "4ec6ce1f",
				["equipslot.pex"] = "3ecc3ca5",
				["f4se.pex"] = "6eeee7d9",
				["favoritesmanager.pex"] = "ac1694b2",
				["form.pex"] = "57919410",
				["game.pex"] = "2b0602cc",
				["headpart.pex"] = "8dc03678",
				["input.pex"] = "92399489",
				["instancedata.pex"] = "73f0fbbc",
				["location.pex"] = "d35726c4",
				["math.pex"] = "8d97f9d2",
				["matswap.pex"] = "49f6c1d2",
				["miscobject.pex"] = "a6de1c41",
				["objectmod.pex"] = "9161cd93",
				["objectreference.pex"] = "ad399939",
				["perk.pex"] = "f7d88ccc",
				["scriptobject.pex"] = "e3af8ec9",
				["ui.pex"] = "00286c67",
				["utility.pex"] = "5fdfab76",
				["watertype.pex"] = "6543e221",
				["weapon.pex"] = "e1e3a63c",
			},
			[GameVersion.NextGen] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["actor.pex"] = "ae6222be",
				["actorbase.pex"] = "fb9bcdab",
				["armor.pex"] = "3f9e535a",
				["armoraddon.pex"] = "4f62262c",
				["cell.pex"] = "29f5583b",
				["component.pex"] = "cf50dea9",
				["constructibleobject.pex"] = "55489ce6",
				["defaultobject.pex"] = "d173ae18",
				["encounterzone.pex"] = "a70d8729",
				["equipslot.pex"] = "73a59f70",
				["f4se.pex"] = "13e2a430",
				["favoritesmanager.pex"] = "4933b1c6",
				["form.pex"] = "06e8a822",
				["game.pex"] = "bcf2e430",
				["headpart.pex"] = "f6e4a814",
				["input.pex"] = "d5fc1734",
				["instancedata.pex"] = "c1f7fa69",
				["location.pex"] = "f2e89378",
				["math.pex"] = "35f956bd",
				["matswap.pex"] = "cc2d9b50",
				["miscobject.pex"] = "c4ea2902",
				["objectmod.pex"] = "97601f72",
				["objectreference.pex"] = "64fc714e",
				["perk.pex"] = "d596624d",
				["scriptobject.pex"] = "68f24e0f",
				["ui.pex"] = "749cc3da",
				["utility.pex"] = "9b902650",
				["watertype.pex"] = "946e310a",
				["weapon.pex"] = "bca3fd37",
			},
			[GameVersion.Anniversary] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["actor.pex"] = "72388175",
				["actorbase.pex"] = "8206385d",
				["armor.pex"] = "06fc8ab2",
				["armoraddon.pex"] = "8453cb43",
				["cell.pex"] = "043b5f5a",
				["component.pex"] = "6a1a2d87",
				["constructibleobject.pex"] = "3c9b9628",
				["defaultobject.pex"] = "ad23389a",
				["encounterzone.pex"] = "6c1c70d5",
				["equipslot.pex"] = "33dee9af",
				["f4se.pex"] = "c3fc9a6e",
				["favoritesmanager.pex"] = "0d6ca970",
				["form.pex"] = "ff43ca3e",
				["game.pex"] = "8876ee5c",
				["headpart.pex"] = "8f5714e9",
				["input.pex"] = "ba2ff626",
				["instancedata.pex"] = "5ab4c774",
				["location.pex"] = "427b459d",
				["math.pex"] = "05deb0f0",
				["matswap.pex"] = "b6e8fa13",
				["miscobject.pex"] = "c672a908",
				["objectmod.pex"] = "d58ca42e",
				["objectreference.pex"] = "4f79982b",
				["perk.pex"] = "432d9eb4",
				["scriptobject.pex"] = "41228361",
				["ui.pex"] = "bbf99ec3",
				["utility.pex"] = "4fa68a14",
				["watertype.pex"] = "11400876",
				["weapon.pex"] = "996b1034",
			},
		};
}
