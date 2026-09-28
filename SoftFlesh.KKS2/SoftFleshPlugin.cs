using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI.Studio;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using Studio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AIChara;

namespace SoftFlesh
{
	// Token: 0x02000003 RID: 3
	[BepInDependency("marco.kkapi")]
	[BepInPlugin("com.kkstudio.softflesh", "SoftFlesh", "1.1.0")]
	[BepInProcess("StudioNEOV2")]
	public class SoftFleshPlugin : MonoBehaviour
	{
		// Token: 0x0600000A RID: 10 RVA: 0x00002CB9 File Offset: 0x00000EB9
		internal static void Log(string s)
		{
			if (SoftFleshPlugin.SLog != null)
			{
				SoftFleshPlugin.SLog.LogInfo(s);
			}
		}

		// Token: 0x17000001 RID: 1
		// (get) Token: 0x0600000B RID: 11 RVA: 0x00002CD0 File Offset: 0x00000ED0
		internal static SoftFleshController LiveController
		{
			get
			{
				SoftFleshPlugin p = SoftFleshPlugin._live;
				if (!(p != null))
				{
					return null;
				}
				return p._ctrl;
			}
		}

		// Token: 0x17000002 RID: 2
		// (get) Token: 0x0600000C RID: 12 RVA: 0x00002CF4 File Offset: 0x00000EF4
		private bool IsCurrentGen
		{
			get
			{
				object g = AppDomain.CurrentDomain.GetData("SoftFlesh.generation");
				return g is int && (int)g == this._gen;
			}
		}

		// Token: 0x0600000D RID: 13 RVA: 0x00002D2C File Offset: 0x00000F2C
		private void Awake()
		{
			SoftFleshPlugin.SLog = base.Logger;
			SFStallSpy.Apply();
			SoftFleshPlugin.CfgHotkey = base.Config.Bind<KeyboardShortcut>("General", "Toggle UI", new KeyboardShortcut(103, new KeyCode[] { 308 }), null);
			SoftFleshPlugin.CfgTouchRadius = base.Config.Bind<float>("Physics", "Touch Radius", 0.01f, new ConfigDescription("Finger capsule RADIUS (m) — match the visual finger, ~0.006–0.009. The old 0.02 slider MINIMUM forced a finger 4x too fat", new AcceptableValueRange<float>(0.004f, 0.05f), Array.Empty<object>()));
			SoftFleshPlugin.CfgMaxDepth = base.Config.Bind<float>("Physics", "Max Depth", 0.014f, new ConfigDescription("Skin stretch limit (m) — depth follows how deep the finger sinks; this only caps it", new AcceptableValueRange<float>(0.005f, 0.06f), Array.Empty<object>()));
			SoftFleshPlugin.CfgKnuckle = base.Config.Bind<float>("Physics", "Knuckle Depth Fraction", 0.5f, new ConfigDescription("How deep the BASE of a finger may press, as a fraction of Max Depth (never below one radius). Fingertips always get the full Max Depth. Below 1 keeps a hand resting inside the hip/waist (normal KK poses) from carving a pit at the wrist; 1 treats the whole finger like a fingertip", new AcceptableValueRange<float>(0.15f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgStiffness = base.Config.Bind<float>("Physics", "Stiffness", 670f, new ConfigDescription("Spring firmness — higher = snappier, less wobble", new AcceptableValueRange<float>(100f, 3000f), Array.Empty<object>()));
			SoftFleshPlugin.CfgDamping = base.Config.Bind<float>("Physics", "Damping", 18f, new ConfigDescription("Lower = wobbles / jiggles longer", new AcceptableValueRange<float>(2f, 60f), Array.Empty<object>()));
			SoftFleshPlugin.CfgWaveSpeed = base.Config.Bind<float>("Soft Body", "Wave Speed", 0.93f, new ConfigDescription("How fast a slap's ripple travels across the flesh (m/s). Mesh-density independent; 0 = no propagation", new AcceptableValueRange<float>(0f, 2f), Array.Empty<object>()));
			SoftFleshPlugin.CfgBulge = base.Config.Bind<float>("Soft Body", "Bulge", 0.3f, new ConfigDescription("Squeezed flesh: fraction of the finger's ACTUAL pressed depth that bulges out around it (0 = none)", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSmooth = base.Config.Bind<float>("Soft Body", "Smooth", 0.5f, new ConfigDescription("Taubin smoothing iterations on the dent SHAPE (above 1 = more passes, more CPU) — rounds 格狀 edges into curves but preserves finger-gap flesh detail", new AcceptableValueRange<float>(0f, 2f), Array.Empty<object>()));
			SoftFleshPlugin.CfgBend = base.Config.Bind<float>("Soft Body", "Skin Bending", 4f, new ConfigDescription("Bending stiffness of the skin around a dent, as smoothing passes on the tension field. 0 = a pure membrane (a steep funnel right at the finger); more = the rim rounds into a bowl and the neighbouring faces follow the print down. Render side only", new AcceptableValueRange<float>(0f, 12f), Array.Empty<object>()));
			SoftFleshPlugin.CfgTension = base.Config.Bind<float>("Soft Body", "Skin Tension mm", 20f, new ConfigDescription("How far the skin around a contact print is pulled along with it: the decay length of a membrane on soft tissue. Small = loose skin, a local dent with a visible edge; large = tight skin, a broad gentle dent. Render side only - the spring and the waves are untouched. 0 = off", new AcceptableValueRange<float>(0f, 60f), Array.Empty<object>()));
			SoftFleshPlugin.CfgShadow = base.Config.Bind<float>("Soft Body", "Shadow", 1f, new ConfigDescription("Dent shading darkness — tilts normals to deepen the dent's shadow under the toon ramp (cosmetic; does not change the shape)", new AcceptableValueRange<float>(1f, 12f), Array.Empty<object>()));
			SoftFleshPlugin.CfgNipFirm = base.Config.Bind<float>("Soft Body", "Nipple Firm", 0f, new ConfigDescription("Nipple/areola firmness — 1 = a grab can't squash it at all; the soft flesh around it still dents", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSlap = base.Config.Bind<float>("Physics", "Slap Impulse", 3f, new ConfigDescription("Fast strike → velocity kick → splash + travelling wave (0 = off)", new AcceptableValueRange<float>(0f, 3f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSoften = base.Config.Bind<float>("Physics", "Slap Soften", 1f, new ConfigDescription("A strike briefly melts the flesh around it (~12cm, ~1.2s) so the ripple rings THERE — the rest of the body stays firm", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgItemRad = base.Config.Bind<float>("Physics", "Item Radius x", 1f, new ConfigDescription("Scales every item collider's capsule radius — trim until the stretch matches the visible item", new AcceptableValueRange<float>(0.2f, 3f), Array.Empty<object>()));
			SoftFleshPlugin.CfgBelly = base.Config.Bind<float>("Physics", "Belly Bulge", 0.62f, new ConfigDescription("Outer skin above a deeply-inserted item rises instead of clipping (0 = off)", new AcceptableValueRange<float>(0f, 1.5f), Array.Empty<object>()));
			SoftFleshPlugin.CfgBulgeWide = base.Config.Bind<bool>("Physics", "Wide Bulge", true, "Belly bulge shape: ON = cylindrical mound all around the item; OFF = narrow depth-only ridge on the covering side");
			SoftFleshPlugin.CfgOpenD = base.Config.Bind<float>("Opening", "Open Depth", 0f, new ConfigDescription("Manual drive of the BAKED opening: depth along the canal (0 = off). Bake first", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgOpenW = base.Config.Bind<float>("Opening", "Open Width", 0f, new ConfigDescription("Manual drive of the BAKED opening: stretch radius (0 = off)", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSmoothShade = base.Config.Bind<float>("Physics", "Smooth (shading)", 0.5f, new ConfigDescription("Smoothing of the SHADING normals only. Smooth handles the shape. Split so a shading artifact can be told apart from a geometric one", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgShadeTau = base.Config.Bind<float>("Physics", "Shadow smooth ms", 90f, new ConfigDescription("The dent's shading follows the moving dent through a low-pass of this many milliseconds, so it does not flicker while a hand strokes the skin (the shape itself is not delayed). 0 = off", new AcceptableValueRange<float>(0f, 250f), Array.Empty<object>()));
			SoftFleshPlugin.CfgClitHold = base.Config.Bind<float>("Opening", "Clit Hold", 0f, new ConfigDescription("Hold the anterior commissure still while the lateral and posterior petals spread. This is what the eversion turned out to be: at 0 the front petals swing outward and take the hood with them. 1 matches the anatomy, where the front of the ring is the apex the opening hinges about", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgTrack = base.Config.Bind<bool>("Opening", "Track items", true, "Insert-mode items drive Open Width/Depth automatically from their measured penetration");
			SoftFleshPlugin.CfgGapeHold = base.Config.Bind<float>("Opening", "Gape Hold", 0f, new ConfigDescription("How long the opening lingers after the item leaves. 0 = closes in half a second, 1 = ~30s", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSuction = base.Config.Bind<float>("Opening", "Suction", 1f, new ConfigDescription("In/out strokes drag the opening flesh a few mm along the canal (through the smoothed bake channel)", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgItemGuide = base.Config.Bind<float>("Opening", "Item Guide", 0.6f, new ConfigDescription("Elastic servo: the inserted item is gently pulled onto the opening (the item follows the hole, not the other way round). 0 = off", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgFollow = base.Config.Bind<float>("Opening", "Track Follow", 0f, new ConfigDescription("The baked opening drifts toward the item's lateral position at the entrance - the follow the petal collision used to provide, through the smooth mesh channel", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgPetalFirm = base.Config.Bind<float>("Opening", "Petal Firm", 0.7f, new ConfigDescription("Petal chains stiffen toward rigid while the opening is driven, so they hug the item instead of bouncing; they relax back as it closes", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgTrackWx = base.Config.Bind<float>("Opening", "Track Width x", 1f, new ConfigDescription("Scales the tracked opening width against the item's fitted radius - trim until the opening matches the visible item", new AcceptableValueRange<float>(0.3f, 2f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSpread = base.Config.Bind<float>("Opening", "Spread", 40f, new ConfigDescription("Millimetres the flesh AROUND the opening slides aside at full width. A rigid slide on helper bones, so it works in any pose and cannot wrinkle or dish (Poison-Body only)", new AcceptableValueRange<float>(0f, 40f), Array.Empty<object>()));
			SoftFleshPlugin.CfgAxisEntry = base.Config.Bind<string>("Opening", "Canal entry (bind)", "0.000,0.923,-0.028", "Calibrated canal entry point in bind space. Overwritten when a guide ball capture is baked");
			SoftFleshPlugin.CfgAxisDir = base.Config.Bind<string>("Opening", "Canal dir (bind)", "0.02,0.94,0.34", "Calibrated canal direction in bind space");
			SoftFleshPlugin.CfgOpenMax = base.Config.Bind<float>("Opening", "Open Max %", 100f, new ConfigDescription("Blendshape weight at slider max. Above 100 EXTRAPOLATES beyond the authored sculpt for a larger opening", new AcceptableValueRange<float>(100f, 200f), Array.Empty<object>()));
			SoftFleshPlugin.CfgItemStretch = base.Config.Bind<float>("Opening", "Item Stretch", 0f, new ConfigDescription("Master gain of the item radial stretch field. 0 = off (test the bone rig alone); blend with bones later", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgItemSquash = base.Config.Bind<float>("Items", "Item Squash", 0f, new ConfigDescription("The flesh pushes back on the ITEM: its rendered mesh narrows at the entrance ring by (item width - what the flesh delivers) x this gain. Colliders and tracking see the original shape, so nothing oscillates. 0 = off", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgWaveShield = base.Config.Bind<float>("Physics", "Crotch Wave Shield", 1f, new ConfigDescription("Waves and slaps fade out over the genital region, so a buttock slap cannot deform the driven opening. 1 = fully excluded (like arms), 0 = old behaviour. Contact denting there still works", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSpanGuard = base.Config.Bind<bool>("Physics", "Cloth Penetration Guard", true, "Lifts a transfer-path garment where a wave would push flesh through it. Measured against how the garment was DRAWN, so an authored intersection is left alone");
			SoftFleshPlugin.CfgSecondSkin = base.Config.Bind<bool>("Physics", "Second Skin", true, "A garment that hugs the body within 2.5mm is rebuilt on the BODY's own mesh, so it cannot desync or be poked through. Turn OFF to send every garment down the transfer path instead - the A/B that tells you which path a problem belongs to");
			SoftFleshPlugin.CfgClothFollow = base.Config.Bind<bool>("Physics", "Clothes Follow Flesh", true, "Tight-fitting clothing rides the body's dents, waves and item pushback. Bound once per Setup from KK's own clothing slots; accessories and hair are never touched");
			SoftFleshPlugin.CfgClothLift = base.Config.Bind<float>("Physics", "Cloth Lift mm", 0.8f, new ConfigDescription("Permanent outward inflation of bound garments - covers the posed skinning divergence between the subdivided body and the original-density cloth", new AcceptableValueRange<float>(0f, 2.5f), Array.Empty<object>()));
			SoftFleshPlugin.CfgClothSubdiv = base.Config.Bind<bool>("Physics", "Cloth Subdivide", true, "Crest cover: lift each cloth vert to the tallest wave crest within its span, so the fabric bridges crests the way tensioned cloth does. Replaces the retired mesh-subdivision approach (same config key)");
			SoftFleshPlugin.CfgCorr = base.Config.Bind<float>("Opening", "Corrective Smooth", 0.6f, new ConfigDescription("Smooths the FINAL skinned surface (Blender's Corrective Smooth). Fixes faceting caused by bone rotation and coarse skin weights, which field smoothing cannot touch", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSubdiv = base.Config.Bind<bool>("Subdivide", "Enabled", true, "Refine oversized faces in the deformed region at Setup. Large flat triangles are the measured cause of faceting that no smoothing can fix");
			SoftFleshPlugin.CfgSubArea = base.Config.Bind<float>("Subdivide", "Min Face Area", 100f, new ConfigDescription("Faces larger than this (mm2) get split", new AcceptableValueRange<float>(5f, 200f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSubWGap = base.Config.Bind<float>("Subdivide", "Weight Boundary Guard", 0.4f, new ConfigDescription("Do not split a long edge whose ends are skinned to different bones by more than this (L1, 0-2). Splitting across a weight boundary reveals the true skinned surface on one side of the split contour only, and the step reads as a crease. The genital core is exempt. 0.5 was measured to crease on this body, 0.4 clean", new AcceptableValueRange<float>(0f, 2f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSubLevels = base.Config.Bind<int>("Subdivide", "Levels", 1, new ConfigDescription("Split passes", new AcceptableValueRange<int>(1, 4), Array.Empty<object>()));
			SoftFleshPlugin.CfgMouthAuto = base.Config.Bind<bool>("Face", "Mouth auto-open", true, "An insert-mode item at the lips switches the character to the 'kuwae' (holding) mouth and opens it to the item's width; the character's own mouth comes back when it leaves");
			SoftFleshPlugin.CfgMouthScale = base.Config.Bind<float>("Face", "Open scale", 1f, new ConfigDescription("The lip aperture is servoed to item radius x this: 1 = the lips touch the item, above 1 leaves a gap, below 1 the lips press into it (the shrinkwrap then pushes them out)", new AcceptableValueRange<float>(0.5f, 1.5f), Array.Empty<object>()));
			SoftFleshPlugin.CfgMouthReach = base.Config.Bind<float>("Face", "Mouth reach mm", 40f, new ConfigDescription("How close an item's surface must come to the lip centre to open the mouth", new AcceptableValueRange<float>(10f, 100f), Array.Empty<object>()));
			SoftFleshPlugin.CfgFaceFirm = base.Config.Bind<float>("Face", "Face firmness x", 3f, new ConfigDescription("The face's spring, as a multiple of the body's Stiffness. Higher = a shallower dent for the same touch and a faster rebound", new AcceptableValueRange<float>(0.5f, 8f), Array.Empty<object>()));
			SoftFleshPlugin.CfgFaceDamp = base.Config.Bind<float>("Face", "Face damping x", 4f, new ConfigDescription("The face's damping, as a multiple of the body's Damping. With firmness x3, x4 is a damping ratio of ~0.85: it settles in one motion instead of wobbling", new AcceptableValueRange<float>(0.5f, 10f), Array.Empty<object>()));
			SoftFleshPlugin.CfgFaceWave = base.Config.Bind<float>("Face", "Face wave x", 0.15f, new ConfigDescription("How much of the body's wave coupling the face carries: 1 = a touch on the cheek ripples across the whole face like a belly, 0 = every vertex answers only its own contact", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgFaceSlap = base.Config.Bind<float>("Face", "Face slap x", 0.3f, new ConfigDescription("The face's share of the Slap Impulse (a fast strike's velocity kick)", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgFaceDepth = base.Config.Bind<float>("Face", "Face max dent mm", 8f, new ConfigDescription("The deepest a finger can dent the face, and how far below the skin a finger's axis may sit and still count as touching it. The body's Max Depth (30mm) let a hand outside the cheek pull the inside of the mouth toward it", new AcceptableValueRange<float>(2f, 20f), Array.Empty<object>()));
			SoftFleshPlugin.CfgFaceDrag = base.Config.Bind<bool>("Face", "Face skin tension", true, "The skin-tension membrane on the face: the skin around a touch is drawn along with it, as on the body - but never the lips or the inside of the mouth. Off = a collider dents the face only where it touches");
			SoftFleshPlugin.CfgMouthTilt = base.Config.Bind<bool>("Face", "Opening follows the item", true, "The mouth's opening is measured and expanded about the INSERTED ITEM's axis instead of the mouth's own fixed axis. Off = the old fixed frame");
			SoftFleshPlugin.CfgTiltFollow = base.Config.Bind<float>("Face", "Tilt follow x", 0.25f, new ConfigDescription("How far the lip ring turns into the plane perpendicular to a slanted item: 0 = the lips stay in the face's plane (a stretched slot), 0.4 = as far as the lips can go before the wall crosses the skin at a withdrawal", new AcceptableValueRange<float>(0f, 0.4f), Array.Empty<object>()));
			SoftFleshPlugin.CfgLipDrag = base.Config.Bind<float>("Face", "Lip stretch mm", 6f, new ConfigDescription("How far the lips are dragged along an item as it moves - in as it goes in, out as it comes out. 0 = the lips are a hole the item passes through", new AcceptableValueRange<float>(0f, 25f), Array.Empty<object>()));
			SoftFleshPlugin.CfgLipPushIn = base.Config.Bind<float>("Face", "Lip push-in mm", 3f, new ConfigDescription("How far the lips fold IN along an item going in before they slip on it (more and the mouth deforms); out is Lip stretch", new AcceptableValueRange<float>(0f, 5f), Array.Empty<object>()));
			SoftFleshPlugin.CfgLipReturn = base.Config.Bind<float>("Face", "Lip return s", 1.2f, new ConfigDescription("How long the dragged lips take to creep back toward their place - the skin's elasticity. Short = springy, long = the lips stay where the item left them; 0 = they spring back at once (no lasting stretch)", new AcceptableValueRange<float>(0f, 3f), Array.Empty<object>()));
			SoftFleshPlugin.CfgLipHold = base.Config.Bind<bool>("Face", "Lip no return", false, "The dragged lips do not creep back: they stay where the item left them until it moves them again (Lip return is ignored)");
			SoftFleshPlugin.CfgLipDamp = base.Config.Bind<float>("Face", "Lip damping s", 0.04f, new ConfigDescription("How far the lips lag behind the item's pull, in seconds of its motion. 0 = rigidly stuck to the item, larger = softer, the flesh trails the item", new AcceptableValueRange<float>(0f, 0.3f), Array.Empty<object>()));
			SoftFleshPlugin.CfgDeepCheck = base.Config.Bind<bool>("Face", "Deep mesh check", false, "Diagnostic: every-triangle-against-every-triangle checks of the wrapped mouth in the readout (WRAP-COVER / CROSS / SELF / OTHER / LAYER). Costs several milliseconds twice a second - leave OFF unless a poke-through is being hunted");
			SoftFleshPlugin.CfgLipSmooth = base.Config.Bind<int>("Face", "Lip Smooth", 3, new ConfigDescription("Corrective Smooth passes on the wrapped lips (after the wrap, before the final outside check). More = smoother ring, slightly less exact on the item", new AcceptableValueRange<int>(0, 12), Array.Empty<object>()));
			SoftFleshPlugin.CfgSubBody = base.Config.Bind<bool>("Subdivide", "Whole Body", false, "Refine oversized faces over the whole body, not only around the genitals. Measured: ~13mm between vertices where hands press, so a finger dents one to three of them and the dent is a V of flat triangles - the straight crease no smoothing can round. Costs about 4x the vertices where faces are large (budget-guarded under Unity's 65535). Clear and Setup again to apply");
			SoftFleshPlugin.CfgVisc = base.Config.Bind<float>("Soft Body", "Viscosity", 0.47f, new ConfigDescription("Damping that rises with spatial frequency: broad waves keep travelling, fine sloshing dies fast. 0 = watery, 1 = thick flesh", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgBellyW = base.Config.Bind<float>("Physics", "Bulge Width", 1f, new ConfigDescription("How far SIDEWAYS of the shaft each segment lifts the skin. Lower until the ridge is as narrow as the item", new AcceptableValueRange<float>(0.1f, 1.5f), Array.Empty<object>()));
			SoftFleshPlugin.CfgBellyLen = base.Config.Bind<float>("Physics", "Bulge Length", 0.35f, new ConfigDescription("How far in from the ENTRANCE the ridge runs, as a fraction of how deep the item actually is", new AcceptableValueRange<float>(0f, 3f), Array.Empty<object>()));
			SoftFleshPlugin.CfgBellyStart = base.Config.Bind<float>("Physics", "Bulge Start", 0f, new ConfigDescription("How far in from the entrance the ridge takes to reach full strength. The mound is naturally strongest right above the opening because the skin is closest to the shaft there; this is what quietens that without touching the abdomen", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgBellyShape = base.Config.Bind<float>("Physics", "Bulge Follows Taper", 0f, new ConfigDescription("0 = one height from the chain's max radius; 1 = the height follows this segment's own thickness", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgEntTilt = base.Config.Bind<float>("Opening", "Entrance Tilt", 0.42f, new ConfigDescription("The opening turns to meet an item that enters at an angle", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgBakeReach = base.Config.Bind<float>("Opening", "Bake Reach", 0.06f, new ConfigDescription("Radius (m) of the support region grown for the bake", new AcceptableValueRange<float>(0.02f, 0.16f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSpreadW = base.Config.Bind<float>("Subdivide", "Spread To Skin", 0f, new ConfigDescription("Spread the ring's skin weights into the surrounding flesh", new AcceptableValueRange<float>(0f, 0.9f), Array.Empty<object>()));
			SoftFleshPlugin.CfgSpreadR = base.Config.Bind<float>("Subdivide", "Spread Reach", 0.055f, new ConfigDescription("How far (m) that weight spreading reaches", new AcceptableValueRange<float>(0.02f, 0.12f), Array.Empty<object>()));
			SoftFleshPlugin.CfgWidthTrim = base.Config.Bind<float>("Opening", "Width Trim", 1f, new ConfigDescription("How far tracking may depart from the calibrated width. 1 = the old free integrator", new AcceptableValueRange<float>(0.05f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgWallOnly = base.Config.Bind<float>("Opening", "Push Wall Only", 0f, new ConfigDescription("Damp the bake's push on flesh whose rest normal faces away from the canal axis", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			SoftFleshPlugin.CfgTaper = base.Config.Bind<bool>("Physics", "Canal Follows Taper", true, "The canal's radius follows the item's own thickness at each depth instead of one width from mouth to tip");
			SoftFleshPlugin.CfgCrotchIso = base.Config.Bind<bool>("Opening", "KKPE Crotch Isolate", true, "Keep the opening still when KKPE's Crotch correction is on (off = the genital region rides the pelvis)");
			for (int i = 0; i < 12; i++)
			{
				string key = ((i < SoftFleshPlugin.PresetNames.Length) ? SoftFleshPlugin.PresetNames[i] : ("Slot " + (i + 1).ToString()));
				SoftFleshPlugin.CfgPreset[i] = base.Config.Bind<string>("Presets", key, "", "Saved slider values for this preset. Written by the Save button; edit by hand at your own risk");
				SoftFleshPlugin.CfgPresetName[i] = base.Config.Bind<string>("Presets", key + " Name", key, "What this slot is called in the panel");
			}
			SoftFleshPlugin.CfgWave = base.Config.Bind<float>("Physics", "Wave Reach", 1f, new ConfigDescription("How loosely flesh AWAY from a finger is anchored — higher lets the ripple travel further before dying out", new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
			int zombies = 0;
			foreach (MonoBehaviour mb in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
			{
				if (!(mb == null))
				{
					Type ty = mb.GetType();
					if (!(ty.FullName != "SoftFlesh.SoftFleshController") && !(ty == typeof(SoftFleshController)))
					{
						try
						{
							MethodInfo j = ty.GetMethod("ClearAll");
							if (j != null)
							{
								j.Invoke(mb, null);
							}
						}
						catch
						{
						}
						if (mb.gameObject != null)
						{
							UnityEngine.Object.Destroy(mb.gameObject);
						}
						zombies++;
					}
				}
			}
			int stale = SoftFleshPlugin.PurgeStaleHandlers();
			if (zombies > 0 || stale > 0)
			{
				SoftFleshPlugin.Log(string.Concat(new string[]
				{
					"[SF] hot-reload purge: destroyed ",
					zombies.ToString(),
					" zombie controller generation(s), removed ",
					stale.ToString(),
					" stale camera/canvas handler(s)"
				}));
			}
			object prevGen = AppDomain.CurrentDomain.GetData("SoftFlesh.generation");
			this._gen = ((prevGen is int) ? ((int)prevGen) : 0) + 1;
			AppDomain.CurrentDomain.SetData("SoftFlesh.generation", this._gen);
			AppDomain.CurrentDomain.SetData("SoftFlesh.camVote", new Func<bool>(() => this._alive && this.IsCurrentGen && this._showUI && this._mouseOverWindow));
			GameObject go = new GameObject("SoftFleshController")
			{
				hideFlags = 61
			};
			UnityEngine.Object.DontDestroyOnLoad(go);
			this._ctrl = go.AddComponent<SoftFleshController>();
			SoftFleshPlugin.TryParseV3(SoftFleshPlugin.CfgAxisEntry.Value, ref this._ctrl.savedEntryB);
			SoftFleshPlugin.TryParseV3(SoftFleshPlugin.CfgAxisDir.Value, ref this._ctrl.savedDirB);
			SoftFleshPlugin._live = this;
			SfTimeline.PublishBridge();
			AppDomain.CurrentDomain.SetData("SoftFlesh.scene.collect", new Func<Dictionary<string, object>>(SoftFleshPlugin.CollectSceneData));
			AppDomain.CurrentDomain.SetData("SoftFlesh.scene.apply", new Action<Dictionary<string, object>, ReadOnlyDictionary<int, ObjectCtrlInfo>, int>(SoftFleshPlugin.ApplySceneDataOp));
			AppDomain.CurrentDomain.SetData("SoftFlesh.scene.clear", new Action(SoftFleshPlugin.SceneClear));
		}

		// Token: 0x0600000E RID: 14 RVA: 0x000040FC File Offset: 0x000022FC
		private void Update()
		{
			if (!SfTimeline.Registered && Time.unscaledTime > this._tlRetry)
			{
				this._tlRetry = Time.unscaledTime + 2f;
				SfTimeline.RegisterOnce();
			}
			if (this._ctrl != null && Time.unscaledTime > this._swapSweepAt)
			{
				this._swapSweepAt = Time.unscaledTime + 1f;
				try
				{
					this._ctrl.PruneDeadBodies();
					this._ctrl.RefreshPressersIfCharsChanged();
					foreach (ChaControl c in this._ctrl.SetupChars())
					{
						if (!(c == null) && !this._rebuilding.Contains(c))
						{
							int st = this._ctrl.CheckBody(c);
							if (st == 1)
							{
								SoftFleshPlugin.Log("[SF] swap sweep: body mesh had been reassigned by the game - re-adopted in place");
							}
							else if (st == 2)
							{
								bool baked = this._ctrl.GetCharBaked(c);
								this._rebuilding.Add(c);
								base.StartCoroutine(this.ResetupWhenReady(c, this._ctrl.GetCharEnabled(c), baked));
							}
						}
					}
				}
				catch (Exception e)
				{
					SoftFleshPlugin.Log("[SF] swap sweep: " + e.Message);
				}
			}
			if (AppDomain.CurrentDomain.GetData("SoftFlesh.sceneHooked") == null)
			{
				try
				{
					if (StudioAPI.InsideStudio)
					{
						AppDomain.CurrentDomain.SetData("SoftFlesh.sceneHooked", "1");
						StudioSaveLoadApi.RegisterExtraBehaviour<SoftFleshScene>("com.kkstudio.softflesh");
					}
				}
				catch (Exception e2)
				{
					SoftFleshPlugin.Log("[SF] scene hook failed: " + e2.Message);
					AppDomain.CurrentDomain.SetData("SoftFlesh.sceneHooked", "1");
				}
			}
			if (SoftFleshPlugin.CfgHotkey.Value.IsDown())
			{
				this._showUI = !this._showUI;
			}
			if (!this._showUI)
			{
				this._mouseOverWindow = false;
			}
			if (this._ctrl == null)
			{
				return;
			}
			this._ctrl.MonitorGuideTick();
			if (this._ctrl.axisDirty)
			{
				SoftFleshPlugin.CfgAxisEntry.Value = SoftFleshPlugin.V3s(this._ctrl.savedEntryB);
				SoftFleshPlugin.CfgAxisDir.Value = SoftFleshPlugin.V3s(this._ctrl.savedDirB);
				this._ctrl.axisDirty = false;
			}
			this._ctrl.touchRadius = SoftFleshPlugin.CfgTouchRadius.Value;
			this._ctrl.maxDepth = SoftFleshPlugin.CfgMaxDepth.Value;
			this._ctrl.knuckleFrac = SoftFleshPlugin.CfgKnuckle.Value;
			this._ctrl.stiffness = SoftFleshPlugin.CfgStiffness.Value;
			this._ctrl.damping = SoftFleshPlugin.CfgDamping.Value;
			this._ctrl.waveSpeed = SoftFleshPlugin.CfgWaveSpeed.Value;
			this._ctrl.viscosity = SoftFleshPlugin.CfgVisc.Value;
			this._ctrl.trackItems = SoftFleshPlugin.CfgTrack.Value;
			this._ctrl.gapeHold = SoftFleshPlugin.CfgGapeHold.Value;
			this._ctrl.suction = SoftFleshPlugin.CfgSuction.Value;
			this._ctrl.petalFirm = SoftFleshPlugin.CfgPetalFirm.Value;
			this._ctrl.trackWidthX = SoftFleshPlugin.CfgTrackWx.Value;
			this._ctrl.itemGuide = SoftFleshPlugin.CfgItemGuide.Value;
			this._ctrl.followGain = SoftFleshPlugin.CfgFollow.Value;
			this._ctrl.spreadShift = SoftFleshPlugin.CfgSpread.Value * 0.001f;
			this._ctrl.bulge = SoftFleshPlugin.CfgBulge.Value;
			this._ctrl.smoothFactor = SoftFleshPlugin.CfgSmooth.Value;
			this._ctrl.skinTension = SoftFleshPlugin.CfgTension.Value * 0.001f;
			this._ctrl.skinBend = Mathf.RoundToInt(SoftFleshPlugin.CfgBend.Value);
			this._ctrl.normalBoost = SoftFleshPlugin.CfgShadow.Value;
			this._ctrl.nipFirm = SoftFleshPlugin.CfgNipFirm.Value;
			this._ctrl.slapGain = SoftFleshPlugin.CfgSlap.Value;
			this._ctrl.slapSoften = SoftFleshPlugin.CfgSoften.Value;
			this._ctrl.itemRadScale = SoftFleshPlugin.CfgItemRad.Value;
			this._ctrl.bellyGain = SoftFleshPlugin.CfgBelly.Value;
			this._ctrl.bulgeWide = SoftFleshPlugin.CfgBulgeWide.Value;
			this._ctrl.openTestDepth = SoftFleshPlugin.CfgOpenD.Value;
			this._ctrl.openTestWidth = SoftFleshPlugin.CfgOpenW.Value;
			this._ctrl.clitHold = SoftFleshPlugin.CfgClitHold.Value;
			this._ctrl.smoothShade = SoftFleshPlugin.CfgSmoothShade.Value;
			this._ctrl.shadeTau = SoftFleshPlugin.CfgShadeTau.Value * 0.001f;
			this._ctrl.openMaxPct = SoftFleshPlugin.CfgOpenMax.Value;
			this._ctrl.itemStretch = SoftFleshPlugin.CfgItemStretch.Value;
			this._ctrl.itemSquash = SoftFleshPlugin.CfgItemSquash.Value;
			this._ctrl.waveShield = SoftFleshPlugin.CfgWaveShield.Value;
			this._ctrl.clothFollow = SoftFleshPlugin.CfgClothFollow.Value;
			this._ctrl.secondSkin = SoftFleshPlugin.CfgSecondSkin.Value;
			this._ctrl.spanGuard = SoftFleshPlugin.CfgSpanGuard.Value;
			this._ctrl.clothLift = SoftFleshPlugin.CfgClothLift.Value * 0.001f;
			this._ctrl.clothSubdiv = SoftFleshPlugin.CfgClothSubdiv.Value;
			this._ctrl.corrSmooth = 0f;
			this._ctrl.subdivEnabled = SoftFleshPlugin.CfgSubdiv.Value;
			this._ctrl.subdivMinArea = SoftFleshPlugin.CfgSubArea.Value;
			this._ctrl.subdivWeightGap = SoftFleshPlugin.CfgSubWGap.Value;
			this._ctrl.subdivLevels = SoftFleshPlugin.CfgSubLevels.Value;
			this._ctrl.subdivBody = SoftFleshPlugin.CfgSubBody.Value;
			this._ctrl.mouthAuto = SoftFleshPlugin.CfgMouthAuto.Value;
			this._ctrl.mouthScale = SoftFleshPlugin.CfgMouthScale.Value;
			this._ctrl.mouthReach = SoftFleshPlugin.CfgMouthReach.Value * 0.001f;
			this._ctrl.lipSmooth = SoftFleshPlugin.CfgLipSmooth.Value;
			this._ctrl.deepCheck = SoftFleshPlugin.CfgDeepCheck.Value;
			this._ctrl.mouthTiltFollow = SoftFleshPlugin.CfgMouthTilt.Value;
			this._ctrl.tiltFollow = SoftFleshPlugin.CfgTiltFollow.Value;
			this._ctrl.faceFirm = SoftFleshPlugin.CfgFaceFirm.Value;
			this._ctrl.faceDamp = SoftFleshPlugin.CfgFaceDamp.Value;
			this._ctrl.faceWave = SoftFleshPlugin.CfgFaceWave.Value;
			this._ctrl.faceSlap = SoftFleshPlugin.CfgFaceSlap.Value;
			this._ctrl.faceMaxDepth = SoftFleshPlugin.CfgFaceDepth.Value * 0.001f;
			this._ctrl.faceDrag = SoftFleshPlugin.CfgFaceDrag.Value;
			this._ctrl.lipDrag = SoftFleshPlugin.CfgLipDrag.Value * 0.001f;
			this._ctrl.lipNoReturn = SoftFleshPlugin.CfgLipHold.Value;
			this._ctrl.lipPushIn = SoftFleshPlugin.CfgLipPushIn.Value * 0.001f;
			this._ctrl.lipReturn = SoftFleshPlugin.CfgLipReturn.Value;
			this._ctrl.lipDamp = SoftFleshPlugin.CfgLipDamp.Value;
			this._ctrl.wave = SoftFleshPlugin.CfgWave.Value;
			this._ctrl.bellyWidth = SoftFleshPlugin.CfgBellyW.Value;
			this._ctrl.bellyLength = SoftFleshPlugin.CfgBellyLen.Value;
			this._ctrl.bellyStart = SoftFleshPlugin.CfgBellyStart.Value;
			this._ctrl.bellyShape = SoftFleshPlugin.CfgBellyShape.Value;
			this._ctrl.entranceTilt = SoftFleshPlugin.CfgEntTilt.Value;
			this._ctrl.bakeReach = SoftFleshPlugin.CfgBakeReach.Value;
			this._ctrl.spreadWeights = SoftFleshPlugin.CfgSpreadW.Value;
			this._ctrl.spreadReach = SoftFleshPlugin.CfgSpreadR.Value;
			this._ctrl.widthTrim = SoftFleshPlugin.CfgWidthTrim.Value;
			this._ctrl.wallOnly = SoftFleshPlugin.CfgWallOnly.Value;
			this._ctrl.taperCanal = SoftFleshPlugin.CfgTaper.Value;
			this._ctrl.crotchIsolate = SoftFleshPlugin.CfgCrotchIso.Value;
		}

		// Token: 0x0600000F RID: 15 RVA: 0x000049C4 File Offset: 0x00002BC4
		private void LateUpdate()
		{
			global::Studio.Studio st = Singleton<global::Studio.Studio>.Instance;
			CameraControl cc = ((st != null) ? st.cameraCtrl : null);
			if (cc == null)
			{
				return;
			}
			if (cc != this._lastCc)
			{
				this._lastCc = cc;
				object chained = AppDomain.CurrentDomain.GetData("SoftFlesh.camChainedFor");
				if (chained is int && (int)chained == cc.GetInstanceID())
				{
					return;
				}
				AppDomain.CurrentDomain.SetData("SoftFlesh.camChainedFor", cc.GetInstanceID());
				CameraControl.NoCtrlFunc prev = cc.noCtrlCondition;
				cc.noCtrlCondition = () => SoftFleshPlugin.Vote() || (prev != null && prev.Invoke());
				CameraControl.NoCtrlFunc prevZ = cc.zoomCondition;
				cc.zoomCondition = () => (prevZ == null || prevZ.Invoke()) && !SoftFleshPlugin.Vote();
			}
		}

		// Token: 0x06000010 RID: 16 RVA: 0x00004A90 File Offset: 0x00002C90
		private static bool Vote()
		{
			bool flag;
			try
			{
				Func<bool> f = AppDomain.CurrentDomain.GetData("SoftFlesh.camVote") as Func<bool>;
				flag = f != null && f();
			}
			catch
			{
				flag = false;
			}
			return flag;
		}

		// Token: 0x06000011 RID: 17 RVA: 0x00004AD8 File Offset: 0x00002CD8
		private static int PurgeStaleHandlers()
		{
			int i = 0;
			Camera.onPreCull = (Camera.CameraCallback)SoftFleshPlugin.PurgeDel(Camera.onPreCull, ref i);
			Camera.onPreRender = (Camera.CameraCallback)SoftFleshPlugin.PurgeDel(Camera.onPreRender, ref i);
			Camera.onPostRender = (Camera.CameraCallback)SoftFleshPlugin.PurgeDel(Camera.onPostRender, ref i);
			try
			{
				FieldInfo fi = typeof(Canvas).GetField("willRenderCanvases", BindingFlags.Static | BindingFlags.NonPublic);
				if (fi != null)
				{
					fi.SetValue(null, SoftFleshPlugin.PurgeDel(fi.GetValue(null) as Delegate, ref i));
				}
			}
			catch
			{
			}
			return i;
		}

		// Token: 0x06000012 RID: 18 RVA: 0x00004B7C File Offset: 0x00002D7C
		private static Delegate PurgeDel(Delegate del, ref int n)
		{
			if (del == null)
			{
				return null;
			}
			Delegate[] list = del.GetInvocationList();
			for (int i = 0; i < list.Length; i++)
			{
				object tgt = list[i].Target;
				if (tgt != null)
				{
					Type ty = tgt.GetType();
					if (ty.FullName != null && ty.FullName.StartsWith("SoftFlesh."))
					{
						bool foreignGen = ty != typeof(SoftFleshController) && ty != typeof(SoftFleshPlugin);
						UnityEngine.Object uo = tgt as UnityEngine.Object;
						bool dead = uo != null && !uo;
						if (foreignGen || dead)
						{
							del = Delegate.Remove(del, list[i]);
							n++;
						}
					}
				}
			}
			return del;
		}

		// Token: 0x06000013 RID: 19 RVA: 0x00004C3C File Offset: 0x00002E3C
		private void OnDestroy()
		{
			SFStallSpy.Remove();
			if (this._ctrl != null)
			{
				UnityEngine.Object.Destroy(this._ctrl.gameObject);
				this._ctrl = null;
			}
			this._alive = false;
			this._showUI = false;
			this._mouseOverWindow = false;
			if (SoftFleshPlugin._live == this)
			{
				SoftFleshPlugin._live = null;
			}
		}

		// Token: 0x06000014 RID: 20 RVA: 0x00004C9C File Offset: 0x00002E9C
		private static bool Ascii(string n)
		{
			if (string.IsNullOrEmpty(n))
			{
				return false;
			}
			for (int i = 0; i < n.Length; i++)
			{
				if (n[i] > '~' || n[i] < ' ')
				{
					return false;
				}
			}
			return true;
		}

		// Token: 0x06000015 RID: 21 RVA: 0x00004CE0 File Offset: 0x00002EE0
		private static string CharLabel(ChaControl c, int i)
		{
			string ws = null;
			string card = null;
			try
			{
				ObjectCtrlInfo oci = SfTimeline.OciOf(c);
				if (oci != null && oci.treeNodeObject != null)
				{
					ws = oci.treeNodeObject.textName;
				}
			}
			catch
			{
				ws = null;
			}
			try
			{
				card = ((c.fileParam != null) ? c.fileParam.fullname : null);
			}
			catch
			{
				card = null;
			}
			string j = (SoftFleshPlugin.Ascii(ws) ? ws : (SoftFleshPlugin.Ascii(card) ? card : null));
			if (j == null)
			{
				j = "Char " + (i + 1).ToString();
			}
			else
			{
				j = j.Trim();
				if (j.Length > 14)
				{
					j = j.Substring(0, 14);
				}
			}
			if (SoftFleshPlugin._labelLogged.Add(j))
			{
				try
				{
					GUIStyle st = UI.BtnPrimary;
					float w = st.CalcSize(new GUIContent(j)).x;
					string font = ((st.font != null) ? st.font.name : ((GUI.skin.font != null) ? GUI.skin.font.name : "?"));
					SoftFleshPlugin.Log(string.Concat(new string[]
					{
						"[SF] pill '",
						j,
						"' (",
						j.Length.ToString(),
						" chars) measured ",
						w.ToString("0"),
						"px font=",
						font,
						"  ws='",
						ws ?? "null",
						"' card='",
						card ?? "null",
						"'"
					}));
				}
				catch
				{
				}
			}
			return j;
		}

		// Token: 0x06000016 RID: 22 RVA: 0x00004EBC File Offset: 0x000030BC
		private void OnGUI()
		{
			if (!this.IsCurrentGen)
			{
				return;
			}
			if (!this._showUI)
			{
				this._mouseOverWindow = false;
				return;
			}
			if (Event.current.type == 8)
			{
				this._winRect.height = 0f;
				SoftFleshPlugin._sSolve = this._ctrl.solveDiag ?? "";
				SoftFleshPlugin._sDiag = this._ctrl.diag ?? "";
				SoftFleshPlugin._sDiagSt = this._ctrl.diagStatus ?? "";
				SoftFleshPlugin._sRig = this._ctrl.rigInfo ?? "";
				SoftFleshPlugin._sSubdiv = this._ctrl.subdivInfo ?? "";
				SoftFleshPlugin._sWeld = this._ctrl.weldInfo ?? "";
				SoftFleshPlugin._sGuide = this._ctrl.guideInfo ?? "";
				SoftFleshPlugin._sGuideL = this._ctrl.guideLive ?? "";
				SoftFleshPlugin._sBake = this._ctrl.bakeStatus ?? "";
				SoftFleshPlugin._sItems = this._ctrl.ItemColliderCount;
			}
			this._winRect.width = this._winW;
			this._winRect.height = this._winH;
			this._winRect = GUI.Window(20727, this._winRect, new GUI.WindowFunction(this.DrawWindow), GUIContent.none, UI.Win);
			this._winRect.x = Mathf.Clamp(this._winRect.x, -this._winRect.width + 90f, (float)Screen.width - 90f);
			this._winRect.y = Mathf.Clamp(this._winRect.y, 0f, (float)Screen.height - 28f);
			if (SoftFleshPlugin._confirmSave >= 0)
			{
				if (!this._dlgPlaced)
				{
					this._dlgRect = new Rect(Mathf.Round(this._winRect.x + (this._winRect.width - 390f) * 0.5f), Mathf.Round(this._winRect.y + Mathf.Max(70f, (this._winRect.height - 152f) * 0.4f)), 390f, 152f);
					this._dlgPlaced = true;
				}
				GUI.BringWindowToFront(20728);
				this._dlgRect = GUI.ModalWindow(20728, this._dlgRect, new GUI.WindowFunction(this.DrawConfirmDialog), GUIContent.none, UI.WinDialog);
				this._dlgRect.x = Mathf.Clamp(this._dlgRect.x, 0f, (float)Screen.width - 120f);
				this._dlgRect.y = Mathf.Clamp(this._dlgRect.y, 0f, (float)Screen.height - 40f);
			}
			else
			{
				this._dlgPlaced = false;
			}
			Vector2 i;
			i = new Vector2(Input.mousePosition.x, (float)Screen.height - Input.mousePosition.y);
			this._mouseOverWindow = this._winRect.Contains(i) || (SoftFleshPlugin._confirmSave >= 0 && this._dlgRect.Contains(i));
		}

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x06000017 RID: 23 RVA: 0x00005218 File Offset: 0x00003418
		private static GUIContent[] TabGC
		{
			get
			{
				if (SoftFleshPlugin._tabGC == null)
				{
					SoftFleshPlugin._tabGC = new GUIContent[SoftFleshPlugin.TabNames.Length];
					for (int i = 0; i < SoftFleshPlugin.TabNames.Length; i++)
					{
						SoftFleshPlugin._tabGC[i] = new GUIContent(SoftFleshPlugin.TabNames[i]);
					}
				}
				return SoftFleshPlugin._tabGC;
			}
		}

		// Token: 0x06000018 RID: 24 RVA: 0x00005268 File Offset: 0x00003468
		private void DrawWindow(int id)
		{
			if (SoftFleshPlugin._grip == null)
			{
				SoftFleshPlugin._grip = new GUIStyle(GUI.skin.label)
				{
					alignment = (TextAnchor)4,
					fixedHeight = 15f,
					fontSize = 10
				};
				SoftFleshPlugin._grip.normal.textColor = UI.TextFaint;
			}
			Rect header = this.DrawHeader();
			this.DrawActions();
			this._ctrl.diagVisible = SoftFleshPlugin._tab == -1;
			float topY = GUILayoutUtility.GetLastRect().yMax;
			float bandH = Mathf.Max(64f, this._winH - topY - 26f);
			GUILayout.BeginHorizontal(new GUILayoutOption[] { GUILayout.Height(bandH) });
			this.DrawSidebar(bandH);
			GUILayout.Space(16f);
			GUILayout.BeginVertical(new GUILayoutOption[] { GUILayout.Height(bandH) });
			GUILayout.Space(13f);
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Label(SoftFleshPlugin.TabNames[SoftFleshPlugin._tab].ToUpper(), UI.SecHead, Array.Empty<GUILayoutOption>());
			if (SoftFleshPlugin._tab >= 0 && SoftFleshPlugin._tab < SfTimeline.TabIds.Length && SfTimeline.CanKey)
			{
				GUILayout.Space(10f);
				Vector2 kpsz = UI.BtnDefault.CalcSize(new GUIContent("Key page"));
				if (GUILayout.Button("Key page", UI.BtnDefault, new GUILayoutOption[]
				{
					GUILayout.Width(kpsz.x + 10f),
					GUILayout.Height(Mathf.Max(24f, kpsz.y))
				}))
				{
					this._tlKeyMsg = SoftFleshPlugin.TabNames[SoftFleshPlugin._tab] + ": " + SfTimeline.KeyTab(SfTimeline.TabIds[SoftFleshPlugin._tab], SoftFleshPlugin.TabValues(SfTimeline.TabIds[SoftFleshPlugin._tab]));
					this._tlKeyMsgAt = Time.unscaledTime;
				}
				if (Time.unscaledTime - this._tlKeyMsgAt < 4f && !string.IsNullOrEmpty(this._tlKeyMsg))
				{
					GUILayout.Space(8f);
					GUILayout.Label(this._tlKeyMsg, UI.LblFaint, Array.Empty<GUILayoutOption>());
				}
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.Space(9f);
			this._scBody = GUILayout.BeginScrollView(this._scBody, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, new GUILayoutOption[] { GUILayout.Height(Mathf.Max(60f, bandH - 46f)) });
			switch (SoftFleshPlugin._tab)
			{
			case 0:
				this.TabContact();
				break;
			case 1:
				this.TabImpact();
				break;
			case 2:
				this.TabItems();
				break;
			case 3:
				this.TabFace();
				break;
			case 4:
				this.TabOpening();
				break;
			case 5:
				this.TabMesh();
				break;
			default:
				if (SoftFleshPlugin._tab == 6)
				{
					this.TabDev();
				}
				break;
			}
			GUILayout.EndScrollView();
			GUILayout.Space(10f);
			GUILayout.EndVertical();
			GUILayout.Space(14f);
			this.DrawSlotRail(bandH);
			GUILayout.Space(14f);
			GUILayout.EndHorizontal();
			Rect grip;
			grip = new Rect(this._winW * 0.5f - 60f, this._winH - 19f, 120f, 15f);
			GUI.Label(grip, "═════  drag  ═════", SoftFleshPlugin._grip ?? GUI.skin.label);
			Rect rz;
			rz = new Rect(this._winW - 21f, this._winH - 21f, 18f, 18f);
			GUI.Label(rz, "◢", SoftFleshPlugin._grip ?? GUI.skin.label);
			Event ev = Event.current;
			if (ev.type == null && rz.Contains(ev.mousePosition))
			{
				this._winResizing = true;
				ev.Use();
			}
			else if (ev.type == 1 && this._winResizing)
			{
				this._winResizing = false;
				ev.Use();
			}
			else if (this._winResizing && ev.type == 3)
			{
				this._winW = Mathf.Clamp(ev.mousePosition.x + 12f, 780f, 1700f);
				this._winH = Mathf.Clamp(ev.mousePosition.y + 12f, 210f, 1200f);
				ev.Use();
			}
			if (this._winResizing && !Input.GetMouseButton(0))
			{
				this._winResizing = false;
			}
			UI.DrawTooltip(this._winRect.width);
			GUI.DragWindow(header);
			GUI.DragWindow(grip);
		}

		// Token: 0x06000019 RID: 25 RVA: 0x000056FC File Offset: 0x000038FC
		private void DrawConfirmDialog(int id)
		{
			string name = SoftFleshPlugin.SlotLabel(SoftFleshPlugin._confirmSave);
			bool had = !string.IsNullOrEmpty(SoftFleshPlugin.CfgPreset[SoftFleshPlugin._confirmSave].Value);
			int have = 0;
			if (had)
			{
				foreach (string part in SoftFleshPlugin.CfgPreset[SoftFleshPlugin._confirmSave].Value.Split(new char[] { ';' }))
				{
					if (part.Length > 0)
					{
						have++;
					}
				}
			}
			float w = this._dlgRect.width;
			float h = this._dlgRect.height;
			UI.FillRound(new Rect(0f, 0f, w, 3f), UI.Amber, 2);
			GUI.Label(new Rect(18f, 14f, w - 36f, 22f), "Save preset", UI.Title);
			GUI.Label(new Rect(18f, 44f, w - 36f, 20f), "Write the current values to \"" + name + "\"?", UI.Lbl);
			GUI.Label(new Rect(18f, 66f, w - 36f, 20f), had ? ("This replaces the " + have.ToString() + " values already saved there.") : "This slot is empty.", UI.LblFaint);
			if (GUI.Button(new Rect(18f, h - 46f, 160f, 30f), "Save to \"" + name + "\"", UI.BtnBake))
			{
				this.SavePreset(SoftFleshPlugin._confirmSave);
				SoftFleshPlugin.Toast("Saved to \"" + name + "\"");
				SoftFleshPlugin._confirmSave = -1;
			}
			if (GUI.Button(new Rect(w - 108f, h - 46f, 90f, 30f), "Cancel", UI.BtnDefault))
			{
				SoftFleshPlugin._confirmSave = -1;
			}
			if (Event.current.type == 4 && Event.current.keyCode == 27)
			{
				SoftFleshPlugin._confirmSave = -1;
				Event.current.Use();
			}
			GUI.DragWindow(new Rect(0f, 0f, w, 40f));
		}

		// Token: 0x0600001A RID: 26 RVA: 0x00005931 File Offset: 0x00003B31
		private static void Toast(string msg)
		{
			SoftFleshPlugin._toast = msg;
			SoftFleshPlugin._toastUntil = Time.realtimeSinceStartup + 3.5f;
		}

		// Token: 0x0600001B RID: 27 RVA: 0x0000594C File Offset: 0x00003B4C
		private Rect DrawHeader()
		{
			Rect r = GUILayoutUtility.GetRect(1f, 40f, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
			UI.FillRound(new Rect(r.x, r.y, r.width, r.height + 9f), UI.BgHeader, 9);
			UI.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), UI.Line);
			GUI.Label(new Rect(r.x + 16f, r.y, 96f, 40f), "SoftFlesh", UI.Title);
			GUI.Label(new Rect(r.x + 104f, r.y + 1f, 150f, 40f), "v1.1.0  (Alt+G)", UI.MonoDim);
			bool up = this._ctrl.HasSetup;
			double[] ms = SoftFleshController.PhaseMs;
			string state = (up ? ("Active on " + this._ctrl.BodyCount.ToString() + " character(s)") : "Not set up — pose a character, then Setup");
			float sw = UI.LblDim.CalcSize(new GUIContent(state)).x;
			float msW = 64f;
			float dotX = r.xMax - 34f - msW - 14f - sw - 14f;
			UI.Fill(new Rect(dotX, r.y + 20f - 3.5f, 7f, 7f), up ? UI.Dot : UI.Amber);
			GUI.Label(new Rect(dotX + 13f, r.y, sw + 6f, 40f), state, UI.LblDim);
			GUI.Label(new Rect(r.xMax - 34f - msW, r.y, msW, 40f), ms[0].ToString("F1") + "ms", UI.MonoAmber);
			if (GUI.Button(new Rect(r.xMax - 27f, r.y + 10f, 20f, 20f), "×", UI.BtnClose))
			{
				this._showUI = false;
			}
			if (SoftFleshPlugin._toast.Length > 0 && Time.realtimeSinceStartup < SoftFleshPlugin._toastUntil)
			{
				float tw = UI.LblBright.CalcSize(new GUIContent(SoftFleshPlugin._toast)).x + 26f;
				Rect tb;
				tb = new Rect(r.x + 104f, r.y + 7f, tw, 26f);
				UI.FillRound(tb, new Color(UI.Accent.r, UI.Accent.g, UI.Accent.b, 0.22f), 5);
				GUI.Label(new Rect(tb.x + 13f, tb.y, tb.width - 26f, tb.height), SoftFleshPlugin._toast, UI.LblBright);
			}
			else if (SoftFleshPlugin._toast.Length > 0 && Time.realtimeSinceStartup >= SoftFleshPlugin._toastUntil)
			{
				SoftFleshPlugin._toast = "";
			}
			return new Rect(0f, 0f, 10000f, 40f);
		}

		// Token: 0x0600001C RID: 28 RVA: 0x00005CC4 File Offset: 0x00003EC4
		private void DrawActions()
		{
			GUILayout.Space(9f);
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Space(13f);
			int selN = 0;
			try
			{
				selN = this._ctrl.SelectedCharCount();
			}
			catch
			{
			}
			string setupLbl = ((selN > 0) ? ((selN == 1) ? "Setup (selected)" : ("Setup (" + selN.ToString() + " selected)")) : "Setup (all characters)");
			if (GUILayout.Button(setupLbl, UI.BtnPrimary, new GUILayoutOption[]
			{
				GUILayout.Width(186f),
				GUILayout.Height(30f)
			}))
			{
				try
				{
					this._ctrl.SetupSelected();
				}
				catch (Exception e)
				{
					string text = "[SF] Setup THREW: ";
					Exception ex = e;
					SoftFleshPlugin.Log(text + ((ex != null) ? ex.ToString() : null));
				}
			}
			if (GUILayout.Button("Clear", UI.BtnDefault, new GUILayoutOption[]
			{
				GUILayout.Width(80f),
				GUILayout.Height(30f)
			}))
			{
				this._ctrl.ClearAll();
			}
			if (selN > 0 && GUILayout.Button((selN == 1) ? "Clear (selected)" : ("Clear (" + selN.ToString() + ")"), UI.BtnDefault, new GUILayoutOption[]
			{
				GUILayout.Width(140f),
				GUILayout.Height(30f)
			}))
			{
				try
				{
					int nH = this._ctrl.HandsOnlySelected();
					SfTimeline.Refresh();
					SoftFleshPlugin.Log("[SF] clear (selected): " + nH.ToString() + " character(s) released - their hands still press");
				}
				catch (Exception e2)
				{
					string text2 = "[SF] Clear (selected) THREW: ";
					Exception ex2 = e2;
					SoftFleshPlugin.Log(text2 + ((ex2 != null) ? ex2.ToString() : null));
				}
			}
			GUILayout.FlexibleSpace();
			GUILayout.Space(13f);
			GUILayout.EndHorizontal();
			ChaControl[] sfChars = null;
			try
			{
				sfChars = this._ctrl.SetupChars();
			}
			catch
			{
			}
			if (sfChars != null && sfChars.Length != 0)
			{
				GUILayout.Space(4f);
				GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
				GUILayout.Space(13f);
				GUILayout.Label("Active", UI.LblDim, new GUILayoutOption[] { GUILayout.Width(44f) });
				float rowX = 57f;
				float maxX = this._winRect.width - 20f;
				for (int i = 0; i < sfChars.Length; i++)
				{
					ChaControl c = sfChars[i];
					if (!(c == null))
					{
						bool on = false;
						bool live = false;
						try
						{
							on = this._ctrl.GetCharEnabled(c);
							live = this._ctrl.IsBodyLive(c);
						}
						catch
						{
						}
						string lbl = SoftFleshPlugin.CharLabel(c, i);
						if (!live)
						{
							lbl += " ...";
						}
						GUIStyle st = ((on && live) ? UI.BtnPrimary : UI.BtnDefault);
						Vector2 sz = st.CalcSize(new GUIContent(lbl));
						Vector2 ksz = UI.BtnDefault.CalcSize(new GUIContent("Key"));
						float pillW = sz.x + 8f;
						float keyW = (SfTimeline.CanKey ? (ksz.x + 8f) : 0f);
						float h = Mathf.Max(28f, sz.y);
						if (rowX > 57f && rowX + pillW + keyW + 8f > maxX)
						{
							GUILayout.FlexibleSpace();
							GUILayout.EndHorizontal();
							GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
							GUILayout.Space(57f);
							rowX = 57f;
						}
						if (GUILayout.Button(lbl, st, new GUILayoutOption[]
						{
							GUILayout.Width(pillW),
							GUILayout.Height(h)
						}))
						{
							try
							{
								this._ctrl.SetCharEnabled(c, !on);
							}
							catch
							{
							}
						}
						if (SfTimeline.CanKey && GUILayout.Button("Key", UI.BtnDefault, new GUILayoutOption[]
						{
							GUILayout.Width(keyW),
							GUILayout.Height(h)
						}))
						{
							this._tlKeyMsg = lbl + ": " + SfTimeline.KeyNow(c);
							this._tlKeyMsgAt = Time.unscaledTime;
						}
						GUILayout.Space(8f);
						rowX += pillW + keyW + 8f;
					}
				}
				GUILayout.FlexibleSpace();
				GUILayout.Space(13f);
				GUILayout.EndHorizontal();
				GUILayout.Space(5f);
				GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
				GUILayout.Space(57f);
				string tlHint = ((!SfTimeline.Registered) ? "Timeline: not detected" : ((Time.unscaledTime - this._tlKeyMsgAt < 4f && !string.IsNullOrEmpty(this._tlKeyMsg)) ? this._tlKeyMsg : "Key = keyframe this state at the playhead  (1 on / 0 off / between: hold)"));
				GUILayout.Label(tlHint, UI.LblFaint, Array.Empty<GUILayoutOption>());
				GUILayout.EndHorizontal();
			}
			GUILayout.Space(4f);
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Space(13f);
			if (GUILayout.Button("Add selected item(s) as collider", UI.BtnLeft, new GUILayoutOption[] { GUILayout.Height(30f) }))
			{
				SoftFleshPlugin.Log("[SF] item colliders added: " + this._ctrl.AddSelectedItems().ToString());
			}
			if (GUILayout.Button("Clear items", UI.BtnDefault, new GUILayoutOption[]
			{
				GUILayout.Width(112f),
				GUILayout.Height(30f)
			}))
			{
				this._ctrl.ClearItemColliders();
			}
			GUILayout.Space(13f);
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Space(13f);
			this._ctrl.showColliders = UI.Switch(this._ctrl.showColliders, "Show colliders (debug spheres)");
			GUILayout.FlexibleSpace();
			GUILayout.Label(SoftFleshController.SkipTook ? "idle-skip TAKEN" : "idle-skip blocked", UI.MonoDim, new GUILayoutOption[] { GUILayout.Height(22f) });
			if (!SoftFleshController.SkipTook)
			{
				GUILayout.Space(8f);
				GUILayout.Label(SoftFleshPlugin.SkipReason(), UI.MonoDim, new GUILayoutOption[] { GUILayout.Height(22f) });
			}
			GUILayout.Space(13f);
			GUILayout.EndHorizontal();
			GUILayout.Space(9f);
			UI.Rule();
		}

		// Token: 0x0600001D RID: 29 RVA: 0x00006308 File Offset: 0x00004508
		private void DrawSlotRail(float bandH)
		{
			GUILayout.BeginVertical(new GUILayoutOption[]
			{
				GUILayout.Width(172f),
				GUILayout.Height(bandH)
			});
			Rect col = GUILayoutUtility.GetRect(172f, 0f, new GUILayoutOption[] { GUILayout.Width(172f) });
			UI.Fill(new Rect(col.x, col.y, 172f, 4000f), UI.BgSide);
			GUILayout.Space(13f);
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Space(12f);
			GUILayout.Label("PRESETS", UI.SecHead, Array.Empty<GUILayoutOption>());
			GUILayout.EndHorizontal();
			GUILayout.Space(8f);
			int nSlots = SoftFleshPlugin.SlotCount();
			float listNeed = (float)nSlots * 30f + 4f;
			float listRoom = Mathf.Max(40f, bandH - 200f);
			float listH = Mathf.Min(Mathf.Max(30f, listNeed), listRoom);
			this._scRail = GUILayout.BeginScrollView(this._scRail, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none, new GUILayoutOption[] { GUILayout.Height(listH) });
			int shown = 0;
			for (int i = 0; i < 12; i++)
			{
				if (SoftFleshPlugin.SlotUsed(i))
				{
					shown++;
					GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
					GUILayout.Space(12f);
					if (GUILayout.Button(SoftFleshPlugin.SlotLabel(i), (i == SoftFleshPlugin._preset) ? UI.PillOn : UI.PillOff, new GUILayoutOption[]
					{
						GUILayout.Width(148f),
						GUILayout.Height(26f)
					}))
					{
						SoftFleshPlugin._preset = i;
						this.LoadPreset(i);
						SoftFleshPlugin._confirmDelete = -1;
					}
					GUILayout.EndHorizontal();
					GUILayout.Space(4f);
				}
			}
			if (shown == 0)
			{
				GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
				GUILayout.Space(12f);
				GUILayout.Label("No presets yet.", UI.WrapDim, new GUILayoutOption[] { GUILayout.Width(148f) });
				GUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();
			GUILayout.Space(9f);
			UI.Rule();
			GUILayout.Space(7f);
			bool sel = SoftFleshPlugin._preset >= 0 && SoftFleshPlugin._preset < 12 && SoftFleshPlugin.SlotUsed(SoftFleshPlugin._preset);
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Space(12f);
			if (sel)
			{
				SoftFleshPlugin.CfgPresetName[SoftFleshPlugin._preset].Value = GUILayout.TextField(SoftFleshPlugin.CfgPresetName[SoftFleshPlugin._preset].Value ?? "", new GUILayoutOption[]
				{
					GUILayout.Width(148f),
					GUILayout.Height(24f)
				});
			}
			else
			{
				GUILayout.Label("select a preset", UI.WrapDim, new GUILayoutOption[] { GUILayout.Width(148f) });
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(6f);
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Space(12f);
			if (GUILayout.Button("SAVE", sel ? UI.BtnPrimary : UI.BtnDefault, new GUILayoutOption[]
			{
				GUILayout.Width(72f),
				GUILayout.Height(26f)
			}) && sel)
			{
				SoftFleshPlugin._confirmSave = SoftFleshPlugin._preset;
				SoftFleshPlugin._confirmDelete = -1;
			}
			GUILayout.Space(4f);
			bool armed = sel && SoftFleshPlugin._confirmDelete == SoftFleshPlugin._preset;
			if (GUILayout.Button(armed ? "SURE?" : "DEL", UI.BtnDefault, new GUILayoutOption[]
			{
				GUILayout.Width(72f),
				GUILayout.Height(26f)
			}) && sel)
			{
				if (armed)
				{
					this.DeleteSlot(SoftFleshPlugin._preset);
				}
				else
				{
					SoftFleshPlugin._confirmDelete = SoftFleshPlugin._preset;
					SoftFleshPlugin._confirmSave = -1;
				}
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(6f);
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Space(12f);
			if (GUILayout.Button("+  New Slot", UI.BtnDefault, new GUILayoutOption[]
			{
				GUILayout.Width(148f),
				GUILayout.Height(26f)
			}))
			{
				int j = SoftFleshPlugin.NextFreeSlot();
				if (j >= 0)
				{
					this.SavePreset(j);
					SoftFleshPlugin._preset = j;
					SoftFleshPlugin._confirmDelete = -1;
					SoftFleshPlugin.Toast("Slot " + (j + 1).ToString() + " created");
				}
				else
				{
					SoftFleshPlugin.Toast("All " + 12.ToString() + " slots are in use");
				}
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(7f);
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Space(12f);
			GUILayout.Label(SoftFleshPlugin.SlotCount().ToString() + " of " + 12.ToString() + " slots", UI.MonoDim, new GUILayoutOption[] { GUILayout.Width(148f) });
			GUILayout.EndHorizontal();
			GUILayout.Space(10f);
			GUILayout.EndVertical();
		}

		// Token: 0x0600001E RID: 30 RVA: 0x00006804 File Offset: 0x00004A04
		private void DrawSidebar(float bandH)
		{
			GUILayout.BeginVertical(new GUILayoutOption[]
			{
				GUILayout.Width(170f),
				GUILayout.Height(bandH)
			});
			Rect col = GUILayoutUtility.GetRect(170f, 0f, new GUILayoutOption[] { GUILayout.Width(170f) });
			UI.Fill(new Rect(col.x, col.y, 170f, 4000f), UI.BgSide);
			GUILayout.Space(10f);
			for (int i = 0; i < SoftFleshPlugin.TabNames.Length; i++)
			{
				bool on = SoftFleshPlugin._tab == i;
				float th = Mathf.Max(32f, UI.TabOn.CalcHeight(SoftFleshPlugin.TabGC[i], 170f));
				if (GUILayout.Button(SoftFleshPlugin.TabGC[i], on ? UI.TabOn : UI.TabOff, new GUILayoutOption[]
				{
					GUILayout.Width(170f),
					GUILayout.Height(th)
				}))
				{
					SoftFleshPlugin._tab = i;
				}
				Rect lr = GUILayoutUtility.GetLastRect();
				if (on)
				{
					UI.Fill(new Rect(lr.x, lr.y, 3f, lr.height), UI.Accent);
				}
				if (SoftFleshPlugin.TabCount[i].Length > 0)
				{
					GUI.Label(new Rect(lr.xMax - 30f, lr.y, 20f, lr.height), SoftFleshPlugin.TabCount[i], UI.MonoRight);
				}
			}
			GUILayout.FlexibleSpace();
			GUILayout.Space(10f);
			GUILayout.EndVertical();
		}

		// Token: 0x0600001F RID: 31 RVA: 0x00006994 File Offset: 0x00004B94
		private void TabDev()
		{
			GUILayout.Space(4f);
			GUILayout.Label("SoftFlesh", UI.Title, Array.Empty<GUILayoutOption>());
			GUILayout.Space(8f);
			GUILayout.Label("Contact soft-skin for Koikatsu Studio: fingers and items dent the flesh, slaps send waves through it, and tight clothing follows what the skin does.", UI.WrapDim, Array.Empty<GUILayoutOption>());
			GUILayout.Space(14f);
			GUILayout.Label("AUTHOR", UI.SecHead, Array.Empty<GUILayoutOption>());
			GUILayout.Space(4f);
			GUILayout.Label("Poison_KK", UI.LblDim, Array.Empty<GUILayoutOption>());
			GUILayout.Space(10f);
			if (GUILayout.Button("linktr.ee/Poison_kk ↗", UI.LinkBtn, new GUILayoutOption[]
			{
				GUILayout.Width(190f),
				GUILayout.Height(28f)
			}))
			{
				Application.OpenURL("https://linktr.ee/Poison_kk");
			}
			GUILayout.Space(16f);
			GUILayout.Label("© 2026 @poison_kk — All Rights Reserved", UI.MonoDim, Array.Empty<GUILayoutOption>());
			GUILayout.Space(3f);
			GUILayout.Label("v1.1.0   ·   hotkey Alt+G", UI.MonoDim, Array.Empty<GUILayoutOption>());
		}

		// Token: 0x06000020 RID: 32 RVA: 0x00006A9C File Offset: 0x00004C9C
		private static string SkipReason()
		{
			return (SoftFleshController.SkipMoved ? "pressersMoved " : "") + (SoftFleshController.SkipBusy ? string.Concat(new string[]
			{
				"openBusy(",
				SoftFleshController.SkipFieldChanged ? "field " : "",
				SoftFleshController.SkipSettling ? "settling " : "",
				(SoftFleshController.SkipCool > 0) ? "cool " : "",
				SoftFleshController.SkipBsDrv ? "bs " : "",
				SoftFleshController.SkipVagDrv ? "vag " : "",
				") "
			}) : "") + ((SoftFleshController.SkipVel >= SoftFleshController.SkipVelLimit) ? ("vel " + SoftFleshController.SkipVel.ToString("F4") + ">=" + SoftFleshController.SkipVelLimit.ToString("F4")) : "");
		}

		// Token: 0x06000021 RID: 33 RVA: 0x00006B9C File Offset: 0x00004D9C
		private void TabContact()
		{
			SoftFleshPlugin.Col2Begin();
			SoftFleshPlugin.Row("Touch Radius", SoftFleshPlugin.CfgTouchRadius, "F3");
			SoftFleshPlugin.Row("Max Depth", SoftFleshPlugin.CfgMaxDepth, "F3");
			SoftFleshPlugin.Row("Knuckle Depth", SoftFleshPlugin.CfgKnuckle, "F2");
			SoftFleshPlugin.Row("Stiffness", SoftFleshPlugin.CfgStiffness, "F0");
			SoftFleshPlugin.Row("Damping", SoftFleshPlugin.CfgDamping, "F0");
			SoftFleshPlugin.Row("Bulge", SoftFleshPlugin.CfgBulge, "F2");
			SoftFleshPlugin.Col2Next();
			SoftFleshPlugin.Row("Smooth", SoftFleshPlugin.CfgSmooth, "F2");
			SoftFleshPlugin.Row("Skin Tension", SoftFleshPlugin.CfgTension, "F0", "mm - how far the skin around a dent follows it (loose skin: small, tight skin: large). Render only");
			SoftFleshPlugin.Row("Skin Bending", SoftFleshPlugin.CfgBend, "F0", "passes - 0 = pure membrane (steep funnel at the finger), more = the rim rounds into a bowl. Render only");
			SoftFleshPlugin.Row("Smooth (shading)", SoftFleshPlugin.CfgSmoothShade, "F2");
			SoftFleshPlugin.Row("Shadow", SoftFleshPlugin.CfgShadow, "F1");
			SoftFleshPlugin.Row("Shadow smooth ms", SoftFleshPlugin.CfgShadeTau, "F0", "the shading follows a moving dent through this low-pass - no flicker while stroking (0 = off)");
			SoftFleshPlugin.Row("Nipple Firm", SoftFleshPlugin.CfgNipFirm, "F2");
			SoftFleshPlugin.Row("Cloth lift mm", SoftFleshPlugin.CfgClothLift, "F1");
			SoftFleshPlugin.Col2End();
			GUILayout.Space(4f);
			SoftFleshPlugin.CfgClothFollow.Value = UI.Switch(SoftFleshPlugin.CfgClothFollow.Value, "Clothes follow flesh (" + SoftFleshController.ClothInfo + ")");
			SoftFleshPlugin.CfgClothSubdiv.Value = UI.Switch(SoftFleshPlugin.CfgClothSubdiv.Value, "Cloth contact cover (lifts the span over the squeeze ridge beside pressing fingers; waves are handled by density parity and are never touched)");
		}

		// Token: 0x06000022 RID: 34 RVA: 0x00006D34 File Offset: 0x00004F34
		private void TabFace()
		{
			int selN = 0;
			try
			{
				selN = this._ctrl.SelectedCharCount();
			}
			catch
			{
			}
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			if (GUILayout.Button((selN > 0) ? "Setup face (selected)" : "Setup face (all)", UI.BtnPrimary, new GUILayoutOption[]
			{
				GUILayout.Width(186f),
				GUILayout.Height(30f)
			}))
			{
				try
				{
					int nF = this._ctrl.SetupFaceSelected();
					SfTimeline.Refresh();
					SoftFleshPlugin.Log("[SF] face: set up " + nF.ToString() + " face(s)");
				}
				catch (Exception e)
				{
					string text = "[SF] Setup face THREW: ";
					Exception ex = e;
					SoftFleshPlugin.Log(text + ((ex != null) ? ex.ToString() : null));
				}
			}
			if (GUILayout.Button("Clear face", UI.BtnDefault, new GUILayoutOption[]
			{
				GUILayout.Width(100f),
				GUILayout.Height(30f)
			}))
			{
				try
				{
					this._ctrl.ClearFaceSelected();
				}
				catch (Exception e2)
				{
					string text2 = "[SF] Clear face THREW: ";
					Exception ex2 = e2;
					SoftFleshPlugin.Log(text2 + ((ex2 != null) ? ex2.ToString() : null));
				}
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(6f);
			ChaControl[] fChars = null;
			try
			{
				fChars = this._ctrl.FaceChars();
			}
			catch
			{
			}
			if (fChars != null && fChars.Length != 0)
			{
				GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
				GUILayout.Label("Face on", UI.LblDim, new GUILayoutOption[] { GUILayout.Width(56f) });
				for (int i = 0; i < fChars.Length; i++)
				{
					ChaControl c = fChars[i];
					if (!(c == null))
					{
						bool on = false;
						try
						{
							on = this._ctrl.GetFaceEnabled(c);
						}
						catch
						{
						}
						string lbl = SoftFleshPlugin.CharLabel(c, i);
						GUIStyle st = (on ? UI.BtnPrimary : UI.BtnDefault);
						Vector2 sz = st.CalcSize(new GUIContent(lbl));
						float h = Mathf.Max(26f, sz.y);
						if (GUILayout.Button(lbl, st, new GUILayoutOption[]
						{
							GUILayout.Width(sz.x + 8f),
							GUILayout.Height(h)
						}))
						{
							try
							{
								this._ctrl.SetFaceEnabled(c, !on);
							}
							catch
							{
							}
						}
						if (SfTimeline.CanKey && GUILayout.Button("Key", UI.BtnDefault, new GUILayoutOption[]
						{
							GUILayout.Width(44f),
							GUILayout.Height(h)
						}))
						{
							this._tlKeyMsg = lbl + " face: " + SfTimeline.KeyNow(c, "faceOn");
							this._tlKeyMsgAt = Time.unscaledTime;
						}
						GUILayout.Space(6f);
					}
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Space(3f);
			}
			SoftFleshPlugin.Col2Begin();
			SoftFleshPlugin.Row("Face firmness x", SoftFleshPlugin.CfgFaceFirm, "F1", "the face's spring = body Stiffness x this");
			SoftFleshPlugin.Row("Face damping x", SoftFleshPlugin.CfgFaceDamp, "F1", "the face's damping = body Damping x this");
			SoftFleshPlugin.Row("Face wave x", SoftFleshPlugin.CfgFaceWave, "F2", "how far a touch travels across the face (1 = like the body)");
			SoftFleshPlugin.Row("Face slap x", SoftFleshPlugin.CfgFaceSlap, "F2", "the face's share of the Slap Impulse");
			SoftFleshPlugin.Row("Face max dent mm", SoftFleshPlugin.CfgFaceDepth, "F0", "the deepest a finger dents the face; a cheek is not a belly");
			SoftFleshPlugin.Row("Tilt follow x", SoftFleshPlugin.CfgTiltFollow, "F2", "how far the lip ring turns square to a slanted item (0 = stays in the face's plane)");
			SoftFleshPlugin.Col2Next();
			SoftFleshPlugin.Row("Open scale", SoftFleshPlugin.CfgMouthScale, "F2", "the lip aperture is servoed to item radius x this");
			SoftFleshPlugin.Row("Mouth reach mm", SoftFleshPlugin.CfgMouthReach, "F0", "how close an item must come to the lips to open the mouth");
			SoftFleshPlugin.Row("Lip stretch mm", SoftFleshPlugin.CfgLipDrag, "F0", "how far the lips are dragged out along a moving item");
			SoftFleshPlugin.Row("Lip push-in mm", SoftFleshPlugin.CfgLipPushIn, "F1", "how far the lips fold in along an item going in before they slip");
			SoftFleshPlugin.Row("Lip return s", SoftFleshPlugin.CfgLipReturn, "F2", "how long the dragged lips take to creep back - the skin's elasticity");
			SoftFleshPlugin.CfgLipHold.Value = UI.Switch(SoftFleshPlugin.CfgLipHold.Value, "No return");
			SoftFleshPlugin.Row("Lip damping s", SoftFleshPlugin.CfgLipDamp, "F2", "how far the lips lag the item's pull; 0 = stuck rigidly, more = the flesh trails");
			SoftFleshPlugin.CfgLipSmooth.Value = (int)UI.SliderStacked(SoftFleshPlugin.CfgLipSmooth.Definition.Key, "Lip Smooth", (float)SoftFleshPlugin.CfgLipSmooth.Value, 0f, 12f, "F0", "corrective smoothing passes on the wrapped lips", null);
			SoftFleshPlugin.Col2End();
			GUILayout.Space(4f);
			SoftFleshPlugin.CfgFaceDrag.Value = UI.Switch(SoftFleshPlugin.CfgFaceDrag.Value, "Face skin tension (the skin around a touch is drawn along with it; never the lips)");
			SoftFleshPlugin.CfgMouthAuto.Value = UI.Switch(SoftFleshPlugin.CfgMouthAuto.Value, "Mouth auto-open (an insert-mode item at the lips opens the 'kuwae' mouth to its width)");
			SoftFleshPlugin.CfgMouthTilt.Value = UI.Switch(SoftFleshPlugin.CfgMouthTilt.Value, "Opening follows the item's axis");
			GUILayout.Space(4f);
			GUILayout.Label(this._ctrl.MouthStatus(), UI.WrapDim, Array.Empty<GUILayoutOption>());
		}

		// Token: 0x06000023 RID: 35 RVA: 0x0000725C File Offset: 0x0000545C
		private void TabImpact()
		{
			SoftFleshPlugin.Col2Begin();
			SoftFleshPlugin.Row("Slap Impulse", SoftFleshPlugin.CfgSlap, "F2");
			SoftFleshPlugin.Row("Slap Soften", SoftFleshPlugin.CfgSoften, "F2");
			SoftFleshPlugin.Row("Wave Speed", SoftFleshPlugin.CfgWaveSpeed, "F2");
			SoftFleshPlugin.Col2Next();
			SoftFleshPlugin.Row("Viscosity", SoftFleshPlugin.CfgVisc, "F2");
			SoftFleshPlugin.Row("Wave Reach", SoftFleshPlugin.CfgWave, "F2");
			SoftFleshPlugin.Row("Crotch wave shield", SoftFleshPlugin.CfgWaveShield, "F2", "Slap waves fade out over the crotch/opening so a buttock impact cannot deform the entrance. 1 = fully excluded (the arm rule, graded); 0 = old behaviour. Fingers pressing there still dent normally.");
			SoftFleshPlugin.Col2End();
		}

		// Token: 0x06000024 RID: 36 RVA: 0x000072F8 File Offset: 0x000054F8
		private void TabItems()
		{
			this._ctrl.addAsInsert = UI.Switch(this._ctrl.addAsInsert, "Insert mode for next added item (stretch openings; OFF = press/dent like a fat finger)");
			SoftFleshPlugin.CfgBulgeWide.Value = UI.Switch(SoftFleshPlugin.CfgBulgeWide.Value, "Wide bulge (cylinder around the item; off = narrow depth ridge)");
			GUILayout.Space(3f);
			SoftFleshPlugin.Col2Begin();
			SoftFleshPlugin.Row("Item Radius x", SoftFleshPlugin.CfgItemRad, "F2");
			SoftFleshPlugin.Row("Item Stretch", SoftFleshPlugin.CfgItemStretch, "F2");
			SoftFleshPlugin.Row("Item squash", SoftFleshPlugin.CfgItemSquash, "F2", "The flesh pushes back on the item: its VISIBLE mesh is clamped to the mouth's measured per-angle aperture - tighter flesh = visible neck, fully-open flesh = exact fill, so no gap at any gain. 1.00 = full conformance. Fast strokes bite an extra margin (the flesh's spring lag). Colliders and tracking still see the original shape. Live, no Bake needed. The SQUASH line in track_live reports the aperture it used.");
			SoftFleshPlugin.Col2Next();
			SoftFleshPlugin.Row("Belly Bulge", SoftFleshPlugin.CfgBelly, "F2");
			SoftFleshPlugin.Row("Bulge width x", SoftFleshPlugin.CfgBellyW, "F2", "How far SIDEWAYS of the shaft each segment lifts the skin. Lower it until the ridge is as narrow as the item itself.");
			SoftFleshPlugin.Row("Bulge length x", SoftFleshPlugin.CfgBellyLen, "F2", "How far IN FROM THE ENTRANCE the ridge runs, so raising it grows the mound from the crotch toward the chest.");
			SoftFleshPlugin.Row("Bulge start (fade in)", SoftFleshPlugin.CfgBellyStart, "F2", "Where along that length the ridge begins to rise, so the mound does not start abruptly at the entrance.");
			SoftFleshPlugin.Row("Bulge follows taper", SoftFleshPlugin.CfgBellyShape, "F2", "Makes the height follow the item's own thickness at each depth instead of one height along the whole shaft.");
			SoftFleshPlugin.Col2End();
		}

		// Token: 0x06000025 RID: 37 RVA: 0x00007418 File Offset: 0x00005618
		private void TabOpening()
		{
			if (GUILayout.Button("Bake openings (3~10s)", UI.BtnBake, Array.Empty<GUILayoutOption>()))
			{
				try
				{
					this._ctrl.BakeOpenings();
				}
				catch (Exception e)
				{
					string text = "[SF] Bake THREW: ";
					Exception ex = e;
					SoftFleshPlugin.Log(text + ((ex != null) ? ex.ToString() : null));
				}
			}
			GUILayout.Space(3f);
			SoftFleshPlugin.Col2Begin();
			SoftFleshPlugin.Row("Open Width", SoftFleshPlugin.CfgOpenW, "F2");
			SoftFleshPlugin.Row("Open Depth", SoftFleshPlugin.CfgOpenD, "F2");
			SoftFleshPlugin.Row("Open Max %", SoftFleshPlugin.CfgOpenMax, "F0");
			SoftFleshPlugin.Row("Clit Hold", SoftFleshPlugin.CfgClitHold, "F2");
			SoftFleshPlugin.Row("Entrance tilt", SoftFleshPlugin.CfgEntTilt, "F2");
			SoftFleshPlugin.Col2Next();
			SoftFleshPlugin.CfgTrack.Value = UI.Switch(SoftFleshPlugin.CfgTrack.Value, "Track inserted items");
			SoftFleshPlugin.Row("Gape Hold", SoftFleshPlugin.CfgGapeHold, "F2");
			SoftFleshPlugin.Row("Suction", SoftFleshPlugin.CfgSuction, "F2");
			SoftFleshPlugin.Row("Track Follow", SoftFleshPlugin.CfgFollow, "F2");
			SoftFleshPlugin.RowMm("Bake reach", SoftFleshPlugin.CfgBakeReach);
			SoftFleshPlugin.Col2End();
		}

		// Token: 0x06000026 RID: 38 RVA: 0x00007560 File Offset: 0x00005760
		private void TabMesh()
		{
			SoftFleshPlugin.CfgSubdiv.Value = UI.Switch(SoftFleshPlugin.CfgSubdiv.Value, "Subdivide oversized faces (fixes faceting at its source)");
			SoftFleshPlugin.CfgSubBody.Value = UI.Switch(SoftFleshPlugin.CfgSubBody.Value, "Whole body - round dents wherever a hand presses (Clear + Setup to apply; ~4x verts on large faces)");
			SoftFleshPlugin.CfgSubArea.Value = UI.SliderRow("subarea", "Min face area", SoftFleshPlugin.CfgSubArea.Value, 5f, 200f, "F0", 168f, "mm2");
			SoftFleshPlugin.CfgSubLevels.Value = (int)UI.SliderRow("sublev", "Subdiv levels", (float)SoftFleshPlugin.CfgSubLevels.Value, 1f, 4f, "F0", 168f, null);
			SoftFleshPlugin.CfgSubWGap.Value = UI.SliderRow("subwgap", "Weight-boundary guard", SoftFleshPlugin.CfgSubWGap.Value, 0f, 2f, "F2", 168f, null);
			GUILayout.Label("0 = split every long edge (creases where bone weights change); 2 = never split across one", UI.WrapDim, Array.Empty<GUILayoutOption>());
			this._ctrl.weldWeights = UI.Switch(this._ctrl.weldWeights, "Weld skin weights on coincident vertices (stops bone-driven tearing)");
			SoftFleshPlugin.Row("Spread to skin", SoftFleshPlugin.CfgSpreadW, "F2");
			SoftFleshPlugin.RowMm("Spread reach", SoftFleshPlugin.CfgSpreadR);
			this._ctrl.capHoles = UI.Switch(this._ctrl.capHoles, "Cap the model's own open holes in the genital area (50 open edges shipped in the asset)");
		}

		// Token: 0x06000027 RID: 39 RVA: 0x000076D0 File Offset: 0x000058D0
		private static void DumpUiDropdowns()
		{
			StringBuilder sb = new StringBuilder(16384);
			try
			{
				Dropdown[] all = Resources.FindObjectsOfTypeAll<Dropdown>();
				sb.Append("dropdowns: ").Append(all.Length).Append("   screen ")
					.Append(Screen.width)
					.Append('x')
					.Append(Screen.height)
					.Append('\n');
				Vector3[] corners = new Vector3[4];
				foreach (Dropdown d in all)
				{
					if (!(d == null))
					{
						Transform t = d.transform;
						if (t.gameObject.scene.name != null)
						{
							StringBuilder path = new StringBuilder();
							string firstOff = null;
							float alpha = 1f;
							Transform q = t;
							while (q != null)
							{
								path.Insert(0, "/" + q.name);
								if (!q.gameObject.activeSelf && firstOff == null)
								{
									firstOff = q.name;
								}
								CanvasGroup cg = q.GetComponent<CanvasGroup>();
								if (cg != null)
								{
									alpha *= cg.alpha;
								}
								q = q.parent;
							}
							RectTransform rt = t as RectTransform;
							string rect = "-";
							if (rt != null)
							{
								rt.GetWorldCorners(corners);
								rect = string.Concat(new string[]
								{
									"(",
									corners[0].x.ToString("F0"),
									",",
									corners[0].y.ToString("F0"),
									")-(",
									corners[2].x.ToString("F0"),
									",",
									corners[2].y.ToString("F0"),
									")"
								});
							}
							Canvas root = null;
							Canvas[] cvs = d.GetComponentsInParent<Canvas>(true);
							for (int j = 0; j < cvs.Length; j++)
							{
								if (cvs[j].isRootCanvas)
								{
									root = cvs[j];
								}
							}
							bool ray = root != null && root.GetComponent<GraphicRaycaster>() != null && root.GetComponent<GraphicRaycaster>().enabled;
							bool listOpen = t.Find("Dropdown List") != null;
							sb.Append('\n').Append(path).Append('\n')
								.Append("   activeInHierarchy ")
								.Append(t.gameObject.activeInHierarchy)
								.Append((firstOff != null) ? ("  (first inactive: " + firstOff + ")") : "")
								.Append("  enabled ")
								.Append(d.enabled)
								.Append("  interactable ")
								.Append(d.interactable)
								.Append("  options ")
								.Append((d.options != null) ? d.options.Count : (-1))
								.Append("  value ")
								.Append(d.value)
								.Append("  caption '")
								.Append((d.captionText != null) ? d.captionText.text : "(none)")
								.Append("'\n")
								.Append("   rect ")
								.Append(rect)
								.Append("  lossyScale ")
								.Append(t.lossyScale.x.ToString("F2"))
								.Append("  groupAlpha ")
								.Append(alpha.ToString("F2"))
								.Append("  template ")
								.Append((d.template != null) ? d.template.name : "(NULL)")
								.Append("  rootCanvas ")
								.Append((root != null) ? string.Concat(new string[]
								{
									root.name,
									" enabled=",
									root.enabled.ToString(),
									" order=",
									root.sortingOrder.ToString(),
									" mode=",
									root.renderMode.ToString()
								}) : "(none)")
								.Append("  raycaster ")
								.Append(ray)
								.Append("  listOpen ")
								.Append(listOpen)
								.Append('\n');
							if (d.options != null)
							{
								int k = 0;
								while (k < d.options.Count && k < 12)
								{
									sb.Append("      [").Append(k).Append("] ")
										.Append(d.options[k].text)
										.Append('\n');
									k++;
								}
							}
						}
					}
				}
				int blockers = 0;
				foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
				{
					if (go != null && go.name == "Blocker" && go.scene.name != null)
					{
						blockers++;
					}
				}
				sb.Append("\nBlocker objects alive: ").Append(blockers).Append('\n');
				EventSystem es = EventSystem.current;
				sb.Append("EventSystem: ").Append((es != null) ? string.Concat(new string[]
				{
					es.name,
					" enabled=",
					es.enabled.ToString(),
					" module=",
					(es.currentInputModule != null) ? es.currentInputModule.GetType().Name : "(none)"
				}) : "(NONE)").Append('\n');
				string dd = Path.Combine(Paths.GameRootPath, "BepInEx/SoftFlesh_diag");
				if (!Directory.Exists(dd))
				{
					Directory.CreateDirectory(dd);
				}
				File.WriteAllText(Path.Combine(dd, "ui_dropdowns.txt"), sb.ToString());
				SoftFleshPlugin._uiDumpMsg = "wrote ui_dropdowns.txt (" + all.Length.ToString() + " dropdowns)";
			}
			catch (Exception e)
			{
				SoftFleshPlugin._uiDumpMsg = "dump failed: " + e.Message;
			}
		}

		// Token: 0x06000028 RID: 40 RVA: 0x00007D5C File Offset: 0x00005F5C
		private void TabDiag()
		{
			if (GUILayout.Button("Dump UI dropdowns (select the character and open its State panel first)", UI.BtnLeft, new GUILayoutOption[] { GUILayout.Height(28f) }))
			{
				SoftFleshPlugin.DumpUiDropdowns();
			}
			if (!string.IsNullOrEmpty(SoftFleshPlugin._uiDumpMsg))
			{
				GUILayout.Label(SoftFleshPlugin._uiDumpMsg, UI.WrapDim, Array.Empty<GUILayoutOption>());
			}
			if (SoftFleshPlugin.Section("Live", ref SoftFleshPlugin._dgLive))
			{
				if (GUILayout.Button("Snapshot bones (press with the toggle you are testing OFF)", UI.BtnLeft, new GUILayoutOption[] { GUILayout.Height(30f) }))
				{
					this._ctrl.boneSnapReq = true;
				}
				GUILayout.Label(SoftFleshController.BoneDiff, UI.WrapDim, Array.Empty<GUILayoutOption>());
				GUILayout.Label(SoftFleshController.MeshDiff, UI.WrapDim, Array.Empty<GUILayoutOption>());
				this.FrameBudget();
				GUILayout.Label(string.Concat(new string[]
				{
					"hitches: ",
					SoftFleshController.HitchTotal.ToString(),
					" total, ",
					SoftFleshController.HitchPerMin.ToString("F0"),
					"/min, worst ",
					SoftFleshController.HitchWorstMs.ToString("F0"),
					"ms in ",
					SoftFleshController.HitchPhase
				}), UI.Box, Array.Empty<GUILayoutOption>());
				GUILayout.Label(string.Concat(new string[]
				{
					"near: ",
					(this._ctrl.nearDist < 0f) ? "-" : ((this._ctrl.nearDist * 1000f).ToString("F0") + "mm"),
					"   contact verts: ",
					this._ctrl.activeVerts.ToString(),
					"   bake this frame: ",
					this._ctrl.didBake ? "yes" : "skipped",
					"   item colliders: ",
					this._ctrl.ItemColliderCount.ToString()
				}), UI.Box, Array.Empty<GUILayoutOption>());
				if (!string.IsNullOrEmpty(SoftFleshPlugin._sSolve))
				{
					GUILayout.Label(SoftFleshPlugin._sSolve, UI.Box, Array.Empty<GUILayoutOption>());
				}
				if (SoftFleshPlugin.CfgTrack.Value && SoftFleshPlugin._sItems > 0)
				{
					GUILayout.Label(string.Concat(new string[]
					{
						"measured at the entrance",
						this._ctrl.planeExact ? "" : " (FALLBACK - nothing spans the plane)",
						": item half-section ",
						this._ctrl.trkRadMm.ToString("F1"),
						"mm, inserted ",
						this._ctrl.trkDepthMm.ToString("F1"),
						"mm, off-axis ",
						this._ctrl.trkLatMm.ToString("F1"),
						"mm | opening now ",
						(this._ctrl.trkRimMm < 0f) ? "n/a" : (this._ctrl.trkRimMm.ToString("F1") + "mm"),
						" | entrance tilt ",
						(!this._ctrl.trkTiltReady) ? "needs a re-bake" : (this._ctrl.trkTiltDeg.ToString("F1") + " deg -> " + this._ctrl.trkTiltMm.ToString("F1") + "mm of flesh"),
						" | wall ",
						SoftFleshController.AimRailMm.ToString("F0"),
						"mm  ->  Open Width ",
						this._ctrl.trkW.ToString("F2"),
						" (of the bake's 50mm ceiling)"
					}), UI.Box, Array.Empty<GUILayoutOption>());
				}
				if (SoftFleshPlugin.CfgTrack.Value && SoftFleshPlugin._sItems > 0)
				{
					GUILayout.Label(string.Concat(new string[]
					{
						"track: W=",
						this._ctrl.trkW.ToString("F2"),
						" D=",
						this._ctrl.trkD.ToString("F2"),
						" gape=",
						this._ctrl.trkG.ToString("F2"),
						" suck=",
						(this._ctrl.trkS * 1000f).ToString("+0.0;-0.0"),
						"mm",
						(this._ctrl.trkOff >= 0f) ? (" off=" + (this._ctrl.trkOff * 1000f).ToString("F0") + "mm") : ""
					}), UI.Box, Array.Empty<GUILayoutOption>());
				}
				GUILayout.Label(this._ctrl.lastStatus, UI.Box, Array.Empty<GUILayoutOption>());
			}
			if (SoftFleshPlugin.Section("Setup & bake report", ref SoftFleshPlugin._dgSetup))
			{
				GUILayout.Label(SoftFleshPlugin._sBake, UI.Box, Array.Empty<GUILayoutOption>());
				if (!string.IsNullOrEmpty(this._ctrl.rigInfo))
				{
					GUILayout.Label(this._ctrl.rigInfo, UI.Box, Array.Empty<GUILayoutOption>());
				}
				if (!string.IsNullOrEmpty(SoftFleshPlugin._sSubdiv))
				{
					GUILayout.Label(SoftFleshPlugin._sSubdiv, UI.Box, Array.Empty<GUILayoutOption>());
				}
				if (!string.IsNullOrEmpty(this._ctrl.weldInfo))
				{
					GUILayout.Label(this._ctrl.weldInfo, UI.Box, Array.Empty<GUILayoutOption>());
				}
				if (!string.IsNullOrEmpty(SoftFleshPlugin._sDiag))
				{
					GUILayout.Label(SoftFleshPlugin._sDiag, UI.Box, Array.Empty<GUILayoutOption>());
				}
			}
			if (SoftFleshPlugin.Section("Canal guide", ref SoftFleshPlugin._dgGuide))
			{
				GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
				if (GUILayout.Button("Set canal from selected item (its green Y = into the canal)", UI.BtnLeft, Array.Empty<GUILayoutOption>()))
				{
					this._ctrl.CaptureCanalGuide();
				}
				if (GUILayout.Button("Clear guide", UI.BtnDefault, new GUILayoutOption[] { GUILayout.Width(90f) }))
				{
					this._ctrl.ClearCanalGuide();
				}
				GUILayout.EndHorizontal();
				GUILayout.Label(string.Concat(new string[]
				{
					"axis in use: entry ",
					this._ctrl.savedEntryB.ToString("F3"),
					"  dir ",
					this._ctrl.savedDirB.ToString("F2"),
					this._ctrl.GuideArmed ? "   [guide captured - press Bake to apply]" : "   [no guide; using the saved calibration]"
				}), UI.Box, Array.Empty<GUILayoutOption>());
				this._ctrl.monitorGuide = UI.Switch(this._ctrl.monitorGuide, "Live monitor the selected item (on-screen + guide_live.txt, no capture needed)");
				if (!string.IsNullOrEmpty(SoftFleshPlugin._sGuide))
				{
					GUILayout.Label(SoftFleshPlugin._sGuide, UI.Box, Array.Empty<GUILayoutOption>());
				}
				if (!string.IsNullOrEmpty(SoftFleshPlugin._sGuideL))
				{
					GUILayout.Label(SoftFleshPlugin._sGuideL, UI.Box, Array.Empty<GUILayoutOption>());
				}
			}
			if (SoftFleshPlugin.Section("Tools", ref SoftFleshPlugin._dgTools))
			{
				if (GUILayout.Button("Export diagnostics (mesh + stats to disk)", UI.BtnLeft, Array.Empty<GUILayoutOption>()))
				{
					this._ctrl.ExportDiagnostics();
				}
				if (GUILayout.Button("Export FACE of selected character (mesh, bones, blend shapes)", UI.BtnLeft, Array.Empty<GUILayoutOption>()))
				{
					try
					{
						this._ctrl.ExportFace();
					}
					catch (Exception e)
					{
						string text = "[SF] face export THREW: ";
						Exception ex = e;
						SoftFleshPlugin.Log(text + ((ex != null) ? ex.ToString() : null));
					}
				}
				if (GUILayout.Button("Survey clothing (measure only -> cloth_survey.txt)", UI.BtnLeft, Array.Empty<GUILayoutOption>()))
				{
					this._ctrl.SurveyClothing();
				}
				GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
				if (GUILayout.Button("Dump ring", UI.BtnDefault, Array.Empty<GUILayoutOption>()))
				{
					this._ctrl.ringDump = true;
				}
				if (GUILayout.Button("Dump rig", UI.BtnDefault, Array.Empty<GUILayoutOption>()))
				{
					this._ctrl.DumpRig();
					this._ctrl.bakeStatus = this._ctrl.rigDumpInfo;
				}
				GUILayout.EndHorizontal();
				this._ctrl.showBulge = UI.Switch(this._ctrl.showBulge, "Show belly-bulge footprint (a dot per pushed vertex, sized by strength)");
				this._ctrl.trackLog = UI.Switch(this._ctrl.trackLog, "Write track_live.txt (4x/sec)");
				this._ctrl.gripFreeze = UI.Switch(this._ctrl.gripFreeze, "Freeze steady-held flesh (diagnostic: still shimmers = lighting, not flesh)");
			}
			if (SoftFleshPlugin.Section("Rarely used", ref SoftFleshPlugin._dgRare))
			{
				GUILayout.Label("Parked out of the daily workflow. The top three WORK and stay at their defaults (on/on/on); the rest are knobs measurement cleared of any effect on the artifact they were added for. Nothing here needs touching in normal use.", UI.Box, Array.Empty<GUILayoutOption>());
				SoftFleshPlugin.CfgSecondSkin.Value = UI.Switch(SoftFleshPlugin.CfgSecondSkin.Value, "Second skin (tight garments rebuilt on the body mesh; OFF sends every garment down the transfer path - the A/B that says which path a bug belongs to)");
				SoftFleshPlugin.CfgSpanGuard.Value = UI.Switch(SoftFleshPlugin.CfgSpanGuard.Value, "Cloth penetration guard (lifts a garment only where a WAVE pushes flesh through it; an authored intersection is left as drawn)");
				GUILayout.Space(3f);
				this._ctrl.seamRepair = UI.Switch(this._ctrl.seamRepair, "Seam repair (closes the under-breast crack; turn OFF to check its effect on impact feel)");
				SoftFleshPlugin.CfgCrotchIso.Value = UI.Switch(SoftFleshPlugin.CfgCrotchIso.Value, "KKPE Crotch: keep the opening still (off = it rides the pelvis)");
				SoftFleshPlugin.CfgTaper.Value = UI.Switch(SoftFleshPlugin.CfgTaper.Value, "Canal follows the item's taper (off = one width from mouth to tip)");
				GUILayout.Space(3f);
				this._ctrl.openNormals = UI.Switch(this._ctrl.openNormals, "Recompute normals around the opening");
				SoftFleshPlugin.Row("Spread mm", SoftFleshPlugin.CfgSpread, "F0");
				SoftFleshPlugin.Row("Width trim +/-", SoftFleshPlugin.CfgWidthTrim, "F2");
				SoftFleshPlugin.Row("Push the wall only x", SoftFleshPlugin.CfgWallOnly, "F2");
				this._ctrl.canalCollider = UI.Switch(this._ctrl.canalCollider, "Canal containment (aims, does not dent)");
				if (!string.IsNullOrEmpty(SoftFleshPlugin._sDiagSt))
				{
					GUILayout.Label(SoftFleshPlugin._sDiagSt, UI.Box, Array.Empty<GUILayoutOption>());
				}
			}
			if (SoftFleshPlugin.Section("Legacy petal-DynamicBone modes", ref SoftFleshPlugin._dgLegacy))
			{
				GUILayout.Label("Both are inert while Track items is on: tracking already keeps the item colliders off the petal chains, and the opening is delivered by the baked field rather than by the petal rig. They only take effect with tracking turned off.", UI.Box, Array.Empty<GUILayoutOption>());
				this._ctrl.boneOpening = UI.Switch(this._ctrl.boneOpening, "Bone-driven opening: inserted items push the petal DynamicBone chains");
				this._ctrl.restPose = UI.Switch(this._ctrl.restPose, "Drive the DynamicBone REST pose (off = overwrite its output)");
			}
		}

		// Token: 0x06000029 RID: 41 RVA: 0x00008790 File Offset: 0x00006990
		private void FrameBudget()
		{
			double[] ms = SoftFleshController.PhaseMs;
			double named = 0.0;
			for (int i = 1; i < ms.Length; i++)
			{
				named += ms[i];
			}
			double other = ms[0] - named;
			if (other < 0.0)
			{
				other = 0.0;
			}
			double[] v = new double[]
			{
				ms[1],
				ms[2],
				ms[3],
				ms[4],
				ms[5],
				ms[6],
				ms[7],
				ms[8],
				ms[9],
				ms[10],
				other
			};
			double total = 0.0;
			for (int j = 0; j < v.Length; j++)
			{
				total += v[j];
			}
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Label(ms[0].ToString("F1") + "ms", UI.MonoAmber, new GUILayoutOption[] { GUILayout.Width(46f) });
			Rect bar = GUILayoutUtility.GetRect(1f, 8f, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
			UI.Fill(bar, UI.BgTrack);
			if (total > 1E-06)
			{
				float x = bar.x;
				for (int k = 0; k < v.Length; k++)
				{
					float w = (float)(v[k] / total) * bar.width;
					if (w > 0.5f)
					{
						UI.Fill(new Rect(x, bar.y, w, bar.height), UI.Phase[k]);
					}
					x += w;
				}
			}
			GUILayout.Label("frame budget", UI.LblFaint, new GUILayoutOption[] { GUILayout.Width(76f) });
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			for (int l = 0; l < v.Length; l++)
			{
				Rect sw = GUILayoutUtility.GetRect(7f, 7f, new GUILayoutOption[] { GUILayout.Width(7f) });
				UI.Fill(new Rect(sw.x, sw.y + 3f, 7f, 7f), UI.Phase[l]);
				GUILayout.Label(SoftFleshPlugin.PhaseNames[l] + " " + v[l].ToString("F1"), UI.MonoDim, new GUILayoutOption[] { GUILayout.Width(60f) });
			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			if (SoftFleshController.FieldPushSkipped || SoftFleshController.FieldDentSkipped)
			{
				GUILayout.Label("field skipped:" + (SoftFleshController.FieldPushSkipped ? " push(12 passes)" : "") + (SoftFleshController.FieldDentSkipped ? " dent(32 passes)" : ""), UI.MonoDim, new GUILayoutOption[] { GUILayout.Width(230f) });
			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Label("alloc KB/frame:", UI.MonoDim, new GUILayoutOption[] { GUILayout.Width(96f) });
			double[] kb = SoftFleshController.PhaseKB;
			for (int m = 1; m < kb.Length; m++)
			{
				if (kb[m] > 0.5)
				{
					GUILayout.Label(SoftFleshPlugin.PhaseNames[m - 1] + " " + kb[m].ToString("F0"), UI.MonoDim, new GUILayoutOption[] { GUILayout.Width(66f) });
				}
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(2f);
		}

		// Token: 0x0600002A RID: 42 RVA: 0x00008B30 File Offset: 0x00006D30
		private static ConfigEntry<float>[] TabKnobs(string id)
		{
			if (id == "tabContact")
			{
				return new ConfigEntry<float>[]
				{
					SoftFleshPlugin.CfgTouchRadius,
					SoftFleshPlugin.CfgMaxDepth,
					SoftFleshPlugin.CfgKnuckle,
					SoftFleshPlugin.CfgStiffness,
					SoftFleshPlugin.CfgDamping,
					SoftFleshPlugin.CfgBulge,
					SoftFleshPlugin.CfgSmooth,
					SoftFleshPlugin.CfgTension,
					SoftFleshPlugin.CfgBend,
					SoftFleshPlugin.CfgSmoothShade,
					SoftFleshPlugin.CfgShadow,
					SoftFleshPlugin.CfgShadeTau,
					SoftFleshPlugin.CfgNipFirm,
					SoftFleshPlugin.CfgClothLift
				};
			}
			if (id == "tabImpact")
			{
				return new ConfigEntry<float>[]
				{
					SoftFleshPlugin.CfgSlap,
					SoftFleshPlugin.CfgSoften,
					SoftFleshPlugin.CfgWaveSpeed,
					SoftFleshPlugin.CfgVisc,
					SoftFleshPlugin.CfgWave,
					SoftFleshPlugin.CfgWaveShield
				};
			}
			if (id == "tabItems")
			{
				return new ConfigEntry<float>[]
				{
					SoftFleshPlugin.CfgItemRad,
					SoftFleshPlugin.CfgItemStretch,
					SoftFleshPlugin.CfgItemSquash,
					SoftFleshPlugin.CfgBelly,
					SoftFleshPlugin.CfgBellyW,
					SoftFleshPlugin.CfgBellyLen,
					SoftFleshPlugin.CfgBellyStart,
					SoftFleshPlugin.CfgBellyShape
				};
			}
			if (id == "tabFace")
			{
				return new ConfigEntry<float>[]
				{
					SoftFleshPlugin.CfgFaceFirm,
					SoftFleshPlugin.CfgFaceDamp,
					SoftFleshPlugin.CfgFaceWave,
					SoftFleshPlugin.CfgFaceSlap,
					SoftFleshPlugin.CfgFaceDepth,
					SoftFleshPlugin.CfgTiltFollow,
					SoftFleshPlugin.CfgMouthScale,
					SoftFleshPlugin.CfgMouthReach,
					SoftFleshPlugin.CfgLipDrag,
					SoftFleshPlugin.CfgLipPushIn,
					SoftFleshPlugin.CfgLipReturn,
					SoftFleshPlugin.CfgLipDamp
				};
			}
			if (!(id == "tabOpening"))
			{
				return new ConfigEntry<float>[0];
			}
			return new ConfigEntry<float>[]
			{
				SoftFleshPlugin.CfgOpenW,
				SoftFleshPlugin.CfgOpenD,
				SoftFleshPlugin.CfgOpenMax,
				SoftFleshPlugin.CfgClitHold,
				SoftFleshPlugin.CfgEntTilt,
				SoftFleshPlugin.CfgGapeHold,
				SoftFleshPlugin.CfgSuction,
				SoftFleshPlugin.CfgFollow,
				SoftFleshPlugin.CfgBakeReach
			};
		}

		// Token: 0x0600002B RID: 43 RVA: 0x00008D4C File Offset: 0x00006F4C
		internal static Dictionary<string, float> TabValues(string id)
		{
			Dictionary<string, float> d = new Dictionary<string, float>();
			foreach (ConfigEntry<float> i in SoftFleshPlugin.TabKnobs(id))
			{
				if (i != null)
				{
					d[i.Definition.Key] = i.Value;
				}
			}
			if (id == "tabFace" && SoftFleshPlugin.CfgLipSmooth != null)
			{
				d[SoftFleshPlugin.CfgLipSmooth.Definition.Key] = (float)SoftFleshPlugin.CfgLipSmooth.Value;
			}
			return d;
		}

		// Token: 0x0600002C RID: 44 RVA: 0x00008DC8 File Offset: 0x00006FC8
		internal static void ApplyTabValues(string id, Dictionary<string, float> v)
		{
			SoftFleshPlugin self = SoftFleshPlugin._live;
			if (self == null || v == null)
			{
				return;
			}
			ConfigFile cf = self.Config;
			bool save = cf.SaveOnConfigSet;
			cf.SaveOnConfigSet = false;
			try
			{
				float f;
				foreach (ConfigEntry<float> i in SoftFleshPlugin.TabKnobs(id))
				{
					if (i != null && v.TryGetValue(i.Definition.Key, out f))
					{
						AcceptableValueRange<float> r = i.Description.AcceptableValues as AcceptableValueRange<float>;
						if (r != null)
						{
							f = Mathf.Clamp(f, r.MinValue, r.MaxValue);
						}
						if (i.Value != f)
						{
							i.Value = f;
						}
					}
				}
				if (id == "tabFace" && SoftFleshPlugin.CfgLipSmooth != null && v.TryGetValue(SoftFleshPlugin.CfgLipSmooth.Definition.Key, out f))
				{
					AcceptableValueRange<int> ri = SoftFleshPlugin.CfgLipSmooth.Description.AcceptableValues as AcceptableValueRange<int>;
					int iv = Mathf.RoundToInt(f);
					if (ri != null)
					{
						iv = Mathf.Clamp(iv, ri.MinValue, ri.MaxValue);
					}
					if (SoftFleshPlugin.CfgLipSmooth.Value != iv)
					{
						SoftFleshPlugin.CfgLipSmooth.Value = iv;
					}
				}
			}
			finally
			{
				cf.SaveOnConfigSet = save;
			}
		}

		// Token: 0x0600002D RID: 45 RVA: 0x00008F14 File Offset: 0x00007114
		private ConfigEntry<float>[] Knobs()
		{
			return new ConfigEntry<float>[]
			{
				SoftFleshPlugin.CfgTouchRadius,
				SoftFleshPlugin.CfgMaxDepth,
				SoftFleshPlugin.CfgKnuckle,
				SoftFleshPlugin.CfgStiffness,
				SoftFleshPlugin.CfgDamping,
				SoftFleshPlugin.CfgBulge,
				SoftFleshPlugin.CfgSmooth,
				SoftFleshPlugin.CfgTension,
				SoftFleshPlugin.CfgBend,
				SoftFleshPlugin.CfgSmoothShade,
				SoftFleshPlugin.CfgShadow,
				SoftFleshPlugin.CfgNipFirm,
				SoftFleshPlugin.CfgSlap,
				SoftFleshPlugin.CfgSoften,
				SoftFleshPlugin.CfgWaveSpeed,
				SoftFleshPlugin.CfgVisc,
				SoftFleshPlugin.CfgWave,
				SoftFleshPlugin.CfgItemRad,
				SoftFleshPlugin.CfgItemStretch,
				SoftFleshPlugin.CfgItemSquash,
				SoftFleshPlugin.CfgWaveShield,
				SoftFleshPlugin.CfgBelly,
				SoftFleshPlugin.CfgOpenW,
				SoftFleshPlugin.CfgOpenD,
				SoftFleshPlugin.CfgOpenMax,
				SoftFleshPlugin.CfgClitHold,
				SoftFleshPlugin.CfgGapeHold,
				SoftFleshPlugin.CfgSuction,
				SoftFleshPlugin.CfgFollow,
				SoftFleshPlugin.CfgSpread,
				SoftFleshPlugin.CfgBellyW,
				SoftFleshPlugin.CfgBellyLen,
				SoftFleshPlugin.CfgBellyStart,
				SoftFleshPlugin.CfgBellyShape,
				SoftFleshPlugin.CfgEntTilt,
				SoftFleshPlugin.CfgBakeReach,
				SoftFleshPlugin.CfgSpreadW,
				SoftFleshPlugin.CfgSpreadR,
				SoftFleshPlugin.CfgWidthTrim,
				SoftFleshPlugin.CfgWallOnly,
				SoftFleshPlugin.CfgFaceFirm,
				SoftFleshPlugin.CfgFaceDamp,
				SoftFleshPlugin.CfgFaceWave,
				SoftFleshPlugin.CfgFaceSlap,
				SoftFleshPlugin.CfgFaceDepth,
				SoftFleshPlugin.CfgMouthScale,
				SoftFleshPlugin.CfgMouthReach,
				SoftFleshPlugin.CfgLipDrag,
				SoftFleshPlugin.CfgLipReturn,
				SoftFleshPlugin.CfgLipDamp,
				SoftFleshPlugin.CfgLipPushIn,
				SoftFleshPlugin.CfgTiltFollow,
				SoftFleshPlugin.CfgShadeTau
			};
		}

		// Token: 0x0600002E RID: 46 RVA: 0x000090FC File Offset: 0x000072FC
		private static bool SlotUsed(int i)
		{
			return i >= 0 && i < 12 && !string.IsNullOrEmpty(SoftFleshPlugin.CfgPreset[i].Value);
		}

		// Token: 0x0600002F RID: 47 RVA: 0x00009120 File Offset: 0x00007320
		private static string SlotLabel(int i)
		{
			string j = ((SoftFleshPlugin.CfgPresetName[i] != null) ? SoftFleshPlugin.CfgPresetName[i].Value : null);
			if (!string.IsNullOrEmpty(j))
			{
				return j;
			}
			return "Slot " + (i + 1).ToString();
		}

		// Token: 0x06000030 RID: 48 RVA: 0x00009168 File Offset: 0x00007368
		private static int SlotCount()
		{
			int i = 0;
			for (int j = 0; j < 12; j++)
			{
				if (SoftFleshPlugin.SlotUsed(j))
				{
					i++;
				}
			}
			return i;
		}

		// Token: 0x06000031 RID: 49 RVA: 0x00009194 File Offset: 0x00007394
		private static int NextFreeSlot()
		{
			for (int i = 0; i < 12; i++)
			{
				if (!SoftFleshPlugin.SlotUsed(i))
				{
					return i;
				}
			}
			return -1;
		}

		// Token: 0x06000032 RID: 50 RVA: 0x000091BC File Offset: 0x000073BC
		private void DeleteSlot(int i)
		{
			SoftFleshPlugin.CfgPreset[i].Value = "";
			SoftFleshPlugin.CfgPresetName[i].Value = ((i < SoftFleshPlugin.PresetNames.Length) ? SoftFleshPlugin.PresetNames[i] : ("Slot " + (i + 1).ToString()));
			if (SoftFleshPlugin._preset == i)
			{
				SoftFleshPlugin._preset = -1;
			}
			if (SoftFleshPlugin._confirmDelete == i)
			{
				SoftFleshPlugin._confirmDelete = -1;
			}
			SoftFleshPlugin.Toast("Deleted slot " + (i + 1).ToString());
			SoftFleshPlugin.Log("[SF] preset slot " + (i + 1).ToString() + " deleted");
		}

		// Token: 0x06000033 RID: 51 RVA: 0x00009264 File Offset: 0x00007464
		private string PackParams()
		{
			StringBuilder sb = new StringBuilder();
			CultureInfo ci = CultureInfo.InvariantCulture;
			foreach (ConfigEntry<float> i in this.Knobs())
			{
				sb.Append(i.Definition.Key).Append('=').Append(i.Value.ToString("R", ci))
					.Append(';');
			}
			sb.Append("tWide=").Append((SoftFleshPlugin.CfgBulgeWide.Value > false) ? 1 : 0).Append(';');
			sb.Append("tTaper=").Append((SoftFleshPlugin.CfgTaper.Value > false) ? 1 : 0).Append(';');
			sb.Append("tCrotch=").Append((SoftFleshPlugin.CfgCrotchIso.Value > false) ? 1 : 0).Append(';');
			sb.Append("tTrack=").Append((SoftFleshPlugin.CfgTrack.Value > false) ? 1 : 0).Append(';');
			sb.Append("tInsert=").Append((this._ctrl.addAsInsert > false) ? 1 : 0).Append(';');
			sb.Append("tSeam=").Append((this._ctrl.seamRepair > false) ? 1 : 0).Append(';');
			sb.Append("tNorm=").Append((this._ctrl.openNormals > false) ? 1 : 0).Append(';');
			sb.Append("tFaceTension=").Append((SoftFleshPlugin.CfgFaceDrag.Value > false) ? 1 : 0).Append(';');
			sb.Append("tMouthAuto=").Append((SoftFleshPlugin.CfgMouthAuto.Value > false) ? 1 : 0).Append(';');
			sb.Append("tMouthTilt=").Append((SoftFleshPlugin.CfgMouthTilt.Value > false) ? 1 : 0).Append(';');
			sb.Append("tLipHold=").Append((SoftFleshPlugin.CfgLipHold.Value > false) ? 1 : 0).Append(';');
			sb.Append("iLipSmooth=").Append(SoftFleshPlugin.CfgLipSmooth.Value).Append(';');
			return sb.ToString();
		}

		// Token: 0x06000034 RID: 52 RVA: 0x00009498 File Offset: 0x00007698
		private void SavePreset(int i)
		{
			SoftFleshPlugin.CfgPreset[i].Value = this.PackParams();
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] preset '",
				SoftFleshPlugin.SlotLabel(i),
				"' saved (",
				this.Knobs().Length.ToString(),
				" values)"
			}));
		}

		// Token: 0x06000035 RID: 53 RVA: 0x000094FC File Offset: 0x000076FC
		private void LoadPreset(int i)
		{
			string blob = SoftFleshPlugin.CfgPreset[i].Value;
			if (string.IsNullOrEmpty(blob))
			{
				SoftFleshPlugin.Toast("\"" + SoftFleshPlugin.SlotLabel(i) + "\" is empty - press Save to fill it");
				SoftFleshPlugin.Log("[SF] preset '" + SoftFleshPlugin.SlotLabel(i) + "' is empty - press Save to fill it");
				return;
			}
			int j = this.UnpackParams(blob);
			SoftFleshPlugin.Toast(string.Concat(new string[]
			{
				"Loaded \"",
				SoftFleshPlugin.PresetNames[i],
				"\" (",
				j.ToString(),
				" values)"
			}));
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] preset '",
				SoftFleshPlugin.PresetNames[i],
				"' loaded (",
				j.ToString(),
				" values)"
			}));
		}

		// Token: 0x06000036 RID: 54 RVA: 0x000095D4 File Offset: 0x000077D4
		private int UnpackParams(string blob)
		{
			if (string.IsNullOrEmpty(blob) || this._ctrl == null)
			{
				return 0;
			}
			Dictionary<string, float> map = new Dictionary<string, float>();
			CultureInfo ci = CultureInfo.InvariantCulture;
			foreach (string part in blob.Split(new char[] { ';' }))
			{
				if (part.Length != 0)
				{
					int eq = part.IndexOf('=');
					float f;
					if (eq > 0 && float.TryParse(part.Substring(eq + 1), NumberStyles.Float, ci, out f))
					{
						map[part.Substring(0, eq)] = f;
					}
				}
			}
			int i = 0;
			foreach (ConfigEntry<float> j in this.Knobs())
			{
				float f2;
				if (map.TryGetValue(j.Definition.Key, out f2))
				{
					AcceptableValueRange<float> r = j.Description.AcceptableValues as AcceptableValueRange<float>;
					j.Value = ((r != null) ? Mathf.Clamp(f2, r.MinValue, r.MaxValue) : f2);
					i++;
				}
			}
			float t;
			if (map.TryGetValue("tWide", out t))
			{
				SoftFleshPlugin.CfgBulgeWide.Value = t > 0.5f;
			}
			if (map.TryGetValue("tTaper", out t))
			{
				SoftFleshPlugin.CfgTaper.Value = t > 0.5f;
			}
			if (map.TryGetValue("tCrotch", out t))
			{
				SoftFleshPlugin.CfgCrotchIso.Value = t > 0.5f;
			}
			if (map.TryGetValue("tTrack", out t))
			{
				SoftFleshPlugin.CfgTrack.Value = t > 0.5f;
			}
			if (map.TryGetValue("tInsert", out t))
			{
				this._ctrl.addAsInsert = t > 0.5f;
			}
			if (map.TryGetValue("tSeam", out t))
			{
				this._ctrl.seamRepair = t > 0.5f;
			}
			if (map.TryGetValue("tNorm", out t))
			{
				this._ctrl.openNormals = t > 0.5f;
			}
			if (map.TryGetValue("tFaceTension", out t))
			{
				SoftFleshPlugin.CfgFaceDrag.Value = t > 0.5f;
			}
			if (map.TryGetValue("tMouthAuto", out t))
			{
				SoftFleshPlugin.CfgMouthAuto.Value = t > 0.5f;
			}
			if (map.TryGetValue("tMouthTilt", out t))
			{
				SoftFleshPlugin.CfgMouthTilt.Value = t > 0.5f;
			}
			if (map.TryGetValue("tLipHold", out t))
			{
				SoftFleshPlugin.CfgLipHold.Value = t > 0.5f;
			}
			if (map.TryGetValue("iLipSmooth", out t))
			{
				AcceptableValueRange<int> ri = SoftFleshPlugin.CfgLipSmooth.Description.AcceptableValues as AcceptableValueRange<int>;
				int v = Mathf.RoundToInt(t);
				SoftFleshPlugin.CfgLipSmooth.Value = ((ri != null) ? Mathf.Clamp(v, ri.MinValue, ri.MaxValue) : v);
				i++;
			}
			return i;
		}

		// Token: 0x06000037 RID: 55 RVA: 0x000098B4 File Offset: 0x00007AB4
		private static float F(Dictionary<string, object> d, string k, float def)
		{
			object v;
			if (!d.TryGetValue(k, out v) || v == null)
			{
				return def;
			}
			return Convert.ToSingle(v);
		}

		// Token: 0x06000038 RID: 56 RVA: 0x000098D8 File Offset: 0x00007AD8
		private static int I(Dictionary<string, object> d, string k, int def)
		{
			object v;
			if (!d.TryGetValue(k, out v) || v == null)
			{
				return def;
			}
			return Convert.ToInt32(v);
		}

		// Token: 0x06000039 RID: 57 RVA: 0x000098FC File Offset: 0x00007AFC
		private static bool B(Dictionary<string, object> d, string k, bool def)
		{
			object v;
			if (!d.TryGetValue(k, out v) || v == null)
			{
				return def;
			}
			return Convert.ToBoolean(v);
		}

		// Token: 0x0600003A RID: 58 RVA: 0x00009920 File Offset: 0x00007B20
		private static string S(Dictionary<string, object> d, string k, string def)
		{
			object v;
			if (!d.TryGetValue(k, out v) || v == null)
			{
				return def;
			}
			return v.ToString();
		}

		// Token: 0x0600003B RID: 59 RVA: 0x00009944 File Offset: 0x00007B44
		internal static Dictionary<string, object> CollectSceneData()
		{
			SoftFleshPlugin p = SoftFleshPlugin._live;
			if (p == null || p._ctrl == null)
			{
				return null;
			}
			Dictionary<string, object> d = new Dictionary<string, object>();
			d["params"] = p.PackParams();
			ChaControl[] chars = p._ctrl.SetupChars();
			int i = 0;
			foreach (ChaControl cha in chars)
			{
				if (!(cha == null))
				{
					ObjectCtrlInfo oci = SfTimeline.OciOf(cha);
					if (oci != null && oci.objectInfo != null)
					{
						string k = "c" + i.ToString() + ".";
						d[k + "key"] = oci.objectInfo.dicKey;
						d[k + "on"] = p._ctrl.GetCharEnabled(cha);
						d[k + "baked"] = p._ctrl.GetCharBaked(cha);
						d[k + "face"] = p._ctrl.HasFace(cha);
						d[k + "faceOn"] = p._ctrl.GetFaceEnabled(cha);
						i++;
					}
				}
			}
			d["n"] = i;
			int ni = 0;
			global::Studio.Studio st = Singleton<global::Studio.Studio>.Instance;
			if (st != null)
			{
				foreach (KeyValuePair<Transform, bool> kv in p._ctrl.ItemColliders())
				{
					int key = -1;
					foreach (KeyValuePair<int, ObjectCtrlInfo> oc in st.dicObjectCtrl)
					{
						OCIItem it = oc.Value as OCIItem;
						if (it != null && it.objectItem != null && it.objectItem.transform == kv.Key)
						{
							key = oc.Key;
							break;
						}
					}
					if (key >= 0)
					{
						d["i" + ni.ToString() + ".key"] = key;
						d["i" + ni.ToString() + ".insert"] = kv.Value;
						ni++;
					}
				}
			}
			d["ni"] = ni;
			d["baked"] = p._ctrl.bakeStatus != null && p._ctrl.bakeStatus.StartsWith("baked");
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] scene: saved block (",
				i.ToString(),
				" character(s), ",
				ni.ToString(),
				" item collider(s)",
				((bool)d["baked"]) ? ", baked" : "",
				")"
			}));
			return d;
		}

		// Token: 0x0600003C RID: 60 RVA: 0x00009CAC File Offset: 0x00007EAC
		internal static void SceneClear()
		{
			SoftFleshPlugin p = SoftFleshPlugin._live;
			if (p != null && p._ctrl != null)
			{
				p._ctrl.ClearAll();
				p._ctrl.ClearItemColliders();
			}
		}

		// Token: 0x0600003D RID: 61 RVA: 0x00009CEC File Offset: 0x00007EEC
		private static void ApplySceneDataOp(Dictionary<string, object> d, ReadOnlyDictionary<int, ObjectCtrlInfo> loaded, int op)
		{
			SoftFleshPlugin.ApplySceneData(d, loaded, op);
		}

		// Token: 0x0600003E RID: 62 RVA: 0x00009CF8 File Offset: 0x00007EF8
		internal static void ApplySceneData(Dictionary<string, object> d, ReadOnlyDictionary<int, ObjectCtrlInfo> loaded, SceneOperationKind operation)
		{
			SoftFleshPlugin.Log(string.Concat(new string[]
			{
				"[SF] scene: load block (",
				SoftFleshPlugin.I(d, "n", 0).ToString(),
				" character(s), ",
				operation.ToString(),
				")"
			}));
			SoftFleshPlugin p = SoftFleshPlugin._live;
			if (p == null || p._ctrl == null || d == null)
			{
				return;
			}
			bool isLoad = operation == 0;
			if (isLoad)
			{
				p._ctrl.ClearAll();
				p._ctrl.ClearItemColliders();
				p.UnpackParams(SoftFleshPlugin.S(d, "params", null));
			}
			List<ChaControl> want = new List<ChaControl>();
			List<bool> wantOn = new List<bool>();
			List<bool> wantBaked = new List<bool>();
			List<bool> wantFace = new List<bool>();
			List<bool> wantFaceOn = new List<bool>();
			bool anyBaked = SoftFleshPlugin.B(d, "baked", false);
			int i = SoftFleshPlugin.I(d, "n", 0);
			int missing = 0;
			for (int j = 0; j < i; j++)
			{
				string k = "c" + j.ToString() + ".";
				int key = SoftFleshPlugin.I(d, k + "key", -1);
				ObjectCtrlInfo oci;
				if (key < 0 || loaded == null || !loaded.TryGetValue(key, ref oci) || oci == null)
				{
					missing++;
				}
				else
				{
					OCIChar oc = oci as OCIChar;
					if (oc == null || oc.charInfo == null)
					{
						missing++;
					}
					else
					{
						want.Add(oc.charInfo);
						wantOn.Add(SoftFleshPlugin.B(d, k + "on", true));
						wantBaked.Add(d.ContainsKey(k + "baked") ? SoftFleshPlugin.B(d, k + "baked", false) : anyBaked);
						wantFace.Add(SoftFleshPlugin.B(d, k + "face", false));
						wantFaceOn.Add(SoftFleshPlugin.B(d, k + "faceOn", true));
					}
				}
			}
			if (missing > 0)
			{
				SoftFleshPlugin.Log("[SF] scene: " + missing.ToString() + " saved character(s) not in the loaded scene - skipped");
			}
			List<OCIItem> wantItems = new List<OCIItem>();
			List<bool> wantIns = new List<bool>();
			int ni = SoftFleshPlugin.I(d, "ni", 0);
			int missingI = 0;
			for (int l = 0; l < ni; l++)
			{
				string m = "i" + l.ToString() + ".";
				int key2 = SoftFleshPlugin.I(d, m + "key", -1);
				ObjectCtrlInfo oci2;
				if (key2 < 0 || loaded == null || !loaded.TryGetValue(key2, ref oci2) || oci2 == null)
				{
					missingI++;
				}
				else
				{
					OCIItem it = oci2 as OCIItem;
					if (it == null || it.objectItem == null)
					{
						missingI++;
					}
					else
					{
						wantItems.Add(it);
						wantIns.Add(SoftFleshPlugin.B(d, m + "insert", false));
					}
				}
			}
			if (missingI > 0)
			{
				SoftFleshPlugin.Log("[SF] scene: " + missingI.ToString() + " saved item collider(s) not in the loaded scene - skipped");
			}
			if (want.Count == 0 && wantItems.Count == 0)
			{
				return;
			}
			p.StartCoroutine(p.RebuildAfterLoad(want, wantOn, wantBaked, isLoad, wantItems, wantIns, wantFace, wantFaceOn));
		}

		// Token: 0x0600003F RID: 63 RVA: 0x0000A037 File Offset: 0x00008237
		private IEnumerator ResetupWhenReady(ChaControl cha, bool on, bool bake)
		{
			SoftFleshPlugin.<ResetupWhenReady>d__185 <ResetupWhenReady>d__ = new SoftFleshPlugin.<ResetupWhenReady>d__185(0);
			<ResetupWhenReady>d__.<>4__this = this;
			<ResetupWhenReady>d__.cha = cha;
			<ResetupWhenReady>d__.on = on;
			<ResetupWhenReady>d__.bake = bake;
			return <ResetupWhenReady>d__;
		}

		// Token: 0x06000040 RID: 64 RVA: 0x0000A05C File Offset: 0x0000825C
		private IEnumerator RebuildAfterLoad(List<ChaControl> want, List<bool> wantOn, List<bool> wantBaked, bool isLoad, List<OCIItem> items, List<bool> itemsInsert, List<bool> wantFace, List<bool> wantFaceOn)
		{
			SoftFleshPlugin.<RebuildAfterLoad>d__186 <RebuildAfterLoad>d__ = new SoftFleshPlugin.<RebuildAfterLoad>d__186(0);
			<RebuildAfterLoad>d__.<>4__this = this;
			<RebuildAfterLoad>d__.want = want;
			<RebuildAfterLoad>d__.wantOn = wantOn;
			<RebuildAfterLoad>d__.wantBaked = wantBaked;
			<RebuildAfterLoad>d__.isLoad = isLoad;
			<RebuildAfterLoad>d__.items = items;
			<RebuildAfterLoad>d__.itemsInsert = itemsInsert;
			<RebuildAfterLoad>d__.wantFace = wantFace;
			<RebuildAfterLoad>d__.wantFaceOn = wantFaceOn;
			return <RebuildAfterLoad>d__;
		}

		// Token: 0x06000041 RID: 65 RVA: 0x0000A0B4 File Offset: 0x000082B4
		private static bool Section(string title, ref bool open)
		{
			GUILayout.Space(3f);
			if (GUILayout.Button((open ? "▼  " : "▶  ") + title, UI.SecHead, Array.Empty<GUILayoutOption>()))
			{
				open = !open;
			}
			UI.Rule();
			return open;
		}

		// Token: 0x06000042 RID: 66 RVA: 0x0000A100 File Offset: 0x00008300
		private static string V3s(Vector3 v)
		{
			return string.Concat(new string[]
			{
				v.x.ToString("F4", CultureInfo.InvariantCulture),
				",",
				v.y.ToString("F4", CultureInfo.InvariantCulture),
				",",
				v.z.ToString("F4", CultureInfo.InvariantCulture)
			});
		}

		// Token: 0x06000043 RID: 67 RVA: 0x0000A174 File Offset: 0x00008374
		private static void TryParseV3(string t, ref Vector3 v)
		{
			if (string.IsNullOrEmpty(t))
			{
				return;
			}
			string[] p = t.Split(new char[] { ',' });
			if (p.Length != 3)
			{
				return;
			}
			CultureInfo ci = CultureInfo.InvariantCulture;
			float x;
			float y;
			float z;
			if (float.TryParse(p[0], NumberStyles.Float, ci, out x) && float.TryParse(p[1], NumberStyles.Float, ci, out y) && float.TryParse(p[2], NumberStyles.Float, ci, out z))
			{
				v = new Vector3(x, y, z);
			}
		}

		// Token: 0x06000044 RID: 68 RVA: 0x0000A1F4 File Offset: 0x000083F4
		private static void RowMm(string label, ConfigEntry<float> c)
		{
			AcceptableValueRange<float> r = (AcceptableValueRange<float>)c.Description.AcceptableValues;
			c.Value = UI.SliderStacked(c.Definition.Key, label, c.Value * 1000f, r.MinValue * 1000f, r.MaxValue * 1000f, "F0", null, "mm") * 0.001f;
		}

		// Token: 0x06000045 RID: 69 RVA: 0x0000A260 File Offset: 0x00008460
		private static void Col2Begin()
		{
			Rect probe = GUILayoutUtility.GetRect(0f, 0f, new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
			if (Event.current.type == 7 && probe.width > 120f)
			{
				float w = (probe.width - 26f) * 0.5f;
				if (Mathf.Abs(w - SoftFleshPlugin._col2W) > 1f)
				{
					SoftFleshPlugin._col2W = w;
				}
			}
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			if (SoftFleshPlugin._col2W > 60f)
			{
				GUILayout.BeginVertical(new GUILayoutOption[] { GUILayout.Width(SoftFleshPlugin._col2W) });
				return;
			}
			GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
		}

		// Token: 0x06000046 RID: 70 RVA: 0x0000A30D File Offset: 0x0000850D
		private static void Col2Next()
		{
			GUILayout.EndVertical();
			GUILayout.Space(26f);
			if (SoftFleshPlugin._col2W > 60f)
			{
				GUILayout.BeginVertical(new GUILayoutOption[] { GUILayout.Width(SoftFleshPlugin._col2W) });
				return;
			}
			GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
		}

		// Token: 0x06000047 RID: 71 RVA: 0x0000A34D File Offset: 0x0000854D
		private static void Col2End()
		{
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
		}

		// Token: 0x06000048 RID: 72 RVA: 0x0000A359 File Offset: 0x00008559
		private static void Row(string label, ConfigEntry<float> c, string fmt)
		{
			SoftFleshPlugin.Row(label, c, fmt, null);
		}

		// Token: 0x06000049 RID: 73 RVA: 0x0000A364 File Offset: 0x00008564
		private static void Row(string label, ConfigEntry<float> c, string fmt, string help)
		{
			AcceptableValueRange<float> r = (AcceptableValueRange<float>)c.Description.AcceptableValues;
			c.Value = UI.SliderStacked(c.Definition.Key, label, c.Value, r.MinValue, r.MaxValue, fmt, help, null);
		}

		// Token: 0x0400000F RID: 15
		public const string GUID = "com.kkstudio.softflesh";

		// Token: 0x04000010 RID: 16
		public const string PluginName = "SoftFlesh";

		// Token: 0x04000011 RID: 17
		public const string Version = "1.1.0";

		// Token: 0x04000012 RID: 18
		internal static ManualLogSource SLog;

		// Token: 0x04000013 RID: 19
		private static ConfigEntry<KeyboardShortcut> CfgHotkey;

		// Token: 0x04000014 RID: 20
		private static ConfigEntry<float> CfgTouchRadius;

		// Token: 0x04000015 RID: 21
		private static ConfigEntry<float> CfgStiffness;

		// Token: 0x04000016 RID: 22
		private static ConfigEntry<float> CfgDamping;

		// Token: 0x04000017 RID: 23
		private static ConfigEntry<float> CfgMaxDepth;

		// Token: 0x04000018 RID: 24
		private static ConfigEntry<float> CfgKnuckle;

		// Token: 0x04000019 RID: 25
		private static ConfigEntry<float> CfgWaveSpeed;

		// Token: 0x0400001A RID: 26
		private static ConfigEntry<float> CfgBulge;

		// Token: 0x0400001B RID: 27
		private static ConfigEntry<float> CfgSmooth;

		// Token: 0x0400001C RID: 28
		private static ConfigEntry<float> CfgTension;

		// Token: 0x0400001D RID: 29
		private static ConfigEntry<float> CfgBend;

		// Token: 0x0400001E RID: 30
		private static ConfigEntry<float> CfgShadow;

		// Token: 0x0400001F RID: 31
		private static ConfigEntry<float> CfgNipFirm;

		// Token: 0x04000020 RID: 32
		private static ConfigEntry<float> CfgSlap;

		// Token: 0x04000021 RID: 33
		private static ConfigEntry<float> CfgSoften;

		// Token: 0x04000022 RID: 34
		private static ConfigEntry<float> CfgWave;

		// Token: 0x04000023 RID: 35
		private static ConfigEntry<float> CfgItemRad;

		// Token: 0x04000024 RID: 36
		private static ConfigEntry<float> CfgBelly;

		// Token: 0x04000025 RID: 37
		private static ConfigEntry<float> CfgOpenD;

		// Token: 0x04000026 RID: 38
		private static ConfigEntry<float> CfgOpenW;

		// Token: 0x04000027 RID: 39
		private static ConfigEntry<float> CfgOpenMax;

		// Token: 0x04000028 RID: 40
		private static ConfigEntry<float> CfgClitHold;

		// Token: 0x04000029 RID: 41
		private static ConfigEntry<float> CfgSmoothShade;

		// Token: 0x0400002A RID: 42
		private static ConfigEntry<float> CfgVisc;

		// Token: 0x0400002B RID: 43
		private static ConfigEntry<float> CfgItemStretch;

		// Token: 0x0400002C RID: 44
		private static ConfigEntry<float> CfgCorr;

		// Token: 0x0400002D RID: 45
		private static ConfigEntry<float> CfgSubArea;

		// Token: 0x0400002E RID: 46
		private static ConfigEntry<float> CfgSubWGap;

		// Token: 0x0400002F RID: 47
		private static ConfigEntry<bool> CfgBulgeWide;

		// Token: 0x04000030 RID: 48
		private static ConfigEntry<bool> CfgSubdiv;

		// Token: 0x04000031 RID: 49
		private static ConfigEntry<bool> CfgSubBody;

		// Token: 0x04000032 RID: 50
		private static ConfigEntry<bool> CfgTrack;

		// Token: 0x04000033 RID: 51
		private static ConfigEntry<bool> CfgMouthAuto;

		// Token: 0x04000034 RID: 52
		private static ConfigEntry<float> CfgMouthScale;

		// Token: 0x04000035 RID: 53
		private static ConfigEntry<float> CfgMouthReach;

		// Token: 0x04000036 RID: 54
		private static ConfigEntry<float> CfgLipDrag;

		// Token: 0x04000037 RID: 55
		private static ConfigEntry<float> CfgLipPushIn;

		// Token: 0x04000038 RID: 56
		private static ConfigEntry<float> CfgTiltFollow;

		// Token: 0x04000039 RID: 57
		private static ConfigEntry<float> CfgFaceFirm;

		// Token: 0x0400003A RID: 58
		private static ConfigEntry<float> CfgFaceDamp;

		// Token: 0x0400003B RID: 59
		private static ConfigEntry<float> CfgFaceWave;

		// Token: 0x0400003C RID: 60
		private static ConfigEntry<float> CfgFaceSlap;

		// Token: 0x0400003D RID: 61
		private static ConfigEntry<float> CfgFaceDepth;

		// Token: 0x0400003E RID: 62
		private static ConfigEntry<float> CfgLipReturn;

		// Token: 0x0400003F RID: 63
		private static ConfigEntry<float> CfgLipDamp;

		// Token: 0x04000040 RID: 64
		private static ConfigEntry<int> CfgLipSmooth;

		// Token: 0x04000041 RID: 65
		private static ConfigEntry<bool> CfgDeepCheck;

		// Token: 0x04000042 RID: 66
		private static ConfigEntry<bool> CfgMouthTilt;

		// Token: 0x04000043 RID: 67
		private static ConfigEntry<bool> CfgFaceDrag;

		// Token: 0x04000044 RID: 68
		private static ConfigEntry<bool> CfgLipHold;

		// Token: 0x04000045 RID: 69
		private static ConfigEntry<float> CfgGapeHold;

		// Token: 0x04000046 RID: 70
		private static ConfigEntry<float> CfgSuction;

		// Token: 0x04000047 RID: 71
		private static ConfigEntry<float> CfgPetalFirm;

		// Token: 0x04000048 RID: 72
		private static ConfigEntry<float> CfgTrackWx;

		// Token: 0x04000049 RID: 73
		private static ConfigEntry<float> CfgItemGuide;

		// Token: 0x0400004A RID: 74
		private static ConfigEntry<float> CfgFollow;

		// Token: 0x0400004B RID: 75
		private static ConfigEntry<float> CfgSpread;

		// Token: 0x0400004C RID: 76
		private static ConfigEntry<float> CfgBellyW;

		// Token: 0x0400004D RID: 77
		private static ConfigEntry<float> CfgBellyLen;

		// Token: 0x0400004E RID: 78
		private static ConfigEntry<float> CfgBellyShape;

		// Token: 0x0400004F RID: 79
		private static ConfigEntry<float> CfgBellyStart;

		// Token: 0x04000050 RID: 80
		private static ConfigEntry<float> CfgEntTilt;

		// Token: 0x04000051 RID: 81
		private static ConfigEntry<float> CfgBakeReach;

		// Token: 0x04000052 RID: 82
		private static ConfigEntry<float> CfgSpreadW;

		// Token: 0x04000053 RID: 83
		private static ConfigEntry<float> CfgSpreadR;

		// Token: 0x04000054 RID: 84
		private static ConfigEntry<float> CfgWidthTrim;

		// Token: 0x04000055 RID: 85
		private static ConfigEntry<float> CfgWallOnly;

		// Token: 0x04000056 RID: 86
		private static ConfigEntry<bool> CfgTaper;

		// Token: 0x04000057 RID: 87
		private static ConfigEntry<bool> CfgCrotchIso;

		// Token: 0x04000058 RID: 88
		private static ConfigEntry<float> CfgItemSquash;

		// Token: 0x04000059 RID: 89
		private static ConfigEntry<float> CfgWaveShield;

		// Token: 0x0400005A RID: 90
		private static ConfigEntry<bool> CfgClothFollow;

		// Token: 0x0400005B RID: 91
		private static ConfigEntry<bool> CfgSecondSkin;

		// Token: 0x0400005C RID: 92
		private static ConfigEntry<bool> CfgSpanGuard;

		// Token: 0x0400005D RID: 93
		private static ConfigEntry<float> CfgClothLift;

		// Token: 0x0400005E RID: 94
		private static ConfigEntry<float> CfgShadeTau;

		// Token: 0x0400005F RID: 95
		private static ConfigEntry<bool> CfgClothSubdiv;

		// Token: 0x04000060 RID: 96
		private static ConfigEntry<int> CfgSubLevels;

		// Token: 0x04000061 RID: 97
		private static ConfigEntry<string> CfgAxisEntry;

		// Token: 0x04000062 RID: 98
		private static ConfigEntry<string> CfgAxisDir;

		// Token: 0x04000063 RID: 99
		public const int SLOTS = 12;

		// Token: 0x04000064 RID: 100
		private static ConfigEntry<string>[] CfgPreset = new ConfigEntry<string>[12];

		// Token: 0x04000065 RID: 101
		private static ConfigEntry<string>[] CfgPresetName = new ConfigEntry<string>[12];

		// Token: 0x04000066 RID: 102
		private static readonly string[] PresetNames = new string[] { "Default", "Soft", "Firm", "Jiggly" };

		// Token: 0x04000067 RID: 103
		private static int _preset;

		// Token: 0x04000068 RID: 104
		private static int _confirmSave = -1;

		// Token: 0x04000069 RID: 105
		private static string _toast = "";

		// Token: 0x0400006A RID: 106
		private static float _toastUntil;

		// Token: 0x0400006B RID: 107
		private SoftFleshController _ctrl;

		// Token: 0x0400006C RID: 108
		private static SoftFleshPlugin _live;

		// Token: 0x0400006D RID: 109
		private float _tlRetry;

		// Token: 0x0400006E RID: 110
		private string _tlKeyMsg;

		// Token: 0x0400006F RID: 111
		private float _tlKeyMsgAt = -100f;

		// Token: 0x04000070 RID: 112
		private float _swapSweepAt;

		// Token: 0x04000071 RID: 113
		private readonly HashSet<ChaControl> _rebuilding = new HashSet<ChaControl>();

		// Token: 0x04000072 RID: 114
		private CameraControl _lastCc;

		// Token: 0x04000073 RID: 115
		private bool _showUI;

		// Token: 0x04000074 RID: 116
		private bool _mouseOverWindow;

		// Token: 0x04000075 RID: 117
		private bool _alive = true;

		// Token: 0x04000076 RID: 118
		private const string GenSlot = "SoftFlesh.generation";

		// Token: 0x04000077 RID: 119
		private const string VoteSlot = "SoftFlesh.camVote";

		// Token: 0x04000078 RID: 120
		private const string ChainSlot = "SoftFlesh.camChainedFor";

		// Token: 0x04000079 RID: 121
		private int _gen;

		// Token: 0x0400007A RID: 122
		private Rect _winRect = new Rect(140f, 90f, 960f, 620f);

		// Token: 0x0400007B RID: 123
		private float _winW = 960f;

		// Token: 0x0400007C RID: 124
		private float _winH = 620f;

		// Token: 0x0400007D RID: 125
		private bool _winResizing;

		// Token: 0x0400007E RID: 126
		private Vector2 _scBody;

		// Token: 0x0400007F RID: 127
		private Vector2 _scRail;

		// Token: 0x04000080 RID: 128
		private static readonly HashSet<string> _labelLogged = new HashSet<string>();

		// Token: 0x04000081 RID: 129
		private static readonly string[] TabNames = new string[] { "Contact", "Impact & waves", "Items", "Face", "Opening", "Mesh repair (at Setup)", "Dev info" };

		// Token: 0x04000082 RID: 130
		private static readonly string[] TabCount = new string[] { "16", "6", "10", "17", "11", "8", "" };

		// Token: 0x04000083 RID: 131
		private static GUIContent[] _tabGC;

		// Token: 0x04000084 RID: 132
		private const int TAB_DIAG = -1;

		// Token: 0x04000085 RID: 133
		private const int TAB_DEV = 6;

		// Token: 0x04000086 RID: 134
		private Rect _dlgRect;

		// Token: 0x04000087 RID: 135
		private bool _dlgPlaced;

		// Token: 0x04000088 RID: 136
		private const string Author = "Poison_KK";

		// Token: 0x04000089 RID: 137
		private const string AuthorLink = "linktr.ee/Poison_kk ↗";

		// Token: 0x0400008A RID: 138
		private const string AuthorUrl = "https://linktr.ee/Poison_kk";

		// Token: 0x0400008B RID: 139
		private const string Copyright = "© 2026 @poison_kk — All Rights Reserved";

		// Token: 0x0400008C RID: 140
		private static string _uiDumpMsg;

		// Token: 0x0400008D RID: 141
		private static readonly string[] PhaseNames = new string[]
		{
			"bake", "corr", "open", "spring", "norm", "contact", "field", "maint", "cloth", "skin",
			"other"
		};

		// Token: 0x0400008E RID: 142
		private static int _confirmDelete = -1;

		// Token: 0x0400008F RID: 143
		private const string SceneHookSlot = "SoftFlesh.sceneHooked";

		// Token: 0x04000090 RID: 144
		private static bool _dgRare;

		// Token: 0x04000091 RID: 145
		private static bool _dgLive = true;

		// Token: 0x04000092 RID: 146
		private static bool _dgSetup = true;

		// Token: 0x04000093 RID: 147
		private static bool _dgGuide;

		// Token: 0x04000094 RID: 148
		private static bool _dgTools;

		// Token: 0x04000095 RID: 149
		private static bool _dgLegacy;

		// Token: 0x04000096 RID: 150
		private static int _tab;

		// Token: 0x04000097 RID: 151
		private static GUIStyle _grip;

		// Token: 0x04000098 RID: 152
		private static string _sSolve = "";

		// Token: 0x04000099 RID: 153
		private static string _sDiag = "";

		// Token: 0x0400009A RID: 154
		private static string _sDiagSt = "";

		// Token: 0x0400009B RID: 155
		private static string _sRig = "";

		// Token: 0x0400009C RID: 156
		private static string _sSubdiv = "";

		// Token: 0x0400009D RID: 157
		private static string _sWeld = "";

		// Token: 0x0400009E RID: 158
		private static string _sGuide = "";

		// Token: 0x0400009F RID: 159
		private static string _sGuideL = "";

		// Token: 0x040000A0 RID: 160
		private static string _sBake = "";

		// Token: 0x040000A1 RID: 161
		private static int _sItems;

		// Token: 0x040000A2 RID: 162
		private static float _col2W;
	}
}
