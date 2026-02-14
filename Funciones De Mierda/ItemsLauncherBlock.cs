using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using Game;

public class ItemsLauncherBlock : Block
{
	public const string DisplayNameKey = "DisplayName";
	public const string DescriptionKey = "Description";

	public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
	{
		// Usar LanguageControl.GetBlock que busca en "Blocks" -> "ItemsLauncher:0" -> "DisplayName"
		return LanguageControl.GetBlock("ItemsLauncher:0", DisplayNameKey);
	}

	public override string GetDescription(int value)
	{
		// Usar LanguageControl.GetBlock que busca en "Blocks" -> "ItemsLauncher:0" -> "Description"
		return LanguageControl.GetBlock("ItemsLauncher:0", DescriptionKey);
	}

	public static int GetSpeedLevel(int data)
	{
		return data & 3;
	}

	public static int GetRateLevel(int data)
	{
		return (data & 124) >> 2;
	}

	public static int GetSpreadLevel(int data)
	{
		return (data & 896) >> 7;
	}

	public static int GetFuel(int data)
	{
		return (data & 64512) >> 10;
	}

	public static int SetSpeedLevel(int data, int level)
	{
		return (data & -4) | (level & 3);
	}

	public static int SetRateLevel(int data, int level)
	{
		return (data & -125) | (level << 2 & 124);
	}

	public static int SetSpreadLevel(int data, int level)
	{
		return (data & -897) | (level << 7 & 896);
	}

	public static int SetAllLevels(int data, int speed, int rate, int spread)
	{
		data = ItemsLauncherBlock.SetSpeedLevel(data, speed);
		data = ItemsLauncherBlock.SetRateLevel(data, rate);
		data = ItemsLauncherBlock.SetSpreadLevel(data, spread);
		return data;
	}

	public static int SetFuel(int data, int fuel)
	{
		return (data & -64513) | (fuel << 10 & 64512);
	}

	public static int GetVirtualProjectileId(int data)
	{
		return data & 255;
	}

	public static int SetVirtualProjectileId(int data, int id)
	{
		return (data & -256) | (id & 255);
	}

	public override void Initialize()
	{
		Model model = ContentManager.Get<Model>("Models/ItemsLauncher");
		Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Cylinder", true).ParentBone);
		this.m_standaloneBlockMesh.AppendModelMeshPart(model.FindMesh("Cylinder", true).MeshParts[0], boneAbsoluteTransform, false, false, false, false, Color.White);
		base.Initialize();
	}

	public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
	{
	}

	public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
	{
		BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMesh, color, 2f * size, ref matrix, environmentData);
	}

	internal static int SetDefaultProjectile(int num, int v)
	{
		throw new NotImplementedException();
	}

	public static int Index = 300;

	private const int SpeedMask = 3;
	private const int RateMask = 124;
	private const int SpreadMask = 896;
	private const int RateShift = 2;
	private const int SpreadShift = 7;
	private const int FuelMask = 64512;
	private const int FuelShift = 10;
	public BlockMesh m_standaloneBlockMesh = new BlockMesh();
}
