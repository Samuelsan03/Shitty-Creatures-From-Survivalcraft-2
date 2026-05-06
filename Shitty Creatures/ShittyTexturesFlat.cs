using System;
using Engine;
using Engine.Graphics;

namespace Game
{
	public abstract class ShittyTexturesFlat : Block
	{
		public Texture2D m_texture;
		public string texture;

		public ShittyTexturesFlat(string textureName)
		{
			this.texture = textureName;
		}

		public override void Initialize()
		{
			base.Initialize();
			if (!string.IsNullOrEmpty(this.texture))
			{
				this.m_texture = ContentManager.Get<Texture2D>(this.texture, null);
			}
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			if (this.m_texture != null)
			{
				TerrainGeometry customGeometry = geometry.GetGeometry(this.m_texture);
				GenerateCrossVertices(generator, value, x, y, z, customGeometry.SubsetAlphaTest);
			}
		}

		private void GenerateCrossVertices(BlockGeometryGenerator generator, int value, int x, int y, int z, TerrainGeometrySubset subset)
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

			float halfSize = 0.45f;

			Vector3[] quadX = new Vector3[4];
			quadX[0] = new Vector3(x + 0.5f - halfSize, y, z + 0.5f);
			quadX[1] = new Vector3(x + 0.5f + halfSize, y, z + 0.5f);
			quadX[2] = new Vector3(x + 0.5f + halfSize, y + 1f, z + 0.5f);
			quadX[3] = new Vector3(x + 0.5f - halfSize, y + 1f, z + 0.5f);

			Vector3[] quadZ = new Vector3[4];
			quadZ[0] = new Vector3(x + 0.5f, y, z + 0.5f - halfSize);
			quadZ[1] = new Vector3(x + 0.5f, y, z + 0.5f + halfSize);
			quadZ[2] = new Vector3(x + 0.5f, y + 1f, z + 0.5f + halfSize);
			quadZ[3] = new Vector3(x + 0.5f, y + 1f, z + 0.5f - halfSize);

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
			if (this.m_texture != null)
			{
				BlocksManager.DrawFlatBlock(primitivesRenderer, value, size * 1f, ref matrix, this.m_texture, Color.White, true, environmentData);
			}
		}

		public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
		{
			BlockPlacementData placementData = new BlockPlacementData();
			placementData.Value = value;
			placementData.CellFace = raycastResult.CellFace;
			return placementData;
		}

		// Caja de colisión física: vacía (no bloquea al jugador)
		public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
		{
			return new BoundingBox[0];
		}

		// Caja de interacción: pequeña, permite apuntar y romper el bloque
		public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value)
		{
			return new BoundingBox[]
			{
				new BoundingBox(new Vector3(0.25f, 0f, 0.25f), new Vector3(0.75f, 1f, 0.75f))
			};
		}

		public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
		{
			return true;
		}

		public override bool ShouldGenerateFace(SubsystemTerrain subsystemTerrain, int face, int value, int neighborValue, int x, int y, int z)
		{
			return false;
		}

		public override int GetFaceTextureSlot(int face, int value)
		{
			return 0;
		}

		// Se elimina la anulación de GetDropValues para que se usen los datos del CSV
		// (DefaultDropContent, DefaultDropCount, etc.)

		public override int GetTextureSlotCount(int value)
		{
			return 1;
		}

		public override bool IsCollidable_(int value)
		{
			return false;
		}

		public override bool IsTransparent_(int value)
		{
			return true;
		}

		public override float? Raycast(Ray3 ray, SubsystemTerrain subsystemTerrain, int value, bool useInteractionBoxes, out int collisionBoxIndex, out BoundingBox collisionBox)
		{
			collisionBoxIndex = 0;
			collisionBox = default(BoundingBox);

			BoundingBox[] array = useInteractionBoxes ? this.GetCustomInteractionBoxes(subsystemTerrain, value) : this.GetCustomCollisionBoxes(subsystemTerrain, value);
			if (array.Length == 0)
			{
				return null;
			}

			float? result = null;
			for (int i = 0; i < array.Length; i++)
			{
				float? num = ray.Intersection(array[i]);
				if (num != null && (result == null || num.Value < result.Value))
				{
					collisionBoxIndex = i;
					result = num;
				}
			}

			if (result != null)
			{
				collisionBox = array[collisionBoxIndex];
			}
			return result;
		}
	}
}
