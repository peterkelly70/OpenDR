#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Dark Reign resource layer with regenerating Water and Taelon deposits.")]
	public class DrResourceLayerInfo : ResourceLayerInfo
	{
		[Desc("Ticks between resource regeneration steps. Set to 0 to disable regeneration.")]
		public readonly int RegenerationInterval = 25;

		[Desc("Resource density restored to each original deposit per regeneration step.")]
		public readonly byte RegenerationAmount = 1;

		public override object Create(ActorInitializer init) { return new DrResourceLayer(init.Self, this); }
	}

	public class DrResourceLayer : ResourceLayer, ITick
	{
		readonly DrResourceLayerInfo info;
		readonly World world;
		readonly Dictionary<CPos, string> regenerationSources = new();
		readonly HashSet<CPos> suppressedRegeneration = new();
		int regenerationTicks;

		public DrResourceLayer(Actor self, DrResourceLayerInfo info)
			: base(self, info)
		{
			this.info = info;
			world = self.World;
		}

		protected override void WorldLoaded(World w, WorldRenderer wr)
		{
			var resourceLayer = (IResourceLayer)this;
			foreach (var cell in w.Map.AllCells)
			{
				var resource = world.Map.Resources[cell];
				if (!ResourceTypesByIndex.TryGetValue(resource.Type, out var resourceType))
					continue;

				if (!AllowResourceAt(resourceType, cell))
					continue;

				if (!info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
					continue;

				var density = resource.Index == 0 ? resourceInfo.MaxDensity : Math.Min(resource.Index, resourceInfo.MaxDensity);
				resourceLayer.AddResource(resourceType, cell, density);
				regenerationSources[cell] = resourceType;
			}
		}

		void ITick.Tick(Actor self)
		{
			if (info.RegenerationInterval <= 0 || info.RegenerationAmount == 0 || regenerationSources.Count == 0)
				return;

			if (++regenerationTicks < info.RegenerationInterval)
				return;

			regenerationTicks = 0;
			var resourceLayer = (IResourceLayer)this;
			foreach (var source in regenerationSources)
			{
				if (suppressedRegeneration.Contains(source.Key))
					continue;

				var current = resourceLayer.GetResource(source.Key);
				if (current.Type != null && current.Type != source.Value)
					continue;

				if (resourceLayer.CanAddResource(source.Value, source.Key, info.RegenerationAmount))
					resourceLayer.AddResource(source.Value, source.Key, info.RegenerationAmount);
			}
		}

		// Water Contaminators and future map logic can permanently suppress a source
		// without deleting the underlying source identity used for normal regeneration.
		public void SuppressRegeneration(CPos cell)
		{
			suppressedRegeneration.Add(cell);
		}

		public void RestoreRegeneration(CPos cell)
		{
			suppressedRegeneration.Remove(cell);
		}
	}
}
