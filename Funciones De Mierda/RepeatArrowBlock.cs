using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class RepeatArrowBlock : Block
	{
		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/RepeatArrow");
			foreach (int num in EnumUtils.GetEnumValues(typeof(RepeatArrowBlock.ArrowType)))
			{
				if (num > 15)
				{
					throw new InvalidOperationException("Too many arrow types.");
				}

				// Solo procesar los 6 tipos que tenemos
				if (num >= 6) continue;

				Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(RepeatArrowBlock.m_shaftNames[num], true).ParentBone);
				Matrix boneAbsoluteTransform2 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(RepeatArrowBlock.m_stabilizerNames[num], true).ParentBone);
				Matrix boneAbsoluteTransform3 = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh(RepeatArrowBlock.m_tipNames[num], true).ParentBone);

				BlockMesh blockMesh = new BlockMesh();
				blockMesh.AppendModelMeshPart(model.FindMesh(RepeatArrowBlock.m_tipNames[num], true).MeshParts[0], boneAbsoluteTransform3 * Matrix.CreateTranslation(0f, RepeatArrowBlock.m_offsets[num], 0f), false, false, false, false, Color.White);
				blockMesh.TransformTextureCoordinates(Matrix.CreateTranslation((float)(RepeatArrowBlock.m_tipTextureSlots[num] % 16) / 16f, (float)(RepeatArrowBlock.m_tipTextureSlots[num] / 16) / 16f, 0f), -1);

				BlockMesh blockMesh2 = new BlockMesh();
				blockMesh2.AppendModelMeshPart(model.FindMesh(RepeatArrowBlock.m_shaftNames[num], true).MeshParts[0], boneAbsoluteTransform * Matrix.CreateTranslation(0f, RepeatArrowBlock.m_offsets[num], 0f), false, false, false, false, Color.White);
				blockMesh2.TransformTextureCoordinates(Matrix.CreateTranslation((float)(RepeatArrowBlock.m_shaftTextureSlots[num] % 16) / 16f, (float)(RepeatArrowBlock.m_shaftTextureSlots[num] / 16) / 16f, 0f), -1);

				BlockMesh blockMesh3 = new BlockMesh();
				blockMesh3.AppendModelMeshPart(model.FindMesh(RepeatArrowBlock.m_stabilizerNames[num], true).MeshParts[0], boneAbsoluteTransform2 * Matrix.CreateTranslation(0f, RepeatArrowBlock.m_offsets[num], 0f), false, false, true, false, Color.White);
				blockMesh3.TransformTextureCoordinates(Matrix.CreateTranslation((float)(RepeatArrowBlock.m_stabilizerTextureSlots[num] % 16) / 16f, (float)(RepeatArrowBlock.m_stabilizerTextureSlots[num] / 16) / 16f, 0f), -1);

				BlockMesh blockMesh4 = new BlockMesh();
				blockMesh4.AppendBlockMesh(blockMesh);
				blockMesh4.AppendBlockMesh(blockMesh2);
				blockMesh4.AppendBlockMesh(blockMesh3);

				this.m_standaloneBlockMeshes.Add(blockMesh4);
			}
			base.Initialize();
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int arrowType = (int)RepeatArrowBlock.GetArrowType(Terrain.ExtractData(value));
			if (arrowType >= 0 && arrowType < this.m_standaloneBlockMeshes.Count)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, this.m_standaloneBlockMeshes[arrowType], color, 2f * size, ref matrix, environmentData);
			}
		}

		public override float GetProjectilePower(int value)
		{
			int arrowType = (int)RepeatArrowBlock.GetArrowType(Terrain.ExtractData(value));
			if (arrowType < 0 || arrowType >= 6)
			{
				return 0f;
			}
			return RepeatArrowBlock.m_weaponPowers[arrowType];
		}

		public override float GetExplosionPressure(int value)
		{
			int arrowType = (int)RepeatArrowBlock.GetArrowType(Terrain.ExtractData(value));
			if (arrowType < 0 || arrowType >= 6)
			{
				return 0f;
			}
			return RepeatArrowBlock.m_explosionPressures[arrowType];
		}

		public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
		{
			int arrowType = (int)RepeatArrowBlock.GetArrowType(Terrain.ExtractData(value));
			if (arrowType < 0 || arrowType >= 6)
			{
				return 1f;
			}
			return RepeatArrowBlock.m_iconViewScales[arrowType];
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.CopperArrow));
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.IronArrow));
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.DiamondArrow));
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.ExplosiveArrow));
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.PoisonArrow));
			yield return Terrain.MakeBlockValue(this.BlockIndex, 0, RepeatArrowBlock.SetArrowType(0, RepeatArrowBlock.ArrowType.SeriousPoisonArrow));
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			int arrowType = (int)RepeatArrowBlock.GetArrowType(Terrain.ExtractData(value));

			switch (arrowType)
			{
				case 0: return "Copper Repeating Arrow";
				case 1: return "Iron Repeating Arrow";
				case 2: return "Diamond Repeating Arrow";
				case 3: return "Explosive Repeating Arrow";
				case 4: return "Poison Repeating Arrow";
				case 5: return "Serious Poison Repeating Arrow";
				default: return "Unknown Arrow";
			}
		}

		// ADD: Descriptions for all repeating arrow types
		public override string GetDescription(int value)
		{
			int arrowType = (int)RepeatArrowBlock.GetArrowType(Terrain.ExtractData(value));

			switch (arrowType)
			{
				case 0: // Copper Repeating Arrow
					return "A specialized bolt for repeating crossbows. Made from copper, this arrow has moderate penetration power and is more accurate than regular Survivalcraft 2 arrows. Designed specifically for rapid-fire crossbows, it offers better stability during continuous firing.";

				case 1: // Iron Repeating Arrow
					return "An iron-tipped bolt engineered for repeating crossbows. With superior damage and armor penetration compared to regular iron arrows, this projectile maintains accuracy during rapid-fire sequences. The repeating crossbow mechanism allows faster shooting than standard crossbows.";

				case 2: // Diamond Repeating Arrow
					return "A high-end diamond-tipped bolt for advanced repeating crossbows. Features exceptional armor-piercing capability and damage output. Unlike standard diamond arrows, these are optimized for the repeating crossbow's unique firing mechanism, providing sustained high-damage output in automatic fire.";

				case 3: // Explosive Repeating Arrow
					return "An explosive-tipped bolt designed for repeating crossbows. Creates a small explosion on impact with 90 pressure force. While regular explosive arrows have a higher individual damage, these allow for rapid explosive shots, making them ideal for area denial and crowd control.";

				case 4: // Poison Repeating Arrow
					return "A poison-coated bolt for repeating crossbows. Inflicts poison damage over time on targets with very low initial impact (3 damage). Causes poison effect for 30 seconds. Designed to weaken enemies over time rather than kill instantly, making them ideal for capturing or slowing down targets.";

				case 5: // Serious Poison Repeating Arrow
					return "A heavily poisoned bolt for repeating crossbows. Delivers a stronger poison effect with higher poison duration (100 seconds) and very low initial damage (4 damage). The repeating crossbow mechanism lets you apply serious poison to multiple targets rapidly, weakening groups of enemies without killing them outright.";

				default:
					return base.GetDescription(value);
			}
		}

		public static RepeatArrowBlock.ArrowType GetArrowType(int data)
		{
			return (RepeatArrowBlock.ArrowType)(data & 15);
		}

		public static int SetArrowType(int data, RepeatArrowBlock.ArrowType arrowType)
		{
			return (data & -16) | (int)(arrowType & (RepeatArrowBlock.ArrowType)15);
		}

		static RepeatArrowBlock()
		{
			float[] array = new float[6];
			array[3] = 90f; // Explosive arrow (aumentado de 85 a 90)
			RepeatArrowBlock.m_explosionPressures = array;
		}

		public static int Index = 301;
		public List<BlockMesh> m_standaloneBlockMeshes = new List<BlockMesh>();
		public static int[] m_order = new int[] { 0, 1, 2, 3, 4, 5 };
		public static string[] m_tipNames = new string[]
		{
			"ArrowTip", "ArrowTip", "ArrowTip", "ArrowTip", "ArrowTip", "ArrowTip"
		};
		public static int[] m_tipTextureSlots = new int[] { 79, 63, 182, 225, 100, 60 };
		public static string[] m_shaftNames = new string[]
		{
			"ArrowShaft", "ArrowShaft", "ArrowShaft", "ArrowShaft", "ArrowShaft", "ArrowShaft"
		};
		public static int[] m_shaftTextureSlots = new int[] { 51, 51, 51, 51, 51, 51 };
		public static string[] m_stabilizerNames = new string[]
		{
			"ArrowStabilizer", "ArrowStabilizer", "ArrowStabilizer", "ArrowStabilizer", "ArrowStabilizer", "ArrowStabilizer"
		};
		public static int[] m_stabilizerTextureSlots = new int[] { 15, 15, 15, 15, 15, 15 };
		public static float[] m_offsets = new float[] { -0.45f, -0.45f, -0.45f, -0.45f, -0.45f, -0.45f };

		// DAÑOS AJUSTADOS PARA VENENOS:
		// Daño inicial MUY reducido - solo 3 para veneno, 4 para veneno grave
		// El daño principal vendrá del efecto de veneno con el tiempo
		public static float[] m_weaponPowers = new float[] { 18f, 26f, 40f, 10f, 3f, 4f };

		public static float[] m_iconViewScales = new float[] { 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f };
		public static float[] m_explosionPressures;

		public enum ArrowType
		{
			CopperArrow,
			IronArrow,
			DiamondArrow,
			ExplosiveArrow,
			PoisonArrow,
			SeriousPoisonArrow
		}
	}

}
