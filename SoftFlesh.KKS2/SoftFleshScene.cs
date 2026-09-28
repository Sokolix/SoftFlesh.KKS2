using System;
using System.Collections.Generic;
using ExtensibleSaveFormat;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using Studio;

namespace SoftFlesh
{
	// Token: 0x02000008 RID: 8
	public class SoftFleshScene : SceneCustomFunctionController
	{
		// Token: 0x060001BF RID: 447 RVA: 0x00056CE4 File Offset: 0x00054EE4
		protected override void OnSceneSave()
		{
			Func<Dictionary<string, object>> f = AppDomain.CurrentDomain.GetData("SoftFlesh.scene.collect") as Func<Dictionary<string, object>>;
			Dictionary<string, object> d = ((f != null) ? f() : null);
			if (d == null)
			{
				base.SetExtendedData(null);
				return;
			}
			base.SetExtendedData(new PluginData
			{
				version = 1,
				data = d
			});
		}

		// Token: 0x060001C0 RID: 448 RVA: 0x00056D38 File Offset: 0x00054F38
		protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
		{
			if (operation == SceneOperationKind.Clear)
			{
				return;
			}
			PluginData data = base.GetExtendedData();
			if (data == null || data.data == null)
			{
				Action c = AppDomain.CurrentDomain.GetData("SoftFlesh.scene.clear") as Action;
				if (c != null)
				{
					c();
				}
				return;
			}
			Action<Dictionary<string, object>, ReadOnlyDictionary<int, ObjectCtrlInfo>, int> a = AppDomain.CurrentDomain.GetData("SoftFlesh.scene.apply") as Action<Dictionary<string, object>, ReadOnlyDictionary<int, ObjectCtrlInfo>, int>;
			if (a != null)
			{
				a(data.data, loadedItems, (int)operation);
			}
		}

		// Token: 0x04000368 RID: 872
		internal const string KCollect = "SoftFlesh.scene.collect";

		// Token: 0x04000369 RID: 873
		internal const string KApply = "SoftFlesh.scene.apply";

		// Token: 0x0400036A RID: 874
		internal const string KClear = "SoftFlesh.scene.clear";
	}
}
