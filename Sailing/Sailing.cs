using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;
using JetBrains.Annotations;
using SkillManager;
using UnityEngine;

namespace Sailing;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class Sailing : BaseUnityPlugin
{
	private const string ModName = "Sailing";
	private const string ModVersion = "1.0.0";
	private const string ModGUID = "org.bepinex.plugins.sailing";

	private static readonly Skill sailing = new("Sailing", "sailing.png");

	public void Awake()
	{
		sailing.Description.English("Increases the health of ships built by you, sailing speed of ships commanded by you and your exploration radius while on a ship.");
		sailing.Name.German("Segeln");
		sailing.Description.German("Erhöht die Lebenspunkte von dir gebauter Schiffe, erhöht die Geschwindigkeiten von Schiffen, die du steuerst und erhöht deinen Erkundungsradius, wenn du dich auf einem Schiff befindest.");
		sailing.Configurable = true;

		Assembly assembly = Assembly.GetExecutingAssembly();
		Harmony harmony = new(ModGUID);
		harmony.PatchAll(assembly);
	}

	[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.OnPlaced))]
	private class AddZDO
	{
		[UsedImplicitly]
		private static void Postfix(WearNTear __instance)
		{
			if (__instance.GetComponent<Ship>())
			{
				__instance.GetComponent<ZNetView>().GetZDO().Set("Sailing Skill Level", Player.m_localPlayer.GetSkillFactor("Sailing"));
				__instance.m_health *= 1 + __instance.GetComponent<ZNetView>().GetZDO().GetFloat("Sailing Skill Level") * 2f;
				Player.m_localPlayer.RaiseSkill("Sailing", 35f);
			}
		}
	}

	[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Awake))]
	private class IncreaseHealth
	{
		[UsedImplicitly]
		private static void Prefix(WearNTear __instance)
		{
			__instance.m_health *= 1 + (__instance.GetComponent<ZNetView>().GetZDO()?.GetFloat("Sailing Skill Level") ?? (__instance.GetComponent<Ship>() ? Player.m_localPlayer.GetSkillFactor("Sailing") : 1)) * 2f;
		}
	}

	[HarmonyPatch(typeof(Minimap), nameof(Minimap.Explore), typeof(Vector3), typeof(float))]
	private class IncreaseExplorationRadius
	{
		[UsedImplicitly]
		private static void Prefix(Minimap __instance, ref float radius)
		{
			if (Player.m_localPlayer is { m_attached: true, m_attachedToShip: true } player)
			{
				radius *= 1 + player.GetSkillFactor("Sailing") * 4;
			}
		}
	}

	[HarmonyPatch(typeof(Ship), nameof(Ship.GetSailForce))]
	private class ChangeShipSpeed
	{
		[UsedImplicitly]
		private class Timer
		{
			public float UpdateDelta = 0;
			public float lastUpdate = Time.fixedTime;
		}

		private static readonly ConditionalWeakTable<Ship, Timer> timers = new();

		private static void Postfix(Ship __instance, ref Vector3 __result)
		{
			Timer timer = timers.GetOrCreateValue(__instance);
			if (Player.m_players.FirstOrDefault(p => p.GetZDOID() == __instance.m_shipControlls.GetUser()) is { } sailor)
			{
				__result *= 1 + sailor.m_nview.GetZDO().GetFloat("Sailing Skill") * sailing.SkillEffectFactor;
				if (__instance.m_speed is not Ship.Speed.Stop)
				{
					timer.UpdateDelta += Time.fixedTime - timer.lastUpdate;
					if (timer.UpdateDelta > 1)
					{
						sailor.m_nview.InvokeRPC("Sailing Skill Increase", 1);
						timer.UpdateDelta -= 1;
					}
				}
			}
			timer.lastUpdate = Time.fixedTime;
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Awake))]
	private class ExposeSailingSkill
	{
		private static void Postfix(Player __instance)
		{
			__instance.m_nview.Register("Sailing Skill Increase", (long _, int amount) => __instance.RaiseSkill("Sailing", amount));
		}
	}

	[HarmonyPatch(typeof(Player), nameof(Player.Update))]
	public class PlayerUpdate
	{
		private static void Postfix(Player __instance)
		{
			if (__instance == Player.m_localPlayer)
			{
				__instance.m_nview.GetZDO().Set("Sailing Skill", __instance.GetSkillFactor(Skill.fromName("Sailing")));
			}
		}
	}
}
