using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

internal class Huntsman : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 16500;
    public static HashSet<byte> playerIdList = [];
    public static bool HasEnabled => playerIdList.Count > 0;
    public override bool IsEnable => HasEnabled;
    public override CustomRoles ThisRoleBase => CustomRoles.Impostor;

    //==================================================================\\

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem SuccessKillCooldown;
    private static OptionItem FailureKillCooldown;
    private static OptionItem NumOfTargets;
    private static OptionItem MinKCD;
    private static OptionItem MaxKCD;

    public static List<byte> Targets = [];
    public static float KCD = 25;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Huntsman, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Huntsman])
            .SetValueFormat(OptionFormat.Seconds);
        SuccessKillCooldown = FloatOptionItem.Create(Id + 11, "HHSuccessKCDDecrease", new(0f, 180f, 0.5f), 5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Huntsman])
            .SetValueFormat(OptionFormat.Seconds);
        FailureKillCooldown = FloatOptionItem.Create(Id + 12, "HHFailureKCDIncrease", new(0f, 180f, 0.5f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Huntsman])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 13, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Huntsman]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 14, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Huntsman]);
        NumOfTargets = IntegerOptionItem.Create(Id + 15, "HHNumOfTargets", new(0, 10, 1), 3, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Huntsman])
            .SetValueFormat(OptionFormat.Times);
        MaxKCD = FloatOptionItem.Create(Id + 16, "HHMaxKCD", new(0f, 180f, 2.5f), 60f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Huntsman])
            .SetValueFormat(OptionFormat.Seconds);
        MinKCD = FloatOptionItem.Create(Id + 17, "HHMinKCD", new(0f, 180f, 2.5f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Huntsman])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public override void Init()
    {
        playerIdList = [];
        Targets = [];
    }
    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        _ = new LateTask(() =>
        {
            ResetTargets(isStartedGame: true);
        }, 8f, "Huntsman Reset Targets");

        KCD = KillCooldown.GetFloat();

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    public override void OnReportDeadBody(PlayerControl Ronaldo, PlayerControl IsTheGoat)
    {
        ResetTargets(isStartedGame: false);
    }
    public override bool OnCheckMurderAsKiller(PlayerControl killer, PlayerControl target)
    {
        float tempkcd = KCD;
        if (Targets.Contains(target.PlayerId)) Math.Clamp(KCD -= SuccessKillCooldown.GetFloat(), MinKCD.GetFloat(), MaxKCD.GetFloat());
        else Math.Clamp(KCD += FailureKillCooldown.GetFloat(), MinKCD.GetFloat(), MaxKCD.GetFloat());
        if (KCD != tempkcd)
        {
            killer.ResetKillCooldown();
            killer.SyncSettings();
        }
        return true;
    }
    public override bool CanUseImpostorVentButton(PlayerControl pc) => Huntsman.CanVent.GetBool();
    public override bool CanUseKillButton(PlayerControl pc) => pc.IsAlive();
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KCD;
    public override string GetLowerText(PlayerControl player, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        var targetId = player.PlayerId;
        string output = string.Empty;
        for (int i = 0; i < Targets.Count; i++) { byte playerId = Targets[i]; if (i != 0) output += ", "; output += Utils.GetPlayerById(playerId).GetRealName(); }
        return targetId != 0xff ? GetString("Targets") + $"<b><color=#ff1919>{output}</color></b>" : string.Empty;
    }
    public static void SendRPC(bool isSetTarget, byte targetId = byte.MaxValue)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncHuntsmanTarget, SendOption.Reliable, -1);
        writer.Write(isSetTarget);
        if (isSetTarget)
        {
            writer.Write(targetId);
        }
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        bool isSetTarget = reader.ReadBoolean();
        if (!isSetTarget)
        {
            Targets.Clear();
            return;
        }
        byte targetId = reader.ReadByte();
        Targets.Add(targetId);
    }
    private static void ResetTargets(bool isStartedGame = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        Targets.Clear();
        SendRPC(isSetTarget: false);

        int potentialTargetCount = Main.AllAlivePlayerControls.Length - 1;
        if (potentialTargetCount < 0) potentialTargetCount = 0;
        int maxLimit = Math.Min(potentialTargetCount, NumOfTargets.GetInt());
        for (var i = 0; i < maxLimit; i++)
        {
            try
            {
                var cTargets = new List<PlayerControl>(Main.AllAlivePlayerControls.Where(pc => !Targets.Contains(pc.PlayerId) && pc.GetCustomRole() != CustomRoles.Huntsman));
                var rand = IRandom.Instance;
                var target = cTargets[rand.Next(0, cTargets.Count)];
                var targetId = target.PlayerId;
                Targets.Add(targetId);
                SendRPC(isSetTarget: true, targetId: targetId);

            }
            catch (Exception ex)
            {
                Logger.Warn($"Not enough targets for Head Hunter could be assigned. This may be due to a low player count or the following error:\n\n{ex}", "HuntsmanAssignTargets");
                break;
            }
        }

        if (isStartedGame)
            Utils.NotifyRoles(ForceLoop: true);
    }
    public override string PlayerKnowTargetColor(PlayerControl seer, PlayerControl target)
        => seer.Is(CustomRoles.Huntsman) && Targets.Contains(target.PlayerId) ? "6e5524" : "";
}
