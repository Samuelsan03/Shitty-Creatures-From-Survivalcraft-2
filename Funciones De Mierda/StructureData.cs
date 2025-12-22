using System;
using System.Runtime.CompilerServices;
using Engine;

namespace Game
{
	// Token: 0x0200003C RID: 60
	public class StructureData
	{
		// Token: 0x1700004C RID: 76
		// (get) Token: 0x060001E3 RID: 483 RVA: 0x00016F52 File Offset: 0x00015152
		// (set) Token: 0x060001E4 RID: 484 RVA: 0x00016F5A File Offset: 0x0001515A
		public string Name { get; set; }

		// Token: 0x1700004D RID: 77
		// (get) Token: 0x060001E5 RID: 485 RVA: 0x00016F63 File Offset: 0x00015163
		// (set) Token: 0x060001E6 RID: 486 RVA: 0x00016F6B File Offset: 0x0001516B
		public string FilePath { get; set; }

		// Token: 0x1700004E RID: 78
		// (get) Token: 0x060001E7 RID: 487 RVA: 0x00016F74 File Offset: 0x00015174
		// (set) Token: 0x060001E8 RID: 488 RVA: 0x00016F7C File Offset: 0x0001517C
		public float Probability { get; set; } = 1f;

		// Token: 0x1700004F RID: 79
		// (get) Token: 0x060001E9 RID: 489 RVA: 0x00016F85 File Offset: 0x00015185
		// (set) Token: 0x060001EA RID: 490 RVA: 0x00016F8D File Offset: 0x0001518D
		public string[] Creatures { get; set; }

		// Token: 0x17000050 RID: 80
		// (get) Token: 0x060001EB RID: 491 RVA: 0x00016F96 File Offset: 0x00015196
		// (set) Token: 0x060001EC RID: 492 RVA: 0x00016F9E File Offset: 0x0001519E
		public Point3[] SpawnLocations { get; set; }
	}
}
