using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.CommonBot.POI;
using Styx.Helpers;
using Styx.Pathing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler
{
	public class WaterWalking
	{
		private static readonly WaitTimer RecastTimer = WaitTimer.FiveSeconds;

		public static bool CanCast
		{
			get
			{
				return AutoAnglerSettings.Instance.UseWaterWalking &&
					   (SpellManager.HasSpell("Levitate") || // priest levitate
						SpellManager.HasSpell("Water Walking") || // shaman water walking
						SpellManager.HasSpell(PathOfFrostSpellId) || // Dk Path of frost
						SpellManager.HasSpell("Soulburn") || // Affliction Warlock
						StyxWoW.Me.HasAura("Still Water") || // hunter with water strider pet.
						Utility.IsItemInBag(ElixirOfWaterWalkingId) || //isItemInBag(8827);
						Utility.IsItemInBag(FishingRaftId)); // Anglers Fishing Raft
			}
		}

		private static readonly TimeSpan MinimumWaterWalkingTimeLeft = TimeSpan.FromSeconds(20);

		public static bool IsActive
		{
			get
			{
				return (from aura in StyxWoW.Me.GetAllAuras()
					let spell = aura.Spell
					where spell != null && spell.IsValid && spell.SpellEffects.Any(e => e.AuraType == WoWApplyAuraType.WaterWalk)
					let timeLeft = aura.TimeLeft
					where timeLeft == TimeSpan.Zero || timeLeft >= MinimumWaterWalkingTimeLeft
					select aura).Any();
			}
		}

		public static async Task<bool> Cast()
		{
			bool casted = false;
			if (!IsActive)
			{
				if (!RecastTimer.IsFinished)
					return false;

				int waterwalkingSpellID = 0;
				switch (StyxWoW.Me.Class)
				{
					case WoWClass.Priest:
						waterwalkingSpellID = LevitateSpellId;
						break;
					case WoWClass.Shaman:
						waterwalkingSpellID = 546;
						break;
					case WoWClass.DeathKnight:
						waterwalkingSpellID = PathOfFrostSpellId;
						break;
					case WoWClass.Warlock:
						waterwalkingSpellID = UnendingBreathSpellId;
						break;
					case WoWClass.Hunter:
						// cast Surface Trot if Water Strider pet is active.
						if (StyxWoW.Me.HasAura("Still Water"))
							waterwalkingSpellID = SurfaceTrotSpellId;
						break;
				}
				if (waterwalkingSpellID != 0 && (SpellManager.CanCast(waterwalkingSpellID) || StyxWoW.Me.HasAura("Still Water")))
				{
					if (StyxWoW.Me.Class == WoWClass.Warlock)
						SpellManager.Cast(SoulburnSpellId); //cast Soulburn
					// use lua to cast spells because SpellManager.Cast can't handle pet spells.
					Lua.DoString("CastSpellByID ({0})", waterwalkingSpellID);
					casted = true;
				}
				else
				{
					WoWItem waterPot;
					if ((waterPot = Utility.GetItemInBag(ElixirOfWaterWalkingId)) != null && waterPot.Use())
					{
						casted = true;
					}
					else
					{
						WoWItem fishingRaft;
						if ((fishingRaft = Utility.GetItemInBag(FishingRaftId)) != null && fishingRaft.Use())
						{
							casted = true;
						}
						else if ((fishingRaft = Utility.GetItemInBag(BipsisBobbingBergId)) != null && fishingRaft.Use())
						{
							casted = true;
						}
					}
				}
			}
			if (casted)
				await CommonCoroutines.SleepForLagDuration();

			return casted;
		}

		#region Static Members

		private const int BipsisBobbingBergId = 107950;
		private const int FishingRaftId = 85500;
		private const int ElixirOfWaterWalkingId = 8827;
		private const int SoulburnSpellId = 74434;
		private const int SurfaceTrotSpellId = 126311;
		private const int UnendingBreathSpellId = 5697;
		private const int PathOfFrostSpellId = 3714;
		private const int LevitateSpellId = 1706;

		#endregion

	}
}