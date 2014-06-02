﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Styx;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.Coroutines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;

namespace HighVoltz.AutoAngler
{
	static partial class Coroutines
	{
		const float PIx2 = 3.14159f * 2f;
		const int TraceStep = 20;

		public async static Task<bool> FaceWater()
		{
			float? bestDirection = GetFaceWaterDirection();
			if (bestDirection.HasValue &&
				!WoWMathHelper.IsFacing(StyxWoW.Me.Location, StyxWoW.Me.Rotation, StyxWoW.Me.Location.RayCast(bestDirection.Value, 10f), WoWMathHelper.DegreesToRadians(15)))
			{
				AutoAnglerBot.Log("auto facing towards water");
				Me.SetFacing(bestDirection.Value);
			}
			return false;
		}

		// ToDo Attach to UNIT_SPELLCAST_FAILED lua event and only face when cast failed.
		// Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
		private static float? GetFaceWaterDirection()
		{
			WoWPoint playerLoc = StyxWoW.Me.Location;
			var sonar = new List<int>(TraceStep);
			var tracelines = new WorldLine[TraceStep * 3];
			bool[] tracelineRetVals;
			for (int i = 0; i < TraceStep; i++)
			{
				// scans 10,15 and 20 yards from player for water at every 18 degress 
				for (int n = 0; n < 3; n++)
				{
					WoWPoint p = (playerLoc.RayCast((i * PIx2) / TraceStep, 10 + (n * 5)));
					WoWPoint highPoint = p;
					highPoint.Z += 5;
					WoWPoint lowPoint = p;
					lowPoint.Z -= 55;
					tracelines[(i * 3) + n].Start = highPoint;
					tracelines[(i * 3) + n].End = lowPoint;
				}
			}
			GameWorld.MassTraceLine(tracelines,
				GameWorld.CGWorldFrameHitFlags.HitTestLiquid | GameWorld.CGWorldFrameHitFlags.HitTestLiquid2,
				out tracelineRetVals);
			for (int i = 0; i < TraceStep; i++)
			{
				int scan = 0;
				for (int n = 0; n < 3; n++)
				{
					if (tracelineRetVals[(i * 3) + n])
						scan++;
				}
				sonar.Add(scan);
			}

			int widest = 0;
			for (int i = 0; i < TraceStep; i++)
			{
				if (sonar[i] > widest)
					widest = sonar[i];
			}
			bool counting = false;
			int startIndex = 0, bigestStartIndex = 0, startLen = 0, endLen = 0, bigestStretch = 0;
			// if we found water find the largest area and face towards the center of it.


			if (widest > 0)
			{
				for (int i = 0; i < TraceStep; i++)
				{
					if (sonar[i] == widest && !counting)
					{
						startIndex = i;
						if (i == 0)
							startLen = 1;
						counting = true;
					}
					if (sonar[i] != widest && counting)
					{
						if ((i) - startIndex > bigestStretch)
						{
							bigestStretch = (i) - startIndex;
							bigestStartIndex = startIndex;
						}
						if (startIndex == 0)
							startLen = i;
						counting = false;
					}
					if (sonar[i] == widest && counting && i == 19)
						endLen = i - startIndex;
				}
				int index;
				if (startLen + endLen > bigestStretch)
				{
					if (startLen >= endLen)
						index = startLen > endLen ? startLen - endLen : endLen - startLen;
					else
						index = (TraceStep - 1) - (endLen - startLen);
				}
				else
					index = bigestStartIndex + (bigestStretch / 2);
				float direction = (index * PIx2) / 20;

				return direction;
			}
			return null;
		}
	}
}
