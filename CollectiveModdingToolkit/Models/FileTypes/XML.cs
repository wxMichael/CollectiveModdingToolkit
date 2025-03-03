/*
 * 
 * Collective Modding Toolkit
 * Copyright (C) 2024, 2025  wxMichael
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

namespace CollectiveModdingToolkit.Models.FileTypes;
internal class XML : IFile
{
	public string Name => throw new NotImplementedException();

	public string PathFull => throw new NotImplementedException();

	public string PathRelative => throw new NotImplementedException();

	public string Ext => ".xml";

	public bool? IsReadable => throw new NotImplementedException();

	public bool WasRead => throw new NotImplementedException();

	public int Priority => throw new NotImplementedException();

	public bool IsManaged => throw new NotImplementedException();

	public string? Mod => throw new NotImplementedException();
}
