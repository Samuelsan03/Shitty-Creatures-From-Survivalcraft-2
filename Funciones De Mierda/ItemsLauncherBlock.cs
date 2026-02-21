using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Game;

namespace Game;

// Token: 0x02000004 RID: 4
public class ItemsLauncherBlock : Block
{
	// Token: 0x06000008 RID: 8 RVA: 0x00002964 File Offset: 0x00000B64
	public static int GetSpeedLevel(int data)
	{
		return data & 3;
	}

	// Token: 0x06000009 RID: 9 RVA: 0x0000297C File Offset: 0x00000B7C
	public static int GetRateLevel(int data)
	{
		return (data & 124) >> 2;
	}

	// Token: 0x0600000A RID: 10 RVA: 0x00002994 File Offset: 0x00000B94
	public static int GetSpreadLevel(int data)
	{
		return (data & 896) >> 7;
	}

	// Token: 0x0600000B RID: 11 RVA: 0x000029B0 File Offset: 0x00000BB0
	public static int GetFuel(int data)
	{
		return (data & 64512) >> 10;
	}

	// Token: 0x0600000C RID: 12 RVA: 0x000029CC File Offset: 0x00000BCC
	public static int SetSpeedLevel(int data, int level)
	{
		return (data & -4) | (level & 3);
	}

	// Token: 0x0600000D RID: 13 RVA: 0x000029E8 File Offset: 0x00000BE8
	public static int SetRateLevel(int data, int level)
	{
		return (data & -125) | (level << 2 & 124);
	}

	// Token: 0x0600000E RID: 14 RVA: 0x00002A08 File Offset: 0x00000C08
	public static int SetSpreadLevel(int data, int level)
	{
		return (data & -897) | (level << 7 & 896);
	}

	// Token: 0x0600000F RID: 15 RVA: 0x00002A2C File Offset: 0x00000C2C
	public static int SetAllLevels(int data, int speed, int rate, int spread)
	{
		data = ItemsLauncherBlock.SetSpeedLevel(data, speed);
		data = ItemsLauncherBlock.SetRateLevel(data, rate);
		data = ItemsLauncherBlock.SetSpreadLevel(data, spread);
		return data;
	}

	// Token: 0x06000010 RID: 16 RVA: 0x00002A5C File Offset: 0x00000C5C
	public static int SetFuel(int data, int fuel)
	{
		return (data & -64513) | (fuel << 10 & 64512);
	}

	// Token: 0x06000011 RID: 17 RVA: 0x00002A80 File Offset: 0x00000C80
	public static int GetVirtualProjectileId(int data)
	{
		return data & 255;
	}

	// Token: 0x06000012 RID: 18 RVA: 0x00002A9C File Offset: 0x00000C9C
	public static int SetVirtualProjectileId(int data, int id)
	{
		return (data & -256) | (id & 255);
	}

	// Token: 0x06000013 RID: 19 RVA: 0x00002AC0 File Offset: 0x00000CC0
	public override void Initialize()
	{
		Model model = ContentManager.Get<Model>("Models/ItemsLauncher");
		Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Cylinder", true).ParentBone);
		this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Cylinder", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
		base.Initialize();
	}

	// Token: 0x06000014 RID: 20 RVA: 0x00002B27 File Offset: 0x00000D27
	public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
	{
	}

	// Token: 0x06000015 RID: 21 RVA: 0x00002B2A File Offset: 0x00000D2A
	public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
	{
		BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
	}

	// Token: 0x04000011 RID: 17
	public static int Index = 359;

	// Token: 0x04000012 RID: 18
	private const int SpeedMask = 3;

	// Token: 0x04000013 RID: 19
	private const int RateMask = 124;

	// Token: 0x04000014 RID: 20
	private const int SpreadMask = 896;

	// Token: 0x04000015 RID: 21
	private const int RateShift = 2;

	// Token: 0x04000016 RID: 22
	private const int SpreadShift = 7;

	// Token: 0x04000017 RID: 23
	private const int FuelMask = 64512;

	// Token: 0x04000018 RID: 24
	private const int FuelShift = 10;

	// Token: 0x04000019 RID: 25
	public BlockMesh m_standaloneBlockMesh = new BlockMesh();
}