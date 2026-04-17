using System;
using System.Runtime.CompilerServices;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	// Token: 0x0200002C RID: 44
	public class ComponentGhostHumanModel : ComponentHumanModel, IUpdateable
	{
		// Token: 0x06000147 RID: 327 RVA: 0x0000E1D8 File Offset: 0x0000C3D8
		public override void Animate()
		{
			base.Animate();
			base.Opacity = new float?(this.m_opacity);
			float? opacity = base.Opacity;
			float num = 1f;
			bool flag = opacity.GetValueOrDefault() >= num & opacity != null;
			if (flag)
			{
				this.RenderingMode = ModelRenderingMode.AlphaThreshold;
			}
			else
			{
				this.RenderingMode = ModelRenderingMode.TransparentAfterWater;
			}
		}

		// Token: 0x06000148 RID: 328 RVA: 0x0000E23B File Offset: 0x0000C43B

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			base.Load(valuesDictionary, idToEntityMap);
			this.m_opacity = valuesDictionary.GetValue<float>("Opacity");
		}

		// Token: 0x04000144 RID: 324
		public float m_opacity;
	}
}
