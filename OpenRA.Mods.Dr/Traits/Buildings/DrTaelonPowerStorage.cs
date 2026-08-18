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

using System;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	[Desc("Stores Taelon delivered by freighters and scales power output as the store fills.")]
	public class DrTaelonPowerStorageInfo : TraitInfo, Requires<PowerInfo>, Requires<IDockHostInfo>
	{
		[Desc("Resource accepted by this power generator.")]
		public readonly string ResourceType = "Taelon";

		[Desc("Maximum stored Taelon value.")]
		public readonly int Capacity = 3000;

		[Desc("Power output percentage when the Taelon store is empty.")]
		public readonly int EmptyPowerModifier = 100;

		[Desc("Power output percentage when the Taelon store is full.")]
		public readonly int FullPowerModifier = 400;

		public override object Create(ActorInitializer init) { return new DrTaelonPowerStorage(init.Self, this); }
	}

	public class DrTaelonPowerStorage : IAcceptResources, IPowerModifier, INotifyOwnerChanged, ISync
	{
		readonly DrTaelonPowerStorageInfo info;
		readonly Actor self;
		PowerManager powerManager;
		PlayerResources playerResources;

		[VerifySync]
		public int Taelon;

		public int TaelonPercentage => info.Capacity > 0 ? 100 * Taelon / info.Capacity : 0;

		public DrTaelonPowerStorage(Actor self, DrTaelonPowerStorageInfo info)
		{
			this.self = self;
			this.info = info;
			powerManager = self.Owner.PlayerActor.Trait<PowerManager>();
			playerResources = self.Owner.PlayerActor.Trait<PlayerResources>();
		}

		int IAcceptResources.AcceptResources(Actor actor, string resourceType, int count)
		{
			if (resourceType != info.ResourceType || count <= 0 || info.Capacity <= 0)
				return 0;

			if (!playerResources.Info.ResourceValues.TryGetValue(resourceType, out var resourceValue) || resourceValue <= 0)
				return 0;

			var remainingCapacity = Math.Max(info.Capacity - Taelon, 0);
			var accepted = Math.Min(count, remainingCapacity / resourceValue);
			if (accepted <= 0)
				return 0;

			Taelon += accepted * resourceValue;
			powerManager.UpdateActor(self);
			return accepted;
		}

		int IPowerModifier.GetPowerModifier()
		{
			if (info.Capacity <= 0)
				return info.EmptyPowerModifier;

			var range = info.FullPowerModifier - info.EmptyPowerModifier;
			return info.EmptyPowerModifier + range * Taelon / info.Capacity;
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor actor, Player oldOwner, Player newOwner)
		{
			powerManager = newOwner.PlayerActor.Trait<PowerManager>();
			playerResources = newOwner.PlayerActor.Trait<PlayerResources>();
			powerManager.UpdateActor(self);
		}
	}
}
