using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Game
{
	public class BlueberryBushBlock : ShittyTexturesFlat
	{
		public const int Index = 432;

		public BlueberryBushBlock()
			: base("Textures/alimentos/arbusto de arandanos")
		{
		}

		public static bool GetIsSmall(int data)
		{
			return (data & 1) != 0;
		}

		public static int SetIsSmall(int data, bool isSmall)
		{
			if (isSmall)
				return data | 1;
			else
				return data & ~1;
		}

		/// <summary>
		/// Verifica si el bloque es válido como soporte para el arbusto
		/// </summary>
		public static bool IsValidSupportBlock(int belowValue, int plantValue)
		{
			int belowContents = Terrain.ExtractContents(belowValue);
			if (belowContents == 0) return false; // Aire no es válido

			Block belowBlock = BlocksManager.Blocks[belowContents];

			// Bloques suitability para plantas O tierra rastrillada (168)
			return belowBlock.IsSuitableForPlants(belowValue, plantValue) || belowContents == 168;
		}

		public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> dropValues, out bool showDebris)
		{
			int data = Terrain.ExtractData(oldValue);

			if (GetIsSmall(data))
			{
				showDebris = (this.DestructionDebrisScale > 0f);
				return;
			}

			base.GetDropValues(subsystemTerrain, oldValue, newValue, toolLevel, dropValues, out showDebris);
		}

		public override bool IsFaceNonAttachable(SubsystemTerrain subsystemTerrain, int face, int value, int attachBlockValue)
		{
			if (face == 4)
			{
				int contents = Terrain.ExtractContents(attachBlockValue);
				Block block = BlocksManager.Blocks[contents];
				if (block.IsSuitableForPlants(value, Terrain.MakeBlockValue(contents, 0, Terrain.ExtractData(attachBlockValue))))
					return false;
				// También permitir si es tierra rastrillada
				if (contents == 168)
					return false;
			}
			return true;
		}

		public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
		{
			BlockPlacementData placementData = base.GetPlacementValue(subsystemTerrain, componentMiner, value, raycastResult);

			Point3 targetCell = placementData.CellFace.Point + CellFace.FaceToPoint3(placementData.CellFace.Face);
			int belowY = targetCell.Y - 1;
			int belowValue = subsystemTerrain.Terrain.GetCellValue(targetCell.X, belowY, targetCell.Z);

			// Usar el método estático para verificación consistente
			if (!IsValidSupportBlock(belowValue, value))
			{
				placementData.Value = 0;
			}

			return placementData;
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			if (this.m_texture != null)
			{
				TerrainGeometry customGeometry = geometry.GetGeometry(this.m_texture);
				int data = Terrain.ExtractData(value);

				if (GetIsSmall(data))
				{
					GenerateSmallCrossVertices(generator, value, x, y, z, customGeometry.SubsetAlphaTest);
				}
				else
				{
					base.GenerateTerrainVertices(generator, geometry, value, x, y, z);
				}
			}
		}

		private void GenerateSmallCrossVertices(BlockGeometryGenerator generator, int value, int x, int y, int z, TerrainGeometrySubset subset)
		{
			int light = Terrain.ExtractLight(value);
			float intensity = LightingManager.LightIntensityByLightValue[light];
			Color color = Color.MultiplyColorOnlyNotSaturated(Color.White, intensity);

			int textureSlot = this.GetFaceTextureSlot(4, value);
			int textureSlotCount = this.GetTextureSlotCount(value);

			float u0 = (float)(textureSlot % textureSlotCount) / textureSlotCount;
			float v0 = (float)(textureSlot / textureSlotCount) / textureSlotCount;
			float u1 = u0 + 1.0f / textureSlotCount;
			float v1 = v0 + 1.0f / textureSlotCount;

			float halfSize = 0.25f;
			float height = 0.6f;

			Vector3[] quadX = new Vector3[4];
			quadX[0] = new Vector3(x + 0.5f - halfSize, y, z + 0.5f);
			quadX[1] = new Vector3(x + 0.5f + halfSize, y, z + 0.5f);
			quadX[2] = new Vector3(x + 0.5f + halfSize, y + height, z + 0.5f);
			quadX[3] = new Vector3(x + 0.5f - halfSize, y + height, z + 0.5f);

			Vector3[] quadZ = new Vector3[4];
			quadZ[0] = new Vector3(x + 0.5f, y, z + 0.5f - halfSize);
			quadZ[1] = new Vector3(x + 0.5f, y, z + 0.5f + halfSize);
			quadZ[2] = new Vector3(x + 0.5f, y + height, z + 0.5f + halfSize);
			quadZ[3] = new Vector3(x + 0.5f, y + height, z + 0.5f - halfSize);

			void AddQuad(Vector3[] quad)
			{
				int start = subset.Vertices.Count;
				subset.Vertices.Count += 4;
				var vertices = subset.Vertices.Array;

				BlockGeometryGenerator.SetupVertex(quad[0].X, quad[0].Y, quad[0].Z, color, u0, v1, ref vertices[start]);
				BlockGeometryGenerator.SetupVertex(quad[1].X, quad[1].Y, quad[1].Z, color, u1, v1, ref vertices[start + 1]);
				BlockGeometryGenerator.SetupVertex(quad[2].X, quad[2].Y, quad[2].Z, color, u1, v0, ref vertices[start + 2]);
				BlockGeometryGenerator.SetupVertex(quad[3].X, quad[3].Y, quad[3].Z, color, u0, v0, ref vertices[start + 3]);

				int idxStart = subset.Indices.Count;
				subset.Indices.Count += 12;
				var indices = subset.Indices.Array;

				indices[idxStart] = start;
				indices[idxStart + 1] = start + 1;
				indices[idxStart + 2] = start + 2;
				indices[idxStart + 3] = start + 2;
				indices[idxStart + 4] = start + 3;
				indices[idxStart + 5] = start;

				indices[idxStart + 6] = start;
				indices[idxStart + 7] = start + 3;
				indices[idxStart + 8] = start + 2;
				indices[idxStart + 9] = start + 2;
				indices[idxStart + 10] = start + 1;
				indices[idxStart + 11] = start;
			}

			AddQuad(quadX);
			AddQuad(quadZ);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int data = Terrain.ExtractData(value);
			bool isSmall = GetIsSmall(data);

			if (isSmall)
			{
				float smallSize = 0.6f;
				Color smallColor = new Color(180, 220, 180);
				base.DrawBlock(primitivesRenderer, value, smallColor, size * smallSize, ref matrix, environmentData);
			}
			else
			{
				base.DrawBlock(primitivesRenderer, value, Color.White, size, ref matrix, environmentData);
			}
		}

		public override int GetShadowStrength(int value)
		{
			int data = Terrain.ExtractData(value);
			if (GetIsSmall(data))
				return this.DefaultShadowStrength / 2;
			return this.DefaultShadowStrength;
		}

		public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value)
		{
			int data = Terrain.ExtractData(value);
			if (GetIsSmall(data))
			{
				return new BoundingBox[]
				{
					new BoundingBox(new Vector3(0.3f, 0f, 0.3f), new Vector3(0.7f, 0.6f, 0.7f))
				};
			}
			return base.GetCustomInteractionBoxes(terrain, value);
		}
	}
}
