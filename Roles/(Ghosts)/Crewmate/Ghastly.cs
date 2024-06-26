﻿using AmongUs.GameOptions;
using TOHE.Roles.Core;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;
using UnityEngine;

namespace TOHE.Roles._Ghosts_.Crewmate
{
    internal class Ghastly : RoleBase
    {
        //===========================SETUP================================\\
        private const int Id = 22060;
        public static bool HasEnabled => CustomRoleManager.HasEnabled(CustomRoles.Ghastly);
        public override CustomRoles ThisRoleBase => CustomRoles.GuardianAngel;
        public override Custom_RoleType ThisRoleType => Custom_RoleType.CrewmateGhosts;
        //==================================================================\\

        private static OptionItem PossessCooldown;
        private static OptionItem MaxPossesions;
        private static OptionItem PossessDur;
        private static OptionItem GhastlySpeed;

        private (byte, byte) killertarget = (byte.MaxValue, byte.MaxValue);
        private readonly Dictionary<byte, long> LastTime = [];
        private bool KillerIsChosen = false;

        public override void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Ghastly);
            PossessCooldown = FloatOptionItem.Create(Id + 10, "GhastlyPossessCD", new(2.5f, 120f, 2.5f), 35f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ghastly])
                .SetValueFormat(OptionFormat.Seconds);
            MaxPossesions = IntegerOptionItem.Create(Id + 11, "GhastlyMaxPossessions", new(1, 99, 1), 10, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ghastly])
                .SetValueFormat(OptionFormat.Players);
            PossessDur = IntegerOptionItem.Create(Id + 12, "GhastlyPossessionDuration", new(5, 120, 5), 40, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ghastly])
                .SetValueFormat(OptionFormat.Seconds);
            GhastlySpeed = FloatOptionItem.Create(Id + 13, "GhastlySpeed", new(1.5f, 5f, 0.5f), 2f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ghastly])
                .SetValueFormat(OptionFormat.Multiplier);
        }
        public override void Add(byte playerId)
        {
            AbilityLimit = MaxPossesions.GetInt();

            CustomRoleManager.OnFixedUpdateOthers.Add(OnFixUpdateOthers);

            // OnCheckProtect(_Player, Utils.GetPlayerById(0));
           // OnCheckProtect(_Player, Utils.GetPlayerById(2));
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.GuardianAngelCooldown = PossessCooldown.GetFloat();
            AURoleOptions.ProtectionDurationSeconds = 0f;
        }
        public override bool OnCheckProtect(PlayerControl angel, PlayerControl target)
        {
            if (AbilityLimit <= 0)
            {
                angel.Notify(GetString("GhastlyNoMorePossess"));
                return false;
            }

            var killer = killertarget.Item1;
            var Target = killertarget.Item2;

            if (!KillerIsChosen && target.PlayerId != killer)
            {
                TargetArrow.Remove(killer, Target);
                LastTime.Remove(killer);
                killer = target.PlayerId;
                Target = byte.MaxValue;
                KillerIsChosen = true;

                angel.Notify(GetString("GhastlyChooseTarget"));
            }
            else if (KillerIsChosen && Target == byte.MaxValue && target.PlayerId != killer)
            {
                Target = target.PlayerId;
                AbilityLimit--;
                SendSkillRPC();
                LastTime.Add(killer, GetTimeStamp());

                KillerIsChosen = false;
                GetPlayerById(killer).Notify(GetString("GhastlyYouvePosses"));

                TargetArrow.Add(killer, Target);
                angel.RpcGuardAndKill(target);
                angel.RpcResetAbilityCooldown();

                Logger.Info($" chosen {target.GetRealName()} for : {GetPlayerById(killer).GetRealName()}", "GhastlyTarget");
            }
            else if (target.PlayerId == killer)
            {
                angel.Notify(GetString("GhastlyCannotPossessTarget"));
            }

            killertarget = (killer, Target);
            // Logger.Info($"{killertarget.Item1} ++ {killertarget.Item2}", "ghasltytargets");

            return false;
        }
        public override void OnFixedUpdate(PlayerControl pc)
        {
            var speed = Main.AllPlayerSpeed[pc.PlayerId];
            if (speed != GhastlySpeed.GetFloat())
            {
                Main.AllPlayerSpeed[pc.PlayerId] = GhastlySpeed.GetFloat();
                pc.MarkDirtySettings();
            }
        }
        public void OnFixUpdateOthers(PlayerControl player)
        {
            if (killertarget.Item1 == player.PlayerId 
                && LastTime.TryGetValue(player.PlayerId, out var now) && now + PossessDur.GetInt() <= GetTimeStamp())
            {
                Logger.Info("removing the possesed!!", "ghastlyremovable");
                TargetArrow.Remove(killertarget.Item1, killertarget.Item2);
                LastTime.Remove(player.PlayerId);
                KillerIsChosen = false;
                killertarget = (byte.MaxValue, byte.MaxValue);
            }

        }
        public override bool CheckMurderOnOthersTarget(PlayerControl killer, PlayerControl target)
        {
            var tuple = killertarget;
            // Logger.Info($" check KILLER {(killer.GetRealName())} : {Utils.GetPlayerById(killertarget.Item1).GetRealName()}" +  $" ++  check TARGET {(target.GetRealName())} : {Utils.GetPlayerById(killertarget.Item2).GetRealName()}", "GHASTLYONMURDEROTHER");
            if (tuple.Item1 == killer.PlayerId && tuple.Item2 != byte.MaxValue)
            {
                if (tuple.Item2 != target.PlayerId)
                {
                    //Logger.Info($"Returned true", "GHASTLYONMURDEROTHER");
                    killer.Notify(GetString("GhastlyNotUrTarget"));
                    return true;
                }
                else 
                {
                    TargetArrow.Remove(killertarget.Item1, killertarget.Item2);
                    LastTime.Remove(killer.PlayerId);
                    KillerIsChosen = false;
                    killertarget = (byte.MaxValue, byte.MaxValue);
                }
            }
            // Logger.Info($"Returned false", "GHASTLYONMURDEROTHER");
            return false;
        }

        public override string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
        {
            var IsMeeting = GameStates.IsMeeting || isForMeeting;
            if (IsMeeting || (seer != seen && seer.IsAlive())) return "";

            var killer = killertarget.Item1;
            var target = killertarget.Item2;

            if (killer == seen.PlayerId && target != byte.MaxValue)
            {
                var arrows = TargetArrow.GetArrows(GetPlayerById(killer), target);
                var tar = GetPlayerById(target).GetRealName();
                if (tar == null) return "";

                var colorstring = ColorString(GetRoleColor(CustomRoles.Ghastly), "<alpha=#88>" + tar + arrows);
                return colorstring;
            }


            return "";
        }
        public override void OnOtherTargetsReducedToAtoms(PlayerControl DeadPlayer)
        {
            var tuple = killertarget;
            if (DeadPlayer.PlayerId == tuple.Item1)
            {
                TargetArrow.Remove(killertarget.Item1, killertarget.Item2);
                LastTime.Remove(DeadPlayer.PlayerId);
                KillerIsChosen = false;
                killertarget = (byte.MaxValue, byte.MaxValue);
            }
        }

        public override string GetProgressText(byte playerId, bool cooms) => ColorString(AbilityLimit > 0 ? GetRoleColor(CustomRoles.Ghastly).ShadeColor(0.25f) : Color.gray, $"({AbilityLimit})");
        
    }
}
