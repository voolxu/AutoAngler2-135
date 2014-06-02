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
				// DKs have 2 Path of Frost auras. only one can be stored in WoWAuras at any time. 
				return IsAuraActive("Levitate", MinimumWaterWalkingTimeLeft)
						|| IsAuraActive("Anglers Fishing Raft", MinimumWaterWalkingTimeLeft)
						|| IsAuraActive("Water Walking", MinimumWaterWalkingTimeLeft)
						|| IsAuraActive("Unending Breath", MinimumWaterWalkingTimeLeft)
						|| IsAuraActive("Bipsi's Bobbing Berg", MinimumWaterWalkingTimeLeft)
						|| IsAuraActive(PathOfFrostSpellId, MinimumWaterWalkingTimeLeft)
						|| IsAuraActive("Surface Trot", MinimumWaterWalkingTimeLeft)
						// Only active when in the Inkgill Mere area in MOP
						|| IsAuraActive("Blessing of the Inkgill");
			}
		}

		static bool IsAuraActive(string auraName, TimeSpan? minTimeLeft = null)
		{
			return IsAuraActive(StyxWoW.Me.GetAuraByName(auraName), minTimeLeft);
		}

		static bool IsAuraActive(int auraId, TimeSpan? minTimeLeft = null)
		{
			return IsAuraActive(StyxWoW.Me.GetAuraById(auraId), minTimeLeft);
		}

		static bool IsAuraActive(WoWAura aura, TimeSpan? minTimeLeft = null)
		{
			return aura != null && (minTimeLeft == null || aura.TimeLeft >= minTimeLeft);
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

			if (StyxWoW.Me.IsSwimming)
			{
				AutoAnglerBot.Log("Jumping up on water surface since I'm swimming but have water walking");
				var sw = Stopwatch.StartNew();
				while (StyxWoW.Me.IsSwimming)
				{
					if (StyxWoW.Me.IsBeingAttacked)
						return false;

					if (sw.ElapsedMilliseconds > 15000)
					{
						var pool = BotPoi.Current.AsObject as WoWGameObject;
						if (pool != null)
						{
							AutoAnglerBot.Log("Moving to another spot since couldn't jump on top.");
							Coroutines.RemovePointAtTop(pool);								
						}
						break;
					}

					try
					{
						// Make sure the player's pitch is not pointing down causing player to not being able to 
						// water walk
						Lua.DoString("VehicleAimIncrement(1)");
						WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend);
						await Coroutine.Wait(15000, () => StyxWoW.Me.IsFalling || !StyxWoW.Me.IsSwimming);
					}
					finally
					{
						WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend);
					}
					if (await Coroutine.Wait(2000, () => !StyxWoW.Me.IsSwimming && !StyxWoW.Me.IsFalling))
					{
						AutoAnglerBot.Log("Successfuly landed on water surface.");
						break;
					}
				}
			}

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