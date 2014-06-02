//!CompilerOption:Optimize:On

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Linq;
using Bots.Gatherbuddy;
using Bots.Grind;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.Common.Helpers;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using Styx.TreeSharp;
using Action = Styx.TreeSharp.Action;
using Profile = Styx.CommonBot.Profiles.Profile;

namespace HighVoltz.AutoAngler
{
    public enum PathingType
    {
        Circle,
        Bounce
    }

    public class AutoAnglerBot : BotBase
    {
        private readonly List<uint> _poolsToFish = new List<uint>();

		private PathingType _pathingType;
	    private string _prevProfilePath;
		private static int _lastUkTagCallTime;
        private static DateTime _botStartTime;

        internal static readonly string BotPath = GetBotPath();
        internal static readonly Version Version = new Version(2, new Svn().Revision);

        public AutoAnglerBot()
        {
            Instance = this;
            BotEvents.Profile.OnNewOuterProfileLoaded += Profile_OnNewOuterProfileLoaded;
            Styx.CommonBot.Profiles.Profile.OnUnknownProfileElement += Profile_OnUnknownProfileElement;
        }

		internal bool LootFrameIsOpen { get; private set; }

		internal Dictionary<string, uint> FishCaught { get; private set; }
		
		internal AutoAnglerProfile Profile { get; private set; }
        internal static AutoAnglerBot Instance { get; private set; }

        #region overrides

        private readonly InventoryType[] _2HWeaponTypes =
        {
            InventoryType.TwoHandWeapon,
            InventoryType.Ranged,
        };

        private Composite _root;

        public override string Name
        {
            get { return "AutoAngler"; }
        }

        public override PulseFlags PulseFlags
        {
            get { return PulseFlags.All & (~PulseFlags.CharacterManager); }
        }

        public override Composite Root
        {
            get { return _root ?? (_root = new ActionRunCoroutine(ctx => Coroutines.RootLogic())); }
        }

        public override bool IsPrimaryType
        {
            get { return true; }
        }

        public override Form ConfigurationForm
        {
            get { return new MainForm(); }
        }

        public override void Pulse() {}

        public override void Initialize()
        {
            try
            {
				WoWItem mainhand = (AutoAnglerSettings.Instance.MainHand != 0
					? StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == AutoAnglerSettings.Instance.MainHand) 
					: null) ?? FindMainHand();

				WoWItem offhand = AutoAnglerSettings.Instance.OffHand != 0 
					? StyxWoW.Me.BagItems.FirstOrDefault(i => i.Entry == AutoAnglerSettings.Instance.OffHand) 
					: null;

                if ((mainhand == null || !_2HWeaponTypes.Contains(mainhand.ItemInfo.InventoryType)) && offhand == null)
                    offhand = FindOffhand();

				if (mainhand != null)
                    Log("Using {0} for mainhand weapon", mainhand.Name);

                if (offhand != null)
                    Log("Using {0} for offhand weapon", offhand.Name);

	            
				_prevProfilePath = ProfileManager.XmlLocation;

	            if (AutoAnglerSettings.Instance.Poolfishing && File.Exists(AutoAnglerSettings.Instance.LastLoadedProfile))
			            ProfileManager.LoadNew(AutoAnglerSettings.Instance.LastLoadedProfile);
	            else
		            ProfileManager.LoadEmpty();

	            if (AutoAnglerSettings.Instance.AutoUpdate)
	            {
		            // check for Autoangler updates
		            new Thread(Updater.CheckForUpdate) {IsBackground = true}.Start();
	            }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
        }

        public override void Start()
        {
            _botStartTime = DateTime.Now;
            FishCaught = new Dictionary<string, uint>();
	        LootTargeting.Instance.IncludeTargetsFilter += LootFilters.IncludeTargetsFilter;
            Lua.Events.AttachEvent("LOOT_OPENED", LootFrameOpenedHandler);
            Lua.Events.AttachEvent("LOOT_CLOSED", LootFrameClosedHandler);
        }


        public override void Stop()
        {            
            if (Utility.EquipWeapons())
				Log("Equipping weapons");

	        if (Utility.EquipMainHat())
				Log("Switched to my normal hat");

            Log("In {0} days, {1} hours and {2} minutes we have caught",
                (DateTime.Now - _botStartTime).Days,
                (DateTime.Now - _botStartTime).Hours,
                (DateTime.Now - _botStartTime).Minutes);

            foreach (var kv in FishCaught)
            {
                Log("{0} x{1}", kv.Key, kv.Value);
            }

			LootTargeting.Instance.IncludeTargetsFilter -= LootFilters.IncludeTargetsFilter;
            Lua.Events.DetachEvent("LOOT_OPENED", LootFrameOpenedHandler);
            Lua.Events.DetachEvent("LOOT_CLOSED", LootFrameClosedHandler);
        }

        #endregion

        #region Handlers

        private void LootFrameClosedHandler(object sender, LuaEventArgs args)
        {
            LootFrameIsOpen = false;
        }

        private void LootFrameOpenedHandler(object sender, LuaEventArgs args)
        {
            LootFrameIsOpen = true;
        }

        #endregion

        #region Profile

        private void Profile_OnNewOuterProfileLoaded(BotEvents.Profile.NewProfileLoadedEventArgs args)
        {
            try
            {
				Profile = new AutoAnglerProfile(args.NewProfile, _pathingType, _poolsToFish);
	            if (!string.IsNullOrEmpty(ProfileManager.XmlLocation))
	            {
		            AutoAnglerSettings.Instance.LastLoadedProfile = ProfileManager.XmlLocation;
					AutoAnglerSettings.Instance.Save();
	            }
            }
            catch (Exception ex)
            {
                Logging.WriteException(ex);
            }
        }

        public void Profile_OnUnknownProfileElement(object sender, UnknownProfileElementEventArgs e)
        {
            if (e.Element.Name == "FishingSchool")
            {
                // hackish way to clear my list of pool before loading new profile... wtb OnNewOuterProfileLoading event
                if (Environment.TickCount - _lastUkTagCallTime > 4000)
					_poolsToFish.Clear();

                _lastUkTagCallTime = Environment.TickCount;
                XAttribute entryAttrib = e.Element.Attribute("Entry");
                if (entryAttrib != null)
                {
                    uint entry;
                    UInt32.TryParse(entryAttrib.Value, out entry);
					if (!_poolsToFish.Contains(entry))
                    {
						_poolsToFish.Add(entry);
                        XAttribute nameAttrib = e.Element.Attribute("Name");
                        if (nameAttrib != null)
                            Log( "Adding Pool Entry: {0} to the list of pools to fish from", nameAttrib.Value);
                        else
                            Log("Adding Pool Entry: {0} to the list of pools to fish from", entry);
                    }
                }
                else
                {
                    Err(
                        "<FishingSchool> tag must have the 'Entry' Attribute, e.g <FishingSchool Entry=\"202780\"/>\nAlso supports 'Name' attribute but only used for display purposes");
                }
                e.Handled = true;
            }
            else if (e.Element.Name == "Pathing")
            {
                XAttribute typeAttrib = e.Element.Attribute("Type");
                if (typeAttrib != null)
                {
                    _pathingType = (PathingType)
                        Enum.Parse(typeof (PathingType), typeAttrib.Value, true);
                    
					Log("Setting Pathing Type to {0} Mode", _pathingType);
                }
                else
                {
                    Err(
                        "<Pathing> tag must have the 'Type' Attribute, e.g <Pathing Type=\"Circle\"/>");
                }
                e.Handled = true;
            }
        }

        #endregion

        private WoWItem FindMainHand()
        {
			WoWItem mainHand = StyxWoW.Me.Inventory.Equipped.MainHand;
	        if (mainHand == null || mainHand.ItemInfo.WeaponClass == WoWItemWeaponClass.FishingPole)
	        {
		        mainHand = StyxWoW.Me.CarriedItems.OrderByDescending(u => u.ItemInfo.Level).
			        FirstOrDefault(
				        i => i.IsSoulbound && (i.ItemInfo.InventoryType == InventoryType.WeaponMainHand ||
												i.ItemInfo.InventoryType == InventoryType.TwoHandWeapon) &&
							StyxWoW.Me.CanEquipItem(i));

		        if (mainHand != null)
			        AutoAnglerSettings.Instance.MainHand = mainHand.Entry;
		        else
			        Err("Unable to find a mainhand weapon to swap to when in combat");
	        }
	        else
	        {
		        AutoAnglerSettings.Instance.MainHand = mainHand.Entry;
	        }
			AutoAnglerSettings.Instance.Save();
            return mainHand;
        }

        // scans bags for offhand weapon if mainhand isn't 2h and none are equipped and uses the highest ilvl one
        private WoWItem FindOffhand()
        {
			WoWItem offHand = StyxWoW.Me.Inventory.Equipped.OffHand;
	        if (offHand == null)
	        {
		        offHand = StyxWoW.Me.CarriedItems.OrderByDescending(u => u.ItemInfo.Level).
			        FirstOrDefault(
				        i => i.IsSoulbound && (i.ItemInfo.InventoryType == InventoryType.WeaponOffHand ||
												i.ItemInfo.InventoryType == InventoryType.Weapon ||
												i.ItemInfo.InventoryType == InventoryType.Shield) &&
							AutoAnglerSettings.Instance.MainHand != i.Entry &&
							StyxWoW.Me.CanEquipItem(i));

		        if (offHand != null)
			        AutoAnglerSettings.Instance.OffHand = offHand.Entry;
		        else
			        Err("Unable to find an offhand weapon to swap to when in combat");
	        }
	        else
	        {
		        AutoAnglerSettings.Instance.OffHand = offHand.Entry;
	        }
			AutoAnglerSettings.Instance.Save();
            return offHand;
        }

        internal static void Log(string format, params object[] args)
        {
            Logging.Write(Colors.DodgerBlue, String.Format("AutoAngler[{0}]: {1}", Version, format), args);
        }

        internal static void Err(string format, params object[] args)
        {
            Logging.Write(Colors.Red, String.Format("AutoAngler[{0}]: {1}", Version, format), args);
        }

        internal static void Debug(string format, params object[] args)
        {
            Logging.WriteDiagnostic(Colors.DodgerBlue, String.Format("AutoAngler[{0}]: {1}", Version, format), args);
        }

        private static string GetBotPath()
        { // taken from Singular.
            // bit of a hack, but location of source code for assembly is only.
            var asmName = Assembly.GetExecutingAssembly().GetName().Name;
            var len = asmName.LastIndexOf("_", StringComparison.Ordinal);
            var folderName = asmName.Substring(0, len);

            var botsPath = GlobalSettings.Instance.BotsPath;
            if (!Path.IsPathRooted(botsPath))
            {
                botsPath = Path.Combine(Utilities.AssemblyDirectory, botsPath);
            }
            return Path.Combine(botsPath, folderName);
        }
    }
}