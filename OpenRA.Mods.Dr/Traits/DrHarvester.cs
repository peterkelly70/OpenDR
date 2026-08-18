#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	[Desc("Dark Reign harvester that routes Water and Taelon to different dock types.")]
	public class DrHarvesterInfo : HarvesterInfo
	{
		[Desc("Resource type delivered to the water launch pad.")]
		public readonly string WaterResource = "Water";

		[Desc("Resource type delivered to the power generator.")]
		public readonly string TaelonResource = "Taelon";

		[Desc("Dock type used when carrying Water.")]
		public readonly BitSet<DockType> WaterDockType = new("WaterUnload");

		[Desc("Dock type used when carrying Taelon.")]
		public readonly BitSet<DockType> TaelonDockType = new("TaelonUnload");

		public override object Create(ActorInitializer init) { return new DrHarvester(init.Self, this); }
	}

	public class DrHarvester : Harvester
	{
		readonly Actor self;
		readonly DrHarvesterInfo drInfo;
		readonly IStoresResources[] storesResources;

		public DrHarvester(Actor self, DrHarvesterInfo info)
			: base(self, info)
		{
			this.self = self;
			drInfo = info;
			storesResources = self.TraitsImplementing<IStoresResources>()
				.Where(sr => info.Resources.Any(r => sr.HasType(r)))
				.ToArray();
		}

		bool HasResource(string resourceType)
		{
			return storesResources.Any(sr =>
				sr.Contents.TryGetValue(resourceType, out var amount) && amount > 0);
		}

		public override BitSet<DockType> GetDockType
		{
			get
			{
				// Prioritise Water for a mixed load. Once Water is unloaded the
				// dock type switches to Taelon and OnDockTick ends the current dock.
				if (HasResource(drInfo.WaterResource))
					return drInfo.WaterDockType;

				if (HasResource(drInfo.TaelonResource))
					return drInfo.TaelonDockType;

				// Empty harvesters can consider either compatible resource dock.
				return drInfo.Type;
			}
		}

		public override bool OnDockTick(Actor self, Actor hostActor, IDockHost host)
		{
			// A mixed load may change its required dock after one resource has
			// been unloaded. End this docking sequence so the remaining resource
			// can be delivered to its own destination.
			if (!GetDockType.Overlaps(host.GetDockType))
				return true;

			return base.OnDockTick(self, hostActor, host);
		}

		public override void OnDockCompleted(Actor self, Actor hostActor, IDockHost host)
		{
			var hasRemainingCargo = !IsEmpty;
			var currentDockMatchesRemainingCargo = GetDockType.Overlaps(host.GetDockType);

			base.OnDockCompleted(self, hostActor, host);

			if (hasRemainingCargo && !currentDockMatchesRemainingCargo)
				this.self.QueueActivity(true, new MoveToDock(this.self, dockLineColor: DockClientManager.DockLineColor));
		}
	}
}
