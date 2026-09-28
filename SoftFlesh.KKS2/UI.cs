using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SoftFlesh
{
	// Token: 0x02000006 RID: 6
	internal static class UI
	{
		// Token: 0x1700000B RID: 11
		// (get) Token: 0x0600016F RID: 367 RVA: 0x000538E0 File Offset: 0x00051AE0
		public static GUIStyle LinkBtn
		{
			get
			{
				if (UI._linkBtn == null)
				{
					UI._linkBtn = UI.Btn(UI.Hex(3094595), UI.Accent, (TextAnchor)4, false, true, 13, 0.4f);
					UI._linkBtn.hover.background = UI.RoundOutline(UI.Hex(3818832), new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.85f), 5);
					UI._linkBtn.hover.textColor = Color.white;
					UI._linkBtn.padding = new RectOffset(8, 8, 4, 4);
				}
				return UI._linkBtn;
			}
		}

		// Token: 0x06000170 RID: 368 RVA: 0x0005398D File Offset: 0x00051B8D
		private static Color Hex(int rgb)
		{
			return new Color((float)((rgb >> 16) & 255) / 255f, (float)((rgb >> 8) & 255) / 255f, (float)(rgb & 255) / 255f);
		}

		// Token: 0x06000171 RID: 369 RVA: 0x000539C4 File Offset: 0x00051BC4
		private static long Key(Color c, int rad, int kind)
		{
			return ((long)Mathf.RoundToInt(c.r * 255f) << 40)
				| ((long)Mathf.RoundToInt(c.g * 255f) << 32)
				| ((long)Mathf.RoundToInt(c.b * 255f) << 24)
				| ((long)Mathf.RoundToInt(c.a * 255f) << 16)
				| ((long)rad << 8)
				| (long)kind;
		}

		// Token: 0x06000172 RID: 370 RVA: 0x00053A30 File Offset: 0x00051C30
		private static Texture2D Make(int w, int h)
		{
			return new Texture2D(w, h, TextureFormat.RGBA32, false)
			{
				hideFlags = HideFlags.HideAndDontSave,
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp
			};
		}

		// Token: 0x06000173 RID: 371 RVA: 0x00053A54 File Offset: 0x00051C54
		public static Texture2D Flat(Color c)
		{
			long i = UI.Key(c, 0, 0);
			Texture2D t;
			if (UI._cache.TryGetValue(i, out t) && t != null)
			{
				return t;
			}
			t = UI.Make(1, 1);
			t.SetPixel(0, 0, c);
			t.Apply();
			UI._cache[i] = t;
			return t;
		}

		// Token: 0x06000174 RID: 372 RVA: 0x00053AA8 File Offset: 0x00051CA8
		public static Texture2D Round(Color c, int rad)
		{
			long i = UI.Key(c, rad, 1);
			Texture2D t;
			if (UI._cache.TryGetValue(i, out t) && t != null)
			{
				return t;
			}
			int j = rad * 2 + 2;
			t = UI.Make(j, j);
			Color[] px = new Color[j * j];
			for (int y = 0; y < j; y++)
			{
				for (int x = 0; x < j; x++)
				{
					float cx = (float)x + 0.5f;
					float cy = (float)y + 0.5f;
					float dx = ((cx < (float)rad) ? ((float)rad - cx) : ((cx > (float)(j - rad)) ? (cx - (float)(j - rad)) : 0f));
					float dy = ((cy < (float)rad) ? ((float)rad - cy) : ((cy > (float)(j - rad)) ? (cy - (float)(j - rad)) : 0f));
					float d = Mathf.Sqrt(dx * dx + dy * dy);
					float a = Mathf.Clamp01((float)rad + 0.5f - d);
					px[y * j + x] = new Color(c.r, c.g, c.b, c.a * a);
				}
			}
			t.SetPixels(px);
			t.Apply();
			UI._cache[i] = t;
			return t;
		}

		// Token: 0x06000175 RID: 373 RVA: 0x00053BE8 File Offset: 0x00051DE8
		public static Texture2D RoundOutline(Color fill, Color edge, int rad)
		{
			long i = UI.Key(fill, rad, 2 + Mathf.RoundToInt(edge.a * 15f));
			Texture2D t;
			if (UI._cache.TryGetValue(i, out t) && t != null)
			{
				return t;
			}
			int j = rad * 2 + 2;
			t = UI.Make(j, j);
			Color[] px = new Color[j * j];
			for (int y = 0; y < j; y++)
			{
				for (int x = 0; x < j; x++)
				{
					float cx = (float)x + 0.5f;
					float cy = (float)y + 0.5f;
					float dx = ((cx < (float)rad) ? ((float)rad - cx) : ((cx > (float)(j - rad)) ? (cx - (float)(j - rad)) : 0f));
					float dy = ((cy < (float)rad) ? ((float)rad - cy) : ((cy > (float)(j - rad)) ? (cy - (float)(j - rad)) : 0f));
					float d = Mathf.Sqrt(dx * dx + dy * dy);
					float outer = Mathf.Clamp01((float)rad + 0.5f - d);
					float inner = Mathf.Clamp01((float)rad - 0.5f - d);
					float ring = Mathf.Clamp01(outer - inner);
					float a = fill.a * inner + edge.a * ring;
					Color rgb = ((a > 0.0001f) ? new Color((fill.r * fill.a * inner + edge.r * edge.a * ring) / a, (fill.g * fill.a * inner + edge.g * edge.a * ring) / a, (fill.b * fill.a * inner + edge.b * edge.a * ring) / a) : Color.clear);
					px[y * j + x] = new Color(rgb.r, rgb.g, rgb.b, a * outer);
				}
			}
			t.SetPixels(px);
			t.Apply();
			UI._cache[i] = t;
			return t;
		}

		// Token: 0x06000176 RID: 374 RVA: 0x00053DF0 File Offset: 0x00051FF0
		public static Texture2D Circle(Color c, int size)
		{
			long i = UI.Key(c, size, 3);
			Texture2D t;
			if (UI._cache.TryGetValue(i, out t) && t != null)
			{
				return t;
			}
			t = UI.Make(size, size);
			Color[] px = new Color[size * size];
			float r = (float)size * 0.5f;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float dx = (float)x + 0.5f - r;
					float dy = (float)y + 0.5f - r;
					float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
					px[y * size + x] = new Color(c.r, c.g, c.b, c.a * a);
				}
			}
			t.SetPixels(px);
			t.Apply();
			UI._cache[i] = t;
			return t;
		}

		// Token: 0x06000177 RID: 375 RVA: 0x00053ED4 File Offset: 0x000520D4
		public static void Fill(Rect r, Color c)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			GUI.DrawTexture(r, UI.Flat(c));
		}

		// Token: 0x06000178 RID: 376 RVA: 0x00053EF0 File Offset: 0x000520F0
		public static void FillRound(Rect r, Color c, int rad)
		{
			if (Event.current.type != EventType.Repaint)
			{
				return;
			}
			if (UI._blit == null)
			{
				UI._blit = new GUIStyle();
			}
			UI._blit.normal.background = UI.Round(c, rad);
			UI._blit.border = new RectOffset(rad, rad, rad, rad);
			UI._blit.Draw(r, false, false, false, false);
		}

		// Token: 0x06000179 RID: 377 RVA: 0x00053F54 File Offset: 0x00052154
		public static void Rule()
		{
			Rect r = GUILayoutUtility.GetRect(1f, 1f, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
			UI.Fill(r, UI.Line);
		}

		// Token: 0x1700000C RID: 12
		// (get) Token: 0x0600017A RID: 378 RVA: 0x00053F8C File Offset: 0x0005218C
		public static GUIStyle Win
		{
			get
			{
				if (UI._win == null)
				{
					UI._win = new GUIStyle(GUI.skin.window);
					UI._win.normal.background = (UI._win.onNormal.background = UI.RoundOutline(UI.Bg, new Color(1f, 1f, 1f, 0.1f), 9));
					UI._win.focused.background = (UI._win.onFocused.background = UI._win.normal.background);
					UI._win.hover.background = (UI._win.onHover.background = UI._win.normal.background);
					UI._win.active.background = (UI._win.onActive.background = UI._win.normal.background);
					UI._win.border = new RectOffset(9, 9, 9, 9);
					UI._win.padding = new RectOffset(0, 0, 0, 0);
					UI._win.overflow = new RectOffset(0, 0, 0, 0);
					UI._win.contentOffset = Vector2.zero;
				}
				return UI._win;
			}
		}

		// Token: 0x1700000D RID: 13
		// (get) Token: 0x0600017B RID: 379 RVA: 0x000540E0 File Offset: 0x000522E0
		public static GUIStyle WinDialog
		{
			get
			{
				if (UI._winDlg == null)
				{
					UI._winDlg = new GUIStyle(GUI.skin.window);
					Texture2D bgTex = UI.RoundOutline(UI.BgHeader, new Color(1f, 1f, 1f, 0.22f), 9);
					UI._winDlg.normal.background = (UI._winDlg.onNormal.background = bgTex);
					UI._winDlg.focused.background = (UI._winDlg.onFocused.background = bgTex);
					UI._winDlg.hover.background = (UI._winDlg.onHover.background = bgTex);
					UI._winDlg.active.background = (UI._winDlg.onActive.background = bgTex);
					UI._winDlg.border = new RectOffset(9, 9, 9, 9);
					UI._winDlg.padding = new RectOffset(0, 0, 0, 0);
					UI._winDlg.overflow = new RectOffset(0, 0, 0, 0);
					UI._winDlg.contentOffset = Vector2.zero;
				}
				return UI._winDlg;
			}
		}

		// Token: 0x0600017C RID: 380 RVA: 0x0005420C File Offset: 0x0005240C
		private static GUIStyle Lab(int size, Color c, FontStyle fs, TextAnchor a)
		{
			GUIStyle s = new GUIStyle(GUI.skin.label)
			{
				fontSize = size,
				fontStyle = fs,
				alignment = a,
				wordWrap = false
			};
			GUIStyleState normal = s.normal;
			GUIStyleState hover = s.hover;
			GUIStyleState active = s.active;
			s.focused.textColor = c;
			active.textColor = c;
			hover.textColor = c;
			normal.textColor = c;
			s.padding = new RectOffset(0, 0, 0, 0);
			s.margin = new RectOffset(0, 0, 0, 0);
			return s;
		}

		// Token: 0x1700000E RID: 14
		// (get) Token: 0x0600017D RID: 381 RVA: 0x0005429A File Offset: 0x0005249A
		public static GUIStyle Lbl
		{
			get
			{
				if (UI._lbl == null)
				{
					UI._lbl = UI.Lab(13, UI.Text, 0, (TextAnchor)3);
				}
				return UI._lbl;
			}
		}

		// Token: 0x1700000F RID: 15
		// (get) Token: 0x0600017E RID: 382 RVA: 0x000542BB File Offset: 0x000524BB
		public static GUIStyle LblBright
		{
			get
			{
				if (UI._lblBright == null)
				{
					UI._lblBright = UI.Lab(13, UI.TextBright, 0, (TextAnchor)3);
				}
				return UI._lblBright;
			}
		}

		// Token: 0x17000010 RID: 16
		// (get) Token: 0x0600017F RID: 383 RVA: 0x000542DC File Offset: 0x000524DC
		public static GUIStyle LblDim
		{
			get
			{
				if (UI._lblDim == null)
				{
					UI._lblDim = UI.Lab(13, UI.TextDim, 0, (TextAnchor)3);
				}
				return UI._lblDim;
			}
		}

		// Token: 0x17000011 RID: 17
		// (get) Token: 0x06000180 RID: 384 RVA: 0x000542FD File Offset: 0x000524FD
		public static GUIStyle LblFaint
		{
			get
			{
				if (UI._lblFaint == null)
				{
					UI._lblFaint = UI.Lab(12, UI.TextFaint, 0, (TextAnchor)3);
				}
				return UI._lblFaint;
			}
		}

		// Token: 0x17000012 RID: 18
		// (get) Token: 0x06000181 RID: 385 RVA: 0x0005431E File Offset: 0x0005251E
		public static GUIStyle Mono
		{
			get
			{
				if (UI._mono == null)
				{
					UI._mono = UI.Lab(12, UI.TextDim, 0, (TextAnchor)3);
				}
				return UI._mono;
			}
		}

		// Token: 0x17000013 RID: 19
		// (get) Token: 0x06000182 RID: 386 RVA: 0x0005433F File Offset: 0x0005253F
		public static GUIStyle MonoDim
		{
			get
			{
				if (UI._monoDim == null)
				{
					UI._monoDim = UI.Lab(12, UI.TextFaint, 0, (TextAnchor)3);
				}
				return UI._monoDim;
			}
		}

		// Token: 0x17000014 RID: 20
		// (get) Token: 0x06000183 RID: 387 RVA: 0x00054360 File Offset: 0x00052560
		public static GUIStyle MonoRight
		{
			get
			{
				if (UI._monoRight == null)
				{
					UI._monoRight = UI.Lab(12, UI.TextFaint, 0, (TextAnchor)5);
				}
				return UI._monoRight;
			}
		}

		// Token: 0x17000015 RID: 21
		// (get) Token: 0x06000184 RID: 388 RVA: 0x00054381 File Offset: 0x00052581
		public static GUIStyle MonoAmber
		{
			get
			{
				if (UI._monoAmber == null)
				{
					UI._monoAmber = UI.Lab(13, UI.Amber, (FontStyle)1, (TextAnchor)5);
				}
				return UI._monoAmber;
			}
		}

		// Token: 0x17000016 RID: 22
		// (get) Token: 0x06000185 RID: 389 RVA: 0x000543A2 File Offset: 0x000525A2
		public static GUIStyle Title
		{
			get
			{
				if (UI._title == null)
				{
					UI._title = UI.Lab(15, UI.TextBright, (FontStyle)1, (TextAnchor)3);
				}
				return UI._title;
			}
		}

		// Token: 0x17000017 RID: 23
		// (get) Token: 0x06000186 RID: 390 RVA: 0x000543C3 File Offset: 0x000525C3
		public static GUIStyle WrapDim
		{
			get
			{
				if (UI._wrapDim == null)
				{
					UI._wrapDim = UI.Lab(12, UI.TextFaint, 0, 0);
					UI._wrapDim.wordWrap = true;
					UI._wrapDim.padding = new RectOffset(0, 0, 2, 3);
				}
				return UI._wrapDim;
			}
		}

		// Token: 0x17000018 RID: 24
		// (get) Token: 0x06000187 RID: 391 RVA: 0x00054402 File Offset: 0x00052602
		public static GUIStyle SecHead
		{
			get
			{
				if (UI._secHead == null)
				{
					UI._secHead = UI.Lab(13, UI.Accent, (FontStyle)1, (TextAnchor)3);
					UI._secHead.padding = new RectOffset(0, 0, 3, 3);
				}
				return UI._secHead;
			}
		}

		// Token: 0x17000019 RID: 25
		// (get) Token: 0x06000188 RID: 392 RVA: 0x00054438 File Offset: 0x00052638
		public static GUIStyle Box
		{
			get
			{
				if (UI._box == null)
				{
					UI._box = new GUIStyle(GUI.skin.box)
					{
						fontSize = 12,
						wordWrap = true,
						alignment = 0
					};
					UI._box.normal.background = UI.Round(UI.BgTrack, 4);
					UI._box.border = new RectOffset(4, 4, 4, 4);
					UI._box.normal.textColor = UI.TextDim;
					UI._box.padding = new RectOffset(8, 8, 6, 6);
					UI._box.margin = new RectOffset(0, 0, 3, 3);
				}
				return UI._box;
			}
		}

		// Token: 0x06000189 RID: 393 RVA: 0x000544E5 File Offset: 0x000526E5
		private static GUIStyle Btn(Color bg, Color fg, TextAnchor a, bool bold, bool outline, int size)
		{
			return UI.Btn(bg, fg, a, bold, outline, size, 0.12f);
		}

		// Token: 0x0600018A RID: 394 RVA: 0x000544FC File Offset: 0x000526FC
		private static GUIStyle Btn(Color bg, Color fg, TextAnchor a, bool bold, bool outline, int size, float edgeA)
		{
			GUIStyle s = new GUIStyle(GUI.skin.button)
			{
				fontSize = size,
				alignment = a,
				fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
				wordWrap = false
			};
			Color edge;
			edge = new Color(1f, 1f, 1f, edgeA);
			s.normal.background = (outline ? UI.RoundOutline(bg, edge, 5) : UI.Round(bg, 5));
			s.hover.background = (outline ? UI.RoundOutline(UI.BgHover, edge, 5) : UI.Round(new Color(bg.r + 0.06f, bg.g + 0.06f, bg.b + 0.06f, bg.a), 5));
			s.active.background = UI.Round(new Color(bg.r * 0.82f, bg.g * 0.82f, bg.b * 0.82f, bg.a), 5);
			s.focused.background = s.normal.background;
			GUIStyleState normal = s.normal;
			s.focused.textColor = fg;
			normal.textColor = fg;
			GUIStyleState hover = s.hover;
			s.active.textColor = fg;
			hover.textColor = fg;
			s.border = new RectOffset(5, 5, 5, 5);
			s.padding = new RectOffset(11, 11, 6, 6);
			s.margin = new RectOffset(3, 3, 3, 3);
			return s;
		}

		// Token: 0x1700001A RID: 26
		// (get) Token: 0x0600018B RID: 395 RVA: 0x00054680 File Offset: 0x00052880
		public static GUIStyle BtnDefault
		{
			get
			{
				if (UI._btn == null)
				{
					UI._btn = UI.Btn(UI.Hex(2830650), UI.Text, (TextAnchor)4, false, true, 13, 0.22f);
					UI._btn.hover.background = UI.RoundOutline(UI.Hex(3489095), new Color(1f, 1f, 1f, 0.34f), 5);
				}
				return UI._btn;
			}
		}

		// Token: 0x1700001B RID: 27
		// (get) Token: 0x0600018C RID: 396 RVA: 0x000546F4 File Offset: 0x000528F4
		public static GUIStyle BtnPrimary
		{
			get
			{
				if (UI._btnPri == null)
				{
					UI._btnPri = UI.Btn(UI.Accent, UI.Bg, (TextAnchor)4, true, false, 13);
				}
				return UI._btnPri;
			}
		}

		// Token: 0x1700001C RID: 28
		// (get) Token: 0x0600018D RID: 397 RVA: 0x0005471B File Offset: 0x0005291B
		public static GUIStyle BtnBake
		{
			get
			{
				if (UI._btnBake == null)
				{
					UI._btnBake = UI.Btn(UI.Amber, UI.Bg, (TextAnchor)4, true, false, 13);
				}
				return UI._btnBake;
			}
		}

		// Token: 0x1700001D RID: 29
		// (get) Token: 0x0600018E RID: 398 RVA: 0x00054744 File Offset: 0x00052944
		public static GUIStyle BtnLeft
		{
			get
			{
				if (UI._btnLeft == null)
				{
					UI._btnLeft = UI.Btn(UI.Hex(3357251), UI.TextBright, (TextAnchor)3, false, true, 13, 0.26f);
					UI._btnLeft.hover.background = UI.RoundOutline(UI.Hex(4015439), new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.55f), 5);
					UI._btnLeft.hover.textColor = UI.TextBright;
				}
				return UI._btnLeft;
			}
		}

		// Token: 0x1700001E RID: 30
		// (get) Token: 0x0600018F RID: 399 RVA: 0x000547DC File Offset: 0x000529DC
		public static GUIStyle BtnClose
		{
			get
			{
				if (UI._btnClose == null)
				{
					UI._btnClose = new GUIStyle(GUI.skin.button);
					UI._btnClose.fontSize = 16;
					UI._btnClose.fontStyle = (FontStyle)1;
					UI._btnClose.alignment = (TextAnchor)4;
					UI._btnClose.normal.textColor = UI.TextFaint;
					UI._btnClose.hover.textColor = Color.white;
					UI._btnClose.active.textColor = Color.white;
					UI._btnClose.normal.background = UI.RoundOutline(new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), 5);
					UI._btnClose.hover.background = UI.RoundOutline(new Color(0.62f, 0.24f, 0.2f, 1f), new Color(0.9f, 0.45f, 0.38f, 1f), 5);
					UI._btnClose.active.background = UI._btnClose.hover.background;
					UI._btnClose.border = new RectOffset(6, 6, 6, 6);
					UI._btnClose.padding = new RectOffset(0, 0, 0, 2);
					UI._btnClose.margin = new RectOffset(0, 0, 0, 0);
				}
				return UI._btnClose;
			}
		}

		// Token: 0x1700001F RID: 31
		// (get) Token: 0x06000190 RID: 400 RVA: 0x00054950 File Offset: 0x00052B50
		public static GUIStyle BtnGhost
		{
			get
			{
				if (UI._btnGhost == null)
				{
					UI._btnGhost = UI.Btn(new Color(0f, 0f, 0f, 0f), UI.TextDim, (TextAnchor)4, false, true, 13);
					UI._btnGhost.hover.textColor = UI.TextBright;
				}
				return UI._btnGhost;
			}
		}

		// Token: 0x17000020 RID: 32
		// (get) Token: 0x06000191 RID: 401 RVA: 0x000549AC File Offset: 0x00052BAC
		public static GUIStyle NumBox
		{
			get
			{
				if (UI._num == null)
				{
					UI._num = new GUIStyle(GUI.skin.textField)
					{
						fontSize = 12,
						alignment = (TextAnchor)5
					};
					UI._num.normal.background = (UI._num.hover.background = UI.RoundOutline(UI.BgTrack, new Color(1f, 1f, 1f, 0.1f), 4));
					UI._num.focused.background = UI.RoundOutline(UI.BgTrack, UI.Accent, 4);
					UI._num.normal.textColor = (UI._num.hover.textColor = UI.TextBright);
					UI._num.focused.textColor = UI.TextBright;
					UI._num.border = new RectOffset(4, 4, 4, 4);
					UI._num.padding = new RectOffset(6, 7, 4, 4);
					UI._num.margin = new RectOffset(3, 0, 3, 3);
				}
				return UI._num;
			}
		}

		// Token: 0x17000021 RID: 33
		// (get) Token: 0x06000192 RID: 402 RVA: 0x00054AC4 File Offset: 0x00052CC4
		public static GUIStyle TabOn
		{
			get
			{
				if (UI._tabOn == null)
				{
					UI._tabOn = new GUIStyle(GUI.skin.button)
					{
						fontSize = 13,
						fontStyle = (FontStyle)1,
						alignment = (TextAnchor)3,
						wordWrap = true
					};
					UI._tabOn.normal.background = (UI._tabOn.hover.background = (UI._tabOn.active.background = (UI._tabOn.focused.background = UI.Flat(UI.BgHeader))));
					UI._tabOn.normal.textColor = (UI._tabOn.hover.textColor = (UI._tabOn.active.textColor = (UI._tabOn.focused.textColor = UI.TextBright)));
					UI._tabOn.border = new RectOffset(0, 0, 0, 0);
					UI._tabOn.padding = new RectOffset(13, 36, 7, 7);
					UI._tabOn.margin = new RectOffset(0, 0, 0, 1);
				}
				return UI._tabOn;
			}
		}

		// Token: 0x17000022 RID: 34
		// (get) Token: 0x06000193 RID: 403 RVA: 0x00054BEC File Offset: 0x00052DEC
		public static GUIStyle TabOff
		{
			get
			{
				if (UI._tabOff == null)
				{
					UI._tabOff = new GUIStyle(UI.TabOn)
					{
						fontStyle = 0
					};
					UI._tabOff.normal.background = (UI._tabOff.focused.background = UI.Flat(UI.BgSide));
					UI._tabOff.hover.background = (UI._tabOff.active.background = UI.Flat(UI.Hex(1777187)));
					UI._tabOff.normal.textColor = (UI._tabOff.focused.textColor = UI.TextDim);
					UI._tabOff.hover.textColor = (UI._tabOff.active.textColor = UI.TextBright);
				}
				return UI._tabOff;
			}
		}

		// Token: 0x17000023 RID: 35
		// (get) Token: 0x06000194 RID: 404 RVA: 0x00054CC8 File Offset: 0x00052EC8
		public static GUIStyle PillOn
		{
			get
			{
				if (UI._pillOn == null)
				{
					UI._pillOn = UI.Btn(UI.Accent, UI.Bg, (TextAnchor)4, true, false, 12);
					UI._pillOn.padding = new RectOffset(9, 9, 5, 5);
					UI._pillOn.margin = new RectOffset(2, 2, 3, 3);
				}
				return UI._pillOn;
			}
		}

		// Token: 0x17000024 RID: 36
		// (get) Token: 0x06000195 RID: 405 RVA: 0x00054D24 File Offset: 0x00052F24
		public static GUIStyle PillOff
		{
			get
			{
				if (UI._pillOff == null)
				{
					UI._pillOff = UI.Btn(UI.Hex(1777187), UI.TextDim, (TextAnchor)4, false, true, 12);
					UI._pillOff.padding = new RectOffset(9, 9, 5, 5);
					UI._pillOff.margin = new RectOffset(2, 2, 3, 3);
				}
				return UI._pillOff;
			}
		}

		// Token: 0x17000025 RID: 37
		// (get) Token: 0x06000196 RID: 406 RVA: 0x00054D83 File Offset: 0x00052F83
		private static GUIStyle SliderBg
		{
			get
			{
				if (UI._sliderBg == null)
				{
					UI._sliderBg = new GUIStyle();
					UI._sliderBg.fixedHeight = 0f;
				}
				return UI._sliderBg;
			}
		}

		// Token: 0x17000026 RID: 38
		// (get) Token: 0x06000197 RID: 407 RVA: 0x00054DAC File Offset: 0x00052FAC
		private static GUIStyle Thumb
		{
			get
			{
				if (UI._thumb == null)
				{
					UI._thumb = new GUIStyle();
					UI._thumb.normal.background = (UI._thumb.hover.background = UI.Circle(UI.TextBright, 22));
					UI._thumb.active.background = UI.Circle(UI.Accent, 22);
					UI._thumb.fixedWidth = 11f;
					UI._thumb.fixedHeight = 11f;
					UI._thumb.border = new RectOffset(0, 0, 0, 0);
				}
				return UI._thumb;
			}
		}

		// Token: 0x17000027 RID: 39
		// (get) Token: 0x06000198 RID: 408 RVA: 0x00054E4C File Offset: 0x0005304C
		public static GUIStyle HelpChip
		{
			get
			{
				if (UI._helpChip == null)
				{
					UI._helpChip = new GUIStyle(GUI.skin.label);
					UI._helpChip.alignment = (TextAnchor)4;
					UI._helpChip.fontSize = 10;
					UI._helpChip.fontStyle = (FontStyle)1;
					UI._helpChip.normal.textColor = UI.TextFaint;
					UI._helpChip.hover.textColor = UI.Bg;
					UI._helpChip.normal.background = UI.RoundOutline(new Color(0f, 0f, 0f, 0f), new Color(1f, 1f, 1f, 0.22f), 7);
					UI._helpChip.hover.background = UI.RoundOutline(UI.Accent, UI.Accent, 7);
					UI._helpChip.border = new RectOffset(8, 8, 8, 8);
					UI._helpChip.padding = new RectOffset(0, 0, 0, 1);
					UI._helpChip.margin = new RectOffset(0, 0, 0, 0);
				}
				return UI._helpChip;
			}
		}

		// Token: 0x17000028 RID: 40
		// (get) Token: 0x06000199 RID: 409 RVA: 0x00054F68 File Offset: 0x00053168
		private static GUIStyle TipBox
		{
			get
			{
				if (UI._tipBox == null)
				{
					UI._tipBox = new GUIStyle(GUI.skin.label);
					UI._tipBox.wordWrap = true;
					UI._tipBox.fontSize = 11;
					UI._tipBox.normal.textColor = UI.TextBright;
					UI._tipBox.normal.background = UI.RoundOutline(UI.Hex(2172203), UI.Accent, 5);
					UI._tipBox.border = new RectOffset(6, 6, 6, 6);
					UI._tipBox.padding = new RectOffset(10, 10, 8, 8);
				}
				return UI._tipBox;
			}
		}

		// Token: 0x0600019A RID: 410 RVA: 0x00055010 File Offset: 0x00053210
		public static void HelpAt(Rect cell, string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			GUI.Label(cell, "?", UI.HelpChip);
			if (cell.Contains(Event.current.mousePosition))
			{
				UI._tip = text;
				UI._tipAt = GUIUtility.GUIToScreenPoint(new Vector2(cell.xMax + 10f, cell.y));
			}
		}

		// Token: 0x0600019B RID: 411 RVA: 0x00055074 File Offset: 0x00053274
		public static void DrawTooltip(float winW)
		{
			if (string.IsNullOrEmpty(UI._tip))
			{
				return;
			}
			Vector2 at = GUIUtility.ScreenToGUIPoint(UI._tipAt);
			float w = Mathf.Min(360f, Mathf.Max(200f, winW - 60f));
			float h = UI.TipBox.CalcHeight(new GUIContent(UI._tip), w);
			float x = Mathf.Min(at.x, winW - w - 8f);
			if (x < 8f)
			{
				x = 8f;
			}
			float y = at.y - h - 8f;
			if (y < 6f)
			{
				y = at.y + 22f;
			}
			GUI.Label(new Rect(x, y, w, h), UI._tip, UI.TipBox);
			UI._tip = null;
		}

		// Token: 0x0600019C RID: 412 RVA: 0x00055134 File Offset: 0x00053334
		public static float SliderStacked(string label, float val, float min, float max, string fmt, string help)
		{
			return UI.SliderStacked(label, val, min, max, fmt, help, null);
		}

		// Token: 0x0600019D RID: 413 RVA: 0x00055144 File Offset: 0x00053344
		public static float SliderStacked(string label, float val, float min, float max, string fmt, string help, string unit)
		{
			return UI.SliderStacked(label, label, val, min, max, fmt, help, unit);
		}

		// Token: 0x0600019E RID: 414 RVA: 0x00055158 File Offset: 0x00053358
		public static float SliderStacked(string key, string label, float val, float min, float max, string fmt, string help, string unit)
		{
			Rect line = GUILayoutUtility.GetRect(10f, 21f, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
			float unitW = (string.IsNullOrEmpty(unit) ? 0f : 22f);
			float labW = line.width - 58f - unitW - 22f;
			GUI.Label(new Rect(line.x, line.y + 2f, labW, 17f), label, UI.LblDim);
			UI.HelpAt(new Rect(line.x + labW + 3f, line.y + 3f, 15f, 15f), help);
			Rect box;
			box = new Rect(line.xMax - 58f - unitW, line.y, 58f, 20f);
			string ctl = "sfnum_" + key;
			GUI.SetNextControlName(ctl);
			bool focused = GUI.GetNameOfFocusedControl() == ctl;
			string shown = val.ToString(fmt);
			string cur;
			if (!focused || !UI._buf.TryGetValue(key, out cur))
			{
				cur = shown;
				UI._buf[key] = cur;
			}
			string ed = GUI.TextField(box, cur, 9, UI.NumBox);
			if (ed != cur)
			{
				UI._buf[key] = ed;
				float parsed;
				if (float.TryParse(ed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
				{
					val = Mathf.Clamp(parsed, min, max);
				}
			}
			if (unitW > 0f)
			{
				GUI.Label(new Rect(box.xMax + 3f, line.y + 2f, unitW, 17f), unit, UI.LblFaint);
			}
			Rect tr = GUILayoutUtility.GetRect(10f, 14f, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
			Rect bar;
			bar = new Rect(tr.x, tr.y + 5f, tr.width, 4f);
			UI.FillRound(bar, UI.BgTrack, 2);
			float t = ((max > min) ? Mathf.Clamp01((val - min) / (max - min)) : 0f);
			if (t > 0.001f)
			{
				UI.FillRound(new Rect(bar.x, bar.y, bar.width * t, 4f), UI.AccentDim, 2);
			}
			float v = GUI.HorizontalSlider(new Rect(tr.x, tr.y + 1f, tr.width, 13f), val, min, max, UI.SliderBg, UI.Thumb);
			GUILayout.Space(5f);
			return v;
		}

		// Token: 0x0600019F RID: 415 RVA: 0x000553F4 File Offset: 0x000535F4
		public static float Track(float val, float min, float max)
		{
			Rect r = GUILayoutUtility.GetRect(70f, 26f, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
			float mid = r.y + r.height * 0.5f;
			Rect line;
			line = new Rect(r.x + 5f, mid - 2f, r.width - 10f, 4f);
			UI.FillRound(line, UI.BgTrack, 2);
			float t = ((max > min) ? Mathf.Clamp01((val - min) / (max - min)) : 0f);
			if (t > 0.001f)
			{
				UI.FillRound(new Rect(line.x, line.y, line.width * t, 4f), UI.AccentDim, 2);
			}
			return GUI.HorizontalSlider(new Rect(r.x, mid - 8f, r.width, 16f), val, min, max, UI.SliderBg, UI.Thumb);
		}

		// Token: 0x060001A0 RID: 416 RVA: 0x000554EC File Offset: 0x000536EC
		public static float SliderRow(string key, string label, float val, float min, float max, string fmt, float labelW, string suffix)
		{
			return UI.SliderRow(key, label, val, min, max, fmt, labelW, suffix, null);
		}

		// Token: 0x060001A1 RID: 417 RVA: 0x0005550C File Offset: 0x0005370C
		public static float SliderRow(string key, string label, float val, float min, float max, string fmt, float labelW, string suffix, string help)
		{
			GUILayout.BeginHorizontal(new GUILayoutOption[] { GUILayout.Height(26f) });
			GUILayout.Label(label, UI.LblDim, new GUILayoutOption[]
			{
				GUILayout.Width(labelW - 20f),
				GUILayout.Height(26f)
			});
			Rect hc = GUILayoutUtility.GetRect(16f, 26f, new GUILayoutOption[] { GUILayout.Width(16f) });
			UI.HelpAt(new Rect(hc.x, hc.y + 5.5f, 15f, 15f), help);
			GUILayout.Space(4f);
			float v = UI.Track(val, min, max);
			string ctl = "sfnum_" + key;
			GUI.SetNextControlName(ctl);
			bool focused = GUI.GetNameOfFocusedControl() == ctl;
			string shown = v.ToString(fmt);
			string cur;
			if (!focused || !UI._buf.TryGetValue(key, out cur))
			{
				cur = shown;
				UI._buf[key] = cur;
			}
			string ed = GUILayout.TextField(cur, 9, UI.NumBox, new GUILayoutOption[]
			{
				GUILayout.Width(60f),
				GUILayout.Height(21f)
			});
			if (ed != cur)
			{
				UI._buf[key] = ed;
				float parsed;
				if (float.TryParse(ed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
				{
					v = Mathf.Clamp(parsed, min, max);
				}
			}
			GUILayout.Label(string.IsNullOrEmpty(suffix) ? "" : suffix, UI.LblFaint, new GUILayoutOption[] { GUILayout.Width(26f) });
			GUILayout.EndHorizontal();
			return v;
		}

		// Token: 0x060001A2 RID: 418 RVA: 0x000556AC File Offset: 0x000538AC
		public static bool Switch(bool on, string label)
		{
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Space(2f);
			Rect r = GUILayoutUtility.GetRect(30f, 22f, new GUILayoutOption[] { GUILayout.Width(30f) });
			Rect pill;
			pill = new Rect(r.x, r.y + 4f, 27f, 14f);
			UI.FillRound(pill, on ? new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.8f) : UI.BgButton, 7);
			if (Event.current.type == EventType.Repaint)
			{
				GUI.DrawTexture(new Rect(pill.x + (on ? 14f : 2f), pill.y + 2f, 10f, 10f), UI.Circle(UI.TextBright, 20));
			}
			bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);
			GUILayout.Space(4f);
			GUILayout.Label(label, UI.WrapDim, Array.Empty<GUILayoutOption>());
			GUILayout.EndHorizontal();
			GUILayout.Space(2f);
			if (!hit)
			{
				return on;
			}
			return !on;
		}

		// Token: 0x04000303 RID: 771
		public static readonly Color Bg = UI.Hex(1382171);

		// Token: 0x04000304 RID: 772
		public static readonly Color BgHeader = UI.Hex(1974824);

		// Token: 0x04000305 RID: 773
		public static readonly Color BgSide = UI.Hex(1053206);

		// Token: 0x04000306 RID: 774
		public static readonly Color BgTrack = UI.Hex(987412);

		// Token: 0x04000307 RID: 775
		public static readonly Color BgButton = UI.Hex(2303789);

		// Token: 0x04000308 RID: 776
		public static readonly Color BgHover = UI.Hex(2896184);

		// Token: 0x04000309 RID: 777
		public static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);

		// Token: 0x0400030A RID: 778
		public static readonly Color Accent = UI.Hex(9426624);

		// Token: 0x0400030B RID: 779
		public static readonly Color AccentDim = new Color(0.56f, 0.84f, 0.75f, 0.55f);

		// Token: 0x0400030C RID: 780
		public static readonly Color Amber = UI.Hex(14068594);

		// Token: 0x0400030D RID: 781
		public static readonly Color Text = UI.Hex(14080478);

		// Token: 0x0400030E RID: 782
		public static readonly Color TextBright = UI.Hex(15133166);

		// Token: 0x0400030F RID: 783
		public static readonly Color TextDim = new Color(0.84f, 0.85f, 0.87f, 0.72f);

		// Token: 0x04000310 RID: 784
		public static readonly Color TextFaint = new Color(0.84f, 0.85f, 0.87f, 0.42f);

		// Token: 0x04000311 RID: 785
		public static readonly Color Dot = UI.Hex(8378024);

		// Token: 0x04000312 RID: 786
		public static readonly Color[] Phase = new Color[]
		{
			UI.Hex(9426624),
			UI.Hex(5148576),
			UI.Hex(8370390),
			UI.Hex(14068594),
			UI.Hex(13209487),
			UI.Hex(10325974),
			UI.Hex(7520936),
			UI.Hex(14061496),
			UI.Hex(14078351),
			UI.Hex(9414870),
			UI.Hex(5922920)
		};

		// Token: 0x04000313 RID: 787
		private static GUIStyle _linkBtn;

		// Token: 0x04000314 RID: 788
		private static readonly Dictionary<long, Texture2D> _cache = new Dictionary<long, Texture2D>();

		// Token: 0x04000315 RID: 789
		private static GUIStyle _blit;

		// Token: 0x04000316 RID: 790
		public const int FS_BODY = 13;

		// Token: 0x04000317 RID: 791
		public const int FS_HEAD = 13;

		// Token: 0x04000318 RID: 792
		public const int FS_SMALL = 12;

		// Token: 0x04000319 RID: 793
		public const int FS_TITLE = 15;

		// Token: 0x0400031A RID: 794
		private const int R_BTN = 5;

		// Token: 0x0400031B RID: 795
		private const int R_BOX = 4;

		// Token: 0x0400031C RID: 796
		private const int R_WIN = 9;

		// Token: 0x0400031D RID: 797
		private static GUIStyle _lbl;

		// Token: 0x0400031E RID: 798
		private static GUIStyle _lblBright;

		// Token: 0x0400031F RID: 799
		private static GUIStyle _lblDim;

		// Token: 0x04000320 RID: 800
		private static GUIStyle _lblFaint;

		// Token: 0x04000321 RID: 801
		private static GUIStyle _mono;

		// Token: 0x04000322 RID: 802
		private static GUIStyle _monoAmber;

		// Token: 0x04000323 RID: 803
		private static GUIStyle _monoDim;

		// Token: 0x04000324 RID: 804
		private static GUIStyle _wrapDim;

		// Token: 0x04000325 RID: 805
		private static GUIStyle _secHead;

		// Token: 0x04000326 RID: 806
		private static GUIStyle _title;

		// Token: 0x04000327 RID: 807
		private static GUIStyle _tabOn;

		// Token: 0x04000328 RID: 808
		private static GUIStyle _tabOff;

		// Token: 0x04000329 RID: 809
		private static GUIStyle _btn;

		// Token: 0x0400032A RID: 810
		private static GUIStyle _btnPri;

		// Token: 0x0400032B RID: 811
		private static GUIStyle _btnBake;

		// Token: 0x0400032C RID: 812
		private static GUIStyle _btnGhost;

		// Token: 0x0400032D RID: 813
		private static GUIStyle _btnLeft;

		// Token: 0x0400032E RID: 814
		private static GUIStyle _num;

		// Token: 0x0400032F RID: 815
		private static GUIStyle _pillOn;

		// Token: 0x04000330 RID: 816
		private static GUIStyle _pillOff;

		// Token: 0x04000331 RID: 817
		private static GUIStyle _win;

		// Token: 0x04000332 RID: 818
		private static GUIStyle _sliderBg;

		// Token: 0x04000333 RID: 819
		private static GUIStyle _thumb;

		// Token: 0x04000334 RID: 820
		private static GUIStyle _box;

		// Token: 0x04000335 RID: 821
		private static GUIStyle _winDlg;

		// Token: 0x04000336 RID: 822
		private static GUIStyle _monoRight;

		// Token: 0x04000337 RID: 823
		private static GUIStyle _btnClose;

		// Token: 0x04000338 RID: 824
		public const float ROW_H = 26f;

		// Token: 0x04000339 RID: 825
		private static string _tip;

		// Token: 0x0400033A RID: 826
		private static Vector2 _tipAt;

		// Token: 0x0400033B RID: 827
		private static float _tipW;

		// Token: 0x0400033C RID: 828
		private static GUIStyle _helpChip;

		// Token: 0x0400033D RID: 829
		private static GUIStyle _tipBox;

		// Token: 0x0400033E RID: 830
		private static readonly Dictionary<string, string> _buf = new Dictionary<string, string>();
	}
}
