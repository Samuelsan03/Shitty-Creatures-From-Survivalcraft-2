using System;
using Engine;
using Engine.Graphics;
using Game;

namespace Game
{
	/// <summary>
	/// Clase base abstracta para bloques de madera que utilizan la textura personalizada "Textures/ShittyCreaturesTextures".
	/// </summary>
	public abstract class ShittyWoodBlock : WoodBlock
	{
		protected Texture2D m_texture;

		/// <summary>
		/// Inicializa el bloque cargando la textura personalizada.
		/// </summary>
		public override void Initialize()
		{
			m_texture = ContentManager.Get<Texture2D>("Textures/ShittyCreaturesTextures");
			base.Initialize();
		}

		public ShittyWoodBlock(int cutTextureSlot, int sideTextureSlot) : base(cutTextureSlot, sideTextureSlot)
		{
			m_cutTextureSlot = cutTextureSlot;
			m_sideTextureSlot = sideTextureSlot;
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
			int cutFace = WoodBlock.GetCutFace(Terrain.ExtractData(value));
			if (cutFace == 0)
			{
				generator.GenerateCubeVertices(this, value, x, y, z, 1, 0, 0, Color.White, geometry.GetGeometry(m_texture).OpaqueSubsetsByFace);
				return;
			}
			if (cutFace == 4)
			{
				generator.GenerateCubeVertices(this, value, x, y, z, Color.White, geometry.GetGeometry(m_texture).OpaqueSubsetsByFace);
				return;
			}
			generator.GenerateCubeVertices(this, value, x, y, z, 0, 1, 1, Color.White, geometry.GetGeometry(m_texture).OpaqueSubsetsByFace);
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			BlocksManager.DrawCubeBlock(primitivesRenderer, value, new Vector3(size), 1f, ref matrix, color, color, environmentData, m_texture);
		}

		public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain subsystemTerrain, Vector3 position, int value, float strength)
		{
			return new BlockDebrisParticleSystem(subsystemTerrain, position, strength, DestructionDebrisScale, Color.White, GetFaceTextureSlot(4, value), m_texture);
		}
	}
}