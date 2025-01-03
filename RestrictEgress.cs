using System;
using System.IO;
using System.Reflection;
using GTA;
using GTA.UI;

namespace RestrictEgress
{
    public class RestrictEgress : Script
    {
        private Ped _character;    
        private Vehicle _vehicle;
        private bool _wasPlayerInVehicleLastFrame = false;
        private bool _wasExitKeyUpAgain = false;
        private bool _wasExitKeyPressedLastFrame = true;
        private int _firstPressTime = -1;
        private bool _isWaterVehicle = false;

        public bool AllowExitIfVehicleIsOnFire = true;
        public bool UseDelayedEgress = false;
        public bool UseHoldToEgress = false;
        public int CancelationTime = 5000;
        public int MinPressTime = 2000;

        public RestrictEgress()
        {
            Interval = 1;  

            //Load Config
            ScriptSettings settings = ScriptSettings.Load(Path.Combine(BaseDirectory, "RestrictEgress.ini"));
            UseDelayedEgress = settings.GetValue("DelayedEgress", "UseDelayedEgress", false);
            UseHoldToEgress = settings.GetValue("HoldToEgress", "UseHoldToEgress", false);
            AllowExitIfVehicleIsOnFire = settings.GetValue("General", "AllowExitIfVehicleIsOnFire", false);
            CancelationTime = settings.GetValue("DelayedEgress", "CancelationTime", 5000);
            MinPressTime = settings.GetValue("HoldToEgress", "MinHoldTime", 2000);

            //Clamp values
            if(CancelationTime < 1000)
            {
                CancelationTime = 1000;
            }
            if (MinPressTime < 400)
            {
                MinPressTime = 400;
            }


            if (UseDelayedEgress && UseHoldToEgress)
            {
                const string message_default = "Using both HoldToEgress and DelayedEgress isn't supported. Script will be disabled!";
                const string message_franklin = "Yo, future franklin, I just found out that you are using both HoldToEgress and DelayedEgress, but that won't work you have to decide! The script disabled itself!";
                const string message_michael = "Hi future self, I bet you thought enabling both HoldToEgress and DelayedEgress might solve your life problems, but let me tell you, that's not how it works. Sadly the script will not work until you decide on your future!";
                const string message_trevor = "Hi idiot, it's you. For once in your fucking life it's not that simple, YOU CANNOT USE BOTH HOLDTOEGRESS AND DELAYEDEGRESS AT ONCE IDIOT. Until you have decided, I will burn the script down for you.";

                Version versionStr = typeof(Script).Assembly.GetName().Version;

                int version = int.Parse(versionStr.ToString().Replace(".", string.Empty));

                if (version >= 3700)
                {
                    Notification.PostUnlockTitleUpdate("Restrict Egress", message_default, FeedUnlockIcon.Vehicle, false);
                }
                else
                {
                    switch (Game.Player.Character.PedType)
                    {
                        case PedType.Player0:
                            Notification.Show(NotificationIcon.Michael, "Michael De Santa", "Restrict Egress", message_michael);
                            break;
                        case PedType.Player1:
                            Notification.Show(NotificationIcon.Franklin, "Franklin", "Restrict Egress", message_franklin);
                            break;
                        case PedType.Player2:
                            Notification.Show(NotificationIcon.Trevor, "Franklin", "Restrict Egress", message_trevor);
                            break;
                        default:
                            Notification.Show(NotificationIcon.Default, "Anonymous", "Restrict Egress", message_default);
                            break;
                    }
                }    
                Abort();
                return;
            }

            Tick += OnTick;
        }


        private void OnTick(object sender, EventArgs e)
        {
            Vehicle lastVeh = _vehicle;
            _character = Game.Player.Character;
            _vehicle = _character.CurrentVehicle;

            if (_vehicle == null) {

                if (_wasPlayerInVehicleLastFrame)
                {
                    _wasPlayerInVehicleLastFrame = false;
                    _firstPressTime = -1;

                    if (!lastVeh.Exists()) return;

                    lastVeh.LockStatus = VehicleLockStatus.Unlocked;
                }

                return;
            }

            if (!_wasExitKeyPressedLastFrame)
            {
                Model model = _vehicle.Model;
                _isWaterVehicle = model.IsBoat || model.IsJetSki || model.IsSubmarine || model.IsSubmarineCar;
            }
            _wasPlayerInVehicleLastFrame = true;
            World.GetGroundHeight(_vehicle.Position, out float height, GetGroundHeightMode.ConsiderWaterAsGround);
            if (_vehicle.Speed <= 1.5f || (AllowExitIfVehicleIsOnFire && _vehicle.IsOnFire) || (height <= 0 && !_isWaterVehicle))
            {
                _vehicle.LockStatus = VehicleLockStatus.Unlocked;
                return;      
            }
            _vehicle.LockStatus = VehicleLockStatus.PlayerCannotLeaveCanBeBrokenIntoPersist;

            if (UseDelayedEgress)
            {
                int time = Game.GameTime;

                if (Game.GetControlValue(Control.VehicleExit) == 254)
                {
                    _wasExitKeyPressedLastFrame = true;

                    if (_wasExitKeyUpAgain)
                    {
                        if (_firstPressTime == -1)
                        {
                            _firstPressTime = time;
                        }
                        else if (_wasExitKeyUpAgain)
                        {
                            _firstPressTime = -1;
                        }
                        _wasExitKeyUpAgain = false;
                    }
                    

                }
                else if (_wasExitKeyPressedLastFrame)
                {
                    _wasExitKeyUpAgain = true;
                }
                if(_firstPressTime != -1)
                {
                    int t = CancelationTime - time + _firstPressTime;
                    Screen.ShowHelpText($"Exiting Vehicle in: {Math.Round(t / 1000f, 0)}", -1, false, false);
                    if(time - _firstPressTime > CancelationTime)
                    {
                        LeaveVehicle();
                        _firstPressTime = -1;
                    }
                }
                return;
            }

            if (UseHoldToEgress)
            {
                if (Game.GetControlValue(Control.VehicleExit) == 254)
                {
                    GTA.UI.Screen.ShowSubtitle(Game.GetControlValue(Control.VehicleExit).ToString());
                    if (_firstPressTime == -1)
                    {
                        _firstPressTime = Game.GameTime;
                    }           
                }
                else
                {
                    if (_firstPressTime != -1)
                    {
                        int passedTime = Game.GameTime - _firstPressTime;
                        _firstPressTime = -1;

                        if (passedTime >= MinPressTime)
                        {
                            LeaveVehicle();
                        }
                    }
                }
            }
        }
        public void LeaveVehicle()
        {
            _vehicle.LockStatus = VehicleLockStatus.Unlocked;
            if (_vehicle.Speed > 1.5f)
            {
                _character.Task.LeaveVehicle(LeaveVehicleFlags.BailOut);
                return;
            }
            _character.Task.LeaveVehicle();
        }
    }
}
