using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	public class RefrigeratorXiaomiBlock : Block, IElectricElementBlock
	{
		public RefrigeratorXiaomiBlock()
		{
			this.m_texture = ContentManager.Get<Texture2D>("Textures/Items/RefrigeratorXiaomi");
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			return LanguageControl.Get("RefrigeratorXiaomi", "BlockName");
		}

		public override string GetDescription(int value)
		{
			return LanguageControl.Get("RefrigeratorXiaomi", "BlockDescription");
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			generator.GenerateCubeVertices(this, value, x, y, z, Color.White, geometry.GetGeometry(this.m_texture).OpaqueSubsetsByFace);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), 1f, ref matrix, color, color, environmentData, this.m_texture);
		}

		public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
		{
			return Enumerable.Empty<CraftingRecipe>();
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			if (face == 4)
			{
				return 1;
			}
			if (face == 5)
			{
				return 1;
			}

			int data = Terrain.ExtractData(value);
			switch (data)
			{
				case 0: // North
					return (face == 0) ? 0 : 1;
				case 1: // West
					return (face == 1) ? 0 : 1;
				case 2: // South
					return (face == 2) ? 0 : 1;
				case 3: // East
					return (face == 3) ? 0 : 1;
				default:
					return 1;
			}
		}

		public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
		{
			Vector3 forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
			float[] array = new float[]
			{
				Vector3.Dot(forward, -Vector3.UnitZ),
				Vector3.Dot(forward, -Vector3.UnitX),
				Vector3.Dot(forward, Vector3.UnitZ),
				Vector3.Dot(forward, Vector3.UnitX)
			};

			int data = 0;
			float max = array[0];
			for (int i = 1; i < 4; i++)
			{
				if (array[i] > max)
				{
					max = array[i];
					data = i;
				}
			}

			return new BlockPlacementData
			{
				Value = Terrain.ReplaceData(Terrain.ReplaceContents(0, RefrigeratorXiaomiBlock.Index), data),
				CellFace = raycastResult.CellFace
			};
		}

		public ElectricElement CreateElectricElement(SubsystemElectricity subsystemElectricity, int value, int x, int y, int z)
		{
			return new RefrigeratorXiaomiElectricElement(subsystemElectricity, new Point3(x, y, z));
		}

		public ElectricConnectorType? GetConnectorType(SubsystemTerrain terrain, int value, int face, int connectorFace, int x, int y, int z)
		{
			return ElectricConnectorType.Input;
		}

		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, 0.6f, Color.White, this.GetFaceTextureSlot(0, value), this.m_texture);
		}

		public int GetConnectionMask(int value)
		{
			return int.MaxValue;
		}

		public static bool GetIsInCamperVan(int data)
		{
			return ((data >> 2) & 1) == 1;
		}

		public static int SetIsInCamperVan(int data, bool flag)
		{
			int num = flag ? 4 : 0;
			return (data & -5) | num;
		}

		public static int Index = 323;
		private readonly Texture2D m_texture;
	}
}
