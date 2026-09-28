using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using BepInEx;
using Studio;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using AIChara;

namespace SoftFlesh
{
	// Token: 0x02000004 RID: 4
	[DefaultExecutionOrder(1000)]
	public class SoftFleshController : MonoBehaviour
	{
		// Token: 0x17000004 RID: 4
		// (get) Token: 0x0600004D RID: 77 RVA: 0x0000A5FC File Offset: 0x000087FC
		// (set) Token: 0x0600004E RID: 78 RVA: 0x0000A603 File Offset: 0x00008803
		public static SoftFleshController Instance { get; private set; }

		// Token: 0x0600004F RID: 79 RVA: 0x0000A60B File Offset: 0x0000880B
		private float TensionOf(SoftFleshController.Body b)
		{
			if (!b.isFace || this.faceDrag)
			{
				return this.skinTension;
			}
			return 0f;
		}

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x06000050 RID: 80 RVA: 0x0000A629 File Offset: 0x00008829
		private bool DeepOn
		{
			get
			{
				return this.deepCheck || this.showCross;
			}
		}

		// Token: 0x06000051 RID: 81 RVA: 0x0000A63C File Offset: 0x0000883C
		public static float FastAtan2(float y, float x)
		{
			float axv = ((x < 0f) ? (-x) : x);
			float ayv = ((y < 0f) ? (-y) : y);
			float a2 = ((axv < ayv) ? (axv / (ayv + 1E-20f)) : (ayv / (axv + 1E-20f)));
			float sq = a2 * a2;
			float r = ((-0.046496473f * sq + 0.15931422f) * sq - 0.32762277f) * sq * a2 + a2;
			if (ayv > axv)
			{
				r = 1.5707964f - r;
			}
			if (x < 0f)
			{
				r = 3.1415927f - r;
			}
			if (y >= 0f)
			{
				return r;
			}
			return -r;
		}

		// Token: 0x06000052 RID: 82 RVA: 0x0000A6D0 File Offset: 0x000088D0
		private static SoftFleshController.DbAccess AccessFor(Type ty)
		{
			SoftFleshController.DbAccess a;
			if (SoftFleshController._dbAcc.TryGetValue(ty, out a))
			{
				return a;
			}
			a = new SoftFleshController.DbAccess
			{
				typeName = ty.Name,
				force = ty.GetField("m_Force", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
				stif = ty.GetField("m_Stiffness", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
				damp = ty.GetField("m_Damping", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
				elast = ty.GetField("m_Elasticity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
				update = ty.GetMethod("UpdateParameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			};
			SoftFleshController._dbAcc[ty] = a;
			return a;
		}

		// Token: 0x06000053 RID: 83 RVA: 0x0000A76C File Offset: 0x0000896C
		private static bool IsHangName(string n)
		{
			if (string.IsNullOrEmpty(n))
			{
				return false;
			}
			string cn = n.ToLower();
			return cn.Contains("ball") || cn.Contains("testic") || cn.Contains("tama") || cn.Contains("kintama") || cn.Contains("sack") || cn.Contains("scrot");
		}

		// Token: 0x06000054 RID: 84 RVA: 0x0000A7D8 File Offset: 0x000089D8
		private static void GrabParticles(SoftFleshController.AimTarget t)
		{
			int nb = t.bones.Length;
			t.parts = new IList[nb];
			t.chain = new Transform[nb][];
			t.restRot = new Quaternion[nb][];
			t.segDir = new Vector3[nb][];
			t.shaft = new bool[nb];
			t.wasEnabled = new bool[nb];
			t.pStif = new float[nb][];
			t.pDamp = new float[nb][];
			t.pElast = new float[nb][];
			int total = 0;
			for (int i = 0; i < nb; i++)
			{
				Component db = t.bones[i];
				if (!(db == null))
				{
					FieldInfo pf = db.GetType().GetField("m_Particles", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (!(pf == null))
					{
						IList lst = pf.GetValue(db) as IList;
						if (lst != null && lst.Count != 0)
						{
							if (SoftFleshController._pStifF == null && lst[0] != null)
							{
								Type pt = lst[0].GetType();
								SoftFleshController._pStifF = pt.GetField("m_Stiffness", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								SoftFleshController._pDampF = pt.GetField("m_Damping", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								SoftFleshController._pElastF = pt.GetField("m_Elasticity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							}
							t.parts[i] = lst;
							if (SoftFleshController._pTrF == null && lst[0] != null)
							{
								SoftFleshController._pTrF = lst[0].GetType().GetField("m_Transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							}
							if (SoftFleshController._pTrF != null)
							{
								List<Transform> trs = new List<Transform>(lst.Count);
								List<Quaternion> rrs = new List<Quaternion>(lst.Count);
								for (int j = 0; j < lst.Count; j++)
								{
									Transform tk = ((lst[j] != null) ? (SoftFleshController._pTrF.GetValue(lst[j]) as Transform) : null);
									if (!(tk == null))
									{
										trs.Add(tk);
										rrs.Add(tk.localRotation);
									}
								}
								t.chain[i] = trs.ToArray();
								t.restRot[i] = rrs.ToArray();
								Vector3[] sd = new Vector3[trs.Count];
								int k = 0;
								while (k + 1 < trs.Count)
								{
									Vector3 d0 = trs[k + 1].localPosition;
									sd[k] = ((d0.sqrMagnitude > 1E-12f) ? d0.normalized : Vector3.forward);
									k++;
								}
								t.segDir[i] = sd;
								string cn = ((trs.Count > 0 && trs[0] != null) ? trs[0].name.ToLower() : "");
								t.shaft[i] = !cn.Contains("ball") && !cn.Contains("testic") && !cn.Contains("tama") && !cn.Contains("kintama") && !cn.Contains("sack");
							}
							t.pStif[i] = new float[lst.Count];
							t.pDamp[i] = new float[lst.Count];
							t.pElast[i] = new float[lst.Count];
							for (int l = 0; l < lst.Count; l++)
							{
								object pk = lst[l];
								if (pk != null)
								{
									if (SoftFleshController._pStifF != null)
									{
										t.pStif[i][l] = (float)SoftFleshController._pStifF.GetValue(pk);
									}
									if (SoftFleshController._pDampF != null)
									{
										t.pDamp[i][l] = (float)SoftFleshController._pDampF.GetValue(pk);
									}
									if (SoftFleshController._pElastF != null)
									{
										t.pElast[i][l] = (float)SoftFleshController._pElastF.GetValue(pk);
									}
								}
							}
							total += lst.Count;
						}
					}
				}
			}
			StringBuilder drv = new StringBuilder();
			for (int m = 0; m < nb; m++)
			{
				if (t.chain[m] != null && t.chain[m].Length != 0 && t.chain[m][0] != null)
				{
					drv.Append(t.chain[m][0].name).Append(t.shaft[m] ? "(steers) " : "(hangs) ");
				}
			}
			SoftFleshPlugin.Log("[SF] item aim chains: " + drv.ToString());
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] item aim particles: ",
				total.ToString(),
				" across ",
				nb.ToString(),
				" chain(s) | fields stif=",
				(SoftFleshController._pStifF != null).ToString(),
				" damp=",
				(SoftFleshController._pDampF != null).ToString(),
				" elast=",
				(SoftFleshController._pElastF != null).ToString()
			}));
		}

		// Token: 0x06000055 RID: 85 RVA: 0x0000AD08 File Offset: 0x00008F08
		private static void SetParticleFirm(SoftFleshController.AimTarget t, float firm)
		{
			if (t.parts == null)
			{
				return;
			}
			for (int i = 0; i < t.parts.Length; i++)
			{
				IList lst = t.parts[i];
				if (lst != null)
				{
					float[] so = t.pStif[i];
					float[] dof = t.pDamp[i];
					float[] eo = t.pElast[i];
					int j = 0;
					while (j < lst.Count && j < so.Length)
					{
						object pk = lst[j];
						if (pk != null)
						{
							if (SoftFleshController._pStifF != null)
							{
								SoftFleshController._pStifF.SetValue(pk, Mathf.Lerp(so[j], 0.95f, firm));
							}
							if (SoftFleshController._pDampF != null)
							{
								SoftFleshController._pDampF.SetValue(pk, Mathf.Lerp(dof[j], 0.85f, firm));
							}
							if (SoftFleshController._pElastF != null)
							{
								SoftFleshController._pElastF.SetValue(pk, Mathf.Lerp(eo[j], 0.55f, firm));
							}
						}
						j++;
					}
				}
			}
		}

		// Token: 0x17000006 RID: 6
		// (get) Token: 0x06000056 RID: 86 RVA: 0x0000AE1D File Offset: 0x0000901D
		public int BodyCount
		{
			get
			{
				return this._bodies.Count;
			}
		}

		// Token: 0x17000007 RID: 7
		// (get) Token: 0x06000057 RID: 87 RVA: 0x0000AE2A File Offset: 0x0000902A
		public bool HasSetup
		{
			get
			{
				return this._bodies.Count > 0;
			}
		}

		// Token: 0x06000058 RID: 88 RVA: 0x0000AE3C File Offset: 0x0000903C
		private void Awake()
		{
			SoftFleshController.Instance = this;
			int swept = 0;
			foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
			{
				if (!(go == null) && go.name.StartsWith("SF_"))
				{
					UnityEngine.Object.Destroy(go);
					swept++;
				}
			}
			int scrubbed = 0;
			foreach (DynamicBone db in Resources.FindObjectsOfTypeAll<DynamicBone>())
			{
				if (!(db == null) && db.m_Colliders != null)
				{
					for (int i = db.m_Colliders.Count - 1; i >= 0; i--)
					{
						DynamicBoneCollider c = db.m_Colliders[i];
						if (c == null || c.gameObject == null || c.gameObject.name.StartsWith("SF_"))
						{
							db.m_Colliders.RemoveAt(i);
							scrubbed++;
						}
					}
				}
			}
			if (swept > 0 || scrubbed > 0)
			{
				SoftFleshPlugin.Log(string.Concat(new string[]
				{
					"[SF] swept ",
					swept.ToString(),
					" leftover object(s), scrubbed ",
					scrubbed.ToString(),
					" stale collider registration(s) from a previous instance"
				}));
			}
		}

		// Token: 0x06000059 RID: 89 RVA: 0x0000AF84 File Offset: 0x00009184
		private void OnDestroy()
		{
			SoftFleshPlugin.Log("[SF] teardown: restoring " + this._bodies.Count.ToString() + " body mesh(es)");
			this.ClearAll();
			SoftFleshController.ShutdownWorkers();
		}

		// Token: 0x0600005A RID: 90 RVA: 0x0000AFC4 File Offset: 0x000091C4
		public ChaControl[] SelectedChars()
		{
			global::Studio.Studio st = Singleton<global::Studio.Studio>.Instance;
			if (st == null || st.treeNodeCtrl == null || st.treeNodeCtrl.selectNodes == null)
			{
				return null;
			}
			List<ChaControl> list = new List<ChaControl>();
			foreach (TreeNodeObject node in st.treeNodeCtrl.selectNodes)
			{
				TreeNodeObject i = node;
				int up = 0;
				while (up < 6 && i != null)
				{
					ObjectCtrlInfo oci;
					if (st.dicInfo.TryGetValue(i, out oci))
					{
						OCIChar oc = oci as OCIChar;
						if (oc != null && oc.charInfo != null)
						{
							if (!list.Contains(oc.charInfo))
							{
								list.Add(oc.charInfo);
								break;
							}
							break;
						}
					}
					i = i.parent;
					up++;
				}
			}
			if (list.Count <= 0)
			{
				return null;
			}
			return list.ToArray();
		}

		// Token: 0x0600005B RID: 91 RVA: 0x0000B0A8 File Offset: 0x000092A8
		public int SelectedCharCount()
		{
			ChaControl[] sel = this.SelectedChars();
			if (sel != null)
			{
				return sel.Length;
			}
			return 0;
		}

		// Token: 0x0600005C RID: 92 RVA: 0x0000B0C4 File Offset: 0x000092C4
		public void SetupAll()
		{
			this.SetupChars(null);
		}

		// Token: 0x0600005D RID: 93 RVA: 0x0000B0D0 File Offset: 0x000092D0
		private void RebuildPressers()
		{
			this._pressers.Clear();
			ChaControl[] all = UnityEngine.Object.FindObjectsOfType<ChaControl>();
			foreach (ChaControl cha in all)
			{
				if (!(cha == null))
				{
					SkinnedMeshRenderer smr = SoftFleshController.FindBodySmr(cha);
					if (!(smr == null))
					{
						int before = this._pressers.Count;
						if (cha.objBodyBone != null)
						{
							SoftFleshController.CollectFingerCapsules(cha.objBodyBone.transform, this._pressers);
						}
						for (int q = before; q < this._pressers.Count; q++)
						{
							this._pressers[q].cha = cha;
						}
					}
				}
			}
			this._pressCharCount = ((all != null) ? all.Length : 0);
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] Setup: ",
				this._bodies.Count.ToString(),
				" body(ies) set up, ",
				this._pressers.Count.ToString(),
				" presser(s) before items"
			}));
			for (int q2 = this._itemDefs.Count - 1; q2 >= 0; q2--)
			{
				if (this._itemDefs[q2].a == null)
				{
					this._itemDefs.RemoveAt(q2);
				}
				else
				{
					this._pressers.Add(this._itemDefs[q2]);
				}
			}
			if (this.boneOpening && this._dynCols.Count > 0)
			{
				for (int b2 = 0; b2 < this._bodies.Count; b2++)
				{
					SoftFleshController.Body bd = this._bodies[b2];
					for (int i = 0; i < bd.vagDyn.Count; i++)
					{
						DynamicBone db = bd.vagDyn[i];
						if (!(db == null))
						{
							if (db.m_Colliders == null)
							{
								db.m_Colliders = new List<DynamicBoneCollider>();
							}
							for (int q3 = 0; q3 < this._dynCols.Count; q3++)
							{
								if (this._dynCols[q3] != null && !db.m_Colliders.Contains(this._dynCols[q3]))
								{
									db.m_Colliders.Add(this._dynCols[q3]);
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x0600005E RID: 94 RVA: 0x0000B344 File Offset: 0x00009544
		private SoftFleshController.Body BodyOf(ChaControl cha)
		{
			for (int i = 0; i < this._bodies.Count; i++)
			{
				if (!this._bodies[i].isFace && this._bodies[i].cha == cha)
				{
					return this._bodies[i];
				}
			}
			return null;
		}

		// Token: 0x0600005F RID: 95 RVA: 0x0000B3A1 File Offset: 0x000095A1
		public bool IsBodyLive(ChaControl cha)
		{
			return this.CheckBody(cha) != 2;
		}

		// Token: 0x06000060 RID: 96 RVA: 0x0000B3B0 File Offset: 0x000095B0
		public int CheckBody(ChaControl cha)
		{
			SoftFleshController.Body b = this.BodyOf(cha);
			if (b == null)
			{
				return 2;
			}
			if (b.cha == null || b.cha.objBodyBone == null)
			{
				return 2;
			}
			if (b.smr == null || b.workMesh == null)
			{
				return 2;
			}
			if (b.smr.sharedMesh == b.workMesh)
			{
				return 0;
			}
			SkinnedMeshRenderer now = null;
			try
			{
				now = SoftFleshController.FindBodySmr(b.cha);
			}
			catch
			{
			}
			if (now != b.smr)
			{
				return 2;
			}
			Mesh cur = b.smr.sharedMesh;
			int builtFrom = ((b.origMesh != null) ? b.origMesh.vertexCount : (-1));
			if (cur == null || builtFrom < 0 || cur.vertexCount != builtFrom)
			{
				return 2;
			}
			if (!SoftFleshController.MeshFitsBones(b.smr, b.workMesh))
			{
				return 2;
			}
			b.smr.sharedMesh = b.workMesh;
			this._ourMeshes[b.smr] = b.workMesh;
			b.clothBound = false;
			return 1;
		}

		// Token: 0x06000061 RID: 97 RVA: 0x0000B4E0 File Offset: 0x000096E0
		private void TeardownBody(SoftFleshController.Body b)
		{
			if (b.isFace)
			{
				this.ReleaseMouth(b);
			}
			this.ReleaseGarments(b);
			if (b.smr != null && b.origMesh != null && SoftFleshController.MeshFitsBones(b.smr, b.origMesh))
			{
				b.smr.sharedMesh = b.origMesh;
			}
			if (b.workMesh != null)
			{
				UnityEngine.Object.Destroy(b.workMesh);
			}
			if (b.bake != null)
			{
				UnityEngine.Object.Destroy(b.bake);
			}
			if (b.refMesh != null)
			{
				UnityEngine.Object.Destroy(b.refMesh);
			}
			this._bodies.Remove(b);
		}

		// Token: 0x06000062 RID: 98 RVA: 0x0000B59C File Offset: 0x0000979C
		private void ReleaseGarments(SoftFleshController.Body body)
		{
			for (int r = this._cloth.Count - 1; r >= 0; r--)
			{
				SoftFleshController.ClothRec rec = this._cloth[r];
				if (rec.body == body)
				{
					if (rec.smr != null && rec.orig != null)
					{
						Mesh cur = rec.smr.sharedMesh;
						if (cur == rec.clone || cur == null || (cur != rec.orig && (cur.name.StartsWith("SF_cloth_") || cur.name.EndsWith("(Clone)"))))
						{
							int nW = ((cur != null) ? cur.blendShapeCount : 0);
							float[] wS = null;
							if (nW > 0)
							{
								wS = new float[nW];
								for (int i = 0; i < nW; i++)
								{
									wS[i] = rec.smr.GetBlendShapeWeight(i);
								}
							}
							rec.smr.sharedMesh = rec.orig;
							if (wS != null)
							{
								int nO = rec.orig.blendShapeCount;
								int j = 0;
								while (j < nW && j < nO)
								{
									if (wS[j] != 0f)
									{
										rec.smr.SetBlendShapeWeight(j, wS[j]);
									}
									j++;
								}
							}
						}
					}
					if (rec.clone != null)
					{
						UnityEngine.Object.Destroy(rec.clone);
					}
					this._cloth.RemoveAt(r);
				}
			}
			for (int r2 = this._skins.Count - 1; r2 >= 0; r2--)
			{
				SoftFleshController.SkinRec sk = this._skins[r2];
				if (sk.body == body)
				{
					if (sk.origSmr != null)
					{
						sk.origSmr.enabled = true;
					}
					if (sk.go != null)
					{
						UnityEngine.Object.Destroy(sk.go);
					}
					if (sk.mesh != null)
					{
						UnityEngine.Object.Destroy(sk.mesh);
					}
					this._skins.RemoveAt(r2);
				}
			}
			body.clothBound = false;
			body.clothCovered = null;
		}

		// Token: 0x06000063 RID: 99 RVA: 0x0000B7C4 File Offset: 0x000099C4
		public bool ResetupChar(ChaControl cha)
		{
			if (cha == null)
			{
				return false;
			}
			SoftFleshController.Body old = this.BodyOf(cha);
			bool on = old == null || old.sfOn;
			if (old != null)
			{
				this.TeardownBody(old);
			}
			int before = this._bodies.Count;
			this.SetupCharacter(cha);
			bool built = this._bodies.Count > before;
			SoftFleshController.Body nb = this.BodyOf(cha);
			if (nb != null)
			{
				nb.sfOn = on;
			}
			this.RebuildPressers();
			this.lastStatus = "bodies=" + this._bodies.Count.ToString() + " pressers=" + this._pressers.Count.ToString();
			SoftFleshPlugin.Log("[SF] Setup(one): " + (built ? "rebuilt" : "FAILED for") + " a character; " + this.lastStatus);
			SfTimeline.Refresh();
			return built;
		}

		// Token: 0x06000064 RID: 100 RVA: 0x0000B8A4 File Offset: 0x00009AA4
		public bool BakeChar(ChaControl cha)
		{
			SoftFleshController.Body b = this.BodyOf(cha);
			if (b == null)
			{
				return false;
			}
			bool ok = false;
			try
			{
				ok = this.BakeBody(b);
			}
			catch (Exception e)
			{
				SoftFleshPlugin.Log("[SF] Bake(one) THREW: " + e.Message);
			}
			if (ok)
			{
				b.baked = true;
			}
			if (ok && (this.bakeStatus == null || !this.bakeStatus.StartsWith("baked")))
			{
				this.bakeStatus = "baked 1 body(ies)";
			}
			return ok;
		}

		// Token: 0x06000065 RID: 101 RVA: 0x0000B928 File Offset: 0x00009B28
		public bool GetCharBaked(ChaControl cha)
		{
			SoftFleshController.Body b = this.BodyOf(cha);
			return b != null && b.baked;
		}

		// Token: 0x06000066 RID: 102 RVA: 0x0000B948 File Offset: 0x00009B48
		public int PruneDeadBodies()
		{
			int i = 0;
			for (int j = this._bodies.Count - 1; j >= 0; j--)
			{
				if (this._bodies[j].cha == null)
				{
					this.TeardownBody(this._bodies[j]);
					i++;
				}
			}
			if (i > 0)
			{
				this.RebuildPressers();
				SfTimeline.Refresh();
			}
			return i;
		}

		// Token: 0x06000067 RID: 103 RVA: 0x0000B9B0 File Offset: 0x00009BB0
		public int HandsOnlySelected()
		{
			ChaControl[] sel = this.SelectedChars();
			if (sel == null || sel.Length == 0)
			{
				return 0;
			}
			int i = 0;
			foreach (ChaControl c in sel)
			{
				if (!(c == null))
				{
					SoftFleshController.Body b = this.BodyOf(c);
					if (b != null)
					{
						this.TeardownBody(b);
					}
					i++;
				}
			}
			this.RebuildPressers();
			return i;
		}

		// Token: 0x06000068 RID: 104 RVA: 0x0000BA10 File Offset: 0x00009C10
		public void RefreshPressersIfCharsChanged()
		{
			if (this._bodies.Count == 0)
			{
				return;
			}
			ChaControl[] all = UnityEngine.Object.FindObjectsOfType<ChaControl>();
			int cnt = ((all != null) ? all.Length : 0);
			if (cnt == this._pressCharCount)
			{
				return;
			}
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] characters in scene: ",
				this._pressCharCount.ToString(),
				" -> ",
				cnt.ToString(),
				", rebuilding pressers"
			}));
			this.RebuildPressers();
		}

		// Token: 0x06000069 RID: 105 RVA: 0x0000BA8C File Offset: 0x00009C8C
		public void SetupSelected()
		{
			ChaControl[] sel = this.SelectedChars();
			if (sel == null || sel.Length == 0)
			{
				this.SetupChars(null);
				return;
			}
			foreach (ChaControl c in sel)
			{
				this.ResetupChar(c);
			}
		}

		// Token: 0x0600006A RID: 106 RVA: 0x0000BACC File Offset: 0x00009CCC
		public void SetupChars(ChaControl[] only)
		{
			this.ParticleCensus();
			this.ClearAll();
			if (this._canalCol != null)
			{
				UnityEngine.Object.Destroy(this._canalCol.gameObject);
				this._canalCol = null;
			}
			this.diag = "";
			ChaControl[] all = UnityEngine.Object.FindObjectsOfType<ChaControl>();
			ChaControl[] chas = ((only != null && only.Length != 0) ? only : all);
			SoftFleshPlugin.Log("[SF] Setup: " + ((chas == null) ? 0 : chas.Length).ToString() + " character(s)" + ((only != null && only.Length != 0) ? (" (selected of " + ((all == null) ? 0 : all.Length).ToString() + ")") : " (all)"));
			foreach (ChaControl cha in chas)
			{
				this.SetupCharacter(cha);
			}
			this.RebuildPressers();
			string pnames = "";
			int i = 0;
			while (i < this._pressers.Count && i < 8)
			{
				pnames = pnames + this._pressers[i].b.name + " ";
				i++;
			}
			this.diag = this.diag + "P: " + pnames + "\n";
			SoftFleshPlugin.Log("pressers(" + this._pressers.Count.ToString() + "): " + pnames);
			this.lastStatus = string.Concat(new string[]
			{
				"chars=",
				chas.Length.ToString(),
				"/",
				((all == null) ? 0 : all.Length).ToString(),
				" bodies=",
				this._bodies.Count.ToString(),
				" pressers=",
				this._pressers.Count.ToString()
			});
			SoftFleshPlugin.Log(this.lastStatus);
			SfTimeline.Refresh();
		}

		// Token: 0x0600006B RID: 107 RVA: 0x0000BCC0 File Offset: 0x00009EC0
		private void SetupCharacter(ChaControl cha)
		{
			this.SetupCharacter(cha, false);
		}

		// Token: 0x0600006C RID: 108 RVA: 0x0000BCCC File Offset: 0x00009ECC
		private void SetupCharacter(ChaControl cha, bool faceMode)
		{
			if (cha == null || cha.objBodyBone == null)
			{
				return;
			}
			SkinnedMeshRenderer smr = (faceMode ? SoftFleshController.FindFaceSmr(cha) : SoftFleshController.FindBodySmr(cha));
			if (smr == null || smr.sharedMesh == null)
			{
				this.diag += (faceMode ? "no face SMR\n" : "no body SMR\n");
				return;
			}
			if (!smr.sharedMesh.isReadable)
			{
				this.diag += "NOT READABLE\n";
				return;
			}
			Mesh mesh = smr.sharedMesh;
			bool inherited = mesh.name != null && mesh.name.StartsWith("SF_");
			if (inherited)
			{
				SoftFleshPlugin.Log("[SF] WARNING: the renderer still carries " + mesh.name + " from a previous plugin instance - Clear was not run before the reload. Working from it WITHOUT re-subdividing; reload the character for a clean base.");
			}
			if (!SoftFleshController.MeshFitsBones(smr, mesh))
			{
				SoftFleshPlugin.Log(string.Concat(new string[]
				{
					"[SF] WARNING: the body mesh has ",
					((mesh.bindposes != null) ? mesh.bindposes.Length : (-1)).ToString(),
					" bind poses but the renderer has ",
					smr.bones.Length.ToString(),
					" bones — this mesh is not the body currently selected. Reload the character so the uncensor re-assigns its mesh; Setup cannot recover it."
				}));
			}
			Mesh trueOrig = mesh;
			Mesh cachedOrig;
			if (this._origMeshes.TryGetValue(smr, out cachedOrig) && cachedOrig != null && this.StillOurs(smr, cachedOrig))
			{
				trueOrig = cachedOrig;
			}
			else
			{
				if (cachedOrig != null)
				{
					SoftFleshPlugin.Log("[SF] body mesh was replaced under us (uncensor change?) — dropping the cached original and re-reading");
				}
				this._origMeshes[smr] = mesh;
			}
			mesh = trueOrig;
			if (this.subdivEnabled && !inherited && !faceMode)
			{
				string sinfo;
				Mesh refined = SoftFleshSubdivide.Build(mesh, smr.bones, this.subdivMinArea, this.subdivLevels, this.capHoles, this.spreadReach, this.subdivWeightGap, this.subdivBody, out sinfo);
				this.subdivInfo = "subdiv: " + sinfo;
				SoftFleshPlugin.Log("[SF] " + this.subdivInfo);
				if (refined != null)
				{
					if (this.body_prevSubdiv != null && this.body_prevSubdiv != mesh)
					{
						UnityEngine.Object.Destroy(this.body_prevSubdiv);
					}
					this.body_prevSubdiv = refined;
					refined.name = "SF_subdiv";
					smr.sharedMesh = refined;
					mesh = refined;
				}
			}
			this.weldInfo = "";
			if (this.weldWeights)
			{
				BoneWeight[] wArr = mesh.boneWeights;
				if (wArr != null && wArr.Length == mesh.vertexCount)
				{
					Dictionary<long, List<int>> groups = new Dictionary<long, List<int>>(wArr.Length);
					Vector3[] mv = mesh.vertices;
					for (int i = 0; i < mv.Length; i++)
					{
						long pk = SoftFleshController.PosKey(mv[i]);
						List<int> g;
						if (!groups.TryGetValue(pk, out g))
						{
							g = new List<int>(2);
							groups[pk] = g;
						}
						g.Add(i);
					}
					int fixedGroups = 0;
					foreach (KeyValuePair<long, List<int>> kv in groups)
					{
						List<int> g2 = kv.Value;
						if (g2.Count >= 2)
						{
							bool same = true;
							int j = 1;
							while (j < g2.Count && same)
							{
								BoneWeight x = wArr[g2[0]];
								BoneWeight y = wArr[g2[j]];
								if (x.boneIndex0 != y.boneIndex0 || Mathf.Abs(x.weight0 - y.weight0) > 0.002f || x.boneIndex1 != y.boneIndex1 || Mathf.Abs(x.weight1 - y.weight1) > 0.002f || x.boneIndex2 != y.boneIndex2 || Mathf.Abs(x.weight2 - y.weight2) > 0.002f || x.boneIndex3 != y.boneIndex3 || Mathf.Abs(x.weight3 - y.weight3) > 0.002f)
								{
									same = false;
								}
								j++;
							}
							if (!same)
							{
								BoneWeight acc = wArr[g2[0]];
								for (int k = 1; k < g2.Count; k++)
								{
									acc = SoftFleshSubdivide.BlendWeights(acc, wArr[g2[k]]);
								}
								for (int l = 0; l < g2.Count; l++)
								{
									wArr[g2[l]] = acc;
								}
								fixedGroups++;
							}
						}
					}
					int spreadV = 0;
					Transform[] smrBones = smr.bones;
					if (this.spreadWeights > 0f && smrBones != null)
					{
						Matrix4x4[] bps2 = mesh.bindposes;
						List<int> vagIx = new List<int>();
						List<Vector3> vagPos = new List<Vector3>();
						for (int b2 = 0; b2 < smrBones.Length; b2++)
						{
							Transform bt = smrBones[b2];
							if (!(bt == null) && bt.name != null && bt.name.IndexOf("vagina", StringComparison.OrdinalIgnoreCase) >= 0 && bps2 != null && b2 < bps2.Length)
							{
								vagIx.Add(b2);
								vagPos.Add(bps2[b2].inverse.MultiplyPoint3x4(Vector3.zero));
							}
						}
						if (vagIx.Count >= 6)
						{
							Vector3 rc2 = Vector3.zero;
							for (int m = 0; m < vagPos.Count; m++)
							{
								rc2 += vagPos[m];
							}
							rc2 /= (float)vagPos.Count;
							Vector3[] mv2 = mesh.vertices;
							int n = 0;
							while (n < mv2.Length && n < wArr.Length)
							{
								float dC = (mv2[n] - rc2).magnitude;
								if (dC <= this.spreadReach)
								{
									BoneWeight w6 = wArr[n];
									float vw = 0f;
									for (int k2 = 0; k2 < vagIx.Count; k2++)
									{
										int bi6 = vagIx[k2];
										if (w6.boneIndex0 == bi6)
										{
											vw += w6.weight0;
										}
										if (w6.boneIndex1 == bi6)
										{
											vw += w6.weight1;
										}
										if (w6.boneIndex2 == bi6)
										{
											vw += w6.weight2;
										}
										if (w6.boneIndex3 == bi6)
										{
											vw += w6.weight3;
										}
									}
									float u6 = Mathf.Clamp01(1f - dC / this.spreadReach);
									float want = u6 * u6 * (3f - 2f * u6) * this.spreadWeights;
									want *= 1f - Mathf.Clamp01(vw / 0.5f);
									if (want > vw + 0.01f)
									{
										int nb = vagIx[0];
										float bd = 1E+09f;
										for (int k3 = 0; k3 < vagIx.Count; k3++)
										{
											float dd6 = (mv2[n] - vagPos[k3]).sqrMagnitude;
											if (dd6 < bd)
											{
												bd = dd6;
												nb = vagIx[k3];
											}
										}
										float add = want - vw;
										float keep = 1f - want;
										float other = 1f - vw;
										if (other >= 1E-05f)
										{
											float sc = keep / other;
											w6.weight0 *= sc;
											w6.weight1 *= sc;
											w6.weight2 *= sc;
											w6.weight3 *= sc;
											int slot = 3;
											float sw = w6.weight3;
											if (w6.weight0 < sw)
											{
												slot = 0;
												sw = w6.weight0;
											}
											if (w6.weight1 < sw)
											{
												slot = 1;
												sw = w6.weight1;
											}
											if (w6.weight2 < sw)
											{
												slot = 2;
												sw = w6.weight2;
											}
											float put = want + sw;
											if (slot == 0)
											{
												w6.boneIndex0 = nb;
												w6.weight0 = put;
											}
											else if (slot == 1)
											{
												w6.boneIndex1 = nb;
												w6.weight1 = put;
											}
											else if (slot == 2)
											{
												w6.boneIndex2 = nb;
												w6.weight2 = put;
											}
											else
											{
												w6.boneIndex3 = nb;
												w6.weight3 = put;
											}
											float tot6 = w6.weight0 + w6.weight1 + w6.weight2 + w6.weight3;
											if (tot6 > 1E-05f)
											{
												w6.weight0 /= tot6;
												w6.weight1 /= tot6;
												w6.weight2 /= tot6;
												w6.weight3 /= tot6;
											}
											wArr[n] = w6;
											spreadV++;
											if (add > 0f)
											{
												fixedGroups++;
											}
										}
									}
								}
								n++;
							}
						}
					}
					this.weldInfo = "spread ring influence into " + spreadV.ToString() + " surrounding verts";
					if (fixedGroups > 0)
					{
						if (mesh == trueOrig)
						{
							Mesh copy = UnityEngine.Object.Instantiate<Mesh>(mesh);
							copy.name = mesh.name + "_weld";
							if (this.body_prevSubdiv != null && this.body_prevSubdiv != mesh)
							{
								UnityEngine.Object.Destroy(this.body_prevSubdiv);
							}
							this.body_prevSubdiv = copy;
							mesh = copy;
							smr.sharedMesh = copy;
						}
						mesh.boneWeights = wArr;
					}
					this.weldInfo = this.weldInfo + "  |  split-skin groups repaired = " + fixedGroups.ToString();
					SoftFleshPlugin.Log("[SF] " + this.weldInfo);
				}
			}
			List<int> foldList = new List<int>();
			Vector3[] fv = mesh.vertices;
			int[] ft = mesh.triangles;
			if (fv != null && ft != null)
			{
				Vector3[] fn = new Vector3[ft.Length / 3];
				int t = 0;
				while (t + 2 < ft.Length)
				{
					fn[t / 3] = Vector3.Cross(fv[ft[t + 1]] - fv[ft[t]], fv[ft[t + 2]] - fv[ft[t]]).normalized;
					t += 3;
				}
				Dictionary<long, int> ed = new Dictionary<long, int>(ft.Length);
				bool[] mark = new bool[fv.Length];
				int t2 = 0;
				while (t2 + 2 < ft.Length)
				{
					for (int e = 0; e < 3; e++)
					{
						int va = ft[t2 + e];
						int vb = ft[t2 + (e + 1) % 3];
						long k4 = ((va < vb) ? (((long)va << 32) | (long)((ulong)vb)) : (((long)vb << 32) | (long)((ulong)va)));
						int other2;
						if (!ed.TryGetValue(k4, out other2))
						{
							ed[k4] = t2 / 3;
						}
						else
						{
							float dp = Vector3.Dot(fn[other2], fn[t2 / 3]);
							if (dp < -0.75f)
							{
								mark[va] = true;
								mark[vb] = true;
							}
						}
					}
					t2 += 3;
				}
				for (int i2 = 0; i2 < mark.Length; i2++)
				{
					if (mark[i2])
					{
						foldList.Add(i2);
					}
				}
			}
			Vector3[] baseV = mesh.vertices;
			Vector3[] baseN = mesh.normals;
			if (baseV == null || baseN == null || baseN.Length != baseV.Length)
			{
				this.diag += "no verts/normals\n";
				return;
			}
			Mesh nm = UnityEngine.Object.Instantiate<Mesh>(mesh);
			nm.name = mesh.name + "_SF";
			nm.MarkDynamic();
			int[] parT;
			if (SoftFleshSubdivide.ParentsByMesh.TryGetValue(mesh.GetInstanceID(), out parT))
			{
				SoftFleshSubdivide.ParentsByMesh[nm.GetInstanceID()] = parT;
			}
			SoftFleshController.Body body = new SoftFleshController.Body
			{
				cha = cha,
				smr = smr,
				origMesh = trueOrig,
				workMesh = nm,
				foldVerts = foldList.ToArray(),
				isFace = faceMode,
				creaseMark = new bool[baseV.Length],
				baseVerts = baseV,
				baseNormals = baseN,
				baseNormalsList = new List<Vector3>(baseN),
				disp = new float[baseV.Length],
				vel = new float[baseV.Length],
				tgtF = new float[baseV.Length],
				wakeStamp = new int[baseV.Length],
				engaged = new byte[baseV.Length],
				reached = new byte[baseV.Length],
				hLast = new float[baseV.Length],
				soften = new float[baseV.Length],
				pushB = new Vector3[baseV.Length],
				pushT = new Vector3[baseV.Length],
				bake = new Mesh()
			};
			body.outV.Capacity = baseV.Length;
			for (int i3 = 0; i3 < baseV.Length; i3++)
			{
				body.outV.Add(baseV[i3]);
				body.normList.Add(baseN[i3]);
			}
			int[] tris = mesh.triangles;
			List<int>[] adjSets = new List<int>[baseV.Length];
			for (int i4 = 0; i4 < baseV.Length; i4++)
			{
				adjSets[i4] = new List<int>(6);
			}
			int t3 = 0;
			while (t3 + 2 < tris.Length)
			{
				int a = tris[t3];
				int b3 = tris[t3 + 1];
				int c = tris[t3 + 2];
				SoftFleshController.AddAdj(adjSets[a], b3);
				SoftFleshController.AddAdj(adjSets[a], c);
				SoftFleshController.AddAdj(adjSets[b3], a);
				SoftFleshController.AddAdj(adjSets[b3], c);
				SoftFleshController.AddAdj(adjSets[c], a);
				SoftFleshController.AddAdj(adjSets[c], b3);
				t3 += 3;
			}
			body.smoothBuf = new Vector3[baseV.Length];
			body.smoothNBuf = new Vector3[baseV.Length];
			body.nTilt = new Vector3[baseV.Length];
			body.nTiltSeen = new int[baseV.Length];
			body.nTiltIn = new bool[baseV.Length];
			body.nTiltLive.Clear();
			body.rdFrac = new float[baseV.Length];
			Dictionary<long, List<int>> map = new Dictionary<long, List<int>>(baseV.Length);
			for (int i5 = 0; i5 < baseV.Length; i5++)
			{
				long key = SoftFleshController.PosKey(baseV[i5]);
				List<int> list;
				if (!map.TryGetValue(key, out list))
				{
					list = new List<int>(2);
					map[key] = list;
				}
				list.Add(i5);
			}
			List<int[]> groups2 = new List<int[]>();
			foreach (KeyValuePair<long, List<int>> kv2 in map)
			{
				if (kv2.Value.Count > 1)
				{
					groups2.Add(kv2.Value.ToArray());
				}
			}
			body.seamGroups = groups2.ToArray();
			body.seamNSync = new bool[body.seamGroups.Length];
			body.seamAligned = new bool[body.seamGroups.Length];
			for (int g3 = 0; g3 < body.seamGroups.Length; g3++)
			{
				int[] grp = body.seamGroups[g3];
				bool ok = true;
				float minDot = 1f;
				for (int j2 = 1; j2 < grp.Length; j2++)
				{
					float d = Vector3.Dot(baseN[grp[0]], baseN[grp[j2]]);
					if (d < minDot)
					{
						minDot = d;
					}
					if (d < 0.995f)
					{
						ok = false;
					}
				}
				body.seamNSync[g3] = ok;
				body.seamAligned[g3] = minDot > -0.25f;
			}
			body.seamOf = new int[baseV.Length];
			for (int i6 = 0; i6 < baseV.Length; i6++)
			{
				body.seamOf[i6] = -1;
			}
			body.seamN = new Vector3[body.seamGroups.Length];
			for (int g4 = 0; g4 < body.seamGroups.Length; g4++)
			{
				int[] grp2 = body.seamGroups[g4];
				Vector3 an = Vector3.zero;
				for (int j3 = 0; j3 < grp2.Length; j3++)
				{
					body.seamOf[grp2[j3]] = g4;
					an += baseN[grp2[j3]];
				}
				float m2 = an.magnitude;
				body.seamN[g4] = ((m2 > 1E-06f) ? (an / m2) : baseN[grp2[0]]);
			}
			for (int g5 = 0; g5 < body.seamGroups.Length; g5++)
			{
				if (body.seamAligned[g5])
				{
					int[] grp3 = body.seamGroups[g5];
					List<int> union = new List<int>(12);
					for (int j4 = 0; j4 < grp3.Length; j4++)
					{
						List<int> lst = adjSets[grp3[j4]];
						for (int q = 0; q < lst.Count; q++)
						{
							int nb2 = lst[q];
							bool self = false;
							for (int w7 = 0; w7 < grp3.Length; w7++)
							{
								if (nb2 == grp3[w7])
								{
									self = true;
									break;
								}
							}
							if (!self)
							{
								SoftFleshController.AddAdj(union, nb2);
							}
						}
					}
					for (int j5 = 0; j5 < grp3.Length; j5++)
					{
						adjSets[grp3[j5]] = union;
					}
				}
			}
			body.adjStart = new int[baseV.Length + 1];
			List<int> flat = new List<int>(baseV.Length * 6);
			for (int i7 = 0; i7 < baseV.Length; i7++)
			{
				body.adjStart[i7] = flat.Count;
				flat.AddRange(adjSets[i7]);
			}
			body.adjStart[baseV.Length] = flat.Count;
			body.adjList = flat.ToArray();
			float esum = 0f;
			int ecnt = 0;
			for (int i8 = 0; i8 < baseV.Length; i8++)
			{
				for (int k5 = body.adjStart[i8]; k5 < body.adjStart[i8 + 1]; k5++)
				{
					esum += (baseV[body.adjList[k5]] - baseV[i8]).magnitude;
					ecnt++;
				}
			}
			body.avgEdge = ((ecnt > 0) ? (esum / (float)ecnt) : 0f);
			float[] ve = new float[baseV.Length];
			float mn = 1E+09f;
			for (int i9 = 0; i9 < baseV.Length; i9++)
			{
				int a2 = body.adjStart[i9];
				int a3 = body.adjStart[i9 + 1];
				if (a3 <= a2)
				{
					ve[i9] = body.avgEdge;
				}
				else
				{
					float sm = 0f;
					for (int k6 = a2; k6 < a3; k6++)
					{
						sm += (baseV[body.adjList[k6]] - baseV[i9]).magnitude;
					}
					ve[i9] = sm / (float)(a3 - a2);
					if (ve[i9] < 1E-05f)
					{
						ve[i9] = body.avgEdge;
					}
					if (ve[i9] < mn)
					{
						mn = ve[i9];
					}
				}
			}
			body.vertEdge = ve;
			body.minEdge = ((mn < 1E+09f) ? mn : body.avgEdge);
			body.foldRegion = new bool[baseV.Length];
			List<int> frontier = new List<int>();
			if (body.seamGroups != null)
			{
				for (int g6 = 0; g6 < body.seamGroups.Length; g6++)
				{
					if (!body.seamAligned[g6])
					{
						int[] grp4 = body.seamGroups[g6];
						for (int j6 = 0; j6 < grp4.Length; j6++)
						{
							if (!body.foldRegion[grp4[j6]])
							{
								body.foldRegion[grp4[j6]] = true;
								frontier.Add(grp4[j6]);
							}
						}
					}
				}
			}
			for (int ring = 0; ring < 4; ring++)
			{
				List<int> next = new List<int>();
				for (int q2 = 0; q2 < frontier.Count; q2++)
				{
					int i10 = frontier[q2];
					for (int k7 = body.adjStart[i10]; k7 < body.adjStart[i10 + 1]; k7++)
					{
						int j7 = body.adjList[k7];
						if (!body.foldRegion[j7])
						{
							body.foldRegion[j7] = true;
							next.Add(j7);
						}
					}
				}
				frontier = next;
			}
			body.armSide = new byte[baseV.Length];
			body.noDent = new bool[baseV.Length];
			body.nipW = new float[baseV.Length];
			body.kokW = new float[baseV.Length];
			body.anaW = new float[baseV.Length];
			body.petW = new float[baseV.Length];
			body.vagW = new float[baseV.Length];
			BoneWeight[] bw = mesh.boneWeights;
			Transform[] bones = smr.bones;
			int armCount = 0;
			int noCount = 0;
			int nipCount = 0;
			int kokCount = 0;
			int vagCount = 0;
			if (bw != null && bw.Length == baseV.Length && bones != null)
			{
				Matrix4x4[] binds = mesh.bindposes;
				body.bonesArr = bones;
				int nb3 = bones.Length;
				int[] domOf = new int[baseV.Length];
				body.domLocalPos = new Vector3[baseV.Length];
				int[] counts = new int[nb3 + 1];
				body.grpMin = new Vector3[nb3];
				body.grpMax = new Vector3[nb3];
				for (int b4 = 0; b4 < nb3; b4++)
				{
					body.grpMin[b4] = new Vector3(1E+09f, 1E+09f, 1E+09f);
					body.grpMax[b4] = new Vector3(-1E+09f, -1E+09f, -1E+09f);
				}
				for (int i11 = 0; i11 < baseV.Length; i11++)
				{
					int di2;
					float dw2;
					SoftFleshController.DominantOf(bw[i11], out di2, out dw2);
					if (di2 < 0 || di2 >= nb3)
					{
						di2 = 0;
					}
					domOf[i11] = di2;
					counts[di2]++;
					if (binds != null && di2 < binds.Length)
					{
						Vector3 lp = binds[di2].MultiplyPoint3x4(baseV[i11]);
						body.domLocalPos[i11] = lp;
						Vector3 lo = body.grpMin[di2];
						Vector3 hi = body.grpMax[di2];
						if (lp.x < lo.x)
						{
							lo.x = lp.x;
						}
						if (lp.x > hi.x)
						{
							hi.x = lp.x;
						}
						if (lp.y < lo.y)
						{
							lo.y = lp.y;
						}
						if (lp.y > hi.y)
						{
							hi.y = lp.y;
						}
						if (lp.z < lo.z)
						{
							lo.z = lp.z;
						}
						if (lp.z > hi.z)
						{
							hi.z = lp.z;
						}
						body.grpMin[di2] = lo;
						body.grpMax[di2] = hi;
					}
				}
				body.domBone = domOf;
				int cap = baseV.Length;
				body.actList.Capacity = cap;
				body.candList.Capacity = cap;
				body.dirty.Capacity = cap;
				body.corrList.Capacity = cap;
				body.simList.Capacity = cap;
				body.prevSim.Capacity = cap;
				body.bakeV.Capacity = cap;
				body.bakeN.Capacity = cap;
				body.outV.Capacity = cap;
				body.normList.Capacity = cap;
				body.refV.Capacity = cap;
				body.origVertN = ((body.origMesh != null) ? body.origMesh.vertexCount : baseV.Length);
				body.bwArr = ((body.workMesh != null) ? body.workMesh.boneWeights : null);
				body.bindPoses = ((body.workMesh != null) ? body.workMesh.bindposes : null);
				body.subPa = null;
				body.subPb = null;
				body.subFix = null;
				if (body.origVertN < baseV.Length && body.adjStart != null && body.adjList != null)
				{
					body.subPa = new int[baseV.Length];
					body.subPb = new int[baseV.Length];
					body.subFix = new Vector3[baseV.Length];
					int paired = 0;
					int pairedDeep = 0;
					bool exact = false;
					for (int i12 = 0; i12 < baseV.Length; i12++)
					{
						body.subPa[i12] = -1;
						body.subPb[i12] = -1;
					}
					int nNew = baseV.Length - body.origVertN;
					int[] parTab = null;
					if (body.workMesh != null)
					{
						SoftFleshSubdivide.ParentsByMesh.TryGetValue(body.workMesh.GetInstanceID(), out parTab);
					}
					if (parTab == null && smr != null && smr.sharedMesh != null)
					{
						SoftFleshSubdivide.ParentsByMesh.TryGetValue(smr.sharedMesh.GetInstanceID(), out parTab);
					}
					if (parTab != null && parTab.Length == nNew * 2)
					{
						exact = true;
						for (int i13 = body.origVertN; i13 < baseV.Length; i13++)
						{
							int k8 = (i13 - body.origVertN) * 2;
							int pa = parTab[k8];
							int pb = parTab[k8 + 1];
							if (pa >= 0 && pb >= 0 && pa < baseV.Length && pb < baseV.Length)
							{
								body.subPa[i13] = pa;
								body.subPb[i13] = pb;
								paired++;
								if (pa >= body.origVertN || pb >= body.origVertN)
								{
									pairedDeep++;
								}
							}
						}
					}
					else
					{
						for (int i14 = body.origVertN; i14 < baseV.Length; i14++)
						{
							int s0 = body.adjStart[i14];
							int s = body.adjStart[i14 + 1];
							int k9 = s0;
							while (k9 < s && body.subPa[i14] < 0)
							{
								int j8 = body.adjList[k9];
								if (j8 < body.origVertN)
								{
									for (int k10 = k9 + 1; k10 < s; k10++)
									{
										int j9 = body.adjList[k10];
										if (j9 < body.origVertN && ((baseV[j8] + baseV[j9]) * 0.5f - baseV[i14]).sqrMagnitude < 1E-10f)
										{
											body.subPa[i14] = j8;
											body.subPb[i14] = j9;
											paired++;
											break;
										}
									}
								}
								k9++;
							}
						}
					}
					SoftFleshPlugin.Log(string.Concat(new string[]
					{
						"[SF] subdiv surface fix: ",
						paired.ToString(),
						"/",
						nNew.ToString(),
						" new verts paired to their parent edge (",
						pairedDeep.ToString(),
						" to midpoint parents, ",
						exact ? "exact from the subdivider" : "geometric, originals only",
						")"
					}));
				}
				body.bindRot = new Quaternion[nb3];
				for (int b5 = 0; b5 < nb3; b5++)
				{
					body.bindRot[b5] = Quaternion.identity;
					if (binds != null && b5 < binds.Length)
					{
						Vector3 f = binds[b5].GetColumn(2);
						Vector3 u7 = binds[b5].GetColumn(1);
						if (f.sqrMagnitude > 1E-10f && u7.sqrMagnitude > 1E-10f)
						{
							body.bindRot[b5] = Quaternion.LookRotation(f, u7);
						}
					}
				}
				body.grpStart = new int[nb3 + 1];
				for (int b6 = 0; b6 < nb3; b6++)
				{
					body.grpStart[b6 + 1] = body.grpStart[b6] + counts[b6];
				}
				body.grpVerts = new int[baseV.Length];
				int[] fill = new int[nb3];
				for (int i15 = 0; i15 < baseV.Length; i15++)
				{
					int b7 = domOf[i15];
					body.grpVerts[body.grpStart[b7] + fill[b7]] = i15;
					fill[b7]++;
				}
				string[] lowNames = new string[bones.Length];
				for (int bi7 = 0; bi7 < bones.Length; bi7++)
				{
					lowNames[bi7] = ((bones[bi7] != null) ? bones[bi7].name.ToLower() : "");
				}
				for (int i16 = 0; i16 < baseV.Length; i16++)
				{
					BoneWeight w8 = bw[i16];
					float wn = 0f;
					if (w8.boneIndex0 >= 0 && w8.boneIndex0 < lowNames.Length && lowNames[w8.boneIndex0].Contains("bnip"))
					{
						wn += w8.weight0;
					}
					if (w8.boneIndex1 >= 0 && w8.boneIndex1 < lowNames.Length && lowNames[w8.boneIndex1].Contains("bnip"))
					{
						wn += w8.weight1;
					}
					if (w8.boneIndex2 >= 0 && w8.boneIndex2 < lowNames.Length && lowNames[w8.boneIndex2].Contains("bnip"))
					{
						wn += w8.weight2;
					}
					if (w8.boneIndex3 >= 0 && w8.boneIndex3 < lowNames.Length && lowNames[w8.boneIndex3].Contains("bnip"))
					{
						wn += w8.weight3;
					}
					if (wn > 0f)
					{
						body.nipW[i16] = ((wn > 1f) ? 1f : wn);
						nipCount++;
					}
					float wk = 0f;
					if (w8.boneIndex0 >= 0 && w8.boneIndex0 < lowNames.Length && lowNames[w8.boneIndex0].Contains("kokan"))
					{
						wk += w8.weight0;
					}
					if (w8.boneIndex1 >= 0 && w8.boneIndex1 < lowNames.Length && lowNames[w8.boneIndex1].Contains("kokan"))
					{
						wk += w8.weight1;
					}
					if (w8.boneIndex2 >= 0 && w8.boneIndex2 < lowNames.Length && lowNames[w8.boneIndex2].Contains("kokan"))
					{
						wk += w8.weight2;
					}
					if (w8.boneIndex3 >= 0 && w8.boneIndex3 < lowNames.Length && lowNames[w8.boneIndex3].Contains("kokan"))
					{
						wk += w8.weight3;
					}
					if (wk > 0f)
					{
						body.kokW[i16] = ((wk > 1f) ? 1f : wk);
						kokCount++;
					}
					float wa = 0f;
					if (w8.boneIndex0 >= 0 && w8.boneIndex0 < lowNames.Length && lowNames[w8.boneIndex0].Contains("ana"))
					{
						wa += w8.weight0;
					}
					if (w8.boneIndex1 >= 0 && w8.boneIndex1 < lowNames.Length && lowNames[w8.boneIndex1].Contains("ana"))
					{
						wa += w8.weight1;
					}
					if (w8.boneIndex2 >= 0 && w8.boneIndex2 < lowNames.Length && lowNames[w8.boneIndex2].Contains("ana"))
					{
						wa += w8.weight2;
					}
					if (w8.boneIndex3 >= 0 && w8.boneIndex3 < lowNames.Length && lowNames[w8.boneIndex3].Contains("ana"))
					{
						wa += w8.weight3;
					}
					if (wa > 0f)
					{
						body.anaW[i16] = ((wa > 1f) ? 1f : wa);
					}
					float wp = 0f;
					if (w8.boneIndex0 >= 0 && w8.boneIndex0 < lowNames.Length && lowNames[w8.boneIndex0].Contains("vagina") && !lowNames[w8.boneIndex0].Contains("inner"))
					{
						wp += w8.weight0;
					}
					if (w8.boneIndex1 >= 0 && w8.boneIndex1 < lowNames.Length && lowNames[w8.boneIndex1].Contains("vagina") && !lowNames[w8.boneIndex1].Contains("inner"))
					{
						wp += w8.weight1;
					}
					if (w8.boneIndex2 >= 0 && w8.boneIndex2 < lowNames.Length && lowNames[w8.boneIndex2].Contains("vagina") && !lowNames[w8.boneIndex2].Contains("inner"))
					{
						wp += w8.weight2;
					}
					if (w8.boneIndex3 >= 0 && w8.boneIndex3 < lowNames.Length && lowNames[w8.boneIndex3].Contains("vagina") && !lowNames[w8.boneIndex3].Contains("inner"))
					{
						wp += w8.weight3;
					}
					if (wp > 0f)
					{
						body.petW[i16] = ((wp > 1f) ? 1f : wp);
					}
					float wv = 0f;
					if (w8.boneIndex0 >= 0 && w8.boneIndex0 < lowNames.Length && lowNames[w8.boneIndex0].Contains("vagina"))
					{
						wv += w8.weight0;
					}
					if (w8.boneIndex1 >= 0 && w8.boneIndex1 < lowNames.Length && lowNames[w8.boneIndex1].Contains("vagina"))
					{
						wv += w8.weight1;
					}
					if (w8.boneIndex2 >= 0 && w8.boneIndex2 < lowNames.Length && lowNames[w8.boneIndex2].Contains("vagina"))
					{
						wv += w8.weight2;
					}
					if (w8.boneIndex3 >= 0 && w8.boneIndex3 < lowNames.Length && lowNames[w8.boneIndex3].Contains("vagina"))
					{
						wv += w8.weight3;
					}
					if (wv > 0f)
					{
						body.vagW[i16] = ((wv > 1f) ? 1f : wv);
						vagCount++;
					}
					int di3;
					float dw3;
					SoftFleshController.DominantOf(w8, out di3, out dw3);
					if (di3 >= 0 && di3 < bones.Length && !(bones[di3] == null))
					{
						string bn = lowNames[di3];
						if (!faceMode && (bn.Contains("head") || bn.Contains("neck") || bn.Contains("kubi")))
						{
							body.noDent[i16] = true;
							noCount++;
						}
						else
						{
							bool isArm = bn.Contains("arm") || bn.Contains("hand") || bn.Contains("elbo") || bn.Contains("wrist") || bn.Contains("shoulder") || bn.Contains("thumb") || bn.Contains("index") || bn.Contains("middle") || bn.Contains("ring") || bn.Contains("little") || bn.Contains("finger");
							if (isArm)
							{
								body.armSide[i16] = ((bn.EndsWith("_r") || bn.Contains("_r_")) ? 2 : 1);
								armCount++;
							}
						}
					}
				}
			}
			if (cha.objBodyBone != null)
			{
				HashSet<Transform> skinned = new HashSet<Transform>();
				if (smr.bones != null)
				{
					foreach (Transform bb in smr.bones)
					{
						if (bb != null)
						{
							skinned.Add(bb);
						}
					}
				}
				SoftFleshController.CollectVaginaBones(cha.objBodyBone.transform, body, skinned);
			}
			body.vagDyn.Clear();
			foreach (DynamicBone db in cha.objBodyBone.GetComponentsInChildren<DynamicBone>(true))
			{
				if (!(db == null) && !(db.m_Root == null) && db.m_Root.name.ToLower().Contains("vagina"))
				{
					body.vagDyn.Add(db);
				}
			}
			SoftFleshController.CacheDynParticles(body);
			this.rigInfo = string.Concat(new string[]
			{
				"vagina rig: pivots=",
				body.vagPivots.Count.ToString(),
				" inner=",
				body.vagInner.Count.ToString(),
				" dynChains=",
				body.vagDyn.Count.ToString(),
				(body.vagRoot != null) ? " +root" : " (no root)"
			});
			body.kosiT = (body.kokanDT = null);
			body.crotchCarry = false;
			if (cha.objBodyBone != null)
			{
				body.vagRootT = null;
				body.anaDT = null;
				body.siriLT = (body.siriRT = null);
				body.siriSLT = (body.siriSRT = null);
				SoftFleshController.FindCrotchPair(cha.objBodyBone.transform, ref body.kosiT, ref body.kokanDT, ref body.vagRootT, ref body.anaDT, ref body.siriLT, ref body.siriRT, ref body.siriSLT, ref body.siriSRT);
				if (body.kosiT != null && body.kokanDT != null)
				{
					body.kosiRestRot = body.kosiT.localRotation;
					body.kosiRestPos = body.kosiT.localPosition;
					body.kokanRestRot = body.kokanDT.localRotation;
					body.kokanRestPos = body.kokanDT.localPosition;
					if (body.vagRootT != null)
					{
						body.vagRootRestRot = body.vagRootT.localRotation;
						body.vagRootRestPos = body.vagRootT.localPosition;
					}
					if (body.anaDT != null)
					{
						body.anaRestRot = body.anaDT.localRotation;
						body.anaRestPos = body.anaDT.localPosition;
					}
					if (body.siriLT != null)
					{
						body.siriLRestRot = body.siriLT.localRotation;
						body.siriLRestPos = body.siriLT.localPosition;
					}
					if (body.siriSLT != null)
					{
						body.siriSLRestRot = body.siriSLT.localRotation;
						body.siriSLRestPos = body.siriSLT.localPosition;
					}
					if (body.siriSRT != null)
					{
						body.siriSRRestRot = body.siriSRT.localRotation;
						body.siriSRRestPos = body.siriSRT.localPosition;
					}
					if (body.siriRT != null)
					{
						body.siriRRestRot = body.siriRT.localRotation;
						body.siriRRestPos = body.siriRT.localPosition;
					}
					body.crotchCarry = true;
					float off = Quaternion.Angle(body.kosiRestRot, Quaternion.identity);
					SoftFleshPlugin.Log("[SF] crotch carry: vagina root " + ((body.vagRootT != null) ? body.vagRootT.name : "NOT FOUND - Isolate will fall back to Carry"));
					SoftFleshPlugin.Log(string.Concat(new string[]
					{
						"[SF] crotch carry: cf_s_waist02 rest ",
						off.ToString("F1"),
						"deg, cf_d_kokan rest offset ",
						((body.kokanRestPos - body.kosiRestPos) * 1000f).ToString("F1"),
						"mm"
					}));
					if (off > 0.5f)
					{
						SoftFleshPlugin.Log("[SF] WARN crotch carry: cf_s_waist02 was already rotated " + off.ToString("F1") + "deg at Setup - turn the Joint CROTCH toggle OFF and Setup again");
					}
				}
				else
				{
					SoftFleshPlugin.Log("[SF] crotch carry: cf_s_waist02 or cf_d_kokan not found - carry off");
				}
			}
			body.spreadL = (body.spreadR = null);
			if (cha.objBodyBone != null && smr.bones != null)
			{
				HashSet<Transform> skinnedSet = new HashSet<Transform>();
				foreach (Transform bb2 in smr.bones)
				{
					if (bb2 != null)
					{
						skinnedSet.Add(bb2);
					}
				}
				SoftFleshController.CollectSpread(cha.objBodyBone.transform, body, skinnedSet);
			}
			if (body.spreadL != null)
			{
				body.spreadLRot = body.spreadL.localRotation;
			}
			if (body.spreadR != null)
			{
				body.spreadRRot = body.spreadR.localRotation;
			}
			if (body.spreadL != null)
			{
				body.spreadLPos = body.spreadL.localPosition;
			}
			if (body.spreadR != null)
			{
				body.spreadRPos = body.spreadR.localPosition;
			}
			SoftFleshPlugin.Log("[SF] spread helpers: " + ((body.spreadL != null) ? "L ok" : "L MISSING") + " / " + ((body.spreadR != null) ? "R ok" : "R MISSING"));
			if (smr.bones != null && mesh != null)
			{
				Matrix4x4[] bpsC = mesh.bindposes;
				Matrix4x4 refM = Matrix4x4.identity;
				bool haveRef = false;
				int b8 = 0;
				while (b8 < smr.bones.Length && b8 < bpsC.Length)
				{
					if (smr.bones[b8] != null && smr.bones[b8].name == "cf_j_kokan")
					{
						refM = smr.bones[b8].localToWorldMatrix * bpsC[b8];
						haveRef = true;
						break;
					}
					b8++;
				}
				if (haveRef)
				{
					StringBuilder sb5 = new StringBuilder("[SF] bone drift vs cf_j_kokan: ");
					int b9 = 0;
					while (b9 < smr.bones.Length && b9 < bpsC.Length)
					{
						Transform t4 = smr.bones[b9];
						if (!(t4 == null))
						{
							string n2 = t4.name;
							if (!(n2 != "k_f_ana_00") || !(n2 != "k_f_ana_01") || !(n2 != "cf_s_ana") || !(n2 != "cf_J_Vagina_F") || !(n2 != "cf_s_siri_L"))
							{
								Matrix4x4 m3 = t4.localToWorldMatrix * bpsC[b9];
								float dev = 0f;
								for (int r5 = 0; r5 < 4; r5++)
								{
									for (int c2 = 0; c2 < 4; c2++)
									{
										float d2 = Mathf.Abs(m3[r5, c2] - refM[r5, c2]);
										if (d2 > dev)
										{
											dev = d2;
										}
									}
								}
								sb5.Append(n2).Append('=').Append((dev * 1000f).ToString("F1"))
									.Append("  ");
							}
						}
						b9++;
					}
					SoftFleshPlugin.Log(sb5.ToString() + "(x1000; ~0 = agrees with the rest of the rig)");
				}
			}
			SoftFleshPlugin.Log("[SF] " + this.rigInfo);
			this.MeasureRingFrame(body, smr);
			int bc = nm.blendShapeCount;
			StringBuilder found = new StringBuilder();
			for (int bi8 = 0; bi8 < bc; bi8++)
			{
				string bn2 = nm.GetBlendShapeName(bi8);
				string lb2 = bn2.ToLower();
				bool dep = lb2.Contains("penetrat") || lb2.Contains("deep");
				bool wid = lb2.Contains("vagina") || lb2.Contains("gap") || lb2.Contains("spread") || lb2.Contains("labia") || lb2.Contains("kupa");
				if (dep || wid)
				{
					body.bsAll.Add(bi8);
					if (dep)
					{
						body.bsDepth.Add(bi8);
					}
					else
					{
						body.bsWidth.Add(bi8);
					}
					found.Append(bn2).Append(' ');
				}
			}
			if (body.bsAll.Count > 0)
			{
				string text = "[SF] authored opening shapes (";
				string text2 = body.bsAll.Count.ToString();
				string text3 = "): ";
				StringBuilder stringBuilder = found;
				SoftFleshPlugin.Log(text + text2 + text3 + ((stringBuilder != null) ? stringBuilder.ToString() : null));
			}
			if (body.domBone != null && body.bonesArr != null)
			{
				Mesh rest = new Mesh();
				smr.BakeMesh(rest);
				Vector3[] rv = rest.vertices;
				UnityEngine.Object.Destroy(rest);
				if (rv != null && rv.Length == baseV.Length)
				{
					int nv = baseV.Length;
					Vector3[] a4 = new Vector3[nv];
					Vector3[] b10 = new Vector3[nv];
					Array.Copy(rv, a4, nv);
					for (int pass = 0; pass < 8; pass++)
					{
						float lam = (((pass & 1) == 0) ? 0.5f : (-0.53f));
						for (int i17 = 0; i17 < nv; i17++)
						{
							Vector3 sum = Vector3.zero;
							int c3 = 0;
							for (int k11 = body.adjStart[i17]; k11 < body.adjStart[i17 + 1]; k11++)
							{
								sum += a4[body.adjList[k11]];
								c3++;
							}
							b10[i17] = ((c3 > 0) ? (a4[i17] + (sum / (float)c3 - a4[i17]) * lam) : a4[i17]);
						}
						Vector3[] sw2 = a4;
						a4 = b10;
						b10 = sw2;
					}
					Matrix4x4 l2wS = SoftFleshController.BakeL2W(smr);
					body.corrRest = new Vector3[nv];
					for (int i18 = 0; i18 < nv; i18++)
					{
						Vector3 dW = l2wS.MultiplyVector(a4[i18] - rv[i18]);
						int db2 = body.domBone[i18];
						body.corrRest[i18] = ((db2 >= 0 && db2 < body.bonesArr.Length && body.bonesArr[db2] != null) ? (Quaternion.Inverse(body.bonesArr[db2].rotation * body.bindRot[db2]) * dW) : Vector3.zero);
					}
				}
			}
			nm.name = "SF_work";
			smr.sharedMesh = nm;
			this._ourMeshes[smr] = nm;
			SoftFleshController.LogSpreadWeights(smr, body.origMesh, nm);
			this._bodies.Add(body);
			if (faceMode)
			{
				this.SetupMouth(body);
			}
			this.diag = string.Concat(new string[]
			{
				this.diag,
				"smr=",
				smr.name,
				" v=",
				baseV.Length.ToString(),
				" arm=",
				armCount.ToString(),
				" head=",
				noCount.ToString(),
				" nip=",
				nipCount.ToString(),
				" kok=",
				kokCount.ToString(),
				" vag=",
				vagCount.ToString(),
				"\n"
			});
			StringBuilder hb = new StringBuilder();
			foreach (Transform b11 in smr.bones)
			{
				if (b11 != null)
				{
					string ln = b11.name.ToLower();
					if (ln.Contains("hand") || ln.Contains("index") || ln.Contains("middle") || ln.Contains("ring") || ln.Contains("little") || ln.Contains("thumb"))
					{
						hb.Append(b11.name).Append(' ');
					}
				}
			}
			SoftFleshPlugin.Log("[SF-handbones] " + hb.ToString());
		}

		// Token: 0x0600006D RID: 109 RVA: 0x0000EE30 File Offset: 0x0000D030
		private static void LogSpreadWeights(SkinnedMeshRenderer smr, Mesh src, Mesh dst)
		{
			Transform[] bones = smr.bones;
			int i0 = -1;
			int i = -1;
			int b = 0;
			while (bones != null && b < bones.Length)
			{
				if (!(bones[b] == null))
				{
					if (bones[b].name == "k_f_ana_00")
					{
						i0 = b;
					}
					else if (bones[b].name == "k_f_ana_01")
					{
						i = b;
					}
				}
				b++;
			}
			if (i0 < 0 && i < 0)
			{
				SoftFleshPlugin.Log("[SF] spread weights: helper bones not in smr.bones");
				return;
			}
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] spread weights: idx ",
				i0.ToString(),
				"/",
				i.ToString(),
				" bones=",
				((bones != null) ? bones.Length : 0).ToString(),
				" bindposes src=",
				((src != null && src.bindposes != null) ? src.bindposes.Length : (-1)).ToString(),
				" dst=",
				((dst != null && dst.bindposes != null) ? dst.bindposes.Length : (-1)).ToString(),
				" | src ",
				SoftFleshController.SumSpread(src, i0, i),
				" | dst ",
				SoftFleshController.SumSpread(dst, i0, i)
			}));
		}

		// Token: 0x0600006E RID: 110 RVA: 0x0000EF84 File Offset: 0x0000D184
		private static string SumSpread(Mesh m, int i0, int i1)
		{
			if (m == null)
			{
				return "(null)";
			}
			BoneWeight[] w = m.boneWeights;
			if (w == null || w.Length == 0)
			{
				return "(no weights)";
			}
			float tot = 0f;
			int cnt = 0;
			float peak = 0f;
			foreach (BoneWeight b in w)
			{
				float f = 0f;
				if (b.boneIndex0 == i0 || b.boneIndex0 == i1)
				{
					f += b.weight0;
				}
				if (b.boneIndex1 == i0 || b.boneIndex1 == i1)
				{
					f += b.weight1;
				}
				if (b.boneIndex2 == i0 || b.boneIndex2 == i1)
				{
					f += b.weight2;
				}
				if (b.boneIndex3 == i0 || b.boneIndex3 == i1)
				{
					f += b.weight3;
				}
				if (f > 0.001f)
				{
					tot += f;
					cnt++;
					if (f > peak)
					{
						peak = f;
					}
				}
			}
			return string.Concat(new string[]
			{
				cnt.ToString(),
				" verts/",
				w.Length.ToString(),
				" sum=",
				tot.ToString("F1"),
				" peak=",
				peak.ToString("F2")
			});
		}

		// Token: 0x0600006F RID: 111 RVA: 0x0000F0E0 File Offset: 0x0000D2E0
		private static bool MeshFitsBones(SkinnedMeshRenderer smr, Mesh m)
		{
			if (smr == null || m == null)
			{
				return false;
			}
			Matrix4x4[] bp = m.bindposes;
			return smr.bones == null || bp == null || bp.Length == smr.bones.Length;
		}

		// Token: 0x06000070 RID: 112 RVA: 0x0000F124 File Offset: 0x0000D324
		private bool StillOurs(SkinnedMeshRenderer smr, Mesh cachedOrig)
		{
			Mesh cur = smr.sharedMesh;
			Mesh ours;
			if (!(cur == cachedOrig) && (!this._ourMeshes.TryGetValue(smr, out ours) || !(cur == ours)))
			{
				return false;
			}
			Matrix4x4[] bp = cachedOrig.bindposes;
			return smr.bones == null || bp == null || bp.Length == smr.bones.Length;
		}

		// Token: 0x06000071 RID: 113 RVA: 0x0000F188 File Offset: 0x0000D388
		private static void MeasureSpreadFlesh(SoftFleshController.Body body, SkinnedMeshRenderer smr)
		{
			if (body.spreadL == null && body.spreadR == null)
			{
				return;
			}
			if (body.lastSpreadMm <= 0.01f)
			{
				body.sprFlesh = 0f;
				return;
			}
			if (body.sprVerts == null)
			{
				Mesh i = smr.sharedMesh;
				BoneWeight[] w = ((i != null) ? i.boneWeights : null);
				Transform[] bones = smr.bones;
				int i2 = -1;
				int i3 = -1;
				int b = 0;
				while (bones != null && b < bones.Length)
				{
					if (!(bones[b] == null))
					{
						if (bones[b].name == "k_f_ana_00")
						{
							i2 = b;
						}
						else if (bones[b].name == "k_f_ana_01")
						{
							i3 = b;
						}
					}
					b++;
				}
				List<int> list = new List<int>();
				int j = 0;
				while (w != null && j < w.Length)
				{
					BoneWeight b2 = w[j];
					float f = 0f;
					if (b2.boneIndex0 == i2 || b2.boneIndex0 == i3)
					{
						f += b2.weight0;
					}
					if (b2.boneIndex1 == i2 || b2.boneIndex1 == i3)
					{
						f += b2.weight1;
					}
					if (b2.boneIndex2 == i2 || b2.boneIndex2 == i3)
					{
						f += b2.weight2;
					}
					if (b2.boneIndex3 == i2 || b2.boneIndex3 == i3)
					{
						f += b2.weight3;
					}
					if (f > 0.05f)
					{
						list.Add(j);
					}
					j++;
				}
				body.sprVerts = list.ToArray();
			}
			if (body.sprVerts.Length == 0)
			{
				body.sprFlesh = -1f;
				return;
			}
			if (body.sprBakeA == null)
			{
				body.sprBakeA = new Mesh();
				body.sprBakeB = new Mesh();
			}
			Vector3 keepL = ((body.spreadL != null) ? body.spreadL.localPosition : Vector3.zero);
			Vector3 keepR = ((body.spreadR != null) ? body.spreadR.localPosition : Vector3.zero);
			if (body.spreadL != null)
			{
				body.spreadL.localPosition = body.spreadLPos;
			}
			if (body.spreadR != null)
			{
				body.spreadR.localPosition = body.spreadRPos;
			}
			smr.BakeMesh(body.sprBakeA);
			if (body.spreadL != null)
			{
				body.spreadL.localPosition = keepL;
			}
			if (body.spreadR != null)
			{
				body.spreadR.localPosition = keepR;
			}
			smr.BakeMesh(body.sprBakeB);
			body.sprBakeA.GetVertices(body.sprA);
			body.sprBakeB.GetVertices(body.sprB);
			List<Vector3> A = body.sprA;
			List<Vector3> B = body.sprB;
			if (A.Count != B.Count)
			{
				body.sprFlesh = -1f;
				return;
			}
			float mx = 0f;
			for (int q = 0; q < body.sprVerts.Length; q++)
			{
				int k = body.sprVerts[q];
				if (k < A.Count)
				{
					float d = (B[k] - A[k]).sqrMagnitude;
					if (d > mx)
					{
						mx = d;
					}
				}
			}
			body.sprFlesh = Mathf.Sqrt(mx) * 1000f;
		}

		// Token: 0x06000072 RID: 114 RVA: 0x0000F4F8 File Offset: 0x0000D6F8
		public void ClearAll()
		{
			this.RestoreCloth();
			this.RestoreSquash();
			for (int i = 0; i < this._bodies.Count; i++)
			{
				SoftFleshController.Body b = this._bodies[i];
				if (b.smr != null && b.origMesh != null && SoftFleshController.MeshFitsBones(b.smr, b.origMesh))
				{
					b.smr.sharedMesh = b.origMesh;
				}
				if (b.workMesh != null)
				{
					UnityEngine.Object.Destroy(b.workMesh);
				}
				if (b.bake != null)
				{
					UnityEngine.Object.Destroy(b.bake);
				}
				if (b.refMesh != null)
				{
					UnityEngine.Object.Destroy(b.refMesh);
				}
			}
			this._bodies.Clear();
			this._pressers.Clear();
			this.ClearViz();
			SfTimeline.Refresh();
		}

		// Token: 0x06000073 RID: 115 RVA: 0x0000F5E8 File Offset: 0x0000D7E8
		private void OnEnable()
		{
			Camera.onPreCull = (Camera.CameraCallback)Delegate.Combine(Camera.onPreCull, new Camera.CameraCallback(this.OnCamPreCull));
			Camera.onPostRender = (Camera.CameraCallback)Delegate.Combine(Camera.onPostRender, new Camera.CameraCallback(this.OnCamPostRender));
			Canvas.willRenderCanvases += new Canvas.WillRenderCanvases(this.OnWillRenderCanvases);
			Font.textureRebuilt += SoftFleshController.OnFontRebuilt;
		}

		// Token: 0x06000074 RID: 116 RVA: 0x0000F658 File Offset: 0x0000D858
		private static void OnFontRebuilt(Font f)
		{
			SoftFleshController.FontRebuilds++;
			Texture tex = null;
			if (f != null && f.material != null)
			{
				tex = f.material.mainTexture;
			}
			SoftFleshController.FontLog = string.Concat(new string[]
			{
				"f",
				Time.frameCount.ToString(),
				" ",
				(f != null) ? f.name : "?",
				(tex != null) ? (" " + tex.width.ToString() + "x" + tex.height.ToString()) : "",
				(SoftFleshController.FontLog.Length > 0) ? ("   |   " + SoftFleshController.FontLog) : ""
			});
			if (SoftFleshController.FontLog.Length > 220)
			{
				SoftFleshController.FontLog = SoftFleshController.FontLog.Substring(0, 220);
			}
		}

		// Token: 0x06000075 RID: 117 RVA: 0x0000F76C File Offset: 0x0000D96C
		private void OnDisable()
		{
			Camera.onPreCull = (Camera.CameraCallback)Delegate.Remove(Camera.onPreCull, new Camera.CameraCallback(this.OnCamPreCull));
			Camera.onPostRender = (Camera.CameraCallback)Delegate.Remove(Camera.onPostRender, new Camera.CameraCallback(this.OnCamPostRender));
			Canvas.willRenderCanvases -= new Canvas.WillRenderCanvases(this.OnWillRenderCanvases);
			Font.textureRebuilt -= SoftFleshController.OnFontRebuilt;
		}

		// Token: 0x06000076 RID: 118 RVA: 0x0000F7DC File Offset: 0x0000D9DC
		private void OnCamPostRender(Camera cam)
		{
			this._tPost = SoftFleshController._sw.ElapsedTicks;
			if (this._cwBody == null || this._cwFrame != Time.frameCount || this._cwDone == Time.frameCount)
			{
				return;
			}
			this._cwDone = Time.frameCount;
			SoftFleshController.Body b = this._cwBody;
			float dk = ((b.kosiT != null) ? Quaternion.Angle(this._cwKosi, b.kosiT.localRotation) : (-1f));
			float ds = ((b.siriLT != null) ? Quaternion.Angle(this._cwSiriL, b.siriLT.localRotation) : (-1f));
			float dss = ((b.siriSLT != null) ? Quaternion.Angle(this._cwSiriSL, b.siriSLT.localRotation) : (-1f));
			float dsp = ((b.siriSLT != null) ? ((b.siriSLT.localPosition - this._cwSiriSLPos).magnitude * 1000f) : (-1f));
			float dp = ((b.kokanDT != null) ? ((b.kokanDT.localPosition - this._cwKokan).magnitude * 1000f) : (-1f));
			SoftFleshController.CrotchHold = string.Concat(new string[]
			{
				"kosi ",
				(dk < 0f) ? "n/a" : (dk.ToString("F1") + "deg"),
				"  siriL ",
				(ds < 0f) ? "n/a" : (ds.ToString("F1") + "deg"),
				"  s_siriL ",
				(dss < 0f) ? "n/a" : (dss.ToString("F1") + "deg/" + dsp.ToString("F1") + "mm"),
				"  kokan ",
				(dp < 0f) ? "n/a" : (dp.ToString("F1") + "mm"),
				(dk > 0.5f || ds > 0.5f || dss > 0.5f || dsp > 0.5f || dp > 0.5f) ? "  *** UNDONE between onPreCull and render - we are not the last writer ***" : "  (held to render)"
			});
			this.BoneDiffTick(b);
		}

		// Token: 0x06000077 RID: 119 RVA: 0x0000FA50 File Offset: 0x0000DC50
		private void BoneDiffTick(SoftFleshController.Body b)
		{
			if (b.smr == null || b.bonesArr == null)
			{
				return;
			}
			if (!this.boneSnapReq && b.boneSnapPos == null)
			{
				return;
			}
			if (!this.boneSnapReq && Time.unscaledTime < this._bdNext)
			{
				return;
			}
			this._bdNext = Time.unscaledTime + 0.25f;
			int i = b.bonesArr.Length;
			Matrix4x4 w2l = SoftFleshController.BakeW2L(b.smr);
			Quaternion rInv = Quaternion.Inverse(b.smr.transform.rotation);
			bool haveMesh = b.bandVerts != null && b.bakeV != null && b.bakeV.Count == b.baseVerts.Length;
			Vector3[] skinNow = null;
			if (haveMesh && b.bandBW != null && b.bindArr != null && b.bindArr.Length == i)
			{
				if (this._skinM == null || this._skinM.Length != i)
				{
					this._skinM = new Matrix4x4[i];
				}
				for (int j = 0; j < i; j++)
				{
					Transform t = b.bonesArr[j];
					this._skinM[j] = ((t != null) ? (w2l * t.localToWorldMatrix * b.bindArr[j]) : Matrix4x4.identity);
				}
				if (this._skinNowBuf == null || this._skinNowBuf.Length != b.bandVerts.Length)
				{
					this._skinNowBuf = new Vector3[b.bandVerts.Length];
				}
				skinNow = this._skinNowBuf;
				for (int k = 0; k < b.bandVerts.Length; k++)
				{
					Vector3 v = b.baseVerts[b.bandVerts[k]];
					BoneWeight w = b.bandBW[k];
					Vector3 acc = Vector3.zero;
					if (w.weight0 > 0f)
					{
						acc += this._skinM[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
					}
					if (w.weight1 > 0f)
					{
						acc += this._skinM[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
					}
					if (w.weight2 > 0f)
					{
						acc += this._skinM[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
					}
					if (w.weight3 > 0f)
					{
						acc += this._skinM[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
					}
					skinNow[k] = acc;
				}
			}
			if (this.boneSnapReq)
			{
				this.boneSnapReq = false;
				b.boneSnapPos = new Vector3[i];
				b.boneSnapRot = new Quaternion[i];
				b.boneSnapScl = new Vector3[i];
				for (int l = 0; l < i; l++)
				{
					Transform t2 = b.bonesArr[l];
					if (!(t2 == null))
					{
						b.boneSnapPos[l] = w2l.MultiplyPoint3x4(t2.position);
						b.boneSnapRot[l] = rInv * t2.rotation;
						b.boneSnapScl[l] = t2.lossyScale;
					}
				}
				if (haveMesh)
				{
					b.meshSnap = new Vector3[b.bandVerts.Length];
					b.pushSnap = new Vector3[b.bandVerts.Length];
					for (int m = 0; m < b.bandVerts.Length; m++)
					{
						b.meshSnap[m] = b.bakeV[b.bandVerts[m]];
						b.pushSnap[m] = ((b.pushB != null) ? b.pushB[b.bandVerts[m]] : Vector3.zero);
					}
					b.skinSnap = ((skinNow != null) ? ((Vector3[])skinNow.Clone()) : null);
					SoftFleshController.MeshDiff = "snapshot taken over " + b.bandVerts.Length.ToString() + " entrance verts" + ((skinNow == null) ? "  (live skinning unavailable - press Bake first)" : "");
				}
				else
				{
					SoftFleshController.MeshDiff = "no entrance band yet - press Bake first";
				}
				SoftFleshController.BoneDiff = "snapshot taken over " + i.ToString() + " bones - now flip the toggle you are testing";
				return;
			}
			if (haveMesh && b.meshSnap != null && b.meshSnap.Length == b.bandVerts.Length)
			{
				float mx = 0f;
				float sum = 0f;
				int over = 0;
				int mxK = -1;
				float pmx = 0f;
				float psum = 0f;
				for (int n = 0; n < b.bandVerts.Length; n++)
				{
					int i2 = b.bandVerts[n];
					float d = (b.bakeV[i2] - b.meshSnap[n]).magnitude * 1000f;
					sum += d;
					if (d > mx)
					{
						mx = d;
						mxK = n;
					}
					if (d > 1f)
					{
						over++;
					}
					float pd = ((b.pushB != null && b.pushSnap != null) ? ((b.pushB[i2] - b.pushSnap[n]).magnitude * 1000f) : (-1f));
					if (pd > 0f)
					{
						psum += pd;
						if (pd > pmx)
						{
							pmx = pd;
						}
					}
				}
				float smx = -1f;
				float ssum = 0f;
				int smxK = -1;
				int s = 0;
				int s2 = 0;
				int s3 = 0;
				string hot = "";
				if (skinNow != null && b.skinSnap != null && b.skinSnap.Length == skinNow.Length)
				{
					smx = 0f;
					Dictionary<string, int> hotBone = new Dictionary<string, int>();
					for (int k2 = 0; k2 < skinNow.Length; k2++)
					{
						float d2 = (skinNow[k2] - b.skinSnap[k2]).magnitude * 1000f;
						ssum += d2;
						if (d2 > smx)
						{
							smx = d2;
							smxK = k2;
						}
						if (d2 > 1f)
						{
							s++;
						}
						if (d2 > 3f)
						{
							s2++;
							string bn = SoftFleshController.BoneAt(b, b.bandVerts[k2]);
							int c0;
							hotBone.TryGetValue(bn, out c0);
							hotBone[bn] = c0 + 1;
						}
						if (d2 > 10f)
						{
							s3++;
						}
					}
					int best = 0;
					foreach (KeyValuePair<string, int> kv in hotBone)
					{
						if (kv.Value > best)
						{
							best = kv.Value;
							hot = kv.Key + " x" + kv.Value.ToString();
						}
					}
					if (hotBone.Count > 1)
					{
						hot = hot + " of " + hotBone.Count.ToString() + " bones";
					}
				}
				SoftFleshController.MeshDiff = string.Concat(new string[]
				{
					"entrance band ",
					b.bandVerts.Length.ToString(),
					" verts | SKINNING(live) ",
					(smx < 0f) ? "n/a" : string.Concat(new string[]
					{
						"max ",
						smx.ToString("F1"),
						"mm mean ",
						(ssum / (float)b.bandVerts.Length).ToString("F2"),
						"mm  >1mm ",
						s.ToString(),
						" >3mm ",
						s2.ToString(),
						" >10mm ",
						s3.ToString(),
						(hot.Length > 0) ? (" [" + hot + "]") : ""
					}),
					" | PLUGIN PUSH max ",
					pmx.ToString("F1"),
					"mm mean ",
					(psum / (float)b.bandVerts.Length).ToString("F2"),
					"mm | bakeV(may be stale) max ",
					mx.ToString("F1"),
					"mm over1mm ",
					over.ToString(),
					(smx >= 0f && smx < 0.5f && pmx < 0.5f) ? "   -> BOTH IDENTICAL: the toggle changes nothing here; the bakeV number is a staleness artifact" : ((smx >= pmx) ? ("   -> SKINNING moved it, worst on " + ((smxK >= 0) ? SoftFleshController.BoneAt(b, b.bandVerts[smxK]) : "?")) : ("   -> the PLUGIN's field moved it, worst on " + ((mxK >= 0) ? SoftFleshController.BoneAt(b, b.bandVerts[mxK]) : "?")))
				});
			}
			if (b.boneSnapPos == null || b.boneSnapPos.Length != i)
			{
				return;
			}
			float[] mm = new float[i];
			float[] dg = new float[i];
			float[] sc = new float[i];
			float[] score = new float[i];
			for (int i3 = 0; i3 < i; i3++)
			{
				Transform t3 = b.bonesArr[i3];
				if (!(t3 == null))
				{
					mm[i3] = (w2l.MultiplyPoint3x4(t3.position) - b.boneSnapPos[i3]).magnitude * 1000f;
					dg[i3] = Quaternion.Angle(b.boneSnapRot[i3], rInv * t3.rotation);
					if (b.boneSnapScl != null && i3 < b.boneSnapScl.Length)
					{
						Vector3 s4 = b.boneSnapScl[i3];
						Vector3 s5 = t3.lossyScale;
						float den = ((s4.magnitude > 1E-05f) ? s4.magnitude : 1E-05f);
						sc[i3] = (s5 - s4).magnitude / den * 100f;
					}
					float share = ((b.bandW != null && i3 < b.bandW.Length) ? b.bandW[i3] : 0f);
					score[i3] = (mm[i3] + dg[i3] * 0.5f + sc[i3] * 0.3f) * (share + 0.0005f);
				}
			}
			int[] ord = new int[i];
			for (int i4 = 0; i4 < i; i4++)
			{
				ord[i4] = i4;
			}
			Array.Sort<int>(ord, (int x, int y) => score[y].CompareTo(score[x]));
			StringBuilder sb = new StringBuilder();
			int shown = 0;
			int k3 = 0;
			while (k3 < i && shown < 6)
			{
				int i5 = ord[k3];
				if (mm[i5] < 0.2f && dg[i5] < 0.2f && sc[i5] < 0.2f)
				{
					break;
				}
				float share2 = ((b.bandW != null && i5 < b.bandW.Length) ? b.bandW[i5] : 0f);
				if (shown > 0)
				{
					sb.Append(" | ");
				}
				sb.Append((b.bonesArr[i5] != null) ? b.bonesArr[i5].name : "?").Append(' ').Append(mm[i5].ToString("F1"))
					.Append("mm ")
					.Append(dg[i5].ToString("F0"))
					.Append("deg scl ")
					.Append(sc[i5].ToString("F1"))
					.Append("% band ")
					.Append((share2 * 100f).ToString("F0"))
					.Append('%');
				shown++;
				k3++;
			}
			int rawI = -1;
			float rawS = -1f;
			for (int i6 = 0; i6 < i; i6++)
			{
				float v2 = mm[i6] + dg[i6] * 0.5f + sc[i6] * 0.3f;
				if (v2 > rawS)
				{
					rawS = v2;
					rawI = i6;
				}
			}
			string raw = ((rawI >= 0) ? string.Concat(new string[]
			{
				"  || largest of ALL ",
				i.ToString(),
				" bones: ",
				(b.bonesArr[rawI] != null) ? b.bonesArr[rawI].name : "?",
				" ",
				mm[rawI].ToString("F2"),
				"mm ",
				dg[rawI].ToString("F1"),
				"deg ",
				sc[rawI].ToString("F1"),
				"%"
			}) : "");
			SoftFleshController.BoneDiff = ((shown == 0) ? "nothing over threshold" : sb.ToString()) + raw;
		}

		// Token: 0x06000078 RID: 120 RVA: 0x00010798 File Offset: 0x0000E998
		private static long SwNow()
		{
			if (SoftFleshController._memDepth < SoftFleshController._memStk.Length)
			{
				SoftFleshController._memStk[SoftFleshController._memDepth] = GC.GetTotalMemory(false);
			}
			SoftFleshController._memDepth++;
			return SoftFleshController._sw.ElapsedTicks;
		}

		// Token: 0x06000079 RID: 121 RVA: 0x000107CF File Offset: 0x0000E9CF
		private static long SwNowNoMem()
		{
			return SoftFleshController._sw.ElapsedTicks;
		}

		// Token: 0x0600007A RID: 122 RVA: 0x000107DC File Offset: 0x0000E9DC
		private static void PhAdd(int i, long t0)
		{
			double ms = (double)(SoftFleshController._sw.ElapsedTicks - t0) * 1000.0 / (double)Stopwatch.Frequency;
			SoftFleshController._phAcc[i] += ms;
			SoftFleshController._phFrame[i] += ms;
			if (SoftFleshController._memDepth > 0)
			{
				SoftFleshController._memDepth--;
			}
			if (SoftFleshController._memDepth < SoftFleshController._memStk.Length)
			{
				long j = GC.GetTotalMemory(false);
				if (j > SoftFleshController._memStk[SoftFleshController._memDepth])
				{
					SoftFleshController._phGc[i] += (double)(j - SoftFleshController._memStk[SoftFleshController._memDepth]) / 1024.0;
				}
			}
		}

		// Token: 0x0600007B RID: 123 RVA: 0x00010888 File Offset: 0x0000EA88
		private static void PhFlush()
		{
			if (++SoftFleshController._phFrames < 30)
			{
				return;
			}
			for (int i = 0; i < 12; i++)
			{
				SoftFleshController.PhaseMs[i] = SoftFleshController._phAcc[i] / (double)SoftFleshController._phFrames;
				SoftFleshController._phAcc[i] = 0.0;
				SoftFleshController.PhaseKB[i] = SoftFleshController._phGc[i] / (double)SoftFleshController._phFrames;
				SoftFleshController._phGc[i] = 0.0;
			}
			SoftFleshController._phFrames = 0;
		}

		// Token: 0x0600007C RID: 124 RVA: 0x00010904 File Offset: 0x0000EB04
		private static void HitchTick()
		{
			int gcNow = GC.CollectionCount(0);
			if (SoftFleshController._gcPrev < 0)
			{
				SoftFleshController._gcPrev = gcNow;
			}
			SoftFleshController.GcThisFrame = gcNow - SoftFleshController._gcPrev;
			SoftFleshController._gcPrev = gcNow;
			SoftFleshController.GcTotal += SoftFleshController.GcThisFrame;
			float dtF = Time.unscaledDeltaTime * 1000f;
			if (SoftFleshController._hAvg <= 0f)
			{
				SoftFleshController._hAvg = dtF;
				return;
			}
			if (dtF > SoftFleshController._hAvg * 2.5f && dtF > 8f)
			{
				SoftFleshController._hCount++;
				SoftFleshController.HitchTotal++;
				if (SoftFleshController.GcThisFrame > 0)
				{
					SoftFleshController.GcSpikes++;
				}
				int worst = 1;
				double wv = -1.0;
				for (int i = 1; i < SoftFleshController._phFrame.Length; i++)
				{
					if (SoftFleshController._phFrame[i] > wv)
					{
						wv = SoftFleshController._phFrame[i];
						worst = i;
					}
				}
				SoftFleshController.Spike sp = new SoftFleshController.Spike
				{
					frame = Time.frameCount,
					dt = dtF,
					gc = SoftFleshController.GcThisFrame,
					ours = (float)SoftFleshController._phFrame[0],
					worst = worst,
					worstMs = (float)wv,
					act = SoftFleshController.LoadAct,
					sim = SoftFleshController.LoadSim,
					steps = SoftFleshController.LoadSteps,
					sU = SoftFleshController.SegU,
					sL1 = SoftFleshController.SegL1,
					sL2 = SoftFleshController.SegL2,
					sR = SoftFleshController.SegR,
					sE = SoftFleshController.SegE,
					cvL = SoftFleshController.CvPrevL,
					cvG = SoftFleshController.CvPrevG
				};
				if (sp.sL2 >= 30f)
				{
					if (SoftFleshController._lastBigL2 > 0 && sp.frame - SoftFleshController._lastBigL2 > 200 && sp.frame - SoftFleshController._lastBigL2 < 4000)
					{
						SoftFleshController.PredPeriod = sp.frame - SoftFleshController._lastBigL2;
					}
					SoftFleshController._lastBigL2 = sp.frame;
					if (SoftFleshController.PredPeriod > 200)
					{
						SoftFleshController.PredLo = sp.frame + SoftFleshController.PredPeriod - 80;
						SoftFleshController.PredHi = sp.frame + SoftFleshController.PredPeriod + 240;
					}
				}
				SoftFleshController._spikes[SoftFleshController._spikeAt] = sp;
				SoftFleshController._spikeAt = (SoftFleshController._spikeAt + 1) % SoftFleshController._spikes.Length;
				if (SoftFleshController._spikeN < SoftFleshController._spikes.Length)
				{
					SoftFleshController._spikeN++;
				}
				if (dtF > SoftFleshController.HitchWorstMs)
				{
					SoftFleshController.HitchWorstMs = dtF;
					for (int j = 0; j < SoftFleshController._phFrame.Length; j++)
					{
						SoftFleshController.HitchPhaseMs[j] = SoftFleshController._phFrame[j];
					}
					SoftFleshController.HitchOursMs = (float)SoftFleshController._phFrame[0];
					SoftFleshController.HitchPhase = SoftFleshController.PhaseNamesC[worst];
				}
			}
			for (int k = 0; k < SoftFleshController._phFrame.Length; k++)
			{
				SoftFleshController._phFrame[k] = 0.0;
			}
			SoftFleshController._hAvg += (dtF - SoftFleshController._hAvg) * 0.05f;
			SoftFleshController.AvgDt += (dtF - SoftFleshController.AvgDt) * 0.03f;
			SoftFleshController.AvgU += (SoftFleshController.SegU - SoftFleshController.AvgU) * 0.03f;
			SoftFleshController.AvgL1 += (SoftFleshController.SegL1 - SoftFleshController.AvgL1) * 0.03f;
			SoftFleshController.AvgL2 += (SoftFleshController.SegL2 - SoftFleshController.AvgL2) * 0.03f;
			SoftFleshController.AvgR += (SoftFleshController.SegR - SoftFleshController.AvgR) * 0.03f;
			SoftFleshController.AvgE += (SoftFleshController.SegE - SoftFleshController.AvgE) * 0.03f;
			SoftFleshController.AvgOurs += ((float)(SoftFleshController._phFrame[0] + SoftFleshController._phFrame[11]) - SoftFleshController.AvgOurs) * 0.03f;
			SoftFleshController._hWinT += Time.unscaledDeltaTime;
			if (SoftFleshController._hWinT >= 5f)
			{
				SoftFleshController.HitchPerMin = (float)SoftFleshController._hCount * 60f / SoftFleshController._hWinT;
				SoftFleshController._hCount = 0;
				SoftFleshController._hWinT = 0f;
				SoftFleshController.HitchWorstMs *= 0.5f;
			}
		}

		// Token: 0x0600007D RID: 125 RVA: 0x00010D2C File Offset: 0x0000EF2C
		public static string SpikeTable()
		{
			if (SoftFleshController._spikeN == 0)
			{
				return "none yet";
			}
			StringBuilder sb = new StringBuilder(320);
			for (int i = 0; i < SoftFleshController._spikeN; i++)
			{
				int idx = (SoftFleshController._spikeAt - 1 - i + SoftFleshController._spikes.Length * 2) % SoftFleshController._spikes.Length;
				SoftFleshController.Spike sp = SoftFleshController._spikes[idx];
				sb.Append(Environment.NewLine).Append("            f").Append(sp.frame)
					.Append("  ")
					.Append(sp.dt.ToString("F0"))
					.Append("ms")
					.Append("  ours ")
					.Append(sp.ours.ToString("F1"))
					.Append("ms")
					.Append("  gc ")
					.Append(sp.gc)
					.Append("  worst ")
					.Append(SoftFleshController.PhaseNamesC[sp.worst])
					.Append(' ')
					.Append(sp.worstMs.ToString("F1"))
					.Append("  act ")
					.Append(sp.act)
					.Append(" sim ")
					.Append(sp.sim)
					.Append(" x")
					.Append(sp.steps)
					.Append("  U ")
					.Append(sp.sU.ToString("F0"))
					.Append(" L1 ")
					.Append(sp.sL1.ToString("F0"))
					.Append(" L2 ")
					.Append(sp.sL2.ToString("F0"))
					.Append(" R ")
					.Append(sp.sR.ToString("F0"))
					.Append(" E ")
					.Append(sp.sE.ToString("F0"))
					.Append("  cvQ ")
					.Append(sp.cvL)
					.Append('/')
					.Append(sp.cvG);
			}
			return sb.ToString();
		}

		// Token: 0x0600007E RID: 126 RVA: 0x00010F60 File Offset: 0x0000F160
		private void ParticleCensus()
		{
			ParticleSystem[] all = UnityEngine.Object.FindObjectsOfType<ParticleSystem>();
			StringBuilder sb = new StringBuilder(256);
			int i = 0;
			foreach (ParticleSystem ps in all)
			{
				if (!(ps == null))
				{
					float dur = ps.main.duration;
					bool loop = ps.main.loop;
					SoftFleshPlugin.Log(string.Concat(new string[]
					{
						"[SF][census] particle '",
						ps.name,
						"' under '",
						(ps.transform.parent != null) ? ps.transform.parent.name : "-",
						"'  duration ",
						dur.ToString("F2"),
						"s  loop ",
						loop.ToString(),
						"  alive ",
						ps.particleCount.ToString()
					}));
					bool sus = loop && ((dur > 6.5f && dur < 9.5f) || (dur > 45f && dur < 60f));
					if (sus && sb.Length < 220)
					{
						sb.Append((i++ > 0) ? "  |  " : "").Append(ps.name).Append(' ')
							.Append(dur.ToString("F1"))
							.Append("s alive ")
							.Append(ps.particleCount);
					}
				}
			}
			string text = all.Length.ToString();
			string text2 = " systems, ";
			string text3;
			if (i <= 0)
			{
				text3 = "none near 8s/54s";
			}
			else
			{
				string text4 = "SUSPECTS: ";
				StringBuilder stringBuilder = sb;
				text3 = text4 + ((stringBuilder != null) ? stringBuilder.ToString() : null);
			}
			SoftFleshController.ParticleSuspects = text + text2 + text3;
			SoftFleshPlugin.Log("[SF][census] " + SoftFleshController.ParticleSuspects);
			VideoPlayer[] vids = UnityEngine.Object.FindObjectsOfType<VideoPlayer>();
			StringBuilder msb = new StringBuilder(220);
			foreach (VideoPlayer vp in vids)
			{
				if (!(vp == null))
				{
					string src = ((vp.clip != null) ? vp.clip.name : vp.url);
					SoftFleshPlugin.Log(string.Concat(new string[]
					{
						"[SF][census] video '",
						vp.name,
						"' src '",
						src,
						"' playing ",
						vp.isPlaying.ToString(),
						" loop ",
						vp.isLooping.ToString()
					}));
					if (msb.Length < 160)
					{
						msb.Append((msb.Length > 0) ? "  |  " : "").Append("video ").Append(src)
							.Append(vp.isPlaying ? " PLAYING" : " idle")
							.Append(vp.isLooping ? " loop" : "");
					}
				}
			}
			AudioSource[] auds = UnityEngine.Object.FindObjectsOfType<AudioSource>();
			int aN = 0;
			foreach (AudioSource au in auds)
			{
				if (!(au == null) && !(au.clip == null))
				{
					float len = au.clip.length;
					SoftFleshPlugin.Log(string.Concat(new string[]
					{
						"[SF][census] audio '",
						au.name,
						"' clip '",
						au.clip.name,
						"' ",
						len.ToString("F1"),
						"s loop ",
						au.loop.ToString(),
						" playing ",
						au.isPlaying.ToString()
					}));
					aN++;
					bool sus2 = au.loop && au.isPlaying && ((len > 6.5f && len < 9.5f) || (len > 45f && len < 60f));
					if (sus2 && msb.Length < 200)
					{
						msb.Append((msb.Length > 0) ? "  |  " : "").Append("audio ").Append(au.clip.name)
							.Append(' ')
							.Append(len.ToString("F1"))
							.Append('s');
					}
				}
			}
			string[] array = new string[5];
			array[0] = vids.Length.ToString();
			array[1] = " video / ";
			array[2] = aN.ToString();
			array[3] = " audio";
			int num = 4;
			string text5;
			if (msb.Length <= 0)
			{
				text5 = ", none suspicious";
			}
			else
			{
				string text6 = ":  ";
				StringBuilder stringBuilder2 = msb;
				text5 = text6 + ((stringBuilder2 != null) ? stringBuilder2.ToString() : null);
			}
			array[num] = text5;
			SoftFleshController.MediaSuspects = string.Concat(array);
			SoftFleshPlugin.Log("[SF][census] " + SoftFleshController.MediaSuspects);
			this.CallbackCensus();
		}

		// Token: 0x0600007F RID: 127 RVA: 0x000114A0 File Offset: 0x0000F6A0
		private void CallbackCensus()
		{
			SoftFleshController.DumpInvocation("Camera.onPreCull", Camera.onPreCull);
			SoftFleshController.DumpInvocation("Camera.onPreRender", Camera.onPreRender);
			SoftFleshController.DumpInvocation("Camera.onPostRender", Camera.onPostRender);
			Delegate wrc = null;
			try
			{
				FieldInfo fi = typeof(Canvas).GetField("willRenderCanvases", BindingFlags.Static | BindingFlags.NonPublic);
				if (fi != null)
				{
					wrc = fi.GetValue(null) as Delegate;
				}
			}
			catch
			{
			}
			SoftFleshController.DumpInvocation("Canvas.willRenderCanvases", wrc);
		}

		// Token: 0x06000080 RID: 128 RVA: 0x0001152C File Offset: 0x0000F72C
		private static void DumpInvocation(string label, Delegate del)
		{
			if (del == null)
			{
				SoftFleshPlugin.Log("[SF][census] " + label + ": (empty or unreadable)");
				return;
			}
			Delegate[] list = del.GetInvocationList();
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF][census] ",
				label,
				": ",
				list.Length.ToString(),
				" subscriber(s)"
			}));
			for (int i = 0; i < list.Length; i++)
			{
				MethodInfo j = list[i].Method;
				string asm = ((j.DeclaringType != null) ? j.DeclaringType.Assembly.GetName().Name : "?");
				string who = string.Concat(new string[]
				{
					asm,
					" : ",
					(j.DeclaringType != null) ? j.DeclaringType.Name : "?",
					".",
					j.Name
				});
				bool ours = asm.StartsWith("SoftFlesh");
				SoftFleshPlugin.Log(string.Concat(new string[]
				{
					"[SF][census]   ",
					(i + 1).ToString(),
					". ",
					who,
					ours ? "   <-- OURS" : ""
				}));
			}
		}

		// Token: 0x06000081 RID: 129 RVA: 0x00011680 File Offset: 0x0000F880
		private void Update()
		{
			long now = SoftFleshController._sw.ElapsedTicks;
			if (this._tPost != 0L)
			{
				double toMs = 1000.0 / (double)Stopwatch.Frequency;
				SoftFleshController.SegU = (float)((double)(this._tLate - this._tUpd) * toMs);
				SoftFleshController.SegL1 = (float)((double)(this._tCanvas - this._tLate) * toMs);
				SoftFleshController.SegL2 = (float)((double)(this._tPre - this._tCanvas) * toMs);
				SoftFleshController.SegR = (float)((double)(this._tPost - this._tPre) * toMs);
				SoftFleshController.SegE = (float)((double)(now - this._tPost) * toMs);
				SoftFleshController.CvPrevL = SoftFleshController.CvLayoutN;
				SoftFleshController.CvPrevG = SoftFleshController.CvGraphicN;
			}
			this._tUpd = now;
		}

		// Token: 0x06000082 RID: 130 RVA: 0x00011738 File Offset: 0x0000F938
		private void LateUpdate()
		{
			this._tLate = SoftFleshController._sw.ElapsedTicks;
		}

		// Token: 0x06000083 RID: 131 RVA: 0x0001174C File Offset: 0x0000F94C
		private void OnWillRenderCanvases()
		{
			this._tCanvas = SoftFleshController._sw.ElapsedTicks;
			try
			{
				CanvasUpdateRegistry reg = CanvasUpdateRegistry.instance;
				if (SoftFleshController._cvLayoutF == null)
				{
					Type t = typeof(CanvasUpdateRegistry);
					SoftFleshController._cvLayoutF = t.GetField("m_LayoutRebuildQueue", BindingFlags.Instance | BindingFlags.NonPublic);
					SoftFleshController._cvGraphicF = t.GetField("m_GraphicRebuildQueue", BindingFlags.Instance | BindingFlags.NonPublic);
				}
				IList<ICanvasElement> lq = ((SoftFleshController._cvLayoutF != null) ? (SoftFleshController._cvLayoutF.GetValue(reg) as IList<ICanvasElement>) : null);
				IList<ICanvasElement> gq = ((SoftFleshController._cvGraphicF != null) ? (SoftFleshController._cvGraphicF.GetValue(reg) as IList<ICanvasElement>) : null);
				SoftFleshController.CvLayoutN = ((lq != null) ? lq.Count : (-1));
				SoftFleshController.CvGraphicN = ((gq != null) ? gq.Count : (-1));
				if (lq != null && SoftFleshController.CvLayoutN >= 8)
				{
					StringBuilder sb2 = new StringBuilder(200);
					sb2.Append('f').Append(Time.frameCount).Append(" n")
						.Append(SoftFleshController.CvLayoutN)
						.Append(':');
					int take = ((SoftFleshController.CvLayoutN < 6) ? SoftFleshController.CvLayoutN : 6);
					for (int i2 = 0; i2 < take; i2++)
					{
						ICanvasElement el = lq[i2];
						Transform tr = ((el != null) ? el.transform : null);
						if (!(tr == null))
						{
							sb2.Append(' ');
							if (tr.parent != null)
							{
								if (tr.parent.parent != null)
								{
									sb2.Append(tr.parent.parent.name).Append('/');
								}
								sb2.Append(tr.parent.name).Append('/');
							}
							sb2.Append(tr.name);
						}
					}
					SoftFleshController.CvBurst = sb2.ToString();
				}
			}
			catch
			{
			}
		}

		// Token: 0x06000084 RID: 132 RVA: 0x0001194C File Offset: 0x0000FB4C
		private void OnCamPreCull(Camera cam)
		{
			if (Time.frameCount == this._solveFrame)
			{
				return;
			}
			this._solveFrame = Time.frameCount;
			this._tPre = SoftFleshController._sw.ElapsedTicks;
			SoftFleshController._memDepth = 0;
			SoftFleshController.HitchTick();
			if (this._pcensusDone < 0 && Time.frameCount > 200)
			{
				this._pcensusDone = 1;
				this.ParticleCensus();
			}
			long tAll = SoftFleshController.SwNow();
			long tMnt = SoftFleshController.SwNow();
			this.MaintainCharacters();
			this.CarryCrotchBranch();
			this.UpdateCanalCollider();
			this.DriveItemAim();
			SoftFleshController.PhAdd(8, tMnt);
			this.Solve();
			SoftFleshController.PhAdd(0, tAll);
			SoftFleshController.PhFlush();
			this.WriteTrackLog();
		}

		// Token: 0x06000085 RID: 133 RVA: 0x000119F4 File Offset: 0x0000FBF4
		private string ShadeLine()
		{
			StringBuilder sb = new StringBuilder(256);
			sb.Append("SHADE     Shadow smooth ").Append((this.shadeTau * 1000f).ToString("F0")).Append("ms");
			for (int q = 0; q < this._bodies.Count; q++)
			{
				SoftFleshController.Body bd = this._bodies[q];
				float mx = 0f;
				if (bd.nTilt != null)
				{
					for (int i = 0; i < bd.nTiltLive.Count; i++)
					{
						int j = bd.nTiltLive[i];
						if (j >= 0 && j < bd.nTilt.Length)
						{
							float k = bd.nTilt[j].magnitude;
							if (k > mx)
							{
								mx = k;
							}
						}
					}
				}
				sb.Append("  | body ").Append(q).Append(bd.isFace ? " (face)" : "")
					.Append(": tilt still decaying on ")
					.Append(bd.nTiltLive.Count)
					.Append(" vertices (largest ")
					.Append((Mathf.Atan(mx) * 57.29578f).ToString("F1"))
					.Append(" deg)")
					.Append("  idle-skip ")
					.Append(bd.dgSkipTook ? "TAKEN" : "blocked")
					.Append(bd.dgSkipTook ? (" x" + bd.dgSkipFrames.ToString() + " frames, decay steps " + bd.dgShadeSteps.ToString()) : "")
					.Append(" [moved ")
					.Append((bd.dgSkipMoved > false) ? 1 : 0)
					.Append(" busy ")
					.Append((bd.dgSkipBusy > false) ? 1 : 0)
					.Append(" free ")
					.Append((bd.dgSkipFree > false) ? 1 : 0)
					.Append(" vel ")
					.Append((bd.dgSkipVel * 1000f).ToString("F2"))
					.Append("mm/s]");
			}
			return sb.ToString();
		}

		// Token: 0x06000086 RID: 134 RVA: 0x00011C24 File Offset: 0x0000FE24
		private void WriteTrackLog()
		{
			if (!this.trackLog || Time.unscaledTime < this._tlNext)
			{
				return;
			}
			this._tlNext = Time.unscaledTime + 0.25f;
			try
			{
				string NL = Environment.NewLine;
				SoftFleshController.Body b = ((this._bodies.Count > 0) ? this._bodies[0] : null);
				this._tlSb.Length = 0;
				this._tlSb.Append("frame ").Append(Time.frameCount).Append("  fps ")
					.Append((1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f)).ToString("F0"))
					.Append(NL)
					.Append("bodies ")
					.Append(this._bodies.Count)
					.Append("  itemDefs ")
					.Append(this._itemDefs.Count)
					.Append("  pressers ")
					.Append(this._pressers.Count)
					.Append("  trackItems ")
					.Append(this.trackItems)
					.Append("  itemRadScale ")
					.Append(this.itemRadScale.ToString("F2"))
					.Append(NL);
				if (b == null)
				{
					this._tlSb.Append("NO BODY - press Setup").Append(NL).Append("STEADY    frame ")
						.Append(SoftFleshController.AvgDt.ToString("F1"))
						.Append("ms (")
						.Append(((SoftFleshController.AvgDt > 0.01f) ? (1000f / SoftFleshController.AvgDt) : 0f).ToString("F0"))
						.Append("fps) | U ")
						.Append(SoftFleshController.AvgU.ToString("F1"))
						.Append("  L1 ")
						.Append(SoftFleshController.AvgL1.ToString("F1"))
						.Append("  L2 ")
						.Append(SoftFleshController.AvgL2.ToString("F1"))
						.Append("  R ")
						.Append(SoftFleshController.AvgR.ToString("F1"))
						.Append("  E ")
						.Append(SoftFleshController.AvgE.ToString("F1"))
						.Append("  | ours ")
						.Append(SoftFleshController.AvgOurs.ToString("F1"))
						.Append(" (mouth ")
						.Append(SoftFleshController.PhaseMs[11].ToString("F1"))
						.Append(')')
						.Append("   (~1s average, not just spikes)")
						.Append(NL)
						.Append("HITCH     ")
						.Append(SoftFleshController.HitchTotal)
						.Append(" total  ")
						.Append(SoftFleshController.HitchPerMin.ToString("F0"))
						.Append("/min  worst frame ")
						.Append(SoftFleshController.HitchWorstMs.ToString("F0"))
						.Append("ms of which SoftFlesh ")
						.Append(SoftFleshController.HitchOursMs.ToString("F1"))
						.Append("ms")
						.Append(NL)
						.Append("GC        ")
						.Append(SoftFleshController.GcTotal)
						.Append(" collections  ")
						.Append(SoftFleshController.GcSpikes)
						.Append(" of ")
						.Append(SoftFleshController.HitchTotal)
						.Append(" spikes had one in the same frame")
						.Append(NL)
						.Append("CANVAS    ambush f")
						.Append(SoftFleshController.PredLo)
						.Append("-f")
						.Append(SoftFleshController.PredHi)
						.Append(" period ")
						.Append(SoftFleshController.PredPeriod)
						.Append("   caught: ")
						.Append((SoftFleshController.CvBurst.Length > 0) ? SoftFleshController.CvBurst : "nothing yet")
						.Append(NL)
						.Append("PARTICLES ")
						.Append(SoftFleshController.ParticleSuspects)
						.Append(NL)
						.Append("MEDIA     ")
						.Append(SoftFleshController.MediaSuspects)
						.Append(NL)
						.Append("SLOWSCRIPT ")
						.Append((SFStallSpy.SlowLog.Length > 0) ? SFStallSpy.SlowLog : "none over 5ms")
						.Append(NL)
						.Append("LATEBUDGET ")
						.Append(SFStallSpy.LateBudget())
						.Append("   (ms per frame, per LateUpdate type)")
						.Append(NL)
						.Append("FONT      ")
						.Append(SoftFleshController.FontRebuilds)
						.Append(" atlas rebuilds   ")
						.Append((SoftFleshController.FontLog.Length > 0) ? SoftFleshController.FontLog : "none seen")
						.Append(NL)
						.Append("UNLOAD    ")
						.Append(SFStallSpy.UnloadCount)
						.Append(" calls   ")
						.Append((SFStallSpy.Recent.Length > 0) ? SFStallSpy.Recent : "none seen")
						.Append(NL)
						.Append("SPIKES    (U=scripts/anim  L1=IK/DynBone/late-scripts  L2=UI-canvas-rebuild  R=render  E=frame-end/vsync)")
						.Append(SoftFleshController.SpikeTable())
						.Append(NL)
						.Append("ALLOC/f   TOTAL ")
						.Append(SoftFleshController.PhaseKB[0].ToString("F0"))
						.Append("  maint ")
						.Append(SoftFleshController.PhaseKB[8].ToString("F0"))
						.Append("  cloth ")
						.Append(SoftFleshController.PhaseKB[9].ToString("F0"))
						.Append("  skin ")
						.Append(SoftFleshController.PhaseKB[10].ToString("F0"))
						.Append("  KB");
				}
				else
				{
					this._tlSb.Append("inside ").Append(b.itemInside).Append("  planeExact ")
						.Append(this.planeExact)
						.Append("  axis age ")
						.Append(Time.frameCount - b.axisFrameNo)
						.Append(" frame(s)")
						.Append("  BAKE AGE ")
						.Append(Time.frameCount - b.bakeFrameNo)
						.Append(" frame(s)")
						.Append("  follow ")
						.Append((b.followB.magnitude * 1000f).ToString("F1"))
						.Append("mm")
						.Append(" of gain ")
						.Append(this.followGain.ToString("F2"))
						.Append(NL)
						.Append("MEASURED  item half-section ")
						.Append(b.trkRadMm.ToString("F1"))
						.Append("mm")
						.Append("  off-axis ")
						.Append(b.trkLatMm.ToString("F1"))
						.Append("mm")
						.Append("  inserted ")
						.Append(b.trkDepthMm.ToString("F1"))
						.Append("mm")
						.Append("  opening now ")
						.Append((b.rimNowMm < 0f) ? "n/a" : (b.rimNowMm.ToString("F1") + "mm"))
						.Append(NL)
						.Append("DRIVEN    itemW ")
						.Append(b.itemW.ToString("F3"))
						.Append("  itemD ")
						.Append(b.itemD.ToString("F3"))
						.Append("  gape ")
						.Append(b.gapeLevel.ToString("F3"))
						.Append("  canalLen ")
						.Append((b.canalLen * 1000f).ToString("F0"))
						.Append("mm")
						.Append("  calibrated ")
						.Append(b.itemWff.ToString("F3"))
						.Append(" +/- ")
						.Append(this.widthTrim.ToString("F2"))
						.Append(NL)
						.Append("SQUASH    ")
						.Append(SoftFleshController.SquashInfo)
						.Append(NL)
						.Append("RIM SHAPE ")
						.Append((b.rimMajMm < 0f) ? "n/a" : string.Concat(new string[]
						{
							b.rimMajMm.ToString("F1"),
							" x ",
							b.rimMinMm.ToString("F1"),
							" x ",
							b.rimThickMm.ToString("F1"),
							"mm  ratio ",
							(b.rimMinMm > 0.01f) ? (b.rimMajMm / b.rimMinMm).ToString("F2") : "inf"
						}))
						.Append("   (world principal axes, independent of the canal axis)")
						.Append(NL)
						.Append("VULVA     ")
						.Append((b.vulMajMm < 0f) ? "n/a" : string.Concat(new string[]
						{
							b.vulMajMm.ToString("F1"),
							" x ",
							b.vulMinMm.ToString("F1"),
							" x ",
							b.vulThickMm.ToString("F1"),
							"mm  ratio ",
							(b.vulMinMm > 0.01f) ? (b.vulMajMm / b.vulMinMm).ToString("F2") : "inf"
						}))
						.Append("  rim/vulva ")
						.Append((b.vulMajMm > 0.01f && b.rimMajMm > 0f) ? (b.rimMajMm / b.vulMajMm).ToString("F2") : "n/a")
						.Append(" - at 1.00 the inner ring has reached the outer flesh")
						.Append((Time.frameCount - b.bakeFrameNo > 3) ? "  *** STALE - the solve is not re-baking, these numbers are frozen ***" : "")
						.Append(NL)
						.Append("STEADY    frame ")
						.Append(SoftFleshController.AvgDt.ToString("F1"))
						.Append("ms (")
						.Append(((SoftFleshController.AvgDt > 0.01f) ? (1000f / SoftFleshController.AvgDt) : 0f).ToString("F0"))
						.Append("fps) | U ")
						.Append(SoftFleshController.AvgU.ToString("F1"))
						.Append("  L1 ")
						.Append(SoftFleshController.AvgL1.ToString("F1"))
						.Append("  L2 ")
						.Append(SoftFleshController.AvgL2.ToString("F1"))
						.Append("  R ")
						.Append(SoftFleshController.AvgR.ToString("F1"))
						.Append("  E ")
						.Append(SoftFleshController.AvgE.ToString("F1"))
						.Append("  | ours ")
						.Append(SoftFleshController.AvgOurs.ToString("F1"))
						.Append(" (mouth ")
						.Append(SoftFleshController.PhaseMs[11].ToString("F1"))
						.Append(')')
						.Append("   (~1s average, not just spikes)")
						.Append(NL)
						.Append("HITCH     ")
						.Append(SoftFleshController.HitchTotal)
						.Append(" total  ")
						.Append(SoftFleshController.HitchPerMin.ToString("F0"))
						.Append("/min  worst frame ")
						.Append(SoftFleshController.HitchWorstMs.ToString("F0"))
						.Append("ms of which SoftFlesh ")
						.Append(SoftFleshController.HitchOursMs.ToString("F1"))
						.Append("ms (biggest ")
						.Append(SoftFleshController.HitchPhase)
						.Append(' ')
						.Append(((float)SoftFleshController.HitchPhaseMs[(Array.IndexOf<string>(SoftFleshController.PhaseNamesC, SoftFleshController.HitchPhase) < 1) ? 1 : Array.IndexOf<string>(SoftFleshController.PhaseNamesC, SoftFleshController.HitchPhase)]).ToString("F1"))
						.Append("ms)")
						.Append(NL)
						.Append("GC        ")
						.Append(SoftFleshController.GcTotal)
						.Append(" collections  ")
						.Append(SoftFleshController.GcSpikes)
						.Append(" of ")
						.Append(SoftFleshController.HitchTotal)
						.Append(" spikes had one in the same frame")
						.Append("   (gc>=1 on a row below = a collection pause, not work)")
						.Append(NL)
						.Append("CANVAS    ambush f")
						.Append(SoftFleshController.PredLo)
						.Append("-f")
						.Append(SoftFleshController.PredHi)
						.Append(" period ")
						.Append(SoftFleshController.PredPeriod)
						.Append("   caught: ")
						.Append((SoftFleshController.CvBurst.Length > 0) ? SoftFleshController.CvBurst : "nothing yet")
						.Append(NL)
						.Append("PARTICLES ")
						.Append(SoftFleshController.ParticleSuspects)
						.Append(NL)
						.Append("MEDIA     ")
						.Append(SoftFleshController.MediaSuspects)
						.Append(NL)
						.Append("SLOWSCRIPT ")
						.Append((SFStallSpy.SlowLog.Length > 0) ? SFStallSpy.SlowLog : "none over 5ms")
						.Append(NL)
						.Append("LATEBUDGET ")
						.Append(SFStallSpy.LateBudget())
						.Append("   (ms per frame, per LateUpdate type)")
						.Append(NL)
						.Append("FONT      ")
						.Append(SoftFleshController.FontRebuilds)
						.Append(" atlas rebuilds   ")
						.Append((SoftFleshController.FontLog.Length > 0) ? SoftFleshController.FontLog : "none seen")
						.Append(NL)
						.Append("UNLOAD    ")
						.Append(SFStallSpy.UnloadCount)
						.Append(" calls   ")
						.Append((SFStallSpy.Recent.Length > 0) ? SFStallSpy.Recent : "none seen")
						.Append(NL)
						.Append("SPIKES    (U=scripts/anim  L1=IK/DynBone/late-scripts  L2=UI-canvas-rebuild  R=render  E=frame-end/vsync)")
						.Append(SoftFleshController.SpikeTable())
						.Append(NL)
						.Append(this.DepthLine())
						.Append(NL)
						.Append("GRIP      dTgt peak ")
						.Append((SoftFleshController.GripDTgtPeak * 1000f).ToString("F2"))
						.Append("mm  slapImp@held peak ")
						.Append((SoftFleshController.GripImpPeak * 1000f).ToString("F2"))
						.Append("  sway peak ")
						.Append((SoftFleshController.GripSwayPeak * 1000f).ToString("F2"))
						.Append("mm/f")
						.Append("   PENbind ")
						.Append(SoftFleshController.PenBindN)
						.Append('/')
						.Append(SoftFleshController.PenBindMm.ToString("F1"))
						.Append("mm peak ")
						.Append(SoftFleshController.PenPeakMm.ToString("F1"))
						.Append("mm  PENpose ")
						.Append(SoftFleshController.PenPoseN)
						.Append('/')
						.Append(SoftFleshController.PenPoseMm.ToString("F1"))
						.Append("mm")
						.Append("   (held verts should read ~0 on both while only the far hand moves)")
						.Append(NL)
						.Append("LOAD      act ")
						.Append(b.lastAct)
						.Append("  sim ")
						.Append(b.lastSim)
						.Append("  substeps ")
						.Append(b.lastSteps)
						.Append("   (field/spring/norm all scale with these)")
						.Append(NL)
						.Append(this.ShadeLine())
						.Append(NL)
						.Append("ALLOC/f   TOTAL ")
						.Append(SoftFleshController.PhaseKB[0].ToString("F0"))
						.Append("  | bake ")
						.Append(SoftFleshController.PhaseKB[1].ToString("F0"))
						.Append("  corr ")
						.Append(SoftFleshController.PhaseKB[2].ToString("F0"))
						.Append("  open ")
						.Append(SoftFleshController.PhaseKB[3].ToString("F0"))
						.Append("  spring ")
						.Append(SoftFleshController.PhaseKB[4].ToString("F0"))
						.Append("  norm ")
						.Append(SoftFleshController.PhaseKB[5].ToString("F0"))
						.Append("  contact ")
						.Append(SoftFleshController.PhaseKB[6].ToString("F0"))
						.Append("  field ")
						.Append(SoftFleshController.PhaseKB[7].ToString("F0"))
						.Append("  maint ")
						.Append(SoftFleshController.PhaseKB[8].ToString("F0"))
						.Append("  cloth ")
						.Append(SoftFleshController.PhaseKB[9].ToString("F0"))
						.Append("  skin ")
						.Append(SoftFleshController.PhaseKB[10].ToString("F0"))
						.Append("  KB")
						.Append(NL)
						.Append("CROTCH    ")
						.Append(SoftFleshController.CrotchMode)
						.Append("  delta ")
						.Append(SoftFleshController.CrotchDeltaDeg.ToString("F1"))
						.Append("deg")
						.Append("  vagRoot ")
						.Append(SoftFleshController.CrotchVagRoot)
						.Append("  carry=")
						.Append(b.crotchCarry)
						.Append("  buttock d ")
						.Append(SoftFleshController.ButtockDeg.ToString("F1"))
						.Append("deg / s ")
						.Append(SoftFleshController.ButtockDegS.ToString("F1"))
						.Append("deg (KKPE's, untouched)")
						.Append(NL)
						.Append("CROTCH-W  ")
						.Append(SoftFleshController.CrotchHold)
						.Append(NL)
						.Append("BONEDIFF  ")
						.Append(SoftFleshController.BoneDiff)
						.Append(NL)
						.Append("MESHDIFF  ")
						.Append(SoftFleshController.MeshDiff)
						.Append(NL)
						.Append("BULGE     verts ")
						.Append(SoftFleshController.MergeBulgeStats())
						.Append("  axial ")
						.Append((SoftFleshController.BgAxLo * 1000f).ToString("F0"))
						.Append("..")
						.Append((SoftFleshController.BgAxHi * 1000f).ToString("F0"))
						.Append("mm")
						.Append("  lat max ")
						.Append((SoftFleshController.BgLat * 1000f).ToString("F0"))
						.Append("mm")
						.Append("  peak ")
						.Append((SoftFleshController.BgPeak * 1000f).ToString("F1"))
						.Append("mm")
						.Append("   (axial 0 = the entrance plane; if the low end is well above 0 the mound never reaches the mound)")
						.Append(NL)
						.Append("ERROR     want - have = ")
						.Append((b.trkRadMm - ((b.rimNowMm < 0f) ? 0f : b.rimNowMm)).ToString("F1"))
						.Append("mm")
						.Append("   (itemW pinned at 1.000 = the flesh has no authority left)")
						.Append(NL)
						.Append("LOBE      ")
						.Append((b.trkLobe == null) ? "-" : b.trkLobe)
						.Append("  max-over-angles ")
						.Append(b.trkRadMm.ToString("F1"))
						.Append("mm")
						.Append("  at-this-angle ")
						.Append(b.trkRadAngMm.ToString("F1"))
						.Append("mm")
						.Append("  along ")
						.Append(b.trkAlongT.ToString("F2"))
						.Append(" of ")
						.Append(b.trkLobeLenMm.ToString("F1"))
						.Append("mm")
						.Append("   (along 0.00 or 1.00 = CLAMPED, reporting the lobe end not the plane)")
						.Append(NL)
						.Append("TAPER     ")
						.Append(this.taperCanal ? "on " : "OFF");
					if (this.taperCanal && b.canalWAt != null)
					{
						for (int i = 0; i < 9; i++)
						{
							this._tlSb.Append(" ").Append((int)(b.canalLen * (float)i / 8f * 1000f)).Append("mm=")
								.Append(b.canalWAt[i].ToString("F2"));
						}
					}
					this._tlSb.Append(NL).Append("          (1.00 = as thick as at the mouth, 0.00 = the item does not reach)").Append(NL)
						.Append("ST-LAT   ");
					if (b.canalLatAt != null)
					{
						for (int j = 0; j < 9; j++)
						{
							this._tlSb.Append(" ").Append((b.canalLatAt[j] < 0f) ? "--" : b.canalLatAt[j].ToString("F0"));
						}
					}
					this._tlSb.Append("  mm off the canal axis. Anything past ~").Append(b.trkRadMm.ToString("F0")).Append("mm is beside the canal, not in it.");
				}
				string dd = Path.Combine(Paths.GameRootPath, "BepInEx/SoftFlesh_diag");
				if (!Directory.Exists(dd))
				{
					Directory.CreateDirectory(dd);
				}
				File.WriteAllText(Path.Combine(dd, "track_live.txt"), this._tlSb.ToString());
				try
				{
					this.WriteMembDump(dd);
				}
				catch (Exception ex)
				{
					SoftFleshPlugin.Log("[SF] memb dump: " + ex.Message);
				}
				try
				{
					this.WriteDentTrack(dd);
				}
				catch (Exception ex2)
				{
					SoftFleshPlugin.Log("[SF] dent track: " + ex2.Message);
				}
			}
			catch
			{
				this.trackLog = false;
			}
		}

		// Token: 0x06000087 RID: 135 RVA: 0x00013280 File Offset: 0x00011480
		private void MaintainCharacters()
		{
			if (Time.unscaledTime < this._homeNext)
			{
				return;
			}
			this._homeNext = Time.unscaledTime + this._maintainGap;
			this._maintChas.Clear();
			global::Studio.Studio st = Singleton<global::Studio.Studio>.Instance;
			if (st != null && st.dicObjectCtrl != null)
			{
				using (Dictionary<int, ObjectCtrlInfo>.Enumerator enumerator = st.dicObjectCtrl.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						KeyValuePair<int, ObjectCtrlInfo> kv = enumerator.Current;
						OCIChar oc = kv.Value as OCIChar;
						if (oc != null && oc.charInfo != null)
						{
							this._maintChas.Add(oc.charInfo);
						}
					}
					goto IL_00CE;
				}
			}
			ChaControl[] found = UnityEngine.Object.FindObjectsOfType<ChaControl>();
			for (int i = 0; i < found.Length; i++)
			{
				this._maintChas.Add(found[i]);
			}
			IL_00CE:
			bool did = false;
			for (int r = this._maintRecs.Count - 1; r >= 0; r--)
			{
				bool alive = false;
				for (int c2 = 0; c2 < this._maintChas.Count; c2++)
				{
					if (this._maintChas[c2] == this._maintRecs[r].cha)
					{
						alive = true;
						break;
					}
				}
				if (!alive)
				{
					this._maintRecs.RemoveAt(r);
				}
			}
			for (int c3 = 0; c3 < this._maintChas.Count; c3++)
			{
				ChaControl cc = this._maintChas[c3];
				if (!(cc == null) && !(cc.objBodyBone == null) && !(cc.objBody == null))
				{
					SoftFleshController.MaintRec rec = null;
					for (int r2 = 0; r2 < this._maintRecs.Count; r2++)
					{
						if (this._maintRecs[r2].cha == cc)
						{
							rec = this._maintRecs[r2];
							break;
						}
					}
					if (rec == null)
					{
						rec = new SoftFleshController.MaintRec();
						rec.cha = cc;
						this._maintRecs.Add(rec);
					}
					if (rec.body != cc.objBody || rec.kokan == null)
					{
						rec.body = cc.objBody;
						rec.kokan = null;
						rec.h0 = null;
						rec.h1 = null;
						rec.mnp = null;
						SkinnedMeshRenderer bsmr = SoftFleshController.FindBodySmr(cc);
						Transform[] barr = ((bsmr != null) ? bsmr.bones : null);
						if (barr != null)
						{
							foreach (Transform bt in barr)
							{
								if (!(bt == null))
								{
									if (bt.name == "cf_j_kokan")
									{
										if (rec.kokan == null)
										{
											rec.kokan = bt;
										}
									}
									else if (bt.name == "k_f_ana_00")
									{
										if (rec.h0 == null)
										{
											rec.h0 = bt;
										}
									}
									else if (bt.name == "k_f_ana_01" && rec.h1 == null)
									{
										rec.h1 = bt;
									}
								}
							}
						}
						SkinnedMeshRenderer[] rends = cc.objBody.GetComponentsInChildren<SkinnedMeshRenderer>(true);
						int nm = 0;
						foreach (SkinnedMeshRenderer sr in rends)
						{
							string mnm = ((sr != null && sr.sharedMesh != null) ? sr.sharedMesh.name : null);
							if (mnm == "o_mnpa" || mnm == "o_mnpb")
							{
								nm++;
							}
						}
						rec.mnp = new SkinnedMeshRenderer[nm];
						nm = 0;
						foreach (SkinnedMeshRenderer sr2 in rends)
						{
							string mnm2 = ((sr2 != null && sr2.sharedMesh != null) ? sr2.sharedMesh.name : null);
							if (mnm2 == "o_mnpa" || mnm2 == "o_mnpb")
							{
								rec.mnp[nm++] = sr2;
							}
						}
						did = true;
					}
					if (rec.kokan != null)
					{
						for (int j = 0; j < 2; j++)
						{
							Transform h = ((j == 0) ? rec.h0 : rec.h1);
							if (!(h == null) && !(h.parent == rec.kokan))
							{
								h.SetParent(rec.kokan, false);
								h.localPosition = Vector3.zero;
								h.localRotation = Quaternion.identity;
								h.localScale = Vector3.one;
								did = true;
								SoftFleshPlugin.Log("[SF] re-homed " + h.name + " onto cf_j_kokan");
							}
						}
					}
					if (rec.mnp != null)
					{
						for (int k = 0; k < rec.mnp.Length; k++)
						{
							SkinnedMeshRenderer sr3 = rec.mnp[k];
							if (!(sr3 == null) && sr3.enabled)
							{
								sr3.enabled = false;
								this._mnpHidden.Add(sr3);
								did = true;
								SoftFleshPlugin.Log("[SF] hid " + sr3.sharedMesh.name + " (stock genital mesh, sits ~2mm under the skin and surfaces when the opening is driven)");
							}
						}
					}
				}
			}
			this._maintainGap = (did ? 1f : 8f);
		}

		// Token: 0x06000088 RID: 136 RVA: 0x00013804 File Offset: 0x00011A04
		private void CarryCrotchBranch()
		{
			for (int b = 0; b < this._bodies.Count; b++)
			{
				SoftFleshController.Body body = this._bodies[b];
				if (body.crotchCarry && !(body.kosiT == null) && !(body.kokanDT == null))
				{
					Quaternion d = body.kosiT.localRotation * Quaternion.Inverse(body.kosiRestRot);
					SoftFleshController.CrotchDeltaDeg = Quaternion.Angle(Quaternion.identity, d);
					if (body.vagRootT != body.vagRootNamed)
					{
						body.vagRootNamed = body.vagRootT;
						body.vagRootName = ((body.vagRootT != null) ? body.vagRootT.name : "(null)");
					}
					SoftFleshController.CrotchVagRoot = body.vagRootName;
					SoftFleshController.CrotchMode = (this.crotchIsolate ? "ISOLATE" : "CARRY");
					float bdD = ((body.siriLT != null) ? Quaternion.Angle(body.siriLRestRot, body.siriLT.localRotation) : 0f);
					float bdS = ((body.siriSLT != null) ? Quaternion.Angle(body.siriSLRestRot, body.siriSLT.localRotation) : 0f);
					SoftFleshController.ButtockDeg = ((bdD > bdS) ? bdD : bdS);
					SoftFleshController.ButtockDegS = bdS;
					if (this.crotchIsolate)
					{
						body.kosiT.localRotation = body.kosiRestRot;
						if (body.anaDT != null)
						{
							body.anaDT.localRotation = body.anaRestRot;
							body.anaDT.localPosition = body.anaRestPos;
						}
						body.kokanDT.localRotation = body.kokanRestRot;
						body.kokanDT.localPosition = body.kokanRestPos;
						if (body.vagRootT != null)
						{
							body.vagRootT.localRotation = body.vagRootRestRot;
							body.vagRootT.localPosition = body.vagRootRestPos;
						}
					}
					else
					{
						if (body.vagRootT != null)
						{
							body.vagRootT.localRotation = body.vagRootRestRot;
							body.vagRootT.localPosition = body.vagRootRestPos;
						}
						body.kokanDT.localRotation = d * body.kokanRestRot;
						body.kokanDT.localPosition = body.kosiT.localPosition + d * (body.kokanRestPos - body.kosiRestPos);
					}
					if (b == 0)
					{
						this._cwBody = body;
						this._cwFrame = Time.frameCount;
						this._cwKosi = body.kosiT.localRotation;
						this._cwSiriL = ((body.siriLT != null) ? body.siriLT.localRotation : Quaternion.identity);
						this._cwSiriSL = ((body.siriSLT != null) ? body.siriSLT.localRotation : Quaternion.identity);
						this._cwSiriSLPos = ((body.siriSLT != null) ? body.siriSLT.localPosition : Vector3.zero);
						this._cwKokan = body.kokanDT.localPosition;
					}
				}
			}
		}

		// Token: 0x06000089 RID: 137 RVA: 0x00013B34 File Offset: 0x00011D34
		private static void FindCrotchPair(Transform t, ref Transform kosi, ref Transform kokanD, ref Transform vagR, ref Transform anaD, ref Transform siriL, ref Transform siriR, ref Transform siriSL, ref Transform siriSR)
		{
			string i = t.name;
			if (kosi == null && i == "cf_s_waist02")
			{
				kosi = t;
			}
			else if (kokanD == null && i == "cf_d_kokan")
			{
				kokanD = t;
			}
			else if (anaD == null && i == "cf_d_ana")
			{
				anaD = t;
			}
			else if (siriL == null && i == "cf_d_siri_L")
			{
				siriL = t;
			}
			else if (siriR == null && i == "cf_d_siri_R")
			{
				siriR = t;
			}
			else if (siriSL == null && i == "cf_s_siri_L")
			{
				siriSL = t;
			}
			else if (siriSR == null && i == "cf_s_siri_R")
			{
				siriSR = t;
			}
			else if (vagR == null)
			{
				string ln = i.ToLower();
				if (ln.Contains("vagina") && ln.Contains("root"))
				{
					vagR = t;
				}
			}
			for (int j = 0; j < t.childCount; j++)
			{
				SoftFleshController.FindCrotchPair(t.GetChild(j), ref kosi, ref kokanD, ref vagR, ref anaD, ref siriL, ref siriR, ref siriSL, ref siriSR);
			}
		}

		// Token: 0x0600008A RID: 138 RVA: 0x00013C7C File Offset: 0x00011E7C
		private static void FindHelperTrio(Transform t, ref Transform kokan, ref Transform h0, ref Transform h1)
		{
			string i = t.name;
			if (kokan == null && i == "cf_j_kokan")
			{
				kokan = t;
			}
			else if (h0 == null && i == "k_f_ana_00")
			{
				h0 = t;
			}
			else if (h1 == null && i == "k_f_ana_01")
			{
				h1 = t;
			}
			for (int j = 0; j < t.childCount; j++)
			{
				SoftFleshController.FindHelperTrio(t.GetChild(j), ref kokan, ref h0, ref h1);
			}
		}

		// Token: 0x0600008B RID: 139 RVA: 0x00013D04 File Offset: 0x00011F04
		private void Solve()
		{
			if (this._bodies.Count == 0)
			{
				return;
			}
			bool anyOn = false;
			for (int i = 0; i < this._bodies.Count; i++)
			{
				SoftFleshController.Body bd0 = this._bodies[i];
				if (bd0.sfOn)
				{
					bd0.restDone = false;
					bd0.restStill = 0;
					anyOn = true;
				}
				else if (!bd0.restDone)
				{
					anyOn = true;
				}
			}
			if (!anyOn)
			{
				return;
			}
			float dt = Time.deltaTime;
			if (dt <= 0f)
			{
				return;
			}
			if (dt > 0.03f)
			{
				dt = 0.03f;
			}
			this.didBake = false;
			bool moved = false;
			int segN = this._pressers.Count;
			if (this._segA == null || this._segA.Length != segN)
			{
				this._segA = new Vector3[segN];
				this._segAB = new Vector3[segN];
				this._segLen2 = new float[segN];
				this._segRight = new bool[segN];
				this._segCha = new ChaControl[segN];
				this._segOwner = new Transform[segN];
				this._segProx = new bool[segN];
				this._segPen = new float[segN];
				this._segPenNext = new float[segN];
				this._segInside = new bool[segN];
				this._segEntryN = new Vector3[segN];
				this._segVel = new Vector3[segN];
				this._segRad = new float[segN];
				this._segRadB = new float[segN];
				this._segItem = new bool[segN];
				this._segTipA = new bool[segN];
				this._segTipB = new bool[segN];
				this._segChainR = new float[segN];
				this._segInsert = new bool[segN];
				this._segBulgeW = new float[segN];
				this._segTipF = new float[segN];
				this._segProf = new SoftFleshController.Prof[segN];
				this._segW2L = new Matrix4x4[segN];
				this._segScale = new float[segN];
				this._segGate2 = new float[segN];
				this._segM = new float[segN * 12];
			}
			this._segRadMax = this.touchRadius;
			for (int p = 0; p < segN; p++)
			{
				SoftFleshController.Presser pr = this._pressers[p];
				if (pr.a == null || pr.b == null)
				{
					this._segLen2[p] = -1f;
				}
				else
				{
					Vector3 pa;
					Vector3 pb;
					if (pr.item)
					{
						pa = pr.a.TransformPoint(pr.la);
						pb = pr.a.TransformPoint(pr.lb);
						Vector3 ls = pr.a.lossyScale;
						float sc = (ls.x + ls.y + ls.z) * 0.33333334f;
						if (sc < 0.01f)
						{
							sc = 0.01f;
						}
						this._segRad[p] = pr.lrad * sc * this.itemRadScale;
						this._segRadB[p] = pr.lradB * sc * this.itemRadScale;
						this._segChainR[p] = pr.lradMax * sc * this.itemRadScale;
						float rMx = ((this._segRad[p] > this._segRadB[p]) ? this._segRad[p] : this._segRadB[p]);
						if (rMx > this._segRadMax)
						{
							this._segRadMax = rMx;
						}
					}
					else
					{
						pa = pr.a.position;
						pb = pr.b.position;
						if (pr.ext > 0f)
						{
							pb += (pb - pa) * pr.ext;
						}
						this._segRad[p] = 0f;
					}
					this._segA[p] = pa;
					this._segAB[p] = pb - pa;
					this._segLen2[p] = this._segAB[p].sqrMagnitude;
					this._segRight[p] = pr.right;
					this._segProx[p] = pr.prox;
					this._segCha[p] = pr.cha;
					this._segOwner[p] = pr.owner;
					this._segItem[p] = pr.item;
					this._segTipA[p] = pr.tipA;
					this._segTipB[p] = pr.tipB;
					this._segInsert[p] = pr.insertMode;
					this._segBulgeW[p] = pr.bulgeW;
					this._segTipF[p] = pr.tipF;
					this._segProf[p] = pr.prof;
					if (pr.prof != null)
					{
						Matrix4x4 w2l = pr.a.worldToLocalMatrix;
						this._segW2L[p] = w2l;
						int mb = p * 12;
						this._segM[mb] = w2l.m00;
						this._segM[mb + 1] = w2l.m01;
						this._segM[mb + 2] = w2l.m02;
						this._segM[mb + 3] = w2l.m03;
						this._segM[mb + 4] = w2l.m10;
						this._segM[mb + 5] = w2l.m11;
						this._segM[mb + 6] = w2l.m12;
						this._segM[mb + 7] = w2l.m13;
						this._segM[mb + 8] = w2l.m20;
						this._segM[mb + 9] = w2l.m21;
						this._segM[mb + 10] = w2l.m22;
						this._segM[mb + 11] = w2l.m23;
						Matrix4x4 l2wI = pr.a.localToWorldMatrix;
						float su = l2wI.MultiplyVector(pr.prof.u).magnitude;
						float sv = l2wI.MultiplyVector(pr.prof.v).magnitude;
						this._segScale[p] = (su + sv) * 0.5f;
						if (this._segScale[p] < 0.01f)
						{
							this._segScale[p] = 0.01f;
						}
					}
					float gr = ((pr.prof != null) ? (pr.prof.rMax * this._segScale[p] * this.itemRadScale) : ((this._segRad[p] > 0f) ? Mathf.Max(this._segRad[p], this._segRadB[p]) : this.touchRadius));
					gr = gr * 4f + this.maxDepth * 3f;
					this._segGate2[p] = gr * gr;
					this._segVel[p] = (pr.init ? ((pb - pr.lastPos) / dt) : Vector3.zero);
					if (!pr.init || (pb - pr.lastPos).sqrMagnitude > 1E-06f)
					{
						moved = true;
					}
					pr.lastPos = pb;
					pr.init = true;
				}
			}
			if (this.showBulge)
			{
				int layerB = 10;
				for (int q = 0; q < this._bodies.Count; q++)
				{
					if (this._bodies[q].smr != null)
					{
						layerB = this._bodies[q].smr.gameObject.layer;
						break;
					}
				}
				List<int> hits = new List<int>(500);
				List<SoftFleshController.Body> hitB = new List<SoftFleshController.Body>(500);
				float peak = 1E-06f;
				for (int b2 = 0; b2 < this._bodies.Count; b2++)
				{
					SoftFleshController.Body bd = this._bodies[b2];
					if (bd.bulgeDbg != null && bd.bakeV != null && bd.bakeV.Count == bd.bulgeDbg.Length)
					{
						for (int j = 0; j < bd.bulgeDbg.Length; j++)
						{
							if (bd.bulgeDbg[j] > peak)
							{
								peak = bd.bulgeDbg[j];
							}
						}
					}
				}
				for (int b3 = 0; b3 < this._bodies.Count; b3++)
				{
					if (hits.Count >= 500)
					{
						break;
					}
					SoftFleshController.Body bd2 = this._bodies[b3];
					if (bd2.bulgeDbg != null && bd2.bakeV != null && bd2.bakeV.Count == bd2.bulgeDbg.Length)
					{
						int live = 0;
						for (int k = 0; k < bd2.bulgeDbg.Length; k++)
						{
							if (bd2.bulgeDbg[k] > peak * 0.02f)
							{
								live++;
							}
						}
						int stride = ((live > 500) ? (live / 500) : 1);
						int seen = 0;
						int l = 0;
						while (l < bd2.bulgeDbg.Length && hits.Count < 500)
						{
							if (bd2.bulgeDbg[l] > peak * 0.02f && seen++ % stride == 0)
							{
								hits.Add(l);
								hitB.Add(bd2);
							}
							l++;
						}
					}
				}
				while (this._vizB.Count < hits.Count)
				{
					GameObject g = GameObject.CreatePrimitive(0);
					g.name = "SF_vizB";
					g.layer = layerB;
					Collider col = g.GetComponent<Collider>();
					if (col != null)
					{
						UnityEngine.Object.Destroy(col);
					}
					this._vizB.Add(g.transform);
				}
				while (this._vizB.Count > hits.Count)
				{
					Transform t0 = this._vizB[this._vizB.Count - 1];
					this._vizB.RemoveAt(this._vizB.Count - 1);
					if (t0 != null)
					{
						UnityEngine.Object.Destroy(t0.gameObject);
					}
				}
				for (int q2 = 0; q2 < hits.Count; q2++)
				{
					SoftFleshController.Body bd3 = hitB[q2];
					Transform t = this._vizB[q2];
					t.position = SoftFleshController.BakeL2W(bd3.smr).MultiplyPoint3x4(bd3.bakeV[hits[q2]]);
					float f = Mathf.Clamp01(bd3.bulgeDbg[hits[q2]] / peak);
					t.localScale = Vector3.one * (0.0015f + 0.0045f * f);
				}
				SoftFleshController.BulgeVizN = hits.Count;
			}
			else if (this._vizB.Count > 0)
			{
				for (int q3 = 0; q3 < this._vizB.Count; q3++)
				{
					if (this._vizB[q3] != null)
					{
						UnityEngine.Object.Destroy(this._vizB[q3].gameObject);
					}
				}
				this._vizB.Clear();
				SoftFleshController.BulgeVizN = 0;
			}
			if (this.showCross)
			{
				int layerX = 10;
				for (int q4 = 0; q4 < this._bodies.Count; q4++)
				{
					if (this._bodies[q4].smr != null)
					{
						layerX = this._bodies[q4].smr.gameObject.layer;
						break;
					}
				}
				if (this._matWall == null)
				{
					Shader sh = Shader.Find("Standard");
					if (sh == null)
					{
						sh = Shader.Find("Diffuse");
					}
					this._matWall = new Material(sh);
					this._matWall.color = new Color(1f, 0.1f, 0.1f);
					this._matSkin = new Material(sh);
					this._matSkin.color = new Color(0.2f, 0.4f, 1f);
				}
				int need = 0;
				for (int q5 = 0; q5 < this._bodies.Count; q5++)
				{
					if (this._bodies[q5].isFace)
					{
						need += this._bodies[q5].mwXVerts.Count;
					}
				}
				if (need > 800)
				{
					need = 800;
				}
				while (this._vizX.Count < need)
				{
					GameObject g2 = GameObject.CreatePrimitive(0);
					g2.name = "SF_vizX";
					g2.layer = layerX;
					Collider col2 = g2.GetComponent<Collider>();
					if (col2 != null)
					{
						UnityEngine.Object.Destroy(col2);
					}
					this._vizX.Add(g2.transform);
				}
				while (this._vizX.Count > need)
				{
					Transform t2 = this._vizX[this._vizX.Count - 1];
					this._vizX.RemoveAt(this._vizX.Count - 1);
					if (t2 != null)
					{
						UnityEngine.Object.Destroy(t2.gameObject);
					}
				}
				int vi = 0;
				int q6 = 0;
				while (q6 < this._bodies.Count && vi < need)
				{
					SoftFleshController.Body bd4 = this._bodies[q6];
					if (bd4.isFace && !(bd4.smr == null) && bd4.bakeV != null && bd4.mouthIn != null)
					{
						Matrix4x4 l2wX = SoftFleshController.BakeL2W(bd4.smr);
						int m = 0;
						while (m < bd4.mwXVerts.Count && vi < need)
						{
							int v = bd4.mwXVerts[m];
							if (v < bd4.bakeV.Count)
							{
								Transform t3 = this._vizX[vi++];
								t3.position = l2wX.MultiplyPoint3x4(bd4.bakeV[v]);
								t3.localScale = Vector3.one * 0.0012f;
								Renderer rd = t3.GetComponent<Renderer>();
								if (rd != null)
								{
									rd.sharedMaterial = (bd4.mouthIn[v] ? this._matWall : this._matSkin);
								}
							}
							m++;
						}
					}
					q6++;
				}
				SoftFleshController.CrossVizN = vi;
			}
			else if (this._vizX.Count > 0)
			{
				for (int q7 = 0; q7 < this._vizX.Count; q7++)
				{
					if (this._vizX[q7] != null)
					{
						UnityEngine.Object.Destroy(this._vizX[q7].gameObject);
					}
				}
				this._vizX.Clear();
				SoftFleshController.CrossVizN = 0;
			}
			if (this.showColliders)
			{
				int vizStamp = Time.frameCount;
				SoftFleshController.Body axB = null;
				for (int q8 = 0; q8 < this._bodies.Count; q8++)
				{
					if (this._bodies[q8].axisFrameOK)
					{
						axB = this._bodies[q8];
						break;
					}
				}
				int need2 = ((axB != null) ? 14 : 0);
				if (this._canalCol != null)
				{
					need2 += 20;
				}
				for (int p2 = 0; p2 < segN; p2++)
				{
					if (this._segLen2[p2] >= 0f)
					{
						SoftFleshController.Prof pf0 = this._segProf[p2];
						if (pf0 == null)
						{
							need2 += 5;
						}
						else if (pf0.vizFrame != vizStamp)
						{
							pf0.vizFrame = vizStamp;
							need2 += pf0.NS * pf0.NB;
						}
					}
				}
				for (int p3 = 0; p3 < segN; p3++)
				{
					if (this._segProf[p3] != null)
					{
						this._segProf[p3].vizFrame = -1;
					}
				}
				int vizLayer = 10;
				for (int q9 = 0; q9 < this._bodies.Count; q9++)
				{
					if (this._bodies[q9].smr != null)
					{
						vizLayer = this._bodies[q9].smr.gameObject.layer;
						IL_102F:
						while (this._viz.Count < need2)
						{
							GameObject g3 = GameObject.CreatePrimitive(0);
							g3.name = "SF_viz";
							g3.layer = vizLayer;
							Collider col3 = g3.GetComponent<Collider>();
							if (col3 != null)
							{
								UnityEngine.Object.Destroy(col3);
							}
							this._viz.Add(g3.transform);
						}
						while (this._viz.Count > need2)
						{
							Transform t4 = this._viz[this._viz.Count - 1];
							this._viz.RemoveAt(this._viz.Count - 1);
							if (t4 != null)
							{
								UnityEngine.Object.Destroy(t4.gameObject);
							}
						}
						int vi2 = 0;
						if (axB != null)
						{
							int q10 = 0;
							while (q10 < 14 && vi2 < this._viz.Count)
							{
								Transform av = this._viz[vi2++];
								av.position = axB.axisEwW + axB.axisDwW * ((float)q10 * 0.006f);
								av.localScale = Vector3.one * ((q10 == 0) ? 0.01f : 0.0035f);
								q10++;
							}
						}
						if (this._canalCol != null)
						{
							Transform ct2 = this._canalCol.transform;
							Vector3 lsc = ct2.lossyScale;
							float sA = (lsc.x + lsc.y + lsc.z) / 3f;
							if (sA < 0.01f)
							{
								sA = 0.01f;
							}
							float hW = Mathf.Max(this._canalCol.m_Height * sA * 0.5f - this._canalCol.m_Radius * sA, 0f);
							float rW = this._canalCol.m_Radius * sA;
							Vector3 axW2 = ct2.up;
							Vector3 c2 = ct2.TransformPoint(this._canalCol.m_Center);
							Vector3 u2 = ct2.right;
							Vector3 v2 = ct2.forward;
							int q11 = 0;
							while (q11 < 20 && vi2 < this._viz.Count)
							{
								float e2 = ((q11 < 10) ? (-1f) : 1f);
								float ang2 = (float)(q11 % 10) * 0.62831855f;
								Transform cv2 = this._viz[vi2++];
								cv2.position = c2 + axW2 * (hW * e2) + (u2 * Mathf.Cos(ang2) + v2 * Mathf.Sin(ang2)) * rW;
								cv2.localScale = Vector3.one * 0.005f;
								q11++;
							}
						}
						for (int p4 = 0; p4 < segN; p4++)
						{
							if (this._segLen2[p4] >= 0f)
							{
								SoftFleshController.Prof pf = this._segProf[p4];
								if (pf != null)
								{
									if (pf.vizFrame != vizStamp)
									{
										pf.vizFrame = vizStamp;
										Matrix4x4 l2wP = this._pressers[p4].a.localToWorldMatrix;
										for (int si = 0; si < pf.NS; si++)
										{
											int bi = 0;
											while (bi < pf.NB && vi2 < this._viz.Count)
											{
												Transform td = this._viz[vi2++];
												td.position = l2wP.MultiplyPoint3x4(pf.SurfacePoint(si, bi));
												td.localScale = Vector3.one * 0.0024f;
												bi++;
											}
										}
									}
								}
								else
								{
									float rA = ((this._segRad[p4] > 0f) ? this._segRad[p4] : this.touchRadius);
									float rB = ((this._segRadB[p4] > 0f) ? this._segRadB[p4] : rA);
									for (int e3 = 0; e3 < 5; e3++)
									{
										float u3 = (float)e3 / 4f;
										Transform t5 = this._viz[vi2++];
										t5.position = this._segA[p4] + this._segAB[p4] * u3;
										t5.localScale = Vector3.one * (Mathf.Lerp(rA, rB, u3) * 2f);
									}
								}
							}
						}
						goto IL_14A7;
					}
				}
				goto IL_102F;
			}
			if (this._viz.Count > 0)
			{
				for (int q12 = 0; q12 < this._viz.Count; q12++)
				{
					if (this._viz[q12] != null)
					{
						UnityEngine.Object.Destroy(this._viz[q12].gameObject);
					}
				}
				this._viz.Clear();
			}
			IL_14A7:
			float ph = this.waveSpeed * 7.1f + this.stiffness * 0.003f + this.wave * 13f + this.damping * 0.7f + this.maxDepth * 211f + this.touchRadius * 97f + this.bulge * 5.3f + this.nipFirm * 3.1f + this.slapGain * 1.7f + this.smoothFactor * 2.3f + this.slapSoften * 1.3f + this.itemRadScale * 17f + this.bellyGain * 23f + (this.bulgeWide ? 31f : 0f) + 0f + this.lipPushIn * 3700f + this.mouthScale * 43f + 169.6f + (this.mouthAuto ? 3f : 0f) + 0f + (float)this.lipSmooth * 17f + 0f + 0f + this.openTestDepth * 41f + this.openTestWidth * 43f + this.itemStretch * 47f + this.corrSmooth * 53f;
			if (ph != this._lastParamHash)
			{
				this._lastParamHash = ph;
				this._paramKick = 25;
			}
			if (this._paramKick > 0)
			{
				this._paramKick--;
				moved = true;
			}
			bool wantPetalCols = this.boneOpening && !this.trackItems;
			bool changed = false;
			for (int bi2 = 0; bi2 < this._bodies.Count; bi2++)
			{
				SoftFleshController.Body bd5 = this._bodies[bi2];
				for (int k2 = 0; k2 < bd5.vagDyn.Count; k2++)
				{
					DynamicBone db2 = bd5.vagDyn[k2];
					if (!(db2 == null))
					{
						if (db2.m_Colliders == null)
						{
							db2.m_Colliders = new List<DynamicBoneCollider>();
						}
						for (int q13 = 0; q13 < this._dynCols.Count; q13++)
						{
							DynamicBoneCollider c3 = this._dynCols[q13];
							if (!(c3 == null))
							{
								bool has = db2.m_Colliders.Contains(c3);
								if (wantPetalCols && !has)
								{
									db2.m_Colliders.Add(c3);
									changed = true;
								}
								else if (!wantPetalCols && has)
								{
									db2.m_Colliders.Remove(c3);
									changed = true;
								}
							}
						}
					}
				}
			}
			if (changed)
			{
				SoftFleshPlugin.Log("[SF] petal-chain item colliders " + (wantPetalCols ? "ATTACHED (bone-collision mode)" : "DETACHED (bake drives the opening alone)"));
			}
			if (this._dynCols.Count > 0)
			{
				this.SyncDynColliders();
			}
			for (int p5 = 0; p5 < segN; p5++)
			{
				this._segPen[p5] = this._segPenNext[p5];
				this._segPenNext[p5] = 0f;
			}
			this.activeVerts = 0;
			this.maxDisp = 0f;
			this._minSq = float.MaxValue;
			this._nearName = "";
			this._diagBuild = Time.unscaledTime >= this._diagNext;
			if (this._diagBuild)
			{
				this._diagNext = Time.unscaledTime + 0.25f;
				this._diagSb.Length = 0;
			}
			for (int n = 0; n < this._bodies.Count; n++)
			{
				SoftFleshController.Body bdi = this._bodies[n];
				if (bdi.isFace)
				{
					long tMouth = SoftFleshController.SwNow();
					if (bdi.sfOn)
					{
						this.DriveMouth(bdi);
					}
					else
					{
						if (bdi.mouthDriven)
						{
							this.ReleaseMouth(bdi);
						}
						bdi.mouthPushValid = false;
					}
					SoftFleshController.PhAdd(11, tMouth);
				}
				if (bdi.sfOn || !bdi.restDone)
				{
					this.SolveBody(bdi, n, dt, moved);
					if (!bdi.sfOn)
					{
						if (!bdi.hadActive && bdi.maxVel <= 1E-06f)
						{
							bdi.restStill++;
						}
						else
						{
							bdi.restStill = 0;
						}
						if (bdi.restStill >= 3)
						{
							bdi.restDone = true;
						}
					}
				}
			}
			if (this._diagBuild)
			{
				this.solveDiag = this._diagSb.ToString();
			}
			this.nearDist = ((this._minSq < float.MaxValue) ? Mathf.Sqrt(this._minSq) : (-1f));
			this.nearName = this._nearName;
		}

		// Token: 0x0600008C RID: 140 RVA: 0x00015640 File Offset: 0x00013840
		private void GuideItems(float dt)
		{
			bool on = this.trackItems && this.itemGuide > 0.01f && this._bodies.Count > 0 && this._bodies[0].axisFrameOK;
			Vector3 Ew = (on ? this._bodies[0].axisEwW : Vector3.zero);
			Vector3 dw = (on ? this._bodies[0].axisDwW : Vector3.up);
			float fl = 1f - Mathf.Exp(-8f * dt);
			Dictionary<Transform, Vector3> want = new Dictionary<Transform, Vector3>();
			if (on)
			{
				for (int q = 0; q < this._pressers.Count; q++)
				{
					SoftFleshController.Presser pr = this._pressers[q];
					if (pr.item && pr.insertMode && !(pr.a == null))
					{
						Vector3 wA = pr.a.TransformPoint(pr.la);
						Vector3 wB = pr.a.TransformPoint(pr.lb);
						float pAx = 0f;
						Vector3 pSp = Vector3.zero;
						bool pv2 = false;
						for (int st = 0; st <= 8; st++)
						{
							Vector3 sp = Vector3.Lerp(wA, wB, (float)st / 8f);
							float ax = Vector3.Dot(sp - Ew, dw);
							if (pv2 && ((pAx <= 0f && ax > 0f) || (pAx > 0f && ax <= 0f)))
							{
								float tt = pAx / (pAx - ax);
								Vector3 cross = Vector3.Lerp(pSp, sp, tt);
								Vector3 eLat = Ew - cross;
								eLat -= dw * Vector3.Dot(eLat, dw);
								if (eLat.magnitude < 0.1f && !want.ContainsKey(pr.a))
								{
									want[pr.a] = Vector3.ClampMagnitude(eLat * this.itemGuide, 0.04f);
								}
							}
							pAx = ax;
							pSp = sp;
							pv2 = true;
						}
					}
				}
			}
			if (this._guideKeys.Count == 0 && want.Count == 0)
			{
				return;
			}
			this._guideKeys.Clear();
			foreach (KeyValuePair<Transform, Vector3> kv in this._guideApplied)
			{
				this._guideKeys.Add(kv.Key);
			}
			foreach (Transform tr in this._guideKeys)
			{
				if (tr == null)
				{
					this._guideApplied.Remove(tr);
				}
				else
				{
					Vector3 applied = this._guideApplied[tr];
					Vector3 target;
					want.TryGetValue(tr, out target);
					Vector3 next = applied + (target - applied) * fl;
					tr.position += next - applied;
					if (next.sqrMagnitude < 1E-10f && target == Vector3.zero)
					{
						this._guideApplied.Remove(tr);
					}
					else
					{
						this._guideApplied[tr] = next;
					}
					want.Remove(tr);
				}
			}
			foreach (KeyValuePair<Transform, Vector3> kv2 in want)
			{
				Vector3 next2 = kv2.Value * fl;
				kv2.Key.position += next2;
				this._guideApplied[kv2.Key] = next2;
			}
		}

		// Token: 0x0600008D RID: 141 RVA: 0x00015A3C File Offset: 0x00013C3C
		private void ApplyPetalFirm(SoftFleshController.Body body, float effW)
		{
			if (body.vagDyn.Count == 0 || body.dynStifOrig.Count != body.vagDyn.Count)
			{
				return;
			}
			float t = this.petalFirm * Mathf.Clamp01(effW * 1.6f);
			if (Mathf.Abs(t - body.lastFirm) < 0.02f)
			{
				return;
			}
			body.lastFirm = t;
			for (int i = 0; i < body.vagDyn.Count; i++)
			{
				DynamicBone db = body.vagDyn[i];
				if (!(db == null))
				{
					db.m_Stiffness = Mathf.Lerp(body.dynStifOrig[i], 1f, t);
					db.m_Damping = Mathf.Lerp(body.dynDampOrig[i], 0.9f, t);
					db.m_Elasticity = Mathf.Lerp(body.dynElastOrig[i], 0.6f, t);
					if (body.dynUpdParams != null)
					{
						try
						{
							body.dynUpdParams.Invoke(db, null);
						}
						catch
						{
						}
					}
				}
			}
		}

		// Token: 0x0600008E RID: 142 RVA: 0x00015B5C File Offset: 0x00013D5C
		private static int MergeBulgeStats()
		{
			SoftFleshController.BgVerts = 0;
			SoftFleshController.BgAxLo = 1E+09f;
			SoftFleshController.BgAxHi = -1E+09f;
			SoftFleshController.BgLat = 0f;
			SoftFleshController.BgPeak = 0f;
			if (SoftFleshController._bgN != null)
			{
				for (int w = 0; w < SoftFleshController._bgN.Length; w++)
				{
					SoftFleshController.BgVerts += SoftFleshController._bgN[w];
					if (SoftFleshController._bgN[w] != 0)
					{
						if (SoftFleshController._bgAxLo[w] < SoftFleshController.BgAxLo)
						{
							SoftFleshController.BgAxLo = SoftFleshController._bgAxLo[w];
						}
						if (SoftFleshController._bgAxHi[w] > SoftFleshController.BgAxHi)
						{
							SoftFleshController.BgAxHi = SoftFleshController._bgAxHi[w];
						}
						if (SoftFleshController._bgLat[w] > SoftFleshController.BgLat)
						{
							SoftFleshController.BgLat = SoftFleshController._bgLat[w];
						}
						if (SoftFleshController._bgPeak[w] > SoftFleshController.BgPeak)
						{
							SoftFleshController.BgPeak = SoftFleshController._bgPeak[w];
						}
					}
				}
			}
			if (SoftFleshController.BgVerts == 0)
			{
				SoftFleshController.BgAxLo = 0f;
				SoftFleshController.BgAxHi = 0f;
			}
			return SoftFleshController.BgVerts;
		}

		// Token: 0x0600008F RID: 143 RVA: 0x00015C5C File Offset: 0x00013E5C
		private static float MeasureRimNow(SoftFleshController.Body body)
		{
			int[] rv = body.rimVerts;
			if (rv == null || rv.Length == 0 || body.smr == null)
			{
				return -1f;
			}
			if (body.bakeV == null || body.bakeV.Count != body.baseVerts.Length)
			{
				return -1f;
			}
			if (!body.axisFrameOK)
			{
				return -1f;
			}
			Matrix4x4 l2w = SoftFleshController.BakeL2W(body.smr);
			Vector3 E = body.axisEwW;
			Vector3 D = body.axisDwW;
			SoftFleshController._rimScratch.Clear();
			for (int i = 0; i < rv.Length; i++)
			{
				Vector3 w = l2w.MultiplyPoint3x4(body.bakeV[rv[i]]) - E;
				w -= D * Vector3.Dot(w, D);
				SoftFleshController._rimScratch.Add(w.magnitude);
			}
			if (SoftFleshController._rimScratch.Count == 0)
			{
				return -1f;
			}
			SoftFleshController._rimScratch.Sort();
			return SoftFleshController._rimScratch[Mathf.Clamp((int)((float)SoftFleshController._rimScratch.Count * 0.25f), 0, SoftFleshController._rimScratch.Count - 1)];
		}

		// Token: 0x06000090 RID: 144 RVA: 0x00015D84 File Offset: 0x00013F84
		private static void SymEigen3(float a00, float a11, float a22, float a01, float a02, float a12, out float e1, out float e2, out float e3)
		{
			float p = a01 * a01 + a02 * a02 + a12 * a12;
			if (p < 1E-18f)
			{
				e1 = Mathf.Max(a00, Mathf.Max(a11, a22));
				e3 = Mathf.Min(a00, Mathf.Min(a11, a22));
				e2 = a00 + a11 + a22 - e1 - e3;
				return;
			}
			float q = (a00 + a11 + a22) / 3f;
			float d0 = a00 - q;
			float d = a11 - q;
			float d2 = a22 - q;
			float p2 = d0 * d0 + d * d + d2 * d2 + 2f * p;
			float pp = Mathf.Sqrt(p2 / 6f);
			if (pp < 1E-12f)
			{
				e1 = (e2 = (e3 = q));
				return;
			}
			float ip = 1f / pp;
			float b0 = d0 * ip;
			float b = d * ip;
			float b2 = d2 * ip;
			float b3 = a01 * ip;
			float b4 = a02 * ip;
			float b5 = a12 * ip;
			float det = b0 * (b * b2 - b5 * b5) - b3 * (b3 * b2 - b5 * b4) + b4 * (b3 * b5 - b * b4);
			float r = Mathf.Clamp(det * 0.5f, -1f, 1f);
			float phi = Mathf.Acos(r) / 3f;
			e1 = q + 2f * pp * Mathf.Cos(phi);
			e3 = q + 2f * pp * Mathf.Cos(phi + 2.0943952f);
			e2 = 3f * q - e1 - e3;
		}

		// Token: 0x06000091 RID: 145 RVA: 0x00015F00 File Offset: 0x00014100
		private static void MeasureRimShape(SoftFleshController.Body body)
		{
			body.rimMajMm = (body.rimMinMm = -1f);
			body.rimFlatDeg = 0f;
			int[] rv = body.rimVerts;
			if (rv == null || rv.Length < 6 || body.smr == null)
			{
				return;
			}
			if (body.bakeV == null || body.bakeV.Count != body.baseVerts.Length)
			{
				return;
			}
			if (!body.axisFrameOK)
			{
				return;
			}
			Matrix4x4 l2w = SoftFleshController.BakeL2W(body.smr);
			Vector3 ctr = Vector3.zero;
			int i = 0;
			for (int j = 0; j < rv.Length; j++)
			{
				ctr += l2w.MultiplyPoint3x4(body.bakeV[rv[j]]);
				i++;
			}
			if (i < 6)
			{
				return;
			}
			ctr /= (float)i;
			float m0 = 0f;
			float m = 0f;
			float m2 = 0f;
			float m3 = 0f;
			float m4 = 0f;
			float m5 = 0f;
			for (int k = 0; k < rv.Length; k++)
			{
				Vector3 w = l2w.MultiplyPoint3x4(body.bakeV[rv[k]]) - ctr;
				m0 += w.x * w.x;
				m += w.y * w.y;
				m2 += w.z * w.z;
				m3 += w.x * w.y;
				m4 += w.x * w.z;
				m5 += w.y * w.z;
			}
			float invN = 1f / (float)i;
			m0 *= invN;
			m *= invN;
			m2 *= invN;
			m3 *= invN;
			m4 *= invN;
			m5 *= invN;
			float ev;
			float ev2;
			float ev3;
			SoftFleshController.SymEigen3(m0, m, m2, m3, m4, m5, out ev, out ev2, out ev3);
			body.rimMajMm = Mathf.Sqrt(2f * Mathf.Max(ev, 0f)) * 1000f;
			body.rimMinMm = Mathf.Sqrt(2f * Mathf.Max(ev2, 0f)) * 1000f;
			body.rimThickMm = Mathf.Sqrt(2f * Mathf.Max(ev3, 0f)) * 1000f;
			body.rimFlatDeg = 0f;
			body.vulMajMm = (body.vulMinMm = (body.vulThickMm = -1f));
			int[] bv = body.bakeVerts;
			if (bv == null || bv.Length < 32)
			{
				return;
			}
			Vector3 E2 = body.axisEwW;
			Vector3 c2 = Vector3.zero;
			int n2 = 0;
			for (int l = 0; l < bv.Length; l++)
			{
				Vector3 w2 = l2w.MultiplyPoint3x4(body.bakeV[bv[l]]);
				if ((w2 - E2).sqrMagnitude <= 0.002025f)
				{
					c2 += w2;
					n2++;
				}
			}
			if (n2 < 32)
			{
				return;
			}
			c2 /= (float)n2;
			float q0 = 0f;
			float q = 0f;
			float q2 = 0f;
			float q3 = 0f;
			float q4 = 0f;
			float q5 = 0f;
			for (int k2 = 0; k2 < bv.Length; k2++)
			{
				Vector3 w3 = l2w.MultiplyPoint3x4(body.bakeV[bv[k2]]);
				if ((w3 - E2).sqrMagnitude <= 0.002025f)
				{
					w3 -= c2;
					q0 += w3.x * w3.x;
					q += w3.y * w3.y;
					q2 += w3.z * w3.z;
					q3 += w3.x * w3.y;
					q4 += w3.x * w3.z;
					q5 += w3.y * w3.z;
				}
			}
			float iv2 = 1f / (float)n2;
			q0 *= iv2;
			q *= iv2;
			q2 *= iv2;
			q3 *= iv2;
			q4 *= iv2;
			q5 *= iv2;
			float f;
			float f2;
			float f3;
			SoftFleshController.SymEigen3(q0, q, q2, q3, q4, q5, out f, out f2, out f3);
			body.vulMajMm = Mathf.Sqrt(2f * Mathf.Max(f, 0f)) * 1000f;
			body.vulMinMm = Mathf.Sqrt(2f * Mathf.Max(f2, 0f)) * 1000f;
			body.vulThickMm = Mathf.Sqrt(2f * Mathf.Max(f3, 0f)) * 1000f;
		}

		// Token: 0x06000092 RID: 146 RVA: 0x000163C4 File Offset: 0x000145C4
		private static float WidthForRim(SoftFleshController.Body body, float rimWanted)
		{
			float[] rad = body.bakeRadii;
			float[] rim = body.bakeRimR;
			if (rad == null || rad.Length < 2)
			{
				return 0f;
			}
			int n2 = rad.Length;
			float span = rad[n2 - 1] - rad[0];
			if (span < 1E-06f)
			{
				return 0f;
			}
			if (rim == null || rim.Length != n2 || rim[n2 - 1] - rim[0] <= 0.001f)
			{
				return rimWanted / Mathf.Max(rad[n2 - 1], 0.001f);
			}
			if (rimWanted <= rim[0])
			{
				return 0f;
			}
			int i = 0;
			while (i + 1 < n2)
			{
				float a2 = rim[i];
				float b2 = rim[i + 1];
				if (b2 > a2 && rimWanted <= b2)
				{
					float f = (rimWanted - a2) / (b2 - a2);
					float rT2 = Mathf.Lerp(rad[i], rad[i + 1], f);
					return (rT2 - rad[0]) / span;
				}
				i++;
			}
			return 1f;
		}

		// Token: 0x06000093 RID: 147 RVA: 0x000164A4 File Offset: 0x000146A4
		private void TrackItemDrive(SoftFleshController.Body body, float dt)
		{
			float tw = 0f;
			float td = 0f;
			body.itemInside = false;
			float depth = 0f;
			float radAtPlane = 0f;
			Vector3 latPlaneW = Vector3.zero;
			float fbRad = 0f;
			Vector3 fbLat = Vector3.zero;
			float bestLat2 = 1E+09f;
			if (SoftFleshController._stRad == null)
			{
				SoftFleshController._stRad = new float[9];
				SoftFleshController._stLat2 = new float[9];
			}
			for (int i = 0; i < 9; i++)
			{
				SoftFleshController._stRad[i] = -1f;
				SoftFleshController._stLat2[i] = 1E+09f;
			}
			if (this.trackItems && this.savedAxisValid && this._segA != null && body.bonesArr != null && body.workMesh != null)
			{
				int ix = body.axisBoneIx;
				if (ix < 0)
				{
					for (int b = 0; b < body.bonesArr.Length; b++)
					{
						if (body.bonesArr[b] != null && body.bonesArr[b].name.ToLower().Contains("waist02"))
						{
							ix = b;
							break;
						}
					}
					if (ix < 0)
					{
						ix = 0;
					}
					body.axisBoneIx = ix;
				}
				Matrix4x4[] bps = ((body.bindPoses != null) ? body.bindPoses : body.workMesh.bindposes);
				if (ix < bps.Length)
				{
					Transform rb = body.bonesArr[ix];
					Vector3 Ew = rb.TransformPoint(bps[ix].MultiplyPoint3x4(this.savedEntryB));
					Vector3 dirB0 = body.tiltB * this.savedDirB;
					Vector3 dw = rb.TransformDirection(bps[ix].MultiplyVector(dirB0)).normalized;
					Vector3 followW = rb.TransformVector(bps[ix].MultiplyVector(body.followB));
					Vector3 EwR = Ew;
					Ew += followW;
					body.axisEwW = Ew;
					body.axisDwW = dw;
					body.axisFrameOK = true;
					body.axisFrameNo = Time.frameCount;
					for (int pIt = 0; pIt < this._segA.Length; pIt++)
					{
						if (this._segLen2[pIt] >= 0f && this._segItem[pIt] && this._segInsert[pIt])
						{
							float a0 = (this._segA[pIt].x - Ew.x) * dw.x + (this._segA[pIt].y - Ew.y) * dw.y + (this._segA[pIt].z - Ew.z) * dw.z;
							Vector3 eB = this._segA[pIt] + this._segAB[pIt];
							float a = (eB.x - Ew.x) * dw.x + (eB.y - Ew.y) * dw.y + (eB.z - Ew.z) * dw.z;
							bool hasLen = Mathf.Abs(a - a0) > 1E-06f;
							bool spans = (hasLen ? (a0 * a <= 0f) : (Mathf.Abs(a0) <= Mathf.Max(this._segRad[pIt], 0.004f)));
							if (spans)
							{
								float tc = (hasLen ? Mathf.Clamp01(-a0 / (a - a0)) : 0f);
								Vector3 spc = this._segA[pIt] + this._segAB[pIt] * tc;
								Vector3 latc = spc - Ew - dw * ((spc.x - Ew.x) * dw.x + (spc.y - Ew.y) * dw.y + (spc.z - Ew.z) * dw.z);
								if (latc.sqrMagnitude <= 0.002025f)
								{
									float rc = this._segRad[pIt] + (this._segRadB[pIt] - this._segRad[pIt]) * tc;
									if (this._segProf[pIt] != null)
									{
										float rm2 = this._segProf[pIt].MaxRadiusAt(this._segW2L[pIt].MultiplyPoint3x4(spc)) * this._segScale[pIt] * this.itemRadScale;
										if (rm2 > 1E-05f)
										{
											rc = rm2;
										}
									}
									float lat2c = latc.sqrMagnitude;
									if (lat2c < bestLat2)
									{
										bestLat2 = lat2c;
										radAtPlane = rc;
										latPlaneW = spc - EwR - dw * ((spc.x - EwR.x) * dw.x + (spc.y - EwR.y) * dw.y + (spc.z - EwR.z) * dw.z);
										SoftFleshController.Prof pf = this._segProf[pIt];
										if (pf != null)
										{
											Vector3 pl = this._segW2L[pIt].MultiplyPoint3x4(spc);
											float alng = Vector3.Dot(pl - pf.origin, pf.ax);
											body.trkAlongT = ((pf.len > 1E-06f) ? (alng / pf.len) : (-1f));
											body.trkRadAngMm = pf.SampleRadius(pl) * this._segScale[pIt] * this.itemRadScale * 1000f;
											body.trkLobeLenMm = pf.len * this._segScale[pIt] * 1000f;
										}
										else
										{
											body.trkAlongT = -1f;
											body.trkRadAngMm = -1f;
											body.trkLobeLenMm = -1f;
										}
										body.trkLobe = ((this._pressers[pIt].a != null) ? this._pressers[pIt].a.name : "?");
										Vector3 tang = ((this._segProf[pIt] != null) ? this._pressers[pIt].a.localToWorldMatrix.MultiplyVector(this._segProf[pIt].ax) : this._segAB[pIt]);
										if (tang.sqrMagnitude > 1E-10f)
										{
											tang.Normalize();
											if (Vector3.Dot(tang, dw) < 0f)
											{
												tang = -tang;
											}
											body.itemDirW = tang;
										}
									}
								}
							}
							if (this.taperCanal && body.canalLen > 0.01f)
							{
								float a0s = (this._segA[pIt].x - Ew.x) * dw.x + (this._segA[pIt].y - Ew.y) * dw.y + (this._segA[pIt].z - Ew.z) * dw.z;
								Vector3 eBs = this._segA[pIt] + this._segAB[pIt];
								float a1s = (eBs.x - Ew.x) * dw.x + (eBs.y - Ew.y) * dw.y + (eBs.z - Ew.z) * dw.z;
								bool hasLenS = Mathf.Abs(a1s - a0s) > 1E-06f;
								float sphR = Mathf.Max(this._segRad[pIt], 0.004f);
								float deepAx = Mathf.Max(a0s, a1s);
								Vector3 dEnd = ((a1s > a0s) ? eBs : this._segA[pIt]);
								if ((dEnd - Ew - dw * ((dEnd.x - Ew.x) * dw.x + (dEnd.y - Ew.y) * dw.y + (dEnd.z - Ew.z) * dw.z)).sqrMagnitude <= 0.002025f)
								{
									float rEnd = Mathf.Max(this._segRad[pIt], this._segRadB[pIt]);
									if (this._segProf[pIt] != null)
									{
										float rp = this._segProf[pIt].MaxRadiusAt(this._segW2L[pIt].MultiplyPoint3x4(dEnd)) * this._segScale[pIt] * this.itemRadScale;
										if (rp > 1E-05f)
										{
											rEnd = rp;
										}
									}
									if (deepAx + rEnd > depth)
									{
										depth = deepAx + rEnd;
									}
								}
								for (int j = 0; j < 9; j++)
								{
									float zk = body.canalLen * (float)j / 8f;
									bool spansK = (hasLenS ? ((a0s - zk) * (a1s - zk) <= 0f) : (Mathf.Abs(a0s - zk) <= sphR));
									if (spansK)
									{
										float tk = (hasLenS ? Mathf.Clamp01((zk - a0s) / (a1s - a0s)) : 0f);
										Vector3 spk = this._segA[pIt] + this._segAB[pIt] * tk;
										float l2k = (spk - Ew - dw * ((spk.x - Ew.x) * dw.x + (spk.y - Ew.y) * dw.y + (spk.z - Ew.z) * dw.z)).sqrMagnitude;
										if (l2k <= 0.002025f)
										{
											float rk = this._segRad[pIt] + (this._segRadB[pIt] - this._segRad[pIt]) * tk;
											if (this._segProf[pIt] != null)
											{
												float rmk = this._segProf[pIt].MaxRadiusAt(this._segW2L[pIt].MultiplyPoint3x4(spk)) * this._segScale[pIt] * this.itemRadScale;
												if (rmk > 1E-05f)
												{
													rk = rmk;
												}
											}
											if (!hasLenS)
											{
												float dz = a0s - zk;
												float rr = Mathf.Max(rk, 0.0001f);
												float ch = 1f - dz * dz / (rr * rr);
												if (ch <= 0f)
												{
													goto IL_0A92;
												}
												rk *= Mathf.Sqrt(ch);
											}
											if (l2k <= (rk + 0.005f) * (rk + 0.005f))
											{
												if (rk > SoftFleshController._stRad[j])
												{
													SoftFleshController._stRad[j] = rk;
												}
												if (l2k < SoftFleshController._stLat2[j])
												{
													SoftFleshController._stLat2[j] = l2k;
												}
											}
										}
									}
									IL_0A92:;
								}
							}
							for (int st = 0; st <= 8; st++)
							{
								float t = (float)st / 8f;
								Vector3 sp = this._segA[pIt] + this._segAB[pIt] * t;
								float ax = (sp.x - Ew.x) * dw.x + (sp.y - Ew.y) * dw.y + (sp.z - Ew.z) * dw.z;
								Vector3 lat = sp - Ew - dw * ax;
								if (lat.sqrMagnitude <= 0.0036f)
								{
									float r = this._segRad[pIt] + (this._segRadB[pIt] - this._segRad[pIt]) * t;
									if (this._segProf[pIt] != null)
									{
										float rm3 = this._segProf[pIt].MaxRadiusAt(this._segW2L[pIt].MultiplyPoint3x4(sp)) * this._segScale[pIt] * this.itemRadScale;
										if (rm3 > 1E-05f)
										{
											r = rm3;
										}
									}
									if (ax > depth)
									{
										depth = ax;
									}
									if (ax > -0.003f && ax < 0.008f && r > fbRad)
									{
										fbRad = r;
										fbLat = lat;
									}
								}
							}
						}
					}
				}
			}
			if (this.taperCanal)
			{
				if (body.canalWAt == null || body.canalWAt.Length != 9)
				{
					body.canalWAt = new float[9];
				}
				if (body.canalLatAt == null || body.canalLatAt.Length != 9)
				{
					body.canalLatAt = new float[9];
				}
				for (int k = 0; k < 9; k++)
				{
					body.canalLatAt[k] = ((SoftFleshController._stLat2[k] < 100000000f) ? (Mathf.Sqrt(SoftFleshController._stLat2[k]) * 1000f) : (-1f));
				}
				if (radAtPlane > 1E-05f && radAtPlane > SoftFleshController._stRad[0])
				{
					SoftFleshController._stRad[0] = radAtPlane;
				}
				int lastOcc = -1;
				for (int l = 0; l < 9; l++)
				{
					if (SoftFleshController._stRad[l] > 0f)
					{
						lastOcc = l;
					}
				}
				for (int m = 1; m < lastOcc; m++)
				{
					if (SoftFleshController._stRad[m] <= 0f)
					{
						int pk = m - 1;
						while (pk > 0 && SoftFleshController._stRad[pk] <= 0f)
						{
							pk--;
						}
						int nk = m + 1;
						while (nk < lastOcc && SoftFleshController._stRad[nk] <= 0f)
						{
							nk++;
						}
						if (SoftFleshController._stRad[pk] > 0f && SoftFleshController._stRad[nk] > 0f)
						{
							SoftFleshController._stRad[m] = Mathf.Lerp(SoftFleshController._stRad[pk], SoftFleshController._stRad[nk], (float)(m - pk) / (float)(nk - pk));
						}
					}
				}
				if (lastOcc > 1)
				{
					if (SoftFleshController._stSm == null || SoftFleshController._stSm.Length != 9)
					{
						SoftFleshController._stSm = new float[9];
					}
					for (int n = 0; n <= lastOcc; n++)
					{
						float c0 = SoftFleshController._stRad[n];
						float lft = ((n > 0 && SoftFleshController._stRad[n - 1] > 0f) ? SoftFleshController._stRad[n - 1] : c0);
						float rgt = ((n < lastOcc && SoftFleshController._stRad[n + 1] > 0f) ? SoftFleshController._stRad[n + 1] : c0);
						SoftFleshController._stSm[n] = (lft + 2f * c0 + rgt) * 0.25f;
					}
					for (int k2 = 0; k2 <= lastOcc; k2++)
					{
						SoftFleshController._stRad[k2] = SoftFleshController._stSm[k2];
					}
				}
				float r0m = ((SoftFleshController._stRad[0] > 1E-05f) ? SoftFleshController._stRad[0] : ((radAtPlane > 1E-05f) ? radAtPlane : (-1f)));
				for (int k3 = 0; k3 < 9; k3++)
				{
					float want = ((r0m > 1E-05f) ? ((SoftFleshController._stRad[k3] > 0f) ? Mathf.Clamp(SoftFleshController._stRad[k3] / r0m, 0f, 1.6f) : 0f) : 1f);
					body.canalWAt[k3] += (want - body.canalWAt[k3]) * (1f - Mathf.Exp(-12f * dt));
				}
			}
			this.planeExact = radAtPlane > 0f;
			if (!this.planeExact && fbRad > 0f)
			{
				radAtPlane = fbRad;
				latPlaneW = fbLat;
			}
			Vector3 targetB = Vector3.zero;
			if (depth > 0.004f && latPlaneW.sqrMagnitude > 1E-10f && body.axisFrameOK && body.axisBoneIx >= 0 && body.workMesh != null)
			{
				Matrix4x4[] bpsF = ((body.bindPoses != null) ? body.bindPoses : body.workMesh.bindposes);
				if (body.axisBoneIx < bpsF.Length)
				{
					Transform rbF = body.bonesArr[body.axisBoneIx];
					Vector3 lo = rbF.InverseTransformVector(latPlaneW);
					targetB = Vector3.ClampMagnitude(bpsF[body.axisBoneIx].inverse.MultiplyVector(lo), 0.03f) * this.followGain;
				}
			}
			body.followB += (targetB - body.followB) * (1f - Mathf.Exp(-20f * dt));
			if (targetB == Vector3.zero && body.followB.sqrMagnitude < 1E-10f)
			{
				body.followB = Vector3.zero;
			}
			if (radAtPlane <= 0f && depth > 0.004f && body.lastRadAtPlane > 0f)
			{
				radAtPlane = body.lastRadAtPlane;
			}
			body.lastRadAtPlane = ((depth > 0.004f) ? radAtPlane : 0f);
			body.trkRadMm = radAtPlane * 1000f;
			body.trkLatMm = ((bestLat2 < 100000000f) ? (Mathf.Sqrt(bestLat2) * 1000f) : (-1f));
			body.trkDepthMm = depth * 1000f;
			body.itemInside = depth > 0.004f;
			Quaternion targetB2 = Quaternion.identity;
			if (body.itemInside && this.entranceTilt > 0.001f && body.axisFrameOK && body.itemDirW.sqrMagnitude > 0.5f && body.workMesh != null && body.axisBoneIx >= 0 && body.bindPoses != null && body.axisBoneIx < body.bindPoses.Length)
			{
				Transform rbT = body.bonesArr[body.axisBoneIx];
				Matrix4x4 bpT = body.bindPoses[body.axisBoneIx];
				Vector3 dirB = bpT.inverse.MultiplyVector(rbT.InverseTransformDirection(body.itemDirW));
				if (dirB.sqrMagnitude > 1E-10f)
				{
					Quaternion q0 = Quaternion.FromToRotation(this.savedDirB.normalized, dirB.normalized);
					targetB2 = Quaternion.RotateTowards(Quaternion.identity, q0, 45f * this.entranceTilt);
				}
			}
			body.tiltB = Quaternion.Slerp(body.tiltB, targetB2, 1f - Mathf.Exp(-10f * dt));
			if (body.itemInside)
			{
				float ff = Mathf.Clamp01(SoftFleshController.WidthForRim(body, radAtPlane));
				float rimNow = SoftFleshController.MeasureRimNow(body);
				body.rimNowMm = ((rimNow >= 0f) ? (rimNow * 1000f) : (-1f));
				if (rimNow >= 0f && body.itemW > 0.02f)
				{
					body.itemWff = ff;
					float raw = body.itemW + (radAtPlane - rimNow) * 12f;
					tw = Mathf.Clamp01(Mathf.Clamp(raw, ff - this.widthTrim, ff + this.widthTrim));
				}
				else
				{
					tw = ff;
				}
				float cl = ((body.canalLen > 0.01f) ? body.canalLen : 0.05f);
				td = Mathf.Clamp01(depth / cl);
			}
			float errW = Mathf.Abs(tw - body.itemW);
			float errD = Mathf.Abs(td - body.itemD);
			float errM = ((errW > errD) ? errW : errD) - 0.08f;
			float rate = ((errM > 0f) ? (12f * (1f + 20f * errM)) : 12f);
			if (rate > 120f)
			{
				rate = 120f;
			}
			float fl = 1f - Mathf.Exp(-rate * dt);
			body.itemW += (tw - body.itemW) * fl;
			body.itemD += (td - body.itemD) * fl;
			if (body.itemW < 0.004f && tw <= 0f)
			{
				body.itemW = 0f;
			}
			if (body.itemD < 0.004f && td <= 0f)
			{
				body.itemD = 0f;
			}
			if (body.itemW > body.gapeLevel)
			{
				body.gapeLevel = body.itemW;
			}
			else if (!body.itemInside && body.gapeLevel > 0f)
			{
				float T = 0.4f + this.gapeHold * 30f;
				body.gapeLevel = Mathf.Max(0f, body.gapeLevel - dt / T);
			}
			float dvel = ((dt > 1E-05f) ? ((depth - body.lastItemDepth) / dt) : 0f);
			body.lastItemDepth = depth;
			if (!body.itemInside)
			{
				dvel = 0f;
			}
			float targ = Mathf.Clamp(dvel * 0.1f * this.suction, -0.012f, 0.012f);
			body.suckOff += (targ - body.suckOff) * (1f - Mathf.Exp(-10f * dt));
			if (targ == 0f && Mathf.Abs(body.suckOff) < 1E-05f)
			{
				body.suckOff = 0f;
			}
			this.trkW = body.itemW;
			this.trkD = body.itemD;
			this.trkG = body.gapeLevel;
			this.trkS = body.suckOff;
			this.trkRadMm = body.trkRadMm;
			this.trkDepthMm = body.trkDepthMm;
			this.trkLatMm = body.trkLatMm;
			this.trkRimMm = body.rimNowMm;
			this.trkTiltDeg = Quaternion.Angle(Quaternion.identity, body.tiltB);
			this.trkTiltMm = body.tiltMm;
			this.trkTiltReady = body.bakeTiltW != null;
			this.trkOff = body.crossOffM;
		}

		// Token: 0x06000094 RID: 148 RVA: 0x000179E8 File Offset: 0x00015BE8
		private static void SyncSpread(SoftFleshController.Body body)
		{
			if (body.spreadL != null)
			{
				body.spreadL.localRotation = body.spreadLRot;
				body.spreadL.localPosition = body.spreadLPos;
			}
			if (body.spreadR != null)
			{
				body.spreadR.localRotation = body.spreadRRot;
				body.spreadR.localPosition = body.spreadRPos;
			}
		}

		// Token: 0x06000095 RID: 149 RVA: 0x00017A55 File Offset: 0x00015C55
		private static void Z(Array a)
		{
			if (a != null)
			{
				Array.Clear(a, 0, a.Length);
			}
		}

		// Token: 0x06000096 RID: 150 RVA: 0x00017A68 File Offset: 0x00015C68
		private void SwapClothMesh(SoftFleshController.Body body, bool toOriginal)
		{
			for (int r = 0; r < this._cloth.Count; r++)
			{
				SoftFleshController.ClothRec rec = this._cloth[r];
				if (rec.body == body && !(rec.smr == null) && !(rec.orig == null) && !(rec.clone == null))
				{
					Mesh want = (toOriginal ? rec.orig : rec.clone);
					Mesh cur = rec.smr.sharedMesh;
					if (!(cur == want))
					{
						int nW = ((cur != null) ? cur.blendShapeCount : 0);
						float[] wS = null;
						if (nW > 0)
						{
							wS = new float[nW];
							for (int i = 0; i < nW; i++)
							{
								wS[i] = rec.smr.GetBlendShapeWeight(i);
							}
						}
						rec.smr.sharedMesh = want;
						if (wS != null)
						{
							int nO = want.blendShapeCount;
							int j = 0;
							while (j < nW && j < nO)
							{
								if (wS[j] != 0f)
								{
									rec.smr.SetBlendShapeWeight(j, wS[j]);
								}
								j++;
							}
						}
					}
				}
			}
		}

		// Token: 0x06000097 RID: 151 RVA: 0x00017B9C File Offset: 0x00015D9C
		public bool IsCharReady(ChaControl cha)
		{
			if (cha == null || cha.objBodyBone == null)
			{
				return false;
			}
			bool flag;
			try
			{
				flag = SoftFleshController.FindBodySmr(cha) != null;
			}
			catch
			{
				flag = false;
			}
			return flag;
		}

		// Token: 0x06000098 RID: 152 RVA: 0x00017BE8 File Offset: 0x00015DE8
		private string JitterLine(SoftFleshController.Body b)
		{
			return string.Concat(new string[]
			{
				"JITTER    render dmax ",
				(b.dgJitR * 1000f).ToString("F2"),
				"mm/f  sim dmax ",
				(b.dgJitS * 1000f).ToString("F2"),
				"mm/f   (still hand: render >> sim = the smoother jumps, not the physics)\nWAVE      since setup: |disp| max ",
				(b.dgDispEver * 1000f).ToString("F1"),
				"mm  |render| max ",
				(b.dgRdEver * 1000f).ToString("F1"),
				"mm  approach max ",
				b.dgApprEver.ToString("F2"),
				"m/s  impulse max ",
				b.dgImpEver.ToString("F3"),
				"   lim ",
				(this.maxDepth * 2000f).ToString("F0"),
				"mm  smooth ",
				this.smoothFactor.ToString("F2")
			});
		}

		// Token: 0x06000099 RID: 153 RVA: 0x00017D10 File Offset: 0x00015F10
		private string MembLine(SoftFleshController.Body b)
		{
			return string.Concat(new string[]
			{
				"MEMB      tension ",
				(this.skinTension * 1000f).ToString("F0"),
				"mm  bend ",
				this.skinBend.ToString(),
				"  region ",
				b.dgMembN.ToString(),
				" verts (",
				b.dgMembSeeds.ToString(),
				" in contact)  sweeps ",
				b.dgMembSweeps.ToString(),
				"  residual ",
				(b.dgMembRes * 1000f).ToString("F3"),
				"mm  surplus max ",
				(b.dgMembSur * 1000f).ToString("F1"),
				"mm  boundary step ",
				(b.dgMembRip * 1000f).ToString("F2"),
				"mm  ",
				b.dgMembMs.ToString("F2"),
				"ms  normalBoost ",
				this.normalBoost.ToString("F1")
			});
		}

		// Token: 0x0600009A RID: 154 RVA: 0x00017E54 File Offset: 0x00016054
		private string PitVert(SoftFleshController.Body b, int i, float v, float nbr)
		{
			if (i < 0)
			{
				return "-";
			}
			float[] rd = b.renderDisp;
			return string.Concat(new string[]
			{
				"v",
				i.ToString(),
				(i < b.origVertN) ? " orig" : " mid",
				" val ",
				(b.adjStart[i + 1] - b.adjStart[i]).ToString(),
				(b.seamOf != null && b.seamOf[i] >= 0) ? " seam" : "",
				(b.membCStamp != null && b.membCStamp[i] == b.membStampCur) ? " contact" : " free",
				" rd ",
				(rd != null) ? (rd[i] * 1000f).ToString("F1") : "?",
				" nbrMean ",
				(nbr * 1000f).ToString("F1"),
				(b.subPa != null && i >= b.origVertN && b.subPa[i] < 0) ? " UNPAIRED" : ""
			});
		}

		// Token: 0x0600009B RID: 155 RVA: 0x00017F90 File Offset: 0x00016190
		private string PitLine(SoftFleshController.Body b)
		{
			return string.Concat(new string[]
			{
				"PIT       field  max ",
				(b.dgPitF * 1000f).ToString("F1"),
				"mm  ",
				this.PitVert(b, b.dgPitFi, b.dgPitF, b.dgPitFNbr),
				"  | ",
				b.dgPitFN.ToString(),
				" verts >2mm\nPIT       geo    max ",
				(b.dgPitG * 1000f).ToString("F1"),
				"mm  ",
				this.PitVert(b, b.dgPitGi, b.dgPitG, b.dgPitGNbr),
				"  | ",
				b.dgPitGN.ToString(),
				" verts >2mm"
			});
		}

		// Token: 0x0600009C RID: 156 RVA: 0x00018074 File Offset: 0x00016274
		private void WriteMembDump(string dd)
		{
			if (this._bodies.Count == 0)
			{
				return;
			}
			if (Time.realtimeSinceStartup - this._membDumpT < 5f)
			{
				return;
			}
			this._membDumpT = Time.realtimeSinceStartup;
			SoftFleshController.Body b = this._bodies[0];
			if (b.memb == null || b.membList.Count == 0 || b.workMesh == null)
			{
				return;
			}
			int i = b.disp.Length;
			int cur = b.membStampCur;
			if (this._dumpTris == null || this._dumpTrisN != i)
			{
				this._dumpTris = b.workMesh.triangles;
				this._dumpTrisN = i;
			}
			if (this._membSb == null)
			{
				this._membSb = new StringBuilder(524288);
			}
			StringBuilder sb = this._membSb;
			sb.Length = 0;
			sb.Append("idx,kind,ring,contact,holdAge,val,seam,bx,by,bz,ox,oy,oz,nx,ny,nz,disp,tgtF,rd,memb,sur,subFix\n");
			List<int> ml = b.membList;
			for (int q = 0; q < ml.Count; q++)
			{
				int j = ml[q];
				Vector3 bp = b.baseVerts[j];
				Vector3 ov = b.outV[j];
				Vector3 nn = b.baseNormals[j];
				sb.Append(j).Append(',').Append((j >= b.origVertN) ? 1 : 0)
					.Append(',')
					.Append(b.membRing[j])
					.Append(',')
					.Append((b.membCStamp[j] == cur) ? 1 : 0)
					.Append(',')
					.Append((int)((b.holdAge != null) ? b.holdAge[j] : 0))
					.Append(',')
					.Append(b.adjStart[j + 1] - b.adjStart[j])
					.Append(',')
					.Append((b.seamOf != null && b.seamOf[j] >= 0) ? 1 : 0)
					.Append(',')
					.Append(SoftFleshController.F(bp.x))
					.Append(',')
					.Append(SoftFleshController.F(bp.y))
					.Append(',')
					.Append(SoftFleshController.F(bp.z))
					.Append(',')
					.Append(SoftFleshController.F(ov.x))
					.Append(',')
					.Append(SoftFleshController.F(ov.y))
					.Append(',')
					.Append(SoftFleshController.F(ov.z))
					.Append(',')
					.Append(SoftFleshController.F(nn.x))
					.Append(',')
					.Append(SoftFleshController.F(nn.y))
					.Append(',')
					.Append(SoftFleshController.F(nn.z))
					.Append(',')
					.Append(SoftFleshController.F(b.disp[j]))
					.Append(',')
					.Append(SoftFleshController.F((b.tgtF != null) ? b.tgtF[j] : 0f))
					.Append(',')
					.Append(SoftFleshController.F((b.renderDisp != null) ? b.renderDisp[j] : 0f))
					.Append(',')
					.Append(SoftFleshController.F(b.memb[j]))
					.Append(',')
					.Append(SoftFleshController.F((b.membSur != null) ? b.membSur[j] : 0f))
					.Append(',')
					.Append(SoftFleshController.F((b.subFix != null) ? b.subFix[j].magnitude : 0f))
					.Append('\n');
			}
			File.WriteAllText(Path.Combine(dd, "memb_live.csv"), sb.ToString());
			sb.Length = 0;
			int[] tris = this._dumpTris;
			int[] st = b.membStamp;
			if (tris != null)
			{
				int t = 0;
				while (t + 2 < tris.Length)
				{
					int a = tris[t];
					int c2 = tris[t + 1];
					int d = tris[t + 2];
					if (st[a] == cur && st[c2] == cur && st[d] == cur)
					{
						sb.Append(a).Append(' ').Append(c2)
							.Append(' ')
							.Append(d)
							.Append('\n');
					}
					t += 3;
				}
			}
			File.WriteAllText(Path.Combine(dd, "memb_faces.txt"), sb.ToString());
		}

		// Token: 0x0600009D RID: 157 RVA: 0x000184D4 File Offset: 0x000166D4
		private static void BuildMembNormals(SoftFleshController.Body body)
		{
			int i = body.baseVerts.Length;
			Vector3[] bv = body.baseVerts;
			Vector3[] bn = body.baseNormals;
			float invR = 55.555557f;
			float R2 = 0.00032399996f;
			float eA = ((body.avgEdge > 0.0001f) ? body.avgEdge : 0.01f);
			Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>(i / 2);
			for (int j = 0; j < i; j++)
			{
				long key = SoftFleshController.CellKey(bv[j], invR);
				List<int> lst;
				if (!cells.TryGetValue(key, out lst))
				{
					lst = new List<int>(8);
					cells[key] = lst;
				}
				lst.Add(j);
			}
			Vector3[] a = new Vector3[i];
			for (int k = 0; k < i; k++)
			{
				Vector3 p = bv[k];
				Vector3 acc = Vector3.zero;
				int cx = Mathf.FloorToInt(p.x * invR);
				int cy = Mathf.FloorToInt(p.y * invR);
				int cz = Mathf.FloorToInt(p.z * invR);
				for (int dx = -1; dx <= 1; dx++)
				{
					for (int dy = -1; dy <= 1; dy++)
					{
						for (int dz = -1; dz <= 1; dz++)
						{
							List<int> lst2;
							if (cells.TryGetValue(SoftFleshController.CellKey(cx + dx, cy + dy, cz + dz), out lst2))
							{
								for (int q = 0; q < lst2.Count; q++)
								{
									int l = lst2[q];
									float d2 = (bv[l] - p).sqrMagnitude;
									if (d2 < R2)
									{
										float t = 1f - d2 / R2;
										float w = t * t;
										float e = ((body.vertEdge != null && body.vertEdge[l] > 1E-05f) ? body.vertEdge[l] : eA);
										acc += bn[l] * (w * e * e);
									}
								}
							}
						}
					}
				}
				float mg = acc.magnitude;
				a[k] = ((mg > 1E-12f) ? (acc / mg) : bn[k]);
			}
			if (body.seamGroups != null)
			{
				for (int g = 0; g < body.seamGroups.Length; g++)
				{
					int[] grp = body.seamGroups[g];
					if (grp != null && grp.Length >= 2)
					{
						Vector3 acc2 = Vector3.zero;
						for (int m = 0; m < grp.Length; m++)
						{
							acc2 += a[grp[m]];
						}
						float mg2 = acc2.magnitude;
						if (mg2 >= 1E-08f)
						{
							acc2 /= mg2;
							for (int n = 0; n < grp.Length; n++)
							{
								a[grp[n]] = acc2;
							}
						}
					}
				}
			}
			body.membN = a;
		}

		// Token: 0x0600009E RID: 158 RVA: 0x000187B8 File Offset: 0x000169B8
		private static long CellKey(Vector3 p, float invR)
		{
			return SoftFleshController.CellKey(Mathf.FloorToInt(p.x * invR), Mathf.FloorToInt(p.y * invR), Mathf.FloorToInt(p.z * invR));
		}

		// Token: 0x0600009F RID: 159 RVA: 0x000187E6 File Offset: 0x000169E6
		private static long CellKey(int x, int y, int z)
		{
			return ((long)(x + 1000000) * 2097152L + (long)(y + 1000000)) * 2097152L + (long)(z + 1000000);
		}

		// Token: 0x060000A0 RID: 160 RVA: 0x00018810 File Offset: 0x00016A10
		private string MouthLine()
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < this._bodies.Count; i++)
			{
				SoftFleshController.Body b = this._bodies[i];
				if (b.isFace)
				{
					if (sb.Length > 0)
					{
						sb.Append('\n');
					}
					sb.Append("MOUTH     ").Append((b.cha != null) ? b.cha.name : "?").Append("  ")
						.Append(b.mouthInfo);
					int gN = 0;
					int cN = 0;
					if (b.mouthW != null)
					{
						for (int q = 0; q < b.mouthW.Length; q++)
						{
							if (b.mouthW[q] > 0f)
							{
								gN++;
							}
							if (b.mouthCore != null && b.mouthCore[q])
							{
								cN++;
							}
						}
					}
					float binNowAvg = -1f;
					if (b.mouthBinNow != null)
					{
						float sN = 0f;
						int nN = 0;
						for (int j = 0; j < b.mouthBinNow.Length; j++)
						{
							if (b.mouthBinNow[j] >= 0f)
							{
								sN += b.mouthBinNow[j];
								nN++;
							}
						}
						if (nN > 0)
						{
							binNowAvg = sN / (float)nN;
						}
					}
					sb.Append('\n').Append("FACE-GRP  group ").Append(gN)
						.Append(" verts (core ")
						.Append(cN)
						.Append(")  entered face branch ")
						.Append(b.fcReach)
						.Append(" (x segments)  sector path ")
						.Append(b.fcSkipBin)
						.Append("  pushed: core ")
						.Append(b.fcPushCore)
						.Append(" outer ")
						.Append(b.fcPushOuter)
						.Append("  max step ")
						.Append((b.fcStepOuter * 1000f).ToString("F1"))
						.Append("mm  min sector push ")
						.Append((b.fcPenOuterMin < 100000000f) ? (b.fcPenOuterMin * 1000f).ToString("F1") : "-")
						.Append("mm  lipStretch ")
						.Append(0f.ToString("F2"))
						.Append("  lining now avg r ")
						.Append((binNowAvg * 1000f).ToString("F1"))
						.Append("mm");
					float rInS = 0f;
					float rInMax = 0f;
					float rOutS = 0f;
					float rOutMax = 0f;
					int nIn = 0;
					int nOut = 0;
					int nInMoved = 0;
					if (b.mouthIdx != null && b.mouthIn != null && (b.pushR != null || b.pushB != null))
					{
						for (int q2 = 0; q2 < b.mouthIdx.Length; q2++)
						{
							int iq = b.mouthIdx[q2];
							if (b.mouthW != null && b.mouthW[iq] > 0f)
							{
								float pm = ((b.pushR != null) ? b.pushR[iq] : b.pushB[iq]).magnitude;
								if (b.mouthIn[iq])
								{
									rInS += pm;
									nIn++;
									if (pm > rInMax)
									{
										rInMax = pm;
									}
									if (pm > 0.0005f)
									{
										nInMoved++;
									}
								}
								else
								{
									rOutS += pm;
									nOut++;
									if (pm > rOutMax)
									{
										rOutMax = pm;
									}
								}
							}
						}
					}
					float resid = 0f;
					int nResid = 0;
					if (b.mouthOwner != null && b.mouthIdx != null && b.mouthIn != null && b.bakeV != null && b.bakeV.Count == b.baseVerts.Length && b.smr != null && this._segOwner != null)
					{
						Matrix4x4 l2wR = SoftFleshController.BakeL2W(b.smr);
						for (int q3 = 0; q3 < b.mouthIdx.Length; q3++)
						{
							int iq2 = b.mouthIdx[q3];
							if (b.mouthIn[iq2] && (b.mouthWet == null || !b.mouthWet[iq2]) && (b.mouthLining == null || !b.mouthLining[iq2]))
							{
								Vector3 wc = l2wR.MultiplyPoint3x4(b.bakeV[iq2]);
								float sMin = float.MaxValue;
								Vector3 rnp;
								Vector3 ru;
								float rr;
								int rl;
								float rla;
								float rlb;
								if (this.ItemNearest(b.mouthOwner, wc, out rnp, out ru, out rr, out sMin, out rl, out rla, out rlb) && sMin < -0.0005f)
								{
									nResid++;
									if (-sMin > resid)
									{
										resid = -sMin;
									}
								}
							}
						}
					}
					b.mwResid = resid;
					sb.Append('\n').Append("WRAP      inner set ").Append(b.mwInN)
						.Append(" (lining ")
						.Append(b.mwLiningN)
						.Append(")  inside item ")
						.Append(b.mwInside)
						.Append("  drawn ")
						.Append(b.mwCarried)
						.Append("  push max ")
						.Append((b.mouthPenMax * 1000f).ToString("F1"))
						.Append("mm  valid ")
						.Append(b.mouthPushValid)
						.Append("  tension ")
						.Append(0f.ToString("F2"))
						.Append("  clamped ")
						.Append(b.mwClamped)
						.Append("  | RENDERED residual: ")
						.Append(nResid)
						.Append(" inner verts still inside, deepest ")
						.Append((resid * 1000f).ToString("F1"))
						.Append("mm")
						.Append("  | RENDERED inner |push| avg ")
						.Append(((nIn > 0) ? (rInS / (float)nIn * 1000f) : 0f).ToString("F1"))
						.Append(" max ")
						.Append((rInMax * 1000f).ToString("F1"))
						.Append("mm (")
						.Append(nInMoved)
						.Append(" moved)  outer avg ")
						.Append(((nOut > 0) ? (rOutS / (float)nOut * 1000f) : 0f).ToString("F1"))
						.Append(" max ")
						.Append((rOutMax * 1000f).ToString("F1"))
						.Append("mm");
					if (b.mouthPushValid && this._mwP != null && this._mwDbgLobe != null)
					{
						for (int k = 0; k < b.mwTop.Length; k++)
						{
							int iv = b.mwTop[k];
							if (iv >= 0 && iv < this._mwP.Length)
							{
								float pmk = (this._mwP[iv] - this._mwR[iv]).magnitude;
								sb.Append('\n').Append("WRAP-TOP  v").Append(iv)
									.Append(b.mouthIn[iv] ? " IN " : " out")
									.Append("  push ")
									.Append((pmk * 1000f).ToString("F1"))
									.Append("mm  w ")
									.Append(b.mouthW[iv].ToString("F2"))
									.Append("  t ")
									.Append((b.mouthT[iv] * 1000f).ToString("F1"))
									.Append("  restPen ")
									.Append((this._mwPen[iv] * 1000f).ToString("F1"))
									.Append("  draw ")
									.Append(this._mwDraw[iv].ToString("F2"))
									.Append("  lobe ")
									.Append(this._mwDbgLobe[iv])
									.Append(" r ")
									.Append((this._mwDbgRB[iv] * 1000f).ToString("F1"))
									.Append("  dNow ")
									.Append((this._mwDbgDNow[iv] * 1000f).ToString("F1"))
									.Append("  target ")
									.Append((this._mwDbgTarget[iv] * 1000f).ToString("F1"))
									.Append("  face ")
									.Append(this._mwDbgFace[iv].ToString("F2"))
									.Append("  detail ")
									.Append((this._mwDbgDetail[iv] * 1000f).ToString("F1"))
									.Append("mm");
							}
						}
					}
					if (b.mouthOwner != null && this._segOwner != null)
					{
						sb.Append('\n').Append("WRAP-LOBES ");
						int pp = 0;
						while (pp < this._pressers.Count && pp < this._segLen2.Length && pp < this._segOwner.Length)
						{
							if (!(this._segOwner[pp] != b.mouthOwner))
							{
								float lenM = ((this._segLen2[pp] >= 0f) ? Mathf.Sqrt(this._segLen2[pp]) : (-1f));
								string clr = "-";
								if (b.mouthCValid && this._segLen2[pp] >= 0f)
								{
									Vector3 pa = this._segA[pp];
									Vector3 ab = this._segAB[pp];
									float t = ((this._segLen2[pp] > 1E-12f) ? (Vector3.Dot(b.mouthC - pa, ab) / this._segLen2[pp]) : 0f);
									if (t < 0f)
									{
										t = 0f;
									}
									else if (t > 1f)
									{
										t = 1f;
									}
									float dl = (b.mouthC - (pa + ab * t)).magnitude;
									float r = this._segRad[pp] + (this._segRadB[pp] - this._segRad[pp]) * t;
									clr = ((dl - r) * 1000f).ToString("F0");
								}
								sb.Append('[').Append(pp).Append(' ')
									.Append(this._pressers[pp].BName)
									.Append(this._segInsert[pp] ? " ins" : " press")
									.Append(" len ")
									.Append((lenM * 1000f).ToString("F0"))
									.Append(" r ")
									.Append((this._segRad[pp] * 1000f).ToString("F0"))
									.Append('/')
									.Append((this._segRadB[pp] * 1000f).ToString("F0"))
									.Append(" clr@lip ")
									.Append(clr)
									.Append("] ");
							}
							pp++;
						}
					}
					if (this.DeepOn && b.mouthOwner != null && b.mouthIdx != null && b.mouthIn != null && b.bakeV != null && b.bakeV.Count == b.baseVerts.Length && b.smr != null)
					{
						Matrix4x4 l2wL = SoftFleshController.BakeL2W(b.smr);
						int nRimL = 0;
						int nExposed = 0;
						int nInv = 0;
						float invMax = 0f;
						float gapMin = float.MaxValue;
						int gN2 = b.mouthIdx.Length;
						Vector3[] wpos = this._mwWposBuf;
						if (wpos == null || wpos.Length < b.baseVerts.Length)
						{
							wpos = (this._mwWposBuf = new Vector3[b.baseVerts.Length]);
						}
						float[] wclr = this._mwWclrBuf;
						if (wclr == null || wclr.Length < b.baseVerts.Length)
						{
							wclr = (this._mwWclrBuf = new float[b.baseVerts.Length]);
						}
						for (int q4 = 0; q4 < gN2; q4++)
						{
							int iq3 = b.mouthIdx[q4];
							wpos[iq3] = l2wL.MultiplyPoint3x4(b.bakeV[iq3]);
							Vector3 np2;
							Vector3 u2;
							float r2;
							float sc2;
							int lk2;
							float ra2;
							float rb2;
							wclr[iq3] = (this.ItemNearest(b.mouthOwner, wpos[iq3], out np2, out u2, out r2, out sc2, out lk2, out ra2, out rb2) ? sc2 : float.MaxValue);
						}
						for (int q5 = 0; q5 < gN2; q5++)
						{
							int iq4 = b.mouthIdx[q5];
							if (b.mouthIn[iq4] && (b.mouthLining == null || !b.mouthLining[iq4]) && (b.mouthWet == null || !b.mouthWet[iq4]) && wclr[iq4] != 3.4028235E+38f)
							{
								nRimL++;
								float best = float.MaxValue;
								bool any = false;
								for (int q6 = 0; q6 < gN2; q6++)
								{
									int l = b.mouthIdx[q6];
									if (!b.mouthIn[l] && wclr[l] != 3.4028235E+38f && (wpos[l] - wpos[iq4]).sqrMagnitude <= 2.5E-05f)
									{
										any = true;
										float g = wclr[l] - wclr[iq4];
										if (g < best)
										{
											best = g;
										}
									}
								}
								if (!any)
								{
									nExposed++;
								}
								else
								{
									if (best < gapMin)
									{
										gapMin = best;
									}
									if (best < 0f)
									{
										nInv++;
										if (-best > invMax)
										{
											invMax = -best;
										}
									}
								}
							}
						}
						sb.Append('\n').Append("WRAP-LAYER rendered: rim ").Append(nRimL)
							.Append("  exposed (no skin within 5mm) ")
							.Append(nExposed)
							.Append("  inverted (skin BELOW the rim) ")
							.Append(nInv)
							.Append(" max ")
							.Append((invMax * 1000f).ToString("F2"))
							.Append("mm  thinnest skin-over-rim ")
							.Append((gapMin < 1E+09f) ? (gapMin * 1000f).ToString("F2") : "-")
							.Append("mm")
							.Append("  | fold exposed-ahead ")
							.Append(b.mwExposed)
							.Append(" sunk ")
							.Append(b.mwSunk);
						float dS = 0f;
						float dN = 0f;
						int nS = 0;
						int nN2 = 0;
						float dSMin = 0f;
						float dNMin = 0f;
						for (int q7 = 0; q7 < gN2; q7++)
						{
							int iq5 = b.mouthIdx[q7];
							if (b.mouthIn[iq5] && (b.mouthLining == null || !b.mouthLining[iq5]) && wclr[iq5] != 3.4028235E+38f && this._mwClr != null && this._mwClr[iq5] < 1E+09f)
							{
								float dd = wclr[iq5] - ((this._mwClrFinal != null && this._mwClrFinal[iq5] < 1E+09f) ? this._mwClrFinal[iq5] : this._mwClr[iq5]);
								bool seam = b.seamOf != null && b.seamOf[iq5] >= 0;
								if (seam)
								{
									dS += dd;
									nS++;
									if (dd < dSMin)
									{
										dSMin = dd;
									}
								}
								else
								{
									dN += dd;
									nN2++;
									if (dd < dNMin)
									{
										dNMin = dd;
									}
								}
							}
						}
						sb.Append('\n').Append("WRAP-RENDER rim rendered minus wrap clearance: seam twins n=").Append(nS)
							.Append(" mean ")
							.Append(((nS > 0) ? (dS / (float)nS * 1000f) : 0f).ToString("F2"))
							.Append(" worst ")
							.Append((dSMin * 1000f).ToString("F2"))
							.Append("mm | others n=")
							.Append(nN2)
							.Append(" mean ")
							.Append(((nN2 > 0) ? (dN / (float)nN2 * 1000f) : 0f).ToString("F2"))
							.Append(" worst ")
							.Append((dNMin * 1000f).ToString("F2"))
							.Append("mm");
						SkinnedMeshRenderer smrF = b.smr;
						Mesh mF = ((smrF != null) ? smrF.sharedMesh : null);
						if (mF != null && mF.blendShapeCount > 0)
						{
							float kw = ((b.mouthKuwae >= 0) ? smrF.GetBlendShapeWeight(b.mouthKuwae) : (-1f));
							sb.Append('\n').Append("WRAP-SHAPE kuwae weight actual ").Append(kw.ToString("F1"))
								.Append(" vs assumed ")
								.Append((b.mouthOpenNow * 100f).ToString("F1"))
								.Append("  other shapes with weight:");
							int listed = 0;
							int si = 0;
							while (si < mF.blendShapeCount && listed < 8)
							{
								if (si != b.mouthKuwae)
								{
									float wv = smrF.GetBlendShapeWeight(si);
									if (wv > 0.5f)
									{
										sb.Append(' ').Append(mF.GetBlendShapeName(si)).Append('=')
											.Append(wv.ToString("F0"));
										listed++;
									}
								}
								si++;
							}
							if (listed == 0)
							{
								sb.Append(" none");
							}
						}
						if (b.bonesArr != null && b.bindPoses != null && b.bwArr != null && b.skinMCache != null && this._mwR != null)
						{
							float dMean = 0f;
							float dMax = 0f;
							int nD = 0;
							int nbM = b.skinMCache.Length;
							for (int q8 = 0; q8 < gN2; q8++)
							{
								int iq6 = b.mouthIdx[q8];
								if (b.mouthIn[iq6] && (b.mouthLining == null || !b.mouthLining[iq6]))
								{
									Vector3 pbind = b.baseVerts[iq6] + b.mouthDv[iq6] * ((b.mouthOpenNow > 0f) ? b.mouthOpenNow : 1f);
									BoneWeight w = b.bwArr[iq6];
									Vector3 rw = Vector3.zero;
									if (w.weight0 > 0f && w.boneIndex0 < nbM && b.bonesArr[w.boneIndex0] != null)
									{
										rw += (b.bonesArr[w.boneIndex0].localToWorldMatrix * b.bindPoses[w.boneIndex0]).MultiplyPoint3x4(pbind) * w.weight0;
									}
									if (w.weight1 > 0f && w.boneIndex1 < nbM && b.bonesArr[w.boneIndex1] != null)
									{
										rw += (b.bonesArr[w.boneIndex1].localToWorldMatrix * b.bindPoses[w.boneIndex1]).MultiplyPoint3x4(pbind) * w.weight1;
									}
									if (w.weight2 > 0f && w.boneIndex2 < nbM && b.bonesArr[w.boneIndex2] != null)
									{
										rw += (b.bonesArr[w.boneIndex2].localToWorldMatrix * b.bindPoses[w.boneIndex2]).MultiplyPoint3x4(pbind) * w.weight2;
									}
									if (w.weight3 > 0f && w.boneIndex3 < nbM && b.bonesArr[w.boneIndex3] != null)
									{
										rw += (b.bonesArr[w.boneIndex3].localToWorldMatrix * b.bindPoses[w.boneIndex3]).MultiplyPoint3x4(pbind) * w.weight3;
									}
									float dd2 = (rw - this._mwR[iq6]).magnitude;
									dMean += dd2;
									nD++;
									if (dd2 > dMax)
									{
										dMax = dd2;
									}
								}
							}
							sb.Append('\n').Append("WRAP-BONES rim skinned with the bones NOW vs the wrap's rest: mean ").Append(((nD > 0) ? (dMean / (float)nD * 1000f) : 0f).ToString("F2"))
								.Append(" max ")
								.Append((dMax * 1000f).ToString("F2"))
								.Append("mm (n=")
								.Append(nD)
								.Append(')');
							float sdMax = 0f;
							float sdMean = 0f;
							int nSd = 0;
							float pdMax = 0f;
							float pdMean = 0f;
							int nPd = 0;
							for (int q9 = 0; q9 < gN2; q9++)
							{
								int iq7 = b.mouthIdx[q9];
								if (b.mouthIn[iq7] && (b.mouthLining == null || !b.mouthLining[iq7]))
								{
									float sd = ((b.renderDisp != null) ? Mathf.Abs(b.renderDisp[iq7]) : 0f);
									sdMean += sd;
									nSd++;
									if (sd > sdMax)
									{
										sdMax = sd;
									}
									if (b.pushR != null && b.mouthPushW != null && b.bwArr != null && b.skinMCache != null)
									{
										Vector3 want = this.WorldToBindPush(b, iq7, b.mouthPushW[iq7]);
										float pd = (b.pushR[iq7] - want).magnitude;
										pdMean += pd;
										nPd++;
										if (pd > pdMax)
										{
											pdMax = pd;
										}
									}
								}
							}
							if (b.mouthHalo != null && b.mouthHalo.Length != 0)
							{
								float hMean = 0f;
								float hMax = 0f;
								int nH = 0;
								Vector3 dSum = Vector3.zero;
								for (int q10 = 0; q10 < b.mouthHalo.Length; q10++)
								{
									int ih = b.mouthHalo[q10];
									if (ih < b.bakeV.Count)
									{
										Vector3 Bh = l2wL.MultiplyPoint3x4(b.bakeV[ih]);
										Vector3 dh = Bh - this._mwR[ih];
										float dl2 = dh.magnitude;
										hMean += dl2;
										nH++;
										if (dl2 > hMax)
										{
											hMax = dl2;
										}
										dSum += dh;
									}
								}
								Vector3 sc3 = b.smr.transform.lossyScale;
								sb.Append('\n').Append("WRAP-BAKE  halo (no push): baked world minus skinned rest mean ").Append(((nH > 0) ? (hMean / (float)nH * 1000f) : 0f).ToString("F2"))
									.Append(" max ")
									.Append((hMax * 1000f).ToString("F2"))
									.Append("mm  mean vector ")
									.Append((nH > 0) ? (dSum / (float)nH * 1000f).ToString("F2") : "-")
									.Append("  smr lossyScale ")
									.Append(sc3.ToString("F3"))
									.Append("  [bake frame = position+rotation]");
							}
							sb.Append('\n').Append("WRAP-CHAN  rim: scalar render dent mean ").Append(((nSd > 0) ? (sdMean / (float)nSd * 1000f) : 0f).ToString("F2"))
								.Append(" max ")
								.Append((sdMax * 1000f).ToString("F2"))
								.Append("mm | rendered push minus wanted push (bind) mean ")
								.Append(((nPd > 0) ? (pdMean / (float)nPd * 1000f) : 0f).ToString("F2"))
								.Append(" max ")
								.Append((pdMax * 1000f).ToString("F2"))
								.Append("mm");
						}
					}
					sb.Append('\n').Append("WRAP-TIME ").Append(b.mwSkipped ? ("SKIPPED (still, " + b.mwSkipRun.ToString() + " frames)") : "computed")
						.Append("  ms: rest+record ")
						.Append(b.mwMs[0].ToString("F1"))
						.Append("  membrane ")
						.Append(b.mwMs[1].ToString("F1"))
						.Append("  S(R) ")
						.Append(b.mwMs[2].ToString("F1"))
						.Append("  fresh x3 ")
						.Append(b.mwMs[3].ToString("F1"))
						.Append("  corrective ")
						.Append(b.mwMs[4].ToString("F1"))
						.Append("  final ")
						.Append(b.mwMs[5].ToString("F1"));
					sb.Append('\n').Append("WRAP-TIME2 ms: rest ").Append(b.mwMs2[0].ToString("F2"))
						.Append("  frame ")
						.Append(b.mwMs2[1].ToString("F2"))
						.Append("  drag ")
						.Append(b.mwMs2[2].ToString("F2"))
						.Append("  record ")
						.Append(b.mwMs2[3].ToString("F2"))
						.Append("  pen ")
						.Append(b.mwMs2[4].ToString("F2"))
						.Append("  ring ")
						.Append(b.mwMs2[5].ToString("F2"))
						.Append("  smooth ")
						.Append(b.mwMs2[6].ToString("F2"))
						.Append("  outside ")
						.Append(b.mwMs2[7].ToString("F2"))
						.Append("  layer+lowpass ")
						.Append(b.mwMs2[8].ToString("F2"))
						.Append("  push ")
						.Append(b.mwMs2[9].ToString("F2"));
					sb.Append('\n').Append("WRAP-FRAME ").Append(this.mouthTiltFollow ? "ring frame = the ITEM's axis" : "ring frame = the mouth's own axis (fixed)")
						.Append("  | item axis tilt ")
						.Append(((b.mwFaceOut.sqrMagnitude > 0.5f && b.mouthOutW.sqrMagnitude > 0.5f) ? (Mathf.Acos(Mathf.Clamp(Vector3.Dot(b.mwFaceOut, b.mouthOutW), -1f, 1f)) * 57.29578f) : 0f).ToString("F0"))
						.Append(" deg off the mouth's own axis")
						.Append("  | collar: ")
						.Append(b.mwCollarN)
						.Append(" vertices, max ")
						.Append((b.mwCollarMax * 1000f).ToString("F1"))
						.Append("mm (tilt follow ")
						.Append(this.tiltFollow.ToString("F2"))
						.Append(" x engaged ")
						.Append(b.mwCollarEng.ToString("F2"))
						.Append(")")
						.Append("  | lip drag ")
						.Append((b.mwDragNow * 1000f).ToString("F2"))
						.Append("mm of ")
						.Append((this.lipDrag * 1000f).ToString("F0"))
						.Append("mm  | the shape opens to r ")
						.Append((b.mwRTarget * 1000f).ToString("F1"))
						.Append("mm (smoothed)");
					sb.Append('\n').Append("WRAP-SKIN  outer vertices inside the item after every stage: ").Append(b.mwSkinIn)
						.Append(" (deepest ")
						.Append((b.mwSkinInMax * 1000f).ToString("F2"))
						.Append("mm)")
						.Append("  | peak since playback started ")
						.Append(b.mwSkinInPk)
						.Append(" / ")
						.Append((b.mwSkinInPkMax * 1000f).ToString("F2"))
						.Append("mm")
						.Append("  | lip skin moved to the surface by the last step ")
						.Append(b.mwSkinFix)
						.Append(" (max ")
						.Append((b.mwSkinFixMax * 1000f).ToString("F1"))
						.Append("mm)")
						.Append("  | ring table filter ")
						.Append((b.mwTableTau * 1000f).ToString("F0"))
						.Append("ms this solve");
					sb.Append('\n').Append("WRAP-IN    lips carried INTO THE FACE (along the mouth's own axis): max ").Append((b.mwInMax * 1000f).ToString("F1"))
						.Append("mm at v")
						.Append(b.mwInV)
						.Append(" | along the item's axis max ")
						.Append((b.mwInAxMax * 1000f).ToString("F1"))
						.Append("mm")
						.Append(" (drag ")
						.Append((b.mwInDrag * 1000f).ToString("F1"))
						.Append(" ring ")
						.Append((b.mwInRing * 1000f).ToString("F1"))
						.Append(" smooth ")
						.Append((b.mwInSmooth * 1000f).ToString("F1"))
						.Append(" outside+layer ")
						.Append((b.mwInOut * 1000f).ToString("F1"))
						.Append(")  | cap ")
						.Append((this.lipPushIn * 1000f).ToString("F1"))
						.Append("mm on the upper lip + philtrum  | clamped ")
						.Append(b.mwInRedir)
						.Append(" vertices, largest excess ")
						.Append((b.mwInClampMax * 1000f).ToString("F1"))
						.Append("mm");
					if (this._mwS1 != null && this._mwS2 != null && this._mwR != null && this._mwP != null && this._mwGR != null && b.mouthW != null && this._mwPen != null && b.cha != null)
					{
						Vector3 hUp = b.cha.transform.up;
						Vector3 hRt = b.cha.transform.right;
						Vector3 hFw = b.cha.transform.forward;
						int[] far = new int[] { -1, -1, -1, -1, -1, -1 };
						float[] farM = new float[6];
						for (int q11 = 0; q11 < b.mouthIdx.Length; q11++)
						{
							int iF = b.mouthIdx[q11];
							if (-this._mwPen[iF] >= 0.015f)
							{
								float mv = (this._mwP[iF] - this._mwR[iF]).magnitude;
								if (mv >= 0.0002f)
								{
									for (int m = 0; m < 6; m++)
									{
										if (far[m] < 0 || mv > farM[m])
										{
											for (int n = 5; n > m; n--)
											{
												far[n] = far[n - 1];
												farM[n] = farM[n - 1];
											}
											far[m] = iF;
											farM[m] = mv;
											break;
										}
									}
								}
							}
						}
						Vector3 uAf = -b.mouthOutW;
						int k2 = 0;
						while (k2 < 6 && far[k2] >= 0)
						{
							int iF2 = far[k2];
							Vector3 uRf = -this._mwGR[iF2];
							Vector3 uTf = Vector3.Cross(uRf, uAf);
							float tmf = uTf.magnitude;
							uTf = ((tmf > 1E-06f) ? (uTf / tmf) : Vector3.zero);
							Vector3 dRing = this._mwS1[iF2] - this._mwR[iF2];
							Vector3 dSm = this._mwS2[iF2] - this._mwS1[iF2];
							Vector3 dOut = this._mwP[iF2] - this._mwS2[iF2];
							Vector3 dAll = this._mwP[iF2] - this._mwR[iF2];
							Vector3 off = this._mwR[iF2] - b.mouthC;
							sb.Append('\n').Append("WRAP-FAR   v").Append(iF2)
								.Append(b.mouthIn[iF2] ? " wall" : " skin")
								.Append(" w ")
								.Append(b.mouthW[iF2].ToString("F2"))
								.Append(" clear ")
								.Append((-this._mwPen[iF2] * 1000f).ToString("F0"))
								.Append("mm  at up ")
								.Append((Vector3.Dot(off, hUp) * 1000f).ToString("F0"))
								.Append(" right ")
								.Append((Vector3.Dot(off, hRt) * 1000f).ToString("F0"))
								.Append(" fwd ")
								.Append((Vector3.Dot(off, hFw) * 1000f).ToString("F0"))
								.Append("mm | moved ")
								.Append((dAll.magnitude * 1000f).ToString("F2"))
								.Append("mm = ring ")
								.Append((dRing.magnitude * 1000f).ToString("F2"))
								.Append(" smooth ")
								.Append((dSm.magnitude * 1000f).ToString("F2"))
								.Append(" outside+layer ")
								.Append((dOut.magnitude * 1000f).ToString("F2"))
								.Append(" | radial ")
								.Append((Vector3.Dot(dAll, uRf) * 1000f).ToString("F2"))
								.Append(" axial ")
								.Append((Vector3.Dot(dAll, uAf) * 1000f).ToString("F2"))
								.Append(" tangential ")
								.Append((Vector3.Dot(dAll, uTf) * 1000f).ToString("F2"))
								.Append(" | down ")
								.Append((Vector3.Dot(dAll, -hUp) * 1000f).ToString("F2"))
								.Append("mm");
							k2++;
						}
					}
					sb.Append('\n').Append("WRAP-JIT   last solve vs the one before: item moved ").Append((b.mwJitItem * 1000f).ToString("F2"))
						.Append("mm | skin moved max ")
						.Append((b.mwJitSkinMax * 1000f).ToString("F2"))
						.Append(" mean ")
						.Append((b.mwJitSkinMean * 1000f).ToString("F2"))
						.Append("mm | wall max ")
						.Append((b.mwJitWallMax * 1000f).ToString("F2"))
						.Append("mm | opening changed ")
						.Append((b.mwOpenDelta * 100f).ToString("F2"))
						.Append("%")
						.Append("  | worst of the last 60 solves: item ")
						.Append((b.mwJitItemW * 1000f).ToString("F2"))
						.Append(" skin ")
						.Append((b.mwJitSkinW * 1000f).ToString("F2"))
						.Append("mm opening ")
						.Append((b.mwJitOpenW * 100f).ToString("F2"))
						.Append("%");
					if (b.mwJitWV >= 0)
					{
						sb.Append('\n').Append("WRAP-JIT   that worst frame: ").Append(this.JitWorstLine(b));
					}
					if (b.mwJitHoldLine != null)
					{
						sb.Append('\n').Append("WRAP-JIT   peak since playback started: skin ").Append((b.mwJitSkinH * 1000f).ToString("F2"))
							.Append("mm  ")
							.Append(b.mwJitHoldLine);
					}
					sb.Append('\n').Append("WRAP-HUG   pulled toward the item: ").Append(b.mwHugN)
						.Append(" vertices, max ")
						.Append((b.mwHugMax * 1000f).ToString("F2"))
						.Append("mm  | of those, more than 10mm clear of it (the nose): ")
						.Append(b.mwHugFar);
					if (b.mwTiltN > 0)
					{
						sb.Append('\n').Append("WRAP-RING  rim exit vs the ring's radial: cos mean ").Append((b.mwTiltSum / (float)b.mwTiltN).ToString("F2"))
							.Append(" worst ")
							.Append(b.mwTiltMin.ToString("F2"))
							.Append(" (")
							.Append((Mathf.Acos(Mathf.Clamp(b.mwTiltMin, -1f, 1f)) * 57.29578f).ToString("F0"))
							.Append(" deg)")
							.Append("  | rim within 3mm of the item's axis ")
							.Append(b.mwNearAxis)
							.Append("  | far skin held back ")
							.Append(b.mwFarHeld)
							.Append(" (max ")
							.Append((b.mwFarHeldMax * 1000f).ToString("F1"))
							.Append("mm withheld)")
							.Append("  | lining held to its own need ")
							.Append(b.mwLinHeld)
							.Append(" (max ")
							.Append((b.mwLinHeldMax * 1000f).ToString("F1"))
							.Append("mm cut)")
							.Append("  | tangential part of the exit: mean ")
							.Append((b.mwGainSum / (float)b.mwTiltN * 1000f).ToString("F1"))
							.Append(" max ")
							.Append((b.mwGainMax * 1000f).ToString("F1"))
							.Append("mm");
					}
					if (b.mwTiltN > 0)
					{
						sb.Append('\n').Append("WRAP-CARRY lip band, own need per sector (mm): ");
						for (int k3 = 0; k3 < 24; k3++)
						{
							sb.Append((this._rgLipOwn[k3] < -100000000f) ? "-" : (this._rgLipOwn[k3] * 1000f).ToString("F0")).Append(' ');
						}
						sb.Append("| the bands behind would have raised ").Append(this._rgCarryN).Append(" sectors, most +")
							.Append((this._rgCarryMax * 1000f).ToString("F1"))
							.Append("mm at s")
							.Append(this._rgCarrySec)
							.Append("  (carried until 6.209; no longer)");
					}
					sb.Append('\n').Append("WRAP-FINAL post-smoothing check moved inner ").Append(b.mwFinIn)
						.Append(" (max ")
						.Append((b.mwFinInMax * 1000f).ToString("F1"))
						.Append("mm)  outer ")
						.Append(b.mwFinOut)
						.Append(" (max ")
						.Append((b.mwFinOutMax * 1000f).ToString("F1"))
						.Append("mm)");
					if (this.DeepOn && b.mouthTris != null && b.bakeV != null && b.bakeV.Count == b.baseVerts.Length && b.mouthW != null && b.mouthIn != null && this._mwP != null && this._mwR != null && this._mwS1 != null && this._mwGW != null && b.smr != null && b.baseNormals != null)
					{
						Matrix4x4 l2wC = SoftFleshController.BakeL2W(b.smr);
						int[] trisC = b.mouthTris;
						int nVC = b.baseVerts.Length;
						Vector3[] wposC = this._mwWposBuf;
						if (wposC == null || wposC.Length < nVC)
						{
							wposC = (this._mwWposBuf = new Vector3[nVC]);
						}
						for (int q12 = 0; q12 < b.mouthIdx.Length; q12++)
						{
							int iq8 = b.mouthIdx[q12];
							wposC[iq8] = l2wC.MultiplyPoint3x4(b.bakeV[iq8]);
						}
						List<int> outT = this._mwOutTris;
						if (outT == null)
						{
							outT = (this._mwOutTris = new List<int>(4096));
						}
						outT.Clear();
						int t2 = 0;
						while (t2 + 2 < trisC.Length)
						{
							int a = trisC[t2];
							int b2 = trisC[t2 + 1];
							int c2 = trisC[t2 + 2];
							if (b.mouthW[a] > 0f && b.mouthW[b2] > 0f && b.mouthW[c2] > 0f && !b.mouthIn[a] && !b.mouthIn[b2] && !b.mouthIn[c2])
							{
								outT.Add(t2);
							}
							t2 += 3;
						}
						int nRimC = 0;
						int nOutNow = 0;
						int nOutRest = 0;
						int nNew = 0;
						int nNoSkin = 0;
						float newMax = 0f;
						int[] topV = new int[] { -1, -1, -1, -1, -1, -1 };
						float[] topS = new float[6];
						for (int q13 = 0; q13 < b.mouthIdx.Length; q13++)
						{
							int iq9 = b.mouthIdx[q13];
							if (b.mouthIn[iq9] && (b.mouthLining == null || !b.mouthLining[iq9]) && (b.mouthWet == null || !b.mouthWet[iq9]))
							{
								nRimC++;
								int tN;
								float sNow = this.SignedToOuter(b, wposC, outT, trisC, wposC[iq9], out tN);
								int tR;
								float sRest = this.SignedToOuter(b, this._mwR, outT, trisC, this._mwR[iq9], out tR);
								if (float.IsNaN(sNow))
								{
									nNoSkin++;
								}
								else
								{
									bool oNow = sNow > 0.0003f;
									bool oRest = !float.IsNaN(sRest) && sRest > 0.0003f;
									if (oNow)
									{
										nOutNow++;
									}
									if (oRest)
									{
										nOutRest++;
									}
									if (oNow && !oRest)
									{
										nNew++;
										if (sNow > newMax)
										{
											newMax = sNow;
										}
										for (int k4 = 0; k4 < 6; k4++)
										{
											if (topV[k4] < 0 || sNow > topS[k4])
											{
												for (int m2 = 5; m2 > k4; m2--)
												{
													topV[m2] = topV[m2 - 1];
													topS[m2] = topS[m2 - 1];
												}
												topV[k4] = iq9;
												topS[k4] = sNow;
												break;
											}
										}
									}
								}
							}
						}
						sb.Append('\n').Append("WRAP-COVER rim ").Append(nRimC)
							.Append("  outside the skin: rendered ")
							.Append(nOutNow)
							.Append("  rest ")
							.Append(nOutRest)
							.Append("  newly out ")
							.Append(nNew)
							.Append(" (max ")
							.Append((newMax * 1000f).ToString("F2"))
							.Append("mm)  no skin within 15mm ")
							.Append(nNoSkin)
							.Append("  outer tris ")
							.Append(outT.Count)
							.Append(" | LAYER lifted ")
							.Append(b.mwLifted)
							.Append(" skin verts, max ")
							.Append((b.mwLiftMax * 1000f).ToString("F2"))
							.Append("mm");
						int k5 = 0;
						while (k5 < 6 && topV[k5] >= 0)
						{
							int iv2 = topV[k5];
							Vector3 oD = -this._mwGW[iv2];
							int tR2;
							float sR2 = this.SignedToOuter(b, this._mwR, outT, trisC, this._mwR[iv2], out tR2);
							int jn = -1;
							float jd = 3.6E-05f;
							for (int q14 = 0; q14 < b.mouthIdx.Length; q14++)
							{
								int j2 = b.mouthIdx[q14];
								if (!b.mouthIn[j2])
								{
									float dd3 = (this._mwR[j2] - this._mwR[iv2]).sqrMagnitude;
									if (dd3 < jd)
									{
										jd = dd3;
										jn = j2;
									}
								}
							}
							sb.Append('\n').Append("WRAP-COVER  v").Append(iv2)
								.Append(" t ")
								.Append((b.mouthT[iv2] * 1000f).ToString("F1"))
								.Append(" w ")
								.Append(b.mouthW[iv2].ToString("F2"))
								.Append("  skin dist rest ")
								.Append(float.IsNaN(sR2) ? "-" : (sR2 * 1000f).ToString("F2"))
								.Append(" -> rendered ")
								.Append((topS[k5] * 1000f).ToString("F2"))
								.Append("mm | radial ring ")
								.Append((Vector3.Dot(this._mwS1[iv2] - this._mwR[iv2], oD) * 1000f).ToString("F1"))
								.Append(" smooth ")
								.Append((Vector3.Dot(this._mwS2[iv2] - this._mwS1[iv2], oD) * 1000f).ToString("F1"))
								.Append(" outside ")
								.Append((Vector3.Dot(this._mwP[iv2] - this._mwS2[iv2], oD) * 1000f).ToString("F1"))
								.Append(" = ")
								.Append((Vector3.Dot(this._mwP[iv2] - this._mwR[iv2], oD) * 1000f).ToString("F1"))
								.Append(" rendered ")
								.Append((Vector3.Dot(wposC[iv2] - this._mwR[iv2], oD) * 1000f).ToString("F1"))
								.Append("mm");
							if (jn >= 0)
							{
								sb.Append(" | skin v").Append(jn).Append(" at ")
									.Append((Mathf.Sqrt(jd) * 1000f).ToString("F1"))
									.Append("mm w ")
									.Append(b.mouthW[jn].ToString("F2"))
									.Append(" radial ring ")
									.Append((Vector3.Dot(this._mwS1[jn] - this._mwR[jn], oD) * 1000f).ToString("F1"))
									.Append(" smooth ")
									.Append((Vector3.Dot(this._mwS2[jn] - this._mwS1[jn], oD) * 1000f).ToString("F1"))
									.Append(" outside ")
									.Append((Vector3.Dot(this._mwP[jn] - this._mwS2[jn], oD) * 1000f).ToString("F1"))
									.Append(" = ")
									.Append((Vector3.Dot(this._mwP[jn] - this._mwR[jn], oD) * 1000f).ToString("F1"))
									.Append(" rendered ")
									.Append((Vector3.Dot(wposC[jn] - this._mwR[jn], oD) * 1000f).ToString("F1"))
									.Append("mm");
							}
							else
							{
								sb.Append(" | no outer vertex within 6mm at rest");
							}
							k5++;
						}
					}
					if (this.DeepOn && b.mouthTris != null && b.bakeV != null && b.bakeV.Count == b.baseVerts.Length && b.mouthIn != null && b.smr != null && b.mouthCValid)
					{
						Matrix4x4 l2wX = SoftFleshController.BakeL2W(b.smr);
						int[] trisX = b.mouthTris;
						int nVX = b.baseVerts.Length;
						Vector3[] wp = this._mwWposBuf;
						if (wp == null || wp.Length < nVX)
						{
							wp = (this._mwWposBuf = new Vector3[nVX]);
						}
						bool[] have = this._mwWposOK;
						if (have == null || have.Length < nVX)
						{
							have = (this._mwWposOK = new bool[nVX]);
						}
						for (int i2 = 0; i2 < nVX; i2++)
						{
							have[i2] = false;
						}
						Vector3 mc = b.mouthC;
						sb.Append('\n').Append("WRAP-RIDE  ring coordinates and direction from the sheet partner: ").Append((b.mouthGR != null) ? "on" : "off");
						List<int> aT = this._mwSelfTris;
						if (aT == null)
						{
							aT = (this._mwSelfTris = new List<int>(2048));
						}
						aT.Clear();
						List<int> kT = this._mwSkinTris;
						if (kT == null)
						{
							kT = (this._mwSkinTris = new List<int>(2048));
						}
						kT.Clear();
						int t3 = 0;
						while (t3 + 2 < trisX.Length)
						{
							int a2 = trisX[t3];
							int b3 = trisX[t3 + 1];
							int c3 = trisX[t3 + 2];
							if (a2 < nVX && b3 < nVX && c3 < nVX)
							{
								if (!have[a2])
								{
									wp[a2] = l2wX.MultiplyPoint3x4(b.bakeV[a2]);
									have[a2] = true;
								}
								if ((wp[a2] - mc).sqrMagnitude <= 0.002025f)
								{
									if (!have[b3])
									{
										wp[b3] = l2wX.MultiplyPoint3x4(b.bakeV[b3]);
										have[b3] = true;
									}
									if (!have[c3])
									{
										wp[c3] = l2wX.MultiplyPoint3x4(b.bakeV[c3]);
										have[c3] = true;
									}
									aT.Add(t3);
									if (!b.mouthIn[a2] && !b.mouthIn[b3] && !b.mouthIn[c3])
									{
										kT.Add(t3);
									}
								}
							}
							t3 += 3;
						}
						int nSelf = 0;
						int nSkin = 0;
						float wH = 0f;
						float wHs = 0f;
						int sA = -1;
						int sB = -1;
						b.mwXVerts.Clear();
						b.mwXPairs.Clear();
						Vector3[] cen = this._mwSelfC;
						if (cen == null || cen.Length < aT.Count)
						{
							cen = (this._mwSelfC = new Vector3[(aT.Count < 512) ? 512 : aT.Count]);
						}
						float[] rad = this._mwSelfR;
						if (rad == null || rad.Length < aT.Count)
						{
							rad = (this._mwSelfR = new float[(aT.Count < 512) ? 512 : aT.Count]);
						}
						for (int k6 = 0; k6 < aT.Count; k6++)
						{
							int t4 = aT[k6];
							Vector3 p0 = wp[trisX[t4]];
							Vector3 p = wp[trisX[t4 + 1]];
							Vector3 p2 = wp[trisX[t4 + 2]];
							Vector3 cc = (p0 + p + p2) * 0.33333334f;
							cen[k6] = cc;
							float r3 = (p0 - cc).magnitude;
							float r4 = (p - cc).magnitude;
							float r5 = (p2 - cc).magnitude;
							rad[k6] = ((r3 > r4) ? ((r3 > r5) ? r3 : r5) : ((r4 > r5) ? r4 : r5));
						}
						for (int ka = 0; ka < aT.Count; ka++)
						{
							int ta = aT[ka];
							int a3 = trisX[ta];
							int a4 = trisX[ta + 1];
							int a2b = trisX[ta + 2];
							for (int kb = ka + 1; kb < aT.Count; kb++)
							{
								float rr2 = rad[ka] + rad[kb];
								if ((cen[kb] - cen[ka]).sqrMagnitude <= rr2 * rr2)
								{
									int tb = aT[kb];
									int b4 = trisX[tb];
									int b5 = trisX[tb + 1];
									int b2b = trisX[tb + 2];
									if (SoftFleshController.Wt(b, a3) != SoftFleshController.Wt(b, b4) && SoftFleshController.Wt(b, a3) != SoftFleshController.Wt(b, b5) && SoftFleshController.Wt(b, a3) != SoftFleshController.Wt(b, b2b) && SoftFleshController.Wt(b, a4) != SoftFleshController.Wt(b, b4) && SoftFleshController.Wt(b, a4) != SoftFleshController.Wt(b, b5) && SoftFleshController.Wt(b, a4) != SoftFleshController.Wt(b, b2b) && SoftFleshController.Wt(b, a2b) != SoftFleshController.Wt(b, b4) && SoftFleshController.Wt(b, a2b) != SoftFleshController.Wt(b, b5) && SoftFleshController.Wt(b, a2b) != SoftFleshController.Wt(b, b2b) && SoftFleshController.TriCross(wp[a3], wp[a4], wp[a2b], wp[b4], wp[b5], wp[b2b]))
									{
										nSelf++;
										bool skinA = !b.mouthIn[a3] || !b.mouthIn[a4] || !b.mouthIn[a2b];
										bool skinB = !b.mouthIn[b4] || !b.mouthIn[b5] || !b.mouthIn[b2b];
										bool vis = skinA || skinB;
										if (vis)
										{
											nSkin++;
											if (b.mwXPairs.Count < 800)
											{
												b.mwXPairs.Add(ta);
												b.mwXPairs.Add(tb);
												b.mwXVerts.Add(a3);
												b.mwXVerts.Add(a4);
												b.mwXVerts.Add(a2b);
												b.mwXVerts.Add(b4);
												b.mwXVerts.Add(b5);
												b.mwXVerts.Add(b2b);
											}
										}
										Vector3 nn = Vector3.Cross(wp[b5] - wp[b4], wp[b2b] - wp[b4]);
										float nm2 = nn.magnitude;
										if (nm2 >= 1E-12f)
										{
											nn /= nm2;
											if (Vector3.Dot(nn, SoftFleshController.SkinNormalW(b, b4)) < 0f)
											{
												nn = -nn;
											}
											Vector3 cb = cen[kb];
											for (int k7 = 0; k7 < 3; k7++)
											{
												float h = Vector3.Dot(wp[trisX[ta + k7]] - cb, nn);
												if (h > wH)
												{
													wH = h;
												}
												if (vis && h > wHs)
												{
													wHs = h;
													sA = ta;
													sB = tb;
												}
											}
										}
									}
								}
							}
						}
						sb.Append('\n').Append("WRAP-SELF  face against itself within 45mm of the mouth: ").Append(nSelf)
							.Append(" crossing pairs of ")
							.Append(aT.Count)
							.Append(" triangles  |  AT THE SKIN (visible) ")
							.Append(nSkin)
							.Append("  inside the cavity (hidden) ")
							.Append(nSelf - nSkin);
						if (sA >= 0)
						{
							sb.Append('\n').Append("WRAP-SELF   visible worst ").Append((wHs * 1000f).ToString("F2"))
								.Append("mm | A ")
								.Append(SoftFleshController.MatOf(b, sA));
							for (int k8 = 0; k8 < 3; k8++)
							{
								int v3 = trisX[sA + k8];
								sb.Append(" v").Append(v3).Append(SoftFleshController.VertTag(b, v3));
							}
							sb.Append(" | B ").Append(SoftFleshController.MatOf(b, sB));
							for (int k9 = 0; k9 < 3; k9++)
							{
								int v4 = trisX[sB + k9];
								sb.Append(" v").Append(v4).Append(SoftFleshController.VertTag(b, v4));
							}
							if (this._mwS1 != null && this._mwR != null && this._mwGR != null && b.mouthW != null)
							{
								int wv2 = -1;
								float hb = -1E+09f;
								for (int k10 = 0; k10 < 3; k10++)
								{
									int v5 = trisX[sA + k10];
									if (b.mouthIn[v5] && b.mouthW[v5] > 0f)
									{
										wv2 = v5;
										break;
									}
								}
								if (wv2 < 0)
								{
									for (int k11 = 0; k11 < 3; k11++)
									{
										int v6 = trisX[sB + k11];
										if (b.mouthIn[v6] && b.mouthW[v6] > 0f)
										{
											wv2 = v6;
											break;
										}
									}
								}
								if (wv2 >= 0 && hb < 1E+09f)
								{
									Vector3 oD2 = -this._mwGR[wv2];
									sb.Append('\n').Append("WRAP-SELF   wall v").Append(wv2)
										.Append(" radial: ring ")
										.Append((Vector3.Dot(this._mwS1[wv2] - this._mwR[wv2], oD2) * 1000f).ToString("F1"))
										.Append(" smooth ")
										.Append((Vector3.Dot(this._mwS2[wv2] - this._mwS1[wv2], oD2) * 1000f).ToString("F1"))
										.Append(" outside ")
										.Append((Vector3.Dot(this._mwP[wv2] - this._mwS2[wv2], oD2) * 1000f).ToString("F1"))
										.Append("mm");
									int pj = -1;
									float pd2 = 1E+09f;
									for (int q15 = 0; q15 < b.mouthIdx.Length; q15++)
									{
										int j3 = b.mouthIdx[q15];
										if (!b.mouthIn[j3])
										{
											float dd4 = (this._mwR[j3] - this._mwR[wv2]).sqrMagnitude;
											if (dd4 < pd2)
											{
												pd2 = dd4;
												pj = j3;
											}
										}
									}
									if (pj >= 0)
									{
										sb.Append(" | its skin v").Append(pj).Append(" at ")
											.Append((Mathf.Sqrt(pd2) * 1000f).ToString("F1"))
											.Append("mm: ring ")
											.Append((Vector3.Dot(this._mwS1[pj] - this._mwR[pj], oD2) * 1000f).ToString("F1"))
											.Append(" smooth ")
											.Append((Vector3.Dot(this._mwS2[pj] - this._mwS1[pj], oD2) * 1000f).ToString("F1"))
											.Append(" outside ")
											.Append((Vector3.Dot(this._mwP[pj] - this._mwS2[pj], oD2) * 1000f).ToString("F1"))
											.Append(" layer ")
											.Append(((this._mwLift != null) ? (this._mwLift[pj] * 1000f) : 0f).ToString("F1"))
											.Append("mm");
									}
								}
							}
							if (this._mwGR != null && b.mouthAngR != null && b.mouthAloR != null && this._rgZStep > 1E-09f && b.mouthW != null)
							{
								int secW = -1;
								for (int side = 0; side < 2; side++)
								{
									for (int k12 = 0; k12 < 3; k12++)
									{
										int v7 = trisX[((side == 0) ? sA : sB) + k12];
										if (b.mouthW[v7] <= 0f)
										{
											sb.Append('\n').Append("WRAP-CELL   v").Append(v7)
												.Append(" NOT IN THE GROUP");
										}
										else
										{
											float angV = ((this._mwAngW != null) ? this._mwAngW[v7] : b.mouthAngR[v7]);
											float aloV = ((this._mwAloW != null) ? this._mwAloW[v7] : b.mouthAloR[v7]);
											float fs2 = (angV + 3.1415927f) * 3.8197186f;
											int sb2 = (int)fs2;
											if (sb2 < 0)
											{
												sb2 = 0;
											}
											else if (sb2 > 23)
											{
												sb2 = 23;
											}
											float fz2 = (aloV - this._rgZMin) / this._rgZStep;
											int zb2 = (int)(fz2 + 0.5f);
											if (zb2 < 0)
											{
												zb2 = 0;
											}
											else if (zb2 > 7)
											{
												zb2 = 7;
											}
											float dC;
											float axC;
											this.RingEval(b, v7, this._rgZMin, this._rgZStep, out dC, out axC);
											if (b.mouthIn[v7] && secW < 0)
											{
												secW = sb2;
											}
											if (b.mouthW[v7] > 0f)
											{
												Vector3[] mwGR = this._mwGR;
											}
											Vector3 oD3 = -this._mwGR[v7];
											sb.Append('\n').Append("WRAP-CELL   v").Append(v7)
												.Append(b.mouthIn[v7] ? ((b.mouthLining != null && b.mouthLining[v7]) ? " lining" : " wall  ") : " skin  ")
												.Append(" w ")
												.Append(b.mouthW[v7].ToString("F2"))
												.Append(" sec ")
												.Append(sb2)
												.Append(" band ")
												.Append(zb2)
												.Append(" (alo ")
												.Append((aloV * 1000f).ToString("F1"))
												.Append("mm)  cell d ")
												.Append((dC * 1000f).ToString("F1"))
												.Append(" ax ")
												.Append((axC * 1000f).ToString("F1"))
												.Append(" | ring ")
												.Append((Vector3.Dot(this._mwS1[v7] - this._mwR[v7], oD3) * 1000f).ToString("F1"))
												.Append(" smooth ")
												.Append((Vector3.Dot(this._mwS2[v7] - this._mwS1[v7], oD3) * 1000f).ToString("F1"))
												.Append(" outside ")
												.Append((Vector3.Dot(this._mwP[v7] - this._mwS2[v7], oD3) * 1000f).ToString("F1"))
												.Append(" layer ")
												.Append(((this._mwLift != null) ? (this._mwLift[v7] * 1000f) : 0f).ToString("F1"))
												.Append(" | restPen ")
												.Append((this._mwPen[v7] * 1000f).ToString("F1"))
												.Append(" clrEnd ")
												.Append((this._mwClrFinal != null && this._mwClrFinal[v7] < 100000000f) ? (this._mwClrFinal[v7] * 1000f).ToString("F1") : "-");
										}
									}
								}
								if (secW >= 0)
								{
									sb.Append('\n').Append("WRAP-CELL   table sector ").Append(secW)
										.Append(" by depth band (deep -> exterior), radial mm:");
									for (int zb3 = 0; zb3 < 8; zb3++)
									{
										sb.Append(' ').Append((this._rgD[zb3 * 24 + secW] * 1000f).ToString("F1"));
									}
									sb.Append("  axial mm:");
									for (int zb4 = 0; zb4 < 8; zb4++)
									{
										sb.Append(' ').Append((this._rgA[zb4 * 24 + secW] * 1000f).ToString("F1"));
									}
									sb.Append("  band 0 alo ").Append((this._rgZMin * 1000f).ToString("F1")).Append("mm step ")
										.Append((this._rgZStep * 1000f).ToString("F1"))
										.Append("mm");
								}
							}
						}
						if (b.cha != null)
						{
							SkinnedMeshRenderer[] smrs = b.cha.GetComponentsInChildren<SkinnedMeshRenderer>();
							int nrep = 0;
							int k13 = 0;
							while (k13 < smrs.Length && nrep < 6)
							{
								SkinnedMeshRenderer sm2 = smrs[k13];
								if (!(sm2 == null) && !(sm2 == b.smr) && !(sm2.sharedMesh == null) && sm2.gameObject.activeInHierarchy && (sm2.bounds.ClosestPoint(mc) - mc).sqrMagnitude <= 0.002025f)
								{
									if (this._mwOtherBake == null)
									{
										this._mwOtherBake = new Mesh();
									}
									sm2.BakeMesh(this._mwOtherBake);
									if (this._mwOtherV == null)
									{
										this._mwOtherV = new List<Vector3>(4096);
									}
									this._mwOtherBake.GetVertices(this._mwOtherV);
									Matrix4x4 l2wO = SoftFleshController.BakeL2W(sm2);
									int nNear = 0;
									int nOutside = 0;
									float outMax = 0f;
									int outV = -1;
									for (int q16 = 0; q16 < this._mwOtherV.Count; q16++)
									{
										Vector3 pw = l2wO.MultiplyPoint3x4(this._mwOtherV[q16]);
										if ((pw - mc).sqrMagnitude <= 0.002025f)
										{
											nNear++;
											int tOut;
											float sd2 = this.SignedToOuter(b, wp, this._mwSkinTris, trisX, pw, out tOut);
											if (!float.IsNaN(sd2) && sd2 > 0.0005f)
											{
												nOutside++;
												if (sd2 > outMax)
												{
													outMax = sd2;
													outV = q16;
												}
											}
										}
									}
									if (nNear != 0)
									{
										nrep++;
										Material[] mats2 = sm2.sharedMaterials;
										string mn = ((mats2 != null && mats2.Length != 0 && mats2[0] != null) ? mats2[0].name : "-");
										sb.Append('\n').Append("WRAP-OTHER ").Append(sm2.name)
											.Append(" [")
											.Append(mn)
											.Append("]  verts near the mouth ")
											.Append(nNear)
											.Append("  OUTSIDE the face ")
											.Append(nOutside)
											.Append(" max ")
											.Append((outMax * 1000f).ToString("F2"))
											.Append("mm")
											.Append((outV >= 0) ? ("  worst vert " + outV.ToString()) : "");
									}
								}
								k13++;
							}
						}
					}
					if (b.mouthTris != null && b.bakeV != null && b.bakeV.Count == b.baseVerts.Length && b.mouthW != null && this._mwP != null && this._mwR != null && b.smr != null)
					{
						Matrix4x4 l2wB = SoftFleshController.BakeL2W(b.smr);
						int[] tris = b.mouthTris;
						int[] topT = new int[] { -1, -1, -1 };
						float[] topL = new float[3];
						int t5 = 0;
						while (t5 + 2 < tris.Length)
						{
							int a5 = tris[t5];
							int b6 = tris[t5 + 1];
							int c4 = tris[t5 + 2];
							if (b.mouthW[a5] > 0f || b.mouthW[b6] > 0f || b.mouthW[c4] > 0f)
							{
								Vector3 pa2 = b.bakeV[a5];
								Vector3 pb = b.bakeV[b6];
								Vector3 pc = b.bakeV[c4];
								float e = (pa2 - pb).sqrMagnitude;
								float e2 = (pb - pc).sqrMagnitude;
								if (e2 > e)
								{
									e = e2;
								}
								e2 = (pc - pa2).sqrMagnitude;
								if (e2 > e)
								{
									e = e2;
								}
								for (int k14 = 0; k14 < 3; k14++)
								{
									if (e > topL[k14])
									{
										for (int m3 = 2; m3 > k14; m3--)
										{
											topL[m3] = topL[m3 - 1];
											topT[m3] = topT[m3 - 1];
										}
										topL[k14] = e;
										topT[k14] = t5;
										break;
									}
								}
							}
							t5 += 3;
						}
						Vector3 axW = b.mouthAxU;
						for (int k15 = 0; k15 < 3; k15++)
						{
							int t6 = topT[k15];
							if (t6 >= 0)
							{
								int a6 = tris[t6];
								int b7 = tris[t6 + 1];
								int c5 = tris[t6 + 2];
								float e3 = (b.baseVerts[a6] - b.baseVerts[b7]).magnitude;
								float e4 = (b.baseVerts[b7] - b.baseVerts[c5]).magnitude;
								float e5 = (b.baseVerts[c5] - b.baseVerts[a6]).magnitude;
								float eRest = ((e3 > e4) ? ((e3 > e5) ? e3 : e5) : ((e4 > e5) ? e4 : e5));
								sb.Append('\n').Append("WRAP-BIG  tri ").Append(t6 / 3)
									.Append("  longest edge ")
									.Append((Mathf.Sqrt(topL[k15]) * 1000f).ToString("F1"))
									.Append("mm (rest ")
									.Append((eRest * 1000f).ToString("F1"))
									.Append(")");
								int[] vs3 = new int[] { a6, b7, c5 };
								for (int m4 = 0; m4 < 3; m4++)
								{
									int v8 = vs3[m4];
									Vector3 push = ((b.mouthW[v8] > 0f && this._mwSeg != null && v8 < this._mwP.Length) ? (this._mwP[v8] - this._mwR[v8]) : Vector3.zero);
									float along = Vector3.Dot(push, axW);
									float across = (push - axW * along).magnitude;
									string cls = ((b.mouthW[v8] <= 0f) ? "out-of-group" : ((b.mouthLining != null && b.mouthLining[v8]) ? "LINING" : (b.mouthIn[v8] ? "rim" : "outer")));
									sb.Append('\n').Append("          v").Append(v8)
										.Append(' ')
										.Append(cls)
										.Append("  w ")
										.Append(b.mouthW[v8].ToString("F2"))
										.Append("  push along ")
										.Append((along * 1000f).ToString("F1"))
										.Append(" across ")
										.Append((across * 1000f).ToString("F1"))
										.Append("mm  restPen ")
										.Append((b.mouthW[v8] > 0f) ? (this._mwPen[v8] * 1000f).ToString("F1") : "-")
										.Append("  draw ")
										.Append((b.mouthW[v8] > 0f) ? this._mwDraw[v8].ToString("F2") : "-")
										.Append("  clr ")
										.Append((b.mouthW[v8] > 0f && this._mwClr[v8] < 1E+09f) ? (this._mwClr[v8] * 1000f).ToString("F1") : "-")
										.Append("  link ")
										.Append((b.mouthW[v8] > 0f) ? this._mwSeg[v8] : (-9));
								}
							}
						}
					}
					sb.Append('\n').Append("WRAP-CHAIN ").Append(this._chN)
						.Append(" points, r (mm):");
					for (int k16 = 0; k16 < this._chN; k16++)
					{
						sb.Append(' ').Append((this._chR[k16] * 1000f).ToString("F0")).Append('(')
							.Append((this._chRaw[k16] * 1000f).ToString("F0"))
							.Append(')');
					}
					sb.Append('\n').Append("WRAP-AXIS ").Append(b.mwAxisInfo);
				}
			}
			if (sb.Length <= 0)
			{
				return "MOUTH     (no face)";
			}
			return sb.ToString();
		}

		// Token: 0x060000A1 RID: 161 RVA: 0x0001DAFC File Offset: 0x0001BCFC
		private int InsideCount()
		{
			int i = 0;
			for (int q = 0; q < this._bodies.Count; q++)
			{
				i += this._bodies[q].dgInsideN;
			}
			return i;
		}

		// Token: 0x060000A2 RID: 162 RVA: 0x0001DB38 File Offset: 0x0001BD38
		private string DepthLine()
		{
			if (this._bodies.Count == 0)
			{
				return "DEPTH     (no body)";
			}
			SoftFleshController.Body b = this._bodies[0];
			float rP = this.touchRadius;
			return string.Concat(new string[]
			{
				"DEPTH     axis h ",
				(b.dgH * 1000f).ToString("F1"),
				"mm  profRaw ",
				(b.dgProfRaw * 1000f).ToString("F1"),
				"  profCap ",
				(b.dgProfCap * 1000f).ToString("F1"),
				" (knuckle ",
				(b.dgProfProx * 1000f).ToString("F1"),
				" / tip ",
				(b.dgProfDist * 1000f).ToString("F1"),
				")  tgt(after slew) ",
				(b.dgTgt * 1000f).ToString("F1"),
				"  disp ",
				(b.dgDisp * 1000f).ToString("F1"),
				"mm  [deepest: ",
				(b.dgProfP >= 0 && SoftFleshController._segName != null && b.dgProfP < SoftFleshController._segName.Length && SoftFleshController._segName[b.dgProfP] != null) ? SoftFleshController._segName[b.dgProfP] : "-",
				" end depth ",
				(b.dgTipD * 1000f).ToString("F1"),
				"mm  l ",
				(b.dgL * 1000f).ToString("F1"),
				"mm]\nFINGER    ",
				(b.dgFingerP >= 0 && SoftFleshController._segName != null && b.dgFingerP < SoftFleshController._segName.Length && SoftFleshController._segName[b.dgFingerP] != null) ? SoftFleshController._segName[b.dgFingerP] : "-",
				"  tip depth ",
				(b.dgFingerTip * 1000f).ToString("F1"),
				"mm (at the vertex nearest the tip)  its max target ",
				(b.dgFingerProf * 1000f).ToString("F1"),
				"mm  inside ",
				b.dgFingerInside.ToString(),
				"  its skips: latch ",
				b.dgFingerLatch.ToString(),
				"  far ",
				b.dgFingerFar.ToString(),
				"\nDEPTH-2   skipped: latch ",
				b.dgSkipLatch.ToString(),
				"  far ",
				b.dgSkipFar.ToString(),
				" | inside ",
				this.InsideCount().ToString(),
				"/",
				this._pressers.Count.ToString(),
				" | rP ",
				(rP * 1000f).ToString("F1"),
				"  engage ",
				((rP + this.maxDepth) * 1000f).ToString("F1"),
				"  maxDepth ",
				(this.maxDepth * 1000f).ToString("F1"),
				"  knuckleCap ",
				(Mathf.Max(this.touchRadius, this.maxDepth * this.knuckleFrac) * 1000f).ToString("F1"),
				"  on ",
				b.sfOn.ToString(),
				"\n",
				this.JitterLine(b),
				"\n",
				this.MembLine(b),
				"\n",
				this.PitLine(b),
				"\n",
				this.MouthLine()
			});
		}

		// Token: 0x060000A3 RID: 163 RVA: 0x0001DF6C File Offset: 0x0001C16C
		public bool HasChar(ChaControl cha)
		{
			for (int i = 0; i < this._bodies.Count; i++)
			{
				if (this._bodies[i].cha == cha)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x060000A4 RID: 164 RVA: 0x0001DFAC File Offset: 0x0001C1AC
		public bool GetCharEnabled(ChaControl cha)
		{
			bool anyBody = false;
			bool on = false;
			for (int i = 0; i < this._bodies.Count; i++)
			{
				if (this._bodies[i].cha == cha && !this._bodies[i].isFace)
				{
					anyBody = true;
					on = this._bodies[i].sfOn;
					break;
				}
			}
			if (anyBody)
			{
				return on;
			}
			SoftFleshController.Body f = this.FaceOf(cha);
			return f != null && f.sfOn;
		}

		// Token: 0x060000A5 RID: 165 RVA: 0x0001E030 File Offset: 0x0001C230
		public void SetCharEnabled(ChaControl cha, bool on)
		{
			bool anyBody = false;
			for (int i = 0; i < this._bodies.Count; i++)
			{
				if (this._bodies[i].cha == cha && !this._bodies[i].isFace)
				{
					this._bodies[i].sfOn = on;
					anyBody = true;
				}
			}
			if (!anyBody)
			{
				SoftFleshController.Body f = this.FaceOf(cha);
				if (f != null)
				{
					f.sfOn = on;
				}
			}
		}

		// Token: 0x060000A6 RID: 166 RVA: 0x0001E0AC File Offset: 0x0001C2AC
		public bool GetFaceEnabled(ChaControl cha)
		{
			SoftFleshController.Body f = this.FaceOf(cha);
			return f != null && f.sfOn;
		}

		// Token: 0x060000A7 RID: 167 RVA: 0x0001E0CC File Offset: 0x0001C2CC
		public void SetFaceEnabled(ChaControl cha, bool on)
		{
			SoftFleshController.Body f = this.FaceOf(cha);
			if (f != null)
			{
				f.sfOn = on;
			}
		}

		// Token: 0x060000A8 RID: 168 RVA: 0x0001E0EC File Offset: 0x0001C2EC
		public ChaControl[] FaceChars()
		{
			List<ChaControl> r = new List<ChaControl>(this._bodies.Count);
			for (int i = 0; i < this._bodies.Count; i++)
			{
				if (this._bodies[i].isFace && this._bodies[i].cha != null)
				{
					r.Add(this._bodies[i].cha);
				}
			}
			return r.ToArray();
		}

		// Token: 0x060000A9 RID: 169 RVA: 0x0001E16C File Offset: 0x0001C36C
		public ChaControl[] SetupChars()
		{
			List<ChaControl> r = new List<ChaControl>(this._bodies.Count);
			for (int i = 0; i < this._bodies.Count; i++)
			{
				if (!this._bodies[i].isFace)
				{
					r.Add(this._bodies[i].cha);
				}
			}
			return r.ToArray();
		}

		// Token: 0x060000AA RID: 170 RVA: 0x0001E1D0 File Offset: 0x0001C3D0
		private void SolveBody(SoftFleshController.Body body, int bi, float dt, bool pressersMoved)
		{
			SoftFleshController.<>c__DisplayClass488_0 CS$<>8__locals1 = new SoftFleshController.<>c__DisplayClass488_0();
			CS$<>8__locals1.<>4__this = this;
			CS$<>8__locals1.body = body;
			CS$<>8__locals1.body.dgH = -1f;
			CS$<>8__locals1.body.dgProfRaw = 0f;
			CS$<>8__locals1.body.dgProfCap = 0f;
			CS$<>8__locals1.body.dgTgt = 0f;
			CS$<>8__locals1.body.dgProfP = -1;
			CS$<>8__locals1.body.dgTipD = 0f;
			CS$<>8__locals1.body.dgL = 0f;
			CS$<>8__locals1.body.dgSkipLatch = 0;
			CS$<>8__locals1.body.dgSkipFar = 0;
			CS$<>8__locals1.body.dgJumpN = 0;
			CS$<>8__locals1.body.dgOnOffN = 0;
			CS$<>8__locals1.body.dgProfProx = 0f;
			CS$<>8__locals1.body.dgProfDist = 0f;
			SkinnedMeshRenderer smr = CS$<>8__locals1.body.smr;
			if (smr == null)
			{
				return;
			}
			CS$<>8__locals1.body.sprCarry = ((CS$<>8__locals1.body.spreadL != null) ? (CS$<>8__locals1.body.spreadL.localPosition.magnitude * 1000f) : (-1f));
			CS$<>8__locals1.body.pivTurn = -1f;
			int pi = 0;
			int num;
			while (pi < CS$<>8__locals1.body.dynPartTr.Count && pi < CS$<>8__locals1.body.dynPartRest.Count)
			{
				Transform ptr = CS$<>8__locals1.body.dynPartTr[pi];
				if (!(ptr == null))
				{
					float dmm = (ptr.localPosition - CS$<>8__locals1.body.dynPartRest[pi]).magnitude * 1000f;
					if (dmm > CS$<>8__locals1.body.pivTurn)
					{
						CS$<>8__locals1.body.pivTurn = dmm;
					}
				}
				num = pi;
				pi = num + 1;
			}
			SoftFleshController.SyncSpread(CS$<>8__locals1.body);
			this.TrackItemDrive(CS$<>8__locals1.body, dt);
			this.SubdivSurfaceFix(CS$<>8__locals1.body);
			this.ClothTick(CS$<>8__locals1.body);
			this.UpdateSecondSkins(CS$<>8__locals1.body);
			bool anyInside = false;
			for (int b2 = 0; b2 < this._bodies.Count; b2 = num + 1)
			{
				if (this._bodies[b2].itemInside && this._bodies[b2].trkRadMm > 0.1f)
				{
					anyInside = true;
					break;
				}
				num = b2;
			}
			if (!anyInside || (CS$<>8__locals1.body.itemInside && CS$<>8__locals1.body.trkRadMm > 0.1f))
			{
				this.SquashItems(CS$<>8__locals1.body, dt);
			}
			float effW = ((this.openTestWidth > CS$<>8__locals1.body.itemW) ? this.openTestWidth : CS$<>8__locals1.body.itemW);
			if (!CS$<>8__locals1.body.itemInside && CS$<>8__locals1.body.gapeLevel > effW)
			{
				effW = CS$<>8__locals1.body.gapeLevel;
			}
			float effD = ((this.openTestDepth > CS$<>8__locals1.body.itemD) ? this.openTestDepth : CS$<>8__locals1.body.itemD);
			float fieldSig = effW * 1013f + effD * 617f + this.clitHold * 71f + this.openMaxPct * 3.1f + CS$<>8__locals1.body.suckOff * 9973f + CS$<>8__locals1.body.followB.sqrMagnitude * 1000000f + Quaternion.Angle(Quaternion.identity, CS$<>8__locals1.body.tiltB) * 11f + this.spreadShift * 7919f + this.itemStretch * 53f + this.bakeReach * 101f + CS$<>8__locals1.body.gapeLevel * 137f + this.bellyGain * 3457f + (this.bulgeWide ? 1571f : 0f) + this.itemRadScale * 761f + 0f + this.mouthScale * 613f + SoftFleshController.CrotchDeltaDeg * 419f + (this.crotchIsolate ? 227f : 0f) + (this.openNormals ? 601f : 0f) + (this.taperCanal ? 887f : 0f) + this.bellyWidth * 2711f + this.bellyLength * 4231f + this.bellyShape * 1279f + this.bellyStart * 3607f;
			bool fieldChanged = !CS$<>8__locals1.body.sigInit || Mathf.Abs(fieldSig - CS$<>8__locals1.body.lastSig) > 1E-05f;
			CS$<>8__locals1.body.lastSig = fieldSig;
			CS$<>8__locals1.body.sigInit = true;
			bool coolingDown = CS$<>8__locals1.body.openCool > 0 && effD <= 0.001f && effW <= 0.001f;
			bool openBusy = (CS$<>8__locals1.body.bakeReady || CS$<>8__locals1.body.bsAll.Count > 0 || CS$<>8__locals1.body.vagPivots.Count > 0) && (fieldChanged || CS$<>8__locals1.body.pushSettling || coolingDown || CS$<>8__locals1.body.bsDriving || Mathf.Abs(CS$<>8__locals1.body.suckOff) > 0.0001f);
			SoftFleshController.SkipMoved = pressersMoved;
			SoftFleshController.SkipBusy = openBusy;
			SoftFleshController.SkipVel = CS$<>8__locals1.body.maxVel;
			SoftFleshController.SkipVelLimit = 0.0005f;
			SoftFleshController.SkipFieldChanged = fieldChanged;
			SoftFleshController.SkipSettling = CS$<>8__locals1.body.pushSettling;
			SoftFleshController.SkipCool = (coolingDown ? CS$<>8__locals1.body.openCool : 0);
			SoftFleshController.SkipBsDrv = CS$<>8__locals1.body.bsDriving;
			SoftFleshController.SkipVagDrv = false;
			CS$<>8__locals1.body.dgSkipMoved = pressersMoved;
			CS$<>8__locals1.body.dgSkipBusy = openBusy;
			CS$<>8__locals1.body.dgSkipFree = CS$<>8__locals1.body.freeResidue;
			CS$<>8__locals1.body.dgSkipVel = CS$<>8__locals1.body.maxVel;
			SoftFleshController.Body body2;
			if (!pressersMoved && !openBusy && !CS$<>8__locals1.body.freeResidue && CS$<>8__locals1.body.maxVel < 0.0005f && (!CS$<>8__locals1.body.isFace || !CS$<>8__locals1.body.mouthDriven))
			{
				SoftFleshController.SkipTook = true;
				CS$<>8__locals1.body.dgSkipTook = true;
				body2 = CS$<>8__locals1.body;
				SoftFleshController.Body body3 = body2;
				num = body2.dgSkipFrames;
				body3.dgSkipFrames = num + 1;
				if (CS$<>8__locals1.body.nTiltLive.Count > 0 && CS$<>8__locals1.body.workMesh != null && CS$<>8__locals1.body.normList.Count == CS$<>8__locals1.body.baseNormals.Length)
				{
					int d0 = CS$<>8__locals1.body.dirty.Count;
					this.TemporalNormals(CS$<>8__locals1.body, false);
					if (CS$<>8__locals1.body.dirty.Count > d0)
					{
						CS$<>8__locals1.body.dirty.RemoveRange(d0, CS$<>8__locals1.body.dirty.Count - d0);
					}
					CS$<>8__locals1.body.workMesh.SetNormals(CS$<>8__locals1.body.normList);
					body2 = CS$<>8__locals1.body;
					SoftFleshController.Body body4 = body2;
					num = body2.outVersion;
					body4.outVersion = num + 1;
					this.UpdateSecondSkins(CS$<>8__locals1.body);
					body2 = CS$<>8__locals1.body;
					SoftFleshController.Body body5 = body2;
					num = body2.dgShadeSteps;
					body5.dgShadeSteps = num + 1;
				}
				if (this._diagBuild)
				{
					this._diagSb.Append('B').Append(bi).Append(CS$<>8__locals1.body.hasActive ? ":held  " : ((CS$<>8__locals1.body.nTiltLive.Count > 0) ? ":shade  " : ":idle  "));
				}
				return;
			}
			SoftFleshController.SkipTook = false;
			CS$<>8__locals1.body.dgSkipTook = false;
			CS$<>8__locals1.body.dgSkipFrames = 0;
			float rad = this.touchRadius;
			float pad = rad * 4f + this.maxDepth;
			Vector3 pmin;
			pmin..ctor(1E+09f, 1E+09f, 1E+09f);
			Vector3 pmax;
			pmax..ctor(-1E+09f, -1E+09f, -1E+09f);
			for (int p = 0; p < this._pressers.Count; p = num + 1)
			{
				if (this._segLen2[p] >= 0f)
				{
					Vector3 pp = this._segA[p];
					for (int e = 0; e < 2; e = num + 1)
					{
						if (pp.x < pmin.x)
						{
							pmin.x = pp.x;
						}
						if (pp.y < pmin.y)
						{
							pmin.y = pp.y;
						}
						if (pp.z < pmin.z)
						{
							pmin.z = pp.z;
						}
						if (pp.x > pmax.x)
						{
							pmax.x = pp.x;
						}
						if (pp.y > pmax.y)
						{
							pmax.y = pp.y;
						}
						if (pp.z > pmax.z)
						{
							pmax.z = pp.z;
						}
						pp = this._segA[p] + this._segAB[p];
						num = e;
					}
				}
				num = p;
			}
			ref float ptr3 = ref pmin.x;
			ptr3 -= pad;
			ptr3 = ref pmin.y;
			ptr3 -= pad;
			ptr3 = ref pmin.z;
			ptr3 -= pad;
			ptr3 = ref pmax.x;
			ptr3 += pad;
			ptr3 = ref pmax.y;
			ptr3 += pad;
			ptr3 = ref pmax.z;
			ptr3 += pad;
			List<int> hits = CS$<>8__locals1.body.hitBones;
			hits.Clear();
			if (CS$<>8__locals1.body.grpStart != null)
			{
				Transform[] bones = ((CS$<>8__locals1.body.bonesArr != null) ? CS$<>8__locals1.body.bonesArr : smr.bones);
				for (int b3 = 0; b3 < CS$<>8__locals1.body.grpMin.Length; b3 = num + 1)
				{
					if (CS$<>8__locals1.body.grpStart[b3] != CS$<>8__locals1.body.grpStart[b3 + 1])
					{
						Transform bt = bones[b3];
						if (!(bt == null))
						{
							Vector3 lo2 = CS$<>8__locals1.body.grpMin[b3];
							Vector3 hi2 = CS$<>8__locals1.body.grpMax[b3];
							Matrix4x4 bm = bt.localToWorldMatrix;
							Vector3 wlo;
							wlo..ctor(1E+09f, 1E+09f, 1E+09f);
							Vector3 whi;
							whi..ctor(-1E+09f, -1E+09f, -1E+09f);
							for (int c = 0; c < 8; c = num + 1)
							{
								Vector3 cw = bm.MultiplyPoint3x4(new Vector3(((c & 1) == 0) ? lo2.x : hi2.x, ((c & 2) == 0) ? lo2.y : hi2.y, ((c & 4) == 0) ? lo2.z : hi2.z));
								if (cw.x < wlo.x)
								{
									wlo.x = cw.x;
								}
								if (cw.x > whi.x)
								{
									whi.x = cw.x;
								}
								if (cw.y < wlo.y)
								{
									wlo.y = cw.y;
								}
								if (cw.y > whi.y)
								{
									whi.y = cw.y;
								}
								if (cw.z < wlo.z)
								{
									wlo.z = cw.z;
								}
								if (cw.z > whi.z)
								{
									whi.z = cw.z;
								}
								num = c;
							}
							if (whi.x + 0.03f >= pmin.x && wlo.x - 0.03f <= pmax.x && whi.y + 0.03f >= pmin.y && wlo.y - 0.03f <= pmax.y && whi.z + 0.03f >= pmin.z && wlo.z - 0.03f <= pmax.z)
							{
								hits.Add(b3);
							}
						}
					}
					num = b3;
				}
				if (hits.Count == 0 && !CS$<>8__locals1.body.hasActive && !openBusy)
				{
					if (this._diagBuild)
					{
						this._diagSb.Append('B').Append(bi).Append(":far  ");
					}
					CS$<>8__locals1.body.maxVel = 0f;
					return;
				}
			}
			this.didBake = true;
			body2 = CS$<>8__locals1.body;
			SoftFleshController.Body body6 = body2;
			num = body2.solveCount;
			body6.solveCount = num + 1;
			if (CS$<>8__locals1.body.hadActive)
			{
				CS$<>8__locals1.body.workMesh.SetNormals(CS$<>8__locals1.body.baseNormalsList);
			}
			long _tbake = SoftFleshController.SwNow();
			smr.BakeMesh(CS$<>8__locals1.body.bake);
			CS$<>8__locals1.body.bake.GetVertices(CS$<>8__locals1.body.bakeV);
			CS$<>8__locals1.body.bake.GetNormals(CS$<>8__locals1.body.bakeN);
			CS$<>8__locals1.body.bakeFrameNo = Time.frameCount;
			SoftFleshController.MeasureRimShape(CS$<>8__locals1.body);
			SoftFleshController.PhAdd(1, _tbake);
			CS$<>8__locals1.n = CS$<>8__locals1.body.baseVerts.Length;
			if (CS$<>8__locals1.body.bakeV.Count != CS$<>8__locals1.n)
			{
				return;
			}
			CS$<>8__locals1.l2w = SoftFleshController.BakeL2W(smr);
			CS$<>8__locals1.chaFwd = ((CS$<>8__locals1.body.cha != null) ? CS$<>8__locals1.body.cha.transform.forward : Vector3.forward);
			long _tcorr = SoftFleshController.SwNow();
			if (this.corrSmooth > 0.01f && CS$<>8__locals1.body.bakeV.Count == CS$<>8__locals1.body.baseVerts.Length && CS$<>8__locals1.body.domBone != null && CS$<>8__locals1.body.vagPivots.Count > 0)
			{
				int nC = CS$<>8__locals1.body.baseVerts.Length;
				if (CS$<>8__locals1.body.corrB == null || CS$<>8__locals1.body.corrB.Length != nC)
				{
					CS$<>8__locals1.body.corrB = new Vector3[nC];
					CS$<>8__locals1.body.corrBuf = new Vector3[nC];
					CS$<>8__locals1.body.corrPos = new Vector3[nC];
					CS$<>8__locals1.body.corrStamp = new int[nC];
					CS$<>8__locals1.body.corrClean = new Vector3[nC];
				}
				bool posed = false;
				float psig = 0f;
				for (int q = 0; q < CS$<>8__locals1.body.vagPivots.Count; q = num + 1)
				{
					Transform pv = CS$<>8__locals1.body.vagPivots[q];
					if (!(pv == null))
					{
						if (Quaternion.Angle(pv.localRotation, CS$<>8__locals1.body.vagPivotRot[q]) > 0.5f)
						{
							posed = true;
						}
						Quaternion lr = pv.localRotation;
						psig += lr.x * 13.7f + lr.y * 29.3f + lr.z * 61.1f + lr.w * 97.9f;
					}
					num = q;
				}
				if (posed && CS$<>8__locals1.body.corrB != null && CS$<>8__locals1.body.corrList.Count > 0 && Mathf.Abs(psig - CS$<>8__locals1.body.lastPetalSig) < 1E-06f)
				{
					posed = false;
				}
				CS$<>8__locals1.body.lastPetalSig = psig;
				if (posed)
				{
					Vector3[] keep = CS$<>8__locals1.body.corrPos;
					if (SoftFleshController._pivSnapBuf == null || SoftFleshController._pivSnapBuf.Length < CS$<>8__locals1.body.vagPivots.Count)
					{
						SoftFleshController._pivSnapBuf = new Quaternion[CS$<>8__locals1.body.vagPivots.Count];
					}
					Quaternion[] savedRot = SoftFleshController._pivSnapBuf;
					for (int q2 = 0; q2 < CS$<>8__locals1.body.vagPivots.Count; q2 = num + 1)
					{
						Transform pv2 = CS$<>8__locals1.body.vagPivots[q2];
						if (!(pv2 == null))
						{
							savedRot[q2] = pv2.localRotation;
							pv2.localRotation = CS$<>8__locals1.body.vagPivotRot[q2];
						}
						num = q2;
					}
					if (CS$<>8__locals1.body.refMesh == null)
					{
						CS$<>8__locals1.body.refMesh = new Mesh();
					}
					smr.BakeMesh(CS$<>8__locals1.body.refMesh);
					CS$<>8__locals1.body.refMesh.GetVertices(CS$<>8__locals1.body.refV);
					for (int q3 = 0; q3 < CS$<>8__locals1.body.vagPivots.Count; q3 = num + 1)
					{
						Transform pv3 = CS$<>8__locals1.body.vagPivots[q3];
						if (pv3 != null)
						{
							pv3.localRotation = savedRot[q3];
						}
						num = q3;
					}
					if (CS$<>8__locals1.body.refV.Count == nC)
					{
						List<int> cl = CS$<>8__locals1.body.corrList;
						cl.Clear();
						body2 = CS$<>8__locals1.body;
						SoftFleshController.Body body7 = body2;
						num = body2.corrCur + 1;
						body7.corrCur = num;
						int cst = num;
						for (int i = 0; i < nC; i = num + 1)
						{
							if ((CS$<>8__locals1.body.bakeV[i] - CS$<>8__locals1.body.refV[i]).sqrMagnitude >= 1E-08f && CS$<>8__locals1.body.corrStamp[i] != cst)
							{
								CS$<>8__locals1.body.corrStamp[i] = cst;
								cl.Add(i);
							}
							num = i;
						}
						int core = cl.Count;
						if (CS$<>8__locals1.body.corrW == null || CS$<>8__locals1.body.corrW.Length != nC)
						{
							CS$<>8__locals1.body.corrW = new float[nC];
						}
						for (int q4 = 0; q4 < core; q4 = num + 1)
						{
							CS$<>8__locals1.body.corrW[cl[q4]] = 1f;
							num = q4;
						}
						int prevEnd = 0;
						int curEnd = core;
						for (int ring = 1; ring <= 3; ring = num + 1)
						{
							float w = 1f - (float)ring / 4f;
							for (int q5 = prevEnd; q5 < curEnd; q5 = num + 1)
							{
								int j = cl[q5];
								for (int k = CS$<>8__locals1.body.adjStart[j]; k < CS$<>8__locals1.body.adjStart[j + 1]; k = num + 1)
								{
									int l = CS$<>8__locals1.body.adjList[k];
									if (CS$<>8__locals1.body.corrStamp[l] != cst)
									{
										CS$<>8__locals1.body.corrStamp[l] = cst;
										CS$<>8__locals1.body.corrW[l] = w;
										cl.Add(l);
									}
									num = k;
								}
								num = q5;
							}
							prevEnd = curEnd;
							curEnd = cl.Count;
							num = ring;
						}
						if (core > 0)
						{
							Vector3[] f0 = CS$<>8__locals1.body.corrPos;
							Vector3[] f = CS$<>8__locals1.body.corrBuf;
							for (int q6 = 0; q6 < cl.Count; q6 = num + 1)
							{
								int m = cl[q6];
								f0[m] = CS$<>8__locals1.body.bakeV[m] - CS$<>8__locals1.body.refV[m];
								num = q6;
							}
							for (int pass = 0; pass < 8; pass = num + 1)
							{
								float lam = (((pass & 1) == 0) ? 0.5f : (-0.53f));
								for (int q7 = 0; q7 < cl.Count; q7 = num + 1)
								{
									int n = cl[q7];
									Vector3 sum = Vector3.zero;
									int c2 = 0;
									for (int k2 = CS$<>8__locals1.body.adjStart[n]; k2 < CS$<>8__locals1.body.adjStart[n + 1]; k2 = num + 1)
									{
										sum += f0[CS$<>8__locals1.body.adjList[k2]];
										num = c2;
										c2 = num + 1;
										num = k2;
									}
									f[n] = ((c2 > 0) ? (f0[n] + (sum / (float)c2 - f0[n]) * lam) : f0[n]);
									num = q7;
								}
								for (int q8 = 0; q8 < cl.Count; q8 = num + 1)
								{
									int i2 = cl[q8];
									f0[i2] = f[i2];
									num = q8;
								}
								num = pass;
							}
							float follow2 = 1f - Mathf.Exp(-16f * dt);
							for (int q9 = 0; q9 < cl.Count; q9 = num + 1)
							{
								int i3 = cl[q9];
								Vector3 dLocal = f0[i3] - (CS$<>8__locals1.body.bakeV[i3] - CS$<>8__locals1.body.refV[i3]);
								Vector3 dWorld = CS$<>8__locals1.l2w.MultiplyVector(dLocal);
								int db2 = CS$<>8__locals1.body.domBone[i3];
								Vector3 tgtC = Vector3.zero;
								if (db2 >= 0 && db2 < CS$<>8__locals1.body.bonesArr.Length && CS$<>8__locals1.body.bonesArr[db2] != null)
								{
									tgtC = Quaternion.Inverse(CS$<>8__locals1.body.bonesArr[db2].rotation * CS$<>8__locals1.body.bindRot[db2]) * dWorld;
								}
								tgtC *= this.corrSmooth * CS$<>8__locals1.body.corrW[i3];
								float mg = tgtC.magnitude;
								if (mg > 0.01f)
								{
									tgtC *= 0.01f / mg;
								}
								ref Vector3 ptr4 = ref CS$<>8__locals1.body.corrB[i3];
								ptr4 += (tgtC - CS$<>8__locals1.body.corrB[i3]) * follow2;
								num = q9;
							}
						}
					}
				}
				else if (CS$<>8__locals1.body.corrList.Count > 0)
				{
					float dcy = 1f - Mathf.Exp(-12f * dt);
					List<int> cl2 = CS$<>8__locals1.body.corrList;
					bool anyLeft = false;
					for (int q10 = 0; q10 < cl2.Count; q10 = num + 1)
					{
						int i4 = cl2[q10];
						ref Vector3 ptr4 = ref CS$<>8__locals1.body.corrB[i4];
						ptr4 -= CS$<>8__locals1.body.corrB[i4] * dcy;
						if (CS$<>8__locals1.body.corrB[i4].sqrMagnitude < 1E-10f)
						{
							CS$<>8__locals1.body.corrB[i4] = Vector3.zero;
						}
						else
						{
							anyLeft = true;
						}
						num = q10;
					}
					if (!anyLeft)
					{
						cl2.Clear();
					}
				}
			}
			else if (this.corrSmooth <= 0.01f && CS$<>8__locals1.body.corrB != null && CS$<>8__locals1.body.corrList.Count > 0)
			{
				float dcy2 = 1f - Mathf.Exp(-12f * dt);
				List<int> cl3 = CS$<>8__locals1.body.corrList;
				bool anyLeft2 = false;
				for (int q11 = 0; q11 < cl3.Count; q11 = num + 1)
				{
					int i5 = cl3[q11];
					ref Vector3 ptr4 = ref CS$<>8__locals1.body.corrB[i5];
					ptr4 -= CS$<>8__locals1.body.corrB[i5] * dcy2;
					if (CS$<>8__locals1.body.corrB[i5].sqrMagnitude < 1E-10f)
					{
						CS$<>8__locals1.body.corrB[i5] = Vector3.zero;
					}
					else
					{
						anyLeft2 = true;
					}
					num = q11;
				}
				if (!anyLeft2)
				{
					cl3.Clear();
				}
			}
			SoftFleshController.PhAdd(2, _tcorr);
			long _tcontact = SoftFleshController.SwNow();
			CS$<>8__locals1.r = this.touchRadius;
			CS$<>8__locals1.eFloor = ((CS$<>8__locals1.body.avgEdge > 0.0001f) ? (CS$<>8__locals1.body.avgEdge * 0.7f) : 0.009f);
			if (CS$<>8__locals1.eFloor < 0.0001f)
			{
				CS$<>8__locals1.eFloor = 0.0001f;
			}
			float kc = 2f * this.waveSpeed / CS$<>8__locals1.eFloor;
			kc *= kc;
			CS$<>8__locals1.kcNum = 2f * this.waveSpeed;
			CS$<>8__locals1.kcNum *= CS$<>8__locals1.kcNum;
			CS$<>8__locals1.kBody = (CS$<>8__locals1.body.isFace ? (this.stiffness * this.faceFirm) : this.stiffness);
			CS$<>8__locals1.cBody = (CS$<>8__locals1.body.isFace ? (this.damping * this.faceDamp) : this.damping);
			float waveB = (CS$<>8__locals1.body.isFace ? (this.wave * this.faceWave) : this.wave);
			CS$<>8__locals1.slapB = (CS$<>8__locals1.body.isFace ? (this.slapGain * this.faceSlap) : this.slapGain);
			CS$<>8__locals1.mdB = (CS$<>8__locals1.body.isFace ? this.faceMaxDepth : this.maxDepth);
			if (CS$<>8__locals1.body.isFace)
			{
				kc *= this.faceWave;
				CS$<>8__locals1.kcNum *= this.faceWave;
			}
			float omega = Mathf.Sqrt(CS$<>8__locals1.kBody + kc);
			int steps = Mathf.Clamp(Mathf.CeilToInt(dt * omega / 0.6f), 1, 16);
			CS$<>8__locals1.body.lastSteps = steps;
			SoftFleshController.LoadSteps = steps;
			CS$<>8__locals1.sdt = dt / (float)steps;
			float kcMax = 0.6f * (float)steps / dt;
			kcMax = kcMax * kcMax - CS$<>8__locals1.kBody;
			if (kcMax < 0f)
			{
				kcMax = 0f;
			}
			if (kc > kcMax)
			{
				kc = kcMax;
				omega = Mathf.Sqrt(CS$<>8__locals1.kBody + kc);
			}
			CS$<>8__locals1.anchorK = CS$<>8__locals1.kBody * Mathf.Lerp(1f, 0.15f, waveB);
			CS$<>8__locals1.softK = CS$<>8__locals1.kBody * 0.06f;
			Matrix4x4 w2l = SoftFleshController.BakeW2L(smr);
			CS$<>8__locals1.lmin = new Vector3(1E+09f, 1E+09f, 1E+09f);
			CS$<>8__locals1.lmax = new Vector3(-1E+09f, -1E+09f, -1E+09f);
			for (int c3 = 0; c3 < 8; c3 = num + 1)
			{
				Vector3 cw2;
				cw2..ctor(((c3 & 1) == 0) ? pmin.x : pmax.x, ((c3 & 2) == 0) ? pmin.y : pmax.y, ((c3 & 4) == 0) ? pmin.z : pmax.z);
				Vector3 cl4 = w2l.MultiplyPoint3x4(cw2);
				if (cl4.x < CS$<>8__locals1.lmin.x)
				{
					CS$<>8__locals1.lmin.x = cl4.x;
				}
				if (cl4.x > CS$<>8__locals1.lmax.x)
				{
					CS$<>8__locals1.lmax.x = cl4.x;
				}
				if (cl4.y < CS$<>8__locals1.lmin.y)
				{
					CS$<>8__locals1.lmin.y = cl4.y;
				}
				if (cl4.y > CS$<>8__locals1.lmax.y)
				{
					CS$<>8__locals1.lmax.y = cl4.y;
				}
				if (cl4.z < CS$<>8__locals1.lmin.z)
				{
					CS$<>8__locals1.lmin.z = cl4.z;
				}
				if (cl4.z > CS$<>8__locals1.lmax.z)
				{
					CS$<>8__locals1.lmax.z = cl4.z;
				}
				num = c3;
			}
			int n2 = CS$<>8__locals1.body.baseVerts.Length;
			if (CS$<>8__locals1.body.dispPrev == null || CS$<>8__locals1.body.dispPrev.Length != n2)
			{
				CS$<>8__locals1.body.dispPrev = new float[n2];
			}
			Array.Copy(CS$<>8__locals1.body.disp, CS$<>8__locals1.body.dispPrev, n2);
			if (CS$<>8__locals1.body.velPrev == null || CS$<>8__locals1.body.velPrev.Length != n2)
			{
				CS$<>8__locals1.body.velPrev = new float[n2];
			}
			Array.Copy(CS$<>8__locals1.body.vel, CS$<>8__locals1.body.velPrev, n2);
			bool anyActive = false;
			float localMax = 0f;
			int localActive = 0;
			float localTgt = 0f;
			float localImp = 0f;
			float maxPush2 = 0f;
			Vector3 impC = Vector3.zero;
			List<int> swap = CS$<>8__locals1.body.prevSim;
			CS$<>8__locals1.body.prevSim = CS$<>8__locals1.body.simList;
			CS$<>8__locals1.body.simList = swap;
			CS$<>8__locals1.simList = CS$<>8__locals1.body.simList;
			CS$<>8__locals1.simList.Clear();
			this._springCount = int.MaxValue;
			CS$<>8__locals1.cand = CS$<>8__locals1.body.candList;
			CS$<>8__locals1.cand.Clear();
			if (CS$<>8__locals1.body.actStamp == null || CS$<>8__locals1.body.actStamp.Length != n2)
			{
				CS$<>8__locals1.body.actStamp = new int[n2];
			}
			if (CS$<>8__locals1.body.holdAge == null || CS$<>8__locals1.body.holdAge.Length != n2)
			{
				CS$<>8__locals1.body.holdAge = new byte[n2];
			}
			if (CS$<>8__locals1.body.nearPress == null || CS$<>8__locals1.body.nearPress.Length != n2)
			{
				CS$<>8__locals1.body.nearPress = new byte[n2];
			}
			body2 = CS$<>8__locals1.body;
			SoftFleshController.Body body8 = body2;
			num = body2.stampCur + 1;
			body8.stampCur = num;
			int cstamp = num;
			if (CS$<>8__locals1.body.isFace && CS$<>8__locals1.body.mouthPushValid && CS$<>8__locals1.body.mouthIdx != null)
			{
				for (int q12 = 0; q12 < CS$<>8__locals1.body.mouthIdx.Length; q12 = num + 1)
				{
					int i6 = CS$<>8__locals1.body.mouthIdx[q12];
					if (CS$<>8__locals1.body.actStamp[i6] != cstamp)
					{
						CS$<>8__locals1.body.actStamp[i6] = cstamp;
						CS$<>8__locals1.cand.Add(i6);
					}
					num = q12;
				}
			}
			if (CS$<>8__locals1.body.grpStart != null)
			{
				for (int hq = 0; hq < hits.Count; hq = num + 1)
				{
					int b4 = hits[hq];
					for (int k3 = CS$<>8__locals1.body.grpStart[b4]; k3 < CS$<>8__locals1.body.grpStart[b4 + 1]; k3 = num + 1)
					{
						int i7 = CS$<>8__locals1.body.grpVerts[k3];
						if (CS$<>8__locals1.body.actStamp[i7] != cstamp)
						{
							CS$<>8__locals1.body.actStamp[i7] = cstamp;
							CS$<>8__locals1.cand.Add(i7);
						}
						num = k3;
					}
					num = hq;
				}
			}
			else
			{
				for (int i8 = 0; i8 < n2; i8 = num + 1)
				{
					CS$<>8__locals1.body.actStamp[i8] = cstamp;
					CS$<>8__locals1.cand.Add(i8);
					num = i8;
				}
			}
			List<int> prev = CS$<>8__locals1.body.prevSim;
			float wD = CS$<>8__locals1.mdB * 0.008f;
			if (wD < 1E-05f)
			{
				wD = 1E-05f;
			}
			float wV = wD * 30f;
			for (int q13 = 0; q13 < prev.Count; q13 = num + 1)
			{
				int i9 = prev[q13];
				float dq = CS$<>8__locals1.body.disp[i9];
				float vq = CS$<>8__locals1.body.vel[i9];
				if (dq != 0f || vq != 0f || CS$<>8__locals1.body.pushB[i9].sqrMagnitude >= 1E-12f || (CS$<>8__locals1.body.corrB != null && CS$<>8__locals1.body.corrB[i9].sqrMagnitude >= 1E-12f))
				{
					if (dq < 0f)
					{
						dq = -dq;
					}
					if (vq < 0f)
					{
						vq = -vq;
					}
					if (CS$<>8__locals1.body.actStamp[i9] != cstamp)
					{
						CS$<>8__locals1.body.actStamp[i9] = cstamp;
						CS$<>8__locals1.cand.Add(i9);
					}
					CS$<>8__locals1.body.wakeStamp[i9] = CS$<>8__locals1.body.solveCount;
					if (dq >= wD || vq >= wV)
					{
						for (int k4 = CS$<>8__locals1.body.adjStart[i9]; k4 < CS$<>8__locals1.body.adjStart[i9 + 1]; k4 = num + 1)
						{
							int j2 = CS$<>8__locals1.body.adjList[k4];
							if (CS$<>8__locals1.body.actStamp[j2] != cstamp)
							{
								CS$<>8__locals1.body.actStamp[j2] = cstamp;
								CS$<>8__locals1.cand.Add(j2);
							}
							CS$<>8__locals1.body.wakeStamp[j2] = CS$<>8__locals1.body.solveCount;
							for (int k5 = CS$<>8__locals1.body.adjStart[j2]; k5 < CS$<>8__locals1.body.adjStart[j2 + 1]; k5 = num + 1)
							{
								int j3 = CS$<>8__locals1.body.adjList[k5];
								if (CS$<>8__locals1.body.actStamp[j3] != cstamp)
								{
									CS$<>8__locals1.body.actStamp[j3] = cstamp;
									CS$<>8__locals1.cand.Add(j3);
								}
								CS$<>8__locals1.body.wakeStamp[j3] = CS$<>8__locals1.body.solveCount;
								num = k5;
							}
							num = k4;
						}
					}
				}
				num = q13;
			}
			if (CS$<>8__locals1.body.corrList.Count > 0)
			{
				for (int q14 = 0; q14 < CS$<>8__locals1.body.corrList.Count; q14 = num + 1)
				{
					int i10 = CS$<>8__locals1.body.corrList[q14];
					if (CS$<>8__locals1.body.actStamp[i10] != cstamp)
					{
						CS$<>8__locals1.body.actStamp[i10] = cstamp;
						CS$<>8__locals1.cand.Add(i10);
					}
					num = q14;
				}
			}
			int nWk = SoftFleshController.MtWorkers;
			if (this._cSimBuf == null || this._cSimBuf.Length != nWk)
			{
				this._cSimBuf = new List<int>[nWk];
				for (int w2 = 0; w2 < nWk; w2 = num + 1)
				{
					this._cSimBuf[w2] = new List<int>(512);
					num = w2;
				}
				this._cTgtB = new float[nWk];
				this._cImpB = new float[nWk];
				this._cMinSqB = new float[nWk];
				this._dgHB = new float[nWk];
				this._dgProfRawB = new float[nWk];
				this._dgProfCapB = new float[nWk];
				this._dgProfPB = new int[nWk];
				this._dgTipDB = new float[nWk];
				this._dgLB = new float[nWk];
				this._dgSkipLatchB = new int[nWk];
				this._dgSkipFarB = new int[nWk];
				this._dgJumpB = new int[nWk];
				this._dgOnOffB = new int[nWk];
				this._dgProxB = new float[nWk];
				this._dgDistB = new float[nWk];
				this._dgApprB = new float[nWk];
				this._dgImpB = new float[nWk];
				this._cImpCB = new Vector3[nWk];
				this._cNameB = new string[nWk];
				SoftFleshController._cGripImpB = new float[nWk];
				SoftFleshController._cGripDTB = new float[nWk];
			}
			for (int w3 = 0; w3 < nWk; w3 = num + 1)
			{
				this._cSimBuf[w3].Clear();
				this._cTgtB[w3] = 0f;
				this._cImpB[w3] = 0f;
				this._cMinSqB[w3] = float.MaxValue;
				this._dgHB[w3] = -1f;
				this._dgProfRawB[w3] = 0f;
				this._dgProfCapB[w3] = 0f;
				this._dgSkipLatchB[w3] = 0;
				this._dgSkipFarB[w3] = 0;
				this._dgJumpB[w3] = 0;
				this._dgOnOffB[w3] = 0;
				this._dgProfPB[w3] = -1;
				this._dgTipDB[w3] = 0f;
				this._dgLB[w3] = 0f;
				this._dgProxB[w3] = 0f;
				this._dgDistB[w3] = 0f;
				this._dgApprB[w3] = 0f;
				this._dgImpB[w3] = 0f;
				this._cImpCB[w3] = Vector3.zero;
				this._cNameB[w3] = null;
				num = w3;
			}
			if (SoftFleshController._bgN == null || SoftFleshController._bgN.Length != nWk)
			{
				SoftFleshController._bgN = new int[nWk];
				SoftFleshController._bgAxLo = new float[nWk];
				SoftFleshController._bgAxHi = new float[nWk];
				SoftFleshController._bgLat = new float[nWk];
				SoftFleshController._bgPeak = new float[nWk];
			}
			for (int _w = 0; _w < nWk; _w = num + 1)
			{
				SoftFleshController._bgN[_w] = 0;
				SoftFleshController._bgAxLo[_w] = 1E+09f;
				SoftFleshController._bgAxHi[_w] = -1E+09f;
				SoftFleshController._bgLat[_w] = 0f;
				SoftFleshController._bgPeak[_w] = 0f;
				num = _w;
			}
			if (this.skinTension > 0f && (CS$<>8__locals1.body.membN == null || CS$<>8__locals1.body.membN.Length != CS$<>8__locals1.body.baseVerts.Length))
			{
				SoftFleshController.BuildMembNormals(CS$<>8__locals1.body);
			}
			if (SoftFleshController._segBoxMin == null || SoftFleshController._segBoxMin.Length < this._pressers.Count)
			{
				SoftFleshController._segBoxMin = new Vector3[this._pressers.Count + 8];
				SoftFleshController._segBoxMax = new Vector3[this._pressers.Count + 8];
				SoftFleshController._segName = new string[this._pressers.Count + 8];
			}
			for (int p2 = 0; p2 < this._pressers.Count; p2 = num + 1)
			{
				if (this._segLen2[p2] >= 0f)
				{
					SoftFleshController._segName[p2] = this._pressers[p2].BName;
					Vector3 sA = this._segA[p2];
					Vector3 sB = sA + this._segAB[p2];
					float g2 = Mathf.Sqrt(this._segGate2[p2]);
					SoftFleshController._segBoxMin[p2] = new Vector3(Mathf.Min(sA.x, sB.x) - g2, Mathf.Min(sA.y, sB.y) - g2, Mathf.Min(sA.z, sB.z) - g2);
					SoftFleshController._segBoxMax[p2] = new Vector3(Mathf.Max(sA.x, sB.x) + g2, Mathf.Max(sA.y, sB.y) + g2, Mathf.Max(sA.z, sB.z) + g2);
				}
				num = p2;
			}
			if (CS$<>8__locals1.body.waveShieldW == null || CS$<>8__locals1.body.waveShieldW.Length != CS$<>8__locals1.body.baseVerts.Length)
			{
				CS$<>8__locals1.body.waveShieldW = new float[CS$<>8__locals1.body.baseVerts.Length];
				for (int i11 = 0; i11 < CS$<>8__locals1.body.waveShieldW.Length; i11 = num + 1)
				{
					float w4 = ((CS$<>8__locals1.body.vagW != null) ? CS$<>8__locals1.body.vagW[i11] : 0f) + ((CS$<>8__locals1.body.kokW != null) ? CS$<>8__locals1.body.kokW[i11] : 0f) + ((CS$<>8__locals1.body.anaW != null) ? CS$<>8__locals1.body.anaW[i11] : 0f);
					float w5 = w4 * 2f;
					CS$<>8__locals1.body.waveShieldW[i11] = ((w5 > 1f) ? 1f : w5);
					num = i11;
				}
			}
			if (CS$<>8__locals1.body.boneRotCache == null || CS$<>8__locals1.body.boneRotCache.Length != CS$<>8__locals1.body.bonesArr.Length)
			{
				CS$<>8__locals1.body.boneRotCache = new Quaternion[CS$<>8__locals1.body.bonesArr.Length];
			}
			for (int b5 = 0; b5 < CS$<>8__locals1.body.bonesArr.Length; b5 = num + 1)
			{
				CS$<>8__locals1.body.boneRotCache[b5] = ((CS$<>8__locals1.body.bonesArr[b5] != null) ? (CS$<>8__locals1.body.bonesArr[b5].rotation * CS$<>8__locals1.body.bindRot[b5]) : Quaternion.identity);
				num = b5;
			}
			if ((this.skinTension > 0f || CS$<>8__locals1.body.isFace) && CS$<>8__locals1.body.bindPoses != null && CS$<>8__locals1.body.bindPoses.Length >= CS$<>8__locals1.body.bonesArr.Length)
			{
				if (CS$<>8__locals1.body.skinMCache == null || CS$<>8__locals1.body.skinMCache.Length != CS$<>8__locals1.body.bonesArr.Length)
				{
					CS$<>8__locals1.body.skinMCache = new Matrix4x4[CS$<>8__locals1.body.bonesArr.Length];
				}
				for (int b6 = 0; b6 < CS$<>8__locals1.body.bonesArr.Length; b6 = num + 1)
				{
					CS$<>8__locals1.body.skinMCache[b6] = ((CS$<>8__locals1.body.bonesArr[b6] != null) ? (CS$<>8__locals1.body.bonesArr[b6].localToWorldMatrix * CS$<>8__locals1.body.bindPoses[b6]) : Matrix4x4.identity);
					num = b6;
				}
			}
			int nPr = this._pressers.Count;
			if (this._inMinD2 == null || this._inMinD2.Length < nWk * nPr)
			{
				this._inMinD2 = new float[nWk * nPr];
				this._inMinH = new float[nWk * nPr];
				this._inMinN = new Vector3[nWk * nPr];
				this._pHmax = new float[nWk * nPr];
				this._pProfMax = new float[nWk * nPr];
				this._pLatch = new int[nWk * nPr];
				this._pFar = new int[nWk * nPr];
				this._pTipD2 = new float[nWk * nPr];
				this._pTipH = new float[nWk * nPr];
			}
			if (this._fcReach == null || this._fcReach.Length != nWk)
			{
				this._fcReach = new int[nWk];
				this._fcPushCore = new int[nWk];
				this._fcPushOuter = new int[nWk];
				this._fcSkipBin = new int[nWk];
				this._fcStepOuter = new float[nWk];
				this._fcPenOuterMin = new float[nWk];
			}
			for (int w6 = 0; w6 < nWk; w6 = num + 1)
			{
				this._fcReach[w6] = 0;
				this._fcPushCore[w6] = 0;
				this._fcPushOuter[w6] = 0;
				this._fcSkipBin[w6] = 0;
				this._fcStepOuter[w6] = 0f;
				this._fcPenOuterMin[w6] = 1E+09f;
				num = w6;
			}
			this._inStride = nPr;
			for (int q15 = 0; q15 < nWk * nPr; q15 = num + 1)
			{
				this._inMinD2[q15] = float.MaxValue;
				this._inMinH[q15] = 0f;
				this._pHmax[q15] = -1f;
				this._pProfMax[q15] = 0f;
				this._pLatch[q15] = 0;
				this._pFar[q15] = 0;
				this._pTipD2[q15] = float.MaxValue;
				this._pTipH[q15] = 0f;
				num = q15;
			}
			if (this._segInside == null || this._segInside.Length < nPr)
			{
				this._segInside = new bool[nPr];
			}
			if (this._segEntryN == null || this._segEntryN.Length < nPr)
			{
				this._segEntryN = new Vector3[nPr];
			}
			if (CS$<>8__locals1.body.segInside == null || CS$<>8__locals1.body.segInside.Length != nPr)
			{
				CS$<>8__locals1.body.segInside = new bool[nPr];
				CS$<>8__locals1.body.segEntryN = new Vector3[nPr];
			}
			SoftFleshController.ParallelForIdx(CS$<>8__locals1.cand.Count, delegate(int wk, int lo, int hi)
			{
				List<int> cSim = CS$<>8__locals1.<>4__this._cSimBuf[wk];
				float cTgt = 0f;
				float cImp = 0f;
				float cMinSq = float.MaxValue;
				float cGripImp = 0f;
				float cGripDT = 0f;
				float wH = -1f;
				float wProfRaw = 0f;
				float wProfCap = 0f;
				int wSkipLatch = 0;
				int wSkipFar = 0;
				int wJump = 0;
				int wOnOff = 0;
				int wProfP = -1;
				float wTipD = 0f;
				float wL = 0f;
				float wProx = 0f;
				float wDist = 0f;
				float wAppr = 0f;
				float wImp = 0f;
				Vector3 cImpC = Vector3.zero;
				string cName = null;
				for (int ci = lo; ci < hi; ci++)
				{
					int i21 = CS$<>8__locals1.cand[ci];
					if (CS$<>8__locals1.<>4__this.showBulge)
					{
						if (CS$<>8__locals1.body.bulgeDbg == null || CS$<>8__locals1.body.bulgeDbg.Length != CS$<>8__locals1.body.baseVerts.Length)
						{
							CS$<>8__locals1.body.bulgeDbg = new float[CS$<>8__locals1.body.baseVerts.Length];
						}
						CS$<>8__locals1.body.bulgeDbg[i21] = 0f;
					}
					if (CS$<>8__locals1.body.noDent[i21])
					{
						CS$<>8__locals1.body.disp[i21] = 0f;
						CS$<>8__locals1.body.vel[i21] = 0f;
						CS$<>8__locals1.body.tgtF[i21] = 0f;
					}
					else
					{
						Vector3 bvl = CS$<>8__locals1.body.bakeV[i21];
						if (CS$<>8__locals1.body.disp[i21] != 0f || CS$<>8__locals1.body.vel[i21] != 0f || CS$<>8__locals1.body.tgtF[i21] != 0f || CS$<>8__locals1.body.wakeStamp[i21] == CS$<>8__locals1.body.solveCount || (CS$<>8__locals1.body.isFace && CS$<>8__locals1.body.mouthPushValid && CS$<>8__locals1.body.mouthW != null && CS$<>8__locals1.body.mouthW[i21] > 0f) || (bvl.x >= CS$<>8__locals1.lmin.x && bvl.x <= CS$<>8__locals1.lmax.x && bvl.y >= CS$<>8__locals1.lmin.y && bvl.y <= CS$<>8__locals1.lmax.y && bvl.z >= CS$<>8__locals1.lmin.z && bvl.z <= CS$<>8__locals1.lmax.z))
						{
							Vector3 wp = CS$<>8__locals1.l2w.MultiplyPoint3x4(bvl);
							byte side = CS$<>8__locals1.body.armSide[i21];
							float dent4 = 0f;
							float bul4 = 0f;
							float minClear = 1E+09f;
							bool nearP = false;
							float dsCur = ((CS$<>8__locals1.body.renderDisp != null) ? CS$<>8__locals1.body.renderDisp[i21] : 0f);
							float dsAbs = ((dsCur < 0f) ? (-dsCur) : dsCur);
							float gateR = CS$<>8__locals1.<>4__this._segRadMax * 4f + CS$<>8__locals1.mdB + dsAbs;
							float gate2 = gateR * gateR;
							float slapImp = 0f;
							float sofV = 0f;
							if (CS$<>8__locals1.body.anyImp && side == 0)
							{
								for (int k13 = 0; k13 < 4; k13++)
								{
									float st2 = CS$<>8__locals1.body.impStr[k13];
									if (st2 > 0.02f)
									{
										float ix = wp.x - CS$<>8__locals1.body.impPos[k13].x;
										float iy = wp.y - CS$<>8__locals1.body.impPos[k13].y;
										float iz = wp.z - CS$<>8__locals1.body.impPos[k13].z;
										float u2 = (ix * ix + iy * iy + iz * iz) / 0.0144f;
										sofV += st2 / (1f + u2 * u2);
									}
								}
								sofV *= CS$<>8__locals1.<>4__this.slapSoften;
								if (sofV > 1f)
								{
									sofV = 1f;
								}
							}
							float wsh2 = ((CS$<>8__locals1.<>4__this.waveShield > 0.001f && CS$<>8__locals1.body.waveShieldW != null) ? (1f - CS$<>8__locals1.<>4__this.waveShield * CS$<>8__locals1.body.waveShieldW[i21]) : 1f);
							CS$<>8__locals1.body.soften[i21] = sofV * wsh2;
							CS$<>8__locals1.body.pushT[i21] = Vector3.zero;
							if (CS$<>8__locals1.body.sfOn && CS$<>8__locals1.body.isFace && CS$<>8__locals1.body.mouthPushValid && CS$<>8__locals1.body.mouthPushW != null && CS$<>8__locals1.body.domBone != null)
							{
								Vector3 mpw = CS$<>8__locals1.body.mouthPushW[i21];
								if ((mpw.x != 0f || mpw.y != 0f || mpw.z != 0f) && CS$<>8__locals1.body.skinMCache != null && CS$<>8__locals1.body.bwArr != null)
								{
									CS$<>8__locals1.body.pushT[i21] = CS$<>8__locals1.<>4__this.WorldToBindPush(CS$<>8__locals1.body, i21, mpw);
									if (CS$<>8__locals1.body.mouthIn != null && CS$<>8__locals1.body.mouthIn[i21])
									{
										CS$<>8__locals1.<>4__this._fcPushCore[wk]++;
									}
									else
									{
										CS$<>8__locals1.<>4__this._fcPushOuter[wk]++;
									}
									float mpl = mpw.magnitude;
									if (mpl > CS$<>8__locals1.<>4__this._fcStepOuter[wk])
									{
										CS$<>8__locals1.<>4__this._fcStepOuter[wk] = mpl;
									}
								}
							}
							bool haveN = false;
							Vector3 nW = Vector3.zero;
							Vector3 restW2 = Vector3.zero;
							bool noPress = CS$<>8__locals1.body.isFace && CS$<>8__locals1.body.mouthIn != null && CS$<>8__locals1.body.mouthIn[i21];
							int pressN = ((CS$<>8__locals1.body.sfOn && !noPress) ? CS$<>8__locals1.<>4__this._pressers.Count : 0);
							float hBest = -1000f;
							for (int p3 = 0; p3 < pressN; p3++)
							{
								if (CS$<>8__locals1.<>4__this._segLen2[p3] >= 0f && (!CS$<>8__locals1.body.isFace || !CS$<>8__locals1.<>4__this._segItem[p3] || !CS$<>8__locals1.<>4__this._segInsert[p3]) && (!CS$<>8__locals1.body.isFace || !(CS$<>8__locals1.body.mouthOwner != null) || CS$<>8__locals1.<>4__this._segOwner == null || p3 >= CS$<>8__locals1.<>4__this._segOwner.Length || !(CS$<>8__locals1.<>4__this._segOwner[p3] == CS$<>8__locals1.body.mouthOwner)))
								{
									if (!CS$<>8__locals1.<>4__this._segItem[p3])
									{
										bool ownHand = CS$<>8__locals1.<>4__this._segCha != null && CS$<>8__locals1.<>4__this._segCha[p3] == CS$<>8__locals1.body.cha;
										if (ownHand && side == 1 && !CS$<>8__locals1.<>4__this._segRight[p3])
										{
											goto IL_2959;
										}
										if (ownHand && side == 2 && CS$<>8__locals1.<>4__this._segRight[p3])
										{
											goto IL_2959;
										}
									}
									else if (side != 0)
									{
										goto IL_2959;
									}
									if (wp.x >= SoftFleshController._segBoxMin[p3].x && wp.x <= SoftFleshController._segBoxMax[p3].x && wp.y >= SoftFleshController._segBoxMin[p3].y && wp.y <= SoftFleshController._segBoxMax[p3].y && wp.z >= SoftFleshController._segBoxMin[p3].z && wp.z <= SoftFleshController._segBoxMax[p3].z)
									{
										Vector3 pa3 = CS$<>8__locals1.<>4__this._segA[p3];
										Vector3 ab = CS$<>8__locals1.<>4__this._segAB[p3];
										float wx = wp.x - pa3.x;
										float wy = wp.y - pa3.y;
										float wz = wp.z - pa3.z;
										float tSeg = ((CS$<>8__locals1.<>4__this._segLen2[p3] > 1E-12f) ? ((wx * ab.x + wy * ab.y + wz * ab.z) / CS$<>8__locals1.<>4__this._segLen2[p3]) : 0f);
										if (tSeg < 0f)
										{
											tSeg = 0f;
										}
										else if (tSeg > 1f)
										{
											tSeg = 1f;
										}
										float dx = wx - ab.x * tSeg;
										float dy = wy - ab.y * tSeg;
										float dz = wz - ab.z * tSeg;
										float sq = dx * dx + dy * dy + dz * dz;
										if (sq < cMinSq)
										{
											cMinSq = sq;
											cName = SoftFleshController._segName[p3];
										}
										if (sq < CS$<>8__locals1.<>4__this._segGate2[p3] && sq < gate2)
										{
											if (!haveN)
											{
												nW = CS$<>8__locals1.l2w.MultiplyVector((CS$<>8__locals1.body.bakeN.Count == CS$<>8__locals1.n) ? CS$<>8__locals1.body.bakeN[i21] : CS$<>8__locals1.body.baseNormals[i21]);
												float nm4 = nW.magnitude;
												if (nm4 > 1E-06f)
												{
													nW /= nm4;
												}
												Vector3 dRec = nW;
												if (CS$<>8__locals1.<>4__this.skinTension > 0f && CS$<>8__locals1.body.membN != null && CS$<>8__locals1.body.membN.Length == CS$<>8__locals1.n && CS$<>8__locals1.body.bwArr != null && CS$<>8__locals1.body.skinMCache != null)
												{
													BoneWeight bwR = CS$<>8__locals1.body.bwArr[i21];
													Vector3 mnR = CS$<>8__locals1.body.membN[i21];
													Vector3 accR = Vector3.zero;
													int nbR = CS$<>8__locals1.body.skinMCache.Length;
													if (bwR.weight0 > 0f && bwR.boneIndex0 < nbR)
													{
														accR += CS$<>8__locals1.body.skinMCache[bwR.boneIndex0].MultiplyVector(mnR) * bwR.weight0;
													}
													if (bwR.weight1 > 0f && bwR.boneIndex1 < nbR)
													{
														accR += CS$<>8__locals1.body.skinMCache[bwR.boneIndex1].MultiplyVector(mnR) * bwR.weight1;
													}
													if (bwR.weight2 > 0f && bwR.boneIndex2 < nbR)
													{
														accR += CS$<>8__locals1.body.skinMCache[bwR.boneIndex2].MultiplyVector(mnR) * bwR.weight2;
													}
													if (bwR.weight3 > 0f && bwR.boneIndex3 < nbR)
													{
														accR += CS$<>8__locals1.body.skinMCache[bwR.boneIndex3].MultiplyVector(mnR) * bwR.weight3;
													}
													dRec = accR;
													float dm3 = accR.magnitude;
													if (dm3 > 1E-06f)
													{
														nW = accR / dm3;
													}
												}
												restW2.x = wp.x + dRec.x * dsCur;
												restW2.y = wp.y + dRec.y * dsCur;
												restW2.z = wp.z + dRec.z * dsCur;
												if (CS$<>8__locals1.body.pushB[i21].sqrMagnitude > 1E-10f && CS$<>8__locals1.body.domBone != null)
												{
													int db3 = CS$<>8__locals1.body.domBone[i21];
													if (db3 >= 0 && db3 < CS$<>8__locals1.body.bonesArr.Length && CS$<>8__locals1.body.bonesArr[db3] != null)
													{
														Vector3 wpu = CS$<>8__locals1.body.boneRotCache[db3] * CS$<>8__locals1.body.pushB[i21];
														restW2.x -= wpu.x;
														restW2.y -= wpu.y;
														restW2.z -= wpu.z;
													}
												}
												haveN = true;
											}
											float rx = restW2.x - pa3.x;
											float ry = restW2.y - pa3.y;
											float rz = restW2.z - pa3.z;
											float tR = ((CS$<>8__locals1.<>4__this._segLen2[p3] > 1E-12f) ? ((rx * ab.x + ry * ab.y + rz * ab.z) / CS$<>8__locals1.<>4__this._segLen2[p3]) : 0f);
											if (tR < 0f)
											{
												tR = 0f;
											}
											else if (tR > 1f)
											{
												tR = 1f;
											}
											float rP = ((CS$<>8__locals1.<>4__this._segRad[p3] > 0f) ? (CS$<>8__locals1.<>4__this._segRad[p3] + (CS$<>8__locals1.<>4__this._segRadB[p3] - CS$<>8__locals1.<>4__this._segRad[p3]) * tR) : CS$<>8__locals1.r);
											if (CS$<>8__locals1.<>4__this._segProf[p3] != null)
											{
												int mb = p3 * 12;
												Vector3 lpt;
												lpt..ctor(CS$<>8__locals1.<>4__this._segM[mb] * restW2.x + CS$<>8__locals1.<>4__this._segM[mb + 1] * restW2.y + CS$<>8__locals1.<>4__this._segM[mb + 2] * restW2.z + CS$<>8__locals1.<>4__this._segM[mb + 3], CS$<>8__locals1.<>4__this._segM[mb + 4] * restW2.x + CS$<>8__locals1.<>4__this._segM[mb + 5] * restW2.y + CS$<>8__locals1.<>4__this._segM[mb + 6] * restW2.z + CS$<>8__locals1.<>4__this._segM[mb + 7], CS$<>8__locals1.<>4__this._segM[mb + 8] * restW2.x + CS$<>8__locals1.<>4__this._segM[mb + 9] * restW2.y + CS$<>8__locals1.<>4__this._segM[mb + 10] * restW2.z + CS$<>8__locals1.<>4__this._segM[mb + 11]);
												float rMeas = CS$<>8__locals1.<>4__this._segProf[p3].SampleRadius(lpt) * CS$<>8__locals1.<>4__this._segScale[p3] * CS$<>8__locals1.<>4__this.itemRadScale;
												if (rMeas > 1E-05f)
												{
													rP = rMeas;
												}
											}
											float bulW = 0.5f * rP;
											float bw2 = 0.5f * CS$<>8__locals1.<>4__this._segPen[p3];
											if (bw2 > bulW)
											{
												bulW = bw2;
											}
											float rr = rP + bulW;
											float slapR = rP * 4f;
											float engage = rP + CS$<>8__locals1.mdB;
											float vx = rx - ab.x * tR;
											float vy = ry - ab.y * tR;
											float vz = rz - ab.z * tR;
											float h = vx * nW.x + vy * nW.y + vz * nW.z;
											if (CS$<>8__locals1.<>4__this._segItem[p3] && CS$<>8__locals1.<>4__this._segInsert[p3])
											{
												float dRest = Mathf.Sqrt(vx * vx + vy * vy + vz * vz);
												if (dRest >= 1E-05f)
												{
													float dn = dRest / rP;
													bool bulgeLive = CS$<>8__locals1.<>4__this.bellyGain > 0f && CS$<>8__locals1.<>4__this.bulgeWide;
													float wWin;
													if (dn <= 2f)
													{
														wWin = 1f;
													}
													else if (dn < 3.5f)
													{
														float uW = 1f - (dn - 2f) / 1.5f;
														wWin = uW * uW * (3f - 2f * uW);
													}
													else
													{
														if (!bulgeLive || dRest >= rP + ((CS$<>8__locals1.<>4__this._segChainR[p3] > rP) ? CS$<>8__locals1.<>4__this._segChainR[p3] : rP) * 3f)
														{
															goto IL_2959;
														}
														wWin = 0f;
													}
													if (CS$<>8__locals1.body.isFace)
													{
														if (CS$<>8__locals1.body.mouthW != null && CS$<>8__locals1.body.mouthW[i21] > 0.001f)
														{
															CS$<>8__locals1.<>4__this._fcReach[wk]++;
															float dCur = Mathf.Sqrt(sq);
															bool mouthItem = CS$<>8__locals1.body.mouthOwner != null && CS$<>8__locals1.body.mouthBinR != null && CS$<>8__locals1.<>4__this._segOwner != null && p3 < CS$<>8__locals1.<>4__this._segOwner.Length && CS$<>8__locals1.<>4__this._segOwner[p3] == CS$<>8__locals1.body.mouthOwner;
															if (!mouthItem || !CS$<>8__locals1.body.mouthPushValid)
															{
																float stepF;
																Vector3 dirF;
																bool addToPushB;
																if (mouthItem)
																{
																	Vector3 axU = CS$<>8__locals1.body.mouthAxU;
																	float rmx = restW2.x - CS$<>8__locals1.body.mouthAxP.x;
																	float rmy = restW2.y - CS$<>8__locals1.body.mouthAxP.y;
																	float rmz = restW2.z - CS$<>8__locals1.body.mouthAxP.z;
																	float alongM = rmx * axU.x + rmy * axU.y + rmz * axU.z;
																	float rxR = rmx - axU.x * alongM;
																	float ryR = rmy - axU.y * alongM;
																	float rzR = rmz - axU.z * alongM;
																	float radR = Mathf.Sqrt(rxR * rxR + ryR * ryR + rzR * rzR);
																	if (radR < 1E-06f)
																	{
																		goto IL_2959;
																	}
																	Vector3 U = CS$<>8__locals1.body.mouthBinU;
																	Vector3 V = CS$<>8__locals1.body.mouthBinV;
																	float angT = Mathf.Atan2(rxR * V.x + ryR * V.y + rzR * V.z, rxR * U.x + ryR * U.y + rzR * U.z);
																	int nbT = CS$<>8__locals1.body.mouthBinR.Length;
																	float fbT = (angT + 3.1415927f) * ((float)nbT / 6.2831855f) - 0.5f;
																	int b0T = Mathf.FloorToInt(fbT);
																	float frT = fbT - (float)b0T;
																	int bA = (b0T % nbT + nbT) % nbT;
																	int bB = (bA + 1) % nbT;
																	float rA = CS$<>8__locals1.body.mouthBinR[bA];
																	float rB = CS$<>8__locals1.body.mouthBinR[bB];
																	float innerR = ((rA > 0f && rB > 0f) ? (rA + (rB - rA) * frT) : ((rA > 0f) ? rA : rB));
																	float rM = CS$<>8__locals1.body.mouthR;
																	float delta = ((innerR > 0f) ? (rM - innerR) : 0f);
																	float own = rM - radR;
																	float push = ((delta > own) ? delta : own);
																	CS$<>8__locals1.<>4__this._fcSkipBin[wk]++;
																	if (push < CS$<>8__locals1.<>4__this._fcPenOuterMin[wk])
																	{
																		CS$<>8__locals1.<>4__this._fcPenOuterMin[wk] = push;
																	}
																	if (push <= 1E-05f)
																	{
																		goto IL_2959;
																	}
																	stepF = push * 0f * CS$<>8__locals1.body.mouthW[i21];
																	dirF..ctor(rxR / radR, ryR / radR, rzR / radR);
																	addToPushB = false;
																}
																else
																{
																	float penC = rP - dCur;
																	if (penC <= 0f)
																	{
																		goto IL_2959;
																	}
																	stepF = penC * 0f * CS$<>8__locals1.body.mouthW[i21];
																	dirF = ((dCur > 1E-06f) ? new Vector3(dx / dCur, dy / dCur, dz / dCur) : nW);
																	addToPushB = true;
																}
																if (stepF > 1E-05f)
																{
																	int dbF = ((CS$<>8__locals1.body.domBone != null) ? CS$<>8__locals1.body.domBone[i21] : (-1));
																	if (dbF >= 0 && dbF < CS$<>8__locals1.body.bonesArr.Length && !(CS$<>8__locals1.body.bonesArr[dbF] == null))
																	{
																		Vector3 bPushF = Quaternion.Inverse(CS$<>8__locals1.body.boneRotCache[dbF]) * (dirF * stepF);
																		if (addToPushB)
																		{
																			bPushF += CS$<>8__locals1.body.pushB[i21];
																		}
																		if (CS$<>8__locals1.body.mouthCore != null && CS$<>8__locals1.body.mouthCore[i21])
																		{
																			CS$<>8__locals1.<>4__this._fcPushCore[wk]++;
																		}
																		else
																		{
																			CS$<>8__locals1.<>4__this._fcPushOuter[wk]++;
																		}
																		if (stepF > CS$<>8__locals1.<>4__this._fcStepOuter[wk])
																		{
																			CS$<>8__locals1.<>4__this._fcStepOuter[wk] = stepF;
																		}
																		if (bPushF.sqrMagnitude > CS$<>8__locals1.body.pushT[i21].sqrMagnitude)
																		{
																			CS$<>8__locals1.body.pushT[i21] = bPushF;
																		}
																		if (stepF > CS$<>8__locals1.<>4__this._segPenNext[p3])
																		{
																			CS$<>8__locals1.<>4__this._segPenNext[p3] = stepF;
																		}
																	}
																}
															}
														}
													}
													else
													{
														float mag = (Mathf.Sqrt(dRest * dRest + rP * rP) - dRest) * wWin * CS$<>8__locals1.<>4__this.itemStretch;
														if (CS$<>8__locals1.<>4__this.boneOpening && CS$<>8__locals1.body.vagW != null)
														{
															float bw3 = 1f - Mathf.Clamp01(CS$<>8__locals1.body.vagW[i21] * 1.5f);
															if (bw3 <= 0.001f)
															{
																goto IL_2959;
															}
															mag *= bw3;
														}
														float chR = ((CS$<>8__locals1.<>4__this._segChainR[p3] > rP) ? CS$<>8__locals1.<>4__this._segChainR[p3] : rP);
														if (mag > chR * 1.2f)
														{
															mag = chR * 1.2f;
														}
														bool radialOn = CS$<>8__locals1.<>4__this.itemStretch > 0.001f;
														if (radialOn || CS$<>8__locals1.<>4__this.bellyGain > 0f)
														{
															if (!radialOn)
															{
																mag = 0f;
															}
															float bwSeg2 = 0f;
															float axD = 0f;
															if (CS$<>8__locals1.body.axisFrameOK && CS$<>8__locals1.<>4__this.bellyLength > 0.0001f)
															{
																float sx = pa3.x + ab.x * tR - CS$<>8__locals1.body.axisEwW.x;
																float sy = pa3.y + ab.y * tR - CS$<>8__locals1.body.axisEwW.y;
																float sz = pa3.z + ab.z * tR - CS$<>8__locals1.body.axisEwW.z;
																axD = sx * CS$<>8__locals1.body.axisDwW.x + sy * CS$<>8__locals1.body.axisDwW.y + sz * CS$<>8__locals1.body.axisDwW.z;
																float wAx;
																if (axD > 0f)
																{
																	wAx = 1f;
																}
																else if (axD > -0.025f)
																{
																	float ua = 1f + axD / 0.025f;
																	wAx = ua * ua * (3f - 2f * ua);
																}
																else
																{
																	wAx = 0f;
																}
																if (wAx > 0.001f)
																{
																	float ins = CS$<>8__locals1.body.trkDepthMm * 0.001f;
																	if (ins < 0.005f)
																	{
																		ins = 0.005f;
																	}
																	float reach = CS$<>8__locals1.<>4__this.bellyLength * ins;
																	float uL = Mathf.Clamp01((reach - axD) / Mathf.Max(reach * 0.35f, 0.005f));
																	bwSeg2 = uL * uL * (3f - 2f * uL) * wAx;
																	if (CS$<>8__locals1.<>4__this.bellyStart > 0.001f)
																	{
																		float ramp = Mathf.Max(reach * CS$<>8__locals1.<>4__this.bellyStart, 0.002f);
																		float uN = Mathf.Clamp01(axD / ramp);
																		bwSeg2 *= uN * uN * (3f - 2f * uN);
																	}
																}
															}
															if (CS$<>8__locals1.<>4__this.bellyGain > 0f && bwSeg2 > 0f)
															{
																float fwB = (vx * nW.x + vy * nW.y + vz * nW.z) / dRest;
																if (CS$<>8__locals1.<>4__this.bulgeWide)
																{
																	float invL = ((CS$<>8__locals1.<>4__this._segLen2[p3] > 1E-12f) ? (1f / Mathf.Sqrt(CS$<>8__locals1.<>4__this._segLen2[p3])) : 0f);
																	float anx = ab.x * invL;
																	float any = ab.y * invL;
																	float anz = ab.z * invL;
																	float over = vx * anx + vy * any + vz * anz;
																	float px = vx - anx * over;
																	float py = vy - any * over;
																	float pz = vz - anz * over;
																	float dp = px * nW.x + py * nW.y + pz * nW.z;
																	float overSpan = rP * 1.5f + 0.004f;
																	float aOver = ((over < 0f) ? (-over) : over);
																	if (dp > 0f && aOver < overSpan)
																	{
																		float lat2 = px * px + py * py + pz * pz - dp * dp;
																		float lat3 = ((lat2 > 0f) ? Mathf.Sqrt(lat2) : 0f);
																		float uO = 1f - aOver / overSpan;
																		float wOver = uO * uO * (3f - 2f * uO);
																		float latSpan = rP * (1f + 3f * Mathf.Clamp(CS$<>8__locals1.<>4__this.bellyWidth, 0.05f, 1.5f));
																		float depthSpan = rP + chR * 3f;
																		if (lat3 < latSpan && dp < depthSpan)
																		{
																			float uL2 = 1f - lat3 / latSpan;
																			float uD = 1f - dp / depthSpan;
																			float wLat = uL2 * uL2 * (3f - 2f * uL2);
																			float wDep = uD * uD * (3f - 2f * uD);
																			float hR = Mathf.Lerp(chR, rP, Mathf.Clamp01(CS$<>8__locals1.<>4__this.bellyShape));
																			float bAdd = hR * CS$<>8__locals1.<>4__this.bellyGain * wOver * wLat * wDep * bwSeg2;
																			mag += bAdd;
																			if (CS$<>8__locals1.<>4__this.showBulge && CS$<>8__locals1.body.bulgeDbg != null && i21 < CS$<>8__locals1.body.bulgeDbg.Length)
																			{
																				CS$<>8__locals1.body.bulgeDbg[i21] += bAdd;
																			}
																			if (bAdd > 1E-05f && SoftFleshController._bgN != null && wk < SoftFleshController._bgN.Length)
																			{
																				SoftFleshController._bgN[wk]++;
																				if (axD < SoftFleshController._bgAxLo[wk])
																				{
																					SoftFleshController._bgAxLo[wk] = axD;
																				}
																				if (axD > SoftFleshController._bgAxHi[wk])
																				{
																					SoftFleshController._bgAxHi[wk] = axD;
																				}
																				if (lat3 > SoftFleshController._bgLat[wk])
																				{
																					SoftFleshController._bgLat[wk] = lat3;
																				}
																				if (bAdd > SoftFleshController._bgPeak[wk])
																				{
																					SoftFleshController._bgPeak[wk] = bAdd;
																				}
																			}
																			if (mag > chR * 1.4f)
																			{
																				mag = chR * 1.4f;
																			}
																		}
																	}
																}
																else
																{
																	float pokeD = rP - dRest;
																	if (pokeD > 0f)
																	{
																		float fN = nW.x * CS$<>8__locals1.chaFwd.x + nW.y * CS$<>8__locals1.chaFwd.y + nW.z * CS$<>8__locals1.chaFwd.z;
																		if (fN > 0f)
																		{
																			float b7 = pokeD * CS$<>8__locals1.<>4__this.bellyGain * fN * fN * bwSeg2;
																			if (b7 > chR)
																			{
																				b7 = chR;
																			}
																			float b8 = b7 * b7;
																			bul4 += b8 * b8;
																		}
																	}
																}
															}
															int db4 = ((CS$<>8__locals1.body.domBone != null) ? CS$<>8__locals1.body.domBone[i21] : (-1));
															if (db4 >= 0 && db4 < CS$<>8__locals1.body.bonesArr.Length && !(CS$<>8__locals1.body.bonesArr[db4] == null))
															{
																Vector3 wPush;
																wPush..ctor(vx / dRest * mag, vy / dRest * mag, vz / dRest * mag);
																Quaternion rw2 = CS$<>8__locals1.body.boneRotCache[db4];
																Vector3 bPush = Quaternion.Inverse(rw2) * wPush;
																if (bPush.sqrMagnitude > CS$<>8__locals1.body.pushT[i21].sqrMagnitude)
																{
																	CS$<>8__locals1.body.pushT[i21] = bPush;
																}
																if (mag > CS$<>8__locals1.<>4__this._segPenNext[p3])
																{
																	CS$<>8__locals1.<>4__this._segPenNext[p3] = mag;
																}
															}
														}
													}
												}
											}
											else
											{
												if (h > hBest)
												{
													hBest = h;
												}
												int qh = wk * CS$<>8__locals1.<>4__this._inStride + p3;
												if (h > CS$<>8__locals1.<>4__this._pHmax[qh])
												{
													CS$<>8__locals1.<>4__this._pHmax[qh] = h;
												}
												float tbx = restW2.x - (pa3.x + ab.x);
												float tby = restW2.y - (pa3.y + ab.y);
												float tbz = restW2.z - (pa3.z + ab.z);
												float td2 = tbx * tbx + tby * tby + tbz * tbz;
												if (td2 < CS$<>8__locals1.<>4__this._pTipD2[qh])
												{
													CS$<>8__locals1.<>4__this._pTipD2[qh] = td2;
													CS$<>8__locals1.<>4__this._pTipH[qh] = tbx * nW.x + tby * nW.y + tbz * nW.z;
												}
												float d2n = vx * vx + vy * vy + vz * vz;
												int qi3 = wk * CS$<>8__locals1.<>4__this._inStride + p3;
												if (d2n < CS$<>8__locals1.<>4__this._inMinD2[qi3])
												{
													CS$<>8__locals1.<>4__this._inMinD2[qi3] = d2n;
													CS$<>8__locals1.<>4__this._inMinH[qi3] = h;
													CS$<>8__locals1.<>4__this._inMinN[qi3] = nW;
												}
												float deepBand = CS$<>8__locals1.mdB * 0.5f;
												if (deepBand < rP * 2f)
												{
													deepBand = rP * 2f;
												}
												if (h >= engage + deepBand)
												{
													wSkipFar++;
												}
												if (h > -rP && h < engage + deepBand)
												{
													float wDeep = ((h > engage) ? (1f - (h - engage) / deepBand) : 1f);
													if (h > wH)
													{
														wH = h;
													}
													float l2v = vx * vx + vy * vy + vz * vz - h * h;
													float l2 = ((l2v > 0f) ? Mathf.Sqrt(l2v) : 0f);
													float rd0 = Mathf.Sqrt(vx * vx + vy * vy + vz * vz) - rP;
													if (rd0 < minClear)
													{
														minClear = rd0;
													}
													float nr2 = rP * 2.5f + CS$<>8__locals1.mdB;
													nr2 *= nr2;
													if (sq < nr2)
													{
														nearP = true;
													}
													if (CS$<>8__locals1.slapB > 0f && side == 0 && l2 < slapR)
													{
														float appr = -(CS$<>8__locals1.<>4__this._segVel[p3].x * nW.x + CS$<>8__locals1.<>4__this._segVel[p3].y * nW.y + CS$<>8__locals1.<>4__this._segVel[p3].z * nW.z);
														if (appr > 0.5f)
														{
															float fall = 1f - l2 / slapR;
															fall *= fall;
															float imp = CS$<>8__locals1.slapB * (appr - 0.5f) * fall;
															if (appr > wAppr)
															{
																wAppr = appr;
															}
															if (imp > wImp)
															{
																wImp = imp;
															}
															if (imp > slapImp)
															{
																slapImp = imp;
															}
														}
													}
													float marg = 0f;
													if (CS$<>8__locals1.body.vertEdge != null)
													{
														float ev = CS$<>8__locals1.body.vertEdge[i21];
														marg = ev * ev / (8f * rP);
														float mCap = rP * 0.35f;
														if (marg > mCap)
														{
															marg = mCap;
														}
													}
													float rPm = rP + marg;
													float eLat = ((CS$<>8__locals1.body.vertEdge != null) ? CS$<>8__locals1.body.vertEdge[i21] : CS$<>8__locals1.body.avgEdge);
													float rLat = 0.6f * eLat;
													if (rLat < rPm)
													{
														rLat = rPm;
													}
													float lP = l2 * (rPm / rLat);
													float lEdge = rLat - marg;
													float skW = 0.5f * rP;
													float skL = 0.6f * eLat;
													if (skL > skW)
													{
														skW = skL;
													}
													float bulWe = ((bulW > skW) ? bulW : skW);
													float exitD = -1f;
													float rc_nlen = Mathf.Sqrt(nW.x * nW.x + nW.y * nW.y + nW.z * nW.z);
													if (rc_nlen > 1E-08f)
													{
														float rc_dx = -nW.x / rc_nlen;
														float rc_dy = -nW.y / rc_nlen;
														float rc_dz = -nW.z / rc_nlen;
														float rc_r2 = rPm * rPm;
														for (int rc_e2 = 0; rc_e2 < 2; rc_e2++)
														{
															float rc_cx = pa3.x + ab.x * (float)rc_e2;
															float rc_cy = pa3.y + ab.y * (float)rc_e2;
															float rc_cz = pa3.z + ab.z * (float)rc_e2;
															float rc_ox = restW2.x - rc_cx;
															float rc_oy = restW2.y - rc_cy;
															float rc_oz = restW2.z - rc_cz;
															float rc_bb = rc_dx * rc_ox + rc_dy * rc_oy + rc_dz * rc_oz;
															float rc_cc = rc_ox * rc_ox + rc_oy * rc_oy + rc_oz * rc_oz - rc_r2;
															float rc_disc = rc_bb * rc_bb - rc_cc;
															if (rc_disc >= 0f)
															{
																float rc_te = -rc_bb + Mathf.Sqrt(rc_disc);
																if (rc_te > exitD)
																{
																	exitD = rc_te;
																}
															}
														}
														float rc_abl2 = CS$<>8__locals1.<>4__this._segLen2[p3];
														if (rc_abl2 > 1E-12f)
														{
															float rc_abl3 = Mathf.Sqrt(rc_abl2);
															float rc_ux = ab.x / rc_abl3;
															float rc_uy = ab.y / rc_abl3;
															float rc_uz = ab.z / rc_abl3;
															float rc_ox2 = restW2.x - pa3.x;
															float rc_oy2 = restW2.y - pa3.y;
															float rc_oz2 = restW2.z - pa3.z;
															float rc_du = rc_dx * rc_ux + rc_dy * rc_uy + rc_dz * rc_uz;
															float rc_ou = rc_ox2 * rc_ux + rc_oy2 * rc_uy + rc_oz2 * rc_uz;
															float rc_px = rc_dx - rc_du * rc_ux;
															float rc_py = rc_dy - rc_du * rc_uy;
															float rc_pz = rc_dz - rc_du * rc_uz;
															float rc_qx = rc_ox2 - rc_ou * rc_ux;
															float rc_qy = rc_oy2 - rc_ou * rc_uy;
															float rc_qz = rc_oz2 - rc_ou * rc_uz;
															float rc_aa = rc_px * rc_px + rc_py * rc_py + rc_pz * rc_pz;
															if (rc_aa > 1E-10f)
															{
																float rc_bb2 = rc_qx * rc_px + rc_qy * rc_py + rc_qz * rc_pz;
																float rc_cc2 = rc_qx * rc_qx + rc_qy * rc_qy + rc_qz * rc_qz - rc_r2;
																float rc_disc2 = rc_bb2 * rc_bb2 - rc_aa * rc_cc2;
																if (rc_disc2 >= 0f)
																{
																	float rc_te2 = (-rc_bb2 + Mathf.Sqrt(rc_disc2)) / rc_aa;
																	float sAx = rc_ou + rc_du * rc_te2;
																	if (sAx >= 0f && sAx <= rc_abl3 && rc_te2 > exitD)
																	{
																		exitD = rc_te2;
																	}
																}
															}
														}
													}
													if (lP < rPm || exitD > 0f)
													{
														if (h < rPm)
														{
															CS$<>8__locals1.body.reached[i21] = 1;
														}
														bool crossed = CS$<>8__locals1.body.hLast[i21] > -900f && CS$<>8__locals1.body.hLast[i21] <= -rP && h > 0f;
														if (crossed)
														{
															CS$<>8__locals1.body.reached[i21] = 1;
														}
														bool insideF = CS$<>8__locals1.body.segInside[p3] && !CS$<>8__locals1.<>4__this._segItem[p3];
														if (insideF)
														{
															if (h >= rPm)
															{
																float dE = Vector3.Dot(nW, CS$<>8__locals1.body.segEntryN[p3]);
																if (dE < 0.05f)
																{
																	wSkipFar++;
																	CS$<>8__locals1.<>4__this._pFar[wk * CS$<>8__locals1.<>4__this._inStride + p3]++;
																	goto IL_2959;
																}
																if (dE < 0.35f)
																{
																	wDeep *= (dE - 0.05f) / 0.3f;
																}
															}
														}
														else if (CS$<>8__locals1.body.engaged[i21] == 0 && CS$<>8__locals1.body.reached[i21] == 0 && h >= rPm)
														{
															wSkipLatch++;
															CS$<>8__locals1.<>4__this._pLatch[wk * CS$<>8__locals1.<>4__this._inStride + p3]++;
															goto IL_2959;
														}
														float capD = ((lP < rPm) ? (h + Mathf.Sqrt(rPm * rPm - lP * lP)) : (-1E+09f));
														float prof = ((exitD > capD) ? exitD : capD) - marg;
														prof -= 0.0015f;
														if (prof > 0f)
														{
															float kLim = CS$<>8__locals1.mdB * CS$<>8__locals1.<>4__this.knuckleFrac;
															if (kLim < rP)
															{
																kLim = rP;
															}
															float dcap = ((CS$<>8__locals1.<>4__this._segProx[p3] && kLim < CS$<>8__locals1.mdB) ? kLim : CS$<>8__locals1.mdB);
															if (prof > wProfRaw)
															{
																wProfRaw = prof;
																wProfP = p3;
																wL = l2;
																float dA = (restW2.x - CS$<>8__locals1.<>4__this._segA[p3].x) * nW.x + (restW2.y - CS$<>8__locals1.<>4__this._segA[p3].y) * nW.y + (restW2.z - CS$<>8__locals1.<>4__this._segA[p3].z) * nW.z;
																float dB2 = dA - (CS$<>8__locals1.<>4__this._segAB[p3].x * nW.x + CS$<>8__locals1.<>4__this._segAB[p3].y * nW.y + CS$<>8__locals1.<>4__this._segAB[p3].z * nW.z);
																wTipD = ((dA > dB2) ? dA : dB2);
															}
															int qp = wk * CS$<>8__locals1.<>4__this._inStride + p3;
															if (prof > CS$<>8__locals1.<>4__this._pProfMax[qp])
															{
																CS$<>8__locals1.<>4__this._pProfMax[qp] = prof;
															}
															if (prof > dcap)
															{
																prof = dcap;
															}
															prof *= wDeep;
															if (prof > wProfCap)
															{
																wProfCap = prof;
															}
															if (CS$<>8__locals1.<>4__this._segProx[p3])
															{
																if (prof > wProx)
																{
																	wProx = prof;
																}
															}
															else if (prof > wDist)
															{
																wDist = prof;
															}
															float p4 = prof * prof;
															dent4 += p4 * p4;
															if (prof > CS$<>8__locals1.<>4__this._segPenNext[p3])
															{
																CS$<>8__locals1.<>4__this._segPenNext[p3] = prof;
															}
														}
													}
													else if (l2 < lEdge + bulWe)
													{
														if (h > 0f && CS$<>8__locals1.<>4__this._segPen[p3] > 0f)
														{
															float t6 = 1f - (l2 - lEdge) / skW;
															if (t6 < 0f)
															{
																t6 = 0f;
															}
															float scap = ((CS$<>8__locals1.<>4__this._segProx[p3] && rP < CS$<>8__locals1.mdB) ? rP : CS$<>8__locals1.mdB);
															float hs = ((h < scap) ? h : scap);
															float skirt = hs * t6 * t6;
															skirt *= wDeep;
															float p5 = skirt * skirt;
															dent4 += p5 * p5;
														}
														float u3 = (l2 - lEdge) / bulWe;
														if (u3 < 0f)
														{
															u3 = 0f;
														}
														else if (u3 > 1f)
														{
															u3 = 1f;
														}
														float s6 = Mathf.Sin(u3 * 3.1415927f);
														float b9 = CS$<>8__locals1.<>4__this._segPen[p3] * CS$<>8__locals1.<>4__this.bulge * s6 * s6;
														float b10 = b9 * b9;
														bul4 += b10 * b10;
													}
												}
											}
										}
									}
								}
								IL_2959:;
							}
							float target = 0f;
							if (dent4 > 0f)
							{
								target = Mathf.Pow(dent4, 0.25f);
							}
							if (target > CS$<>8__locals1.mdB)
							{
								target = CS$<>8__locals1.mdB;
							}
							if (bul4 > 0f)
							{
								float bulV = Mathf.Pow(bul4, 0.25f);
								float bCap = minClear - 0.0005f;
								if (bCap < 0f)
								{
									bCap = 0f;
								}
								if (bulV > bCap)
								{
									bulV = bCap;
								}
								target -= bulV;
							}
							float fw2 = CS$<>8__locals1.body.nipW[i21];
							if (fw2 > 0f && target != 0f)
							{
								target *= 1f - CS$<>8__locals1.<>4__this.nipFirm * fw2;
							}
							float tPrev = CS$<>8__locals1.body.tgtF[i21];
							if (tPrev != 0f && target != 0f)
							{
								float dT2 = target - tPrev;
								float aT = ((dT2 < 0f) ? (-dT2) : dT2);
								float slew = aT / 0.002f;
								slew *= slew;
								if (slew < 0.1f)
								{
									slew = 0.1f;
								}
								if (slew > 1f)
								{
									slew = 1f;
								}
								target = tPrev + dT2 * slew;
							}
							if (!CS$<>8__locals1.body.sfOn)
							{
								target = 0f;
							}
							if (target > cTgt)
							{
								cTgt = target;
							}
							float dJ = target - tPrev;
							if (dJ < 0f)
							{
								dJ = -dJ;
							}
							if (dJ > 0.008f)
							{
								wJump++;
								if (tPrev == 0f || target == 0f)
								{
									wOnOff++;
								}
							}
							CS$<>8__locals1.body.tgtF[i21] = target;
							CS$<>8__locals1.body.engaged[i21] = ((target > 0f) ? 1 : 0);
							CS$<>8__locals1.body.nearPress[i21] = ((nearP > false) ? 1 : 0);
							if (!nearP)
							{
								CS$<>8__locals1.body.reached[i21] = 0;
							}
							CS$<>8__locals1.body.hLast[i21] = hBest;
							CS$<>8__locals1.body.holdAge[i21] = ((target != 0f) ? ((CS$<>8__locals1.body.holdAge[i21] < byte.MaxValue) ? (CS$<>8__locals1.body.holdAge[i21] + 1) : byte.MaxValue) : 0);
							slapImp *= wsh2;
							if (slapImp > 0f && CS$<>8__locals1.body.holdAge[i21] > 30)
							{
								float tHold = ((tPrev < 0f) ? (-tPrev) : tPrev);
								float hgi = tHold / (CS$<>8__locals1.mdB * 0.35f);
								if (hgi > 1f)
								{
									hgi = 1f;
								}
								slapImp *= 1f - 0.85f * hgi;
							}
							if (slapImp > cImp)
							{
								cImp = slapImp;
								cImpC = wp;
							}
							if (slapImp > 0f)
							{
								CS$<>8__locals1.body.vel[i21] += slapImp;
								if (CS$<>8__locals1.body.holdAge[i21] > 30 && slapImp > cGripImp)
								{
									cGripImp = slapImp;
								}
							}
							if (CS$<>8__locals1.body.holdAge[i21] > 30)
							{
								float dTg = CS$<>8__locals1.body.tgtF[i21] - tPrev;
								if (dTg < 0f)
								{
									dTg = -dTg;
								}
								if (dTg > cGripDT)
								{
									cGripDT = dTg;
								}
							}
							if (target != 0f || slapImp > 0f || CS$<>8__locals1.body.disp[i21] != 0f || CS$<>8__locals1.body.vel[i21] != 0f || CS$<>8__locals1.body.wakeStamp[i21] == CS$<>8__locals1.body.solveCount || CS$<>8__locals1.body.pushT[i21].x != 0f || CS$<>8__locals1.body.pushT[i21].y != 0f || CS$<>8__locals1.body.pushT[i21].z != 0f || CS$<>8__locals1.body.pushB[i21].sqrMagnitude > 1E-10f || (CS$<>8__locals1.body.corrB != null && CS$<>8__locals1.body.corrB[i21].sqrMagnitude > 1E-10f))
							{
								cSim.Add(i21);
							}
						}
					}
				}
				CS$<>8__locals1.<>4__this._cSimBuf[wk] = cSim;
				CS$<>8__locals1.<>4__this._cTgtB[wk] = cTgt;
				CS$<>8__locals1.<>4__this._cImpB[wk] = cImp;
				CS$<>8__locals1.<>4__this._dgHB[wk] = wH;
				CS$<>8__locals1.<>4__this._dgProfRawB[wk] = wProfRaw;
				CS$<>8__locals1.<>4__this._dgProfCapB[wk] = wProfCap;
				CS$<>8__locals1.<>4__this._dgProfPB[wk] = wProfP;
				CS$<>8__locals1.<>4__this._dgTipDB[wk] = wTipD;
				CS$<>8__locals1.<>4__this._dgLB[wk] = wL;
				CS$<>8__locals1.<>4__this._dgSkipLatchB[wk] = wSkipLatch;
				CS$<>8__locals1.<>4__this._dgSkipFarB[wk] = wSkipFar;
				CS$<>8__locals1.<>4__this._dgJumpB[wk] = wJump;
				CS$<>8__locals1.<>4__this._dgOnOffB[wk] = wOnOff;
				CS$<>8__locals1.<>4__this._dgProxB[wk] = wProx;
				CS$<>8__locals1.<>4__this._dgDistB[wk] = wDist;
				CS$<>8__locals1.<>4__this._dgApprB[wk] = wAppr;
				CS$<>8__locals1.<>4__this._dgImpB[wk] = wImp;
				CS$<>8__locals1.<>4__this._cMinSqB[wk] = cMinSq;
				CS$<>8__locals1.<>4__this._cImpCB[wk] = cImpC;
				CS$<>8__locals1.<>4__this._cNameB[wk] = cName;
				SoftFleshController._cGripImpB[wk] = cGripImp;
				SoftFleshController._cGripDTB[wk] = cGripDT;
			});
			float gI = 0f;
			float gD = 0f;
			int nIn = 0;
			for (int pp2 = 0; pp2 < nPr; pp2 = num + 1)
			{
				float best = float.MaxValue;
				float bh = 0f;
				Vector3 bn = Vector3.zero;
				for (int w7 = 0; w7 < nWk; w7 = num + 1)
				{
					int qi = w7 * this._inStride + pp2;
					if (this._inMinD2[qi] < best)
					{
						best = this._inMinD2[qi];
						bh = this._inMinH[qi];
						bn = this._inMinN[qi];
					}
					num = w7;
				}
				float bandR = CS$<>8__locals1.mdB * 0.5f;
				if (bandR < this.touchRadius * 2f)
				{
					bandR = this.touchRadius * 2f;
				}
				float reachR = this.touchRadius + CS$<>8__locals1.mdB + bandR;
				if (CS$<>8__locals1.body.segInside[pp2])
				{
					reachR *= 1.15f;
				}
				bool insideNow = best < reachR * reachR && bh > 0f;
				if (!insideNow || !CS$<>8__locals1.body.segInside[pp2])
				{
					CS$<>8__locals1.body.segEntryN[pp2] = bn;
				}
				CS$<>8__locals1.body.segInside[pp2] = insideNow;
				if (insideNow)
				{
					num = nIn;
					nIn = num + 1;
				}
				num = pp2;
			}
			CS$<>8__locals1.body.dgInsideN = nIn;
			if (CS$<>8__locals1.body.isFace)
			{
				CS$<>8__locals1.body.fcReach = 0;
				CS$<>8__locals1.body.fcPushCore = 0;
				CS$<>8__locals1.body.fcPushOuter = 0;
				CS$<>8__locals1.body.fcSkipBin = 0;
				CS$<>8__locals1.body.fcStepOuter = 0f;
				CS$<>8__locals1.body.fcPenOuterMin = 1E+09f;
				for (int w8 = 0; w8 < nWk; w8 = num + 1)
				{
					body2 = CS$<>8__locals1.body;
					body2.fcReach += this._fcReach[w8];
					body2 = CS$<>8__locals1.body;
					body2.fcPushCore += this._fcPushCore[w8];
					body2 = CS$<>8__locals1.body;
					body2.fcPushOuter += this._fcPushOuter[w8];
					body2 = CS$<>8__locals1.body;
					body2.fcSkipBin += this._fcSkipBin[w8];
					if (this._fcStepOuter[w8] > CS$<>8__locals1.body.fcStepOuter)
					{
						CS$<>8__locals1.body.fcStepOuter = this._fcStepOuter[w8];
					}
					if (this._fcPenOuterMin[w8] < CS$<>8__locals1.body.fcPenOuterMin)
					{
						CS$<>8__locals1.body.fcPenOuterMin = this._fcPenOuterMin[w8];
					}
					num = w8;
				}
			}
			int bestP = -1;
			float bestH = -1f;
			for (int pp3 = 0; pp3 < nPr; pp3 = num + 1)
			{
				if (pp3 >= this._pressers.Count || !this._pressers[pp3].item)
				{
					float hm = -1f;
					for (int w9 = 0; w9 < nWk; w9 = num + 1)
					{
						float v = this._pHmax[w9 * this._inStride + pp3];
						if (v > hm)
						{
							hm = v;
						}
						num = w9;
					}
					if (hm > bestH)
					{
						bestH = hm;
						bestP = pp3;
					}
				}
				num = pp3;
			}
			CS$<>8__locals1.body.dgFingerP = bestP;
			CS$<>8__locals1.body.dgFingerH = bestH;
			CS$<>8__locals1.body.dgFingerProf = 0f;
			CS$<>8__locals1.body.dgFingerLatch = 0;
			CS$<>8__locals1.body.dgFingerFar = 0;
			CS$<>8__locals1.body.dgFingerInside = false;
			CS$<>8__locals1.body.dgFingerTip = 0f;
			if (bestP >= 0)
			{
				float tbest = float.MaxValue;
				for (int w10 = 0; w10 < nWk; w10 = num + 1)
				{
					int qi2 = w10 * this._inStride + bestP;
					if (this._pProfMax[qi2] > CS$<>8__locals1.body.dgFingerProf)
					{
						CS$<>8__locals1.body.dgFingerProf = this._pProfMax[qi2];
					}
					body2 = CS$<>8__locals1.body;
					body2.dgFingerLatch += this._pLatch[qi2];
					body2 = CS$<>8__locals1.body;
					body2.dgFingerFar += this._pFar[qi2];
					if (this._pTipD2[qi2] < tbest)
					{
						tbest = this._pTipD2[qi2];
						CS$<>8__locals1.body.dgFingerTip = this._pTipH[qi2];
					}
					num = w10;
				}
				CS$<>8__locals1.body.dgFingerInside = CS$<>8__locals1.body.segInside[bestP];
			}
			for (int w11 = 0; w11 < nWk; w11 = num + 1)
			{
				List<int> cs = this._cSimBuf[w11];
				for (int q16 = 0; q16 < cs.Count; q16 = num + 1)
				{
					CS$<>8__locals1.simList.Add(cs[q16]);
					num = q16;
				}
				if (this._cTgtB[w11] > localTgt)
				{
					localTgt = this._cTgtB[w11];
				}
				CS$<>8__locals1.body.dgTgt = localTgt;
				if (this._dgHB[w11] > CS$<>8__locals1.body.dgH)
				{
					CS$<>8__locals1.body.dgH = this._dgHB[w11];
				}
				if (this._dgProfRawB[w11] > CS$<>8__locals1.body.dgProfRaw)
				{
					CS$<>8__locals1.body.dgProfRaw = this._dgProfRawB[w11];
					CS$<>8__locals1.body.dgProfP = this._dgProfPB[w11];
					CS$<>8__locals1.body.dgTipD = this._dgTipDB[w11];
					CS$<>8__locals1.body.dgL = this._dgLB[w11];
				}
				if (this._dgProfCapB[w11] > CS$<>8__locals1.body.dgProfCap)
				{
					CS$<>8__locals1.body.dgProfCap = this._dgProfCapB[w11];
				}
				body2 = CS$<>8__locals1.body;
				body2.dgSkipLatch += this._dgSkipLatchB[w11];
				body2 = CS$<>8__locals1.body;
				body2.dgSkipFar += this._dgSkipFarB[w11];
				body2 = CS$<>8__locals1.body;
				body2.dgJumpN += this._dgJumpB[w11];
				body2 = CS$<>8__locals1.body;
				body2.dgOnOffN += this._dgOnOffB[w11];
				if (this._dgProxB[w11] > CS$<>8__locals1.body.dgProfProx)
				{
					CS$<>8__locals1.body.dgProfProx = this._dgProxB[w11];
				}
				if (this._dgDistB[w11] > CS$<>8__locals1.body.dgProfDist)
				{
					CS$<>8__locals1.body.dgProfDist = this._dgDistB[w11];
				}
				if (this._dgApprB[w11] > CS$<>8__locals1.body.dgApprEver)
				{
					CS$<>8__locals1.body.dgApprEver = this._dgApprB[w11];
				}
				if (this._dgImpB[w11] > CS$<>8__locals1.body.dgImpEver)
				{
					CS$<>8__locals1.body.dgImpEver = this._dgImpB[w11];
				}
				if (this._cImpB[w11] > localImp)
				{
					localImp = this._cImpB[w11];
					impC = this._cImpCB[w11];
				}
				if (this._cMinSqB[w11] < this._minSq)
				{
					this._minSq = this._cMinSqB[w11];
					this._nearName = this._cNameB[w11] ?? this._nearName;
				}
				if (SoftFleshController._cGripImpB[w11] > gI)
				{
					gI = SoftFleshController._cGripImpB[w11];
				}
				if (SoftFleshController._cGripDTB[w11] > gD)
				{
					gD = SoftFleshController._cGripDTB[w11];
				}
				num = w11;
			}
			SoftFleshController.GripImpPeak = ((gI > SoftFleshController.GripImpPeak) ? gI : (SoftFleshController.GripImpPeak * 0.98f));
			SoftFleshController.GripDTgtPeak = ((gD > SoftFleshController.GripDTgtPeak) ? gD : (SoftFleshController.GripDTgtPeak * 0.98f));
			if (SoftFleshController._gripProbe >= 0 && (SoftFleshController._gripProbe >= CS$<>8__locals1.body.holdAge.Length || CS$<>8__locals1.body.holdAge[SoftFleshController._gripProbe] < 30))
			{
				SoftFleshController._gripProbe = -1;
			}
			if (SoftFleshController._gripProbe < 0)
			{
				float bestTg = 0f;
				for (int q17 = 0; q17 < CS$<>8__locals1.simList.Count; q17 = num + 1)
				{
					int i12 = CS$<>8__locals1.simList[q17];
					float t2 = CS$<>8__locals1.body.tgtF[i12];
					if (t2 < 0f)
					{
						t2 = -t2;
					}
					if (CS$<>8__locals1.body.holdAge[i12] > 30 && t2 > bestTg)
					{
						bestTg = t2;
						SoftFleshController._gripProbe = i12;
					}
					num = q17;
				}
				SoftFleshController._gripProbeFresh = true;
			}
			if (SoftFleshController._gripProbe >= 0)
			{
				Vector3 wpP = CS$<>8__locals1.l2w.MultiplyPoint3x4(CS$<>8__locals1.body.bakeV[SoftFleshController._gripProbe]);
				Vector3 nP = CS$<>8__locals1.l2w.MultiplyVector((CS$<>8__locals1.body.bakeN.Count == CS$<>8__locals1.n) ? CS$<>8__locals1.body.bakeN[SoftFleshController._gripProbe] : CS$<>8__locals1.body.baseNormals[SoftFleshController._gripProbe]);
				float nm2 = nP.magnitude;
				if (nm2 > 1E-06f)
				{
					nP /= nm2;
				}
				float dsP = ((CS$<>8__locals1.body.renderDisp != null) ? CS$<>8__locals1.body.renderDisp[SoftFleshController._gripProbe] : 0f);
				Vector3 restP = wpP + nP * dsP;
				if (!SoftFleshController._gripProbeFresh)
				{
					float sw = (restP - SoftFleshController._gripProbePrev).magnitude;
					SoftFleshController.GripSwayPeak = ((sw > SoftFleshController.GripSwayPeak) ? sw : (SoftFleshController.GripSwayPeak * 0.98f));
				}
				SoftFleshController._gripProbePrev = restP;
				SoftFleshController._gripProbeFresh = false;
			}
			SoftFleshController.PhAdd(6, _tcontact);
			if (CS$<>8__locals1.body.vagPivots.Count > 0)
			{
				Vector3 ringC = Vector3.zero;
				for (int q18 = 0; q18 < CS$<>8__locals1.body.vagPivots.Count; q18 = num + 1)
				{
					ringC += CS$<>8__locals1.body.vagPivots[q18].position;
					num = q18;
				}
				ringC /= (float)CS$<>8__locals1.body.vagPivots.Count;
				Vector3 axis;
				if (CS$<>8__locals1.body.vagInner.Count > 0)
				{
					Vector3 innAvg = Vector3.zero;
					for (int q19 = 0; q19 < CS$<>8__locals1.body.vagInner.Count; q19 = num + 1)
					{
						innAvg += CS$<>8__locals1.body.vagInner[q19].position;
						num = q19;
					}
					innAvg /= (float)CS$<>8__locals1.body.vagInner.Count;
					axis = (((innAvg - ringC).sqrMagnitude > 1E-10f) ? (innAvg - ringC).normalized : (-CS$<>8__locals1.chaFwd));
				}
				else
				{
					axis = -CS$<>8__locals1.chaFwd;
				}
				Vector3 clitU = Vector3.zero;
				if (CS$<>8__locals1.body.vagRoot != null)
				{
					Vector3 wR0 = CS$<>8__locals1.body.vagRoot.position - ringC;
					Vector3 rR0 = wR0 - axis * Vector3.Dot(wR0, axis);
					if (rR0.sqrMagnitude > 1E-10f)
					{
						clitU = rR0.normalized;
					}
				}
				SoftFleshController.SyncSpread(CS$<>8__locals1.body);
				CS$<>8__locals1.body.lastSpreadMm = ((CS$<>8__locals1.body.spreadL == null && CS$<>8__locals1.body.spreadR == null) ? (-1f) : (this.spreadShift * effW * 1000f));
				if ((CS$<>8__locals1.body.spreadL != null || CS$<>8__locals1.body.spreadR != null) && this.spreadShift * effW > 0.0001f)
				{
					Vector3 sideU = ((clitU.sqrMagnitude > 1E-08f) ? Vector3.Cross(axis, clitU) : Vector3.ProjectOnPlane(smr.transform.right, axis));
					if (sideU.sqrMagnitude < 1E-08f)
					{
						sideU = Vector3.Cross(axis, Vector3.forward);
					}
					sideU = sideU.normalized;
					if (Vector3.Dot(sideU, smr.transform.right) < 0f)
					{
						sideU = -sideU;
					}
					Vector3 off = sideU * (this.spreadShift * effW);
					if (CS$<>8__locals1.body.spreadL != null && CS$<>8__locals1.body.spreadL.parent != null)
					{
						CS$<>8__locals1.body.spreadL.localPosition = CS$<>8__locals1.body.spreadLPos + CS$<>8__locals1.body.spreadL.parent.InverseTransformVector(off);
					}
					if (CS$<>8__locals1.body.spreadR != null && CS$<>8__locals1.body.spreadR.parent != null)
					{
						CS$<>8__locals1.body.spreadR.localPosition = CS$<>8__locals1.body.spreadRPos - CS$<>8__locals1.body.spreadR.parent.InverseTransformVector(off);
					}
					CS$<>8__locals1.body.sprApplied = ((CS$<>8__locals1.body.spreadL != null) ? (CS$<>8__locals1.body.spreadL.localPosition.magnitude * 1000f) : (-1f));
				}
				bool actV = this.openTestDepth > 0.001f || this.openTestWidth > 0.001f;
				if (this.diagVisible && Time.unscaledTime >= this._openLogNext)
				{
					SoftFleshController.MeasureSpreadFlesh(CS$<>8__locals1.body, smr);
				}
				if (Time.unscaledTime >= this._openLogNext && (actV || effW > 0.001f || CS$<>8__locals1.body.lastSpreadMm > 0.01f))
				{
					this._openLogNext = Time.unscaledTime + 0.5f;
					SoftFleshPlugin.Log(string.Concat(new string[]
					{
						"[SF] open drive: openW=",
						this.openTestWidth.ToString("F2"),
						" openD=",
						this.openTestDepth.ToString("F2"),
						" itemW=",
						CS$<>8__locals1.body.itemW.ToString("F2"),
						" effW=",
						effW.ToString("F2"),
						" | rig pivots=",
						CS$<>8__locals1.body.vagPivots.Count.ToString(),
						" driving=",
						actV.ToString(),
						" | bakeReady=",
						CS$<>8__locals1.body.bakeReady.ToString(),
						" bakeDelta=",
						CS$<>8__locals1.body.lastOpenMm.ToString("F1"),
						"mm | spread want=",
						CS$<>8__locals1.body.lastSpreadMm.ToString("F1"),
						" applied=",
						CS$<>8__locals1.body.sprApplied.ToString("F1"),
						" carried=",
						CS$<>8__locals1.body.sprCarry.ToString("F1"),
						"mm helpers=",
						(CS$<>8__locals1.body.spreadL != null) ? "L" : "-",
						(CS$<>8__locals1.body.spreadR != null) ? "R" : "-",
						" | FLESH followed=",
						CS$<>8__locals1.body.sprFlesh.ToString("F1"),
						"mm over ",
						((CS$<>8__locals1.body.sprVerts != null) ? CS$<>8__locals1.body.sprVerts.Length : (-1)).ToString(),
						" verts skinWeights=",
						QualitySettings.skinWeights.ToString(),
						"/",
						smr.quality.ToString(),
						" | chain moved=",
						CS$<>8__locals1.body.pivTurn.ToString("F1"),
						"mm of ",
						CS$<>8__locals1.body.dynPartTr.Count.ToString(),
						" particles (restPose=",
						this.restPose.ToString(),
						") kokanScale=",
						(CS$<>8__locals1.body.spreadL != null && CS$<>8__locals1.body.spreadL.parent != null) ? CS$<>8__locals1.body.spreadL.parent.lossyScale.ToString("F3") : "?"
					}));
				}
				if (actV)
				{
					CS$<>8__locals1.body.vagDriving = true;
					float angMax = 55f * (this.openMaxPct / 150f);
					float ang = this.openTestWidth * angMax;
					float sink = this.openTestDepth * 0.025f * (this.openMaxPct / 150f);
					for (int q20 = 0; q20 < CS$<>8__locals1.body.vagPivots.Count; q20 = num + 1)
					{
						Transform pv4 = CS$<>8__locals1.body.vagPivots[q20];
						pv4.localRotation = CS$<>8__locals1.body.vagPivotRot[q20];
						if (q20 < CS$<>8__locals1.body.vagPivotPos.Count)
						{
							pv4.localPosition = CS$<>8__locals1.body.vagPivotPos[q20];
						}
						if (q20 < CS$<>8__locals1.body.vagPivotPos.Count)
						{
							pv4.localPosition = CS$<>8__locals1.body.vagPivotPos[q20];
						}
						Transform child = ((pv4.childCount > 0) ? pv4.GetChild(0) : null);
						Vector3 refP = ((child != null) ? child.position : pv4.position);
						Vector3 w12 = refP - ringC;
						Vector3 rad2 = w12 - axis * Vector3.Dot(w12, axis);
						if (rad2.sqrMagnitude >= 1E-10f)
						{
							Vector3 radU = rad2.normalized;
							float ang2 = ang;
							if (clitU != Vector3.zero && this.clitHold > 0f)
							{
								float t3 = Vector3.Dot(radU, clitU);
								if (t3 > 0f)
								{
									ang2 = ang * (1f - this.clitHold * t3 * t3);
								}
							}
							Vector3 hinge = Vector3.Cross(axis, radU);
							if (hinge.sqrMagnitude >= 1E-10f)
							{
								hinge.Normalize();
								Vector3 vRel = refP - pv4.position;
								if (Vector3.Dot(Vector3.Cross(hinge, vRel), radU) < 0f)
								{
									hinge = -hinge;
								}
								if (this.restPose && CS$<>8__locals1.body.dynInitPosF != null)
								{
									Quaternion rot = Quaternion.AngleAxis(ang2, hinge);
									for (int pi2 = 0; pi2 < CS$<>8__locals1.body.dynPartTr.Count; pi2 = num + 1)
									{
										Transform ptr2 = CS$<>8__locals1.body.dynPartTr[pi2];
										if (!(ptr2 == null) && !(ptr2.parent != pv4))
										{
											Vector3 restW = pv4.TransformPoint(CS$<>8__locals1.body.dynPartRest[pi2]);
											Vector3 movedW = pv4.position + rot * (restW - pv4.position);
											CS$<>8__locals1.body.dynInitPosF.SetValue(CS$<>8__locals1.body.dynParts[pi2], pv4.InverseTransformPoint(movedW));
										}
										num = pi2;
									}
								}
								else
								{
									pv4.rotation = Quaternion.AngleAxis(ang2, hinge) * pv4.rotation;
								}
								if (this.ringDump)
								{
									string[] array = new string[9];
									array[0] = "[SF] ring ";
									array[1] = pv4.name;
									array[2] = " r=";
									array[3] = (rad2.magnitude * 1000f).ToString("F1");
									array[4] = "mm dir=";
									int num2 = 5;
									Vector3 vector = radU;
									array[num2] = vector.ToString();
									array[6] = " ang=";
									array[7] = ang2.ToString("F1");
									array[8] = "deg";
									SoftFleshPlugin.Log(string.Concat(array));
								}
							}
						}
						num = q20;
					}
					this.ringDump = false;
					for (int q21 = 0; q21 < CS$<>8__locals1.body.vagInner.Count; q21 = num + 1)
					{
						Transform ib = CS$<>8__locals1.body.vagInner[q21];
						Vector3 offW = axis * Mathf.Max(sink, 0f);
						Transform par = ib.parent;
						ib.localPosition = CS$<>8__locals1.body.vagInnerBase[q21] + ((par != null) ? par.InverseTransformVector(offW) : offW);
						num = q21;
					}
				}
				else if (CS$<>8__locals1.body.vagDriving)
				{
					if (CS$<>8__locals1.body.dynInitPosF != null)
					{
						for (int pi3 = 0; pi3 < CS$<>8__locals1.body.dynParts.Count; pi3 = num + 1)
						{
							CS$<>8__locals1.body.dynInitPosF.SetValue(CS$<>8__locals1.body.dynParts[pi3], CS$<>8__locals1.body.dynPartRest[pi3]);
							num = pi3;
						}
					}
					SoftFleshController.SyncSpread(CS$<>8__locals1.body);
					int q22 = 0;
					while (q22 < CS$<>8__locals1.body.ringBones.Count && q22 < CS$<>8__locals1.body.ringBonePos.Count)
					{
						if (CS$<>8__locals1.body.ringBones[q22] != null)
						{
							CS$<>8__locals1.body.ringBones[q22].localPosition = CS$<>8__locals1.body.ringBonePos[q22];
						}
						num = q22;
						q22 = num + 1;
					}
					for (int q23 = 0; q23 < CS$<>8__locals1.body.vagPivots.Count; q23 = num + 1)
					{
						CS$<>8__locals1.body.vagPivots[q23].localRotation = CS$<>8__locals1.body.vagPivotRot[q23];
						if (q23 < CS$<>8__locals1.body.vagPivotPos.Count)
						{
							CS$<>8__locals1.body.vagPivots[q23].localPosition = CS$<>8__locals1.body.vagPivotPos[q23];
						}
						num = q23;
					}
					for (int q24 = 0; q24 < CS$<>8__locals1.body.vagInner.Count; q24 = num + 1)
					{
						CS$<>8__locals1.body.vagInner[q24].localPosition = CS$<>8__locals1.body.vagInnerBase[q24];
						num = q24;
					}
					CS$<>8__locals1.body.vagDriving = false;
				}
			}
			if (CS$<>8__locals1.body.bsAll.Count > 0)
			{
				bool actB = effD > 0.001f || effW > 0.001f;
				if (actB)
				{
					if (!CS$<>8__locals1.body.bsDriving)
					{
						CS$<>8__locals1.body.bsOrig.Clear();
						for (int q25 = 0; q25 < CS$<>8__locals1.body.bsAll.Count; q25 = num + 1)
						{
							CS$<>8__locals1.body.bsOrig.Add(smr.GetBlendShapeWeight(CS$<>8__locals1.body.bsAll[q25]));
							num = q25;
						}
						CS$<>8__locals1.body.bsDriving = true;
					}
					for (int q26 = 0; q26 < CS$<>8__locals1.body.bsDepth.Count; q26 = num + 1)
					{
						smr.SetBlendShapeWeight(CS$<>8__locals1.body.bsDepth[q26], effD * this.openMaxPct);
						num = q26;
					}
					for (int q27 = 0; q27 < CS$<>8__locals1.body.bsWidth.Count; q27 = num + 1)
					{
						smr.SetBlendShapeWeight(CS$<>8__locals1.body.bsWidth[q27], effW * this.openMaxPct);
						num = q27;
					}
				}
				else if (CS$<>8__locals1.body.bsDriving)
				{
					for (int q28 = 0; q28 < CS$<>8__locals1.body.bsAll.Count; q28 = num + 1)
					{
						smr.SetBlendShapeWeight(CS$<>8__locals1.body.bsAll[q28], (q28 < CS$<>8__locals1.body.bsOrig.Count) ? CS$<>8__locals1.body.bsOrig[q28] : 0f);
						num = q28;
					}
					CS$<>8__locals1.body.bsDriving = false;
				}
			}
			long _topen = SoftFleshController.SwNow();
			if (CS$<>8__locals1.body.bakeReady && CS$<>8__locals1.body.bsAll.Count == 0)
			{
				bool act = effD > 0.001f || effW > 0.001f;
				if (act)
				{
					CS$<>8__locals1.body.openCool = 90;
				}
				else if (CS$<>8__locals1.body.openCool > 0)
				{
					body2 = CS$<>8__locals1.body;
					SoftFleshController.Body body9 = body2;
					num = body2.openCool;
					body9.openCool = num - 1;
				}
				if (act || CS$<>8__locals1.body.openCool > 0)
				{
					int nR = CS$<>8__locals1.body.bakeRadii.Length;
					int nD = CS$<>8__locals1.body.bakeDepths.Length;
					float rT = CS$<>8__locals1.body.bakeRadii[0] + (CS$<>8__locals1.body.bakeRadii[nR - 1] - CS$<>8__locals1.body.bakeRadii[0]) * effW;
					float dT = effD * CS$<>8__locals1.body.canalLen;
					int r0 = 0;
					while (r0 + 2 < nR && CS$<>8__locals1.body.bakeRadii[r0 + 1] < rT)
					{
						num = r0;
						r0 = num + 1;
					}
					int d = 0;
					while (d + 2 < nD && CS$<>8__locals1.body.bakeDepths[d + 1] < dT)
					{
						num = d;
						d = num + 1;
					}
					float tr = Mathf.InverseLerp(CS$<>8__locals1.body.bakeRadii[r0], CS$<>8__locals1.body.bakeRadii[r0 + 1], rT);
					float td = Mathf.InverseLerp(CS$<>8__locals1.body.bakeDepths[d], CS$<>8__locals1.body.bakeDepths[d + 1], dT);
					Vector3[] s0 = CS$<>8__locals1.body.bakeDelta[d * nR + r0];
					Vector3[] s = CS$<>8__locals1.body.bakeDelta[d * nR + r0 + 1];
					Vector3[] s2 = CS$<>8__locals1.body.bakeDelta[(d + 1) * nR + r0];
					Vector3[] s3 = CS$<>8__locals1.body.bakeDelta[(d + 1) * nR + r0 + 1];
					float w13 = (1f - td) * (1f - tr);
					float w14 = (1f - td) * tr;
					float w15 = td * (1f - tr);
					float w16 = td * tr;
					bool taperOn = this.taperCanal && CS$<>8__locals1.body.canalWAt != null && CS$<>8__locals1.body.bakeAxialT != null;
					float rLo = CS$<>8__locals1.body.bakeRadii[0];
					float rSpan = CS$<>8__locals1.body.bakeRadii[nR - 1] - CS$<>8__locals1.body.bakeRadii[0];
					if (taperOn)
					{
						if (SoftFleshController._rowD0 == null || SoftFleshController._rowD0.Length != nR)
						{
							SoftFleshController._rowD0 = new Vector3[nR][];
							SoftFleshController._rowD1 = new Vector3[nR][];
						}
						for (int rw = 0; rw < nR; rw = num + 1)
						{
							SoftFleshController._rowD0[rw] = CS$<>8__locals1.body.bakeDelta[d * nR + rw];
							SoftFleshController._rowD1[rw] = CS$<>8__locals1.body.bakeDelta[(d + 1) * nR + rw];
							num = rw;
						}
					}
					float openPk = 0f;
					float tiltPk = 0f;
					if (SoftFleshController._tiltLut == null)
					{
						SoftFleshController._tiltLut = new Quaternion[17];
					}
					for (int t4 = 0; t4 <= 16; t4 = num + 1)
					{
						SoftFleshController._tiltLut[t4] = Quaternion.Slerp(Quaternion.identity, CS$<>8__locals1.body.tiltB, (float)t4 / 16f);
						num = t4;
					}
					if (CS$<>8__locals1.body.openStamp == null || CS$<>8__locals1.body.openStamp.Length != CS$<>8__locals1.n)
					{
						CS$<>8__locals1.body.openStamp = new int[CS$<>8__locals1.n];
					}
					if (CS$<>8__locals1.body.pushLast == null || CS$<>8__locals1.body.pushLast.Length != CS$<>8__locals1.n)
					{
						CS$<>8__locals1.body.pushLast = new Vector3[CS$<>8__locals1.n];
					}
					body2 = CS$<>8__locals1.body;
					SoftFleshController.Body body10 = body2;
					num = body2.openCur + 1;
					body10.openCur = num;
					int ostamp = num;
					for (int q29 = 0; q29 < CS$<>8__locals1.simList.Count; q29 = num + 1)
					{
						CS$<>8__locals1.body.openStamp[CS$<>8__locals1.simList[q29]] = ostamp;
						num = q29;
					}
					this._springCount = CS$<>8__locals1.simList.Count;
					CS$<>8__locals1.body.lastSim = CS$<>8__locals1.simList.Count;
					SoftFleshController.LoadSim = CS$<>8__locals1.simList.Count;
					for (int q30 = 0; q30 < CS$<>8__locals1.body.bakeVerts.Length; q30 = num + 1)
					{
						int i13 = CS$<>8__locals1.body.bakeVerts[q30];
						Vector3 dv = Vector3.zero;
						if (act)
						{
							if (taperOn)
							{
								float ft = Mathf.Clamp01(CS$<>8__locals1.body.bakeAxialT[q30]) * 8f;
								int k6 = (int)ft;
								if (k6 > 7)
								{
									k6 = 7;
								}
								float kt = ft - (float)k6;
								float wq = effW * (CS$<>8__locals1.body.canalWAt[k6] + (CS$<>8__locals1.body.canalWAt[k6 + 1] - CS$<>8__locals1.body.canalWAt[k6]) * kt);
								if (wq < 0f)
								{
									wq = 0f;
								}
								else if (wq > 1f)
								{
									wq = 1f;
								}
								float rTq = rLo + rSpan * wq;
								int rq = 0;
								while (rq + 2 < nR && CS$<>8__locals1.body.bakeRadii[rq + 1] < rTq)
								{
									num = rq;
									rq = num + 1;
								}
								float trq = Mathf.InverseLerp(CS$<>8__locals1.body.bakeRadii[rq], CS$<>8__locals1.body.bakeRadii[rq + 1], rTq);
								Vector3[] a0v = SoftFleshController._rowD0[rq];
								Vector3[] a1v = SoftFleshController._rowD0[rq + 1];
								Vector3[] b0v = SoftFleshController._rowD1[rq];
								Vector3[] b1v = SoftFleshController._rowD1[rq + 1];
								float q31 = (1f - td) * (1f - trq);
								float q32 = (1f - td) * trq;
								float q33 = td * (1f - trq);
								float q34 = td * trq;
								dv.x = a0v[q30].x * q31 + a1v[q30].x * q32 + b0v[q30].x * q33 + b1v[q30].x * q34;
								dv.y = a0v[q30].y * q31 + a1v[q30].y * q32 + b0v[q30].y * q33 + b1v[q30].y * q34;
								dv.z = a0v[q30].z * q31 + a1v[q30].z * q32 + b0v[q30].z * q33 + b1v[q30].z * q34;
							}
							else
							{
								dv.x = s0[q30].x * w13 + s[q30].x * w14 + s2[q30].x * w15 + s3[q30].x * w16;
								dv.y = s0[q30].y * w13 + s[q30].y * w14 + s2[q30].y * w15 + s3[q30].y * w16;
								dv.z = s0[q30].z * w13 + s[q30].z * w14 + s2[q30].z * w15 + s3[q30].z * w16;
							}
							if (this.clitHold > 0f && CS$<>8__locals1.body.bakeClitW != null && q30 < CS$<>8__locals1.body.bakeClitW.Length)
							{
								float cw3 = 1f - this.clitHold * CS$<>8__locals1.body.bakeClitW[q30];
								ptr3 = ref dv.x;
								ptr3 *= cw3;
								ptr3 = ref dv.y;
								ptr3 *= cw3;
								ptr3 = ref dv.z;
								ptr3 *= cw3;
							}
							float axT = ((CS$<>8__locals1.body.bakeAxialT != null) ? CS$<>8__locals1.body.bakeAxialT[q30] : 0f);
							float om = this.openMaxPct / 150f - 1f;
							dv *= 1f + om * ((om > 0f) ? (1f - 0.75f * axT) : 1f);
							if (CS$<>8__locals1.body.bakeSuckW != null && q30 < CS$<>8__locals1.body.bakeSuckW.Length && CS$<>8__locals1.body.suckOff != 0f)
							{
								float wgt = ((CS$<>8__locals1.body.suckOff >= 0f) ? CS$<>8__locals1.body.bakeSuckW[q30] : ((CS$<>8__locals1.body.bakePetW != null && q30 < CS$<>8__locals1.body.bakePetW.Length) ? CS$<>8__locals1.body.bakePetW[q30] : 0f));
								float sk = wgt * CS$<>8__locals1.body.suckOff;
								ptr3 = ref dv.x;
								ptr3 += CS$<>8__locals1.body.lastBakeDirB.x * sk;
								ptr3 = ref dv.y;
								ptr3 += CS$<>8__locals1.body.lastBakeDirB.y * sk;
								ptr3 = ref dv.z;
								ptr3 += CS$<>8__locals1.body.lastBakeDirB.z * sk;
							}
							if (CS$<>8__locals1.body.bakeSuckW != null && q30 < CS$<>8__locals1.body.bakeSuckW.Length && CS$<>8__locals1.body.followB.sqrMagnitude > 1E-12f)
							{
								float fw = CS$<>8__locals1.body.bakeSuckW[q30];
								ptr3 = ref dv.x;
								ptr3 += CS$<>8__locals1.body.followB.x * fw;
								ptr3 = ref dv.y;
								ptr3 += CS$<>8__locals1.body.followB.y * fw;
								ptr3 = ref dv.z;
								ptr3 += CS$<>8__locals1.body.followB.z * fw;
							}
							if (CS$<>8__locals1.body.bakeTiltW != null && q30 < CS$<>8__locals1.body.bakeTiltW.Length && CS$<>8__locals1.body.bakeTiltW[q30] > 0.002f)
							{
								float tf = CS$<>8__locals1.body.bakeTiltW[q30];
								Vector3 rel = CS$<>8__locals1.body.baseVerts[i13] - CS$<>8__locals1.body.rimOrigin;
								Vector3 rot2 = SoftFleshController._tiltLut[(int)(tf * 16f + 0.5f)] * rel;
								float tdx = rot2.x - rel.x;
								float tdy = rot2.y - rel.y;
								float tdz = rot2.z - rel.z;
								ptr3 = ref dv.x;
								ptr3 += tdx;
								ptr3 = ref dv.y;
								ptr3 += tdy;
								ptr3 = ref dv.z;
								ptr3 += tdz;
								float t5 = tdx * tdx + tdy * tdy + tdz * tdz;
								if (t5 > tiltPk)
								{
									tiltPk = t5;
								}
							}
							Vector3 keepC = ((CS$<>8__locals1.body.actStamp[i13] == CS$<>8__locals1.body.stampCur) ? CS$<>8__locals1.body.pushT[i13] : Vector3.zero);
							float dvm2 = dv.sqrMagnitude;
							float k7 = dvm2 / 6.400001E-05f;
							float share = 1f / (1f + k7);
							CS$<>8__locals1.body.pushT[i13] = dv + keepC * share;
							float dm2 = dv.sqrMagnitude;
							if (dm2 > openPk)
							{
								openPk = dm2;
							}
						}
						else
						{
							CS$<>8__locals1.body.pushT[i13] = ((CS$<>8__locals1.body.actStamp[i13] == CS$<>8__locals1.body.stampCur) ? CS$<>8__locals1.body.pushT[i13] : Vector3.zero);
						}
						Vector3 prevT = CS$<>8__locals1.body.pushLast[i13];
						CS$<>8__locals1.body.pushLast[i13] = dv;
						if (((dv - prevT).sqrMagnitude > 1E-10f || (dv - CS$<>8__locals1.body.pushB[i13]).sqrMagnitude > 2.5E-09f) && CS$<>8__locals1.body.openStamp[i13] != ostamp)
						{
							CS$<>8__locals1.body.openStamp[i13] = ostamp;
							CS$<>8__locals1.simList.Add(i13);
						}
						num = q30;
					}
					CS$<>8__locals1.body.lastOpenMm = Mathf.Sqrt(openPk) * 1000f;
					CS$<>8__locals1.body.tiltMm = Mathf.Sqrt(tiltPk) * 1000f;
				}
			}
			SoftFleshController.PhAdd(3, _topen);
			float dec = Mathf.Exp(-dt / 1.2f);
			CS$<>8__locals1.body.anyImp = false;
			for (int k8 = 0; k8 < 4; k8 = num + 1)
			{
				ptr3 = ref CS$<>8__locals1.body.impStr[k8];
				ptr3 *= dec;
				if (CS$<>8__locals1.body.impStr[k8] > 0.02f)
				{
					CS$<>8__locals1.body.anyImp = true;
				}
				num = k8;
			}
			if (localImp > 0.05f)
			{
				float st = localImp;
				if (st > 1f)
				{
					st = 1f;
				}
				int slot = -1;
				int k9 = 0;
				while (k9 < 4 && slot < 0)
				{
					if (CS$<>8__locals1.body.impStr[k9] > 0.02f && (CS$<>8__locals1.body.impPos[k9] - impC).sqrMagnitude < 0.0036f)
					{
						slot = k9;
					}
					num = k9;
					k9 = num + 1;
				}
				if (slot < 0)
				{
					slot = 0;
					for (int k10 = 1; k10 < 4; k10 = num + 1)
					{
						if (CS$<>8__locals1.body.impStr[k10] < CS$<>8__locals1.body.impStr[slot])
						{
							slot = k10;
						}
						num = k10;
					}
				}
				CS$<>8__locals1.body.impPos[slot] = impC;
				if (st > CS$<>8__locals1.body.impStr[slot])
				{
					CS$<>8__locals1.body.impStr[slot] = st;
				}
				CS$<>8__locals1.body.anyImp = true;
			}
			long _tspring = SoftFleshController.SwNow();
			float lim = CS$<>8__locals1.mdB * 2f;
			float vlim = 40f * CS$<>8__locals1.mdB * omega;
			if (CS$<>8__locals1.body.tavgBuf == null || CS$<>8__locals1.body.tavgBuf.Length != CS$<>8__locals1.body.disp.Length)
			{
				CS$<>8__locals1.body.tavgBuf = new float[CS$<>8__locals1.body.disp.Length];
			}
			if (CS$<>8__locals1.body.velPrev == null || CS$<>8__locals1.body.velPrev.Length != CS$<>8__locals1.body.vel.Length)
			{
				CS$<>8__locals1.body.velPrev = new float[CS$<>8__locals1.body.vel.Length];
			}
			int nS0 = ((this._springCount > CS$<>8__locals1.simList.Count) ? CS$<>8__locals1.simList.Count : this._springCount);
			for (int q35 = 0; q35 < nS0; q35 = num + 1)
			{
				int i14 = CS$<>8__locals1.simList[q35];
				float ta = 0f;
				int nc0 = 0;
				for (int k11 = CS$<>8__locals1.body.adjStart[i14]; k11 < CS$<>8__locals1.body.adjStart[i14 + 1]; k11 = num + 1)
				{
					ta += CS$<>8__locals1.body.tgtF[CS$<>8__locals1.body.adjList[k11]];
					num = nc0;
					nc0 = num + 1;
					num = k11;
				}
				CS$<>8__locals1.body.tavgBuf[i14] = ((nc0 > 0) ? (ta / (float)nc0) : 0f);
				num = q35;
			}
			Action<int, int> <>9__1;
			for (int s4 = 0; s4 < steps; s4 = num + 1)
			{
				int nSpring = ((this._springCount > CS$<>8__locals1.simList.Count) ? CS$<>8__locals1.simList.Count : this._springCount);
				for (int q36 = 0; q36 < nSpring; q36 = num + 1)
				{
					int i0s = CS$<>8__locals1.simList[q36];
					CS$<>8__locals1.body.dispPrev[i0s] = CS$<>8__locals1.body.disp[i0s];
					CS$<>8__locals1.body.velPrev[i0s] = CS$<>8__locals1.body.vel[i0s];
					num = q36;
				}
				int num3 = nSpring;
				Action<int, int> action;
				if ((action = <>9__1) == null)
				{
					action = (<>9__1 = delegate(int _q0, int _q1)
					{
						for (int q44 = _q0; q44 < _q1; q44++)
						{
							int i22 = CS$<>8__locals1.simList[q44];
							float tg = CS$<>8__locals1.body.tgtF[i22];
							if (CS$<>8__locals1.<>4__this.gripFreeze && CS$<>8__locals1.body.holdAge != null && CS$<>8__locals1.body.holdAge[i22] > 30)
							{
								CS$<>8__locals1.body.vel[i22] = 0f;
							}
							else
							{
								float navg = 0f;
								float vavg = 0f;
								int nc = 0;
								int a2 = CS$<>8__locals1.body.adjStart[i22];
								int a3 = CS$<>8__locals1.body.adjStart[i22 + 1];
								for (int k14 = a2; k14 < a3; k14++)
								{
									int j10 = CS$<>8__locals1.body.adjList[k14];
									navg += CS$<>8__locals1.body.dispPrev[j10];
									vavg += CS$<>8__locals1.body.velPrev[j10];
									nc++;
								}
								if (nc > 0)
								{
									navg /= (float)nc;
									vavg /= (float)nc;
								}
								float tavg = CS$<>8__locals1.body.tavgBuf[i22];
								float ds2 = CS$<>8__locals1.body.disp[i22];
								float vl2 = CS$<>8__locals1.body.vel[i22];
								bool armV = CS$<>8__locals1.body.armSide[i22] > 0;
								float sof = (armV ? 0f : CS$<>8__locals1.body.soften[i22]);
								float kA = ((tg != 0f) ? CS$<>8__locals1.kBody : (armV ? CS$<>8__locals1.kBody : (CS$<>8__locals1.anchorK + (CS$<>8__locals1.softK - CS$<>8__locals1.anchorK) * sof)));
								float eEff = CS$<>8__locals1.eFloor;
								if (CS$<>8__locals1.body.vertEdge != null && CS$<>8__locals1.body.vertEdge[i22] > CS$<>8__locals1.eFloor)
								{
									eEff = CS$<>8__locals1.body.vertEdge[i22];
								}
								float kcI = CS$<>8__locals1.kcNum / (eEff * eEff);
								float wsh3 = 1f;
								if (CS$<>8__locals1.<>4__this.waveShield > 0.001f && CS$<>8__locals1.body.waveShieldW != null && CS$<>8__locals1.body.waveShieldW[i22] > 0f)
								{
									wsh3 = 1f - CS$<>8__locals1.<>4__this.waveShield * CS$<>8__locals1.body.waveShieldW[i22];
									kcI *= wsh3;
								}
								if (tg != 0f && CS$<>8__locals1.body.holdAge != null && CS$<>8__locals1.body.holdAge[i22] > 30)
								{
									float hold = ((tg < 0f) ? (-tg) : tg) / (CS$<>8__locals1.mdB * 0.35f);
									if (hold > 1f)
									{
										hold = 1f;
									}
									float hw = 1f - 0.85f * hold;
									kcI *= hw;
									wsh3 *= hw;
								}
								float spring = (armV ? (kA * (tg - ds2)) : (kA * (tg - ds2) + kcI * (navg - tavg - (ds2 - tg))));
								float dampV = CS$<>8__locals1.cBody * (1f - 0.75f * sof);
								float kv = ((CS$<>8__locals1.<>4__this.viscosity > 0f && !armV) ? (CS$<>8__locals1.<>4__this.viscosity * (2f * CS$<>8__locals1.<>4__this.waveSpeed) / eEff * wsh3) : 0f);
								vl2 = (vl2 + spring * CS$<>8__locals1.sdt + kv * CS$<>8__locals1.sdt * vavg) / (1f + (dampV + kv) * CS$<>8__locals1.sdt);
								if (vl2 > vlim)
								{
									vl2 = vlim;
								}
								else if (vl2 < -vlim)
								{
									vl2 = -vlim;
								}
								ds2 += vl2 * CS$<>8__locals1.sdt;
								if (ds2 > lim)
								{
									ds2 = lim;
									if (vl2 > 0f)
									{
										vl2 = 0f;
									}
								}
								else if (ds2 < -lim)
								{
									ds2 = -lim;
									if (vl2 < 0f)
									{
										vl2 = 0f;
									}
								}
								CS$<>8__locals1.body.disp[i22] = ds2;
								CS$<>8__locals1.body.vel[i22] = vl2;
							}
						}
					});
				}
				SoftFleshController.ParallelFor(num3, action);
				num = s4;
			}
			SoftFleshController.PhAdd(4, _tspring);
			float dEps = this.maxDepth * 0.008f;
			if (dEps < 1E-05f)
			{
				dEps = 1E-05f;
			}
			float vEps = dEps * 30f;
			float peakV = 0f;
			float peakD = 0f;
			bool pushLive = false;
			bool stillSettling = false;
			float follow3 = 1f - Mathf.Exp(-14f * dt);
			for (int q37 = 0; q37 < CS$<>8__locals1.simList.Count; q37 = num + 1)
			{
				int i15 = CS$<>8__locals1.simList[q37];
				Vector3 pt = CS$<>8__locals1.body.pushT[i15];
				Vector3 pb = CS$<>8__locals1.body.pushB[i15];
				if (pt.x != 0f || pt.y != 0f || pt.z != 0f || pb.sqrMagnitude > 1E-10f)
				{
					if ((pt - pb).sqrMagnitude > 2.5E-09f)
					{
						stillSettling = true;
					}
					bool wrapDirect = CS$<>8__locals1.body.isFace && CS$<>8__locals1.body.mouthPushValid && CS$<>8__locals1.body.mouthW != null && CS$<>8__locals1.body.mouthW[i15] > 0f && (pt.x != 0f || pt.y != 0f || pt.z != 0f);
					pb += (pt - pb) * (wrapDirect ? 1f : follow3);
					if (pb.sqrMagnitude < 2.5E-09f && pt.x == 0f && pt.y == 0f && pt.z == 0f)
					{
						pb = Vector3.zero;
					}
					CS$<>8__locals1.body.pushB[i15] = pb;
					float pm2 = pb.sqrMagnitude;
					if (pm2 > 1E-10f)
					{
						pushLive = true;
					}
					if (pm2 > maxPush2)
					{
						maxPush2 = pm2;
					}
				}
				float ds = CS$<>8__locals1.body.disp[i15];
				float vl = CS$<>8__locals1.body.vel[i15];
				float ad = ((ds < 0f) ? (-ds) : ds);
				float av = ((vl < 0f) ? (-vl) : vl);
				if (av > peakV)
				{
					peakV = av;
				}
				if (ad > peakD)
				{
					peakD = ad;
				}
				if (ad > dEps || av > vEps || CS$<>8__locals1.body.pushB[i15].sqrMagnitude > 2.5E-09f)
				{
					anyActive = true;
					if (ad > localMax)
					{
						localMax = ad;
					}
					if (CS$<>8__locals1.body.tgtF[i15] > 0f)
					{
						num = localActive;
						localActive = num + 1;
					}
				}
				else
				{
					CS$<>8__locals1.body.disp[i15] = 0f;
					CS$<>8__locals1.body.vel[i15] = 0f;
				}
				num = q37;
			}
			CS$<>8__locals1.body.pushSettling = stillSettling;
			bool keepAwake = fieldChanged || stillSettling || pressersMoved;
			if (pushLive)
			{
				anyActive = true;
				if (keepAwake && peakV < this.maxDepth)
				{
					peakV = this.maxDepth;
				}
			}
			if (this.corrSmooth > 0.01f && CS$<>8__locals1.body.corrList.Count > 0)
			{
				anyActive = true;
				if (keepAwake && peakV < this.maxDepth)
				{
					peakV = this.maxDepth;
				}
			}
			CS$<>8__locals1.body.maxVel = peakV;
			CS$<>8__locals1.body.dgDisp = peakD;
			if (peakD > CS$<>8__locals1.body.dgDispEver)
			{
				CS$<>8__locals1.body.dgDispEver = peakD;
			}
			CS$<>8__locals1.body.freeResidue = anyActive && !pushLive && localActive == 0;
			if (CS$<>8__locals1.body.seamGroups != null && CS$<>8__locals1.body.seamOf != null && this.seamRepair)
			{
				for (int q38 = 0; q38 < CS$<>8__locals1.simList.Count; q38 = num + 1)
				{
					int g3 = CS$<>8__locals1.body.seamOf[CS$<>8__locals1.simList[q38]];
					if (g3 >= 0)
					{
						int[] grp = CS$<>8__locals1.body.seamGroups[g3];
						bool mixed = false;
						byte s5 = CS$<>8__locals1.body.armSide[grp[0]];
						int j4 = 1;
						while (j4 < grp.Length && !mixed)
						{
							if (CS$<>8__locals1.body.armSide[grp[j4]] != s5)
							{
								mixed = true;
							}
							num = j4;
							j4 = num + 1;
						}
						if (!mixed)
						{
							float mx = 0f;
							for (int j5 = 0; j5 < grp.Length; j5 = num + 1)
							{
								if (Mathf.Abs(CS$<>8__locals1.body.disp[grp[j5]]) > Mathf.Abs(mx))
								{
									mx = CS$<>8__locals1.body.disp[grp[j5]];
								}
								num = j5;
							}
							if (mx != 0f)
							{
								for (int j6 = 0; j6 < grp.Length; j6 = num + 1)
								{
									CS$<>8__locals1.body.disp[grp[j6]] = mx;
									int gj = grp[j6];
									if (CS$<>8__locals1.body.actStamp[gj] != cstamp)
									{
										CS$<>8__locals1.body.actStamp[gj] = cstamp;
										CS$<>8__locals1.simList.Add(gj);
									}
									num = j6;
								}
							}
						}
					}
					num = q38;
				}
			}
			List<int> dirty = CS$<>8__locals1.body.dirty;
			for (int q39 = 0; q39 < dirty.Count; q39 = num + 1)
			{
				int i16 = dirty[q39];
				CS$<>8__locals1.body.outV[i16] = ((CS$<>8__locals1.body.subFix != null) ? (CS$<>8__locals1.body.baseVerts[i16] + CS$<>8__locals1.body.subFix[i16]) : CS$<>8__locals1.body.baseVerts[i16]);
				CS$<>8__locals1.body.normList[i16] = CS$<>8__locals1.body.baseNormals[i16];
				num = q39;
			}
			dirty.Clear();
			if (anyActive)
			{
				long _tfield = SoftFleshController.SwNow();
				this.SmoothRenderField(CS$<>8__locals1.body);
				this.DentTrackRecord(CS$<>8__locals1.body, bi);
				SoftFleshController.PhAdd(7, _tfield);
				float[] rdo = CS$<>8__locals1.body.renderDisp;
				List<int> act2 = CS$<>8__locals1.body.actList;
				if (CS$<>8__locals1.body.seamGroups != null && CS$<>8__locals1.body.seamOf != null)
				{
					for (int q40 = 0; q40 < act2.Count; q40 = num + 1)
					{
						int g4 = CS$<>8__locals1.body.seamOf[act2[q40]];
						if (g4 >= 0 && (!CS$<>8__locals1.body.isFace || !CS$<>8__locals1.body.mouthPushValid || CS$<>8__locals1.body.mouthW == null || CS$<>8__locals1.body.mouthW[act2[q40]] <= 0f))
						{
							int[] grp2 = CS$<>8__locals1.body.seamGroups[g4];
							Vector3 pAvg = Vector3.zero;
							Vector3 cAvg = Vector3.zero;
							for (int j7 = 0; j7 < grp2.Length; j7 = num + 1)
							{
								if (CS$<>8__locals1.body.pushR != null)
								{
									pAvg += CS$<>8__locals1.body.pushR[grp2[j7]];
								}
								if (CS$<>8__locals1.body.corrB != null)
								{
									cAvg += CS$<>8__locals1.body.corrB[grp2[j7]];
								}
								num = j7;
							}
							pAvg /= (float)grp2.Length;
							cAvg /= (float)grp2.Length;
							for (int j8 = 0; j8 < grp2.Length; j8 = num + 1)
							{
								if (CS$<>8__locals1.body.pushR != null)
								{
									CS$<>8__locals1.body.pushR[grp2[j8]] = pAvg;
								}
								if (CS$<>8__locals1.body.corrB != null)
								{
									CS$<>8__locals1.body.corrB[grp2[j8]] = cAvg;
								}
								num = j8;
							}
						}
						num = q40;
					}
				}
				if (CS$<>8__locals1.body.clothCovered != null && CS$<>8__locals1.body.subPa != null && CS$<>8__locals1.body.origVertN > 0)
				{
					for (int q41 = 0; q41 < act2.Count; q41 = num + 1)
					{
						int i17 = act2[q41];
						if (i17 >= CS$<>8__locals1.body.origVertN && CS$<>8__locals1.body.clothCovered[i17])
						{
							int pa2 = CS$<>8__locals1.body.subPa[i17];
							if (pa2 >= 0)
							{
								int pb2 = CS$<>8__locals1.body.subPb[i17];
								rdo[i17] = (rdo[pa2] + rdo[pb2]) * 0.5f;
								if (CS$<>8__locals1.body.pushR != null)
								{
									CS$<>8__locals1.body.pushR[i17] = (CS$<>8__locals1.body.pushR[pa2] + CS$<>8__locals1.body.pushR[pb2]) * 0.5f;
								}
								if (CS$<>8__locals1.body.corrB != null)
								{
									CS$<>8__locals1.body.corrB[i17] = (CS$<>8__locals1.body.corrB[pa2] + CS$<>8__locals1.body.corrB[pb2]) * 0.5f;
								}
							}
						}
						num = q41;
					}
				}
				bool surOut = this.TensionOf(CS$<>8__locals1.body) > 0f && CS$<>8__locals1.body.membSur != null && CS$<>8__locals1.body.membN != null && CS$<>8__locals1.body.membStamp != null;
				Vector3[] macroN = CS$<>8__locals1.body.membN;
				for (int q42 = 0; q42 < act2.Count; q42 = num + 1)
				{
					int i18 = act2[q42];
					float dv2 = rdo[i18];
					Vector3 pb3 = ((CS$<>8__locals1.body.pushR != null) ? CS$<>8__locals1.body.pushR[i18] : CS$<>8__locals1.body.pushB[i18]);
					if (CS$<>8__locals1.body.corrB != null)
					{
						pb3 += CS$<>8__locals1.body.corrB[i18];
					}
					bool hasP = pb3.sqrMagnitude > 1E-10f;
					if (dv2 != 0f || hasP)
					{
						int sg = ((CS$<>8__locals1.body.seamOf != null) ? CS$<>8__locals1.body.seamOf[i18] : (-1));
						CS$<>8__locals1.body.outV[i18] = CS$<>8__locals1.body.baseVerts[i18] - (surOut ? macroN[i18] : ((sg >= 0) ? CS$<>8__locals1.body.seamN[sg] : CS$<>8__locals1.body.baseNormals[i18])) * dv2 + (hasP ? pb3 : Vector3.zero) + ((CS$<>8__locals1.body.subFix != null) ? CS$<>8__locals1.body.subFix[i18] : Vector3.zero);
					}
					num = q42;
				}
				float pitF = 0f;
				float pitG = 0f;
				float pitFN = 0f;
				float pitGN = 0f;
				int pitFi = -1;
				int pitGi = -1;
				int nF = 0;
				int nG = 0;
				List<Vector3> ov = CS$<>8__locals1.body.outV;
				Vector3[] bv2 = CS$<>8__locals1.body.baseVerts;
				Vector3[] bn2 = CS$<>8__locals1.body.baseNormals;
				for (int q43 = 0; q43 < act2.Count; q43 = num + 1)
				{
					int i19 = act2[q43];
					int a0 = CS$<>8__locals1.body.adjStart[i19];
					int a = CS$<>8__locals1.body.adjStart[i19 + 1];
					if (a > a0)
					{
						float sum2 = 0f;
						Vector3 so = Vector3.zero;
						Vector3 sb = Vector3.zero;
						for (int k12 = a0; k12 < a; k12 = num + 1)
						{
							int j9 = CS$<>8__locals1.body.adjList[k12];
							sum2 += rdo[j9];
							so += ov[j9];
							sb += bv2[j9];
							num = k12;
						}
						float inv = 1f / (float)(a - a0);
						float nm3 = sum2 * inv;
						float pf = rdo[i19] - nm3;
						Vector3 dO = so * inv - ov[i19];
						Vector3 dB = sb * inv - bv2[i19];
						float pg = Vector3.Dot(dO - dB, bn2[i19]);
						if (pf > 0.002f)
						{
							num = nF;
							nF = num + 1;
						}
						if (pg > 0.002f)
						{
							num = nG;
							nG = num + 1;
						}
						if (pf > pitF)
						{
							pitF = pf;
							pitFi = i19;
							pitFN = nm3;
						}
						if (pg > pitG)
						{
							pitG = pg;
							pitGi = i19;
							pitGN = Vector3.Dot(dO - dB, bn2[i19]) - pg + nm3;
						}
					}
					num = q43;
				}
				CS$<>8__locals1.body.dgPitF = pitF;
				CS$<>8__locals1.body.dgPitFi = pitFi;
				CS$<>8__locals1.body.dgPitFNbr = pitFN;
				CS$<>8__locals1.body.dgPitFN = nF;
				CS$<>8__locals1.body.dgPitG = pitG;
				CS$<>8__locals1.body.dgPitGi = pitGi;
				CS$<>8__locals1.body.dgPitGNbr = pitGN;
				CS$<>8__locals1.body.dgPitGN = nG;
				long _tnorm = SoftFleshController.SwNow();
				this.ComputeDentNormals(CS$<>8__locals1.body);
				this.TemporalNormals(CS$<>8__locals1.body, true);
				this.SmoothOpeningNormals(CS$<>8__locals1.body);
				SoftFleshController.UploadBodyVerts(CS$<>8__locals1.body, true);
				SoftFleshController.PhAdd(5, _tnorm);
				this.ApplyClothFollow(CS$<>8__locals1.body);
				this.UpdateSecondSkins(CS$<>8__locals1.body);
			}
			else if (CS$<>8__locals1.body.hadActive || CS$<>8__locals1.body.nTiltLive.Count > 0)
			{
				this.TemporalNormals(CS$<>8__locals1.body, false);
				CS$<>8__locals1.body.ampScaleF = 0f;
				SoftFleshController.UploadBodyVerts(CS$<>8__locals1.body, true);
				if (CS$<>8__locals1.body.renderDisp != null)
				{
					Array.Clear(CS$<>8__locals1.body.renderDisp, 0, CS$<>8__locals1.body.renderDisp.Length);
				}
				this.ApplyClothFollow(CS$<>8__locals1.body);
				this.UpdateSecondSkins(CS$<>8__locals1.body);
			}
			if (CS$<>8__locals1.body.subFixDirty && CS$<>8__locals1.body.subFix != null)
			{
				CS$<>8__locals1.body.subFixDirty = false;
				float[] rdF = CS$<>8__locals1.body.renderDisp;
				for (int i20 = CS$<>8__locals1.body.origVertN; i20 < CS$<>8__locals1.body.subFix.Length; i20 = num + 1)
				{
					if ((rdF == null || rdF[i20] == 0f) && CS$<>8__locals1.body.pushB[i20].sqrMagnitude <= 1E-10f && (CS$<>8__locals1.body.pushR == null || CS$<>8__locals1.body.pushR[i20].sqrMagnitude <= 1E-10f) && (CS$<>8__locals1.body.corrB == null || CS$<>8__locals1.body.corrB[i20].sqrMagnitude <= 1E-10f))
					{
						CS$<>8__locals1.body.outV[i20] = CS$<>8__locals1.body.baseVerts[i20] + CS$<>8__locals1.body.subFix[i20];
					}
					num = i20;
				}
				SoftFleshController.UploadBodyVerts(CS$<>8__locals1.body, false);
				this.UpdateSecondSkins(CS$<>8__locals1.body);
			}
			CS$<>8__locals1.body.hadActive = anyActive;
			CS$<>8__locals1.body.hasActive = anyActive;
			if (localActive > this.activeVerts)
			{
				this.activeVerts = localActive;
			}
			if (localMax > this.maxDisp)
			{
				this.maxDisp = localMax;
			}
			if (!this._diagBuild)
			{
				return;
			}
			this._diagSb.Append('B').Append(bi).Append(":tgt")
				.Append((localTgt * 1000f).ToString("F0"))
				.Append(" act")
				.Append(localActive)
				.Append(" d")
				.Append((localMax * 1000f).ToString("F1"))
				.Append("mm")
				.Append(" slap")
				.Append(localImp.ToString("F2"))
				.Append(" push")
				.Append((Mathf.Sqrt(maxPush2) * 1000f).ToString("F1"))
				.Append(" open")
				.Append(CS$<>8__locals1.body.lastOpenMm.ToString("F1"))
				.Append(" spr")
				.Append((CS$<>8__locals1.body.lastSpreadMm < 0f) ? "n/a" : CS$<>8__locals1.body.lastSpreadMm.ToString("F1"))
				.Append(" sim")
				.Append(CS$<>8__locals1.simList.Count)
				.Append('/')
				.Append(steps)
				.Append("  ");
		}

		// Token: 0x060000AB RID: 171 RVA: 0x000256FC File Offset: 0x000238FC
		private static void TaubinScalarChunk(int q0, int q1)
		{
			float[] rdA = SoftFleshController._tbRdS;
			float[] wrB = SoftFleshController._tbWrS;
			int[] act = SoftFleshController._tbAct;
			int[] adjS = SoftFleshController._tbAdjS;
			int[] adjL = SoftFleshController._tbAdjL;
			float lam = SoftFleshController._tbLam;
			for (int ai = q0; ai < q1; ai++)
			{
				int i = act[ai];
				int a0 = adjS[i];
				int a = adjS[i + 1];
				int c = a - a0;
				float sum = 0f;
				for (int j = a0; j < a; j++)
				{
					sum += rdA[adjL[j]];
				}
				wrB[i] = ((c > 0) ? (rdA[i] + lam * (sum / (float)c - rdA[i])) : rdA[i]);
			}
		}

		// Token: 0x060000AC RID: 172 RVA: 0x000257A4 File Offset: 0x000239A4
		private static void TaubinVectorChunk(int q0, int q1)
		{
			Vector3[] rdA = SoftFleshController._tbRdV;
			Vector3[] wrB = SoftFleshController._tbWrV;
			int[] act = SoftFleshController._tbAct;
			int[] adjS = SoftFleshController._tbAdjS;
			int[] adjL = SoftFleshController._tbAdjL;
			float lam = SoftFleshController._tbLam;
			for (int q2 = q0; q2 < q1; q2++)
			{
				int i = act[q2];
				int k0 = adjS[i];
				int k = adjS[i + 1];
				int c = k - k0;
				Vector3 sum = Vector3.zero;
				for (int j = k0; j < k; j++)
				{
					sum += rdA[adjL[j]];
				}
				wrB[i] = ((c > 0) ? (rdA[i] + (sum / (float)c - rdA[i]) * lam) : rdA[i]);
			}
		}

		// Token: 0x060000AD RID: 173 RVA: 0x00025878 File Offset: 0x00023A78
		private void SmoothRenderField(SoftFleshController.Body body)
		{
			int i = body.disp.Length;
			if (body.renderDisp == null || body.renderDisp.Length != i)
			{
				body.renderDisp = new float[i];
				body.rdBuf = new float[i];
				body.actStamp = new int[i];
			}
			float[] rd = body.renderDisp;
			Array.Copy(body.disp, rd, i);
			int num = body.stampCur + 1;
			body.stampCur = num;
			int stamp = num;
			List<int> act = body.actList;
			act.Clear();
			List<int> solved = body.simList;
			body.lastSim = solved.Count;
			SoftFleshController.LoadSim = solved.Count;
			for (int q = 0; q < solved.Count; q++)
			{
				int j = solved[q];
				if (rd[j] != 0f || body.pushB[j].sqrMagnitude >= 1E-10f || (body.corrB != null && body.corrB[j].sqrMagnitude >= 1E-10f))
				{
					if (body.actStamp[j] != stamp)
					{
						body.actStamp[j] = stamp;
						act.Add(j);
					}
					if (body.seamOf != null)
					{
						int sg2 = body.seamOf[j];
						if (sg2 >= 0)
						{
							foreach (int tw in body.seamGroups[sg2])
							{
								if (body.actStamp[tw] != stamp)
								{
									body.actStamp[tw] = stamp;
									act.Add(tw);
								}
							}
						}
					}
					int a0 = body.adjStart[j];
					int a = body.adjStart[j + 1];
					for (int l = a0; l < a; l++)
					{
						int m = body.adjList[l];
						if ((body.armSide[m] == 0 || rd[m] != 0f) && body.actStamp[m] != stamp)
						{
							body.actStamp[m] = stamp;
							act.Add(m);
						}
					}
				}
			}
			if (body.seamOf != null && this.seamRepair)
			{
				int q2 = 0;
				int cnt = act.Count;
				while (q2 < cnt)
				{
					int src = act[q2];
					int sg3 = body.seamOf[src];
					if (sg3 >= 0)
					{
						foreach (int tw2 in body.seamGroups[sg3])
						{
							if (body.armSide[tw2] == body.armSide[src] && body.actStamp[tw2] != stamp)
							{
								body.actStamp[tw2] = stamp;
								act.Add(tw2);
							}
						}
					}
					q2++;
				}
			}
			body.actCore = act.Count;
			int membMs = 0;
			if (this.TensionOf(body) > 0f && body.holdAge != null)
			{
				if (body.memb == null || body.memb.Length != i)
				{
					body.memb = new float[i];
					body.membSur = new float[i];
					body.membBuf = new float[i];
					body.membBuf2 = new float[i];
					body.membStamp = new int[i];
					body.membCStamp = new int[i];
					body.membRing = new int[i];
				}
				if (body.membN == null || body.membN.Length != i)
				{
					SoftFleshController.BuildMembNormals(body);
				}
				num = body.membStampCur + 1;
				body.membStampCur = num;
				membMs = num;
				List<int> ml2 = body.membList;
				ml2.Clear();
				int seeds = 0;
				int q3 = 0;
				int cnt2 = act.Count;
				while (q3 < cnt2)
				{
					int i2 = act[q3];
					if (body.holdAge[i2] != 0 && !body.noDent[i2])
					{
						body.membCStamp[i2] = membMs;
						body.membStamp[i2] = membMs;
						body.membRing[i2] = 0;
						ml2.Add(i2);
						seeds++;
					}
					q3++;
				}
				body.dgMembSeeds = seeds;
				if (seeds > 0)
				{
					float eAvg = ((body.avgEdge > 0.0001f) ? body.avgEdge : 0.01f);
					int rings = Mathf.Clamp(Mathf.CeilToInt(4f * this.skinTension / eAvg), 2, 30);
					int lo = 0;
					body.membRingsUsed = 0;
					int ring = 0;
					while (ring < rings && ml2.Count <= 3500)
					{
						body.membRingsUsed = ring + 1;
						int hi = ml2.Count;
						for (int q4 = lo; q4 < hi; q4++)
						{
							int i3 = ml2[q4];
							for (int k2 = body.adjStart[i3]; k2 < body.adjStart[i3 + 1]; k2++)
							{
								int j2 = body.adjList[k2];
								if (body.membStamp[j2] != membMs && !body.noDent[j2] && body.armSide[j2] == body.armSide[i3] && (!body.isFace || body.mouthIn == null || !body.mouthIn[j2]))
								{
									body.membStamp[j2] = membMs;
									body.membRing[j2] = ring + 1;
									ml2.Add(j2);
								}
							}
						}
						lo = hi;
						ring++;
					}
					if (body.seamOf != null)
					{
						int q5 = 0;
						int cnt3 = ml2.Count;
						while (q5 < cnt3)
						{
							int i4 = ml2[q5];
							int sg4 = body.seamOf[i4];
							if (sg4 >= 0)
							{
								foreach (int tw3 in body.seamGroups[sg4])
								{
									if (body.membStamp[tw3] != membMs && body.armSide[tw3] == body.armSide[i4])
									{
										body.membStamp[tw3] = membMs;
										body.membRing[tw3] = body.membRing[i4];
										ml2.Add(tw3);
									}
								}
							}
							q5++;
						}
					}
					for (int q6 = 0; q6 < ml2.Count; q6++)
					{
						int i5 = ml2[q6];
						if (body.actStamp[i5] != stamp)
						{
							body.actStamp[i5] = stamp;
							act.Add(i5);
						}
					}
				}
				body.dgMembN = ml2.Count;
			}
			else
			{
				body.dgMembN = 0;
				body.dgMembSeeds = 0;
			}
			body.lastAct = act.Count;
			SoftFleshController.LoadAct = act.Count;
			if (body.actArr == null || body.actArr.Length < act.Count)
			{
				body.actArr = new int[act.Count + 512];
			}
			act.CopyTo(body.actArr, 0);
			if (body.pushR == null || body.pushR.Length != i)
			{
				body.pushR = new Vector3[i];
			}
			Vector3[] pr = body.pushR;
			bool pushAny = false;
			for (int q7 = 0; q7 < act.Count; q7++)
			{
				int i6 = act[q7];
				Vector3 pv0 = body.pushB[i6];
				pr[i6] = pv0;
				if (!pushAny && (pv0.x != 0f || pv0.y != 0f || pv0.z != 0f))
				{
					pushAny = true;
				}
			}
			SoftFleshController.FieldPushSkipped = !pushAny;
			float sfExpP = ((this.smoothFactor <= 0.5f) ? this.smoothFactor : (0.5f + (this.smoothFactor - 0.5f) * 2f));
			int pIters = (pushAny ? Mathf.Clamp(Mathf.RoundToInt(sfExpP * 6f), 0, 21) : 0);
			Vector3[] pA = pr;
			Vector3[] pB = body.smoothBuf;
			for (int ai = 0; ai < act.Count; ai++)
			{
				int ib = act[ai];
				for (int k3 = body.adjStart[ib]; k3 < body.adjStart[ib + 1]; k3++)
				{
					int jb = body.adjList[k3];
					if (body.actStamp[jb] != stamp)
					{
						pB[jb] = pA[jb];
					}
				}
			}
			SoftFleshController._tbAct = body.actArr;
			SoftFleshController._tbAdjS = body.adjStart;
			SoftFleshController._tbAdjL = body.adjList;
			for (int pass = 0; pass < pIters * 2; pass++)
			{
				SoftFleshController._tbLam = (((pass & 1) == 0) ? 0.5f : (-0.53f));
				SoftFleshController._tbRdV = pA;
				SoftFleshController._tbWrV = pB;
				SoftFleshController.ParallelFor(act.Count, SoftFleshController._tbVectorD);
				Vector3[] sw = pA;
				pA = pB;
				pB = sw;
			}
			if (body.isFace && body.mouthPushValid && body.mouthW != null)
			{
				for (int q8 = 0; q8 < act.Count; q8++)
				{
					int i7 = act[q8];
					if (body.mouthW[i7] > 0f)
					{
						pA[i7] = body.pushB[i7];
					}
				}
			}
			float sfExp = ((this.smoothFactor <= 0.5f) ? this.smoothFactor : (0.5f + (this.smoothFactor - 0.5f) * 2f));
			int iters = Mathf.Clamp(Mathf.RoundToInt(sfExp * 16f), 0, 48);
			if (iters > 0)
			{
				float fcapF = (float)body.actCore / 40f;
				if (fcapF < 2f)
				{
					fcapF = 2f;
				}
				float itersF = (((float)iters < fcapF) ? ((float)iters) : fcapF);
				iters = (int)itersF;
				float itersFrac = itersF - (float)iters;
				body.dgItersF = itersF;
				body.dgAmpScale = 1f;
				float maxD = 0f;
				for (int ai2 = 0; ai2 < act.Count; ai2++)
				{
					float v0 = rd[act[ai2]];
					if (v0 < 0f)
					{
						v0 = -v0;
					}
					if (v0 > maxD)
					{
						maxD = v0;
					}
				}
				SoftFleshController.FieldDentSkipped = maxD <= 0f;
				int passes2 = ((maxD > 0f) ? (iters * 2) : 0);
				float[] sA = rd;
				float[] sB = body.rdBuf;
				for (int ai3 = 0; ai3 < act.Count; ai3++)
				{
					int ib2 = act[ai3];
					for (int k4 = body.adjStart[ib2]; k4 < body.adjStart[ib2 + 1]; k4++)
					{
						int jb2 = body.adjList[k4];
						if (body.actStamp[jb2] != stamp)
						{
							sB[jb2] = sA[jb2];
						}
					}
				}
				SoftFleshController._tbAct = body.actArr;
				SoftFleshController._tbAdjS = body.adjStart;
				SoftFleshController._tbAdjL = body.adjList;
				for (int pass2 = 0; pass2 < passes2; pass2++)
				{
					SoftFleshController._tbLam = (((pass2 & 1) == 0) ? 0.5f : (-0.53f));
					SoftFleshController._tbRdS = sA;
					SoftFleshController._tbWrS = sB;
					SoftFleshController.ParallelFor(act.Count, SoftFleshController._tbScalarD);
					float[] sw2 = sA;
					sA = sB;
					sB = sw2;
				}
				if (passes2 > 0 && itersFrac > 0.01f && body.rdFrac != null)
				{
					float[] keep = body.rdFrac;
					for (int ai4 = 0; ai4 < act.Count; ai4++)
					{
						int i8 = act[ai4];
						keep[i8] = sA[i8];
					}
					for (int pass3 = 0; pass3 < 2; pass3++)
					{
						SoftFleshController._tbLam = (((pass3 & 1) == 0) ? 0.5f : (-0.53f));
						SoftFleshController._tbRdS = sA;
						SoftFleshController._tbWrS = sB;
						SoftFleshController.ParallelFor(act.Count, SoftFleshController._tbScalarD);
						float[] sw3 = sA;
						sA = sB;
						sB = sw3;
					}
					for (int ai5 = 0; ai5 < act.Count; ai5++)
					{
						int i9 = act[ai5];
						sA[i9] = keep[i9] + (sA[i9] - keep[i9]) * itersFrac;
					}
				}
				float maxR = 0f;
				for (int ai6 = 0; ai6 < act.Count; ai6++)
				{
					float v = rd[act[ai6]];
					if (v < 0f)
					{
						v = -v;
					}
					if (v > maxR)
					{
						maxR = v;
					}
				}
				float scT = ((maxR > 1E-06f && maxD > maxR) ? (maxD / maxR) : 1f);
				if (scT > 2f)
				{
					scT = 2f;
				}
				float dtA = Time.deltaTime;
				if (dtA > 0.1f)
				{
					dtA = 0.1f;
				}
				float sc = ((body.ampScaleF > 0f) ? (body.ampScaleF + (scT - body.ampScaleF) * (1f - Mathf.Exp(-dtA / 0.15f))) : scT);
				body.ampScaleF = sc;
				body.dgAmpScale = sc;
				if (sc > 1.0005f)
				{
					for (int ai7 = 0; ai7 < act.Count; ai7++)
					{
						rd[act[ai7]] *= sc;
					}
				}
				if (body.tgtF != null)
				{
					for (int ai8 = 0; ai8 < act.Count; ai8++)
					{
						int i10 = act[ai8];
						float tg = body.tgtF[i10] * 0.9f;
						if (tg > 0f)
						{
							float sim = body.disp[i10];
							float lim = ((sim < tg) ? sim : tg);
							if (rd[i10] < lim)
							{
								rd[i10] = lim;
							}
						}
					}
				}
			}
			if (membMs != 0 && body.dgMembSeeds > 0)
			{
				long tM = SoftFleshController.SwNowNoMem();
				List<int> ml = body.membList;
				float[] m2 = body.memb;
				int[] ms = body.membStamp;
				int[] cs = body.membCStamp;
				int[] adjS = body.adjStart;
				int[] adjL = body.adjList;
				if (body.adjW == null || body.adjW.Length != adjL.Length)
				{
					body.adjW = new float[adjL.Length];
					body.adjWSum = new float[i];
					Vector3[] bv = body.baseVerts;
					for (int i11 = 0; i11 < i; i11++)
					{
						float ws = 0f;
						for (int k5 = adjS[i11]; k5 < adjS[i11 + 1]; k5++)
						{
							float dl = (bv[adjL[k5]] - bv[i11]).magnitude;
							if (dl < 0.0001f)
							{
								dl = 0.0001f;
							}
							float w = 1f / dl;
							body.adjW[k5] = w;
							ws += w;
						}
						body.adjWSum[i11] = ws;
					}
				}
				float[] aW = body.adjW;
				float[] aWS = body.adjWSum;
				float lam = this.skinTension;
				float inv4 = 1f / (4f * lam * lam);
				float eAvg2 = ((body.avgEdge > 0.0001f) ? body.avgEdge : 0.01f);
				int rings2 = ((body.membRingsUsed > 0) ? body.membRingsUsed : Mathf.Clamp(Mathf.CeilToInt(4f * lam / eAvg2), 2, 30));
				int sweepsMax = 8 + rings2 * 2;
				float res = 0f;
				int sweeps = 0;
				for (int sw4 = 0; sw4 < sweepsMax; sw4++)
				{
					bool fwd = (sw4 & 1) == 0;
					res = 0f;
					for (int qq = 0; qq < ml.Count; qq++)
					{
						int i12 = (fwd ? ml[qq] : ml[ml.Count - 1 - qq]);
						int a2 = adjS[i12];
						int a3 = adjS[i12 + 1];
						float sum = 0f;
						for (int k6 = a2; k6 < a3; k6++)
						{
							int j4 = adjL[k6];
							if (ms[j4] == membMs)
							{
								sum += aW[k6] * m2[j4];
							}
						}
						float mean = ((aWS[i12] > 0f) ? (sum / aWS[i12]) : 0f);
						float e = ((body.vertEdge != null && body.vertEdge[i12] > 1E-05f) ? body.vertEdge[i12] : eAvg2);
						int rg = body.membRing[i12];
						int half = rings2 - rings2 / 4;
						float tp = ((rg <= half) ? 1f : (1f - (float)(rg - half) / (float)(rings2 + 1 - half)));
						if (tp < 0.08f)
						{
							tp = 0.08f;
						}
						float s = 1f / (1f + e * e * inv4 / (tp * tp));
						float v2 = m2[i12] + 1.5f * (mean * s - m2[i12]);
						float c = rd[i12];
						if (c < 0f)
						{
							c = 0f;
						}
						if (v2 < c)
						{
							v2 = c;
						}
						float d = v2 - m2[i12];
						if (d < 0f)
						{
							d = -d;
						}
						if (d > res)
						{
							res = d;
						}
						m2[i12] = v2;
					}
					sweeps++;
					if (sw4 >= 1 && res < 2E-05f)
					{
						break;
					}
				}
				float[] mbA = body.membBuf;
				float[] mbB = body.membBuf2;
				for (int qq2 = 0; qq2 < ml.Count; qq2++)
				{
					int i13 = ml[qq2];
					mbA[i13] = m2[i13];
				}
				for (int bp = 0; bp < this.skinBend; bp++)
				{
					float[] rdA = mbA;
					float[] wrB = mbB;
					int stampB = membMs;
					SoftFleshController.ParallelFor(ml.Count, delegate(int _q0, int _q1)
					{
						for (int qq5 = _q0; qq5 < _q1; qq5++)
						{
							int i17 = ml[qq5];
							int a4 = adjS[i17];
							int a5 = adjS[i17 + 1];
							float sum2 = 0f;
							for (int k8 = a4; k8 < a5; k8++)
							{
								int j8 = adjL[k8];
								if (ms[j8] == stampB)
								{
									sum2 += aW[k8] * rdA[j8];
								}
							}
							float mean2 = ((aWS[i17] > 0f) ? (sum2 / aWS[i17]) : 0f);
							float v4 = 0.5f * rdA[i17] + 0.5f * mean2;
							float c3 = rd[i17];
							if (c3 < 0f)
							{
								c3 = 0f;
							}
							if (v4 < c3)
							{
								v4 = c3;
							}
							wrB[i17] = v4;
						}
					});
					float[] t2 = mbA;
					mbA = mbB;
					mbB = t2;
				}
				float[] mOut = mbA;
				float surMax = 0f;
				for (int qq3 = 0; qq3 < ml.Count; qq3++)
				{
					int i14 = ml[qq3];
					float c2 = rd[i14];
					if (c2 < 0f)
					{
						c2 = 0f;
					}
					float sur = mOut[i14] - c2;
					if (sur < 0f)
					{
						sur = 0f;
					}
					if (body.isFace && body.mouthW != null && i14 < body.mouthW.Length && body.mouthW[i14] > 0f)
					{
						sur *= 1f - body.mouthW[i14];
					}
					body.membSur[i14] = sur;
					if (sur > 0f)
					{
						rd[i14] += sur;
						if (sur > surMax)
						{
							surMax = sur;
						}
					}
				}
				float rip = 0f;
				for (int qq4 = 0; qq4 < ml.Count; qq4++)
				{
					int i15 = ml[qq4];
					for (int k7 = adjS[i15]; k7 < adjS[i15 + 1]; k7++)
					{
						int j5 = adjL[k7];
						if (ms[j5] != membMs)
						{
							float st = rd[i15] - rd[j5];
							if (st < 0f)
							{
								st = -st;
							}
							if (st > rip)
							{
								rip = st;
							}
						}
					}
				}
				body.dgMembSur = surMax;
				body.dgMembRip = rip;
				body.dgMembSweeps = sweeps;
				body.dgMembRes = res;
				body.dgMembMs = (float)((double)(SoftFleshController.SwNowNoMem() - tM) * 1000.0 / (double)Stopwatch.Frequency);
			}
			if (body.dgRdPrev == null || body.dgRdPrev.Length != i)
			{
				body.dgRdPrev = new float[i];
				body.dgDispPrevF = new float[i];
			}
			float jr = 0f;
			float js = 0f;
			float rmax = body.dgRdEver;
			for (int ai9 = 0; ai9 < act.Count; ai9++)
			{
				int i16 = act[ai9];
				float d2 = rd[i16] - body.dgRdPrev[i16];
				if (d2 < 0f)
				{
					d2 = -d2;
				}
				if (d2 > jr)
				{
					jr = d2;
				}
				float d3 = body.disp[i16] - body.dgDispPrevF[i16];
				if (d3 < 0f)
				{
					d3 = -d3;
				}
				if (d3 > js)
				{
					js = d3;
				}
				float ar = ((rd[i16] < 0f) ? (-rd[i16]) : rd[i16]);
				if (ar > rmax)
				{
					rmax = ar;
				}
			}
			body.dgJitR = jr;
			body.dgJitS = js;
			body.dgRdEver = rmax;
			Array.Copy(rd, body.dgRdPrev, i);
			Array.Copy(body.disp, body.dgDispPrevF, i);
			if (body.seamGroups != null && body.seamOf != null)
			{
				if (body.seamSwStamp == null || body.seamSwStamp.Length != body.seamGroups.Length)
				{
					body.seamSwStamp = new int[body.seamGroups.Length];
				}
				body.seamSwTick++;
				for (int ai10 = 0; ai10 < act.Count; ai10++)
				{
					int g = body.seamOf[act[ai10]];
					if (g >= 0 && body.seamSwStamp[g] != body.seamSwTick)
					{
						body.seamSwStamp[g] = body.seamSwTick;
						int[] grp5 = body.seamGroups[g];
						float avg = 0f;
						bool any = false;
						for (int j6 = 0; j6 < grp5.Length; j6++)
						{
							float v3 = rd[grp5[j6]];
							avg += v3;
							if (v3 != 0f)
							{
								any = true;
							}
						}
						if (any)
						{
							avg /= (float)grp5.Length;
							for (int j7 = 0; j7 < grp5.Length; j7++)
							{
								rd[grp5[j7]] = avg;
							}
						}
					}
				}
			}
		}

		// Token: 0x060000AE RID: 174 RVA: 0x00026E08 File Offset: 0x00025008
		private void RelaxFolds(SoftFleshController.Body body)
		{
			float amt = Mathf.Max(this.openTestWidth, this.openTestDepth);
			if (amt <= 0.02f || body.creaseMark == null)
			{
				return;
			}
			if (body.creaseTris == null)
			{
				int[] tri = ((body.workMesh != null) ? body.workMesh.triangles : null);
				if (tri == null)
				{
					return;
				}
				List<int> keep = new List<int>();
				int t = 0;
				while (t + 2 < tri.Length)
				{
					int a = tri[t];
					int b = tri[t + 1];
					int c = tri[t + 2];
					bool any = (body.vagW != null && (body.vagW[a] > 0.02f || body.vagW[b] > 0.02f || body.vagW[c] > 0.02f)) || (body.kokW != null && (body.kokW[a] > 0.02f || body.kokW[b] > 0.02f || body.kokW[c] > 0.02f));
					if (any)
					{
						keep.Add(a);
						keep.Add(b);
						keep.Add(c);
					}
					t += 3;
				}
				body.creaseTris = keep.ToArray();
			}
			int[] ct = body.creaseTris;
			if (ct.Length < 9)
			{
				return;
			}
			List<Vector3> outV = body.outV;
			List<int> dirty = body.dirty;
			int nf = ct.Length / 3;
			if (body.foldBuf == null || body.foldBuf.Length != outV.Count)
			{
				body.foldBuf = new Vector3[outV.Count];
			}
			Vector3[] fn = new Vector3[nf];
			for (int f = 0; f < nf; f++)
			{
				int a2 = ct[f * 3];
				int b2 = ct[f * 3 + 1];
				int c2 = ct[f * 3 + 2];
				fn[f] = Vector3.Cross(outV[b2] - outV[a2], outV[c2] - outV[a2]).normalized;
			}
			bool[] mark = body.creaseMark;
			Array.Clear(mark, 0, mark.Length);
			Dictionary<long, int> seen = new Dictionary<long, int>(ct.Length);
			int creases = 0;
			for (int f2 = 0; f2 < nf; f2++)
			{
				for (int e = 0; e < 3; e++)
				{
					int va = ct[f2 * 3 + e];
					int vb = ct[f2 * 3 + (e + 1) % 3];
					long i = ((va < vb) ? (((long)va << 32) | (long)((ulong)vb)) : (((long)vb << 32) | (long)((ulong)va)));
					int o;
					if (!seen.TryGetValue(i, out o))
					{
						seen[i] = f2;
					}
					else if (Vector3.Dot(fn[o], fn[f2]) <= 0.55f)
					{
						mark[va] = true;
						mark[vb] = true;
						creases++;
					}
				}
			}
			if (creases == 0)
			{
				return;
			}
			Vector3[] buf = body.foldBuf;
			int passes = Mathf.Clamp(Mathf.RoundToInt(amt * 5f), 1, 5);
			for (int pass = 0; pass < passes; pass++)
			{
				for (int j = 0; j < mark.Length; j++)
				{
					if (mark[j])
					{
						int a3 = body.adjStart[j];
						int a4 = body.adjStart[j + 1];
						if (a4 <= a3)
						{
							buf[j] = outV[j];
						}
						else
						{
							Vector3 sum = Vector3.zero;
							for (int k = a3; k < a4; k++)
							{
								sum += outV[body.adjList[k]];
							}
							buf[j] = Vector3.Lerp(outV[j], sum / (float)(a4 - a3), 0.6f * amt);
						}
					}
				}
				for (int l = 0; l < mark.Length; l++)
				{
					if (mark[l])
					{
						outV[l] = buf[l];
						dirty.Add(l);
					}
				}
			}
			this.creaseInfo = "opening creases relaxed: " + creases.ToString() + " edges";
		}

		// Token: 0x060000AF RID: 175 RVA: 0x00027214 File Offset: 0x00025414
		private static Vector3 ClampToAuthored(Vector3 n, Vector3 bn)
		{
			float d = n.x * bn.x + n.y * bn.y + n.z * bn.z;
			if (d >= 0.766f)
			{
				return n;
			}
			Vector3 perp;
			perp..ctor(n.x - bn.x * d, n.y - bn.y * d, n.z - bn.z * d);
			float pl = perp.magnitude;
			if (pl < 1E-06f)
			{
				return bn;
			}
			float i = 0.643f / pl;
			return new Vector3(bn.x * 0.766f + perp.x * i, bn.y * 0.766f + perp.y * i, bn.z * 0.766f + perp.z * i);
		}

		// Token: 0x060000B0 RID: 176 RVA: 0x000272E8 File Offset: 0x000254E8
		private void SmoothOpeningNormals(SoftFleshController.Body body)
		{
			Vector3[] pr = ((body.pushR != null) ? body.pushR : body.pushB);
			if (pr == null || body.workMesh == null)
			{
				return;
			}
			if (body.bakeVerts == null)
			{
				return;
			}
			if (!body.creaseBuilt || body.creaseTris == null)
			{
				body.creaseBuilt = true;
				int[] tri = body.workMesh.triangles;
				if (tri == null)
				{
					return;
				}
				bool[] inSup2 = new bool[body.baseVerts.Length];
				if (body.bakeVerts != null)
				{
					for (int q2 = 0; q2 < body.bakeVerts.Length; q2++)
					{
						inSup2[body.bakeVerts[q2]] = true;
					}
				}
				List<int> keep = new List<int>();
				int t = 0;
				while (t + 2 < tri.Length)
				{
					int a = tri[t];
					int b = tri[t + 1];
					int c = tri[t + 2];
					if (inSup2[a] || inSup2[b] || inSup2[c])
					{
						keep.Add(a);
						keep.Add(b);
						keep.Add(c);
					}
					t += 3;
				}
				body.creaseTris = keep.ToArray();
				SoftFleshPlugin.Log("[SF] opening-normal region built: " + (body.creaseTris.Length / 3).ToString() + " tris");
			}
			int[] ct = body.creaseTris;
			if (ct.Length < 9 || body.lastOpenMm < 1f || !this.openNormals)
			{
				return;
			}
			if (body.creaseVerts == null || body.creaseVerts.Length == 0)
			{
				bool[] seenV = new bool[body.baseVerts.Length];
				List<int> uniq = new List<int>(ct.Length / 4);
				foreach (int vi in ct)
				{
					if (!seenV[vi])
					{
						seenV[vi] = true;
						uniq.Add(vi);
					}
				}
				body.creaseVerts = uniq.ToArray();
				SoftFleshPlugin.Log(string.Concat(new string[]
				{
					"[SF] opening-normal region: ",
					(ct.Length / 3).ToString(),
					" tris, ",
					body.creaseVerts.Length.ToString(),
					" unique verts"
				}));
			}
			int[] cv = body.creaseVerts;
			if (body.foldVert == null || body.foldVert.Length != body.baseVerts.Length)
			{
				body.foldVert = new bool[body.baseVerts.Length];
				int nf = 0;
				if (body.seamOf != null)
				{
					for (int i2 = 0; i2 < body.foldVert.Length; i2++)
					{
						int g2 = body.seamOf[i2];
						if (g2 >= 0 && (body.seamAligned == null || !body.seamAligned[g2]))
						{
							body.foldVert[i2] = true;
							nf++;
						}
					}
				}
				SoftFleshPlugin.Log("[SF] opening normals: " + nf.ToString() + " fold verts excluded (they keep their authored normal)");
			}
			bool[] fold = body.foldVert;
			List<Vector3> outV = body.outV;
			List<Vector3> nl = body.normList;
			List<int> dirty = body.dirty;
			bool[] mark = body.creaseMark;
			Array.Clear(mark, 0, mark.Length);
			Vector3[] acc = body.smoothNBuf;
			for (int f = 0; f < cv.Length; f++)
			{
				acc[cv[f]] = Vector3.zero;
			}
			for (int f2 = 0; f2 < ct.Length; f2 += 3)
			{
				int a2 = ct[f2];
				int b2 = ct[f2 + 1];
				int c2 = ct[f2 + 2];
				if (!fold[a2] && !fold[b2] && !fold[c2])
				{
					Vector3 fn = Vector3.Cross(outV[b2] - outV[a2], outV[c2] - outV[a2]);
					Vector3 bn0 = body.baseNormals[a2] + body.baseNormals[b2] + body.baseNormals[c2];
					if (Vector3.Dot(fn, bn0) < 0f)
					{
						fn = -fn;
					}
					acc[a2] += fn;
					acc[b2] += fn;
					acc[c2] += fn;
					mark[a2] = true;
					mark[b2] = true;
					mark[c2] = true;
				}
			}
			foreach (int j in cv)
			{
				if (mark[j])
				{
					mark[j] = false;
					if (pr[j].sqrMagnitude >= 1E-06f)
					{
						float pm = pr[j].magnitude;
						float w = Mathf.Clamp01((pm - 0.001f) / 0.005f);
						if (w > 0f)
						{
							Vector3 gn = acc[j];
							if (gn.sqrMagnitude >= 1E-14f)
							{
								if (Vector3.Dot(gn, body.baseNormals[j]) < 0f)
								{
									gn = -gn;
								}
								gn = SoftFleshController.ClampToAuthored(gn.normalized, body.baseNormals[j]);
								nl[j] = Vector3.Lerp(nl[j], gn, w).normalized;
								acc[j] = nl[j];
								mark[j] = true;
								dirty.Add(j);
							}
						}
					}
				}
			}
			for (int pass = 0; pass < 3; pass++)
			{
				int[] adjS3 = body.adjStart;
				int[] adjL3 = body.adjList;
				int[] cvL = cv;
				List<Vector3> nlL = nl;
				Vector3[] accL = acc;
				bool[] markL = mark;
				bool[] foldL = fold;
				SoftFleshController.ParallelFor(cvL.Length, delegate(int f0, int f1)
				{
					for (int f6 = f0; f6 < f1; f6++)
					{
						int i3 = cvL[f6];
						if (markL[i3])
						{
							Vector3 sum = nlL[i3];
							int a3 = adjS3[i3];
							int a4 = adjS3[i3 + 1];
							for (int k2 = a3; k2 < a4; k2++)
							{
								int j2 = adjL3[k2];
								if (!foldL[j2])
								{
									sum += nlL[j2];
								}
							}
							if (sum.sqrMagnitude > 1E-12f)
							{
								accL[i3] = sum.normalized;
							}
						}
					}
				});
				SoftFleshController.ParallelFor(cvL.Length, delegate(int f0, int f1)
				{
					for (int f7 = f0; f7 < f1; f7++)
					{
						int i4 = cvL[f7];
						if (markL[i4])
						{
							nlL[i4] = accL[i4];
						}
					}
				});
			}
			foreach (int k in cv)
			{
				if (mark[k])
				{
					nl[k] = SoftFleshController.ClampToAuthored(nl[k], body.baseNormals[k]);
				}
			}
			if (body.seamGroups != null && body.seamOf != null)
			{
				foreach (int l in cv)
				{
					if (mark[l])
					{
						int g3 = body.seamOf[l];
						if (g3 >= 0 && (body.seamAligned == null || body.seamAligned[g3]))
						{
							int[] grp = body.seamGroups[g3];
							Vector3 avg = Vector3.zero;
							for (int m = 0; m < grp.Length; m++)
							{
								avg += nl[grp[m]];
							}
							if (avg.sqrMagnitude > 1E-12f)
							{
								avg.Normalize();
								for (int n = 0; n < grp.Length; n++)
								{
									nl[grp[n]] = avg;
									dirty.Add(grp[n]);
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x060000B1 RID: 177 RVA: 0x00027A04 File Offset: 0x00025C04
		private void DentTrackRecord(SoftFleshController.Body body, int bi)
		{
			if (body.isFace || body.actList == null || body.renderDisp == null || body.disp == null)
			{
				return;
			}
			if (this._dtLastFrame == Time.frameCount)
			{
				return;
			}
			List<int> act = body.actList;
			float[] rd = body.renderDisp;
			float[] sd = body.disp;
			Vector3[] bv = body.baseVerts;
			float sr = 0f;
			float ss = 0f;
			float pr = 0f;
			float ps = 0f;
			int ir = -1;
			int isv = -1;
			Vector3 cr = Vector3.zero;
			Vector3 cs = Vector3.zero;
			for (int q = 0; q < act.Count; q++)
			{
				int i = act[q];
				float r = ((rd[i] < 0f) ? (-rd[i]) : rd[i]);
				float s2 = ((sd[i] < 0f) ? (-sd[i]) : sd[i]);
				cr += bv[i] * r;
				sr += r;
				cs += bv[i] * s2;
				ss += s2;
				if (r > pr)
				{
					pr = r;
					ir = i;
				}
				if (s2 > ps)
				{
					ps = s2;
					isv = i;
				}
			}
			if (sr < 1E-06f || ss < 1E-06f)
			{
				this._dtHave = false;
				return;
			}
			cr /= sr;
			cs /= ss;
			int p = body.dgProfP;
			Vector3 tip = ((p >= 0 && this._segA != null && p < this._segA.Length) ? (this._segA[p] + this._segAB[p] * 0.5f) : Vector3.zero);
			this._dtLastFrame = Time.frameCount;
			int h = this._dtHead;
			this._dtHead = (this._dtHead + 1) % 360;
			if (this._dtCount < 360)
			{
				this._dtCount++;
			}
			this._dtTime[h] = Time.time;
			this._dtDt[h] = Time.deltaTime;
			this._dtBody[h] = bi;
			this._dtTip[h] = (this._dtHave ? (tip - this._dtTipPrev).magnitude : 0f);
			this._dtCenS[h] = (this._dtHave ? (cs - this._dtCenSPrev).magnitude : 0f);
			this._dtCenR[h] = (this._dtHave ? (cr - this._dtCenRPrev).magnitude : 0f);
			this._dtPkS[h] = ps;
			this._dtPkR[h] = pr;
			this._dtVS[h] = isv;
			this._dtVR[h] = ir;
			this._dtCore[h] = body.actCore;
			this._dtIters[h] = body.dgItersF;
			this._dtAmp[h] = body.dgAmpScale;
			this._dtSteps[h] = body.lastSteps;
			this._dtIn[h] = body.dgInsideN;
			this._dtLatch[h] = body.dgSkipLatch;
			this._dtJump[h] = body.dgJumpN;
			this._dtOnOff[h] = body.dgOnOffN;
			this._dtEdge[h] = ((body.vertEdge != null && ir >= 0 && ir < body.vertEdge.Length) ? body.vertEdge[ir] : body.avgEdge);
			this._dtTipPrev = tip;
			this._dtCenSPrev = cs;
			this._dtCenRPrev = cr;
			this._dtHave = true;
		}

		// Token: 0x060000B2 RID: 178 RVA: 0x00027DA8 File Offset: 0x00025FA8
		private void WriteDentTrack(string dir)
		{
			if (this._dtCount == 0)
			{
				return;
			}
			StringBuilder sb = new StringBuilder(32768);
			sb.Append("# touchRadius_mm ").Append((this.touchRadius * 1000f).ToString("F1")).Append("  smooth ")
				.Append(this.smoothFactor.ToString("F2"))
				.Append("  shadow ")
				.Append(this.normalBoost.ToString("F1"))
				.Append("  shadeTau_ms ")
				.Append((this.shadeTau * 1000f).ToString("F0"))
				.Append('\n');
			sb.Append("t,dt_ms,body,tip_mm,cenSim_mm,cenRender_mm,peakSim_mm,peakRender_mm,vSim,vRender,edgeAtPeak_mm,actCore,itersF,ampScale,substeps,inside,latch,tgtJumps,tgtOnOff\n");
			int start = (this._dtHead - this._dtCount + 360) % 360;
			for (int i = 0; i < this._dtCount; i++)
			{
				int h = (start + i) % 360;
				sb.Append(this._dtTime[h].ToString("F3")).Append(',').Append((this._dtDt[h] * 1000f).ToString("F1"))
					.Append(',')
					.Append(this._dtBody[h])
					.Append(',')
					.Append((this._dtTip[h] * 1000f).ToString("F2"))
					.Append(',')
					.Append((this._dtCenS[h] * 1000f).ToString("F2"))
					.Append(',')
					.Append((this._dtCenR[h] * 1000f).ToString("F2"))
					.Append(',')
					.Append((this._dtPkS[h] * 1000f).ToString("F2"))
					.Append(',')
					.Append((this._dtPkR[h] * 1000f).ToString("F2"))
					.Append(',')
					.Append(this._dtVS[h])
					.Append(',')
					.Append(this._dtVR[h])
					.Append(',')
					.Append((this._dtEdge[h] * 1000f).ToString("F1"))
					.Append(',')
					.Append(this._dtCore[h])
					.Append(',')
					.Append(this._dtIters[h].ToString("F2"))
					.Append(',')
					.Append(this._dtAmp[h].ToString("F3"))
					.Append(',')
					.Append(this._dtSteps[h])
					.Append(',')
					.Append(this._dtIn[h])
					.Append(',')
					.Append(this._dtLatch[h])
					.Append(',')
					.Append(this._dtJump[h])
					.Append(',')
					.Append(this._dtOnOff[h])
					.Append('\n');
			}
			File.WriteAllText(Path.Combine(dir, "dent_track.csv"), sb.ToString());
		}

		// Token: 0x060000B3 RID: 179 RVA: 0x00028100 File Offset: 0x00026300
		private void TemporalNormals(SoftFleshController.Body body, bool haveActive)
		{
			if (body.nTilt == null || body.normList == null)
			{
				return;
			}
			List<Vector3> nl = body.normList;
			Vector3[] baseN = body.baseNormals;
			Vector3[] tilt = body.nTilt;
			int[] seen = body.nTiltSeen;
			List<int> live = body.nTiltLive;
			bool[] inLive = body.nTiltIn;
			List<int> dirty = body.dirty;
			if (nl.Count != baseN.Length)
			{
				return;
			}
			float dtN = Time.deltaTime;
			if (dtN > 0.1f)
			{
				dtN = 0.1f;
			}
			bool off = this.shadeTau <= 0.001f;
			float a = (off ? 1f : (1f - Mathf.Exp(-dtN / this.shadeTau)));
			int num = body.nTiltFrame + 1;
			body.nTiltFrame = num;
			int fr = num;
			if (haveActive && body.actList != null)
			{
				List<int> act = body.actList;
				for (int q = 0; q < act.Count; q++)
				{
					int i = act[q];
					seen[i] = fr;
					Vector3 target = nl[i] - baseN[i];
					Vector3 f = tilt[i] + (target - tilt[i]) * a;
					tilt[i] = f;
					Vector3 v = baseN[i] + f;
					float j = v.magnitude;
					if (j > 1E-06f)
					{
						nl[i] = v / j;
					}
					if (!inLive[i])
					{
						inLive[i] = true;
						live.Add(i);
					}
				}
			}
			for (int q2 = live.Count - 1; q2 >= 0; q2--)
			{
				int k = live[q2];
				if (seen[k] != fr)
				{
					Vector3 f2 = (off ? Vector3.zero : (tilt[k] * (1f - a)));
					if (f2.sqrMagnitude < 1E-07f)
					{
						tilt[k] = Vector3.zero;
						inLive[k] = false;
						live[q2] = live[live.Count - 1];
						live.RemoveAt(live.Count - 1);
						nl[k] = baseN[k];
					}
					else
					{
						tilt[k] = f2;
						Vector3 v2 = baseN[k] + f2;
						float l = v2.magnitude;
						if (l > 1E-06f)
						{
							nl[k] = v2 / l;
						}
						dirty.Add(k);
					}
				}
			}
		}

		// Token: 0x060000B4 RID: 180 RVA: 0x00028390 File Offset: 0x00026590
		private void ComputeDentNormals(SoftFleshController.Body body)
		{
			int i = body.baseVerts.Length;
			List<Vector3> nl = body.normList;
			Vector3[] baseN = body.baseNormals;
			Vector3[] bv = body.baseVerts;
			float[] rd = ((body.renderDisp != null) ? body.renderDisp : body.disp);
			if (nl.Count != i)
			{
				nl.Clear();
				for (int j = 0; j < i; j++)
				{
					nl.Add(baseN[j]);
				}
			}
			List<int> act = body.actList;
			List<int> dirty = body.dirty;
			for (int q = 0; q < act.Count; q++)
			{
				dirty.Add(act[q]);
			}
			Vector3[] pRef = ((body.pushR != null) ? body.pushR : body.pushB);
			bool useSur = this.TensionOf(body) > 0f && body.membSur != null && body.membStamp != null;
			float[] surA = body.membSur;
			int[] surStamp = body.membStamp;
			int surCur = body.membStampCur;
			float boost = this.normalBoost;
			SoftFleshController.ParallelFor(act.Count, delegate(int _q0, int _q1)
			{
				for (int q5 = _q0; q5 < _q1; q5++)
				{
					int i2 = act[q5];
					float si = ((useSur && surStamp[i2] == surCur) ? surA[i2] : 0f);
					float di = rd[i2] - Vector3.Dot(pRef[i2], baseN[i2]) - si;
					int a2 = body.adjStart[i2];
					int a3 = body.adjStart[i2 + 1];
					Vector3 N = baseN[i2];
					Vector3 grad = Vector3.zero;
					Vector3 gradS = Vector3.zero;
					int c = 0;
					for (int k2 = a2; k2 < a3; k2++)
					{
						int j8 = body.adjList[k2];
						Vector3 e = bv[j8] - bv[i2];
						Vector3 eT = e - Vector3.Dot(e, N) * N;
						float l2 = eT.sqrMagnitude;
						if (l2 > 1E-10f)
						{
							float sj = ((useSur && surStamp[j8] == surCur) ? surA[j8] : 0f);
							float dj = rd[j8] - Vector3.Dot(pRef[j8], baseN[j8]) - sj;
							grad += eT * ((dj - di) / l2);
							gradS += eT * ((sj - si) / l2);
							c++;
						}
					}
					if (c > 0)
					{
						grad = (grad * boost + gradS) * (2f / (float)c);
					}
					Vector3 nv = N + grad;
					float m5 = nv.magnitude;
					nl[i2] = ((m5 > 1E-06f) ? (nv / m5) : N);
				}
			});
			float sf = ((this.smoothShade >= 0f) ? this.smoothShade : this.smoothFactor);
			int passes = Mathf.Clamp(Mathf.RoundToInt(sf * 8f), 0, 16);
			float passFrac = 0f;
			float fcapF = (float)body.actCore / 80f;
			if (fcapF < 1f)
			{
				fcapF = 1f;
			}
			if ((float)passes > fcapF)
			{
				passes = (int)fcapF;
				passFrac = fcapF - (float)passes;
			}
			Vector3[] buf = body.smoothNBuf;
			Action<int, int> <>9__1;
			Action<int, int> <>9__2;
			for (int pass = 0; pass < passes; pass++)
			{
				int count = act.Count;
				Action<int, int> action;
				if ((action = <>9__1) == null)
				{
					action = (<>9__1 = delegate(int _q0, int _q1)
					{
						for (int q6 = _q0; q6 < _q1; q6++)
						{
							int i3 = act[q6];
							Vector3 sum2 = nl[i3];
							int a4 = body.adjStart[i3];
							int a5 = body.adjStart[i3 + 1];
							for (int k3 = a4; k3 < a5; k3++)
							{
								sum2 += nl[body.adjList[k3]];
							}
							float m6 = sum2.magnitude;
							buf[i3] = ((m6 > 1E-06f) ? (sum2 / m6) : nl[i3]);
						}
					});
				}
				SoftFleshController.ParallelFor(count, action);
				int count2 = act.Count;
				Action<int, int> action2;
				if ((action2 = <>9__2) == null)
				{
					action2 = (<>9__2 = delegate(int _q0, int _q1)
					{
						for (int q7 = _q0; q7 < _q1; q7++)
						{
							int i4 = act[q7];
							nl[i4] = buf[i4];
						}
					});
				}
				SoftFleshController.ParallelFor(count2, action2);
			}
			if (passFrac > 0.01f)
			{
				for (int q2 = 0; q2 < act.Count; q2++)
				{
					int k = act[q2];
					Vector3 sum = nl[k];
					int a0 = body.adjStart[k];
					int a = body.adjStart[k + 1];
					for (int l = a0; l < a; l++)
					{
						sum += nl[body.adjList[l]];
					}
					float m = sum.magnitude;
					buf[k] = ((m > 1E-06f) ? (sum / m) : nl[k]);
				}
				for (int q3 = 0; q3 < act.Count; q3++)
				{
					int n = act[q3];
					Vector3 v = nl[n] + (buf[n] - nl[n]) * passFrac;
					float m2 = v.magnitude;
					if (m2 > 1E-06f)
					{
						nl[n] = v / m2;
					}
				}
			}
			if (body.seamGroups != null && body.seamOf != null)
			{
				for (int q4 = 0; q4 < act.Count; q4++)
				{
					int g = body.seamOf[act[q4]];
					if (g >= 0 && (body.seamAligned == null || body.seamAligned[g]))
					{
						int[] grp = body.seamGroups[g];
						bool anyA = false;
						for (int j2 = 0; j2 < grp.Length; j2++)
						{
							if (rd[grp[j2]] != 0f)
							{
								anyA = true;
								break;
							}
						}
						if (anyA)
						{
							for (int j3 = 0; j3 < grp.Length; j3++)
							{
								dirty.Add(grp[j3]);
							}
							if (body.seamNSync == null || body.seamNSync[g])
							{
								Vector3 avg = Vector3.zero;
								for (int j4 = 0; j4 < grp.Length; j4++)
								{
									avg += nl[grp[j4]];
								}
								float m3 = avg.magnitude;
								if (m3 > 1E-06f)
								{
									avg /= m3;
									for (int j5 = 0; j5 < grp.Length; j5++)
									{
										nl[grp[j5]] = avg;
									}
								}
							}
							else
							{
								Vector3 dAvg = Vector3.zero;
								for (int j6 = 0; j6 < grp.Length; j6++)
								{
									dAvg += nl[grp[j6]] - baseN[grp[j6]];
								}
								dAvg /= (float)grp.Length;
								for (int j7 = 0; j7 < grp.Length; j7++)
								{
									Vector3 v2 = baseN[grp[j7]] + dAvg;
									float m4 = v2.magnitude;
									if (m4 > 1E-06f)
									{
										nl[grp[j7]] = v2 / m4;
									}
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x060000B5 RID: 181 RVA: 0x000289E8 File Offset: 0x00026BE8
		private static float[] AuthoredShapeWeights(SkinnedMeshRenderer r, int n)
		{
			if (n <= 0)
			{
				return null;
			}
			float[] live = new float[n];
			bool anyLive = false;
			for (int i = 0; i < n; i++)
			{
				live[i] = r.GetBlendShapeWeight(i);
				if (live[i] != 0f)
				{
					anyLive = true;
				}
			}
			float[] cached;
			if (!SoftFleshController._shapeW0.TryGetValue(r, out cached) || cached == null || cached.Length != n)
			{
				SoftFleshController._shapeW0[r] = live;
				return live;
			}
			bool anyCached = false;
			for (int j = 0; j < n; j++)
			{
				if (cached[j] != 0f)
				{
					anyCached = true;
					break;
				}
			}
			if (anyCached && !anyLive)
			{
				return cached;
			}
			if (anyLive)
			{
				SoftFleshController._shapeW0[r] = live;
				return live;
			}
			return cached;
		}

		// Token: 0x060000B6 RID: 182 RVA: 0x00028A8C File Offset: 0x00026C8C
		private static Quaternion RotFromBindpose(Matrix4x4 bp)
		{
			Vector3 f = bp.GetColumn(2);
			Vector3 u = bp.GetColumn(1);
			if (f.sqrMagnitude < 1E-10f || u.sqrMagnitude < 1E-10f)
			{
				return Quaternion.identity;
			}
			return Quaternion.LookRotation(f, u);
		}

		// Token: 0x060000B7 RID: 183 RVA: 0x00028AE0 File Offset: 0x00026CE0
		private static Vector3 ClothClosestOnTri(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
		{
			Vector3 ab = b - a;
			Vector3 ac = c - a;
			Vector3 ap = p - a;
			float d = Vector3.Dot(ab, ap);
			float d2 = Vector3.Dot(ac, ap);
			if (d <= 0f && d2 <= 0f)
			{
				bary = new Vector3(1f, 0f, 0f);
				return a;
			}
			Vector3 bp2 = p - b;
			float d3 = Vector3.Dot(ab, bp2);
			float d4 = Vector3.Dot(ac, bp2);
			if (d3 >= 0f && d4 <= d3)
			{
				bary = new Vector3(0f, 1f, 0f);
				return b;
			}
			float vc = d * d4 - d3 * d2;
			if (vc <= 0f && d >= 0f && d3 <= 0f)
			{
				float t = d / (d - d3);
				bary = new Vector3(1f - t, t, 0f);
				return a + ab * t;
			}
			Vector3 cp2 = p - c;
			float d5 = Vector3.Dot(ab, cp2);
			float d6 = Vector3.Dot(ac, cp2);
			if (d6 >= 0f && d5 <= d6)
			{
				bary = new Vector3(0f, 0f, 1f);
				return c;
			}
			float vb = d5 * d2 - d * d6;
			if (vb <= 0f && d2 >= 0f && d6 <= 0f)
			{
				float t2 = d2 / (d2 - d6);
				bary = new Vector3(1f - t2, 0f, t2);
				return a + ac * t2;
			}
			float va = d3 * d6 - d5 * d4;
			if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
			{
				float t3 = (d4 - d3) / (d4 - d3 + (d5 - d6));
				bary = new Vector3(0f, 1f - t3, t3);
				return b + (c - b) * t3;
			}
			float den = 1f / (va + vb + vc);
			float v = vb * den;
			float w = vc * den;
			bary = new Vector3(1f - v - w, v, w);
			return a + ab * v + ac * w;
		}

		// Token: 0x060000B8 RID: 184 RVA: 0x00028D50 File Offset: 0x00026F50
		private static bool[] CanonFaces(Vector3[] pos, Mesh m, out int dropped)
		{
			dropped = 0;
			int[] wid = new int[pos.Length];
			Dictionary<long, int> wmap = new Dictionary<long, int>(pos.Length);
			for (int i = 0; i < pos.Length; i++)
			{
				long kx = (long)Mathf.Round(pos[i].x * 10000f);
				long ky = (long)Mathf.Round(pos[i].y * 10000f);
				long kz = (long)Mathf.Round(pos[i].z * 10000f);
				long kk = (kx & 2097151L) | ((ky & 2097151L) << 21) | ((kz & 2097151L) << 42);
				int w;
				if (!wmap.TryGetValue(kk, out w))
				{
					w = wmap.Count;
					wmap.Add(kk, w);
				}
				wid[i] = w;
			}
			List<int> flat = new List<int>();
			for (int sm = 0; sm < m.subMeshCount; sm++)
			{
				int[] tt = m.GetTriangles(sm);
				for (int q = 0; q < tt.Length; q++)
				{
					flat.Add(tt[q]);
				}
			}
			int nF = flat.Count / 3;
			bool[] keep = new bool[nF];
			HashSet<long> seen = new HashSet<long>();
			for (int f = 0; f < nF; f++)
			{
				int a2 = wid[flat[f * 3]];
				int b2 = wid[flat[f * 3 + 1]];
				int c2 = wid[flat[f * 3 + 2]];
				if (a2 > b2)
				{
					int sw = a2;
					a2 = b2;
					b2 = sw;
				}
				if (b2 > c2)
				{
					int sw = b2;
					b2 = c2;
					c2 = sw;
				}
				if (a2 > b2)
				{
					int sw = a2;
					a2 = b2;
					b2 = sw;
				}
				long key = ((long)(a2 & 2097151) << 42) | ((long)(b2 & 2097151) << 21) | (long)(c2 & 2097151);
				if (seen.Add(key))
				{
					keep[f] = true;
				}
				else
				{
					keep[f] = false;
					dropped++;
				}
			}
			return keep;
		}

		// Token: 0x060000B9 RID: 185 RVA: 0x00028F40 File Offset: 0x00027140
		private static bool ClothBaryExtrap(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
		{
			Vector3 ab = b - a;
			Vector3 ac = c - a;
			Vector3 ap = p - a;
			float d0 = Vector3.Dot(ab, ab);
			float d = Vector3.Dot(ab, ac);
			float d2 = Vector3.Dot(ac, ac);
			float d3 = Vector3.Dot(ap, ab);
			float d4 = Vector3.Dot(ap, ac);
			float den = d0 * d2 - d * d;
			if (den > -1E-14f && den < 1E-14f)
			{
				bary = new Vector3(1f, 0f, 0f);
				return false;
			}
			float v = (d2 * d3 - d * d4) / den;
			float w = (d0 * d4 - d * d3) / den;
			float u = 1f - v - w;
			if (u < -0.6f || u > 1.6f || v < -0.6f || v > 1.6f || w < -0.6f || w > 1.6f)
			{
				bary = new Vector3(1f, 0f, 0f);
				return false;
			}
			bary = new Vector3(u, v, w);
			return true;
		}

		// Token: 0x060000BA RID: 186 RVA: 0x00029060 File Offset: 0x00027260
		private static Vector3[] ClothSkin(Vector3[] verts, Transform[] bones, Matrix4x4[] binds, BoneWeight[] bw)
		{
			if (verts == null || bones == null || binds == null || bw == null)
			{
				return null;
			}
			if (bw.Length != verts.Length)
			{
				return null;
			}
			int nm = ((binds.Length < bones.Length) ? binds.Length : bones.Length);
			if (nm == 0)
			{
				return null;
			}
			Matrix4x4[] i = new Matrix4x4[nm];
			for (int j = 0; j < nm; j++)
			{
				i[j] = ((bones[j] != null) ? (bones[j].localToWorldMatrix * binds[j]) : Matrix4x4.identity);
			}
			Vector3[] outV = new Vector3[verts.Length];
			for (int k = 0; k < verts.Length; k++)
			{
				BoneWeight w = bw[k];
				Vector3 acc = Vector3.zero;
				float used = 0f;
				if (w.weight0 > 0f && w.boneIndex0 < nm)
				{
					acc += i[w.boneIndex0].MultiplyPoint3x4(verts[k]) * w.weight0;
					used += w.weight0;
				}
				if (w.weight1 > 0f && w.boneIndex1 < nm)
				{
					acc += i[w.boneIndex1].MultiplyPoint3x4(verts[k]) * w.weight1;
					used += w.weight1;
				}
				if (w.weight2 > 0f && w.boneIndex2 < nm)
				{
					acc += i[w.boneIndex2].MultiplyPoint3x4(verts[k]) * w.weight2;
					used += w.weight2;
				}
				if (w.weight3 > 0f && w.boneIndex3 < nm)
				{
					acc += i[w.boneIndex3].MultiplyPoint3x4(verts[k]) * w.weight3;
					used += w.weight3;
				}
				if (used < 0.999f && used > 0.0001f)
				{
					acc /= used;
				}
				else if (used <= 0.0001f)
				{
					return null;
				}
				outV[k] = acc;
			}
			return outV;
		}

		// Token: 0x060000BB RID: 187 RVA: 0x00029298 File Offset: 0x00027498
		private static Dictionary<long, List<int>> ClothHash(Vector3[] pts, bool[] skip, float cell)
		{
			Dictionary<long, List<int>> h = new Dictionary<long, List<int>>(4096);
			for (int i = 0; i < pts.Length; i++)
			{
				if (skip == null || !skip[i])
				{
					long cx = (long)Mathf.Floor(pts[i].x / cell);
					long cy = (long)Mathf.Floor(pts[i].y / cell);
					long cz = (long)Mathf.Floor(pts[i].z / cell);
					long key = (cx & 2097151L) | ((cy & 2097151L) << 21) | ((cz & 2097151L) << 42);
					List<int> c;
					if (!h.TryGetValue(key, out c))
					{
						c = new List<int>(8);
						h[key] = c;
					}
					c.Add(i);
				}
			}
			return h;
		}

		// Token: 0x060000BC RID: 188 RVA: 0x0002935C File Offset: 0x0002755C
		private static float ClothNearFrac(Vector3[] cv, Dictionary<long, List<int>> h, Vector3[] bp, float cell, float maxD2)
		{
			if (cv == null || h == null)
			{
				return -1f;
			}
			int hit = 0;
			int tot = 0;
			for (int i = 0; i < cv.Length; i += 4)
			{
				tot++;
				long cx = (long)Mathf.Floor(cv[i].x / cell);
				long cy = (long)Mathf.Floor(cv[i].y / cell);
				long cz = (long)Mathf.Floor(cv[i].z / cell);
				bool near = false;
				long dx = -1L;
				while (dx <= 1L && !near)
				{
					long dy = -1L;
					while (dy <= 1L && !near)
					{
						long dz = -1L;
						while (dz <= 1L && !near)
						{
							long key = ((cx + dx) & 2097151L) | (((cy + dy) & 2097151L) << 21) | (((cz + dz) & 2097151L) << 42);
							List<int> c;
							if (h.TryGetValue(key, out c))
							{
								for (int j = 0; j < c.Count; j++)
								{
									if ((bp[c[j]] - cv[i]).sqrMagnitude < maxD2)
									{
										near = true;
										break;
									}
								}
							}
							dz += 1L;
						}
						dy += 1L;
					}
					dx += 1L;
				}
				if (near)
				{
					hit++;
				}
			}
			if (tot <= 0)
			{
				return 0f;
			}
			return (float)hit / (float)tot;
		}

		// Token: 0x060000BD RID: 189 RVA: 0x000294C0 File Offset: 0x000276C0
		private static void SubdivideGarmentRegion(Mesh m, bool[] hot, out int added)
		{
			added = 0;
			if (m.blendShapeCount > 0 || hot == null)
			{
				return;
			}
			List<Vector3> pos = new List<Vector3>(m.vertices);
			int before = pos.Count;
			if (hot.Length != before)
			{
				return;
			}
			Vector3[] nrmA = m.normals;
			List<Vector3> nrm = new List<Vector3>(nrmA ?? new Vector3[0]);
			Vector4[] tanA = m.tangents;
			List<Vector4> tan = new List<Vector4>(tanA ?? new Vector4[0]);
			Vector2[] uv0A = m.uv;
			List<Vector2> uv0 = new List<Vector2>(uv0A ?? new Vector2[0]);
			Vector2[] uv1A = m.uv2;
			List<Vector2> uv = new List<Vector2>(uv1A ?? new Vector2[0]);
			Color32[] colA = m.colors32;
			List<Color32> col = new List<Color32>(colA ?? new Color32[0]);
			BoneWeight[] bwA = m.boneWeights;
			List<BoneWeight> bwL = new List<BoneWeight>(bwA ?? new BoneWeight[0]);
			bool hN = nrm.Count == before;
			bool hT = tan.Count == before;
			bool h0 = uv0.Count == before;
			bool h = uv.Count == before;
			bool hC = col.Count == before;
			bool hW = bwL.Count == before;
			int sub = m.subMeshCount;
			Dictionary<long, int> split = new Dictionary<long, int>(before);
			for (int sm = 0; sm < sub; sm++)
			{
				int[] tr = m.GetTriangles(sm);
				int f = 0;
				while (f + 2 < tr.Length)
				{
					int a = tr[f];
					int b = tr[f + 1];
					int c = tr[f + 2];
					if (hot[a] || hot[b] || hot[c])
					{
						split[SoftFleshController.EdgeKey(a, b)] = -1;
						split[SoftFleshController.EdgeKey(b, c)] = -1;
						split[SoftFleshController.EdgeKey(c, a)] = -1;
					}
					f += 3;
				}
			}
			if (split.Count == 0)
			{
				return;
			}
			Dictionary<long, int> mid = new Dictionary<long, int>(split.Count);
			int[][] newTris = new int[sub][];
			for (int sm2 = 0; sm2 < sub; sm2++)
			{
				int[] tr2 = m.GetTriangles(sm2);
				List<int> outT = new List<int>(tr2.Length * 2);
				int f2 = 0;
				while (f2 + 2 < tr2.Length)
				{
					int a2 = tr2[f2];
					int b2 = tr2[f2 + 1];
					int c2 = tr2[f2 + 2];
					int mAB = (split.ContainsKey(SoftFleshController.EdgeKey(a2, b2)) ? SoftFleshController.GarmMid(a2, b2, mid, pos, nrm, hN, tan, hT, uv0, h0, uv, h, col, hC, bwL, hW) : (-1));
					int mBC = (split.ContainsKey(SoftFleshController.EdgeKey(b2, c2)) ? SoftFleshController.GarmMid(b2, c2, mid, pos, nrm, hN, tan, hT, uv0, h0, uv, h, col, hC, bwL, hW) : (-1));
					int mCA = (split.ContainsKey(SoftFleshController.EdgeKey(c2, a2)) ? SoftFleshController.GarmMid(c2, a2, mid, pos, nrm, hN, tan, hT, uv0, h0, uv, h, col, hC, bwL, hW) : (-1));
					int i = (((mAB >= 0) + (mBC >= 0) + (mCA >= 0)) ? 1 : 0);
					if (i == 0)
					{
						outT.Add(a2);
						outT.Add(b2);
						outT.Add(c2);
					}
					else if (i == 3)
					{
						outT.Add(a2);
						outT.Add(mAB);
						outT.Add(mCA);
						outT.Add(mAB);
						outT.Add(b2);
						outT.Add(mBC);
						outT.Add(mCA);
						outT.Add(mBC);
						outT.Add(c2);
						outT.Add(mAB);
						outT.Add(mBC);
						outT.Add(mCA);
					}
					else if (i == 1)
					{
						if (mAB >= 0)
						{
							outT.Add(a2);
							outT.Add(mAB);
							outT.Add(c2);
							outT.Add(mAB);
							outT.Add(b2);
							outT.Add(c2);
						}
						else if (mBC >= 0)
						{
							outT.Add(a2);
							outT.Add(b2);
							outT.Add(mBC);
							outT.Add(a2);
							outT.Add(mBC);
							outT.Add(c2);
						}
						else
						{
							outT.Add(a2);
							outT.Add(b2);
							outT.Add(mCA);
							outT.Add(mCA);
							outT.Add(b2);
							outT.Add(c2);
						}
					}
					else
					{
						int x = a2;
						int y = b2;
						int z = c2;
						int p = mAB;
						int p2 = mBC;
						if (mCA >= 0)
						{
							if (mAB < 0)
							{
								x = b2;
								y = c2;
								z = a2;
								p = mBC;
								p2 = mCA;
							}
							else
							{
								x = c2;
								y = a2;
								z = b2;
								p = mCA;
								p2 = mAB;
							}
						}
						outT.Add(p);
						outT.Add(y);
						outT.Add(p2);
						outT.Add(x);
						outT.Add(p);
						outT.Add(p2);
						outT.Add(x);
						outT.Add(p2);
						outT.Add(z);
					}
					f2 += 3;
				}
				newTris[sm2] = outT.ToArray();
			}
			m.Clear(false);
			m.SetVertices(pos);
			if (hN)
			{
				m.SetNormals(nrm);
			}
			if (hT)
			{
				m.SetTangents(tan);
			}
			if (h0)
			{
				m.SetUVs(0, uv0);
			}
			if (h)
			{
				m.SetUVs(1, uv);
			}
			if (hC)
			{
				m.colors32 = col.ToArray();
			}
			if (hW)
			{
				m.boneWeights = bwL.ToArray();
			}
			m.subMeshCount = sub;
			for (int sm3 = 0; sm3 < sub; sm3++)
			{
				m.SetTriangles(newTris[sm3], sm3);
			}
			m.RecalculateBounds();
			added = pos.Count - before;
		}

		// Token: 0x060000BE RID: 190 RVA: 0x00029A68 File Offset: 0x00027C68
		private static void SyncGarmentRiders(Mesh m, Vector3[] garmS0, Vector3[] bodyS, int origVertN, int budget, List<int> outRid, List<int> outRbv, out int added)
		{
			added = 0;
			if (m.blendShapeCount > 0 || origVertN <= 0 || origVertN >= bodyS.Length)
			{
				return;
			}
			int nHot = bodyS.Length - origVertN;
			SoftFleshController.<>c__DisplayClass561_0 CS$<>8__locals1;
			CS$<>8__locals1.pos = new List<Vector3>(m.vertices);
			int before = CS$<>8__locals1.pos.Count;
			if (garmS0.Length != before)
			{
				return;
			}
			Vector3[] nrmA = m.normals;
			CS$<>8__locals1.nrm = new List<Vector3>(nrmA ?? new Vector3[0]);
			Vector4[] tanA = m.tangents;
			CS$<>8__locals1.tan = new List<Vector4>(tanA ?? new Vector4[0]);
			Vector2[] uv0A = m.uv;
			CS$<>8__locals1.uv0 = new List<Vector2>(uv0A ?? new Vector2[0]);
			Vector2[] uv1A = m.uv2;
			CS$<>8__locals1.uv1 = new List<Vector2>(uv1A ?? new Vector2[0]);
			Color32[] colA = m.colors32;
			CS$<>8__locals1.col = new List<Color32>(colA ?? new Color32[0]);
			BoneWeight[] bwA = m.boneWeights;
			CS$<>8__locals1.bwL = new List<BoneWeight>(bwA ?? new BoneWeight[0]);
			CS$<>8__locals1.hN = CS$<>8__locals1.nrm.Count == before;
			CS$<>8__locals1.hT = CS$<>8__locals1.tan.Count == before;
			CS$<>8__locals1.h0 = CS$<>8__locals1.uv0.Count == before;
			CS$<>8__locals1.h1 = CS$<>8__locals1.uv1.Count == before;
			CS$<>8__locals1.hC = CS$<>8__locals1.col.Count == before;
			CS$<>8__locals1.hW = CS$<>8__locals1.bwL.Count == before;
			CS$<>8__locals1.garmS = new List<Vector3>(garmS0);
			int sub = m.subMeshCount;
			List<int>[] tris = new List<int>[sub];
			for (int sm = 0; sm < sub; sm++)
			{
				tris[sm] = new List<int>(m.GetTriangles(sm));
			}
			bool[] done = new bool[nHot];
			HashSet<int> claimedCodes = new HashSet<int>();
			List<int> clPoint = new List<int>();
			List<List<int>> clCodes = new List<List<int>>();
			List<List<Vector3>> clBarys = new List<List<Vector3>>();
			int pass = 0;
			while (pass < 3 && added < budget)
			{
				Dictionary<long, List<int>> hash = new Dictionary<long, List<int>>(CS$<>8__locals1.garmS.Count);
				for (int i = 0; i < CS$<>8__locals1.garmS.Count; i++)
				{
					long key = SoftFleshController.<SyncGarmentRiders>g__Key|561_0((long)Mathf.Floor(CS$<>8__locals1.garmS[i].x / 0.02f), (long)Mathf.Floor(CS$<>8__locals1.garmS[i].y / 0.02f), (long)Mathf.Floor(CS$<>8__locals1.garmS[i].z / 0.02f));
					List<int> cell;
					if (!hash.TryGetValue(key, out cell))
					{
						cell = (hash[key] = new List<int>(4));
					}
					cell.Add(i);
				}
				List<int>[] inc = new List<int>[CS$<>8__locals1.garmS.Count];
				for (int sm2 = 0; sm2 < sub; sm2++)
				{
					List<int> T = tris[sm2];
					int t = 0;
					while (t + 2 < T.Count)
					{
						int code = sm2 * 10000000 + t;
						for (int e = 0; e < 3; e++)
						{
							int v = T[t + e];
							if (inc[v] == null)
							{
								inc[v] = new List<int>(8);
							}
							inc[v].Add(code);
						}
						t += 3;
					}
				}
				claimedCodes.Clear();
				clPoint.Clear();
				clCodes.Clear();
				clBarys.Clear();
				HashSet<int> visited = new HashSet<int>();
				List<int> cand = new List<int>(32);
				List<float> candD = new List<float>(32);
				for (int hIdx = 0; hIdx < nHot; hIdx++)
				{
					if (!done[hIdx] && added + clPoint.Count < budget)
					{
						Vector3 pt = bodyS[origVertN + hIdx];
						long cx = (long)Mathf.Floor(pt.x / 0.02f);
						long cy = (long)Mathf.Floor(pt.y / 0.02f);
						long cz = (long)Mathf.Floor(pt.z / 0.02f);
						float bd = 0.000225f;
						Vector3 bW = Vector3.zero;
						int bCode = -1;
						visited.Clear();
						cand.Clear();
						candD.Clear();
						for (long dx = -1L; dx <= 1L; dx += 1L)
						{
							for (long dy = -1L; dy <= 1L; dy += 1L)
							{
								for (long dz = -1L; dz <= 1L; dz += 1L)
								{
									List<int> cell2;
									if (hash.TryGetValue(SoftFleshController.<SyncGarmentRiders>g__Key|561_0(cx + dx, cy + dy, cz + dz), out cell2))
									{
										for (int c0 = 0; c0 < cell2.Count; c0++)
										{
											List<int> lst = inc[cell2[c0]];
											if (lst != null)
											{
												for (int li = 0; li < lst.Count; li++)
												{
													int code2 = lst[li];
													if (visited.Add(code2))
													{
														List<int> T2 = tris[code2 / 10000000];
														int off = code2 % 10000000;
														Vector3 by;
														Vector3 cpt = SoftFleshController.ClothClosestOnTri(pt, CS$<>8__locals1.garmS[T2[off]], CS$<>8__locals1.garmS[T2[off + 1]], CS$<>8__locals1.garmS[T2[off + 2]], out by);
														float d2 = (cpt - pt).sqrMagnitude;
														cand.Add(code2);
														candD.Add(d2);
														if (d2 < bd)
														{
															bd = d2;
															bW = by;
															bCode = code2;
														}
													}
												}
											}
										}
									}
								}
							}
						}
						if (bCode < 0)
						{
							done[hIdx] = true;
						}
						else
						{
							List<int> Tb = tris[bCode / 10000000];
							int ob = bCode % 10000000;
							int bia = Tb[ob];
							int bib = Tb[ob + 1];
							int bic = Tb[ob + 2];
							float ea = (CS$<>8__locals1.garmS[bia] - pt).sqrMagnitude;
							float eb = (CS$<>8__locals1.garmS[bib] - pt).sqrMagnitude;
							float ec = (CS$<>8__locals1.garmS[bic] - pt).sqrMagnitude;
							float emin = ((ea < eb) ? ((ea < ec) ? ea : ec) : ((eb < ec) ? eb : ec));
							if (emin < 6.25E-06f)
							{
								done[hIdx] = true;
							}
							else
							{
								bW.x = Mathf.Max(bW.x, 0.12f);
								bW.y = Mathf.Max(bW.y, 0.12f);
								bW.z = Mathf.Max(bW.z, 0.12f);
								float ws = bW.x + bW.y + bW.z;
								bW /= ws;
								Vector3 pA = CS$<>8__locals1.garmS[bia];
								Vector3 pB = CS$<>8__locals1.garmS[bib];
								Vector3 pC = CS$<>8__locals1.garmS[bic];
								List<int> codes = new List<int>(2);
								List<Vector3> barys = new List<Vector3>(2);
								codes.Add(bCode);
								barys.Add(bW);
								for (int q2 = 0; q2 < cand.Count; q2++)
								{
									int code3 = cand[q2];
									if (code3 != bCode && candD[q2] <= bd + 1E-10f)
									{
										List<int> T3 = tris[code3 / 10000000];
										int o2 = code3 % 10000000;
										int used = 0;
										Vector3 w2 = Vector3.zero;
										bool ok = true;
										int e2 = 0;
										while (e2 < 3 && ok)
										{
											Vector3 pe = CS$<>8__locals1.garmS[T3[o2 + e2]];
											int slot = (((pe - pA).sqrMagnitude < 6.250001E-08f) ? 0 : (((pe - pB).sqrMagnitude < 6.250001E-08f) ? 1 : (((pe - pC).sqrMagnitude < 6.250001E-08f) ? 2 : (-1))));
											if (slot < 0 || (used & (1 << slot)) != 0)
											{
												ok = false;
												break;
											}
											used |= 1 << slot;
											float wv = ((slot == 0) ? bW.x : ((slot == 1) ? bW.y : bW.z));
											if (e2 == 0)
											{
												w2.x = wv;
											}
											else if (e2 == 1)
											{
												w2.y = wv;
											}
											else
											{
												w2.z = wv;
											}
											e2++;
										}
										if (ok)
										{
											codes.Add(code3);
											barys.Add(w2);
										}
									}
								}
								bool clash = false;
								int q3 = 0;
								while (q3 < codes.Count && !clash)
								{
									if (claimedCodes.Contains(codes[q3]))
									{
										clash = true;
									}
									q3++;
								}
								if (!clash)
								{
									for (int q4 = 0; q4 < codes.Count; q4++)
									{
										claimedCodes.Add(codes[q4]);
									}
									clPoint.Add(hIdx);
									clCodes.Add(codes);
									clBarys.Add(barys);
								}
							}
						}
					}
				}
				if (clPoint.Count == 0)
				{
					break;
				}
				int q5 = 0;
				while (q5 < clPoint.Count && added < budget)
				{
					List<int> codes2 = clCodes[q5];
					List<Vector3> barys2 = clBarys[q5];
					int c = codes2[0];
					List<int> T4 = tris[c / 10000000];
					int o3 = c % 10000000;
					int ia0 = T4[o3];
					int ib0 = T4[o3 + 1];
					int ic0 = T4[o3 + 2];
					int nIdx = SoftFleshController.<SyncGarmentRiders>g__AddVert|561_1(ia0, ib0, ic0, barys2[0], ref CS$<>8__locals1);
					outRid.Add(nIdx);
					outRbv.Add(origVertN + clPoint[q5]);
					SoftFleshController.<SyncGarmentRiders>g__SplitTri|561_2(T4, o3, nIdx);
					for (int cc = 1; cc < codes2.Count; cc++)
					{
						int c2 = codes2[cc];
						List<int> T5 = tris[c2 / 10000000];
						int o4 = c2 % 10000000;
						int ja = T5[o4];
						int jb = T5[o4 + 1];
						int jc = T5[o4 + 2];
						bool shares = (ja == ia0 || ja == ib0 || ja == ic0) && (jb == ia0 || jb == ib0 || jb == ic0) && (jc == ia0 || jc == ib0 || jc == ic0);
						int useIdx;
						if (shares)
						{
							useIdx = nIdx;
						}
						else
						{
							useIdx = SoftFleshController.<SyncGarmentRiders>g__AddVert|561_1(ja, jb, jc, barys2[cc], ref CS$<>8__locals1);
							outRid.Add(useIdx);
							outRbv.Add(origVertN + clPoint[q5]);
						}
						SoftFleshController.<SyncGarmentRiders>g__SplitTri|561_2(T5, o4, useIdx);
					}
					done[clPoint[q5]] = true;
					added++;
					q5++;
				}
				pass++;
			}
			if (added == 0)
			{
				return;
			}
			m.Clear(false);
			m.SetVertices(CS$<>8__locals1.pos);
			if (CS$<>8__locals1.hN)
			{
				m.SetNormals(CS$<>8__locals1.nrm);
			}
			if (CS$<>8__locals1.hT)
			{
				m.SetTangents(CS$<>8__locals1.tan);
			}
			if (CS$<>8__locals1.h0)
			{
				m.SetUVs(0, CS$<>8__locals1.uv0);
			}
			if (CS$<>8__locals1.h1)
			{
				m.SetUVs(1, CS$<>8__locals1.uv1);
			}
			if (CS$<>8__locals1.hC)
			{
				m.colors32 = CS$<>8__locals1.col.ToArray();
			}
			if (CS$<>8__locals1.hW)
			{
				m.boneWeights = CS$<>8__locals1.bwL.ToArray();
			}
			m.subMeshCount = sub;
			for (int sm3 = 0; sm3 < sub; sm3++)
			{
				m.SetTriangles(tris[sm3], sm3);
			}
			m.RecalculateBounds();
		}

		// Token: 0x060000BF RID: 191 RVA: 0x0002A61C File Offset: 0x0002881C
		private static Matrix4x4 BlendM(BoneWeight w, Matrix4x4[] m)
		{
			Matrix4x4 r = Matrix4x4.zero;
			if (w.weight0 > 0f)
			{
				SoftFleshController.AccM(ref r, m[w.boneIndex0], w.weight0);
			}
			if (w.weight1 > 0f)
			{
				SoftFleshController.AccM(ref r, m[w.boneIndex1], w.weight1);
			}
			if (w.weight2 > 0f)
			{
				SoftFleshController.AccM(ref r, m[w.boneIndex2], w.weight2);
			}
			if (w.weight3 > 0f)
			{
				SoftFleshController.AccM(ref r, m[w.boneIndex3], w.weight3);
			}
			return r;
		}

		// Token: 0x060000C0 RID: 192 RVA: 0x0002A6D4 File Offset: 0x000288D4
		private static void AccM(ref Matrix4x4 r, Matrix4x4 a, float f)
		{
			r.m00 += a.m00 * f;
			r.m01 += a.m01 * f;
			r.m02 += a.m02 * f;
			r.m03 += a.m03 * f;
			r.m10 += a.m10 * f;
			r.m11 += a.m11 * f;
			r.m12 += a.m12 * f;
			r.m13 += a.m13 * f;
			r.m20 += a.m20 * f;
			r.m21 += a.m21 * f;
			r.m22 += a.m22 * f;
			r.m23 += a.m23 * f;
			r.m30 += a.m30 * f;
			r.m31 += a.m31 * f;
			r.m32 += a.m32 * f;
			r.m33 += a.m33 * f;
		}

		// Token: 0x060000C1 RID: 193 RVA: 0x0002A804 File Offset: 0x00028A04
		private static Vector3 SkinW(Vector3 v, BoneWeight w, Matrix4x4[] m)
		{
			Vector3 acc = Vector3.zero;
			if (w.weight0 > 0f)
			{
				acc += m[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
			}
			if (w.weight1 > 0f)
			{
				acc += m[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
			}
			if (w.weight2 > 0f)
			{
				acc += m[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
			}
			if (w.weight3 > 0f)
			{
				acc += m[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
			}
			return acc;
		}

		// Token: 0x060000C2 RID: 194 RVA: 0x0002A8E8 File Offset: 0x00028AE8
		private static BoneWeight BlendW3(BoneWeight a, BoneWeight b, BoneWeight c, Vector3 w)
		{
			SoftFleshController.<>c__DisplayClass565_0 CS$<>8__locals1;
			CS$<>8__locals1.bi = new int[12];
			CS$<>8__locals1.bw = new float[12];
			CS$<>8__locals1.bn = 0;
			SoftFleshController.<BlendW3>g__Acc|565_0(a.boneIndex0, a.weight0 * w.x, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(a.boneIndex1, a.weight1 * w.x, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(a.boneIndex2, a.weight2 * w.x, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(a.boneIndex3, a.weight3 * w.x, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(b.boneIndex0, b.weight0 * w.y, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(b.boneIndex1, b.weight1 * w.y, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(b.boneIndex2, b.weight2 * w.y, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(b.boneIndex3, b.weight3 * w.y, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(c.boneIndex0, c.weight0 * w.z, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(c.boneIndex1, c.weight1 * w.z, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(c.boneIndex2, c.weight2 * w.z, ref CS$<>8__locals1);
			SoftFleshController.<BlendW3>g__Acc|565_0(c.boneIndex3, c.weight3 * w.z, ref CS$<>8__locals1);
			int i = 0;
			while (i < CS$<>8__locals1.bn && i < 4)
			{
				int mx = i;
				for (int j = i + 1; j < CS$<>8__locals1.bn; j++)
				{
					if (CS$<>8__locals1.bw[j] > CS$<>8__locals1.bw[mx])
					{
						mx = j;
					}
				}
				float tf = CS$<>8__locals1.bw[i];
				CS$<>8__locals1.bw[i] = CS$<>8__locals1.bw[mx];
				CS$<>8__locals1.bw[mx] = tf;
				int ti = CS$<>8__locals1.bi[i];
				CS$<>8__locals1.bi[i] = CS$<>8__locals1.bi[mx];
				CS$<>8__locals1.bi[mx] = ti;
				i++;
			}
			BoneWeight r = default(BoneWeight);
			float sum = 0f;
			int k = 0;
			while (k < CS$<>8__locals1.bn && k < 4)
			{
				sum += CS$<>8__locals1.bw[k];
				k++;
			}
			if (sum <= 0f)
			{
				return a;
			}
			if (CS$<>8__locals1.bn > 0)
			{
				r.boneIndex0 = CS$<>8__locals1.bi[0];
				r.weight0 = CS$<>8__locals1.bw[0] / sum;
			}
			if (CS$<>8__locals1.bn > 1)
			{
				r.boneIndex1 = CS$<>8__locals1.bi[1];
				r.weight1 = CS$<>8__locals1.bw[1] / sum;
			}
			if (CS$<>8__locals1.bn > 2)
			{
				r.boneIndex2 = CS$<>8__locals1.bi[2];
				r.weight2 = CS$<>8__locals1.bw[2] / sum;
			}
			if (CS$<>8__locals1.bn > 3)
			{
				r.boneIndex3 = CS$<>8__locals1.bi[3];
				r.weight3 = CS$<>8__locals1.bw[3] / sum;
			}
			return r;
		}

		// Token: 0x060000C3 RID: 195 RVA: 0x0002ABE5 File Offset: 0x00028DE5
		private static long EdgeKey(int a, int b)
		{
			if (a >= b)
			{
				return ((long)b << 32) | (long)((ulong)a);
			}
			return ((long)a << 32) | (long)((ulong)b);
		}

		// Token: 0x060000C4 RID: 196 RVA: 0x0002ABFC File Offset: 0x00028DFC
		private static int GarmMid(int a, int b, Dictionary<long, int> mid, List<Vector3> pos, List<Vector3> nrm, bool hN, List<Vector4> tan, bool hT, List<Vector2> uv0, bool h0, List<Vector2> uv1, bool h1, List<Color32> col, bool hC, List<BoneWeight> bwL, bool hW)
		{
			long key = SoftFleshController.EdgeKey(a, b);
			int idx;
			if (mid.TryGetValue(key, out idx))
			{
				return idx;
			}
			idx = pos.Count;
			pos.Add((pos[a] + pos[b]) * 0.5f);
			if (hN)
			{
				Vector3 nv = nrm[a] + nrm[b];
				float mv = nv.magnitude;
				nrm.Add((mv > 1E-06f) ? (nv / mv) : nrm[a]);
			}
			if (hT)
			{
				Vector4 t4 = (tan[a] + tan[b]) * 0.5f;
				Vector3 t5;
				t5..ctor(t4.x, t4.y, t4.z);
				float tm = t5.magnitude;
				if (tm > 1E-06f)
				{
					t5 /= tm;
				}
				tan.Add(new Vector4(t5.x, t5.y, t5.z, tan[a].w));
			}
			if (h0)
			{
				uv0.Add((uv0[a] + uv0[b]) * 0.5f);
			}
			if (h1)
			{
				uv1.Add((uv1[a] + uv1[b]) * 0.5f);
			}
			if (hC)
			{
				col.Add(Color32.Lerp(col[a], col[b], 0.5f));
			}
			if (hW)
			{
				bwL.Add(SoftFleshSubdivide.BlendWeights(bwL[a], bwL[b]));
			}
			mid.Add(key, idx);
			return idx;
		}

		// Token: 0x060000C5 RID: 197 RVA: 0x0002ADBC File Offset: 0x00028FBC
		private static Mesh FindRootMesh(string rootName)
		{
			Mesh root = null;
			foreach (Mesh mm in Resources.FindObjectsOfTypeAll<Mesh>())
			{
				if (!(mm == null) && !(mm.name != rootName) && mm.isReadable && mm.vertexCount != 0 && (root == null || mm.vertexCount < root.vertexCount))
				{
					root = mm;
				}
			}
			return root;
		}

		// Token: 0x060000C6 RID: 198 RVA: 0x0002AE24 File Offset: 0x00029024
		private void BindClothes(SoftFleshController.Body body)
		{
			SoftFleshController.<>c__DisplayClass569_0 CS$<>8__locals1;
			CS$<>8__locals1.body = body;
			CS$<>8__locals1.body.clothBound = true;
			int budget = 90000;
			if (CS$<>8__locals1.body.cha == null || CS$<>8__locals1.body.smr == null || CS$<>8__locals1.body.workMesh == null)
			{
				return;
			}
			GameObject[] oc = CS$<>8__locals1.body.cha.objClothes;
			if (oc == null)
			{
				SoftFleshController.ClothInfo = "no clothing slots on this character";
				return;
			}
			Vector3[] bv = CS$<>8__locals1.body.baseVerts;
			bool[] skipC = CS$<>8__locals1.body.noDent;
			if (CS$<>8__locals1.body.origVertN > 0 && CS$<>8__locals1.body.origVertN < bv.Length)
			{
				skipC = new bool[bv.Length];
				for (int i = 0; i < bv.Length; i++)
				{
					skipC[i] = (CS$<>8__locals1.body.noDent != null && CS$<>8__locals1.body.noDent[i]) || i >= CS$<>8__locals1.body.origVertN;
				}
			}
			Dictionary<long, List<int>> hashBind = SoftFleshController.ClothHash(bv, skipC, 0.02f);
			Vector3[] bodyPosed = SoftFleshController.ClothSkin(bv, CS$<>8__locals1.body.bonesArr, CS$<>8__locals1.body.workMesh.bindposes, CS$<>8__locals1.body.workMesh.boneWeights);
			Dictionary<long, List<int>> hashPose = ((bodyPosed != null) ? SoftFleshController.ClothHash(bodyPosed, skipC, 0.02f) : null);
			int[] tri = ((CS$<>8__locals1.body.origMesh != null && CS$<>8__locals1.body.origVertN > 0 && CS$<>8__locals1.body.origVertN < bv.Length) ? CS$<>8__locals1.body.origMesh.triangles : CS$<>8__locals1.body.workMesh.triangles);
			int[] vtStart = new int[bv.Length + 1];
			for (int f = 0; f < tri.Length; f++)
			{
				vtStart[tri[f] + 1]++;
			}
			for (int j = 1; j <= bv.Length; j++)
			{
				vtStart[j] += vtStart[j - 1];
			}
			int[] vtList = new int[tri.Length];
			int[] vtFill = new int[bv.Length];
			for (int f2 = 0; f2 < tri.Length; f2++)
			{
				int vi = tri[f2];
				vtList[vtStart[vi] + vtFill[vi]] = f2 / 3;
				vtFill[vi]++;
			}
			if (CS$<>8__locals1.body.origVertN > 0 && CS$<>8__locals1.body.origVertN < bv.Length)
			{
				int nNew = bv.Length - CS$<>8__locals1.body.origVertN;
				Vector3[] hotPts = new Vector3[nNew];
				Vector3[] srcPts = ((bodyPosed != null) ? bodyPosed : bv);
				for (int k = 0; k < nNew; k++)
				{
					hotPts[k] = srcPts[CS$<>8__locals1.body.origVertN + k];
				}
				Dictionary<long, List<int>> hotHash = SoftFleshController.ClothHash(hotPts, null, 0.02f);
			}
			StringBuilder sb = new StringBuilder();
			HashSet<SkinnedMeshRenderer> seenSmr = new HashSet<SkinnedMeshRenderer>();
			int okN = 0;
			for (int slot = 0; slot < oc.Length; slot++)
			{
				if (!(oc[slot] == null))
				{
					string sn = ((slot < SoftFleshController.ClothSlotName.Length) ? SoftFleshController.ClothSlotName[slot] : ("slot" + slot.ToString()));
					if (slot == 4)
					{
						sb.Append(' ').Append(sn).Append("=skipped(hands do not deform)");
					}
					else
					{
						foreach (SkinnedMeshRenderer smr in oc[slot].GetComponentsInChildren<SkinnedMeshRenderer>(true))
						{
							if (!(smr == null) && !(smr == CS$<>8__locals1.body.smr) && seenSmr.Add(smr))
							{
								bool alreadySkinned = false;
								for (int sk2 = 0; sk2 < this._skins.Count; sk2++)
								{
									if (this._skins[sk2].origSmr == smr)
									{
										alreadySkinned = true;
										break;
									}
								}
								if (!alreadySkinned)
								{
									Mesh src = smr.sharedMesh;
									if (!(src == null) && src.vertexCount != 0)
									{
										if (src.name.StartsWith("SF_cloth_"))
										{
											bool live = false;
											for (int r2 = 0; r2 < this._cloth.Count; r2++)
											{
												if (this._cloth[r2].clone == src)
												{
													live = true;
													break;
												}
											}
											if (live)
											{
												goto IL_27A1;
											}
											string orn = src.name.Substring(9);
											while (orn.EndsWith("(Clone)"))
											{
												orn = orn.Substring(0, orn.Length - 7);
											}
											Mesh oroot = SoftFleshController.FindRootMesh(orn);
											if (oroot == null)
											{
												goto IL_27A1;
											}
											SoftFleshPlugin.Log(string.Concat(new string[] { "[SF] cloth: adopted orphaned clone on '", smr.name, "', rebinding from root '", orn, "'" }));
											smr.sharedMesh = oroot;
											src = oroot;
										}
										if (src.name.EndsWith("(Clone)"))
										{
											string rootName = src.name;
											while (rootName.EndsWith("(Clone)"))
											{
												rootName = rootName.Substring(0, rootName.Length - 7);
											}
											Mesh root = SoftFleshController.FindRootMesh(rootName);
											if (root != null)
											{
												SoftFleshPlugin.Log(string.Concat(new string[]
												{
													"[SF] cloth: '",
													smr.name,
													"' carried a clone-chain relic (",
													src.vertexCount.ToString(),
													" verts); binding from root '",
													rootName,
													"' (",
													root.vertexCount.ToString(),
													" verts)"
												}));
												src = root;
											}
										}
										if (!src.isReadable)
										{
											SoftFleshPlugin.Log("[SF] cloth skip '" + smr.name + "' (mesh not readable)");
										}
										else
										{
											Vector3[] cv0 = src.vertices;
											Vector3[] clothPosed = SoftFleshController.ClothSkin(cv0, smr.bones, src.bindposes, src.boneWeights);
											float fB = SoftFleshController.ClothNearFrac(cv0, hashBind, bv, 0.02f, 0.0004f);
											float fP = ((hashPose != null) ? SoftFleshController.ClothNearFrac(clothPosed, hashPose, bodyPosed, 0.02f, 0.0004f) : (-1f));
											bool usePose = fP > fB;
											float best = (usePose ? fP : fB);
											if (best < 0.2f)
											{
												SoftFleshPlugin.Log(string.Concat(new string[]
												{
													"[SF] cloth skip '",
													smr.name,
													"' (only ",
													(best * 100f).ToString("F0"),
													"% near the skin)"
												}));
											}
											else
											{
												Vector3[] searchBody = (usePose ? bodyPosed : bv);
												Dictionary<long, List<int>> searchHash = (usePose ? hashPose : hashBind);
												if (searchBody != null && searchHash != null)
												{
													Mesh clone = Object.Instantiate<Mesh>(src);
													clone.name = "SF_cloth_" + src.name;
													int addedV = 0;
													List<int> riderIdxL = new List<int>(0);
													List<int> riderBodyL = new List<int>(0);
													budget -= addedV;
													clone.MarkDynamic();
													Vector3[] cv = clone.vertices;
													Vector3[] densePosed = ((addedV > 0) ? SoftFleshController.ClothSkin(cv, smr.bones, clone.bindposes, clone.boneWeights) : clothPosed);
													Vector3[] searchCloth = (usePose ? densePosed : cv);
													if (searchCloth == null)
													{
														Object.Destroy(clone);
													}
													else
													{
														Vector3[] cv0d = cv;
														SoftFleshController.ClothRec rec = new SoftFleshController.ClothRec();
														rec.smr = smr;
														rec.orig = src;
														rec.body = CS$<>8__locals1.body;
														rec.usePose = usePose;
														rec.rawV = cv0d;
														rec.triA = new int[cv0d.Length];
														rec.triB = new int[cv0d.Length];
														rec.triC = new int[cv0d.Length];
														rec.triW = new Vector3[cv0d.Length];
														int bound = 0;
														float dSum = 0f;
														int[] nbS = new int[cv0d.Length + 1];
														List<int> nbL = new List<int>(cv0d.Length * 6);
														List<float> nbWL = new List<float>(cv0d.Length * 6);
														for (int l = 0; l < cv0d.Length; l++)
														{
															nbS[l] = nbL.Count;
															rec.triA[l] = -1;
															float bd = 0.0004f;
															Vector3 bBary = Vector3.zero;
															int bTri = -1;
															Vector3 q = searchCloth[l];
															long cx = (long)Mathf.Floor(q.x / 0.02f);
															long cy = (long)Mathf.Floor(q.y / 0.02f);
															long cz = (long)Mathf.Floor(q.z / 0.02f);
															for (long dx = -1L; dx <= 1L; dx += 1L)
															{
																for (long dy = -1L; dy <= 1L; dy += 1L)
																{
																	for (long dz = -1L; dz <= 1L; dz += 1L)
																	{
																		long key = ((cx + dx) & 2097151L) | (((cy + dy) & 2097151L) << 21) | (((cz + dz) & 2097151L) << 42);
																		List<int> cell;
																		if (searchHash.TryGetValue(key, out cell))
																		{
																			for (int c0 = 0; c0 < cell.Count; c0++)
																			{
																				int vi2 = cell[c0];
																				for (int ti = vtStart[vi2]; ti < vtStart[vi2 + 1]; ti++)
																				{
																					int t3 = vtList[ti];
																					int ia = tri[t3 * 3];
																					int ib = tri[t3 * 3 + 1];
																					int ic = tri[t3 * 3 + 2];
																					Vector3 by;
																					Vector3 cpt = SoftFleshController.ClothClosestOnTri(q, searchBody[ia], searchBody[ib], searchBody[ic], out by);
																					float d2 = (cpt - q).sqrMagnitude;
																					if (d2 < bd)
																					{
																						bd = d2;
																						bBary = by;
																						bTri = t3;
																					}
																				}
																			}
																		}
																	}
																}
															}
															if (bTri >= 0)
															{
																rec.triA[l] = tri[bTri * 3];
																rec.triB[l] = tri[bTri * 3 + 1];
																rec.triC[l] = tri[bTri * 3 + 2];
																rec.triW[l] = bBary;
																bound++;
																dSum += Mathf.Sqrt(bd);
																for (long dx2 = -1L; dx2 <= 1L; dx2 += 1L)
																{
																	for (long dy2 = -1L; dy2 <= 1L; dy2 += 1L)
																	{
																		for (long dz2 = -1L; dz2 <= 1L; dz2 += 1L)
																		{
																			long key2 = ((cx + dx2) & 2097151L) | (((cy + dy2) & 2097151L) << 21) | (((cz + dz2) & 2097151L) << 42);
																			List<int> cell2;
																			if (searchHash.TryGetValue(key2, out cell2))
																			{
																				for (int c = 0; c < cell2.Count; c++)
																				{
																					int vi3 = cell2[c];
																					float d2n = (searchBody[vi3] - q).sqrMagnitude;
																					if (d2n < 8.099999E-05f && nbL.Count - nbS[l] < 20)
																					{
																						nbL.Add(vi3);
																						float dn = Mathf.Sqrt(d2n);
																						float wn = ((dn <= 0.0045f) ? 1f : (1f - (dn - 0.0045f) / 0.0045f));
																						if (wn < 0f)
																						{
																							wn = 0f;
																						}
																						nbWL.Add(wn * wn * (3f - 2f * wn));
																					}
																				}
																			}
																		}
																	}
																}
															}
														}
														nbS[cv0d.Length] = nbL.Count;
														rec.nbrStart = nbS;
														rec.nbrList = nbL.ToArray();
														rec.nbrW = nbWL.ToArray();
														rec.revBody = null;
														List<int> candB = new List<int>(rec.nbrList.Length);
														HashSet<int> seenB = new HashSet<int>();
														for (int q2 = 0; q2 < rec.nbrList.Length; q2++)
														{
															if (!SoftFleshController.<BindClothes>g__OpenV|569_0(rec.nbrList[q2], ref CS$<>8__locals1) && seenB.Add(rec.nbrList[q2]))
															{
																candB.Add(rec.nbrList[q2]);
															}
														}
														for (int q3 = 0; q3 < cv0d.Length; q3++)
														{
															if (rec.triA[q3] >= 0)
															{
																if (!SoftFleshController.<BindClothes>g__OpenV|569_0(rec.triA[q3], ref CS$<>8__locals1) && seenB.Add(rec.triA[q3]))
																{
																	candB.Add(rec.triA[q3]);
																}
																if (!SoftFleshController.<BindClothes>g__OpenV|569_0(rec.triB[q3], ref CS$<>8__locals1) && seenB.Add(rec.triB[q3]))
																{
																	candB.Add(rec.triB[q3]);
																}
																if (!SoftFleshController.<BindClothes>g__OpenV|569_0(rec.triC[q3], ref CS$<>8__locals1) && seenB.Add(rec.triC[q3]))
																{
																	candB.Add(rec.triC[q3]);
																}
															}
														}
														if (this.secondSkin && slot != 2 && bound > 0 && dSum / (float)bound <= 0.0025f && bound * 10 >= cv0d.Length * 7)
														{
															for (int q4 = 0; q4 < searchBody.Length; q4++)
															{
																if (seenB.Add(q4))
																{
																	candB.Add(q4);
																}
															}
														}
														Dictionary<long, List<int>> chash = SoftFleshController.ClothHash(searchCloth, null, 0.02f);
														List<int> ctris = new List<int>(cv0d.Length * 3);
														for (int sm2 = 0; sm2 < clone.subMeshCount; sm2++)
														{
															int[] tt = clone.GetTriangles(sm2);
															for (int q5 = 0; q5 < tt.Length; q5++)
															{
																ctris.Add(tt[q5]);
															}
														}
														int canonDrop;
														bool[] keepF = SoftFleshController.CanonFaces(searchCloth, clone, out canonDrop);
														List<int>[] cInc = new List<int>[cv0d.Length];
														int t4 = 0;
														while (t4 + 2 < ctris.Count)
														{
															for (int e2 = 0; e2 < 3; e2++)
															{
																int v2 = ctris[t4 + e2];
																if (cInc[v2] == null)
																{
																	cInc[v2] = new List<int>(6);
																}
																cInc[v2].Add(t4);
															}
															t4 += 3;
														}
														List<int> rT = new List<int>(candB.Count * 3);
														List<Vector3> rW = new List<Vector3>(candB.Count);
														List<int> rB = new List<int>(candB.Count);
														List<float> rD = new List<float>(candB.Count);
														List<int> rF = new List<int>(candB.Count);
														List<bool> rE = new List<bool>(candB.Count);
														int[] wIdC = new int[cv0d.Length];
														Dictionary<long, int> wMapC = new Dictionary<long, int>(cv0d.Length);
														for (int m = 0; m < cv0d.Length; m++)
														{
															Vector3 pw = searchCloth[m];
															long kx = (long)Mathf.Round(pw.x * 10000f);
															long ky = (long)Mathf.Round(pw.y * 10000f);
															long kz = (long)Mathf.Round(pw.z * 10000f);
															long kk = (kx & 2097151L) | ((ky & 2097151L) << 21) | ((kz & 2097151L) << 42);
															int w2;
															if (!wMapC.TryGetValue(kk, out w2))
															{
																w2 = wMapC.Count;
																wMapC.Add(kk, w2);
															}
															wIdC[m] = w2;
														}
														Dictionary<long, int> edgeC = new Dictionary<long, int>(ctris.Count);
														int t5 = 0;
														while (t5 + 2 < ctris.Count)
														{
															if (t5 / 3 >= keepF.Length || keepF[t5 / 3])
															{
																for (int e3 = 0; e3 < 3; e3++)
																{
																	int va = wIdC[ctris[t5 + e3]];
																	int vb = wIdC[ctris[t5 + (e3 + 1) % 3]];
																	if (va != vb)
																	{
																		long ek = ((va < vb) ? (((long)va << 24) | (long)((ulong)vb)) : (((long)vb << 24) | (long)((ulong)va)));
																		int c2;
																		edgeC.TryGetValue(ek, out c2);
																		edgeC[ek] = c2 + 1;
																	}
																}
															}
															t5 += 3;
														}
														Func<int, int, bool> openC = delegate(int gA, int gB)
														{
															int va4 = wIdC[gA];
															int vb5 = wIdC[gB];
															if (va4 == vb5)
															{
																return false;
															}
															long ek3 = ((va4 < vb5) ? (((long)va4 << 24) | (long)((ulong)vb5)) : (((long)vb5 << 24) | (long)((ulong)va4)));
															int c6;
															return !edgeC.TryGetValue(ek3, out c6) || c6 < 2;
														};
														HashSet<int> seenT = new HashSet<int>();
														for (int q6 = 0; q6 < candB.Count; q6++)
														{
															int bvI = candB[q6];
															Vector3 pB = searchBody[bvI];
															long hx2 = (long)Mathf.Floor(pB.x / 0.02f);
															long hy2 = (long)Mathf.Floor(pB.y / 0.02f);
															long hz2 = (long)Mathf.Floor(pB.z / 0.02f);
															float bd2 = 0.001225f;
															int bT = -1;
															Vector3 bw3 = Vector3.zero;
															seenT.Clear();
															for (long dx3 = -2L; dx3 <= 2L; dx3 += 1L)
															{
																for (long dy3 = -2L; dy3 <= 2L; dy3 += 1L)
																{
																	for (long dz3 = -2L; dz3 <= 2L; dz3 += 1L)
																	{
																		long key3 = ((hx2 + dx3) & 2097151L) | (((hy2 + dy3) & 2097151L) << 21) | (((hz2 + dz3) & 2097151L) << 42);
																		List<int> cell3;
																		if (chash.TryGetValue(key3, out cell3))
																		{
																			for (int c3 = 0; c3 < cell3.Count; c3++)
																			{
																				List<int> lst3 = cInc[cell3[c3]];
																				if (lst3 != null)
																				{
																					for (int li3 = 0; li3 < lst3.Count; li3++)
																					{
																						int t6 = lst3[li3];
																						if (seenT.Add(t6) && (t6 / 3 >= keepF.Length || keepF[t6 / 3]))
																						{
																							Vector3 by2;
																							Vector3 cp3 = SoftFleshController.ClothClosestOnTri(pB, searchCloth[ctris[t6]], searchCloth[ctris[t6 + 1]], searchCloth[ctris[t6 + 2]], out by2);
																							float d3 = (cp3 - pB).sqrMagnitude;
																							if (d3 < bd2)
																							{
																								bd2 = d3;
																								bT = t6;
																								bw3 = by2;
																							}
																						}
																					}
																				}
																			}
																		}
																	}
																}
															}
															if (bT >= 0)
															{
																rT.Add(ctris[bT]);
																rT.Add(ctris[bT + 1]);
																rT.Add(ctris[bT + 2]);
																rW.Add(bw3);
																rB.Add(bvI);
																rD.Add(Mathf.Sqrt(bd2));
																rF.Add(bT / 3);
																float m2 = bw3.x;
																float m3 = bw3.y;
																float m4 = bw3.z;
																bool hem;
																if (m2 <= m3 && m2 <= m4)
																{
																	hem = m2 < 0.02f && openC(ctris[bT + 1], ctris[bT + 2]);
																}
																else if (m3 <= m4)
																{
																	hem = m3 < 0.02f && openC(ctris[bT + 2], ctris[bT]);
																}
																else
																{
																	hem = m4 < 0.02f && openC(ctris[bT], ctris[bT + 1]);
																}
																rE.Add(hem);
															}
														}
														if (rB.Count > 0)
														{
															rec.revTri = rT.ToArray();
															rec.revBary = rW.ToArray();
															rec.revBody = rB.ToArray();
															rec.revDist = rD.ToArray();
															rec.revFace = rF.ToArray();
															rec.revHem = rE.ToArray();
															int hemN = 0;
															for (int q7 = 0; q7 < rec.revHem.Length; q7++)
															{
																if (rec.revHem[q7])
																{
																	hemN++;
																}
															}
															if (hemN > 0)
															{
																SoftFleshPlugin.Log(string.Concat(new string[]
																{
																	"[SF]   hem rows: ",
																	hemN.ToString(),
																	"/",
																	rec.revHem.Length.ToString(),
																	" project onto the garment's own edge - excluded from the span guard"
																}));
															}
															rec.needLift = new float[cv0d.Length];
															SoftFleshPlugin.Log(string.Concat(new string[]
															{
																"[SF] span guard: ",
																rB.Count.ToString(),
																" body verts mapped on ",
																src.name,
																usePose ? " (pose)" : " (bind)"
															}));
														}
														if (bound < cv0d.Length / 20)
														{
															SoftFleshPlugin.Log(string.Concat(new string[]
															{
																"[SF] cloth skip '",
																smr.name,
																"' (bound only ",
																bound.ToString(),
																"/",
																cv0d.Length.ToString(),
																")"
															}));
															Object.Destroy(clone);
														}
														else
														{
															BoneWeight[] gbw = clone.boneWeights;
															Matrix4x4[] gbind = clone.bindposes;
															Transform[] gbones = smr.bones;
															rec.gBind = gbind;
															rec.gbwArr = gbw;
															rec.domCloth = new int[cv0d.Length];
															if (gbw != null && gbw.Length == cv0d.Length)
															{
																for (int n = 0; n < cv0d.Length; n++)
																{
																	BoneWeight w3 = gbw[n];
																	int di = w3.boneIndex0;
																	float dw = w3.weight0;
																	if (w3.weight1 > dw)
																	{
																		di = w3.boneIndex1;
																		dw = w3.weight1;
																	}
																	if (w3.weight2 > dw)
																	{
																		di = w3.boneIndex2;
																		dw = w3.weight2;
																	}
																	if (w3.weight3 > dw)
																	{
																		di = w3.boneIndex3;
																	}
																	rec.domCloth[n] = di;
																}
															}
															rec.clothBones = gbones;
															if (gbind != null && gbones != null)
															{
																int nrb = ((gbind.Length < gbones.Length) ? gbind.Length : gbones.Length);
																rec.clothBindRot = new Quaternion[nrb];
																for (int k2 = 0; k2 < nrb; k2++)
																{
																	rec.clothBindRot[k2] = SoftFleshController.RotFromBindpose(gbind[k2]);
																}
															}
															Dictionary<long, List<int>> gmap = new Dictionary<long, List<int>>(cv0d.Length / 2);
															for (int k3 = 0; k3 < cv0d.Length; k3++)
															{
																long gx = (long)Mathf.Round(cv0d[k3].x * 10000f);
																long gy = (long)Mathf.Round(cv0d[k3].y * 10000f);
																long gz = (long)Mathf.Round(cv0d[k3].z * 10000f);
																long gk = (gx & 2097151L) | ((gy & 2097151L) << 21) | ((gz & 2097151L) << 42);
																List<int> gl;
																if (!gmap.TryGetValue(gk, out gl))
																{
																	gl = new List<int>(2);
																	gmap[gk] = gl;
																}
																gl.Add(k3);
															}
															Vector3[] srcN = clone.normals;
															rec.liftN = new Vector3[cv0d.Length];
															for (int k4 = 0; k4 < cv0d.Length; k4++)
															{
																rec.liftN[k4] = ((srcN != null && srcN.Length == cv0d.Length) ? srcN[k4] : Vector3.up);
															}
															List<int> gs = new List<int>(64);
															List<int> gv = new List<int>(256);
															foreach (KeyValuePair<long, List<int>> kv in gmap)
															{
																List<int> gl2 = kv.Value;
																if (gl2.Count >= 2)
																{
																	Vector3 avg = Vector3.zero;
																	for (int j2 = 0; j2 < gl2.Count; j2++)
																	{
																		avg += rec.liftN[gl2[j2]];
																	}
																	float m5 = avg.magnitude;
																	if (m5 > 1E-06f)
																	{
																		avg /= m5;
																		for (int j3 = 0; j3 < gl2.Count; j3++)
																		{
																			rec.liftN[gl2[j3]] = avg;
																		}
																	}
																	gs.Add(gv.Count);
																	for (int j4 = 0; j4 < gl2.Count; j4++)
																	{
																		gv.Add(gl2[j4]);
																	}
																	gs.Add(gv.Count);
																}
															}
															rec.grpStart = gs.ToArray();
															rec.grpList = gv.ToArray();
															int[] wid = new int[cv0d.Length];
															int nw = 0;
															foreach (KeyValuePair<long, List<int>> kv2 in gmap)
															{
																List<int> gl3 = kv2.Value;
																for (int j5 = 0; j5 < gl3.Count; j5++)
																{
																	wid[gl3[j5]] = nw;
																}
																nw++;
															}
															HashSet<long> fSeen = new HashSet<long>();
															Dictionary<long, int> eCnt = new Dictionary<long, int>();
															Dictionary<int, List<int>> adj = new Dictionary<int, List<int>>();
															for (int sm3 = 0; sm3 < clone.subMeshCount; sm3++)
															{
																int[] tt2 = clone.GetTriangles(sm3);
																int t7 = 0;
																while (t7 + 2 < tt2.Length)
																{
																	int a2 = wid[tt2[t7]];
																	int b2 = wid[tt2[t7 + 1]];
																	int c4 = wid[tt2[t7 + 2]];
																	int lo = ((a2 < b2) ? ((a2 < c4) ? a2 : c4) : ((b2 < c4) ? b2 : c4));
																	int hi = ((a2 > b2) ? ((a2 > c4) ? a2 : c4) : ((b2 > c4) ? b2 : c4));
																	int md = a2 + b2 + c4 - lo - hi;
																	if (fSeen.Add(((long)lo << 42) | ((long)md << 21) | (long)((ulong)hi)))
																	{
																		for (int e4 = 0; e4 < 3; e4++)
																		{
																			int va2 = ((e4 == 0) ? a2 : ((e4 == 1) ? b2 : c4));
																			int vb2 = ((e4 == 0) ? b2 : ((e4 == 1) ? c4 : a2));
																			if (va2 != vb2)
																			{
																				long ek2 = ((va2 < vb2) ? (((long)va2 << 21) | (long)((ulong)vb2)) : (((long)vb2 << 21) | (long)((ulong)va2)));
																				int c5;
																				eCnt[ek2] = (eCnt.TryGetValue(ek2, out c5) ? (c5 + 1) : 1);
																				List<int> al;
																				if (!adj.TryGetValue(va2, out al))
																				{
																					al = new List<int>(6);
																					adj[va2] = al;
																				}
																				al.Add(vb2);
																				if (!adj.TryGetValue(vb2, out al))
																				{
																					al = new List<int>(6);
																					adj[vb2] = al;
																				}
																				al.Add(va2);
																			}
																		}
																	}
																	t7 += 3;
																}
															}
															int[] ring = new int[nw];
															for (int j6 = 0; j6 < nw; j6++)
															{
																ring[j6] = int.MaxValue;
															}
															Queue<int> bfs = new Queue<int>();
															using (Dictionary<long, int>.Enumerator enumerator3 = eCnt.GetEnumerator())
															{
																while (enumerator3.MoveNext())
																{
																	KeyValuePair<long, int> kv3 = enumerator3.Current;
																	if (kv3.Value == 1)
																	{
																		int va3 = (int)(kv3.Key >> 21);
																		int vb3 = (int)(kv3.Key & 2097151L);
																		if (ring[va3] != 0)
																		{
																			ring[va3] = 0;
																			bfs.Enqueue(va3);
																		}
																		if (ring[vb3] != 0)
																		{
																			ring[vb3] = 0;
																			bfs.Enqueue(vb3);
																		}
																	}
																}
																goto IL_1BB0;
															}
															goto IL_1B44;
															IL_1BB0:
															if (bfs.Count <= 0)
															{
																rec.liftW = new float[cv0d.Length];
																for (int k5 = 0; k5 < cv0d.Length; k5++)
																{
																	int rg = ring[wid[k5]];
																	rec.liftW[k5] = ((rg >= 3) ? 1f : ((float)rg / 3f));
																}
																rec.nrm0 = ((srcN != null && srcN.Length == cv0d.Length) ? ((Vector3[])srcN.Clone()) : null);
																if (rec.nrm0 != null)
																{
																	rec.nrmW = (Vector3[])rec.nrm0.Clone();
																}
																if (riderIdxL.Count > 0 && CS$<>8__locals1.body.domBone != null && CS$<>8__locals1.body.bwArr != null && CS$<>8__locals1.body.bindPoses != null && CS$<>8__locals1.body.bonesArr != null && gbw != null)
																{
																	int nR = riderIdxL.Count;
																	rec.riderIdx = riderIdxL.ToArray();
																	rec.riderBodyV = riderBodyL.ToArray();
																	rec.riderOffB = new Vector3[nR];
																	rec.isRider = new bool[cv0d.Length];
																	int nbB = CS$<>8__locals1.body.bonesArr.Length;
																	Matrix4x4[] bmB = new Matrix4x4[nbB];
																	for (int b3 = 0; b3 < nbB; b3++)
																	{
																		bmB[b3] = ((CS$<>8__locals1.body.bonesArr[b3] != null) ? (CS$<>8__locals1.body.bonesArr[b3].localToWorldMatrix * CS$<>8__locals1.body.bindPoses[b3]) : Matrix4x4.identity);
																	}
																	int nbG = ((gbones.Length < gbind.Length) ? gbones.Length : gbind.Length);
																	Matrix4x4[] bmG = new Matrix4x4[nbG];
																	for (int b4 = 0; b4 < nbG; b4++)
																	{
																		bmG[b4] = ((gbones[b4] != null) ? (gbones[b4].localToWorldMatrix * gbind[b4]) : Matrix4x4.identity);
																	}
																	for (int q8 = 0; q8 < nR; q8++)
																	{
																		int gi = rec.riderIdx[q8];
																		int vb4 = rec.riderBodyV[q8];
																		rec.isRider[gi] = true;
																		Vector3 restB = ((vb4 < CS$<>8__locals1.body.outV.Count) ? CS$<>8__locals1.body.outV[vb4] : ((CS$<>8__locals1.body.subFix != null) ? (CS$<>8__locals1.body.baseVerts[vb4] + CS$<>8__locals1.body.subFix[vb4]) : CS$<>8__locals1.body.baseVerts[vb4]));
																		Vector3 bodyW = SoftFleshController.SkinW(restB, CS$<>8__locals1.body.bwArr[vb4], bmB);
																		Vector3 ridW = ((gi < gbw.Length) ? SoftFleshController.SkinW(cv0d[gi], gbw[gi], bmG) : bodyW);
																		int db2 = CS$<>8__locals1.body.domBone[vb4];
																		Quaternion qb = ((db2 >= 0 && db2 < nbB && CS$<>8__locals1.body.bonesArr[db2] != null) ? (CS$<>8__locals1.body.bonesArr[db2].rotation * CS$<>8__locals1.body.bindRot[db2]) : Quaternion.identity);
																		rec.riderOffB[q8] = Quaternion.Inverse(qb) * (ridW - bodyW);
																	}
																}
																float meanFit = ((bound > 0) ? (dSum / (float)bound) : 1f);
																bool skinEligible = this.secondSkin && slot != 2 && meanFit <= 0.0025f && bound * 10 >= cv0d.Length * 7 && rec.revBody != null && rec.revDist != null;
																if (skinEligible && this.BuildSecondSkin(CS$<>8__locals1.body, smr, clone, rec, meanFit, searchBody, searchCloth, slot))
																{
																	Object.Destroy(clone);
																	okN++;
																	sb.Append(' ').Append(sn).Append('/')
																		.Append(smr.name)
																		.Append("=SKIN ")
																		.Append((meanFit * 1000f).ToString("F1"))
																		.Append("mm");
																	goto IL_27A1;
																}
																if (skinEligible)
																{
																	SoftFleshPlugin.Log(string.Concat(new string[]
																	{
																		"[SF] skin DECLINED on '",
																		smr.name,
																		"' (fit ",
																		(meanFit * 1000f).ToString("F1"),
																		"mm, bound ",
																		bound.ToString(),
																		"/",
																		cv0d.Length.ToString(),
																		") - transfer path, penetration guard applies"
																	}));
																}
																rec.clone = clone;
																rec.baseV = new Vector3[cv0d.Length];
																for (int k6 = 0; k6 < cv0d.Length; k6++)
																{
																	rec.baseV[k6] = cv0d[k6] + rec.liftN[k6] * (this.clothLift * ((rec.liftW != null) ? rec.liftW[k6] : 1f));
																}
																rec.work = (Vector3[])rec.baseV.Clone();
																if (rec.revBody != null && rec.revTri != null && rec.liftN != null)
																{
																	IList<Vector3> list2;
																	if (CS$<>8__locals1.body.outV == null || CS$<>8__locals1.body.outV.Count != CS$<>8__locals1.body.baseVerts.Length)
																	{
																		IList<Vector3> list = CS$<>8__locals1.body.baseVerts;
																		list2 = list;
																	}
																	else
																	{
																		IList<Vector3> list = CS$<>8__locals1.body.outV;
																		list2 = list;
																	}
																	IList<Vector3> restB2 = list2;
																	rec.revRest = new float[rec.revBody.Length];
																	for (int r3 = 0; r3 < rec.revBody.Length; r3++)
																	{
																		int q9 = rec.revTri[r3 * 3];
																		int q10 = rec.revTri[r3 * 3 + 1];
																		int q11 = rec.revTri[r3 * 3 + 2];
																		Vector3 w4 = rec.revBary[r3];
																		Vector3 S0 = rec.baseV[q9] * w4.x + rec.baseV[q10] * w4.y + rec.baseV[q11] * w4.z;
																		Vector3 n2 = rec.liftN[q9] * w4.x + rec.liftN[q10] * w4.y + rec.liftN[q11] * w4.z;
																		Vector3 b5 = restB2[rec.revBody[r3]];
																		rec.revRest[r3] = (b5.x - S0.x) * n2.x + (b5.y - S0.y) * n2.y + (b5.z - S0.z) * n2.z;
																	}
																}
																rec.clone.vertices = rec.work;
																rec.boundN = bound;
																int nBsW = src.blendShapeCount;
																float[] bsW = SoftFleshController.AuthoredShapeWeights(smr, nBsW);
																smr.sharedMesh = rec.clone;
																if (bsW != null)
																{
																	int nBsC = rec.clone.blendShapeCount;
																	StringBuilder sbW = new StringBuilder();
																	int k7 = 0;
																	while (k7 < nBsW && k7 < nBsC)
																	{
																		smr.SetBlendShapeWeight(k7, bsW[k7]);
																		if (k7 > 0)
																		{
																			sbW.Append(' ');
																		}
																		sbW.Append(src.GetBlendShapeName(k7)).Append('=').Append(bsW[k7].ToString("F0"));
																		k7++;
																	}
																	Vector3[] dvQ = new Vector3[cv0d.Length];
																	int k8 = 0;
																	while (k8 < nBsW && k8 < nBsC)
																	{
																		if (bsW[k8] != 0f)
																		{
																			float mx = 0f;
																			try
																			{
																				if (SoftFleshController._bsN == null || SoftFleshController._bsN.Length != cv0d.Length)
																				{
																					SoftFleshController._bsN = new Vector3[cv0d.Length];
																					SoftFleshController._bsT = new Vector3[cv0d.Length];
																				}
																				src.GetBlendShapeFrameVertices(k8, src.GetBlendShapeFrameCount(k8) - 1, dvQ, SoftFleshController._bsN, SoftFleshController._bsT);
																				for (int q12 = 0; q12 < dvQ.Length; q12++)
																				{
																					float m6 = dvQ[q12].sqrMagnitude;
																					if (m6 > mx)
																					{
																						mx = m6;
																					}
																				}
																			}
																			catch
																			{
																			}
																			sbW.Append(" [").Append(src.GetBlendShapeName(k8)).Append(" moves ")
																				.Append((Mathf.Sqrt(mx) * bsW[k8] * 0.01f * 1000f).ToString("F1"))
																				.Append("mm]");
																		}
																		k8++;
																	}
																	if (rec.revDist != null && rec.revDist.Length != 0)
																	{
																		float[] dcp = (float[])rec.revDist.Clone();
																		Array.Sort<float>(dcp);
																		sbW.Append(" clearance min/med/max ").Append((dcp[0] * 1000f).ToString("F1")).Append('/')
																			.Append((dcp[dcp.Length / 2] * 1000f).ToString("F1"))
																			.Append('/')
																			.Append((dcp[dcp.Length - 1] * 1000f).ToString("F1"))
																			.Append("mm over ")
																			.Append(dcp.Length)
																			.Append(" rows");
																	}
																	string text = "[SF] cloth '";
																	string name = smr.name;
																	string text2 = "' shapes: ";
																	StringBuilder stringBuilder = sbW;
																	SoftFleshPlugin.Log(text + name + text2 + ((stringBuilder != null) ? stringBuilder.ToString() : null));
																}
																this._cloth.Add(rec);
																okN++;
																sb.Append(' ').Append(sn).Append('/')
																	.Append(smr.name)
																	.Append('=')
																	.Append(usePose ? "pose" : "bind")
																	.Append(' ')
																	.Append(bound)
																	.Append('/')
																	.Append(cv0d.Length)
																	.Append(' ')
																	.Append((dSum / (float)bound * 1000f).ToString("F1"))
																	.Append("mm")
																	.Append((addedV > 0) ? ("+" + addedV.ToString()) : "");
																goto IL_27A1;
															}
															IL_1B44:
															int cw2 = bfs.Dequeue();
															List<int> al2;
															if (ring[cw2] < 3 && adj.TryGetValue(cw2, out al2))
															{
																for (int j7 = 0; j7 < al2.Count; j7++)
																{
																	if (ring[al2[j7]] > ring[cw2] + 1)
																	{
																		ring[al2[j7]] = ring[cw2] + 1;
																		bfs.Enqueue(al2[j7]);
																	}
																}
																goto IL_1BB0;
															}
															goto IL_1BB0;
														}
													}
												}
											}
										}
									}
								}
							}
							IL_27A1:;
						}
					}
				}
			}
			bool[] cov = new bool[CS$<>8__locals1.body.baseVerts.Length];
			bool anyCov = false;
			for (int r4 = 0; r4 < this._cloth.Count; r4++)
			{
				SoftFleshController.ClothRec rc2 = this._cloth[r4];
				if (rc2.body == CS$<>8__locals1.body)
				{
					if (rc2.nbrList != null)
					{
						for (int q13 = 0; q13 < rc2.nbrList.Length; q13++)
						{
							cov[rc2.nbrList[q13]] = true;
							anyCov = true;
						}
					}
					if (rc2.triA != null)
					{
						for (int q14 = 0; q14 < rc2.triA.Length; q14++)
						{
							if (rc2.triA[q14] >= 0)
							{
								cov[rc2.triA[q14]] = true;
								cov[rc2.triB[q14]] = true;
								cov[rc2.triC[q14]] = true;
								anyCov = true;
							}
						}
					}
				}
			}
			if (anyCov && CS$<>8__locals1.body.subPa != null && CS$<>8__locals1.body.origVertN > 0)
			{
				int i2 = CS$<>8__locals1.body.origVertN;
				while (i2 < cov.Length && i2 < CS$<>8__locals1.body.subPa.Length)
				{
					if (CS$<>8__locals1.body.subPa[i2] >= 0 && (cov[CS$<>8__locals1.body.subPa[i2]] || cov[CS$<>8__locals1.body.subPb[i2]]))
					{
						cov[i2] = true;
					}
					i2++;
				}
			}
			CS$<>8__locals1.body.clothCovered = (anyCov ? cov : null);
			this._clothLiftApplied = this.clothLift;
			SoftFleshController.ClothInfo = okN.ToString() + " garment(s) bound";
			SoftFleshPlugin.Log("[SF] cloth bind: f" + Time.frameCount.ToString() + ((sb.Length > 0) ? sb.ToString() : " nothing bound"));
		}

		// Token: 0x060000C7 RID: 199 RVA: 0x0002D8E8 File Offset: 0x0002BAE8
		private bool BuildSecondSkin(SoftFleshController.Body body, SkinnedMeshRenderer gsmr, Mesh gclone, SoftFleshController.ClothRec rec, float meanFit, Vector3[] searchBody, Vector3[] searchCloth, int slot)
		{
			if (body.bonesArr == null || body.bindPoses == null || body.bwArr == null || body.workMesh == null || body.smr == null)
			{
				return false;
			}
			Vector2[] guv = gclone.uv;
			if (guv == null || guv.Length == 0)
			{
				return false;
			}
			Vector2[] guv2A = gclone.uv2;
			Vector2[] guv2 = ((guv2A != null && guv2A.Length == guv.Length) ? guv2A : null);
			Color32[] gcolA = gclone.colors32;
			Color32[] gcol = ((gcolA != null && gcolA.Length == guv.Length) ? gcolA : null);
			Vector4[] gtanA = gclone.tangents;
			Vector4[] gtan = ((gtanA != null && gtanA.Length == guv.Length) ? gtanA : null);
			int nB = body.baseVerts.Length;
			int[] weldId = new int[searchCloth.Length];
			Dictionary<long, int> wmap = new Dictionary<long, int>();
			for (int i = 0; i < searchCloth.Length; i++)
			{
				Vector3 pw = searchCloth[i];
				long kx = (long)Mathf.Round(pw.x * 10000f);
				long ky = (long)Mathf.Round(pw.y * 10000f);
				long kz = (long)Mathf.Round(pw.z * 10000f);
				long kk = (kx & 2097151L) | ((ky & 2097151L) << 21) | ((kz & 2097151L) << 42);
				int wid;
				if (!wmap.TryGetValue(kk, out wid))
				{
					wid = wmap.Count;
					wmap.Add(kk, wid);
				}
				weldId[i] = wid;
			}
			int canonDropped;
			bool[] keepFace = SoftFleshController.CanonFaces(searchCloth, gclone, out canonDropped);
			Dictionary<long, int> edgeN = new Dictionary<long, int>();
			int fIdx = 0;
			for (int sm = 0; sm < gclone.subMeshCount; sm++)
			{
				int[] tt = gclone.GetTriangles(sm);
				int t = 0;
				while (t + 2 < tt.Length)
				{
					if (fIdx >= keepFace.Length || keepFace[fIdx])
					{
						for (int e2 = 0; e2 < 3; e2++)
						{
							int va = weldId[tt[t + e2]];
							int vb = weldId[tt[t + (e2 + 1) % 3]];
							long ek = ((va < vb) ? (((long)va << 24) | (long)((ulong)vb)) : (((long)vb << 24) | (long)((ulong)va)));
							int cnt;
							edgeN.TryGetValue(ek, out cnt);
							edgeN[ek] = cnt + 1;
						}
					}
					t += 3;
					fIdx++;
				}
			}
			Func<int, int, bool> openEdge = delegate(int gA, int gB)
			{
				int va8 = weldId[gA];
				int vb8 = weldId[gB];
				long ek4 = ((va8 < vb8) ? (((long)va8 << 24) | (long)((ulong)vb8)) : (((long)vb8 << 24) | (long)((ulong)va8)));
				int cnt2;
				return !edgeN.TryGetValue(ek4, out cnt2) || cnt2 < 2;
			};
			int[] inv = new int[nB];
			for (int j = 0; j < nB; j++)
			{
				inv[j] = -1;
			}
			List<int> map = new List<int>(rec.revBody.Length);
			List<int> revRow = new List<int>(rec.revBody.Length);
			List<Vector3> baryL = new List<Vector3>(rec.revBody.Length);
			List<Vector3> offL = new List<Vector3>(rec.revBody.Length);
			int far15 = 0;
			int rejFar = 0;
			int rejEdge = 0;
			Matrix4x4[] bmB = null;
			if (rec.usePose)
			{
				bmB = new Matrix4x4[body.bonesArr.Length];
				for (int b2 = 0; b2 < bmB.Length; b2++)
				{
					bmB[b2] = ((body.bonesArr[b2] != null) ? (body.bonesArr[b2].localToWorldMatrix * body.bindPoses[b2]) : Matrix4x4.identity);
				}
			}
			for (int r = 0; r < rec.revBody.Length; r++)
			{
				float d = rec.revDist[r];
				Vector3 w3 = rec.revBary[r];
				if (d > 0.015f)
				{
					far15++;
				}
				bool okC = false;
				if (d <= 0.032f)
				{
					bool inX = w3.x >= 0.02f;
					bool inY = w3.y >= 0.02f;
					bool inZ = w3.z >= 0.02f;
					if (inX && inY && inZ)
					{
						okC = true;
					}
					else
					{
						int g0 = rec.revTri[r * 3];
						int g = rec.revTri[r * 3 + 1];
						int g2 = rec.revTri[r * 3 + 2];
						if (!inX && inY && inZ)
						{
							okC = !openEdge(g, g2);
						}
						else if (inX && !inY && inZ)
						{
							okC = !openEdge(g0, g2);
						}
						else if (inX && inY && !inZ)
						{
							okC = !openEdge(g0, g);
						}
						else if (inX)
						{
							okC = !openEdge(g0, g) && !openEdge(g0, g2);
						}
						else if (inY)
						{
							okC = !openEdge(g, g0) && !openEdge(g, g2);
						}
						else if (inZ)
						{
							okC = !openEdge(g2, g0) && !openEdge(g2, g);
						}
					}
				}
				if (!okC)
				{
					if (d > 0.032f)
					{
						rejFar++;
					}
					else
					{
						rejEdge++;
					}
				}
				else
				{
					int bvI3 = rec.revBody[r];
					if (inv[bvI3] < 0)
					{
						inv[bvI3] = map.Count;
						map.Add(bvI3);
						revRow.Add(r);
						baryL.Add(w3);
						int f0 = rec.revTri[r * 3];
						int f = rec.revTri[r * 3 + 1];
						int f2 = rec.revTri[r * 3 + 2];
						Vector3 cp = searchCloth[f0] * w3.x + searchCloth[f] * w3.y + searchCloth[f2] * w3.z;
						Vector3 offS = cp - searchBody[bvI3];
						if (bmB != null)
						{
							offS = SoftFleshController.BlendM(body.bwArr[bvI3], bmB).inverse.MultiplyVector(offS);
						}
						Vector3 nrm2 = body.baseNormals[bvI3];
						float outc = offS.x * nrm2.x + offS.y * nrm2.y + offS.z * nrm2.z;
						if (outc < 0.0004f)
						{
							float fx = 0.0004f - outc;
							offS.x += nrm2.x * fx;
							offS.y += nrm2.y * fx;
							offS.z += nrm2.z * fx;
						}
						offL.Add(offS);
					}
				}
			}
			if (map.Count < 64)
			{
				return false;
			}
			if (map.Count * 2 < gclone.vertexCount)
			{
				return false;
			}
			int filled = 0;
			for (int hp = 0; hp < 2; hp++)
			{
				List<int> addV = new List<int>();
				List<Vector3> addO = new List<Vector3>();
				List<int> addR = new List<int>();
				List<Vector3> addW = new List<Vector3>();
				for (int k = 0; k < nB; k++)
				{
					if (inv[k] < 0 && body.adjStart != null)
					{
						int a0 = body.adjStart[k];
						int a = body.adjStart[k + 1];
						if (a > a0)
						{
							int acc = 0;
							Vector3 av = Vector3.zero;
							int nbRow = -1;
							for (int l = a0; l < a; l++)
							{
								int j9 = body.adjList[l];
								if (inv[j9] >= 0)
								{
									acc++;
									av += offL[inv[j9]];
									if (nbRow < 0 && revRow[inv[j9]] >= 0)
									{
										nbRow = revRow[inv[j9]];
									}
								}
							}
							if (acc * 3 >= (a - a0) * 2 && nbRow >= 0)
							{
								int g3 = rec.revTri[nbRow * 3];
								int g4 = rec.revTri[nbRow * 3 + 1];
								int g5 = rec.revTri[nbRow * 3 + 2];
								Vector3 byF;
								SoftFleshController.ClothClosestOnTri(searchBody[k], searchCloth[g3], searchCloth[g4], searchCloth[g5], out byF);
								addV.Add(k);
								addO.Add(av / (float)acc);
								addR.Add(nbRow);
								addW.Add(byF);
							}
						}
					}
				}
				for (int q = 0; q < addV.Count; q++)
				{
					inv[addV[q]] = map.Count;
					map.Add(addV[q]);
					revRow.Add(addR[q]);
					baryL.Add(addW[q]);
					offL.Add(addO[q]);
					filled++;
				}
				if (addV.Count == 0)
				{
					break;
				}
			}
			Func<int, Vector3, Vector2> SampUV = (int rr, Vector3 ww) => guv[rec.revTri[rr * 3]] * ww.x + guv[rec.revTri[rr * 3 + 1]] * ww.y + guv[rec.revTri[rr * 3 + 2]] * ww.z;
			int nS0 = map.Count;
			List<Vector3> vertsL = new List<Vector3>(nS0 + 256);
			List<Vector3> normsL = new List<Vector3>(nS0 + 256);
			List<BoneWeight> bwsL = new List<BoneWeight>(nS0 + 256);
			List<Vector2> uvL = new List<Vector2>(nS0 + 256);
			List<Vector2> uv2L = ((guv2 != null) ? new List<Vector2>(nS0 + 256) : null);
			List<Color32> colL = ((gcol != null) ? new List<Color32>(nS0 + 256) : null);
			List<Vector4> tanL = ((gtan != null) ? new List<Vector4>(nS0 + 256) : null);
			List<Vector3> offL2 = new List<Vector3>(nS0 + 256);
			List<int> mapL = new List<int>(nS0 + 256);
			List<int> shpC0 = null;
			List<int> shpC1 = null;
			List<int> shpC2 = null;
			List<Vector3> shpW = null;
			if (gsmr.sharedMesh != null && gsmr.sharedMesh.blendShapeCount > 0)
			{
				shpC0 = new List<int>(nS0);
				shpC1 = new List<int>(nS0);
				shpC2 = new List<int>(nS0);
				shpW = new List<Vector3>(nS0);
			}
			Action<int, int, Vector3> emit = delegate(int bvI, int rr, Vector3 ww)
			{
				int c3 = rec.revTri[rr * 3];
				int c4 = rec.revTri[rr * 3 + 1];
				int c5 = rec.revTri[rr * 3 + 2];
				vertsL.Add(Vector3.zero);
				normsL.Add(body.baseNormals[bvI]);
				bwsL.Add(body.bwArr[bvI]);
				uvL.Add(guv[c3] * ww.x + guv[c4] * ww.y + guv[c5] * ww.z);
				if (uv2L != null)
				{
					uv2L.Add(guv2[c3] * ww.x + guv2[c4] * ww.y + guv2[c5] * ww.z);
				}
				if (colL != null)
				{
					colL.Add(Color32.Lerp(Color32.Lerp(gcol[c3], gcol[c4], ww.y / (ww.x + ww.y + 1E-06f)), gcol[c5], ww.z));
				}
				if (tanL != null)
				{
					Vector4 t12 = gtan[c3] * ww.x + gtan[c4] * ww.y + gtan[c5] * ww.z;
					Vector3 t13;
					t13..ctor(t12.x, t12.y, t12.z);
					float tm = t13.magnitude;
					if (tm > 1E-06f)
					{
						t13 /= tm;
					}
					float hw = 1f;
					Vector3 he = searchCloth[c4] - searchCloth[c3];
					Vector3 he2 = searchCloth[c5] - searchCloth[c3];
					Vector2 hd = guv[c4] - guv[c3];
					Vector2 hd2 = guv[c5] - guv[c3];
					float hden = hd.x * hd2.y - hd2.x * hd.y;
					if (hden > 1E-12f || hden < -1E-12f)
					{
						Vector3 hbt = (he2 * hd.x - he * hd2.x) / hden;
						Vector3 hnB = body.baseNormals[bvI];
						if (Vector3.Dot(Vector3.Cross(hnB, t13), hbt) < 0f)
						{
							hw = -1f;
						}
					}
					tanL.Add(new Vector4(t13.x, t13.y, t13.z, hw));
				}
				mapL.Add(bvI);
				if (shpC0 != null)
				{
					shpC0.Add(c3);
					shpC1.Add(c4);
					shpC2.Add(c5);
					shpW.Add(ww);
				}
			};
			for (int m = 0; m < nS0; m++)
			{
				emit(map[m], revRow[m], baryL[m]);
				offL2.Add(offL[m]);
			}
			int[] btr = body.workMesh.triangles;
			List<int> tris = new List<int>(nS0 * 6);
			int t2 = 0;
			while (t2 + 2 < btr.Length)
			{
				int i0b = inv[btr[t2]];
				int i1b = inv[btr[t2 + 1]];
				int i2b = inv[btr[t2 + 2]];
				if (i0b >= 0 && i1b >= 0 && i2b >= 0)
				{
					tris.Add(i0b);
					tris.Add(i1b);
					tris.Add(i2b);
				}
				t2 += 3;
			}
			if (tris.Count < 96)
			{
				return false;
			}
			int localTri = 0;
			int localDup = 0;
			int localTear = 0;
			List<float> dens = new List<float>(tris.Count);
			int t3 = 0;
			while (t3 + 2 < tris.Count)
			{
				for (int e3 = 0; e3 < 3; e3++)
				{
					int va2 = tris[t3 + e3];
					int vb2 = tris[t3 + (e3 + 1) % 3];
					float mm = (searchBody[mapL[va2]] - searchBody[mapL[vb2]]).magnitude * 1000f;
					if (mm >= 0.001f)
					{
						float dd = (uvL[va2] - uvL[vb2]).magnitude;
						if (dd < 0.4f)
						{
							dens.Add(dd / mm);
						}
					}
				}
				t3 += 3;
			}
			dens.Sort();
			float tearUV = ((dens.Count >= 32) ? (2.5f * dens[dens.Count / 2]) : 0f);
			if (tearUV > 0f)
			{
				Dictionary<long, int> lkey = new Dictionary<long, int>();
				Vector2[] shA = new Vector2[3];
				Vector2[] shBest = new Vector2[3];
				int t4 = 0;
				while (t4 + 2 < tris.Count)
				{
					int c0a = tris[t4];
					int c1a = tris[t4 + 1];
					int c2a = tris[t4 + 2];
					Vector2 U0 = uvL[c0a];
					Vector2 U = uvL[c1a];
					Vector2 U2 = uvL[c2a];
					if (Mathf.Abs(U0.x - U.x) >= 0.5f || Mathf.Abs(U.x - U2.x) >= 0.5f || Mathf.Abs(U0.x - U2.x) >= 0.5f || Mathf.Abs(U0.y - U.y) >= 0.5f || Mathf.Abs(U.y - U2.y) >= 0.5f || Mathf.Abs(U0.y - U2.y) >= 0.5f)
					{
						int bestGood = -1;
						for (int r2 = 0; r2 < 3; r2++)
						{
							int cr = ((r2 == 0) ? c0a : ((r2 == 1) ? c1a : c2a));
							shA[0] = Vector2.zero;
							shA[1] = Vector2.zero;
							shA[2] = Vector2.zero;
							int good = 0;
							for (int e4 = 0; e4 < 3; e4++)
							{
								if (e4 != r2)
								{
									int ce = ((e4 == 0) ? c0a : ((e4 == 1) ? c1a : c2a));
									Vector2 dd2 = uvL[cr] - uvL[ce];
									Vector2 kk2;
									kk2..ctor(Mathf.Round(dd2.x), Mathf.Round(dd2.y));
									float mm2 = (searchBody[mapL[cr]] - searchBody[mapL[ce]]).magnitude * 1000f;
									if (mm2 < 1f)
									{
										mm2 = 1f;
									}
									if ((dd2 - kk2).magnitude <= tearUV * mm2)
									{
										shA[e4] = kk2;
										good++;
									}
								}
							}
							if (good > bestGood)
							{
								bestGood = good;
								shBest[0] = shA[0];
								shBest[1] = shA[1];
								shBest[2] = shA[2];
							}
						}
						if (bestGood < 2)
						{
							localTear++;
						}
						else
						{
							Vector2 n0 = U0 + shBest[0];
							Vector2 n = U + shBest[1];
							Vector2 n2 = U2 + shBest[2];
							if (Mathf.Max(n0.x, Mathf.Max(n.x, n2.x)) - Mathf.Min(n0.x, Mathf.Min(n.x, n2.x)) > 0.5f || Mathf.Max(n0.y, Mathf.Max(n.y, n2.y)) - Mathf.Min(n0.y, Mathf.Min(n.y, n2.y)) > 0.5f)
							{
								localTear++;
							}
							else
							{
								localTri++;
								int bu = Mathf.FloorToInt((n0.x + n.x + n2.x) / 3f);
								int bv = Mathf.FloorToInt((n0.y + n.y + n2.y) / 3f);
								for (int e5 = 0; e5 < 3; e5++)
								{
									int ce2 = ((e5 == 0) ? c0a : ((e5 == 1) ? c1a : c2a));
									int ku = (int)shBest[e5].x - bu;
									int kv = (int)shBest[e5].y - bv;
									if ((ku != 0 || kv != 0) && ku >= -4 && ku <= 4 && kv >= -4 && kv <= 4)
									{
										long key = ((long)ce2 << 8) | (long)((long)(ku + 4) << 4) | (long)(kv + 4);
										int dj;
										if (!lkey.TryGetValue(key, out dj))
										{
											dj = vertsL.Count;
											emit(mapL[ce2], revRow[ce2], baryL[ce2]);
											offL2.Add(offL2[ce2]);
											uvL[dj] = new Vector2(uvL[ce2].x + (float)ku, uvL[ce2].y + (float)kv);
											lkey.Add(key, dj);
											localDup++;
										}
										tris[t4 + e5] = dj;
									}
								}
							}
						}
					}
					t4 += 3;
				}
			}
			List<int> gFaces = new List<int>();
			for (int sm2 = 0; sm2 < gclone.subMeshCount; sm2++)
			{
				int[] tt2 = gclone.GetTriangles(sm2);
				for (int q2 = 0; q2 < tt2.Length; q2++)
				{
					gFaces.Add(tt2[q2]);
				}
			}
			int nFace = gFaces.Count / 3;
			int islandCount = 0;
			int[] islandOf = new int[nFace];
			for (int i2 = 0; i2 < nFace; i2++)
			{
				islandOf[i2] = -1;
			}
			Dictionary<long, int> eFaces = new Dictionary<long, int>();
			Dictionary<long, long> ePartner = new Dictionary<long, long>();
			List<int>[] adjF = new List<int>[nFace];
			for (int fI = 0; fI < nFace; fI++)
			{
				if (fI < keepFace.Length && !keepFace[fI])
				{
					islandOf[fI] = -2;
				}
				else
				{
					for (int e6 = 0; e6 < 3; e6++)
					{
						int gA2 = gFaces[fI * 3 + e6];
						int gB2 = gFaces[fI * 3 + (e6 + 1) % 3];
						int va3 = weldId[gA2];
						int vb3 = weldId[gB2];
						long ek2 = ((va3 < vb3) ? (((long)va3 << 24) | (long)((ulong)vb3)) : (((long)vb3 << 24) | (long)((ulong)va3)));
						int other;
						if (eFaces.TryGetValue(ek2, out other) && other >= 0)
						{
							int oA = -1;
							int oB = -1;
							for (int e7 = 0; e7 < 3; e7++)
							{
								if (weldId[gFaces[other * 3 + e7]] == va3)
								{
									oA = gFaces[other * 3 + e7];
								}
								if (weldId[gFaces[other * 3 + e7]] == vb3)
								{
									oB = gFaces[other * 3 + e7];
								}
							}
							float emm = (searchCloth[gA2] - searchCloth[gB2]).magnitude * 1000f;
							if (emm < 1f)
							{
								emm = 1f;
							}
							float wtol = tearUV * emm;
							float duA = 0f;
							float dvA = 0f;
							float duB = 0f;
							float dvB = 0f;
							if (oA >= 0)
							{
								duA = guv[oA].x - guv[gA2].x;
								float rA = Mathf.Round(duA);
								duA = ((Mathf.Abs(duA - rA) < wtol) ? (duA - rA) : duA);
								dvA = guv[oA].y - guv[gA2].y;
								float rA2 = Mathf.Round(dvA);
								dvA = ((Mathf.Abs(dvA - rA2) < wtol) ? (dvA - rA2) : dvA);
							}
							if (oB >= 0)
							{
								duB = guv[oB].x - guv[gB2].x;
								float rB = Mathf.Round(duB);
								duB = ((Mathf.Abs(duB - rB) < wtol) ? (duB - rB) : duB);
								dvB = guv[oB].y - guv[gB2].y;
								float rB2 = Mathf.Round(dvB);
								dvB = ((Mathf.Abs(dvB - rB2) < wtol) ? (dvB - rB2) : dvB);
							}
							if (oA >= 0 && oB >= 0 && duA * duA + dvA * dvA < 0.0004f && duB * duB + dvB * dvB < 0.0004f)
							{
								if (adjF[fI] == null)
								{
									adjF[fI] = new List<int>(3);
								}
								if (adjF[other] == null)
								{
									adjF[other] = new List<int>(3);
								}
								adjF[fI].Add(other);
								adjF[other].Add(fI);
							}
							eFaces[ek2] = -2;
						}
						else if (other != -2)
						{
							eFaces[ek2] = fI;
						}
					}
				}
			}
			Stack<int> stack = new Stack<int>();
			int isl = 0;
			for (int fI2 = 0; fI2 < nFace; fI2++)
			{
				if (islandOf[fI2] == -1)
				{
					stack.Push(fI2);
					islandOf[fI2] = isl;
					while (stack.Count > 0)
					{
						int cf = stack.Pop();
						List<int> al = adjF[cf];
						if (al != null)
						{
							for (int q3 = 0; q3 < al.Count; q3++)
							{
								if (islandOf[al[q3]] < 0)
								{
									islandOf[al[q3]] = isl;
									stack.Push(al[q3]);
								}
							}
						}
					}
					isl++;
				}
			}
			islandCount = isl;
			Func<int, int> islandOfVert = delegate(int j2)
			{
				int rr2 = revRow[j2];
				if (rr2 < 0 || rec.revFace == null || rr2 >= rec.revFace.Length)
				{
					return -1;
				}
				return islandOf[rec.revFace[rr2]];
			};
			List<float> dens2 = new List<float>(tris.Count);
			int t5 = 0;
			while (t5 + 2 < tris.Count)
			{
				for (int e8 = 0; e8 < 3; e8++)
				{
					int va4 = tris[t5 + e8];
					int vb4 = tris[t5 + (e8 + 1) % 3];
					float pd = (searchBody[mapL[va4]] - searchBody[mapL[vb4]]).magnitude;
					if (pd >= 1E-06f)
					{
						dens2.Add((uvL[va4] - uvL[vb4]).magnitude / pd);
					}
				}
				t5 += 3;
			}
			float tearLim;
			if (dens2.Count < 32)
			{
				tearLim = float.MaxValue;
			}
			else
			{
				dens2.Sort();
				float med = dens2[dens2.Count / 2];
				tearLim = ((med > 1E-09f) ? (med * 2.5f) : float.MaxValue);
			}
			Dictionary<long, int> dupKey = new Dictionary<long, int>();
			int seamExtrap = 0;
			int tornTri = 0;
			int t6 = 0;
			while (t6 + 2 < tris.Count)
			{
				int a2 = tris[t6];
				int a3 = tris[t6 + 1];
				int a4 = tris[t6 + 2];
				Vector2 u0 = uvL[a2];
				Vector2 u = uvL[a3];
				Vector2 u2 = uvL[a4];
				float d2 = (u0 - u).sqrMagnitude;
				float d3 = (u - u2).sqrMagnitude;
				float d4 = (u2 - u0).sqrMagnitude;
				float span = Mathf.Max(d2, Mathf.Max(d3, d4));
				int i3 = ((a2 < nS0) ? islandOfVert(a2) : (-9));
				int i4 = ((a3 < nS0) ? islandOfVert(a3) : (-9));
				int i2b2 = ((a4 < nS0) ? islandOfVert(a4) : (-9));
				bool islMixed = (i3 >= 0 && i4 >= 0 && i3 != i4) || (i4 >= 0 && i2b2 >= 0 && i4 != i2b2) || (i3 >= 0 && i2b2 >= 0 && i3 != i2b2);
				float p = (searchBody[mapL[a2]] - searchBody[mapL[a3]]).magnitude;
				float p2 = (searchBody[mapL[a3]] - searchBody[mapL[a4]]).magnitude;
				float p3 = (searchBody[mapL[a4]] - searchBody[mapL[a2]]).magnitude;
				bool t7 = p > 1E-06f && Mathf.Sqrt(d2) / p > tearLim;
				bool t8 = p2 > 1E-06f && Mathf.Sqrt(d3) / p2 > tearLim;
				bool t9 = p3 > 1E-06f && Mathf.Sqrt(d4) / p3 > tearLim;
				bool torn = t7 || t8 || t9;
				if (torn || islMixed)
				{
					if (torn)
					{
						tornTri++;
					}
					if (torn && !islMixed)
					{
						bool o0 = t7 && t9;
						bool o = t7 && t8;
						bool o2 = t8 && t9;
						if (o0 && !o && !o2)
						{
							d3 = 0f;
							d2 = 1f;
							d4 = 1f;
						}
						else if (o && !o0 && !o2)
						{
							d4 = 0f;
							d2 = 1f;
							d3 = 1f;
						}
						else if (o2 && !o0 && !o)
						{
							d2 = 0f;
							d3 = 1f;
							d4 = 1f;
						}
					}
					if (islMixed)
					{
						if (i3 == i4 && i3 >= 0)
						{
							d2 = 0f;
							d3 = 1f;
							d4 = 1f;
						}
						else if (i4 == i2b2 && i4 >= 0)
						{
							d3 = 0f;
							d2 = 1f;
							d4 = 1f;
						}
						else if (i3 == i2b2 && i3 >= 0)
						{
							d4 = 0f;
							d2 = 1f;
							d3 = 1f;
						}
					}
					int oddSlot;
					int ancA;
					int ancB;
					if (d2 <= d3 && d2 <= d4)
					{
						oddSlot = 2;
						ancA = a2;
						ancB = a3;
					}
					else if (d3 <= d4)
					{
						oddSlot = 0;
						ancA = a3;
						ancB = a4;
					}
					else
					{
						oddSlot = 1;
						ancA = a4;
						ancB = a2;
					}
					int aj2 = ((oddSlot == 0) ? a2 : ((oddSlot == 1) ? a3 : a4));
					if (aj2 < nS0)
					{
						int anc = ((ancA < nS0) ? ancA : ((ancB < nS0) ? ancB : (-1)));
						if (anc >= 0)
						{
							int rrA = revRow[anc];
							if (rrA >= 0)
							{
								int fc0 = rec.revTri[rrA * 3];
								int fc = rec.revTri[rrA * 3 + 1];
								int fc2 = rec.revTri[rrA * 3 + 2];
								int bvJ = mapL[aj2];
								Vector3 byJ;
								if (!SoftFleshController.ClothBaryExtrap(searchBody[bvJ], searchCloth[fc0], searchCloth[fc], searchCloth[fc2], out byJ))
								{
									SoftFleshController.ClothClosestOnTri(searchBody[bvJ], searchCloth[fc0], searchCloth[fc], searchCloth[fc2], out byJ);
								}
								else
								{
									seamExtrap++;
								}
								Vector2 nu = guv[fc0] * byJ.x + guv[fc] * byJ.y + guv[fc2] * byJ.z;
								if ((nu - uvL[aj2]).sqrMagnitude >= 0.0025000002f)
								{
									long key2 = ((long)bvJ << 20) | (long)((ulong)rrA);
									int dj2;
									if (!dupKey.TryGetValue(key2, out dj2))
									{
										dj2 = vertsL.Count;
										emit(bvJ, rrA, byJ);
										offL2.Add(offL2[aj2]);
										dupKey.Add(key2, dj2);
									}
									if (oddSlot == 0)
									{
										tris[t6] = dj2;
									}
									else if (oddSlot == 1)
									{
										tris[t6 + 1] = dj2;
									}
									else
									{
										tris[t6 + 2] = dj2;
									}
								}
							}
						}
					}
				}
				t6 += 3;
			}
			int nSk = vertsL.Count;
			float[] sEdge = new float[nSk];
			int[] sCnt = new int[nSk];
			int t10 = 0;
			while (t10 + 2 < tris.Count)
			{
				for (int e9 = 0; e9 < 3; e9++)
				{
					int va5 = tris[t10 + e9];
					int vb5 = tris[t10 + (e9 + 1) % 3];
					if (va5 < nSk && vb5 < nSk)
					{
						float L = (searchBody[mapL[va5]] - searchBody[mapL[vb5]]).magnitude;
						sEdge[va5] += L;
						sCnt[va5]++;
						sEdge[vb5] += L;
						sCnt[vb5]++;
					}
				}
				t10 += 3;
			}
			for (int j3 = 0; j3 < nSk; j3++)
			{
				if (sCnt[j3] > 0)
				{
					sEdge[j3] /= (float)sCnt[j3];
				}
			}
			Dictionary<long, List<int>> sHash = new Dictionary<long, List<int>>(nSk);
			for (int j4 = 0; j4 < nSk; j4++)
			{
				Vector3 pj = searchBody[mapL[j4]];
				long hk = ((long)Mathf.Floor(pj.x / 0.02f) & 2097151L) | (((long)Mathf.Floor(pj.y / 0.02f) & 2097151L) << 21) | (((long)Mathf.Floor(pj.z / 0.02f) & 2097151L) << 42);
				List<int> lj;
				if (!sHash.TryGetValue(hk, out lj))
				{
					lj = new List<int>(8);
					sHash.Add(hk, lj);
				}
				lj.Add(j4);
			}
			float areaAll = 0f;
			float areaCoarse = 0f;
			int faceAll = 0;
			int faceCoarse = 0;
			for (int f3 = 0; f3 < nFace; f3++)
			{
				if (f3 >= keepFace.Length || keepFace[f3])
				{
					int g6 = gFaces[f3 * 3];
					int g7 = gFaces[f3 * 3 + 1];
					int g8 = gFaces[f3 * 3 + 2];
					Vector3 p4 = searchCloth[g6];
					Vector3 p5 = searchCloth[g7];
					Vector3 p6 = searchCloth[g8];
					float ge = ((p5 - p4).magnitude + (p6 - p5).magnitude + (p4 - p6).magnitude) / 3f;
					if (ge >= 1E-06f)
					{
						Vector3 ctr = (p4 + p5 + p6) / 3f;
						float ar = Vector3.Cross(p5 - p4, p6 - p4).magnitude * 0.5f;
						long cx = (long)Mathf.Floor(ctr.x / 0.02f);
						long cy = (long)Mathf.Floor(ctr.y / 0.02f);
						long cz = (long)Mathf.Floor(ctr.z / 0.02f);
						float bd = 1E+09f;
						int bj = -1;
						for (long dx = -1L; dx <= 1L; dx += 1L)
						{
							for (long dy = -1L; dy <= 1L; dy += 1L)
							{
								for (long dz = -1L; dz <= 1L; dz += 1L)
								{
									long hk2 = ((cx + dx) & 2097151L) | (((cy + dy) & 2097151L) << 21) | (((cz + dz) & 2097151L) << 42);
									List<int> lj2;
									if (sHash.TryGetValue(hk2, out lj2))
									{
										for (int q4 = 0; q4 < lj2.Count; q4++)
										{
											float dd3 = (searchBody[mapL[lj2[q4]]] - ctr).sqrMagnitude;
											if (dd3 < bd)
											{
												bd = dd3;
												bj = lj2[q4];
											}
										}
									}
								}
							}
						}
						if (bj >= 0 && sCnt[bj] != 0)
						{
							areaAll += ar;
							faceAll++;
							if (sEdge[bj] > ge * 2f)
							{
								areaCoarse += ar;
								faceCoarse++;
							}
						}
					}
				}
			}
			float gBnd = 0f;
			float sBnd = 0f;
			List<float> gL = new List<float>(256);
			for (int f4 = 0; f4 < nFace; f4++)
			{
				if (f4 >= keepFace.Length || keepFace[f4])
				{
					for (int e10 = 0; e10 < 3; e10++)
					{
						int ga = gFaces[f4 * 3 + e10];
						int gb = gFaces[f4 * 3 + (e10 + 1) % 3];
						if (openEdge(ga, gb))
						{
							gL.Add((searchCloth[ga] - searchCloth[gb]).magnitude * 1000f);
						}
					}
				}
			}
			Dictionary<long, int> sc = new Dictionary<long, int>(tris.Count);
			int t11 = 0;
			while (t11 + 2 < tris.Count)
			{
				for (int e11 = 0; e11 < 3; e11++)
				{
					int va6 = mapL[tris[t11 + e11]];
					int vb6 = mapL[tris[t11 + (e11 + 1) % 3]];
					if (va6 != vb6)
					{
						long ek3 = ((va6 < vb6) ? (((long)va6 << 24) | (long)((ulong)vb6)) : (((long)vb6 << 24) | (long)((ulong)va6)));
						int c2;
						sc.TryGetValue(ek3, out c2);
						sc[ek3] = c2 + 1;
					}
				}
				t11 += 3;
			}
			List<float> sL = new List<float>(256);
			foreach (KeyValuePair<long, int> kv2 in sc)
			{
				if (kv2.Value == 1)
				{
					int va7 = (int)(kv2.Key >> 24);
					int vb7 = (int)(kv2.Key & 16777215L);
					sL.Add((searchBody[va7] - searchBody[vb7]).magnitude * 1000f);
				}
			}
			if (gL.Count >= 8 && sL.Count >= 8)
			{
				gL.Sort();
				sL.Sort();
				gBnd = gL[gL.Count / 2];
				sBnd = sL[sL.Count / 2];
			}
			float bndRatio = ((gBnd > 0.001f) ? (sBnd / gBnd) : 0f);
			float coarseFrac = ((areaAll > 1E-09f) ? (areaCoarse / areaAll) : 0f);
			bool tooCoarse = coarseFrac > 0.02f;
			bool tooBlunt = bndRatio > 1.25f;
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF]   fidelity: ",
				(coarseFrac * 100f).ToString("F1"),
				"% of garment area is 2x coarser on the body (",
				faceCoarse.ToString(),
				"/",
				faceAll.ToString(),
				" faces); hem ",
				gBnd.ToString("F1"),
				"mm vs skin ",
				sBnd.ToString("F1"),
				"mm = ",
				bndRatio.ToString("F2"),
				"x",
				tooCoarse ? " - DECLINED, the body cannot hold this detail" : "",
				tooBlunt ? " - DECLINED, the body cannot cut this silhouette" : ""
			}));
			if (tooCoarse || tooBlunt)
			{
				return false;
			}
			int nS = vertsL.Count;
			Vector3[] verts = new Vector3[nS];
			for (int j5 = 0; j5 < nS; j5++)
			{
				int bvI2 = mapL[j5];
				verts[j5] = body.baseVerts[bvI2] + offL2[j5] + body.baseNormals[bvI2] * this.clothLift;
			}
			Material[] mats0 = gsmr.sharedMaterials;
			List<Material> useMats = new List<Material>(2);
			int bestLen = -1;
			for (int sm3 = 0; sm3 < gclone.subMeshCount; sm3++)
			{
				int len = gclone.GetTriangles(sm3).Length;
				if (len > bestLen)
				{
					bestLen = len;
				}
			}
			int sm4 = 0;
			while (sm4 < gclone.subMeshCount && mats0 != null && sm4 < mats0.Length)
			{
				if (gclone.GetTriangles(sm4).Length * 10 >= bestLen * 3 && mats0[sm4] != null)
				{
					useMats.Add(mats0[sm4]);
				}
				sm4++;
			}
			if (useMats.Count == 0 && mats0 != null && mats0.Length != 0)
			{
				useMats.Add(mats0[0]);
			}
			Mesh mesh = new Mesh();
			mesh.name = "SF_skin_" + gsmr.name;
			mesh.vertices = verts;
			mesh.normals = normsL.ToArray();
			mesh.uv = uvL.ToArray();
			if (uv2L != null)
			{
				mesh.uv2 = uv2L.ToArray();
			}
			if (colL != null)
			{
				mesh.colors32 = colL.ToArray();
			}
			if (tanL != null)
			{
				mesh.tangents = tanL.ToArray();
			}
			mesh.boneWeights = bwsL.ToArray();
			mesh.bindposes = body.bindPoses;
			int[] triArr = tris.ToArray();
			mesh.subMeshCount = ((useMats.Count > 0) ? useMats.Count : 1);
			for (int sm5 = 0; sm5 < mesh.subMeshCount; sm5++)
			{
				mesh.SetTriangles(triArr, sm5);
			}
			int shapesMade = 0;
			List<int> shapeSrcL = new List<int>(8);
			if (shpC0 != null && gsmr.sharedMesh != null)
			{
				Mesh gsm = gsmr.sharedMesh;
				int nGv = gsm.vertexCount;
				Vector3[] cvBase = gclone.vertices;
				if (cvBase != null && cvBase.Length == nGv && searchCloth.Length == nGv)
				{
					Matrix4x4[] invS = null;
					if (bmB != null)
					{
						invS = new Matrix4x4[nS];
						for (int j6 = 0; j6 < nS; j6++)
						{
							invS[j6] = SoftFleshController.BlendM(body.bwArr[mapL[j6]], bmB).inverse;
						}
					}
					Vector3[] dV = new Vector3[nGv];
					if (SoftFleshController._bsN == null || SoftFleshController._bsN.Length != nGv)
					{
						SoftFleshController._bsN = new Vector3[nGv];
						SoftFleshController._bsT = new Vector3[nGv];
					}
					Vector3[] shaped = new Vector3[nGv];
					Vector3[] dSearch = new Vector3[nGv];
					Vector3[] sd = new Vector3[nS];
					for (int bs = 0; bs < gsm.blendShapeCount; bs++)
					{
						int fr = gsm.GetBlendShapeFrameCount(bs) - 1;
						if (fr >= 0)
						{
							try
							{
								gsm.GetBlendShapeFrameVertices(bs, fr, dV, SoftFleshController._bsN, SoftFleshController._bsT);
							}
							catch (Exception ex)
							{
								SoftFleshPlugin.Log("[SF] shape read failed on '" + gsmr.name + "': " + ex.Message);
								break;
							}
							for (int k2 = 0; k2 < nGv; k2++)
							{
								shaped[k2] = cvBase[k2] + dV[k2];
							}
							Vector3[] shSearch = (rec.usePose ? SoftFleshController.ClothSkin(shaped, rec.clothBones, gclone.bindposes, gclone.boneWeights) : shaped);
							if (shSearch != null && shSearch.Length == nGv)
							{
								for (int k3 = 0; k3 < nGv; k3++)
								{
									dSearch[k3] = shSearch[k3] - searchCloth[k3];
								}
								float big = 0f;
								for (int j7 = 0; j7 < nS; j7++)
								{
									Vector3 ww2 = shpW[j7];
									Vector3 dd4 = dSearch[shpC0[j7]] * ww2.x + dSearch[shpC1[j7]] * ww2.y + dSearch[shpC2[j7]] * ww2.z;
									if (invS != null)
									{
										dd4 = invS[j7].MultiplyVector(dd4);
									}
									sd[j7] = dd4;
									float m2 = dd4.sqrMagnitude;
									if (m2 > big)
									{
										big = m2;
									}
								}
								if (big >= 1E-08f)
								{
									mesh.AddBlendShapeFrame(gsm.GetBlendShapeName(bs), gsm.GetBlendShapeFrameWeight(bs, fr), sd, null, null);
									shapeSrcL.Add(bs);
									sd = new Vector3[nS];
									shapesMade++;
								}
							}
						}
					}
				}
			}
			mesh.RecalculateBounds();
			mesh.MarkDynamic();
			GameObject go = new GameObject("SF_skin_" + gsmr.name);
			go.layer = gsmr.gameObject.layer;
			go.transform.position = body.smr.transform.position;
			go.transform.rotation = body.smr.transform.rotation;
			go.transform.localScale = body.smr.transform.lossyScale;
			SkinnedMeshRenderer smr2 = go.AddComponent<SkinnedMeshRenderer>();
			smr2.bones = body.bonesArr;
			smr2.rootBone = body.smr.rootBone;
			smr2.sharedMesh = mesh;
			smr2.localBounds = body.smr.localBounds;
			smr2.updateWhenOffscreen = body.smr.updateWhenOffscreen;
			smr2.shadowCastingMode = gsmr.shadowCastingMode;
			smr2.receiveShadows = gsmr.receiveShadows;
			smr2.probeAnchor = ((gsmr.probeAnchor != null) ? gsmr.probeAnchor : gsmr.transform);
			smr2.lightProbeUsage = gsmr.lightProbeUsage;
			smr2.reflectionProbeUsage = gsmr.reflectionProbeUsage;
			smr2.sharedMaterials = useMats.ToArray();
			gsmr.enabled = false;
			SoftFleshController.SkinRec sk = new SoftFleshController.SkinRec();
			sk.shapeN = shapesMade;
			if (shapesMade > 0)
			{
				sk.shapeSrc = shapeSrcL.ToArray();
				sk.shapeW = new float[shapesMade];
				float[] w4 = SoftFleshController.AuthoredShapeWeights(gsmr, gsmr.sharedMesh.blendShapeCount);
				StringBuilder sbW2 = new StringBuilder();
				if (w4 != null)
				{
					for (int k4 = 0; k4 < w4.Length; k4++)
					{
						gsmr.SetBlendShapeWeight(k4, w4[k4]);
						if (k4 > 0)
						{
							sbW2.Append(' ');
						}
						sbW2.Append(gsmr.sharedMesh.GetBlendShapeName(k4)).Append('=').Append(w4[k4].ToString("F0"));
					}
				}
				string text = "[SF] skin '";
				string name = gsmr.name;
				string text2 = "' shapes: ";
				StringBuilder stringBuilder = sbW2;
				SoftFleshPlugin.Log(text + name + text2 + ((stringBuilder != null) ? stringBuilder.ToString() : null));
			}
			sk.origSmr = gsmr;
			sk.origMesh = gsmr.sharedMesh;
			sk.slot = slot;
			sk.matCache = gsmr.sharedMaterials;
			sk.go = go;
			sk.smr = smr2;
			sk.mesh = mesh;
			sk.body = body;
			sk.map = mapL.ToArray();
			sk.offV = offL2.ToArray();
			sk.scrV = new List<Vector3>(nS);
			sk.scrN = new List<Vector3>(nS);
			for (int j8 = 0; j8 < nS; j8++)
			{
				sk.scrV.Add(verts[j8]);
				sk.scrN.Add(normsL[j8]);
			}
			this._skins.Add(sk);
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] second skin: ",
				nS.ToString(),
				" verts / ",
				(tris.Count / 3).ToString(),
				" tris (",
				dupKey.Count.ToString(),
				" seam dups, ",
				shapesMade.ToString(),
				" shapes (",
				seamExtrap.ToString(),
				" extrap, ",
				tornTri.ToString(),
				" torn), canon ",
				canonDropped.ToString(),
				" dropped, ",
				islandCount.ToString(),
				" islands, local ",
				localTri.ToString(),
				"/",
				localDup.ToString(),
				"/",
				localTear.ToString(),
				", tearUV ",
				tearUV.ToString("F6"),
				") on '",
				gsmr.name,
				"' (fit ",
				(meanFit * 1000f).ToString("F1"),
				"mm)"
			}));
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF]   census: rows ",
				rec.revBody.Length.ToString(),
				", deeper-than-15mm ",
				far15.ToString(),
				", rejected far ",
				rejFar.ToString(),
				" / edge ",
				rejEdge.ToString(),
				", hole-filled ",
				filled.ToString(),
				rec.usePose ? "  [pose]" : "  [bind]"
			}));
			for (int sm6 = 0; sm6 < gclone.subMeshCount; sm6++)
			{
				Material mm3 = ((mats0 != null && sm6 < mats0.Length) ? mats0[sm6] : null);
				SoftFleshPlugin.Log(string.Concat(new string[]
				{
					"[SF]   sub",
					sm6.ToString(),
					": ",
					(gclone.GetTriangles(sm6).Length / 3).ToString(),
					" tris, mat '",
					(mm3 != null) ? mm3.name : "null",
					"' shader '",
					(mm3 != null && mm3.shader != null) ? mm3.shader.name : "-",
					"' q",
					((mm3 != null) ? mm3.renderQueue : 0).ToString(),
					useMats.Contains(mm3) ? "  [USED]" : "  [skip]"
				}));
			}
			return true;
		}

		// Token: 0x060000C8 RID: 200 RVA: 0x00030F58 File Offset: 0x0002F158
		private void UpdateSecondSkins(SoftFleshController.Body body)
		{
			if (this._skins.Count == 0)
			{
				return;
			}
			long _tskin = SoftFleshController.SwNow();
			try
			{
				this.UpdateSecondSkinsInner(body);
			}
			finally
			{
				SoftFleshController.PhAdd(10, _tskin);
			}
		}

		// Token: 0x060000C9 RID: 201 RVA: 0x00030F9C File Offset: 0x0002F19C
		private void UpdateSecondSkinsInner(SoftFleshController.Body body)
		{
			for (int r = this._skins.Count - 1; r >= 0; r--)
			{
				SoftFleshController.SkinRec sk = this._skins[r];
				if (sk.body == body)
				{
					if (sk.origSmr == null || sk.origSmr.sharedMesh != sk.origMesh)
					{
						if (sk.origSmr != null)
						{
							sk.origSmr.enabled = true;
						}
						if (sk.go != null)
						{
							Object.Destroy(sk.go);
						}
						if (sk.mesh != null)
						{
							Object.Destroy(sk.mesh);
						}
						if (sk.body != null)
						{
							sk.body.clothBound = false;
						}
						this._skins.RemoveAt(r);
					}
					else
					{
						byte stNow = byte.MaxValue;
						ChaControl cha2 = ((sk.body != null) ? sk.body.cha : null);
						if (cha2 != null && cha2.fileStatus != null && cha2.fileStatus.clothesState != null && sk.slot >= 0 && sk.slot < cha2.fileStatus.clothesState.Length)
						{
							stNow = cha2.fileStatus.clothesState[sk.slot];
						}
						bool actNow = sk.origSmr.gameObject.activeInHierarchy;
						bool enNow = sk.origSmr.enabled;
						if (sk.matCache != null && sk.matCache.Length != 0)
						{
							bool suspect = sk.origSmr.sharedMaterial != sk.matCache[0] || stNow != sk.lastState || actNow != sk.lastActive;
							bool same = true;
							if (suspect)
							{
								Material[] mNow = sk.origSmr.sharedMaterials;
								same = mNow != null && mNow.Length == sk.matCache.Length;
								if (same)
								{
									for (int m4 = 0; m4 < mNow.Length; m4++)
									{
										if (mNow[m4] != sk.matCache[m4])
										{
											same = false;
											break;
										}
									}
								}
							}
							if (!same)
							{
								SoftFleshPlugin.Log("[SF][skin] materials changed on '" + sk.origSmr.name + "' - scheduling re-bind");
								sk.origSmr.enabled = true;
								if (sk.go != null)
								{
									Object.Destroy(sk.go);
								}
								if (sk.mesh != null)
								{
									Object.Destroy(sk.mesh);
								}
								if (sk.body != null)
								{
									sk.body.clothBound = false;
								}
								this._skins.RemoveAt(r);
								goto IL_089D;
							}
						}
						if (sk.shapeN > 0 && sk.smr != null)
						{
							for (int bs = 0; bs < sk.shapeN; bs++)
							{
								float w2 = sk.origSmr.GetBlendShapeWeight(sk.shapeSrc[bs]);
								if (w2 != sk.shapeW[bs])
								{
									sk.shapeW[bs] = w2;
									sk.smr.SetBlendShapeWeight(bs, w2);
								}
							}
						}
						if (stNow != sk.lastState || actNow != sk.lastActive || enNow != sk.lastEnabled)
						{
							SoftFleshPlugin.Log(string.Concat(new string[]
							{
								"[SF][skin] '",
								sk.origSmr.name,
								"' slot ",
								sk.slot.ToString(),
								" state ",
								sk.lastState.ToString(),
								"->",
								stNow.ToString(),
								" activeInH ",
								sk.lastActive.ToString(),
								"->",
								actNow.ToString(),
								" enabled ",
								sk.lastEnabled.ToString(),
								"->",
								enNow.ToString()
							}));
							sk.lastState = stNow;
							sk.lastActive = actNow;
							sk.lastEnabled = enNow;
							if (cha2 != null && cha2.objClothes != null && sk.slot >= 0 && sk.slot < cha2.objClothes.Length && cha2.objClothes[sk.slot] != null)
							{
								Renderer[] rends = cha2.objClothes[sk.slot].GetComponentsInChildren<Renderer>(true);
								StringBuilder sb2 = new StringBuilder("[SF][slot] probe around '");
								sb2.Append(sk.origSmr.name).Append("':");
								foreach (Renderer rd in rends)
								{
									sb2.Append("\n   ").Append(rd.name).Append(" actSelf=")
										.Append(rd.gameObject.activeSelf)
										.Append(" actInH=")
										.Append(rd.gameObject.activeInHierarchy)
										.Append(" en=")
										.Append(rd.enabled);
									foreach (Material mt in rd.sharedMaterials)
									{
										Texture tx = ((mt != null) ? mt.mainTexture : null);
										sb2.Append(" | ").Append((mt != null) ? mt.name : "null").Append(':')
											.Append((tx != null) ? string.Concat(new string[]
											{
												tx.name,
												" ",
												tx.width.ToString(),
												"x",
												tx.height.ToString()
											}) : "noTex");
									}
								}
								if (sk.smr != null)
								{
									sb2.Append("\n   SKIN ").Append(sk.smr.name).Append(" en=")
										.Append(sk.smr.enabled);
									foreach (Material mt2 in sk.smr.sharedMaterials)
									{
										Texture tx2 = ((mt2 != null) ? mt2.mainTexture : null);
										sb2.Append(" | ").Append((mt2 != null) ? mt2.name : "null").Append(':')
											.Append((tx2 != null) ? string.Concat(new string[]
											{
												tx2.name,
												" ",
												tx2.width.ToString(),
												"x",
												tx2.height.ToString()
											}) : "noTex");
									}
								}
								SoftFleshPlugin.Log(sb2.ToString());
							}
						}
						if (enNow)
						{
							sk.wantVis = true;
							sk.origSmr.enabled = false;
							sk.lastEnabled = false;
						}
						bool vis = sk.wantVis && actNow;
						if (sk.smr != null && sk.smr.enabled != vis)
						{
							sk.smr.enabled = vis;
						}
						if (!vis)
						{
							sk.lastVis = false;
						}
						else if (sk.lastVersion != body.outVersion || !sk.lastVis)
						{
							sk.lastVersion = body.outVersion;
							sk.lastVis = true;
							List<Vector3> oV = body.outV;
							List<Vector3> oN = body.normList;
							if (oV.Count == body.baseVerts.Length && oN.Count == body.baseVerts.Length)
							{
								List<Vector3> sv = sk.scrV;
								List<Vector3> sn2 = sk.scrN;
								for (int i = 0; i < sk.map.Length; i++)
								{
									int bvI = sk.map[i];
									Vector3 j = oN[bvI];
									Vector3 ov2 = sk.offV[i];
									Vector3 pv = oV[bvI];
									pv.x += ov2.x + j.x * this.clothLift;
									pv.y += ov2.y + j.y * this.clothLift;
									pv.z += ov2.z + j.z * this.clothLift;
									sv[i] = pv;
									sn2[i] = j;
								}
								sk.mesh.SetVertices(sv);
								sk.mesh.SetNormals(sn2);
							}
						}
					}
				}
				IL_089D:;
			}
		}

		// Token: 0x060000CA RID: 202 RVA: 0x00031854 File Offset: 0x0002FA54
		private void SubdivSurfaceFix(SoftFleshController.Body body)
		{
			if (body.subPa == null || body.subFix == null || body.bwArr == null || body.bonesArr == null || body.workMesh == null)
			{
				return;
			}
			Matrix4x4[] binds = body.bindPoses;
			if (binds == null || binds.Length < body.bonesArr.Length)
			{
				return;
			}
			float h = 0f;
			for (int b2 = 0; b2 < body.bonesArr.Length; b2++)
			{
				Transform t = body.bonesArr[b2];
				if (!(t == null))
				{
					Vector3 pp = t.position;
					Quaternion qq = t.rotation;
					h += pp.x * 3.7f + pp.y * 11.3f + pp.z * 23.9f + qq.x * 41.1f + qq.y * 53.3f + qq.z * 67.7f + qq.w * 79.9f;
				}
			}
			if (body.subFix != null && Mathf.Abs(h - body.subPoseHash) < 1E-06f)
			{
				return;
			}
			body.subPoseHash = h;
			int nb = body.bonesArr.Length;
			if (SoftFleshController._subSkinM == null || SoftFleshController._subSkinM.Length != nb)
			{
				SoftFleshController._subSkinM = new Matrix4x4[nb];
			}
			if (SoftFleshController._subInvRot == null || SoftFleshController._subInvRot.Length != nb)
			{
				SoftFleshController._subInvRot = new Quaternion[nb];
			}
			for (int b3 = 0; b3 < nb; b3++)
			{
				Transform tb = body.bonesArr[b3];
				SoftFleshController._subSkinM[b3] = ((tb != null) ? (tb.localToWorldMatrix * binds[b3]) : Matrix4x4.identity);
				SoftFleshController._subInvRot[b3] = ((tb != null) ? Quaternion.Inverse(tb.rotation * body.bindRot[b3]) : Quaternion.identity);
			}
			int n0 = body.origVertN;
			int nAll = body.subPa.Length;
			if (SoftFleshController._subCorrW == null || SoftFleshController._subCorrW.Length != nAll)
			{
				SoftFleshController._subCorrW = new Vector3[nAll];
			}
			for (int pass = 0; pass < 2; pass++)
			{
				int ps = pass;
				SoftFleshController.ParallelFor(nAll - n0, delegate(int _q0, int _q1)
				{
					for (int q = _q0; q < _q1; q++)
					{
						int i = n0 + q;
						int pa = body.subPa[i];
						if (pa < 0)
						{
							if (ps == 0)
							{
								body.subFix[i] = Vector3.zero;
								SoftFleshController._subCorrW[i] = Vector3.zero;
							}
						}
						else
						{
							int pb = body.subPb[i];
							bool deep = pa >= n0 || pb >= n0;
							if (deep == (ps == 1))
							{
								Vector3 pM = SoftFleshController.SubSkinPt(body, i, SoftFleshController._subSkinM);
								Vector3 pA = SoftFleshController.SubSkinPt(body, pa, SoftFleshController._subSkinM) + ((pa >= n0) ? SoftFleshController._subCorrW[pa] : Vector3.zero);
								Vector3 pB = SoftFleshController.SubSkinPt(body, pb, SoftFleshController._subSkinM) + ((pb >= n0) ? SoftFleshController._subCorrW[pb] : Vector3.zero);
								Vector3 corrW = (pA + pB) * 0.5f - pM;
								SoftFleshController._subCorrW[i] = corrW;
								int db = ((body.domBone != null) ? body.domBone[i] : (-1));
								body.subFix[i] = ((db >= 0 && db < nb) ? (SoftFleshController._subInvRot[db] * corrW) : corrW);
							}
						}
					}
				});
			}
			body.subFixDirty = true;
		}

		// Token: 0x060000CB RID: 203 RVA: 0x00031B4D File Offset: 0x0002FD4D
		private static void UploadBodyVerts(SoftFleshController.Body body, bool withNormals)
		{
			body.workMesh.SetVertices(body.outV);
			if (withNormals)
			{
				body.workMesh.SetNormals(body.normList);
			}
			body.outVersion++;
		}

		// Token: 0x060000CC RID: 204 RVA: 0x00031B84 File Offset: 0x0002FD84
		private static Vector3 SubSkinPt(SoftFleshController.Body body, int i, Matrix4x4[] m)
		{
			BoneWeight w = body.bwArr[i];
			Vector3 v = body.baseVerts[i];
			Vector3 acc = Vector3.zero;
			if (w.weight0 > 0f)
			{
				acc += m[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
			}
			if (w.weight1 > 0f)
			{
				acc += m[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
			}
			if (w.weight2 > 0f)
			{
				acc += m[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
			}
			if (w.weight3 > 0f)
			{
				acc += m[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
			}
			return acc;
		}

		// Token: 0x060000CD RID: 205 RVA: 0x00031C84 File Offset: 0x0002FE84
		private void ClothTick(SoftFleshController.Body body)
		{
			if (!this.clothFollow)
			{
				this.RestoreCloth();
				return;
			}
			if (!body.clothBound && !body.isFace)
			{
				this.BindClothes(body);
			}
			if (Mathf.Abs(this.clothLift - this._clothLiftApplied) > 1E-05f)
			{
				this._clothLiftApplied = this.clothLift;
				for (int b3 = 0; b3 < this._bodies.Count; b3++)
				{
					this._bodies[b3].outVersion++;
				}
				for (int r = 0; r < this._cloth.Count; r++)
				{
					SoftFleshController.ClothRec rc = this._cloth[r];
					if (!(rc.clone == null) && !(rc.smr == null) && !(rc.smr.sharedMesh != rc.clone))
					{
						for (int i = 0; i < rc.rawV.Length; i++)
						{
							rc.baseV[i] = rc.rawV[i] + rc.liftN[i] * (this.clothLift * ((rc.liftW != null) ? rc.liftW[i] : 1f));
						}
						Array.Copy(rc.baseV, rc.work, rc.baseV.Length);
						rc.clone.vertices = rc.work;
						rc.active = false;
					}
				}
			}
		}

		// Token: 0x060000CE RID: 206 RVA: 0x00031E04 File Offset: 0x00030004
		private void ApplyClothFollow(SoftFleshController.Body body)
		{
			if (!this.clothFollow)
			{
				return;
			}
			long _tcloth = SoftFleshController.SwNow();
			try
			{
				float[] rdo = body.renderDisp;
				if (rdo != null && body.bonesArr != null)
				{
					if (SoftFleshController._clRotScratch == null || SoftFleshController._clRotScratch.Length != body.bonesArr.Length)
					{
						SoftFleshController._clRotScratch = new Quaternion[body.bonesArr.Length];
					}
					for (int b2 = 0; b2 < body.bonesArr.Length; b2++)
					{
						SoftFleshController._clRotScratch[b2] = ((body.bonesArr[b2] != null) ? (body.bonesArr[b2].rotation * body.bindRot[b2]) : Quaternion.identity);
					}
					for (int r = this._cloth.Count - 1; r >= 0; r--)
					{
						SoftFleshController.ClothRec rec = this._cloth[r];
						if (rec.body == body)
						{
							if (rec.smr == null)
							{
								if (rec.clone != null)
								{
									Object.Destroy(rec.clone);
								}
								if (rec.body != null)
								{
									rec.body.clothBound = false;
								}
								this._cloth.RemoveAt(r);
							}
							else if (rec.smr.sharedMesh != rec.clone)
							{
								SoftFleshPlugin.Log(string.Concat(new string[]
								{
									"[SF] cloth: foreign mesh '",
									(rec.smr.sharedMesh != null) ? rec.smr.sharedMesh.name : "null",
									"' appeared on '",
									rec.smr.name,
									"' - scheduling re-bind"
								}));
								if (rec.clone != null)
								{
									Object.Destroy(rec.clone);
								}
								if (rec.body != null)
								{
									rec.body.clothBound = false;
								}
								this._cloth.RemoveAt(r);
							}
							else if (rec.smr.gameObject.activeInHierarchy)
							{
								if (rec.usePose && rec.clothBindRot != null && rec.clothBones != null)
								{
									int nrb = rec.clothBindRot.Length;
									if (rec.clothRotInv == null || rec.clothRotInv.Length != nrb)
									{
										rec.clothRotInv = new Quaternion[nrb];
									}
									for (int b3 = 0; b3 < nrb; b3++)
									{
										rec.clothRotInv[b3] = ((b3 < rec.clothBones.Length && rec.clothBones[b3] != null) ? Quaternion.Inverse(rec.clothBones[b3].rotation * rec.clothBindRot[b3]) : Quaternion.identity);
									}
								}
								bool any = false;
								bool anyN = false;
								for (int i = 0; i < rec.triA.Length; i++)
								{
									if (rec.isRider == null || !rec.isRider[i])
									{
										int ia = rec.triA[i];
										if (ia < 0)
										{
											rec.work[i] = rec.baseV[i];
										}
										else
										{
											int ib = rec.triB[i];
											int ic = rec.triC[i];
											Vector3 w3 = rec.triW[i];
											Vector3 dl = Vector3.zero;
											Vector3 dn = Vector3.zero;
											bool nz = false;
											int domBody = ia;
											float pMin = 0f;
											float pMax = 0f;
											float outSelf = 0f;
											float bw = w3.x;
											if (w3.y > bw)
											{
												bw = w3.y;
												domBody = ib;
											}
											if (w3.z > bw)
											{
												domBody = ic;
											}
											for (int corner = 0; corner < 3; corner++)
											{
												int b4 = ((corner == 0) ? ia : ((corner == 1) ? ib : ic));
												float cw = ((corner == 0) ? w3.x : ((corner == 1) ? w3.y : w3.z));
												if (cw > 0f)
												{
													float dv = rdo[b4];
													Vector3 pb = ((body.pushR != null) ? body.pushR[b4] : body.pushB[b4]);
													if (body.corrB != null)
													{
														pb += body.corrB[b4];
													}
													if (rec.nrm0 != null)
													{
														Vector3 bn3 = body.baseNormals[b4];
														Vector3 cn3 = body.normList[b4];
														dn.x += (cn3.x - bn3.x) * cw;
														dn.y += (cn3.y - bn3.y) * cw;
														dn.z += (cn3.z - bn3.z) * cw;
													}
													if (dv != 0f || pb.sqrMagnitude > 1E-10f)
													{
														int sg = ((body.seamOf != null) ? body.seamOf[b4] : (-1));
														Vector3 dirN = ((sg >= 0) ? body.seamN[sg] : body.baseNormals[b4]);
														outSelf += (-dv + pb.x * dirN.x + pb.y * dirN.y + pb.z * dirN.z) * cw;
														float cx2 = pb.x - dirN.x * dv;
														float cy2 = pb.y - dirN.y * dv;
														float cz2 = pb.z - dirN.z * dv;
														dl.x += cx2 * cw;
														dl.y += cy2 * cw;
														dl.z += cz2 * cw;
														Vector3 cn4 = ((rec.liftN != null) ? rec.liftN[i] : Vector3.zero);
														float proj = cx2 * cn4.x + cy2 * cn4.y + cz2 * cn4.z;
														if (!nz)
														{
															pMin = proj;
															pMax = proj;
														}
														else
														{
															if (proj < pMin)
															{
																pMin = proj;
															}
															if (proj > pMax)
															{
																pMax = proj;
															}
														}
														nz = true;
													}
												}
											}
											if (rec.nrmW != null)
											{
												Vector3 na = rec.nrm0[i];
												if (dn.x != 0f || dn.y != 0f || dn.z != 0f)
												{
													if (rec.usePose)
													{
														int dbN = ((body.domBone != null) ? body.domBone[domBody] : (-1));
														int dcN = ((rec.domCloth != null) ? rec.domCloth[i] : (-1));
														if (dbN >= 0 && dbN < SoftFleshController._clRotScratch.Length && rec.clothRotInv != null && dcN >= 0 && dcN < rec.clothRotInv.Length)
														{
															dn = rec.clothRotInv[dcN] * (SoftFleshController._clRotScratch[dbN] * dn);
														}
													}
													na.x += dn.x;
													na.y += dn.y;
													na.z += dn.z;
													float nm2 = na.x * na.x + na.y * na.y + na.z * na.z;
													if (nm2 > 1E-12f)
													{
														float inm = 1f / Mathf.Sqrt(nm2);
														na.x *= inm;
														na.y *= inm;
														na.z *= inm;
													}
												}
												if (rec.nrmW[i].x != na.x || rec.nrmW[i].y != na.y || rec.nrmW[i].z != na.z)
												{
													rec.nrmW[i] = na;
													anyN = true;
												}
											}
											float crest = 0f;
											bool pressed = body.nearPress != null && (body.nearPress[ia] != 0 || body.nearPress[ib] != 0 || body.nearPress[ic] > 0);
											if (pressed && this.clothSubdiv && rec.nbrStart != null && i + 1 < rec.nbrStart.Length)
											{
												for (int nq = rec.nbrStart[i]; nq < rec.nbrStart[i + 1]; nq++)
												{
													int nb2 = rec.nbrList[nq];
													float ov = -rdo[nb2];
													Vector3 pv = ((body.pushR != null) ? body.pushR[nb2] : body.pushB[nb2]);
													if (body.corrB != null)
													{
														pv += body.corrB[nb2];
													}
													if (pv.x != 0f || pv.y != 0f || pv.z != 0f)
													{
														int sg2 = ((body.seamOf != null) ? body.seamOf[nb2] : (-1));
														Vector3 nn2 = ((sg2 >= 0) ? body.seamN[sg2] : body.baseNormals[nb2]);
														ov += pv.x * nn2.x + pv.y * nn2.y + pv.z * nn2.z;
													}
													float cc2 = (ov - outSelf) * ((rec.nbrW != null) ? rec.nbrW[nq] : 1f);
													if (cc2 > crest)
													{
														crest = cc2;
													}
												}
												if (crest > 0.012f)
												{
													crest = 0.012f;
												}
												if (crest < 0f)
												{
													crest = 0f;
												}
											}
											if (!nz && crest <= 0f)
											{
												rec.work[i] = rec.baseV[i];
											}
											else
											{
												float puff = (pMax - pMin) * 0.5f;
												if (puff > 0.002f)
												{
													puff = 0.002f;
												}
												if (rec.usePose)
												{
													int db = ((body.domBone != null) ? body.domBone[domBody] : (-1));
													int dc = ((rec.domCloth != null) ? rec.domCloth[i] : (-1));
													if (db >= 0 && db < SoftFleshController._clRotScratch.Length && rec.clothRotInv != null && dc >= 0 && dc < rec.clothRotInv.Length)
													{
														dl = rec.clothRotInv[dc] * (SoftFleshController._clRotScratch[db] * dl);
													}
												}
												if (puff > 0f)
												{
													Vector3 pn = ((rec.liftN != null) ? rec.liftN[i] : Vector3.zero);
													dl.x += pn.x * puff;
													dl.y += pn.y * puff;
													dl.z += pn.z * puff;
												}
												if (crest > 0f)
												{
													Vector3 cn5 = ((rec.liftN != null) ? rec.liftN[i] : Vector3.zero);
													dl.x += cn5.x * crest;
													dl.y += cn5.y * crest;
													dl.z += cn5.z * crest;
												}
												rec.work[i].x = rec.baseV[i].x + dl.x;
												rec.work[i].y = rec.baseV[i].y + dl.y;
												rec.work[i].z = rec.baseV[i].z + dl.z;
												any = true;
											}
										}
									}
								}
								if (rec.riderIdx != null && rec.riderIdx.Length != 0 && body.bwArr != null && body.bindPoses != null && rec.gBind != null)
								{
									int nbB = body.bonesArr.Length;
									if (SoftFleshController._glueBM == null || SoftFleshController._glueBM.Length != nbB)
									{
										SoftFleshController._glueBM = new Matrix4x4[nbB];
										SoftFleshController._glueFrame = -1;
									}
									if (SoftFleshController._glueFrame != Time.frameCount || SoftFleshController._glueBody != body)
									{
										SoftFleshController._glueFrame = Time.frameCount;
										SoftFleshController._glueBody = body;
										for (int b5 = 0; b5 < nbB; b5++)
										{
											SoftFleshController._glueBM[b5] = ((body.bonesArr[b5] != null) ? (body.bonesArr[b5].localToWorldMatrix * body.bindPoses[b5]) : Matrix4x4.identity);
										}
									}
									Transform[] gbn = rec.clothBones;
									int nbG = ((gbn != null && gbn.Length < rec.gBind.Length) ? gbn.Length : rec.gBind.Length);
									if (rec.gSkinInv == null || rec.gSkinInv.Length != nbG)
									{
										rec.gSkinInv = new Matrix4x4[nbG];
									}
									for (int b6 = 0; b6 < nbG; b6++)
									{
										rec.gSkinInv[b6] = ((gbn != null && gbn[b6] != null) ? (gbn[b6].localToWorldMatrix * rec.gBind[b6]).inverse : Matrix4x4.identity);
									}
									for (int q2 = 0; q2 < rec.riderIdx.Length; q2++)
									{
										int gi = rec.riderIdx[q2];
										int vb2 = rec.riderBodyV[q2];
										Vector3 restB = ((vb2 < body.outV.Count) ? body.outV[vb2] : body.baseVerts[vb2]);
										Vector3 bodyW = SoftFleshController.SkinW(restB, body.bwArr[vb2], SoftFleshController._glueBM);
										int db2 = body.domBone[vb2];
										Quaternion qb = ((db2 >= 0 && db2 < nbB && body.bonesArr[db2] != null) ? (body.bonesArr[db2].rotation * body.bindRot[db2]) : Quaternion.identity);
										Vector3 want = bodyW + qb * rec.riderOffB[q2];
										int dc2 = ((rec.domCloth != null && gi < rec.domCloth.Length) ? rec.domCloth[gi] : (-1));
										Vector3 nw2 = ((dc2 >= 0 && dc2 < rec.gSkinInv.Length) ? rec.gSkinInv[dc2].MultiplyPoint3x4(want) : rec.baseV[gi]);
										if ((nw2 - rec.work[gi]).sqrMagnitude > 1E-12f)
										{
											any = true;
										}
										rec.work[gi] = nw2;
									}
								}
								if (any && rec.grpStart != null)
								{
									int gi2 = 0;
									while (gi2 + 1 < rec.grpStart.Length)
									{
										int g0 = rec.grpStart[gi2];
										int g = rec.grpStart[gi2 + 1];
										Vector3 avg = Vector3.zero;
										for (int j = g0; j < g; j++)
										{
											avg += rec.work[rec.grpList[j]];
										}
										avg /= (float)(g - g0);
										for (int k = g0; k < g; k++)
										{
											rec.work[rec.grpList[k]] = avg;
										}
										gi2 += 2;
									}
								}
								if (this.spanGuard && !rec.usePose && rec.revBody != null && body.outV.Count == body.baseVerts.Length)
								{
									if (SoftFleshController._wrapFrame != Time.frameCount)
									{
										SoftFleshController._wrapFrame = Time.frameCount;
										SoftFleshController.PenBindN = 0;
										SoftFleshController.PenBindMm = 0f;
										SoftFleshController.PenPoseN = 0;
										SoftFleshController.PenPoseMm = 0f;
										SoftFleshController.PenPeakMm *= 0.985f;
									}
									float[] nl = rec.needLift;
									Array.Clear(nl, 0, nl.Length);
									float clr = ((this.clothLift > 0.0005f) ? this.clothLift : 0.0005f);
									bool wrapAny = false;
									for (int r2 = 0; r2 < rec.revBody.Length; r2++)
									{
										if (rec.revHem == null || r2 >= rec.revHem.Length || !rec.revHem[r2])
										{
											int c0 = rec.revTri[r2 * 3];
											int c = rec.revTri[r2 * 3 + 1];
											int c2 = rec.revTri[r2 * 3 + 2];
											Vector3 w4 = rec.revBary[r2];
											Vector3 S = rec.work[c0] * w4.x + rec.work[c] * w4.y + rec.work[c2] * w4.z;
											Vector3 nS = rec.liftN[c0] * w4.x + rec.liftN[c] * w4.y + rec.liftN[c2] * w4.z;
											Vector3 bp = body.outV[rec.revBody[r2]];
											float v3 = (bp.x - S.x) * nS.x + (bp.y - S.y) * nS.y + (bp.z - S.z) * nS.z;
											if (rec.revRest != null && r2 < rec.revRest.Length)
											{
												v3 -= rec.revRest[r2];
											}
											else
											{
												v3 += clr;
											}
											if (v3 > 0f)
											{
												if (v3 > 0.02f)
												{
													v3 = 0.02f;
												}
												float vm = v3 * 1000f;
												SoftFleshController.PenBindN++;
												if (vm > SoftFleshController.PenBindMm)
												{
													SoftFleshController.PenBindMm = vm;
												}
												if (vm > SoftFleshController.PenPeakMm)
												{
													SoftFleshController.PenPeakMm = vm;
												}
												if (v3 > nl[c0])
												{
													nl[c0] = v3;
												}
												if (v3 > nl[c])
												{
													nl[c] = v3;
												}
												if (v3 > nl[c2])
												{
													nl[c2] = v3;
												}
												wrapAny = true;
											}
										}
									}
									if (wrapAny)
									{
										if (rec.grpStart != null)
										{
											int gi3 = 0;
											while (gi3 + 1 < rec.grpStart.Length)
											{
												int g2 = rec.grpStart[gi3];
												int g3 = rec.grpStart[gi3 + 1];
												float gmax = 0f;
												for (int l = g2; l < g3; l++)
												{
													if (nl[rec.grpList[l]] > gmax)
													{
														gmax = nl[rec.grpList[l]];
													}
												}
												if (gmax > 0f)
												{
													for (int m = g2; m < g3; m++)
													{
														nl[rec.grpList[m]] = gmax;
													}
												}
												gi3 += 2;
											}
										}
										for (int k2 = 0; k2 < nl.Length; k2++)
										{
											if (nl[k2] > 0f)
											{
												rec.work[k2] += rec.liftN[k2] * nl[k2];
											}
										}
										any = true;
									}
								}
								if (any || anyN || rec.active)
								{
									rec.clone.vertices = rec.work;
									if (rec.nrmW != null)
									{
										rec.clone.normals = rec.nrmW;
									}
									rec.active = any || anyN;
								}
							}
						}
					}
				}
			}
			finally
			{
				SoftFleshController.PhAdd(9, _tcloth);
			}
		}

		// Token: 0x060000CF RID: 207 RVA: 0x00033278 File Offset: 0x00031478
		public void RestoreCloth()
		{
			for (int r = 0; r < this._cloth.Count; r++)
			{
				SoftFleshController.ClothRec rec = this._cloth[r];
				if (rec.smr != null && rec.orig != null)
				{
					Mesh cur = rec.smr.sharedMesh;
					if (cur == rec.clone || cur == null || (cur != rec.orig && (cur.name.StartsWith("SF_cloth_") || cur.name.EndsWith("(Clone)"))))
					{
						int nW = ((cur != null) ? cur.blendShapeCount : 0);
						float[] wS = null;
						if (nW > 0)
						{
							wS = new float[nW];
							for (int i = 0; i < nW; i++)
							{
								wS[i] = rec.smr.GetBlendShapeWeight(i);
							}
						}
						rec.smr.sharedMesh = rec.orig;
						if (wS != null)
						{
							int nO = rec.orig.blendShapeCount;
							int j = 0;
							while (j < nW && j < nO)
							{
								if (wS[j] != 0f)
								{
									rec.smr.SetBlendShapeWeight(j, wS[j]);
								}
								j++;
							}
						}
					}
				}
				if (rec.clone != null)
				{
					Object.Destroy(rec.clone);
				}
			}
			for (int r2 = this._skins.Count - 1; r2 >= 0; r2--)
			{
				SoftFleshController.SkinRec sk = this._skins[r2];
				if (sk.origSmr != null)
				{
					sk.origSmr.enabled = true;
				}
				if (sk.go != null)
				{
					Object.Destroy(sk.go);
				}
				if (sk.mesh != null)
				{
					Object.Destroy(sk.mesh);
				}
			}
			if (this._skins.Count > 0)
			{
				SoftFleshPlugin.Log("[SF] second skin: restored " + this._skins.Count.ToString() + " garment renderer(s)");
			}
			this._skins.Clear();
			if (this._cloth.Count > 0)
			{
				SoftFleshPlugin.Log("[SF] cloth: restored " + this._cloth.Count.ToString() + " garment renderer(s)");
			}
			this._cloth.Clear();
			for (int b = 0; b < this._bodies.Count; b++)
			{
				this._bodies[b].clothBound = false;
				this._bodies[b].clothCovered = null;
			}
			this._clothLiftApplied = -1f;
			SoftFleshController.ClothInfo = "off";
		}

		// Token: 0x060000D0 RID: 208 RVA: 0x00033534 File Offset: 0x00031734
		public void SurveyClothing()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("SoftFlesh clothing survey\n");
			sb.Append("renderer\tcontainer\tverts\ttris\treadable\tshapes\tbones\tsharedBones\t").Append("bindMed_mm\tbindP90_mm\tposeMed_mm\tposeP90_mm\tnearBind%\tnearPose%\tvertsPerBodyVert\n");
			ChaControl[] chas = Object.FindObjectsOfType<ChaControl>();
			int rows = 0;
			foreach (ChaControl cha in chas)
			{
				if (!(cha == null) && !(cha.objBodyBone == null))
				{
					SoftFleshController.Body bd = null;
					for (int b2 = 0; b2 < this._bodies.Count; b2++)
					{
						if (this._bodies[b2].cha == cha)
						{
							bd = this._bodies[b2];
							break;
						}
					}
					SkinnedMeshRenderer bodySmr = ((bd != null) ? bd.smr : null);
					if (bodySmr == null)
					{
						SkinnedMeshRenderer[] all0 = cha.GetComponentsInChildren<SkinnedMeshRenderer>(true);
						for (int r0 = 0; r0 < all0.Length; r0++)
						{
							if (all0[r0] != null && all0[r0].name.StartsWith("o_body"))
							{
								bodySmr = all0[r0];
								break;
							}
						}
					}
					if (!(bodySmr == null) && !(bodySmr.sharedMesh == null))
					{
						Mesh bodyMesh = ((bd != null && bd.workMesh != null) ? bd.workMesh : bodySmr.sharedMesh);
						Vector3[] bvB = ((bd != null) ? bd.baseVerts : bodyMesh.vertices);
						if (bvB != null && bvB.Length != 0)
						{
							Transform[] bodyBonesL = bodySmr.bones;
							Matrix4x4[] bodyBindL = bodyMesh.bindposes;
							BoneWeight[] bodyBWL = bodyMesh.boneWeights;
							Vector3[] bposed = new Vector3[bvB.Length];
							bool bodyPosed = bodyBonesL != null && bodyBindL != null && bodyBWL != null && bodyBindL.Length == bodyBonesL.Length && bodyBWL.Length == bvB.Length;
							if (bodyPosed)
							{
								Matrix4x4[] bm = new Matrix4x4[bodyBonesL.Length];
								for (int i = 0; i < bm.Length; i++)
								{
									bm[i] = ((bodyBonesL[i] != null) ? (bodyBonesL[i].localToWorldMatrix * bodyBindL[i]) : Matrix4x4.identity);
								}
								for (int j = 0; j < bvB.Length; j++)
								{
									BoneWeight w = bodyBWL[j];
									Vector3 a = Vector3.zero;
									if (w.weight0 > 0f)
									{
										a += bm[w.boneIndex0].MultiplyPoint3x4(bvB[j]) * w.weight0;
									}
									if (w.weight1 > 0f)
									{
										a += bm[w.boneIndex1].MultiplyPoint3x4(bvB[j]) * w.weight1;
									}
									if (w.weight2 > 0f)
									{
										a += bm[w.boneIndex2].MultiplyPoint3x4(bvB[j]) * w.weight2;
									}
									if (w.weight3 > 0f)
									{
										a += bm[w.boneIndex3].MultiplyPoint3x4(bvB[j]) * w.weight3;
									}
									bposed[j] = a;
								}
							}
							Dictionary<long, List<int>> hB = SoftFleshController.SurveyHash(bvB);
							Dictionary<long, List<int>> hP = (bodyPosed ? SoftFleshController.SurveyHash(bposed) : null);
							Dictionary<Transform, bool> bodyBoneSet = new Dictionary<Transform, bool>();
							if (bodyBonesL != null)
							{
								for (int k = 0; k < bodyBonesL.Length; k++)
								{
									if (bodyBonesL[k] != null)
									{
										bodyBoneSet[bodyBonesL[k]] = true;
									}
								}
							}
							sb.Append("# character: ").Append(cha.name).Append("  body=")
								.Append(bodySmr.name)
								.Append(" verts=")
								.Append(bvB.Length)
								.Append((bd != null) ? " (SET UP, subdivided)" : " (not set up)")
								.Append('\n');
							GameObject[] oc = cha.objClothes;
							List<SkinnedMeshRenderer> slotSmrs = new List<SkinnedMeshRenderer>();
							List<string> slotOf = new List<string>();
							if (oc != null)
							{
								for (int slot = 0; slot < oc.Length; slot++)
								{
									if (!(oc[slot] == null))
									{
										string sn = ((slot < SoftFleshController.ClothSlotName.Length) ? SoftFleshController.ClothSlotName[slot] : ("slot" + slot.ToString()));
										SkinnedMeshRenderer[] ss = oc[slot].GetComponentsInChildren<SkinnedMeshRenderer>(true);
										for (int q = 0; q < ss.Length; q++)
										{
											if (ss[q] != null && ss[q] != bodySmr)
											{
												slotSmrs.Add(ss[q]);
												slotOf.Add(sn);
											}
										}
									}
								}
							}
							sb.Append("# worn: ").Append(slotSmrs.Count).Append(" renderer(s) across the clothing slots")
								.Append('\n');
							if (bd != null && bd.origMesh != null && bd.bakeV != null && bd.bakeV.Count == bd.baseVerts.Length && bd.adjStart != null)
							{
								int nOrig = bd.origMesh.vertexCount;
								List<float> dev = new List<float>(256);
								float worst = 0f;
								int worstI = -1;
								int noParent = 0;
								for (int l = nOrig; l < bd.baseVerts.Length; l++)
								{
									int pa = -1;
									int pb = -1;
									int a2 = bd.adjStart[l];
									int a3 = bd.adjStart[l + 1];
									int m = a2;
									while (m < a3 && pa < 0)
									{
										int j2 = bd.adjList[m];
										if (j2 < nOrig)
										{
											for (int k2 = m + 1; k2 < a3; k2++)
											{
												int j3 = bd.adjList[k2];
												if (j3 < nOrig && ((bd.baseVerts[j2] + bd.baseVerts[j3]) * 0.5f - bd.baseVerts[l]).sqrMagnitude < 1E-10f)
												{
													pa = j2;
													pb = j3;
													break;
												}
											}
										}
										m++;
									}
									if (pa < 0)
									{
										noParent++;
									}
									else
									{
										Vector3 chord = (bd.bakeV[pa] + bd.bakeV[pb]) * 0.5f;
										Vector3 d3 = bd.bakeV[l] - chord;
										float outward = Vector3.Dot(d3, bd.baseNormals[l]);
										dev.Add(outward);
										float am = ((outward < 0f) ? (-outward) : outward);
										if (am > worst)
										{
											worst = am;
											worstI = l;
										}
									}
								}
								if (dev.Count > 0)
								{
									dev.Sort();
									sb.Append("# subdivision surface shift over ").Append(dev.Count).Append(" new verts (")
										.Append(noParent)
										.Append(" without a parent pair)")
										.Append(": median ")
										.Append((dev[dev.Count / 2] * 1000f).ToString("F2"))
										.Append("mm  p90 ")
										.Append((dev[(int)((float)dev.Count * 0.9f)] * 1000f).ToString("F2"))
										.Append("mm  p99 ")
										.Append((dev[(int)((float)dev.Count * 0.99f)] * 1000f).ToString("F2"))
										.Append("mm  worst |")
										.Append((worst * 1000f).ToString("F2"))
										.Append("mm|");
									if (worstI >= 0)
									{
										sb.Append(" on ").Append(SoftFleshController.BoneAt(bd, worstI));
									}
									sb.Append('\n');
								}
							}
							if (bd != null && bd.origMesh != null && bodyPosed && bodyBonesL != null)
							{
								Vector3[] ov = bd.origMesh.vertices;
								BoneWeight[] obw = bd.origMesh.boneWeights;
								Matrix4x4[] obind = bd.origMesh.bindposes;
								int nC = Mathf.Min(ov.Length, bvB.Length);
								int idxOK = 0;
								int idxN = 0;
								for (int n = 0; n < nC; n += 7)
								{
									idxN++;
									if ((ov[n] - bvB[n]).sqrMagnitude < 1E-10f)
									{
										idxOK++;
									}
								}
								Vector3[] pOrig = SoftFleshController.ClothSkin(ov, bodyBonesL, obind, obw);
								if (pOrig == null)
								{
									sb.Append("# body divergence: original rig not poseable (bindposes ").Append((obind != null) ? obind.Length : (-1)).Append(" vs bones ")
										.Append(bodyBonesL.Length)
										.Append(")")
										.Append('\n');
								}
								else
								{
									List<float> dl2 = new List<float>(nC / 4 + 1);
									float worst2 = 0f;
									int worstI2 = -1;
									int over = 0;
									for (int i2 = 0; i2 < nC; i2 += 4)
									{
										float d4 = (bposed[i2] - pOrig[i2]).magnitude;
										dl2.Add(d4);
										if (d4 > 0.001f)
										{
											over++;
										}
										if (d4 > worst2)
										{
											worst2 = d4;
											worstI2 = i2;
										}
									}
									dl2.Sort();
									sb.Append("# body divergence (post-Setup skinning vs original), ").Append(nC).Append(" shared verts, index-aligned ")
										.Append((idxN > 0) ? (100 * idxOK / idxN) : 0)
										.Append("%")
										.Append(": median ")
										.Append((dl2[dl2.Count / 2] * 1000f).ToString("F2"))
										.Append("mm  p90 ")
										.Append((dl2[(int)((float)dl2.Count * 0.9f)] * 1000f).ToString("F2"))
										.Append("mm  max ")
										.Append((worst2 * 1000f).ToString("F2"))
										.Append("mm  over1mm ")
										.Append((100f * (float)over / (float)dl2.Count).ToString("F0"))
										.Append("%");
									if (worstI2 >= 0)
									{
										sb.Append("  worst on ").Append(SoftFleshController.BoneAt(bd, worstI2));
									}
									sb.Append('\n');
								}
							}
							for (int r = 0; r < slotSmrs.Count; r++)
							{
								SkinnedMeshRenderer smr = slotSmrs[r];
								Mesh m2 = smr.sharedMesh;
								string cont = slotOf[r];
								sb.Append(smr.name).Append('\t').Append(cont)
									.Append('\t');
								if (m2 == null)
								{
									sb.Append("-\t-\tNOMESH\t-\t-\t-\t-\t-\t-\t-\t-\t-\t-\n");
									rows++;
								}
								else
								{
									sb.Append(m2.vertexCount).Append('\t').Append((m2.triangles != null) ? (m2.triangles.Length / 3) : 0)
										.Append('\t')
										.Append(m2.isReadable ? "yes" : "NO")
										.Append('\t')
										.Append(m2.blendShapeCount)
										.Append('\t');
									Transform[] cb = smr.bones;
									int shared = 0;
									if (cb != null)
									{
										for (int k3 = 0; k3 < cb.Length; k3++)
										{
											if (cb[k3] != null && bodyBoneSet.ContainsKey(cb[k3]))
											{
												shared++;
											}
										}
									}
									sb.Append((cb != null) ? cb.Length : 0).Append('\t').Append(shared)
										.Append('\t');
									if (!m2.isReadable)
									{
										sb.Append("-\t-\t-\t-\t-\t-\t-\n");
										rows++;
									}
									else
									{
										Vector3[] cv = m2.vertices;
										float[] dB = SoftFleshController.SurveyDist(cv, hB, bvB);
										float[] dP = null;
										Transform[] gb = smr.bones;
										Matrix4x4[] gbind = m2.bindposes;
										BoneWeight[] gbw = m2.boneWeights;
										if (hP != null && gb != null && gbind != null && gbw != null && gbind.Length == gb.Length && gbw.Length == cv.Length)
										{
											Matrix4x4[] gm = new Matrix4x4[gb.Length];
											for (int k4 = 0; k4 < gm.Length; k4++)
											{
												gm[k4] = ((gb[k4] != null) ? (gb[k4].localToWorldMatrix * gbind[k4]) : Matrix4x4.identity);
											}
											Vector3[] pp = new Vector3[cv.Length];
											for (int k5 = 0; k5 < cv.Length; k5++)
											{
												BoneWeight w2 = gbw[k5];
												Vector3 a4 = Vector3.zero;
												if (w2.weight0 > 0f)
												{
													a4 += gm[w2.boneIndex0].MultiplyPoint3x4(cv[k5]) * w2.weight0;
												}
												if (w2.weight1 > 0f)
												{
													a4 += gm[w2.boneIndex1].MultiplyPoint3x4(cv[k5]) * w2.weight1;
												}
												if (w2.weight2 > 0f)
												{
													a4 += gm[w2.boneIndex2].MultiplyPoint3x4(cv[k5]) * w2.weight2;
												}
												if (w2.weight3 > 0f)
												{
													a4 += gm[w2.boneIndex3].MultiplyPoint3x4(cv[k5]) * w2.weight3;
												}
												pp[k5] = a4;
											}
											dP = SoftFleshController.SurveyDist(pp, hP, bposed);
										}
										SoftFleshController.SurveyStats(sb, dB);
										SoftFleshController.SurveyStats(sb, dP);
										sb.Append(SoftFleshController.SurveyNearPct(dB)).Append('\t').Append(SoftFleshController.SurveyNearPct(dP))
											.Append('\t')
											.Append(((float)m2.vertexCount / (float)bvB.Length).ToString("F2"))
											.Append('\n');
										rows++;
									}
								}
							}
						}
					}
				}
			}
			string dirS = Path.Combine(Paths.GameRootPath, "BepInEx/SoftFlesh_diag");
			string path = Path.Combine(dirS, "cloth_survey.txt");
			try
			{
				Directory.CreateDirectory(dirS);
				File.WriteAllText(path, sb.ToString());
			}
			catch (Exception e)
			{
				SoftFleshPlugin.Log("[SF] survey write failed: " + e.Message);
				return;
			}
			this.bakeStatus = "clothing survey: " + rows.ToString() + " renderer(s) -> cloth_survey.txt";
			SoftFleshPlugin.Log(string.Concat(new string[] { "[SF] ", this.bakeStatus, " (", path, ")" }));
		}

		// Token: 0x060000D1 RID: 209 RVA: 0x00034494 File Offset: 0x00032694
		private static Dictionary<long, List<int>> SurveyHash(Vector3[] pts)
		{
			Dictionary<long, List<int>> h = new Dictionary<long, List<int>>(4096);
			for (int i = 0; i < pts.Length; i++)
			{
				long cx = (long)Mathf.Floor(pts[i].x / 0.02f);
				long cy = (long)Mathf.Floor(pts[i].y / 0.02f);
				long cz = (long)Mathf.Floor(pts[i].z / 0.02f);
				long key = (cx & 2097151L) | ((cy & 2097151L) << 21) | ((cz & 2097151L) << 42);
				List<int> c;
				if (!h.TryGetValue(key, out c))
				{
					c = new List<int>(8);
					h[key] = c;
				}
				c.Add(i);
			}
			return h;
		}

		// Token: 0x060000D2 RID: 210 RVA: 0x00034558 File Offset: 0x00032758
		private static float[] SurveyDist(Vector3[] cv, Dictionary<long, List<int>> h, Vector3[] bpts)
		{
			List<float> outD = new List<float>(cv.Length / 4 + 1);
			for (int i = 0; i < cv.Length; i += 4)
			{
				float best = 1E+09f;
				long cx = (long)Mathf.Floor(cv[i].x / 0.02f);
				long cy = (long)Mathf.Floor(cv[i].y / 0.02f);
				long cz = (long)Mathf.Floor(cv[i].z / 0.02f);
				for (long dx = -1L; dx <= 1L; dx += 1L)
				{
					for (long dy = -1L; dy <= 1L; dy += 1L)
					{
						for (long dz = -1L; dz <= 1L; dz += 1L)
						{
							long key = ((cx + dx) & 2097151L) | (((cy + dy) & 2097151L) << 21) | (((cz + dz) & 2097151L) << 42);
							List<int> c;
							if (h.TryGetValue(key, out c))
							{
								for (int j = 0; j < c.Count; j++)
								{
									float d2 = (bpts[c[j]] - cv[i]).sqrMagnitude;
									if (d2 < best)
									{
										best = d2;
									}
								}
							}
						}
					}
				}
				outD.Add((best > 100000000f) ? 999f : Mathf.Sqrt(best));
			}
			outD.Sort();
			return outD.ToArray();
		}

		// Token: 0x060000D3 RID: 211 RVA: 0x000346C8 File Offset: 0x000328C8
		private static void SurveyStats(StringBuilder sb, float[] d)
		{
			if (d == null || d.Length == 0)
			{
				sb.Append("-\t-\t");
				return;
			}
			sb.Append((d[d.Length / 2] * 1000f).ToString("F1")).Append('\t').Append((d[Mathf.Min(d.Length - 1, (int)((float)d.Length * 0.9f))] * 1000f).ToString("F1"))
				.Append('\t');
		}

		// Token: 0x060000D4 RID: 212 RVA: 0x00034748 File Offset: 0x00032948
		private static string SurveyNearPct(float[] d)
		{
			if (d == null || d.Length == 0)
			{
				return "-";
			}
			int i = 0;
			for (int j = 0; j < d.Length; j++)
			{
				if (d[j] < 0.02f)
				{
					i++;
				}
			}
			return (100f * (float)i / (float)d.Length).ToString("F0");
		}

		// Token: 0x060000D5 RID: 213 RVA: 0x0003479C File Offset: 0x0003299C
		public void DumpRig()
		{
			this.rigDumpInfo = "rig dumped to log";
			this.DumpDynamicBones();
			for (int b = 0; b < this._bodies.Count; b++)
			{
				SoftFleshController.Body body = this._bodies[b];
				if (body.vagPivots.Count == 0)
				{
					SoftFleshPlugin.Log("[SF-rig] body " + b.ToString() + ": no vagina rig");
				}
				else
				{
					Vector3 ring = Vector3.zero;
					for (int q = 0; q < body.vagPivots.Count; q++)
					{
						ring += body.vagPivots[q].position;
					}
					ring /= (float)body.vagPivots.Count;
					Vector3 up = ((body.cha != null) ? body.cha.transform.up : Vector3.up);
					Vector3 fwd = ((body.cha != null) ? body.cha.transform.forward : Vector3.forward);
					Vector3 axis = Vector3.zero;
					for (int q2 = 0; q2 < body.vagInner.Count; q2++)
					{
						axis += body.vagInner[q2].position - ring;
					}
					if (axis.sqrMagnitude < 1E-10f)
					{
						axis = up;
					}
					axis.Normalize();
					Vector3 uAx = up - axis * Vector3.Dot(up, axis);
					if (uAx.sqrMagnitude < 1E-10f)
					{
						uAx = fwd - axis * Vector3.Dot(fwd, axis);
					}
					uAx.Normalize();
					Vector3 rAx = Vector3.Cross(axis, uAx).normalized;
					SoftFleshPlugin.Log(string.Concat(new string[]
					{
						"[SF-rig] body ",
						b.ToString(),
						" ring=",
						ring.ToString("F3"),
						" axis=",
						axis.ToString("F2"),
						" (chaUp=",
						up.ToString("F2"),
						")"
					}));
					for (int q3 = 0; q3 < body.vagPivots.Count; q3++)
					{
						SoftFleshController.LogBone(body.vagPivots[q3], "PIVOT", ring, axis, uAx, rAx);
					}
					for (int q4 = 0; q4 < body.vagInner.Count; q4++)
					{
						SoftFleshController.LogBone(body.vagInner[q4], "INNER", ring, axis, uAx, rAx);
					}
					if (body.vagRoot != null)
					{
						SoftFleshController.LogBone(body.vagRoot, "ROOT ", ring, axis, uAx, rAx);
					}
				}
			}
		}

		// Token: 0x060000D6 RID: 214 RVA: 0x00034A64 File Offset: 0x00032C64
		private void DumpDynamicBones()
		{
			for (int b = 0; b < this._bodies.Count; b++)
			{
				SoftFleshController.Body body = this._bodies[b];
				if (!(body.cha == null) && !(body.cha.objBodyBone == null))
				{
					MonoBehaviour[] comps = body.cha.objBodyBone.GetComponentsInChildren<MonoBehaviour>(true);
					int found = 0;
					List<string> seenTypes = new List<string>();
					foreach (MonoBehaviour c in comps)
					{
						if (!(c == null))
						{
							Type ty = c.GetType();
							if (ty.Name.IndexOf("DynamicBone", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								string tn = ty.FullName;
								if (!seenTypes.Contains(tn))
								{
									seenTypes.Add(tn);
									StringBuilder sb = new StringBuilder();
									sb.Append("[SF-dyn] TYPE ").Append(tn).Append(" asm=")
										.Append(ty.Assembly.GetName().Name)
										.Append(" fields:");
									foreach (FieldInfo f in ty.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
									{
										sb.Append(' ').Append(f.FieldType.Name).Append(' ')
											.Append(f.Name)
											.Append(';');
									}
									SoftFleshPlugin.Log(sb.ToString());
								}
								string owner = c.transform.name;
								string rootName = "";
								FieldInfo rootF = ty.GetField("m_Root", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								if (rootF != null)
								{
									Transform rv = rootF.GetValue(c) as Transform;
									if (rv != null)
									{
										rootName = rv.name;
									}
								}
								if (owner.ToLower().Contains("vagina") || rootName.ToLower().Contains("vagina"))
								{
									FieldInfo colF = ty.GetField("m_Colliders", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
									string colInfo = "none";
									if (colF != null)
									{
										IList lst = colF.GetValue(c) as IList;
										colInfo = colF.FieldType.Name + " count=" + ((lst != null) ? lst.Count.ToString() : "null");
										if (lst != null && lst.Count > 0 && lst[0] != null)
										{
											colInfo = colInfo + " elem0=" + lst[0].GetType().FullName;
										}
									}
									SoftFleshPlugin.Log(string.Concat(new string[] { "[SF-dyn]   ", ty.Name, " on=", owner, " root=", rootName, " colliders=", colInfo }));
									found++;
								}
							}
						}
					}
					SoftFleshPlugin.Log("[SF-dyn] body " + b.ToString() + ": vagina dynamic-bone components = " + found.ToString());
				}
			}
		}

		// Token: 0x060000D7 RID: 215 RVA: 0x00034D80 File Offset: 0x00032F80
		private static void LogBone(Transform t, string kind, Vector3 ring, Vector3 axis, Vector3 uAx, Vector3 rAx)
		{
			Vector3 d = t.position - ring;
			float depth = Vector3.Dot(d, axis);
			Vector3 rad = d - axis * depth;
			float r = rad.magnitude;
			float ang = Mathf.Atan2(Vector3.Dot(rad, rAx), Vector3.Dot(rad, uAx)) * 57.29578f;
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF-rig]   ",
				kind,
				" ",
				t.name,
				"  r=",
				(r * 1000f).ToString("F1"),
				"mm  ang=",
				ang.ToString("F0"),
				"deg (0=up toward clit, +90=char right)  depth=",
				(depth * 1000f).ToString("F1"),
				"mm",
				(t.childCount > 0) ? ("  child=" + t.GetChild(0).name) : "  (leaf)"
			}));
		}

		// Token: 0x060000D8 RID: 216 RVA: 0x00034E90 File Offset: 0x00033090
		private static string BoneAt(SoftFleshController.Body body, int vi)
		{
			if (body.domBone == null || body.bonesArr == null || vi < 0 || vi >= body.domBone.Length)
			{
				return "?";
			}
			int db = body.domBone[vi];
			if (db < 0 || db >= body.bonesArr.Length || body.bonesArr[db] == null)
			{
				return "?";
			}
			return body.bonesArr[db].name;
		}

		// Token: 0x060000D9 RID: 217 RVA: 0x00034F00 File Offset: 0x00033100
		public void ExportFace()
		{
			ChaControl[] sel = this.SelectedChars();
			ChaControl cha = ((sel != null && sel.Length != 0) ? sel[0] : null);
			if (cha == null)
			{
				ChaControl[] all = Object.FindObjectsOfType<ChaControl>();
				if (all != null && all.Length != 0)
				{
					cha = all[0];
				}
			}
			if (cha == null)
			{
				this.diagStatus = "face export: no character";
				return;
			}
			string dir = Path.Combine(Paths.GameRootPath, "BepInEx/SoftFlesh_diag");
			Directory.CreateDirectory(dir);
			try
			{
				this.DumpWrap(cha, dir);
			}
			catch (Exception e)
			{
				string text = "[SF] wrap dump THREW: ";
				Exception ex = e;
				SoftFleshPlugin.Log(text + ((ex != null) ? ex.ToString() : null));
			}
			SkinnedMeshRenderer face = null;
			SkinnedMeshRenderer tooth = null;
			SkinnedMeshRenderer tongue = null;
			SkinnedMeshRenderer[] smrs = cha.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			StringBuilder parts = new StringBuilder();
			parts.Append("renderers under ").Append(cha.name).Append(":\n");
			foreach (SkinnedMeshRenderer r in smrs)
			{
				if (!(r == null) && !(r.sharedMesh == null))
				{
					string nm = r.name.ToLower();
					parts.Append("  ").Append(r.name).Append("  mesh ")
						.Append(r.sharedMesh.name)
						.Append("  verts ")
						.Append(r.sharedMesh.vertexCount)
						.Append("  shapes ")
						.Append(r.sharedMesh.blendShapeCount)
						.Append("  bones ")
						.Append((r.bones != null) ? r.bones.Length : 0)
						.Append("  bounds ")
						.Append(r.sharedMesh.bounds.size.ToString("F3"))
						.Append(r.enabled ? "" : "  (disabled)")
						.Append('\n');
					if (nm == "cf_o_face" || (face == null && nm.Contains("face") && !nm.Contains("eye")))
					{
						face = r;
					}
					else if (nm.Contains("tooth") || nm.Contains("ha_"))
					{
						tooth = tooth ?? r;
					}
					else if (nm.Contains("tang") || nm.Contains("tongue") || nm.Contains("sita"))
					{
						tongue = tongue ?? r;
					}
				}
			}
			if (face == null)
			{
				this.diagStatus = "face export: no face renderer found";
				File.WriteAllText(Path.Combine(dir, "face_parts.txt"), parts.ToString());
				return;
			}
			Mesh mesh = face.sharedMesh;
			Transform[] bones = face.bones;
			Vector3[] v = mesh.vertices;
			Vector3[] nrm = mesh.normals;
			BoneWeight[] bw = mesh.boneWeights;
			int i = v.Length;
			bool hasW = bw != null && bw.Length == i;
			bool hasN = nrm != null && nrm.Length == i;
			try
			{
				parts.Append("\nmouth state: ptn ").Append(cha.fileStatus.mouthPtn).Append("  openMax ")
					.Append(cha.fileStatus.mouthOpenMax.ToString("F2"))
					.Append('\n');
			}
			catch (Exception e2)
			{
				parts.Append("\nmouth state: (").Append(e2.GetType().Name).Append(")\n");
			}
			parts.Append("current shape weights (nonzero): ");
			for (int j = 0; j < mesh.blendShapeCount; j++)
			{
				float w = face.GetBlendShapeWeight(j);
				if (w > 0.01f)
				{
					parts.Append(mesh.GetBlendShapeName(j)).Append('=').Append(w.ToString("F0"))
						.Append(' ');
				}
			}
			parts.Append('\n');
			if (tooth != null)
			{
				parts.Append("teeth: ").Append(tooth.name).Append("  verts ")
					.Append(tooth.sharedMesh.vertexCount)
					.Append("  bounds(local) ")
					.Append(tooth.sharedMesh.bounds.ToString("F3"))
					.Append("  root bone ")
					.Append((tooth.rootBone != null) ? tooth.rootBone.name : "-")
					.Append('\n');
			}
			if (tongue != null)
			{
				parts.Append("tongue: ").Append(tongue.name).Append("  verts ")
					.Append(tongue.sharedMesh.vertexCount)
					.Append("  bounds(local) ")
					.Append(tongue.sharedMesh.bounds.ToString("F3"))
					.Append("  root bone ")
					.Append((tongue.rootBone != null) ? tongue.rootBone.name : "-")
					.Append('\n');
			}
			File.WriteAllText(Path.Combine(dir, "face_parts.txt"), parts.ToString());
			int nb = ((bones != null) ? bones.Length : 0);
			int[] domCnt = new int[nb + 1];
			float[] wSum = new float[nb + 1];
			int[] anyCnt = new int[nb + 1];
			Vector3[] bmin = new Vector3[nb + 1];
			Vector3[] bmax = new Vector3[nb + 1];
			for (int b = 0; b <= nb; b++)
			{
				bmin[b] = new Vector3(1E+09f, 1E+09f, 1E+09f);
				bmax[b] = new Vector3(-1E+09f, -1E+09f, -1E+09f);
			}
			int[] dom = new int[i];
			if (hasW)
			{
				for (int k = 0; k < i; k++)
				{
					BoneWeight w2 = bw[k];
					int di;
					float dw;
					SoftFleshController.DominantOf(w2, out di, out dw);
					if (di < 0 || di >= nb)
					{
						di = nb;
					}
					dom[k] = di;
					domCnt[di]++;
					if (w2.weight0 > 0.001f && w2.boneIndex0 < nb)
					{
						wSum[w2.boneIndex0] += w2.weight0;
						anyCnt[w2.boneIndex0]++;
					}
					if (w2.weight1 > 0.001f && w2.boneIndex1 < nb)
					{
						wSum[w2.boneIndex1] += w2.weight1;
						anyCnt[w2.boneIndex1]++;
					}
					if (w2.weight2 > 0.001f && w2.boneIndex2 < nb)
					{
						wSum[w2.boneIndex2] += w2.weight2;
						anyCnt[w2.boneIndex2]++;
					}
					if (w2.weight3 > 0.001f && w2.boneIndex3 < nb)
					{
						wSum[w2.boneIndex3] += w2.weight3;
						anyCnt[w2.boneIndex3]++;
					}
					Vector3 lo = bmin[di];
					Vector3 hi = bmax[di];
					Vector3 p = v[k];
					if (p.x < lo.x)
					{
						lo.x = p.x;
					}
					if (p.y < lo.y)
					{
						lo.y = p.y;
					}
					if (p.z < lo.z)
					{
						lo.z = p.z;
					}
					if (p.x > hi.x)
					{
						hi.x = p.x;
					}
					if (p.y > hi.y)
					{
						hi.y = p.y;
					}
					if (p.z > hi.z)
					{
						hi.z = p.z;
					}
					bmin[di] = lo;
					bmax[di] = hi;
				}
			}
			StringBuilder bsb = new StringBuilder();
			bsb.Append("face ").Append(face.name).Append("  mesh ")
				.Append(mesh.name)
				.Append("  verts ")
				.Append(i)
				.Append("  bones ")
				.Append(nb)
				.Append("  shapes ")
				.Append(mesh.blendShapeCount)
				.Append('\n');
			bsb.Append("idx\tname\tdominant\ttouched\tweightSum\tbbox(dominant, bind space)\n");
			List<int> mouthBones = new List<int>();
			for (int b2 = 0; b2 < nb; b2++)
			{
				if (anyCnt[b2] != 0)
				{
					string bn = ((bones[b2] != null) ? bones[b2].name : "(null)");
					string ln = bn.ToLower();
					bool isMouth = ln.Contains("mouth") || ln.Contains("kuti") || ln.Contains("lip") || ln.Contains("jaw") || ln.Contains("chin") || ln.Contains("ago") || ln.Contains("tang") || ln.Contains("tooth");
					if (isMouth)
					{
						mouthBones.Add(b2);
					}
					bsb.Append(b2).Append('\t').Append(bn)
						.Append(isMouth ? " *" : "")
						.Append('\t')
						.Append(domCnt[b2])
						.Append('\t')
						.Append(anyCnt[b2])
						.Append('\t')
						.Append(wSum[b2].ToString("F1"))
						.Append('\t');
					if (domCnt[b2] > 0)
					{
						bsb.Append(bmin[b2].ToString("F3")).Append(" .. ").Append(bmax[b2].ToString("F3"));
					}
					bsb.Append('\n');
				}
			}
			File.WriteAllText(Path.Combine(dir, "face_bones.txt"), bsb.ToString());
			int ns = mesh.blendShapeCount;
			Vector3[] dv = new Vector3[i];
			Vector3[] dn = new Vector3[i];
			Vector3[] dt = new Vector3[i];
			int[] shapeMoved = new int[ns];
			float[] shapeMax = new float[ns];
			Vector3[] shapeCen = new Vector3[ns];
			string[] shapeName = new string[ns];
			StringBuilder ssb = new StringBuilder();
			ssb.Append("idx\tname\tframes\tmoved>0.5mm\tmax mm\tcentroid of moved (bind)\tcurrent weight\n");
			List<KeyValuePair<int, float[]>> mouthShapeDelta = new List<KeyValuePair<int, float[]>>();
			for (int si = 0; si < ns; si++)
			{
				shapeName[si] = mesh.GetBlendShapeName(si);
				int fr = mesh.GetBlendShapeFrameCount(si);
				if (fr > 0)
				{
					mesh.GetBlendShapeFrameVertices(si, fr - 1, dv, dn, dt);
					int moved = 0;
					float mx = 0f;
					Vector3 cen = Vector3.zero;
					float[] mags = null;
					string lname = shapeName[si].ToLower();
					bool isMouthShape = lname.Contains("kuti") || lname.Contains("mouth");
					if (isMouthShape)
					{
						mags = new float[i];
					}
					for (int l = 0; l < i; l++)
					{
						float m = dv[l].magnitude;
						if (mags != null)
						{
							mags[l] = m;
						}
						if (m > 0.0005f)
						{
							moved++;
							cen += v[l];
						}
						if (m > mx)
						{
							mx = m;
						}
					}
					shapeMoved[si] = moved;
					shapeMax[si] = mx;
					shapeCen[si] = ((moved > 0) ? (cen / (float)moved) : Vector3.zero);
					if (mags != null && moved > 0)
					{
						mouthShapeDelta.Add(new KeyValuePair<int, float[]>(si, mags));
					}
					ssb.Append(si).Append('\t').Append(shapeName[si])
						.Append('\t')
						.Append(fr)
						.Append('\t')
						.Append(moved)
						.Append('\t')
						.Append((mx * 1000f).ToString("F1"))
						.Append('\t')
						.Append((moved > 0) ? shapeCen[si].ToString("F3") : "-")
						.Append('\t')
						.Append(face.GetBlendShapeWeight(si).ToString("F0"))
						.Append('\n');
				}
			}
			File.WriteAllText(Path.Combine(dir, "face_shapes.txt"), ssb.ToString());
			mouthShapeDelta.Sort((KeyValuePair<int, float[]> x, KeyValuePair<int, float[]> y) => shapeMax[y.Key].CompareTo(shapeMax[x.Key]));
			if (mouthShapeDelta.Count > 8)
			{
				mouthShapeDelta.RemoveRange(8, mouthShapeDelta.Count - 8);
			}
			StringBuilder extra = new StringBuilder();
			foreach (SkinnedMeshRenderer r2 in smrs)
			{
				if (!(r2 == null) && !(r2.sharedMesh == null) && r2.sharedMesh.blendShapeCount != 0)
				{
					string ln2 = r2.name.ToLower();
					if (ln2.Contains("tooth") || ln2.Contains("tang") || ln2.Contains("canine") || ln2.Contains("noseline") || ln2 == "cf_o_face")
					{
						extra.Append("== ").Append(r2.name).Append(" (")
							.Append(r2.sharedMesh.blendShapeCount)
							.Append(" shapes)\n");
						for (int si2 = 0; si2 < r2.sharedMesh.blendShapeCount; si2++)
						{
							extra.Append("  ").Append(si2).Append(' ')
								.Append(r2.sharedMesh.GetBlendShapeName(si2))
								.Append("  w=")
								.Append(r2.GetBlendShapeWeight(si2).ToString("F0"))
								.Append('\n');
						}
					}
				}
			}
			try
			{
				FaceBlendShape fbs = cha.fbsCtrl;
				extra.Append("\n== fbsCtrl: ").Append((fbs != null) ? fbs.GetType().FullName : "null").Append('\n');
				if (fbs != null)
				{
					foreach (FieldInfo fi in fbs.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
					{
						object val = null;
						try
						{
							val = fi.GetValue(fbs);
						}
						catch
						{
						}
						extra.Append("  field ").Append(fi.FieldType.Name).Append(' ')
							.Append(fi.Name)
							.Append(" = ")
							.Append((val == null) ? "null" : val.ToString())
							.Append('\n');
						if (val != null && fi.FieldType.Name.ToLower().Contains("mouth"))
						{
							foreach (FieldInfo f2 in val.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
							{
								object v2 = null;
								try
								{
									v2 = f2.GetValue(val);
								}
								catch
								{
								}
								extra.Append("      ").Append(f2.FieldType.Name).Append(' ')
									.Append(f2.Name)
									.Append(" = ")
									.Append((v2 == null) ? "null" : v2.ToString())
									.Append('\n');
								IList lst = v2 as IList;
								if (lst != null && lst.Count > 0 && lst.Count < 200)
								{
									for (int n = 0; n < lst.Count; n++)
									{
										object it = lst[n];
										if (it == null)
										{
											extra.Append("         [").Append(n).Append("] null\n");
										}
										else
										{
											extra.Append("         [").Append(n).Append("] ")
												.Append(it.ToString());
											foreach (FieldInfo f3 in it.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
											{
												object v3 = null;
												try
												{
													v3 = f3.GetValue(it);
												}
												catch
												{
												}
												extra.Append("  ").Append(f3.Name).Append('=')
													.Append((v3 == null) ? "null" : v3.ToString());
											}
											extra.Append('\n');
										}
									}
								}
							}
							foreach (PropertyInfo pi in val.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
							{
								extra.Append("      prop ").Append(pi.PropertyType.Name).Append(' ')
									.Append(pi.Name)
									.Append('\n');
							}
						}
					}
				}
			}
			catch (Exception e3)
			{
				extra.Append("fbsCtrl reflection: ").Append(e3.GetType().Name).Append(' ')
					.Append(e3.Message)
					.Append('\n');
			}
			File.WriteAllText(Path.Combine(dir, "face_shapes_all.txt"), extra.ToString());
			StringBuilder csv = new StringBuilder(1048576);
			csv.Append("idx,x,y,z,nx,ny,nz,domBone,domW");
			foreach (int b3 in mouthBones)
			{
				csv.Append(",w:").Append((bones[b3] != null) ? bones[b3].name : ("b" + b3.ToString()));
			}
			foreach (KeyValuePair<int, float[]> kv in mouthShapeDelta)
			{
				csv.Append(",d:").Append(shapeName[kv.Key]);
			}
			csv.Append('\n');
			int i2 = 0;
			while (i2 < i)
			{
				Vector3 p2 = v[i2];
				Vector3 nn = (hasN ? nrm[i2] : Vector3.zero);
				csv.Append(i2).Append(',').Append(SoftFleshController.F(p2.x))
					.Append(',')
					.Append(SoftFleshController.F(p2.y))
					.Append(',')
					.Append(SoftFleshController.F(p2.z))
					.Append(',')
					.Append(SoftFleshController.F(nn.x))
					.Append(',')
					.Append(SoftFleshController.F(nn.y))
					.Append(',')
					.Append(SoftFleshController.F(nn.z))
					.Append(',');
				if (hasW)
				{
					int di2;
					float dw2;
					SoftFleshController.DominantOf(bw[i2], out di2, out dw2);
					csv.Append((di2 >= 0 && di2 < nb && bones[di2] != null) ? bones[di2].name : "-").Append(',').Append(SoftFleshController.F(dw2));
					using (List<int>.Enumerator enumerator3 = mouthBones.GetEnumerator())
					{
						while (enumerator3.MoveNext())
						{
							int b4 = enumerator3.Current;
							BoneWeight w3 = bw[i2];
							float wb = 0f;
							if (w3.boneIndex0 == b4)
							{
								wb += w3.weight0;
							}
							if (w3.boneIndex1 == b4)
							{
								wb += w3.weight1;
							}
							if (w3.boneIndex2 == b4)
							{
								wb += w3.weight2;
							}
							if (w3.boneIndex3 == b4)
							{
								wb += w3.weight3;
							}
							csv.Append(',').Append(SoftFleshController.F(wb));
						}
						goto IL_14F0;
					}
					goto IL_14A9;
				}
				goto IL_14A9;
				IL_14F0:
				foreach (KeyValuePair<int, float[]> kv2 in mouthShapeDelta)
				{
					csv.Append(',').Append(SoftFleshController.F(kv2.Value[i2]));
				}
				csv.Append('\n');
				i2++;
				continue;
				IL_14A9:
				csv.Append("-,0");
				foreach (int b5 in mouthBones)
				{
					csv.Append(",0");
				}
				goto IL_14F0;
			}
			File.WriteAllText(Path.Combine(dir, "face_verts.csv"), csv.ToString());
			StringBuilder obj = new StringBuilder(1048576);
			for (int i3 = 0; i3 < i; i3++)
			{
				obj.Append("v ").Append(SoftFleshController.F(v[i3].x)).Append(' ')
					.Append(SoftFleshController.F(v[i3].y))
					.Append(' ')
					.Append(SoftFleshController.F(v[i3].z))
					.Append('\n');
			}
			for (int sm = 0; sm < mesh.subMeshCount; sm++)
			{
				int[] tri = mesh.GetTriangles(sm);
				obj.Append("g sub").Append(sm).Append('\n');
				int t = 0;
				while (t + 2 < tri.Length)
				{
					obj.Append("f ").Append(tri[t] + 1).Append(' ')
						.Append(tri[t + 1] + 1)
						.Append(' ')
						.Append(tri[t + 2] + 1)
						.Append('\n');
					t += 3;
				}
			}
			File.WriteAllText(Path.Combine(dir, "face_mesh.obj"), obj.ToString());
			this.diagStatus = string.Concat(new string[]
			{
				"face exported: ",
				i.ToString(),
				" verts, ",
				mouthBones.Count.ToString(),
				" mouth bone(s), ",
				ns.ToString(),
				" shapes -> BepInEx/SoftFlesh_diag/face_*"
			});
			SoftFleshPlugin.Log("[SF] " + this.diagStatus);
		}

		// Token: 0x060000DA RID: 218 RVA: 0x00036730 File Offset: 0x00034930
		public void ExportDiagnostics()
		{
			try
			{
				string dir = Path.Combine(Paths.GameRootPath, "BepInEx/SoftFlesh_diag");
				Directory.CreateDirectory(dir);
				int files = 0;
				for (int b = 0; b < this._bodies.Count; b++)
				{
					files += this.ExportBody(this._bodies[b], b, dir);
				}
				this.diagStatus = "exported " + files.ToString() + " file(s) -> BepInEx/SoftFlesh_diag";
				SoftFleshPlugin.Log("[SF-diag] " + this.diagStatus);
			}
			catch (Exception e)
			{
				this.diagStatus = "export FAILED: " + e.Message;
				SoftFleshPlugin.Log("[SF-diag] " + this.diagStatus);
			}
		}

		// Token: 0x060000DB RID: 219 RVA: 0x000367F4 File Offset: 0x000349F4
		private int ExportBody(SoftFleshController.Body body, int bi, string dir)
		{
			SkinnedMeshRenderer smr = body.smr;
			if (smr == null || body.adjStart == null)
			{
				return 0;
			}
			int i = body.baseVerts.Length;
			bool[] inR = new bool[i];
			List<int> fr = new List<int>();
			for (int j = 0; j < i; j++)
			{
				bool gen = (body.vagW != null && body.vagW[j] > 0.02f) || (body.kokW != null && body.kokW[j] > 0.05f);
				if (gen)
				{
					inR[j] = true;
					fr.Add(j);
				}
			}
			if (fr.Count < 8)
			{
				return 0;
			}
			for (int ring = 0; ring < 3; ring++)
			{
				List<int> nx = new List<int>();
				for (int q = 0; q < fr.Count; q++)
				{
					int k = fr[q];
					for (int l = body.adjStart[k]; l < body.adjStart[k + 1]; l++)
					{
						int m = body.adjList[l];
						if (!inR[m])
						{
							inR[m] = true;
							nx.Add(m);
						}
					}
				}
				fr = nx;
			}
			List<int> reg = new List<int>();
			int[] remap = new int[i];
			for (int n = 0; n < i; n++)
			{
				remap[n] = -1;
			}
			for (int i2 = 0; i2 < i; i2++)
			{
				if (inR[i2])
				{
					remap[i2] = reg.Count;
					reg.Add(i2);
				}
			}
			Mesh tmp = new Mesh();
			smr.BakeMesh(tmp);
			Vector3[] curV = tmp.vertices;
			Vector3[] curN = tmp.normals;
			Object.Destroy(tmp);
			if (curV == null || curV.Length != i)
			{
				return 0;
			}
			Vector3[] refV = null;
			if (body.workMesh != null && body.outV != null && body.baseVerts != null)
			{
				List<Vector3> savedV = new List<Vector3>(body.outV);
				List<Vector3> flat = new List<Vector3>(i);
				for (int i3 = 0; i3 < i; i3++)
				{
					flat.Add(body.baseVerts[i3]);
				}
				body.workMesh.SetVertices(flat);
				Mesh t2 = new Mesh();
				smr.BakeMesh(t2);
				refV = t2.vertices;
				Object.Destroy(t2);
				body.workMesh.SetVertices(savedV);
			}
			int[] tris = ((body.workMesh != null) ? body.workMesh.triangles : ((body.origMesh != null) ? body.origMesh.triangles : null));
			List<int> faces = new List<int>();
			if (tris != null)
			{
				int t3 = 0;
				while (t3 + 2 < tris.Length)
				{
					if (inR[tris[t3]] && inR[tris[t3 + 1]] && inR[tris[t3 + 2]])
					{
						faces.Add(tris[t3]);
						faces.Add(tris[t3 + 1]);
						faces.Add(tris[t3 + 2]);
					}
					t3 += 3;
				}
			}
			string tag = "_b" + bi.ToString();
			SoftFleshController.WriteObj(Path.Combine(dir, "region" + tag + ".obj"), reg, remap, faces, curV, curN);
			if (refV != null)
			{
				SoftFleshController.WriteObj(Path.Combine(dir, "region_bonesreset" + tag + ".obj"), reg, remap, faces, refV, null);
			}
			BoneWeight[] bw = ((body.workMesh != null && body.workMesh.vertexCount == i) ? body.workMesh.boneWeights : ((body.origMesh != null) ? body.origMesh.boneWeights : null));
			StringBuilder sb = new StringBuilder();
			sb.Append("idx,bindX,bindY,bindZ,curX,curY,curZ,refX,refY,refZ,nx,ny,nz,vagW,kokW,nipW,nBones,topBone,topW,disp,pushMag,corrMag,armSide\n");
			for (int q2 = 0; q2 < reg.Count; q2++)
			{
				int i4 = reg[q2];
				int nb = 0;
				string top = "";
				float tw = 0f;
				if (bw != null && i4 < bw.Length)
				{
					BoneWeight w = bw[i4];
					if (w.weight0 > 0.001f)
					{
						nb++;
					}
					if (w.weight1 > 0.001f)
					{
						nb++;
					}
					if (w.weight2 > 0.001f)
					{
						nb++;
					}
					if (w.weight3 > 0.001f)
					{
						nb++;
					}
					int di;
					float dw;
					SoftFleshController.DominantOf(w, out di, out dw);
					tw = dw;
					if (body.bonesArr != null && di >= 0 && di < body.bonesArr.Length && body.bonesArr[di] != null)
					{
						top = body.bonesArr[di].name;
					}
				}
				Vector3 bp = body.baseVerts[i4];
				Vector3 cv = curV[i4];
				Vector3 rv = ((refV != null) ? refV[i4] : cv);
				Vector3 nn = ((curN != null && i4 < curN.Length) ? curN[i4] : Vector3.zero);
				sb.Append(i4).Append(',').Append(SoftFleshController.F(bp.x))
					.Append(',')
					.Append(SoftFleshController.F(bp.y))
					.Append(',')
					.Append(SoftFleshController.F(bp.z))
					.Append(',')
					.Append(SoftFleshController.F(cv.x))
					.Append(',')
					.Append(SoftFleshController.F(cv.y))
					.Append(',')
					.Append(SoftFleshController.F(cv.z))
					.Append(',')
					.Append(SoftFleshController.F(rv.x))
					.Append(',')
					.Append(SoftFleshController.F(rv.y))
					.Append(',')
					.Append(SoftFleshController.F(rv.z))
					.Append(',')
					.Append(SoftFleshController.F(nn.x))
					.Append(',')
					.Append(SoftFleshController.F(nn.y))
					.Append(',')
					.Append(SoftFleshController.F(nn.z))
					.Append(',')
					.Append(SoftFleshController.F((body.vagW != null) ? body.vagW[i4] : 0f))
					.Append(',')
					.Append(SoftFleshController.F((body.kokW != null) ? body.kokW[i4] : 0f))
					.Append(',')
					.Append(SoftFleshController.F((body.nipW != null) ? body.nipW[i4] : 0f))
					.Append(',')
					.Append(nb)
					.Append(',')
					.Append(top)
					.Append(',')
					.Append(SoftFleshController.F(tw))
					.Append(',')
					.Append(SoftFleshController.F(body.disp[i4]))
					.Append(',')
					.Append(SoftFleshController.F(body.pushB[i4].magnitude))
					.Append(',')
					.Append(SoftFleshController.F((body.corrB != null) ? body.corrB[i4].magnitude : 0f))
					.Append(',')
					.Append(body.armSide[i4])
					.Append('\n');
			}
			File.WriteAllText(Path.Combine(dir, "verts" + tag + ".csv"), sb.ToString());
			StringBuilder st = new StringBuilder();
			st.Append("SoftFlesh diagnostics, body ").Append(bi).Append('\n');
			st.Append("region verts=").Append(reg.Count).Append(" faces=")
				.Append(faces.Count / 3)
				.Append('\n');
			st.Append("mesh verts=").Append(i).Append(" avgEdge=")
				.Append(SoftFleshController.F(body.avgEdge * 1000f))
				.Append("mm\n");
			st.Append("vagW>0.02=").Append(SoftFleshController.CountW(body.vagW, 0.02f)).Append(" kokW>0.05=")
				.Append(SoftFleshController.CountW(body.kokW, 0.05f))
				.Append('\n');
			st.Append("dynChains=").Append(body.vagDyn.Count).Append(" pivots=")
				.Append(body.vagPivots.Count)
				.Append('\n');
			List<int>[] adjN = new List<int>[i];
			if (tris != null)
			{
				int t4 = 0;
				while (t4 + 2 < tris.Length)
				{
					for (int e = 0; e < 3; e++)
					{
						int va = tris[t4 + e];
						int vb = tris[t4 + (e + 1) % 3];
						if (adjN[va] == null)
						{
							adjN[va] = new List<int>(6);
						}
						if (!adjN[va].Contains(vb))
						{
							adjN[va].Add(vb);
						}
					}
					t4 += 3;
				}
			}
			int spikes = 0;
			float worst = 0f;
			for (int q3 = 0; q3 < reg.Count; q3++)
			{
				int i5 = reg[q3];
				List<int> nb2 = adjN[i5];
				if (nb2 != null && nb2.Count != 0)
				{
					Vector3 cen = Vector3.zero;
					for (int j2 = 0; j2 < nb2.Count; j2++)
					{
						cen += curV[nb2[j2]];
					}
					cen /= (float)nb2.Count;
					float off = (curV[i5] - cen).magnitude * 1000f;
					if (off > 15f)
					{
						spikes++;
					}
					if (off > worst)
					{
						worst = off;
					}
				}
			}
			st.Append("live mesh: verts>15mm off neighbourhood=").Append(spikes).Append(" worst=")
				.Append(worst.ToString("F1"))
				.Append("mm")
				.Append('\n');
			int spikesB = 0;
			float worstB = 0f;
			for (int q4 = 0; q4 < reg.Count; q4++)
			{
				int i6 = reg[q4];
				List<int> nb3 = adjN[i6];
				if (nb3 != null && nb3.Count != 0)
				{
					Vector3 cen2 = Vector3.zero;
					for (int j3 = 0; j3 < nb3.Count; j3++)
					{
						cen2 += body.baseVerts[nb3[j3]];
					}
					cen2 /= (float)nb3.Count;
					float off2 = (body.baseVerts[i6] - cen2).magnitude * 1000f;
					if (off2 > 15f)
					{
						spikesB++;
					}
					if (off2 > worstB)
					{
						worstB = off2;
					}
				}
			}
			st.Append("bind mesh: verts>15mm off neighbourhood=").Append(spikesB).Append(" worst=")
				.Append(worstB.ToString("F1"))
				.Append("mm")
				.Append('\n');
			int oddW = 0;
			BoneWeight[] bwm = ((body.workMesh != null) ? body.workMesh.boneWeights : null);
			if (bwm != null)
			{
				for (int q5 = 0; q5 < reg.Count; q5++)
				{
					int i7 = reg[q5];
					if (i7 < bwm.Length)
					{
						BoneWeight w2 = bwm[i7];
						float tot = w2.weight0 + w2.weight1 + w2.weight2 + w2.weight3;
						if (tot < 0.99f || tot > 1.01f)
						{
							oddW++;
						}
					}
				}
			}
			st.Append("skin weights: region verts not summing to 1 = ").Append(oddW).Append('\n');
			if (body.seamGroups != null)
			{
				int gaps = 0;
				float worstGap = 0f;
				int gAligned = 0;
				int gFold = 0;
				int worstG = -1;
				float worstAng = 0f;
				int angBad = 0;
				int worstAngG = -1;
				Vector3[] nlCur = ((body.workMesh != null) ? body.workMesh.normals : null);
				for (int g = 0; g < body.seamGroups.Length; g++)
				{
					int[] grp = body.seamGroups[g];
					if (body.seamAligned != null && body.seamAligned[g])
					{
						gAligned++;
					}
					else
					{
						gFold++;
					}
					float mx = 0f;
					for (int j4 = 1; j4 < grp.Length; j4++)
					{
						float dd = (curV[grp[j4]] - curV[grp[0]]).magnitude * 1000f;
						if (dd > mx)
						{
							mx = dd;
						}
					}
					if (mx > 0.5f)
					{
						gaps++;
					}
					if (mx > worstGap)
					{
						worstGap = mx;
						worstG = g;
					}
					if (nlCur != null && (body.seamAligned == null || body.seamAligned[g]))
					{
						float ma = 0f;
						for (int j5 = 1; j5 < grp.Length; j5++)
						{
							if (grp[j5] < nlCur.Length && grp[0] < nlCur.Length)
							{
								float dp = Vector3.Dot(nlCur[grp[j5]].normalized, nlCur[grp[0]].normalized);
								float ang = Mathf.Acos(Mathf.Clamp(dp, -1f, 1f)) * 57.29578f;
								if (ang > ma)
								{
									ma = ang;
								}
							}
						}
						if (ma > 5f)
						{
							angBad++;
						}
						if (ma > worstAng)
						{
							worstAng = ma;
							worstAngG = g;
						}
					}
				}
				st.Append("seam shading: aligned groups whose twins' normals differ >5deg = ").Append(angBad).Append(" worst=")
					.Append(worstAng.ToString("F1"))
					.Append("deg");
				if (worstAngG >= 0)
				{
					st.Append(" at ").Append(SoftFleshController.BoneAt(body, body.seamGroups[worstAngG][0]));
				}
				st.Append('\n');
				if (worstG >= 0)
				{
					st.Append("worst gap sits on bone: ").Append(SoftFleshController.BoneAt(body, body.seamGroups[worstG][0])).Append('\n');
				}
				Dictionary<long, int> useB = new Dictionary<long, int>();
				int[] canonB = new int[i];
				Dictionary<long, int> byPosB = new Dictionary<long, int>(i);
				for (int i8 = 0; i8 < i; i8++)
				{
					long pk = SoftFleshController.PosKey(body.baseVerts[i8]);
					int c0;
					if (byPosB.TryGetValue(pk, out c0))
					{
						canonB[i8] = c0;
					}
					else
					{
						byPosB[pk] = i8;
						canonB[i8] = i8;
					}
				}
				Dictionary<long, int> firstV = new Dictionary<long, int>();
				if (tris != null)
				{
					int t5 = 0;
					while (t5 + 2 < tris.Length)
					{
						for (int e2 = 0; e2 < 3; e2++)
						{
							int va2 = canonB[tris[t5 + e2]];
							int vb2 = canonB[tris[t5 + (e2 + 1) % 3]];
							if (va2 != vb2)
							{
								long k2 = ((va2 < vb2) ? (((long)va2 << 32) | (long)((ulong)vb2)) : (((long)vb2 << 32) | (long)((ulong)va2)));
								int cnt2;
								useB.TryGetValue(k2, out cnt2);
								useB[k2] = cnt2 + 1;
								if (cnt2 == 0)
								{
									firstV[k2] = tris[t5 + e2];
								}
							}
						}
						t5 += 3;
					}
				}
				Dictionary<string, int> byBone = new Dictionary<string, int>();
				foreach (KeyValuePair<long, int> kv in useB)
				{
					if (kv.Value == 1)
					{
						int vi = firstV[kv.Key];
						string bn = "?";
						if (body.domBone != null && body.bonesArr != null)
						{
							int db3 = body.domBone[vi];
							if (db3 >= 0 && db3 < body.bonesArr.Length && body.bonesArr[db3] != null)
							{
								bn = body.bonesArr[db3].name;
							}
						}
						int c;
						byBone.TryGetValue(bn, out c);
						byBone[bn] = c + 1;
					}
				}
				st.Append("model's own open edges by bone: ");
				foreach (KeyValuePair<string, int> kv2 in byBone)
				{
					st.Append(kv2.Key).Append('=').Append(kv2.Value)
						.Append("  ");
				}
				st.Append('\n');
				st.Append("seam gaps: groups=").Append(body.seamGroups.Length).Append(" (uvSeam=")
					.Append(gAligned)
					.Append(" fold=")
					.Append(gFold)
					.Append(") open>0.5mm=")
					.Append(gaps)
					.Append(" worst=")
					.Append(worstGap.ToString("F2"))
					.Append("mm")
					.Append('\n');
			}
			st.Append('\n').Append(SoftFleshController.Dihedral("CURRENT (deformed)", faces, curV));
			if (refV != null)
			{
				st.Append('\n').Append(SoftFleshController.Dihedral("BONES RESET (reference)", faces, refV));
			}
			File.WriteAllText(Path.Combine(dir, "stats" + tag + ".txt"), st.ToString());
			if (refV == null)
			{
				return 3;
			}
			return 4;
		}

		// Token: 0x060000DC RID: 220 RVA: 0x00037930 File Offset: 0x00035B30
		private static string F(float v)
		{
			return v.ToString("F5");
		}

		// Token: 0x060000DD RID: 221 RVA: 0x00037940 File Offset: 0x00035B40
		private static int CountW(float[] w, float t)
		{
			if (w == null)
			{
				return 0;
			}
			int c = 0;
			for (int i = 0; i < w.Length; i++)
			{
				if (w[i] > t)
				{
					c++;
				}
			}
			return c;
		}

		// Token: 0x060000DE RID: 222 RVA: 0x00037970 File Offset: 0x00035B70
		private static void WriteObj(string path, List<int> reg, int[] remap, List<int> faces, Vector3[] pos, Vector3[] nrm)
		{
			StringBuilder sb = new StringBuilder();
			for (int q = 0; q < reg.Count; q++)
			{
				Vector3 v = pos[reg[q]];
				sb.Append("v ").Append(SoftFleshController.F(v.x)).Append(' ')
					.Append(SoftFleshController.F(v.y))
					.Append(' ')
					.Append(SoftFleshController.F(v.z))
					.Append('\n');
			}
			if (nrm != null)
			{
				for (int q2 = 0; q2 < reg.Count; q2++)
				{
					Vector3 v2 = nrm[reg[q2]];
					sb.Append("vn ").Append(SoftFleshController.F(v2.x)).Append(' ')
						.Append(SoftFleshController.F(v2.y))
						.Append(' ')
						.Append(SoftFleshController.F(v2.z))
						.Append('\n');
				}
			}
			int t = 0;
			while (t + 2 < faces.Count)
			{
				sb.Append("f ").Append(remap[faces[t]] + 1).Append(' ')
					.Append(remap[faces[t + 1]] + 1)
					.Append(' ')
					.Append(remap[faces[t + 2]] + 1)
					.Append('\n');
				t += 3;
			}
			File.WriteAllText(path, sb.ToString());
		}

		// Token: 0x060000DF RID: 223 RVA: 0x00037AE8 File Offset: 0x00035CE8
		private static string Dihedral(string label, List<int> faces, Vector3[] pos)
		{
			Dictionary<long, int> edge = new Dictionary<long, int>();
			List<Vector3> norm = new List<Vector3>();
			List<float> pair = new List<float>();
			int t = 0;
			while (t + 2 < faces.Count)
			{
				int a = faces[t];
				int b = faces[t + 1];
				int c = faces[t + 2];
				Vector3 fn = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
				if (fn.sqrMagnitude >= 1E-16f)
				{
					fn.Normalize();
					int fi = norm.Count;
					norm.Add(fn);
					SoftFleshController.AddEdge(edge, pair, norm, a, b, fi);
					SoftFleshController.AddEdge(edge, pair, norm, b, c, fi);
					SoftFleshController.AddEdge(edge, pair, norm, c, a, fi);
				}
				t += 3;
			}
			if (pair.Count == 0)
			{
				return label + ": no shared edges\n";
			}
			pair.Sort();
			float sum = 0f;
			for (int i = 0; i < pair.Count; i++)
			{
				sum += pair[i];
			}
			StringBuilder sb = new StringBuilder();
			sb.Append(label).Append(": shared edges=").Append(pair.Count)
				.Append(" meanDihedral=")
				.Append((sum / (float)pair.Count).ToString("F2"))
				.Append("deg")
				.Append(" median=")
				.Append(pair[pair.Count / 2].ToString("F2"))
				.Append(" p90=")
				.Append(pair[(int)((float)pair.Count * 0.9f)].ToString("F2"))
				.Append(" p99=")
				.Append(pair[(int)((float)pair.Count * 0.99f)].ToString("F2"))
				.Append(" max=")
				.Append(pair[pair.Count - 1].ToString("F2"))
				.Append('\n');
			int[] bins = new int[9];
			for (int j = 0; j < pair.Count; j++)
			{
				bins[Mathf.Clamp((int)(pair[j] / 20f), 0, 8)]++;
			}
			sb.Append("  histogram (deg): ");
			for (int k = 0; k < 9; k++)
			{
				sb.Append(k * 20).Append('-').Append(k * 20 + 20)
					.Append(':')
					.Append(bins[k])
					.Append("  ");
			}
			sb.Append('\n');
			return sb.ToString();
		}

		// Token: 0x060000E0 RID: 224 RVA: 0x00037DC8 File Offset: 0x00035FC8
		private static void AddEdge(Dictionary<long, int> edge, List<float> pair, List<Vector3> norm, int a, int b, int fi)
		{
			long key = ((a < b) ? (((long)a << 32) | (long)((ulong)b)) : (((long)b << 32) | (long)((ulong)a)));
			int other;
			if (edge.TryGetValue(key, out other))
			{
				if (other >= 0)
				{
					pair.Add(Vector3.Angle(norm[other], norm[fi]));
					edge[key] = -1;
					return;
				}
			}
			else
			{
				edge[key] = fi;
			}
		}

		// Token: 0x060000E1 RID: 225 RVA: 0x00037E2C File Offset: 0x0003602C
		public void BakeOpenings()
		{
			int done = 0;
			this._bakeDetail = "";
			Stopwatch sw = Stopwatch.StartNew();
			for (int b = 0; b < this._bodies.Count; b++)
			{
				if (this.BakeBody(this._bodies[b]))
				{
					done++;
					this._bodies[b].baked = true;
				}
			}
			sw.Stop();
			this.bakeStatus = ((done > 0) ? string.Concat(new string[]
			{
				"baked ",
				done.ToString(),
				" body(ies) in ",
				sw.ElapsedMilliseconds.ToString(),
				"ms",
				this._bakeDetail
			}) : ("bake FAILED - no seed region found (Setup first?)" + this._bakeDetail));
			SoftFleshPlugin.Log("[SF] " + this.bakeStatus);
		}

		// Token: 0x060000E2 RID: 226 RVA: 0x00037F0C File Offset: 0x0003610C
		private bool BakeBody(SoftFleshController.Body body)
		{
			if (body.isFace)
			{
				return false;
			}
			int i = body.baseVerts.Length;
			if (body.foldRegion == null || body.adjStart == null)
			{
				return false;
			}
			List<int> seeds = new List<int>();
			Vector3 rigCenterB = Vector3.zero;
			Vector3 rigAxisB = Vector3.zero;
			bool rigAnchor = false;
			if (body.vagPivots.Count >= 3 && body.domBone != null && body.bonesArr != null)
			{
				Vector3 ringW = Vector3.zero;
				for (int q = 0; q < body.vagPivots.Count; q++)
				{
					ringW += body.vagPivots[q].position;
				}
				ringW /= (float)body.vagPivots.Count;
				Vector3 axisW;
				if (body.vagInner.Count > 0)
				{
					Vector3 innW = Vector3.zero;
					for (int q2 = 0; q2 < body.vagInner.Count; q2++)
					{
						innW += body.vagInner[q2].position;
					}
					innW /= (float)body.vagInner.Count;
					axisW = innW - ringW;
				}
				else
				{
					axisW = ((body.cha != null) ? (-body.cha.transform.forward) : Vector3.back);
				}
				if (axisW.sqrMagnitude > 1E-10f)
				{
					axisW.Normalize();
					for (int j = 0; j < i; j++)
					{
						body.outV[j] = body.baseVerts[j];
						body.normList[j] = body.baseNormals[j];
					}
					body.dirty.Clear();
					SoftFleshController.UploadBodyVerts(body, false);
					body.workMesh.SetNormals(body.normList);
					body.smr.BakeMesh(body.bake);
					body.bake.GetVertices(body.bakeV);
					if (body.bakeV.Count == i)
					{
						Matrix4x4 l2wB = SoftFleshController.BakeL2W(body.smr);
						int best = -1;
						float bestD = 1E+09f;
						for (int k = 0; k < i; k++)
						{
							float dd = (l2wB.MultiplyPoint3x4(body.bakeV[k]) - ringW).sqrMagnitude;
							if (dd < bestD)
							{
								bestD = dd;
								best = k;
							}
						}
						if (best >= 0)
						{
							int db = body.domBone[best];
							if (db >= 0 && db < body.bonesArr.Length && body.bonesArr[db] != null)
							{
								rigCenterB = body.baseVerts[best];
								rigAxisB = (Quaternion.Inverse(body.bonesArr[db].rotation * body.bindRot[db]) * axisW).normalized;
								rigAnchor = true;
								float rSeed = 0.09f;
								for (int l = 0; l < i; l++)
								{
									bool gen = (body.vagW != null && body.vagW[l] > 0.05f) || (body.kokW != null && body.kokW[l] > 0.1f);
									if (gen && (l2wB.MultiplyPoint3x4(body.bakeV[l]) - ringW).sqrMagnitude < rSeed * rSeed)
									{
										seeds.Add(l);
									}
								}
								if (seeds.Count < 60)
								{
									seeds.Clear();
									for (int m = 0; m < i; m++)
									{
										if ((body.vagW != null && body.vagW[m] > 0.05f) || (body.kokW != null && body.kokW[m] > 0.1f))
										{
											seeds.Add(m);
										}
									}
								}
							}
						}
					}
				}
			}
			if (seeds.Count < 8)
			{
				for (int n = 0; n < i; n++)
				{
					if ((body.vagW != null && body.vagW[n] > 0.05f) || (body.kokW != null && body.kokW[n] > 0.15f))
					{
						seeds.Add(n);
					}
				}
			}
			if (seeds.Count < 8)
			{
				for (int i2 = 0; i2 < i; i2++)
				{
					if (body.foldRegion[i2])
					{
						seeds.Add(i2);
					}
				}
			}
			if (seeds.Count < 8)
			{
				return false;
			}
			if (body.anaW != null)
			{
				for (int i3 = 0; i3 < i; i3++)
				{
					if (body.anaW[i3] > 0.1f && !seeds.Contains(i3))
					{
						seeds.Add(i3);
					}
				}
			}
			bool[] inSup = new bool[i];
			List<int> frontier = new List<int>();
			float[] supW = new float[i];
			for (int q3 = 0; q3 < seeds.Count; q3++)
			{
				inSup[seeds[q3]] = true;
				supW[seeds[q3]] = 1f;
				frontier.Add(seeds[q3]);
			}
			bool[] pinned = new bool[i];
			int rings = Mathf.Clamp(Mathf.RoundToInt(this.bakeReach / Mathf.Max(body.avgEdge, 0.0001f)), 3, 24);
			for (int ring = 0; ring < rings; ring++)
			{
				List<int> next = new List<int>();
				float t6 = ((float)ring + 1f) / (float)rings;
				float w = ((t6 <= 0.5f) ? 1f : (1f - (t6 - 0.5f) / 0.5f));
				w = w * w * (3f - 2f * w);
				for (int q4 = 0; q4 < frontier.Count; q4++)
				{
					int i4 = frontier[q4];
					for (int k2 = body.adjStart[i4]; k2 < body.adjStart[i4 + 1]; k2++)
					{
						int j2 = body.adjList[k2];
						if (!inSup[j2])
						{
							inSup[j2] = true;
							supW[j2] = w;
							next.Add(j2);
							if (ring == rings - 1)
							{
								pinned[j2] = true;
							}
						}
					}
				}
				frontier = next;
			}
			if (body.seamGroups != null)
			{
				for (int g = 0; g < body.seamGroups.Length; g++)
				{
					int[] grp = body.seamGroups[g];
					bool any = false;
					for (int j3 = 0; j3 < grp.Length; j3++)
					{
						if (inSup[grp[j3]])
						{
							any = true;
							break;
						}
					}
					if (any)
					{
						for (int j4 = 0; j4 < grp.Length; j4++)
						{
							inSup[grp[j4]] = true;
							pinned[grp[j4]] = false;
						}
					}
				}
			}
			List<int> sup = new List<int>();
			for (int i5 = 0; i5 < i; i5++)
			{
				if (inSup[i5])
				{
					sup.Add(i5);
				}
			}
			Vector3 extN = Vector3.zero;
			int extC = 0;
			for (int i6 = 0; i6 < i; i6++)
			{
				if (body.kokW != null && body.kokW[i6] > 0.1f)
				{
					extN += body.baseNormals[i6];
					extC++;
				}
			}
			if (extC == 0)
			{
				for (int q5 = 0; q5 < seeds.Count; q5++)
				{
					extN += body.baseNormals[seeds[q5]];
					extC++;
				}
			}
			if (extN.sqrMagnitude < 1E-10f)
			{
				return false;
			}
			Vector3 dir = Vector3.up;
			if (Vector3.Dot(-extN, dir) < 0.3f)
			{
				dir = (-extN).normalized;
			}
			Vector3 entryPt = Vector3.zero;
			bool haveEntry = false;
			List<int> canal = new List<int>();
			for (int i7 = 0; i7 < i; i7++)
			{
				if (body.vagW != null && body.vagW[i7] > 0.05f)
				{
					canal.Add(i7);
				}
			}
			if (canal.Count < 32)
			{
				canal.Clear();
				for (int i8 = 0; i8 < i; i8++)
				{
					if (body.kokW != null && body.kokW[i8] > 0.1f)
					{
						canal.Add(i8);
					}
				}
			}
			if (canal.Count < 16)
			{
				return false;
			}
			for (int pass = 0; pass < 2; pass++)
			{
				float lo = 1E+09f;
				float hi = -1E+09f;
				for (int q6 = 0; q6 < canal.Count; q6++)
				{
					float t7 = Vector3.Dot(body.baseVerts[canal[q6]], dir);
					if (t7 < lo)
					{
						lo = t7;
					}
					if (t7 > hi)
					{
						hi = t7;
					}
				}
				if (hi - lo < 1E-05f)
				{
					break;
				}
				float qLo = lo + (hi - lo) * 0.25f;
				float qHi = hi - (hi - lo) * 0.25f;
				Vector3 cOut = Vector3.zero;
				int nOut = 0;
				int nIn = 0;
				Vector3 cIn = Vector3.zero;
				for (int q7 = 0; q7 < canal.Count; q7++)
				{
					int i9 = canal[q7];
					float t8 = Vector3.Dot(body.baseVerts[i9], dir);
					if (t8 <= qLo)
					{
						cOut += body.baseVerts[i9];
						nOut++;
					}
					else if (t8 >= qHi)
					{
						cIn += body.baseVerts[i9];
						nIn++;
					}
				}
				if (nOut == 0 || nIn == 0)
				{
					break;
				}
				cOut /= (float)nOut;
				cIn /= (float)nIn;
				if (pass == 1)
				{
					entryPt = cOut;
					haveEntry = true;
				}
			}
			if (!haveEntry)
			{
				return false;
			}
			if (body.guideSet && body.guideBoneIx >= 0 && body.workMesh != null)
			{
				Matrix4x4[] bpsG = body.workMesh.bindposes;
				if (body.guideBoneIx < bpsG.Length)
				{
					Matrix4x4 invG = bpsG[body.guideBoneIx].inverse;
					dir = invG.MultiplyVector(body.guideLocalDir).normalized;
					Vector3 gp = invG.MultiplyPoint3x4(body.guideLocalPos);
					float bestD2 = 1E+09f;
					for (int gi = 0; gi < i; gi++)
					{
						if ((body.kokW != null && body.kokW[gi] >= 0.1f) || (body.vagW != null && body.vagW[gi] >= 0.1f))
						{
							float dd2 = (body.baseVerts[gi] - gp).sqrMagnitude;
							if (dd2 < bestD2)
							{
								bestD2 = dd2;
							}
						}
					}
					if (bestD2 < 0.0009f)
					{
						entryPt = gp;
						this.savedEntryB = gp;
						this.savedDirB = dir;
						this.savedAxisValid = true;
						this.axisDirty = true;
						SoftFleshPlugin.Log(string.Concat(new string[]
						{
							"[SF] bake using USER GUIDE: entry=",
							gp.ToString("F3"),
							" (",
							(Mathf.Sqrt(bestD2) * 1000f).ToString("F0"),
							"mm from flesh) dir=",
							dir.ToString("F2")
						}));
					}
					else
					{
						SoftFleshPlugin.Log("[SF] guide entry REJECTED (" + (Mathf.Sqrt(bestD2) * 1000f).ToString("F0") + "mm from genital flesh) - auto entry kept, dir=" + dir.ToString("F2"));
					}
				}
			}
			if (!body.guideSet && this.savedAxisValid)
			{
				float bd2 = 1E+09f;
				for (int gi2 = 0; gi2 < i; gi2++)
				{
					if ((body.kokW != null && body.kokW[gi2] >= 0.1f) || (body.vagW != null && body.vagW[gi2] >= 0.1f))
					{
						float dd3 = (body.baseVerts[gi2] - this.savedEntryB).sqrMagnitude;
						if (dd3 < bd2)
						{
							bd2 = dd3;
						}
					}
				}
				if (bd2 < 0.0009f)
				{
					entryPt = this.savedEntryB;
					dir = this.savedDirB.normalized;
					SoftFleshPlugin.Log("[SF] bake using SAVED axis: entry=" + this.savedEntryB.ToString("F3") + " dir=" + dir.ToString("F2"));
				}
				else
				{
					SoftFleshPlugin.Log("[SF] saved axis rejected (" + (Mathf.Sqrt(bd2) * 1000f).ToString("F0") + "mm from flesh) - auto axis used");
				}
			}
			Vector3 mean = entryPt;
			body.lastBakeEntryB = mean;
			body.lastBakeDirB = dir;
			Vector3 origin = mean;
			float tMin2 = 1E+09f;
			float tMax2 = -1E+09f;
			for (int q8 = 0; q8 < canal.Count; q8++)
			{
				float t9 = Vector3.Dot(body.baseVerts[canal[q8]] - origin, dir);
				if (t9 < tMin2)
				{
					tMin2 = t9;
				}
				if (t9 > tMax2)
				{
					tMax2 = t9;
				}
			}
			float pMax = tMax2;
			body.canalLen = Mathf.Clamp(pMax, 0.03f, 0.05f);
			if (body.canalLen < 0.02f)
			{
				return false;
			}
			float[] restLen = new float[body.adjList.Length];
			for (int q9 = 0; q9 < sup.Count; q9++)
			{
				int i10 = sup[q9];
				for (int k3 = body.adjStart[i10]; k3 < body.adjStart[i10 + 1]; k3++)
				{
					restLen[k3] = (body.baseVerts[body.adjList[k3]] - body.baseVerts[i10]).magnitude;
				}
			}
			body.bakeRadii = new float[] { 0.006f, 0.016f, 0.03f, 0.05f };
			body.bakeDepths = new float[]
			{
				body.canalLen * 0.35f,
				body.canalLen * 0.7f,
				body.canalLen
			};
			body.bakeVerts = sup.ToArray();
			body.creaseVerts = null;
			body.creaseTris = null;
			body.creaseBuilt = false;
			body.bakeAxialT = new float[sup.Count];
			for (int q10 = 0; q10 < sup.Count; q10++)
			{
				float t10 = Vector3.Dot(body.baseVerts[sup[q10]] - origin, dir) / Mathf.Max(body.canalLen, 0.0001f);
				body.bakeAxialT[q10] = Mathf.Clamp01(t10);
			}
			Vector3 clitU = Vector3.zero;
			string clitSrc = "none";
			if (body.bonesArr != null && body.workMesh != null)
			{
				Matrix4x4[] bpsA = body.workMesh.bindposes;
				int ikk = -1;
				int iaa = -1;
				for (int q11 = 0; q11 < body.bonesArr.Length; q11++)
				{
					Transform bn = body.bonesArr[q11];
					if (!(bn == null))
					{
						if (ikk < 0 && bn.name == "cf_j_kokan")
						{
							ikk = q11;
						}
						else if (iaa < 0 && bn.name == "cf_s_ana")
						{
							iaa = q11;
						}
					}
				}
				if (ikk >= 0 && iaa >= 0 && bpsA != null && ikk < bpsA.Length && iaa < bpsA.Length)
				{
					Vector3 kB = bpsA[ikk].inverse.MultiplyPoint3x4(Vector3.zero);
					Vector3 aB = bpsA[iaa].inverse.MultiplyPoint3x4(Vector3.zero);
					Vector3 rrA = kB - aB;
					rrA -= dir * Vector3.Dot(rrA, dir);
					if (rrA.sqrMagnitude > 1E-10f)
					{
						clitU = rrA.normalized;
						clitSrc = "bind kokan-ana";
					}
				}
			}
			if (clitU == Vector3.zero && body.vagPivots.Count > 0 && body.bonesArr != null && body.workMesh != null)
			{
				Vector3 ringW2 = Vector3.zero;
				for (int q12 = 0; q12 < body.vagPivots.Count; q12++)
				{
					ringW2 += body.vagPivots[q12].position;
				}
				ringW2 /= (float)body.vagPivots.Count;
				Vector3 axW = Vector3.zero;
				for (int q13 = 0; q13 < body.vagInner.Count; q13++)
				{
					axW += body.vagInner[q13].position;
				}
				if (body.vagInner.Count > 0)
				{
					axW = axW / (float)body.vagInner.Count - ringW2;
				}
				if (axW.sqrMagnitude < 1E-10f)
				{
					axW = Vector3.forward;
				}
				axW.Normalize();
				Vector3 upW = Vector3.up - axW * Vector3.Dot(Vector3.up, axW);
				if (upW.sqrMagnitude > 1E-10f)
				{
					upW.Normalize();
					Transform best2 = null;
					float bestT = -2f;
					for (int q14 = 0; q14 < body.vagPivots.Count; q14++)
					{
						Transform pv = body.vagPivots[q14];
						Vector3 rw = pv.position - ringW2;
						rw -= axW * Vector3.Dot(rw, axW);
						if (rw.sqrMagnitude >= 1E-10f)
						{
							float t11 = Vector3.Dot(rw.normalized, upW);
							if (t11 > bestT)
							{
								bestT = t11;
								best2 = pv;
							}
						}
					}
					Matrix4x4[] bps = body.workMesh.bindposes;
					int pb = ((best2 != null) ? Array.IndexOf<Transform>(body.bonesArr, best2) : (-1));
					if (pb >= 0 && bps != null && pb < bps.Length)
					{
						Vector3 pBind = bps[pb].inverse.MultiplyPoint3x4(Vector3.zero);
						Vector3 rr = pBind - origin;
						rr -= dir * Vector3.Dot(rr, dir);
						if (rr.sqrMagnitude > 1E-10f)
						{
							clitU = rr.normalized;
							clitSrc = "superior petal " + best2.name;
						}
					}
				}
			}
			body.bakeTiltW = new float[sup.Count];
			float dMax = 0f;
			for (int q15 = 0; q15 < sup.Count; q15++)
			{
				float d3 = (body.baseVerts[sup[q15]] - origin).magnitude;
				if (d3 > dMax)
				{
					dMax = d3;
				}
			}
			float plateau = Mathf.Min(0.02f, dMax * 0.25f);
			float fade = Mathf.Max(dMax - plateau, 0.0001f);
			for (int q16 = 0; q16 < sup.Count; q16++)
			{
				float d4 = (body.baseVerts[sup[q16]] - origin).magnitude;
				float u2 = ((d4 <= plateau) ? 1f : Mathf.Clamp01(1f - (d4 - plateau) / fade));
				body.bakeTiltW[q16] = u2 * u2 * (3f - 2f * u2);
			}
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] entrance tilt falloff: full to ",
				(plateau * 1000f).ToString("F0"),
				"mm, zero by ",
				(dMax * 1000f).ToString("F0"),
				"mm (the support's own edge)"
			}));
			body.bakeClitW = new float[sup.Count];
			int clitHot = 0;
			if (clitU != Vector3.zero)
			{
				for (int q17 = 0; q17 < sup.Count; q17++)
				{
					Vector3 rr2 = body.baseVerts[sup[q17]] - origin;
					rr2 -= dir * Vector3.Dot(rr2, dir);
					if (rr2.sqrMagnitude >= 1E-10f)
					{
						float t12 = Vector3.Dot(rr2.normalized, clitU);
						body.bakeClitW[q17] = ((t12 > 0f) ? (t12 * t12) : 0f);
						if (body.bakeClitW[q17] > 0.25f)
						{
							clitHot++;
						}
					}
				}
			}
			string[] array = new string[8];
			array[0] = "[SF] clit taper: src=";
			array[1] = clitSrc;
			array[2] = " dir=";
			int num = 3;
			Vector3 vector = clitU;
			array[num] = vector.ToString();
			array[4] = " anterior verts(w>0.25)=";
			array[5] = clitHot.ToString();
			array[6] = "/";
			array[7] = sup.Count.ToString();
			SoftFleshPlugin.Log(string.Concat(array));
			if (body.bonesArr != null && body.workMesh != null)
			{
				BoneWeight[] bwA = body.workMesh.boneWeights;
				if (bwA != null && bwA.Length == body.baseVerts.Length)
				{
					Transform[] cT = new Transform[] { body.kosiT, body.kokanDT, body.anaDT, body.siriLT, body.siriRT };
					string[] cN = new string[] { "cf_s_waist02", "cf_d_kokan", "cf_d_ana", "cf_d_siri_L", "cf_d_siri_R" };
					bool[][] own = new bool[cT.Length][];
					for (int c = 0; c < cT.Length; c++)
					{
						own[c] = new bool[body.bonesArr.Length];
						if (!(cT[c] == null))
						{
							for (int bi = 0; bi < body.bonesArr.Length; bi++)
							{
								Transform t13 = body.bonesArr[bi];
								while (t13 != null)
								{
									if (t13 == cT[c])
									{
										own[c][bi] = true;
										break;
									}
									t13 = t13.parent;
								}
							}
						}
					}
					int[] cCnt = new int[cT.Length];
					float[] cSum = new float[cT.Length];
					int band = 0;
					body.bandW = new float[body.bonesArr.Length];
					List<int> bandList = new List<int>(1024);
					for (int q18 = 0; q18 < sup.Count; q18++)
					{
						int i11 = sup[q18];
						Vector3 rr3 = body.baseVerts[i11] - origin;
						float t14 = Vector3.Dot(rr3, dir);
						if (t14 <= 0.015f && t14 >= -0.015f && (rr3 - dir * t14).magnitude <= 0.03f)
						{
							band++;
							bandList.Add(i11);
							BoneWeight w2 = bwA[i11];
							if (w2.boneIndex0 >= 0 && w2.boneIndex0 < body.bandW.Length)
							{
								body.bandW[w2.boneIndex0] += w2.weight0;
							}
							if (w2.boneIndex1 >= 0 && w2.boneIndex1 < body.bandW.Length)
							{
								body.bandW[w2.boneIndex1] += w2.weight1;
							}
							if (w2.boneIndex2 >= 0 && w2.boneIndex2 < body.bandW.Length)
							{
								body.bandW[w2.boneIndex2] += w2.weight2;
							}
							if (w2.boneIndex3 >= 0 && w2.boneIndex3 < body.bandW.Length)
							{
								body.bandW[w2.boneIndex3] += w2.weight3;
							}
							for (int c2 = 0; c2 < cT.Length; c2++)
							{
								if (!(cT[c2] == null))
								{
									float wv6 = 0f;
									if (w2.boneIndex0 >= 0 && w2.boneIndex0 < own[c2].Length && own[c2][w2.boneIndex0])
									{
										wv6 += w2.weight0;
									}
									if (w2.boneIndex1 >= 0 && w2.boneIndex1 < own[c2].Length && own[c2][w2.boneIndex1])
									{
										wv6 += w2.weight1;
									}
									if (w2.boneIndex2 >= 0 && w2.boneIndex2 < own[c2].Length && own[c2][w2.boneIndex2])
									{
										wv6 += w2.weight2;
									}
									if (w2.boneIndex3 >= 0 && w2.boneIndex3 < own[c2].Length && own[c2][w2.boneIndex3])
									{
										wv6 += w2.weight3;
									}
									if (wv6 > 0.05f)
									{
										cCnt[c2]++;
										cSum[c2] += wv6;
									}
								}
							}
						}
					}
					StringBuilder sbO = new StringBuilder();
					sbO.Append("[SF] crotch ownership of the entrance band (").Append(band).Append(" verts): ");
					for (int c3 = 0; c3 < cT.Length; c3++)
					{
						sbO.Append(cN[c3]).Append('=');
						if (cT[c3] == null)
						{
							sbO.Append("NOT FOUND");
						}
						else
						{
							sbO.Append(cCnt[c3]).Append((band > 0) ? (" (" + (100f * (float)cCnt[c3] / (float)band).ToString("F0") + "%)") : "").Append(" mean w ")
								.Append(((cCnt[c3] > 0) ? (cSum[c3] / (float)cCnt[c3]) : 0f).ToString("F2"));
						}
						sbO.Append("  ");
					}
					body.bandVerts = bandList.ToArray();
					body.bandBW = new BoneWeight[body.bandVerts.Length];
					for (int k4 = 0; k4 < body.bandVerts.Length; k4++)
					{
						body.bandBW[k4] = bwA[body.bandVerts[k4]];
					}
					body.bindArr = body.workMesh.bindposes;
					body.meshSnap = null;
					body.skinSnap = null;
					SoftFleshPlugin.Log(sbO.ToString());
					if (band > 0)
					{
						for (int bi2 = 0; bi2 < body.bandW.Length; bi2++)
						{
							body.bandW[bi2] /= (float)band;
						}
						int[] ord = new int[body.bandW.Length];
						for (int bi3 = 0; bi3 < ord.Length; bi3++)
						{
							ord[bi3] = bi3;
						}
						float[] bwCopy = body.bandW;
						Array.Sort<int>(ord, (int x, int y) => bwCopy[y].CompareTo(bwCopy[x]));
						StringBuilder sbT = new StringBuilder("[SF] entrance band owners (share of the band's total weight): ");
						int k5 = 0;
						while (k5 < 10 && k5 < ord.Length && bwCopy[ord[k5]] >= 0.005f)
						{
							Transform tb = body.bonesArr[ord[k5]];
							sbT.Append((tb != null) ? tb.name : "?").Append(' ').Append((bwCopy[ord[k5]] * 100f).ToString("F0"))
								.Append("%  ");
							k5++;
						}
						SoftFleshPlugin.Log(sbT.ToString());
					}
				}
				else
				{
					SoftFleshPlugin.Log("[SF] crotch ownership: bone weights unavailable (" + ((bwA == null) ? "null" : (bwA.Length.ToString() + " vs " + body.baseVerts.Length.ToString())) + ")");
				}
			}
			float[] envMax = new float[72];
			Vector3 uAxE = Vector3.Cross(dir, Vector3.up);
			if (uAxE.sqrMagnitude < 1E-08f)
			{
				uAxE = Vector3.Cross(dir, Vector3.right);
			}
			uAxE.Normalize();
			Vector3 vAxE = Vector3.Cross(dir, uAxE);
			float clen = ((body.canalLen > 0.01f) ? body.canalLen : 0.05f);
			int[] binOf = new int[sup.Count];
			float[] radOf = new float[sup.Count];
			for (int q19 = 0; q19 < sup.Count; q19++)
			{
				int vi = sup[q19];
				Vector3 rr4 = body.baseVerts[vi] - origin;
				float along = Vector3.Dot(rr4, dir);
				rr4 -= dir * along;
				float rad = rr4.magnitude;
				radOf[q19] = rad;
				float ang = Mathf.Atan2(Vector3.Dot(rr4, vAxE), Vector3.Dot(rr4, uAxE));
				int sc = (int)((ang + 3.1415927f) * 3.8197186f);
				if (sc < 0)
				{
					sc = 0;
				}
				else if (sc > 23)
				{
					sc = 23;
				}
				int bd3 = (int)(Mathf.Clamp01(along / clen) * 2f + 0.5f);
				if (bd3 < 0)
				{
					bd3 = 0;
				}
				else if (bd3 > 2)
				{
					bd3 = 2;
				}
				binOf[q19] = sc * 3 + bd3;
				if (body.kokW != null && vi < body.kokW.Length && body.kokW[vi] > 0.3f && rad > envMax[binOf[q19]])
				{
					envMax[binOf[q19]] = rad;
				}
			}
			float envAll = 0f;
			for (int e = 0; e < envMax.Length; e++)
			{
				if (envMax[e] > envAll)
				{
					envAll = envMax[e];
				}
			}
			for (int sc2 = 0; sc2 < 24; sc2++)
			{
				for (int bd4 = 0; bd4 < 3; bd4++)
				{
					int e2 = sc2 * 3 + bd4;
					if (envMax[e2] <= 1E-06f)
					{
						float best3 = 0f;
						for (int dsc = -2; dsc <= 2; dsc++)
						{
							int s2 = ((sc2 + dsc) % 24 + 24) % 24;
							if (envMax[s2 * 3 + bd4] > best3)
							{
								best3 = envMax[s2 * 3 + bd4];
							}
						}
						envMax[e2] = ((best3 > 1E-06f) ? best3 : envAll);
					}
				}
			}
			body.bakeOuterR = new float[sup.Count];
			int envLo = 0;
			for (int q20 = 0; q20 < sup.Count; q20++)
			{
				body.bakeOuterR[q20] = envMax[binOf[q20]];
				if (body.bakeOuterR[q20] <= radOf[q20] + 1E-06f)
				{
					envLo++;
				}
			}
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] flesh envelope: ",
				(envAll * 1000f).ToString("F0"),
				"mm max, ",
				envLo.ToString(),
				"/",
				sup.Count.ToString(),
				" verts already at or past it (they will not be pushed)"
			}));
			body.bakeDelta = new Vector3[body.bakeDepths.Length * body.bakeRadii.Length][];
			body.bakeRimR = new float[body.bakeRadii.Length];
			float[] pushW = new float[i];
			for (int q21 = 0; q21 < canal.Count; q21++)
			{
				pushW[canal[q21]] = 1f;
			}
			int wallCut = 0;
			if (this.wallOnly > 0.001f)
			{
				for (int q22 = 0; q22 < canal.Count; q22++)
				{
					int i12 = canal[q22];
					Vector3 rr5 = body.baseVerts[i12] - origin;
					rr5 -= dir * Vector3.Dot(rr5, dir);
					if (rr5.sqrMagnitude >= 1E-12f)
					{
						float facing = -Vector3.Dot(body.baseNormals[i12], rr5.normalized);
						float keep = Mathf.Clamp01((facing + 0.2f) / 0.6f);
						float wF = 1f - this.wallOnly * (1f - keep);
						if (wF < 0.999f)
						{
							wallCut++;
						}
						pushW[i12] *= wF;
					}
				}
			}
			if (this.wallOnly > 0.001f)
			{
				SoftFleshPlugin.Log(string.Concat(new string[]
				{
					"[SF] wall-only push x",
					this.wallOnly.ToString("F2"),
					": ",
					wallCut.ToString(),
					"/",
					canal.Count.ToString(),
					" canal verts damped for facing outward"
				}));
			}
			List<int> fr = new List<int>();
			for (int q23 = 0; q23 < canal.Count; q23++)
			{
				fr.Add(canal[q23]);
			}
			for (int ring2 = 1; ring2 <= 5; ring2++)
			{
				float w3 = 1f - (float)ring2 / 6f;
				List<int> nx = new List<int>();
				for (int q24 = 0; q24 < fr.Count; q24++)
				{
					int i13 = fr[q24];
					for (int k6 = body.adjStart[i13]; k6 < body.adjStart[i13 + 1]; k6++)
					{
						int j5 = body.adjList[k6];
						if (pushW[j5] <= 0f && inSup[j5])
						{
							pushW[j5] = w3;
							nx.Add(j5);
						}
					}
				}
				fr = nx;
				if (fr.Count == 0)
				{
					break;
				}
			}
			body.bakeSuckW = new float[sup.Count];
			body.bakePetW = new float[sup.Count];
			for (int q25 = 0; q25 < sup.Count; q25++)
			{
				int si = sup[q25];
				float w4 = supW[si] * (0.35f + 0.65f * Mathf.Clamp01(pushW[si]));
				float pw = ((body.petW != null) ? (body.petW[si] * 1.6f) : 0f);
				body.bakePetW[q25] = pw;
				body.bakeSuckW[q25] = ((pw > w4) ? pw : w4);
			}
			Vector3[] axPt = new Vector3[6];
			int[] axCnt = new int[6];
			for (int q26 = 0; q26 < canal.Count; q26++)
			{
				int i14 = canal[q26];
				float t15 = Vector3.Dot(body.baseVerts[i14] - origin, dir) / Mathf.Max(body.canalLen, 0.0001f);
				int sl = (int)(Mathf.Clamp01(t15) * 5f + 0.5f);
				axPt[sl] += body.baseVerts[i14];
				axCnt[sl]++;
			}
			for (int sl2 = 0; sl2 < 6; sl2++)
			{
				if (axCnt[sl2] > 0)
				{
					axPt[sl2] /= (float)axCnt[sl2];
				}
				else
				{
					axPt[sl2] = origin + dir * (body.canalLen * (float)sl2 / 5f);
				}
			}
			axPt[0] = origin;
			for (int sl3 = 1; sl3 < 6; sl3++)
			{
				if (sl3 < 5)
				{
					axPt[sl3] = (axPt[sl3 - 1] + axPt[sl3] * 2f + axPt[sl3 + 1]) * 0.25f;
				}
			}
			for (int sl4 = 1; sl4 < 6; sl4++)
			{
				float want = body.canalLen * (float)sl4 / 5f;
				Vector3 straight = origin + dir * want;
				Vector3 lat = axPt[sl4] - straight;
				lat -= dir * Vector3.Dot(lat, dir);
				float lm = lat.magnitude;
				if (lm > 0.008f)
				{
					lat *= 0.008f / lm;
				}
				axPt[sl4] = straight + lat;
			}
			int[] supIdx = new int[i];
			for (int i15 = 0; i15 < i; i15++)
			{
				supIdx[i15] = -1;
			}
			for (int q27 = 0; q27 < sup.Count; q27++)
			{
				supIdx[sup[q27]] = q27;
			}
			Vector3[] P = new Vector3[i];
			Vector3[] buf = new Vector3[i];
			float bakePeak2 = 0f;
			for (int di = 0; di < body.bakeDepths.Length; di++)
			{
				for (int ri = 0; ri < body.bakeRadii.Length; ri++)
				{
					float R = body.bakeRadii[ri];
					float D = body.bakeDepths[di];
					Array.Copy(body.baseVerts, P, i);
					float[] restT = new float[sup.Count];
					for (int q28 = 0; q28 < sup.Count; q28++)
					{
						restT[q28] = Vector3.Dot(body.baseVerts[sup[q28]] - origin, dir);
					}
					int iters = ((sup.Count > 2500) ? Mathf.Max(120, 750000 / sup.Count) : 300);
					for (int it = 0; it < iters; it++)
					{
						float lam = (((it & 1) == 0) ? 0.45f : (-0.47f));
						SoftFleshController.ParallelFor(sup.Count, delegate(int _q0, int _q1)
						{
							for (int q37 = _q0; q37 < _q1; q37++)
							{
								int i21 = sup[q37];
								if (pinned[i21])
								{
									buf[i21] = P[i21];
								}
								else
								{
									Vector3 rest0 = body.baseVerts[i21];
									Vector3 pos = P[i21];
									if (pushW[i21] > 0f)
									{
										Vector3 cp = axPt[0];
										float best4 = float.MaxValue;
										int sl5 = 0;
										while (sl5 + 1 < 6)
										{
											Vector3 pA = axPt[sl5];
											Vector3 pB = axPt[sl5 + 1];
											if (Vector3.Dot(pA - origin, dir) > D)
											{
												break;
											}
											Vector3 ab = pB - pA;
											float ll = ab.sqrMagnitude;
											float tt = ((ll > 1E-12f) ? (Vector3.Dot(pos - pA, ab) / ll) : 0f);
											if (tt < 0f)
											{
												tt = 0f;
											}
											else if (tt > 1f)
											{
												tt = 1f;
											}
											Vector3 c5 = pA + ab * tt;
											if (Vector3.Dot(c5 - origin, dir) <= D)
											{
												float dd4 = (pos - c5).sqrMagnitude;
												if (dd4 < best4)
												{
													best4 = dd4;
													cp = c5;
												}
											}
											sl5++;
										}
										float tD = Vector3.Dot(cp - origin, dir) / Mathf.Max(D, 0.0001f);
										float Reff = R;
										if (tD > 0.65f)
										{
											float u3 = Mathf.Clamp01((1f - tD) / 0.35f);
											Reff = R * u3 * u3 * (3f - 2f * u3);
										}
										Vector3 rv = pos - cp;
										float rd2 = rv.magnitude;
										if (Reff > 1E-06f && rd2 > 1E-07f)
										{
											float rdT = ((rd2 < Reff) ? Reff : rd2);
											if (rdT > rd2)
											{
												pos += rv * ((rdT - rd2) / rd2 * 0.6f * pushW[i21]);
											}
										}
									}
									Vector3 corr = Vector3.zero;
									int nc = 0;
									int a0 = body.adjStart[i21];
									int a = body.adjStart[i21 + 1];
									float allowStretch = 1f - supW[i21];
									for (int k8 = a0; k8 < a; k8++)
									{
										Vector3 e3 = P[body.adjList[k8]] - pos;
										float le = e3.magnitude;
										if (le > 1E-07f && restLen[k8] > 0f)
										{
											float f = 1f - restLen[k8] / le;
											if (f > 0f)
											{
												f *= 1f - allowStretch * 0.85f;
											}
											corr += e3 * f;
											nc++;
										}
									}
									if (nc > 0)
									{
										pos += corr * (0.3f / (float)nc);
									}
									Vector3 avgD = Vector3.zero;
									int nd = 0;
									for (int k9 = a0; k9 < a; k9++)
									{
										int j9 = body.adjList[k9];
										avgD += P[j9] - body.baseVerts[j9];
										nd++;
									}
									if (nd > 0)
									{
										avgD /= (float)nd;
										Vector3 own2 = pos - body.baseVerts[i21];
										pos += (avgD - own2) * (lam * 0.8f);
									}
									if (restT[q37] >= 0f)
									{
										float curT = Vector3.Dot(pos - origin, dir);
										if (curT < -0.002f)
										{
											pos += dir * (-0.002f - curT);
										}
									}
									float sw = supW[i21];
									buf[i21] = ((sw >= 0.999f) ? pos : (rest0 + (pos - rest0) * sw));
								}
							}
						});
						for (int q29 = 0; q29 < sup.Count; q29++)
						{
							int i16 = sup[q29];
							P[i16] = buf[i16];
						}
					}
					if (di == body.bakeDepths.Length - 1 && ri == body.bakeRadii.Length - 1)
					{
						int[] hist = new int[6];
						int petN = 0;
						int petHi = 0;
						int petLo = 0;
						for (int q30 = 0; q30 < sup.Count; q30++)
						{
							int i17 = sup[q30];
							float w5 = pushW[i17];
							int hb = ((w5 <= 0.001f) ? 0 : ((int)(w5 * 4.999f) + 1));
							if (hb > 5)
							{
								hb = 5;
							}
							hist[hb]++;
							if (body.petW != null && body.petW[i17] > 0.05f)
							{
								petN++;
								if (w5 > 0.95f)
								{
									petHi++;
								}
								else if (w5 < 0.35f)
								{
									petLo++;
								}
							}
						}
						int inv = 0;
						int tot = 0;
						int invPet = 0;
						int inv2mm = 0;
						int inv2mmPet = 0;
						float gapInv = 0f;
						float gapOk = 0f;
						float worstInv = 0f;
						float[] topOver = new float[5];
						string[] topDesc = new string[5];
						for (int q31 = 0; q31 < sup.Count; q31++)
						{
							int i18 = sup[q31];
							Vector3 bi4 = body.baseVerts[i18] - origin;
							bi4 -= dir * Vector3.Dot(bi4, dir);
							Vector3 pi = P[i18] - origin;
							pi -= dir * Vector3.Dot(pi, dir);
							float ri2 = bi4.magnitude;
							float ri3 = pi.magnitude;
							for (int k7 = body.adjStart[i18]; k7 < body.adjStart[i18 + 1]; k7++)
							{
								int j6 = body.adjList[k7];
								if (j6 > i18 && supIdx[j6] >= 0)
								{
									Vector3 bj = body.baseVerts[j6] - origin;
									float rj0 = (bj - dir * Vector3.Dot(bj, dir)).magnitude;
									if (Mathf.Abs(ri2 - rj0) >= 0.0005f)
									{
										Vector3 pj = P[j6] - origin;
										float rj = (pj - dir * Vector3.Dot(pj, dir)).magnitude;
										tot++;
										float g2 = Mathf.Abs(pushW[i18] - pushW[j6]);
										if (ri2 < rj0 != ri3 < rj)
										{
											inv++;
											gapInv += g2;
											float over = Mathf.Abs(ri3 - rj);
											if (over > worstInv)
											{
												worstInv = over;
											}
											bool onPet = body.petW != null && (body.petW[i18] > 0.05f || body.petW[j6] > 0.05f);
											if (onPet)
											{
												invPet++;
											}
											if (over > 0.002f)
											{
												inv2mm++;
												if (onPet)
												{
													inv2mmPet++;
												}
											}
											if (over > topOver[4])
											{
												int at = 4;
												while (at > 0 && over > topOver[at - 1])
												{
													at--;
												}
												for (int z = 4; z > at; z--)
												{
													topOver[z] = topOver[z - 1];
													topDesc[z] = topDesc[z - 1];
												}
												topOver[at] = over;
												float tAx = Vector3.Dot(body.baseVerts[i18] - origin, dir);
												topDesc[at] = string.Concat(new string[]
												{
													(over * 1000f).ToString("F1"),
													"mm at t=",
													(tAx * 1000f).ToString("F0"),
													"mm r ",
													(ri2 * 1000f).ToString("F0"),
													"->",
													(ri3 * 1000f).ToString("F0"),
													" vs ",
													(rj0 * 1000f).ToString("F0"),
													"->",
													(rj * 1000f).ToString("F0"),
													" w ",
													pushW[i18].ToString("F2"),
													"/",
													pushW[j6].ToString("F2"),
													" ",
													SoftFleshController.BoneAt(body, i18)
												});
											}
										}
										else
										{
											gapOk += g2;
										}
									}
								}
							}
						}
						SoftFleshPlugin.Log(string.Concat(new string[]
						{
							"[SF] pushW over ",
							sup.Count.ToString(),
							" support verts: zero=",
							hist[0].ToString(),
							" <0.2=",
							hist[1].ToString(),
							" <0.4=",
							hist[2].ToString(),
							" <0.6=",
							hist[3].ToString(),
							" <0.8=",
							hist[4].ToString(),
							" <=1=",
							hist[5].ToString(),
							" | petal verts ",
							petN.ToString(),
							": full-rate ",
							petHi.ToString(),
							", starved(<0.35) ",
							petLo.ToString()
						}));
						SoftFleshPlugin.Log(string.Concat(new string[]
						{
							"[SF] layer order: ",
							inv.ToString(),
							"/",
							tot.ToString(),
							" adjacent pairs finished INVERTED (",
							invPet.ToString(),
							" of them on a petal), worst by ",
							(worstInv * 1000f).ToString("F1"),
							"mm | >2mm: ",
							inv2mm.ToString(),
							" (",
							inv2mmPet.ToString(),
							" on a petal) | mean pushW gap: inverted ",
							((inv > 0) ? (gapInv / (float)inv) : 0f).ToString("F3"),
							" vs intact ",
							((tot > inv) ? (gapOk / (float)(tot - inv)) : 0f).ToString("F3"),
							"  (target=rest-bound, envelope-anchored)"
						}));
						int z2 = 0;
						while (z2 < 5 && topOver[z2] > 0f)
						{
							SoftFleshPlugin.Log("[SF]   worst#" + (z2 + 1).ToString() + ": " + topDesc[z2]);
							z2++;
						}
					}
					Vector3[] deltas = new Vector3[sup.Count];
					for (int q32 = 0; q32 < sup.Count; q32++)
					{
						deltas[q32] = P[sup[q32]] - body.baseVerts[sup[q32]];
					}
					if (body.seamGroups != null && supIdx != null)
					{
						for (int g3 = 0; g3 < body.seamGroups.Length; g3++)
						{
							int[] grp2 = body.seamGroups[g3];
							Vector3 acc = Vector3.zero;
							int cnt2 = 0;
							for (int j7 = 0; j7 < grp2.Length; j7++)
							{
								int si2 = supIdx[grp2[j7]];
								if (si2 >= 0)
								{
									acc += deltas[si2];
									cnt2++;
								}
							}
							if (cnt2 >= 2)
							{
								acc /= (float)cnt2;
								for (int j8 = 0; j8 < grp2.Length; j8++)
								{
									int si3 = supIdx[grp2[j8]];
									if (si3 >= 0)
									{
										deltas[si3] = acc;
									}
								}
							}
						}
					}
					for (int q33 = 0; q33 < sup.Count; q33++)
					{
						float dm = deltas[q33].sqrMagnitude;
						if (dm > bakePeak2)
						{
							bakePeak2 = dm;
						}
					}
					if (di == 0)
					{
						List<float> rimR = new List<float>(64);
						List<int> rimIdx = ((ri == 0) ? new List<int>(64) : null);
						for (int q34 = 0; q34 < sup.Count; q34++)
						{
							if (restT[q34] >= -0.004f && restT[q34] <= 0.004f)
							{
								Vector3 r0w = body.baseVerts[sup[q34]] - origin;
								if ((r0w - dir * Vector3.Dot(r0w, dir)).sqrMagnitude <= 0.000144f)
								{
									Vector3 pw2 = body.baseVerts[sup[q34]] + deltas[q34] - origin;
									rimR.Add((pw2 - dir * Vector3.Dot(pw2, dir)).magnitude);
									if (rimIdx != null)
									{
										rimIdx.Add(sup[q34]);
									}
								}
							}
						}
						if (rimIdx != null && rimIdx.Count > 0)
						{
							body.rimVerts = rimIdx.ToArray();
							body.rimOrigin = origin;
							body.rimDir = dir;
						}
						if (rimR.Count > 0)
						{
							rimR.Sort();
							body.bakeRimR[ri] = rimR[Mathf.Clamp((int)((float)rimR.Count * 0.25f), 0, rimR.Count - 1)];
						}
					}
					body.bakeDelta[di * body.bakeRadii.Length + ri] = deltas;
				}
			}
			body.bakeReady = true;
			this._bakeDetail = string.Concat(new string[]
			{
				rigAnchor ? " [rig]" : " [centroid]",
				" thr=",
				(Environment.ProcessorCount - 1).ToString(),
				" seeds=",
				seeds.Count.ToString(),
				" sup=",
				sup.Count.ToString(),
				" pmax=",
				(pMax * 1000f).ToString("F0"),
				"mm dmax=",
				(Mathf.Sqrt(bakePeak2) * 1000f).ToString("F1"),
				"mm canal=",
				(body.canalLen * 1000f).ToString("F0"),
				"mm"
			});
			if (bakePeak2 < 1E-08f)
			{
				this._bakeDetail += "  <-- EMPTY BAKE (capsule missed the mesh)";
			}
			Vector3[] last = body.bakeDelta[body.bakeDelta.Length - 1];
			double tot2 = 0.0;
			double bad = 0.0;
			int moved = 0;
			int q35 = 0;
			while (q35 < body.bakeVerts.Length && last != null && q35 < last.Length)
			{
				int i19 = body.bakeVerts[q35];
				Vector3 dv = last[q35];
				float m2 = dv.magnitude;
				if (m2 >= 1E-06f)
				{
					moved++;
					tot2 += (double)m2;
					Vector3 rr6 = body.baseVerts[i19] - mean;
					rr6 -= dir * Vector3.Dot(rr6, dir);
					if (rr6.sqrMagnitude >= 1E-12f && Vector3.Dot(body.baseNormals[i19], rr6.normalized) > 0f)
					{
						bad += (double)m2;
					}
				}
				q35++;
			}
			int escaped = 0;
			float deepest = 0f;
			float worstOut = 0f;
			float escR = 0f;
			float escRMax = 0f;
			int escN = 0;
			int escSlit = 0;
			Dictionary<string, int> byBone = new Dictionary<string, int>();
			int q36 = 0;
			while (q36 < body.bakeVerts.Length && last != null && q36 < last.Length)
			{
				int i20 = body.bakeVerts[q36];
				float tR = Vector3.Dot(body.baseVerts[i20] - mean, dir);
				float tM = Vector3.Dot(body.baseVerts[i20] + last[q36] - mean, dir);
				if (tR > 0f && tM < 0f)
				{
					escaped++;
					Vector3 rrE = body.baseVerts[i20] - mean;
					float rE = (rrE - dir * Vector3.Dot(rrE, dir)).magnitude;
					escR += rE;
					escN++;
					if (rE < 0.012f)
					{
						escSlit++;
					}
					if (rE > escRMax)
					{
						escRMax = rE;
					}
					if (tR > deepest)
					{
						deepest = tR;
					}
					if (-tM > worstOut)
					{
						worstOut = -tM;
					}
					string bn2 = SoftFleshController.BoneAt(body, i20);
					int c4;
					byBone.TryGetValue(bn2, out c4);
					byBone[bn2] = c4 + 1;
				}
				q36++;
			}
			StringBuilder sb4 = new StringBuilder("[SF] bake escape: ");
			sb4.Append(escaped).Append(" verts crossed OUT of the entrance");
			if (escaped > 0)
			{
				sb4.Append(" | rest radius mean ").Append(((escN > 0) ? (escR / (float)escN * 1000f) : 0f).ToString("F0")).Append("mm max ")
					.Append((escRMax * 1000f).ToString("F0"))
					.Append("mm, ")
					.Append(escSlit)
					.Append(" of them INSIDE the 12mm slit");
				sb4.Append(" | deepest origin ").Append((deepest * 1000f).ToString("F0")).Append("mm in");
				sb4.Append(", furthest out ").Append((worstOut * 1000f).ToString("F0")).Append("mm");
				sb4.Append(" | ");
				foreach (KeyValuePair<string, int> kv in byBone)
				{
					sb4.Append(kv.Key).Append('=').Append(kv.Value)
						.Append("  ");
				}
			}
			SoftFleshPlugin.Log(sb4.ToString());
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] bake check: moved=",
				moved.ToString(),
				"/",
				body.bakeVerts.Length.ToString(),
				" displacement on OUTWARD-facing flesh = ",
				(tot2 > 0.0) ? (100.0 * bad / tot2).ToString("F0") : "0",
				"%  (was 36% when the eversion was measured)"
			}));
			StringBuilder rb2 = new StringBuilder("[SF] rim calibration (sample radius -> rim actually delivered): ");
			for (int ri4 = 0; ri4 < body.bakeRadii.Length; ri4++)
			{
				rb2.Append((body.bakeRadii[ri4] * 1000f).ToString("F0")).Append("mm->").Append((body.bakeRimR[ri4] * 1000f).ToString("F1"))
					.Append("mm  ");
			}
			SoftFleshPlugin.Log(rb2.ToString());
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] baked opening: anchor=",
				rigAnchor ? "RIG" : "centroid",
				" support=",
				sup.Count.ToString(),
				" canal=",
				(body.canalLen * 1000f).ToString("F0"),
				"mm dir=",
				dir.ToString("F2")
			}));
			return true;
		}

		// Token: 0x060000E3 RID: 227 RVA: 0x0003C360 File Offset: 0x0003A560
		private static SoftFleshController.Prof BuildProfile(List<Vector3> pts, Vector3 c, Vector3 ax, float lo, float span, float rFallback)
		{
			SoftFleshController.Prof pf = new SoftFleshController.Prof
			{
				NS = 6,
				NB = 12,
				ax = ax,
				len = Mathf.Max(span, 1E-05f),
				origin = c + ax * lo,
				r = new float[72]
			};
			Vector3 hint = ((Mathf.Abs(ax.y) < 0.9f) ? Vector3.up : Vector3.right);
			pf.u = Vector3.Cross(ax, hint).normalized;
			pf.v = Vector3.Cross(ax, pf.u).normalized;
			bool[] have = new bool[72];
			for (int i = 0; i < pts.Count; i++)
			{
				Vector3 d = pts[i] - pf.origin;
				float along = Vector3.Dot(d, ax);
				int si = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(along / pf.len) * 5f), 0, 5);
				Vector3 lat = d - ax * along;
				float rad = lat.magnitude;
				float ang = SoftFleshController.FastAtan2(Vector3.Dot(lat, pf.v), Vector3.Dot(lat, pf.u));
				int bi = Mathf.Clamp((int)((ang + 3.1415927f) / 6.2831855f * 12f), 0, 11);
				int j = si * 12 + bi;
				if (rad > pf.r[j])
				{
					pf.r[j] = rad;
				}
				have[j] = true;
			}
			for (int pass = 0; pass < 4; pass++)
			{
				bool any = false;
				for (int si2 = 0; si2 < 6; si2++)
				{
					for (int bi2 = 0; bi2 < 12; bi2++)
					{
						int k = si2 * 12 + bi2;
						if (!have[k])
						{
							float sum = 0f;
							int cnt = 0;
							for (int ds = -1; ds <= 1; ds++)
							{
								for (int db = -1; db <= 1; db++)
								{
									int s2 = si2 + ds;
									if (s2 >= 0 && s2 < 6)
									{
										int b2 = ((bi2 + db) % 12 + 12) % 12;
										if (have[s2 * 12 + b2])
										{
											sum += pf.r[s2 * 12 + b2];
											cnt++;
										}
									}
								}
							}
							if (cnt > 0)
							{
								pf.r[k] = sum / (float)cnt;
								have[k] = true;
								any = true;
							}
						}
					}
				}
				if (!any)
				{
					break;
				}
			}
			for (int l = 0; l < pf.r.Length; l++)
			{
				if (!have[l])
				{
					pf.r[l] = rFallback;
				}
			}
			float[] tmp = new float[72];
			for (int si3 = 0; si3 < 6; si3++)
			{
				for (int bi3 = 0; bi3 < 12; bi3++)
				{
					float a0 = pf.r[si3 * 12 + (bi3 - 1 + 12) % 12];
					float a = pf.r[si3 * 12 + bi3];
					float a2 = pf.r[si3 * 12 + (bi3 + 1) % 12];
					tmp[si3 * 12 + bi3] = (a0 + 2f * a + a2) * 0.25f;
				}
			}
			Array.Copy(tmp, pf.r, pf.r.Length);
			for (int m = 0; m < pf.r.Length; m++)
			{
				if (pf.r[m] > pf.rMax)
				{
					pf.rMax = pf.r[m];
				}
			}
			return pf;
		}

		// Token: 0x060000E4 RID: 228 RVA: 0x0003C6E0 File Offset: 0x0003A8E0
		private void FinishItemChain(Transform tr, List<SoftFleshController.Presser> made, int lobes)
		{
			Vector3[] mid = new Vector3[made.Count];
			float[] scl = new float[made.Count];
			float[] rw = new float[made.Count];
			float worldMax = 0f;
			for (int i = 0; i < made.Count; i++)
			{
				SoftFleshController.Presser d = made[i];
				Vector3 s3 = d.a.lossyScale;
				scl[i] = (s3.x + s3.y + s3.z) * 0.33333334f;
				if (scl[i] < 0.01f)
				{
					scl[i] = 0.01f;
				}
				mid[i] = d.a.TransformPoint((d.la + d.lb) * 0.5f);
				rw[i] = Mathf.Max(d.lrad, d.lradB) * scl[i];
				if (rw[i] > worldMax)
				{
					worldMax = rw[i];
				}
			}
			int e0 = 0;
			int e = 0;
			float best = -1f;
			for (int j = 0; j < made.Count; j++)
			{
				for (int k = j + 1; k < made.Count; k++)
				{
					float d2 = (mid[j] - mid[k]).sqrMagnitude;
					if (d2 > best)
					{
						best = d2;
						e0 = j;
						e = k;
					}
				}
			}
			int tip = ((rw[e0] <= rw[e]) ? e0 : e);
			int baseI = ((tip == e0) ? e : e0);
			float total = Mathf.Sqrt(best);
			Vector3 axW = mid[baseI] - mid[tip];
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] item ends: TIP=",
				(made[tip].a != null) ? made[tip].a.name : "?",
				" r=",
				(rw[tip] * 1000f).ToString("F1"),
				"mm  BASE=",
				(made[baseI].a != null) ? made[baseI].a.name : "?",
				" r=",
				(rw[baseI] * 1000f).ToString("F1"),
				"mm | length ",
				(total * 1000f).ToString("F0"),
				"mm | tip->base world dir ",
				(axW.sqrMagnitude > 1E-09f) ? axW.normalized.ToString("F2") : "?",
				"  (the belly mound grows FROM the tip; if these two are swapped it grows the wrong way)"
			}));
			for (int l = 0; l < made.Count; l++)
			{
				SoftFleshController.Presser d3 = made[l];
				d3.lradMax = worldMax / scl[l];
				d3.insertMode = this.addAsInsert && (!(d3.a != null) || !SoftFleshController.IsHangName(d3.a.name));
				d3.tipA = l == tip;
				d3.tipB = l == baseI;
				float f = ((total > 1E-05f) ? ((mid[l] - mid[tip]).magnitude / total) : 0f);
				d3.tipF = f;
				d3.bulgeW = ((f < 0.3f) ? 1f : ((f < 0.4f) ? 0.5f : 0f));
				float rmin = 0.003f / scl[l];
				if (d3.lrad < rmin)
				{
					d3.lrad = rmin;
				}
				if (d3.lradB < rmin)
				{
					d3.lradB = rmin;
				}
				d3.owner = tr;
				this._itemDefs.Add(d3);
				this._pressers.Add(d3);
			}
			float profMax = 0f;
			for (int m = 0; m < made.Count; m++)
			{
				SoftFleshController.Prof pf = made[m].prof;
				if (pf != null)
				{
					for (int n = 0; n < pf.r.Length; n++)
					{
						float rr2 = pf.r[n] * scl[m];
						if (rr2 > profMax)
						{
							profMax = rr2;
						}
					}
				}
			}
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] item bone-chain: ",
				tr.name,
				" ",
				lobes.ToString(),
				" lobe(s) -> ",
				made.Count.ToString(),
				" capsules riding ",
				SoftFleshController.CountBones(made).ToString(),
				" bone(s) (follows DynamicBone) | measured profile max radius ",
				(profMax * 1000f).ToString("F1"),
				"mm, capsule envelope ",
				(worldMax * 1000f).ToString("F1"),
				"mm"
			}));
			this.AttachDynColliders(tr);
		}

		// Token: 0x060000E5 RID: 229 RVA: 0x0003CC10 File Offset: 0x0003AE10
		private static int CountBones(List<SoftFleshController.Presser> made)
		{
			List<Transform> seen = new List<Transform>();
			for (int i = 0; i < made.Count; i++)
			{
				if (!seen.Contains(made[i].a))
				{
					seen.Add(made[i].a);
				}
			}
			return seen.Count;
		}

		// Token: 0x060000E6 RID: 230 RVA: 0x0003CC60 File Offset: 0x0003AE60
		private void ClearViz()
		{
			for (int q = 0; q < this._viz.Count; q++)
			{
				if (this._viz[q] != null)
				{
					Object.Destroy(this._viz[q].gameObject);
				}
			}
			this._viz.Clear();
		}

		// Token: 0x060000E7 RID: 231 RVA: 0x0003CCB8 File Offset: 0x0003AEB8
		private DynamicBoneCollider EnsureCanalCollider()
		{
			if (this._canalCol != null)
			{
				return this._canalCol;
			}
			for (int b = 0; b < this._bodies.Count; b++)
			{
				SoftFleshController.Body body = this._bodies[b];
				if (body.axisFrameOK && body.bonesArr != null)
				{
					int ix = body.axisBoneIx;
					if (ix >= 0 && ix < body.bonesArr.Length)
					{
						Transform rb = body.bonesArr[ix];
						if (!(rb == null))
						{
							GameObject go2 = new GameObject("SF_canalCol");
							go2.transform.SetParent(rb, false);
							Vector3 ps = rb.lossyScale;
							go2.transform.localScale = new Vector3((Mathf.Abs(ps.x) > 0.0001f) ? (1f / ps.x) : 1f, (Mathf.Abs(ps.y) > 0.0001f) ? (1f / ps.y) : 1f, (Mathf.Abs(ps.z) > 0.0001f) ? (1f / ps.z) : 1f);
							DynamicBoneCollider col = go2.AddComponent<DynamicBoneCollider>();
							col.m_Direction = 1;
							col.m_Bound = 1;
							col.m_Center = Vector3.zero;
							col.m_Radius = 0.02f;
							col.m_Height = 0.09f;
							this._canalCol = col;
							SoftFleshPlugin.Log("[SF] flesh push-back: canal collider created under " + rb.name);
							return this._canalCol;
						}
					}
				}
			}
			return null;
		}

		// Token: 0x060000E8 RID: 232 RVA: 0x0003CE54 File Offset: 0x0003B054
		private void UpdateCanalCollider()
		{
			if (!this.canalCollider || this._canalCol == null || this._bodies.Count == 0)
			{
				return;
			}
			SoftFleshController.Body body = null;
			for (int i = 0; i < this._bodies.Count; i++)
			{
				if (this._bodies[i].axisFrameOK)
				{
					body = this._bodies[i];
					break;
				}
			}
			if (body == null)
			{
				return;
			}
			float len = ((body.canalLen > 0.01f) ? body.canalLen : 0.05f);
			float r = ((body.rimNowMm > 1f) ? (body.rimNowMm * 0.001f) : 0.018f);
			r = Mathf.Clamp(r + 0.002f, 0.01f, 0.07f);
			Transform tr = this._canalCol.transform;
			Transform pr = tr.parent;
			if (pr != null)
			{
				Vector3 ps = pr.lossyScale;
				tr.localScale = new Vector3((Mathf.Abs(ps.x) > 0.0001f) ? (1f / ps.x) : 1f, (Mathf.Abs(ps.y) > 0.0001f) ? (1f / ps.y) : 1f, (Mathf.Abs(ps.z) > 0.0001f) ? (1f / ps.z) : 1f);
			}
			float outLead = 0.02f;
			tr.rotation = Quaternion.FromToRotation(Vector3.up, body.axisDwW);
			tr.position = body.axisEwW + body.axisDwW * (len * 0.5f - outLead * 0.5f);
			this._canalCol.m_Radius = r;
			this._canalCol.m_Height = len + outLead + r * 2f;
			SoftFleshController.AimRailMm = r * 1000f;
		}

		// Token: 0x060000E9 RID: 233 RVA: 0x0003D040 File Offset: 0x0003B240
		private void AttachCanalToItem(GameObject go)
		{
			if (!this.canalCollider)
			{
				return;
			}
			DynamicBoneCollider col = this.EnsureCanalCollider();
			if (col == null)
			{
				SoftFleshPlugin.Log("[SF] canal collider: no body set up yet - Setup first");
				return;
			}
			DynamicBone[] all = go.GetComponentsInChildren<DynamicBone>();
			int n2 = 0;
			int i = 0;
			while (all != null && i < all.Length)
			{
				DynamicBone db = all[i];
				if (!(db == null))
				{
					if (db.m_Colliders == null)
					{
						db.m_Colliders = new List<DynamicBoneCollider>();
					}
					if (!db.m_Colliders.Contains(col))
					{
						db.m_Colliders.Add(col);
						n2++;
					}
				}
				i++;
			}
			SoftFleshPlugin.Log("[SF] canal collider registered on " + n2.ToString() + " item chain(s)");
		}

		// Token: 0x060000EA RID: 234 RVA: 0x0003D0EC File Offset: 0x0003B2EC
		private void RegisterAim(Transform tr, GameObject go)
		{
			MonoBehaviour[] all = go.GetComponentsInChildren<MonoBehaviour>();
			List<Component> found = new List<Component>();
			int i = 0;
			while (all != null && i < all.Length)
			{
				if (!(all[i] == null))
				{
					string tn = all[i].GetType().Name;
					if (tn.StartsWith("DynamicBone") && !tn.Contains("Collider") && !all[i].gameObject.name.StartsWith("SF_"))
					{
						found.Add(all[i]);
					}
				}
				i++;
			}
			if (found.Count == 0)
			{
				SoftFleshPlugin.Log("[SF] item aim: " + tr.name + " has NO DynamicBone-like component - its flex comes from something else, so aiming cannot drive it");
				return;
			}
			Component[] dbs = found.ToArray();
			for (int j = 0; j < this._aims.Count; j++)
			{
				if (this._aims[j].root == tr)
				{
					return;
				}
			}
			Vector3[] of = new Vector3[dbs.Length];
			SoftFleshController.DbAccess[] ac = new SoftFleshController.DbAccess[dbs.Length];
			float[] os = new float[dbs.Length];
			float[] od = new float[dbs.Length];
			float[] oe = new float[dbs.Length];
			for (int k = 0; k < dbs.Length; k++)
			{
				SoftFleshController.DbAccess a = SoftFleshController.AccessFor(dbs[k].GetType());
				ac[k] = a;
				if (a.force != null)
				{
					of[k] = (Vector3)a.force.GetValue(dbs[k]);
				}
				if (a.stif != null)
				{
					os[k] = (float)a.stif.GetValue(dbs[k]);
				}
				if (a.damp != null)
				{
					od[k] = (float)a.damp.GetValue(dbs[k]);
				}
				if (a.elast != null)
				{
					oe[k] = (float)a.elast.GetValue(dbs[k]);
				}
			}
			this._aims.Add(new SoftFleshController.AimTarget
			{
				root = tr,
				bones = dbs,
				acc = ac,
				origForce = of,
				origStif = os,
				origDamp = od,
				origElast = oe
			});
			SoftFleshController.GrabParticles(this._aims[this._aims.Count - 1]);
			SoftFleshController.DbAccess a2 = ac[0];
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] item aim: ",
				tr.name,
				" -> ",
				dbs.Length.ToString(),
				" x ",
				a2.typeName,
				" | fields force=",
				(a2.force != null).ToString(),
				" stif=",
				(a2.stif != null).ToString(),
				" damp=",
				(a2.damp != null).ToString(),
				" elast=",
				(a2.elast != null).ToString(),
				" | UpdateParameters=",
				(a2.update != null).ToString(),
				" | authored stif=",
				os[0].ToString("F2"),
				" damp=",
				od[0].ToString("F2"),
				" elast=",
				oe[0].ToString("F2")
			}));
		}

		// Token: 0x060000EB RID: 235 RVA: 0x0003D49C File Offset: 0x0003B69C
		private void RestoreAims()
		{
			for (int ai2 = 0; ai2 < this._aims.Count; ai2++)
			{
				SoftFleshController.AimTarget t = this._aims[ai2];
				for (int i = 0; i < t.bones.Length; i++)
				{
					Component db = t.bones[i];
					SoftFleshController.DbAccess a = t.acc[i];
					if (!(db == null) && a != null)
					{
						if (a.force != null)
						{
							a.force.SetValue(db, t.origForce[i]);
						}
						if (a.stif != null)
						{
							a.stif.SetValue(db, t.origStif[i]);
						}
						if (a.damp != null)
						{
							a.damp.SetValue(db, t.origDamp[i]);
						}
						if (a.elast != null)
						{
							a.elast.SetValue(db, t.origElast[i]);
						}
						MonoBehaviour mb = db as MonoBehaviour;
						if (mb != null && t.wasEnabled != null && i < t.wasEnabled.Length && t.driving)
						{
							mb.enabled = t.wasEnabled[i];
						}
					}
				}
				if (t.lastFirm != 0f)
				{
					SoftFleshController.SetParticleFirm(t, 0f);
					t.lastFirm = 0f;
				}
				t.driving = false;
				t.blend = 0f;
			}
		}

		// Token: 0x060000EC RID: 236 RVA: 0x0003D62A File Offset: 0x0003B82A
		private static Vector3 RestTip(SoftFleshController.AimTarget t, int c)
		{
			return SoftFleshController.RestChain(t, c, null);
		}

		// Token: 0x060000ED RID: 237 RVA: 0x0003D634 File Offset: 0x0003B834
		private static Vector3 RestChain(SoftFleshController.AimTarget t, int c, Vector3[] into)
		{
			Transform[] ch = t.chain[c];
			Quaternion[] rr = t.restRot[c];
			Transform p0 = ch[0];
			Quaternion rot = ((p0.parent != null) ? p0.parent.rotation : Quaternion.identity) * rr[0];
			Vector3 pos = p0.position;
			if (into != null && into.Length != 0)
			{
				into[0] = pos;
			}
			int b = 0;
			while (b + 1 < ch.Length)
			{
				Transform nx = ch[b + 1];
				if (nx == null)
				{
					break;
				}
				Vector3 lp = nx.localPosition;
				Vector3 sc = ch[b].lossyScale;
				pos += rot * new Vector3(lp.x * sc.x, lp.y * sc.y, lp.z * sc.z);
				rot *= rr[b + 1];
				if (into != null && b + 1 < into.Length)
				{
					into[b + 1] = pos;
				}
				b++;
			}
			return pos;
		}

		// Token: 0x060000EE RID: 238 RVA: 0x0003D74B File Offset: 0x0003B94B
		private static void PublishAimStats()
		{
			SoftFleshController.AimGripped = SoftFleshController._aimGripped;
			SoftFleshController.AimJoints = SoftFleshController._aimJoints;
			SoftFleshController.AimMaxDeg = SoftFleshController._aimMaxDeg;
		}

		// Token: 0x060000EF RID: 239 RVA: 0x0003D76C File Offset: 0x0003B96C
		private static void PushDbParams(SoftFleshController.AimTarget t)
		{
			for (int i = 0; i < t.bones.Length; i++)
			{
				SoftFleshController.DbAccess a = t.acc[i];
				if (!(t.bones[i] == null) && a != null && !(a.update == null))
				{
					try
					{
						a.update.Invoke(t.bones[i], null);
					}
					catch
					{
					}
				}
			}
		}

		// Token: 0x060000F0 RID: 240 RVA: 0x0003D7E4 File Offset: 0x0003B9E4
		private void DriveItemAim()
		{
			for (int q = 0; q < this._aims.Count; q++)
			{
				SoftFleshController.AimTarget ta = this._aims[q];
				if (ta != null && ta.bones != null)
				{
					for (int i2 = 0; i2 < ta.bones.Length; i2++)
					{
						MonoBehaviour mb0 = ta.bones[i2] as MonoBehaviour;
						if (mb0 != null && !mb0.enabled)
						{
							mb0.enabled = true;
						}
					}
					if (ta.railHeld)
					{
						if (ta.root != null)
						{
							ta.root.position = ta.userPos;
						}
						ta.railHeld = false;
					}
				}
			}
		}

		// Token: 0x17000008 RID: 8
		// (get) Token: 0x060000F1 RID: 241 RVA: 0x0003D895 File Offset: 0x0003BA95
		public int ItemColliderCount
		{
			get
			{
				return this._itemDefs.Count;
			}
		}

		// Token: 0x060000F2 RID: 242 RVA: 0x0003D8A4 File Offset: 0x0003BAA4
		public void MonitorGuideTick()
		{
			if (!this.monitorGuide)
			{
				this.guideLive = "";
				return;
			}
			if (Time.unscaledTime < this._monNext)
			{
				return;
			}
			this._monNext = Time.unscaledTime + 0.35f;
			global::Studio.Studio st = Singleton<global::Studio.Studio>.Instance;
			Transform g = null;
			string gname = "";
			if (st != null && st.treeNodeCtrl != null && st.treeNodeCtrl.selectNodes != null)
			{
				foreach (TreeNodeObject node in st.treeNodeCtrl.selectNodes)
				{
					ObjectCtrlInfo oci;
					if (!(node == null) && st.dicInfo.TryGetValue(node, out oci))
					{
						OCIItem item = oci as OCIItem;
						if (item != null && item.objectItem != null)
						{
							g = item.objectItem.transform;
							gname = g.name;
							break;
						}
					}
				}
			}
			if (g == null)
			{
				this.guideLive = "monitor: select a studio ITEM to track";
				return;
			}
			StringBuilder sb = new StringBuilder();
			sb.Append("[").Append(gname).Append("]")
				.Append('\n');
			sb.Append("world  pos=").Append(g.position.ToString("F3")).Append("  up=")
				.Append(g.up.ToString("F2"))
				.Append('\n');
			foreach (SoftFleshController.Body body in this._bodies)
			{
				if (body.bonesArr != null && !(body.workMesh == null))
				{
					int ix = -1;
					for (int b = 0; b < body.bonesArr.Length; b++)
					{
						if (body.bonesArr[b] != null && body.bonesArr[b].name.ToLower().Contains("waist02"))
						{
							ix = b;
							break;
						}
					}
					if (ix < 0)
					{
						ix = 0;
					}
					Transform rb = body.bonesArr[ix];
					Matrix4x4[] bps = body.workMesh.bindposes;
					if (ix < bps.Length)
					{
						Matrix4x4 inv = bps[ix].inverse;
						Vector3 bp = inv.MultiplyPoint3x4(rb.InverseTransformPoint(g.position));
						Vector3 bd = inv.MultiplyVector(rb.InverseTransformDirection(g.up)).normalized;
						sb.Append("bind   pos=").Append(bp.ToString("F3")).Append("  dir=")
							.Append(bd.ToString("F2"))
							.Append('\n');
						if (body.lastBakeDirB != Vector3.zero)
						{
							Vector3 rel = bp - body.lastBakeEntryB;
							float t = Vector3.Dot(rel, body.lastBakeDirB);
							float r = (rel - body.lastBakeDirB * t).magnitude;
							sb.Append("bakeAx t=").Append((t * 1000f).ToString("F0")).Append("mm in, r=")
								.Append((r * 1000f).ToString("F0"))
								.Append("mm off-axis")
								.Append('\n');
						}
						if (body.smr == null)
						{
							sb.Append("HERE   no renderer").Append('\n');
						}
						else if (body.bakeV == null || body.bakeV.Count != body.baseVerts.Length)
						{
							sb.Append("HERE   no baked mesh yet (").Append((body.bakeV == null) ? 0 : body.bakeV.Count).Append(" of ")
								.Append(body.baseVerts.Length)
								.Append(") - nudge the item so the solve runs a frame")
								.Append('\n');
						}
						else
						{
							Matrix4x4 l2wM = SoftFleshController.BakeL2W(body.smr);
							List<int> near = new List<int>(64);
							for (int i = 0; i < body.bakeV.Count; i++)
							{
								if ((l2wM.MultiplyPoint3x4(body.bakeV[i]) - g.position).sqrMagnitude < 0.00062500004f)
								{
									near.Add(i);
								}
							}
							sb.Append("HERE   ").Append(near.Count).Append(" verts within 25mm of the ball")
								.Append('\n');
							if (near.Count > 0)
							{
								Dictionary<string, int> byBone = new Dictionary<string, int>();
								Dictionary<string, float> moveB = new Dictionary<string, float>();
								float worst = 0f;
								int worstI = -1;
								for (int q = 0; q < near.Count; q++)
								{
									int j = near[q];
									float mv = ((body.pushB != null && j < body.pushB.Length) ? body.pushB[j].magnitude : 0f);
									string bn = SoftFleshController.BoneAt(body, j);
									int c0;
									byBone.TryGetValue(bn, out c0);
									byBone[bn] = c0 + 1;
									float m0;
									moveB.TryGetValue(bn, out m0);
									if (mv > m0)
									{
										moveB[bn] = mv;
									}
									if (mv > worst)
									{
										worst = mv;
										worstI = j;
									}
								}
								foreach (KeyValuePair<string, int> kv in byBone)
								{
									sb.Append("       ").Append(kv.Key).Append(" x")
										.Append(kv.Value)
										.Append("  field pushes up to ")
										.Append((moveB[kv.Key] * 1000f).ToString("F1"))
										.Append("mm")
										.Append('\n');
								}
								if (worstI >= 0)
								{
									float pb = ((body.pushB != null && worstI < body.pushB.Length) ? body.pushB[worstI].magnitude : 0f);
									sb.Append("       worst ").Append((worst * 1000f).ToString("F1")).Append("mm of field push on ")
										.Append(SoftFleshController.BoneAt(body, worstI))
										.Append("  (pushB ")
										.Append((pb * 1000f).ToString("F1"))
										.Append("mm)")
										.Append("  vagW=")
										.Append(((body.vagW != null) ? body.vagW[worstI] : 0f).ToString("F2"))
										.Append(" kokW=")
										.Append(((body.kokW != null) ? body.kokW[worstI] : 0f).ToString("F2"))
										.Append(" anaW=")
										.Append(((body.anaW != null) ? body.anaW[worstI] : 0f).ToString("F2"))
										.Append(" petW=")
										.Append(((body.petW != null) ? body.petW[worstI] : 0f).ToString("F2"))
										.Append('\n');
								}
							}
						}
						int ni = -1;
						float nd = 1E+09f;
						for (int k = 0; k < body.baseVerts.Length; k++)
						{
							float d2 = (body.baseVerts[k] - bp).sqrMagnitude;
							if (d2 < nd)
							{
								nd = d2;
								ni = k;
							}
						}
						if (ni >= 0)
						{
							sb.Append("nearest vert ").Append((Mathf.Sqrt(nd) * 1000f).ToString("F0")).Append("mm  vagW=")
								.Append(((body.vagW != null) ? body.vagW[ni] : 0f).ToString("F2"))
								.Append(" kokW=")
								.Append(((body.kokW != null) ? body.kokW[ni] : 0f).ToString("F2"))
								.Append(" anaW=")
								.Append(((body.anaW != null) ? body.anaW[ni] : 0f).ToString("F2"))
								.Append(" petW=")
								.Append(((body.petW != null) ? body.petW[ni] : 0f).ToString("F2"))
								.Append("  bone=")
								.Append(SoftFleshController.BoneAt(body, ni))
								.Append('\n');
							break;
						}
						break;
					}
				}
			}
			this.guideLive = sb.ToString().TrimEnd(Array.Empty<char>());
			try
			{
				string dd = Path.Combine(Paths.GameRootPath, "BepInEx/SoftFlesh_diag");
				Directory.CreateDirectory(dd);
				File.WriteAllText(Path.Combine(dd, "guide_live.txt"), this.guideLive);
			}
			catch
			{
			}
		}

		// Token: 0x17000009 RID: 9
		// (get) Token: 0x060000F3 RID: 243 RVA: 0x0003E26C File Offset: 0x0003C46C
		public bool GuideArmed
		{
			get
			{
				for (int b = 0; b < this._bodies.Count; b++)
				{
					if (this._bodies[b].guideSet)
					{
						return true;
					}
				}
				return false;
			}
		}

		// Token: 0x060000F4 RID: 244 RVA: 0x0003E2A8 File Offset: 0x0003C4A8
		public void CaptureCanalGuide()
		{
			global::Studio.Studio st = Singleton<global::Studio.Studio>.Instance;
			if (st == null || st.treeNodeCtrl == null || st.treeNodeCtrl.selectNodes == null)
			{
				this.guideInfo = "select the guide item first";
				return;
			}
			Transform g = null;
			foreach (TreeNodeObject node in st.treeNodeCtrl.selectNodes)
			{
				ObjectCtrlInfo oci;
				if (!(node == null) && st.dicInfo.TryGetValue(node, out oci))
				{
					OCIItem item = oci as OCIItem;
					if (item != null && item.objectItem != null)
					{
						g = item.objectItem.transform;
						break;
					}
				}
			}
			if (g == null)
			{
				this.guideInfo = "no studio ITEM selected";
				return;
			}
			foreach (SoftFleshController.Body body in this._bodies)
			{
				if (body.bonesArr != null && !(body.workMesh == null))
				{
					int ix = -1;
					for (int b = 0; b < body.bonesArr.Length; b++)
					{
						if (body.bonesArr[b] != null && body.bonesArr[b].name.ToLower().Contains("waist02"))
						{
							ix = b;
							break;
						}
					}
					if (ix < 0)
					{
						ix = 0;
					}
					Transform rb = body.bonesArr[ix];
					body.guideBoneIx = ix;
					body.guideLocalPos = rb.InverseTransformPoint(g.position);
					body.guideLocalDir = rb.InverseTransformDirection(g.up);
					body.guideSet = true;
					Matrix4x4[] bps = body.workMesh.bindposes;
					Vector3 bd = bps[ix].inverse.MultiplyVector(body.guideLocalDir).normalized;
					Vector3 bp = bps[ix].inverse.MultiplyPoint3x4(body.guideLocalPos);
					this.guideInfo = string.Concat(new string[]
					{
						"guide captured: pos=",
						bp.ToString("F3"),
						" dir=",
						bd.ToString("F2"),
						"  (re-bake to apply)"
					});
					SoftFleshPlugin.Log(string.Concat(new string[]
					{
						"[SF] canal guide captured from '",
						g.name,
						"': pos=",
						bp.ToString("F3"),
						" dir=",
						bd.ToString("F2")
					}));
				}
			}
		}

		// Token: 0x060000F5 RID: 245 RVA: 0x0003E568 File Offset: 0x0003C768
		public void ClearCanalGuide()
		{
			foreach (SoftFleshController.Body body in this._bodies)
			{
				body.guideSet = false;
			}
			this.guideInfo = "guide cleared (re-bake to revert to auto axis)";
		}

		// Token: 0x060000F6 RID: 246 RVA: 0x0003E5C8 File Offset: 0x0003C7C8
		public int AddSelectedItems()
		{
			global::Studio.Studio st = Singleton<global::Studio.Studio>.Instance;
			if (st == null || st.treeNodeCtrl == null)
			{
				return 0;
			}
			TreeNodeObject[] nodes = st.treeNodeCtrl.selectNodes;
			if (nodes == null)
			{
				return 0;
			}
			int added = 0;
			foreach (TreeNodeObject node in nodes)
			{
				ObjectCtrlInfo oci;
				if (!(node == null) && st.dicInfo.TryGetValue(node, out oci) && this.AddItemCollider(oci as OCIItem, this.addAsInsert))
				{
					added++;
				}
			}
			return added;
		}

		// Token: 0x060000F7 RID: 247 RVA: 0x0003E658 File Offset: 0x0003C858
		public List<KeyValuePair<Transform, bool>> ItemColliders()
		{
			List<KeyValuePair<Transform, bool>> outL = new List<KeyValuePair<Transform, bool>>();
			for (int q = 0; q < this._itemDefs.Count; q++)
			{
				SoftFleshController.Presser d = this._itemDefs[q];
				if (!(d.owner == null))
				{
					bool seen = false;
					for (int i = 0; i < outL.Count; i++)
					{
						if (outL[i].Key == d.owner)
						{
							seen = true;
							break;
						}
					}
					if (!seen)
					{
						outL.Add(new KeyValuePair<Transform, bool>(d.owner, d.insertMode));
					}
				}
			}
			return outL;
		}

		// Token: 0x060000F8 RID: 248 RVA: 0x0003E6F0 File Offset: 0x0003C8F0
		public bool AddItemCollider(OCIItem item, bool insert)
		{
			if (item == null || item.objectItem == null)
			{
				return false;
			}
			bool prevIns = this.addAsInsert;
			this.addAsInsert = insert;
			bool flag;
			try
			{
				flag = this.AddItemColliderCore(item);
			}
			finally
			{
				this.addAsInsert = prevIns;
			}
			return flag;
		}

		// Token: 0x060000F9 RID: 249 RVA: 0x0003E744 File Offset: 0x0003C944
		private bool AddItemColliderCore(OCIItem item)
		{
			Transform tr = item.objectItem.transform;
			bool dup = false;
			for (int q = 0; q < this._itemDefs.Count; q++)
			{
				if (this._itemDefs[q].owner == tr)
				{
					dup = true;
					break;
				}
			}
			if (dup)
			{
				return false;
			}
			this.RegisterAim(tr, item.objectItem);
			this.AttachCanalToItem(item.objectItem);
			if (this.TryAddBoneChain(tr, item.objectItem))
			{
				return true;
			}
			if (this.TryAddMeshChain(tr, item.objectItem))
			{
				return true;
			}
			Renderer[] rends = item.objectItem.GetComponentsInChildren<Renderer>();
			if (rends == null || rends.Length == 0)
			{
				return false;
			}
			bool first = true;
			Vector3 lmin2 = Vector3.zero;
			Vector3 lmax2 = Vector3.zero;
			foreach (Renderer rend in rends)
			{
				MeshFilter mf = rend.GetComponent<MeshFilter>();
				Mesh ms = ((mf != null) ? mf.sharedMesh : null);
				Bounds lb2 = ((ms != null) ? ms.bounds : new Bounds(Vector3.zero, Vector3.one * 0.05f));
				for (int c8 = 0; c8 < 8; c8++)
				{
					Vector3 corner = lb2.center + Vector3.Scale(lb2.extents, new Vector3(((c8 & 1) == 0) ? (-1f) : 1f, ((c8 & 2) == 0) ? (-1f) : 1f, ((c8 & 4) == 0) ? (-1f) : 1f));
					Vector3 lc = tr.InverseTransformPoint(rend.transform.TransformPoint(corner));
					if (first)
					{
						lmax2 = (lmin2 = lc);
						first = false;
					}
					else
					{
						if (lc.x < lmin2.x)
						{
							lmin2.x = lc.x;
						}
						if (lc.x > lmax2.x)
						{
							lmax2.x = lc.x;
						}
						if (lc.y < lmin2.y)
						{
							lmin2.y = lc.y;
						}
						if (lc.y > lmax2.y)
						{
							lmax2.y = lc.y;
						}
						if (lc.z < lmin2.z)
						{
							lmin2.z = lc.z;
						}
						if (lc.z > lmax2.z)
						{
							lmax2.z = lc.z;
						}
					}
				}
			}
			if (first)
			{
				return false;
			}
			Vector3 ctr = (lmin2 + lmax2) * 0.5f;
			Vector3 e = (lmax2 - lmin2) * 0.5f;
			int ax = ((e.x >= e.y && e.x >= e.z) ? 0 : ((e.y >= e.z) ? 1 : 2));
			Vector3 dir = ((ax == 0) ? Vector3.right : ((ax == 1) ? Vector3.up : Vector3.forward));
			float half = ((ax == 0) ? e.x : ((ax == 1) ? e.y : e.z));
			float rad = 0.5f * (e.x + e.y + e.z - half);
			float sc = (tr.lossyScale.x + tr.lossyScale.y + tr.lossyScale.z) * 0.33333334f;
			if (sc < 0.01f)
			{
				sc = 0.01f;
			}
			if (rad * sc < 0.004f)
			{
				rad = 0.004f / sc;
			}
			float seg = half - rad;
			if (seg < half * 0.5f)
			{
				seg = half * 0.5f;
			}
			SoftFleshController.Presser def = new SoftFleshController.Presser
			{
				a = tr,
				b = tr,
				item = true,
				la = ctr - dir * seg,
				lb = ctr + dir * seg,
				lrad = rad,
				lradB = rad,
				tipA = true,
				tipB = true,
				lradMax = rad,
				insertMode = this.addAsInsert,
				bulgeW = 1f,
				owner = tr
			};
			this._itemDefs.Add(def);
			this._pressers.Add(def);
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] item collider: ",
				tr.name,
				" r=",
				(rad * sc * 1000f).ToString("F0"),
				"mm (x Item Radius slider)"
			}));
			this.AttachDynColliders(tr);
			return true;
		}

		// Token: 0x060000FA RID: 250 RVA: 0x0003EBE8 File Offset: 0x0003CDE8
		private bool TryAddBoneChain(Transform tr, GameObject go)
		{
			SkinnedMeshRenderer[] sks = go.GetComponentsInChildren<SkinnedMeshRenderer>();
			if (sks == null || sks.Length == 0)
			{
				return false;
			}
			List<SoftFleshController.Presser> made = new List<SoftFleshController.Presser>();
			int lobes = 0;
			foreach (SkinnedMeshRenderer sk in sks)
			{
				Mesh ms = ((sk != null) ? sk.sharedMesh : null);
				if (!(ms == null) && ms.isReadable)
				{
					Transform[] bones = sk.bones;
					Matrix4x4[] bps = ms.bindposes;
					BoneWeight[] bw = ms.boneWeights;
					Vector3[] vs = ms.vertices;
					if (bones != null && bps != null && bw != null && vs != null && bw.Length == vs.Length && bps.Length >= bones.Length)
					{
						List<Vector3>[] buckets = new List<Vector3>[bones.Length];
						for (int i = 0; i < vs.Length; i++)
						{
							int bi;
							float wv;
							SoftFleshController.DominantOf(bw[i], out bi, out wv);
							if (bi >= 0 && bi < bones.Length && !(bones[bi] == null))
							{
								if (buckets[bi] == null)
								{
									buckets[bi] = new List<Vector3>(64);
								}
								buckets[bi].Add(bps[bi].MultiplyPoint3x4(vs[i]));
							}
						}
						for (int b = 0; b < bones.Length; b++)
						{
							if (buckets[b] != null && buckets[b].Count >= 12 && SoftFleshController.FitLobe(bones[b], buckets[b], made))
							{
								lobes++;
							}
						}
					}
				}
			}
			if (made.Count == 0)
			{
				return false;
			}
			this.FinishItemChain(tr, made, lobes);
			return true;
		}

		// Token: 0x060000FB RID: 251 RVA: 0x0003ED88 File Offset: 0x0003CF88
		private static bool FitLobe(Transform bone, List<Vector3> pts, List<SoftFleshController.Presser> made)
		{
			Vector3 c = Vector3.zero;
			for (int i = 0; i < pts.Count; i++)
			{
				c += pts[i];
			}
			c /= (float)pts.Count;
			Vector3 ax = Vector3.zero;
			for (int j = 0; j < bone.childCount; j++)
			{
				Vector3 cp = bone.GetChild(j).localPosition;
				if (cp.sqrMagnitude > ax.sqrMagnitude)
				{
					ax = cp;
				}
			}
			bool fromBone = ax.sqrMagnitude > 1E-10f;
			if (fromBone)
			{
				ax.Normalize();
			}
			else
			{
				float sxx = 0f;
				float sxy = 0f;
				float sxz = 0f;
				float syy = 0f;
				float syz = 0f;
				float szz = 0f;
				for (int k = 0; k < pts.Count; k++)
				{
					Vector3 d = pts[k] - c;
					sxx += d.x * d.x;
					sxy += d.x * d.y;
					sxz += d.x * d.z;
					syy += d.y * d.y;
					syz += d.y * d.z;
					szz += d.z * d.z;
				}
				ax = Vector3.right;
				if (syy >= sxx && syy >= szz)
				{
					ax = Vector3.up;
				}
				else if (szz >= sxx && szz >= syy)
				{
					ax = Vector3.forward;
				}
				for (int it = 0; it < 32; it++)
				{
					ax..ctor(sxx * ax.x + sxy * ax.y + sxz * ax.z, sxy * ax.x + syy * ax.y + syz * ax.z, sxz * ax.x + syz * ax.y + szz * ax.z);
					float l = ax.magnitude;
					if (l < 1E-14f)
					{
						return false;
					}
					ax /= l;
				}
			}
			float lo = float.MaxValue;
			float hi = float.MinValue;
			List<float> lat = new List<float>(pts.Count);
			for (int m = 0; m < pts.Count; m++)
			{
				Vector3 d2 = pts[m] - c;
				float t = Vector3.Dot(d2, ax);
				if (t < lo)
				{
					lo = t;
				}
				if (t > hi)
				{
					hi = t;
				}
				lat.Add((d2 - ax * t).magnitude);
			}
			float span = hi - lo;
			lat.Sort();
			SoftFleshController.Prof prof = SoftFleshController.BuildProfile(pts, c, ax, lo, span, lat[Mathf.Clamp((int)((float)lat.Count * 0.9f), 0, lat.Count - 1)]);
			float rad = lat[Mathf.Clamp((int)((float)lat.Count * 0.9f), 0, lat.Count - 1)];
			if (rad < 1E-06f)
			{
				return false;
			}
			if (span < rad * 1.2f)
			{
				made.Add(new SoftFleshController.Presser
				{
					a = bone,
					b = bone,
					item = true,
					la = c,
					lb = c,
					lrad = rad,
					lradB = rad,
					prof = prof
				});
				return true;
			}
			int K = Mathf.Clamp(Mathf.RoundToInt(span / (rad * 2f)), 1, 4);
			int NB = K + 1;
			Vector3[] cen = new Vector3[NB];
			int[] cnt = new int[NB];
			List<float>[] sl = new List<float>[NB];
			for (int b = 0; b < NB; b++)
			{
				sl[b] = new List<float>();
			}
			for (int n = 0; n < pts.Count; n++)
			{
				int b2 = Mathf.Clamp((int)((Vector3.Dot(pts[n] - c, ax) - lo) / span * (float)NB), 0, NB - 1);
				cen[b2] += pts[n];
				cnt[b2]++;
			}
			for (int b3 = 0; b3 < NB; b3++)
			{
				if (cnt[b3] > 0)
				{
					cen[b3] /= (float)cnt[b3];
				}
			}
			for (int i2 = 0; i2 < pts.Count; i2++)
			{
				int b4 = Mathf.Clamp((int)((Vector3.Dot(pts[i2] - c, ax) - lo) / span * (float)NB), 0, NB - 1);
				if (cnt[b4] != 0)
				{
					Vector3 d3 = pts[i2] - cen[b4];
					sl[b4].Add((d3 - ax * Vector3.Dot(d3, ax)).magnitude);
				}
			}
			List<Vector3> nPos = new List<Vector3>(NB);
			List<float> nRad = new List<float>(NB);
			for (int b5 = 0; b5 < NB; b5++)
			{
				if (cnt[b5] >= 3 && sl[b5].Count != 0)
				{
					sl[b5].Sort();
					nPos.Add(cen[b5]);
					nRad.Add(sl[b5][Mathf.Clamp((int)((float)sl[b5].Count * 0.9f), 0, sl[b5].Count - 1)]);
				}
			}
			if (nPos.Count < 2)
			{
				made.Add(new SoftFleshController.Presser
				{
					a = bone,
					b = bone,
					item = true,
					la = c,
					lb = c,
					lrad = rad,
					lradB = rad,
					prof = prof
				});
				return true;
			}
			nPos[0] = nPos[0] + ax * (lo - Vector3.Dot(nPos[0] - c, ax));
			int last = nPos.Count - 1;
			nPos[last] += ax * (hi - Vector3.Dot(nPos[last] - c, ax));
			int b6 = 0;
			while (b6 + 1 < nPos.Count)
			{
				made.Add(new SoftFleshController.Presser
				{
					a = bone,
					b = bone,
					item = true,
					prof = prof,
					la = nPos[b6],
					lb = nPos[b6 + 1],
					lrad = nRad[b6],
					lradB = nRad[b6 + 1]
				});
				b6++;
			}
			return true;
		}

		// Token: 0x060000FC RID: 252 RVA: 0x0003F440 File Offset: 0x0003D640
		private bool TryAddMeshChain(Transform tr, GameObject go)
		{
			List<Vector3> pts = new List<Vector3>(2048);
			foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
			{
				Mesh ms = ((mf != null) ? mf.sharedMesh : null);
				if (!(ms == null) && ms.isReadable)
				{
					Vector3[] vs = ms.vertices;
					int stride = ((vs.Length > 2000) ? (vs.Length / 2000) : 1);
					for (int i = 0; i < vs.Length; i += stride)
					{
						pts.Add(tr.InverseTransformPoint(mf.transform.TransformPoint(vs[i])));
					}
				}
			}
			foreach (SkinnedMeshRenderer sk in go.GetComponentsInChildren<SkinnedMeshRenderer>())
			{
				if (!(sk == null) && !(sk.sharedMesh == null))
				{
					Mesh tmp = new Mesh();
					sk.BakeMesh(tmp);
					Vector3[] vs2 = tmp.vertices;
					int stride2 = ((vs2.Length > 2000) ? (vs2.Length / 2000) : 1);
					Vector3 skPos = sk.transform.position;
					Quaternion skRot = sk.transform.rotation;
					for (int j = 0; j < vs2.Length; j += stride2)
					{
						pts.Add(tr.InverseTransformPoint(skPos + skRot * vs2[j]));
					}
					Object.Destroy(tmp);
				}
			}
			if (pts.Count < 32)
			{
				SoftFleshPlugin.Log(string.Concat(new string[]
				{
					"[SF] ",
					tr.name,
					": no CPU-readable mesh (",
					pts.Count.ToString(),
					" pts) → bounds-capsule fallback"
				}));
				return false;
			}
			Vector3 mn = pts[0];
			Vector3 mx = pts[0];
			for (int k = 1; k < pts.Count; k++)
			{
				Vector3 v = pts[k];
				if (v.x < mn.x)
				{
					mn.x = v.x;
				}
				if (v.x > mx.x)
				{
					mx.x = v.x;
				}
				if (v.y < mn.y)
				{
					mn.y = v.y;
				}
				if (v.y > mx.y)
				{
					mx.y = v.y;
				}
				if (v.z < mn.z)
				{
					mn.z = v.z;
				}
				if (v.z > mx.z)
				{
					mx.z = v.z;
				}
			}
			Vector3 ctrP = Vector3.zero;
			for (int l = 0; l < pts.Count; l++)
			{
				ctrP += pts[l];
			}
			ctrP /= (float)pts.Count;
			float sxx = 0f;
			float sxy = 0f;
			float sxz = 0f;
			float syy = 0f;
			float syz = 0f;
			float szz = 0f;
			for (int m = 0; m < pts.Count; m++)
			{
				Vector3 d = pts[m] - ctrP;
				sxx += d.x * d.x;
				sxy += d.x * d.y;
				sxz += d.x * d.z;
				syy += d.y * d.y;
				syz += d.y * d.z;
				szz += d.z * d.z;
			}
			Vector3 pcAx = (mx - mn).normalized;
			for (int it = 0; it < 40; it++)
			{
				pcAx..ctor(sxx * pcAx.x + sxy * pcAx.y + sxz * pcAx.z, sxy * pcAx.x + syy * pcAx.y + syz * pcAx.z, sxz * pcAx.x + syz * pcAx.y + szz * pcAx.z);
				float m2 = pcAx.magnitude;
				if (m2 < 1E-12f)
				{
					return false;
				}
				pcAx /= m2;
			}
			float lo = float.MaxValue;
			float hiP = float.MinValue;
			for (int n = 0; n < pts.Count; n++)
			{
				float t = Vector3.Dot(pts[n], pcAx);
				if (t < lo)
				{
					lo = t;
				}
				if (t > hiP)
				{
					hiP = t;
				}
			}
			float span = hiP - lo;
			if (span < 0.0001f)
			{
				return false;
			}
			int[] cnt = new int[18];
			Vector3[] cen = new Vector3[18];
			float[] rad2 = new float[18];
			List<float>[] slice = new List<float>[18];
			for (int s2 = 0; s2 < 18; s2++)
			{
				slice[s2] = new List<float>();
			}
			for (int i2 = 0; i2 < pts.Count; i2++)
			{
				float t2 = (Vector3.Dot(pts[i2], pcAx) - lo) / span;
				int sIdx = (int)(t2 * 18f);
				if (sIdx >= 18)
				{
					sIdx = 17;
				}
				if (sIdx < 0)
				{
					sIdx = 0;
				}
				cen[sIdx] += pts[i2];
				cnt[sIdx]++;
			}
			for (int s3 = 0; s3 < 18; s3++)
			{
				if (cnt[s3] > 0)
				{
					cen[s3] /= (float)cnt[s3];
				}
			}
			for (int i3 = 0; i3 < pts.Count; i3++)
			{
				float t3 = (Vector3.Dot(pts[i3], pcAx) - lo) / span;
				int sIdx2 = (int)(t3 * 18f);
				if (sIdx2 >= 18)
				{
					sIdx2 = 17;
				}
				if (sIdx2 < 0)
				{
					sIdx2 = 0;
				}
				Vector3 d2 = pts[i3] - cen[sIdx2];
				d2 -= pcAx * Vector3.Dot(d2, pcAx);
				slice[sIdx2].Add(d2.magnitude);
			}
			for (int s4 = 0; s4 < 18; s4++)
			{
				List<float> L = slice[s4];
				if (L.Count == 0)
				{
					rad2[s4] = 0f;
					cnt[s4] = 0;
				}
				else
				{
					L.Sort();
					rad2[s4] = L[Mathf.Clamp((int)((float)L.Count * 0.9f), 0, L.Count - 1)] * (float)cnt[s4];
				}
			}
			List<Vector3> nPos = new List<Vector3>(20);
			List<float> nRad = new List<float>(20);
			for (int s5 = 0; s5 < 18; s5++)
			{
				if (cnt[s5] >= 3)
				{
					nPos.Add(cen[s5]);
					nRad.Add(rad2[s5] / (float)cnt[s5]);
				}
			}
			if (nPos.Count < 2)
			{
				return false;
			}
			Vector3 axU = pcAx;
			float aF = Vector3.Dot(nPos[0], axU);
			Vector3 endA = nPos[0] + axU * (lo - aF);
			nPos.Insert(0, endA);
			nRad.Insert(0, nRad[0] * 0.65f);
			float aL = Vector3.Dot(nPos[nPos.Count - 1], axU);
			Vector3 endB = nPos[nPos.Count - 1] + axU * (lo + span - aL);
			nPos.Add(endB);
			nRad.Add(nRad[nRad.Count - 1] * 0.65f);
			float sc2 = (tr.lossyScale.x + tr.lossyScale.y + tr.lossyScale.z) * 0.33333334f;
			if (sc2 < 0.01f)
			{
				sc2 = 0.01f;
			}
			if (nRad.Count >= 3)
			{
				float[] sm = new float[nRad.Count];
				for (int s6 = 0; s6 < nRad.Count; s6++)
				{
					sm[s6] = ((s6 == 0 || s6 == nRad.Count - 1) ? nRad[s6] : ((nRad[s6 - 1] + 2f * nRad[s6] + nRad[s6 + 1]) * 0.25f));
				}
				for (int s7 = 0; s7 < nRad.Count; s7++)
				{
					nRad[s7] = sm[s7];
				}
			}
			float chainMax = 0f;
			for (int s8 = 0; s8 < nRad.Count; s8++)
			{
				if (nRad[s8] > chainMax)
				{
					chainMax = nRad[s8];
				}
			}
			bool tipIsA = nRad[0] <= nRad[nRad.Count - 1];
			int segTotal = nPos.Count - 1;
			int madeC = 0;
			float rMin = 1E+09f;
			float rMax = 0f;
			int s9 = 0;
			while (s9 + 1 < nPos.Count)
			{
				float rA = nRad[s9];
				float rB = nRad[s9 + 1];
				if (rA * sc2 < 0.003f)
				{
					rA = 0.003f / sc2;
				}
				if (rB * sc2 < 0.003f)
				{
					rB = 0.003f / sc2;
				}
				int distTip = (tipIsA ? s9 : (segTotal - 1 - s9));
				float bwSeg = ((distTip < 5) ? 1f : ((distTip == 5) ? 0.5f : 0f));
				float tipFrac = ((segTotal > 1) ? ((float)distTip / (float)(segTotal - 1)) : 0f);
				SoftFleshController.Presser def = new SoftFleshController.Presser
				{
					a = tr,
					b = tr,
					item = true,
					la = nPos[s9],
					lb = nPos[s9 + 1],
					lrad = rA,
					lradB = rB,
					tipA = (s9 == 0),
					tipB = (s9 + 2 == nPos.Count),
					lradMax = chainMax,
					insertMode = this.addAsInsert,
					bulgeW = bwSeg,
					tipF = tipFrac,
					owner = tr
				};
				this._itemDefs.Add(def);
				this._pressers.Add(def);
				madeC++;
				float rw = 0.5f * (rA + rB) * sc2;
				if (rw < rMin)
				{
					rMin = rw;
				}
				if (rw > rMax)
				{
					rMax = rw;
				}
				s9++;
			}
			if (madeC == 0)
			{
				return false;
			}
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] item chain: ",
				tr.name,
				" x",
				madeC.ToString(),
				" capsules r=",
				(rMin * 1000f).ToString("F0"),
				"-",
				(rMax * 1000f).ToString("F0"),
				"mm"
			}));
			this.AttachDynColliders(tr);
			return true;
		}

		// Token: 0x060000FD RID: 253 RVA: 0x0003FF68 File Offset: 0x0003E168
		private void AttachDynColliders(Transform itemTr)
		{
			if (!this.boneOpening)
			{
				return;
			}
			for (int q = 0; q < this._itemDefs.Count; q++)
			{
				SoftFleshController.Presser d = this._itemDefs[q];
				if (!(d.a != itemTr) && d.item && d.insertMode)
				{
					Vector3 seg = d.lb - d.la;
					float len = seg.magnitude;
					GameObject go = new GameObject("SF_dynCol");
					go.transform.SetParent(itemTr, false);
					go.transform.localPosition = (d.la + d.lb) * 0.5f;
					go.transform.localRotation = ((len > 1E-06f) ? Quaternion.FromToRotation(Vector3.up, seg / len) : Quaternion.identity);
					DynamicBoneCollider col = go.AddComponent<DynamicBoneCollider>();
					col.m_Direction = 1;
					col.m_Bound = 0;
					col.m_Center = Vector3.zero;
					float rBase = 0.5f * (d.lrad + d.lradB);
					col.m_Radius = rBase * this.itemRadScale;
					col.m_Height = len + rBase * 2f;
					this._dynCols.Add(col);
					this._dynColBaseR.Add(rBase);
					for (int b = 0; b < this._bodies.Count; b++)
					{
						SoftFleshController.Body body = this._bodies[b];
						for (int i = 0; i < body.vagDyn.Count; i++)
						{
							DynamicBone db = body.vagDyn[i];
							if (!(db == null))
							{
								if (db.m_Colliders == null)
								{
									db.m_Colliders = new List<DynamicBoneCollider>();
								}
								if (!db.m_Colliders.Contains(col))
								{
									db.m_Colliders.Add(col);
								}
							}
						}
					}
				}
			}
			SoftFleshPlugin.Log("[SF] dyn colliders on " + itemTr.name + ": total " + this._dynCols.Count.ToString());
		}

		// Token: 0x060000FE RID: 254 RVA: 0x0004018C File Offset: 0x0003E38C
		private void DetachDynColliders()
		{
			for (int b = 0; b < this._bodies.Count; b++)
			{
				SoftFleshController.Body body = this._bodies[b];
				for (int i = 0; i < body.vagDyn.Count; i++)
				{
					DynamicBone db = body.vagDyn[i];
					if (!(db == null) && db.m_Colliders != null)
					{
						for (int q = 0; q < this._dynCols.Count; q++)
						{
							db.m_Colliders.Remove(this._dynCols[q]);
						}
					}
				}
			}
			for (int q2 = 0; q2 < this._dynCols.Count; q2++)
			{
				if (this._dynCols[q2] != null)
				{
					Object.Destroy(this._dynCols[q2].gameObject);
				}
			}
			this._dynCols.Clear();
			this._dynColBaseR.Clear();
		}

		// Token: 0x060000FF RID: 255 RVA: 0x00040284 File Offset: 0x0003E484
		private void SyncDynColliders()
		{
			for (int q = 0; q < this._dynCols.Count; q++)
			{
				DynamicBoneCollider c = this._dynCols[q];
				if (!(c == null))
				{
					float rb = this._dynColBaseR[q];
					c.m_Radius = rb * this.itemRadScale;
					c.m_Height = c.m_Height;
				}
			}
		}

		// Token: 0x06000100 RID: 256 RVA: 0x000402E4 File Offset: 0x0003E4E4
		private void EnsureSquashRecs(Transform owner)
		{
			if (owner == null || this._sqDone.Contains(owner))
			{
				return;
			}
			this._sqDone.Add(owner);
			int budget = 40000;
			int added = 0;
			Renderer[] rends = owner.GetComponentsInChildren<Renderer>(true);
			for (int i = 0; i < rends.Length; i++)
			{
				Mesh src = null;
				MeshFilter mf = null;
				SkinnedMeshRenderer smr = rends[i] as SkinnedMeshRenderer;
				if (smr != null)
				{
					src = smr.sharedMesh;
				}
				else
				{
					mf = rends[i].GetComponent<MeshFilter>();
					if (mf != null)
					{
						src = mf.sharedMesh;
					}
				}
				if (!(src == null) && src.vertexCount != 0 && !src.name.StartsWith("SF_squash_"))
				{
					if (!src.isReadable)
					{
						SoftFleshPlugin.Log("[SF] item squash: '" + rends[i].name + "' mesh is not CPU-readable - skipped");
					}
					else if (src.vertexCount > budget)
					{
						SoftFleshPlugin.Log(string.Concat(new string[]
						{
							"[SF] item squash: '",
							rends[i].name,
							"' skipped (",
							src.vertexCount.ToString(),
							" verts over the remaining ",
							budget.ToString(),
							" budget)"
						}));
					}
					else
					{
						SoftFleshController.SquashRec rec = new SoftFleshController.SquashRec();
						rec.rend = rends[i];
						rec.mf = mf;
						rec.owner = owner;
						rec.orig = src;
						rec.clone = Object.Instantiate<Mesh>(src);
						rec.clone.name = "SF_squash_" + src.name;
						rec.baseV = src.vertices;
						rec.work = (Vector3[])rec.baseV.Clone();
						if (smr != null)
						{
							rec.bones = smr.bones;
							rec.binds = src.bindposes;
							BoneWeight[] bw = src.boneWeights;
							if (rec.bones == null || rec.binds == null || bw == null || rec.binds.Length != rec.bones.Length || bw.Length != rec.baseV.Length)
							{
								Object.Destroy(rec.clone);
								goto IL_0366;
							}
							rec.domBone = new int[bw.Length];
							for (int j = 0; j < bw.Length; j++)
							{
								int di = bw[j].boneIndex0;
								float dw = bw[j].weight0;
								if (bw[j].weight1 > dw)
								{
									di = bw[j].boneIndex1;
									dw = bw[j].weight1;
								}
								if (bw[j].weight2 > dw)
								{
									di = bw[j].boneIndex2;
									dw = bw[j].weight2;
								}
								if (bw[j].weight3 > dw)
								{
									di = bw[j].boneIndex3;
								}
								rec.domBone[j] = ((di >= 0 && di < rec.bones.Length) ? di : 0);
							}
							smr.sharedMesh = rec.clone;
						}
						else
						{
							mf.sharedMesh = rec.clone;
						}
						budget -= src.vertexCount;
						added += rec.baseV.Length;
						this._squash.Add(rec);
					}
				}
				IL_0366:;
			}
			if (added > 0)
			{
				SoftFleshPlugin.Log(string.Concat(new string[]
				{
					"[SF] item squash: cloned ",
					this._squash.Count.ToString(),
					" renderer(s), ",
					added.ToString(),
					" verts under '",
					owner.name,
					"' (originals kept for restore)"
				}));
			}
		}

		// Token: 0x06000101 RID: 257 RVA: 0x000406C4 File Offset: 0x0003E8C4
		private void SquashItems(SoftFleshController.Body body, float dt)
		{
			if (this.itemSquash <= 0.001f || this._itemDefs.Count == 0)
			{
				this.RelaxSquash();
				SoftFleshController.SquashInfo = ((this.itemSquash <= 0.001f) ? "off (gain 0)" : "off (no item colliders)");
				return;
			}
			if (!body.itemInside || body.trkRadMm <= 0.1f)
			{
				this.RelaxSquash();
				this._sqPrevD = -1f;
				this._sqVel = 0f;
				SoftFleshController.SquashInfo = "idle (no item crossing the entrance)";
				return;
			}
			if (this._sqPrevD >= 0f && dt > 1E-05f)
			{
				float rawV = (body.trkDepthMm - this._sqPrevD) / dt;
				this._sqVel += (rawV - this._sqVel) * (1f - Mathf.Exp(-20f * dt));
			}
			this._sqPrevD = body.trkDepthMm;
			float velMm = ((this._sqVel > 20f) ? (this._sqVel * 0.08f) : 0f);
			if (velMm > 10f)
			{
				velMm = 10f;
			}
			Vector3 D0 = body.axisDwW;
			Vector3 E0 = body.axisEwW + D0 * body.suckOff;
			Vector3 u0 = Vector3.Cross(D0, Vector3.up);
			if (u0.sqrMagnitude < 1E-08f)
			{
				u0 = Vector3.Cross(D0, Vector3.right);
			}
			u0.Normalize();
			Vector3 v0 = Vector3.Cross(D0, u0);
			if (SoftFleshController._apR == null)
			{
				SoftFleshController._apR = new float[12];
				SoftFleshController._apN = new int[12];
				SoftFleshController._apT = new float[12];
			}
			bool haveAp = false;
			if (body.rimVerts != null && body.rimVerts.Length != 0 && body.smr != null && body.bakeV != null && body.bakeV.Count == body.baseVerts.Length)
			{
				for (int k2 = 0; k2 < 12; k2++)
				{
					SoftFleshController._apR[k2] = 0f;
					SoftFleshController._apN[k2] = 0;
				}
				Matrix4x4 l2wA = SoftFleshController.BakeL2W(body.smr);
				for (int k3 = 0; k3 < body.rimVerts.Length; k3++)
				{
					Vector3 w = l2wA.MultiplyPoint3x4(body.bakeV[body.rimVerts[k3]]) - E0;
					Vector3 rr = w - D0 * Vector3.Dot(w, D0);
					float rm = rr.magnitude;
					if (rm >= 1E-05f)
					{
						float ang = Mathf.Atan2(Vector3.Dot(rr, v0), Vector3.Dot(rr, u0));
						int bi = (int)((ang + 3.1415927f) * 1.9098593f);
						if (bi < 0)
						{
							bi = 0;
						}
						else if (bi > 11)
						{
							bi = 11;
						}
						if (SoftFleshController._apN[bi] == 0 || rm < SoftFleshController._apR[bi])
						{
							SoftFleshController._apR[bi] = rm;
						}
						SoftFleshController._apN[bi]++;
					}
				}
				int filled = 0;
				for (int k4 = 0; k4 < 12; k4++)
				{
					if (SoftFleshController._apN[k4] > 0)
					{
						filled++;
					}
				}
				if (filled >= 4)
				{
					for (int k5 = 0; k5 < 12; k5++)
					{
						if (SoftFleshController._apN[k5] == 0)
						{
							for (int d2 = 1; d2 < 12; d2++)
							{
								int a2 = (k5 + d2) % 12;
								int b3 = (k5 - d2 + 12) % 12;
								if (SoftFleshController._apN[a2] > 0)
								{
									SoftFleshController._apR[k5] = SoftFleshController._apR[a2];
									break;
								}
								if (SoftFleshController._apN[b3] > 0)
								{
									SoftFleshController._apR[k5] = SoftFleshController._apR[b3];
									break;
								}
							}
						}
					}
					for (int k6 = 0; k6 < 12; k6++)
					{
						SoftFleshController._apT[k6] = (SoftFleshController._apR[(k6 + 12 - 1) % 12] + 2f * SoftFleshController._apR[k6] + SoftFleshController._apR[(k6 + 1) % 12]) * 0.25f;
					}
					haveAp = true;
				}
			}
			if (!haveAp)
			{
				float rim = body.rimNowMm;
				if (rim >= 0f)
				{
					this._sqLastRim = rim;
				}
				else
				{
					rim = this._sqLastRim;
				}
				if (rim < 0f)
				{
					SoftFleshController.SquashInfo = "waiting for a rim measurement";
					return;
				}
				for (int k7 = 0; k7 < 12; k7++)
				{
					SoftFleshController._apT[k7] = rim * 0.001f;
				}
			}
			float apMax0 = 0f;
			for (int k8 = 0; k8 < 12; k8++)
			{
				if (SoftFleshController._apT[k8] > apMax0)
				{
					apMax0 = SoftFleshController._apT[k8];
				}
			}
			float apFloor = apMax0 * 0.45f;
			for (int k9 = 0; k9 < 12; k9++)
			{
				if (SoftFleshController._apT[k9] < apFloor)
				{
					SoftFleshController._apT[k9] = apFloor;
				}
			}
			if (body.bakeRimR != null && body.bakeRadii != null && body.bakeRimR.Length == body.bakeRadii.Length && body.bakeRadii.Length >= 2)
			{
				float mScal = 0f;
				for (int k10 = 0; k10 < 12; k10++)
				{
					mScal += SoftFleshController._apT[k10];
				}
				mScal /= 12f;
				if (mScal > 0.0001f)
				{
					int nR2 = body.bakeRadii.Length;
					float w2c = ((this.openTestWidth > body.itemW) ? this.openTestWidth : body.itemW);
					float rT2 = body.bakeRadii[0] + (body.bakeRadii[nR2 - 1] - body.bakeRadii[0]) * w2c;
					int r2 = 0;
					while (r2 + 2 < nR2 && body.bakeRadii[r2 + 1] < rT2)
					{
						r2++;
					}
					float tt2 = Mathf.InverseLerp(body.bakeRadii[r2], body.bakeRadii[r2 + 1], rT2);
					float cmdRim = Mathf.Lerp(body.bakeRimR[r2], body.bakeRimR[r2 + 1], tt2);
					if (cmdRim > 0.0001f)
					{
						float ratio = cmdRim / mScal;
						if (this._sqRatioLP <= 0f)
						{
							this._sqRatioLP = ratio;
						}
						this._sqRatioLP += (ratio - this._sqRatioLP) * (1f - Mathf.Exp(-2f * dt));
						float sizeF = ((this._sqRatioLP > 0.0001f) ? (ratio / this._sqRatioLP) : 1f);
						if (sizeF < 0.7f)
						{
							sizeF = 0.7f;
						}
						if (sizeF < 1f)
						{
							for (int k11 = 0; k11 < 12; k11++)
							{
								SoftFleshController._apT[k11] *= sizeF;
							}
						}
					}
				}
			}
			float margin = velMm * 0.001f;
			float apLo = 1E+09f;
			float apHi = 0f;
			for (int k12 = 0; k12 < 12; k12++)
			{
				SoftFleshController._apT[k12] -= margin;
				if (SoftFleshController._apT[k12] < 0.002f)
				{
					SoftFleshController._apT[k12] = 0.002f;
				}
				if (SoftFleshController._apT[k12] < apLo)
				{
					apLo = SoftFleshController._apT[k12];
				}
				if (SoftFleshController._apT[k12] > apHi)
				{
					apHi = SoftFleshController._apT[k12];
				}
			}
			float sqSig = E0.x * 811f + E0.y * 463f + E0.z * 677f + D0.x * 97f + D0.y * 131f + D0.z * 61f + apLo * 2311f + apHi * 1531f + velMm * 17f + this.itemSquash * 503f + body.trkDepthMm * 3.7f + body.trkRadMm * 5.1f + body.trkLatMm * 7.3f;
			if (this._sqAny && Mathf.Abs(sqSig - this._sqSigLast) < 0.0001f)
			{
				return;
			}
			this._sqSigLast = sqSig;
			for (int i = 0; i < this._itemDefs.Count; i++)
			{
				if (this._itemDefs[i].owner != null)
				{
					this.EnsureSquashRecs(this._itemDefs[i].owner);
				}
			}
			int touched = 0;
			for (int ri = this._squash.Count - 1; ri >= 0; ri--)
			{
				SoftFleshController.SquashRec rec = this._squash[ri];
				if (rec.rend == null || rec.owner == null)
				{
					if (rec.clone != null)
					{
						Object.Destroy(rec.clone);
					}
					this._squash.RemoveAt(ri);
				}
				else
				{
					Mesh cur = ((rec.mf != null) ? rec.mf.sharedMesh : ((SkinnedMeshRenderer)rec.rend).sharedMesh);
					if (cur != rec.clone)
					{
						this._squash.RemoveAt(ri);
					}
					else
					{
						int j = rec.baseV.Length;
						int movedR = 0;
						if (rec.domBone == null)
						{
							Transform tr = rec.rend.transform;
							Vector3 entL = tr.InverseTransformPoint(E0);
							Vector3 dv = tr.InverseTransformVector(D0);
							float uS = dv.magnitude;
							if (uS < 1E-06f)
							{
								goto IL_0C06;
							}
							Vector3 dirL = dv / uS;
							Vector3 ubL = tr.InverseTransformVector(u0);
							Vector3 vbL = tr.InverseTransformVector(v0);
							for (int l = 0; l < j; l++)
							{
								rec.work[l] = SoftFleshController.SquashVert(rec.baseV[l], entL, dirL, ubL, vbL, 0.012f * uS, uS, SoftFleshController._apT, this.itemSquash);
								if (rec.work[l] != rec.baseV[l])
								{
									movedR++;
								}
							}
						}
						else
						{
							int nb = rec.bones.Length;
							if (SoftFleshController._sqEntB == null || SoftFleshController._sqEntB.Length < nb)
							{
								SoftFleshController._sqEntB = new Vector3[nb];
								SoftFleshController._sqDirB = new Vector3[nb];
								SoftFleshController._sqScaleB = new float[nb];
								SoftFleshController._sqUB = new Vector3[nb];
								SoftFleshController._sqVB = new Vector3[nb];
								SoftFleshController._sqHangB = new bool[nb];
							}
							for (int b4 = 0; b4 < nb; b4++)
							{
								Transform bn = rec.bones[b4];
								if (bn == null)
								{
									SoftFleshController._sqScaleB[b4] = 0f;
									SoftFleshController._sqHangB[b4] = false;
								}
								else
								{
									SoftFleshController._sqHangB[b4] = SoftFleshController.IsHangName(bn.name);
									Matrix4x4 w2b = rec.binds[b4].inverse * bn.worldToLocalMatrix;
									SoftFleshController._sqEntB[b4] = w2b.MultiplyPoint3x4(E0);
									Vector3 dv2 = w2b.MultiplyVector(D0);
									float uS2 = dv2.magnitude;
									SoftFleshController._sqScaleB[b4] = uS2;
									SoftFleshController._sqDirB[b4] = ((uS2 > 1E-06f) ? (dv2 / uS2) : Vector3.forward);
									SoftFleshController._sqUB[b4] = w2b.MultiplyVector(u0);
									SoftFleshController._sqVB[b4] = w2b.MultiplyVector(v0);
								}
							}
							for (int m = 0; m < j; m++)
							{
								int b5 = rec.domBone[m];
								float uS3 = SoftFleshController._sqScaleB[b5];
								rec.work[m] = ((uS3 < 1E-06f || SoftFleshController._sqHangB[b5]) ? rec.baseV[m] : SoftFleshController.SquashVert(rec.baseV[m], SoftFleshController._sqEntB[b5], SoftFleshController._sqDirB[b5], SoftFleshController._sqUB[b5], SoftFleshController._sqVB[b5], 0.012f * uS3, uS3, SoftFleshController._apT, this.itemSquash));
								if (rec.work[m] != rec.baseV[m])
								{
									movedR++;
								}
							}
						}
						rec.clone.vertices = rec.work;
						rec.active = true;
						touched += movedR;
					}
				}
				IL_0C06:;
			}
			this._sqAny = touched > 0;
			SoftFleshController.SquashInfo = ((touched > 0) ? string.Concat(new string[]
			{
				"clamped to the mouth (aperture ",
				(apLo * 1000f).ToString("F0"),
				"-",
				(apHi * 1000f).ToString("F0"),
				"mm, fast margin ",
				velMm.ToString("F1"),
				"mm, gain ",
				this.itemSquash.ToString("F2"),
				"), ",
				touched.ToString(),
				" verts MOVED / ",
				this._squash.Count.ToString(),
				" renderer(s)"
			}) : ((this._squash.Count > 0) ? "in contact but nothing to clamp (the item already sits inside the aperture everywhere)" : "ran but found no squashable mesh (unreadable or over budget)"));
		}

		// Token: 0x06000102 RID: 258 RVA: 0x000413DC File Offset: 0x0003F5DC
		private static Vector3 SquashVert(Vector3 v, Vector3 ent, Vector3 dir, Vector3 ub, Vector3 vb, float band, float uS, float[] ap, float gain)
		{
			Vector3 rel = v - ent;
			float t = Vector3.Dot(rel, dir);
			float u = (t + band * 1.6667f) / (2f * band);
			if (u <= 0f)
			{
				return v;
			}
			if (u > 1f)
			{
				u = 1f;
			}
			float s = u * u * (3f - 2f * u);
			Vector3 rad = rel - dir * t;
			float r = rad.magnitude;
			if (r < 1E-06f)
			{
				return v;
			}
			float ang = Mathf.Atan2(Vector3.Dot(rad, vb), Vector3.Dot(rad, ub));
			int bi = (int)((ang + 3.1415927f) * ((float)ap.Length / 6.2831855f));
			if (bi < 0)
			{
				bi = 0;
			}
			else if (bi > ap.Length - 1)
			{
				bi = ap.Length - 1;
			}
			float tgt = ap[bi] * uS;
			if (r <= tgt)
			{
				return v;
			}
			float d = (r - tgt) * s * gain;
			float cap = r * 0.45f;
			if (d > cap)
			{
				d = cap;
			}
			return v - rad * (d / r);
		}

		// Token: 0x06000103 RID: 259 RVA: 0x000414EC File Offset: 0x0003F6EC
		private void RelaxSquash()
		{
			if (!this._sqAny)
			{
				return;
			}
			this._sqAny = false;
			for (int i = 0; i < this._squash.Count; i++)
			{
				SoftFleshController.SquashRec rec = this._squash[i];
				if (rec.active && !(rec.clone == null))
				{
					Array.Copy(rec.baseV, rec.work, rec.baseV.Length);
					rec.clone.vertices = rec.work;
					rec.active = false;
				}
			}
		}

		// Token: 0x06000104 RID: 260 RVA: 0x00041574 File Offset: 0x0003F774
		public void RestoreSquash()
		{
			for (int i = 0; i < this._squash.Count; i++)
			{
				SoftFleshController.SquashRec rec = this._squash[i];
				if (rec.rend != null && rec.orig != null)
				{
					if (rec.mf != null)
					{
						if (rec.mf.sharedMesh == rec.clone)
						{
							rec.mf.sharedMesh = rec.orig;
						}
					}
					else
					{
						SkinnedMeshRenderer smr = rec.rend as SkinnedMeshRenderer;
						if (smr != null && smr.sharedMesh == rec.clone)
						{
							smr.sharedMesh = rec.orig;
						}
					}
				}
				if (rec.clone != null)
				{
					Object.Destroy(rec.clone);
				}
			}
			if (this._squash.Count > 0)
			{
				SoftFleshPlugin.Log("[SF] item squash: restored " + this._squash.Count.ToString() + " renderer(s) to their original meshes");
			}
			this._squash.Clear();
			this._sqDone.Clear();
			this._sqAny = false;
		}

		// Token: 0x06000105 RID: 261 RVA: 0x000416A0 File Offset: 0x0003F8A0
		public void ClearItemColliders()
		{
			this.ClearViz();
			this.RestoreAims();
			this._aims.Clear();
			this.RestoreSquash();
			this.DetachDynColliders();
			this._itemDefs.Clear();
			this._pressers.RemoveAll((SoftFleshController.Presser pr) => pr.item);
		}

		// Token: 0x06000106 RID: 262 RVA: 0x00041708 File Offset: 0x0003F908
		private static void CollectFingerCapsules(Transform root, List<SoftFleshController.Presser> into)
		{
			if (root == null)
			{
				return;
			}
			Dictionary<string, Transform[]> joints = new Dictionary<string, Transform[]>();
			SoftFleshController.CollectFingerJoints(root, joints);
			foreach (KeyValuePair<string, Transform[]> kv in joints)
			{
				Transform[] i = kv.Value;
				bool right = kv.Key.EndsWith("r");
				if (i[0] != null && i[1] != null)
				{
					into.Add(new SoftFleshController.Presser
					{
						a = i[0],
						b = i[1],
						right = right,
						prox = true
					});
				}
				if (i[1] != null && i[2] != null)
				{
					into.Add(new SoftFleshController.Presser
					{
						a = i[1],
						b = i[2],
						ext = 0.7f,
						right = right
					});
				}
				else if (i[2] != null)
				{
					into.Add(new SoftFleshController.Presser
					{
						a = i[2],
						b = i[2],
						right = right
					});
				}
			}
		}

		// Token: 0x1700000A RID: 10
		// (get) Token: 0x06000107 RID: 263 RVA: 0x00041844 File Offset: 0x0003FA44
		public static int MtWorkers
		{
			get
			{
				if (SoftFleshController._mtWorkers < 0)
				{
					SoftFleshController._mtWorkers = Environment.ProcessorCount - 1;
					if (SoftFleshController._mtWorkers < 1)
					{
						SoftFleshController._mtWorkers = 1;
					}
					if (SoftFleshController._mtWorkers > 15)
					{
						SoftFleshController._mtWorkers = 15;
					}
					SoftFleshController.WkGen g = new SoftFleshController.WkGen();
					g.go = new Semaphore(0, int.MaxValue);
					SoftFleshController._wkGen = g;
					for (int i = 0; i < SoftFleshController._mtWorkers; i++)
					{
						new Thread(new ParameterizedThreadStart(SoftFleshController.WkLoop))
						{
							IsBackground = true,
							Name = "SoftFlesh worker " + i.ToString()
						}.Start(g);
					}
				}
				return SoftFleshController._mtWorkers;
			}
		}

		// Token: 0x06000108 RID: 264 RVA: 0x000418F0 File Offset: 0x0003FAF0
		public static void ShutdownWorkers()
		{
			SoftFleshController.WkGen g = SoftFleshController._wkGen;
			if (g == null)
			{
				return;
			}
			SoftFleshController._wkGen = null;
			SoftFleshController._mtWorkers = -1;
			g.quit = true;
			try
			{
				g.go.Release(64);
			}
			catch
			{
			}
		}

		// Token: 0x06000109 RID: 265 RVA: 0x00041940 File Offset: 0x0003FB40
		private static void WkLoop(object o)
		{
			SoftFleshController.WkGen g = (SoftFleshController.WkGen)o;
			for (;;)
			{
				g.go.WaitOne();
				if (g.quit)
				{
					break;
				}
				SoftFleshController.RunClaims(SoftFleshController._mtSt);
			}
		}

		// Token: 0x0600010A RID: 266 RVA: 0x00041974 File Offset: 0x0003FB74
		private static void RunClaims(SoftFleshController.MtState st)
		{
			for (;;)
			{
				int c = Interlocked.Increment(ref SoftFleshController._wkNext) - 1;
				if (c >= SoftFleshController._wkChunks)
				{
					break;
				}
				int lo = c * SoftFleshController._wkPer;
				int hi = lo + SoftFleshController._wkPer;
				if (hi > SoftFleshController._wkCount)
				{
					hi = SoftFleshController._wkCount;
				}
				try
				{
					if (hi > lo)
					{
						if (st.body3 != null)
						{
							st.body3(c, lo, hi);
						}
						else
						{
							st.body2(lo, hi);
						}
					}
				}
				catch (Exception e)
				{
					st.err = e;
				}
				if (Interlocked.Decrement(ref st.remaining) == 0)
				{
					st.done.Set();
				}
			}
		}

		// Token: 0x0600010B RID: 267 RVA: 0x00041A18 File Offset: 0x0003FC18
		private static void DispatchChunks(int count, int chunks, Action<int, int> b2, Action<int, int, int> b3)
		{
			ManualResetEvent done = SoftFleshController._mtEvt;
			if (done == null)
			{
				done = new ManualResetEvent(false);
				SoftFleshController._mtEvt = done;
			}
			done.Reset();
			SoftFleshController.MtState st = SoftFleshController._mtSt;
			st.body2 = b2;
			st.body3 = b3;
			st.remaining = chunks;
			st.err = null;
			st.done = done;
			SoftFleshController._wkCount = count;
			SoftFleshController._wkPer = (count + chunks - 1) / chunks;
			SoftFleshController._wkChunks = chunks;
			Interlocked.Exchange(ref SoftFleshController._wkNext, 0);
			SoftFleshController.WkGen g = SoftFleshController._wkGen;
			if (g != null)
			{
				g.go.Release(chunks - 1);
			}
			SoftFleshController.RunClaims(st);
			done.WaitOne();
			Exception err = st.err;
			if (err != null)
			{
				throw err;
			}
		}

		// Token: 0x0600010C RID: 268 RVA: 0x00041AC0 File Offset: 0x0003FCC0
		private static void ParallelForIdx(int count, Action<int, int, int> body)
		{
			int nw = SoftFleshController.MtWorkers;
			int chunks = count >> 9;
			if (chunks > nw)
			{
				chunks = nw;
			}
			if (chunks < 2)
			{
				body(0, 0, count);
				return;
			}
			SoftFleshController.DispatchChunks(count, chunks, null, body);
		}

		// Token: 0x0600010D RID: 269 RVA: 0x00041AF8 File Offset: 0x0003FCF8
		private static void ParallelFor(int count, Action<int, int> body)
		{
			int nw = SoftFleshController.MtWorkers;
			int chunks = count >> 9;
			if (chunks > nw)
			{
				chunks = nw;
			}
			if (chunks < 2)
			{
				body(0, count);
				return;
			}
			SoftFleshController.DispatchChunks(count, chunks, body, null);
		}

		// Token: 0x0600010E RID: 270 RVA: 0x00041B2C File Offset: 0x0003FD2C
		private static void CacheDynParticles(SoftFleshController.Body body)
		{
			body.dynParts.Clear();
			body.dynPartTr.Clear();
			body.dynPartRest.Clear();
			body.dynInitPosF = null;
			if (body.vagDyn.Count == 0)
			{
				return;
			}
			Type dbT = body.vagDyn[0].GetType();
			FieldInfo partsF = dbT.GetField("m_Particles", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (partsF == null)
			{
				SoftFleshPlugin.Log("[SF] rest-pose drive: m_Particles not found");
				return;
			}
			FieldInfo trF = null;
			FieldInfo posF = null;
			for (int i = 0; i < body.vagDyn.Count; i++)
			{
				IList lst = partsF.GetValue(body.vagDyn[i]) as IList;
				if (lst != null)
				{
					for (int q = 0; q < lst.Count; q++)
					{
						object pt = lst[q];
						if (pt != null)
						{
							if (trF == null)
							{
								Type pT = pt.GetType();
								trF = pT.GetField("m_Transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								posF = pT.GetField("m_InitLocalPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								if (trF == null || posF == null)
								{
									SoftFleshPlugin.Log("[SF] rest-pose drive: particle fields not found");
									return;
								}
							}
							Transform tr = trF.GetValue(pt) as Transform;
							if (!(tr == null))
							{
								body.dynParts.Add(pt);
								body.dynPartTr.Add(tr);
								body.dynPartRest.Add((Vector3)posF.GetValue(pt));
							}
						}
					}
				}
			}
			body.dynStifOrig.Clear();
			body.dynDampOrig.Clear();
			body.dynElastOrig.Clear();
			for (int j = 0; j < body.vagDyn.Count; j++)
			{
				DynamicBone db2 = body.vagDyn[j];
				body.dynStifOrig.Add((db2 != null) ? db2.m_Stiffness : 0f);
				body.dynDampOrig.Add((db2 != null) ? db2.m_Damping : 0f);
				body.dynElastOrig.Add((db2 != null) ? db2.m_Elasticity : 0f);
			}
			body.lastFirm = -1f;
			body.dynInitPosF = posF;
			try
			{
				body.dynUpdParams = dbT.GetMethod("UpdateParameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			catch
			{
				body.dynUpdParams = null;
			}
			SoftFleshPlugin.Log("[SF] petal params writable: UpdateParameters " + ((body.dynUpdParams != null) ? "found" : "MISSING - Petal Firm cannot work"));
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] rest-pose drive: cached ",
				body.dynParts.Count.ToString(),
				" particles across ",
				body.vagDyn.Count.ToString(),
				" chains"
			}));
		}

		// Token: 0x0600010F RID: 271 RVA: 0x00041E24 File Offset: 0x00040024
		private static void CollectSpread(Transform t, SoftFleshController.Body body, HashSet<Transform> skinned)
		{
			string ln = t.name;
			if (skinned.Contains(t))
			{
				if (ln == "k_f_ana_00" && body.spreadL == null)
				{
					body.spreadL = t;
				}
				else if (ln == "k_f_ana_01" && body.spreadR == null)
				{
					body.spreadR = t;
				}
			}
			for (int i = 0; i < t.childCount; i++)
			{
				SoftFleshController.CollectSpread(t.GetChild(i), body, skinned);
			}
		}

		// Token: 0x06000110 RID: 272 RVA: 0x00041EA8 File Offset: 0x000400A8
		private static void CollectVaginaBones(Transform t, SoftFleshController.Body body, HashSet<Transform> skinned)
		{
			string ln = t.name.ToLower();
			if (ln.Contains("vagina") && (skinned == null || skinned.Contains(t)))
			{
				if (ln.Contains("root"))
				{
					if (body.vagRoot == null)
					{
						body.vagRoot = t;
					}
				}
				else if (ln.Contains("inner"))
				{
					if (!body.vagInner.Contains(t))
					{
						body.vagInner.Add(t);
						body.vagInnerBase.Add(t.localPosition);
					}
				}
				else if (!body.vagPivots.Contains(t))
				{
					body.vagPivots.Add(t);
					body.vagPivotRot.Add(t.localRotation);
					body.vagPivotPos.Add(t.localPosition);
				}
			}
			for (int i = 0; i < t.childCount; i++)
			{
				SoftFleshController.CollectVaginaBones(t.GetChild(i), body, skinned);
			}
		}

		// Token: 0x06000111 RID: 273 RVA: 0x00041F9C File Offset: 0x0004019C
		private static void CollectFingerJoints(Transform t, Dictionary<string, Transform[]> joints)
		{
			string ln = t.name.ToLower();
			if (ln.StartsWith("cf_j_"))
			{
				string fn = null;
				if (ln.Contains("thumb"))
				{
					fn = "thumb";
				}
				else if (ln.Contains("index"))
				{
					fn = "index";
				}
				else if (ln.Contains("middle"))
				{
					fn = "middle";
				}
				else if (ln.Contains("ring"))
				{
					fn = "ring";
				}
				else if (ln.Contains("little"))
				{
					fn = "little";
				}
				if (fn != null)
				{
					int seg = -1;
					if (ln.EndsWith("01_l") || ln.EndsWith("01_r"))
					{
						seg = 0;
					}
					else if (ln.EndsWith("02_l") || ln.EndsWith("02_r"))
					{
						seg = 1;
					}
					else if (ln.EndsWith("03_l") || ln.EndsWith("03_r"))
					{
						seg = 2;
					}
					if (seg >= 0)
					{
						string key = fn + "_" + (ln.EndsWith("_r") ? "r" : "l");
						Transform[] arr;
						if (!joints.TryGetValue(key, out arr))
						{
							arr = new Transform[3];
							joints[key] = arr;
						}
						arr[seg] = t;
					}
				}
			}
			for (int i = 0; i < t.childCount; i++)
			{
				SoftFleshController.CollectFingerJoints(t.GetChild(i), joints);
			}
		}

		// Token: 0x06000112 RID: 274 RVA: 0x000420FC File Offset: 0x000402FC
		private static void AddAdj(List<int> l, int v)
		{
			for (int i = 0; i < l.Count; i++)
			{
				if (l[i] == v)
				{
					return;
				}
			}
			l.Add(v);
		}

		// Token: 0x06000113 RID: 275 RVA: 0x0004212C File Offset: 0x0004032C
		private void MeasureRingFrame(SoftFleshController.Body body, SkinnedMeshRenderer smr)
		{
			body.ringFrameOK = false;
			if (body.vagPivots.Count == 0 || smr == null || body.vagW == null || body.kokW == null)
			{
				return;
			}
			Mesh bm = new Mesh();
			smr.BakeMesh(bm);
			Vector3[] bv = bm.vertices;
			Vector3[] nrm = bm.normals;
			Object.Destroy(bm);
			Mesh bm2 = null;
			if (bv == null || bv.Length != body.baseVerts.Length)
			{
				return;
			}
			Transform tr = smr.transform;
			Vector3 c = Vector3.zero;
			int nc = 0;
			for (int i = 0; i < bv.Length; i++)
			{
				if (body.vagW[i] > 0.25f && body.kokW[i] > 0.25f)
				{
					c += bv[i];
					nc++;
				}
			}
			if (nc < 16)
			{
				return;
			}
			c /= (float)nc;
			Mesh mesh = ((bm2 != null) ? bm2 : null);
			Vector3 nAcc = Vector3.zero;
			int nn = 0;
			for (int j = 0; j < bv.Length; j++)
			{
				if (body.vagW[j] > 0.25f && body.kokW[j] > 0.25f && nrm != null && j < nrm.Length)
				{
					nAcc += nrm[j];
					nn++;
				}
			}
			if (nn < 16 || nAcc.sqrMagnitude < 1E-12f)
			{
				return;
			}
			Vector3 ax = -nAcc.normalized;
			Vector3 cW = tr.TransformPoint(c);
			Vector3 aW = tr.TransformDirection(ax).normalized;
			Transform rf = body.vagPivots[0];
			body.ringRef = rf;
			body.ringLocalC = rf.InverseTransformPoint(cW);
			body.ringLocalAxis = rf.InverseTransformDirection(aW);
			body.ringFrameOK = true;
			float rsum = 0f;
			int rn = 0;
			for (int k = 0; k < bv.Length; k++)
			{
				if (body.vagW[k] > 0.25f && body.kokW[k] > 0.25f)
				{
					Vector3 d = bv[k] - c;
					rsum += (d - ax * Vector3.Dot(d, ax)).magnitude;
					rn++;
				}
			}
			float nearV = 1E+09f;
			for (int l = 0; l < bv.Length; l++)
			{
				if (body.vagW[l] > 0.25f && body.kokW[l] > 0.25f)
				{
					float dv2 = (tr.TransformPoint(bv[l]) - cW).sqrMagnitude;
					if (dv2 < nearV)
					{
						nearV = dv2;
					}
				}
			}
			StringBuilder sb2 = new StringBuilder();
			sb2.Append("[SF] rig vs mesh ring: nearestBandVert=").Append((Mathf.Sqrt(nearV) * 1000f).ToString("F0")).Append("mm | ");
			for (int q = 0; q < body.vagPivots.Count; q++)
			{
				sb2.Append(body.vagPivots[q].name.Replace("cf_J_Vagina_Pivot", "P")).Append('=').Append(((body.vagPivots[q].position - cW).magnitude * 1000f).ToString("F0"))
					.Append("  ");
			}
			for (int q2 = 0; q2 < body.vagInner.Count; q2++)
			{
				sb2.Append(body.vagInner[q2].name.Replace("cf_J_Vagina_", "I_")).Append('=').Append(((body.vagInner[q2].position - cW).magnitude * 1000f).ToString("F0"))
					.Append("  ");
			}
			SoftFleshPlugin.Log(sb2.ToString());
			body.ringBones.Clear();
			body.ringBonePos.Clear();
			for (int q3 = 0; q3 < body.vagInner.Count; q3++)
			{
				Transform t4 = body.vagInner[q3];
				if (!(t4 == null) && (t4.position - cW).magnitude <= 0.06f)
				{
					body.ringBones.Add(t4);
					body.ringBonePos.Add(t4.localPosition);
				}
			}
			for (int q4 = 0; q4 < body.vagPivots.Count; q4++)
			{
				Transform t5 = body.vagPivots[q4];
				if (!(t5 == null) && !body.ringBones.Contains(t5) && (t5.position - cW).magnitude <= 0.06f)
				{
					body.ringBones.Add(t5);
					body.ringBonePos.Add(t5.localPosition);
				}
			}
			StringBuilder sb3 = new StringBuilder("[SF] ring bones skinning: ");
			BoneWeight[] bws = ((body.workMesh != null) ? body.workMesh.boneWeights : null);
			for (int q5 = 0; q5 < body.ringBones.Count; q5++)
			{
				int bi3 = Array.IndexOf<Transform>(body.bonesArr, body.ringBones[q5]);
				int cnt3 = 0;
				if (bi3 >= 0 && bws != null)
				{
					foreach (BoneWeight w3 in bws)
					{
						if ((w3.boneIndex0 == bi3 && w3.weight0 > 0.02f) || (w3.boneIndex1 == bi3 && w3.weight1 > 0.02f) || (w3.boneIndex2 == bi3 && w3.weight2 > 0.02f) || (w3.boneIndex3 == bi3 && w3.weight3 > 0.02f))
						{
							cnt3++;
						}
					}
				}
				sb3.Append(body.ringBones[q5].name.Replace("cf_J_Vagina_", "")).Append('=').Append((bi3 < 0) ? "NOT-A-SKIN-BONE" : (cnt3.ToString() + "v"))
					.Append("  ");
			}
			SoftFleshPlugin.Log(sb3.ToString());
			SoftFleshPlugin.Log("[SF] ring bones selected by distance: " + body.ringBones.Count.ToString() + " of " + (body.vagPivots.Count + body.vagInner.Count).ToString());
		}

		// Token: 0x06000114 RID: 276 RVA: 0x000427F4 File Offset: 0x000409F4
		private static long PosKey(Vector3 v)
		{
			long x = (long)Mathf.Round(v.x * 10000f);
			long y = (long)Mathf.Round(v.y * 10000f);
			long z = (long)Mathf.Round(v.z * 10000f);
			return (x * 73856093L) ^ (y * 19349663L) ^ (z * 83492791L);
		}

		// Token: 0x06000115 RID: 277 RVA: 0x00042854 File Offset: 0x00040A54
		private static void DominantOf(BoneWeight b, out int idx, out float w)
		{
			idx = b.boneIndex0;
			w = b.weight0;
			if (b.weight1 > w)
			{
				w = b.weight1;
				idx = b.boneIndex1;
			}
			if (b.weight2 > w)
			{
				w = b.weight2;
				idx = b.boneIndex2;
			}
			if (b.weight3 > w)
			{
				w = b.weight3;
				idx = b.boneIndex3;
			}
		}

		// Token: 0x06000116 RID: 278 RVA: 0x000428CC File Offset: 0x00040ACC
		private static Transform FindLimbBone(Transform[] bones, string key, string side)
		{
			if (bones == null)
			{
				return null;
			}
			string exact = "_" + key + "_" + side;
			string sfx = "_" + side;
			Transform loose = null;
			foreach (Transform b in bones)
			{
				if (!(b == null))
				{
					string ln = b.name.ToLower();
					if (ln.EndsWith(exact))
					{
						return b;
					}
					if (loose == null && ln.Contains(key) && ln.EndsWith(sfx))
					{
						loose = b;
					}
				}
			}
			return loose;
		}

		// Token: 0x06000117 RID: 279 RVA: 0x00042960 File Offset: 0x00040B60
		private static SkinnedMeshRenderer FindFaceSmr(ChaControl cha)
		{
			SkinnedMeshRenderer best = null;
			SkinnedMeshRenderer[] smrs = cha.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			foreach (SkinnedMeshRenderer r in smrs)
			{
				if (!(r == null) && !(r.sharedMesh == null))
				{
					string nm = r.name.ToLower();
					if (nm == "cf_o_face")
					{
						return r;
					}
					if (best == null && nm.Contains("face") && !nm.Contains("eye") && !nm.Contains("mayuge") && !nm.Contains("noseline"))
					{
						best = r;
					}
				}
			}
			return best;
		}

		// Token: 0x06000118 RID: 280 RVA: 0x00042A10 File Offset: 0x00040C10
		private void SetupMouth(SoftFleshController.Body body)
		{
			body.mouthIdx = null;
			body.mouthPtn = -1;
			Mesh mesh = body.workMesh;
			if (mesh == null || body.cha == null)
			{
				return;
			}
			body.mouthTris = mesh.triangles;
			int nt = body.mouthTris.Length / 3;
			body.mouthTriSub = new byte[nt];
			int at = 0;
			int sm = 0;
			while (sm < mesh.subMeshCount && sm < 255)
			{
				int cnt3 = mesh.GetTriangles(sm).Length / 3;
				int i = 0;
				while (i < cnt3 && at < nt)
				{
					body.mouthTriSub[at++] = (byte)sm;
					i++;
				}
				sm++;
			}
			Material[] mats = ((body.smr != null) ? body.smr.sharedMaterials : null);
			body.mouthMatNames = new string[(mats != null) ? mats.Length : 0];
			for (int j = 0; j < body.mouthMatNames.Length; j++)
			{
				body.mouthMatNames[j] = ((mats[j] != null) ? mats[j].name : "-");
			}
			SoftFleshPlugin.Log("[SF] face materials: " + string.Join(", ", body.mouthMatNames));
			int k = mesh.vertexCount;
			int kuwae = -1;
			for (int si = 0; si < mesh.blendShapeCount; si++)
			{
				if (mesh.GetBlendShapeName(si).ToLower().Contains("kuwae"))
				{
					kuwae = si;
					body.mouthKuwae = si;
					break;
				}
			}
			if (kuwae < 0)
			{
				SoftFleshPlugin.Log("[SF] face: no 'kuwae' blend shape on " + mesh.name + " - mouth drive off");
				return;
			}
			Vector3[] dv = new Vector3[k];
			Vector3[] dn = new Vector3[k];
			Vector3[] dt = new Vector3[k];
			mesh.GetBlendShapeFrameVertices(kuwae, mesh.GetBlendShapeFrameCount(kuwae) - 1, dv, dn, dt);
			List<int> idx = new List<int>();
			body.mouthW = new float[k];
			body.mouthCore = new bool[k];
			body.mouthDv = dv;
			bool[] isLining = new bool[k];
			for (int l = 0; l < k; l++)
			{
				float m = dv[l].magnitude;
				if (m > 0.003f)
				{
					idx.Add(l);
				}
				float w = ((m > 0.0015f) ? 1f : 0f);
				body.mouthCore[l] = m > 0.006f;
				if (body.domBone != null && body.bonesArr != null)
				{
					int dbm = body.domBone[l];
					isLining[l] = dbm >= 0 && dbm < body.bonesArr.Length && body.bonesArr[dbm] != null && body.bonesArr[dbm].name.ToLower().Contains("cavity");
					if (isLining[l])
					{
						w = 0f;
					}
				}
				body.mouthW[l] = w;
			}
			body.mouthNbrStart = null;
			body.mouthNbrList = null;
			if (body.adjStart != null && body.adjList != null)
			{
				Dictionary<long, List<int>> map = new Dictionary<long, List<int>>(k);
				for (int n = 0; n < k; n++)
				{
					long key = SoftFleshController.PosKey(body.baseVerts[n]);
					List<int> l2;
					if (!map.TryGetValue(key, out l2))
					{
						l2 = new List<int>(2);
						map[key] = l2;
					}
					l2.Add(n);
				}
				int[][] twins = new int[k][];
				int[] canon = new int[k];
				for (int i2 = 0; i2 < k; i2++)
				{
					canon[i2] = i2;
				}
				foreach (KeyValuePair<long, List<int>> kv in map)
				{
					if (kv.Value.Count > 1)
					{
						int[] arr = kv.Value.ToArray();
						int c0 = arr[0];
						for (int k2 = 1; k2 < arr.Length; k2++)
						{
							if (arr[k2] < c0)
							{
								c0 = arr[k2];
							}
						}
						for (int k3 = 0; k3 < arr.Length; k3++)
						{
							twins[arr[k3]] = arr;
							canon[arr[k3]] = c0;
						}
					}
				}
				int[] nbStart = new int[k + 1];
				List<int> nbList = new List<int>(body.adjList.Length + k);
				List<int> tmp = new List<int>();
				for (int i3 = 0; i3 < k; i3++)
				{
					tmp.Clear();
					int[] srcs = twins[i3];
					int sc = ((srcs != null) ? srcs.Length : 1);
					for (int s2 = 0; s2 < sc; s2++)
					{
						int src = ((srcs != null) ? srcs[s2] : i3);
						for (int k4 = body.adjStart[src]; k4 < body.adjStart[src + 1]; k4++)
						{
							int j2 = canon[body.adjList[k4]];
							if (j2 != canon[i3] && !tmp.Contains(j2))
							{
								tmp.Add(j2);
							}
						}
					}
					nbStart[i3] = nbList.Count;
					nbList.AddRange(tmp);
				}
				nbStart[k] = nbList.Count;
				body.mouthNbrStart = nbStart;
				body.mouthNbrList = nbList.ToArray();
				for (int i4 = 0; i4 < k; i4++)
				{
					if (twins[i4] != null)
					{
						float wm = 0f;
						int[] arr2 = twins[i4];
						for (int k5 = 0; k5 < arr2.Length; k5++)
						{
							if (body.mouthW[arr2[k5]] > wm)
							{
								wm = body.mouthW[arr2[k5]];
							}
						}
						for (int k6 = 0; k6 < arr2.Length; k6++)
						{
							if (wm > 0f && body.mouthW[arr2[k6]] <= 0f)
							{
								body.mouthW[arr2[k6]] = wm;
								if (!idx.Contains(arr2[k6]))
								{
									idx.Add(arr2[k6]);
								}
							}
						}
					}
				}
			}
			int[] nbS = body.mouthNbrStart;
			int[] nbL = body.mouthNbrList;
			if (nbS != null)
			{
				List<int> ringPrev = new List<int>(idx);
				for (int ring = 0; ring < 6; ring++)
				{
					float wr = 0.5f * (1f + Mathf.Cos(3.1415927f * (float)(ring + 1) / 7f));
					List<int> ringNext = new List<int>();
					for (int q = 0; q < ringPrev.Count; q++)
					{
						int i5 = ringPrev[q];
						for (int k7 = nbS[i5]; k7 < nbS[i5 + 1]; k7++)
						{
							int j3 = nbL[k7];
							if (body.mouthW[j3] <= 0f && !isLining[j3])
							{
								body.mouthW[j3] = wr;
								ringNext.Add(j3);
								idx.Add(j3);
							}
						}
					}
					ringPrev = ringNext;
				}
				float[] linW = new float[] { 1f, 1f, 1f, 1f, 0.8f };
				List<int> lp = new List<int>();
				for (int i6 = 0; i6 < k; i6++)
				{
					if (body.mouthW[i6] > 0f && !isLining[i6])
					{
						lp.Add(i6);
					}
				}
				for (int ring2 = 0; ring2 < linW.Length; ring2++)
				{
					List<int> nxt = new List<int>();
					for (int q2 = 0; q2 < lp.Count; q2++)
					{
						int i7 = lp[q2];
						for (int k8 = nbS[i7]; k8 < nbS[i7 + 1]; k8++)
						{
							int j4 = nbL[k8];
							if (isLining[j4] && body.mouthW[j4] <= 0f)
							{
								body.mouthW[j4] = linW[ring2];
								nxt.Add(j4);
								if (!idx.Contains(j4))
								{
									idx.Add(j4);
								}
							}
						}
					}
					lp = nxt;
				}
				Dictionary<long, float> map2 = new Dictionary<long, float>(k);
				for (int i8 = 0; i8 < k; i8++)
				{
					long key2 = SoftFleshController.PosKey(body.baseVerts[i8]);
					float wv;
					if (!map2.TryGetValue(key2, out wv) || body.mouthW[i8] > wv)
					{
						map2[key2] = body.mouthW[i8];
					}
				}
				for (int i9 = 0; i9 < k; i9++)
				{
					float wv2 = map2[SoftFleshController.PosKey(body.baseVerts[i9])];
					if (wv2 > 0f && body.mouthW[i9] <= 0f)
					{
						body.mouthW[i9] = wv2;
						if (!idx.Contains(i9))
						{
							idx.Add(i9);
						}
					}
				}
			}
			idx.Clear();
			for (int i10 = 0; i10 < k; i10++)
			{
				if (body.mouthW[i10] > 0f)
				{
					idx.Add(i10);
				}
			}
			body.mouthIdx = idx.ToArray();
			List<int> halo = new List<int>();
			bool[] seen = new bool[k];
			if (nbS != null)
			{
				for (int q3 = 0; q3 < body.mouthIdx.Length; q3++)
				{
					int i11 = body.mouthIdx[q3];
					for (int k9 = nbS[i11]; k9 < nbS[i11 + 1]; k9++)
					{
						int j5 = nbL[k9];
						if (body.mouthW[j5] <= 0f && !seen[j5])
						{
							seen[j5] = true;
							halo.Add(j5);
						}
					}
				}
			}
			body.mouthHalo = halo.ToArray();
			body.mouthIn = new bool[k];
			body.mouthT = new float[k];
			body.mouthG = new Vector3[k];
			body.mwInN = 0;
			body.mwLiningN = 0;
			body.mouthLining = isLining;
			body.mouthPartner = new int[k];
			for (int i12 = 0; i12 < k; i12++)
			{
				body.mouthPartner[i12] = -1;
			}
			body.mouthPartners = new int[k * 4];
			for (int i13 = 0; i13 < k * 4; i13++)
			{
				body.mouthPartners[i13] = -1;
			}
			float kMax = 0f;
			for (int q4 = 0; q4 < body.mouthIdx.Length; q4++)
			{
				float m2 = dv[body.mouthIdx[q4]].magnitude;
				if (m2 > kMax)
				{
					kMax = m2;
				}
			}
			Vector3 cLin = Vector3.zero;
			Vector3 cEdge = Vector3.zero;
			int nL = 0;
			int nE = 0;
			for (int q5 = 0; q5 < body.mouthIdx.Length; q5++)
			{
				int i14 = body.mouthIdx[q5];
				Vector3 p = body.baseVerts[i14] + dv[i14];
				if (isLining[i14])
				{
					cLin += p;
					nL++;
					bool rim = false;
					if (nbS != null)
					{
						for (int k10 = nbS[i14]; k10 < nbS[i14 + 1]; k10++)
						{
							int j6 = nbL[k10];
							if (!isLining[j6] && body.mouthW[j6] > 0f)
							{
								rim = true;
								break;
							}
						}
					}
					if (rim)
					{
						cEdge += p;
						nE++;
					}
				}
			}
			Vector3 P0 = ((nE > 0) ? (cEdge / (float)nE) : ((nL > 0) ? (cLin / (float)nL) : Vector3.zero));
			Vector3 U = ((nE > 0 && nL > 0) ? (cEdge / (float)nE - cLin / (float)nL) : Vector3.forward);
			if (Vector3.Dot(U, Vector3.forward) < 0f)
			{
				U = -U;
			}
			if (U.sqrMagnitude < 1E-10f)
			{
				U = Vector3.forward;
			}
			U.Normalize();
			Vector3 Uu = Vector3.Cross(U, Vector3.up);
			if (Uu.sqrMagnitude < 1E-08f)
			{
				Uu = Vector3.Cross(U, Vector3.right);
			}
			Uu.Normalize();
			Vector3 Vv = Vector3.Cross(U, Uu);
			int gN = body.mouthIdx.Length;
			int[] sec = new int[gN];
			float[] alo = new float[gN];
			float[] rad = new float[gN];
			body.mouthAng = new float[k];
			body.mouthAlo = new float[k];
			for (int q6 = 0; q6 < gN; q6++)
			{
				int i15 = body.mouthIdx[q6];
				Vector3 rel = body.baseVerts[i15] + dv[i15] - P0;
				float a = Vector3.Dot(rel, U);
				Vector3 rr = rel - U * a;
				float r = rr.magnitude;
				alo[q6] = a;
				rad[q6] = r;
				int sb = (int)((Mathf.Atan2(Vector3.Dot(rr, Vv), Vector3.Dot(rr, Uu)) + 3.1415927f) * 3.8197186f);
				if (sb < 0)
				{
					sb = 0;
				}
				else if (sb > 23)
				{
					sb = 23;
				}
				sec[q6] = sb;
				body.mouthG[i15] = ((r > 1E-06f) ? (-rr / r) : (-Uu));
				body.mouthAng[i15] = Mathf.Atan2(Vector3.Dot(rr, Vv), Vector3.Dot(rr, Uu));
				body.mouthAlo[i15] = a;
			}
			for (int q7 = 0; q7 < gN; q7++)
			{
				int i16 = body.mouthIdx[q7];
				if (isLining[i16])
				{
					body.mouthT[i16] = 0f;
					body.mouthIn[i16] = true;
					body.mwInN++;
					body.mwLiningN++;
				}
				else
				{
					float rMin = float.MaxValue;
					for (int q8 = 0; q8 < gN; q8++)
					{
						if (sec[q8] == sec[q7])
						{
							float rc = rad[q8] + 0.7f * Mathf.Abs(alo[q8] - alo[q7]);
							if (rc < rMin)
							{
								rMin = rc;
							}
						}
					}
					float t = ((rMin < float.MaxValue) ? (rad[q7] - rMin) : 0f);
					if (t < 0f)
					{
						t = 0f;
					}
					body.mouthT[i16] = t;
				}
			}
			for (int q9 = 0; q9 < gN; q9++)
			{
				int i17 = body.mouthIdx[q9];
				if (!isLining[i17])
				{
					bool inn = body.mouthT[i17] < 0.0015f;
					body.mouthIn[i17] = inn;
					if (inn)
					{
						body.mwInN++;
						body.mouthT[i17] = 0f;
					}
				}
			}
			int closed = 0;
			if (nbS != null)
			{
				for (int round = 0; round < 2; round++)
				{
					int changed = 0;
					for (int q10 = 0; q10 < gN; q10++)
					{
						int i18 = body.mouthIdx[q10];
						if (!isLining[i18] && !body.mouthIn[i18])
						{
							int nr = 0;
							int no = 0;
							for (int k11 = nbS[i18]; k11 < nbS[i18 + 1]; k11++)
							{
								int j7 = nbL[k11];
								if (body.mouthW[j7] > 0f && !isLining[j7])
								{
									if (body.mouthIn[j7])
									{
										nr++;
									}
									else
									{
										no++;
									}
								}
							}
							if (nr >= 3 && nr >= no)
							{
								body.mouthIn[i18] = true;
								body.mouthT[i18] = 0f;
								body.mwInN++;
								changed++;
							}
						}
					}
					closed += changed;
					if (changed == 0)
					{
						break;
					}
				}
			}
			SoftFleshPlugin.Log("[SF] face: rim islands closed " + closed.ToString());
			body.mouthWet = null;
			int nWet = 0;
			int nRim = 0;
			SoftFleshPlugin.Log("[SF] face: wet band " + nWet.ToString() + ", contact ring (skin edge) " + nRim.ToString());
			body.mouthRing1 = new bool[k];
			int nR = 0;
			if (nbS != null)
			{
				for (int q11 = 0; q11 < gN; q11++)
				{
					int i19 = body.mouthIdx[q11];
					if (!body.mouthIn[i19] && !isLining[i19])
					{
						for (int k12 = nbS[i19]; k12 < nbS[i19 + 1]; k12++)
						{
							int j8 = nbL[k12];
							if (body.mouthW[j8] > 0f && body.mouthIn[j8] && !isLining[j8] && (body.mouthWet == null || !body.mouthWet[j8]))
							{
								body.mouthRing1[i19] = true;
								nR++;
								break;
							}
						}
					}
				}
			}
			SoftFleshPlugin.Log("[SF] face: skin edge ring " + nR.ToString());
			List<int> innL = new List<int>();
			for (int q12 = 0; q12 < gN; q12++)
			{
				int i20 = body.mouthIdx[q12];
				if (body.mouthIn[i20] && (body.mouthWet == null || !body.mouthWet[i20]))
				{
					innL.Add(i20);
				}
			}
			for (int q13 = 0; q13 < gN; q13++)
			{
				int i21 = body.mouthIdx[q13];
				if (body.mouthIn[i21])
				{
					body.mouthT[i21] = 0f;
				}
				else
				{
					Vector3 pi = body.baseVerts[i21] + dv[i21];
					float best = float.MaxValue;
					int bj = -1;
					for (int k13 = 0; k13 < innL.Count; k13++)
					{
						int j9 = innL[k13];
						float d2 = (body.baseVerts[j9] + dv[j9] - pi).sqrMagnitude;
						if (d2 < best)
						{
							best = d2;
							bj = j9;
						}
					}
					body.mouthPartner[i21] = ((best < 0.0009f) ? bj : (-1));
					int filled = 0;
					int k14 = 0;
					while (k14 < innL.Count && filled < 4)
					{
						int j10 = innL[k14];
						float d3 = (body.baseVerts[j10] + dv[j10] - pi).sqrMagnitude;
						if (d3 < 0.0001f)
						{
							body.mouthPartners[i21 * 4 + filled++] = j10;
						}
						k14++;
					}
					float ts = ((best < float.MaxValue) ? Mathf.Sqrt(best) : 0f);
					body.mouthT[i21] = ((ts > 0.012f) ? 0.012f : ts);
				}
			}
			int[] cs0 = new int[k + 1];
			List<int> cl0 = new List<int>(4096);
			List<int> innA = new List<int>();
			for (int q14 = 0; q14 < gN; q14++)
			{
				int i22 = body.mouthIdx[q14];
				if (body.mouthIn[i22] && (body.mouthWet == null || !body.mouthWet[i22]))
				{
					innA.Add(i22);
				}
			}
			for (int j11 = 0; j11 < k; j11++)
			{
				cs0[j11] = cl0.Count;
				if (body.mouthW[j11] > 0f && !body.mouthIn[j11])
				{
					Vector3 pj = body.baseVerts[j11] + dv[j11];
					for (int k15 = 0; k15 < innA.Count; k15++)
					{
						int i23 = innA[k15];
						if ((body.baseVerts[i23] + dv[i23] - pj).sqrMagnitude < 1.6000002E-05f)
						{
							cl0.Add(i23);
						}
					}
				}
			}
			cs0[k] = cl0.Count;
			body.mouthCovStart = cs0;
			body.mouthCovList = cl0.ToArray();
			body.mouthRow = new byte[k];
			for (int i24 = 0; i24 < k; i24++)
			{
				body.mouthRow[i24] = byte.MaxValue;
			}
			List<int> fr = new List<int>();
			List<int> nx = new List<int>();
			for (int q15 = 0; q15 < gN; q15++)
			{
				int i25 = body.mouthIdx[q15];
				if (!body.mouthIn[i25])
				{
					body.mouthRow[i25] = 0;
					fr.Add(i25);
				}
			}
			int ring3 = 1;
			while (ring3 <= 12 && fr.Count > 0)
			{
				nx.Clear();
				for (int q16 = 0; q16 < fr.Count; q16++)
				{
					int i26 = fr[q16];
					for (int k16 = nbS[i26]; k16 < nbS[i26 + 1]; k16++)
					{
						int j12 = nbL[k16];
						if (body.mouthRow[j12] == 255 && body.mouthW[j12] > 0f)
						{
							body.mouthRow[j12] = (byte)ring3;
							nx.Add(j12);
						}
					}
				}
				List<int> sw = fr;
				fr = nx;
				nx = sw;
				ring3++;
			}
			float[] ld = new float[k];
			for (int i27 = 0; i27 < k; i27++)
			{
				ld[i27] = 1E+09f;
			}
			for (int q17 = 0; q17 < gN; q17++)
			{
				int i28 = body.mouthIdx[q17];
				if (body.mouthIn[i28])
				{
					ld[i28] = 0f;
				}
				else
				{
					for (int k17 = nbS[i28]; k17 < nbS[i28 + 1]; k17++)
					{
						if (body.mouthIn[nbL[k17]])
						{
							ld[i28] = 0f;
							break;
						}
					}
				}
			}
			for (int pass = 0; pass < 80; pass++)
			{
				bool changed2 = false;
				for (int q18 = 0; q18 < gN; q18++)
				{
					int i29 = body.mouthIdx[q18];
					if (!body.mouthIn[i29])
					{
						Vector3 pi2 = body.baseVerts[i29] + dv[i29];
						float best2 = ld[i29];
						for (int k18 = nbS[i29]; k18 < nbS[i29 + 1]; k18++)
						{
							int j13 = nbL[k18];
							if (body.mouthW[j13] > 0f && !body.mouthIn[j13])
							{
								float c = ld[j13] + (body.baseVerts[j13] + dv[j13] - pi2).magnitude;
								if (c < best2)
								{
									best2 = c;
								}
							}
						}
						if (best2 < ld[i29] - 1E-06f)
						{
							ld[i29] = best2;
							changed2 = true;
						}
					}
				}
				if (!changed2)
				{
					break;
				}
			}
			int nFarL = 0;
			int nMidL = 0;
			float ldMax = 0f;
			for (int q19 = 0; q19 < gN; q19++)
			{
				int i30 = body.mouthIdx[q19];
				if (!body.mouthIn[i30])
				{
					if (ld[i30] > 100000000f)
					{
						ld[i30] = 0.1f;
					}
					if (ld[i30] >= 0.02f)
					{
						nFarL++;
					}
					else if (ld[i30] > 0.01f)
					{
						nMidL++;
					}
					if (ld[i30] > ldMax)
					{
						ldMax = ld[i30];
					}
				}
			}
			body.mouthLipDist = ld;
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] face: lip distance over the skin: ",
				nFarL.ToString(),
				" skin vertices beyond 20mm (the face), ",
				nMidL.ToString(),
				" in the 10-20mm fade, farthest ",
				(ldMax * 1000f).ToString("F0"),
				"mm"
			}));
			float[] uf = new float[k];
			float cy = 0f;
			int nc = 0;
			for (int q20 = 0; q20 < gN; q20++)
			{
				int i31 = body.mouthIdx[q20];
				if (body.mouthIn[i31] && !isLining[i31])
				{
					cy += (body.baseVerts[i31] + dv[i31]).y;
					nc++;
				}
			}
			if (nc > 0)
			{
				cy /= (float)nc;
			}
			int nUp = 0;
			for (int q21 = 0; q21 < gN; q21++)
			{
				int i32 = body.mouthIdx[q21];
				float fy = ((body.baseVerts[i32] + dv[i32]).y - cy + 0.002f) / 0.004f;
				if (fy < 0f)
				{
					fy = 0f;
				}
				else if (fy > 1f)
				{
					fy = 1f;
				}
				float fr2;
				if (body.mouthIn[i32])
				{
					if (isLining[i32])
					{
						fr2 = 0f;
					}
					else
					{
						int r2 = (int)((body.mouthRow != null) ? body.mouthRow[i32] : byte.MaxValue);
						fr2 = ((r2 <= 3) ? 1f : ((r2 >= 6) ? 0f : (1f - (float)(r2 - 3) / 3f)));
					}
				}
				else
				{
					float ldm = ((body.mouthLipDist != null) ? body.mouthLipDist[i32] : 0f);
					fr2 = ((ldm <= 0.015f) ? 1f : ((ldm >= 0.025f) ? 0f : (1f - (ldm - 0.015f) / 0.01f)));
				}
				uf[i32] = fy * fr2;
				if (uf[i32] > 0.5f)
				{
					nUp++;
				}
			}
			body.mouthUpperF = uf;
			SoftFleshPlugin.Log("[SF] face: push-in clamp region (upper lip + philtrum): " + nUp.ToString() + " vertices");
			body.mouthAngR = new float[k];
			body.mouthAloR = new float[k];
			body.mouthGR = new Vector3[k];
			float[] cs = new float[k];
			float[] sn = new float[k];
			float[] al = new float[k];
			Vector3[] gg = new Vector3[k];
			float[] cs2 = new float[k];
			float[] sn2 = new float[k];
			float[] al2 = new float[k];
			Vector3[] gg2 = new Vector3[k];
			for (int q22 = 0; q22 < gN; q22++)
			{
				int i33 = body.mouthIdx[q22];
				int src2 = i33;
				if (!body.mouthIn[i33] && body.mouthPartner != null && body.mouthPartner[i33] >= 0 && body.mouthW[body.mouthPartner[i33]] > 0f)
				{
					src2 = body.mouthPartner[i33];
				}
				cs[i33] = Mathf.Cos(body.mouthAng[src2]);
				sn[i33] = Mathf.Sin(body.mouthAng[src2]);
				al[i33] = body.mouthAlo[src2];
				gg[i33] = body.mouthG[src2];
			}
			for (int pass2 = 0; pass2 < 3; pass2++)
			{
				for (int q23 = 0; q23 < gN; q23++)
				{
					int i34 = body.mouthIdx[q23];
					cs2[i34] = cs[i34];
					sn2[i34] = sn[i34];
					al2[i34] = al[i34];
					gg2[i34] = gg[i34];
					if (!body.mouthIn[i34])
					{
						float ac = 0f;
						float as2 = 0f;
						float aa = 0f;
						Vector3 ag = Vector3.zero;
						int cnt4 = 0;
						for (int k19 = nbS[i34]; k19 < nbS[i34 + 1]; k19++)
						{
							int j14 = nbL[k19];
							if (body.mouthW[j14] > 0f)
							{
								ac += cs[j14];
								as2 += sn[j14];
								aa += al[j14];
								ag += gg[j14];
								cnt4++;
							}
						}
						if (cnt4 != 0)
						{
							cs2[i34] = cs[i34] + (ac / (float)cnt4 - cs[i34]) * 0.5f;
							sn2[i34] = sn[i34] + (as2 / (float)cnt4 - sn[i34]) * 0.5f;
							al2[i34] = al[i34] + (aa / (float)cnt4 - al[i34]) * 0.5f;
							gg2[i34] = gg[i34] + (ag / (float)cnt4 - gg[i34]) * 0.5f;
						}
					}
				}
				for (int q24 = 0; q24 < gN; q24++)
				{
					int i35 = body.mouthIdx[q24];
					cs[i35] = cs2[i35];
					sn[i35] = sn2[i35];
					al[i35] = al2[i35];
					gg[i35] = gg2[i35];
				}
			}
			for (int q25 = 0; q25 < gN; q25++)
			{
				int i36 = body.mouthIdx[q25];
				body.mouthAngR[i36] = Mathf.Atan2(sn[i36], cs[i36]);
				body.mouthAloR[i36] = al[i36];
				float gm = gg[i36].magnitude;
				body.mouthGR[i36] = ((gm > 1E-06f) ? (gg[i36] / gm) : body.mouthG[i36]);
			}
			StringBuilder sbA = new StringBuilder();
			sbA.Append("axis P0 ").Append(P0.ToString("F3")).Append(" U ")
				.Append(U.ToString("F2"))
				.Append("  lining ")
				.Append(nL)
				.Append(" rim ")
				.Append(nE)
				.Append(" kMax ")
				.Append((kMax * 1000f).ToString("F0"))
				.Append("mm | sector min r (mm):");
			for (int sb2 = 0; sb2 < 24; sb2++)
			{
				float mn = float.MaxValue;
				for (int q26 = 0; q26 < gN; q26++)
				{
					if (sec[q26] == sb2 && rad[q26] < mn)
					{
						mn = rad[q26];
					}
				}
				sbA.Append(' ').Append((mn < float.MaxValue) ? (mn * 1000f).ToString("F0") : "-");
			}
			List<float> ts2 = new List<float>();
			for (int q27 = 0; q27 < gN; q27++)
			{
				if (!isLining[body.mouthIdx[q27]])
				{
					ts2.Add(body.mouthT[body.mouthIdx[q27]]);
				}
			}
			ts2.Sort();
			if (ts2.Count > 0)
			{
				sbA.Append(" | thickness pct 0/25/50/75/100 (mm): ").Append((ts2[0] * 1000f).ToString("F1")).Append('/')
					.Append((ts2[ts2.Count / 4] * 1000f).ToString("F1"))
					.Append('/')
					.Append((ts2[ts2.Count / 2] * 1000f).ToString("F1"))
					.Append('/')
					.Append((ts2[ts2.Count * 3 / 4] * 1000f).ToString("F1"))
					.Append('/')
					.Append((ts2[ts2.Count - 1] * 1000f).ToString("F1"));
			}
			body.mwAxisInfo = sbA.ToString();
			SoftFleshPlugin.Log("[SF] face: " + body.mwAxisInfo);
			body.mouthPushW = new Vector3[k];
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] face: mouth group ",
				body.mouthIdx.Length.ToString(),
				" verts (halo ",
				((body.mouthHalo != null) ? body.mouthHalo.Length : 0).ToString(),
				"), inner surface ",
				body.mwInN.ToString(),
				" (lining ",
				body.mwLiningN.ToString(),
				"), welded adjacency ",
				((body.mouthNbrList != null) ? body.mouthNbrList.Length : 0).ToString(),
				" entries"
			}));
			try
			{
				FaceBlendShape fbs = body.cha.fbsCtrl;
				FBSCtrlMouth mc = ((fbs != null) ? fbs.MouthCtrl : null);
				FBSTargetInfo[] tg = ((mc != null) ? mc.FBSTarget : null);
				if (tg != null && tg.Length != 0 && tg[0].PtnSet != null)
				{
					FieldInfo fOpen = null;
					for (int k20 = 0; k20 < tg[0].PtnSet.Length; k20++)
					{
						FBSTargetInfo.CloseOpen co = tg[0].PtnSet[k20];
						if (co != null)
						{
							if (fOpen == null)
							{
								foreach (FieldInfo fi in co.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
								{
									if (fi.FieldType == typeof(int) && fi.Name.ToLower().Contains("open"))
									{
										fOpen = fi;
										break;
									}
								}
							}
							if (fOpen == null)
							{
								SoftFleshPlugin.Log("[SF] face: CloseOpen has no int 'open' field");
								break;
							}
							if ((int)fOpen.GetValue(co) == kuwae)
							{
								body.mouthPtn = k20;
								break;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				SoftFleshPlugin.Log("[SF] face: mouth pattern lookup THREW: " + e.Message);
			}
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] face: ",
				body.mouthIdx.Length.ToString(),
				" lip verts (kuwae >3mm), kuwae shape ",
				kuwae.ToString(),
				", mouth pattern ",
				body.mouthPtn.ToString()
			}));
		}

		// Token: 0x06000119 RID: 281 RVA: 0x00044AF0 File Offset: 0x00042CF0
		private void DriveMouth(SoftFleshController.Body b)
		{
			b.mouthPushValid = false;
			this.RefreshSkinCache(b);
			if (!b.isFace || b.cha == null)
			{
				return;
			}
			if (!this.mouthAuto || b.mouthPtn < 0 || b.mouthIdx == null || b.mouthIdx.Length == 0)
			{
				this.ReleaseMouth(b);
				return;
			}
			if (b.smr == null || b.bakeV == null || b.bakeV.Count != b.baseVerts.Length)
			{
				return;
			}
			Matrix4x4 l2w = SoftFleshController.BakeL2W(b.smr);
			Vector3 c = Vector3.zero;
			for (int q = 0; q < b.mouthIdx.Length; q++)
			{
				c += b.bakeV[b.mouthIdx[q]];
			}
			c = l2w.MultiplyPoint3x4(c / (float)b.mouthIdx.Length);
			b.mouthC = c;
			b.mouthCValid = true;
			float bestClear = float.MaxValue;
			float bestR = 0f;
			int bestP = -1;
			int p = 0;
			while (p < this._pressers.Count && this._segLen2 != null && p < this._segLen2.Length)
			{
				if (this._segItem[p] && this._segInsert[p] && this._segLen2[p] >= 0f)
				{
					Vector3 pa = this._segA[p];
					Vector3 ab = this._segAB[p];
					float t = ((this._segLen2[p] > 1E-12f) ? (Vector3.Dot(c - pa, ab) / this._segLen2[p]) : 0f);
					if (t < 0f)
					{
						t = 0f;
					}
					else if (t > 1f)
					{
						t = 1f;
					}
					float d = (pa + ab * t - c).magnitude;
					float r = this._segRad[p] + (this._segRadB[p] - this._segRad[p]) * t;
					if (d - r < bestClear)
					{
						bestClear = d - r;
						bestR = r;
						bestP = p;
					}
				}
				p++;
			}
			float now = Time.unscaledTime;
			if (bestP >= 0 && bestClear < this.mouthReach)
			{
				Vector3 paB = this._segA[bestP];
				Vector3 abB = this._segAB[bestP];
				float l2B = this._segLen2[bestP];
				float tB0 = ((l2B > 1E-12f) ? (Vector3.Dot(c - paB, abB) / l2B) : 0f);
				if (tB0 < 0f)
				{
					tB0 = 0f;
				}
				else if (tB0 > 1f)
				{
					tB0 = 1f;
				}
				b.mouthAxP = paB + abB * tB0;
				Transform ownerB = ((this._segOwner != null && bestP < this._segOwner.Length) ? this._segOwner[bestP] : null);
				Vector3 cNear = b.mouthAxP;
				Vector3 cFar = b.mouthAxP;
				float dNear = float.MaxValue;
				float dFar = -1f;
				if (ownerB != null)
				{
					int q2 = 0;
					while (q2 < this._pressers.Count && q2 < this._segLen2.Length)
					{
						if (this._segOwner != null && !(this._segOwner[q2] != ownerB) && this._segLen2[q2] >= 0f && this._segInsert[q2])
						{
							Vector3 cq = this._segA[q2] + this._segAB[q2] * 0.5f;
							float dq = (cq - c).sqrMagnitude;
							if (dq < dNear)
							{
								dNear = dq;
								cNear = cq;
							}
							if (dq > dFar)
							{
								dFar = dq;
								cFar = cq;
							}
						}
						q2++;
					}
				}
				Vector3 axD = cFar - cNear;
				if (axD.sqrMagnitude < 1E-06f)
				{
					axD = abB;
				}
				if (axD.sqrMagnitude < 1E-08f)
				{
					axD = Vector3.forward;
				}
				b.mouthAxU = axD.normalized;
				b.mouthOwner = ownerB;
				b.mouthR = bestR;
				this.BuildChain(ownerB, b.mouthAxU, cNear);
				Vector3 axL;
				if (this.ChainDirAt(c, out axL) && Vector3.Dot(axL, b.mouthAxU) < 0f)
				{
					axL = -axL;
				}
				if (axL.sqrMagnitude > 0.5f)
				{
					b.mouthAxU = axL;
				}
				float rMeanC;
				float clearC;
				if (this.ItemMeanRadiusAt(c, out rMeanC, out clearC))
				{
					bestR = rMeanC;
					bestClear = clearC;
					b.mouthR = bestR;
				}
				float openEst = 2f * bestR * this.mouthScale / 0.032f;
				float openF0 = ((b.mouthOpenNow > 0f) ? b.mouthOpenNow : openEst);
				b.mouthBinP = -1;
				if (b.mouthDv != null && b.skinMCache != null && b.bwArr != null && b.mouthW != null)
				{
					if (b.mouthBinR == null || b.mouthBinR.Length != 24)
					{
						b.mouthBinR = new float[24];
					}
					for (int i = 0; i < 24; i++)
					{
						b.mouthBinR[i] = -1f;
					}
					Vector3 axU = b.mouthAxU;
					Vector3 u0 = Vector3.Cross(axU, Vector3.up);
					if (u0.sqrMagnitude < 1E-08f)
					{
						u0 = Vector3.Cross(axU, Vector3.right);
					}
					u0.Normalize();
					Vector3 v0 = Vector3.Cross(axU, u0);
					float openF = openF0;
					int nbM = b.skinMCache.Length;
					int filled = 0;
					for (int q3 = 0; q3 < b.mouthIdx.Length; q3++)
					{
						int iq = b.mouthIdx[q3];
						if (b.mouthCore != null && b.mouthCore[iq])
						{
							Vector3 pbind = b.baseVerts[iq] + b.mouthDv[iq] * openF;
							BoneWeight w = b.bwArr[iq];
							Vector3 wq = Vector3.zero;
							if (w.weight0 > 0f && w.boneIndex0 < nbM)
							{
								wq += b.skinMCache[w.boneIndex0].MultiplyPoint3x4(pbind) * w.weight0;
							}
							if (w.weight1 > 0f && w.boneIndex1 < nbM)
							{
								wq += b.skinMCache[w.boneIndex1].MultiplyPoint3x4(pbind) * w.weight1;
							}
							if (w.weight2 > 0f && w.boneIndex2 < nbM)
							{
								wq += b.skinMCache[w.boneIndex2].MultiplyPoint3x4(pbind) * w.weight2;
							}
							if (w.weight3 > 0f && w.boneIndex3 < nbM)
							{
								wq += b.skinMCache[w.boneIndex3].MultiplyPoint3x4(pbind) * w.weight3;
							}
							Vector3 rel = wq - b.mouthAxP;
							float along = Vector3.Dot(rel, axU);
							Vector3 rr = rel - axU * along;
							float rad = rr.magnitude;
							if (rad >= 1E-05f)
							{
								int bin = (int)((Mathf.Atan2(Vector3.Dot(rr, v0), Vector3.Dot(rr, u0)) + 3.1415927f) * 3.8197186f);
								if (bin < 0)
								{
									bin = 0;
								}
								else if (bin > 23)
								{
									bin = 23;
								}
								if (b.mouthBinR[bin] < 0f)
								{
									filled++;
								}
								if (b.mouthBinR[bin] < 0f || rad < b.mouthBinR[bin])
								{
									b.mouthBinR[bin] = rad;
								}
							}
						}
					}
					float tRest = 0f;
					float tNow = 0f;
					int tN = 0;
					float[] binMaxR = new float[24];
					float[] binMinC = new float[24];
					float[] binMaxC = new float[24];
					for (int j = 0; j < 24; j++)
					{
						binMaxR[j] = -1f;
						binMinC[j] = -1f;
						binMaxC[j] = -1f;
					}
					for (int q4 = 0; q4 < b.mouthIdx.Length; q4++)
					{
						int iq2 = b.mouthIdx[q4];
						if (b.mouthCore != null && b.mouthCore[iq2])
						{
							Vector3 pbind2 = b.baseVerts[iq2] + b.mouthDv[iq2] * openF;
							BoneWeight w2 = b.bwArr[iq2];
							Vector3 wq2 = Vector3.zero;
							if (w2.weight0 > 0f && w2.boneIndex0 < nbM)
							{
								wq2 += b.skinMCache[w2.boneIndex0].MultiplyPoint3x4(pbind2) * w2.weight0;
							}
							if (w2.weight1 > 0f && w2.boneIndex1 < nbM)
							{
								wq2 += b.skinMCache[w2.boneIndex1].MultiplyPoint3x4(pbind2) * w2.weight1;
							}
							if (w2.weight2 > 0f && w2.boneIndex2 < nbM)
							{
								wq2 += b.skinMCache[w2.boneIndex2].MultiplyPoint3x4(pbind2) * w2.weight2;
							}
							if (w2.weight3 > 0f && w2.boneIndex3 < nbM)
							{
								wq2 += b.skinMCache[w2.boneIndex3].MultiplyPoint3x4(pbind2) * w2.weight3;
							}
							Vector3 rel2 = wq2 - b.mouthAxP;
							Vector3 rr2 = rel2 - axU * Vector3.Dot(rel2, axU);
							float rad2 = rr2.magnitude;
							if (rad2 >= 1E-05f)
							{
								int bin2 = (int)((Mathf.Atan2(Vector3.Dot(rr2, v0), Vector3.Dot(rr2, u0)) + 3.1415927f) * 3.8197186f);
								if (bin2 < 0)
								{
									bin2 = 0;
								}
								else if (bin2 > 23)
								{
									bin2 = 23;
								}
								if (rad2 > binMaxR[bin2])
								{
									binMaxR[bin2] = rad2;
								}
								Vector3 wc = l2w.MultiplyPoint3x4(b.bakeV[iq2]);
								Vector3 relc = wc - b.mouthAxP;
								float radc = (relc - axU * Vector3.Dot(relc, axU)).magnitude;
								if (binMinC[bin2] < 0f || radc < binMinC[bin2])
								{
									binMinC[bin2] = radc;
								}
								if (radc > binMaxC[bin2])
								{
									binMaxC[bin2] = radc;
								}
							}
						}
					}
					for (int k = 0; k < 24; k++)
					{
						if (b.mouthBinR[k] >= 0f && binMaxR[k] >= 0f && binMinC[k] >= 0f)
						{
							tRest += binMaxR[k] - b.mouthBinR[k];
							tNow += binMaxC[k] - binMinC[k];
							tN++;
						}
					}
					b.mouthThickRest = ((tN > 0) ? (tRest / (float)tN) : (-1f));
					b.mouthThickNow = ((tN > 0) ? (tNow / (float)tN) : (-1f));
					if (b.mouthBinNow == null || b.mouthBinNow.Length != 24)
					{
						b.mouthBinNow = new float[24];
					}
					for (int l = 0; l < 24; l++)
					{
						b.mouthBinNow[l] = binMinC[l];
					}
					for (int m = 0; m < 24; m++)
					{
						if (b.mouthBinNow[m] < 0f)
						{
							for (int d2 = 1; d2 < 24; d2++)
							{
								int a2 = (m + d2) % 24;
								int b2 = (m - d2 + 24) % 24;
								if (b.mouthBinNow[a2] >= 0f)
								{
									b.mouthBinNow[m] = b.mouthBinNow[a2];
									break;
								}
								if (b.mouthBinNow[b2] >= 0f)
								{
									b.mouthBinNow[m] = b.mouthBinNow[b2];
									break;
								}
							}
						}
					}
					if (filled >= 4)
					{
						for (int n = 0; n < 24; n++)
						{
							if (b.mouthBinR[n] < 0f)
							{
								for (int d3 = 1; d3 < 24; d3++)
								{
									int a3 = (n + d3) % 24;
									int b3 = (n - d3 + 24) % 24;
									if (b.mouthBinR[a3] >= 0f)
									{
										b.mouthBinR[n] = b.mouthBinR[a3];
										break;
									}
									if (b.mouthBinR[b3] >= 0f)
									{
										b.mouthBinR[n] = b.mouthBinR[b3];
										break;
									}
								}
							}
						}
						b.mouthBinU = u0;
						b.mouthBinV = v0;
						b.mouthBinP = bestP;
					}
				}
				float aperture = -1f;
				if (b.mouthBinP == bestP && b.mouthBinR != null)
				{
					float apS = 0f;
					int apN = 0;
					for (int k2 = 0; k2 < b.mouthBinR.Length; k2++)
					{
						if (b.mouthBinR[k2] >= 0f)
						{
							apS += b.mouthBinR[k2];
							apN++;
						}
					}
					if (apN > 0)
					{
						aperture = apS / (float)apN;
					}
				}
				float open;
				if (b.mouthOpenNow < 0f || aperture < 0f)
				{
					open = openEst;
				}
				else
				{
					float rT = ((b.mwRTarget > 0f) ? (b.mwRTarget + (bestR - b.mwRTarget) * 0.25f) : bestR);
					b.mwRTarget = rT;
					float err = rT * this.mouthScale - aperture;
					float step = err / 0.032f * 0.3f;
					if (step > 0.02f)
					{
						step = 0.02f;
					}
					else if (step < -0.02f)
					{
						step = -0.02f;
					}
					open = b.mouthOpenNow + step;
				}
				if (open < 0.15f)
				{
					open = 0.15f;
				}
				else if (open > 1f)
				{
					open = 1f;
				}
				b.mouthApert = aperture;
				try
				{
					if (!b.mouthDriven)
					{
						b.mouthOrigPtn = b.cha.fileStatus.mouthPtn;
						b.mouthOrigOpen = b.cha.fileStatus.mouthOpenMax;
						b.mouthDriven = true;
						b.mouthOpenNow = -1f;
						b.cha.ChangeMouthPtn(b.mouthPtn, true);
					}
					b.mwOpenDelta = open - b.mouthOpenNow;
					if (Mathf.Abs(open - b.mouthOpenNow) > 0.002f)
					{
						b.cha.ChangeMouthOpenMax(open);
						b.mouthOpenNow = open;
					}
				}
				catch (Exception e)
				{
					b.mouthInfo = "mouth drive THREW: " + e.Message;
					return;
				}
				b.mouthLastSeen = now;
				this.MouthWrap(b, b.mouthOwner, (b.mouthOpenNow > 0f) ? b.mouthOpenNow : open);
				b.mouthInfo = string.Concat(new string[]
				{
					"kuwae ",
					(open * 100f).ToString("F0"),
					"%  item r ",
					(bestR * 1000f).ToString("F0"),
					"mm  lip aperture ",
					(b.mouthApert * 1000f).ToString("F0"),
					"mm  clearance ",
					(bestClear * 1000f).ToString("F0"),
					"mm  wrap pen max ",
					(b.mouthPenMax * 1000f).ToString("F1"),
					"mm  roll thick rest ",
					(b.mouthThickRest * 1000f).ToString("F1"),
					" / now ",
					(b.mouthThickNow * 1000f).ToString("F1"),
					"mm  sectors ",
					b.mouthBinP.ToString()
				});
			}
			else if (b.mouthDriven && now - b.mouthLastSeen > 0.5f)
			{
				b.mouthBinP = -1;
				b.mouthOwner = null;
				b.mouthPushValid = false;
				this.ReleaseMouth(b);
			}
			if (bestP < 0 || bestClear >= this.mouthReach)
			{
				b.mouthPushValid = false;
				return;
			}
			if (!b.mouthDriven)
			{
				b.mouthInfo = ((bestP >= 0) ? string.Concat(new string[]
				{
					"nearest item clearance ",
					(bestClear * 1000f).ToString("F0"),
					"mm (reach ",
					(this.mouthReach * 1000f).ToString("F0"),
					")"
				}) : "no insert-mode item");
			}
		}

		// Token: 0x0600011A RID: 282 RVA: 0x00045BD4 File Offset: 0x00043DD4
		private void MouthWrap(SoftFleshController.Body b, Transform owner, float openF)
		{
			b.mouthPushValid = false;
			if (owner == null || b.mouthIdx == null || b.mouthPushW == null || b.mouthDv == null || b.skinMCache == null || b.bwArr == null || this._segOwner == null || b.mouthIn == null || b.mouthT == null || b.mouthG == null || b.mouthNbrStart == null || b.mouthHalo == null || b.boneRotCache == null)
			{
				return;
			}
			int nV = b.baseVerts.Length;
			if (this._mwR == null || this._mwR.Length < nV)
			{
				this._mwR = new Vector3[nV];
				this._mwP = new Vector3[nV];
				this._mwP2 = new Vector3[nV];
				this._mwSR = new Vector3[nV];
				this._mwGW = new Vector3[nV];
				this._mwGW2 = new Vector3[nV];
				this._mwPen = new float[nV];
				this._mwDir = new Vector3[nV];
				this._mwDraw = new float[nV];
				this._mwSeg = new int[nV];
				this._mwRA = new float[nV];
				this._mwRB = new float[nV];
				this._mwClr = new float[nV];
				this._gNext = new int[nV];
				this._mwShell = new float[nV];
				this._mwReq = new float[nV];
				this._mwShellTmp = new float[nV];
				this._mwUdir = new Vector3[nV];
				this._mwUnder = new float[nV];
				this._mwOffr = new float[nV];
				this._mwRB2 = new float[nV];
				this._mwClrFinal = new float[nV];
				this._mwDbgLobe = new int[nV];
				this._mwDbgRB = new float[nV];
				this._mwDbgDNow = new float[nV];
				this._mwDbgTarget = new float[nV];
				this._mwDbgFace = new float[nV];
				this._mwDbgDetail = new float[nV];
				this._mwS1 = new Vector3[nV];
				this._mwS2 = new Vector3[nV];
				this._mwGR = new Vector3[nV];
				this._mwPenPrev = new float[nV];
				this._mwDragV = new Vector3[nV];
				this._mwRingF = new float[nV];
				this._mwOutPrev = new Vector3[nV];
				this._mwFPrev = new float[nV];
				this._mwRPrev = new Vector3[nV];
				this._mwS1Prev = new Vector3[nV];
				this._mwS2Prev = new Vector3[nV];
				this._mwDragVPrev = new Vector3[nV];
				this._mwAngOwn = new float[nV];
				this._mwAloOwn = new float[nV];
				this._mwAngW = new float[nV];
				this._mwAloW = new float[nV];
				this._mwAcc = new float[nV];
				this._mwAsc = new float[nV];
				this._mwAcc2 = new float[nV];
				this._mwAsc2 = new float[nV];
				this._mwAloW2 = new float[nV];
				this._mwN = new Vector3[nV];
				this._mwNR = new Vector3[nV];
				this._mwLift = new float[nV];
				this._mwLift2 = new float[nV];
				this._mwLiftRaw = new float[nV];
			}
			Vector3[] R = this._mwR;
			Vector3[] P = this._mwP;
			Vector3[] P2 = this._mwP2;
			Vector3[] SR = this._mwSR;
			Vector3[] GW = this._mwGW;
			double sig = (double)openF * 1000.0 + (double)this.lipSmooth * 3.0 + (double)this.itemRadScale * 11.0 + (double)this._chN * 0.37 + (double)b.mwDrag * 9137.0 + (this.mouthTiltFollow ? 5.0 : 0.0) + (double)this.lipDrag * 313.0 + (double)this.lipReturn * 17.0 + (this.lipNoReturn ? 7.0 : 0.0) + (double)this.lipDamp * 431.0 + (double)this.tiltFollow * 271.0 + (double)this.lipPushIn * 3701.0;
			for (int i = 0; i < this._chN; i++)
			{
				sig += ((double)this._chP[i].x * 1.7 + (double)this._chP[i].y * 2.3 + (double)this._chP[i].z * 3.1) * 100000.0 + (double)this._chR[i] * 40000.0;
			}
			sig += ((double)b.mouthC.x * 3.7 + (double)b.mouthC.y * 4.1 + (double)b.mouthC.z * 5.3) * 100000.0;
			int jb = ((b.domBone != null && b.mouthIdx.Length != 0) ? b.domBone[b.mouthIdx[0]] : (-1));
			if (jb >= 0 && jb < b.skinMCache.Length)
			{
				Matrix4x4 j = b.skinMCache[jb];
				for (int e = 0; e < 16; e++)
				{
					sig += (double)j[e] * (100000.0 + (double)e * 1234.5);
				}
			}
			if (b.mwSig != 0.0 && Math.Abs(sig - b.mwSig) < 0.5 && b.mouthPushW != null)
			{
				b.mouthPushValid = true;
				b.mwSkipped = true;
				b.mwSkipRun++;
				return;
			}
			b.mwSig = 0.0;
			b.mwSkipped = false;
			b.mwSkipRun = 0;
			this._mwSigPending = sig;
			b.mouthPenMax = 0f;
			b.mwInside = 0;
			b.mwCarried = 0;
			b.mwFinIn = 0;
			b.mwFinOut = 0;
			b.mwFinInMax = 0f;
			b.mwFinOutMax = 0f;
			long tw0 = SoftFleshController._sw.ElapsedTicks;
			double tickMs = 1000.0 / (double)Stopwatch.Frequency;
			long tw = tw0;
			float tension = 0f;
			int[] idx = b.mouthIdx;
			int[] halo = b.mouthHalo;
			foreach (int k in idx)
			{
				R[k] = this.SkinRest(b, k, openF);
				P[k] = R[k];
				SR[k] = R[k];
				int db = ((b.domBone != null) ? b.domBone[k] : (-1));
				GW[k] = ((db >= 0 && db < b.boneRotCache.Length) ? (b.boneRotCache[db] * b.mouthG[k]) : b.mouthG[k]);
				Vector3 gr = ((b.mouthGR != null) ? b.mouthGR[k] : b.mouthG[k]);
				this._mwGR[k] = ((db >= 0 && db < b.boneRotCache.Length) ? (b.boneRotCache[db] * gr) : gr);
				this._mwPenPrev[k] = ((this._mwSeg[k] < 0) ? (-1f) : this._mwPen[k]);
				this._mwDragV[k] = Vector3.zero;
				this._mwPen[k] = 0f;
				this._mwDir[k] = Vector3.zero;
			}
			foreach (int l in halo)
			{
				R[l] = this.SkinRest(b, l, openF);
				P[l] = R[l];
				SR[l] = R[l];
			}
			b.mwMs2[0] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw) * tickMs);
			tw = SoftFleshController._sw.ElapsedTicks;
			Vector3 cL = Vector3.zero;
			Vector3 cR = Vector3.zero;
			int nL2 = 0;
			int nR2 = 0;
			foreach (int m in idx)
			{
				if (b.mouthLining != null && b.mouthLining[m])
				{
					cL += R[m];
					nL2++;
				}
				else if (b.mouthIn[m])
				{
					cR += R[m];
					nR2++;
				}
			}
			Vector3 o = ((nL2 > 0 && nR2 > 0) ? (cR / (float)nR2 - cL / (float)nL2) : b.mouthAxU);
			Vector3 ax = b.mouthAxU;
			if (Vector3.Dot(ax, o) < 0f)
			{
				ax = -ax;
			}
			ax = ((ax.sqrMagnitude > 1E-08f) ? ax.normalized : Vector3.zero);
			b.mwFaceOut = ((o.sqrMagnitude > 1E-08f) ? o.normalized : ax);
			if (b.mwAxSmooth.sqrMagnitude > 1E-08f && Vector3.Dot(b.mwAxSmooth, ax) > 0f)
			{
				ax = (b.mwAxSmooth + (ax - b.mwAxSmooth) * 0.25f).normalized;
			}
			b.mwAxSmooth = ax;
			b.mouthOutW = ax;
			b.mwDepthO = ((nR2 > 0) ? (cR / (float)nR2) : ((nL2 > 0) ? (cL / (float)nL2) : b.mouthAxP));
			Vector3 A = b.mouthAxP;
			Vector3 U = b.mouthOutW;
			bool useItem = this.mouthTiltFollow && U.sqrMagnitude > 1E-08f;
			Vector3 refR = ((b.cha != null) ? b.cha.transform.right : Vector3.right);
			Vector3 U2 = refR - U * Vector3.Dot(refR, U);
			if (U2.sqrMagnitude < 1E-08f)
			{
				refR = ((b.cha != null) ? b.cha.transform.up : Vector3.up);
				U2 = refR - U * Vector3.Dot(refR, U);
			}
			if (U2.sqrMagnitude < 1E-08f)
			{
				U2 = Vector3.right;
			}
			U2.Normalize();
			Vector3 V0 = Vector3.Cross(U, U2);
			foreach (int n in idx)
			{
				if (!useItem)
				{
					this._mwAngOwn[n] = b.mouthAng[n];
					this._mwAloOwn[n] = b.mouthAlo[n];
					this._mwAngW[n] = ((b.mouthAngR != null) ? b.mouthAngR[n] : b.mouthAng[n]);
					this._mwAloW[n] = ((b.mouthAloR != null) ? b.mouthAloR[n] : b.mouthAlo[n]);
				}
				else
				{
					Vector3 rel = R[n] - A;
					float al = Vector3.Dot(rel, U);
					Vector3 rr = rel - U * al;
					float rm = rr.magnitude;
					this._mwAloOwn[n] = Vector3.Dot(R[n] - b.mwDepthO, U);
					this._mwAngOwn[n] = ((rm > 1E-06f) ? Mathf.Atan2(Vector3.Dot(rr, V0), Vector3.Dot(rr, U2)) : 0f);
					if (rm > 1E-06f)
					{
						Vector3 itemDir = -rr / rm;
						Vector3 mouthDir = this._mwGR[n];
						float fI = (rm - 0.003f) / 0.006f;
						if (fI < 0f)
						{
							fI = 0f;
						}
						else if (fI > 1f)
						{
							fI = 1f;
						}
						Vector3 mix = mouthDir + (itemDir - mouthDir) * fI;
						float mm = mix.magnitude;
						this._mwGR[n] = ((mm > 1E-06f) ? (mix / mm) : mouthDir);
					}
				}
			}
			if (useItem)
			{
				int[] nbS2 = b.mouthNbrStart;
				int[] nbL2 = b.mouthNbrList;
				foreach (int i2 in idx)
				{
					int src = i2;
					if (!b.mouthIn[i2] && b.mouthPartner != null && b.mouthPartner[i2] >= 0 && b.mouthW[b.mouthPartner[i2]] > 0f)
					{
						src = b.mouthPartner[i2];
					}
					this._mwAcc[i2] = Mathf.Cos(this._mwAngOwn[src]);
					this._mwAsc[i2] = Mathf.Sin(this._mwAngOwn[src]);
					this._mwAloW[i2] = this._mwAloOwn[src];
				}
				for (int pass = 0; pass < 3; pass++)
				{
					foreach (int i3 in idx)
					{
						this._mwAcc2[i3] = this._mwAcc[i3];
						this._mwAsc2[i3] = this._mwAsc[i3];
						this._mwAloW2[i3] = this._mwAloW[i3];
						if (!b.mouthIn[i3])
						{
							float ac = 0f;
							float as2 = 0f;
							float aa = 0f;
							int cnt = 0;
							for (int k2 = nbS2[i3]; k2 < nbS2[i3 + 1]; k2++)
							{
								int j2 = nbL2[k2];
								if (b.mouthW[j2] > 0f)
								{
									ac += this._mwAcc[j2];
									as2 += this._mwAsc[j2];
									aa += this._mwAloW[j2];
									cnt++;
								}
							}
							if (cnt != 0)
							{
								this._mwAcc2[i3] = this._mwAcc[i3] + (ac / (float)cnt - this._mwAcc[i3]) * 0.5f;
								this._mwAsc2[i3] = this._mwAsc[i3] + (as2 / (float)cnt - this._mwAsc[i3]) * 0.5f;
								this._mwAloW2[i3] = this._mwAloW[i3] + (aa / (float)cnt - this._mwAloW[i3]) * 0.5f;
							}
						}
					}
					foreach (int i4 in idx)
					{
						this._mwAcc[i4] = this._mwAcc2[i4];
						this._mwAsc[i4] = this._mwAsc2[i4];
						this._mwAloW[i4] = this._mwAloW2[i4];
					}
				}
				foreach (int i5 in idx)
				{
					this._mwAngW[i5] = Mathf.Atan2(this._mwAsc[i5], this._mwAcc[i5]);
				}
				Vector3[] gB = this._mwP2;
				for (int pass2 = 0; pass2 < 3; pass2++)
				{
					foreach (int i6 in idx)
					{
						Vector3 acc = Vector3.zero;
						int cnt2 = 0;
						for (int k3 = nbS2[i6]; k3 < nbS2[i6 + 1]; k3++)
						{
							int j3 = nbL2[k3];
							if (b.mouthW[j3] > 0f)
							{
								acc += this._mwGR[j3];
								cnt2++;
							}
						}
						gB[i6] = ((cnt2 > 0) ? (this._mwGR[i6] + (acc / (float)cnt2 - this._mwGR[i6]) * 0.5f) : this._mwGR[i6]);
					}
					foreach (int i7 in idx)
					{
						float m2 = gB[i7].magnitude;
						this._mwGR[i7] = ((m2 > 1E-06f) ? (gB[i7] / m2) : this._mwGR[i7]);
					}
				}
			}
			b.mwMs2[1] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw) * tickMs);
			tw = SoftFleshController._sw.ElapsedTicks;
			b.mwDragNow = 0f;
			if (owner != null && b.mouthOutW.sqrMagnitude > 1E-08f && this.lipDrag > 0f)
			{
				float depth = Vector3.Dot(b.mwDepthO - owner.position, b.mouthOutW);
				float tNow = Time.time;
				float dt = ((b.mwDragT > 0f) ? (tNow - b.mwDragT) : 0f);
				b.mwDragT = tNow;
				if (dt > 0.0005f && dt < 0.5f && b.mwDepthPrev > -100000000f)
				{
					float dd = depth - b.mwDepthPrev;
					if (dd > 0.02f || dd < -0.02f)
					{
						dd = 0f;
					}
					b.mwStick += dd;
					if (b.mwStick > this.lipPushIn)
					{
						b.mwStick = this.lipPushIn;
					}
					else if (b.mwStick < -this.lipDrag)
					{
						b.mwStick = -this.lipDrag;
					}
					float tr = ((this.lipReturn < 0.01f) ? 0.01f : this.lipReturn);
					if (!this.lipNoReturn)
					{
						b.mwStick *= Mathf.Exp(-dt / tr);
					}
					float td = ((this.lipDamp < 0.005f) ? 0.005f : this.lipDamp);
					b.mwDrag += (b.mwStick - b.mwDrag) * (1f - Mathf.Exp(-dt / td));
				}
				else if (dt >= 0.5f)
				{
					b.mwDrag = 0f;
					b.mwStick = 0f;
					b.mwDepthPrev = -1E+09f;
				}
				b.mwDepthPrev = depth;
				if (b.mwDrag > -1E-05f && b.mwDrag < 1E-05f)
				{
					b.mwDrag = 0f;
				}
				b.mwDragNow = b.mwDrag;
			}
			else
			{
				b.mwDrag = 0f;
				b.mwStick = 0f;
				b.mwDepthPrev = -1E+09f;
				b.mwDragT = 0f;
			}
			float tG = Time.time;
			b.mwSolveGap = ((b.mwSolveT > 0f) ? (tG - b.mwSolveT) : 1f);
			b.mwSolveT = tG;
			if (b.mwDragNow != 0f)
			{
				Vector3 dv = -b.mouthOutW * b.mwDragNow;
				float[] fD = this._mwLift2;
				float[] fD2 = this._mwLiftRaw;
				int[] nbSd = b.mouthNbrStart;
				int[] nbLd = b.mouthNbrList;
				float aF = ((b.mwSolveGap > 0f && b.mwSolveGap < 0.3f) ? (1f - Mathf.Exp(-b.mwSolveGap / 0.08f)) : 1f);
				foreach (int i8 in idx)
				{
					float pp = this._mwPenPrev[i8];
					float f = ((pp <= -0.9f) ? 0f : ((pp + 0.01f) / 0.01f));
					f = ((f < 0f) ? 0f : ((f > 1f) ? 1f : f));
					f = this._mwFPrev[i8] + (f - this._mwFPrev[i8]) * aF;
					this._mwFPrev[i8] = f;
					float fl = 1f;
					if (!b.mouthIn[i8] && b.mouthLipDist != null)
					{
						float ldm = b.mouthLipDist[i8];
						fl = ((ldm <= 0.01f) ? 1f : ((ldm >= 0.02f) ? 0f : (1f - (ldm - 0.01f) / 0.01f)));
					}
					fD[i8] = f * b.mouthW[i8] * fl;
				}
				if (b.mouthPartner != null)
				{
					foreach (int i9 in idx)
					{
						if (!b.mouthIn[i9])
						{
							int pj = b.mouthPartner[i9];
							if (pj >= 0 && b.mouthW[pj] > 0f && fD[pj] > fD[i9] && (R[pj] - R[i9]).sqrMagnitude < 6.400001E-05f)
							{
								fD[i9] = fD[pj];
							}
						}
					}
				}
				for (int pass3 = 0; pass3 < 2; pass3++)
				{
					foreach (int i10 in idx)
					{
						float acc2 = 0f;
						int cnt3 = 0;
						for (int k4 = nbSd[i10]; k4 < nbSd[i10 + 1]; k4++)
						{
							int j4 = nbLd[k4];
							if (b.mouthW[j4] > 0f)
							{
								acc2 += fD[j4];
								cnt3++;
							}
						}
						fD2[i10] = ((cnt3 > 0) ? (fD[i10] + (acc2 / (float)cnt3 - fD[i10]) * 0.5f) : fD[i10]);
					}
					foreach (int i11 in idx)
					{
						fD[i11] = fD2[i11];
					}
				}
				foreach (int i12 in idx)
				{
					if (fD[i12] > 0f)
					{
						this._mwDragV[i12] = dv * fD[i12];
						R[i12] += this._mwDragV[i12];
						P[i12] = R[i12];
						SR[i12] = R[i12];
					}
				}
			}
			b.mwCollarN = 0;
			b.mwCollarMax = 0f;
			float gMax = -1f;
			foreach (int i13 in idx)
			{
				if (b.mouthIn[i13])
				{
					float pp2 = this._mwPenPrev[i13];
					if (pp2 > -0.9f && pp2 > gMax)
					{
						gMax = pp2;
					}
				}
			}
			float g = ((gMax <= -0.9f) ? 0f : ((gMax + 0.003f) / 0.006f));
			if (g < 0f)
			{
				g = 0f;
			}
			else if (g > 1f)
			{
				g = 1f;
			}
			float aG = ((b.mwSolveGap > 0f && b.mwSolveGap < 0.3f) ? (1f - Mathf.Exp(-b.mwSolveGap / 0.08f)) : 1f);
			b.mwCollarEng += (g - b.mwCollarEng) * aG;
			if (this.mouthTiltFollow && this.tiltFollow > 0f && b.mwCollarEng > 0.001f && b.mouthOutW.sqrMagnitude > 0.5f)
			{
				Vector3 oW = b.mouthOutW;
				float kT = this.tiltFollow * b.mwCollarEng;
				foreach (int i14 in idx)
				{
					float f2 = this.LipFactor(b, i14);
					if (f2 > 0f)
					{
						float along = Vector3.Dot(R[i14] - this._mwDragV[i14] - b.mwDepthO, oW);
						float sh = -along * kT * f2 * b.mouthW[i14];
						if (sh <= -1E-05f || sh >= 1E-05f)
						{
							Vector3 v = oW * sh;
							this._mwDragV[i14] += v;
							R[i14] += v;
							P[i14] = R[i14];
							SR[i14] = R[i14];
							b.mwCollarN++;
							float am = ((sh < 0f) ? (-sh) : sh);
							if (am > b.mwCollarMax)
							{
								b.mwCollarMax = am;
							}
						}
					}
				}
			}
			this._ceOutW = b.mouthOutW;
			this._ceIn = -b.mwFaceOut;
			this._ceCap = this.lipPushIn;
			this._ceRedir = 0;
			b.mwMs2[2] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw) * tickMs);
			tw = SoftFleshController._sw.ElapsedTicks;
			this.MouthConstrain(b, P, GW, owner, 0.001f, true);
			foreach (int i15 in idx)
			{
				P[i15] = R[i15];
			}
			b.mwMs2[3] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw) * tickMs);
			tw = SoftFleshController._sw.ElapsedTicks;
			b.mwExposed = 0;
			b.mwSunk = 0;
			float[] f3 = this._mwRB2;
			int[] nbS3 = b.mouthNbrStart;
			int[] nbL3 = b.mouthNbrList;
			float[] tmp2 = this._mwShellTmp;
			bool gridO = this.GridBuildSel(b, P, idx, 0);
			foreach (int i16 in idx)
			{
				f3[i16] = 0f;
				this._mwOffr[i16] = 0.001f;
				if (b.mouthIn[i16] && (b.mouthLining == null || !b.mouthLining[i16]) && (b.mouthWet == null || !b.mouthWet[i16]))
				{
					bool covered = false;
					Vector3 pi = P[i16];
					if (gridO)
					{
						int cx = Mathf.FloorToInt((pi.x - this._gMin.x) / 0.008f);
						int cy = Mathf.FloorToInt((pi.y - this._gMin.y) / 0.008f);
						int cz = Mathf.FloorToInt((pi.z - this._gMin.z) / 0.008f);
						for (int dx = -1; dx <= 1; dx++)
						{
							if (covered)
							{
								break;
							}
							int dy = -1;
							while (dy <= 1 && !covered)
							{
								int dz = -1;
								while (dz <= 1 && !covered)
								{
									int x = cx + dx;
									int y = cy + dy;
									int z = cz + dz;
									if (x >= 0 && y >= 0 && z >= 0 && x < this._gNx && y < this._gNy && z < this._gNz)
									{
										for (int j5 = this._gHead[(x * this._gNy + y) * this._gNz + z]; j5 >= 0; j5 = this._gNext[j5])
										{
											Vector3 dj = P[j5] - pi;
											if (dj.sqrMagnitude <= 2.5E-05f && Vector3.Dot(dj, b.mouthOutW) > 0f)
											{
												covered = true;
												break;
											}
										}
									}
									dz++;
								}
								dy++;
							}
						}
					}
					else
					{
						int q20 = 0;
						while (q20 < idx.Length && !covered)
						{
							int j6 = idx[q20];
							if (!b.mouthIn[j6])
							{
								Vector3 dj2 = P[j6] - pi;
								if (dj2.sqrMagnitude <= 2.5E-05f && Vector3.Dot(dj2, b.mouthOutW) > 0f)
								{
									covered = true;
								}
							}
							q20++;
						}
					}
					if (!covered)
					{
						f3[i16] = 1f;
						b.mwExposed++;
					}
				}
			}
			for (int pass4 = 0; pass4 < 2; pass4++)
			{
				foreach (int i17 in idx)
				{
					tmp2[i17] = f3[i17];
					if (b.mouthIn[i17] && (b.mouthLining == null || !b.mouthLining[i17]) && (b.mouthWet == null || !b.mouthWet[i17]))
					{
						float acc3 = 0f;
						int cnt4 = 0;
						for (int k5 = nbS3[i17]; k5 < nbS3[i17 + 1]; k5++)
						{
							int j7 = nbL3[k5];
							if (b.mouthW[j7] > 0f && b.mouthIn[j7] && (b.mouthLining == null || !b.mouthLining[j7]) && (b.mouthWet == null || !b.mouthWet[j7]))
							{
								acc3 += f3[j7];
								cnt4++;
							}
						}
						if (cnt4 > 0)
						{
							tmp2[i17] = f3[i17] + (acc3 / (float)cnt4 - f3[i17]) * 0.5f;
						}
					}
				}
				foreach (int i18 in idx)
				{
					f3[i18] = tmp2[i18];
				}
			}
			foreach (int i19 in idx)
			{
				if (b.mouthIn[i19] && (b.mouthLining == null || !b.mouthLining[i19]) && (b.mouthWet == null || !b.mouthWet[i19]))
				{
					this._mwOffr[i19] = 0.001f - 0.001f * f3[i19];
					if (f3[i19] > 0.5f)
					{
						b.mwSunk++;
					}
				}
			}
			int[] nbS4 = b.mouthNbrStart;
			int[] nbL4 = b.mouthNbrList;
			float[] tmp3 = this._mwRB2;
			if (tmp3 == null || tmp3.Length < nV)
			{
				tmp3 = (this._mwRB2 = new float[nV]);
			}
			for (int pass5 = 0; pass5 < 2; pass5++)
			{
				foreach (int i20 in idx)
				{
					tmp3[i20] = this._mwPen[i20];
					if (b.mouthIn[i20] && this._mwSeg[i20] >= 0 && (b.mouthLining == null || !b.mouthLining[i20]))
					{
						float acc4 = 0f;
						int cnt5 = 0;
						for (int k6 = nbS4[i20]; k6 < nbS4[i20 + 1]; k6++)
						{
							int j8 = nbL4[k6];
							if (b.mouthW[j8] > 0f && b.mouthIn[j8] && this._mwSeg[j8] >= 0 && (b.mouthLining == null || !b.mouthLining[j8]))
							{
								acc4 += this._mwPen[j8];
								cnt5++;
							}
						}
						if (cnt5 > 0)
						{
							tmp3[i20] = this._mwPen[i20] + (acc4 / (float)cnt5 - this._mwPen[i20]) * 0.5f;
						}
					}
				}
				foreach (int i21 in idx)
				{
					this._mwPen[i21] = tmp3[i21];
				}
			}
			foreach (int i22 in idx)
			{
				float sB = -this._mwPen[i22];
				this._mwDraw[i22] = ((sB <= 0.012f) ? 1f : ((sB >= 0.018f) ? 0f : (1f - (sB - 0.012f) / 0.006f)));
			}
			b.mwMs[0] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw0) * tickMs);
			tw0 = SoftFleshController._sw.ElapsedTicks;
			b.mwMs2[4] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw) * tickMs);
			tw = SoftFleshController._sw.ElapsedTicks;
			if (this.MouthRing(b, owner, 0.001f, idx, R, P, GW))
			{
				b.mwMs[1] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw0) * tickMs);
				tw0 = SoftFleshController._sw.ElapsedTicks;
				b.mwMs[2] = 0f;
				b.mwMs[3] = 0f;
				b.mwMs[4] = 0f;
				b.mwMs2[5] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw) * tickMs);
				tw = SoftFleshController._sw.ElapsedTicks;
				foreach (int i23 in idx)
				{
					this._mwS1[i23] = P[i23];
				}
				this._mwFresh = true;
				int nCS2 = ((this.lipSmooth < 0) ? 0 : ((this.lipSmooth > 12) ? 12 : this.lipSmooth));
				if (nCS2 > 0)
				{
					for (int it = 0; it < nCS2; it++)
					{
						this.MouthLaplace(b, P, P2, R);
					}
					foreach (int i24 in idx)
					{
						SR[i24] = R[i24];
					}
					foreach (int i25 in halo)
					{
						SR[i25] = R[i25];
					}
					int mPre2 = ((nCS2 < 2) ? nCS2 : 2);
					for (int it2 = 0; it2 < mPre2; it2++)
					{
						this.MouthLaplace(b, SR, P2, R);
					}
					foreach (int i26 in idx)
					{
						this._mwGW2[i26] = SR[i26];
					}
					for (int it3 = mPre2; it3 < nCS2; it3++)
					{
						this.MouthLaplace(b, SR, P2, R);
					}
					float engR = 0f;
					foreach (int i27 in idx)
					{
						float m3 = (this._mwS1[i27] - R[i27]).sqrMagnitude;
						if (m3 > engR)
						{
							engR = m3;
						}
					}
					engR = Mathf.Sqrt(engR) / 0.003f;
					if (engR > 1f)
					{
						engR = 1f;
					}
					foreach (int i28 in idx)
					{
						P[i28] += this._mwGW2[i28] - SR[i28] + (R[i28] - this._mwGW2[i28]) * (1f - b.mouthW[i28] * engR);
					}
				}
				foreach (int i29 in idx)
				{
					this._mwS2[i29] = P[i29];
				}
				b.mwMs2[6] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw) * tickMs);
				tw = SoftFleshController._sw.ElapsedTicks;
				this.MouthOutsideRing(b, P, GW, owner, 0.001f);
				b.mwMs2[7] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw) * tickMs);
				tw = SoftFleshController._sw.ElapsedTicks;
				this.MouthLayer(b, P, R, GW);
				if (b.mouthLipDist != null && this._mwOutPrev != null)
				{
					float aO = ((b.mwSolveGap <= 0f || b.mwSolveGap >= 0.3f) ? 1f : (1f - Mathf.Exp(-b.mwSolveGap / 0.08f)));
					foreach (int i30 in idx)
					{
						Vector3 dO = P[i30] - this._mwS2[i30];
						if (b.mouthIn[i30])
						{
							this._mwOutPrev[i30] = dO;
						}
						else
						{
							float ldm2 = b.mouthLipDist[i30];
							float far = ((ldm2 <= 0.01f) ? 0f : ((ldm2 >= 0.02f) ? 1f : ((ldm2 - 0.01f) / 0.01f)));
							Vector3 filt = this._mwOutPrev[i30] + (dO - this._mwOutPrev[i30]) * aO;
							Vector3 use = dO + (filt - dO) * far;
							this._mwOutPrev[i30] = use;
							P[i30] = this._mwS2[i30] + use;
						}
					}
				}
				this._mwFresh = false;
				b.mwMs[5] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw0) * tickMs);
				tw0 = SoftFleshController._sw.ElapsedTicks;
				b.mwMs2[8] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw) * tickMs);
				tw = SoftFleshController._sw.ElapsedTicks;
			}
			else
			{
				for (int it4 = 0; it4 < 12; it4++)
				{
					this.MouthLaplace(b, P, P2, R);
					this.MouthConstrain(b, P, GW, owner, 0.001f, false);
				}
				foreach (int i31 in idx)
				{
					P[i31] = R[i31] + (P[i31] - R[i31]) * b.mouthW[i31];
				}
				b.mwMs[1] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw0) * tickMs);
				tw0 = SoftFleshController._sw.ElapsedTicks;
				for (int it5 = 0; it5 < 12; it5++)
				{
					this.MouthLaplace(b, SR, P2, R);
				}
				float keep = 1f - tension;
				foreach (int i32 in idx)
				{
					Vector3 det = (R[i32] - SR[i32]) * (keep * b.mouthW[i32]);
					P[i32] += det;
					this._mwDbgDetail[i32] = det.magnitude;
				}
				b.mwMs[2] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw0) * tickMs);
				tw0 = SoftFleshController._sw.ElapsedTicks;
				this._mwFresh = true;
				for (int it6 = 0; it6 < 3; it6++)
				{
					this.MouthConstrain(b, P, GW, owner, 0.001f, false);
				}
				b.mwMs[3] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw0) * tickMs);
				tw0 = SoftFleshController._sw.ElapsedTicks;
				int nCS3 = ((this.lipSmooth < 0) ? 0 : ((this.lipSmooth > 12) ? 12 : this.lipSmooth));
				for (int it7 = 0; it7 < nCS3; it7++)
				{
					this.MouthLaplace(b, P, P2, R);
				}
				if (nCS3 > 0)
				{
					foreach (int i33 in idx)
					{
						SR[i33] = R[i33];
					}
					foreach (int i34 in halo)
					{
						SR[i34] = R[i34];
					}
					int mPre3 = ((nCS3 < 2) ? nCS3 : 2);
					for (int it8 = 0; it8 < mPre3; it8++)
					{
						this.MouthLaplace(b, SR, P2, R);
					}
					foreach (int i35 in idx)
					{
						this._mwGW2[i35] = SR[i35];
					}
					for (int it9 = mPre3; it9 < nCS3; it9++)
					{
						this.MouthLaplace(b, SR, P2, R);
					}
					foreach (int i36 in idx)
					{
						P[i36] += (this._mwGW2[i36] - SR[i36]) * b.mouthW[i36];
					}
				}
				b.mwMs[4] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw0) * tickMs);
				tw0 = SoftFleshController._sw.ElapsedTicks;
				this.MouthOutside(b, P, GW, owner, 0.001f);
				b.mwMs[5] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw0) * tickMs);
				tw0 = SoftFleshController._sw.ElapsedTicks;
				this._mwFresh = false;
			}
			b.mwClamped = 0;
			b.mwInMax = 0f;
			b.mwInV = -1;
			b.mwInRedir = 0;
			b.mwInClampMax = 0f;
			b.mwInAxMax = 0f;
			b.mwInDrag = 0f;
			b.mwInRing = 0f;
			b.mwInSmooth = 0f;
			b.mwInOut = 0f;
			b.mwSkinIn = 0;
			b.mwSkinInMax = 0f;
			if (b.mwTableReset)
			{
				b.mwSkinInPk = 0;
				b.mwSkinInPkMax = 0f;
			}
			if (b.mouthUpperF != null && b.mwFaceOut.sqrMagnitude > 0.5f)
			{
				Vector3 inU = -b.mwFaceOut;
				foreach (int i37 in idx)
				{
					float fu = b.mouthUpperF[i37];
					if (fu > 0f)
					{
						float cap = this.lipPushIn + (1f - fu) * 0.05f;
						float inw = Vector3.Dot(P[i37] - R[i37] + this._mwDragV[i37], inU);
						if (inw > cap)
						{
							float ex = inw - cap;
							P[i37] -= inU * ex;
							b.mwInRedir++;
							if (ex > b.mwInClampMax)
							{
								b.mwInClampMax = ex;
							}
						}
					}
				}
			}
			for (int k7 = 0; k7 < b.mwTop.Length; k7++)
			{
				b.mwTop[k7] = -1;
			}
			foreach (int i38 in idx)
			{
				Vector3 push = P[i38] - R[i38] + this._mwDragV[i38];
				float pm = push.magnitude;
				Vector3 cS;
				Vector3 uS;
				float rS;
				float sS;
				if (!b.mouthIn[i38] && this._mwSeg[i38] >= 0 && this.ItemNearestCachedV(i38, P[i38], this._mwSeg[i38], out cS, out uS, out rS, out sS) && sS < 0f)
				{
					b.mwSkinIn++;
					if (-sS > b.mwSkinInMax)
					{
						b.mwSkinInMax = -sS;
					}
				}
				if ((b.mouthIn[i38] && (b.mouthLining == null || !b.mouthLining[i38])) || (!b.mouthIn[i38] && b.mouthLipDist != null && b.mouthLipDist[i38] <= 0.01f))
				{
					Vector3 inU2 = -b.mwFaceOut;
					float inw2 = Vector3.Dot(push, inU2);
					float inAx = Vector3.Dot(push, -b.mouthOutW);
					if (inAx > b.mwInAxMax)
					{
						b.mwInAxMax = inAx;
					}
					if (inw2 > b.mwInMax)
					{
						b.mwInMax = inw2;
						b.mwInV = i38;
						b.mwInDrag = Vector3.Dot(this._mwDragV[i38], inU2);
						b.mwInRing = Vector3.Dot(this._mwS1[i38] - R[i38], inU2);
						b.mwInSmooth = Vector3.Dot(this._mwS2[i38] - this._mwS1[i38], inU2);
						b.mwInOut = Vector3.Dot(P[i38] - this._mwS2[i38], inU2);
					}
				}
				if (pm > b.mouthPenMax)
				{
					b.mouthPenMax = pm;
				}
				for (int k8 = 0; k8 < b.mwTop.Length; k8++)
				{
					int tk = b.mwTop[k8];
					if (tk < 0 || pm > (this._mwP[tk] - this._mwR[tk]).magnitude)
					{
						for (int m4 = b.mwTop.Length - 1; m4 > k8; m4--)
						{
							b.mwTop[m4] = b.mwTop[m4 - 1];
						}
						b.mwTop[k8] = i38;
						break;
					}
				}
				if (pm > 0.06f)
				{
					push *= 0.06f / pm;
					pm = 0.06f;
					b.mwClamped++;
				}
				b.mouthPushW[i38] = ((pm > 1E-05f) ? push : Vector3.zero);
				if (this._mwSeg[i38] >= 0 && b.mouthIn[i38])
				{
					Vector3 cF;
					Vector3 uF;
					float rF;
					float sF;
					this._mwClrFinal[i38] = (this.ItemNearestCachedV(i38, P[i38], this._mwSeg[i38], out cF, out uF, out rF, out sF) ? sF : float.MaxValue);
				}
				if (this._mwPen[i38] > 0f)
				{
					b.mwInside++;
				}
				else if (pm > 0.0005f)
				{
					b.mwCarried++;
				}
			}
			b.mwMs2[9] = (float)((double)(SoftFleshController._sw.ElapsedTicks - tw) * tickMs);
			tw = SoftFleshController._sw.ElapsedTicks;
			if (b.mwSkinIn > b.mwSkinInPk)
			{
				b.mwSkinInPk = b.mwSkinIn;
			}
			if (b.mwSkinInMax > b.mwSkinInPkMax)
			{
				b.mwSkinInPkMax = b.mwSkinInMax;
			}
			b.mouthPushValid = true;
			if (b.mwPPrev == null || b.mwPPrev.Length < P.Length)
			{
				b.mwPPrev = new Vector3[P.Length];
				b.mwJitN = -1;
			}
			float sMax = 0f;
			float sSum = 0f;
			float wMax = 0f;
			int nS = 0;
			int sV = -1;
			if (b.mwJitN >= 0)
			{
				foreach (int i39 in idx)
				{
					float d3 = (P[i39] - b.mwPPrev[i39]).magnitude;
					if (b.mouthIn[i39])
					{
						if (d3 > wMax)
						{
							wMax = d3;
						}
					}
					else
					{
						sSum += d3;
						nS++;
						if (d3 > sMax)
						{
							sMax = d3;
							sV = i39;
						}
					}
				}
			}
			b.mwJitSkinMax = sMax;
			b.mwJitSkinMean = ((nS > 0) ? (sSum / (float)nS) : 0f);
			b.mwJitWallMax = wMax;
			if (b.mwJitN % 60 == 0)
			{
				b.mwJitSkinW = 0f;
				b.mwJitOpenW = 0f;
				b.mwJitItemW = 0f;
			}
			if (b.mwTableReset)
			{
				b.mwJitSkinH = 0f;
				b.mwJitHoldLine = null;
			}
			bool loopFrame = owner != null && b.mwJitN >= 0 && (owner.position - b.mwOwnerPrev).magnitude > 0.02f;
			if (sMax > b.mwJitSkinW && !loopFrame)
			{
				b.mwJitSkinW = sMax;
				b.mwJitWV = sV;
				b.mwJitWGap = b.mwSolveGap;
				b.mwJitWReset = b.mwTableReset;
				if (sV >= 0)
				{
					b.mwJitWDragV = (this._mwDragV[sV] - this._mwDragVPrev[sV]).magnitude;
					b.mwJitWRing = (this._mwS1[sV] - R[sV] - (this._mwS1Prev[sV] - this._mwRPrev[sV])).magnitude;
					b.mwJitWSmooth = (this._mwS2[sV] - this._mwS1[sV] - (this._mwS2Prev[sV] - this._mwS1Prev[sV])).magnitude;
					b.mwJitWOut = (P[sV] - this._mwS2[sV] - (b.mwPPrev[sV] - this._mwS2Prev[sV])).magnitude;
					b.mwJitWLip = ((b.mouthLipDist != null) ? b.mouthLipDist[sV] : (-1f));
					b.mwJitWFR = ((this._mwRingF != null) ? this._mwRingF[sV] : (-1f));
					b.mwJitWClr = ((this._mwSeg[sV] < 0) ? 1f : (-this._mwPen[sV]));
				}
				b.mwJitWDrag = b.mwDragNow - b.mwDragPrevSolve;
				b.mwJitWItem = b.mwJitItem;
				if (sV >= 0 && b.cha != null)
				{
					Vector3 off = R[sV] - b.mouthC;
					b.mwJitWUp = Vector3.Dot(off, b.cha.transform.up);
					b.mwJitWRt = Vector3.Dot(off, b.cha.transform.right);
					b.mwJitWFw = Vector3.Dot(off, b.cha.transform.forward);
					b.mwJitWW = b.mouthW[sV];
				}
				if (sMax > b.mwJitSkinH && !b.mwTableReset)
				{
					b.mwJitSkinH = sMax;
					b.mwJitHoldLine = this.JitWorstLine(b);
				}
			}
			b.mwDragPrevSolve = b.mwDragNow;
			float od = ((b.mwOpenDelta < 0f) ? (-b.mwOpenDelta) : b.mwOpenDelta);
			if (od > b.mwJitOpenW)
			{
				b.mwJitOpenW = od;
			}
			b.mwJitItem = ((owner != null && b.mwJitN >= 0) ? (owner.position - b.mwOwnerPrev).magnitude : 0f);
			if (b.mwJitItem > b.mwJitItemW)
			{
				b.mwJitItemW = b.mwJitItem;
			}
			if (owner != null)
			{
				b.mwOwnerPrev = owner.position;
			}
			foreach (int i40 in idx)
			{
				b.mwPPrev[i40] = P[i40];
				this._mwRPrev[i40] = R[i40];
				this._mwS1Prev[i40] = this._mwS1[i40];
				this._mwS2Prev[i40] = this._mwS2[i40];
				this._mwDragVPrev[i40] = this._mwDragV[i40];
			}
			b.mwJitN = ((b.mwJitN < 0) ? 0 : (b.mwJitN + 1));
			b.mwSig = this._mwSigPending;
		}

		// Token: 0x0600011B RID: 283 RVA: 0x000490D0 File Offset: 0x000472D0
		private bool MouthRing(SoftFleshController.Body b, Transform owner, float OFF, int[] idx, Vector3[] R, Vector3[] P, Vector3[] GW)
		{
			if (b.mouthAng == null || b.mouthAlo == null || this._chN == 0 || this._mwAloOwn == null)
			{
				return false;
			}
			float zMin = float.MaxValue;
			float zMax = float.MinValue;
			foreach (int i in idx)
			{
				if (b.mouthIn[i] && (b.mouthLining == null || !b.mouthLining[i]))
				{
					float z = this._mwAloOwn[i];
					if (z < zMin)
					{
						zMin = z;
					}
					if (z > zMax)
					{
						zMax = z;
					}
				}
			}
			if (zMin >= zMax)
			{
				return false;
			}
			zMin -= 0.003f;
			zMax += 0.003f;
			float zStep = (zMax - zMin) / 7f;
			this._rgZMin = zMin;
			this._rgZStep = zStep;
			int nc = 192;
			for (int c = 0; c < nc; c++)
			{
				this._rgD[c] = 0f;
				this._rgA[c] = 0f;
				this._rgT[c] = 0f;
				this._rgNeed[c] = -1E+09f;
				this._rgHave[c] = false;
			}
			b.mwTiltSum = 0f;
			b.mwTiltN = 0;
			b.mwTiltMin = 1f;
			b.mwGainMax = 0f;
			b.mwGainSum = 0f;
			b.mwNearAxis = 0;
			Vector3 outW = b.mouthOutW;
			foreach (int j in idx)
			{
				if (b.mouthIn[j] && (b.mouthLining == null || !b.mouthLining[j]) && this._mwSeg[j] >= 0)
				{
					float need = OFF + this._mwPen[j];
					Vector3 relN = R[j] - b.mouthAxP;
					float alN = Vector3.Dot(relN, b.mouthOutW);
					if ((relN - b.mouthOutW * alN).sqrMagnitude < 9E-06f)
					{
						b.mwNearAxis++;
					}
					Vector3 e = this._mwDir[j] * need;
					Vector3 uRad = -this._mwGR[j];
					Vector3 uAxi = -outW;
					Vector3 uTan = Vector3.Cross(uRad, uAxi);
					float tm = uTan.magnitude;
					uTan = ((tm > 1E-06f) ? (uTan / tm) : Vector3.zero);
					float rad = Vector3.Dot(e, uRad);
					float axi = Vector3.Dot(e, uAxi);
					float tang = Vector3.Dot(e, uTan);
					if (tang > 0.004f)
					{
						tang = 0.004f;
					}
					else if (tang < -0.004f)
					{
						tang = -0.004f;
					}
					if (need <= 0f)
					{
						rad = ((need < -0.004f) ? 0f : need);
						axi = 0f;
						tang = 0f;
					}
					if (rad < -0.004f)
					{
						rad = -0.004f;
					}
					float tilt = Vector3.Dot(this._mwDir[j], uRad);
					b.mwTiltSum += ((tilt < -1f) ? (-1f) : ((tilt > 1f) ? 1f : tilt));
					b.mwTiltN++;
					if (tilt < b.mwTiltMin)
					{
						b.mwTiltMin = tilt;
					}
					float gain = Mathf.Abs(tang);
					if (gain > b.mwGainMax)
					{
						b.mwGainMax = gain;
					}
					b.mwGainSum += gain;
					if (axi < 0f)
					{
						axi = 0f;
					}
					int sb = (int)((this._mwAngOwn[j] + 3.1415927f) * 3.8197186f);
					if (sb < 0)
					{
						sb = 0;
					}
					else if (sb > 23)
					{
						sb = 23;
					}
					int zb = (int)((this._mwAloOwn[j] - zMin) / zStep + 0.5f);
					if (zb < 0)
					{
						zb = 0;
					}
					else if (zb > 7)
					{
						zb = 7;
					}
					int c2 = zb * 24 + sb;
					if (!this._rgHave[c2] || need > this._rgNeed[c2])
					{
						this._rgD[c2] = rad;
						this._rgA[c2] = axi;
						this._rgT[c2] = tang;
						this._rgNeed[c2] = need;
						this._rgHave[c2] = true;
					}
				}
			}
			this.RingTableV(this._rgNeed, this._rgD, this._rgA, this._rgT, this._rgHave);
			this.RingSmooth(this._rgD, this._rgD2);
			this.RingSmooth(this._rgA, this._rgA2);
			this.RingSmooth(this._rgT, this._rgT2);
			float tNowR = Time.time;
			float dtR = ((b.mwTableT > 0f) ? (tNowR - b.mwTableT) : 1f);
			b.mwTableT = tNowR;
			b.mwTableReset = dtR >= 0.3f || !b.mwTableOK;
			float stepI = ((owner != null && b.mwJitN >= 0) ? (owner.position - b.mwOwnerPrev).magnitude : 0f);
			float stepD = b.mwDragNow - b.mwDragPrevSolve;
			if (stepD < 0f)
			{
				stepD = -stepD;
			}
			float fastR = (stepI + stepD) / 0.006f;
			if (fastR > 1f)
			{
				fastR = 1f;
			}
			float tauR = 0.06f * (1f - fastR);
			b.mwTableTau = tauR;
			if (dtR < 0.3f && b.mwTableOK && tauR > 0.002f)
			{
				float aR = 1f - Mathf.Exp(-dtR / tauR);
				for (int c3 = 0; c3 < nc; c3++)
				{
					this._rgD[c3] = b.mwTD[c3] + (this._rgD[c3] - b.mwTD[c3]) * aR;
					this._rgA[c3] = b.mwTA[c3] + (this._rgA[c3] - b.mwTA[c3]) * aR;
					this._rgT[c3] = b.mwTT[c3] + (this._rgT[c3] - b.mwTT[c3]) * aR;
				}
			}
			if (b.mwTD == null || b.mwTD.Length != nc)
			{
				b.mwTD = new float[nc];
				b.mwTA = new float[nc];
				b.mwTT = new float[nc];
			}
			Array.Copy(this._rgD, b.mwTD, nc);
			Array.Copy(this._rgA, b.mwTA, nc);
			Array.Copy(this._rgT, b.mwTT, nc);
			b.mwTableOK = true;
			b.mwRode = ((b.mouthAngR != null) ? 1 : 0);
			b.mwHugN = 0;
			b.mwHugFar = 0;
			b.mwHugMax = 0f;
			b.mwLinHeld = 0;
			b.mwLinHeldMax = 0f;
			float[] fR = this._mwRingF;
			float[] fR2 = this._mwLiftRaw;
			int[] nbSr = b.mouthNbrStart;
			int[] nbLr = b.mouthNbrList;
			b.mwFarHeld = 0;
			b.mwFarHeldMax = 0f;
			foreach (int k in idx)
			{
				float f = 1f;
				if (!b.mouthIn[k])
				{
					bool roll = false;
					if (b.mouthPartner != null)
					{
						int pj = b.mouthPartner[k];
						roll = pj >= 0 && b.mouthW[pj] > 0f && (R[pj] - R[k]).sqrMagnitude < 6.400001E-05f;
					}
					if (!roll)
					{
						float clr = ((this._mwSeg[k] < 0) ? 1f : (-this._mwPen[k]));
						f = ((clr <= 0.006f) ? 1f : ((clr >= 0.015f) ? 0f : (1f - (clr - 0.006f) / 0.009f)));
						if (b.mouthLipDist != null)
						{
							float ldm = b.mouthLipDist[k];
							f *= ((ldm <= 0.01f) ? 1f : ((ldm >= 0.02f) ? 0f : (1f - (ldm - 0.01f) / 0.01f)));
						}
					}
				}
				fR[k] = f;
			}
			if (nbSr != null && nbLr != null)
			{
				for (int pass = 0; pass < 2; pass++)
				{
					foreach (int l in idx)
					{
						fR2[l] = fR[l];
						if (!b.mouthIn[l])
						{
							float acc = 0f;
							int cnt = 0;
							for (int m = nbSr[l]; m < nbSr[l + 1]; m++)
							{
								int n = nbLr[m];
								if (b.mouthW[n] > 0f)
								{
									acc += fR[n];
									cnt++;
								}
							}
							if (cnt > 0)
							{
								fR2[l] = fR[l] + (acc / (float)cnt - fR[l]) * 0.5f;
							}
						}
					}
					foreach (int i2 in idx)
					{
						fR[i2] = fR2[i2];
					}
				}
			}
			foreach (int i3 in idx)
			{
				float d;
				float ax;
				float tg;
				this.RingEval(b, i3, zMin, zStep, out d, out ax, out tg);
				if (d < 0f)
				{
					float clr2 = -this._mwPen[i3];
					float cf = ((clr2 <= 0.006f) ? 1f : ((clr2 >= 0.015f) ? 0f : (1f - (clr2 - 0.006f) / 0.009f)));
					d *= cf;
					if (cf > 0f)
					{
						b.mwHugN++;
						if (-d > b.mwHugMax)
						{
							b.mwHugMax = -d;
						}
						if (clr2 > 0.01f)
						{
							b.mwHugFar++;
						}
					}
				}
				if (d > 0f && b.mouthLining != null && b.mouthLining[i3])
				{
					float own = ((this._mwSeg[i3] < 0) ? 0f : (OFF + this._mwPen[i3]));
					if (own < 0f)
					{
						own = 0f;
					}
					if (own < d)
					{
						float scL = own / d;
						b.mwLinHeld++;
						if (d - own > b.mwLinHeldMax)
						{
							b.mwLinHeldMax = d - own;
						}
						d = own;
						ax *= scL;
						tg *= scL;
					}
				}
				Vector3 uR = -this._mwGR[i3];
				Vector3 uA = -outW;
				Vector3 uT = Vector3.Cross(uR, uA);
				float tmv = uT.magnitude;
				uT = ((tmv > 1E-06f) ? (uT / tmv) : Vector3.zero);
				Vector3 vec = uR * d + uA * ax + uT * tg;
				float fr = fR[i3];
				if (fr < 1f)
				{
					float held = vec.magnitude * (1f - fr) * b.mouthW[i3];
					if (held > 0.0005f)
					{
						b.mwFarHeld++;
						if (held > b.mwFarHeldMax)
						{
							b.mwFarHeldMax = held;
						}
					}
				}
				P[i3] = R[i3] + vec * (b.mouthW[i3] * fr);
			}
			return true;
		}

		// Token: 0x0600011C RID: 284 RVA: 0x00049C50 File Offset: 0x00047E50
		private void RingEval(SoftFleshController.Body b, int i, float zMin, float zStep, out float d, out float ax)
		{
			float tg;
			this.RingEval(b, i, zMin, zStep, out d, out ax, out tg);
		}

		// Token: 0x0600011D RID: 285 RVA: 0x00049C70 File Offset: 0x00047E70
		private void RingEval(SoftFleshController.Body b, int i, float zMin, float zStep, out float d, out float ax, out float tg)
		{
			float[] angA = ((this._mwAngW != null) ? this._mwAngW : b.mouthAng);
			float[] aloA = ((this._mwAloW != null) ? this._mwAloW : b.mouthAlo);
			float fs = (angA[i] + 3.1415927f) * 3.8197186f - 0.5f;
			int s0 = Mathf.FloorToInt(fs);
			float ts = fs - (float)s0;
			s0 = (s0 % 24 + 24) % 24;
			int s = (s0 + 1) % 24;
			float fz = (aloA[i] - zMin) / zStep;
			if (fz < 0f)
			{
				fz = 0f;
			}
			if (fz > 7f)
			{
				fz = 7f;
			}
			int z0 = (int)fz;
			if (z0 > 6)
			{
				z0 = 6;
			}
			float tz = fz - (float)z0;
			int c0 = z0 * 24 + s0;
			int c = z0 * 24 + s;
			int c2 = (z0 + 1) * 24 + s0;
			int c3 = (z0 + 1) * 24 + s;
			d = Mathf.Lerp(Mathf.Lerp(this._rgD[c0], this._rgD[c], ts), Mathf.Lerp(this._rgD[c2], this._rgD[c3], ts), tz);
			ax = Mathf.Lerp(Mathf.Lerp(this._rgA[c0], this._rgA[c], ts), Mathf.Lerp(this._rgA[c2], this._rgA[c3], ts), tz);
			tg = Mathf.Lerp(Mathf.Lerp(this._rgT[c0], this._rgT[c], ts), Mathf.Lerp(this._rgT[c2], this._rgT[c3], ts), tz);
		}

		// Token: 0x0600011E RID: 286 RVA: 0x00049E00 File Offset: 0x00048000
		private void RingTableV(float[] Nd, float[] D, float[] A, float[] T, bool[] have)
		{
			for (int zb = 0; zb < 8; zb++)
			{
				for (int sb = 0; sb < 24; sb++)
				{
					int c = zb * 24 + sb;
					if (!have[c])
					{
						for (int dd = 1; dd < 24; dd++)
						{
							int a2 = zb * 24 + (sb + dd) % 24;
							int b3 = zb * 24 + (sb - dd + 24) % 24;
							if (have[a2])
							{
								D[c] = D[a2];
								A[c] = A[a2];
								T[c] = T[a2];
								Nd[c] = Nd[a2];
								break;
							}
							if (have[b3])
							{
								D[c] = D[b3];
								A[c] = A[b3];
								T[c] = T[b3];
								Nd[c] = Nd[b3];
								break;
							}
						}
					}
				}
			}
			for (int zb2 = 0; zb2 < 8; zb2++)
			{
				bool any = false;
				for (int sb2 = 0; sb2 < 24; sb2++)
				{
					if (have[zb2 * 24 + sb2])
					{
						any = true;
						break;
					}
				}
				if (!any)
				{
					for (int dd2 = 1; dd2 < 8; dd2++)
					{
						int za = zb2 + dd2;
						int zc = zb2 - dd2;
						int src = -1;
						if (za < 8)
						{
							for (int sb3 = 0; sb3 < 24; sb3++)
							{
								if (have[za * 24 + sb3])
								{
									src = za;
									break;
								}
							}
						}
						if (src < 0 && zc >= 0)
						{
							for (int sb4 = 0; sb4 < 24; sb4++)
							{
								if (have[zc * 24 + sb4])
								{
									src = zc;
									break;
								}
							}
						}
						if (src >= 0)
						{
							for (int sb5 = 0; sb5 < 24; sb5++)
							{
								int c2 = zb2 * 24 + sb5;
								int cs = src * 24 + sb5;
								D[c2] = D[cs];
								A[c2] = A[cs];
								T[c2] = T[cs];
								Nd[c2] = Nd[cs];
							}
							break;
						}
					}
				}
			}
			this._rgCarryN = 0;
			this._rgCarryMax = 0f;
			this._rgCarrySec = -1;
			for (int sb6 = 0; sb6 < 24; sb6++)
			{
				float own = Nd[168 + sb6];
				float behind = -1E+09f;
				for (int zb3 = 0; zb3 < 7; zb3++)
				{
					float v = Nd[zb3 * 24 + sb6];
					if (v > behind)
					{
						behind = v;
					}
				}
				this._rgLipOwn[sb6] = own;
				this._rgLipBehind[sb6] = behind;
				float carry = behind - own;
				if (own > -100000000f && behind > -100000000f && carry > 0.002f)
				{
					this._rgCarryN++;
					if (carry > this._rgCarryMax)
					{
						this._rgCarryMax = carry;
						this._rgCarrySec = sb6;
					}
				}
			}
		}

		// Token: 0x0600011F RID: 287 RVA: 0x0004A090 File Offset: 0x00048290
		private void RingSmooth(float[] D, float[] D2)
		{
			for (int pass = 0; pass < 3; pass++)
			{
				for (int zb = 0; zb < 8; zb++)
				{
					for (int sb = 0; sb < 24; sb++)
					{
						int c = zb * 24 + sb;
						float acc = 0f;
						int cnt = 0;
						acc += D[zb * 24 + (sb + 1) % 24];
						cnt++;
						acc += D[zb * 24 + (sb - 1 + 24) % 24];
						cnt++;
						if (zb + 1 < 8)
						{
							acc += D[(zb + 1) * 24 + sb];
							cnt++;
						}
						if (zb - 1 >= 0)
						{
							acc += D[(zb - 1) * 24 + sb];
							cnt++;
						}
						D2[c] = D[c] + (acc / (float)cnt - D[c]) * 0.5f;
					}
				}
				Array.Copy(D2, D, 192);
			}
		}

		// Token: 0x06000120 RID: 288 RVA: 0x0004A174 File Offset: 0x00048374
		private void RingTable(float[] D, float[] D2, bool[] have)
		{
			for (int zb = 0; zb < 8; zb++)
			{
				for (int sb = 0; sb < 24; sb++)
				{
					int c = zb * 24 + sb;
					if (!have[c])
					{
						for (int d = 1; d < 24; d++)
						{
							int a2 = zb * 24 + (sb + d) % 24;
							int b3 = zb * 24 + (sb - d + 24) % 24;
							if (have[a2])
							{
								D[c] = D[a2];
								break;
							}
							if (have[b3])
							{
								D[c] = D[b3];
								break;
							}
						}
					}
				}
			}
			for (int zb2 = 0; zb2 < 8; zb2++)
			{
				bool any = false;
				for (int sb2 = 0; sb2 < 24; sb2++)
				{
					if (have[zb2 * 24 + sb2])
					{
						any = true;
						break;
					}
				}
				if (!any)
				{
					for (int d2 = 1; d2 < 8; d2++)
					{
						int za = zb2 + d2;
						int zc = zb2 - d2;
						int src = -1;
						if (za < 8)
						{
							for (int sb3 = 0; sb3 < 24; sb3++)
							{
								if (have[za * 24 + sb3])
								{
									src = za;
									break;
								}
							}
						}
						if (src < 0 && zc >= 0)
						{
							for (int sb4 = 0; sb4 < 24; sb4++)
							{
								if (have[zc * 24 + sb4])
								{
									src = zc;
									break;
								}
							}
						}
						if (src >= 0)
						{
							for (int sb5 = 0; sb5 < 24; sb5++)
							{
								D[zb2 * 24 + sb5] = D[src * 24 + sb5];
							}
							break;
						}
					}
				}
			}
			for (int sb6 = 0; sb6 < 24; sb6++)
			{
				for (int zb3 = 1; zb3 < 8; zb3++)
				{
					if (D[(zb3 - 1) * 24 + sb6] > D[zb3 * 24 + sb6])
					{
						D[zb3 * 24 + sb6] = D[(zb3 - 1) * 24 + sb6];
					}
				}
			}
			for (int pass = 0; pass < 3; pass++)
			{
				for (int zb4 = 0; zb4 < 8; zb4++)
				{
					for (int sb7 = 0; sb7 < 24; sb7++)
					{
						int c2 = zb4 * 24 + sb7;
						float acc = 0f;
						int cnt = 0;
						acc += D[zb4 * 24 + (sb7 + 1) % 24];
						cnt++;
						acc += D[zb4 * 24 + (sb7 - 1 + 24) % 24];
						cnt++;
						if (zb4 + 1 < 8)
						{
							acc += D[(zb4 + 1) * 24 + sb7];
							cnt++;
						}
						if (zb4 - 1 >= 0)
						{
							acc += D[(zb4 - 1) * 24 + sb7];
							cnt++;
						}
						D2[c2] = D[c2] + (acc / (float)cnt - D[c2]) * 0.5f;
					}
				}
				Array.Copy(D2, D, 192);
			}
		}

		// Token: 0x06000121 RID: 289 RVA: 0x0004A418 File Offset: 0x00048618
		private float LipFactor(SoftFleshController.Body b, int i)
		{
			if (b.mouthIn[i])
			{
				if (b.mouthLining != null && b.mouthLining[i])
				{
					return 0f;
				}
				int r0 = (int)((b.mouthRow != null) ? b.mouthRow[i] : byte.MaxValue);
				if (r0 <= 3)
				{
					return 1f;
				}
				if (r0 < 6)
				{
					return 1f - (float)(r0 - 3) / 3f;
				}
				return 0f;
			}
			else
			{
				float ldm = ((b.mouthLipDist != null) ? b.mouthLipDist[i] : 0f);
				if (ldm <= 0.01f)
				{
					return 1f;
				}
				if (ldm < 0.02f)
				{
					return 1f - (ldm - 0.01f) / 0.01f;
				}
				return 0f;
			}
		}

		// Token: 0x06000122 RID: 290 RVA: 0x0004A4CC File Offset: 0x000486CC
		private string JitWorstLine(SoftFleshController.Body b)
		{
			StringBuilder sb = new StringBuilder(256);
			sb.Append("v").Append(b.mwJitWV).Append(" w ")
				.Append(b.mwJitWW.ToString("F2"))
				.Append(" at up ")
				.Append((b.mwJitWUp * 1000f).ToString("F0"))
				.Append(" right ")
				.Append((b.mwJitWRt * 1000f).ToString("F0"))
				.Append(" fwd ")
				.Append((b.mwJitWFw * 1000f).ToString("F0"))
				.Append("mm | item moved ")
				.Append((b.mwJitWItem * 1000f).ToString("F2"))
				.Append("mm | ")
				.Append((b.mwJitWGap * 1000f).ToString("F0"))
				.Append("ms since the previous solve")
				.Append(b.mwJitWReset ? " (TABLE RESET)" : "")
				.Append(" | drag changed ")
				.Append((b.mwJitWDrag * 1000f).ToString("F2"))
				.Append("mm")
				.Append(" | lip dist ")
				.Append((b.mwJitWLip * 1000f).ToString("F0"))
				.Append("mm ring factor ")
				.Append(b.mwJitWFR.ToString("F2"))
				.Append(" clear ")
				.Append((b.mwJitWClr * 1000f).ToString("F0"))
				.Append("mm")
				.Append(" | its move split: drag part ")
				.Append((b.mwJitWDragV * 1000f).ToString("F1"))
				.Append(" ring ")
				.Append((b.mwJitWRing * 1000f).ToString("F1"))
				.Append(" smooth ")
				.Append((b.mwJitWSmooth * 1000f).ToString("F1"))
				.Append(" outside+layer ")
				.Append((b.mwJitWOut * 1000f).ToString("F1"))
				.Append("mm");
			return sb.ToString();
		}

		// Token: 0x06000123 RID: 291 RVA: 0x0004A760 File Offset: 0x00048960
		private void MouthOutsideRing(SoftFleshController.Body b, Vector3[] P, Vector3[] GW, Transform owner, float OFF)
		{
			int[] idx = b.mouthIdx;
			int[] nbS = b.mouthNbrStart;
			int[] nbL = b.mouthNbrList;
			Vector3[] E = this._mwP2;
			Vector3[] E2 = this._mwSR;
			for (int round = 0; round < 2; round++)
			{
				foreach (int i in idx)
				{
					E[i] = Vector3.zero;
					Vector3 cB;
					Vector3 uB;
					float rB;
					float sBest;
					if (this._mwSeg[i] >= 0 && (b.mouthLining == null || !b.mouthLining[i]) && this.ItemNearestCachedV(i, P[i], this._mwSeg[i], out cB, out uB, out rB, out sBest))
					{
						this.CoherentExit(P[i], this._mwSeg[i], rB, sBest, this._mwGR[i], ref cB, ref uB);
						if (sBest < OFF)
						{
							E[i] = uB * (OFF - sBest);
							if (b.mouthIn[i])
							{
								b.mwFinIn++;
							}
							else
							{
								b.mwFinOut++;
							}
						}
					}
				}
				if (b.mouthRow != null)
				{
					Vector3[] Ea = this._mwGW2;
					foreach (int j in idx)
					{
						Ea[j] = E[j];
					}
					bool gridW = this.GridBuildSel(b, P, idx, 1);
					foreach (int k in idx)
					{
						int r0 = (int)b.mouthRow[k];
						if (b.mouthIn[k] && r0 >= 1 && r0 <= 3 && (b.mouthLining == null || !b.mouthLining[k]))
						{
							float best = E[k].sqrMagnitude;
							Vector3 bv = E[k];
							Vector3 pi = P[k];
							if (gridW)
							{
								int cx = Mathf.FloorToInt((pi.x - this._gMin.x) / 0.008f);
								int cy = Mathf.FloorToInt((pi.y - this._gMin.y) / 0.008f);
								int cz = Mathf.FloorToInt((pi.z - this._gMin.z) / 0.008f);
								for (int dx = -1; dx <= 1; dx++)
								{
									for (int dy = -1; dy <= 1; dy++)
									{
										for (int dz = -1; dz <= 1; dz++)
										{
											int x = cx + dx;
											int y = cy + dy;
											int z = cz + dz;
											if (x >= 0 && y >= 0 && z >= 0 && x < this._gNx && y < this._gNy && z < this._gNz)
											{
												for (int l = this._gHead[(x * this._gNy + y) * this._gNz + z]; l >= 0; l = this._gNext[l])
												{
													float m2 = E[l].sqrMagnitude;
													if (m2 > best && (P[l] - pi).sqrMagnitude <= 1.6000002E-05f)
													{
														best = m2;
														bv = E[l];
													}
												}
											}
										}
									}
								}
							}
							else
							{
								foreach (int n in idx)
								{
									if (b.mouthIn[n] && (b.mouthLining == null || !b.mouthLining[n]))
									{
										float m3 = E[n].sqrMagnitude;
										if (m3 > best && (P[n] - pi).sqrMagnitude <= 1.6000002E-05f)
										{
											best = m3;
											bv = E[n];
										}
									}
								}
							}
							Ea[k] = bv;
						}
					}
					foreach (int i2 in idx)
					{
						E[i2] = Ea[i2];
					}
				}
				if (b.mouthPartner != null)
				{
					foreach (int i3 in idx)
					{
						if (!b.mouthIn[i3])
						{
							int pj = b.mouthPartner[i3];
							if (pj >= 0 && b.mouthW[pj] > 0f && E[pj].sqrMagnitude > E[i3].sqrMagnitude && (P[pj] - P[i3]).sqrMagnitude < 6.400001E-05f)
							{
								E[i3] = E[pj];
							}
						}
					}
				}
				for (int pass = 0; pass < 4; pass++)
				{
					foreach (int i4 in idx)
					{
						Vector3 acc = Vector3.zero;
						int cnt = 0;
						for (int k2 = nbS[i4]; k2 < nbS[i4 + 1]; k2++)
						{
							int j2 = nbL[k2];
							if (b.mouthW[j2] > 0f)
							{
								acc += E[j2];
							}
							cnt++;
						}
						E2[i4] = ((cnt > 0) ? (E[i4] + (acc / (float)cnt - E[i4]) * 0.5f) : E[i4]);
					}
					foreach (int i5 in idx)
					{
						E[i5] = E2[i5];
					}
				}
				foreach (int i6 in idx)
				{
					if (b.mouthLining == null || !b.mouthLining[i6])
					{
						float m4 = E[i6].magnitude;
						if (m4 > b.mwFinOutMax)
						{
							b.mwFinOutMax = m4;
						}
						P[i6] += E[i6] * b.mouthW[i6];
					}
				}
			}
			Vector3[] rimPush = this._mwGW2;
			for (int q10 = 0; q10 < idx.Length; q10++)
			{
				rimPush[idx[q10]] = Vector3.zero;
			}
			foreach (int i7 in idx)
			{
				Vector3 cB2;
				Vector3 uB2;
				float rB2;
				float sBest2;
				if (this._mwSeg[i7] >= 0 && b.mouthIn[i7] && (b.mouthLining == null || !b.mouthLining[i7]) && this.ItemNearestCachedV(i7, P[i7], this._mwSeg[i7], out cB2, out uB2, out rB2, out sBest2))
				{
					this.CoherentExit(P[i7], this._mwSeg[i7], rB2, sBest2, this._mwGR[i7], ref cB2, ref uB2);
					if (sBest2 < OFF)
					{
						float mv = OFF - sBest2;
						Vector3 pv = uB2 * (mv * b.mouthW[i7]);
						P[i7] += pv;
						rimPush[i7] = pv;
						if (mv > b.mwFinInMax)
						{
							b.mwFinInMax = mv;
						}
					}
				}
			}
			if (b.mouthPartner != null)
			{
				foreach (int j3 in idx)
				{
					if (!b.mouthIn[j3])
					{
						int pj2 = b.mouthPartner[j3];
						if (pj2 >= 0 && b.mouthW[pj2] > 0f && rimPush[pj2].sqrMagnitude > 0f && (P[pj2] - P[j3]).sqrMagnitude < 6.400001E-05f)
						{
							P[j3] += rimPush[pj2];
						}
					}
				}
			}
			b.mwSkinFix = 0;
			b.mwSkinFixMax = 0f;
			foreach (int i8 in idx)
			{
				Vector3 cB3;
				Vector3 uB3;
				float rB3;
				float sBest3;
				if (!b.mouthIn[i8] && this._mwSeg[i8] >= 0 && this.LipFactor(b, i8) > 0f && this.ItemNearestCachedV(i8, P[i8], this._mwSeg[i8], out cB3, out uB3, out rB3, out sBest3) && sBest3 < OFF)
				{
					this.CoherentExit(P[i8], this._mwSeg[i8], rB3, sBest3, this._mwGR[i8], ref cB3, ref uB3);
					Vector3 target = cB3 + uB3 * (rB3 + OFF);
					Vector3 mv2 = target - P[i8];
					float mm = mv2.magnitude;
					if (mm > b.mwSkinFixMax)
					{
						b.mwSkinFixMax = mm;
					}
					P[i8] = target;
					b.mwSkinFix++;
					if (b.seamOf != null && b.seamGroups != null && i8 < b.seamOf.Length && b.seamOf[i8] >= 0)
					{
						int[] grp = b.seamGroups[b.seamOf[i8]];
						for (int g = 0; g < grp.Length; g++)
						{
							if (grp[g] != i8 && !b.mouthIn[grp[g]] && b.mouthW[grp[g]] > 0f)
							{
								P[grp[g]] += mv2;
							}
						}
					}
				}
			}
		}

		// Token: 0x06000124 RID: 292 RVA: 0x0004B100 File Offset: 0x00049300
		private static Matrix4x4 BakeL2W(SkinnedMeshRenderer smr)
		{
			Transform t = smr.transform;
			return Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
		}

		// Token: 0x06000125 RID: 293 RVA: 0x0004B12C File Offset: 0x0004932C
		private static Matrix4x4 BakeW2L(SkinnedMeshRenderer smr)
		{
			Transform t = smr.transform;
			return Matrix4x4.TRS(t.position, t.rotation, Vector3.one).inverse;
		}

		// Token: 0x06000126 RID: 294 RVA: 0x0004B160 File Offset: 0x00049360
		private void MouthNormals(SoftFleshController.Body b, Vector3[] pos, Vector3[] N, Vector3[] GW)
		{
			int[] idx = b.mouthIdx;
			int[] tris = b.mouthTris;
			for (int q = 0; q < idx.Length; q++)
			{
				N[idx[q]] = Vector3.zero;
			}
			int t3 = 0;
			while (t3 + 2 < tris.Length)
			{
				int a = tris[t3];
				int b2 = tris[t3 + 1];
				int c2 = tris[t3 + 2];
				if (b.mouthW[a] > 0f && b.mouthW[b2] > 0f && b.mouthW[c2] > 0f)
				{
					Vector3 fn = Vector3.Cross(pos[b2] - pos[a], pos[c2] - pos[a]);
					N[a] += fn;
					N[b2] += fn;
					N[c2] += fn;
				}
				t3 += 3;
			}
			if (b.seamGroups != null && b.seamOf != null)
			{
				foreach (int i in idx)
				{
					int g = b.seamOf[i];
					if (g >= 0)
					{
						int[] grp = b.seamGroups[g];
						if (grp != null && grp.Length >= 2 && grp[0] == i)
						{
							Vector3 acc = Vector3.zero;
							for (int j = 0; j < grp.Length; j++)
							{
								acc += N[grp[j]];
							}
							for (int k = 0; k < grp.Length; k++)
							{
								N[grp[k]] = acc;
							}
						}
					}
				}
			}
			foreach (int l in idx)
			{
				float m = N[l].magnitude;
				N[l] = ((m > 1E-12f) ? (N[l] / m) : (-GW[l]));
				if (!b.mouthIn[l] && Vector3.Dot(N[l], -GW[l]) < 0f)
				{
					N[l] = -N[l];
				}
			}
		}

		// Token: 0x06000127 RID: 295 RVA: 0x0004B3C4 File Offset: 0x000495C4
		private void MouthLayer(SoftFleshController.Body b, Vector3[] P, Vector3[] R, Vector3[] GW)
		{
			int[] idx = b.mouthIdx;
			if (b.mouthTris == null || b.mouthW == null)
			{
				return;
			}
			Vector3[] N = this._mwN;
			Vector3[] NR = this._mwNR;
			float[] lift = this._mwLift;
			float[] lift2 = this._mwLift2;
			float[] raw = this._mwLiftRaw;
			this.MouthNormals(b, R, NR, GW);
			b.mwLifted = 0;
			b.mwLiftMax = 0f;
			for (int round = 0; round < 2; round++)
			{
				this.MouthNormals(b, P, N, GW);
				foreach (int i in idx)
				{
					lift[i] = 0f;
					raw[i] = 0f;
				}
				foreach (int j in idx)
				{
					if (!b.mouthIn[j] && b.mouthW[j] > 0f)
					{
						float d = 0f;
						if (b.mouthCovStart != null)
						{
							for (int k2 = b.mouthCovStart[j]; k2 < b.mouthCovStart[j + 1]; k2++)
							{
								int l = b.mouthCovList[k2];
								Vector3 dv = R[l] - R[j];
								float dd = dv.sqrMagnitude;
								if (dd <= 2.4000003E-05f)
								{
									float g = Vector3.Dot(-dv, NR[j]);
									if (g > 0f && g * g >= 0.25f * dd)
									{
										float need = ((g < 0.001f) ? g : 0.001f);
										float have = Vector3.Dot(P[j] - P[l], N[j]);
										if (need - have > d)
										{
											d = need - have;
										}
									}
								}
							}
						}
						if (b.mouthPartner != null)
						{
							for (int m = -1; m < 4; m++)
							{
								int n = ((m < 0) ? b.mouthPartner[j] : ((b.mouthPartners != null) ? b.mouthPartners[j * 4 + m] : (-1)));
								if (n >= 0 && b.mouthIn[n] && b.mouthW[n] > 0f && (b.mouthWet == null || !b.mouthWet[n]))
								{
									Vector3 dv2 = R[n] - R[j];
									float dd2 = dv2.sqrMagnitude;
									if (dd2 <= 0.0001f)
									{
										float g2 = Vector3.Dot(-dv2, NR[j]);
										if (g2 > 0f)
										{
											float need2 = ((g2 < 0.001f) ? g2 : 0.001f);
											float have2 = Vector3.Dot(P[j] - P[n], N[j]);
											if (need2 - have2 > d)
											{
												d = need2 - have2;
											}
										}
									}
								}
							}
						}
						lift[j] = d;
						raw[j] = d;
					}
				}
				int[] nbS = b.mouthNbrStart;
				int[] nbL = b.mouthNbrList;
				for (int pass = 0; pass < 3; pass++)
				{
					foreach (int j2 in idx)
					{
						lift2[j2] = lift[j2];
						if (!b.mouthIn[j2] && b.mouthW[j2] > 0f)
						{
							float acc = 0f;
							int cnt = 0;
							for (int k3 = nbS[j2]; k3 < nbS[j2 + 1]; k3++)
							{
								int m2 = nbL[k3];
								if (!b.mouthIn[m2] && b.mouthW[m2] > 0f)
								{
									acc += lift[m2];
									cnt++;
								}
							}
							lift2[j2] = ((cnt > 0) ? (lift[j2] * 0.5f + acc / (float)cnt * 0.5f) : lift[j2]);
						}
					}
					foreach (int j3 in idx)
					{
						lift[j3] = lift2[j3];
					}
				}
				foreach (int j4 in idx)
				{
					if (!b.mouthIn[j4] && b.mouthW[j4] > 0f && lift[j4] > 1E-05f)
					{
						P[j4] += N[j4] * (lift[j4] * b.mouthW[j4]);
						if (round == 0)
						{
							b.mwLifted++;
						}
						if (lift[j4] > b.mwLiftMax)
						{
							b.mwLiftMax = lift[j4];
						}
					}
				}
			}
		}

		// Token: 0x06000128 RID: 296 RVA: 0x0004B86C File Offset: 0x00049A6C
		private static bool SegTri(Vector3 p0, Vector3 p1, Vector3 a, Vector3 b, Vector3 c)
		{
			Vector3 dir = p1 - p0;
			float L = dir.magnitude;
			if (L < 1E-09f)
			{
				return false;
			}
			dir /= L;
			Vector3 e = b - a;
			Vector3 e2 = c - a;
			Vector3 pv = Vector3.Cross(dir, e2);
			float det = Vector3.Dot(e, pv);
			if (det > -1E-12f && det < 1E-12f)
			{
				return false;
			}
			float inv = 1f / det;
			Vector3 tv = p0 - a;
			float u = Vector3.Dot(tv, pv) * inv;
			if (u < 0f || u > 1f)
			{
				return false;
			}
			Vector3 qv = Vector3.Cross(tv, e);
			float v = Vector3.Dot(dir, qv) * inv;
			if (v < 0f || u + v > 1f)
			{
				return false;
			}
			float t = Vector3.Dot(e2, qv) * inv;
			return t > 1E-06f && t < L - 1E-06f;
		}

		// Token: 0x06000129 RID: 297 RVA: 0x0004B95C File Offset: 0x00049B5C
		private static bool TriCross(Vector3 a0, Vector3 a1, Vector3 a2, Vector3 b0, Vector3 b1, Vector3 b2)
		{
			return SoftFleshController.SegTri(a0, a1, b0, b1, b2) || SoftFleshController.SegTri(a1, a2, b0, b1, b2) || SoftFleshController.SegTri(a2, a0, b0, b1, b2) || SoftFleshController.SegTri(b0, b1, a0, a1, a2) || SoftFleshController.SegTri(b1, b2, a0, a1, a2) || SoftFleshController.SegTri(b2, b0, a0, a1, a2);
		}

		// Token: 0x0600012A RID: 298 RVA: 0x0004B9BC File Offset: 0x00049BBC
		private static Vector3 SkinNormalW(SoftFleshController.Body b, int i)
		{
			if (b.baseNormals == null || i >= b.baseNormals.Length)
			{
				return Vector3.up;
			}
			Quaternion q = ((b.domBone != null && b.boneRotCache != null && i < b.domBone.Length && b.domBone[i] >= 0 && b.domBone[i] < b.boneRotCache.Length) ? b.boneRotCache[b.domBone[i]] : Quaternion.identity);
			return q * b.baseNormals[i];
		}

		// Token: 0x0600012B RID: 299 RVA: 0x0004BA48 File Offset: 0x00049C48
		private static int Wt(SoftFleshController.Body b, int i)
		{
			if (b.seamOf == null || b.seamGroups == null || i >= b.seamOf.Length)
			{
				return i;
			}
			int g = b.seamOf[i];
			if (g < 0)
			{
				return i;
			}
			int[] grp = b.seamGroups[g];
			if (grp == null || grp.Length == 0)
			{
				return i;
			}
			return grp[0];
		}

		// Token: 0x0600012C RID: 300 RVA: 0x0004BA98 File Offset: 0x00049C98
		private static string MatOf(SoftFleshController.Body b, int t3)
		{
			int ti = t3 / 3;
			if (b.mouthTriSub == null || ti >= b.mouthTriSub.Length)
			{
				return "[?]";
			}
			int sm = (int)b.mouthTriSub[ti];
			return "[" + ((b.mouthMatNames != null && sm < b.mouthMatNames.Length) ? b.mouthMatNames[sm] : ("sub" + sm.ToString())) + "]";
		}

		// Token: 0x0600012D RID: 301 RVA: 0x0004BB08 File Offset: 0x00049D08
		private static string VertTag(SoftFleshController.Body b, int i)
		{
			string c = ((b.mouthLining != null && b.mouthLining[i]) ? "lining" : ((b.mouthIn != null && b.mouthIn[i]) ? "wall" : "skin"));
			return string.Concat(new string[]
			{
				"(",
				c,
				" w ",
				((b.mouthW != null && i < b.mouthW.Length) ? b.mouthW[i] : 0f).ToString("F2"),
				(b.mouthT != null && i < b.mouthT.Length) ? (" t " + (b.mouthT[i] * 1000f).ToString("F1")) : "",
				")"
			});
		}

		// Token: 0x0600012E RID: 302 RVA: 0x0004BBE8 File Offset: 0x00049DE8
		private static Vector3 ClosestOnTri(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
		{
			Vector3 ab = b - a;
			Vector3 ac = c - a;
			Vector3 ap = p - a;
			float d = Vector3.Dot(ab, ap);
			float d2 = Vector3.Dot(ac, ap);
			if (d <= 0f && d2 <= 0f)
			{
				return a;
			}
			Vector3 bp = p - b;
			float d3 = Vector3.Dot(ab, bp);
			float d4 = Vector3.Dot(ac, bp);
			if (d3 >= 0f && d4 <= d3)
			{
				return b;
			}
			float vc = d * d4 - d3 * d2;
			if (vc <= 0f && d >= 0f && d3 <= 0f)
			{
				float v = d / (d - d3);
				return a + ab * v;
			}
			Vector3 cp = p - c;
			float d5 = Vector3.Dot(ab, cp);
			float d6 = Vector3.Dot(ac, cp);
			if (d6 >= 0f && d5 <= d6)
			{
				return c;
			}
			float vb = d5 * d2 - d * d6;
			if (vb <= 0f && d2 >= 0f && d6 <= 0f)
			{
				float w = d2 / (d2 - d6);
				return a + ac * w;
			}
			float va = d3 * d6 - d5 * d4;
			if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
			{
				float w2 = (d4 - d3) / (d4 - d3 + (d5 - d6));
				return b + (c - b) * w2;
			}
			float den = 1f / (va + vb + vc);
			float v2 = vb * den;
			float w3 = vc * den;
			return a + ab * v2 + ac * w3;
		}

		// Token: 0x0600012F RID: 303 RVA: 0x0004BD9C File Offset: 0x00049F9C
		private float SignedToOuter(SoftFleshController.Body b, Vector3[] pos, List<int> outT, int[] tris, Vector3 p, out int triOut)
		{
			float best = float.MaxValue;
			Vector3 bestN = Vector3.zero;
			Vector3 bestC = Vector3.zero;
			triOut = -1;
			for (int i = 0; i < outT.Count; i++)
			{
				int t3 = outT[i];
				int a = tris[t3];
				int b2 = tris[t3 + 1];
				int c2 = tris[t3 + 2];
				Vector3 pa = pos[a];
				Vector3 pb = pos[b2];
				Vector3 pc = pos[c2];
				Vector3 cen = (pa + pb + pc) * 0.33333334f;
				if ((cen - p).sqrMagnitude <= 0.000225f)
				{
					Vector3 cl = SoftFleshController.ClosestOnTri(p, pa, pb, pc);
					float d = (cl - p).sqrMagnitude;
					if (d < best)
					{
						best = d;
						bestC = cl;
						triOut = t3;
						Vector3 nn = Vector3.Cross(pb - pa, pc - pa);
						Quaternion rq = ((b.domBone != null && b.boneRotCache != null && b.domBone[a] >= 0 && b.domBone[a] < b.boneRotCache.Length) ? b.boneRotCache[b.domBone[a]] : Quaternion.identity);
						Vector3 nRef = rq * b.baseNormals[a];
						if (Vector3.Dot(nn, nRef) < 0f)
						{
							nn = -nn;
						}
						bestN = nn;
					}
				}
			}
			if (triOut < 0)
			{
				return float.NaN;
			}
			float dist = Mathf.Sqrt(best);
			if (Vector3.Dot(p - bestC, bestN) >= 0f)
			{
				return dist;
			}
			return -dist;
		}

		// Token: 0x06000130 RID: 304 RVA: 0x0004BF4C File Offset: 0x0004A14C
		private Vector3 SkinRest(SoftFleshController.Body b, int i, float openF)
		{
			Vector3 pbind = b.baseVerts[i] + b.mouthDv[i] * openF;
			int nbM = b.skinMCache.Length;
			BoneWeight w = b.bwArr[i];
			Vector3 rw = Vector3.zero;
			if (w.weight0 > 0f && w.boneIndex0 < nbM)
			{
				rw += b.skinMCache[w.boneIndex0].MultiplyPoint3x4(pbind) * w.weight0;
			}
			if (w.weight1 > 0f && w.boneIndex1 < nbM)
			{
				rw += b.skinMCache[w.boneIndex1].MultiplyPoint3x4(pbind) * w.weight1;
			}
			if (w.weight2 > 0f && w.boneIndex2 < nbM)
			{
				rw += b.skinMCache[w.boneIndex2].MultiplyPoint3x4(pbind) * w.weight2;
			}
			if (w.weight3 > 0f && w.boneIndex3 < nbM)
			{
				rw += b.skinMCache[w.boneIndex3].MultiplyPoint3x4(pbind) * w.weight3;
			}
			return rw;
		}

		// Token: 0x06000131 RID: 305 RVA: 0x0004C0A6 File Offset: 0x0004A2A6
		private void MouthLaplace(SoftFleshController.Body b, Vector3[] P, Vector3[] P2, Vector3[] R)
		{
			this.MouthLaplace(b, P, P2, R, false);
		}

		// Token: 0x06000132 RID: 306 RVA: 0x0004C0B4 File Offset: 0x0004A2B4
		private void MouthLaplace(SoftFleshController.Body b, Vector3[] P, Vector3[] P2, Vector3[] R, bool keepLining)
		{
			int[] idx = b.mouthIdx;
			int[] nbS = b.mouthNbrStart;
			int[] nbL = b.mouthNbrList;
			bool[] lin = b.mouthLining;
			foreach (int i in idx)
			{
				Vector3 acc = Vector3.zero;
				int cnt = 0;
				if (keepLining && lin != null && lin[i])
				{
					P2[i] = P[i];
				}
				else
				{
					bool rim = b.mouthIn[i] && lin != null && !lin[i] && (b.mouthWet == null || !b.mouthWet[i]);
					for (int j = nbS[i]; j < nbS[i + 1]; j++)
					{
						int k = nbL[j];
						if (!rim || !lin[k])
						{
							acc += ((b.mouthW[k] > 0f) ? P[k] : R[k]);
							cnt++;
						}
					}
					P2[i] = ((cnt > 0) ? (P[i] + (acc / (float)cnt - P[i]) * 0.5f) : P[i]);
				}
			}
			foreach (int l in idx)
			{
				P[l] = P2[l];
			}
		}

		// Token: 0x06000133 RID: 307 RVA: 0x0004C220 File Offset: 0x0004A420
		private void BuildChain(Transform owner, Vector3 axU, Vector3 origin)
		{
			this._chN = 0;
			if (owner == null || this._segOwner == null)
			{
				return;
			}
			int pp = 0;
			while (pp < this._pressers.Count && pp < this._segLen2.Length && pp < this._segOwner.Length && this._chN < 62)
			{
				if (!(this._segOwner[pp] != owner) && this._segLen2[pp] >= 0f && this._segInsert[pp])
				{
					if (this._segLen2[pp] > 1E-06f)
					{
						this._chP[this._chN] = this._segA[pp];
						this._chRaw[this._chN] = this._segRad[pp];
						this._chLobe[this._chN] = pp;
						this._chN++;
						this._chP[this._chN] = this._segA[pp] + this._segAB[pp];
						this._chRaw[this._chN] = this._segRadB[pp];
						this._chLobe[this._chN] = pp;
						this._chN++;
					}
					else
					{
						this._chP[this._chN] = this._segA[pp] + this._segAB[pp] * 0.5f;
						this._chRaw[this._chN] = this._segRad[pp];
						this._chLobe[this._chN] = pp;
						this._chN++;
					}
				}
				pp++;
			}
			if (this._chN == 0)
			{
				return;
			}
			int w = 0;
			for (int i = 0; i < this._chN; i++)
			{
				if (w > 0 && (this._chP[i] - this._chP[w - 1]).sqrMagnitude < 1E-06f)
				{
					if (this._chRaw[i] > this._chRaw[w - 1])
					{
						this._chRaw[w - 1] = this._chRaw[i];
						this._chLobe[w - 1] = this._chLobe[i];
					}
				}
				else
				{
					this._chP[w] = this._chP[i];
					this._chRaw[w] = this._chRaw[i];
					this._chLobe[w] = this._chLobe[i];
					w++;
				}
			}
			this._chN = w;
			for (int j = 0; j < this._chN; j++)
			{
				if (j == 0 || j == this._chN - 1)
				{
					this._chR[j] = this._chRaw[j];
				}
				else
				{
					float r0 = this._chRaw[j - 1];
					float r = this._chRaw[j];
					float r2 = this._chRaw[j + 1];
					float lo = ((r0 < r) ? r0 : r);
					float hi = ((r0 > r) ? r0 : r);
					this._chR[j] = ((r2 < lo) ? lo : ((r2 > hi) ? hi : r2));
				}
			}
			for (int k = 0; k < this._chN; k++)
			{
				int lobe = this._chLobe[k];
				SoftFleshController.Prof pf = ((this._segProf != null && lobe < this._segProf.Length) ? this._segProf[lobe] : null);
				if (pf == null || this._pressers[lobe].a == null)
				{
					this._chRMax[k] = this._chRaw[k];
				}
				else
				{
					Matrix4x4 M = this._pressers[lobe].a.localToWorldMatrix;
					float fro = M.m00 * M.m00 + M.m01 * M.m01 + M.m02 * M.m02 + M.m10 * M.m10 + M.m11 * M.m11 + M.m12 * M.m12 + M.m20 * M.m20 + M.m21 * M.m21 + M.m22 * M.m22;
					this._chRMax[k] = pf.rMax * Mathf.Sqrt(fro) * this.itemRadScale;
				}
			}
		}

		// Token: 0x06000134 RID: 308 RVA: 0x0004C694 File Offset: 0x0004A894
		private bool ChainDirAt(Vector3 p, out Vector3 dir)
		{
			dir = Vector3.zero;
			if (this._chN < 2)
			{
				return false;
			}
			int bk = -1;
			float bd = float.MaxValue;
			int i = 0;
			while (i + 1 < this._chN)
			{
				Vector3 pa = this._chP[i];
				Vector3 ab = this._chP[i + 1] - pa;
				float l2 = ab.sqrMagnitude;
				float tt = ((l2 > 1E-12f) ? (Vector3.Dot(p - pa, ab) / l2) : 0f);
				if (tt < 0f)
				{
					tt = 0f;
				}
				else if (tt > 1f)
				{
					tt = 1f;
				}
				float d = (p - (pa + ab * tt)).sqrMagnitude;
				if (d < bd)
				{
					bd = d;
					bk = i;
				}
				i++;
			}
			if (bk < 0)
			{
				return false;
			}
			Vector3 acc = Vector3.zero;
			for (int j = bk - 1; j <= bk + 1; j++)
			{
				if (j >= 0 && j + 1 < this._chN)
				{
					Vector3 ab2 = this._chP[j + 1] - this._chP[j];
					float k = ab2.magnitude;
					if (k >= 1E-06f)
					{
						acc += ab2 / k * ((j == bk) ? 1f : 0.5f);
					}
				}
			}
			float am = acc.magnitude;
			if (am < 1E-06f)
			{
				return false;
			}
			dir = acc / am;
			return true;
		}

		// Token: 0x06000135 RID: 309 RVA: 0x0004C82C File Offset: 0x0004AA2C
		private bool ChainNearest(Vector3 p, out Vector3 np, out Vector3 u, out float r, out float sc)
		{
			np = Vector3.zero;
			u = Vector3.zero;
			r = 0f;
			sc = float.MaxValue;
			if (this._chN == 0)
			{
				return false;
			}
			if (this._chN != 1)
			{
				int i = 0;
				while (i + 1 < this._chN)
				{
					Vector3 pa = this._chP[i];
					Vector3 ab = this._chP[i + 1] - pa;
					float l2 = ab.sqrMagnitude;
					float t = ((l2 > 1E-12f) ? (Vector3.Dot(p - pa, ab) / l2) : 0f);
					if (t < 0f)
					{
						t = 0f;
					}
					else if (t > 1f)
					{
						t = 1f;
					}
					Vector3 q = pa + ab * t;
					Vector3 d = p - q;
					float dl = d.magnitude;
					if (dl >= 1E-06f)
					{
						float rk = this._chR[i] + (this._chR[i + 1] - this._chR[i]) * t;
						float s2 = dl - rk;
						if (s2 < sc)
						{
							sc = s2;
							np = q;
							u = d / dl;
							r = rk;
						}
					}
					i++;
				}
				return sc < float.MaxValue;
			}
			Vector3 d2 = p - this._chP[0];
			float dl2 = d2.magnitude;
			if (dl2 < 1E-06f)
			{
				return false;
			}
			np = this._chP[0];
			u = d2 / dl2;
			r = this._chR[0];
			sc = dl2 - r;
			return true;
		}

		// Token: 0x06000136 RID: 310 RVA: 0x0004C9DC File Offset: 0x0004ABDC
		private float RadiusInDir(int k, Vector3 p, Vector3 uW)
		{
			int lobe = this._chLobe[k];
			SoftFleshController.Prof pf = ((this._segProf != null && lobe < this._segProf.Length) ? this._segProf[lobe] : null);
			if (pf == null || this._pressers[lobe].a == null)
			{
				return this._chRaw[k];
			}
			int fr = Time.frameCount;
			if (this._lobeL2W == null || this._lobeL2W.Length <= lobe)
			{
				int nS0 = this._segLen2.Length;
				this._lobeL2W = new Matrix4x4[nS0];
				this._lobeL2WFrame = new int[nS0];
				this._lobeUniS = new float[nS0];
			}
			if (this._lobeL2WFrame[lobe] != fr)
			{
				Matrix4x4 M = this._pressers[lobe].a.localToWorldMatrix;
				this._lobeL2W[lobe] = M;
				this._lobeL2WFrame[lobe] = fr;
				float c0 = Mathf.Sqrt(M.m00 * M.m00 + M.m10 * M.m10 + M.m20 * M.m20);
				float c = Mathf.Sqrt(M.m01 * M.m01 + M.m11 * M.m11 + M.m21 * M.m21);
				float c2 = Mathf.Sqrt(M.m02 * M.m02 + M.m12 * M.m12 + M.m22 * M.m22);
				float cMin = ((c0 < c) ? ((c0 < c2) ? c0 : c2) : ((c < c2) ? c : c2));
				float cMax = ((c0 > c) ? ((c0 > c2) ? c0 : c2) : ((c > c2) ? c : c2));
				this._lobeUniS[lobe] = ((cMax - cMin < 0.0001f * cMax) ? ((c0 + c + c2) * 0.33333334f) : (-1f));
			}
			Vector3 pl = this._segW2L[lobe].MultiplyPoint3x4(p);
			float rL = pf.SampleRadius(pl);
			float sDir = this._lobeUniS[lobe];
			if (sDir <= 0f)
			{
				Vector3 dirL = this._segW2L[lobe].MultiplyVector(uW);
				float dm = dirL.magnitude;
				if (dm < 1E-09f)
				{
					return this._chRaw[k];
				}
				dirL /= dm;
				sDir = this._lobeL2W[lobe].MultiplyVector(dirL).magnitude;
			}
			return rL * sDir * this.itemRadScale;
		}

		// Token: 0x06000137 RID: 311 RVA: 0x0004CC64 File Offset: 0x0004AE64
		private bool ItemNearest(Transform owner, Vector3 p, out Vector3 np, out Vector3 u, out float r, out float sc, out int link, out float rA, out float rB)
		{
			return this.ItemNearest(owner, p, float.MaxValue, out np, out u, out r, out sc, out link, out rA, out rB);
		}

		// Token: 0x06000138 RID: 312 RVA: 0x0004CC8C File Offset: 0x0004AE8C
		private bool ItemNearest(Transform owner, Vector3 p, float farCut, out Vector3 np, out Vector3 u, out float r, out float sc, out int link, out float rA, out float rB)
		{
			np = Vector3.zero;
			u = Vector3.zero;
			r = 0f;
			sc = float.MaxValue;
			link = -1;
			rA = 0f;
			rB = 0f;
			if (owner == null || this._chN == 0)
			{
				return false;
			}
			int nSeg = this._segLen2.Length;
			if (this._lobeL2W == null || this._lobeL2W.Length < nSeg)
			{
				this._lobeL2W = new Matrix4x4[nSeg];
				this._lobeL2WFrame = new int[nSeg];
				this._lobeUniS = new float[nSeg];
			}
			if (this._chN != 1)
			{
				int nL = this._chN - 1;
				for (int i = 0; i < nL; i++)
				{
					Vector3 pa = this._chP[i];
					Vector3 ab = this._chP[i + 1] - pa;
					float l2 = ab.sqrMagnitude;
					float tt = ((l2 > 1E-12f) ? (Vector3.Dot(p - pa, ab) / l2) : 0f);
					float tc = ((tt < 0f) ? 0f : ((tt > 1f) ? 1f : tt));
					Vector3 q = pa + ab * tc;
					float dl = (p - q).magnitude;
					float j = ((this._chRMax[i] > this._chRMax[i + 1]) ? this._chRMax[i] : this._chRMax[i + 1]);
					bool cap = (i == 0 && tt < 0f) || (i == nL - 1 && tt > 1f);
					this._inTt[i] = tt;
					this._inDl[i] = dl;
					this._inBound[i] = dl - (cap ? 2f : 1f) * j - 1E-05f;
					this._inDone[i] = dl < 1E-07f;
				}
				if (farCut < 3.4028235E+38f)
				{
					float minB = float.MaxValue;
					for (int k = 0; k < nL; k++)
					{
						if (!this._inDone[k] && this._inBound[k] < minB)
						{
							minB = this._inBound[k];
						}
					}
					if (minB >= farCut)
					{
						sc = minB;
						return false;
					}
				}
				for (;;)
				{
					int m = -1;
					float bb = float.MaxValue;
					for (int n = 0; n < nL; n++)
					{
						if (!this._inDone[n] && this._inBound[n] < bb)
						{
							bb = this._inBound[n];
							m = n;
						}
					}
					if (m < 0 || bb >= sc)
					{
						break;
					}
					this._inDone[m] = true;
					Vector3 pa2 = this._chP[m];
					Vector3 ab2 = this._chP[m + 1] - pa2;
					float l3 = ab2.sqrMagnitude;
					float tt2 = this._inTt[m];
					float tc2 = ((tt2 < 0f) ? 0f : ((tt2 > 1f) ? 1f : tt2));
					Vector3 q2 = pa2 + ab2 * tc2;
					Vector3 lat = p - q2;
					float dl2 = this._inDl[m];
					Vector3 uW = lat / dl2;
					float ra = this.RadiusInDir(m, p, uW);
					float rb = this.RadiusInDir(m + 1, p, uW);
					float rr = ra + (rb - ra) * tc2;
					bool capA = m == 0 && tt2 < 0f;
					bool capB = m == this._chN - 2 && tt2 > 1f;
					float s2;
					Vector3 u2;
					Vector3 np2;
					if ((capA || capB) && l3 > 1E-12f)
					{
						Vector3 axn = ab2 / Mathf.Sqrt(l3);
						Vector3 C = (capA ? (pa2 + axn * rr) : (pa2 + ab2 - axn * rr));
						Vector3 dc = p - C;
						float dcl = dc.magnitude;
						if (dcl < 1E-07f)
						{
							continue;
						}
						s2 = dcl - rr;
						u2 = dc / dcl;
						np2 = C;
					}
					else
					{
						s2 = dl2 - rr;
						u2 = uW;
						np2 = q2;
					}
					if (s2 < sc)
					{
						sc = s2;
						u = u2;
						np = np2;
						r = rr;
						link = m;
						rA = ra;
						rB = rb;
					}
				}
				return sc < float.MaxValue;
			}
			Vector3 d0 = p - this._chP[0];
			float dl3 = d0.magnitude;
			if (dl3 < 1E-07f)
			{
				return false;
			}
			u = d0 / dl3;
			r = this.RadiusInDir(0, p, u);
			np = this._chP[0];
			sc = dl3 - r;
			link = 0;
			rA = r;
			rB = r;
			return true;
		}

		// Token: 0x06000139 RID: 313 RVA: 0x0004D16C File Offset: 0x0004B36C
		private bool ItemNearestCachedV(int v, Vector3 p, int k, out Vector3 np, out Vector3 u, out float r, out float sc)
		{
			this._mwFreshVert = v;
			bool okv = this.ItemNearestCached(p, k, this._mwRA[v], this._mwRB[v], out np, out u, out r, out sc);
			this._mwFreshVert = -1;
			return okv;
		}

		// Token: 0x0600013A RID: 314 RVA: 0x0004D1AC File Offset: 0x0004B3AC
		private bool ItemNearestCached(Vector3 p, int k, float ra, float rb, out Vector3 np, out Vector3 u, out float r, out float sc)
		{
			np = Vector3.zero;
			u = Vector3.zero;
			r = 0f;
			sc = float.MaxValue;
			if (k < 0 || k + 1 >= this._chN)
			{
				if (k != 0 || this._chN != 1)
				{
					return false;
				}
				Vector3 d0 = p - this._chP[0];
				float dl0 = d0.magnitude;
				if (dl0 < 1E-07f)
				{
					return false;
				}
				u = d0 / dl0;
				r = (this._mwFresh ? this.RadiusInDir(0, p, u) : ra);
				np = this._chP[0];
				sc = dl0 - r;
				return true;
			}
			else
			{
				Vector3 pa = this._chP[k];
				Vector3 ab = this._chP[k + 1] - pa;
				float l2 = ab.sqrMagnitude;
				float tt = ((l2 > 1E-12f) ? (Vector3.Dot(p - pa, ab) / l2) : 0f);
				float tc = ((tt < 0f) ? 0f : ((tt > 1f) ? 1f : tt));
				if (this._mwFresh)
				{
					Vector3 qf = pa + ab * tc;
					Vector3 latf = p - qf;
					float dlf = latf.magnitude;
					if (dlf > 1E-07f)
					{
						Vector3 uf = latf / dlf;
						int v = this._mwFreshVert;
						if (v >= 0 && this._mwUdir != null && Vector3.Dot(this._mwUdir[v], uf) > 0.995f)
						{
							ra = this._mwRA[v];
							rb = this._mwRB[v];
						}
						else
						{
							ra = this.RadiusInDir(k, p, uf);
							rb = this.RadiusInDir(k + 1, p, uf);
							if (v >= 0 && this._mwUdir != null)
							{
								this._mwUdir[v] = uf;
								this._mwRA[v] = ra;
								this._mwRB[v] = rb;
							}
						}
					}
				}
				float rr = ra + (rb - ra) * tc;
				bool capA = k == 0 && tt < 0f;
				bool capB = k == this._chN - 2 && tt > 1f;
				if ((capA || capB) && l2 > 1E-12f)
				{
					Vector3 axn = ab / Mathf.Sqrt(l2);
					Vector3 C = (capA ? (pa + axn * rr) : (pa + ab - axn * rr));
					Vector3 dc = p - C;
					float dcl = dc.magnitude;
					if (dcl < 1E-07f)
					{
						return false;
					}
					sc = dcl - rr;
					u = dc / dcl;
					np = C;
					r = rr;
					return true;
				}
				else
				{
					Vector3 q = pa + ab * tc;
					Vector3 lat = p - q;
					float dl = lat.magnitude;
					if (dl < 1E-07f)
					{
						return false;
					}
					sc = dl - rr;
					u = lat / dl;
					np = q;
					r = rr;
					return true;
				}
			}
		}

		// Token: 0x0600013B RID: 315 RVA: 0x0004D4D4 File Offset: 0x0004B6D4
		private bool ItemMeanRadiusAt(Vector3 c, out float rMean, out float clear)
		{
			rMean = 0f;
			clear = float.MaxValue;
			if (this._chN == 0)
			{
				return false;
			}
			int nSeg = this._segLen2.Length;
			if (this._lobeL2W == null || this._lobeL2W.Length < nSeg)
			{
				this._lobeL2W = new Matrix4x4[nSeg];
				this._lobeL2WFrame = new int[nSeg];
				this._lobeUniS = new float[nSeg];
			}
			int bk = -1;
			float bt = 0f;
			float bd = float.MaxValue;
			if (this._chN == 1)
			{
				bk = 0;
				bt = 0f;
				bd = (c - this._chP[0]).magnitude;
			}
			else
			{
				int i = 0;
				while (i + 1 < this._chN)
				{
					Vector3 pa = this._chP[i];
					Vector3 ab = this._chP[i + 1] - pa;
					float l2 = ab.sqrMagnitude;
					float tt = ((l2 > 1E-12f) ? (Vector3.Dot(c - pa, ab) / l2) : 0f);
					if (tt < 0f)
					{
						tt = 0f;
					}
					else if (tt > 1f)
					{
						tt = 1f;
					}
					float d = (c - (pa + ab * tt)).magnitude;
					if (d < bd)
					{
						bd = d;
						bk = i;
						bt = tt;
					}
					i++;
				}
			}
			if (bk < 0)
			{
				return false;
			}
			float ma = this.MeanRadiusOf(bk, c);
			float mb = ((this._chN == 1) ? ma : this.MeanRadiusOf(bk + 1, c));
			rMean = ma + (mb - ma) * bt;
			clear = bd - rMean;
			return true;
		}

		// Token: 0x0600013C RID: 316 RVA: 0x0004D678 File Offset: 0x0004B878
		private float MeanRadiusOf(int k, Vector3 p)
		{
			int lobe = this._chLobe[k];
			SoftFleshController.Prof pf = ((this._segProf != null && lobe < this._segProf.Length) ? this._segProf[lobe] : null);
			if (pf == null)
			{
				return this._chRaw[k];
			}
			return pf.MeanRadiusAt(this._segW2L[lobe].MultiplyPoint3x4(p)) * this._segScale[lobe] * this.itemRadScale;
		}

		// Token: 0x0600013D RID: 317 RVA: 0x0004D6E4 File Offset: 0x0004B8E4
		private float PartnersClearance(SoftFleshController.Body b, Vector3[] P, int i, float OFF)
		{
			float baseC = OFF;
			for (int j = -1; j < 4; j++)
			{
				int pj = ((j < 0) ? ((b.mouthPartner != null) ? b.mouthPartner[i] : (-1)) : ((b.mouthPartners != null) ? b.mouthPartners[i * 4 + j] : (-1)));
				if (pj >= 0 && this._mwSeg[pj] >= 0 && this._mwClr[pj] < 3.4028235E+38f && this._mwClr[pj] > baseC)
				{
					baseC = this._mwClr[pj];
				}
			}
			return baseC;
		}

		// Token: 0x0600013E RID: 318 RVA: 0x0004D764 File Offset: 0x0004B964
		private bool BuildInnerGrid(SoftFleshController.Body b, Vector3[] P)
		{
			int[] idx = b.mouthIdx;
			Vector3 gMin;
			gMin..ctor(float.MaxValue, float.MaxValue, float.MaxValue);
			Vector3 gMax;
			gMax..ctor(float.MinValue, float.MinValue, float.MinValue);
			int nInner = 0;
			foreach (int i in idx)
			{
				if (this._mwSeg[i] >= 0 && b.mouthIn[i] && this._mwClr[i] != 3.4028235E+38f)
				{
					Vector3 pp = P[i];
					nInner++;
					if (pp.x < gMin.x)
					{
						gMin.x = pp.x;
					}
					if (pp.y < gMin.y)
					{
						gMin.y = pp.y;
					}
					if (pp.z < gMin.z)
					{
						gMin.z = pp.z;
					}
					if (pp.x > gMax.x)
					{
						gMax.x = pp.x;
					}
					if (pp.y > gMax.y)
					{
						gMax.y = pp.y;
					}
					if (pp.z > gMax.z)
					{
						gMax.z = pp.z;
					}
				}
			}
			if (nInner == 0)
			{
				return false;
			}
			this._gMin = gMin - new Vector3(0.008f, 0.008f, 0.008f);
			this._gNx = (int)((gMax.x - this._gMin.x) / 0.008f) + 2;
			this._gNy = (int)((gMax.y - this._gMin.y) / 0.008f) + 2;
			this._gNz = (int)((gMax.z - this._gMin.z) / 0.008f) + 2;
			if (this._gNx > 64)
			{
				this._gNx = 64;
			}
			if (this._gNy > 64)
			{
				this._gNy = 64;
			}
			if (this._gNz > 64)
			{
				this._gNz = 64;
			}
			int cells = this._gNx * this._gNy * this._gNz;
			if (this._gHead == null || this._gHead.Length < cells)
			{
				this._gHead = new int[(cells < 4096) ? 4096 : cells];
			}
			for (int c = 0; c < cells; c++)
			{
				this._gHead[c] = -1;
			}
			foreach (int j in idx)
			{
				if (this._mwSeg[j] >= 0 && b.mouthIn[j] && this._mwClr[j] != 3.4028235E+38f)
				{
					int c2 = this.GridCell(P[j]);
					if (c2 >= 0)
					{
						this._gNext[j] = this._gHead[c2];
						this._gHead[c2] = j;
					}
				}
			}
			return true;
		}

		// Token: 0x0600013F RID: 319 RVA: 0x0004DA44 File Offset: 0x0004BC44
		private bool GridBuildSel(SoftFleshController.Body b, Vector3[] P, int[] idx, int mode)
		{
			Vector3 gMin;
			gMin..ctor(float.MaxValue, float.MaxValue, float.MaxValue);
			Vector3 gMax;
			gMax..ctor(float.MinValue, float.MinValue, float.MinValue);
			int i = 0;
			foreach (int j in idx)
			{
				if (this.GridTake(b, j, mode))
				{
					Vector3 pp = P[j];
					i++;
					if (pp.x < gMin.x)
					{
						gMin.x = pp.x;
					}
					if (pp.y < gMin.y)
					{
						gMin.y = pp.y;
					}
					if (pp.z < gMin.z)
					{
						gMin.z = pp.z;
					}
					if (pp.x > gMax.x)
					{
						gMax.x = pp.x;
					}
					if (pp.y > gMax.y)
					{
						gMax.y = pp.y;
					}
					if (pp.z > gMax.z)
					{
						gMax.z = pp.z;
					}
				}
			}
			if (i == 0)
			{
				return false;
			}
			this._gMin = gMin - new Vector3(0.008f, 0.008f, 0.008f);
			this._gNx = (int)((gMax.x - this._gMin.x) / 0.008f) + 2;
			this._gNy = (int)((gMax.y - this._gMin.y) / 0.008f) + 2;
			this._gNz = (int)((gMax.z - this._gMin.z) / 0.008f) + 2;
			if (this._gNx > 64 || this._gNy > 64 || this._gNz > 64)
			{
				return false;
			}
			int cells = this._gNx * this._gNy * this._gNz;
			if (this._gHead == null || this._gHead.Length < cells)
			{
				this._gHead = new int[(cells < 4096) ? 4096 : cells];
			}
			for (int c = 0; c < cells; c++)
			{
				this._gHead[c] = -1;
			}
			foreach (int k in idx)
			{
				if (this.GridTake(b, k, mode))
				{
					int c2 = this.GridCell(P[k]);
					if (c2 >= 0)
					{
						this._gNext[k] = this._gHead[c2];
						this._gHead[c2] = k;
					}
				}
			}
			return true;
		}

		// Token: 0x06000140 RID: 320 RVA: 0x0004DCC6 File Offset: 0x0004BEC6
		private bool GridTake(SoftFleshController.Body b, int i, int mode)
		{
			if (mode == 0)
			{
				return !b.mouthIn[i];
			}
			return b.mouthIn[i] && (b.mouthLining == null || !b.mouthLining[i]);
		}

		// Token: 0x06000141 RID: 321 RVA: 0x0004DCF8 File Offset: 0x0004BEF8
		private float GridUnder(Vector3[] P, Vector3 pp, float baseC)
		{
			return this.GridUnder(P, pp, baseC, 0.008f);
		}

		// Token: 0x06000142 RID: 322 RVA: 0x0004DD08 File Offset: 0x0004BF08
		private float GridUnder(Vector3[] P, Vector3 pp, float baseC, float rad)
		{
			int cx = (int)((pp.x - this._gMin.x) / 0.008f);
			int cy = (int)((pp.y - this._gMin.y) / 0.008f);
			int cz = (int)((pp.z - this._gMin.z) / 0.008f);
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					for (int dz = -1; dz <= 1; dz++)
					{
						int x = cx + dx;
						int y = cy + dy;
						int z = cz + dz;
						if (x >= 0 && y >= 0 && z >= 0 && x < this._gNx && y < this._gNy && z < this._gNz)
						{
							for (int i = this._gHead[(x * this._gNy + y) * this._gNz + z]; i >= 0; i = this._gNext[i])
							{
								if ((P[i] - pp).sqrMagnitude < rad * rad && this._mwClr[i] > baseC)
								{
									baseC = this._mwClr[i];
								}
							}
						}
					}
				}
			}
			return baseC;
		}

		// Token: 0x06000143 RID: 323 RVA: 0x0004DE50 File Offset: 0x0004C050
		private void CoherentExit(Vector3 p, int link, float rB, float sBest, Vector3 gw, ref Vector3 cB, ref Vector3 uB)
		{
			if (sBest >= 0f || link < 0 || link + 1 >= this._chN)
			{
				return;
			}
			float dl = sBest + rB;
			if (dl >= 0.5f * rB)
			{
				return;
			}
			Vector3 pa = this._chP[link];
			Vector3 ab = this._chP[link + 1] - pa;
			float l2 = ab.sqrMagnitude;
			if (l2 < 1E-12f)
			{
				return;
			}
			float tt = Vector3.Dot(p - pa, ab) / l2;
			if (tt < 0f || tt > 1f)
			{
				return;
			}
			Vector3 axn = ab / Mathf.Sqrt(l2);
			Vector3 g = -gw;
			g -= axn * Vector3.Dot(g, axn);
			if (g.sqrMagnitude < 0.01f)
			{
				return;
			}
			uB = g.normalized;
			cB = pa + ab * tt;
		}

		// Token: 0x06000144 RID: 324 RVA: 0x0004DF40 File Offset: 0x0004C140
		private int GridCell(Vector3 p)
		{
			int x = (int)((p.x - this._gMin.x) / 0.008f);
			int y = (int)((p.y - this._gMin.y) / 0.008f);
			int z = (int)((p.z - this._gMin.z) / 0.008f);
			if (x < 0 || y < 0 || z < 0 || x >= this._gNx || y >= this._gNy || z >= this._gNz)
			{
				return -1;
			}
			return (x * this._gNy + y) * this._gNz + z;
		}

		// Token: 0x06000145 RID: 325 RVA: 0x0004DFD8 File Offset: 0x0004C1D8
		private void MouthOutside(SoftFleshController.Body b, Vector3[] P, Vector3[] GW, Transform owner, float OFF)
		{
			foreach (int i in b.mouthIdx)
			{
				if (this._mwSeg[i] >= 0 && b.mouthIn[i])
				{
					Vector3 cB;
					Vector3 uB;
					float rB;
					float sBest;
					if (!this.ItemNearestCachedV(i, P[i], this._mwSeg[i], out cB, out uB, out rB, out sBest))
					{
						this._mwClr[i] = float.MaxValue;
					}
					else if ((b.mouthLining != null && b.mouthLining[i]) || (b.mouthWet != null && b.mouthWet[i]))
					{
						this._mwClr[i] = sBest;
					}
					else
					{
						this.CoherentExit(P[i], this._mwSeg[i], rB, sBest, this._mwGR[i], ref cB, ref uB);
						float offv = this._mwOffr[i];
						if (sBest < offv)
						{
							float mv = offv - sBest;
							P[i] = cB + uB * (rB + offv);
							sBest = offv;
							b.mwFinIn++;
							if (mv > b.mwFinInMax)
							{
								b.mwFinInMax = mv;
							}
						}
						this._mwClr[i] = sBest;
					}
				}
			}
			bool gridNow = this.BuildInnerGrid(b, P);
			int[] idx;
			foreach (int j in idx)
			{
				this._mwShell[j] = -1f;
			}
			foreach (int k in idx)
			{
				this._mwUnder[k] = -1f;
				if (this._mwSeg[k] >= 0 && !b.mouthIn[k])
				{
					float u0 = this.PartnersClearance(b, P, k, OFF);
					if (gridNow)
					{
						u0 = this.GridUnder(P, P[k], u0, 0.005f);
					}
					this._mwUnder[k] = u0 + 0.001f;
				}
			}
			int[] nbS = b.mouthNbrStart;
			int[] nbL = b.mouthNbrList;
			float[] req = this._mwReq;
			float[] req2 = this._mwRB2;
			foreach (int l in idx)
			{
				req[l] = -1f;
				if (this._mwSeg[l] >= 0 && !b.mouthIn[l])
				{
					float baseR = this.PartnersClearance(b, P, l, OFF);
					if (gridNow)
					{
						baseR = this.GridUnder(P, P[l], baseR, 0.005f);
					}
					float sh = this.SheetOf(b, l) * b.mouthW[l];
					req[l] = baseR + sh;
				}
			}
			for (int pass = 0; pass < 3; pass++)
			{
				foreach (int m in idx)
				{
					req2[m] = req[m];
					if (req[m] >= 0f)
					{
						float acc = 0f;
						int cnt = 0;
						for (int n = nbS[m]; n < nbS[m + 1]; n++)
						{
							int j2 = nbL[n];
							if (b.mouthW[j2] > 0f && !b.mouthIn[j2] && req[j2] >= 0f)
							{
								acc += req[j2];
								cnt++;
							}
						}
						if (cnt > 0)
						{
							req2[m] = req[m] + (acc / (float)cnt - req[m]) * 0.5f;
						}
					}
				}
				foreach (int i2 in idx)
				{
					req[i2] = req2[i2];
				}
			}
			foreach (int i3 in idx)
			{
				Vector3 cB2;
				Vector3 uB2;
				float rB2;
				float sBest2;
				if (this._mwSeg[i3] < 0 || b.mouthIn[i3])
				{
					this._mwShellTmp[i3] = float.MaxValue;
				}
				else if (!this.ItemNearestCachedV(i3, P[i3], this._mwSeg[i3], out cB2, out uB2, out rB2, out sBest2))
				{
					this._mwShellTmp[i3] = float.MaxValue;
				}
				else
				{
					this.CoherentExit(P[i3], this._mwSeg[i3], rB2, sBest2, this._mwGR[i3], ref cB2, ref uB2);
					float under = ((this._mwReq[i3] >= 0f) ? this._mwReq[i3] : OFF);
					if (under < OFF)
					{
						under = OFF;
					}
					if (sBest2 < under)
					{
						float mv2 = under - sBest2;
						P[i3] = cB2 + uB2 * (rB2 + under);
						b.mwFinOut++;
						if (mv2 > b.mwFinOutMax)
						{
							b.mwFinOutMax = mv2;
						}
						sBest2 = under;
					}
					this._mwShellTmp[i3] = sBest2;
				}
			}
			for (int round = 0; round < 4; round++)
			{
				int[] nbS2 = b.mouthNbrStart;
				int[] nbL2 = b.mouthNbrList;
				Vector3[] P2 = this._mwP2;
				foreach (int i4 in idx)
				{
					if (!b.mouthIn[i4])
					{
						Vector3 acc2 = Vector3.zero;
						int cnt2 = 0;
						for (int k2 = nbS2[i4]; k2 < nbS2[i4 + 1]; k2++)
						{
							int j3 = nbL2[k2];
							acc2 += ((b.mouthW[j3] > 0f) ? P[j3] : this._mwR[j3]);
							cnt2++;
						}
						P2[i4] = ((cnt2 > 0) ? (P[i4] + (acc2 / (float)cnt2 - P[i4]) * 0.5f) : P[i4]);
					}
				}
				foreach (int i5 in idx)
				{
					if (!b.mouthIn[i5])
					{
						P[i5] = P2[i5];
					}
				}
				foreach (int i6 in idx)
				{
					Vector3 cB3;
					Vector3 uB3;
					float rB3;
					float sBest3;
					if (this._mwSeg[i6] >= 0 && !b.mouthIn[i6] && this.ItemNearestCachedV(i6, P[i6], this._mwSeg[i6], out cB3, out uB3, out rB3, out sBest3))
					{
						this.CoherentExit(P[i6], this._mwSeg[i6], rB3, sBest3, this._mwGR[i6], ref cB3, ref uB3);
						float under2 = ((this._mwReq[i6] >= 0f) ? this._mwReq[i6] : OFF);
						if (under2 < OFF)
						{
							under2 = OFF;
						}
						if (sBest3 < under2)
						{
							P[i6] = cB3 + uB3 * (rB3 + under2);
							sBest3 = under2;
						}
						this._mwShellTmp[i6] = sBest3;
					}
				}
			}
			for (int round2 = 0; round2 < 0; round2++)
			{
				int[] nbS3 = b.mouthNbrStart;
				int[] nbL3 = b.mouthNbrList;
				float[] cf = this._mwShellTmp;
				float[] cf2 = this._mwRB2;
				foreach (int i7 in idx)
				{
					cf[i7] = float.MaxValue;
					Vector3 cB4;
					Vector3 uB4;
					float rB4;
					float sBest4;
					if (this._mwSeg[i7] >= 0 && !b.mouthIn[i7] && this.ItemNearestCachedV(i7, P[i7], this._mwSeg[i7], out cB4, out uB4, out rB4, out sBest4))
					{
						cf[i7] = sBest4;
					}
				}
				for (int pass2 = 0; pass2 < 4; pass2++)
				{
					float lam = (((pass2 & 1) == 0) ? 0.5f : (-0.53f));
					foreach (int i8 in idx)
					{
						cf2[i8] = cf[i8];
						if (cf[i8] != 3.4028235E+38f)
						{
							float acc3 = 0f;
							int cnt3 = 0;
							for (int k3 = nbS3[i8]; k3 < nbS3[i8 + 1]; k3++)
							{
								int j4 = nbL3[k3];
								if (b.mouthW[j4] > 0f && !b.mouthIn[j4] && cf[j4] != 3.4028235E+38f)
								{
									acc3 += cf[j4];
									cnt3++;
								}
							}
							if (cnt3 > 0)
							{
								cf2[i8] = cf[i8] + (acc3 / (float)cnt3 - cf[i8]) * lam;
							}
						}
					}
					foreach (int i9 in idx)
					{
						cf[i9] = cf2[i9];
					}
				}
				foreach (int i10 in idx)
				{
					Vector3 cB5;
					Vector3 uB5;
					float rB5;
					float sBest5;
					if (this._mwSeg[i10] >= 0 && !b.mouthIn[i10] && cf[i10] != 3.4028235E+38f && this.ItemNearestCachedV(i10, P[i10], this._mwSeg[i10], out cB5, out uB5, out rB5, out sBest5))
					{
						this.CoherentExit(P[i10], this._mwSeg[i10], rB5, sBest5, this._mwGR[i10], ref cB5, ref uB5);
						float under3 = ((this._mwReq[i10] >= 0f) ? this._mwReq[i10] : OFF);
						if (under3 < OFF)
						{
							under3 = OFF;
						}
						float tgt = ((cf[i10] > under3) ? cf[i10] : under3);
						P[i10] = cB5 + uB5 * (rB5 + tgt);
						this._mwShellTmp[i10] = tgt;
					}
				}
			}
			foreach (int i11 in idx)
			{
				if (this._mwSeg[i11] >= 0 && !b.mouthIn[i11] && this._mwShellTmp[i11] != 3.4028235E+38f)
				{
					float sBest6 = this._mwShellTmp[i11];
					for (int k4 = -1; k4 < 4; k4++)
					{
						int pj = ((k4 < 0) ? ((b.mouthPartner != null) ? b.mouthPartner[i11] : (-1)) : ((b.mouthPartners != null) ? b.mouthPartners[i11 * 4 + k4] : (-1)));
						if (pj >= 0 && this._mwSeg[pj] >= 0 && (this._mwShell[pj] < 0f || sBest6 < this._mwShell[pj]))
						{
							this._mwShell[pj] = sBest6;
						}
					}
				}
			}
			foreach (int i12 in idx)
			{
				if (this._mwSeg[i12] >= 0 && b.mouthIn[i12] && this._mwShell[i12] >= 0f && (b.mouthLining == null || !b.mouthLining[i12]) && (b.mouthWet == null || !b.mouthWet[i12]))
				{
					float cap = this._mwShell[i12] - 0.0005f;
					if (cap < this._mwOffr[i12])
					{
						cap = this._mwOffr[i12];
					}
					Vector3 cB6;
					Vector3 uB6;
					float rB6;
					float sBest7;
					if (this._mwClr[i12] > cap + 1E-05f && this.ItemNearestCachedV(i12, P[i12], this._mwSeg[i12], out cB6, out uB6, out rB6, out sBest7))
					{
						this.CoherentExit(P[i12], this._mwSeg[i12], rB6, sBest7, this._mwGR[i12], ref cB6, ref uB6);
						P[i12] = cB6 + uB6 * (rB6 + cap);
						this._mwClr[i12] = cap;
						b.mwFinIn++;
					}
				}
			}
		}

		// Token: 0x06000146 RID: 326 RVA: 0x0004EB50 File Offset: 0x0004CD50
		private void MouthConstrain(SoftFleshController.Body b, Vector3[] P, Vector3[] GW, Transform owner, float OFF, bool record)
		{
			int[] idx = b.mouthIdx;
			bool grid = false;
			int q = 0;
			while (q < idx.Length)
			{
				int i = idx[q];
				Vector3 p = P[i];
				Vector3 cB;
				Vector3 uB;
				float rB;
				float sBest;
				int lobeB;
				if (record)
				{
					float farCut = (b.mouthIn[i] ? float.MaxValue : 0.02f);
					float ra;
					float rb2;
					if (this.ItemNearest(owner, p, farCut, out cB, out uB, out rB, out sBest, out lobeB, out ra, out rb2))
					{
						this._mwSeg[i] = lobeB;
						this._mwRA[i] = ra;
						this._mwRB[i] = rb2;
						this._mwClr[i] = sBest;
						this._mwUdir[i] = uB;
						goto IL_0116;
					}
					this._mwSeg[i] = -1;
					if (sBest < 3.4028235E+38f)
					{
						this._mwClr[i] = sBest;
						this._mwPen[i] = -sBest;
						this._mwDir[i] = Vector3.zero;
						this._mwDraw[i] = 0f;
					}
					else
					{
						this._mwClr[i] = float.MaxValue;
					}
				}
				else
				{
					lobeB = this._mwSeg[i];
					if (lobeB >= 0 && this.ItemNearestCachedV(i, p, lobeB, out cB, out uB, out rB, out sBest))
					{
						goto IL_0116;
					}
				}
				IL_04A9:
				q++;
				continue;
				IL_0116:
				this.CoherentExit(p, lobeB, rB, sBest, this._mwGR[i], ref cB, ref uB);
				if (record)
				{
					this._mwPen[i] = -sBest;
					this._mwDir[i] = uB;
					this._mwDraw[i] = ((sBest <= 0.012f) ? 1f : ((sBest >= 0.018f) ? 0f : (1f - (sBest - 0.012f) / 0.006f)));
				}
				if (b.mouthIn[i] && b.mouthLining != null && b.mouthLining[i])
				{
					float target = rB + OFF;
					this._mwClr[i] = sBest;
					this._mwDbgLobe[i] = lobeB;
					this._mwDbgRB[i] = rB;
					this._mwDbgDNow[i] = sBest + rB;
					this._mwDbgTarget[i] = target;
					this._mwDbgFace[i] = -2f;
					goto IL_04A9;
				}
				if (b.mouthWet != null && b.mouthWet[i])
				{
					float restClear = -this._mwPen[i];
					if (restClear < 0f)
					{
						restClear = 0f;
					}
					float target2 = rB + -0.001f + restClear * (1f - this._mwDraw[i]);
					if (this._mwDraw[i] > 0f || sBest < -0.001f)
					{
						float tt = ((sBest < -0.001f && target2 < rB + -0.001f) ? (rB + -0.001f) : target2);
						P[i] = cB + uB * tt;
						this._mwClr[i] = tt - rB;
					}
					else
					{
						this._mwClr[i] = sBest;
					}
					this._mwDbgLobe[i] = lobeB;
					this._mwDbgRB[i] = rB;
					this._mwDbgDNow[i] = sBest + rB;
					this._mwDbgTarget[i] = target2;
					this._mwDbgFace[i] = -3f;
					goto IL_04A9;
				}
				if (b.mouthIn[i])
				{
					float restClear2 = -this._mwPen[i];
					if (restClear2 < 0f)
					{
						restClear2 = 0f;
					}
					float offr = this._mwOffr[i];
					float target3 = rB + offr + restClear2 * (1f - this._mwDraw[i]);
					if (this._mwDraw[i] > 0f || sBest < offr)
					{
						float tt2 = ((sBest < offr && target3 < rB + offr) ? (rB + offr) : target3);
						P[i] = cB + uB * tt2;
						this._mwClr[i] = tt2 - rB;
					}
					else
					{
						this._mwClr[i] = sBest;
					}
					this._mwDbgLobe[i] = lobeB;
					this._mwDbgRB[i] = rB;
					this._mwDbgDNow[i] = sBest + rB;
					this._mwDbgTarget[i] = target3;
					this._mwDbgFace[i] = -1f;
					goto IL_04A9;
				}
				float face = -Vector3.Dot(uB, GW[i]) / 0.3f;
				if (face < 0f)
				{
					face = 0f;
				}
				else if (face > 1f)
				{
					face = 1f;
				}
				float tSheet = this.SheetOf(b, i);
				float baseC = this.PartnersClearance(b, P, i, OFF);
				if (grid)
				{
					baseC = this.GridUnder(P, P[i], baseC);
				}
				float floor = rB + baseC + tSheet * face * b.mouthW[i];
				float dNow = sBest + rB;
				if (dNow < floor)
				{
					P[i] = cB + uB * floor;
				}
				this._mwDbgLobe[i] = lobeB;
				this._mwDbgRB[i] = rB;
				this._mwDbgDNow[i] = dNow;
				this._mwDbgTarget[i] = floor;
				this._mwDbgFace[i] = face;
				goto IL_04A9;
			}
		}

		// Token: 0x06000147 RID: 327 RVA: 0x0004F014 File Offset: 0x0004D214
		private Vector3 WorldToBindPush(SoftFleshController.Body b, int i, Vector3 mpw)
		{
			BoneWeight w = b.bwArr[i];
			int nbM = b.skinMCache.Length;
			Matrix4x4 M = Matrix4x4.zero;
			float ws = 0f;
			if (w.weight0 > 0f && w.boneIndex0 < nbM)
			{
				Matrix4x4 mk = b.skinMCache[w.boneIndex0];
				for (int e = 0; e < 16; e++)
				{
					ref Matrix4x4 ptr = ref M;
					int num = e;
					ptr[num] += mk[e] * w.weight0;
				}
				ws += w.weight0;
			}
			if (w.weight1 > 0f && w.boneIndex1 < nbM)
			{
				Matrix4x4 mk2 = b.skinMCache[w.boneIndex1];
				for (int e2 = 0; e2 < 16; e2++)
				{
					ref Matrix4x4 ptr = ref M;
					int num = e2;
					ptr[num] += mk2[e2] * w.weight1;
				}
				ws += w.weight1;
			}
			if (w.weight2 > 0f && w.boneIndex2 < nbM)
			{
				Matrix4x4 mk3 = b.skinMCache[w.boneIndex2];
				for (int e3 = 0; e3 < 16; e3++)
				{
					ref Matrix4x4 ptr = ref M;
					int num = e3;
					ptr[num] += mk3[e3] * w.weight2;
				}
				ws += w.weight2;
			}
			if (w.weight3 > 0f && w.boneIndex3 < nbM)
			{
				Matrix4x4 mk4 = b.skinMCache[w.boneIndex3];
				for (int e4 = 0; e4 < 16; e4++)
				{
					ref Matrix4x4 ptr = ref M;
					int num = e4;
					ptr[num] += mk4[e4] * w.weight3;
				}
				ws += w.weight3;
			}
			if (ws >= 0.0001f)
			{
				M[15] = 1f;
				M[3] = 0f;
				M[7] = 0f;
				M[11] = 0f;
				return M.inverse.MultiplyVector(mpw);
			}
			int db = ((b.domBone != null) ? b.domBone[i] : (-1));
			if (db < 0 || db >= b.boneRotCache.Length)
			{
				return mpw;
			}
			return Quaternion.Inverse(b.boneRotCache[db]) * mpw;
		}

		// Token: 0x06000148 RID: 328 RVA: 0x0004F2A0 File Offset: 0x0004D4A0
		private void RefreshSkinCache(SoftFleshController.Body body)
		{
			if (body.bonesArr == null || body.bindRot == null)
			{
				return;
			}
			if (body.boneRotCache == null || body.boneRotCache.Length != body.bonesArr.Length)
			{
				body.boneRotCache = new Quaternion[body.bonesArr.Length];
			}
			for (int b2 = 0; b2 < body.bonesArr.Length; b2++)
			{
				body.boneRotCache[b2] = ((body.bonesArr[b2] != null) ? (body.bonesArr[b2].rotation * body.bindRot[b2]) : Quaternion.identity);
			}
			if (body.bindPoses != null && body.bindPoses.Length >= body.bonesArr.Length)
			{
				if (body.skinMCache == null || body.skinMCache.Length != body.bonesArr.Length)
				{
					body.skinMCache = new Matrix4x4[body.bonesArr.Length];
				}
				for (int b3 = 0; b3 < body.bonesArr.Length; b3++)
				{
					body.skinMCache[b3] = ((body.bonesArr[b3] != null) ? (body.bonesArr[b3].localToWorldMatrix * body.bindPoses[b3]) : Matrix4x4.identity);
				}
			}
		}

		// Token: 0x06000149 RID: 329 RVA: 0x0004F3E0 File Offset: 0x0004D5E0
		private float SheetOf(SoftFleshController.Body b, int i)
		{
			float j = b.mouthT[i];
			if (j < 0.006f)
			{
				return j * 0f;
			}
			return 0f;
		}

		// Token: 0x0600014A RID: 330 RVA: 0x0004F40C File Offset: 0x0004D60C
		private void ReleaseMouth(SoftFleshController.Body b)
		{
			b.mwDrag = 0f;
			b.mwStick = 0f;
			b.mwDepthPrev = -1E+09f;
			b.mwDragT = 0f;
			if (!b.mouthDriven)
			{
				return;
			}
			try
			{
				b.cha.ChangeMouthPtn(b.mouthOrigPtn, true);
				b.cha.ChangeMouthOpenMax(b.mouthOrigOpen);
			}
			catch
			{
			}
			b.mouthDriven = false;
			b.mouthOpenNow = -1f;
			b.mouthInfo = "released";
			b.mouthPushValid = false;
		}

		// Token: 0x0600014B RID: 331 RVA: 0x0004F4AC File Offset: 0x0004D6AC
		private void DumpWrap(ChaControl cha, string dir)
		{
			SoftFleshController.Body b = null;
			for (int i = 0; i < this._bodies.Count; i++)
			{
				if (this._bodies[i].isFace && (this._bodies[i].cha == cha || cha == null))
				{
					b = this._bodies[i];
					break;
				}
			}
			if (b == null)
			{
				for (int j = 0; j < this._bodies.Count; j++)
				{
					if (this._bodies[j].isFace)
					{
						b = this._bodies[j];
						break;
					}
				}
			}
			if (b == null || b.mouthIdx == null || this._mwP == null || this._mwR == null || b.smr == null)
			{
				SoftFleshPlugin.Log("[SF] wrap dump: no wrapped face");
				return;
			}
			Matrix4x4 l2w = SoftFleshController.BakeL2W(b.smr);
			StringBuilder csv = new StringBuilder();
			csv.Append("idx,w,cls,Rx,Ry,Rz,Px,Py,Pz,Bx,By,Bz,restPen,draw,clr,seg,rA,rB,partner,p0,p1,p2,p3,mouthT,sheet,req,GWx,GWy,GWz,baseX,baseY,baseZ,dvX,dvY,dvZ,ang,alo,wet,S1x,S1y,S1z,S2x,S2y,S2z\n");
			Func<float, string> F = (float v) => v.ToString("F5");
			for (int q = 0; q < b.mouthIdx.Length; q++)
			{
				int k = b.mouthIdx[q];
				int cls = ((b.mouthLining != null && b.mouthLining[k]) ? 2 : ((b.mouthIn[k] > false) ? 1 : 0));
				Vector3 R = this._mwR[k];
				Vector3 P = this._mwP[k];
				Vector3 B = ((b.bakeV != null && b.bakeV.Count > k) ? l2w.MultiplyPoint3x4(b.bakeV[k]) : Vector3.zero);
				Vector3 gw = ((this._mwGW != null) ? this._mwGW[k] : Vector3.zero);
				csv.Append(k).Append(',').Append(F(b.mouthW[k]))
					.Append(',')
					.Append(cls)
					.Append(',')
					.Append(F(R.x))
					.Append(',')
					.Append(F(R.y))
					.Append(',')
					.Append(F(R.z))
					.Append(',')
					.Append(F(P.x))
					.Append(',')
					.Append(F(P.y))
					.Append(',')
					.Append(F(P.z))
					.Append(',')
					.Append(F(B.x))
					.Append(',')
					.Append(F(B.y))
					.Append(',')
					.Append(F(B.z))
					.Append(',')
					.Append(F(this._mwPen[k]))
					.Append(',')
					.Append(F(this._mwDraw[k]))
					.Append(',')
					.Append((this._mwClr != null && this._mwClr[k] < 1E+09f) ? F(this._mwClr[k]) : "nan")
					.Append(',')
					.Append((this._mwSeg != null) ? this._mwSeg[k] : (-1))
					.Append(',')
					.Append(F((this._mwRA != null) ? this._mwRA[k] : 0f))
					.Append(',')
					.Append(F((this._mwRB != null) ? this._mwRB[k] : 0f))
					.Append(',')
					.Append((b.mouthPartner != null) ? b.mouthPartner[k] : (-1));
				for (int l = 0; l < 4; l++)
				{
					csv.Append(',').Append((b.mouthPartners != null) ? b.mouthPartners[k * 4 + l] : (-1));
				}
				csv.Append(',').Append(F(b.mouthT[k])).Append(',')
					.Append(F(this.SheetOf(b, k)))
					.Append(',')
					.Append((this._mwReq != null && this._mwReq[k] >= 0f) ? F(this._mwReq[k]) : "nan")
					.Append(',')
					.Append(F(gw.x))
					.Append(',')
					.Append(F(gw.y))
					.Append(',')
					.Append(F(gw.z))
					.Append(',')
					.Append(F(b.baseVerts[k].x))
					.Append(',')
					.Append(F(b.baseVerts[k].y))
					.Append(',')
					.Append(F(b.baseVerts[k].z))
					.Append(',')
					.Append(F(b.mouthDv[k].x))
					.Append(',')
					.Append(F(b.mouthDv[k].y))
					.Append(',')
					.Append(F(b.mouthDv[k].z))
					.Append(',')
					.Append(F((b.mouthAng != null) ? b.mouthAng[k] : 0f))
					.Append(',')
					.Append(F((b.mouthAlo != null) ? b.mouthAlo[k] : 0f))
					.Append(',')
					.Append((b.mouthWet != null && b.mouthWet[k]) ? 1 : 0)
					.Append(',')
					.Append(F((this._mwS1 != null) ? this._mwS1[k].x : 0f))
					.Append(',')
					.Append(F((this._mwS1 != null) ? this._mwS1[k].y : 0f))
					.Append(',')
					.Append(F((this._mwS1 != null) ? this._mwS1[k].z : 0f))
					.Append(',')
					.Append(F((this._mwS2 != null) ? this._mwS2[k].x : 0f))
					.Append(',')
					.Append(F((this._mwS2 != null) ? this._mwS2[k].y : 0f))
					.Append(',')
					.Append(F((this._mwS2 != null) ? this._mwS2[k].z : 0f))
					.Append('\n');
			}
			File.WriteAllText(Path.Combine(dir, "mouth_wrap.csv"), csv.ToString());
			StringBuilder meta = new StringBuilder();
			meta.Append("version ").Append("1.1.0").Append('\n');
			meta.Append("open ").Append(b.mouthOpenNow).Append("  lipStretch ")
				.Append(0f)
				.Append("  lipThick ")
				.Append(0f)
				.Append("  lipSmooth ")
				.Append(this.lipSmooth)
				.Append("  itemRadScale ")
				.Append(this.itemRadScale)
				.Append('\n');
			meta.Append("mouthAxP ").Append(b.mouthAxP.ToString("F5")).Append(" mouthAxU ")
				.Append(b.mouthAxU.ToString("F5"))
				.Append(" mouthC ")
				.Append(b.mouthC.ToString("F5"))
				.Append('\n');
			meta.Append("mouthOutW ").Append(b.mouthOutW.ToString("F5")).Append(" OFF ")
				.Append(0.001f.ToString("F5"))
				.Append(" lipSmooth ")
				.Append(this.lipSmooth)
				.Append('\n');
			meta.Append("chain ").Append(this._chN).Append('\n');
			for (int m = 0; m < this._chN; m++)
			{
				meta.Append("chainpt ").Append(m).Append(' ')
					.Append(this._chP[m].x.ToString("F5"))
					.Append(' ')
					.Append(this._chP[m].y.ToString("F5"))
					.Append(' ')
					.Append(this._chP[m].z.ToString("F5"))
					.Append(" r ")
					.Append(this._chR[m].ToString("F5"))
					.Append(" raw ")
					.Append(this._chRaw[m].ToString("F5"))
					.Append(" lobe ")
					.Append(this._chLobe[m])
					.Append('\n');
			}
			if (this._segOwner != null)
			{
				int pp = 0;
				while (pp < this._pressers.Count && pp < this._segLen2.Length && pp < this._segOwner.Length)
				{
					if (!(this._segOwner[pp] != b.mouthOwner))
					{
						meta.Append("lobe ").Append(pp).Append(" insert ")
							.Append(this._segInsert[pp])
							.Append(" A ")
							.Append(this._segA[pp].ToString("F5"))
							.Append(" AB ")
							.Append(this._segAB[pp].ToString("F5"))
							.Append(" rad ")
							.Append(this._segRad[pp].ToString("F5"))
							.Append(' ')
							.Append(this._segRadB[pp].ToString("F5"))
							.Append(" scale ")
							.Append(this._segScale[pp].ToString("F5"))
							.Append('\n');
						SoftFleshController.Prof pf = ((this._segProf != null && pp < this._segProf.Length) ? this._segProf[pp] : null);
						if (pf != null)
						{
							meta.Append("  prof NS ").Append(pf.NS).Append(" NB ")
								.Append(pf.NB)
								.Append(" origin ")
								.Append(pf.origin.ToString("F5"))
								.Append(" ax ")
								.Append(pf.ax.ToString("F5"))
								.Append(" u ")
								.Append(pf.u.ToString("F5"))
								.Append(" v ")
								.Append(pf.v.ToString("F5"))
								.Append(" len ")
								.Append(pf.len.ToString("F5"))
								.Append(" r");
							for (int n = 0; n < pf.r.Length; n++)
							{
								meta.Append(' ').Append(pf.r[n].ToString("F5"));
							}
							meta.Append('\n');
							Matrix4x4 m2 = this._segW2L[pp];
							meta.Append("  w2l");
							for (int e = 0; e < 16; e++)
							{
								meta.Append(' ').Append(m2[e].ToString("F6"));
							}
							meta.Append('\n');
							Matrix4x4 m3 = ((this._pressers[pp].a != null) ? this._pressers[pp].a.localToWorldMatrix : Matrix4x4.identity);
							meta.Append("  l2w");
							for (int e2 = 0; e2 < 16; e2++)
							{
								meta.Append(' ').Append(m3[e2].ToString("F6"));
							}
							meta.Append('\n');
						}
					}
					pp++;
				}
			}
			File.WriteAllText(Path.Combine(dir, "mouth_wrap_meta.txt"), meta.ToString());
			if (b.mouthNbrStart != null)
			{
				StringBuilder adj = new StringBuilder();
				int[] halo = b.mouthHalo;
				adj.Append("halo");
				if (halo != null)
				{
					for (int q2 = 0; q2 < halo.Length; q2++)
					{
						adj.Append(' ').Append(halo[q2]);
					}
				}
				adj.Append('\n');
				for (int q3 = 0; q3 < b.mouthIdx.Length; q3++)
				{
					int i2 = b.mouthIdx[q3];
					adj.Append(i2).Append(':');
					for (int k2 = b.mouthNbrStart[i2]; k2 < b.mouthNbrStart[i2 + 1]; k2++)
					{
						adj.Append(' ').Append(b.mouthNbrList[k2]);
					}
					adj.Append('\n');
				}
				if (halo != null)
				{
					foreach (int i3 in halo)
					{
						Vector3 R2 = this._mwR[i3];
						adj.Append("haloR ").Append(i3).Append(' ')
							.Append(R2.x.ToString("F5"))
							.Append(' ')
							.Append(R2.y.ToString("F5"))
							.Append(' ')
							.Append(R2.z.ToString("F5"))
							.Append('\n');
					}
				}
				File.WriteAllText(Path.Combine(dir, "mouth_wrap_adj.txt"), adj.ToString());
				StringBuilder cx = new StringBuilder();
				int k3 = 0;
				while (k3 + 1 < b.mwXPairs.Count)
				{
					int ta = b.mwXPairs[k3];
					int tb = b.mwXPairs[k3 + 1];
					cx.Append("pair ").Append(b.mouthTris[ta]).Append(' ')
						.Append(b.mouthTris[ta + 1])
						.Append(' ')
						.Append(b.mouthTris[ta + 2])
						.Append("  ")
						.Append(b.mouthTris[tb])
						.Append(' ')
						.Append(b.mouthTris[tb + 1])
						.Append(' ')
						.Append(b.mouthTris[tb + 2])
						.Append('\n');
					k3 += 2;
				}
				File.WriteAllText(Path.Combine(dir, "mouth_wrap_cross.txt"), cx.ToString());
			}
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] wrap dump: ",
				b.mouthIdx.Length.ToString(),
				" verts, chain ",
				this._chN.ToString(),
				" -> mouth_wrap.csv / _meta.txt / _adj.txt"
			}));
		}

		// Token: 0x0600014C RID: 332 RVA: 0x00050528 File Offset: 0x0004E728
		private SoftFleshController.Body FaceOf(ChaControl cha)
		{
			for (int i = 0; i < this._bodies.Count; i++)
			{
				if (this._bodies[i].isFace && this._bodies[i].cha == cha)
				{
					return this._bodies[i];
				}
			}
			return null;
		}

		// Token: 0x0600014D RID: 333 RVA: 0x00050585 File Offset: 0x0004E785
		public bool HasFace(ChaControl cha)
		{
			return this.FaceOf(cha) != null;
		}

		// Token: 0x0600014E RID: 334 RVA: 0x00050594 File Offset: 0x0004E794
		public void SetupFace(ChaControl cha)
		{
			SoftFleshController.Body f = this.FaceOf(cha);
			if (f != null)
			{
				this.ReleaseMouth(f);
				this.TeardownBody(f);
			}
			this.SetupCharacter(cha, true);
			this.RebuildPressers();
		}

		// Token: 0x0600014F RID: 335 RVA: 0x000505C8 File Offset: 0x0004E7C8
		public int SetupFaceSelected()
		{
			ChaControl[] sel = this.SelectedChars();
			if (sel == null || sel.Length == 0)
			{
				sel = Object.FindObjectsOfType<ChaControl>();
			}
			int i = 0;
			if (sel != null)
			{
				foreach (ChaControl c in sel)
				{
					if (!(c == null))
					{
						this.SetupFace(c);
						if (this.FaceOf(c) != null)
						{
							i++;
						}
					}
				}
			}
			return i;
		}

		// Token: 0x06000150 RID: 336 RVA: 0x00050624 File Offset: 0x0004E824
		public int ClearFaceSelected()
		{
			ChaControl[] sel = this.SelectedChars();
			if (sel == null || sel.Length == 0)
			{
				sel = Object.FindObjectsOfType<ChaControl>();
			}
			int i = 0;
			if (sel != null)
			{
				foreach (ChaControl c in sel)
				{
					SoftFleshController.Body f = this.FaceOf(c);
					if (f != null)
					{
						this.ReleaseMouth(f);
						this.TeardownBody(f);
						i++;
					}
				}
			}
			return i;
		}

		// Token: 0x06000151 RID: 337 RVA: 0x00050684 File Offset: 0x0004E884
		public string MouthStatus()
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < this._bodies.Count; i++)
			{
				SoftFleshController.Body b = this._bodies[i];
				if (b.isFace)
				{
					sb.Append((b.cha != null) ? b.cha.name : "?").Append(": lips ").Append((b.mouthIdx != null) ? b.mouthIdx.Length : 0)
						.Append(" verts, kuwae pattern ")
						.Append(b.mouthPtn)
						.Append(" - ")
						.Append(b.mouthInfo)
						.Append('\n');
				}
			}
			if (sb.Length <= 0)
			{
				return "no face set up";
			}
			return sb.ToString();
		}

		// Token: 0x06000152 RID: 338 RVA: 0x00050758 File Offset: 0x0004E958
		private static SkinnedMeshRenderer FindBodySmr(ChaControl cha)
		{
			SkinnedMeshRenderer best = null;
			int bestV = -1;
			SkinnedMeshRenderer[] smrs = cha.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			foreach (SkinnedMeshRenderer r in smrs)
			{
				if (!(r == null) && !(r.sharedMesh == null))
				{
					string nm = r.name.ToLower();
					if ((nm.Contains("o_body") || nm.Contains("body")) && r.sharedMesh.vertexCount > bestV)
					{
						best = r;
						bestV = r.sharedMesh.vertexCount;
					}
				}
			}
			if (best == null)
			{
				foreach (SkinnedMeshRenderer r2 in smrs)
				{
					if (!(r2 == null) && !(r2.sharedMesh == null) && r2.sharedMesh.vertexCount > bestV)
					{
						best = r2;
						bestV = r2.sharedMesh.vertexCount;
					}
				}
			}
			return best;
		}

		// Token: 0x06000155 RID: 341 RVA: 0x0005125F File Offset: 0x0004F45F
		[CompilerGenerated]
		internal static long <SyncGarmentRiders>g__Key|561_0(long hx, long hy, long hz)
		{
			return (hx & 2097151L) | ((hy & 2097151L) << 21) | ((hz & 2097151L) << 42);
		}

		// Token: 0x06000156 RID: 342 RVA: 0x00051284 File Offset: 0x0004F484
		[CompilerGenerated]
		internal static int <SyncGarmentRiders>g__AddVert|561_1(int ia, int ib, int ic, Vector3 w, ref SoftFleshController.<>c__DisplayClass561_0 A_4)
		{
			int nIdx = A_4.pos.Count;
			A_4.pos.Add(A_4.pos[ia] * w.x + A_4.pos[ib] * w.y + A_4.pos[ic] * w.z);
			A_4.garmS.Add(A_4.garmS[ia] * w.x + A_4.garmS[ib] * w.y + A_4.garmS[ic] * w.z);
			if (A_4.hN)
			{
				Vector3 v = A_4.nrm[ia] * w.x + A_4.nrm[ib] * w.y + A_4.nrm[ic] * w.z;
				float mg = v.magnitude;
				A_4.nrm.Add((mg > 1E-06f) ? (v / mg) : A_4.nrm[ia]);
			}
			if (A_4.hT)
			{
				Vector4 t4 = A_4.tan[ia] * w.x + A_4.tan[ib] * w.y + A_4.tan[ic] * w.z;
				Vector3 t5;
				t5..ctor(t4.x, t4.y, t4.z);
				float tm = t5.magnitude;
				if (tm > 1E-06f)
				{
					t5 /= tm;
				}
				A_4.tan.Add(new Vector4(t5.x, t5.y, t5.z, A_4.tan[ia].w));
			}
			if (A_4.h0)
			{
				A_4.uv0.Add(A_4.uv0[ia] * w.x + A_4.uv0[ib] * w.y + A_4.uv0[ic] * w.z);
			}
			if (A_4.h1)
			{
				A_4.uv1.Add(A_4.uv1[ia] * w.x + A_4.uv1[ib] * w.y + A_4.uv1[ic] * w.z);
			}
			if (A_4.hC)
			{
				A_4.col.Add(Color32.Lerp(Color32.Lerp(A_4.col[ia], A_4.col[ib], w.y / (w.x + w.y + 1E-06f)), A_4.col[ic], w.z));
			}
			if (A_4.hW)
			{
				A_4.bwL.Add(SoftFleshController.BlendW3(A_4.bwL[ia], A_4.bwL[ib], A_4.bwL[ic], w));
			}
			return nIdx;
		}

		// Token: 0x06000157 RID: 343 RVA: 0x00051630 File Offset: 0x0004F830
		[CompilerGenerated]
		internal static void <SyncGarmentRiders>g__SplitTri|561_2(List<int> T, int off, int n)
		{
			int a = T[off];
			int b = T[off + 1];
			int c = T[off + 2];
			T[off + 2] = n;
			T.Add(b);
			T.Add(c);
			T.Add(n);
			T.Add(c);
			T.Add(a);
			T.Add(n);
		}

		// Token: 0x06000158 RID: 344 RVA: 0x00051690 File Offset: 0x0004F890
		[CompilerGenerated]
		internal static void <BlendW3>g__Acc|565_0(int idx, float f, ref SoftFleshController.<>c__DisplayClass565_0 A_2)
		{
			if (f <= 0f)
			{
				return;
			}
			for (int i = 0; i < A_2.bn; i++)
			{
				if (A_2.bi[i] == idx)
				{
					A_2.bw[i] += f;
					return;
				}
			}
			if (A_2.bn < 12)
			{
				A_2.bi[A_2.bn] = idx;
				A_2.bw[A_2.bn] = f;
				int bn = A_2.bn;
				A_2.bn = bn + 1;
			}
		}

		// Token: 0x06000159 RID: 345 RVA: 0x0005170C File Offset: 0x0004F90C
		[CompilerGenerated]
		internal static bool <BindClothes>g__OpenV|569_0(int vq, ref SoftFleshController.<>c__DisplayClass569_0 A_1)
		{
			return (A_1.body.vagW != null && A_1.body.vagW[vq] > 0.01f) || (A_1.body.kokW != null && A_1.body.kokW[vq] > 0.01f) || (A_1.body.anaW != null && A_1.body.anaW[vq] > 0.01f);
		}

		// Token: 0x040000A4 RID: 164
		public float touchRadius = 0.045f;

		// Token: 0x040000A5 RID: 165
		public float stiffness = 900f;

		// Token: 0x040000A6 RID: 166
		public float damping = 22f;

		// Token: 0x040000A7 RID: 167
		public float maxDepth = 0.025f;

		// Token: 0x040000A8 RID: 168
		public float knuckleFrac = 0.5f;

		// Token: 0x040000A9 RID: 169
		public float waveSpeed = 0.8f;

		// Token: 0x040000AA RID: 170
		public float viscosity = 0.35f;

		// Token: 0x040000AB RID: 171
		public float bulge = 0.35f;

		// Token: 0x040000AC RID: 172
		public float smoothFactor = 0.7f;

		// Token: 0x040000AD RID: 173
		public float skinTension = 0.02f;

		// Token: 0x040000AE RID: 174
		public bool mouthAuto = true;

		// Token: 0x040000AF RID: 175
		public float mouthScale = 1f;

		// Token: 0x040000B0 RID: 176
		public float mouthReach = 0.04f;

		// Token: 0x040000B1 RID: 177
		private const float mouthFullHeight = 0.032f;

		// Token: 0x040000B2 RID: 178
		public float faceFirm = 3f;

		// Token: 0x040000B3 RID: 179
		public float faceDamp = 4f;

		// Token: 0x040000B4 RID: 180
		public float faceWave = 0.15f;

		// Token: 0x040000B5 RID: 181
		public float faceSlap = 0.3f;

		// Token: 0x040000B6 RID: 182
		public float faceMaxDepth = 0.008f;

		// Token: 0x040000B7 RID: 183
		public bool faceDrag = true;

		// Token: 0x040000B8 RID: 184
		public bool mouthTiltFollow = true;

		// Token: 0x040000B9 RID: 185
		public float tiltFollow = 0.6f;

		// Token: 0x040000BA RID: 186
		public float lipDrag = 0.006f;

		// Token: 0x040000BB RID: 187
		public float lipPushIn = 0.003f;

		// Token: 0x040000BC RID: 188
		private Vector3 _ceOutW;

		// Token: 0x040000BD RID: 189
		private Vector3 _ceIn;

		// Token: 0x040000BE RID: 190
		private float _ceCap = 0.003f;

		// Token: 0x040000BF RID: 191
		private int _ceRedir;

		// Token: 0x040000C0 RID: 192
		public float lipReturn = 1.2f;

		// Token: 0x040000C1 RID: 193
		public bool lipNoReturn;

		// Token: 0x040000C2 RID: 194
		public float lipDamp = 0.04f;

		// Token: 0x040000C3 RID: 195
		public bool deepCheck;

		// Token: 0x040000C4 RID: 196
		public bool showCross;

		// Token: 0x040000C5 RID: 197
		private const float lipStretch = 0f;

		// Token: 0x040000C6 RID: 198
		private const float lipThick = 0f;

		// Token: 0x040000C7 RID: 199
		public int lipSmooth = 3;

		// Token: 0x040000C8 RID: 200
		private const bool lipWetInside = false;

		// Token: 0x040000C9 RID: 201
		private const float lipEdgePull = 0f;

		// Token: 0x040000CA RID: 202
		private const float lipFoldSink = 0f;

		// Token: 0x040000CB RID: 203
		public int skinBend = 4;

		// Token: 0x040000CC RID: 204
		public float smoothShade = -1f;

		// Token: 0x040000CD RID: 205
		public float shadeTau = 0.09f;

		// Token: 0x040000CE RID: 206
		public float normalBoost = 4f;

		// Token: 0x040000CF RID: 207
		public float nipFirm = 0.8f;

		// Token: 0x040000D0 RID: 208
		public float slapGain = 0.8f;

		// Token: 0x040000D1 RID: 209
		public float slapSoften = 0.7f;

		// Token: 0x040000D2 RID: 210
		public float itemRadScale = 1f;

		// Token: 0x040000D3 RID: 211
		public float itemSquash;

		// Token: 0x040000D4 RID: 212
		public float waveShield = 1f;

		// Token: 0x040000D5 RID: 213
		public float bellyGain = 0.8f;

		// Token: 0x040000D6 RID: 214
		public float bellyWidth = 1f;

		// Token: 0x040000D7 RID: 215
		public float bellyLength = 0.35f;

		// Token: 0x040000D8 RID: 216
		public float bellyShape;

		// Token: 0x040000D9 RID: 217
		public float bellyStart;

		// Token: 0x040000DA RID: 218
		public float openTestDepth;

		// Token: 0x040000DB RID: 219
		public float openTestWidth;

		// Token: 0x040000DC RID: 220
		public float clitHold = 1f;

		// Token: 0x040000DD RID: 221
		public float openMaxPct = 150f;

		// Token: 0x040000DE RID: 222
		public float itemStretch = 1f;

		// Token: 0x040000DF RID: 223
		public string bakeStatus = "(not baked)";

		// Token: 0x040000E0 RID: 224
		public string rigInfo = "";

		// Token: 0x040000E1 RID: 225
		public string rigDumpInfo = "(dump rig: see BepInEx log)";

		// Token: 0x040000E2 RID: 226
		private string _bakeDetail = "";

		// Token: 0x040000E3 RID: 227
		public bool bulgeWide;

		// Token: 0x040000E4 RID: 228
		public float wave = 0.4f;

		// Token: 0x040000E5 RID: 229
		public string lastStatus = "(not run)";

		// Token: 0x040000E6 RID: 230
		public string diag = "";

		// Token: 0x040000E7 RID: 231
		public int activeVerts;

		// Token: 0x040000E8 RID: 232
		public float maxDisp;

		// Token: 0x040000E9 RID: 233
		public float nearDist = -1f;

		// Token: 0x040000EA RID: 234
		public string nearName = "";

		// Token: 0x040000EB RID: 235
		public bool didBake;

		// Token: 0x040000EC RID: 236
		public string solveDiag = "";

		// Token: 0x040000ED RID: 237
		private readonly StringBuilder _diagSb = new StringBuilder();

		// Token: 0x040000EE RID: 238
		private float _minSq;

		// Token: 0x040000EF RID: 239
		private string _nearName = "";

		// Token: 0x040000F0 RID: 240
		private Mesh body_prevSubdiv;

		// Token: 0x040000F1 RID: 241
		private readonly Dictionary<SkinnedMeshRenderer, Mesh> _origMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();

		// Token: 0x040000F2 RID: 242
		private readonly List<SoftFleshController.Body> _bodies = new List<SoftFleshController.Body>();

		// Token: 0x040000F3 RID: 243
		public bool seamRepair = true;

		// Token: 0x040000F4 RID: 244
		public bool canalCollider;

		// Token: 0x040000F5 RID: 245
		public float itemRail;

		// Token: 0x040000F6 RID: 246
		public float itemAim;

		// Token: 0x040000F7 RID: 247
		public float aimFirm = 0.6f;

		// Token: 0x040000F8 RID: 248
		public float entranceTilt = 0.7f;

		// Token: 0x040000F9 RID: 249
		public float spreadShift = 0.04f;

		// Token: 0x040000FA RID: 250
		public float bakeReach = 0.06f;

		// Token: 0x040000FB RID: 251
		public const int CANAL_ST = 9;

		// Token: 0x040000FC RID: 252
		public float wallOnly;

		// Token: 0x040000FD RID: 253
		public float widthTrim = 1f;

		// Token: 0x040000FE RID: 254
		public bool clothFollow = true;

		// Token: 0x040000FF RID: 255
		public float clothLift = 0.0008f;

		// Token: 0x04000100 RID: 256
		public bool clothSubdiv = true;

		// Token: 0x04000101 RID: 257
		public bool gripFreeze;

		// Token: 0x04000102 RID: 258
		public bool crotchIsolate = true;

		// Token: 0x04000103 RID: 259
		public bool taperCanal = true;

		// Token: 0x04000104 RID: 260
		public bool trackItems = true;

		// Token: 0x04000105 RID: 261
		public float gapeHold = 0.35f;

		// Token: 0x04000106 RID: 262
		public float suction = 0.5f;

		// Token: 0x04000107 RID: 263
		public float petalFirm = 0.7f;

		// Token: 0x04000108 RID: 264
		public float trackWidthX = 1f;

		// Token: 0x04000109 RID: 265
		public float itemGuide = 0.6f;

		// Token: 0x0400010A RID: 266
		public float followGain = 0.6f;

		// Token: 0x0400010B RID: 267
		public float trkW;

		// Token: 0x0400010C RID: 268
		public float trkD;

		// Token: 0x0400010D RID: 269
		public float trkG;

		// Token: 0x0400010E RID: 270
		public float trkS;

		// Token: 0x0400010F RID: 271
		public float trkOff;

		// Token: 0x04000110 RID: 272
		public float trkRadMm;

		// Token: 0x04000111 RID: 273
		public float trkDepthMm;

		// Token: 0x04000112 RID: 274
		public float trkLatMm;

		// Token: 0x04000113 RID: 275
		public float trkRimMm;

		// Token: 0x04000114 RID: 276
		public float trkTiltDeg;

		// Token: 0x04000115 RID: 277
		public bool trkTiltReady;

		// Token: 0x04000116 RID: 278
		public float trkTiltMm;

		// Token: 0x04000117 RID: 279
		public bool restPose = true;

		// Token: 0x04000118 RID: 280
		public bool ringDump;

		// Token: 0x04000119 RID: 281
		private readonly List<SoftFleshController.Presser> _pressers = new List<SoftFleshController.Presser>();

		// Token: 0x0400011A RID: 282
		private Vector3[] _segA;

		// Token: 0x0400011B RID: 283
		private Vector3[] _segAB;

		// Token: 0x0400011C RID: 284
		private float[] _segLen2;

		// Token: 0x0400011D RID: 285
		private bool[] _segRight;

		// Token: 0x0400011E RID: 286
		private bool[] _segProx;

		// Token: 0x0400011F RID: 287
		private ChaControl[] _segCha;

		// Token: 0x04000120 RID: 288
		private Transform[] _segOwner;

		// Token: 0x04000121 RID: 289
		public bool showColliders;

		// Token: 0x04000122 RID: 290
		public bool showBulge;

		// Token: 0x04000123 RID: 291
		private readonly List<Transform> _viz = new List<Transform>();

		// Token: 0x04000124 RID: 292
		private float[] _segRad;

		// Token: 0x04000125 RID: 293
		private float[] _segRadB;

		// Token: 0x04000126 RID: 294
		private float[] _segChainR;

		// Token: 0x04000127 RID: 295
		private float[] _segBulgeW;

		// Token: 0x04000128 RID: 296
		private float[] _segTipF;

		// Token: 0x04000129 RID: 297
		private bool[] _segItem;

		// Token: 0x0400012A RID: 298
		private bool[] _segInsert;

		// Token: 0x0400012B RID: 299
		private bool[] _segTipA;

		// Token: 0x0400012C RID: 300
		private bool[] _segTipB;

		// Token: 0x0400012D RID: 301
		private float _segRadMax;

		// Token: 0x0400012E RID: 302
		private SoftFleshController.Prof[] _segProf;

		// Token: 0x0400012F RID: 303
		private Matrix4x4[] _segW2L;

		// Token: 0x04000130 RID: 304
		private float[] _segScale;

		// Token: 0x04000131 RID: 305
		private float[] _segGate2;

		// Token: 0x04000132 RID: 306
		private List<int>[] _cSimBuf;

		// Token: 0x04000133 RID: 307
		private float[] _cTgtB;

		// Token: 0x04000134 RID: 308
		private float[] _cImpB;

		// Token: 0x04000135 RID: 309
		private float[] _cMinSqB;

		// Token: 0x04000136 RID: 310
		private Vector3[] _cImpCB;

		// Token: 0x04000137 RID: 311
		private string[] _cNameB;

		// Token: 0x04000138 RID: 312
		private float[] _dgHB;

		// Token: 0x04000139 RID: 313
		private float[] _dgProfRawB;

		// Token: 0x0400013A RID: 314
		private float[] _dgProfCapB;

		// Token: 0x0400013B RID: 315
		private int[] _dgSkipLatchB;

		// Token: 0x0400013C RID: 316
		private int[] _dgSkipFarB;

		// Token: 0x0400013D RID: 317
		private int[] _dgJumpB;

		// Token: 0x0400013E RID: 318
		private int[] _dgOnOffB;

		// Token: 0x0400013F RID: 319
		private int[] _dgProfPB;

		// Token: 0x04000140 RID: 320
		private float[] _dgTipDB;

		// Token: 0x04000141 RID: 321
		private float[] _dgLB;

		// Token: 0x04000142 RID: 322
		private float[] _dgProxB;

		// Token: 0x04000143 RID: 323
		private float[] _dgDistB;

		// Token: 0x04000144 RID: 324
		private float[] _dgApprB;

		// Token: 0x04000145 RID: 325
		private float[] _dgImpB;

		// Token: 0x04000146 RID: 326
		private bool[] _segInside;

		// Token: 0x04000147 RID: 327
		private float[] _inMinD2;

		// Token: 0x04000148 RID: 328
		private float[] _inMinH;

		// Token: 0x04000149 RID: 329
		private int _inStride;

		// Token: 0x0400014A RID: 330
		private float[] _pHmax;

		// Token: 0x0400014B RID: 331
		private float[] _pProfMax;

		// Token: 0x0400014C RID: 332
		private int[] _pLatch;

		// Token: 0x0400014D RID: 333
		private int[] _pFar;

		// Token: 0x0400014E RID: 334
		private float[] _pTipD2;

		// Token: 0x0400014F RID: 335
		private float[] _pTipH;

		// Token: 0x04000150 RID: 336
		private int[] _fcReach;

		// Token: 0x04000151 RID: 337
		private int[] _fcPushCore;

		// Token: 0x04000152 RID: 338
		private int[] _fcPushOuter;

		// Token: 0x04000153 RID: 339
		private int[] _fcSkipBin;

		// Token: 0x04000154 RID: 340
		private float[] _fcStepOuter;

		// Token: 0x04000155 RID: 341
		private float[] _fcPenOuterMin;

		// Token: 0x04000156 RID: 342
		private Vector3[] _inMinN;

		// Token: 0x04000157 RID: 343
		private Vector3[] _segEntryN;

		// Token: 0x04000158 RID: 344
		private static float[] _cGripImpB;

		// Token: 0x04000159 RID: 345
		private static float[] _cGripDTB;

		// Token: 0x0400015A RID: 346
		public static float GripImpPeak;

		// Token: 0x0400015B RID: 347
		public static float GripDTgtPeak;

		// Token: 0x0400015C RID: 348
		public static float GripSwayPeak;

		// Token: 0x0400015D RID: 349
		public static int WrapN;

		// Token: 0x0400015E RID: 350
		public static float WrapMaxMm;

		// Token: 0x0400015F RID: 351
		private static int _wrapFrame = -1;

		// Token: 0x04000160 RID: 352
		public static int PenBindN;

		// Token: 0x04000161 RID: 353
		public static int PenPoseN;

		// Token: 0x04000162 RID: 354
		public static float PenBindMm;

		// Token: 0x04000163 RID: 355
		public static float PenPoseMm;

		// Token: 0x04000164 RID: 356
		private static bool _spanGuardOn = false;

		// Token: 0x04000165 RID: 357
		public static float PenPeakMm;

		// Token: 0x04000166 RID: 358
		private static int _gripProbe = -1;

		// Token: 0x04000167 RID: 359
		private static Vector3 _gripProbePrev;

		// Token: 0x04000168 RID: 360
		private static bool _gripProbeFresh;

		// Token: 0x04000169 RID: 361
		private float[] _segM;

		// Token: 0x0400016A RID: 362
		public bool addAsInsert = true;

		// Token: 0x0400016B RID: 363
		private static readonly Dictionary<Type, SoftFleshController.DbAccess> _dbAcc = new Dictionary<Type, SoftFleshController.DbAccess>();

		// Token: 0x0400016C RID: 364
		private static FieldInfo _pStifF;

		// Token: 0x0400016D RID: 365
		private static FieldInfo _pDampF;

		// Token: 0x0400016E RID: 366
		private static FieldInfo _pElastF;

		// Token: 0x0400016F RID: 367
		private static FieldInfo _partsF;

		// Token: 0x04000170 RID: 368
		private static FieldInfo _pTrF;

		// Token: 0x04000171 RID: 369
		private readonly List<SoftFleshController.AimTarget> _aims = new List<SoftFleshController.AimTarget>();

		// Token: 0x04000172 RID: 370
		private readonly List<SoftFleshController.Presser> _itemDefs = new List<SoftFleshController.Presser>();

		// Token: 0x04000173 RID: 371
		private readonly List<DynamicBoneCollider> _dynCols = new List<DynamicBoneCollider>();

		// Token: 0x04000174 RID: 372
		private readonly List<float> _dynColBaseR = new List<float>();

		// Token: 0x04000175 RID: 373
		private const int CORR_PASSES = 4;

		// Token: 0x04000176 RID: 374
		public float corrSmooth = 0.6f;

		// Token: 0x04000177 RID: 375
		public bool diagVisible;

		// Token: 0x04000178 RID: 376
		public bool openNormals = true;

		// Token: 0x04000179 RID: 377
		public bool boneOpening = true;

		// Token: 0x0400017A RID: 378
		public float spreadWeights;

		// Token: 0x0400017B RID: 379
		public float spreadReach = 0.055f;

		// Token: 0x0400017C RID: 380
		public bool weldWeights = true;

		// Token: 0x0400017D RID: 381
		public string weldInfo = "";

		// Token: 0x0400017E RID: 382
		public bool capHoles = true;

		// Token: 0x0400017F RID: 383
		public bool subdivEnabled = true;

		// Token: 0x04000180 RID: 384
		public float subdivMinArea = 30f;

		// Token: 0x04000181 RID: 385
		public int subdivLevels = 2;

		// Token: 0x04000182 RID: 386
		public bool subdivBody;

		// Token: 0x04000183 RID: 387
		public float subdivWeightGap = 0.4f;

		// Token: 0x04000184 RID: 388
		public string subdivInfo = "";

		// Token: 0x04000185 RID: 389
		private float[] _segPen;

		// Token: 0x04000186 RID: 390
		private float[] _segPenNext;

		// Token: 0x04000187 RID: 391
		private float _lastParamHash;

		// Token: 0x04000188 RID: 392
		private int _paramKick;

		// Token: 0x04000189 RID: 393
		private Vector3[] _segVel;

		// Token: 0x0400018A RID: 394
		private int _pressCharCount = -1;

		// Token: 0x0400018B RID: 395
		private readonly Dictionary<SkinnedMeshRenderer, Mesh> _ourMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();

		// Token: 0x0400018C RID: 396
		public static int FontRebuilds;

		// Token: 0x0400018D RID: 397
		public static string FontLog = "";

		// Token: 0x0400018E RID: 398
		private SoftFleshController.Body _cwBody;

		// Token: 0x0400018F RID: 399
		private int _cwFrame = -1;

		// Token: 0x04000190 RID: 400
		private int _cwDone = -1;

		// Token: 0x04000191 RID: 401
		private Quaternion _cwKosi;

		// Token: 0x04000192 RID: 402
		private Quaternion _cwSiriL;

		// Token: 0x04000193 RID: 403
		private Quaternion _cwSiriSL;

		// Token: 0x04000194 RID: 404
		private Vector3 _cwKokan;

		// Token: 0x04000195 RID: 405
		private Vector3 _cwSiriSLPos;

		// Token: 0x04000196 RID: 406
		public static string CrotchHold = "(no frame yet)";

		// Token: 0x04000197 RID: 407
		public bool boneSnapReq;

		// Token: 0x04000198 RID: 408
		private Matrix4x4[] _skinM;

		// Token: 0x04000199 RID: 409
		private Vector3[] _skinNowBuf;

		// Token: 0x0400019A RID: 410
		private float _bdNext;

		// Token: 0x0400019B RID: 411
		public static string BoneDiff = "(no snapshot - press \"Snapshot bones\" with Crotch OFF, then turn it on)";

		// Token: 0x0400019C RID: 412
		public static string MeshDiff = "(no snapshot)";

		// Token: 0x0400019D RID: 413
		private float _openLogNext;

		// Token: 0x0400019E RID: 414
		public const int PH_TOTAL = 0;

		// Token: 0x0400019F RID: 415
		public const int PH_BAKE = 1;

		// Token: 0x040001A0 RID: 416
		public const int PH_CORR = 2;

		// Token: 0x040001A1 RID: 417
		public const int PH_OPEN = 3;

		// Token: 0x040001A2 RID: 418
		public const int PH_SPRING = 4;

		// Token: 0x040001A3 RID: 419
		public const int PH_NORM = 5;

		// Token: 0x040001A4 RID: 420
		public const int PH_CONTACT = 6;

		// Token: 0x040001A5 RID: 421
		public const int PH_FIELD = 7;

		// Token: 0x040001A6 RID: 422
		public const int PH_MAINT = 8;

		// Token: 0x040001A7 RID: 423
		public const int PH_CLOTH = 9;

		// Token: 0x040001A8 RID: 424
		public const int PH_SKIN = 10;

		// Token: 0x040001A9 RID: 425
		public const int PH_MOUTH = 11;

		// Token: 0x040001AA RID: 426
		public const int PH_N = 12;

		// Token: 0x040001AB RID: 427
		private static readonly Stopwatch _sw = Stopwatch.StartNew();

		// Token: 0x040001AC RID: 428
		private static readonly double[] _phAcc = new double[12];

		// Token: 0x040001AD RID: 429
		public static readonly double[] PhaseMs = new double[12];

		// Token: 0x040001AE RID: 430
		private static int _phFrames;

		// Token: 0x040001AF RID: 431
		private static readonly long[] _memStk = new long[16];

		// Token: 0x040001B0 RID: 432
		private static int _memDepth;

		// Token: 0x040001B1 RID: 433
		private static readonly double[] _phGc = new double[12];

		// Token: 0x040001B2 RID: 434
		public static readonly double[] PhaseKB = new double[12];

		// Token: 0x040001B3 RID: 435
		public static int HitchTotal;

		// Token: 0x040001B4 RID: 436
		private static float _hAvg;

		// Token: 0x040001B5 RID: 437
		private static float _hWinT;

		// Token: 0x040001B6 RID: 438
		private static int _hCount;

		// Token: 0x040001B7 RID: 439
		public static float HitchPerMin;

		// Token: 0x040001B8 RID: 440
		public static float HitchWorstMs;

		// Token: 0x040001B9 RID: 441
		public static string HitchPhase = "-";

		// Token: 0x040001BA RID: 442
		private static readonly double[] _phFrame = new double[12];

		// Token: 0x040001BB RID: 443
		public static readonly double[] HitchPhaseMs = new double[12];

		// Token: 0x040001BC RID: 444
		public static float HitchOursMs;

		// Token: 0x040001BD RID: 445
		private static readonly SoftFleshController.Spike[] _spikes = new SoftFleshController.Spike[8];

		// Token: 0x040001BE RID: 446
		private static int _spikeAt;

		// Token: 0x040001BF RID: 447
		private static int _spikeN;

		// Token: 0x040001C0 RID: 448
		public static int LoadAct;

		// Token: 0x040001C1 RID: 449
		public static int LoadSim;

		// Token: 0x040001C2 RID: 450
		public static int LoadSteps;

		// Token: 0x040001C3 RID: 451
		public static bool FieldPushSkipped;

		// Token: 0x040001C4 RID: 452
		public static bool FieldDentSkipped;

		// Token: 0x040001C5 RID: 453
		private static int _gcPrev = -1;

		// Token: 0x040001C6 RID: 454
		public static int GcThisFrame;

		// Token: 0x040001C7 RID: 455
		public static int GcTotal;

		// Token: 0x040001C8 RID: 456
		public static int GcSpikes;

		// Token: 0x040001C9 RID: 457
		private static readonly string[] PhaseNamesC = new string[]
		{
			"TOTAL", "bake", "corr", "open", "spring", "norm", "contact", "field", "maint", "cloth",
			"skin", "mouth"
		};

		// Token: 0x040001CA RID: 458
		public static bool SkipMoved;

		// Token: 0x040001CB RID: 459
		public static bool SkipBusy;

		// Token: 0x040001CC RID: 460
		public static bool SkipTook;

		// Token: 0x040001CD RID: 461
		public static bool SkipFieldChanged;

		// Token: 0x040001CE RID: 462
		public static bool SkipSettling;

		// Token: 0x040001CF RID: 463
		public static bool SkipBsDrv;

		// Token: 0x040001D0 RID: 464
		public static bool SkipVagDrv;

		// Token: 0x040001D1 RID: 465
		public static float SkipVel;

		// Token: 0x040001D2 RID: 466
		public static float SkipVelLimit;

		// Token: 0x040001D3 RID: 467
		public static int SkipCool;

		// Token: 0x040001D4 RID: 468
		private int _springCount;

		// Token: 0x040001D5 RID: 469
		private static Quaternion[] _tiltLut;

		// Token: 0x040001D6 RID: 470
		private int _solveFrame = -1;

		// Token: 0x040001D7 RID: 471
		private float _diagNext;

		// Token: 0x040001D8 RID: 472
		private bool _diagBuild;

		// Token: 0x040001D9 RID: 473
		public static string ParticleSuspects = "not scanned yet";

		// Token: 0x040001DA RID: 474
		private int _pcensusDone = -1;

		// Token: 0x040001DB RID: 475
		public static string MediaSuspects = "not scanned yet";

		// Token: 0x040001DC RID: 476
		private long _tUpd;

		// Token: 0x040001DD RID: 477
		private long _tLate;

		// Token: 0x040001DE RID: 478
		private long _tCanvas;

		// Token: 0x040001DF RID: 479
		private long _tPre;

		// Token: 0x040001E0 RID: 480
		private long _tPost;

		// Token: 0x040001E1 RID: 481
		public static float SegU;

		// Token: 0x040001E2 RID: 482
		public static float SegL1;

		// Token: 0x040001E3 RID: 483
		public static float SegL2;

		// Token: 0x040001E4 RID: 484
		public static float SegR;

		// Token: 0x040001E5 RID: 485
		public static float SegE;

		// Token: 0x040001E6 RID: 486
		public static float AvgDt;

		// Token: 0x040001E7 RID: 487
		public static float AvgU;

		// Token: 0x040001E8 RID: 488
		public static float AvgL1;

		// Token: 0x040001E9 RID: 489
		public static float AvgL2;

		// Token: 0x040001EA RID: 490
		public static float AvgR;

		// Token: 0x040001EB RID: 491
		public static float AvgE;

		// Token: 0x040001EC RID: 492
		public static float AvgOurs;

		// Token: 0x040001ED RID: 493
		private static FieldInfo _cvLayoutF;

		// Token: 0x040001EE RID: 494
		private static FieldInfo _cvGraphicF;

		// Token: 0x040001EF RID: 495
		public static int CvLayoutN;

		// Token: 0x040001F0 RID: 496
		public static int CvGraphicN;

		// Token: 0x040001F1 RID: 497
		public static int CvPrevL;

		// Token: 0x040001F2 RID: 498
		public static int CvPrevG;

		// Token: 0x040001F3 RID: 499
		private static int _lastBigL2;

		// Token: 0x040001F4 RID: 500
		public static int PredLo = -1;

		// Token: 0x040001F5 RID: 501
		public static int PredHi = -1;

		// Token: 0x040001F6 RID: 502
		public static int PredPeriod;

		// Token: 0x040001F7 RID: 503
		public static string CvBurst = "";

		// Token: 0x040001F8 RID: 504
		public bool trackLog = true;

		// Token: 0x040001F9 RID: 505
		private float _tlNext;

		// Token: 0x040001FA RID: 506
		private readonly StringBuilder _tlSb = new StringBuilder(512);

		// Token: 0x040001FB RID: 507
		private readonly List<SkinnedMeshRenderer> _mnpHidden = new List<SkinnedMeshRenderer>();

		// Token: 0x040001FC RID: 508
		private float _homeNext;

		// Token: 0x040001FD RID: 509
		private float _maintainGap = 1f;

		// Token: 0x040001FE RID: 510
		private readonly List<SoftFleshController.MaintRec> _maintRecs = new List<SoftFleshController.MaintRec>();

		// Token: 0x040001FF RID: 511
		private readonly List<ChaControl> _maintChas = new List<ChaControl>();

		// Token: 0x04000200 RID: 512
		private readonly Dictionary<Transform, Vector3> _guideApplied = new Dictionary<Transform, Vector3>();

		// Token: 0x04000201 RID: 513
		private readonly List<Transform> _guideKeys = new List<Transform>();

		// Token: 0x04000202 RID: 514
		private static readonly List<float> _rimScratch = new List<float>(64);

		// Token: 0x04000203 RID: 515
		private static float[] _stRad;

		// Token: 0x04000204 RID: 516
		private static float[] _stLat2;

		// Token: 0x04000205 RID: 517
		private static float[] _stSm;

		// Token: 0x04000206 RID: 518
		private static int[] _bgN;

		// Token: 0x04000207 RID: 519
		private static float[] _bgAxLo;

		// Token: 0x04000208 RID: 520
		private static float[] _bgAxHi;

		// Token: 0x04000209 RID: 521
		private static float[] _bgLat;

		// Token: 0x0400020A RID: 522
		private static float[] _bgPeak;

		// Token: 0x0400020B RID: 523
		public static int BgVerts;

		// Token: 0x0400020C RID: 524
		public static float BgAxLo;

		// Token: 0x0400020D RID: 525
		public static float BgAxHi;

		// Token: 0x0400020E RID: 526
		public static float BgLat;

		// Token: 0x0400020F RID: 527
		public static float BgPeak;

		// Token: 0x04000210 RID: 528
		public static int BgSegs;

		// Token: 0x04000211 RID: 529
		public static int BulgeVizN;

		// Token: 0x04000212 RID: 530
		public static int CrossVizN;

		// Token: 0x04000213 RID: 531
		private readonly List<Transform> _vizB = new List<Transform>();

		// Token: 0x04000214 RID: 532
		private readonly List<Transform> _vizX = new List<Transform>();

		// Token: 0x04000215 RID: 533
		private Material _matWall;

		// Token: 0x04000216 RID: 534
		private Material _matSkin;

		// Token: 0x04000217 RID: 535
		public static float CrotchDeltaDeg;

		// Token: 0x04000218 RID: 536
		public static float ButtockDeg;

		// Token: 0x04000219 RID: 537
		public static float ButtockDegS;

		// Token: 0x0400021A RID: 538
		public static string CrotchMode = "never ran";

		// Token: 0x0400021B RID: 539
		public static string CrotchVagRoot = "?";

		// Token: 0x0400021C RID: 540
		private static Vector3[][] _rowD0;

		// Token: 0x0400021D RID: 541
		private static Vector3[][] _rowD1;

		// Token: 0x0400021E RID: 542
		public bool planeExact;

		// Token: 0x0400021F RID: 543
		private int[] _dumpTris;

		// Token: 0x04000220 RID: 544
		private int _dumpTrisN;

		// Token: 0x04000221 RID: 545
		private StringBuilder _membSb;

		// Token: 0x04000222 RID: 546
		private float _membDumpT = -100f;

		// Token: 0x04000223 RID: 547
		private static float[] _tbRdS;

		// Token: 0x04000224 RID: 548
		private static float[] _tbWrS;

		// Token: 0x04000225 RID: 549
		private static Vector3[] _tbRdV;

		// Token: 0x04000226 RID: 550
		private static Vector3[] _tbWrV;

		// Token: 0x04000227 RID: 551
		private static int[] _tbAct;

		// Token: 0x04000228 RID: 552
		private static int[] _tbAdjS;

		// Token: 0x04000229 RID: 553
		private static int[] _tbAdjL;

		// Token: 0x0400022A RID: 554
		private static float _tbLam;

		// Token: 0x0400022B RID: 555
		private static readonly Action<int, int> _tbScalarD = new Action<int, int>(SoftFleshController.TaubinScalarChunk);

		// Token: 0x0400022C RID: 556
		private static readonly Action<int, int> _tbVectorD = new Action<int, int>(SoftFleshController.TaubinVectorChunk);

		// Token: 0x0400022D RID: 557
		private const float CLAMP_COS = 0.766f;

		// Token: 0x0400022E RID: 558
		private const float CLAMP_SIN = 0.643f;

		// Token: 0x0400022F RID: 559
		private const int DT_N = 360;

		// Token: 0x04000230 RID: 560
		private readonly float[] _dtTime = new float[360];

		// Token: 0x04000231 RID: 561
		private readonly float[] _dtDt = new float[360];

		// Token: 0x04000232 RID: 562
		private readonly float[] _dtTip = new float[360];

		// Token: 0x04000233 RID: 563
		private readonly float[] _dtCenS = new float[360];

		// Token: 0x04000234 RID: 564
		private readonly float[] _dtCenR = new float[360];

		// Token: 0x04000235 RID: 565
		private readonly float[] _dtPkS = new float[360];

		// Token: 0x04000236 RID: 566
		private readonly float[] _dtPkR = new float[360];

		// Token: 0x04000237 RID: 567
		private readonly float[] _dtIters = new float[360];

		// Token: 0x04000238 RID: 568
		private readonly float[] _dtAmp = new float[360];

		// Token: 0x04000239 RID: 569
		private readonly float[] _dtEdge = new float[360];

		// Token: 0x0400023A RID: 570
		private readonly int[] _dtVS = new int[360];

		// Token: 0x0400023B RID: 571
		private readonly int[] _dtVR = new int[360];

		// Token: 0x0400023C RID: 572
		private readonly int[] _dtCore = new int[360];

		// Token: 0x0400023D RID: 573
		private readonly int[] _dtBody = new int[360];

		// Token: 0x0400023E RID: 574
		private readonly int[] _dtSteps = new int[360];

		// Token: 0x0400023F RID: 575
		private readonly int[] _dtIn = new int[360];

		// Token: 0x04000240 RID: 576
		private readonly int[] _dtLatch = new int[360];

		// Token: 0x04000241 RID: 577
		private readonly int[] _dtJump = new int[360];

		// Token: 0x04000242 RID: 578
		private readonly int[] _dtOnOff = new int[360];

		// Token: 0x04000243 RID: 579
		private int _dtHead;

		// Token: 0x04000244 RID: 580
		private int _dtCount;

		// Token: 0x04000245 RID: 581
		private int _dtLastFrame = -1;

		// Token: 0x04000246 RID: 582
		private Vector3 _dtTipPrev;

		// Token: 0x04000247 RID: 583
		private Vector3 _dtCenSPrev;

		// Token: 0x04000248 RID: 584
		private Vector3 _dtCenRPrev;

		// Token: 0x04000249 RID: 585
		private bool _dtHave;

		// Token: 0x0400024A RID: 586
		private readonly List<SoftFleshController.SkinRec> _skins = new List<SoftFleshController.SkinRec>();

		// Token: 0x0400024B RID: 587
		private static readonly HashSet<string> _dumped = new HashSet<string>();

		// Token: 0x0400024C RID: 588
		private const bool DumpOK = false;

		// Token: 0x0400024D RID: 589
		private static readonly Dictionary<SkinnedMeshRenderer, float[]> _shapeW0 = new Dictionary<SkinnedMeshRenderer, float[]>();

		// Token: 0x0400024E RID: 590
		private static Vector3[] _bsN;

		// Token: 0x0400024F RID: 591
		private static Vector3[] _bsT;

		// Token: 0x04000250 RID: 592
		public bool secondSkin = true;

		// Token: 0x04000251 RID: 593
		public bool spanGuard = true;

		// Token: 0x04000252 RID: 594
		private readonly List<SoftFleshController.ClothRec> _cloth = new List<SoftFleshController.ClothRec>();

		// Token: 0x04000253 RID: 595
		private float _clothLiftApplied = -1f;

		// Token: 0x04000254 RID: 596
		private static Quaternion[] _clRotScratch;

		// Token: 0x04000255 RID: 597
		public static string ClothInfo = "off";

		// Token: 0x04000256 RID: 598
		private static Matrix4x4[] _subSkinM;

		// Token: 0x04000257 RID: 599
		private static Quaternion[] _subInvRot;

		// Token: 0x04000258 RID: 600
		private static Vector3[] _subCorrW;

		// Token: 0x04000259 RID: 601
		private static Matrix4x4[] _glueBM;

		// Token: 0x0400025A RID: 602
		private static int _glueFrame = -1;

		// Token: 0x0400025B RID: 603
		private static SoftFleshController.Body _glueBody;

		// Token: 0x0400025C RID: 604
		private const int SLOT_GLOVES = 4;

		// Token: 0x0400025D RID: 605
		private static readonly string[] ClothSlotName = new string[] { "top", "bottom", "bra", "underwear", "gloves", "pantyhose", "legwear", "shoes_in", "shoes_out" };

		// Token: 0x0400025E RID: 606
		private const float SVCELL = 0.02f;

		// Token: 0x0400025F RID: 607
		public string diagStatus = "";

		// Token: 0x04000260 RID: 608
		public string creaseInfo = "";

		// Token: 0x04000261 RID: 609
		private const char NL = '\n';

		// Token: 0x04000262 RID: 610
		private DynamicBoneCollider _canalCol;

		// Token: 0x04000263 RID: 611
		private static Vector3[] _restPos = new Vector3[64];

		// Token: 0x04000264 RID: 612
		private static Vector3[] _ikPos = new Vector3[64];

		// Token: 0x04000265 RID: 613
		private static Quaternion[] _ikRot = new Quaternion[64];

		// Token: 0x04000266 RID: 614
		private static Vector3[] _ikOff = new Vector3[64];

		// Token: 0x04000267 RID: 615
		public static int AimGripped;

		// Token: 0x04000268 RID: 616
		public static int AimJoints;

		// Token: 0x04000269 RID: 617
		public static float AimMaxDeg;

		// Token: 0x0400026A RID: 618
		public static float AimBlend;

		// Token: 0x0400026B RID: 619
		public static float AimTipAxial;

		// Token: 0x0400026C RID: 620
		public static float AimTipLat;

		// Token: 0x0400026D RID: 621
		public static float AimRailMm;

		// Token: 0x0400026E RID: 622
		private static int _aimGripped;

		// Token: 0x0400026F RID: 623
		private static int _aimJoints;

		// Token: 0x04000270 RID: 624
		private static float _aimMaxDeg;

		// Token: 0x04000271 RID: 625
		public string guideInfo = "";

		// Token: 0x04000272 RID: 626
		public Vector3 savedEntryB = new Vector3(0f, 0.923f, -0.028f);

		// Token: 0x04000273 RID: 627
		public Vector3 savedDirB = new Vector3(0.02f, 0.94f, 0.34f);

		// Token: 0x04000274 RID: 628
		public bool savedAxisValid = true;

		// Token: 0x04000275 RID: 629
		public bool axisDirty;

		// Token: 0x04000276 RID: 630
		public bool monitorGuide;

		// Token: 0x04000277 RID: 631
		public string guideLive = "";

		// Token: 0x04000278 RID: 632
		private float _monNext;

		// Token: 0x04000279 RID: 633
		private readonly List<SoftFleshController.SquashRec> _squash = new List<SoftFleshController.SquashRec>();

		// Token: 0x0400027A RID: 634
		private readonly List<Transform> _sqDone = new List<Transform>();

		// Token: 0x0400027B RID: 635
		public static string SquashInfo = "off";

		// Token: 0x0400027C RID: 636
		private float _sqLastRim = -1f;

		// Token: 0x0400027D RID: 637
		private float _sqPrevD = -1f;

		// Token: 0x0400027E RID: 638
		private float _sqRatioLP = -1f;

		// Token: 0x0400027F RID: 639
		private float _sqSigLast = float.MinValue;

		// Token: 0x04000280 RID: 640
		private static Quaternion[] _pivSnapBuf;

		// Token: 0x04000281 RID: 641
		private static Vector3[] _segBoxMin;

		// Token: 0x04000282 RID: 642
		private static Vector3[] _segBoxMax;

		// Token: 0x04000283 RID: 643
		private static string[] _segName;

		// Token: 0x04000284 RID: 644
		private float _sqVel;

		// Token: 0x04000285 RID: 645
		private bool _sqAny;

		// Token: 0x04000286 RID: 646
		private static Vector3[] _sqEntB;

		// Token: 0x04000287 RID: 647
		private static Vector3[] _sqDirB;

		// Token: 0x04000288 RID: 648
		private static float[] _sqScaleB;

		// Token: 0x04000289 RID: 649
		private static Vector3[] _sqUB;

		// Token: 0x0400028A RID: 650
		private static Vector3[] _sqVB;

		// Token: 0x0400028B RID: 651
		private static bool[] _sqHangB;

		// Token: 0x0400028C RID: 652
		private static float[] _apR;

		// Token: 0x0400028D RID: 653
		private static int[] _apN;

		// Token: 0x0400028E RID: 654
		private static float[] _apT;

		// Token: 0x0400028F RID: 655
		private static readonly SoftFleshController.MtState _mtSt = new SoftFleshController.MtState();

		// Token: 0x04000290 RID: 656
		private static ManualResetEvent _mtEvt;

		// Token: 0x04000291 RID: 657
		private static int _mtWorkers = -1;

		// Token: 0x04000292 RID: 658
		private static SoftFleshController.WkGen _wkGen;

		// Token: 0x04000293 RID: 659
		private static int _wkNext;

		// Token: 0x04000294 RID: 660
		private static int _wkChunks;

		// Token: 0x04000295 RID: 661
		private static int _wkPer;

		// Token: 0x04000296 RID: 662
		private static int _wkCount;

		// Token: 0x04000297 RID: 663
		private double _mwSigPending;

		// Token: 0x04000298 RID: 664
		private const int RING_NS = 24;

		// Token: 0x04000299 RID: 665
		private const int RING_NZ = 8;

		// Token: 0x0400029A RID: 666
		private float _rgZMin;

		// Token: 0x0400029B RID: 667
		private float _rgZStep;

		// Token: 0x0400029C RID: 668
		private int _rgCarryN;

		// Token: 0x0400029D RID: 669
		private int _rgCarrySec;

		// Token: 0x0400029E RID: 670
		private float _rgCarryMax;

		// Token: 0x0400029F RID: 671
		private float[] _rgLipOwn = new float[24];

		// Token: 0x040002A0 RID: 672
		private float[] _rgLipBehind = new float[24];

		// Token: 0x040002A1 RID: 673
		private float[] _rgD = new float[192];

		// Token: 0x040002A2 RID: 674
		private float[] _rgD2 = new float[192];

		// Token: 0x040002A3 RID: 675
		private float[] _rgA = new float[192];

		// Token: 0x040002A4 RID: 676
		private float[] _rgA2 = new float[192];

		// Token: 0x040002A5 RID: 677
		private float[] _rgT = new float[192];

		// Token: 0x040002A6 RID: 678
		private float[] _rgT2 = new float[192];

		// Token: 0x040002A7 RID: 679
		private float[] _rgNeed = new float[192];

		// Token: 0x040002A8 RID: 680
		private bool[] _rgHave = new bool[192];

		// Token: 0x040002A9 RID: 681
		private Vector3[] _chP = new Vector3[64];

		// Token: 0x040002AA RID: 682
		private float[] _chR = new float[64];

		// Token: 0x040002AB RID: 683
		private float[] _chRaw = new float[64];

		// Token: 0x040002AC RID: 684
		private float[] _chKey = new float[64];

		// Token: 0x040002AD RID: 685
		private int[] _chLobe = new int[64];

		// Token: 0x040002AE RID: 686
		private int _chN;

		// Token: 0x040002AF RID: 687
		private float[] _chRMax = new float[64];

		// Token: 0x040002B0 RID: 688
		private float[] _inTt = new float[64];

		// Token: 0x040002B1 RID: 689
		private float[] _inDl = new float[64];

		// Token: 0x040002B2 RID: 690
		private float[] _inBound = new float[64];

		// Token: 0x040002B3 RID: 691
		private bool[] _inDone = new bool[64];

		// Token: 0x040002B4 RID: 692
		private Matrix4x4[] _lobeL2W;

		// Token: 0x040002B5 RID: 693
		private int[] _lobeL2WFrame;

		// Token: 0x040002B6 RID: 694
		private float[] _lobeUniS;

		// Token: 0x040002B7 RID: 695
		private bool _mwFresh;

		// Token: 0x040002B8 RID: 696
		private int _mwFreshVert = -1;

		// Token: 0x040002B9 RID: 697
		private Vector3[] _mwWposBuf;

		// Token: 0x040002BA RID: 698
		private float[] _mwWclrBuf;

		// Token: 0x040002BB RID: 699
		private float[] _mwPen;

		// Token: 0x040002BC RID: 700
		private float[] _mwDraw;

		// Token: 0x040002BD RID: 701
		private Vector3[] _mwDir;

		// Token: 0x040002BE RID: 702
		private Vector3[] _mwR;

		// Token: 0x040002BF RID: 703
		private Vector3[] _mwP;

		// Token: 0x040002C0 RID: 704
		private Vector3[] _mwP2;

		// Token: 0x040002C1 RID: 705
		private Vector3[] _mwSR;

		// Token: 0x040002C2 RID: 706
		private Vector3[] _mwGW;

		// Token: 0x040002C3 RID: 707
		private Vector3[] _mwGW2;

		// Token: 0x040002C4 RID: 708
		private Vector3[] _mwS1;

		// Token: 0x040002C5 RID: 709
		private Vector3[] _mwS2;

		// Token: 0x040002C6 RID: 710
		private Vector3[] _mwN;

		// Token: 0x040002C7 RID: 711
		private Vector3[] _mwNR;

		// Token: 0x040002C8 RID: 712
		private Vector3[] _mwGR;

		// Token: 0x040002C9 RID: 713
		private Vector3[] _mwDragV;

		// Token: 0x040002CA RID: 714
		private Vector3[] _mwRPrev;

		// Token: 0x040002CB RID: 715
		private Vector3[] _mwS1Prev;

		// Token: 0x040002CC RID: 716
		private Vector3[] _mwS2Prev;

		// Token: 0x040002CD RID: 717
		private Vector3[] _mwDragVPrev;

		// Token: 0x040002CE RID: 718
		private float[] _mwPenPrev;

		// Token: 0x040002CF RID: 719
		private float[] _mwFPrev;

		// Token: 0x040002D0 RID: 720
		private float[] _mwRingF;

		// Token: 0x040002D1 RID: 721
		private Vector3[] _mwOutPrev;

		// Token: 0x040002D2 RID: 722
		private float[] _mwAngOwn;

		// Token: 0x040002D3 RID: 723
		private float[] _mwAloOwn;

		// Token: 0x040002D4 RID: 724
		private float[] _mwAngW;

		// Token: 0x040002D5 RID: 725
		private float[] _mwAloW;

		// Token: 0x040002D6 RID: 726
		private float[] _mwAcc;

		// Token: 0x040002D7 RID: 727
		private float[] _mwAsc;

		// Token: 0x040002D8 RID: 728
		private float[] _mwAcc2;

		// Token: 0x040002D9 RID: 729
		private float[] _mwAsc2;

		// Token: 0x040002DA RID: 730
		private float[] _mwAloW2;

		// Token: 0x040002DB RID: 731
		private float[] _mwLift;

		// Token: 0x040002DC RID: 732
		private float[] _mwLift2;

		// Token: 0x040002DD RID: 733
		private float[] _mwLiftRaw;

		// Token: 0x040002DE RID: 734
		private List<int> _mwOutTris;

		// Token: 0x040002DF RID: 735
		private List<int> _mwInTris;

		// Token: 0x040002E0 RID: 736
		private bool[] _mwWposOK;

		// Token: 0x040002E1 RID: 737
		private int[] _mwXHead;

		// Token: 0x040002E2 RID: 738
		private int[] _mwXNext;

		// Token: 0x040002E3 RID: 739
		private List<int> _mwSelfTris;

		// Token: 0x040002E4 RID: 740
		private List<int> _mwSkinTris;

		// Token: 0x040002E5 RID: 741
		private Vector3[] _mwSelfC;

		// Token: 0x040002E6 RID: 742
		private float[] _mwSelfR;

		// Token: 0x040002E7 RID: 743
		private Mesh _mwOtherBake;

		// Token: 0x040002E8 RID: 744
		private List<Vector3> _mwOtherV;

		// Token: 0x040002E9 RID: 745
		private int[] _mwDbgLobe;

		// Token: 0x040002EA RID: 746
		private float[] _mwDbgRB;

		// Token: 0x040002EB RID: 747
		private float[] _mwDbgDNow;

		// Token: 0x040002EC RID: 748
		private float[] _mwDbgTarget;

		// Token: 0x040002ED RID: 749
		private float[] _mwDbgFace;

		// Token: 0x040002EE RID: 750
		private float[] _mwDbgDetail;

		// Token: 0x040002EF RID: 751
		private int[] _mwSeg;

		// Token: 0x040002F0 RID: 752
		private float[] _mwRA;

		// Token: 0x040002F1 RID: 753
		private float[] _mwRB;

		// Token: 0x040002F2 RID: 754
		private float[] _mwRB2;

		// Token: 0x040002F3 RID: 755
		private float[] _mwClr;

		// Token: 0x040002F4 RID: 756
		private float[] _mwShell;

		// Token: 0x040002F5 RID: 757
		private float[] _mwReq;

		// Token: 0x040002F6 RID: 758
		private float[] _mwShellTmp;

		// Token: 0x040002F7 RID: 759
		private float[] _mwUnder;

		// Token: 0x040002F8 RID: 760
		private float[] _mwOffr;

		// Token: 0x040002F9 RID: 761
		private float[] _mwClrFinal;

		// Token: 0x040002FA RID: 762
		private Vector3[] _mwUdir;

		// Token: 0x040002FB RID: 763
		private int[] _gHead;

		// Token: 0x040002FC RID: 764
		private int[] _gNext;

		// Token: 0x040002FD RID: 765
		private int _gNx;

		// Token: 0x040002FE RID: 766
		private int _gNy;

		// Token: 0x040002FF RID: 767
		private int _gNz;

		// Token: 0x04000300 RID: 768
		private Vector3 _gMin;

		// Token: 0x04000301 RID: 769
		private const float G_CELL = 0.008f;

		// Token: 0x0200000F RID: 15
		private class Body
		{
			// Token: 0x04000387 RID: 903
			public ChaControl cha;

			// Token: 0x04000388 RID: 904
			public bool sfOn = true;

			// Token: 0x04000389 RID: 905
			public bool restDone;

			// Token: 0x0400038A RID: 906
			public int restStill;

			// Token: 0x0400038B RID: 907
			public SkinnedMeshRenderer smr;

			// Token: 0x0400038C RID: 908
			public Mesh origMesh;

			// Token: 0x0400038D RID: 909
			public Mesh workMesh;

			// Token: 0x0400038E RID: 910
			public Vector3[] baseVerts;

			// Token: 0x0400038F RID: 911
			public Vector3[] baseNormals;

			// Token: 0x04000390 RID: 912
			public List<Vector3> baseNormalsList;

			// Token: 0x04000391 RID: 913
			public int[] adjStart;

			// Token: 0x04000392 RID: 914
			public int[] adjList;

			// Token: 0x04000393 RID: 915
			public int[][] seamGroups;

			// Token: 0x04000394 RID: 916
			public bool[] seamNSync;

			// Token: 0x04000395 RID: 917
			public bool[] seamAligned;

			// Token: 0x04000396 RID: 918
			public bool[] foldRegion;

			// Token: 0x04000397 RID: 919
			public int[] seamOf;

			// Token: 0x04000398 RID: 920
			public Vector3[] seamN;

			// Token: 0x04000399 RID: 921
			public Vector3[] corrRest;

			// Token: 0x0400039A RID: 922
			public Vector3[] corrB;

			// Token: 0x0400039B RID: 923
			public readonly List<int> corrList = new List<int>();

			// Token: 0x0400039C RID: 924
			public int[] corrStamp;

			// Token: 0x0400039D RID: 925
			public int corrCur;

			// Token: 0x0400039E RID: 926
			public Vector3[] corrBuf;

			// Token: 0x0400039F RID: 927
			public Vector3[] corrPos;

			// Token: 0x040003A0 RID: 928
			public Vector3[] corrClean;

			// Token: 0x040003A1 RID: 929
			public float[] corrW;

			// Token: 0x040003A2 RID: 930
			public Mesh refMesh;

			// Token: 0x040003A3 RID: 931
			public readonly List<Vector3> refV = new List<Vector3>();

			// Token: 0x040003A4 RID: 932
			public Vector3[] pushR;

			// Token: 0x040003A5 RID: 933
			public float[] tgtF;

			// Token: 0x040003A6 RID: 934
			public int[] wakeStamp;

			// Token: 0x040003A7 RID: 935
			public int solveCount;

			// Token: 0x040003A8 RID: 936
			public Vector3[] smoothBuf;

			// Token: 0x040003A9 RID: 937
			public readonly List<Vector3> normList = new List<Vector3>();

			// Token: 0x040003AA RID: 938
			public Vector3[] smoothNBuf;

			// Token: 0x040003AB RID: 939
			public Vector3[] nTilt;

			// Token: 0x040003AC RID: 940
			public int[] nTiltSeen;

			// Token: 0x040003AD RID: 941
			public List<int> nTiltLive = new List<int>();

			// Token: 0x040003AE RID: 942
			public bool[] nTiltIn;

			// Token: 0x040003AF RID: 943
			public int nTiltFrame;

			// Token: 0x040003B0 RID: 944
			public float[] rdFrac;

			// Token: 0x040003B1 RID: 945
			public float avgEdge;

			// Token: 0x040003B2 RID: 946
			public float[] vertEdge;

			// Token: 0x040003B3 RID: 947
			public float minEdge;

			// Token: 0x040003B4 RID: 948
			public int lastAct;

			// Token: 0x040003B5 RID: 949
			public int actCore;

			// Token: 0x040003B6 RID: 950
			public int lastSim;

			// Token: 0x040003B7 RID: 951
			public int lastSteps;

			// Token: 0x040003B8 RID: 952
			public float maxVel;

			// Token: 0x040003B9 RID: 953
			public float[] disp;

			// Token: 0x040003BA RID: 954
			public float[] dispPrev;

			// Token: 0x040003BB RID: 955
			public float[] vel;

			// Token: 0x040003BC RID: 956
			public byte[] armSide;

			// Token: 0x040003BD RID: 957
			public bool[] noDent;

			// Token: 0x040003BE RID: 958
			public byte[] engaged;

			// Token: 0x040003BF RID: 959
			public byte[] reached;

			// Token: 0x040003C0 RID: 960
			public float[] hLast;

			// Token: 0x040003C1 RID: 961
			public float dgH;

			// Token: 0x040003C2 RID: 962
			public float dgProfRaw;

			// Token: 0x040003C3 RID: 963
			public float dgProfCap;

			// Token: 0x040003C4 RID: 964
			public float dgTgt;

			// Token: 0x040003C5 RID: 965
			public float dgDisp;

			// Token: 0x040003C6 RID: 966
			public int dgProfP = -1;

			// Token: 0x040003C7 RID: 967
			public float dgTipD;

			// Token: 0x040003C8 RID: 968
			public float dgL;

			// Token: 0x040003C9 RID: 969
			public float ampScaleF;

			// Token: 0x040003CA RID: 970
			public bool[] segInside;

			// Token: 0x040003CB RID: 971
			public Vector3[] segEntryN;

			// Token: 0x040003CC RID: 972
			public int dgInsideN;

			// Token: 0x040003CD RID: 973
			public float dgItersF;

			// Token: 0x040003CE RID: 974
			public float dgAmpScale = 1f;

			// Token: 0x040003CF RID: 975
			public int dgFingerP = -1;

			// Token: 0x040003D0 RID: 976
			public int dgFingerLatch;

			// Token: 0x040003D1 RID: 977
			public int dgFingerFar;

			// Token: 0x040003D2 RID: 978
			public float dgFingerH;

			// Token: 0x040003D3 RID: 979
			public float dgFingerProf;

			// Token: 0x040003D4 RID: 980
			public float dgFingerTip;

			// Token: 0x040003D5 RID: 981
			public bool dgFingerInside;

			// Token: 0x040003D6 RID: 982
			public float dgProfProx;

			// Token: 0x040003D7 RID: 983
			public float dgProfDist;

			// Token: 0x040003D8 RID: 984
			public float[] dgRdPrev;

			// Token: 0x040003D9 RID: 985
			public float[] dgDispPrevF;

			// Token: 0x040003DA RID: 986
			public float dgJitR;

			// Token: 0x040003DB RID: 987
			public float dgJitS;

			// Token: 0x040003DC RID: 988
			public float[] memb;

			// Token: 0x040003DD RID: 989
			public float[] membSur;

			// Token: 0x040003DE RID: 990
			public float[] membBuf;

			// Token: 0x040003DF RID: 991
			public float[] membBuf2;

			// Token: 0x040003E0 RID: 992
			public int[] membStamp;

			// Token: 0x040003E1 RID: 993
			public int[] membCStamp;

			// Token: 0x040003E2 RID: 994
			public int[] membRing;

			// Token: 0x040003E3 RID: 995
			public int membStampCur;

			// Token: 0x040003E4 RID: 996
			public int membRingsUsed;

			// Token: 0x040003E5 RID: 997
			public Vector3[] membN;

			// Token: 0x040003E6 RID: 998
			public readonly List<int> membList = new List<int>(2048);

			// Token: 0x040003E7 RID: 999
			public int dgMembN;

			// Token: 0x040003E8 RID: 1000
			public int dgMembSeeds;

			// Token: 0x040003E9 RID: 1001
			public int dgMembSweeps;

			// Token: 0x040003EA RID: 1002
			public float dgMembRes;

			// Token: 0x040003EB RID: 1003
			public float dgMembMs;

			// Token: 0x040003EC RID: 1004
			public float dgMembSur;

			// Token: 0x040003ED RID: 1005
			public float dgMembRip;

			// Token: 0x040003EE RID: 1006
			public float dgPitF;

			// Token: 0x040003EF RID: 1007
			public float dgPitG;

			// Token: 0x040003F0 RID: 1008
			public float dgPitFNbr;

			// Token: 0x040003F1 RID: 1009
			public float dgPitGNbr;

			// Token: 0x040003F2 RID: 1010
			public int dgPitFi = -1;

			// Token: 0x040003F3 RID: 1011
			public int dgPitGi = -1;

			// Token: 0x040003F4 RID: 1012
			public int dgPitFN;

			// Token: 0x040003F5 RID: 1013
			public int dgPitGN;

			// Token: 0x040003F6 RID: 1014
			public float[] adjW;

			// Token: 0x040003F7 RID: 1015
			public float[] adjWSum;

			// Token: 0x040003F8 RID: 1016
			public float dgDispEver;

			// Token: 0x040003F9 RID: 1017
			public float dgRdEver;

			// Token: 0x040003FA RID: 1018
			public float dgApprEver;

			// Token: 0x040003FB RID: 1019
			public float dgImpEver;

			// Token: 0x040003FC RID: 1020
			public int dgSkipLatch;

			// Token: 0x040003FD RID: 1021
			public int dgSkipFar;

			// Token: 0x040003FE RID: 1022
			public int dgJumpN;

			// Token: 0x040003FF RID: 1023
			public int dgOnOffN;

			// Token: 0x04000400 RID: 1024
			public bool dgSkipTook;

			// Token: 0x04000401 RID: 1025
			public bool dgSkipMoved;

			// Token: 0x04000402 RID: 1026
			public bool dgSkipBusy;

			// Token: 0x04000403 RID: 1027
			public bool dgSkipFree;

			// Token: 0x04000404 RID: 1028
			public float dgSkipVel;

			// Token: 0x04000405 RID: 1029
			public int dgSkipFrames;

			// Token: 0x04000406 RID: 1030
			public int dgShadeSteps;

			// Token: 0x04000407 RID: 1031
			public byte[] holdAge;

			// Token: 0x04000408 RID: 1032
			public byte[] nearPress;

			// Token: 0x04000409 RID: 1033
			public Vector3[] pushB;

			// Token: 0x0400040A RID: 1034
			public Vector3[] pushT;

			// Token: 0x0400040B RID: 1035
			public int[] domBone;

			// Token: 0x0400040C RID: 1036
			public Vector3[] domLocalPos;

			// Token: 0x0400040D RID: 1037
			public Quaternion[] bindRot;

			// Token: 0x0400040E RID: 1038
			public int[] subPa;

			// Token: 0x0400040F RID: 1039
			public int[] subPb;

			// Token: 0x04000410 RID: 1040
			public Vector3[] subFix;

			// Token: 0x04000411 RID: 1041
			public BoneWeight[] bwArr;

			// Token: 0x04000412 RID: 1042
			public Matrix4x4[] bindPoses;

			// Token: 0x04000413 RID: 1043
			public int origVertN;

			// Token: 0x04000414 RID: 1044
			public float subPoseHash;

			// Token: 0x04000415 RID: 1045
			public bool[] clothCovered;

			// Token: 0x04000416 RID: 1046
			public bool subFixDirty;

			// Token: 0x04000417 RID: 1047
			public bool clothBound;

			// Token: 0x04000418 RID: 1048
			public Transform[] bonesArr;

			// Token: 0x04000419 RID: 1049
			public float[] soften;

			// Token: 0x0400041A RID: 1050
			public readonly Vector3[] impPos = new Vector3[4];

			// Token: 0x0400041B RID: 1051
			public readonly float[] impStr = new float[4];

			// Token: 0x0400041C RID: 1052
			public bool anyImp;

			// Token: 0x0400041D RID: 1053
			public bool bakeReady;

			// Token: 0x0400041E RID: 1054
			public int[] bakeVerts;

			// Token: 0x0400041F RID: 1055
			public Vector3[][] bakeDelta;

			// Token: 0x04000420 RID: 1056
			public float[] bakeRadii;

			// Token: 0x04000421 RID: 1057
			public float[] bakeRimR;

			// Token: 0x04000422 RID: 1058
			public int[] rimVerts;

			// Token: 0x04000423 RID: 1059
			public Vector3 rimOrigin;

			// Token: 0x04000424 RID: 1060
			public Vector3 rimDir;

			// Token: 0x04000425 RID: 1061
			public float[] bakeDepths;

			// Token: 0x04000426 RID: 1062
			public float canalLen;

			// Token: 0x04000427 RID: 1063
			public float[] bakeAxialT;

			// Token: 0x04000428 RID: 1064
			public float[] bakeClitW;

			// Token: 0x04000429 RID: 1065
			public float[] bandW;

			// Token: 0x0400042A RID: 1066
			public Vector3[] boneSnapPos;

			// Token: 0x0400042B RID: 1067
			public Quaternion[] boneSnapRot;

			// Token: 0x0400042C RID: 1068
			public Vector3[] boneSnapScl;

			// Token: 0x0400042D RID: 1069
			public int[] bandVerts;

			// Token: 0x0400042E RID: 1070
			public BoneWeight[] bandBW;

			// Token: 0x0400042F RID: 1071
			public Matrix4x4[] bindArr;

			// Token: 0x04000430 RID: 1072
			public Vector3[] meshSnap;

			// Token: 0x04000431 RID: 1073
			public Vector3[] skinSnap;

			// Token: 0x04000432 RID: 1074
			public Vector3[] pushSnap;

			// Token: 0x04000433 RID: 1075
			public float[] bulgeDbg;

			// Token: 0x04000434 RID: 1076
			public float[] bakeOuterR;

			// Token: 0x04000435 RID: 1077
			public float[] bakeTiltW;

			// Token: 0x04000436 RID: 1078
			public Quaternion tiltB = Quaternion.identity;

			// Token: 0x04000437 RID: 1079
			public Vector3 itemDirW;

			// Token: 0x04000438 RID: 1080
			public bool[] foldVert;

			// Token: 0x04000439 RID: 1081
			public int openCool;

			// Token: 0x0400043A RID: 1082
			public readonly List<int> bsDepth = new List<int>();

			// Token: 0x0400043B RID: 1083
			public readonly List<int> bsWidth = new List<int>();

			// Token: 0x0400043C RID: 1084
			public readonly List<float> bsOrig = new List<float>();

			// Token: 0x0400043D RID: 1085
			public readonly List<int> bsAll = new List<int>();

			// Token: 0x0400043E RID: 1086
			public bool bsDriving;

			// Token: 0x0400043F RID: 1087
			public readonly List<Transform> vagPivots = new List<Transform>();

			// Token: 0x04000440 RID: 1088
			public readonly List<Quaternion> vagPivotRot = new List<Quaternion>();

			// Token: 0x04000441 RID: 1089
			public readonly List<Vector3> vagPivotPos = new List<Vector3>();

			// Token: 0x04000442 RID: 1090
			public readonly List<Transform> vagInner = new List<Transform>();

			// Token: 0x04000443 RID: 1091
			public readonly List<Vector3> vagInnerBase = new List<Vector3>();

			// Token: 0x04000444 RID: 1092
			public Transform vagRoot;

			// Token: 0x04000445 RID: 1093
			public Transform kosiT;

			// Token: 0x04000446 RID: 1094
			public Transform kokanDT;

			// Token: 0x04000447 RID: 1095
			public Quaternion kosiRestRot;

			// Token: 0x04000448 RID: 1096
			public Quaternion kokanRestRot;

			// Token: 0x04000449 RID: 1097
			public Vector3 kosiRestPos;

			// Token: 0x0400044A RID: 1098
			public Vector3 kokanRestPos;

			// Token: 0x0400044B RID: 1099
			public bool crotchCarry;

			// Token: 0x0400044C RID: 1100
			public Transform vagRootT;

			// Token: 0x0400044D RID: 1101
			public Transform anaDT;

			// Token: 0x0400044E RID: 1102
			public Transform siriLT;

			// Token: 0x0400044F RID: 1103
			public Transform siriRT;

			// Token: 0x04000450 RID: 1104
			public Quaternion siriLRestRot;

			// Token: 0x04000451 RID: 1105
			public Quaternion siriRRestRot;

			// Token: 0x04000452 RID: 1106
			public Transform siriSLT;

			// Token: 0x04000453 RID: 1107
			public Transform siriSRT;

			// Token: 0x04000454 RID: 1108
			public Quaternion siriSLRestRot;

			// Token: 0x04000455 RID: 1109
			public Quaternion siriSRRestRot;

			// Token: 0x04000456 RID: 1110
			public Vector3 siriLRestPos;

			// Token: 0x04000457 RID: 1111
			public Vector3 siriRRestPos;

			// Token: 0x04000458 RID: 1112
			public Vector3 siriSLRestPos;

			// Token: 0x04000459 RID: 1113
			public Vector3 siriSRRestPos;

			// Token: 0x0400045A RID: 1114
			public Quaternion anaRestRot;

			// Token: 0x0400045B RID: 1115
			public Vector3 anaRestPos;

			// Token: 0x0400045C RID: 1116
			public Quaternion vagRootRestRot;

			// Token: 0x0400045D RID: 1117
			public Vector3 vagRootRestPos;

			// Token: 0x0400045E RID: 1118
			public float itemW;

			// Token: 0x0400045F RID: 1119
			public float itemD;

			// Token: 0x04000460 RID: 1120
			public float[] velPrev;

			// Token: 0x04000461 RID: 1121
			public float[] waveShieldW;

			// Token: 0x04000462 RID: 1122
			public float[] tavgBuf;

			// Token: 0x04000463 RID: 1123
			public int[] seamSwStamp;

			// Token: 0x04000464 RID: 1124
			public int seamSwTick;

			// Token: 0x04000465 RID: 1125
			public Quaternion[] boneRotCache;

			// Token: 0x04000466 RID: 1126
			public Matrix4x4[] skinMCache;

			// Token: 0x04000467 RID: 1127
			public float gapeLevel;

			// Token: 0x04000468 RID: 1128
			public float lastItemDepth;

			// Token: 0x04000469 RID: 1129
			public float suckOff;

			// Token: 0x0400046A RID: 1130
			public bool itemInside;

			// Token: 0x0400046B RID: 1131
			public float crossOffM = -1f;

			// Token: 0x0400046C RID: 1132
			public Vector3 axisEwW;

			// Token: 0x0400046D RID: 1133
			public Vector3 axisDwW;

			// Token: 0x0400046E RID: 1134
			public Transform spreadL;

			// Token: 0x0400046F RID: 1135
			public Transform spreadR;

			// Token: 0x04000470 RID: 1136
			public Quaternion spreadLRot;

			// Token: 0x04000471 RID: 1137
			public Quaternion spreadRRot;

			// Token: 0x04000472 RID: 1138
			public Vector3 spreadLPos;

			// Token: 0x04000473 RID: 1139
			public Vector3 spreadRPos;

			// Token: 0x04000474 RID: 1140
			public Vector3 followB;

			// Token: 0x04000475 RID: 1141
			public bool axisFrameOK;

			// Token: 0x04000476 RID: 1142
			public int axisBoneIx = -1;

			// Token: 0x04000477 RID: 1143
			public float[] bakeSuckW;

			// Token: 0x04000478 RID: 1144
			public float[] bakePetW;

			// Token: 0x04000479 RID: 1145
			public bool guideSet;

			// Token: 0x0400047A RID: 1146
			public Vector3 lastBakeEntryB;

			// Token: 0x0400047B RID: 1147
			public Vector3 lastBakeDirB;

			// Token: 0x0400047C RID: 1148
			public int guideBoneIx = -1;

			// Token: 0x0400047D RID: 1149
			public Vector3 guideLocalPos;

			// Token: 0x0400047E RID: 1150
			public Vector3 guideLocalDir;

			// Token: 0x0400047F RID: 1151
			public Transform vagHelper;

			// Token: 0x04000480 RID: 1152
			public Vector3 vagHelperScale = Vector3.one;

			// Token: 0x04000481 RID: 1153
			public Transform ringRef;

			// Token: 0x04000482 RID: 1154
			public Vector3 ringLocalC;

			// Token: 0x04000483 RID: 1155
			public Vector3 ringLocalAxis;

			// Token: 0x04000484 RID: 1156
			public bool ringFrameOK;

			// Token: 0x04000485 RID: 1157
			public readonly List<Transform> ringBones = new List<Transform>();

			// Token: 0x04000486 RID: 1158
			public readonly List<Vector3> ringBonePos = new List<Vector3>();

			// Token: 0x04000487 RID: 1159
			public bool vagDriving;

			// Token: 0x04000488 RID: 1160
			public int[] foldVerts;

			// Token: 0x04000489 RID: 1161
			public Vector3[] foldBuf;

			// Token: 0x0400048A RID: 1162
			public int[] creaseTris;

			// Token: 0x0400048B RID: 1163
			public bool creaseBuilt;

			// Token: 0x0400048C RID: 1164
			public int[] creaseVerts;

			// Token: 0x0400048D RID: 1165
			public bool[] creaseMark;

			// Token: 0x0400048E RID: 1166
			public readonly List<DynamicBone> vagDyn = new List<DynamicBone>();

			// Token: 0x0400048F RID: 1167
			public readonly List<object> dynParts = new List<object>();

			// Token: 0x04000490 RID: 1168
			public readonly List<Transform> dynPartTr = new List<Transform>();

			// Token: 0x04000491 RID: 1169
			public readonly List<Vector3> dynPartRest = new List<Vector3>();

			// Token: 0x04000492 RID: 1170
			public FieldInfo dynInitPosF;

			// Token: 0x04000493 RID: 1171
			public readonly List<float> dynStifOrig = new List<float>();

			// Token: 0x04000494 RID: 1172
			public readonly List<float> dynDampOrig = new List<float>();

			// Token: 0x04000495 RID: 1173
			public readonly List<float> dynElastOrig = new List<float>();

			// Token: 0x04000496 RID: 1174
			public MethodInfo dynUpdParams;

			// Token: 0x04000497 RID: 1175
			public float lastFirm = -1f;

			// Token: 0x04000498 RID: 1176
			public int[] openStamp;

			// Token: 0x04000499 RID: 1177
			public Vector3[] pushLast;

			// Token: 0x0400049A RID: 1178
			public float lastSig;

			// Token: 0x0400049B RID: 1179
			public float lastPetalSig;

			// Token: 0x0400049C RID: 1180
			public bool sigInit;

			// Token: 0x0400049D RID: 1181
			public bool pushSettling;

			// Token: 0x0400049E RID: 1182
			public int openCur;

			// Token: 0x0400049F RID: 1183
			public float lastOpenMm;

			// Token: 0x040004A0 RID: 1184
			public float lastSpreadMm;

			// Token: 0x040004A1 RID: 1185
			public float sprCarry;

			// Token: 0x040004A2 RID: 1186
			public float sprApplied;

			// Token: 0x040004A3 RID: 1187
			public float pivTurn;

			// Token: 0x040004A4 RID: 1188
			public float trkRadMm;

			// Token: 0x040004A5 RID: 1189
			public float trkDepthMm;

			// Token: 0x040004A6 RID: 1190
			public float trkLatMm;

			// Token: 0x040004A7 RID: 1191
			public float trkRadAngMm;

			// Token: 0x040004A8 RID: 1192
			public float trkAlongT;

			// Token: 0x040004A9 RID: 1193
			public float trkLobeLenMm;

			// Token: 0x040004AA RID: 1194
			public string trkLobe;

			// Token: 0x040004AB RID: 1195
			public float[] canalWAt;

			// Token: 0x040004AC RID: 1196
			public float[] canalLatAt;

			// Token: 0x040004AD RID: 1197
			public float lastRadAtPlane;

			// Token: 0x040004AE RID: 1198
			public float rimNowMm;

			// Token: 0x040004AF RID: 1199
			public float itemWff;

			// Token: 0x040004B0 RID: 1200
			public float rimMajMm;

			// Token: 0x040004B1 RID: 1201
			public float rimMinMm;

			// Token: 0x040004B2 RID: 1202
			public float rimThickMm;

			// Token: 0x040004B3 RID: 1203
			public float rimFlatDeg;

			// Token: 0x040004B4 RID: 1204
			public float vulMajMm;

			// Token: 0x040004B5 RID: 1205
			public float vulMinMm;

			// Token: 0x040004B6 RID: 1206
			public float vulThickMm;

			// Token: 0x040004B7 RID: 1207
			public int axisFrameNo;

			// Token: 0x040004B8 RID: 1208
			public int bakeFrameNo;

			// Token: 0x040004B9 RID: 1209
			public bool baked;

			// Token: 0x040004BA RID: 1210
			public bool isFace;

			// Token: 0x040004BB RID: 1211
			public int[] mouthIdx;

			// Token: 0x040004BC RID: 1212
			public int mouthPtn = -1;

			// Token: 0x040004BD RID: 1213
			public int mouthKuwae = -1;

			// Token: 0x040004BE RID: 1214
			public bool[] mouthCore;

			// Token: 0x040004BF RID: 1215
			public float[] mouthW;

			// Token: 0x040004C0 RID: 1216
			public Vector3 mouthC;

			// Token: 0x040004C1 RID: 1217
			public bool mouthCValid;

			// Token: 0x040004C2 RID: 1218
			public Vector3[] mouthDv;

			// Token: 0x040004C3 RID: 1219
			public int mouthBinP = -1;

			// Token: 0x040004C4 RID: 1220
			public Vector3 mouthBinU;

			// Token: 0x040004C5 RID: 1221
			public Vector3 mouthBinV;

			// Token: 0x040004C6 RID: 1222
			public float[] mouthBinR;

			// Token: 0x040004C7 RID: 1223
			public Transform mouthOwner;

			// Token: 0x040004C8 RID: 1224
			public Vector3 mouthAxP;

			// Token: 0x040004C9 RID: 1225
			public Vector3 mouthAxU;

			// Token: 0x040004CA RID: 1226
			public float mouthR;

			// Token: 0x040004CB RID: 1227
			public int[] mouthPartner;

			// Token: 0x040004CC RID: 1228
			public Vector3[] mouthPushW;

			// Token: 0x040004CD RID: 1229
			public bool mouthPushValid;

			// Token: 0x040004CE RID: 1230
			public float mouthPenMax;

			// Token: 0x040004CF RID: 1231
			public byte[] mouthRow;

			// Token: 0x040004D0 RID: 1232
			public float[] mouthLipDist;

			// Token: 0x040004D1 RID: 1233
			public int[] mouthCovStart;

			// Token: 0x040004D2 RID: 1234
			public int[] mouthCovList;

			// Token: 0x040004D3 RID: 1235
			public int[] mouthPartners;

			// Token: 0x040004D4 RID: 1236
			public bool[] mouthIn;

			// Token: 0x040004D5 RID: 1237
			public bool[] mouthLining;

			// Token: 0x040004D6 RID: 1238
			public bool[] mouthWet;

			// Token: 0x040004D7 RID: 1239
			public bool[] mouthRing1;

			// Token: 0x040004D8 RID: 1240
			public int mwInside;

			// Token: 0x040004D9 RID: 1241
			public int mwCarried;

			// Token: 0x040004DA RID: 1242
			public int mwInN;

			// Token: 0x040004DB RID: 1243
			public int mwLiningN;

			// Token: 0x040004DC RID: 1244
			public int mwClamped;

			// Token: 0x040004DD RID: 1245
			public float mwResid;

			// Token: 0x040004DE RID: 1246
			public Vector3[] mouthGR;

			// Token: 0x040004DF RID: 1247
			public float[] mouthAngR;

			// Token: 0x040004E0 RID: 1248
			public float[] mouthAloR;

			// Token: 0x040004E1 RID: 1249
			public float[] mouthAng;

			// Token: 0x040004E2 RID: 1250
			public float[] mouthAlo;

			// Token: 0x040004E3 RID: 1251
			public float mouthAloMin;

			// Token: 0x040004E4 RID: 1252
			public float mouthAloMax;

			// Token: 0x040004E5 RID: 1253
			public Vector3 mouthOutW;

			// Token: 0x040004E6 RID: 1254
			public float mwDepthPrev = -1E+09f;

			// Token: 0x040004E7 RID: 1255
			public float mwDrag;

			// Token: 0x040004E8 RID: 1256
			public float mwDragNow;

			// Token: 0x040004E9 RID: 1257
			public float mwStick;

			// Token: 0x040004EA RID: 1258
			public int mwHugN;

			// Token: 0x040004EB RID: 1259
			public int mwHugFar;

			// Token: 0x040004EC RID: 1260
			public float mwHugMax;

			// Token: 0x040004ED RID: 1261
			public int mwFarHeld;

			// Token: 0x040004EE RID: 1262
			public float mwFarHeldMax;

			// Token: 0x040004EF RID: 1263
			public float mwInMax;

			// Token: 0x040004F0 RID: 1264
			public float mwInDrag;

			// Token: 0x040004F1 RID: 1265
			public float mwInRing;

			// Token: 0x040004F2 RID: 1266
			public float mwInSmooth;

			// Token: 0x040004F3 RID: 1267
			public float mwInOut;

			// Token: 0x040004F4 RID: 1268
			public float mwInAxMax;

			// Token: 0x040004F5 RID: 1269
			public int mwInV = -1;

			// Token: 0x040004F6 RID: 1270
			public int mwInRedir;

			// Token: 0x040004F7 RID: 1271
			public Vector3 mwFaceOut;

			// Token: 0x040004F8 RID: 1272
			public float[] mouthUpperF;

			// Token: 0x040004F9 RID: 1273
			public float mwInClampMax;

			// Token: 0x040004FA RID: 1274
			public int mwCollarN;

			// Token: 0x040004FB RID: 1275
			public float mwCollarMax;

			// Token: 0x040004FC RID: 1276
			public float mwCollarEng;

			// Token: 0x040004FD RID: 1277
			public int mwLinHeld;

			// Token: 0x040004FE RID: 1278
			public float mwLinHeldMax;

			// Token: 0x040004FF RID: 1279
			public int mwSkinIn;

			// Token: 0x04000500 RID: 1280
			public int mwSkinInPk;

			// Token: 0x04000501 RID: 1281
			public int mwSkinFix;

			// Token: 0x04000502 RID: 1282
			public float mwSkinInMax;

			// Token: 0x04000503 RID: 1283
			public float mwSkinInPkMax;

			// Token: 0x04000504 RID: 1284
			public float mwSkinFixMax;

			// Token: 0x04000505 RID: 1285
			public float mwTableTau;

			// Token: 0x04000506 RID: 1286
			public float mwRTarget;

			// Token: 0x04000507 RID: 1287
			public float mwDragT;

			// Token: 0x04000508 RID: 1288
			public float mwTableT;

			// Token: 0x04000509 RID: 1289
			public float mwOpenDelta;

			// Token: 0x0400050A RID: 1290
			public bool mwTableOK;

			// Token: 0x0400050B RID: 1291
			public float[] mwTD;

			// Token: 0x0400050C RID: 1292
			public float[] mwTA;

			// Token: 0x0400050D RID: 1293
			public float[] mwTT;

			// Token: 0x0400050E RID: 1294
			public float mwSolveT;

			// Token: 0x0400050F RID: 1295
			public float mwJitWDragV;

			// Token: 0x04000510 RID: 1296
			public float mwJitWRing;

			// Token: 0x04000511 RID: 1297
			public float mwJitWSmooth;

			// Token: 0x04000512 RID: 1298
			public float mwJitWOut;

			// Token: 0x04000513 RID: 1299
			public float mwJitWLip;

			// Token: 0x04000514 RID: 1300
			public float mwJitWFR;

			// Token: 0x04000515 RID: 1301
			public float mwJitWClr;

			// Token: 0x04000516 RID: 1302
			public bool mwTableReset;

			// Token: 0x04000517 RID: 1303
			public bool mwJitWReset;

			// Token: 0x04000518 RID: 1304
			public float mwSolveGap;

			// Token: 0x04000519 RID: 1305
			public float mwJitWGap;

			// Token: 0x0400051A RID: 1306
			public float mwJitWDrag;

			// Token: 0x0400051B RID: 1307
			public float mwJitWItem;

			// Token: 0x0400051C RID: 1308
			public float mwJitWUp;

			// Token: 0x0400051D RID: 1309
			public float mwJitWRt;

			// Token: 0x0400051E RID: 1310
			public float mwJitWFw;

			// Token: 0x0400051F RID: 1311
			public float mwJitWW;

			// Token: 0x04000520 RID: 1312
			public float mwDragPrevSolve;

			// Token: 0x04000521 RID: 1313
			public int mwJitWV = -1;

			// Token: 0x04000522 RID: 1314
			public string mwJitHoldLine;

			// Token: 0x04000523 RID: 1315
			public float mwJitSkinH;

			// Token: 0x04000524 RID: 1316
			public Vector3[] mwPPrev;

			// Token: 0x04000525 RID: 1317
			public Vector3 mwOwnerPrev;

			// Token: 0x04000526 RID: 1318
			public float mwJitSkinMax;

			// Token: 0x04000527 RID: 1319
			public float mwJitSkinMean;

			// Token: 0x04000528 RID: 1320
			public float mwJitWallMax;

			// Token: 0x04000529 RID: 1321
			public float mwJitItem;

			// Token: 0x0400052A RID: 1322
			public float mwJitSkinW;

			// Token: 0x0400052B RID: 1323
			public float mwJitOpenW;

			// Token: 0x0400052C RID: 1324
			public float mwJitItemW;

			// Token: 0x0400052D RID: 1325
			public int mwJitN;

			// Token: 0x0400052E RID: 1326
			public Vector3 mwAxSmooth;

			// Token: 0x0400052F RID: 1327
			public Vector3 mwDepthO;

			// Token: 0x04000530 RID: 1328
			public int mwExposed;

			// Token: 0x04000531 RID: 1329
			public int mwSunk;

			// Token: 0x04000532 RID: 1330
			public List<int> mwXVerts = new List<int>();

			// Token: 0x04000533 RID: 1331
			public List<int> mwXPairs = new List<int>();

			// Token: 0x04000534 RID: 1332
			public int mwLifted;

			// Token: 0x04000535 RID: 1333
			public float mwLiftMax;

			// Token: 0x04000536 RID: 1334
			public float mwTiltSum;

			// Token: 0x04000537 RID: 1335
			public float mwTiltMin;

			// Token: 0x04000538 RID: 1336
			public float mwGainMax;

			// Token: 0x04000539 RID: 1337
			public float mwGainSum;

			// Token: 0x0400053A RID: 1338
			public int mwTiltN;

			// Token: 0x0400053B RID: 1339
			public int mwNearAxis;

			// Token: 0x0400053C RID: 1340
			public int mwRode;

			// Token: 0x0400053D RID: 1341
			public int mwCross;

			// Token: 0x0400053E RID: 1342
			public int mwCrossV;

			// Token: 0x0400053F RID: 1343
			public float mwCrossH;

			// Token: 0x04000540 RID: 1344
			public int[] mouthTris;

			// Token: 0x04000541 RID: 1345
			public byte[] mouthTriSub;

			// Token: 0x04000542 RID: 1346
			public string[] mouthMatNames;

			// Token: 0x04000543 RID: 1347
			public double mwSig;

			// Token: 0x04000544 RID: 1348
			public bool mwSkipped;

			// Token: 0x04000545 RID: 1349
			public int mwSkipRun;

			// Token: 0x04000546 RID: 1350
			public float[] mwMs = new float[8];

			// Token: 0x04000547 RID: 1351
			public float[] mwMs2 = new float[10];

			// Token: 0x04000548 RID: 1352
			public int mwFinIn;

			// Token: 0x04000549 RID: 1353
			public int mwFinOut;

			// Token: 0x0400054A RID: 1354
			public float mwFinInMax;

			// Token: 0x0400054B RID: 1355
			public float mwFinOutMax;

			// Token: 0x0400054C RID: 1356
			public int[] mwTop = new int[6];

			// Token: 0x0400054D RID: 1357
			public string mwAxisInfo = "";

			// Token: 0x0400054E RID: 1358
			public int[] mouthNbrStart;

			// Token: 0x0400054F RID: 1359
			public int[] mouthNbrList;

			// Token: 0x04000550 RID: 1360
			public int[] mouthHalo;

			// Token: 0x04000551 RID: 1361
			public float[] mouthT;

			// Token: 0x04000552 RID: 1362
			public Vector3[] mouthG;

			// Token: 0x04000553 RID: 1363
			public float mouthThickRest = -1f;

			// Token: 0x04000554 RID: 1364
			public float mouthThickNow = -1f;

			// Token: 0x04000555 RID: 1365
			public float[] mouthBinNow;

			// Token: 0x04000556 RID: 1366
			public int fcReach;

			// Token: 0x04000557 RID: 1367
			public int fcPushCore;

			// Token: 0x04000558 RID: 1368
			public int fcPushOuter;

			// Token: 0x04000559 RID: 1369
			public int fcSkipBin;

			// Token: 0x0400055A RID: 1370
			public float fcStepOuter;

			// Token: 0x0400055B RID: 1371
			public float fcPenOuterMin;

			// Token: 0x0400055C RID: 1372
			public bool mouthDriven;

			// Token: 0x0400055D RID: 1373
			public int mouthOrigPtn;

			// Token: 0x0400055E RID: 1374
			public float mouthOrigOpen;

			// Token: 0x0400055F RID: 1375
			public float mouthOpenNow = -1f;

			// Token: 0x04000560 RID: 1376
			public float mouthLastSeen;

			// Token: 0x04000561 RID: 1377
			public float mouthApert = -1f;

			// Token: 0x04000562 RID: 1378
			public string mouthInfo = "";

			// Token: 0x04000563 RID: 1379
			public float tiltMm;

			// Token: 0x04000564 RID: 1380
			public int[] sprVerts;

			// Token: 0x04000565 RID: 1381
			public Mesh sprBakeA;

			// Token: 0x04000566 RID: 1382
			public Mesh sprBakeB;

			// Token: 0x04000567 RID: 1383
			public readonly List<Vector3> sprA = new List<Vector3>();

			// Token: 0x04000568 RID: 1384
			public readonly List<Vector3> sprB = new List<Vector3>();

			// Token: 0x04000569 RID: 1385
			public float sprFlesh = -1f;

			// Token: 0x0400056A RID: 1386
			public float[] nipW;

			// Token: 0x0400056B RID: 1387
			public float[] kokW;

			// Token: 0x0400056C RID: 1388
			public float[] anaW;

			// Token: 0x0400056D RID: 1389
			public float[] petW;

			// Token: 0x0400056E RID: 1390
			public float[] vagW;

			// Token: 0x0400056F RID: 1391
			public Mesh bake;

			// Token: 0x04000570 RID: 1392
			public readonly List<Vector3> bakeV = new List<Vector3>();

			// Token: 0x04000571 RID: 1393
			public readonly List<Vector3> bakeN = new List<Vector3>();

			// Token: 0x04000572 RID: 1394
			public readonly List<Vector3> outV = new List<Vector3>();

			// Token: 0x04000573 RID: 1395
			public int outVersion;

			// Token: 0x04000574 RID: 1396
			public Transform vagRootNamed;

			// Token: 0x04000575 RID: 1397
			public string vagRootName = "(null)";

			// Token: 0x04000576 RID: 1398
			public int[] actArr;

			// Token: 0x04000577 RID: 1399
			public bool hasActive;

			// Token: 0x04000578 RID: 1400
			public bool freeResidue;

			// Token: 0x04000579 RID: 1401
			public bool hadActive;

			// Token: 0x0400057A RID: 1402
			public float[] renderDisp;

			// Token: 0x0400057B RID: 1403
			public float[] rdBuf;

			// Token: 0x0400057C RID: 1404
			public int[] actStamp;

			// Token: 0x0400057D RID: 1405
			public int stampCur;

			// Token: 0x0400057E RID: 1406
			public readonly List<int> actList = new List<int>();

			// Token: 0x0400057F RID: 1407
			public List<int> simList = new List<int>();

			// Token: 0x04000580 RID: 1408
			public List<int> prevSim = new List<int>();

			// Token: 0x04000581 RID: 1409
			public readonly List<int> candList = new List<int>();

			// Token: 0x04000582 RID: 1410
			public readonly List<int> dirty = new List<int>();

			// Token: 0x04000583 RID: 1411
			public int[] grpStart;

			// Token: 0x04000584 RID: 1412
			public int[] grpVerts;

			// Token: 0x04000585 RID: 1413
			public Vector3[] grpMin;

			// Token: 0x04000586 RID: 1414
			public Vector3[] grpMax;

			// Token: 0x04000587 RID: 1415
			public readonly List<int> hitBones = new List<int>();
		}

		// Token: 0x02000010 RID: 16
		private class Presser
		{
			// Token: 0x1700002F RID: 47
			// (get) Token: 0x060001D5 RID: 469 RVA: 0x00057927 File Offset: 0x00055B27
			public string BName
			{
				get
				{
					if (this._bn == null)
					{
						this._bn = ((this.b != null) ? this.b.name : "?");
					}
					return this._bn;
				}
			}

			// Token: 0x04000588 RID: 1416
			public Transform a;

			// Token: 0x04000589 RID: 1417
			public Transform b;

			// Token: 0x0400058A RID: 1418
			public float ext;

			// Token: 0x0400058B RID: 1419
			public bool right;

			// Token: 0x0400058C RID: 1420
			public bool prox;

			// Token: 0x0400058D RID: 1421
			public ChaControl cha;

			// Token: 0x0400058E RID: 1422
			public Vector3 lastPos;

			// Token: 0x0400058F RID: 1423
			public bool init;

			// Token: 0x04000590 RID: 1424
			public bool item;

			// Token: 0x04000591 RID: 1425
			public Vector3 la;

			// Token: 0x04000592 RID: 1426
			public Vector3 lb;

			// Token: 0x04000593 RID: 1427
			public float lrad;

			// Token: 0x04000594 RID: 1428
			public float lradB;

			// Token: 0x04000595 RID: 1429
			public bool tipA;

			// Token: 0x04000596 RID: 1430
			public bool tipB;

			// Token: 0x04000597 RID: 1431
			public bool insertMode;

			// Token: 0x04000598 RID: 1432
			public float bulgeW;

			// Token: 0x04000599 RID: 1433
			public float lradMax;

			// Token: 0x0400059A RID: 1434
			public float tipF;

			// Token: 0x0400059B RID: 1435
			public SoftFleshController.Prof prof;

			// Token: 0x0400059C RID: 1436
			public Transform owner;

			// Token: 0x0400059D RID: 1437
			private string _bn;
		}

		// Token: 0x02000011 RID: 17
		public class Prof
		{
			// Token: 0x060001D7 RID: 471 RVA: 0x00057968 File Offset: 0x00055B68
			public float SampleRadius(Vector3 pLocal)
			{
				Vector3 d = pLocal - this.origin;
				float along = Vector3.Dot(d, this.ax);
				float fs = Mathf.Clamp01((this.len > 1E-06f) ? (along / this.len) : 0f) * (float)(this.NS - 1);
				int s0 = (int)fs;
				if (s0 > this.NS - 2)
				{
					s0 = this.NS - 2;
				}
				if (s0 < 0)
				{
					s0 = 0;
				}
				float ts = fs - (float)s0;
				Vector3 lat = d - this.ax * along;
				float ang = SoftFleshController.FastAtan2(Vector3.Dot(lat, this.v), Vector3.Dot(lat, this.u));
				float fb = (ang + 3.1415927f) * ((float)this.NB / 6.2831855f) - 0.5f;
				int b0 = Mathf.FloorToInt(fb);
				float tb = fb - (float)b0;
				b0 = (b0 % this.NB + this.NB) % this.NB;
				int b = (b0 + 1) % this.NB;
				float lo2 = Mathf.Lerp(this.r[s0 * this.NB + b0], this.r[s0 * this.NB + b], tb);
				float hi2 = Mathf.Lerp(this.r[(s0 + 1) * this.NB + b0], this.r[(s0 + 1) * this.NB + b], tb);
				return Mathf.Lerp(lo2, hi2, ts);
			}

			// Token: 0x060001D8 RID: 472 RVA: 0x00057AD4 File Offset: 0x00055CD4
			public float MeanRadiusAt(Vector3 pLocal)
			{
				float along = Vector3.Dot(pLocal - this.origin, this.ax);
				float fs = Mathf.Clamp01((this.len > 1E-06f) ? (along / this.len) : 0f) * (float)(this.NS - 1);
				int s0 = (int)fs;
				if (s0 > this.NS - 2)
				{
					s0 = this.NS - 2;
				}
				if (s0 < 0)
				{
					s0 = 0;
				}
				float ts = fs - (float)s0;
				float mA = 0f;
				float mB = 0f;
				int o0 = s0 * this.NB;
				int o = (s0 + 1) * this.NB;
				for (int b = 0; b < this.NB; b++)
				{
					mA += this.r[o0 + b];
					mB += this.r[o + b];
				}
				return (mA + (mB - mA) * ts) / (float)this.NB;
			}

			// Token: 0x060001D9 RID: 473 RVA: 0x00057BB4 File Offset: 0x00055DB4
			public float MaxRadiusAt(Vector3 pLocal)
			{
				float along = Vector3.Dot(pLocal - this.origin, this.ax);
				float fs = Mathf.Clamp01((this.len > 1E-06f) ? (along / this.len) : 0f) * (float)(this.NS - 1);
				int s0 = (int)fs;
				if (s0 > this.NS - 2)
				{
					s0 = this.NS - 2;
				}
				if (s0 < 0)
				{
					s0 = 0;
				}
				float ts = fs - (float)s0;
				float mA = 0f;
				float mB = 0f;
				int o0 = s0 * this.NB;
				int o = (s0 + 1) * this.NB;
				for (int b = 0; b < this.NB; b++)
				{
					if (this.r[o0 + b] > mA)
					{
						mA = this.r[o0 + b];
					}
					if (this.r[o + b] > mB)
					{
						mB = this.r[o + b];
					}
				}
				return mA + (mB - mA) * ts;
			}

			// Token: 0x060001DA RID: 474 RVA: 0x00057CA8 File Offset: 0x00055EA8
			public Vector3 SurfacePoint(int s, int b)
			{
				float ang = ((float)b + 0.5f) * (6.2831855f / (float)this.NB) - 3.1415927f;
				float t = ((this.NS > 1) ? ((float)s / (float)(this.NS - 1)) : 0f);
				return this.origin + this.ax * (t * this.len) + (this.u * Mathf.Cos(ang) + this.v * Mathf.Sin(ang)) * this.r[s * this.NB + b];
			}

			// Token: 0x0400059E RID: 1438
			public Vector3 origin;

			// Token: 0x0400059F RID: 1439
			public Vector3 ax;

			// Token: 0x040005A0 RID: 1440
			public Vector3 u;

			// Token: 0x040005A1 RID: 1441
			public Vector3 v;

			// Token: 0x040005A2 RID: 1442
			public float len;

			// Token: 0x040005A3 RID: 1443
			public int NS;

			// Token: 0x040005A4 RID: 1444
			public int NB;

			// Token: 0x040005A5 RID: 1445
			public float[] r;

			// Token: 0x040005A6 RID: 1446
			public float rMax;

			// Token: 0x040005A7 RID: 1447
			public int vizFrame = -1;
		}

		// Token: 0x02000012 RID: 18
		private class DbAccess
		{
			// Token: 0x040005A8 RID: 1448
			public FieldInfo force;

			// Token: 0x040005A9 RID: 1449
			public FieldInfo stif;

			// Token: 0x040005AA RID: 1450
			public FieldInfo damp;

			// Token: 0x040005AB RID: 1451
			public FieldInfo elast;

			// Token: 0x040005AC RID: 1452
			public MethodInfo update;

			// Token: 0x040005AD RID: 1453
			public string typeName;
		}

		// Token: 0x02000013 RID: 19
		private class AimTarget
		{
			// Token: 0x040005AE RID: 1454
			public Transform root;

			// Token: 0x040005AF RID: 1455
			public Component[] bones;

			// Token: 0x040005B0 RID: 1456
			public SoftFleshController.DbAccess[] acc;

			// Token: 0x040005B1 RID: 1457
			public Vector3[] origForce;

			// Token: 0x040005B2 RID: 1458
			public float[] origStif;

			// Token: 0x040005B3 RID: 1459
			public float[] origDamp;

			// Token: 0x040005B4 RID: 1460
			public float[] origElast;

			// Token: 0x040005B5 RID: 1461
			public IList[] parts;

			// Token: 0x040005B6 RID: 1462
			public float[][] pStif;

			// Token: 0x040005B7 RID: 1463
			public float[][] pDamp;

			// Token: 0x040005B8 RID: 1464
			public float[][] pElast;

			// Token: 0x040005B9 RID: 1465
			public float lastFirm = -1f;

			// Token: 0x040005BA RID: 1466
			public Vector3 lastTip;

			// Token: 0x040005BB RID: 1467
			public bool tipInit;

			// Token: 0x040005BC RID: 1468
			public Vector3 userPos;

			// Token: 0x040005BD RID: 1469
			public Vector3 lastWritten;

			// Token: 0x040005BE RID: 1470
			public Vector3 railOff;

			// Token: 0x040005BF RID: 1471
			public bool railHeld;

			// Token: 0x040005C0 RID: 1472
			public Transform[][] chain;

			// Token: 0x040005C1 RID: 1473
			public Quaternion[][] restRot;

			// Token: 0x040005C2 RID: 1474
			public Vector3[][] segDir;

			// Token: 0x040005C3 RID: 1475
			public bool[] shaft;

			// Token: 0x040005C4 RID: 1476
			public bool[] wasEnabled;

			// Token: 0x040005C5 RID: 1477
			public bool driving;

			// Token: 0x040005C6 RID: 1478
			public float blend;
		}

		// Token: 0x02000014 RID: 20
		private struct Spike
		{
			// Token: 0x040005C7 RID: 1479
			public int frame;

			// Token: 0x040005C8 RID: 1480
			public int gc;

			// Token: 0x040005C9 RID: 1481
			public int act;

			// Token: 0x040005CA RID: 1482
			public int sim;

			// Token: 0x040005CB RID: 1483
			public int steps;

			// Token: 0x040005CC RID: 1484
			public int worst;

			// Token: 0x040005CD RID: 1485
			public int cvL;

			// Token: 0x040005CE RID: 1486
			public int cvG;

			// Token: 0x040005CF RID: 1487
			public float dt;

			// Token: 0x040005D0 RID: 1488
			public float ours;

			// Token: 0x040005D1 RID: 1489
			public float worstMs;

			// Token: 0x040005D2 RID: 1490
			public float sU;

			// Token: 0x040005D3 RID: 1491
			public float sL1;

			// Token: 0x040005D4 RID: 1492
			public float sL2;

			// Token: 0x040005D5 RID: 1493
			public float sR;

			// Token: 0x040005D6 RID: 1494
			public float sE;
		}

		// Token: 0x02000015 RID: 21
		private sealed class MaintRec
		{
			// Token: 0x040005D7 RID: 1495
			public ChaControl cha;

			// Token: 0x040005D8 RID: 1496
			public GameObject body;

			// Token: 0x040005D9 RID: 1497
			public Transform kokan;

			// Token: 0x040005DA RID: 1498
			public Transform h0;

			// Token: 0x040005DB RID: 1499
			public Transform h1;

			// Token: 0x040005DC RID: 1500
			public SkinnedMeshRenderer[] mnp;
		}

		// Token: 0x02000016 RID: 22
		private sealed class SkinRec
		{
			// Token: 0x040005DD RID: 1501
			public SkinnedMeshRenderer origSmr;

			// Token: 0x040005DE RID: 1502
			public Mesh origMesh;

			// Token: 0x040005DF RID: 1503
			public int slot = -1;

			// Token: 0x040005E0 RID: 1504
			public byte lastState = byte.MaxValue;

			// Token: 0x040005E1 RID: 1505
			public bool lastActive = true;

			// Token: 0x040005E2 RID: 1506
			public bool lastEnabled;

			// Token: 0x040005E3 RID: 1507
			public bool wantVis = true;

			// Token: 0x040005E4 RID: 1508
			public GameObject go;

			// Token: 0x040005E5 RID: 1509
			public SkinnedMeshRenderer smr;

			// Token: 0x040005E6 RID: 1510
			public Mesh mesh;

			// Token: 0x040005E7 RID: 1511
			public SoftFleshController.Body body;

			// Token: 0x040005E8 RID: 1512
			public int[] map;

			// Token: 0x040005E9 RID: 1513
			public Vector3[] offV;

			// Token: 0x040005EA RID: 1514
			public List<Vector3> scrV;

			// Token: 0x040005EB RID: 1515
			public List<Vector3> scrN;

			// Token: 0x040005EC RID: 1516
			public Material[] matCache;

			// Token: 0x040005ED RID: 1517
			public int lastVersion = -1;

			// Token: 0x040005EE RID: 1518
			public bool lastVis;

			// Token: 0x040005EF RID: 1519
			public int shapeN;

			// Token: 0x040005F0 RID: 1520
			public int[] shapeSrc;

			// Token: 0x040005F1 RID: 1521
			public float[] shapeW;
		}

		// Token: 0x02000017 RID: 23
		private class ClothRec
		{
			// Token: 0x040005F2 RID: 1522
			public SkinnedMeshRenderer smr;

			// Token: 0x040005F3 RID: 1523
			public Mesh orig;

			// Token: 0x040005F4 RID: 1524
			public Mesh clone;

			// Token: 0x040005F5 RID: 1525
			public Vector3[] rawV;

			// Token: 0x040005F6 RID: 1526
			public Vector3[] baseV;

			// Token: 0x040005F7 RID: 1527
			public Vector3[] work;

			// Token: 0x040005F8 RID: 1528
			public Vector3[] liftN;

			// Token: 0x040005F9 RID: 1529
			public float[] liftW;

			// Token: 0x040005FA RID: 1530
			public Vector3[] nrm0;

			// Token: 0x040005FB RID: 1531
			public Vector3[] nrmW;

			// Token: 0x040005FC RID: 1532
			public int[] triA;

			// Token: 0x040005FD RID: 1533
			public int[] triB;

			// Token: 0x040005FE RID: 1534
			public int[] triC;

			// Token: 0x040005FF RID: 1535
			public Vector3[] triW;

			// Token: 0x04000600 RID: 1536
			public int[] domCloth;

			// Token: 0x04000601 RID: 1537
			public Quaternion[] clothBindRot;

			// Token: 0x04000602 RID: 1538
			public Transform[] clothBones;

			// Token: 0x04000603 RID: 1539
			public Quaternion[] clothRotInv;

			// Token: 0x04000604 RID: 1540
			public int[] grpStart;

			// Token: 0x04000605 RID: 1541
			public int[] grpList;

			// Token: 0x04000606 RID: 1542
			public SoftFleshController.Body body;

			// Token: 0x04000607 RID: 1543
			public int[] nbrStart;

			// Token: 0x04000608 RID: 1544
			public int[] nbrList;

			// Token: 0x04000609 RID: 1545
			public float[] nbrW;

			// Token: 0x0400060A RID: 1546
			public int[] revTri;

			// Token: 0x0400060B RID: 1547
			public Vector3[] revBary;

			// Token: 0x0400060C RID: 1548
			public int[] revBody;

			// Token: 0x0400060D RID: 1549
			public float[] revDist;

			// Token: 0x0400060E RID: 1550
			public int[] revFace;

			// Token: 0x0400060F RID: 1551
			public bool[] revHem;

			// Token: 0x04000610 RID: 1552
			public float[] revRest;

			// Token: 0x04000611 RID: 1553
			public float[] needLift;

			// Token: 0x04000612 RID: 1554
			public Vector3[] needDir;

			// Token: 0x04000613 RID: 1555
			public BoneWeight[] gbwArr;

			// Token: 0x04000614 RID: 1556
			public Matrix4x4[] gSkinM;

			// Token: 0x04000615 RID: 1557
			public Vector3[] workW;

			// Token: 0x04000616 RID: 1558
			public int[] riderIdx;

			// Token: 0x04000617 RID: 1559
			public int[] riderBodyV;

			// Token: 0x04000618 RID: 1560
			public Vector3[] riderOffB;

			// Token: 0x04000619 RID: 1561
			public bool[] isRider;

			// Token: 0x0400061A RID: 1562
			public Matrix4x4[] gBind;

			// Token: 0x0400061B RID: 1563
			public Matrix4x4[] gSkinInv;

			// Token: 0x0400061C RID: 1564
			public int boundN;

			// Token: 0x0400061D RID: 1565
			public bool usePose;

			// Token: 0x0400061E RID: 1566
			public bool active;
		}

		// Token: 0x02000018 RID: 24
		private class SquashRec
		{
			// Token: 0x0400061F RID: 1567
			public Renderer rend;

			// Token: 0x04000620 RID: 1568
			public MeshFilter mf;

			// Token: 0x04000621 RID: 1569
			public Mesh orig;

			// Token: 0x04000622 RID: 1570
			public Mesh clone;

			// Token: 0x04000623 RID: 1571
			public Vector3[] baseV;

			// Token: 0x04000624 RID: 1572
			public Vector3[] work;

			// Token: 0x04000625 RID: 1573
			public int[] domBone;

			// Token: 0x04000626 RID: 1574
			public Transform[] bones;

			// Token: 0x04000627 RID: 1575
			public Matrix4x4[] binds;

			// Token: 0x04000628 RID: 1576
			public Transform owner;

			// Token: 0x04000629 RID: 1577
			public bool active;
		}

		// Token: 0x02000019 RID: 25
		private sealed class MtState
		{
			// Token: 0x0400062A RID: 1578
			public Action<int, int> body2;

			// Token: 0x0400062B RID: 1579
			public Action<int, int, int> body3;

			// Token: 0x0400062C RID: 1580
			public int remaining;

			// Token: 0x0400062D RID: 1581
			public Exception err;

			// Token: 0x0400062E RID: 1582
			public ManualResetEvent done;
		}

		// Token: 0x0200001A RID: 26
		private sealed class WkGen
		{
			// Token: 0x0400062F RID: 1583
			public Semaphore go;

			// Token: 0x04000630 RID: 1584
			public bool quit;
		}
	}
}
