using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace SoftFlesh
{
	// Token: 0x02000002 RID: 2
	public static class SFStallSpy
	{
		// Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
		public static void Apply()
		{
			if (SFStallSpy._h != null)
			{
				return;
			}
			try
			{
				SFStallSpy._h = new Harmony("poison.softflesh.stallspy");
				MethodInfo target = typeof(Resources).GetMethod("UnloadUnusedAssets", BindingFlags.Static | BindingFlags.Public);
				SFStallSpy._h.Patch(target, new HarmonyMethod(typeof(SFStallSpy).GetMethod("UnloadPrefix")), null, null, null, null);
				MethodInfo cvPerform = typeof(CanvasUpdateRegistry).GetMethod("PerformUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
				if (cvPerform != null)
				{
					SFStallSpy._h.Patch(cvPerform, new HarmonyMethod(typeof(SFStallSpy).GetMethod("CanvasPrefix")), null, null, null, null);
				}
				SoftFleshPlugin.Log("[SF][spy] watching Resources.UnloadUnusedAssets");
				SFStallSpy.PatchLateUpdates();
			}
			catch (Exception e)
			{
				SoftFleshPlugin.Log("[SF][spy] hook failed: " + e.Message);
				SFStallSpy._h = null;
			}
		}

		// Token: 0x06000002 RID: 2 RVA: 0x00002140 File Offset: 0x00000340
		public static void Remove()
		{
			if (SFStallSpy._h == null)
			{
				return;
			}
			try
			{
				SFStallSpy._h.UnpatchSelf();
			}
			catch
			{
			}
			SFStallSpy._h = null;
		}

		// Token: 0x06000003 RID: 3 RVA: 0x0000217C File Offset: 0x0000037C
		public static void CanvasPrefix(CanvasUpdateRegistry __instance)
		{
			int fc = Time.frameCount;
			int lo = SoftFleshController.PredLo;
			int hi = SoftFleshController.PredHi;
			if (lo < 0)
			{
				return;
			}
			if (fc > hi)
			{
				if (SFStallSpy._cvAggLo == lo && SFStallSpy._cvAgg.Count > 0)
				{
					SFStallSpy._cvAggLo = -1;
					StringBuilder sb = new StringBuilder(300);
					sb.Append("window f").Append(lo).Append("-f")
						.Append(hi)
						.Append(": ");
					for (int pass = 0; pass < 2; pass++)
					{
						foreach (KeyValuePair<string, int> kv in SFStallSpy._cvAgg)
						{
							if (!((pass == 0) ? (kv.Value > 5) : (kv.Value <= 5)))
							{
								if (sb.Length > 380)
								{
									sb.Append(" ...");
									break;
								}
								sb.Append(kv.Key).Append(" x").Append(kv.Value)
									.Append("  ");
							}
						}
					}
					SoftFleshController.CvBurst = sb.ToString();
					SoftFleshPlugin.Log("[SF][spy] canvas ambush " + SoftFleshController.CvBurst);
					SFStallSpy._cvAgg.Clear();
				}
				return;
			}
			if (fc < lo)
			{
				return;
			}
			try
			{
				if (SFStallSpy._cvAggLo != lo)
				{
					SFStallSpy._cvAggLo = lo;
					SFStallSpy._cvAgg.Clear();
					Canvas[] roots = UnityEngine.Object.FindObjectsOfType<Canvas>();
					List<RectTransform> list = new List<RectTransform>(1024);
					for (int r = 0; r < roots.Length; r++)
					{
						if (!(roots[r] == null) && roots[r].isRootCanvas)
						{
							RectTransform[] rts = roots[r].GetComponentsInChildren<RectTransform>(true);
							for (int i = 0; i < rts.Length; i++)
							{
								rts[i].hasChanged = false;
								list.Add(rts[i]);
							}
						}
					}
					SFStallSpy._rtCache = list.ToArray();
				}
				if (SFStallSpy._rtCache != null)
				{
					for (int j = 0; j < SFStallSpy._rtCache.Length; j++)
					{
						RectTransform rt = SFStallSpy._rtCache[j];
						if (!(rt == null) && rt.hasChanged)
						{
							rt.hasChanged = false;
							string mk = "[T]" + ((rt.parent != null) ? (rt.parent.name + "/") : "") + rt.name;
							int mn;
							SFStallSpy._cvAgg.TryGetValue(mk, out mn);
							SFStallSpy._cvAgg[mk] = mn + 1;
						}
					}
				}
				if (SFStallSpy._cvLQ == null)
				{
					SFStallSpy._cvLQ = typeof(CanvasUpdateRegistry).GetField("m_LayoutRebuildQueue", BindingFlags.Instance | BindingFlags.NonPublic);
					SFStallSpy._cvGQ = typeof(CanvasUpdateRegistry).GetField("m_GraphicRebuildQueue", BindingFlags.Instance | BindingFlags.NonPublic);
				}
				IList<ICanvasElement> lq = ((SFStallSpy._cvLQ != null) ? (SFStallSpy._cvLQ.GetValue(__instance) as IList<ICanvasElement>) : null);
				IList<ICanvasElement> gq = ((SFStallSpy._cvGQ != null) ? (SFStallSpy._cvGQ.GetValue(__instance) as IList<ICanvasElement>) : null);
				for (int q = 0; q < 2; q++)
				{
					IList<ICanvasElement> src = ((q == 0) ? lq : gq);
					if (src != null)
					{
						for (int k = 0; k < src.Count; k++)
						{
							ICanvasElement el = src[k];
							Transform tr = ((el != null) ? el.transform : null);
							if (!(tr == null))
							{
								string key = ((q == 0) ? "[L]" : "[G]") + ((tr.parent != null) ? (tr.parent.name + "/") : "") + tr.name;
								int l;
								SFStallSpy._cvAgg.TryGetValue(key, out l);
								SFStallSpy._cvAgg[key] = l + 1;
							}
						}
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x06000004 RID: 4 RVA: 0x000025B8 File Offset: 0x000007B8
		private static void PatchLateUpdates()
		{
			int patched = 0;
			int failed = 0;
			HarmonyMethod pre = new HarmonyMethod(typeof(SFStallSpy).GetMethod("LuPrefix"));
			HarmonyMethod post = new HarmonyMethod(typeof(SFStallSpy).GetMethod("LuPostfix"));
			foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
			{
				string an = asm.GetName().Name;
				if (!an.StartsWith("SoftFlesh") && !an.StartsWith("Unity") && !an.StartsWith("System") && !(an == "mscorlib") && !an.StartsWith("0Harmony") && !an.StartsWith("Mono") && !an.StartsWith("BepInEx") && !an.StartsWith("netstandard") && !an.StartsWith("HarmonyX"))
				{
					Type[] types;
					try
					{
						types = asm.GetTypes();
					}
					catch (ReflectionTypeLoadException e)
					{
						types = e.Types;
					}
					catch
					{
						goto IL_01B2;
					}
					foreach (Type ty in types)
					{
						if (!(ty == null) && !ty.IsAbstract && !ty.IsGenericTypeDefinition && typeof(MonoBehaviour).IsAssignableFrom(ty))
						{
							MethodInfo j = ty.GetMethod("LateUpdate", BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
							if (!(j == null) && !j.IsAbstract)
							{
								try
								{
									SFStallSpy._h.Patch(j, pre, post, null, null, null);
									patched++;
								}
								catch
								{
									failed++;
								}
							}
						}
					}
				}
				IL_01B2:;
			}
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF][spy] stopwatch on ",
				patched.ToString(),
				" LateUpdate method(s), ",
				failed.ToString(),
				" unpatchable"
			}));
		}

		// Token: 0x06000005 RID: 5 RVA: 0x000027EC File Offset: 0x000009EC
		public static string LateBudget()
		{
			if (SFStallSpy._luFrames < 1 || SFStallSpy._luMs.Count == 0)
			{
				return "(no samples yet)";
			}
			List<KeyValuePair<Type, double>> list = new List<KeyValuePair<Type, double>>(SFStallSpy._luMs);
			list.Sort((KeyValuePair<Type, double> x, KeyValuePair<Type, double> y) => y.Value.CompareTo(x.Value));
			StringBuilder sb = new StringBuilder(300);
			int shown = 0;
			int i = 0;
			while (i < list.Count && shown < 8)
			{
				float msf = (float)(list[i].Value / (double)SFStallSpy._luFrames);
				if (msf < 0.05f)
				{
					break;
				}
				int cnt = SFStallSpy._luN[list[i].Key] / SFStallSpy._luFrames;
				if (sb.Length > 0)
				{
					sb.Append("   ");
				}
				sb.Append(list[i].Key.Name).Append(' ').Append(msf.ToString("F1"))
					.Append("ms");
				if (cnt > 1)
				{
					sb.Append('x').Append(cnt);
				}
				shown++;
				i++;
			}
			SFStallSpy._luMs.Clear();
			SFStallSpy._luN.Clear();
			SFStallSpy._luFrames = 0;
			if (sb.Length <= 0)
			{
				return "(all under 0.05ms/frame)";
			}
			return sb.ToString();
		}

		// Token: 0x06000006 RID: 6 RVA: 0x0000294E File Offset: 0x00000B4E
		public static void LuPrefix(out long __state)
		{
			__state = Stopwatch.GetTimestamp();
		}

		// Token: 0x06000007 RID: 7 RVA: 0x00002958 File Offset: 0x00000B58
		public static void LuPostfix(object __instance, long __state)
		{
			long dt = Stopwatch.GetTimestamp() - __state;
			double ms = (double)dt * 1000.0 / (double)Stopwatch.Frequency;
			if (__instance == null)
			{
				return;
			}
			int fc = Time.frameCount;
			if (fc != SFStallSpy._luLastFrame)
			{
				SFStallSpy._luLastFrame = fc;
				SFStallSpy._luFrames++;
			}
			Type ty = __instance.GetType();
			double cur;
			SFStallSpy._luMs.TryGetValue(ty, out cur);
			SFStallSpy._luMs[ty] = cur + ms;
			int cn;
			SFStallSpy._luN.TryGetValue(ty, out cn);
			SFStallSpy._luN[ty] = cn + 1;
			if (ms < 5.0)
			{
				return;
			}
			SFStallSpy.SlowLog = string.Concat(new string[]
			{
				"f",
				Time.frameCount.ToString(),
				" ",
				__instance.GetType().FullName,
				" ",
				ms.ToString("F0"),
				"ms",
				(SFStallSpy.SlowLog.Length > 0) ? ("   |   " + SFStallSpy.SlowLog) : ""
			});
			if (SFStallSpy.SlowLog.Length > 300)
			{
				SFStallSpy.SlowLog = SFStallSpy.SlowLog.Substring(0, 300);
			}
		}

		// Token: 0x06000008 RID: 8 RVA: 0x00002AA0 File Offset: 0x00000CA0
		public static void UnloadPrefix()
		{
			SFStallSpy.LastUnloadFrame = Time.frameCount;
			SFStallSpy.UnloadCount++;
			string who = "?";
			try
			{
				StackTrace stk = new StackTrace(1, false);
				for (int i = 0; i < stk.FrameCount; i++)
				{
					MethodBase mth = stk.GetFrame(i).GetMethod();
					if (!(mth == null) && !(mth.DeclaringType == null))
					{
						string asm = mth.DeclaringType.Assembly.GetName().Name;
						if (!(asm == "UnityEngine") && !(asm == "mscorlib") && !asm.StartsWith("System") && !asm.StartsWith("0Harmony") && !asm.StartsWith("SoftFlesh"))
						{
							who = string.Concat(new string[]
							{
								asm,
								" : ",
								mth.DeclaringType.Name,
								".",
								mth.Name
							});
							break;
						}
					}
				}
			}
			catch
			{
			}
			SFStallSpy.Recent = string.Concat(new string[]
			{
				"f",
				Time.frameCount.ToString(),
				" by ",
				who,
				(SFStallSpy.Recent.Length > 0) ? ("   |   " + SFStallSpy.Recent) : ""
			});
			if (SFStallSpy.Recent.Length > 240)
			{
				SFStallSpy.Recent = SFStallSpy.Recent.Substring(0, 240);
			}
			SoftFleshPlugin.Log("[SF][spy] Resources.UnloadUnusedAssets by " + who + " at frame " + Time.frameCount.ToString());
		}

		// Token: 0x04000001 RID: 1
		public static int UnloadCount;

		// Token: 0x04000002 RID: 2
		public static int LastUnloadFrame = -99999;

		// Token: 0x04000003 RID: 3
		public static string Recent = "";

		// Token: 0x04000004 RID: 4
		private static Harmony _h;

		// Token: 0x04000005 RID: 5
		private static FieldInfo _cvLQ;

		// Token: 0x04000006 RID: 6
		private static FieldInfo _cvGQ;

		// Token: 0x04000007 RID: 7
		private static readonly Dictionary<string, int> _cvAgg = new Dictionary<string, int>();

		// Token: 0x04000008 RID: 8
		private static RectTransform[] _rtCache;

		// Token: 0x04000009 RID: 9
		private static int _cvAggLo = -1;

		// Token: 0x0400000A RID: 10
		public static string SlowLog = "";

		// Token: 0x0400000B RID: 11
		private static readonly Dictionary<Type, double> _luMs = new Dictionary<Type, double>();

		// Token: 0x0400000C RID: 12
		private static readonly Dictionary<Type, int> _luN = new Dictionary<Type, int>();

		// Token: 0x0400000D RID: 13
		private static int _luFrames;

		// Token: 0x0400000E RID: 14
		private static int _luLastFrame = -1;
	}
}
