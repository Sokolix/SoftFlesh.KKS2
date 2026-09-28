using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SoftFlesh
{
	// Token: 0x02000005 RID: 5
	internal static class SoftFleshSubdivide
	{
		// Token: 0x0600015A RID: 346 RVA: 0x00051780 File Offset: 0x0004F980
		public static Mesh Build(Mesh src, Transform[] bones, float minAreaMm2, int levels, bool capHoles, float reachM, float maxWeightGap, bool wholeBody, out string info)
		{
			info = "";
			if (src == null || bones == null)
			{
				return null;
			}
			List<Vector3> verts = new List<Vector3>(src.vertices);
			List<Vector3> norms = new List<Vector3>(src.normals);
			List<Vector4> tans = new List<Vector4>(src.tangents);
			List<Vector2> uv0 = new List<Vector2>(src.uv);
			List<Vector2> uv = new List<Vector2>(src.uv2);
			List<Vector2> uv2 = new List<Vector2>(src.uv3);
			List<Vector2> uv3 = new List<Vector2>(src.uv4);
			List<Color32> cols = new List<Color32>(src.colors32);
			List<BoneWeight> bw = new List<BoneWeight>(src.boneWeights);
			int n0 = verts.Count;
			if (n0 == 0)
			{
				info = "empty mesh";
				return null;
			}
			bool hasN = norms.Count == n0;
			bool hasT = tans.Count == n0;
			bool hasU0 = uv0.Count == n0;
			bool hasU = uv.Count == n0;
			bool hasC = cols.Count == n0;
			bool hasW = bw.Count == n0;
			bool hasU2 = uv2.Count == n0;
			bool hasU3 = uv3.Count == n0;
			int sub = src.subMeshCount;
			List<int>[] tris = new List<int>[sub];
			for (int s = 0; s < sub; s++)
			{
				tris[s] = new List<int>(src.GetTriangles(s));
			}
			bool[] core;
			List<bool> genRegion = SoftFleshSubdivide.BuildRegion(bw, bones, n0, tris, sub, hasW, verts, reachM, out core);
			if (genRegion == null && !wholeBody)
			{
				info = "no genital region found";
				return null;
			}
			List<bool> region;
			if (wholeBody)
			{
				region = new List<bool>(n0);
				for (int i = 0; i < n0; i++)
				{
					region.Add(true);
				}
			}
			else
			{
				region = genRegion;
			}
			string budgetNote = "";
			int[] canon = new int[n0];
			Dictionary<long, int> byPos = new Dictionary<long, int>(n0);
			for (int j = 0; j < n0; j++)
			{
				long pk = SoftFleshSubdivide.PosKey(verts[j]);
				int c0;
				if (byPos.TryGetValue(pk, out c0))
				{
					canon[j] = c0;
				}
				else
				{
					byPos[pk] = j;
					canon[j] = j;
				}
			}
			List<SoftFleshSubdivide.NewVert> newVerts = new List<SoftFleshSubdivide.NewVert>();
			float minArea = minAreaMm2 * 1E-06f;
			float minEdge = Mathf.Sqrt(minArea) * 0.9f;
			int splitTotal = 0;
			int skippedW = 0;
			for (int lev = 0; lev < levels; lev++)
			{
				Dictionary<long, int> edgeMid = new Dictionary<long, int>();
				HashSet<long> marked = new HashSet<long>();
				for (int s2 = 0; s2 < sub; s2++)
				{
					List<int> T = tris[s2];
					int t = 0;
					while (t + 2 < T.Count)
					{
						int a = T[t];
						int b = T[t + 1];
						int c = T[t + 2];
						if ((region[a] || region[b] || region[c]) && SoftFleshSubdivide.Area(verts[a], verts[b], verts[c]) >= minArea)
						{
							if (SoftFleshSubdivide.Long(verts, a, b, minEdge) && SoftFleshSubdivide.WeightSafe(bw, hasW, a, b, maxWeightGap, core))
							{
								marked.Add(SoftFleshSubdivide.CKey(canon, a, b));
							}
							else if (SoftFleshSubdivide.Long(verts, a, b, minEdge))
							{
								skippedW++;
							}
							if (SoftFleshSubdivide.Long(verts, b, c, minEdge) && SoftFleshSubdivide.WeightSafe(bw, hasW, b, c, maxWeightGap, core))
							{
								marked.Add(SoftFleshSubdivide.CKey(canon, b, c));
							}
							else if (SoftFleshSubdivide.Long(verts, b, c, minEdge))
							{
								skippedW++;
							}
							if (SoftFleshSubdivide.Long(verts, c, a, minEdge) && SoftFleshSubdivide.WeightSafe(bw, hasW, c, a, maxWeightGap, core))
							{
								marked.Add(SoftFleshSubdivide.CKey(canon, c, a));
							}
							else if (SoftFleshSubdivide.Long(verts, c, a, minEdge))
							{
								skippedW++;
							}
						}
						t += 3;
					}
				}
				if (marked.Count == 0)
				{
					break;
				}
				if (verts.Count + marked.Count + marked.Count / 4 > 62000)
				{
					budgetNote = string.Concat(new string[]
					{
						"budget stop before level ",
						(lev + 1).ToString(),
						" (",
						marked.Count.ToString(),
						" edges marked), "
					});
					break;
				}
				for (int s3 = 0; s3 < sub; s3++)
				{
					List<int> T2 = tris[s3];
					List<int> outT = new List<int>(T2.Count * 2);
					int t2 = 0;
					while (t2 + 2 < T2.Count)
					{
						int a2 = T2[t2];
						int b2 = T2[t2 + 1];
						int c2 = T2[t2 + 2];
						int ab = (marked.Contains(SoftFleshSubdivide.CKey(canon, a2, b2)) ? SoftFleshSubdivide.Mid(a2, b2, edgeMid, verts, norms, tans, uv0, uv, uv2, uv3, cols, bw, newVerts, hasN, hasT, hasU0, hasU, hasU2, hasU3, hasC, hasW, region, genRegion) : (-1));
						int bc = (marked.Contains(SoftFleshSubdivide.CKey(canon, b2, c2)) ? SoftFleshSubdivide.Mid(b2, c2, edgeMid, verts, norms, tans, uv0, uv, uv2, uv3, cols, bw, newVerts, hasN, hasT, hasU0, hasU, hasU2, hasU3, hasC, hasW, region, genRegion) : (-1));
						int ca = (marked.Contains(SoftFleshSubdivide.CKey(canon, c2, a2)) ? SoftFleshSubdivide.Mid(c2, a2, edgeMid, verts, norms, tans, uv0, uv, uv2, uv3, cols, bw, newVerts, hasN, hasT, hasU0, hasU, hasU2, hasU3, hasC, hasW, region, genRegion) : (-1));
						int cntM = (((ab >= 0) && (bc >= 0) && (ca >= 0)) ? 1 : 0);
						if (cntM == 0)
						{
							outT.Add(a2);
							outT.Add(b2);
							outT.Add(c2);
						}
						else
						{
							splitTotal++;
							if (cntM == 3)
							{
								SoftFleshSubdivide.Tri(outT, a2, ab, ca);
								SoftFleshSubdivide.Tri(outT, ab, b2, bc);
								SoftFleshSubdivide.Tri(outT, ca, bc, c2);
								SoftFleshSubdivide.Tri(outT, ab, bc, ca);
							}
							else if (cntM == 2)
							{
								int g = 0;
								while (g < 2 && ca >= 0)
								{
									SoftFleshSubdivide.Rot(ref a2, ref b2, ref c2);
									SoftFleshSubdivide.Rot3(ref ab, ref bc, ref ca);
									g++;
								}
								if (ab < 0 || bc < 0)
								{
									outT.Add(a2);
									outT.Add(b2);
									outT.Add(c2);
								}
								else
								{
									SoftFleshSubdivide.Tri(outT, a2, ab, bc);
									SoftFleshSubdivide.Tri(outT, ab, b2, bc);
									SoftFleshSubdivide.Tri(outT, a2, bc, c2);
								}
							}
							else
							{
								int g2 = 0;
								while (g2 < 2 && ab < 0)
								{
									SoftFleshSubdivide.Rot(ref a2, ref b2, ref c2);
									SoftFleshSubdivide.Rot3(ref ab, ref bc, ref ca);
									g2++;
								}
								if (ab < 0)
								{
									outT.Add(a2);
									outT.Add(b2);
									outT.Add(c2);
								}
								else
								{
									SoftFleshSubdivide.Tri(outT, a2, ab, c2);
									SoftFleshSubdivide.Tri(outT, ab, b2, c2);
								}
							}
						}
						t2 += 3;
					}
					tris[s3] = outT;
				}
				if (verts.Count > canon.Length)
				{
					int[] nc = new int[verts.Count];
					Array.Copy(canon, nc, canon.Length);
					Dictionary<long, int> byPos2 = new Dictionary<long, int>();
					for (int k = canon.Length; k < verts.Count; k++)
					{
						long pk2 = SoftFleshSubdivide.PosKey(verts[k]);
						int c3;
						if (byPos2.TryGetValue(pk2, out c3))
						{
							nc[k] = c3;
						}
						else
						{
							byPos2[pk2] = k;
							nc[k] = k;
						}
					}
					canon = nc;
				}
				if (verts.Count > 60000)
				{
					break;
				}
			}
			int midCount = newVerts.Count;
			int capped = 0;
			int capTris = 0;
			if (capHoles && genRegion != null)
			{
				int[] canonC = new int[verts.Count];
				Dictionary<long, int> byPosC = new Dictionary<long, int>(verts.Count);
				for (int l = 0; l < verts.Count; l++)
				{
					long pk3 = SoftFleshSubdivide.PosKey(verts[l]);
					int c4;
					if (byPosC.TryGetValue(pk3, out c4))
					{
						canonC[l] = c4;
					}
					else
					{
						byPosC[pk3] = l;
						canonC[l] = l;
					}
				}
				Dictionary<long, int> useC = new Dictionary<long, int>();
				Dictionary<long, int[]> dirC = new Dictionary<long, int[]>();
				for (int sm = 0; sm < sub; sm++)
				{
					List<int> T3 = tris[sm];
					int t3 = 0;
					while (t3 + 2 < T3.Count)
					{
						for (int e = 0; e < 3; e++)
						{
							int ra = T3[t3 + e];
							int rb = T3[t3 + (e + 1) % 3];
							int ca2 = canonC[ra];
							int cb = canonC[rb];
							if (ca2 != cb)
							{
								long m = SoftFleshSubdivide.Key(ca2, cb);
								int cnt;
								useC.TryGetValue(m, out cnt);
								useC[m] = cnt + 1;
								if (cnt == 0)
								{
									dirC[m] = new int[] { ra, rb, sm };
								}
							}
						}
						t3 += 3;
					}
				}
				Dictionary<int, List<int>> link = new Dictionary<int, List<int>>();
				Dictionary<long, int[]> realOf = new Dictionary<long, int[]>();
				foreach (KeyValuePair<long, int> kv in useC)
				{
					if (kv.Value == 1)
					{
						int[] rp = dirC[kv.Key];
						if (genRegion[rp[0]] || genRegion[rp[1]])
						{
							int ca3 = canonC[rp[0]];
							int cb2 = canonC[rp[1]];
							if (!link.ContainsKey(ca3))
							{
								link[ca3] = new List<int>(2);
							}
							if (!link.ContainsKey(cb2))
							{
								link[cb2] = new List<int>(2);
							}
							link[ca3].Add(cb2);
							link[cb2].Add(ca3);
							realOf[SoftFleshSubdivide.Key(ca3, cb2)] = rp;
						}
					}
				}
				HashSet<long> unused = new HashSet<long>();
				using (Dictionary<long, int[]>.Enumerator enumerator2 = realOf.GetEnumerator())
				{
					while (enumerator2.MoveNext())
					{
						KeyValuePair<long, int[]> kv2 = enumerator2.Current;
						unused.Add(kv2.Key);
					}
					goto IL_0ECC;
				}
				IL_0A12:
				long seed = 0L;
				using (HashSet<long>.Enumerator enumerator3 = unused.GetEnumerator())
				{
					if (enumerator3.MoveNext())
					{
						long k2 = enumerator3.Current;
						seed = k2;
					}
				}
				int sa = (int)(seed >> 32);
				int sb = (int)seed;
				unused.Remove(seed);
				List<int> loop = new List<int>();
				loop.Add(sa);
				loop.Add(sb);
				int cur = sb;
				int guard = 0;
				List<int> nbs;
				while (guard < 8192 && link.TryGetValue(cur, out nbs))
				{
					int nxt = -1;
					for (int i2 = 0; i2 < nbs.Count; i2++)
					{
						if (unused.Contains(SoftFleshSubdivide.Key(cur, nbs[i2])))
						{
							nxt = nbs[i2];
							break;
						}
					}
					if (nxt < 0)
					{
						break;
					}
					unused.Remove(SoftFleshSubdivide.Key(cur, nxt));
					if (nxt == loop[0])
					{
						break;
					}
					loop.Add(nxt);
					cur = nxt;
					guard++;
				}
				if (loop.Count >= 3)
				{
					List<int> reals = new List<int>(loop.Count);
					for (int i3 = 0; i3 < loop.Count; i3++)
					{
						int nx = loop[(i3 + 1) % loop.Count];
						int[] rp2;
						if (realOf.TryGetValue(SoftFleshSubdivide.Key(loop[i3], nx), out rp2))
						{
							reals.Add(rp2[0]);
						}
						else
						{
							reals.Add(loop[i3]);
						}
					}
					int cIdx = verts.Count;
					Vector3 cPos = Vector3.zero;
					for (int i4 = 0; i4 < reals.Count; i4++)
					{
						cPos += verts[reals[i4]];
					}
					verts.Add(cPos / (float)reals.Count);
					if (hasN)
					{
						Vector3 cn = Vector3.zero;
						for (int i5 = 0; i5 < reals.Count; i5++)
						{
							cn += norms[reals[i5]];
						}
						norms.Add((cn.sqrMagnitude > 1E-12f) ? cn.normalized : Vector3.up);
					}
					if (hasT)
					{
						tans.Add(tans[reals[0]]);
					}
					if (hasU0)
					{
						Vector2 u = Vector2.zero;
						for (int i6 = 0; i6 < reals.Count; i6++)
						{
							u += uv0[reals[i6]];
						}
						uv0.Add(u / (float)reals.Count);
					}
					if (hasU)
					{
						Vector2 u2 = Vector2.zero;
						for (int i7 = 0; i7 < reals.Count; i7++)
						{
							u2 += uv[reals[i7]];
						}
						uv.Add(u2 / (float)reals.Count);
					}
					if (hasU2)
					{
						Vector2 u3 = Vector2.zero;
						for (int i8 = 0; i8 < reals.Count; i8++)
						{
							u3 += uv2[reals[i8]];
						}
						uv2.Add(u3 / (float)reals.Count);
					}
					if (hasU3)
					{
						Vector2 u4 = Vector2.zero;
						for (int i9 = 0; i9 < reals.Count; i9++)
						{
							u4 += uv3[reals[i9]];
						}
						uv3.Add(u4 / (float)reals.Count);
					}
					if (hasC)
					{
						cols.Add(cols[reals[0]]);
					}
					if (hasW)
					{
						BoneWeight acc = bw[reals[0]];
						for (int i10 = 1; i10 < reals.Count; i10++)
						{
							acc = SoftFleshSubdivide.BlendWeights(acc, bw[reals[i10]]);
						}
						bw.Add(acc);
					}
					newVerts.Add(new SoftFleshSubdivide.NewVert
					{
						a = reals[0],
						b = reals[(reals.Count > 1) ? 1 : 0]
					});
					region.Add(true);
					if (genRegion != region)
					{
						genRegion.Add(true);
					}
					for (int i11 = 0; i11 < loop.Count; i11++)
					{
						int ca4 = loop[i11];
						int cb3 = loop[(i11 + 1) % loop.Count];
						int[] rp3;
						if (realOf.TryGetValue(SoftFleshSubdivide.Key(ca4, cb3), out rp3))
						{
							List<int> TT = tris[rp3[2]];
							TT.Add(rp3[1]);
							TT.Add(rp3[0]);
							TT.Add(cIdx);
							capTris++;
						}
					}
					capped++;
				}
				IL_0ECC:
				if (unused.Count > 0)
				{
					goto IL_0A12;
				}
			}
			if (newVerts.Count == 0)
			{
				info = "nothing to refine (all faces already small)";
				return null;
			}
			Mesh dst = new Mesh();
			dst.name = src.name + "_subdiv";
			dst.SetVertices(verts);
			if (hasN)
			{
				dst.SetNormals(norms);
			}
			if (hasT)
			{
				dst.SetTangents(tans);
			}
			if (hasU0)
			{
				dst.SetUVs(0, uv0);
			}
			if (hasU)
			{
				dst.SetUVs(1, uv);
			}
			if (hasU2)
			{
				dst.SetUVs(2, uv2);
			}
			if (hasU3)
			{
				dst.SetUVs(3, uv3);
			}
			if (hasC)
			{
				dst.SetColors(cols);
			}
			if (hasW)
			{
				dst.boneWeights = bw.ToArray();
			}
			dst.bindposes = src.bindposes;
			dst.subMeshCount = sub;
			for (int s4 = 0; s4 < sub; s4++)
			{
				dst.SetTriangles(tris[s4], s4);
			}
			int nNew = verts.Count;
			int shapes = src.blendShapeCount;
			Vector3[] dV = new Vector3[n0];
			Vector3[] dN = new Vector3[n0];
			Vector3[] dT = new Vector3[n0];
			Vector3[] eV = new Vector3[nNew];
			Vector3[] eN = new Vector3[nNew];
			Vector3[] eT = new Vector3[nNew];
			for (int sh = 0; sh < shapes; sh++)
			{
				string nm = src.GetBlendShapeName(sh);
				int frames = src.GetBlendShapeFrameCount(sh);
				for (int f = 0; f < frames; f++)
				{
					float w = src.GetBlendShapeFrameWeight(sh, f);
					src.GetBlendShapeFrameVertices(sh, f, dV, dN, dT);
					Array.Copy(dV, eV, n0);
					Array.Copy(dN, eN, n0);
					Array.Copy(dT, eT, n0);
					for (int i12 = 0; i12 < newVerts.Count; i12++)
					{
						SoftFleshSubdivide.NewVert nv = newVerts[i12];
						int idx = n0 + i12;
						eV[idx] = (eV[nv.a] + eV[nv.b]) * 0.5f;
						eN[idx] = (eN[nv.a] + eN[nv.b]) * 0.5f;
						eT[idx] = (eT[nv.a] + eT[nv.b]) * 0.5f;
					}
					dst.AddBlendShapeFrame(nm, w, eV, eN, eT);
				}
			}
			dst.RecalculateBounds();
			string vSrc = SoftFleshSubdivide.Manifold(src.vertices, SoftFleshSubdivide.AllTris(src));
			string vDst = SoftFleshSubdivide.Manifold(verts.ToArray(), SoftFleshSubdivide.AllTrisList(tris, sub));
			StringBuilder chan = new StringBuilder();
			chan.Append(hasN ? "N" : "-").Append(hasT ? "T" : "-").Append(hasU0 ? "0" : "-")
				.Append(hasU ? "1" : "-")
				.Append(hasU2 ? "2" : "-")
				.Append(hasU3 ? "3" : "-")
				.Append(hasC ? "C" : "-")
				.Append(hasW ? "W" : "-");
			StringBuilder smInfo = new StringBuilder();
			for (int s5 = 0; s5 < sub; s5++)
			{
				smInfo.Append(src.GetTriangles(s5).Length / 3).Append("->").Append(tris[s5].Count / 3)
					.Append(' ');
			}
			string[] array = new string[27];
			array[0] = (wholeBody ? "whole body, " : "genital, ");
			array[1] = budgetNote;
			array[2] = "verts ";
			array[3] = n0.ToString();
			array[4] = " -> ";
			array[5] = nNew.ToString();
			array[6] = " (+";
			array[7] = newVerts.Count.ToString();
			array[8] = "), faces split ";
			array[9] = splitTotal.ToString();
			array[10] = ", long edges skipped on a weight boundary ";
			array[11] = skippedW.ToString();
			array[12] = ", holes capped ";
			array[13] = capped.ToString();
			array[14] = "/";
			array[15] = capTris.ToString();
			array[16] = "tri, shapes ";
			array[17] = shapes.ToString();
			array[18] = ", channels ";
			int num = 19;
			StringBuilder stringBuilder = chan;
			array[num] = ((stringBuilder != null) ? stringBuilder.ToString() : null);
			array[20] = ", submesh ";
			int num2 = 21;
			StringBuilder stringBuilder2 = smInfo;
			array[num2] = ((stringBuilder2 != null) ? stringBuilder2.ToString() : null);
			array[22] = "| before[";
			array[23] = vSrc;
			array[24] = "] after[";
			array[25] = vDst;
			array[26] = "]";
			info = string.Concat(array);
			int[] par = new int[newVerts.Count * 2];
			for (int k3 = 0; k3 < newVerts.Count; k3++)
			{
				if (k3 < midCount)
				{
					par[2 * k3] = newVerts[k3].a;
					par[2 * k3 + 1] = newVerts[k3].b;
				}
				else
				{
					par[2 * k3] = -1;
					par[2 * k3 + 1] = -1;
				}
			}
			SoftFleshSubdivide.ParentsByMesh[dst.GetInstanceID()] = par;
			return dst;
		}

		// Token: 0x0600015B RID: 347 RVA: 0x00052BE4 File Offset: 0x00050DE4
		private static int[] AllTris(Mesh m)
		{
			List<int> all = new List<int>();
			for (int s = 0; s < m.subMeshCount; s++)
			{
				all.AddRange(m.GetTriangles(s));
			}
			return all.ToArray();
		}

		// Token: 0x0600015C RID: 348 RVA: 0x00052C1C File Offset: 0x00050E1C
		private static int[] AllTrisList(List<int>[] tris, int sub)
		{
			List<int> all = new List<int>();
			for (int s = 0; s < sub; s++)
			{
				all.AddRange(tris[s]);
			}
			return all.ToArray();
		}

		// Token: 0x0600015D RID: 349 RVA: 0x00052C4C File Offset: 0x00050E4C
		private static string Manifold(Vector3[] pos, int[] tri)
		{
			if (pos == null || tri == null)
			{
				return "n/a";
			}
			int[] canon = new int[pos.Length];
			Dictionary<long, int> byPos = new Dictionary<long, int>(pos.Length);
			for (int i = 0; i < pos.Length; i++)
			{
				long pk = SoftFleshSubdivide.PosKey(pos[i]);
				int c0;
				if (byPos.TryGetValue(pk, out c0))
				{
					canon[i] = c0;
				}
				else
				{
					byPos[pk] = i;
					canon[i] = i;
				}
			}
			Dictionary<long, int> use = new Dictionary<long, int>(tri.Length);
			int degen = 0;
			int t = 0;
			while (t + 2 < tri.Length)
			{
				int a = canon[tri[t]];
				int b = canon[tri[t + 1]];
				int c = canon[tri[t + 2]];
				if (a == b || b == c || c == a)
				{
					degen++;
				}
				else
				{
					SoftFleshSubdivide.Bump(use, a, b);
					SoftFleshSubdivide.Bump(use, b, c);
					SoftFleshSubdivide.Bump(use, c, a);
				}
				t += 3;
			}
			int b2 = 0;
			int b3 = 0;
			foreach (KeyValuePair<long, int> kv in use)
			{
				if (kv.Value == 1)
				{
					b2++;
				}
				else if (kv.Value > 2)
				{
					b3++;
				}
			}
			return string.Concat(new string[]
			{
				"boundary=",
				b2.ToString(),
				" nonManifold=",
				b3.ToString(),
				" degenerate=",
				degen.ToString()
			});
		}

		// Token: 0x0600015E RID: 350 RVA: 0x00052DD4 File Offset: 0x00050FD4
		private static void Bump(Dictionary<long, int> d, int a, int b)
		{
			long i = SoftFleshSubdivide.Key(a, b);
			int v;
			d.TryGetValue(i, out v);
			d[i] = v + 1;
		}

		// Token: 0x0600015F RID: 351 RVA: 0x00052DFD File Offset: 0x00050FFD
		private static long Key(int a, int b)
		{
			if (a >= b)
			{
				return ((long)b << 32) | (long)a;
			}
			return ((long)a << 32) | (long)b;
		}

		// Token: 0x06000160 RID: 352 RVA: 0x00052E14 File Offset: 0x00051014
		private static long CKey(int[] canon, int a, int b)
		{
			int ca = ((a < canon.Length) ? canon[a] : a);
			int cb = ((b < canon.Length) ? canon[b] : b);
			return SoftFleshSubdivide.Key(ca, cb);
		}

		// Token: 0x06000161 RID: 353 RVA: 0x00052E44 File Offset: 0x00051044
		private static long PosKey(Vector3 v)
		{
			long x = (long)Mathf.Round(v.x * 10000f);
			long y = (long)Mathf.Round(v.y * 10000f);
			long z = (long)Mathf.Round(v.z * 10000f);
			return (x * 73856093L) ^ (y * 19349663L) ^ (z * 83492791L);
		}

		// Token: 0x06000162 RID: 354 RVA: 0x00052EA4 File Offset: 0x000510A4
		private static void Tri(List<int> t, int a, int b, int c)
		{
			if (a < 0 || b < 0 || c < 0)
			{
				return;
			}
			t.Add(a);
			t.Add(b);
			t.Add(c);
		}

		// Token: 0x06000163 RID: 355 RVA: 0x00052EC8 File Offset: 0x000510C8
		private static void Rot(ref int a, ref int b, ref int c)
		{
			int tmp = a;
			a = b;
			b = c;
			c = tmp;
		}

		// Token: 0x06000164 RID: 356 RVA: 0x00052EE4 File Offset: 0x000510E4
		private static void Rot3(ref int ab, ref int bc, ref int ca)
		{
			int tmp = ab;
			ab = bc;
			bc = ca;
			ca = tmp;
		}

		// Token: 0x06000165 RID: 357 RVA: 0x00052F00 File Offset: 0x00051100
		private static bool Long(List<Vector3> v, int a, int b, float minEdge)
		{
			return (v[a] - v[b]).magnitude > minEdge;
		}

		// Token: 0x06000166 RID: 358 RVA: 0x00052F2C File Offset: 0x0005112C
		private static float WeightGap(BoneWeight p, BoneWeight q)
		{
			int[] pi = new int[] { p.boneIndex0, p.boneIndex1, p.boneIndex2, p.boneIndex3 };
			float[] pw = new float[] { p.weight0, p.weight1, p.weight2, p.weight3 };
			int[] qi = new int[] { q.boneIndex0, q.boneIndex1, q.boneIndex2, q.boneIndex3 };
			float[] qw = new float[] { q.weight0, q.weight1, q.weight2, q.weight3 };
			float gap = 0f;
			for (int i = 0; i < 4; i++)
			{
				if (pw[i] > 0f)
				{
					float other = 0f;
					for (int j = 0; j < 4; j++)
					{
						if (qi[j] == pi[i])
						{
							other = qw[j];
							break;
						}
					}
					gap += Mathf.Abs(pw[i] - other);
				}
			}
			for (int k = 0; k < 4; k++)
			{
				if (qw[k] > 0f)
				{
					bool seen = false;
					for (int l = 0; l < 4; l++)
					{
						if (pi[l] == qi[k] && pw[l] > 0f)
						{
							seen = true;
							break;
						}
					}
					if (!seen)
					{
						gap += qw[k];
					}
				}
			}
			return gap;
		}

		// Token: 0x06000167 RID: 359 RVA: 0x000530A8 File Offset: 0x000512A8
		private static bool WeightSafe(List<BoneWeight> bw, bool hasW, int a, int b, float maxGap, bool[] core)
		{
			return !hasW || maxGap <= 0f || (core != null && a < core.Length && b < core.Length && core[a] && core[b]) || SoftFleshSubdivide.WeightGap(bw[a], bw[b]) <= maxGap;
		}

		// Token: 0x06000168 RID: 360 RVA: 0x00053100 File Offset: 0x00051300
		private static float Area(Vector3 a, Vector3 b, Vector3 c)
		{
			return 0.5f * Vector3.Cross(b - a, c - a).magnitude;
		}

		// Token: 0x06000169 RID: 361 RVA: 0x00053130 File Offset: 0x00051330
		private static List<bool> BuildRegion(List<BoneWeight> bw, Transform[] bones, int n, List<int>[] tris, int sub, bool hasW, List<Vector3> verts, float reachM, out bool[] core)
		{
			core = null;
			string[] low = new string[bones.Length];
			for (int i = 0; i < bones.Length; i++)
			{
				low[i] = ((bones[i] != null) ? bones[i].name.ToLower() : "");
			}
			bool[] regA = new bool[n];
			int seeds = 0;
			if (hasW)
			{
				for (int j = 0; j < n; j++)
				{
					BoneWeight w = bw[j];
					if (SoftFleshSubdivide.Gen(low, w.boneIndex0, w.weight0) || SoftFleshSubdivide.Gen(low, w.boneIndex1, w.weight1) || SoftFleshSubdivide.Gen(low, w.boneIndex2, w.weight2) || SoftFleshSubdivide.Gen(low, w.boneIndex3, w.weight3))
					{
						regA[j] = true;
						seeds++;
					}
				}
			}
			if (seeds < 8)
			{
				return null;
			}
			core = new bool[n];
			Array.Copy(regA, core, n);
			float avgE = 0f;
			int nE = 0;
			for (int s2 = 0; s2 < sub; s2++)
			{
				List<int> T2 = tris[s2];
				int t = 0;
				while (t + 2 < T2.Count)
				{
					avgE += (verts[T2[t + 1]] - verts[T2[t]]).magnitude;
					nE++;
					if (nE > 4000)
					{
						break;
					}
					t += 3;
				}
				if (nE > 4000)
				{
					break;
				}
			}
			avgE = ((nE > 0) ? (avgE / (float)nE) : 0.01f);
			int rings = Mathf.Clamp(Mathf.RoundToInt(reachM / Mathf.Max(avgE, 0.0001f)), 2, 12);
			for (int ring = 0; ring < rings; ring++)
			{
				List<int> add = new List<int>();
				for (int s3 = 0; s3 < sub; s3++)
				{
					List<int> T3 = tris[s3];
					int t2 = 0;
					while (t2 + 2 < T3.Count)
					{
						int a = T3[t2];
						int b = T3[t2 + 1];
						int c = T3[t2 + 2];
						if (regA[a] || regA[b] || regA[c])
						{
							if (!regA[a])
							{
								add.Add(a);
							}
							if (!regA[b])
							{
								add.Add(b);
							}
							if (!regA[c])
							{
								add.Add(c);
							}
						}
						t2 += 3;
					}
				}
				for (int k = 0; k < add.Count; k++)
				{
					regA[add[k]] = true;
				}
			}
			List<bool> reg = new List<bool>(regA.Length);
			for (int l = 0; l < regA.Length; l++)
			{
				reg.Add(regA[l]);
			}
			return reg;
		}

		// Token: 0x0600016A RID: 362 RVA: 0x000533D8 File Offset: 0x000515D8
		private static bool Gen(string[] low, int bi, float w)
		{
			if (w <= 0.02f || bi < 0 || bi >= low.Length)
			{
				return false;
			}
			string s = low[bi];
			return s.Contains("kokan") || s.Contains("vagina") || s.Contains("ana");
		}

		// Token: 0x0600016B RID: 363 RVA: 0x00053424 File Offset: 0x00051624
		private static int Mid(int a, int b, Dictionary<long, int> map, List<Vector3> verts, List<Vector3> norms, List<Vector4> tans, List<Vector2> uv0, List<Vector2> uv2, List<Vector2> uv3, List<Vector2> uv4, List<Color32> cols, List<BoneWeight> bw, List<SoftFleshSubdivide.NewVert> newVerts, bool hasN, bool hasT, bool hasU0, bool hasU2, bool hasU3, bool hasU4, bool hasC, bool hasW, List<bool> region, List<bool> region2)
		{
			long i = SoftFleshSubdivide.Key(a, b);
			int idx;
			if (map.TryGetValue(i, out idx))
			{
				return idx;
			}
			idx = verts.Count;
			verts.Add((verts[a] + verts[b]) * 0.5f);
			if (hasN)
			{
				Vector3 nn = norms[a] + norms[b];
				norms.Add((nn.sqrMagnitude > 1E-12f) ? nn.normalized : norms[a]);
			}
			if (hasT)
			{
				Vector4 t4 = (tans[a] + tans[b]) * 0.5f;
				Vector3 t5;
				t5 = new Vector3(t4.x, t4.y, t4.z);
				if (t5.sqrMagnitude > 1E-12f)
				{
					t5.Normalize();
				}
				tans.Add(new Vector4(t5.x, t5.y, t5.z, tans[a].w));
			}
			if (hasU0)
			{
				uv0.Add((uv0[a] + uv0[b]) * 0.5f);
			}
			if (hasU2)
			{
				uv2.Add((uv2[a] + uv2[b]) * 0.5f);
			}
			if (hasU3)
			{
				uv3.Add((uv3[a] + uv3[b]) * 0.5f);
			}
			if (hasU4)
			{
				uv4.Add((uv4[a] + uv4[b]) * 0.5f);
			}
			if (hasC)
			{
				cols.Add(Color32.Lerp(cols[a], cols[b], 0.5f));
			}
			if (hasW)
			{
				bw.Add(SoftFleshSubdivide.BlendWeights(bw[a], bw[b]));
			}
			newVerts.Add(new SoftFleshSubdivide.NewVert
			{
				a = a,
				b = b
			});
			if (region != null)
			{
				region.Add(true);
			}
			if (region2 != null && region2 != region)
			{
				region2.Add(region2[a] || region2[b]);
			}
			map[i] = idx;
			return idx;
		}

		// Token: 0x0600016C RID: 364 RVA: 0x0005367C File Offset: 0x0005187C
		public static BoneWeight BlendWeights(BoneWeight x, BoneWeight y)
		{
			int[] idx = new int[8];
			float[] wt = new float[8];
			int c = 0;
			SoftFleshSubdivide.AddW(idx, wt, ref c, x.boneIndex0, x.weight0 * 0.5f);
			SoftFleshSubdivide.AddW(idx, wt, ref c, x.boneIndex1, x.weight1 * 0.5f);
			SoftFleshSubdivide.AddW(idx, wt, ref c, x.boneIndex2, x.weight2 * 0.5f);
			SoftFleshSubdivide.AddW(idx, wt, ref c, x.boneIndex3, x.weight3 * 0.5f);
			SoftFleshSubdivide.AddW(idx, wt, ref c, y.boneIndex0, y.weight0 * 0.5f);
			SoftFleshSubdivide.AddW(idx, wt, ref c, y.boneIndex1, y.weight1 * 0.5f);
			SoftFleshSubdivide.AddW(idx, wt, ref c, y.boneIndex2, y.weight2 * 0.5f);
			SoftFleshSubdivide.AddW(idx, wt, ref c, y.boneIndex3, y.weight3 * 0.5f);
			for (int i = 0; i < c; i++)
			{
				for (int j = i + 1; j < c; j++)
				{
					if (wt[j] > wt[i])
					{
						float tw = wt[i];
						wt[i] = wt[j];
						wt[j] = tw;
						int ti = idx[i];
						idx[i] = idx[j];
						idx[j] = ti;
					}
				}
			}
			float sum = 0f;
			int keep = ((c < 4) ? c : 4);
			for (int k = 0; k < keep; k++)
			{
				sum += wt[k];
			}
			if (sum < 1E-06f)
			{
				sum = 1f;
			}
			BoneWeight r = default(BoneWeight);
			if (keep > 0)
			{
				r.boneIndex0 = idx[0];
				r.weight0 = wt[0] / sum;
			}
			if (keep > 1)
			{
				r.boneIndex1 = idx[1];
				r.weight1 = wt[1] / sum;
			}
			if (keep > 2)
			{
				r.boneIndex2 = idx[2];
				r.weight2 = wt[2] / sum;
			}
			if (keep > 3)
			{
				r.boneIndex3 = idx[3];
				r.weight3 = wt[3] / sum;
			}
			return r;
		}

		// Token: 0x0600016D RID: 365 RVA: 0x00053884 File Offset: 0x00051A84
		private static void AddW(int[] idx, float[] wt, ref int c, int bi, float w)
		{
			if (w <= 0f)
			{
				return;
			}
			for (int i = 0; i < c; i++)
			{
				if (idx[i] == bi)
				{
					wt[i] += w;
					return;
				}
			}
			if (c >= 8)
			{
				return;
			}
			idx[c] = bi;
			wt[c] = w;
			c++;
		}

		// Token: 0x04000302 RID: 770
		public static readonly Dictionary<int, int[]> ParentsByMesh = new Dictionary<int, int[]>();

		// Token: 0x0200002F RID: 47
		private struct NewVert
		{
			// Token: 0x040006B0 RID: 1712
			public int a;

			// Token: 0x040006B1 RID: 1713
			public int b;
		}
	}
}
