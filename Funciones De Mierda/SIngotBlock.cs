using Engine;
using Engine.Graphics;
using System.Collections.Generic;

namespace Game
{
	// Items
	public class SIngotBlock : Block
	{
		public static int Index = 700;
		private Texture2D texture;
		public enum HTTool
		{
			GoldIngot,
			SteelIngot
		}
		public List<BlockMesh> m_standaloneBlockMeshes = new List<BlockMesh>();
		public static int[] m_textureCount = new int[2]
		{
			16,
			17
		};
		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			switch (Terrain.ExtractData(value))
			{
				case 0: return "Gold Ingot";
				case 1: return "Steel Ingot";
				default: return "SIngotBlock";
			}
		}
		public override string GetDescription(int value)
		{
			switch (Terrain.ExtractData(value))
			{
				case 0: return "Smelt gold ore into gold ingots. Gold ingots are a rare material that can be used to craft highly efficient gold tools or combined into a solid gold block.";
				case 1:
					return "Smelt diamonds and iron ingots into steel ingots. Steel is a very strong metal that can be used to make tools or combined into a solid steel block.";
				default: return "SIngotBlock";
			}
		}
		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(SIngotBlock.Index, 0, 0);
			yield return Terrain.MakeBlockValue(SIngotBlock.Index, 0, 1);
			yield break;
		}
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Ingots");
			texture = ContentManager.Get<Texture2D>("Textures/ToolBlocks");
			foreach (int enumValue in EnumUtils.GetEnumValues(typeof(HTTool)))
			{
				Matrix matrix = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("IronIngot").ParentBone);
				var blockMesh = new BlockMesh();
				blockMesh.AppendModelMeshPart(model.FindMesh("IronIngot").MeshParts[0], matrix * Matrix.CreateTranslation(0f, -0.1f, 0f), makeEmissive: false, flipWindingOrder: false, doubleSided: false, flipNormals: false, Color.White);
				blockMesh.TransformTextureCoordinates(Matrix.CreateTranslation((m_textureCount[enumValue] % 16 - 15) / 16f, (m_textureCount[enumValue] / 16 - 3) / 16f, 0f));
				m_standaloneBlockMeshes.Add(blockMesh);
			}
			base.Initialize();
		}
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int htTool = (int)GetHTTool(Terrain.ExtractData(value));
			if (htTool >= 0 && htTool < m_standaloneBlockMeshes.Count)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMeshes[htTool], texture, color, 2f * size, ref matrix, environmentData);
			}
		}
		public static HTTool GetHTTool(int data)
		{
			return (HTTool)(data & 0xF);
		}
	}
	public class GoldAxeBlock : SToolBlock // Gold Axe
	{
		public static int Index = 701;

		public GoldAxeBlock()
			: base("Axe", 0, 16)
		{
		}
	}
	public class GoldPickaxeBlock : SToolBlock // Gold Pickaxe
	{
		public static int Index = 702;

		public GoldPickaxeBlock()
			: base("Pickaxe", 0, 16)
		{
		}
	}
	public class GoldShovelBlock : SToolBlock // Gold Shovel
	{
		public static int Index = 703;

		public GoldShovelBlock()
			: base("Shovel", 0, 16)
		{
		}
	}
	public class GoldSpearBlock : SToolBlock // Gold Spear
	{
		public static int Index = 704;

		public GoldSpearBlock()
			: base("Spear", 0, 16)
		{
		}
	}
	public class GoldMacheteBlock : SToolBlock // Gold Machete
	{
		public static int Index = 705;

		public GoldMacheteBlock()
			: base("Machete", 0, 16)
		{
		}
	}
	public class GoldHammerBlock : SToolBlock // Gold Hammer
	{
		public static int Index = 706;

		public GoldHammerBlock()
			: base("StoneAxe", 0, 16)
		{
		}
	}
	public class GoldRakeBlock : SToolBlock // Gold Rake
	{
		public static int Index = 707;

		public GoldRakeBlock()
			: base("Rake", 0, 16)
		{
		}
	}
	public class SteelAxeBlock : SToolBlock // Steel Axe
	{
		public static int Index = 708;

		public SteelAxeBlock()
			: base("Axe", 0, 17)
		{
		}
	}
	public class SteelPickaxeBlock : SToolBlock // Steel Pickaxe
	{
		public static int Index = 709;

		public SteelPickaxeBlock()
			: base("Pickaxe", 0, 17)
		{
		}
	}
	public class SteelShovelBlock : SToolBlock // Steel Shovel
	{
		public static int Index = 710;

		public SteelShovelBlock()
			: base("Shovel", 0, 17)
		{
		}
	}
	public class SteelSpearBlock : SToolBlock // Steel Spear
	{
		public static int Index = 711;

		public SteelSpearBlock()
			: base("Spear", 0, 17)
		{
		}
	}
	public class SteelMacheteBlock : SToolBlock // Steel Machete
	{
		public static int Index = 712;

		public SteelMacheteBlock()
			: base("Machete", 0, 17)
		{
		}
	}
	public class SteelHammerBlock : SToolBlock // Steel Hammer
	{
		public static int Index = 713;

		public SteelHammerBlock()
			: base("StoneAxe", 0, 17)
		{
		}
	}
	public class SteelRakeBlock : SToolBlock // Steel Rake
	{
		public static int Index = 714;

		public SteelRakeBlock()
			: base("Rake", 0, 17)
		{
		}
	}
	public class GoldOreBlock : SCubeBlock // Gold Ore
	{
		public static int Index = 715;
		public GoldOreBlock()
		  : base(32, 32, 32, 32, 32, 32)
		{
		}
	}
	public class GoldCubeBlock : SCubeBlock // Gold Block
	{
		public static int Index = 716;
		public GoldCubeBlock()
		  : base(16, 16, 16, 16, 16, 16)
		{
		}
	}
	public class SteelCubeBlock : SCubeBlock // Steel Block
	{
		public static int Index = 717;
		public SteelCubeBlock()
		  : base(17, 17, 17, 17, 17, 17)
		{
		}
	}
	public class SDrillBlock : SFlatBlock // Electric Drill
	{
		public static int Index = 718;

		public SDrillBlock()
			: base(48)
		{
		}
	}
	public class SSawBlock : SFlatBlock // Chainsaw
	{
		public static int Index = 719;

		public SSawBlock()
			: base(49)
		{
		}
	}
	public class GoldOreChunkBlock : ChunkBlock
	{
		public static int Index = 720;

		public GoldOreChunkBlock()
			: base(Matrix.CreateRotationX(0f) * Matrix.CreateRotationZ(2f), Matrix.CreateTranslation(0.9375f, 0.1875f, 0f), new Color(255, 196, 0), smooth: false)
		{
		}
	}
	public class CopperHammerBlock : SToolBlock // Copper Hammer
	{
		public static int Index = 797;

		public CopperHammerBlock()
			: base("StoneAxe", 0, 1)
		{
		}
	}
	public class IronHammer2Block : SToolBlock // Iron Hammer
	{
		public static int Index = 798;

		public IronHammer2Block()
			: base("StoneAxe", 0, 2)
		{
		}
	}
	public class DiamondHammerBlock : SToolBlock // Diamond Hammer
	{
		public static int Index = 799;

		public DiamondHammerBlock()
			: base("StoneAxe", 0, 3)
		{
		}
	}

	public class GoldOreProduce : ModLoader
	{
		public Random m_random = new Random(); public override void __ModInitialize() { ModsManager.RegisterHook("OnTerrainContentsGenerated", this); }
		public override void OnTerrainContentsGenerated(TerrainChunk chunk)
		{
			GenerateChunk(chunk, 5, 30, GoldOreBlock.Index, 67);
		}
		public void GenerateChunk(TerrainChunk chunk, int min, int max, int index, int oldindex)
		{
			List<TerrainBrush> m_goldBrushes = new List<TerrainBrush>(); int x = chunk.Coords.X; int y = chunk.Coords.Y; for (int l = 0; l < 16; l++)
			{
				TerrainBrush terrainBrush = new TerrainBrush();
				int num2 = m_random.Int(0, 6); for (int m = 0; m < num2; m++)
				{
					Vector3 vector2 = 0.5f * Vector3.Normalize(new Vector3(m_random.Float(-1f, 1f), m_random.Float(-1f, 1f), m_random.Float(-1f, 1f)));
					int num3 = m_random.Int(0, 6); Vector3 zero2 = Vector3.Zero; for (int n = 0; n < num3; n++) { terrainBrush.AddBox((int)MathUtils.Floor(zero2.X), (int)MathUtils.Floor(zero2.Y), (int)MathUtils.Floor(zero2.Z), 1, 1, 1, index); zero2 += vector2; }
				}
				terrainBrush.Compile(); m_goldBrushes.Add(terrainBrush);
			}
			for (int i = x - 1; i <= x + 1; i++) { for (int j = y - 1; j <= y + 1; j++) { int num = (int)(5f + 2f * SimplexNoise.OctavedNoise(i + 713, j + 211, 0.33f, 1, 1f, 1f)); for (int n = 0; n < num; n++) { int x1 = i * 16 + m_random.Int(0, 15); int y1 = m_random.Int(min, max); int z1 = j * 16 + m_random.Int(0, 15); m_goldBrushes[m_random.Int(0, m_goldBrushes.Count - 1)].PaintFastSelective(chunk, x1, y1, z1, oldindex); } } }
		}
	}
	public abstract class SToolBlock : Block
	{
		public int m_handleTextureSlot;
		public int m_headTextureSlot;
		public Texture2D texture;
		public string m_type;
		public BlockMesh m_standaloneBlockMesh = new BlockMesh();
		public SToolBlock(string type, int handleTextureSlot, int headTextureSlot)
		{
			m_type = type;
			m_handleTextureSlot = handleTextureSlot;
			m_headTextureSlot = headTextureSlot;
		}
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/" + m_type);
			texture = ContentManager.Get<Texture2D>("Textures/ToolBlocks");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Handle").ParentBone);
			Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Head").ParentBone);
			BlockMesh blockMesh = new BlockMesh();
			blockMesh.AppendModelMeshPart(model.FindMesh("Handle").MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f), makeEmissive: false, flipWindingOrder: false, doubleSided: false, flipNormals: false, Color.White);
			blockMesh.TransformTextureCoordinates(Matrix.CreateTranslation((float)(m_handleTextureSlot % 16) / 16f, (float)(m_handleTextureSlot / 16) / 16f, 0f));
			BlockMesh blockMesh2 = new BlockMesh();
			blockMesh2.AppendModelMeshPart(model.FindMesh("Head").MeshParts[0], boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, -0.5f, 0f), makeEmissive: false, flipWindingOrder: false, doubleSided: false, flipNormals: false, Color.White);
			blockMesh2.TransformTextureCoordinates(Matrix.CreateTranslation((float)(m_headTextureSlot % 16) / 16f, (float)(m_headTextureSlot / 16) / 16f, 0f));
			m_standaloneBlockMesh.AppendBlockMesh(blockMesh);
			m_standaloneBlockMesh.AppendBlockMesh(blockMesh2);
			base.Initialize();
		}
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawMeshBlock(primitivesRenderer, m_standaloneBlockMesh, texture, color, 2f * size, ref matrix, environmentData);
		}
	}
	public abstract class SCubeBlock : Block
	{
		public Texture2D texture;
		public int m_face1;
		public int m_face2;
		public int m_face3;
		public int m_face4;
		public int m_face5;
		public int m_face6;
		public SCubeBlock(int face1, int face2, int face3, int face4, int face5, int face6)
		{
			m_face1 = face1;
			m_face2 = face2;
			m_face3 = face3;
			m_face4 = face4;
			m_face5 = face5;
			m_face6 = face6;
		}
		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, Color.White, GetFaceTextureSlot(1, m_face4), texture);
		}
		public override void Initialize()
		{
			texture = ContentManager.Get<Texture2D>("Textures/ToolBlocks");
		}
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateCubeVertices(this, value, x, y, z, Color.White, geometry.GetGeometry(texture).OpaqueSubsetsByFace);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), 1f, ref matrix, color, color, environmentData, texture);
		}

		public override int GetTextureSlotCount(int value)
		{
			return 16;
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			switch (face)
			{
				case 0: return m_face1;
				case 1: return m_face2;
				case 2: return m_face3;
				case 3: return m_face4;
				case 4: return m_face5;
				case 5: return m_face6;
			}
			return 0;
		}
	}
	public abstract class SFlatBlock : Block
	{
		public Texture2D texture;
		public int m_face;
		public SFlatBlock(int facenum)
		{
			m_face = facenum;
		}
		public override void Initialize()
		{
			base.Initialize();
			texture = ContentManager.Get<Texture2D>("Textures/ToolBlocks");
		}
		public override int GetTextureSlotCount(int value)
		{
			return 16;
		}
		public override int GetFaceTextureSlot(int face, int value)
		{
			if (face == -1) return m_face;
			return m_face;
		}
		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}
		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 1f, ref matrix, texture, Color.White, isEmissive: true, environmentData);
		}
	}
}
