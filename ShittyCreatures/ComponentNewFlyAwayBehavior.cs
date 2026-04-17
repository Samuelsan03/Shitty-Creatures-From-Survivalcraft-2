using System;
using System.Runtime.CompilerServices;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x02000047 RID: 71
	public class ComponentNewFlyAwayBehavior : ComponentBehavior, IUpdateable
	{
		// Token: 0x17000055 RID: 85
		// (get) Token: 0x0600034C RID: 844 RVA: 0x0002B436 File Offset: 0x00029636
		// (set) Token: 0x0600034D RID: 845 RVA: 0x0002B43E File Offset: 0x0002963E
		public float LowHealthToEscape { get; set; }

		// Token: 0x17000056 RID: 86
		// (get) Token: 0x0600034E RID: 846 RVA: 0x0002B447 File Offset: 0x00029647
		public UpdateOrder UpdateOrder
		{
			get
			{
				return UpdateOrder.Default;
			}
		}

		// Token: 0x17000057 RID: 87
		// (get) Token: 0x0600034F RID: 847 RVA: 0x0002B44A File Offset: 0x0002964A
		public override float ImportanceLevel
		{
			get
			{
				return 0f;
			}
		}

		// Token: 0x06000350 RID: 848 RVA: 0x0002B451 File Offset: 0x00029651
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
		}

		// Token: 0x06000351 RID: 849 RVA: 0x0002B454 File Offset: 0x00029654
		public void Update(float dt)
		{
		}

		// Token: 0x06000352 RID: 850 RVA: 0x0002B457 File Offset: 0x00029657
		public void HearNoise(ComponentBody sourceBody, Vector3 sourcePosition, float loudness)
		{
		}
	}
}
