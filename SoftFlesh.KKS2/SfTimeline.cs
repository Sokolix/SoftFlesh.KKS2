using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using Studio;
using UnityEngine;

namespace SoftFlesh
{
	// Token: 0x02000007 RID: 7
	internal static class SfTimeline
	{
		// Token: 0x17000029 RID: 41
		// (get) Token: 0x060001A4 RID: 420 RVA: 0x000559EB File Offset: 0x00053BEB
		public static bool Registered
		{
			get
			{
				return SfTimeline._registered;
			}
		}

		// Token: 0x1700002A RID: 42
		// (get) Token: 0x060001A5 RID: 421 RVA: 0x000559F2 File Offset: 0x00053BF2
		public static bool CanKey
		{
			get
			{
				SfTimeline.FindTypes();
				return SfTimeline._registered && SfTimeline._ctorInterp != null && SfTimeline._ctorKeyframe != null;
			}
		}

		// Token: 0x060001A6 RID: 422 RVA: 0x00055A1C File Offset: 0x00053C1C
		public static void PublishBridge()
		{
			AppDomain dom = AppDomain.CurrentDomain;
			dom.SetData("SoftFlesh.TL.compat", new Func<ObjectCtrlInfo, bool>(SfTimeline.BridgeCompat));
			dom.SetData("SoftFlesh.TL.compat2", new Func<ObjectCtrlInfo, string, bool>(SfTimeline.BridgeCompat2));
			dom.SetData("SoftFlesh.TL.compatTab", new Func<ObjectCtrlInfo, bool>(SfTimeline.BridgeCompatTab));
			dom.SetData("SoftFlesh.TL.get", new Func<ObjectCtrlInfo, string, float>(SfTimeline.BridgeGet));
			dom.SetData("SoftFlesh.TL.set", new Action<ObjectCtrlInfo, string, float>(SfTimeline.BridgeSet));
			dom.SetData("SoftFlesh.TL.getTab", new Func<ObjectCtrlInfo, string, Dictionary<string, float>>(SfTimeline.BridgeGetTab));
			dom.SetData("SoftFlesh.TL.setTab", new Action<ObjectCtrlInfo, string, Dictionary<string, float>>(SfTimeline.BridgeSetTab));
		}

		// Token: 0x060001A7 RID: 423 RVA: 0x00055AD0 File Offset: 0x00053CD0
		private static ChaControl CharOf(ObjectCtrlInfo oci)
		{
			OCIChar oc = oci as OCIChar;
			if (oc == null)
			{
				return null;
			}
			return oc.charInfo;
		}

		// Token: 0x060001A8 RID: 424 RVA: 0x00055AF0 File Offset: 0x00053CF0
		internal static ObjectCtrlInfo OciOf(ChaControl cha)
		{
			try
			{
				global::Studio.Studio st = Singleton<global::Studio.Studio>.Instance;
				if (st == null || cha == null)
				{
					return null;
				}
				foreach (KeyValuePair<int, ObjectCtrlInfo> kv in st.dicObjectCtrl)
				{
					OCIChar oc = kv.Value as OCIChar;
					if (oc != null && oc.charInfo == cha)
					{
						return oc;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		// Token: 0x060001A9 RID: 425 RVA: 0x00055B94 File Offset: 0x00053D94
		private static bool BridgeCompat(ObjectCtrlInfo oci)
		{
			SoftFleshController c = SoftFleshPlugin.LiveController;
			ChaControl cha = SfTimeline.CharOf(oci);
			return c != null && cha != null && c.HasChar(cha);
		}

		// Token: 0x060001AA RID: 426 RVA: 0x00055BCC File Offset: 0x00053DCC
		private static bool BridgeCompat2(ObjectCtrlInfo oci, string id)
		{
			SoftFleshController c = SoftFleshPlugin.LiveController;
			ChaControl cha = SfTimeline.CharOf(oci);
			if (c == null || cha == null)
			{
				return false;
			}
			if (!(id == "faceOn"))
			{
				return c.HasChar(cha);
			}
			return c.HasFace(cha);
		}

		// Token: 0x060001AB RID: 427 RVA: 0x00055C16 File Offset: 0x00053E16
		private static bool BridgeCompatTab(ObjectCtrlInfo oci)
		{
			return SoftFleshPlugin.LiveController != null;
		}

		// Token: 0x060001AC RID: 428 RVA: 0x00055C24 File Offset: 0x00053E24
		private static float BridgeGet(ObjectCtrlInfo oci, string id)
		{
			SoftFleshController c = SoftFleshPlugin.LiveController;
			ChaControl cha = SfTimeline.CharOf(oci);
			if (c == null || cha == null)
			{
				return 0f;
			}
			if (!((id == "faceOn") ? c.GetFaceEnabled(cha) : c.GetCharEnabled(cha)))
			{
				return 0f;
			}
			return 1f;
		}

		// Token: 0x060001AD RID: 429 RVA: 0x00055C80 File Offset: 0x00053E80
		private static void BridgeSet(ObjectCtrlInfo oci, string id, float v)
		{
			SoftFleshController c = SoftFleshPlugin.LiveController;
			ChaControl cha = SfTimeline.CharOf(oci);
			if (c == null || cha == null)
			{
				return;
			}
			string hk = id + "#" + oci.GetHashCode().ToString();
			float last;
			bool had = SfTimeline._lastV.TryGetValue(hk, out last);
			SfTimeline._lastV[hk] = v;
			bool on;
			if (v >= 0.999f)
			{
				on = true;
			}
			else if (v <= 0.001f)
			{
				on = false;
			}
			else if (had && v - last > 0.5f)
			{
				on = true;
			}
			else
			{
				if (!had || last - v <= 0.5f)
				{
					return;
				}
				on = false;
			}
			if (id == "faceOn")
			{
				c.SetFaceEnabled(cha, on);
				return;
			}
			c.SetCharEnabled(cha, on);
		}

		// Token: 0x060001AE RID: 430 RVA: 0x00055D44 File Offset: 0x00053F44
		private static Dictionary<string, float> BridgeGetTab(ObjectCtrlInfo oci, string id)
		{
			return SoftFleshPlugin.TabValues(id);
		}

		// Token: 0x060001AF RID: 431 RVA: 0x00055D4C File Offset: 0x00053F4C
		private static void BridgeSetTab(ObjectCtrlInfo oci, string id, Dictionary<string, float> v)
		{
			SoftFleshPlugin.ApplyTabValues(id, v);
		}

		// Token: 0x060001B0 RID: 432 RVA: 0x00055D58 File Offset: 0x00053F58
		private static void FindTypes()
		{
			if (SfTimeline._typesFound)
			{
				return;
			}
			SfTimeline._typesFound = true;
			try
			{
				Type tc = null;
				Type a5open = null;
				Type tl = null;
				foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
				{
					if (tc == null)
					{
						try
						{
							tc = asm.GetType("ToolBox.TimelineCompatibility");
						}
						catch
						{
						}
					}
					if (a5open == null)
					{
						try
						{
							a5open = asm.GetType("ToolBox.Extensions.Action`5");
						}
						catch
						{
						}
					}
					if (tl == null)
					{
						try
						{
							tl = asm.GetType("Timeline.Timeline");
						}
						catch
						{
						}
					}
					if (tc != null && a5open != null && tl != null)
					{
						break;
					}
				}
				if (tc == null || a5open == null)
				{
					SoftFleshPlugin.Log("[SF] Timeline not found - keyframing disabled");
				}
				else
				{
					SfTimeline._action5 = a5open.MakeGenericType(new Type[]
					{
						typeof(ObjectCtrlInfo),
						typeof(object),
						typeof(object),
						typeof(object),
						typeof(float)
					});
					foreach (MethodInfo i in tc.GetMethods(BindingFlags.Static | BindingFlags.Public))
					{
						if (i.Name == "AddInterpolableModelDynamic" && i.GetParameters().Length == 16)
						{
							SfTimeline._add = i;
							break;
						}
					}
					if (SfTimeline._add != null)
					{
						SfTimeline._addParams = SfTimeline._add.GetParameters();
						for (int j = 0; j < SfTimeline._addParams.Length; j++)
						{
							if (SfTimeline._addParams[j].Name == "useOciInHash")
							{
								SfTimeline._idxUseOci = j;
							}
						}
					}
					SfTimeline._init = tc.GetMethod("Init", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
					SfTimeline._fInterpDeleg = tc.GetField("_interpolableDelegate", BindingFlags.Static | BindingFlags.NonPublic);
					SfTimeline._fRefresh = tc.GetField("_refreshInterpolablesList", BindingFlags.Static | BindingFlags.NonPublic);
					if (tl != null)
					{
						Assembly asmTl = tl.Assembly;
						SfTimeline._tInterp = asmTl.GetType("Timeline.Interpolable");
						SfTimeline._tModel = asmTl.GetType("Timeline.InterpolableModel");
						SfTimeline._tKeyframe = asmTl.GetType("Timeline.Keyframe");
						SfTimeline._fSelf = tl.GetField("_self", BindingFlags.Static | BindingFlags.NonPublic);
						SfTimeline._fInterpolables = tl.GetField("_interpolables", BindingFlags.Instance | BindingFlags.NonPublic);
						SfTimeline._fInterpTree = tl.GetField("_interpolablesTree", BindingFlags.Instance | BindingFlags.NonPublic);
						SfTimeline._fModelsList = tl.GetField("_interpolableModelsList", BindingFlags.Instance | BindingFlags.NonPublic);
						SfTimeline._refreshList = tl.GetMethod("RefreshInterpolablesList", BindingFlags.Static | BindingFlags.Public);
						SfTimeline._pPlaybackTime = tl.GetProperty("playbackTime", BindingFlags.Static | BindingFlags.Public);
						if (SfTimeline._tInterp != null && SfTimeline._tModel != null && SfTimeline._tKeyframe != null)
						{
							SfTimeline._fOci = SfTimeline._tInterp.GetField("oci");
							SfTimeline._fKeyframes = SfTimeline._tInterp.GetField("keyframes");
							SfTimeline._fOwner = SfTimeline._tModel.GetField("owner");
							SfTimeline._fId = SfTimeline._tModel.GetField("id");
							SfTimeline._ctorInterp = SfTimeline._tInterp.GetConstructor(new Type[]
							{
								typeof(ObjectCtrlInfo),
								SfTimeline._tModel
							});
							SfTimeline._ctorKeyframe = SfTimeline._tKeyframe.GetConstructor(new Type[]
							{
								typeof(object),
								SfTimeline._tInterp,
								typeof(AnimationCurve)
							});
						}
					}
				}
			}
			catch (Exception e)
			{
				SoftFleshPlugin.Log("[SF] Timeline FindTypes failed: " + e.Message);
			}
		}

		// Token: 0x060001B1 RID: 433 RVA: 0x0005616C File Offset: 0x0005436C
		private static bool EnsureReady()
		{
			SfTimeline.FindTypes();
			if (SfTimeline._add == null || SfTimeline._init == null)
			{
				return false;
			}
			if (SfTimeline._ready)
			{
				return true;
			}
			try
			{
				SfTimeline._init.Invoke(null, null);
				SfTimeline._ready = SfTimeline._fInterpDeleg != null && SfTimeline._fInterpDeleg.GetValue(null) != null;
			}
			catch
			{
			}
			return SfTimeline._ready;
		}

		// Token: 0x060001B2 RID: 434 RVA: 0x000561F0 File Offset: 0x000543F0
		public static void RegisterOnce()
		{
			if (SfTimeline._registered)
			{
				return;
			}
			if (AppDomain.CurrentDomain.GetData("SoftFlesh.TL.registered") != null)
			{
				SfTimeline._registered = true;
				if (SfTimeline.EnsureReady())
				{
					SfTimeline.EnsureModels(false);
				}
				return;
			}
			if (!SfTimeline.EnsureReady())
			{
				return;
			}
			SfTimeline._registered = true;
			AppDomain.CurrentDomain.SetData("SoftFlesh.TL.registered", "1");
			SfTimeline.Add("on", "SoftFlesh On", 0);
			SfTimeline.EnsureModels(true);
			SoftFleshPlugin.Log("[SF] Timeline keyframing ready (tracks: SoftFlesh On / Face On - 1 = on, 0 = off, between = hold; one track per tab holding the page's values)");
			SfTimeline.Refresh();
		}

		// Token: 0x060001B3 RID: 435 RVA: 0x00056274 File Offset: 0x00054474
		private static void EnsureModels(bool first)
		{
			if (SfTimeline._modelsChecked)
			{
				return;
			}
			SfTimeline._modelsChecked = true;
			if (!first && SfTimeline._fModelsList == null)
			{
				return;
			}
			int added = 0;
			if (first || SfTimeline.FindModel("faceOn") == null)
			{
				SfTimeline.Add("faceOn", "SoftFlesh Face On", 1);
				added++;
			}
			for (int t = 0; t < SfTimeline.TabIds.Length; t++)
			{
				if (first || SfTimeline.FindModel(SfTimeline.TabIds[t]) == null)
				{
					SfTimeline.Add(SfTimeline.TabIds[t], SfTimeline.TabNames[t], 2);
					added++;
				}
			}
			if (added > 0 && !first)
			{
				SoftFleshPlugin.Log("[SF] Timeline: " + added.ToString() + " track(s) added by this build registered");
				SfTimeline.Refresh();
			}
		}

		// Token: 0x060001B4 RID: 436 RVA: 0x00056328 File Offset: 0x00054528
		public static void Refresh()
		{
			try
			{
				FieldInfo fRefresh = SfTimeline._fRefresh;
				Delegate @delegate = ((fRefresh != null) ? fRefresh.GetValue(null) : null) as Delegate;
				if (@delegate != null)
				{
					@delegate.DynamicInvoke(Array.Empty<object>());
				}
			}
			catch
			{
			}
		}

		// Token: 0x060001B5 RID: 437 RVA: 0x00056374 File Offset: 0x00054574
		private static void Add(string id, string name, int kind)
		{
			try
			{
				Delegate interpBefore;
				Func<ObjectCtrlInfo, bool> isCompat;
				Func<ObjectCtrlInfo, object, object> getValue;
				Func<object, XmlNode, object> readXml;
				Action<object, XmlTextWriter, object> writeXml;
				Func<ObjectCtrlInfo, object> getParam;
				if (kind == 2)
				{
					SfTimeline.TabParam tp = new SfTimeline.TabParam(id);
					interpBefore = Delegate.CreateDelegate(SfTimeline._action5, tp, typeof(SfTimeline.TabParam).GetMethod("InterpolateBefore"));
					isCompat = new Func<ObjectCtrlInfo, bool>(tp.IsCompatible);
					getValue = new Func<ObjectCtrlInfo, object, object>(tp.GetValue);
					readXml = new Func<object, XmlNode, object>(tp.ReadXml);
					writeXml = new Action<object, XmlTextWriter, object>(tp.WriteXml);
					getParam = new Func<ObjectCtrlInfo, object>(tp.GetParameter);
				}
				else
				{
					SfTimeline.Param sp = new SfTimeline.Param(id, kind == 1);
					interpBefore = Delegate.CreateDelegate(SfTimeline._action5, sp, typeof(SfTimeline.Param).GetMethod("InterpolateBefore"));
					isCompat = new Func<ObjectCtrlInfo, bool>(sp.IsCompatible);
					getValue = new Func<ObjectCtrlInfo, object, object>(sp.GetValue);
					readXml = new Func<object, XmlNode, object>(sp.ReadXml);
					writeXml = new Action<object, XmlTextWriter, object>(sp.WriteXml);
					getParam = new Func<ObjectCtrlInfo, object>(sp.GetParameter);
				}
				object[] args = new object[16];
				args[0] = "SoftFlesh";
				args[1] = id;
				args[2] = name;
				args[3] = interpBefore;
				args[4] = null;
				args[5] = isCompat;
				args[6] = getValue;
				args[7] = readXml;
				args[8] = writeXml;
				args[9] = getParam;
				for (int i = 10; i < 16; i++)
				{
					object def = SfTimeline._addParams[i].DefaultValue;
					args[i] = ((def == DBNull.Value || def == Type.Missing) ? null : def);
				}
				if (kind == 2)
				{
					if (SfTimeline._idxUseOci >= 0)
					{
						args[SfTimeline._idxUseOci] = false;
					}
					else
					{
						SoftFleshPlugin.Log("[SF] Timeline: no useOciInHash parameter - the tab track '" + id + "' will be per object");
					}
				}
				SfTimeline._add.Invoke(null, args);
			}
			catch (Exception e)
			{
				TargetInvocationException ex = e as TargetInvocationException;
				Exception inner = ((ex != null) ? ex.InnerException : null) ?? e;
				SoftFleshPlugin.Log(string.Concat(new string[]
				{
					"[SF] Timeline Add '",
					id,
					"' failed: ",
					inner.GetType().Name,
					": ",
					inner.Message
				}));
			}
		}

		// Token: 0x060001B6 RID: 438 RVA: 0x000565B8 File Offset: 0x000547B8
		private static object FindInstance(ObjectCtrlInfo oci, string id)
		{
			object self = ((SfTimeline._fSelf != null) ? SfTimeline._fSelf.GetValue(null) : null);
			if (self == null)
			{
				return null;
			}
			IDictionary dict = SfTimeline._fInterpolables.GetValue(self) as IDictionary;
			if (dict == null)
			{
				return null;
			}
			foreach (object obj in dict)
			{
				object it = ((DictionaryEntry)obj).Value;
				if (it != null && SfTimeline._fOci.GetValue(it) == oci && !((string)SfTimeline._fOwner.GetValue(it) != "SoftFlesh") && !((string)SfTimeline._fId.GetValue(it) != id))
				{
					return it;
				}
			}
			return null;
		}

		// Token: 0x060001B7 RID: 439 RVA: 0x0005669C File Offset: 0x0005489C
		private static object FindModel(string id)
		{
			object self = ((SfTimeline._fSelf != null) ? SfTimeline._fSelf.GetValue(null) : null);
			if (self == null || SfTimeline._fModelsList == null)
			{
				return null;
			}
			IEnumerable list = SfTimeline._fModelsList.GetValue(self) as IEnumerable;
			if (list == null)
			{
				return null;
			}
			foreach (object i in list)
			{
				if ((string)SfTimeline._fOwner.GetValue(i) == "SoftFlesh" && (string)SfTimeline._fId.GetValue(i) == id)
				{
					return i;
				}
			}
			return null;
		}

		// Token: 0x060001B8 RID: 440 RVA: 0x00056768 File Offset: 0x00054968
		private static float PlaybackTime()
		{
			float num;
			try
			{
				num = ((SfTimeline._pPlaybackTime != null) ? ((float)SfTimeline._pPlaybackTime.GetValue(null, null)) : 0f);
			}
			catch
			{
				num = 0f;
			}
			return num;
		}

		// Token: 0x060001B9 RID: 441 RVA: 0x000567B8 File Offset: 0x000549B8
		private static object InstanceFor(ObjectCtrlInfo oci, string id, out string why)
		{
			why = null;
			object inst = SfTimeline.FindInstance(oci, id);
			if (inst != null)
			{
				return inst;
			}
			object model = SfTimeline.FindModel(id);
			if (model == null)
			{
				why = "track model not registered yet";
				return null;
			}
			object self = SfTimeline._fSelf.GetValue(null);
			inst = SfTimeline._ctorInterp.Invoke(new object[] { oci, model });
			IDictionary dict = SfTimeline._fInterpolables.GetValue(self) as IDictionary;
			if (dict == null || dict.Contains(inst.GetHashCode()))
			{
				why = "could not add track instance";
				return null;
			}
			dict[inst.GetHashCode()] = inst;
			object tree = SfTimeline._fInterpTree.GetValue(self);
			if (SfTimeline._mAddLeaf == null)
			{
				SfTimeline._mAddLeaf = tree.GetType().GetMethod("AddLeaf");
			}
			MethodBase mAddLeaf = SfTimeline._mAddLeaf;
			object obj = tree;
			object[] array = new object[2];
			array[0] = inst;
			mAddLeaf.Invoke(obj, array);
			return inst;
		}

		// Token: 0x060001BA RID: 442 RVA: 0x00056898 File Offset: 0x00054A98
		private static bool WriteKey(object inst, object value, ref float time)
		{
			IDictionary keys = SfTimeline._fKeyframes.GetValue(inst) as IDictionary;
			if (keys == null)
			{
				throw new Exception("no keyframe list");
			}
			foreach (object obj in keys)
			{
				DictionaryEntry en = (DictionaryEntry)obj;
				if (Mathf.Abs((float)en.Key - time) < 0.0005f)
				{
					time = (float)en.Key;
					break;
				}
			}
			bool existed = keys.Contains(time);
			if (existed)
			{
				SfTimeline._tKeyframe.GetField("value").SetValue(keys[time], value);
			}
			else
			{
				keys[time] = SfTimeline._ctorKeyframe.Invoke(new object[]
				{
					value,
					inst,
					AnimationCurve.Linear(0f, 0f, 1f, 1f)
				});
			}
			try
			{
				MethodInfo refreshList = SfTimeline._refreshList;
				if (refreshList != null)
				{
					refreshList.Invoke(null, null);
				}
			}
			catch
			{
			}
			return existed;
		}

		// Token: 0x060001BB RID: 443 RVA: 0x000569D0 File Offset: 0x00054BD0
		public static string KeyNow(ChaControl cha)
		{
			return SfTimeline.KeyNow(cha, "on");
		}

		// Token: 0x060001BC RID: 444 RVA: 0x000569E0 File Offset: 0x00054BE0
		public static string KeyNow(ChaControl cha, string id)
		{
			if (!SfTimeline.CanKey)
			{
				return "Timeline: not ready";
			}
			SoftFleshController c = SoftFleshPlugin.LiveController;
			if (c == null || cha == null)
			{
				return "no controller";
			}
			bool face = id == "faceOn";
			if (face ? (!c.HasFace(cha)) : (!c.HasChar(cha)))
			{
				if (!face)
				{
					return "Setup first";
				}
				return "Setup face first";
			}
			else
			{
				ObjectCtrlInfo oci = SfTimeline.OciOf(cha);
				if (oci == null)
				{
					return "character has no studio object";
				}
				float value = ((face ? c.GetFaceEnabled(cha) : c.GetCharEnabled(cha)) ? 1f : 0f);
				float time = SfTimeline.PlaybackTime();
				string text;
				try
				{
					string why;
					object inst = SfTimeline.InstanceFor(oci, id, out why);
					if (inst == null)
					{
						text = why;
					}
					else
					{
						bool existed = SfTimeline.WriteKey(inst, value, ref time);
						text = string.Concat(new string[]
						{
							existed ? "updated key " : "keyed ",
							(value >= 1f) ? "ON" : "OFF",
							" @ ",
							time.ToString("0.00"),
							"s"
						});
					}
				}
				catch (Exception e)
				{
					TargetInvocationException ex = e as TargetInvocationException;
					Exception inner = ((ex != null) ? ex.InnerException : null) ?? e;
					SoftFleshPlugin.Log("[SF] KeyNow failed: " + inner.Message);
					text = "key failed: " + inner.Message;
				}
				return text;
			}
		}

		// Token: 0x060001BD RID: 445 RVA: 0x00056B64 File Offset: 0x00054D64
		public static string KeyTab(string id, Dictionary<string, float> values)
		{
			if (!SfTimeline.CanKey)
			{
				return "Timeline: not ready";
			}
			if (values == null || values.Count == 0)
			{
				return "nothing to key";
			}
			float time = SfTimeline.PlaybackTime();
			string text;
			try
			{
				string why;
				object inst = SfTimeline.InstanceFor(null, id, out why);
				if (inst == null)
				{
					text = why;
				}
				else
				{
					bool existed = SfTimeline.WriteKey(inst, values, ref time);
					text = string.Concat(new string[]
					{
						existed ? "updated key: " : "keyed ",
						values.Count.ToString(),
						" values @ ",
						time.ToString("0.00"),
						"s"
					});
				}
			}
			catch (Exception e)
			{
				TargetInvocationException ex = e as TargetInvocationException;
				Exception inner = ((ex != null) ? ex.InnerException : null) ?? e;
				SoftFleshPlugin.Log("[SF] KeyTab failed: " + inner.Message);
				text = "key failed: " + inner.Message;
			}
			return text;
		}

		// Token: 0x0400033F RID: 831
		private const string Owner = "SoftFlesh";

		// Token: 0x04000340 RID: 832
		private const string TrackId = "on";

		// Token: 0x04000341 RID: 833
		public const string FaceTrackId = "faceOn";

		// Token: 0x04000342 RID: 834
		public static readonly string[] TabIds = new string[] { "tabContact", "tabImpact", "tabItems", "tabFace", "tabOpening" };

		// Token: 0x04000343 RID: 835
		public static readonly string[] TabNames = new string[] { "SoftFlesh Contact", "SoftFlesh Impact & waves", "SoftFlesh Items", "SoftFlesh Face", "SoftFlesh Opening" };

		// Token: 0x04000344 RID: 836
		private const string KReg = "SoftFlesh.TL.registered";

		// Token: 0x04000345 RID: 837
		private const string KGet = "SoftFlesh.TL.get";

		// Token: 0x04000346 RID: 838
		private const string KSet = "SoftFlesh.TL.set";

		// Token: 0x04000347 RID: 839
		private const string KCompat = "SoftFlesh.TL.compat";

		// Token: 0x04000348 RID: 840
		private const string KCompat2 = "SoftFlesh.TL.compat2";

		// Token: 0x04000349 RID: 841
		private const string KCompatTab = "SoftFlesh.TL.compatTab";

		// Token: 0x0400034A RID: 842
		private const string KGetTab = "SoftFlesh.TL.getTab";

		// Token: 0x0400034B RID: 843
		private const string KSetTab = "SoftFlesh.TL.setTab";

		// Token: 0x0400034C RID: 844
		private static bool _typesFound;

		// Token: 0x0400034D RID: 845
		private static bool _ready;

		// Token: 0x0400034E RID: 846
		private static bool _registered;

		// Token: 0x0400034F RID: 847
		private static bool _modelsChecked;

		// Token: 0x04000350 RID: 848
		private static MethodInfo _add;

		// Token: 0x04000351 RID: 849
		private static MethodInfo _init;

		// Token: 0x04000352 RID: 850
		private static ParameterInfo[] _addParams;

		// Token: 0x04000353 RID: 851
		private static int _idxUseOci = -1;

		// Token: 0x04000354 RID: 852
		private static Type _action5;

		// Token: 0x04000355 RID: 853
		private static FieldInfo _fInterpDeleg;

		// Token: 0x04000356 RID: 854
		private static FieldInfo _fRefresh;

		// Token: 0x04000357 RID: 855
		private static Type _tInterp;

		// Token: 0x04000358 RID: 856
		private static Type _tModel;

		// Token: 0x04000359 RID: 857
		private static Type _tKeyframe;

		// Token: 0x0400035A RID: 858
		private static FieldInfo _fSelf;

		// Token: 0x0400035B RID: 859
		private static FieldInfo _fInterpolables;

		// Token: 0x0400035C RID: 860
		private static FieldInfo _fInterpTree;

		// Token: 0x0400035D RID: 861
		private static FieldInfo _fModelsList;

		// Token: 0x0400035E RID: 862
		private static FieldInfo _fOci;

		// Token: 0x0400035F RID: 863
		private static FieldInfo _fKeyframes;

		// Token: 0x04000360 RID: 864
		private static FieldInfo _fOwner;

		// Token: 0x04000361 RID: 865
		private static FieldInfo _fId;

		// Token: 0x04000362 RID: 866
		private static MethodInfo _refreshList;

		// Token: 0x04000363 RID: 867
		private static MethodInfo _mAddLeaf;

		// Token: 0x04000364 RID: 868
		private static PropertyInfo _pPlaybackTime;

		// Token: 0x04000365 RID: 869
		private static ConstructorInfo _ctorInterp;

		// Token: 0x04000366 RID: 870
		private static ConstructorInfo _ctorKeyframe;

		// Token: 0x04000367 RID: 871
		private static readonly Dictionary<string, float> _lastV = new Dictionary<string, float>();

		// Token: 0x02000030 RID: 48
		private sealed class Param
		{
			// Token: 0x0600020A RID: 522 RVA: 0x0005C59D File Offset: 0x0005A79D
			public Param(string id, bool byId)
			{
				this._id = id;
				this._byId = byId;
			}

			// Token: 0x0600020B RID: 523 RVA: 0x0005C5B4 File Offset: 0x0005A7B4
			public bool IsCompatible(ObjectCtrlInfo oci)
			{
				if (this._byId)
				{
					Func<ObjectCtrlInfo, string, bool> f2 = AppDomain.CurrentDomain.GetData("SoftFlesh.TL.compat2") as Func<ObjectCtrlInfo, string, bool>;
					return f2 != null && f2(oci, this._id);
				}
				Func<ObjectCtrlInfo, bool> f3 = AppDomain.CurrentDomain.GetData("SoftFlesh.TL.compat") as Func<ObjectCtrlInfo, bool>;
				return f3 != null && f3(oci);
			}

			// Token: 0x0600020C RID: 524 RVA: 0x0005C614 File Offset: 0x0005A814
			public object GetValue(ObjectCtrlInfo oci, object parameter)
			{
				Func<ObjectCtrlInfo, string, float> f = AppDomain.CurrentDomain.GetData("SoftFlesh.TL.get") as Func<ObjectCtrlInfo, string, float>;
				return (f != null) ? f(oci, this._id) : 0f;
			}

			// Token: 0x0600020D RID: 525 RVA: 0x0005C652 File Offset: 0x0005A852
			public object GetParameter(ObjectCtrlInfo oci)
			{
				return null;
			}

			// Token: 0x0600020E RID: 526 RVA: 0x0005C658 File Offset: 0x0005A858
			public void InterpolateBefore(ObjectCtrlInfo oci, object parameter, object left, object right, float factor)
			{
				Action<ObjectCtrlInfo, string, float> set = AppDomain.CurrentDomain.GetData("SoftFlesh.TL.set") as Action<ObjectCtrlInfo, string, float>;
				if (set == null)
				{
					return;
				}
				float? i = left as float?;
				float? r = right as float?;
				if (i == null && r == null)
				{
					return;
				}
				float v = ((r != null && (i == null || factor >= 0.999f)) ? r.Value : i.Value);
				set(oci, this._id, v);
			}

			// Token: 0x0600020F RID: 527 RVA: 0x0005C6E4 File Offset: 0x0005A8E4
			public object ReadXml(object parameter, XmlNode node)
			{
				XmlAttribute a = ((node != null && node.Attributes != null) ? node.Attributes["value"] : null);
				if (a == null)
				{
					return 0f;
				}
				return XmlConvert.ToSingle(a.Value);
			}

			// Token: 0x06000210 RID: 528 RVA: 0x0005C72E File Offset: 0x0005A92E
			public void WriteXml(object parameter, XmlTextWriter writer, object value)
			{
				writer.WriteAttributeString("value", XmlConvert.ToString((float)value));
			}

			// Token: 0x040006B2 RID: 1714
			private readonly string _id;

			// Token: 0x040006B3 RID: 1715
			private readonly bool _byId;
		}

		// Token: 0x02000031 RID: 49
		private sealed class TabParam
		{
			// Token: 0x06000211 RID: 529 RVA: 0x0005C746 File Offset: 0x0005A946
			public TabParam(string id)
			{
				this._id = id;
			}

			// Token: 0x06000212 RID: 530 RVA: 0x0005C758 File Offset: 0x0005A958
			public bool IsCompatible(ObjectCtrlInfo oci)
			{
				Func<ObjectCtrlInfo, bool> f = AppDomain.CurrentDomain.GetData("SoftFlesh.TL.compatTab") as Func<ObjectCtrlInfo, bool>;
				return f != null && f(oci);
			}

			// Token: 0x06000213 RID: 531 RVA: 0x0005C788 File Offset: 0x0005A988
			public object GetValue(ObjectCtrlInfo oci, object parameter)
			{
				Func<ObjectCtrlInfo, string, Dictionary<string, float>> f = AppDomain.CurrentDomain.GetData("SoftFlesh.TL.getTab") as Func<ObjectCtrlInfo, string, Dictionary<string, float>>;
				Dictionary<string, float> d = ((f != null) ? f(oci, this._id) : null);
				return d ?? new Dictionary<string, float>();
			}

			// Token: 0x06000214 RID: 532 RVA: 0x0005C7C8 File Offset: 0x0005A9C8
			public object GetParameter(ObjectCtrlInfo oci)
			{
				return null;
			}

			// Token: 0x06000215 RID: 533 RVA: 0x0005C7CC File Offset: 0x0005A9CC
			public void InterpolateBefore(ObjectCtrlInfo oci, object parameter, object left, object right, float factor)
			{
				Action<ObjectCtrlInfo, string, Dictionary<string, float>> set = AppDomain.CurrentDomain.GetData("SoftFlesh.TL.setTab") as Action<ObjectCtrlInfo, string, Dictionary<string, float>>;
				if (set == null)
				{
					return;
				}
				Dictionary<string, float> i = left as Dictionary<string, float>;
				Dictionary<string, float> r = right as Dictionary<string, float>;
				if (i == null && r == null)
				{
					return;
				}
				if (this._out == null)
				{
					this._out = new Dictionary<string, float>();
				}
				this._out.Clear();
				Dictionary<string, float> src = r ?? i;
				foreach (KeyValuePair<string, float> kv in src)
				{
					float lv;
					if (i == null || !i.TryGetValue(kv.Key, out lv))
					{
						lv = kv.Value;
					}
					float rv;
					if (r == null || !r.TryGetValue(kv.Key, out rv))
					{
						rv = kv.Value;
					}
					this._out[kv.Key] = lv + (rv - lv) * factor;
				}
				if (i != null && r != null)
				{
					foreach (KeyValuePair<string, float> kv2 in i)
					{
						if (!this._out.ContainsKey(kv2.Key))
						{
							this._out[kv2.Key] = kv2.Value;
						}
					}
				}
				set(oci, this._id, this._out);
			}

			// Token: 0x06000216 RID: 534 RVA: 0x0005C940 File Offset: 0x0005AB40
			public object ReadXml(object parameter, XmlNode node)
			{
				Dictionary<string, float> d = new Dictionary<string, float>();
				XmlAttribute a = ((node != null && node.Attributes != null) ? node.Attributes["v"] : null);
				if (a == null)
				{
					return d;
				}
				foreach (string part in a.Value.Split(new char[] { ';' }))
				{
					int eq = part.IndexOf('=');
					float f;
					if (eq > 0 && float.TryParse(part.Substring(eq + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out f))
					{
						d[part.Substring(0, eq)] = f;
					}
				}
				return d;
			}

			// Token: 0x06000217 RID: 535 RVA: 0x0005C9E4 File Offset: 0x0005ABE4
			public void WriteXml(object parameter, XmlTextWriter writer, object value)
			{
				Dictionary<string, float> d = value as Dictionary<string, float>;
				StringBuilder sb = new StringBuilder();
				if (d != null)
				{
					foreach (KeyValuePair<string, float> kv in d)
					{
						sb.Append(kv.Key).Append('=').Append(kv.Value.ToString("R", CultureInfo.InvariantCulture))
							.Append(';');
					}
				}
				writer.WriteAttributeString("v", sb.ToString());
			}

			// Token: 0x040006B4 RID: 1716
			private readonly string _id;

			// Token: 0x040006B5 RID: 1717
			private Dictionary<string, float> _out;
		}
	}
}
